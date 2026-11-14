import { describe, expect, it } from "vitest";
import reference from "../stats/reference.json";
import { analyzeTrend, comparePairedSeries, compareSlopes, mannKendall, ols, senSlope } from "../stats/trend";
import { toSeries } from "../series";
import { analyzeDataset, parseDataset } from "./dataset";
import { EXAMPLES, exampleReview } from "./examples";
import { judge, NEW_PROGRESS, playRound } from "../game";

describe("independent SciPy references", () => {
  for (const fixture of reference.cases) {
    it(`matches OLS and Sen including confidence limits: ${fixture.name}`, () => {
      const result = ols(fixture.points);
      for (const key of ["slope", "intercept", "se", "p"] as const) expect(result[key]).toBeCloseTo(fixture.ols[key], 9);
      result.ci.forEach((value, i) => expect(value).toBeCloseTo(fixture.ols.ci[i], 8));
      const sen = senSlope(fixture.points, mannKendall(fixture.points.map((p) => p.v)).varS);
      expect(sen.slope).toBeCloseTo(fixture.sen.slope, 10);
      expect(sen.intercept).toBeCloseTo(fixture.sen.intercept, 9);
      sen.ci.forEach((value, i) => expect(value).toBeCloseTo(fixture.sen.ci[i], 10));
    });
  }
  for (const fixture of reference.mannKendall) {
    it(`matches independent MK enumeration: ${fixture.values.join(",")}`, () => {
      const result = mannKendall(fixture.values);
      expect(result.s).toBe(fixture.s);
      expect(result.varS).toBeCloseTo(fixture.varS, 12);
      expect(result.p).toBeCloseTo(fixture.p, 12);
    });
  }
});

describe("scientific eligibility", () => {
  const pts = reference.cases[1].points;
  it("keeps unreviewed data descriptive", () => {
    const r = analyzeTrend(pts)!;
    expect(r.strength).toBe("not-established");
    expect(r.magnitude).toBeCloseTo(r.sen.slope * r.spanYears);
  });
  it("flags irregular time and serial correlation even after review", () => {
    expect(analyzeTrend(pts.filter((_, i) => i !== 5), .05, { assumptionsReviewed: true })!.inference.reasons.join()).toContain("Irregular");
    const wave = Array.from({ length: 60 }, (_, i) => ({ t: i, v: i * .1 + Math.sin(i / 6) }));
    expect(analyzeTrend(wave, .05, { assumptionsReviewed: true })!.strength).toBe("not-established");
  });
  it("rejects duplicates, non-finite values and invalid alpha", () => {
    expect(() => analyzeTrend([...pts, pts[0]])).toThrow(/Duplicate/);
    expect(() => analyzeTrend([{ t: 0, v: NaN }])).toThrow(/finite/);
    expect(() => analyzeTrend(pts, 0)).toThrow(/alpha/);
    expect(() => ols([{ t: 0, v: 2 }, { t: 0, v: 3 }, { t: 1, v: 4 }])).toThrow();
  });
  it("does not assign certainty to an exact line or constant series", () => {
    for (const slope of [0, 2]) expect(analyzeTrend(Array.from({ length: 20 }, (_, t) => ({ t, v: 3 + slope * t })), .05, { assumptionsReviewed: true })!.strength).toBe("not-established");
  });
  it("leaves round progress unchanged when significance is unresolved", () => {
    const trend = { direction: "rising", strength: "not-established" } as const;
    expect(judge("rising", trend).xp).toBe(0);
    const result = playRound(NEW_PROGRESS, { guesses: ["rising"], trends: [trend], caseId: "one", totalCases: 1, theme: "heat", spots: [{ lat: 0, lon: 0 }] });
    expect(result.progress).toBe(NEW_PROGRESS);
    expect(result.xpGained).toBe(0);
    expect(result.stars).toBeNull();
  });
});

