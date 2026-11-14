import type { Variable } from "../catalog";
import { PowerError, type MonthlySeries } from "../power";
import type { Mode } from "../series";
import { analyzeHistoricalRecord, HISTORICAL_SCHEMA_VERSION } from "./historical";
import type { EvidenceDataset } from "./member3";
import { POWER_RAIN_UNIT, precipitationTotal } from "./power-precipitation";

export function connectedHistory(record: MonthlySeries, variable: Variable, mode: Mode, month: number) {
  const rain = variable.id === "PRECTOTCORR";
  if (rain && record.source.nativeUnit !== "mm/day") {
    throw new PowerError("NASA POWER did not confirm the precipitation unit.", 502);
  }
  const reanalysis = record.source.products.includes("MERRA2") || variable.origin === "MERRA-2 reanalysis";
  const provider = reanalysis ? "reanalysis" : "satellite";
  const units = rain ? POWER_RAIN_UNIT : variable.unit || record.source.nativeUnit || "1";
  const { startYear, endYear } = record;
  const bbox = record.bbox;
  const point = bbox ? null : record.footprint[0];
  const region = bbox
    ? `POWER cells within ${bbox.latMin}°, ${bbox.lonMin}° to ${bbox.latMax}°, ${bbox.lonMax}° (latitude, longitude)`
    : `POWER point at ${record.footprint[0].latitude}°, ${record.footprint[0].longitude}° (latitude, longitude)`;
  const resolution = reanalysis ? "0.5° latitude x 0.625° longitude" : undefined;
  const version = `POWER API ${record.source.version ?? "UNKNOWN"}; source ${record.source.products.join(",") || variable.origin}; connected-v1`;
  const observations = record.monthly.map((value, i) => {
    const year = startYear + Math.floor(i / 12);
    const m = i % 12 + 1;
    return {
      date: `${year}-${String(m).padStart(2, "0")}`,
      value: rain && value !== null ? precipitationTotal(value, year, m) : value,
    };
  });
  const years = Array.from({ length: endYear - startYear + 1 }, (_, offset) => offset + startYear);
  const completeYears = years.filter((_, i) => record.monthly.slice(i * 12, i * 12 + 12).every((v) => v !== null));
  let longestMissingRunMonths = 0;
  let run = 0;
  for (const { value } of observations) {
    run = value === null ? run + 1 : 0;
    longestMissingRunMonths = Math.max(longestMissingRunMonths, run);
  }
  const seriesId = `power.${variable.id}.${record.lat}.${record.lon}.${record.size}`;
  const dataset: EvidenceDataset = {
    schemaVersion: 1,
    id: `${seriesId}.${startYear}.${endYear}.${record.source.sha256.slice(0, 16)}`,
    seriesId, region, regionId: "custom", gameRegionId: "custom", gameVariable: null,
    kind: reanalysis ? "reanalysis" : "observation", providerKind: provider, variable: variable.name,
    definition: rain ? "Water equivalent of precipitation, including rain and snow." : variable.definition,
    units, annualInterpretation: rain ? "Equal-month mean of 12 complete monthly totals (mm/month)"
      : "Equal-month arithmetic mean of 12 complete monthly values",
    provenance: {
      product: `${variable.origin} via NASA POWER`, version, sourceUrl: record.source.url,
      retrievedAt: record.source.retrievedAt,
      license: "NASA open Earth-science data. Acknowledge NASA POWER and the source products.",
      redistribution: "permitted",
      processing: [
        bbox ? "Cosine-latitude weighted mean. A month requires at least half the returned grid weight."
          : "Single POWER point estimate.",
        ...(rain ? ["Converted mm/day to mm/month using calendar days."] : []),
        "Kept fill values as missing and omitted POWER annual month 13.",
      ],
      limitations: [
        ...(point ? ["The response supplies point coordinates. Exact source cell bounds are unavailable."] : []),
        ...(variable.caveat ? [variable.caveat] : []),
      ],
    },
    rawSha256: record.source.sha256,
    source: { parameter: variable.id, version, nativeUnit: record.source.nativeUnit ?? undefined,
      citation: ["NASA Langley Research Center POWER Project, https://power.larc.nasa.gov/docs/acknowledgements/"] },
    spatial: {
      type: bbox ? "area-mean" : "grid-point", nativeResolution: resolution, gridCells: record.cells,
      bbox: bbox ?? undefined,
      requestedCoordinates: { lat: record.lat, lon: record.lon },
      gridCellCenters: record.footprint.map(({ longitude, latitude }) => [longitude, latitude]),
    },
    temporal: { start: observations[0].date, end: observations.at(-1)!.date,
      timeStandard: record.source.timeStandard ?? undefined },
    observations,
    quality: {
      missingMonths: observations.filter(({ value }) => value === null).length,
      longestMissingRunMonths, completeYears,
      excludedAnnualYears: years.filter((year) => !completeYears.includes(year)), monthsWithMissingDays: 0,
    },
  };
  const { observations: monthly, ...metadata } = dataset;
  const bounds = bbox ? [bbox.lonMin, bbox.latMin, bbox.lonMax, bbox.latMax] : null;
  return {
    schemaVersion: HISTORICAL_SCHEMA_VERSION,
    dataset: metadata,
    footprint: { type: dataset.spatial.type, bbox: bounds, point, cells: bbox ? record.footprint : [] },
    observations: monthly.map(({ date, value }) => ({ date, value, hasValue: value !== null })),
    finding: analyzeHistoricalRecord(dataset, { startYear, endYear }, mode, month),
  };
}
