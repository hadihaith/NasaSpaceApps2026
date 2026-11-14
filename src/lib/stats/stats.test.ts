import { describe, expect, it } from "vitest";
import {
  incompleteBeta,
  lnGamma,
  normalCdf,
  normalCritical,
  studentTCritical,
  studentTTwoSidedP,
} from "./distributions";
import { analyzeTrend, compareSlopes, mannKendall, ols, senSlope, type Point } from "./trend";

describe("distributions", () => {
  it("normal CDF matches reference values", () => {
    expect(normalCdf(0)).toBeCloseTo(0.5, 14);
    expect(normalCdf(1.96)).toBeCloseTo(0.9750021048517795, 12);
    expect(normalCdf(-1)).toBeCloseTo(0.15865525393145707, 12);
    expect(normalCdf(-8)).toBeCloseTo(6.220960574271785e-16, 22);
  });

  it("normal critical value for 95% is 1.96", () => {
    expect(normalCritical(0.05)).toBeCloseTo(1.959963984540054, 8);
  });

  it("lnGamma matches known factorials", () => {
    expect(lnGamma(5)).toBeCloseTo(Math.log(24), 11);
    expect(lnGamma(0.5)).toBeCloseTo(Math.log(Math.sqrt(Math.PI)), 11);
  });

  it("incomplete beta is symmetric and exact for simple cases", () => {
    expect(incompleteBeta(0.5, 2, 2)).toBeCloseTo(0.5, 12);
    // I_x(1, b) = 1 - (1 - x)^b
    expect(incompleteBeta(0.3, 1, 4)).toBeCloseTo(1 - 0.7 ** 4, 12);
  });

  it("Student t p-values match reference values", () => {
    // t(1) is the Cauchy distribution: P(|T| > 1) = 0.5
    expect(studentTTwoSidedP(1, 1)).toBeCloseTo(0.5, 12);
    // t(2) has a closed form: P(|T| > t) = 1 - t / sqrt(2 + t^2)
    expect(studentTTwoSidedP(2, 2)).toBeCloseTo(1 - 2 / Math.sqrt(6), 12);
    // Standard table value: t = 2.228 at df 10 gives p = 0.05
    expect(studentTTwoSidedP(2.228138851986274, 10)).toBeCloseTo(0.05, 9);
  });

  it("Student t critical values match tables", () => {
    expect(studentTCritical(0.05, 10)).toBeCloseTo(2.228138851986274, 7);
    expect(studentTCritical(0.05, 30)).toBeCloseTo(2.042272456301238, 7);
    expect(studentTCritical(0.05, 1e7)).toBeCloseTo(1.96, 3);
  });
});

describe("ordinary least squares", () => {
  it("recovers an exact line", () => {
    const pts: Point[] = [0, 1, 2, 3, 4].map((t) => ({ t, v: 3 + 2 * t }));
    const r = ols(pts);
    expect(r.slope).toBeCloseTo(2, 12);
    expect(r.intercept).toBeCloseTo(3, 12);
    expect(r.r2).toBeCloseTo(1, 12);
  });

  it("matches a hand-computed regression", () => {
    // x = 1..5, y = 2, 4, 5, 4, 5: slope 0.6, intercept 2.2, SSE 2.4, Sxx 10
    const pts: Point[] = [2, 4, 5, 4, 5].map((v, i) => ({ t: i + 1, v }));
    const r = ols(pts);
    expect(r.slope).toBeCloseTo(0.6, 12);
    expect(r.intercept).toBeCloseTo(2.2, 12);
    expect(r.se).toBeCloseTo(Math.sqrt(2.4 / 3 / 10), 12);
    expect(r.t).toBeCloseTo(0.6 / Math.sqrt(0.08), 10);
    expect(r.df).toBe(3);
    expect(r.r2).toBeCloseTo(0.6, 12);
    expect(r.p).toBeGreaterThan(0.1);
    expect(r.p).toBeLessThan(0.15);
    expect(r.ci[0]).toBeLessThan(0);
    expect(r.ci[1]).toBeGreaterThan(1);
  });

  it("widens uncertainty when residuals are autocorrelated", () => {
    // A slow wave around a weak trend: neighbours look alike.
    const pts: Point[] = Array.from({ length: 60 }, (_, i) => ({
      t: i,
      v: 0.01 * i + Math.sin(i / 6),
    }));
    const r = ols(pts);
    expect(r.r1).toBeGreaterThan(0.8);
    expect(r.nEff).toBeLessThan(10);
    expect(r.pAdjusted).toBeGreaterThan(r.p);
  });
});

