import type { NextRequest } from "next/server";

/**
 * GET /api/tiles/{layer}/{z}/{y}/{x}
 *
 * Passes globe tiles through from NASA GIBS. GIBS answers a share of requests
 * with a 500 that succeeds on retry, and the map library does not retry, so
 * this route retries for it and lets the browser cache the result.
 */
const GIBS = "https://gibs.earthdata.nasa.gov/wmts/epsg3857/best";

const LAYERS: Record<string, { path: string; ext: string; type: string; maxZoom: number }> = {
  bluemarble: {
    path: "BlueMarble_ShadedRelief_Bathymetry/default/GoogleMapsCompatible_Level8",
    ext: "jpeg",
    type: "image/jpeg",
    maxZoom: 8,
  },
};

const ATTEMPTS = 4;

export async function GET(_request: NextRequest, ctx: RouteContext<"/api/tiles/[layer]/[z]/[y]/[x]">) {
  const params = await ctx.params;
  const layer = LAYERS[params.layer];
  const [z, y, x] = [params.z, params.y, params.x].map(Number);
  const valid =
    layer &&
    [z, y, x].every(Number.isInteger) &&
    z >= 0 &&
    z <= layer.maxZoom &&
    x >= 0 &&
    y >= 0 &&
    x < 2 ** z &&
    y < 2 ** z;
  if (!valid) return new Response(null, { status: 404 });

  const url = `${GIBS}/${layer.path}/${z}/${y}/${x}.${layer.ext}`;
  for (let attempt = 0; attempt < ATTEMPTS; attempt++) {
    try {
      const res = await fetch(url, { signal: AbortSignal.timeout(15_000) });
      if (res.ok) {
        return new Response(res.body, {
          headers: {
            "Content-Type": layer.type,
            "Cache-Control": "public, max-age=604800, immutable",
          },
        });
      }
      // A 4xx will not improve on retry.
      if (res.status < 500) break;
    } catch {
      // Network error or timeout: try again.
    }
    await new Promise((resolve) => setTimeout(resolve, 150 * (attempt + 1)));
  }
  return new Response(null, { status: 404 });
}
