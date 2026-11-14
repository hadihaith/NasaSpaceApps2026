/**
 * Linear trend detection for a time series.
 *
 * Three complementary summaries (not a challenge-mandated combination):
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
export type Strength = "significant" | "borderline" | "not-significant" | "not-established";

export interface TrendResult {
  inference: { established: boolean; reasons: string[]; method: string };
  magnitude: number;
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

/** Application minimum, not a universal scientific sufficiency threshold. */
export const MIN_POINTS = 10;

export function ols(points: Point[], alpha = 0.05): OlsResult {
  validatePoints(points, 3, alpha);
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

  const residuals = points.map((p) => p.v - meanV - slope * (p.t - meanT));
  const sse = residuals.reduce((acc, r) => acc + r * r, 0);
  const df = n - 2;
  const se = Math.sqrt(sse / df / sxx);
  const t = se === 0 ? (slope === 0 ? 0 : Math.sign(slope) * Infinity) : slope / se;
  const p = studentTTwoSidedP(t, df);
  const half = studentTCritical(alpha, df) * se;
  const r2 = syy === 0 ? 0 : 1 - sse / syy;

  // Residuals that resemble their neighbours carry less independent
  // information than n suggests. As an exploratory AR(1) sensitivity, shrink n to an
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
  if (values.length < 2 || values.some((v) => !Number.isFinite(v))) {
    throw new RangeError("Mann-Kendall needs at least two finite values in time order.");
  }
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
  validatePoints(points, 2, alpha);
  if (!Number.isFinite(varS) || varS < 0) throw new RangeError("Invalid rank variance.");
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
  // Discrete order-statistic bounds, matching scipy.stats.theilslopes.
  const lower = valueAt(sorted, Math.round((k - c) / 2) - 1);
  const upper = valueAt(sorted, Math.round((k + c) / 2));
  return { slope, intercept, ci: [lower, upper] };
}

export interface SlopeDifference {
  difference: number;
  p: number | null;
  significant: boolean;
  established: boolean;
  reason: string;
  n?: number;
}

/** Independent-series Welch approximation. Prefer comparePairedSeries for regions. */
export function compareSlopes(a: OlsResult, b: OlsResult, alpha = 0.05,
  independenceReviewed = false): SlopeDifference {
  validateAlpha(alpha);
  const difference = a.slope - b.slope;
  const va = a.seAdjusted ** 2;
  const vb = b.seAdjusted ** 2;
  const denominator = va ** 2 / a.dfAdjusted + vb ** 2 / b.dfAdjusted;
  const established = independenceReviewed && denominator > 0 && Number.isFinite(denominator);
  const df = (va + vb) ** 2 / denominator;
  const p = established ? studentTTwoSidedP(difference / Math.sqrt(va + vb), df) : null;
  return { difference, p, significant: p !== null && p < alpha, established,
    reason: established ? "Welch approximation; independent series assumed." : "Independence or uncertainty has not been established." };
}

export interface InferenceOptions {
  /** Explicit scientific review of independence, linearity, residual variance and distribution.
   * This is not a user confidence slider. Default is descriptive analysis only. */
  assumptionsReviewed?: boolean;
  /** Monthly anomalies are descriptive until a seasonal inference model is validated. */
  seasonalAdjustment?: boolean;
}

/** Analyze the paired A-B series on shared timestamps, retaining spatial covariance. */
export function comparePairedSeries(a: Point[], b: Point[], alpha = 0.05,
  options: InferenceOptions = {}): SlopeDifference {
  validatePoints(a, 2, alpha);
  validatePoints(b, 2, alpha);
  const bByTime = new Map(b.map((p) => [p.t, p.v]));
  const paired = a.filter((p) => bByTime.has(p.t)).map((p) => ({ t: p.t, v: p.v - bByTime.get(p.t)! }));
  const result = analyzeTrend(paired, alpha, options);
  const established = result?.inference.established ?? false;
  return { difference: result?.ols.slope ?? NaN, p: established ? result!.ols.p : null,
    significant: established && result!.ols.p < alpha, established, n: paired.length,
    reason: result ? result.inference.reasons.join(" ") || "OLS test of paired A minus B values on common dates." : "Too few shared dates." };
}

export function analyzeTrend(points: Point[], alpha = 0.05,
  options: InferenceOptions = {}): TrendResult | null {
  validateAlpha(alpha);
  if (points.some((p) => !Number.isFinite(p.t) || !Number.isFinite(p.v))) {
    throw new RangeError("Times and values must be finite; remove missing values explicitly.");
  }
  if (new Set(points.map((p) => p.t)).size !== points.length) throw new RangeError("Duplicate times are not allowed.");
  if (points.length > 2400) throw new RangeError("This browser analysis supports at most 2400 points.");
  if (points.length < MIN_POINTS) return null;
  const sorted = [...points].sort((a, b) => a.t - b.t);
  validatePoints(sorted, MIN_POINTS, alpha);
  const regression = ols(sorted, alpha);
  const mk = mannKendall(sorted.map((p) => p.v));
  const sen = senSlope(sorted, mk.varS, alpha);
  const direction: Direction = sen.slope > 0 ? "rising" : sen.slope < 0 ? "falling" : "flat";
  const reasons: string[] = [];
  if (!options.assumptionsReviewed) reasons.push("Model assumptions have not been reviewed for this dataset.");
  if (options.seasonalAdjustment) reasons.push("Monthly anomaly inference requires a validated seasonal model.");
  const step = sorted[1].t - sorted[0].t;
  if (sorted.slice(2).some((p, i) => Math.abs(p.t - sorted[i + 1].t - step) > Math.abs(step) * 1e-6)) {
    reasons.push("Irregular intervals or missing periods require a separate dependence model.");
  }
  // An exploratory flag, not a proof of independence or a calibrated eligibility test.
  if (Math.abs(regression.r1) > 1.96 / Math.sqrt(sorted.length)) {
    reasons.push("Residual serial correlation is flagged; ordinary test assumptions may not hold.");
  }
  if (regression.se <= Number.EPSILON * Math.max(1, Math.abs(regression.slope))) {
    reasons.push("Residual variation is zero or numerically negligible; uncertainty cannot be estimated reliably.");
  }
  const established = reasons.length === 0;
  // One prespecified primary test, not a custom combination of multiple p-values.
  const strength: Strength = !established ? "not-established"
    : regression.p < alpha ? "significant" : "not-significant";
  const spanYears = sorted[sorted.length - 1].t - sorted[0].t;
  return { n: sorted.length, alpha, spanYears,
    mean: sorted.reduce((acc, p) => acc + p.v, 0) / sorted.length,
    direction, strength, ols: regression, mannKendall: mk, sen,
    magnitude: sen.slope * spanYears,
    inference: { established, reasons, method: "Two-sided OLS slope t-test, conditional on reviewed assumptions" } };
}

function validateAlpha(alpha: number) {
  if (!Number.isFinite(alpha) || alpha <= 0 || alpha >= 1) throw new RangeError("alpha must be between 0 and 1.");
}

function validatePoints(points: Point[], minimum: number, alpha: number) {
  validateAlpha(alpha);
  if (points.length < minimum || points.some((p) => !Number.isFinite(p.t) || !Number.isFinite(p.v))) {
    throw new RangeError(`At least ${minimum} finite points are required.`);
  }
  if (new Set(points.map((p) => p.t)).size !== points.length) throw new RangeError("Duplicate times are not allowed.");
  if (points.some((p, i) => i > 0 && p.t <= points[i - 1].t)) throw new RangeError("Points must be in increasing time order.");
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
