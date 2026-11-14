/**
 * Linear trend detection for a time series.
 *
 * Three complementary methods, as the challenge suggests:
 *  - Mann-Kendall test: is there a consistent direction? (rank based, no normality assumption)
 *  - Theil-Sen slope: how fast, robust to outliers, with a confidence interval
 *  - Least-squares regression: slope, t-test and confidence interval, plus a
 *    correction for autocorrelation ("memory") in the residuals
 */
import {
  normalCritical,
  normalTwoSidedP,
  studentTCritical,
  studentTTwoSidedP,
} from "./distributions";

export interface Point {
  /** Time in decimal years, e.g. 1995.5 */
  t: number;
  v: number;
}

export interface OlsResult {
  slope: number;
  intercept: number;
  /** Standard error of the slope */
  se: number;
  t: number;
  df: number;
  p: number;
  ci: [number, number];
  r2: number;
  /** Lag-1 autocorrelation of the residuals */
  r1: number;
  /** Effective number of independent points, n(1 - r1)/(1 + r1) */
  nEff: number;
  /** Standard error and degrees of freedom after allowing for autocorrelation */
  seAdjusted: number;
  dfAdjusted: number;
  /** p-value after widening the standard error for autocorrelation */
  pAdjusted: number;
  ciAdjusted: [number, number];
}

export interface MannKendallResult {
  s: number;
  varS: number;
  z: number;
  p: number;
  /** Kendall's tau, from -1 (always falling) to +1 (always rising) */
  tau: number;
}

export interface SenResult {
  slope: number;
  intercept: number;
  ci: [number, number];
}

export type Direction = "rising" | "falling" | "flat";
export type Strength = "significant" | "borderline" | "not-significant";

export interface TrendResult {
  n: number;
  alpha: number;
  spanYears: number;
  mean: number;
  direction: Direction;
  strength: Strength;
  ols: OlsResult;
  mannKendall: MannKendallResult;
  sen: SenResult;
}

/** Fewest points for which the tests are worth reporting. */
export const MIN_POINTS = 10;

export function ols(points: Point[], alpha = 0.05): OlsResult {
  const n = points.length;
  let sumT = 0;
  let sumV = 0;
  for (const p of points) {
    sumT += p.t;
    sumV += p.v;
  }
  const meanT = sumT / n;
  const meanV = sumV / n;
  let sxx = 0;
  let sxy = 0;
  let syy = 0;
  for (const p of points) {
    const dt = p.t - meanT;
    const dv = p.v - meanV;
    sxx += dt * dt;
    sxy += dt * dv;
    syy += dv * dv;
  }
  const slope = sxy / sxx;
  const intercept = meanV - slope * meanT;

  const residuals = points.map((p) => p.v - (intercept + slope * p.t));
  const sse = residuals.reduce((acc, r) => acc + r * r, 0);
  const df = n - 2;
  const se = Math.sqrt(sse / df / sxx);
  const t = se === 0 ? (slope === 0 ? 0 : Math.sign(slope) * Infinity) : slope / se;
  const p = studentTTwoSidedP(t, df);
  const half = studentTCritical(alpha, df) * se;
  const r2 = syy === 0 ? 0 : 1 - sse / syy;

  // Residuals that resemble their neighbours carry less independent
  // information than n suggests. Following Santer et al. (2008), shrink n to an
  // effective sample size and widen the standard error accordingly.
  let num = 0;
  for (let i = 1; i < n; i++) num += residuals[i] * residuals[i - 1];
  const r1 = sse === 0 ? 0 : num / sse;
  const rPos = Math.min(Math.max(r1, 0), 0.99);
  const nEff = Math.max((n * (1 - rPos)) / (1 + rPos), 3);
  const dfAdj = nEff - 2;
  const seAdj = se * Math.sqrt(df / dfAdj);
  const tAdj = seAdj === 0 ? t : slope / seAdj;
  const pAdjusted = studentTTwoSidedP(tAdj, dfAdj);
  const halfAdj = studentTCritical(alpha, dfAdj) * seAdj;

  return {
    slope,
    intercept,
    se,
    t,
    df,
    p,
    ci: [slope - half, slope + half],
    r2,
    r1,
    nEff,
    seAdjusted: seAdj,
    dfAdjusted: dfAdj,
    pAdjusted,
    ciAdjusted: [slope - halfAdj, slope + halfAdj],
  };
}

