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

// Past months never change, so keep recent answers in memory. Failed requests
// are dropped so they can be retried.
const CACHE_LIMIT = 200;
const cache = new Map<string, Promise<MonthlySeries>>();

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
  if (hit) return hit;
  const pending = fetchMonthlySeries(variableId, lat, lon, size, startYear, endYear);
  cache.set(key, pending);
  pending.catch(() => cache.delete(key));
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

  let res: Response;
  try {
    res = await fetch(url, { signal: AbortSignal.timeout(60_000) });
  } catch {
    throw new PowerError("NASA POWER did not respond. Try again in a moment.", 504);
  }
  const body = await res.json().catch(() => null);
  if (!res.ok || !body) {
    const detail = Array.isArray(body?.messages) ? body.messages.join(" ") : "";
    throw new PowerError(detail || `NASA POWER returned ${res.status}.`, res.status || 502);
  }

  const fill: number = body.header?.fill_value ?? -999;
  const features: PowerFeature[] = bbox ? body.features : [body];
  const months = (endYear - startYear + 1) * 12;
  const sum = new Float64Array(months);
  const weight = new Float64Array(months);
  let totalWeight = 0;

  for (const feature of features) {
    const values = feature.properties.parameter[variableId];
    if (!values) continue;
    // Grid cells shrink toward the poles, so weight each by the cosine of its latitude.
    const w = Math.max(Math.cos((feature.geometry.coordinates[1] * Math.PI) / 180), 1e-6);
    totalWeight += w;
    for (let i = 0; i < months; i++) {
      const key = `${startYear + Math.floor(i / 12)}${String((i % 12) + 1).padStart(2, "0")}`;
      const v = values[key];
      if (v === undefined || v === fill || !Number.isFinite(v)) continue;
      sum[i] += w * v;
      weight[i] += w;
    }
  }

  // Keep a month only when at least half of the region reported.
  const monthly = Array.from(sum, (s, i) =>
    weight[i] > 0 && weight[i] >= totalWeight / 2 ? s / weight[i] : null,
  );

  return { variableId, lat, lon, size, bbox, cells: features.length, startYear, endYear, monthly };
}
