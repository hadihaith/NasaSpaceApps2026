"use client";

import { useEffect, useRef } from "react";
import type { GeoJSONSource, Map as MapLibreMap, Marker } from "maplibre-gl";
import type { GeometryObject, Topology } from "topojson-specification";
import "maplibre-gl/dist/maplibre-gl.css";
import type { RegionSize } from "@/lib/catalog";
import { regionBBox, wrapLon } from "@/lib/region";

/** NASA GIBS imagery, fetched through our own route because it retries failed tiles. */
const tiles = (layer: string) => [`${window.location.origin}/api/tiles/${layer}/{z}/{y}/{x}`];

export type SpotId = "a" | "b";

export interface MapSpot {
  id: SpotId;
  lat: number;
  lon: number;
}

interface Props {
  /** One place, or two when comparing */
  spots: MapSpot[];
  size: RegionSize;
  /** The place a click on the globe moves */
  active: SpotId;
  /** Changes when the view should travel to the places, such as on opening a case */
  flyToken: number;
  onPick: (lat: number, lon: number) => void;
}

const TINT: Record<SpotId, string> = { a: "#3987e5", b: "#eb6834" };

function regionGeoJSON(spots: MapSpot[], size: RegionSize): GeoJSON.FeatureCollection {
  const comparing = spots.length > 1;
  const features: GeoJSON.Feature[] = [];
  for (const spot of spots) {
    const b = regionBBox(spot.lat, spot.lon, size);
    if (!b) continue;
    features.push({
      type: "Feature",
      properties: { tint: comparing ? TINT[spot.id] : "#ffffff" },
      geometry: {
        type: "Polygon",
        coordinates: [
          [
            [b.lonMin, b.latMin],
            [b.lonMax, b.latMin],
            [b.lonMax, b.latMax],
            [b.lonMin, b.latMax],
            [b.lonMin, b.latMin],
          ],
        ],
      },
    });
  }
  return { type: "FeatureCollection", features };
}

