import { analyse } from "../analysis";
import type { Mode, MonthlyRecord } from "../series";

/** Member 3 supplies this portable JSON document; no provider SDK is needed. */
export interface EarthDataset {
  schemaVersion: 1;
  id: string;
  variable: string;
  units: string;
  region: string;
  kind: "observation" | "reanalysis" | "synthetic" | "simulation";
  provenance: {
    product: string;
    version: string;
    sourceUrl: string;
    retrievedAt: string;
    license: string;
    redistribution: "permitted" | "unknown" | "restricted";
    processing: string[];
    limitations: string[];
  };
  /** Every calendar month must appear, in order; missing values use null. */
  observations: { date: string; value: number | null }[];
}

export interface ScientificReview {
  reviewer: string;
  rationale: string;
  datasetId: string;
  productVersion: string;
  mode: Mode;
  month?: number;
  startYear: number;
  endYear: number;
  assumptionsReviewed: boolean;
}

export function parseDataset(input: unknown): EarthDataset {
  if (!input || typeof input !== "object") throw new Error("Expected a dataset object.");
  const d = input as EarthDataset;
  const text = (v: unknown) => typeof v === "string" && v.trim().length > 0;
  const texts = (v: unknown) => Array.isArray(v) && v.every(text);
  if (d.schemaVersion !== 1 || ![d.id, d.variable, d.units, d.region].every(text)) {
    throw new Error("schemaVersion 1, id, variable, units and region are required.");
  }
  if (!["observation", "reanalysis", "synthetic", "simulation"].includes(d.kind)) throw new Error("Invalid data kind.");
  const p = d.provenance;
  if (!p || ![p.product, p.version, p.sourceUrl, p.retrievedAt, p.license].every(text) ||
      !["permitted", "unknown", "restricted"].includes(p.redistribution) ||
      !texts(p.processing) || !texts(p.limitations)) throw new Error("Complete provenance is required; use UNKNOWN for unverified facts.");
  if (!/^https?:\/\//.test(p.sourceUrl) && !p.sourceUrl.startsWith("synthetic:")) throw new Error("Use an http(s) source URL or synthetic: identifier.");
  if (!Array.isArray(d.observations) || !d.observations.length || d.observations.length > 2400) {
    throw new Error("Supply 1–2400 monthly observations for this browser module.");
  }
  let previous: number | undefined;
  for (const point of d.observations) {
    if (!point || typeof point.date !== "string" || !/^\d{4}-(0[1-9]|1[0-2])$/.test(point.date)) throw new Error("Dates must use YYYY-MM.");
    const [year, month] = point.date.split("-").map(Number);
    const current = year * 12 + month - 1;
    if (previous !== undefined && current !== previous + 1) throw new Error("Dates must be unique, ascending and consecutive; fill missing months with null.");
    if (point.value !== null && (typeof point.value !== "number" || !Number.isFinite(point.value))) throw new Error("Values must be finite numbers or null.");
    previous = current;
  }
  return d;
}

export function analyzeDataset(input: unknown, mode: Mode = "annual", month = 0,
  period?: { startYear: number; endYear: number }, review?: ScientificReview,
  anomalyNormals?: MonthlyRecord["anomalyNormals"]) {
  const dataset = parseDataset(input);
  const firstYear = Number(dataset.observations[0].date.slice(0, 4));
  const lastYear = Number(dataset.observations.at(-1)!.date.slice(0, 4));
  const startYear = period?.startYear ?? firstYear;
  const endYear = period?.endYear ?? lastYear;
  if (!Number.isInteger(startYear) || !Number.isInteger(endYear) || startYear > endYear || startYear < firstYear || endYear > lastYear) {
    throw new Error("Selected years must be inside the dataset coverage.");
  }
  const monthly: (number | null)[] = Array((endYear - startYear + 1) * 12).fill(null);
  for (const point of dataset.observations) {
    const [year, m] = point.date.split("-").map(Number);
    if (year >= startYear && year <= endYear) monthly[(year - startYear) * 12 + m - 1] = point.value;
  }
  const reviewed = review?.assumptionsReviewed === true && review.datasetId === dataset.id &&
    review.productVersion === dataset.provenance.version && review.mode === mode &&
    (mode !== "month" || review.month === month) && review.startYear === startYear && review.endYear === endYear &&
    Boolean(review.reviewer.trim() && review.rationale.trim()) && dataset.kind !== "simulation";
  const record: MonthlyRecord = { startYear, monthly, assumptionsReviewed: reviewed, anomalyNormals };
  const result = analyse(record, mode, month);
  const warnings = [...(result.trend?.inference.reasons ?? ["Fewer than ten usable points; significance not established."])];
  if (dataset.kind === "synthetic" || dataset.kind === "simulation") warnings.unshift("Generated values: not observed Earth changes.");
  if (monthly.includes(null)) warnings.push("Missing months are not interpolated; incomplete years are excluded from annual means.");
  if (dataset.provenance.redistribution !== "permitted") warnings.push("Redistribution is not confirmed; do not bundle or publish this dataset.");
  return { dataset, record, ...result, warnings, startYear, endYear,
    missingMonths: monthly.filter((v) => v === null).length };
}
