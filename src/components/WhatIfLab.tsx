"use client";

import { useMemo, useState } from "react";
import type { ClimateSource } from "@/lib/earth/climate";
import { MONTH_NAMES } from "@/lib/series";
import {
  LIMITS,
  NO_CHANGE,
  YEAR_LIMITS,
  simulate,
  type Climate,
  type Controls,
  type Indicators,
} from "@/lib/whatif/model";
import { DISCOVERY_INFO, buildReport } from "@/lib/whatif/report";
import { CompareChart } from "./CompareChart";
import { DropIcon, SunIcon, ThermometerIcon, WindIcon } from "./icons";

const TODAY = "var(--series-a)";
const WHAT_IF = "var(--series-b)";
const SHORT_MONTHS = MONTH_NAMES.map((m) => m.slice(0, 3));

const SLIDERS: {
  key: keyof Controls;
  label: string;
  unit: string;
  Icon: typeof ThermometerIcon;
  format: (v: number) => string;
}[] = [
  { key: "temperatureDeltaC", label: "Air temperature", unit: "°C", Icon: ThermometerIcon, format: (v) => `${v > 0 ? "+" : ""}${v} °C` },
  { key: "precipitationChangePct", label: "Rain and snow", unit: "%", Icon: DropIcon, format: (v) => `${v > 0 ? "+" : ""}${v}%` },
  { key: "relativeHumidityDeltaPctPoints", label: "Relative humidity", unit: "points", Icon: SunIcon, format: (v) => `${v > 0 ? "+" : ""}${v} points` },
  { key: "windSpeedChangePct", label: "Wind speed", unit: "%", Icon: WindIcon, format: (v) => `${v > 0 ? "+" : ""}${v}%` },
];

const PRESETS: { label: string; controls: Controls }[] = [
  { label: "Hotter and drier", controls: { temperatureDeltaC: 2, precipitationChangePct: -30, relativeHumidityDeltaPctPoints: -10, windSpeedChangePct: 15 } },
  { label: "Super monsoon", controls: { temperatureDeltaC: 0, precipitationChangePct: 60, relativeHumidityDeltaPctPoints: 10, windSpeedChangePct: 0 } },
  { label: "Cooler and calmer", controls: { temperatureDeltaC: -2, precipitationChangePct: 10, relativeHumidityDeltaPctPoints: 5, windSpeedChangePct: -30 } },
];

const TILES: { key: keyof Indicators; label: string; unit: string; digits: number; scale?: number; higherIsWorse: boolean }[] = [
  { key: "soilMoistureIndex", label: "Soil water", unit: "% full", digits: 0, scale: 100, higherIsWorse: false },
  { key: "droughtStressIndex", label: "Drought stress", unit: "0–1", digits: 2, higherIsWorse: true },
  { key: "vpdKpa", label: "Thirsty air (VPD)", unit: "kPa", digits: 2, higherIsWorse: true },
  { key: "et0MmPerYear", label: "Evaporative demand", unit: "mm/yr", digits: 0, higherIsWorse: true },
  { key: "precipitationMmPerYear", label: "Rain", unit: "mm/yr", digits: 0, higherIsWorse: false },
  { key: "extremeHeatMonths", label: "40 °C months", unit: "per year", digits: 0, higherIsWorse: true },
];

type AiState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "done"; text: string; model: string }
  | { status: "error"; message: string };