/** Mann-Kendall trend test with tie correction and continuity correction. */
export function mannKendall(values: number[]): MannKendallResult {
  const n = values.length;
  let s = 0;
  for (let i = 0; i < n - 1; i++) {
    for (let j = i + 1; j < n; j++) {
      s += Math.sign(values[j] - values[i]);
    }
  }
  const tieCounts = new Map<number, number>();
  for (const v of values) tieCounts.set(v, (tieCounts.get(v) ?? 0) + 1);
  let tieTerm = 0;
  for (const c of tieCounts.values()) {
    if (c > 1) tieTerm += c * (c - 1) * (2 * c + 5);
  }
  const varS = (n * (n - 1) * (2 * n + 5) - tieTerm) / 18;
  let z = 0;
  if (varS > 0) {
    if (s > 0) z = (s - 1) / Math.sqrt(varS);
    else if (s < 0) z = (s + 1) / Math.sqrt(varS);
  }
  return { s, varS, z, p: normalTwoSidedP(z), tau: s / ((n * (n - 1)) / 2) };
}

/** Theil-Sen slope: the median of the slopes between every pair of points. */
export function senSlope(points: Point[], varS: number, alpha = 0.05): SenResult {
  const n = points.length;
  const slopes = new Float64Array((n * (n - 1)) / 2);
  let k = 0;
  for (let i = 0; i < n - 1; i++) {
    for (let j = i + 1; j < n; j++) {
      const dt = points[j].t - points[i].t;
      if (dt !== 0) slopes[k++] = (points[j].v - points[i].v) / dt;
    }
  }
  const sorted = slopes.subarray(0, k).sort();
  const slope = quantileSorted(sorted, 0.5);
  const intercept = median(points.map((p) => p.v - slope * p.t));

  // Confidence limits from the Mann-Kendall variance (Gilbert, 1987).
  const c = normalCritical(alpha) * Math.sqrt(varS);
  const lower = valueAt(sorted, (k - c) / 2 - 1);
  const upper = valueAt(sorted, (k + c) / 2);
  return { slope, intercept, ci: [lower, upper] };
}

export interface SlopeDifference {
  /** Slope of the first series minus slope of the second */
  difference: number;
  p: number;
  significant: boolean;
}

/**
 * Do two series change at different rates? A t-test on the difference of the
 * two regression slopes, using the autocorrelation-adjusted standard errors.
 * It treats the series as independent, which makes it cautious for
 * neighbouring regions that rise and fall together.
 */
export function compareSlopes(a: OlsResult, b: OlsResult, alpha = 0.05): SlopeDifference {
  const difference = a.slope - b.slope;
  const se = Math.hypot(a.seAdjusted, b.seAdjusted);
  const df = a.dfAdjusted + b.dfAdjusted;
  const p = se === 0 ? (difference === 0 ? 1 : 0) : studentTTwoSidedP(difference / se, df);
  return { difference, p, significant: p < alpha };
}

export function analyzeTrend(points: Point[], alpha = 0.05): TrendResult | null {
  if (points.length < MIN_POINTS) return null;
  const sorted = [...points].sort((a, b) => a.t - b.t);
  const regression = ols(sorted, alpha);
  const mk = mannKendall(sorted.map((p) => p.v));
  const sen = senSlope(sorted, mk.varS, alpha);

  const direction: Direction =
    sen.slope > 0 ? "rising" : sen.slope < 0 ? "falling" : "flat";

  // Call a trend significant only when the rank test and the
  // autocorrelation-adjusted regression agree. One without the other is borderline.
  const pMk = mk.p;
  const pReg = regression.pAdjusted;
  let strength: Strength = "not-significant";
  if (pMk < alpha && pReg < alpha) strength = "significant";
  else if (Math.min(pMk, pReg) < alpha || Math.max(pMk, pReg) < 2 * alpha) {
    strength = "borderline";
  }

  return {
    n: sorted.length,
    alpha,
    spanYears: sorted[sorted.length - 1].t - sorted[0].t,
    mean: sorted.reduce((acc, p) => acc + p.v, 0) / sorted.length,
    direction,
    strength,
    ols: regression,
    mannKendall: mk,
    sen,
  };
}

function median(values: number[]): number {
  return quantileSorted(Float64Array.from(values).sort(), 0.5);
}

function quantileSorted(sorted: Float64Array, q: number): number {
  return valueAt(sorted, (sorted.length - 1) * q);
}

/** Value at a fractional index of a sorted array, linearly interpolated and clamped. */
function valueAt(sorted: Float64Array, index: number): number {
  if (sorted.length === 0) return NaN;
  const i = Math.min(Math.max(index, 0), sorted.length - 1);
  const lo = Math.floor(i);
  const hi = Math.ceil(i);
  return sorted[lo] + (sorted[hi] - sorted[lo]) * (i - lo);
}
