"use client";

import { useEffect, useState } from "react";
import type { RegionSize } from "./catalog";
import type { MonthlySeries } from "./power";

export type SeriesState =
  | { status: "loading" }
  | { status: "error"; error: string; retry: () => void }
  | { status: "ready"; record: MonthlySeries };

type Result = { key: string; record: MonthlySeries } | { key: string; error: string };

/**
 * The monthly record for one variable at one place, from /api/timeseries.
 * Pass a null place to fetch nothing, which is how the optional second place
 * stays idle.
 */
export function useSeries(
  variableId: string,
  place: { lat: number; lon: number } | null,
  size: RegionSize,
): SeriesState | null {
  const [result, setResult] = useState<Result | null>(null);
  const [attempt, setAttempt] = useState(0);
  const lat = place?.lat;
  const lon = place?.lon;
  const key = place ? `${variableId}|${lat}|${lon}|${size}|${attempt}` : null;

  useEffect(() => {
    if (key === null) return;
    const controller = new AbortController();
    const query = new URLSearchParams({
      variable: variableId,
      lat: String(lat),
      lon: String(lon),
      size: String(size),
    });
    fetch(`/api/timeseries?${query}`, { signal: controller.signal })
      .then(async (res) => {
        const body = await res.json();
        if (!res.ok) throw new Error(body.error ?? "The data request failed.");
        setResult({ key, record: body });
      })
      .catch((err) => {
        if (controller.signal.aborted) return;
        setResult({ key, error: err instanceof Error ? err.message : "The data request failed." });
      });
    return () => controller.abort();
  }, [key, variableId, lat, lon, size]);

  if (key === null) return null;
  if (result?.key !== key) return { status: "loading" };
  if ("error" in result) {
    return { status: "error", error: result.error, retry: () => setAttempt((n) => n + 1) };
  }
  return { status: "ready", record: result.record };
}
