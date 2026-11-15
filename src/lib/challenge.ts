/**
 * The numbers behind the step-by-step walkthrough (/challenge).
 *
 * It reads one series three ways, matching the challenge's own list:
 *  - rate of change: least-squares slope and the robust Theil-Sen slope
 *  - a t-test on the least-squares slope, plus a version widened for autocorrelation
 *  - the Mann-Kendall rank test
 *  - 95% confidence intervals for the slope, and a confidence band for the fitted line
 *
 * The verdict is significant only when the autocorrelation-adjusted t-test and the
 * Mann-Kendall test both give p < 0.05; one without the other is "mixed evidence".
 */
import { lnGamma, normalCritical, studentTCritical } from "./stats/distributions";
import {
  MIN_POINTS,
  mannKendall,
  ols,
  senSlope,
  type MannKendallResult,
  type OlsResult,
  type Point,
  type SenResult,
} from "./stats/trend";

export const ALPHA = 0.05;

export type VerdictKind = "significant" | "mixed" | "none";
export type Direction = "rising" | "falling" | "flat";

export interface PairCounts {
  /** Pairs where the later value is higher */
  up: number;
  /** Pairs where the later value is lower */
  down: number;
  ties: number;
  total: number;
}

export interface BandPoint {
  t: number;
  fit: number;
  lo: number;
  hi: number;
}

export interface ChallengeStats {
  n: number;
  t0: number;
  t1: number;
  spanYears: number;
  mean: number;
  ols: OlsResult;
  mk: MannKendallResult;
  sen: SenResult;
  pairs: PairCounts;
  /** Two-sided 5% critical values */
  tCrit: number;
  tCritAdjusted: number;
  zCrit: number;
  /** 95% confidence band for the least-squares line */
  band: BandPoint[];
  /** Theil-Sen slope times the span: the fitted change over the whole record */
  change: number;
  /** Typical scatter of the points around the least-squares line */
  residualSd: number;
  direction: Direction;
  tTestSignificant: boolean;
  mkSignificant: boolean;
  verdict: VerdictKind;
}

/** Run every test the walkthrough shows. Returns null below the minimum record length. */
export function challengeStats(points: Point[]): ChallengeStats | null {
  if (points.length < MIN_POINTS) return null;
  // A series that never changes (no snow in a desert) has nothing to test.
  if (points.every((p) => p.v === points[0].v)) return null;
  const sorted = [...points].sort((a, b) => a.t - b.t);
  const fit = ols(sorted, ALPHA);
  const mk = mannKendall(sorted.map((p) => p.v));
  const sen = senSlope(sorted, mk.varS, ALPHA);
  const n = sorted.length;
  const t0 = sorted[0].t;
  const t1 = sorted[n - 1].t;
  const spanYears = t1 - t0;
  const meanT = sorted.reduce((acc, p) => acc + p.t, 0) / n;
  const sxx = sorted.reduce((acc, p) => acc + (p.t - meanT) ** 2, 0);
  // Residual standard deviation, recovered from the slope's standard error.
  const s = fit.se * Math.sqrt(sxx);
  const tCrit = studentTCritical(ALPHA, fit.df);

  const band: BandPoint[] = Array.from({ length: 41 }, (_, i) => {
    const t = t0 + (spanYears * i) / 40;
    const y = fit.intercept + fit.slope * t;
    const half = tCrit * s * Math.sqrt(1 / n + (t - meanT) ** 2 / sxx);
    return { t, fit: y, lo: y - half, hi: y + half };
  });

  const tTestSignificant = fit.pAdjusted < ALPHA;
  const mkSignificant = mk.p < ALPHA;
  const direction: Direction = sen.slope > 0 ? "rising" : sen.slope < 0 ? "falling" : "flat";
  const sameSign = Math.sign(fit.slope) === Math.sign(mk.s);

  return {
    n,
    t0,
    t1,
    spanYears,
    mean: sorted.reduce((acc, p) => acc + p.v, 0) / n,
    ols: fit,
    mk,
    sen,
    pairs: countPairs(sorted.map((p) => p.v)),
    tCrit,
    tCritAdjusted: studentTCritical(ALPHA, fit.dfAdjusted),
    zCrit: normalCritical(ALPHA),
    band,
    change: sen.slope * spanYears,
    residualSd: s,
    direction,
    tTestSignificant,
    mkSignificant,
    verdict: tTestSignificant && mkSignificant && sameSign ? "significant"
      : tTestSignificant || mkSignificant ? "mixed" : "none",
  };
}

/** The pairwise comparisons the Mann-Kendall S statistic is built from. S = up - down. */
export function countPairs(values: number[]): PairCounts {
  let up = 0;
  let down = 0;
  let ties = 0;
  for (let i = 0; i < values.length - 1; i++) {
    for (let j = i + 1; j < values.length; j++) {
      if (values[j] > values[i]) up++;
      else if (values[j] < values[i]) down++;
      else ties++;
    }
  }
  return { up, down, ties, total: up + down + ties };
}

/** Student's t probability density. */
export function tDensity(x: number, df: number): number {
  const lnC = lnGamma((df + 1) / 2) - lnGamma(df / 2) - 0.5 * Math.log(df * Math.PI);
  return Math.exp(lnC - ((df + 1) / 2) * Math.log(1 + (x * x) / df));
}

/** Standard normal probability density. */
export function normalDensity(x: number): number {
  return Math.exp(-0.5 * x * x) / Math.sqrt(2 * Math.PI);
}