/** A NASA Blue Marble globe. Click anywhere to move the place under investigation. */
export function RegionMap({ spots, size, active, flyToken, onPick }: Props) {
  const container = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const markers = useRef(new Map<SpotId, Marker>());
  const MarkerClass = useRef<typeof Marker | null>(null);
  const latest = useRef({ spots, size, active, onPick });

  useEffect(() => {
    latest.current = { spots, size, active, onPick };
  });

  /** Bring the outlines and markers in line with the current props. */
  const sync = useRef(() => {
    const map = mapRef.current;
    const MarkerCtor = MarkerClass.current;
    if (!map || !MarkerCtor) return;
    const { spots, size, active } = latest.current;
    const comparing = spots.length > 1;

    for (const id of ["a", "b"] as const) {
      const spot = spots.find((s) => s.id === id);
      let marker = markers.current.get(id);
      if (!spot) {
        marker?.remove();
        markers.current.delete(id);
        continue;
      }
      if (!marker) {
        const el = document.createElement("div");
        el.className = "region-marker";
        marker = new MarkerCtor({ element: el }).setLngLat([spot.lon, spot.lat]).addTo(map);
        markers.current.set(id, marker);
      }
      marker.setLngLat([spot.lon, spot.lat]);
      const el = marker.getElement();
      if (comparing) {
        el.dataset.letter = id.toUpperCase();
        el.dataset.active = String(id === active);
      } else {
        delete el.dataset.letter;
        delete el.dataset.active;
      }
    }

    const apply = () =>
      (map.getSource("region") as GeoJSONSource | undefined)?.setData(regionGeoJSON(spots, size));
    if (map.isStyleLoaded()) apply();
    else map.once("load", apply);
  });

  // Create the map once.
  useEffect(() => {
    let cancelled = false;
    let map: MapLibreMap | undefined;
    const createdMarkers = markers.current;

    import("maplibre-gl").then((maplibre) => {
      if (cancelled || !container.current) return;
      // MapLibre looks for its worker next to its own file, which a bundler
      // moves. Point it at the copy the bundler emits instead.
      maplibre.setWorkerUrl(
        new URL("maplibre-gl/dist/maplibre-gl-worker.mjs", import.meta.url).toString(),
      );
      const start = latest.current;
      map = new maplibre.Map({
        container: container.current,
        center: [start.spots[0].lon, start.spots[0].lat],
        zoom: 2.2,
        maxZoom: 7,
        attributionControl: { compact: true },
        style: {
          version: 8,
          projection: { type: "globe" },
          sources: {
            bluemarble: {
              type: "raster",
              tiles: tiles("bluemarble"),
              tileSize: 256,
              maxzoom: 8,
              attribution: "Imagery: NASA GIBS, Blue Marble",
            },
            // Country outlines ship with the app (Natural Earth, 1:110m), so
            // the globe stays readable even when imagery is slow.
            borders: { type: "geojson", data: { type: "FeatureCollection", features: [] } },
            region: { type: "geojson", data: regionGeoJSON(start.spots, start.size) },
          },
          layers: [
            { id: "space", type: "background", paint: { "background-color": "#060d11" } },
            { id: "bluemarble", type: "raster", source: "bluemarble" },
            {
              id: "borders",
              type: "line",
              source: "borders",
              paint: { "line-color": "#ffffff", "line-opacity": 0.45, "line-width": 0.75 },
            },
            {
              id: "region-fill",
              type: "fill",
              source: "region",
              paint: { "fill-color": ["get", "tint"], "fill-opacity": 0.22 },
            },
            {
              id: "region-casing",
              type: "line",
              source: "region",
              paint: { "line-color": "#10242e", "line-width": 4.5 },
            },
            {
              id: "region-line",
              type: "line",
              source: "region",
              paint: { "line-color": "#ffffff", "line-width": 2 },
            },
          ],
        },
      });

      // A tile that still fails after the proxy's retries leaves a gap, which
      // the globe survives. Keep those misses out of the console.
      map.on("error", (e) => {
        if (!/\/api\/tiles\//.test(String(e.error?.message))) console.error(e.error);
      });

      map.addControl(new maplibre.NavigationControl({ showCompass: false }), "top-right");
      map.getCanvas().style.cursor = "crosshair";

      map.on("click", (e) => {
        latest.current.onPick(e.lngLat.lat, wrapLon(e.lngLat.lng));
      });

      mapRef.current = map;
      MarkerClass.current = maplibre.Marker;
      sync.current();

      const loaded = map;
      Promise.all([import("world-atlas/countries-110m.json"), import("topojson-client")]).then(
        ([atlas, topojson]) => {
          if (cancelled) return;
          const topology = atlas.default as unknown as Topology;
          const outlines = topojson.mesh(topology, topology.objects.countries as GeometryObject);
          const apply = () =>
            (loaded.getSource("borders") as GeoJSONSource | undefined)?.setData({
              type: "Feature",
              properties: {},
              geometry: outlines,
            });
          if (loaded.isStyleLoaded()) apply();
          else loaded.once("load", apply);
        },
      );
    });

    return () => {
      cancelled = true;
      map?.remove();
      mapRef.current = null;
      createdMarkers.clear();
    };
  }, []);

  useEffect(() => {
    sync.current();
  }, [spots, size, active]);

  // Travel to the places when a case is opened.
  useEffect(() => {
    const map = mapRef.current;
    if (!map || flyToken === 0) return;
    const { spots, size } = latest.current;
    if (spots.length === 1) {
      map.flyTo({
        center: [spots[0].lon, spots[0].lat],
        zoom: size >= 10 ? 2.6 : 3.4,
        duration: 1400,
      });
      return;
    }
    const half = size / 2;
    const lats = spots.map((s) => s.lat);
    const lons = spots.map((s) => s.lon);
    map.fitBounds(
      [
        [Math.min(...lons) - half, Math.max(Math.min(...lats) - half, -85)],
        [Math.max(...lons) + half, Math.min(Math.max(...lats) + half, 85)],
      ],
      { padding: 48, maxZoom: 3.4, duration: 1400 },
    );
  }, [flyToken]);

  return (
    <div
      ref={container}
      role="application"
      aria-label="Globe. Click to choose where to investigate. You can also type a latitude and longitude."
      className="h-full min-h-[340px] w-full overflow-hidden rounded-md bg-[#060d11]"
    />
  );
}
