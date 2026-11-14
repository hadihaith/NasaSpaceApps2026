import type { NextRequest } from "next/server";
import { REGION_SIZES, VARIABLES_BY_ID, type RegionSize } from "@/lib/catalog";
import { connectedHistory } from "@/lib/earth/connected-history";
import { getMonthlySeries, PowerError } from "@/lib/power";
import { wrapLon } from "@/lib/region";
import { MODES, type Mode } from "@/lib/series";

/**
 * GET /api/timeseries?variable=T2M&lat=78.2&lon=15.6&size=5
 *
 * Returns the monthly series for one variable, averaged over a box of `size`
 * degrees centred on the point (size 0 is the single nearest grid cell).
 */
export async function GET(request: NextRequest) {
  const params = request.nextUrl.searchParams;
  const variableId = params.get("variable") ?? "";
  const variable = Object.hasOwn(VARIABLES_BY_ID, variableId) ? VARIABLES_BY_ID[variableId] : undefined;
  const rawLat = params.get("lat");
  const rawLon = params.get("lon");
  const lat = Number(rawLat);
  const lon = Number(rawLon);
  const size = Number(params.get("size") ?? 0) as RegionSize;

  if (!variable) return problem(400, "Unknown variable.");
  if (!rawLat?.trim() || !Number.isFinite(lat) || Math.abs(lat) > 90) return problem(400, "Latitude must be between -90 and 90.");
  if (!rawLon?.trim() || !Number.isFinite(lon)) return problem(400, "Longitude must be a finite number.");
  if (!REGION_SIZES.includes(size)) return problem(400, `Region size must be one of ${REGION_SIZES.join(", ")}.`);

  const format = params.get("format");
  if (format !== null && format !== "evidence") return problem(400, "Use format=evidence for a historical finding.");
  const mode = (params.get("mode") ?? "annual") as Mode;
  const month = Number(params.get("month") ?? "1");
  if (!MODES.some(({ id }) => id === mode)) return problem(400, "Mode must be annual, month or anomaly.");
  if (!Number.isInteger(month) || month < 1 || month > 12) return problem(400, "Month must be 1 through 12.");

  // Round so nearby clicks share a cache entry; the grid is 0.5 degrees at best.
  const latR = Math.round(lat * 100) / 100;
  const roundedLon = Math.round(wrapLon(lon) * 100) / 100;
  const lonR = roundedLon === 180 ? -180 : roundedLon;

  // POWER publishes complete years. Early in January last year may not be out yet, so step back once.
  const lastYear = new Date().getUTCFullYear() - 1;
  const rawEnd = params.get("endYear");
  const startYear = Number(params.get("startYear") ?? variable.startYear);
  const requestedEnd = Number(rawEnd ?? lastYear);
  if (!Number.isInteger(startYear) || !Number.isInteger(requestedEnd) ||
      startYear < variable.startYear || startYear > requestedEnd || requestedEnd > lastYear) {
    return problem(400, `Years must be between ${variable.startYear} and ${lastYear}, in order.`);
  }
  const endYears = rawEnd === null && requestedEnd > startYear ? [requestedEnd, requestedEnd - 1] : [requestedEnd];
  for (const endYear of endYears) {
    try {
      const series = await getMonthlySeries(variable.id, latR, lonR, size, startYear, endYear);
      return Response.json(format === "evidence" ? connectedHistory(series, variable, mode, month - 1) : series, {
        headers: { "Cache-Control": "public, max-age=3600, stale-while-revalidate=86400" },
      });
    } catch (err) {
      const retryable = err instanceof PowerError && err.status === 422 && endYears.length > 1 && endYear === requestedEnd;
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
