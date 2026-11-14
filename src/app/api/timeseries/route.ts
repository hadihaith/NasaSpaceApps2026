import type { NextRequest } from "next/server";
import { REGION_SIZES, VARIABLES_BY_ID, type RegionSize } from "@/lib/catalog";
import { getMonthlySeries, PowerError } from "@/lib/power";
import { wrapLon } from "@/lib/region";

/**
 * GET /api/timeseries?variable=T2M&lat=78.2&lon=15.6&size=5
 *
 * Returns the monthly series for one variable, averaged over a box of `size`
 * degrees centred on the point (size 0 is the single nearest grid cell).
 */
export async function GET(request: NextRequest) {
  const params = request.nextUrl.searchParams;
  const variable = VARIABLES_BY_ID[params.get("variable") ?? ""];
  const lat = Number(params.get("lat"));
  const lon = Number(params.get("lon"));
  const size = Number(params.get("size") ?? 0) as RegionSize;

  if (!variable) return problem(400, "Unknown variable.");
  if (!Number.isFinite(lat) || Math.abs(lat) > 90) return problem(400, "Latitude must be between -90 and 90.");
  if (!Number.isFinite(lon)) return problem(400, "Longitude is missing.");
  if (!REGION_SIZES.includes(size)) return problem(400, `Region size must be one of ${REGION_SIZES.join(", ")}.`);

  // Round so nearby clicks share a cache entry; the grid is 0.5 degrees at best.
  const latR = Math.round(lat * 100) / 100;
  const lonR = Math.round(wrapLon(lon) * 100) / 100;

  // POWER publishes complete years. Early in January last year may not be out yet, so step back once.
  const lastYear = new Date().getUTCFullYear() - 1;
  for (const endYear of [lastYear, lastYear - 1]) {
    try {
      const series = await getMonthlySeries(variable.id, latR, lonR, size, variable.startYear, endYear);
      return Response.json(series, {
        headers: { "Cache-Control": "public, max-age=3600, stale-while-revalidate=86400" },
      });
    } catch (err) {
      const retryable = err instanceof PowerError && err.status === 422 && endYear === lastYear;
      if (retryable) continue;
      if (err instanceof PowerError) return problem(err.status === 504 ? 504 : 502, err.message);
      throw err;
    }
  }
  return problem(502, "NASA POWER has no data for that request.");
}

function problem(status: number, error: string) {
  return Response.json({ error }, { status });
}
