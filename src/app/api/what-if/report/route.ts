import { ApiError, FinishReason, GoogleGenAI, ThinkingLevel } from "@google/genai";
import { regionClimate } from "@/lib/earth/climate";
import { GEMINI_MODEL, geminiKey } from "@/lib/whatif/ai";
import { sanitizeControls, sanitizeYears, simulate } from "@/lib/whatif/model";
import { DISCOVERY_INFO, buildReport, describeControls } from "@/lib/whatif/report";

/**
 * POST /api/what-if/report  { controls, years }
 *
 * Writes a short AI briefing for a what-if run with Google Gemini. The client sends
 * only slider values; the server clamps them, re-runs the model and gives Gemini the
 * computed facts, so the prompt never contains user text.
 *
 * Needs GEMINI_API_KEY (or GOOGLE_API_KEY) on the server. GEMINI_MODEL overrides the model.
 */

const MODEL = GEMINI_MODEL;

const SYSTEM = `You write short briefings for Trend Detective, an educational Earth-science game for high-school and college students.
The player changed the climate of a real region in a simple physics model and wants to know what it means.
Write 120-180 words of plain text in two or three short paragraphs, friendly and clear, for a student.
Use only the numbers and facts in FACTS. Do not add numbers, places, events or claims that are not there.
Say plainly that this is a hypothetical model run, not a forecast or an observation, and mention the single most important caveat.
Explain the chain of cause and effect the numbers show, for example how warmer or drier air raises evaporative demand and empties the soil.
No headings, bullet points or markdown.`;

const CACHE_LIMIT = 200;
const cache = new Map<string, { text: string; model: string }>();

const WINDOW_MS = 60_000;
const MAX_PER_WINDOW = 6;
const recent = new Map<string, number[]>();

function rateLimited(ip: string): boolean {
  const now = Date.now();
  const times = (recent.get(ip) ?? []).filter((t) => now - t < WINDOW_MS);
  times.push(now);
  recent.set(ip, times);
  if (recent.size > 5000) recent.clear();
  return times.length > MAX_PER_WINDOW;
}

function problem(status: number, error: string) {
  return Response.json({ error }, { status });
}

export async function POST(request: Request) {
  const apiKey = geminiKey();
  if (!apiKey) return problem(503, "The AI briefing is not switched on for this site.");

  const ip = request.headers.get("x-forwarded-for")?.split(",")[0]?.trim() || "unknown";
  if (rateLimited(ip)) return problem(429, "Too many briefings at once. Wait a minute and try again.");

  const body = (await request.json().catch(() => null)) as { controls?: Record<string, unknown>; years?: unknown } | null;
  const controls = sanitizeControls(body?.controls ?? {});
  const years = sanitizeYears(body?.years);
  const key = JSON.stringify([controls, years]);
  const hit = cache.get(key);
  if (hit) return Response.json(hit);

  const { climate } = regionClimate();
  const result = simulate(climate, controls, years);
  const report = buildReport(result);
  const facts = {
    region: "Phoenix and central Arizona (a 2 x 2 degree box), starting from its real 1991-2020 monthly climate",
    change: describeControls(result),
    yearsSimulated: years,
    headline: report.headline,
    findings: report.findings,
    thresholdsCrossed: result.discoveries.map((d) => `${DISCOVERY_INFO[d.id].title} (${d.severity})`),
    caveats: report.caveats,
  };

  const ai = new GoogleGenAI({ apiKey });
  try {
    const response = await ai.models.generateContent({
      model: MODEL,
      contents: `FACTS:\n${JSON.stringify(facts, null, 2)}\n\nWrite the briefing.`,
      config: {
        systemInstruction: SYSTEM,
        // A short, fact-bound write-up does not need deep reasoning.
        thinkingConfig: { thinkingLevel: ThinkingLevel.LOW },
        // Generous, because thinking tokens count toward the output on these models.
        maxOutputTokens: 4096,
      },
    });

    const finish = response.candidates?.[0]?.finishReason;
    if (response.promptFeedback?.blockReason || finish === FinishReason.SAFETY) {
      return problem(502, "The AI declined to write this briefing. The report above still stands.");
    }
    const text = response.text?.trim();
    if (!text) return problem(502, "The AI returned an empty briefing. Try again.");

    const out = { text, model: response.modelVersion ?? MODEL };
    cache.set(key, out);
    if (cache.size > CACHE_LIMIT) cache.delete(cache.keys().next().value!);
    return Response.json(out);
  } catch (error) {
    if (error instanceof ApiError) {
      if (error.status === 429) return problem(429, "The AI service is busy or over its quota. Try again later.");
      if (error.status === 400 || error.status === 401 || error.status === 403 || error.status === 404) {
        return problem(503, "The AI briefing is misconfigured on the server (key rejected or model unavailable).");
      }
      return problem(502, `The AI service returned an error (${error.status}).`);
    }
    return problem(502, "Could not reach the AI service.");
  }
}
