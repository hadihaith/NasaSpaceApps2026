import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import archive from "../../../public/data/earth-archive.json";
import reviews from "../../../data/reviews/homecoming-trends.json";
import { chileRainDataset, CHILE_RAIN_SOURCE, GUIDED_RAIN, DEMO_RAIN, DEMO_RAIN_SOURCES, demoRainDataset } from "./curated-history";
import { analyzeHistoricalComparison, analyzeHistoricalRecord } from "./historical";
import { parseArchive } from "./member3";

const { evidence, comparisons, regions } = JSON.parse(readFileSync(
  "unity/Homecoming/Assets/FieldSimulation/Data/Climate.json", "utf8",
)) as {
  regions: { id: string; normals: unknown[] }[];
  evidence: Array<{
    id: string; seriesId: string; rawSha256: string; fingerprint: string;
    observations: { date: string; hasValue: boolean; value: number | null }[];
    footprint: { bbox: number[] | null; dataBbox: number[] | null; point: { latitude: number; longitude: number } | null };
    finding: ReturnType<typeof analyzeHistoricalRecord>;
    monthFindings: ReturnType<typeof analyzeHistoricalRecord>[];
    anomalyFinding: ReturnType<typeof analyzeHistoricalRecord>;
  }>;
  comparisons: ReturnType<typeof analyzeHistoricalComparison>[];
};

const datasets = parseArchive(archive);
const record = (id: string) => datasets.find((dataset) => dataset.seriesId === id)!;
const exported = (id: string) => evidence.find((dataset) => dataset.seriesId === id)!;
const sahel = record("sahel.merra2.precipitation");
const raw = readFileSync(CHILE_RAIN_SOURCE.path);
const chile = chileRainDataset(JSON.parse(raw.toString()), createHash("sha256").update(raw).digest("hex"), sahel);

