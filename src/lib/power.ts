/**
 * NASA POWER client (server only).
 *
 * POWER serves MERRA-2 and CERES/SRB variables as ready-made monthly time
 * series for a point or a small region, with no API key. It is the same family
 * of data the challenge names, without the Earthdata login that Giovanni and
 * AppEEARS need. Docs: https://power.larc.nasa.gov/docs/services/api/temporal/monthly/
 *
 * To add another source (Giovanni, AppEEARS), return the same MonthlySeries shape.
 */
import { createHash } from "node:crypto";
import type { RegionSize } from "./catalog";
import { regionBBox, type BBox } from "./region";

const BASE = "https://power.larc.nasa.gov/api/temporal/monthly";

export interface MonthlySeries {
  variableId: string;
  lat: number;
  lon: number;
  size: RegionSize;
  bbox: BBox | null;
  /** Number of grid cells averaged */
  cells: number;
  startYear: number;
  endYear: number;
  /** One value per month from January of startYear; null where data are missing */
  monthly: (number | null)[];
  source: {
    url: string;
    version: string | null;
    products: string[];
    nativeUnit: string | null;
    timeStandard: string | null;
    retrievedAt: string;
    sha256: string;
  };
  footprint: { longitude: number; latitude: number; weight: number }[];
}

export class PowerError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
  }
}

interface PowerFeature {
  geometry: { coordinates: [number, number, number] };
  properties: { parameter: Record<string, Record<string, number>> };
}

// POWER can revise historical data. Expire cached responses and share in-flight
// requests for the same variable, window and footprint.
const CACHE_LIMIT = 200;
const CACHE_MS = 3_600_000;
const cache = new Map<string, { pending: Promise<MonthlySeries>; expires: number }>();

/** Monthly series for a point or box. */
export function getMonthlySeries(
  variableId: string,
  lat: number,
  lon: number,
  size: RegionSize,
  startYear: number,
  endYear: number,
): Promise<MonthlySeries> {
  const key = [variableId, lat, lon, size, startYear, endYear].join("|");
  const hit = cache.get(key);
  if (hit && hit.expires > Date.now()) return hit.pending;
  const pending = fetchMonthlySeries(variableId, lat, lon, size, startYear, endYear);
  cache.delete(key);
  cache.set(key, { pending, expires: Date.now() + CACHE_MS });
  pending.catch(() => {
    if (cache.get(key)?.pending === pending) cache.delete(key);
  });
  if (cache.size > CACHE_LIMIT) cache.delete(cache.keys().next().value!);
  return pending;
}

async function fetchMonthlySeries(
  variableId: string,
  lat: number,
  lon: number,
  size: RegionSize,
  startYear: number,
  endYear: number,
): Promise<MonthlySeries> {
  const bbox = regionBBox(lat, lon, size);
  const common = `parameters=${variableId}&community=AG&start=${startYear}&end=${endYear}&format=JSON`;
  const url = bbox
    ? `${BASE}/regional?${common}&latitude-min=${bbox.latMin}&latitude-max=${bbox.latMax}&longitude-min=${bbox.lonMin}&longitude-max=${bbox.lonMax}`
    : `${BASE}/point?${common}&latitude=${lat}&longitude=${lon}`;

  let res = await requestPower(url);
  if (!res || res.status >= 500) {
    await new Promise((resolve) => setTimeout(resolve, 300));
    res = await requestPower(url);
  }
  if (!res) throw new PowerError("NASA POWER did not respond. Try again in a moment.", 504);
  const bytes = await res.arrayBuffer().catch(() => new ArrayBuffer(0));
  const raw = Buffer.from(bytes);
  const body = parseResponse(raw.toString("utf8"));
  if (!res.ok) {
    const detail = Array.isArray(body?.messages) ? body.messages.join(" ") : "";
    throw new PowerError(detail || `NASA POWER returned ${res.status}.`, res.status);
  }

  if (!body) throw new PowerError("NASA POWER returned an unreadable response.", 502);
  const fill = body.header?.fill_value ?? -999;
  const features = bbox ? body.features : [body];
  if (!Array.isArray(features) || !features.length || features.some((feature) => !validFeature(feature, variableId))) {
    throw new PowerError("NASA POWER did not return valid grid coverage for that parameter.", 502);
  }
  const months = (endYear - startYear + 1) * 12;
  const sum = new Float64Array(months);
  const weight = new Float64Array(months);
  let totalWeight = 0;

  const footprint = features.map((feature) => {
    const values = feature.properties.parameter[variableId];
    const [longitude, latitude] = feature.geometry.coordinates;
    // Grid cells shrink toward the poles, so weight each by the cosine of its latitude.
    const w = bbox ? Math.max(Math.cos(latitude * Math.PI / 180), 1e-6) : 1;
    totalWeight += w;
    for (let i = 0; i < months; i++) {
      const key = `${startYear + Math.floor(i / 12)}${String((i % 12) + 1).padStart(2, "0")}`;
      const v = values[key];
      if (v === undefined || v === fill || !Number.isFinite(v)) continue;
      sum[i] += w * v;
      weight[i] += w;
    }
    return { longitude, latitude, weight: w };
  });

  // Keep a month only when at least half of the region reported.
  const monthly = Array.from(sum, (s, i) =>
    weight[i] > 0 && weight[i] >= totalWeight / 2 ? s / weight[i] : null,
  );

  if (monthly.every((value) => value === null)) {
    throw new PowerError("NASA POWER reported no usable months for this request.", 422);
  }
  return { variableId, lat, lon, size, bbox, cells: features.length, startYear, endYear, monthly, footprint, source: {
    url,
    version: body.header?.api?.version ?? null,
    products: Array.isArray(body.header?.sources) ? body.header.sources : [],
    nativeUnit: body.parameters?.[variableId]?.units ?? null,
    timeStandard: body.header?.time_standard ?? null,
    retrievedAt: new Date().toISOString(),
    sha256: createHash("sha256").update(raw).digest("hex"),
  } };
}

function requestPower(url: string) {
  return fetch(url, { signal: AbortSignal.timeout(25_000) }).catch(() => null);
}

type PowerBody = PowerFeature & {
  features?: PowerFeature[];
  messages?: string[];
  header?: { fill_value?: number; api?: { version?: string }; sources?: string[]; time_standard?: string };
  parameters?: Record<string, { units?: string }>;
};

function parseResponse(bytes: string): PowerBody | null {
  try { return JSON.parse(bytes); }
  catch { return null; }
}

function validFeature(feature: PowerFeature | null, variableId: string) {
  const coordinates = feature?.geometry?.coordinates;
  const values = feature?.properties?.parameter?.[variableId];
  return coordinates && Number.isFinite(coordinates[0]) && Math.abs(coordinates[0]) <= 180 &&
    Number.isFinite(coordinates[1]) && Math.abs(coordinates[1]) <= 90 &&
    values && typeof values === "object" && !Array.isArray(values);
}