describe("Mann-Kendall", () => {
  it("gives the textbook S, variance and Z for a small series", () => {
    // Strictly increasing, n = 10: S = 45, Var = 10*9*25/18 = 125
    const r = mannKendall([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    expect(r.s).toBe(45);
    expect(r.varS).toBe(125);
    expect(r.z).toBeCloseTo(44 / Math.sqrt(125), 12);
    expect(r.tau).toBe(1);
    expect(r.p).toBeLessThan(0.001);
  });

  it("applies the tie correction", () => {
    // n = 5 with one tied pair: Var = (5*4*15 - 2*1*9) / 18
    const r = mannKendall([1, 2, 2, 3, 4]);
    expect(r.s).toBe(9);
    expect(r.varS).toBeCloseTo((300 - 18) / 18, 12);
  });

  it("reports no trend for a constant series", () => {
    const r = mannKendall([3, 3, 3, 3, 3, 3]);
    expect(r.s).toBe(0);
    expect(r.z).toBe(0);
    expect(r.p).toBe(1);
  });

  it("is antisymmetric", () => {
    const up = mannKendall([1, 3, 2, 5, 4, 7, 6, 9]);
    const down = mannKendall([9, 6, 7, 4, 5, 2, 3, 1]);
    expect(down.s).toBe(-up.s);
    expect(down.z).toBeCloseTo(-up.z, 12);
  });
});

describe("Theil-Sen slope", () => {
  it("ignores a wild outlier that drags least squares", () => {
    const pts: Point[] = Array.from({ length: 21 }, (_, i) => ({ t: i, v: 0.5 * i }));
    pts[10] = { t: 10, v: 500 };
    const mk = mannKendall(pts.map((p) => p.v));
    const sen = senSlope(pts, mk.varS);
    expect(sen.slope).toBeCloseTo(0.5, 10);
    expect(sen.ci[0]).toBeLessThanOrEqual(sen.slope);
    expect(sen.ci[1]).toBeGreaterThanOrEqual(sen.slope);
  });
});

// Deterministic pseudo-noise so the tests never flake.
const noise = (i: number) => Math.sin(i * 12.9898) * 43758.5453 - Math.floor(Math.sin(i * 12.9898) * 43758.5453) - 0.5;

describe("compareSlopes", () => {
  const series = (slope: number, seed: number): Point[] =>
    Array.from({ length: 45 }, (_, i) => ({ t: 1981 + i, v: slope * i + 0.3 * noise(i + seed) }));

  it("separates a rising series from a falling one", () => {
    const r = compareSlopes(ols(series(0.03, 0)), ols(series(-0.03, 100)), 0.05, true);
    expect(r.difference).toBeGreaterThan(0.04);
    expect(r.significant).toBe(true);
  });

  it("does not separate two series with the same underlying rate", () => {
    const r = compareSlopes(ols(series(0.03, 0)), ols(series(0.03, 100)), 0.05, true);
    expect(r.significant).toBe(false);
  });

  it("is antisymmetric", () => {
    const a = ols(series(0.03, 0));
    const b = ols(series(0.01, 100));
    expect(compareSlopes(a, b, 0.05, true).difference).toBeCloseTo(-compareSlopes(b, a, 0.05, true).difference, 12);
    expect(compareSlopes(a, b, 0.05, true).p).toBeCloseTo(compareSlopes(b, a, 0.05, true).p!, 12);
  });
});

describe("analyzeTrend", () => {

  it("declines to analyse a series that is too short", () => {
    expect(analyzeTrend([{ t: 1, v: 1 }, { t: 2, v: 2 }])).toBeNull();
  });

  it("finds a clear rising trend", () => {
    const pts: Point[] = Array.from({ length: 45 }, (_, i) => ({
      t: 1981 + i,
      v: 15 + 0.03 * i + 0.3 * noise(i),
    }));
    const r = analyzeTrend(pts, 0.05, { assumptionsReviewed: true })!;
    expect(r.direction).toBe("rising");
    expect(r.strength).toBe("significant");
    expect(r.sen.slope).toBeGreaterThan(0.02);
    expect(r.sen.slope).toBeLessThan(0.04);
    expect(r.sen.ci[0]).toBeGreaterThan(0);
  });

  it("does not call pure noise a trend", () => {
    const pts: Point[] = Array.from({ length: 45 }, (_, i) => ({ t: 1981 + i, v: noise(i) }));
    const r = analyzeTrend(pts, 0.05, { assumptionsReviewed: true })!;
    expect(r.strength).toBe("not-significant");
    expect(r.sen.ci[0]).toBeLessThan(0);
    expect(r.sen.ci[1]).toBeGreaterThan(0);
  });
});
