import { ApiError, FinishReason, GoogleGenAI, ThinkingLevel, type Content } from "@google/genai";
import { assistantInstructions } from "@/lib/chat/knowledge";
import { GEMINI_MODEL, geminiKey } from "@/lib/whatif/ai";

/**
 * POST /api/chat  { messages: [{ role: "user" | "assistant", text }] }
 *
 * The app assistant. Streams plain text back from Google Gemini. The conversation is
 * trimmed and size-limited on the server, and each IP address is rate-limited.
 */

const MAX_TURNS = 12;
const MAX_CHARS_PER_MESSAGE = 1500;
const MAX_TOTAL_CHARS = 12_000;
const TIMEOUT_MS = 45_000;

const WINDOW_MS = 60_000;
const MAX_PER_WINDOW = 10;
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

interface IncomingMessage {
  role?: unknown;
  text?: unknown;
}

/** Validate and trim the conversation into Gemini's format. Returns null if unusable. */
function toContents(raw: unknown): Content[] | null {
  if (!Array.isArray(raw)) return null;
  const cleaned = (raw as IncomingMessage[])
    .filter((m) => (m.role === "user" || m.role === "assistant") && typeof m.text === "string" && m.text.trim())
    .slice(-MAX_TURNS)
    .map((m) => ({ role: m.role as "user" | "assistant", text: (m.text as string).trim().slice(0, MAX_CHARS_PER_MESSAGE) }));
  // Drop the oldest turns until the whole conversation fits.
  while (cleaned.reduce((n, m) => n + m.text.length, 0) > MAX_TOTAL_CHARS) cleaned.shift();
  // Gemini expects the conversation to start with the user and end with the user.
  while (cleaned.length && cleaned[0].role !== "user") cleaned.shift();
  if (!cleaned.length || cleaned[cleaned.length - 1].role !== "user") return null;
  return cleaned.map((m) => ({ role: m.role === "assistant" ? "model" : "user", parts: [{ text: m.text }] }));
}

export async function POST(request: Request) {
  const apiKey = geminiKey();
  if (!apiKey) return problem(503, "The assistant is not switched on for this site yet.");

  const ip = request.headers.get("x-forwarded-for")?.split(",")[0]?.trim() || "unknown";
  if (rateLimited(ip)) return problem(429, "That's a lot of questions at once. Wait a minute and try again.");

  const body = (await request.json().catch(() => null)) as { messages?: unknown } | null;
  const contents = toContents(body?.messages);
  if (!contents) return problem(400, "Send at least one question.");

  const ai = new GoogleGenAI({ apiKey });
  const timeout = AbortSignal.timeout(TIMEOUT_MS);

  let stream: AsyncGenerator<{ text?: string; candidates?: { finishReason?: FinishReason }[]; promptFeedback?: { blockReason?: unknown } }>;
  try {
    stream = await ai.models.generateContentStream({
      model: GEMINI_MODEL,
      contents,
      config: {
        abortSignal: timeout,
        httpOptions: { timeout: TIMEOUT_MS },
        systemInstruction: assistantInstructions(),
        thinkingConfig: { thinkingLevel: ThinkingLevel.LOW },
        maxOutputTokens: 2048,
      },
    });
  } catch (error) {
    if (timeout.aborted) return problem(504, "The assistant took too long to answer. Try again in a moment.");
    if (error instanceof ApiError) {
      if (error.status === 429) return problem(429, "The assistant is busy or over its quota. Try again later.");
      if ([400, 401, 403, 404].includes(error.status)) {
        return problem(503, "The assistant is misconfigured on the server (key rejected or model unavailable).");
      }
      return problem(502, `The AI service returned an error (${error.status}).`);
    }
    return problem(502, "Could not reach the AI service.");
  }

  const encoder = new TextEncoder();
  const out = new ReadableStream<Uint8Array>({
    async start(controller) {
      let wrote = false;
      try {
        for await (const chunk of stream) {
          const blocked =
            chunk.promptFeedback?.blockReason || chunk.candidates?.[0]?.finishReason === FinishReason.SAFETY;
          if (blocked && !wrote) {
            controller.enqueue(encoder.encode("Sorry, I can't help with that one. Ask me about the app, its data or its science."));
            wrote = true;
            break;
          }
          if (chunk.text) {
            controller.enqueue(encoder.encode(chunk.text));
            wrote = true;
          }
        }
        if (!wrote) controller.enqueue(encoder.encode("I didn't get an answer back. Please try asking again."));
      } catch {
        controller.enqueue(
          encoder.encode(wrote ? "\n\n(The answer was cut off. Try again.)" : "Sorry, something went wrong. Please try again."),
        );
      } finally {
        controller.close();
      }
    },
  });

  return new Response(out, {
    headers: { "content-type": "text/plain; charset=utf-8", "cache-control": "no-store" },
  });
}
