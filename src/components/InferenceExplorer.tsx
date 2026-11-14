"use client";

import { useState } from "react";
import { VERDICT } from "@/lib/earth/verdict";
import { pText, sig, signed, withUnit } from "@/lib/format";
import type { SeriesPoint } from "@/lib/series";
import type { TrendResult } from "@/lib/stats/trend";
import { TrendChart } from "./TrendChart";

export interface ExplorerSeries {
  id: string;
  label: string;
  kind: "reanalysis" | "satellite" | "station";
  definition: string;
  source: string;
  caveats: string[];
  unit: string;
  firstYear: number;
  lastYear: number;
  points: SeriesPoint[];
  trend: TrendResult;
}

const KIND_LABEL = {
  reanalysis: "NASA model (MERRA-2)",
  satellite: "Satellite (GPM IMERG)",
  station: "Weather station",
} as const;

function displayUnit(unit: string) {
  return unit === "fraction (0-1)" ? "" : unit;
}

/** The trend board: every record as a tile; picking one shows its chart and test results. */
export function InferenceExplorer({ series }: { series: ExplorerSeries[] }) {
  const [selectedId, setSelectedId] = useState(series[0]?.id);
  const selected = series.find((s) => s.id === selectedId) ?? series[0];
  if (!selected) return null;
  const unit = displayUnit(selected.unit);
  const t = selected.trend;
  const perDecade = t.sen.slope * 10;
  const total = t.sen.slope * t.spanYears;

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,4fr)_minmax(0,7fr)]">
      <ul className="grid content-start gap-2" role="list" aria-label="Records">
        {series.map((s) => {
          const v = VERDICT[s.trend.strength];
          return (
            <li key={s.id}>
              <button
                type="button"
                aria-pressed={s.id === selected.id}
                onClick={() => setSelectedId(s.id)}
                className="tile flex w-full items-center justify-between gap-3 px-3 py-2 text-left"
              >
                <span className="min-w-0">
                  <span className="block font-bold leading-tight">{s.label}</span>
                  <span className="block text-xs text-ink-2">
                    {KIND_LABEL[s.kind]} · {s.firstYear}–{s.lastYear}
                  </span>
                </span>
                <span className={`shrink-0 text-xs font-bold ${v.tone}`}>
                  {s.trend.direction === "rising" ? "▲" : s.trend.direction === "falling" ? "▼" : "■"} {v.text}
                </span>
              </button>
            </li>
          );
        })}
      </ul>

      <section className="card min-w-0 p-5 sm:p-6" aria-live="polite" aria-labelledby="explorer-heading">
        <p className="chip">{KIND_LABEL[selected.kind]}</p>
        <h3 id="explorer-heading" className="display mt-3 text-3xl">
          {selected.label}
        </h3>
        <p className="mt-2 max-w-[70ch] text-ink-2">{selected.definition}</p>

        <dl className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div>
            <dt className="text-xs text-ink-2">Verdict</dt>
            <dd className={`font-bold ${VERDICT[t.strength].tone}`}>{VERDICT[t.strength].text}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-2">Rate (Theil-Sen)</dt>
            <dd className="tabular font-bold">{withUnit(signed(perDecade, 2), unit)} per decade</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-2">
              Change over {Math.round(t.spanYears)} years
            </dt>
            <dd className="tabular font-bold">{withUnit(signed(total, 2), unit)}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-2">Yearly average</dt>
            <dd className="tabular font-bold">{withUnit(sig(t.mean, 3), unit)}</dd>
          </div>
        </dl>

        <div className="mt-5">
          <TrendChart
            key={selected.id}
            series={[{ letter: "", label: selected.label, color: "var(--series-a)", points: selected.points, trend: t, offset: 0 }]}
            axisLabel={selected.unit === "mm/year" ? "Total per year" : "Yearly average"}
            unit={unit}
            showMarkers
            relative={false}
            animate
          />
        </div>

        <p className="mt-4 text-sm text-ink-2">
          {t.n} complete years. 95% range of the rate: {withUnit(signed(t.sen.ci[0] * 10, 2), unit)} to{" "}
          {withUnit(signed(t.sen.ci[1] * 10, 2), unit)} per decade (conditional slope interval).
          {t.inference.established
            ? ` Primary OLS test: ${pText(t.ols.p)}.`
            : ` Significance not established; p-value unavailable. ${t.inference.reasons.join(" ")}`}
        </p>
        <details className="mt-3 text-sm text-ink-2">
          <summary className="cursor-pointer font-bold text-ink">Source and caveats</summary>
          <p className="mt-2">{selected.source}</p>
          <ul className="mt-2 list-disc space-y-1 pl-5">
            {selected.caveats.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
        </details>
        <details className="mt-3 text-sm">
          <summary className="cursor-pointer font-bold">Table of yearly values</summary>
          <div className="mt-2 max-h-64 overflow-auto">
            <table className="tabular w-full text-left">
              <thead>
                <tr className="text-ink-2">
                  <th className="py-1 pr-4 font-bold">Year</th>
                  <th className="py-1 font-bold">Value{unit ? ` (${unit})` : ""}</th>
                </tr>
              </thead>
              <tbody>
                {selected.points.map((p) => (
                  <tr key={p.t} className="border-t border-line">
                    <td className="py-1 pr-4">{p.label}</td>
                    <td className="py-1">{sig(p.v, 4)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </details>
      </section>
    </div>
  );
}
