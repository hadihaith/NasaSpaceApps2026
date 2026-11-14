/**
 * Inferences from the bundled Phoenix records (public/data/earth-observations.json).
 *
 * Server-side only: it reads the full observations file (about 300 KB), runs the
 * repo's trend tests on yearly values, and returns small summaries for the page.
 * Every sentence the page shows is built from these numbers; nothing is hand-written
 * about what the data "show".
 */
import observations from "../../../public/data/earth-observations.json";
import { analyzeTrend, comparePairedSeries, type TrendResult } from "../stats/trend";
import type { SeriesPoint } from "../series";

export type Kind = "reanalysis" | "satellite" | "station";

interface ObservationSeries {
  id: string;
  variable: string;
  label: string;
  unit: string;
  kind: Kind;
  definition: string;
  source: { agency: string; dataset: string; version: string };
  temporal: { start: string; end: string };
  caveats: string[];
  observations: { date: string; value: number | null }[];
}

const SERIES = (observations as unknown as { series: ObservationSeries[] }).series;

export interface SeriesSummary {
  id: string;
  variable: string;
  label: string;
  kind: Kind;
  definition: string;
  source: string;
  caveats: string[];
  /** Unit of the yearly values (mm/month series become mm/year totals) */
  unit: string;
  firstYear: number;
  lastYear: number;
  points: SeriesPoint[];
  trend: TrendResult;
}

/** One value per complete calendar year: the mean, or the total for precipitation. */
function yearly(s: ObservationSeries): { points: SeriesPoint[]; unit: string } {
  const byYear = new Map<number, number[]>();
  for (const o of s.observations) {
    if (o.value === null) continue;
    const y = Number(o.date.slice(0, 4));
    const list = byYear.get(y) ?? [];
    list.push(o.value);
    byYear.set(y, list);
  }
  const total = s.unit === "mm/month";
  const points: SeriesPoint[] = [];
  for (const [year, values] of [...byYear.entries()].sort((a, b) => a[0] - b[0])) {
    // A year with a missing month would be biased toward the seasons it has.
    if (values.length !== 12) continue;
    const sum = values.reduce((a, b) => a + b, 0);
    points.push({ t: year, v: total ? sum : sum / 12, label: String(year) });
  }
  return { points, unit: total ? "mm/year" : s.unit };
}

let cache: SeriesSummary[] | null = null;

export function seriesSummaries(): SeriesSummary[] {
  if (cache) return cache;
  cache = SERIES.flatMap((s) => {
    const { points, unit } = yearly(s);
    const trend = analyzeTrend(points);
    if (!trend) return [];
    return [{
      id: s.id,
      variable: s.variable,
      label: s.label,
      kind: s.kind,
      definition: s.definition,
      source: `${s.source.agency} ${s.source.dataset} (${s.source.version})`,
      caveats: s.caveats,
      unit,
      firstYear: points[0].t,
      lastYear: points[points.length - 1].t,
      points,
      trend,
    }];
  });
  return cache;
}

function byId(id: string) {
  const s = seriesSummaries().find((x) => x.id === id);
  if (!s) throw new Error(`No series ${id}`);
  return s;
}

/** Restrict a summary to a range of years and re-run the tests. */
function between(s: SeriesSummary, from: number, to: number) {
  const points = s.points.filter((p) => p.t >= from && p.t <= to);
  return { points, trend: analyzeTrend(points) };
}

export interface CityVsRegion {
  from: number;
  to: number;
  cityPerDecade: number;
  regionPerDecade: number;
  differencePerDecade: number;
  p: number | null;
  significant: boolean;
  established: boolean;
  reason: string;
}