describe("calendar and Member 3 contract", () => {
  it("requires twelve months for an annual value but retains a partial final-year month", () => {
    const record = { startYear: 2000, monthly: Array(13).fill(12) as number[] };
    expect(toSeries(record, "annual")).toHaveLength(1);
    expect(toSeries(record, "month", 0)).toHaveLength(2);
  });
  it("does not impute missing values or turn non-finite values into observations", () => {
    expect(toSeries({ startYear: 2000, monthly: [null, ...Array(11).fill(4)] }, "annual")).toEqual([]);
    expect(() => toSeries({ startYear: 2000, monthly: [Infinity] }, "annual")).toThrow();
    expect(() => toSeries({ startYear: 2000, monthly: [1] }, "month", 12)).toThrow();
  });
  it("centres each calendar month across the chosen anomaly baseline", () => {
    const pts = analyzeDataset(EXAMPLES[0], "anomaly").points;
    for (let m = 0; m < 12; m++) expect(pts.filter((_, i) => i % 12 === m).reduce((sum, p) => sum + p.v, 0)).toBeCloseTo(0, 10);
  });
  it("validates chronology and provenance and refuses an oversized series", () => {
    const d = structuredClone(EXAMPLES[0]);
    d.observations[1].date = d.observations[0].date;
    expect(() => parseDataset(d)).toThrow(/consecutive/);
    expect(() => parseDataset({ ...EXAMPLES[0], provenance: {} })).toThrow(/provenance/);
    expect(() => parseDataset({ ...EXAMPLES[0], observations: Array(2401).fill(EXAMPLES[0].observations[0]) })).toThrow(/2400/);
  });
  it("matches review to dataset, version, period and mode", () => {
    const d = EXAMPLES[0];
    const review = exampleReview(d, "annual", 0, 1980, 2019)!;
    expect(analyzeDataset(d, "annual", 0, undefined, review).trend!.inference.established).toBe(true);
    expect(analyzeDataset(d, "annual", 0, { startYear: 1990, endYear: 2019 }, review).trend!.strength).toBe("not-established");
    expect(analyzeDataset(d, "anomaly", 0, undefined, review).trend!.strength).toBe("not-established");
    expect(analyzeDataset({ ...d, kind: "simulation" }, "annual", 0, undefined, review).trend!.strength).toBe("not-established");
  });
  it("warns about unknown redistribution and distinguishes synthetic values", () => {
    const d = structuredClone(EXAMPLES[0]); d.provenance.redistribution = "unknown";
    const r = analyzeDataset(d);
    expect(r.warnings.join()).toContain("not observed Earth");
    expect(r.warnings.join()).toContain("Redistribution");
  });
  it("preserves missing years and reports a short period without inventing a test", () => {
    expect(analyzeDataset(EXAMPLES[4]).missingMonths).toBe(12);
    expect(analyzeDataset(EXAMPLES[0], "annual", 0, { startYear: 1980, endYear: 1985 }).trend).toBeNull();
  });
});

describe("paired regional comparison", () => {
  it("uses common dates and agrees with an independent regression of differences", () => {
    const differences = reference.cases[1];
    const a = differences.points.map((p) => ({ t: p.t, v: p.v + Math.sin(p.t) * 100 }));
    const b = differences.points.map((p) => ({ t: p.t, v: Math.sin(p.t) * 100 }));
    const r = comparePairedSeries(a, b, .05, { assumptionsReviewed: true });
    expect(r.difference).toBeCloseTo(differences.ols.slope, 10);
    expect(r.p).toBeCloseTo(differences.ols.p, 10);
    expect(comparePairedSeries(a, b.slice(0, 5)).established).toBe(false);
  });
  it("requires an explicit independence review for the legacy unpaired comparison", () => {
    const a = ols(reference.cases[0].points);
    const b = ols(reference.cases[1].points);
    expect(compareSlopes(a, b).p).toBeNull();
  });
});
