"use client";

import { REGION_SIZES, type RegionSize, type Variable } from "@/lib/catalog";
import { formatLatLon } from "@/lib/region";
import type { SeriesState } from "@/lib/useSeries";
import { RegionMap } from "../RegionMap";
import { SeriesPlot } from "./charts";
import { CountUp } from "./hooks";

export interface Place {
  lat: number;
  lon: number;
  name: string | null;
  /** A region size that suits the place, applied when it is picked */
  size?: RegionSize;
}

/** Kuwait is about 2° across, so its box is 2°: a bigger one would mostly average Iraq, Saudi Arabia and the Gulf. */
export const KUWAIT: Place = { name: "Kuwait", lat: 29.3, lon: 47.6, size: 2 };
const SAHEL: Place = { name: "Central Sahel", lat: 14, lon: 2 };
const CHILE: Place = { name: "Central Chile", lat: -36, lon: -72 };

export const PLACES: Place[] = [
  { name: "Svalbard (Arctic)", lat: 78.2, lon: 15.6 },
  KUWAIT,
  SAHEL,
  { name: "Andalusia, Spain", lat: 37.5, lon: -4.5 },
  { name: "Delhi, India", lat: 28.6, lon: 77.2 },
  { name: "Amazon Basin", lat: -3.5, lon: -62 },
  { name: "Phoenix, Arizona", lat: 33.4, lon: -112.1 },
  { name: "North China Plain", lat: 36, lon: 116 },
  CHILE,
];

/** Ready-made comparisons where one variable runs in opposite directions in two places (checked on NASA POWER, 1981-2025). */
export interface Example {
  id: string;
  title: string;
  blurb: string;
  variableId: string;
  size: RegionSize;
  a: Place;
  b: Place;
}

export const EXAMPLES: Example[] = [
  {
    id: "rain",
    title: "Rain: Sahel vs Central Chile",
    blurb: "Rainfall returning to the Sahel while central Chile dries out.",
    variableId: "PRECTOTCORR",
    size: 10,
    a: SAHEL,
    b: CHILE,
  },
  {
    id: "humidity",
    title: "Humidity: Kuwait vs Sahel",
    blurb: "Kuwait's air getting drier while the Sahel's gets more humid.",
    variableId: "RH2M",
    size: 2,
    a: KUWAIT,
    b: SAHEL,
  },
];

const SIZE_LABELS: Record<RegionSize, string> = { 0: "One spot", 2: "2° box", 5: "5° box", 10: "10° box" };

export const placeLabel = (p: Place) => p.name ?? formatLatLon(p.lat, p.lon);

export function regionLabel(p: Place, size: RegionSize) {
  return size === 0 ? `the grid cell at ${placeLabel(p)}` : `a ${size}° × ${size}° box around ${placeLabel(p)}`;
}

interface Props {
  variable: Variable;
  place: Place;
  /** Short gaps filled in the acquired record */
  filledMonths: number;
  size: RegionSize;
  flyToken: number;
  onPlace: (place: Place, fly: boolean) => void;
  onSize: (size: RegionSize) => void;
  /** null until the visitor asks for the data */
  series: SeriesState | null;
  /** The data shown belongs to the current selection */
  current: boolean;
  onAcquire: () => void;
}

/** Step 2: choose where, then watch the request go out to NASA POWER and the record come back. */
export function RegionStep({ variable, place, filledMonths, size, flyToken, onPlace, onSize, series, current, onAcquire }: Props) {
  const loading = current && series?.status === "loading";
  return (
    <div className="space-y-6">
      <div className="grid gap-6 lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
        <div className="min-w-0 space-y-5">
          <div>
            <p className="font-bold">Pick a place, or click anywhere on the globe</p>
            <div className="mt-2 flex flex-wrap gap-2">
              {PLACES.map((p, i) => (
                <button
                  key={p.name}
                  type="button"
                  aria-pressed={p.name === place.name}
                  onClick={() => onPlace(p, true)}
                  className="tile ch-pop px-3 py-1.5 text-sm font-bold"
                  style={{ animationDelay: `${i * 50}ms` }}
                >
                  {p.name}
                </button>
              ))}
            </div>
          </div>
          <div>
            <p className="font-bold">How big a region?</p>
            <div role="radiogroup" aria-label="Region size" className="seg mt-2">
              {REGION_SIZES.map((s) => (
                <button key={s} type="button" role="radio" aria-checked={size === s} onClick={() => onSize(s)}>
                  {SIZE_LABELS[s]}
                </button>
              ))}
            </div>
            <p className="mt-2 text-sm text-ink-2">
              A bigger box averages more grid cells, which smooths out local quirks. A single spot shows one ~50 km cell.
            </p>
          </div>
          <div key={`${place.lat},${place.lon},${size}`} className="ch-rise rounded-xl border-2 border-line-strong bg-surface-2 p-4">
            <p className="text-sm font-bold text-ink-2">Region to acquire</p>
            <p className="display mt-1 text-2xl">{placeLabel(place)}</p>
            <p className="tabular text-sm text-ink-2">
              {formatLatLon(place.lat, place.lon)} · {size === 0 ? "single grid cell" : `${size}° × ${size}° box`}
            </p>
          </div>
          <button type="button" onClick={onAcquire} disabled={loading} className="btn btn-primary w-full text-lg sm:w-auto">
            {current && series?.status === "ready" ? "Acquire again" : `Acquire ${variable.name.toLowerCase()} data`}
            <span aria-hidden>↓</span>
          </button>
        </div>

        <div className="h-[320px] min-w-0 overflow-hidden rounded-xl border-[2.5px] border-edge lg:h-auto lg:min-h-[400px]">
          <RegionMap
            spots={[{ id: "a", lat: place.lat, lon: place.lon }]}
            size={size}
            active="a"
            flyToken={flyToken}
            onPick={(lat, lon) => onPlace({ lat: Math.round(lat * 10) / 10, lon: Math.round(lon * 10) / 10, name: null }, false)}
          />
        </div>
      </div>

      {series && <Acquisition variable={variable} series={series} current={current} filledMonths={filledMonths} />}
    </div>
  );
}