/** Is the Phoenix Airport station warming faster than the region as a whole? */
export function cityVsRegionWarming(): CityVsRegion | null {
  const city = byId("region_01.station.USW00023183.air_temperature_mean");
  const region = byId("region_01.merra2.air_temperature_2m");
  const from = Math.max(city.firstYear, region.firstYear);
  const to = Math.min(city.lastYear, region.lastYear);
  const a = between(city, from, to).trend;
  const b = between(region, from, to).trend;
  if (!a || !b) return null;
  const diff = comparePairedSeries(between(city, from, to).points, between(region, from, to).points);
  return {
    from,
    to,
    cityPerDecade: a.ols.slope * 10,
    regionPerDecade: b.ols.slope * 10,
    differencePerDecade: diff.difference * 10,
    p: diff.p,
    significant: diff.significant,
    established: diff.established,
    reason: diff.reason,
  };
}

export interface RainRecord {
  id: string;
  label: string;
  kind: Kind;
  meanPerYear: number;
  perDecade: number;
  strength: TrendResult["strength"];
  direction: TrendResult["direction"];
}

/** The three precipitation records over the years they all cover. */
export function rainAgreement(): { from: number; to: number; records: RainRecord[] } {
  const ids = [
    "region_01.imerg.precipitation",
    "region_01.merra2.precipitation",
    "region_01.station.USW00023183.precipitation",
  ];
  const all = ids.map(byId);
  const from = Math.max(...all.map((s) => s.firstYear));
  const to = Math.min(...all.map((s) => s.lastYear));
  const records = all.flatMap((s) => {
    const w = between(s, from, to);
    if (!w.trend) return [];
    return [{
      id: s.id,
      label: s.label,
      kind: s.kind,
      meanPerYear: w.trend.mean,
      perDecade: w.trend.sen.slope * 10,
      strength: w.trend.strength,
      direction: w.trend.direction,
    }];
  });
  return { from, to, records };
}

export interface Relationship {
  a: string;
  b: string;
  aLabel: string;
  bLabel: string;
  n: number;
  r: number;
  /** Descriptive correlation only; no reviewed dependence model is available. */
  p: null;
  from: number;
  to: number;
}

/**
 * Correlation between the year-to-year ups and downs of two series, after removing
 * each one's straight-line trend, so a shared trend does not create a false link.
 */
function relationship(aId: string, bId: string): Relationship | null {
  const a = byId(aId);
  const b = byId(bId);
  const bMap = new Map(b.points.map((p) => [p.t, p.v]));
  const pairs = a.points.filter((p) => bMap.has(p.t)).map((p) => [p.t, p.v, bMap.get(p.t)!] as const);
  if (pairs.length < 10) return null;
  const detrend = (ts: number[], vs: number[]) => {
    const tr = analyzeTrend(ts.map((t, i) => ({ t, v: vs[i] })))!;
    return vs.map((v, i) => v - (tr.ols.intercept + tr.ols.slope * ts[i]));
  };
  const ts = pairs.map((p) => p[0]);
  const xa = detrend(ts, pairs.map((p) => p[1]));
  const xb = detrend(ts, pairs.map((p) => p[2]));
  const n = xa.length;
  const sxy = xa.reduce((acc, v, i) => acc + v * xb[i], 0);
  const sxx = xa.reduce((acc, v) => acc + v * v, 0);
  const syy = xb.reduce((acc, v) => acc + v * v, 0);
  const r = sxy / Math.sqrt(sxx * syy);
  return {
    a: aId,
    b: bId,
    aLabel: a.label,
    bLabel: b.label,
    n,
    r,
    p: null,
    from: ts[0],
    to: ts[ts.length - 1],
  };
}

export function relationships(): Relationship[] {
  return [
    ["region_01.merra2.precipitation", "region_01.merra2.root_zone_soil_wetness"],
    ["region_01.imerg.precipitation", "region_01.merra2.root_zone_soil_wetness"],
    ["region_01.merra2.air_temperature_2m", "region_01.merra2.relative_humidity_2m"],
    ["region_01.merra2.precipitation", "region_01.merra2.air_temperature_2m"],
  ].flatMap(([a, b]) => {
    const rel = relationship(a, b);
    return rel ? [rel] : [];
  });
}
