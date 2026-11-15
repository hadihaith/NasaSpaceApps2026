import { analyzeTrend, comparePairedSeries, type Point, type TrendResult } from "../../../../lib/stats/trend";

const MAX_POINTS = 1200;
const MAX_BODY = 180_000;

type Mode = "annual" | "month" | "anomaly";
type DatedPoint = Point & { date: string };

function points(input: unknown, mode: Mode, month: number): DatedPoint[] | null {
  if (!Array.isArray(input) || input.length > MAX_POINTS) return null;
  const result: DatedPoint[] = [];
  for (const point of input) {
    if (typeof point?.date !== "string" || !Number.isFinite(point.value) || Math.abs(point.value) > 1e9)
      return null;
    const match = /^(\d{4})(?:-(0[1-9]|1[0-2]))?$/.exec(point.date);
    if (!match) return null;
    const year = Number(match[1]);
    const calendarMonth = Number(match[2]);
    if (year < 1900 || year > 2200 || (mode === "annual") !== !match[2] ||
        (mode === "month" && calendarMonth !== month)) return null;
    const t = mode === "anomaly" ? year + (calendarMonth - .5) / 12 : year;
    if (result.length && t <= result.at(-1)!.t) return null;
    result.push({ t, v: point.value, date: point.date });
  }
  return result;
}

function exploratory(trend: TrendResult | null) {
  if (!trend) return null;
  const { ols, mannKendall: rank, sen, alpha, magnitude } = trend;
  if (ols.se === 0 && !ols.slope && !rank.s) {
    return { ratePerDecade: 0, magnitude: 0, outcome: "constant" };
  }
  const olsPass = ols.pAdjusted < alpha;
  const mkPass = rank.p < alpha;
  const adjustedT = ols.slope / ols.seAdjusted;
  return {
    ratePerDecade: sen.slope * 10,
    magnitude,
    outcome: olsPass && mkPass && Math.sign(ols.slope) === Math.sign(rank.s)
      ? "both" : olsPass || mkPass ? "mixed" : "neither",
    alpha,
    adjustedP: ols.pAdjusted,
    adjustedT: Number.isFinite(adjustedT) ? adjustedT : null,
    hasAdjustedT: Number.isFinite(adjustedT),
    mkP: rank.p,
    mkS: rank.s,
    mkZ: rank.z,
    lowerPerDecade: ols.ciAdjusted[0] * 10,
    upperPerDecade: ols.ciAdjusted[1] * 10,
  };
}

export async function POST(request: Request) {
  const bad = () => Response.json({ error: "Supply two ordered, dated evidence views in the same time mode." }, { status: 400 });
  if (Number(request.headers.get("content-length")) > MAX_BODY) return bad();
  const text = await request.text();
  if (text.length > MAX_BODY) return bad();
  let body;
  try { body = JSON.parse(text); }
  catch { return bad(); }
  const { mode, month = 1, leftId, rightId } = body ?? {};
  if (!(mode === "annual" || mode === "month" || mode === "anomaly") ||
      !Number.isInteger(month) || month < 1 || month > 12 ||
      typeof leftId !== "string" || typeof rightId !== "string" ||
      !leftId || !rightId || leftId === rightId || leftId.length > 256 || rightId.length > 256)
    return bad();

  const left = points(body.left, mode, month);
  const right = points(body.right, mode, month);
  if (!left || !right) return bad();
  // The same Theil–Sen, adjusted OLS and Mann–Kendall implementations used by /challenge.
  // Match the evidence views as supplied, with no gap filling.
  const paired = comparePairedSeries(left, right);
  const common = new Set(paired.points.map(p => p.t));
  const matchedLeft = left.filter(p => common.has(p.t));
  const matchedRight = right.filter(p => common.has(p.t));
  return Response.json({
    schemaVersion: 1, leftId, rightId, mode, month, n: paired.n,
    firstDate: matchedLeft[0]?.date ?? null,
    lastDate: matchedLeft.at(-1)?.date ?? null,
    left: exploratory(analyzeTrend(matchedLeft)),
    right: exploratory(analyzeTrend(matchedRight)),
    difference: exploratory(paired.trend),
  });
}
