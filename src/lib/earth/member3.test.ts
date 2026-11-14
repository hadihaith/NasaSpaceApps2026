import { readFileSync, readdirSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import archive from "../../../public/data/earth-archive.json";
import canonical from "../../../public/data/earth-observations.json";
import baselines from "../../../public/data/baselines.json";
import climate from "../../../public/homecoming/regional-climate.json";
import { toNormals, type RegionalClimate } from "../../homecoming/model";
import { analyzeDataset, parseDataset } from "../member4/dataset";
import { analyzeEvidence, evidenceKey, parseArchive } from "./member3";
import { VERDICT } from "./verdict";
import { cityVsRegionWarming } from "./inferences";
import { pText } from "../format";

const datasets = parseArchive(archive);

describe("Member 3 real-data handoff", () => {
  it("passes Member 4's real validator and size limit for all 38 exports", () => {
    const folder = resolve("data/processed/member4");
    const names = readdirSync(folder).filter((name) => name.endsWith(".json"));
    expect(names).toHaveLength(38);
    for (const name of names) {
      const raw = readFileSync(resolve(folder, name));
      expect(raw.byteLength).toBeLessThanOrEqual(2_000_000);
      const d = parseDataset(JSON.parse(raw.toString()));
      const result = analyzeDataset(d);
      expect(d.kind).not.toBe("synthetic");
      expect(d.kind).not.toBe("simulation");
      expect(result.trend?.inference.established).toBe(false);
    }
  });

  it("retains every canonical Phoenix value, null, footprint and citation", () => {
    for (const s of canonical.series) {
      const d = JSON.parse(readFileSync(resolve(`data/processed/member4/${s.id}.json`), "utf8"));
      expect(d.observations).toEqual(s.observations);
      expect(d.spatial).toEqual(s.spatial);
      expect(d.units).toBe(s.unit);
      expect(d.source.citation).toEqual(s.source.citation);
      expect(d.providerKind).toBe(s.kind);
      expect(d.regionId).toBe("region_01");
      expect(d.gameRegionId).toBe("phoenix");
    }
  });

  it("ships only the 32 NASA records with documented redistribution in the new Archive", () => {
    expect(datasets).toHaveLength(32);
    expect(datasets.every((d) => d.provenance.redistribution === "permitted" && d.providerKind !== "station")).toBe(true);
    const d = structuredClone(archive);
    d.datasets[0].kind = "simulation";
    expect(() => parseArchive(d)).toThrow(/real records/);
  });

  it("preserves IMERG's partial 2025 endpoint and excludes it only from annual means", () => {
    const d = datasets.find((d) => d.providerKind === "satellite")!;
    expect(d.temporal.end).toBe("2025-09");
    expect(d.quality.completeYears).toEqual(Array.from({ length: 27 }, (_, i) => 1998 + i));
    expect(analyzeEvidence(d, "annual", 1998, 2025).points.at(-1)?.t).toBe(2024);
    expect(analyzeEvidence(d, "month", 1998, 2025, 0).points.at(-1)?.t).toBe(2025);
    expect(analyzeEvidence(d, "anomaly", 1998, 2025).points).toHaveLength(333);
  });

  it("labels annual precipitation as an equal-month mean rather than an annual total", () => {
    const d = datasets.find((d) => d.seriesId === "region_01.merra2.precipitation")!;
    const r = analyzeEvidence(d, "annual", 1981, 2025);
    const sum = d.observations.slice(0, 12).reduce((n, o) => n + o.value!, 0);
    expect(r.points[0].v).toBeCloseTo(sum / 12, 10);
    expect(d.annualInterpretation).toContain("not annual accumulation");
    expect(r.trend?.strength).toBe("not-established");
  });

  it("converts raw point precipitation using leap-year calendar days", () => {
    const raw = JSON.parse(readFileSync(resolve("data/raw/homecoming/sahel_monthly_1991_2025.json"), "utf8"));
    const d = datasets.find((d) => d.seriesId === "sahel.merra2.precipitation")!;
    for (const [date, days] of [["2000-02", 29], ["2001-02", 28]] as const) {
      const rate = raw.properties.parameter.PRECTOTCORR[date.replace("-", "")];
      expect(d.observations.find((o) => o.date === date)?.value).toBeCloseTo(rate * days, 2);
    }
    expect(d.units).toBe("mm/month");
    expect(d.region).toContain("not a region mean");
    expect(d.observations.some((o) => o.date.endsWith("-13"))).toBe(false);
  });

  it("traces Phoenix POWER and IMERG leap-month values back to raw inputs", () => {
    const raw = JSON.parse(readFileSync(resolve("data/raw/nasa_power/region_01_PRECTOTCORR_monthly_1981_2025.json"), "utf8"));
    let sum = 0; let weight = 0;
    for (const cell of raw.features) {
      const w = Math.cos(cell.geometry.coordinates[1] * Math.PI / 180);
      sum += cell.properties.parameter.PRECTOTCORR["200002"] * w; weight += w;
    }
    const power = datasets.find((d) => d.seriesId === "region_01.merra2.precipitation")!;
    expect(power.observations.find((o) => o.date === "2000-02")?.value).toBeCloseTo(sum / weight * 29, 2);
    const csv = readFileSync(resolve("data/raw/giovanni/g4.areaAvgTimeSeries.GPM_3IMERGM_07_precipitation.19980101-20250930.113W_32N_111W_34N.csv"), "latin1");
    const rate = Number(csv.split("\n").find((line) => line.startsWith("2000-02-01"))!.split(",")[1]);
    const imerg = datasets.find((d) => d.providerKind === "satellite")!;
    expect(imerg.observations.find((o) => o.date === "2000-02")?.value).toBeCloseTo(rate * 24 * 29, 2);
  });

  it("uses the documented Phoenix normals and 30 samples per month everywhere", () => {
    for (const item of Object.values(climate)) {
      const c = item as RegionalClimate;
      expect(toNormals(c)).toHaveLength(12);
      for (const counts of Object.values(c.baseline.counts)) expect(counts).toEqual(Array(12).fill(30));
    }
    const rain = baselines.baselines.find((b) => b.seriesId === "region_01.merra2.precipitation")!;
    expect(toNormals(climate.phoenix).map((m) => m.rain)).toEqual(rain.monthly.map((m) => m.mean));
    const bad = structuredClone(climate.phoenix); bad.baseline.units.rain = "mm/day";
    expect(() => toNormals(bad)).toThrow(/mm\/month/);
  });

  it("reports station gaps without inventing observations or redistribution permission", () => {
    const d = JSON.parse(readFileSync(resolve("data/processed/member4/region_01.station.USW00023183.relative_humidity.json"), "utf8"));
    expect(d.quality.missingMonths).toBe(314);
    expect(d.quality.longestMissingRunMonths).toBe(300);
    expect(d.provenance.redistribution).toBe("unknown");
  });

  it("distinguishes filed periods, data versions and time treatments", () => {
    const base = { datasetId: "one", productVersion: "v1", period: "1998–2025", mode: "annual" };
    expect(evidenceKey(base)).not.toBe(evidenceKey({ ...base, mode: "anomaly" }));
    expect(evidenceKey(base)).not.toBe(evidenceKey({ ...base, productVersion: "v2" }));
    expect(evidenceKey({ ...base, mode: "month", month: 0 })).not.toBe(evidenceKey({ ...base, mode: "month", month: 1 }));
  });
});

describe("deployment regression: unavailable inference", () => {
  it("renders the new strength and nullable paired p-value without fabricating a negative test", () => {
    expect(VERDICT["not-established"].text).toBe("Significance not established");
    const city = cityVsRegionWarming()!;
    expect(city.established).toBe(false);
    expect(city.p).toBeNull();
    expect(pText(city.p)).toBe("p unavailable");
  });
});