describe("historical evidence exported for Unity", () => {
  it("preserves the six simulation regions and traces every bundled observation to its input", () => {
    expect(regions).toHaveLength(6);
    expect(regions.every((region) => region.normals.length === 12)).toBe(true);
    expect(evidence).toHaveLength(34);
    for (const dataset of [...datasets.filter((d) => d.gameVariable), chile]) {
      const found = exported(dataset.seriesId);
      expect(found.id).toBe(dataset.id);
      expect(found.rawSha256).toBe(dataset.rawSha256);
      expect(found.fingerprint).toBe(found.finding.fingerprint);
      expect(found.fingerprint).toMatch(/^[a-f0-9]{64}$/);
      expect(found.observations).toEqual(dataset.observations.map(({ date, value }) => ({ date, value, hasValue: value !== null })));
    }
  });

  it("pins actual Kuwait and UK observations with a separately labeled exploratory test", () => {
    const demo = DEMO_RAIN_SOURCES.map((source) => {
      const bytes = readFileSync(source.path);
      return demoRainDataset(JSON.parse(bytes.toString()), createHash("sha256").update(bytes).digest("hex"), source, sahel);
    });
    const comparison = comparisons.find((item) => item.id === DEMO_RAIN.id)!;
    expect(comparison).toMatchObject(analyzeHistoricalComparison(DEMO_RAIN, demo[0], demo[1]));
    for (const dataset of demo) {
      expect(exported(dataset.seriesId).observations).toEqual(dataset.observations.map(({ date, value }) => ({ date, value, hasValue: true })));
      expect(exported(dataset.seriesId).rawSha256).toBe(dataset.rawSha256);
    }
    expect(exported(demo[0].seriesId).footprint.point).toMatchObject({ latitude: 29.376, longitude: 47.977 });
    expect(exported(demo[1].seriesId).footprint.point).toMatchObject({ latitude: 51.507, longitude: -0.128 });
    expect(comparison.difference.status).toBe("not-established");
    expect(comparison.difference.test).toBeNull();
    expect(comparison.difference).toMatchObject({
      exploratoryTest: { degreesOfFreedom: 33, tStatistic: -1.2333526679427778, p: 0.2261574433994732 },
    });
    expect(comparison).toMatchObject({ pearsonR: -0.03958287162742103 });
  });

  it("uses reported grid points and exact requested and contributing Phoenix bounds", () => {
    expect(exported(chile.seriesId).footprint).toMatchObject({ bbox: null, point: { latitude: -36, longitude: -72 } });
    expect(exported(sahel.seriesId).footprint).toMatchObject({ bbox: null, point: { latitude: 14, longitude: 6 } });
    expect(exported("region_01.merra2.precipitation").footprint.bbox).toEqual([-113, 32.5, -111, 34.5]);
    expect(exported("region_01.imerg.precipitation").footprint.dataBbox).toEqual([-112.95, 32.55, -111.05, 34.45]);
  });

  it("pins a standalone reviewed trend and withholds it after a period, mode or fingerprint change", () => {
    const temperature = record("region_01.merra2.air_temperature_2m");
    const { finding } = exported(temperature.seriesId);
    expect(finding.status).toBe("significant");
    expect(finding.n).toBe(45);
    expect(finding.slopePerDecade).toBeCloseTo(0.5087719298245631, 10);
    expect(finding.test?.p).toBeCloseTo(2.1440920500778286e-9, 18);
    expect(finding.test?.lowerPerDecade).toBeCloseTo(0.34259101, 7);
    expect(analyzeHistoricalRecord(temperature, finding, "annual", 0, reviews)).toEqual(finding);
    const revisedFootprint = structuredClone(temperature);
    revisedFootprint.spatial.bbox!.latMin += 0.25;
    for (const newFinding of [
      analyzeHistoricalRecord(revisedFootprint, finding, "annual", 0, reviews),
      analyzeHistoricalRecord(temperature, { ...finding, startYear: 1991 }, "annual", 0, reviews),
      analyzeHistoricalRecord(temperature, finding, "month", 0, reviews),
    ]) {
      expect(newFinding.status).toBe("not-established");
      expect(newFinding.test).toBeNull();
      expect(newFinding.review).toBeNull();
    }
  });

  it("keeps fixed normals stable across display periods and labels a short normal as period-relative", () => {
    const actual = exported(sahel.seriesId).anomalyFinding;
    expect(actual.baseline).toMatchObject({ type: "fixed", startYear: 1991, endYear: 2020, counts: Array(12).fill(30) });
    const short = analyzeHistoricalRecord(sahel, { startYear: 2005, endYear: 2015 }, "anomaly");
    expect(short.baseline).toEqual(actual.baseline);
    expect(short.points[0]).toEqual(actual.points.find((p) => p.date === "2005-01"));
    expect(short.test).toBeNull();
    const imerg = exported("region_01.imerg.precipitation");
    expect(imerg.anomalyFinding.baseline?.type).toBe("period-relative");
    expect(imerg.finding.lastDate).toBe("2024");
    expect(imerg.monthFindings[8].lastDate).toBe("2025-09");
    expect(imerg.monthFindings[9].lastDate).toBe("2024-10");
  });

  it("uses one reviewed paired OLS decision with common timestamps and two descriptive rates", () => {
    const comparison = comparisons.find((item) => item.id === GUIDED_RAIN.id)!;
    expect(analyzeHistoricalComparison(GUIDED_RAIN, sahel, chile, reviews)).toEqual(comparison);
    const { left, right, difference } = comparison;
    expect(left.slopePerDecade).toBeCloseTo(2.133333333333335, 10);
    expect(right.slopePerDecade).toBeCloseTo(-18.840151515151526, 10);
    expect(left.test).toBeNull();
    expect(right.test).toBeNull();
    expect(left.review?.accepted).toBe(false);
    expect(right.review?.accepted).toBe(false);
    expect(difference.status).toBe("significant");
    expect(difference.n).toBe(35);
    expect(difference.points.map(({ value }) => value)).toEqual(left.points.map(({ value }, i) => value - right.points[i].value));
    expect(difference.slopePerDecade).toBeCloseTo(24.6453125, 10);
    expect(difference.test?.slopePerDecade).toBeCloseTo(22.843984593837526, 10);
    expect(difference.test?.p).toBeCloseTo(3.2300390595167366e-8, 17);
    expect(difference.test?.lowerPerDecade).toBeCloseTo(16.36212436, 7);
    expect(difference.test?.upperPerDecade).toBeCloseTo(29.32584482, 7);
  });

  it("preserves common years for a gappy pair and rejects incompatible footprints and measurements", () => {
    const gappy = structuredClone(chile);
    gappy.observations.find(({ date }) => date === "2000-02")!.value = null;
    const comparison = analyzeHistoricalComparison(GUIDED_RAIN, sahel, gappy, reviews);
    expect(comparison.left.points.map(({ date }) => date)).toEqual(comparison.right.points.map(({ date }) => date));
    expect(comparison.difference.n).toBe(34);
    expect(comparison.difference.test).toBeNull();
    expect(comparison.difference.reasons).toContain("The paired test requires complete annual coverage across the pinned window.");
    expect(() => analyzeHistoricalComparison(GUIDED_RAIN, sahel, record("region_01.merra2.precipitation")))
      .toThrow(/compatible/);
    expect(() => analyzeHistoricalComparison(GUIDED_RAIN, sahel, record("sahel.merra2.air_temperature_2m")))
      .toThrow(/compatible/);
  });

  it("cross-checks Phoenix precipitation over 27 common years without an inferential claim", () => {
    const check = comparisons.find((item) => item.type === "product-cross-check")!;
    expect(check).toMatchObject({ startYear: 1998, endYear: 2024,
      monthlyOverlap: { firstDate: "1998-01", lastDate: "2025-09", n: 333 } });
    expect(check.left.n).toBe(27);
    expect(check.right.n).toBe(27);
    expect(check.left.slopePerDecade).toBeCloseTo(0.23939393939394127, 10);
    expect(check.right.slopePerDecade).toBeCloseTo(0.43, 10);
    expect(check.difference.test).toBeNull();
  });
});