/** Calendar-month averages of a monthly record, and how far apart the warmest and coldest are. */
export function seasonalCycle(monthly: (number | null)[]): { normals: (number | null)[]; range: number } {
  const normals = Array.from({ length: 12 }, (_, m) => {
    let sum = 0;
    let count = 0;
    for (let i = m; i < monthly.length; i += 12) {
      const v = monthly[i];
      if (v !== null && Number.isFinite(v)) {
        sum += v;
        count++;
      }
    }
    return count > 0 ? sum / count : null;
  });
  const known = normals.filter((v): v is number => v !== null);
  return { normals, range: known.length ? Math.max(...known) - Math.min(...known) : NaN };
}

/* ---------- Plain-language reading ---------- */

export interface EvidenceLevel {
  level: 0 | 1 | 2 | 3 | 4;
  label: string;
  /** One line for someone who has never met a p-value */
  meaning: string;
}

export const EVIDENCE_LEVELS: Omit<EvidenceLevel, "level">[] = [
  { label: "No evidence", meaning: "Normal ups and downs could easily produce this." },
  { label: "A weak hint", meaning: "Something may be happening, but luck is still a good explanation." },
  { label: "Clear evidence", meaning: "Luck alone would rarely produce this (less than 1 time in 20)." },
  { label: "Strong evidence", meaning: "Luck alone would very rarely produce this (less than 1 time in 100)." },
  { label: "Very strong evidence", meaning: "Luck alone would almost never produce this (less than 1 time in 1,000)." },
];

/**
 * How much the data back a trend, on a five-step scale a newcomer can read.
 * It is set by the weaker of the two tests, so "clear evidence" or better
 * always means both tests passed, matching the verdict.
 */
export function evidenceLevel(stats: ChallengeStats): EvidenceLevel {
  const p = Math.max(stats.ols.pAdjusted, stats.mk.p);
  let level: EvidenceLevel["level"] = p < 0.001 ? 4 : p < 0.01 ? 3 : p < ALPHA ? 2 : p < 0.2 ? 1 : 0;
  if (stats.verdict !== "significant") level = Math.min(level, 1) as EvidenceLevel["level"];
  return { level, ...EVIDENCE_LEVELS[level] };
}

/** A p-value as everyday odds: "less than 1 time in 1,000", "about 1 time in 40". */
export function chanceOdds(p: number): string {
  if (!Number.isFinite(p)) return "an unknown share of the time";
  if (p < 0.001) return "less than 1 time in 1,000";
  if (p >= 0.5) return "about half the time or more";
  const n = 1 / p;
  const rounded = n < 10 ? Math.round(n) : n < 100 ? Math.round(n / 5) * 5 : Math.round(n / 50) * 50;
  return `about 1 time in ${rounded.toLocaleString("en-US")}`;
}

/* ---------- Gaps in a monthly record ---------- */

/**
 * Fill short gaps so a year with a month or two missing is not thrown away.
 * A missing month gets that calendar month's normal, shifted by how far the
 * rest of that year ran above or below normal. Years missing more than
 * `maxPerYear` months stay incomplete, and are left out of yearly averages.
 */
export function fillGaps(monthly: (number | null)[], maxPerYear = 3): {
  monthly: (number | null)[];
  filled: number;
  missing: number;
} {
  const { normals } = seasonalCycle(monthly);
  const out = [...monthly];
  let filled = 0;
  const missing = monthly.filter((v) => v === null).length;
  for (let start = 0; start < monthly.length; start += 12) {
    const year = monthly.slice(start, start + 12);
    const gaps = year.flatMap((v, m) => (v === null ? [m] : []));
    if (year.length < 12 || gaps.length === 0 || gaps.length > maxPerYear) continue;
    if (gaps.some((m) => normals[m] === null)) continue;
    const anomalies = year.flatMap((v, m) => (v === null || normals[m] === null ? [] : [v - normals[m]!]));
    const shift = anomalies.reduce((a, b) => a + b, 0) / anomalies.length;
    for (const m of gaps) {
      out[start + m] = normals[m]! + shift;
      filled++;
    }
  }
  return { monthly: out, filled, missing };
}

/* ---------- Two regions ---------- */

export type ComparisonKind = "opposite" | "same-different" | "same-similar" | "one" | "neither";

export interface RegionComparison {
  kind: ComparisonKind;
  /** Rate of A minus rate of B, per year (Theil-Sen) */
  gap: number;
  /** The year-by-year difference A - B, tested like any other series */
  difference: ChallengeStats | null;
  sharedPoints: number;
}

/** Compare the same variable in two places, read the same way. */
export function compareRegions(
  a: ChallengeStats,
  b: ChallengeStats,
  pointsA: Point[],
  pointsB: Point[],
): RegionComparison {
  const bByTime = new Map(pointsB.map((p) => [p.t, p.v]));
  const paired = pointsA.filter((p) => bByTime.has(p.t)).map((p) => ({ t: p.t, v: p.v - bByTime.get(p.t)! }));
  const difference = challengeStats(paired);
  const sigA = a.verdict === "significant";
  const sigB = b.verdict === "significant";
  const kind: ComparisonKind =
    sigA && sigB
      ? a.direction !== b.direction
        ? "opposite"
        : difference?.verdict === "significant"
          ? "same-different"
          : "same-similar"
      : sigA || sigB
        ? "one"
        : "neither";
  return { kind, gap: a.sen.slope - b.sen.slope, difference, sharedPoints: paired.length };
}
