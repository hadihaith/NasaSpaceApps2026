import { analyzeDataset, parseDataset, type EarthDataset, type ScientificReview } from "../member4/dataset";
import type { BBox } from "../region";
import type { Mode, MonthlyRecord } from "../series";

/** Export extensions retain the source distinctions Member 4's broad kind does not. */
export interface EvidenceDataset extends EarthDataset {
  seriesId: string;
  regionId: string;
  gameRegionId: string;
  gameVariable: string | null;
  providerKind: "station" | "satellite" | "reanalysis";
  definition: string;
  annualInterpretation: string;
  rawSha256: string;
  source: { citation: string[]; nativeUnit?: string; doi?: string; parameter: string; rawFile?: string; version: string };
  spatial: {
    type: string;
    nativeResolution?: string;
    footprintNote?: string;
    bbox?: BBox;
    dataBbox?: BBox;
    gridCells?: number;
    gridCellCenters?: [number, number][];
    requestedCoordinates?: { lat: number; lon: number };
    returnedCoordinates?: [number, number, number];
    method?: string;
  };
  temporal: { start: string; end: string; timeStandard?: string };
  quality: {
    missingMonths: number;
    longestMissingRunMonths: number;
    completeYears: number[];
    excludedAnnualYears: number[];
    monthsWithMissingDays: number;
  };
}

export const PROVIDER_LABEL = {
  reanalysis: "NASA model reanalysis", satellite: "Gauge-calibrated satellite estimate", station: "Ground station measurement",
} as const;

export function parseArchive(input: unknown): EvidenceDataset[] {
  const bundle = input as { schemaVersion?: number; datasets?: unknown[] } | null;
  if (bundle?.schemaVersion !== 1 || !Array.isArray(bundle.datasets) || !bundle.datasets.length) throw new Error("Archive records are unavailable.");
  return bundle.datasets.map((value) => {
    const d = parseDataset(value) as EvidenceDataset;
    if (!["reanalysis", "observation"].includes(d.kind) || d.provenance.redistribution !== "permitted" ||
        !["reanalysis", "station", "satellite"].includes(d.providerKind) ||
        !d.gameRegionId || !d.seriesId || !d.quality || !d.temporal || !d.source?.citation || !d.spatial) {
      throw new Error("Archive requires documented real records with permitted redistribution.");
    }
    return d;
  });
}

/** Pass a dataset-specific review only after matching the requested time treatment. */
export function analyzeEvidence(dataset: EvidenceDataset, mode: Mode, startYear: number, endYear: number,
  month = 0, review?: ScientificReview, anomalyNormals?: MonthlyRecord["anomalyNormals"]) {
  return analyzeDataset(dataset, mode, month, { startYear, endYear }, review, anomalyNormals);
}

export function evidenceKey(entry: { datasetId?: string; productVersion?: string; mode?: string; month?: number; period: string }) {
  return JSON.stringify([entry.datasetId, entry.productVersion, entry.mode, entry.month, entry.period]);
}
