import type { EarthDataset, ScientificReview } from "./dataset";

/** Deterministic teaching examples only. Never described as NASA measurements. */
function example(id: string, slope: number, correlated = false, missing = false): EarthDataset {
  let seed = 107;
  let memory = 0;
  const uniform = () => { seed = (1664525 * seed + 1013904223) >>> 0; return (seed + 1) / 4294967297; };
  const observations = Array.from({ length: 40 * 12 }, (_, i) => {
    const noise = Math.sqrt(-2 * Math.log(uniform())) * Math.cos(2 * Math.PI * uniform());
    memory = 0.9 * memory + noise;
    return { date: `${1980 + Math.floor(i / 12)}-${String(i % 12 + 1).padStart(2, "0")}`,
      value: missing && i >= 120 && i < 132 ? null : 15 + slope * i / 12 + 3 * Math.sin(2 * Math.PI * (i % 12) / 12) + (correlated ? memory : noise) };
  });
  return { schemaVersion: 1, id, variable: "Synthetic temperature", units: "°C", region: "Fictional teaching region",
    kind: "synthetic", provenance: { product: "Member 4 deterministic fixture", version: "1.0",
      sourceUrl: "synthetic:member4", retrievedAt: "Not applicable — generated locally", license: "Project-generated test values",
      redistribution: "permitted", processing: ["Linear term + seasonal sine + seeded Gaussian noise"],
      limitations: ["Not NASA data", "Does not establish any real environmental trend"] }, observations };
}

export const EXAMPLES = [example("rising", 0.08), example("falling", -0.08), example("no-trend", 0),
  example("correlated", 0.015, true), example("missing-year", 0.08, false, true)];

/** Only the independent-noise teaching examples have a known data-generating model. */
export function exampleReview(dataset: EarthDataset, mode: "annual" | "anomaly" | "month", month: number,
  startYear: number, endYear: number): ScientificReview | undefined {
  if (!EXAMPLES.includes(dataset) || ["correlated", "missing-year"].includes(dataset.id)) return undefined;
  return { reviewer: "Member 4 synthetic fixture specification", rationale: "Known linear mean and independent Gaussian innovations; annual means or a fixed calendar month remove the deterministic seasonal term. Screening may still flag a finite realization.",
    datasetId: dataset.id, productVersion: dataset.provenance.version, mode, month, startYear, endYear,
    assumptionsReviewed: mode !== "anomaly" };
}