export function WhatIfLab({ climate, sources, aiEnabled }: { climate: Climate; sources: ClimateSource[]; aiEnabled: boolean }) {
  const [controls, setControls] = useState<Controls>(PRESETS[0].controls);
  const [years, setYears] = useState(3);
  const [ai, setAi] = useState<AiState>({ status: "idle" });

  const result = useMemo(() => simulate(climate, controls, years), [climate, controls, years]);
  const report = useMemo(() => buildReport(result), [result]);

  function update(next: Controls, nextYears = years) {
    setControls(next);
    setYears(nextYears);
    setAi({ status: "idle" });
  }

  async function askAi() {
    setAi({ status: "loading" });
    try {
      const res = await fetch("/api/what-if/report", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ controls, years }),
      });
      const body = await res.json().catch(() => null);
      if (!res.ok || !body?.text) throw new Error(body?.error ?? `The briefing service returned ${res.status}.`);
      setAi({ status: "done", text: body.text, model: body.model });
    } catch (e) {
      setAi({ status: "error", message: e instanceof Error ? e.message : "Something went wrong." });
    }
  }

  const months = result.scenarioMonths.length;
  const runLabels = result.scenarioMonths.map((s) => `Year ${Math.ceil(s.step / 12)} ${SHORT_MONTHS[s.month - 1]}`);

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,4fr)_minmax(0,7fr)]">
      {/* ---------------- controls ---------------- */}
      <section aria-labelledby="controls-heading" className="card h-fit p-5 sm:p-6 lg:sticky lg:top-4">
        <h2 id="controls-heading" className="display text-3xl">
          The dials
        </h2>
        <p className="mt-2 text-sm text-ink-2">
          Each change is applied to every month of the region&apos;s real 1991–2020 climate.
        </p>

        <div className="mt-4 flex flex-wrap gap-2">
          {PRESETS.map((p) => (
            <button key={p.label} type="button" className="btn px-3 py-1 text-sm" onClick={() => update(p.controls)}>
              {p.label}
            </button>
          ))}
          <button type="button" className="btn tile-dashed px-3 py-1 text-sm" onClick={() => update(NO_CHANGE)}>
            Reset
          </button>
        </div>

        <div className="mt-5 space-y-5">
          {SLIDERS.map(({ key, label, Icon, format }) => {
            const lim = LIMITS[key];
            const id = `slider-${key}`;
            return (
              <div key={key}>
                <div className="flex items-center justify-between gap-2">
                  <label htmlFor={id} className="flex items-center gap-2 font-bold">
                    <Icon className="h-5 w-5" />
                    {label}
                  </label>
                  <output htmlFor={id} className="chip tabular">
                    {format(controls[key])}
                  </output>
                </div>
                <input
                  id={id}
                  type="range"
                  className="slider mt-2"
                  min={lim.min}
                  max={lim.max}
                  step={lim.step}
                  value={controls[key]}
                  aria-valuetext={format(controls[key])}
                  onChange={(e) => update({ ...controls, [key]: Number(e.target.value) })}
                />
                <div className="flex justify-between text-xs text-ink-2 tabular">
                  <span>{format(lim.min)}</span>
                  <span>{format(lim.max)}</span>
                </div>
              </div>
            );
          })}

          <div>
            <div className="flex items-center justify-between gap-2">
              <label htmlFor="slider-years" className="font-bold">
                Run for
              </label>
              <output htmlFor="slider-years" className="chip tabular">
                {years} year{years === 1 ? "" : "s"}
              </output>
            </div>
            <input
              id="slider-years"
              type="range"
              className="slider mt-2"
              min={YEAR_LIMITS.min}
              max={YEAR_LIMITS.max}
              step={1}
              value={years}
              onChange={(e) => update(controls, Number(e.target.value))}
            />
          </div>
        </div>
      </section>

      {/* ---------------- results ---------------- */}
      <div className="min-w-0 space-y-6">
        <section aria-labelledby="report-heading" className="card p-5 sm:p-6" aria-live="polite">
          <p className="chip">Hypothetical · not a forecast</p>
          <h2 id="report-heading" className="display mt-3 text-3xl">
            The report
          </h2>
          <p className="mt-2 text-lg">{report.headline}</p>

          {result.discoveries.length > 0 && (
            <ul className="mt-4 flex flex-wrap gap-2" aria-label="Discoveries">
              {result.discoveries.map((d) => (
                <li key={d.id} className="tile px-3 py-2" title={DISCOVERY_INFO[d.id].blurb}>
                  <span className="font-bold">{DISCOVERY_INFO[d.id].title}</span>
                  <span className="ml-2 text-xs uppercase text-ink-2">{d.severity}</span>
                </li>
              ))}
            </ul>
          )}

          <dl className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3">
            {TILES.map((t) => {
              const scale = t.scale ?? 1;
              const before = result.baseline[t.key] * scale;
              const after = result.scenario[t.key] * scale;
              const delta = after - before;
              const worse = t.higherIsWorse ? delta > 0 : delta < 0;
              const same = Math.abs(delta) < 10 ** -t.digits / 2;
              return (
                <div key={t.key} className="rounded-xl border-2 border-line p-3">
                  <dt className="text-xs text-ink-2">
                    {t.label} <span className="whitespace-nowrap">({t.unit})</span>
                  </dt>
                  <dd className="mt-1 tabular">
                    <span className="text-ink-2">{before.toFixed(t.digits)} → </span>
                    <strong className="text-xl">{after.toFixed(t.digits)}</strong>
                    {!same && (
                      <span className={`ml-1 text-sm font-bold ${worse ? "text-miss" : "text-good"}`}>
                        {delta > 0 ? "▲" : "▼"}
                        <span className="sr-only">{worse ? " worse" : " better"}</span>
                      </span>
                    )}
                  </dd>
                </div>
              );
            })}
          </dl>
          <p className="mt-2 text-xs text-ink-2">Today → what-if, final year of the run. Red marks a change for the worse, green for the better.</p>

          <h3 className="mt-6 font-bold">What the model works out</h3>
          <ul className="mt-2 list-disc space-y-1.5 pl-5">
            {report.findings.map((f) => (
              <li key={f}>{f}</li>
            ))}
          </ul>

          <div className="mt-6 rounded-xl border-2 border-dashed border-line-strong p-4">
            <h3 className="font-bold">AI briefing</h3>
            {aiEnabled ? (
              <>
                <p className="mt-1 text-sm text-ink-2">
                  Google Gemini writes a short briefing from the numbers above. It sees only this model&apos;s
                  results, and it can still make mistakes, so check it against them.
                </p>
                {ai.status === "done" ? (
                  <div className="mt-3 whitespace-pre-line">{ai.text}</div>
                ) : ai.status === "error" ? (
                  <p className="mt-3 text-miss">{ai.message}</p>
                ) : null}
                <button
                  type="button"
                  className="btn btn-primary mt-3 text-sm"
                  disabled={ai.status === "loading"}
                  onClick={askAi}
                >
                  {ai.status === "loading" ? "Writing…" : ai.status === "done" ? "Write it again" : "Write an AI briefing"}
                </button>
                {ai.status === "done" && <p className="mt-2 text-xs text-ink-2">Written by {ai.model}.</p>}
              </>
            ) : (
              <p className="mt-1 text-sm text-ink-2">
                Not switched on for this site yet. It needs a Gemini API key set on the server; the report above
                works without it.
              </p>
            )}
          </div>
        </section>

        <section aria-labelledby="charts-heading" className="card p-5 sm:p-6">
          <h2 id="charts-heading" className="display text-3xl">
            The graphs
          </h2>
          <div className="mt-4 grid gap-8 xl:grid-cols-2">
            <CompareChart
              title="Air temperature by month (°C)"
              unit="°C"
              kind="line"
              xLabels={SHORT_MONTHS}
              series={[
                { key: "today", label: "Today", color: TODAY, values: climate.temperatureC },
                { key: "whatif", label: "What if", color: WHAT_IF, values: result.scenarioClimate.temperatureC },
              ]}
            />
            <CompareChart
              title="Rain by month (mm)"
              unit="mm"
              kind="bar"
              xLabels={SHORT_MONTHS}
              series={[
                { key: "today", label: "Today", color: TODAY, values: climate.precipitationMm },
                { key: "whatif", label: "What if", color: WHAT_IF, values: result.scenarioClimate.precipitationMm },
              ]}
            />
            <CompareChart
              title="Evaporative demand by month (mm)"
              unit="mm"
              kind="bar"
              xLabels={SHORT_MONTHS}
              series={[
                { key: "today", label: "Today", color: TODAY, values: result.baselineMonths.slice(0, 12).map((s) => s.et0Mm) },
                { key: "whatif", label: "What if", color: WHAT_IF, values: result.scenarioMonths.slice(0, 12).map((s) => s.et0Mm) },
              ]}
            />
            <CompareChart
              title={`Soil water, month by month for ${years} year${years === 1 ? "" : "s"} (mm)`}
              unit="mm"
              kind="line"
              tickEvery={months > 24 ? 12 : 3}
              xLabels={runLabels}
              series={[
                { key: "today", label: "Today", color: TODAY, values: result.baselineMonths.map((s) => s.soilWaterMm) },
                { key: "whatif", label: "What if", color: WHAT_IF, values: result.scenarioMonths.map((s) => s.soilWaterMm) },
              ]}
            />
          </div>
        </section>

        <section aria-labelledby="limits-heading" className="card p-5 sm:p-6">
          <h2 id="limits-heading" className="display text-3xl">
            Read this before you believe it
          </h2>
          <ul className="mt-3 list-disc space-y-1.5 pl-5 text-ink-2">
            {report.caveats.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
          <details className="mt-4 text-sm">
            <summary className="cursor-pointer font-bold">How the model works and where its starting climate comes from</summary>
            <p className="mt-2 text-ink-2">
              Evaporative demand is the FAO-56 Penman-Monteith reference evapotranspiration, with sunshine estimated
              from the gap between daily highs and lows (Hargreaves). Soil water is a monthly bucket: rain fills it,
              and evaporation empties it more slowly as it dries. Thirsty air is the vapour pressure deficit, worked
              out from temperature and humidity. The starting climate is real, measured or modelled data:
            </p>
            <ul className="mt-2 list-disc space-y-1 pl-5 text-ink-2">
              {sources.map((s) => (
                <li key={s.seriesId}>
                  {s.label}, {s.period} monthly averages
                </li>
              ))}
            </ul>
          </details>
        </section>
      </div>
    </div>
  );
}