function Acquisition({ variable, series, current, filledMonths }: { variable: Variable; series: SeriesState; current: boolean; filledMonths: number }) {
  if (series.status === "error") {
    return (
      <div role="alert" className="ch-rise rounded-xl border-2 border-miss p-4">
        <p className="font-bold">The data did not arrive.</p>
        <p className="text-ink-2">{series.error}</p>
        <button type="button" onClick={series.retry} className="btn mt-3">
          Try again
        </button>
      </div>
    );
  }
  const ready = series.status === "ready";
  const record = ready ? series.record : null;
  const months = record?.monthly.length ?? 0;
  const missing = record?.monthly.filter((v) => v === null).length ?? 0;
  const raw = record
    ? record.monthly.flatMap((v, i) =>
        v === null ? [] : [{ t: record.startYear + (i + 0.5) / 12, v, label: `${i % 12 + 1}/${record.startYear + Math.floor(i / 12)}` }],
      )
    : [];
  const stages = [
    { label: `Request ${variable.name.toLowerCase()} (${variable.id}) from the NASA POWER API`, detail: "Monthly, from MERRA-2 and CERES records" },
    { label: "Average the grid cells inside the region, weighted by area", detail: record ? `${record.cells} grid cell${record.cells === 1 ? "" : "s"}` : "" },
    { label: "Receive the monthly record", detail: record ? `${record.startYear} to ${record.endYear}` : "" },
  ];

  return (
    <div className="ch-rise rounded-2xl border-[2.5px] border-edge bg-surface-2 p-5" aria-live="polite">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p className="display text-2xl">{ready ? "Data acquired" : "Acquiring data…"}</p>
        {!current && ready && <span className="chip">Selection changed. Acquire again to use it.</span>}
      </div>
      <ol className="ch-pipe mt-4">
        {stages.map((s, i) => {
          const state = ready ? "done" : i === 0 ? "active" : "waiting";
          return (
            <li key={s.label} className="ch-pipe-step" data-state={state} style={{ animationDelay: `${i * 350}ms` }}>
              <span className="ch-pipe-icon" aria-hidden style={{ animationDelay: `${i * 350}ms` }}>
                {state === "done" ? "✓" : ""}
              </span>
              <span>
                <span className="font-bold">{s.label}</span>
                {s.detail && <span className="block text-sm text-ink-2">{s.detail}</span>}
              </span>
            </li>
          );
        })}
      </ol>

      {record && (
        <div className="mt-5 space-y-4">
          <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <Stat label="Months received" value={months} delay={600} />
            <Stat label="Years" value={record.endYear - record.startYear + 1} delay={750} />
            <Stat label="Grid cells averaged" value={record.cells} delay={900} />
            <Stat label="Missing months" value={missing} delay={1050} />
          </dl>
          {missing > 0 && (
            <p className="ch-rise rounded-xl border-2 border-close p-3 text-sm" style={{ animationDelay: "1200ms" }}>
              <strong>Gaps handled. </strong>
              {missing} month{missing === 1 ? " is" : "s are"} missing.{" "}
              {filledMonths > 0
                ? `${filledMonths} short gap${filledMonths === 1 ? " was" : "s were"} filled with that calendar month's normal, shifted by how warm, wet or dry the rest of that year was. `
                : ""}
              {missing > filledMonths
                ? "Years missing more than three months are left out of yearly averages rather than guessed."
                : "No year had to be dropped."}
            </p>
          )}
          <div className="rounded-xl border-2 border-line bg-surface p-3">
            <SeriesPlot
              points={raw}
              markers={false}
              duration={1800}
              height={190}
              unit={variable.unit}
              axisLabel={`Raw monthly ${variable.name.toLowerCase()}${variable.unit ? ` (${variable.unit})` : ""}`}
              label={`Raw monthly ${variable.name}`}
            />
            <p className="mt-1 text-sm text-ink-2">
              The raw record. The big up-and-down is the seasons, which can hide a slow trend. Step 3 deals with that.
            </p>
          </div>
          <p className="text-sm text-ink-2">
            Source:{" "}
            <a className="underline underline-offset-4" href={record.source.url} target="_blank" rel="noreferrer">
              the exact NASA POWER request
            </a>
            {record.source.version ? ` (API ${record.source.version})` : ""}.
          </p>
        </div>
      )}
    </div>
  );
}

function Stat({ label, value, delay }: { label: string; value: number; delay: number }) {
  return (
    <div className="ch-pop rounded-xl border-2 border-edge bg-surface p-3" style={{ animationDelay: `${delay}ms` }}>
      <dt className="text-sm text-ink-2">{label}</dt>
      <dd className="display tabular text-3xl">
        <CountUp value={value} delay={delay} format={(v) => Math.round(v).toLocaleString("en-US")} />
      </dd>
    </div>
  );
}
