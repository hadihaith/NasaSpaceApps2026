import type { RegionSize } from "./catalog";

export interface BBox {
  latMin: number;
  latMax: number;
  lonMin: number;
  lonMax: number;
}

/**
 * The box of `size` degrees centred on a point, slid inward where it would
 * cross a pole or the antimeridian. Shared by the map and the data request so
 * the outline drawn is exactly the area averaged.
 */
export function regionBBox(lat: number, lon: number, size: RegionSize): BBox | null {
  if (size === 0) return null;
  // Edges snap to half degrees: that matches the data grid, and keeps the
  // box width exact in floating point (POWER rejects 10.000000000000002).
  const snap = (x: number) => Math.round(x * 2) / 2;
  const latMin = clamp(snap(lat - size / 2), -90, 90 - size);
  const lonMin = clamp(snap(lon - size / 2), -180, 180 - size);
  return { latMin, latMax: latMin + size, lonMin, lonMax: lonMin + size };
}

export function clamp(x: number, lo: number, hi: number): number {
  return Math.min(Math.max(x, lo), hi);
}

/** Wrap a longitude into [-180, 180). */
export function wrapLon(lon: number): number {
  return ((((lon + 180) % 360) + 360) % 360) - 180;
}

export function formatLatLon(lat: number, lon: number): string {
  const ns = lat >= 0 ? "N" : "S";
  const ew = lon >= 0 ? "E" : "W";
  return `${Math.abs(lat).toFixed(1)}°${ns}, ${Math.abs(lon).toFixed(1)}°${ew}`;
}
