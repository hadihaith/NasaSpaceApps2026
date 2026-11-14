import type { EvidenceDataset } from "./member3";
import { POWER_RAIN_UNIT, precipitationTotal } from "./power-precipitation";

export const CHILE_RAIN_SOURCE = {
  url: "https://power.larc.nasa.gov/api/temporal/monthly/point?parameters=PRECTOTCORR&community=AG&longitude=-72&latitude=-36&start=1991&end=2025&format=JSON",
  path: "data/raw/homecoming/central_chile_rain_1991_2025.json",
  retrievedAt: "2026-11-14T15:51:59Z",
  sha256: "c022d227ed89c0958c3ef9c9dd9c20d84a36127977f5cbc55b4055ca0d7c305e",
};

export const GUIDED_RAIN = {
  id: "rain-two-ways",
  type: "historical-comparison",
  question: "Did precipitation change at the same rate at the Sahel and central Chile points?",
  scope: "Same POWER parameter at two native-grid points. MERRA-2 precipitation corrections use different inputs in Africa and Chile. The result applies to the archived product values at those points.",
  startYear: 1991,
  endYear: 2025,
} as const;

export const PHOENIX_RAIN = {
  id: "phoenix-rain-cross-check",
  type: "product-cross-check",
  question: "How do two NASA precipitation products describe central Arizona?",
  scope: "Both products cover the same requested box. POWER uses 15 MERRA-2 cells (0.5° × 0.625°) and cosine weights. Giovanni averages the available 0.1° IMERG Final V07 cells. Native sampling, retrieval and correction differ. The 2025 IMERG endpoint is September, so the annual comparison stops at 2024.",
  startYear: 1998,
  endYear: 2024,
} as const;

interface ChileResponse {
  header: { api: { version: string }; sources: string[]; time_standard: string; fill_value: number; start: string; end: string };
  geometry: { coordinates: [number, number, number] };
  parameters: { PRECTOTCORR: { units: string } };
  properties: { parameter: { PRECTOTCORR: Record<string, number> } };
}

export function chileRainDataset(raw: unknown, rawSha256: string, sahelRain: EvidenceDataset): EvidenceDataset {
  const response = raw as ChileResponse;
  const { header, geometry } = response;
  if (rawSha256 !== CHILE_RAIN_SOURCE.sha256 || !header.sources.includes("MERRA2") ||
      header.start !== "19910101" || header.end !== "20251231" ||
      header.time_standard !== sahelRain.temporal.timeStandard ||
      response.parameters.PRECTOTCORR.units !== "mm/day" ||
      geometry.coordinates[0] !== -72 || geometry.coordinates[1] !== -36) {
    throw new Error("The Chile POWER response does not match the pinned source.");
  }
  const observations = Array.from({ length: 35 * 12 }, (_, i) => {
    const year = 1991 + Math.floor(i / 12);
    const month = i % 12 + 1;
    const mm = String(month).padStart(2, "0");
    const rate = response.properties.parameter.PRECTOTCORR[`${year}${mm}`];
    if (!Number.isFinite(rate) || rate === header.fill_value || rate < 0) {
      throw new Error(`Incomplete pinned Chile coverage at ${year}-${mm}.`);
    }
    return { date: `${year}-${mm}`, value: Math.round(precipitationTotal(rate, year, month) * 100) / 100 };
  });
  const start = observations[0].date;
  const end = observations.at(-1)!.date;
  const version = `POWER API ${header.api.version}; source ${header.sources.join(",")}`;
  return {
    ...sahelRain,
    id: `central-chile.merra2.precipitation.homecoming-v1-${rawSha256.slice(0, 12)}`,
    seriesId: "central-chile.merra2.precipitation",
    regionId: "central-chile",
    gameRegionId: "central-chile",
    region: "Central Chile POWER point (36°S, 72°W)",
    units: POWER_RAIN_UNIT,
    rawSha256,
    provenance: {
      ...sahelRain.provenance,
      version: `${version}; homecoming-v1; raw SHA-256 ${rawSha256}`,
      sourceUrl: CHILE_RAIN_SOURCE.url,
      retrievedAt: CHILE_RAIN_SOURCE.retrievedAt,
      processing: ["Selected one POWER point. Omitted POWER's annual month 13.",
        "Converted mm/day to mm/month using calendar days and rounded to 0.01 mm to match the Member 3 point archive."],
      limitations: ["Single POWER point estimate at 36°S, 72°W.",
        "Changes in inputs to the reanalysis can affect trends."],
    },
    source: {
      parameter: "PRECTOTCORR", nativeUnit: "mm/day", citation: sahelRain.source.citation,
      version, rawFile: CHILE_RAIN_SOURCE.path,
    },
    spatial: {
      type: "grid-point", nativeResolution: "0.5° latitude x 0.625° longitude",
      requestedCoordinates: { lat: -36, lon: -72 },
      returnedCoordinates: geometry.coordinates, gridCells: 1,
      footprintNote: "Single POWER point/grid estimate. Exact cell bounds were not supplied.",
    },
    temporal: { start, end, timeStandard: header.time_standard },
    quality: {
      missingMonths: 0, longestMissingRunMonths: 0, monthsWithMissingDays: 0,
      completeYears: Array.from({ length: 35 }, (_, i) => i + 1991), excludedAnnualYears: [],
    },
    observations,
  };
}
