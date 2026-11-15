"use client";

import { useState } from "react";
import { seasonalCycle } from "@/lib/challenge";
import type { Variable } from "@/lib/catalog";
import { sig, withUnit } from "@/lib/format";
import type { MonthlySeries } from "@/lib/power";
import { MIN_POINTS } from "@/lib/stats/trend";
import { MODES, MONTH_NAMES, type Mode, type SeriesPoint } from "@/lib/series";
import { SeriesPlot } from "./charts";
import { CountUp } from "./hooks";

interface Props {
  variable: Variable;
  record: MonthlySeries;
  region: string;
  points: SeriesPoint[];
  mode: Mode;
  month: number;
  onMode: (mode: Mode) => void;
  onMonth: (month: number) => void;
}

/** Step 3: take the seasons out and draw the series that will be tested. */
export function SeriesStep({ variable, record, region, points, mode, month, onMode, onMonth }: Props) {
  const [replay, setReplay] = useState(0);
  const { range } = seasonalCycle(record.monthly);
  const values = points.map((p) => p.v);
  const mean = values.reduce((a, b) => a + b, 0) / (values.length || 1);
  const sd = Math.sqrt(values.reduce((a, v) => a + (v - mean) ** 2, 0) / Math.max(values.length - 1, 1));
  const lowest = points.reduce((a, p) => (p.v < a.v ? p : a), points[0]);
  const highest = points.reduce((a, p) => (p.v > a.v ? p : a), points[0]);
  const span = points.length ? points[points.length - 1].t - points[0].t : 0;
  const enough = points.length >= MIN_POINTS;
  const flat = points.length > 0 && values.every((v) => v === values[0]);
  const relative = mode === "anomaly";
  const unit = variable.unit;
  const axisLabel = `${relative ? "Difference from that month's normal" : mode === "month" ? `${MONTH_NAMES[month]} ${variable.name.toLowerCase()}` : `Yearly average ${variable.name.toLowerCase()}`}${unit ? ` (${unit})` : ""}`;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end gap-x-5 gap-y-3">
        <div>
          <p className="font-bold">How to read time</p>
          <div role="radiogroup" aria-label="Time series type" className="seg mt-2">
            {MODES.map((m) => (
              <button key={m.id} type="button" role="radio" aria-checked={mode === m.id} onClick={() => onMode(m.id)}>
                {m.label}
              </button>
            ))}
          </div>
        </div>
        {mode === "month" && (
          <label className="ch-rise flex flex-col text-sm font-bold">
            Month
            <select
              value={month}
              onChange={(e) => onMonth(Number(e.target.value))}
              className="mt-2 rounded-lg border-[2.5px] border-edge bg-surface px-2 py-1.5 font-normal"
            >
              {MONTH_NAMES.map((name, i) => (
                <option key={name} value={i}>
                  {name}
                </option>
              ))}
            </select>
          </label>
        )}
        <button type="button" onClick={() => setReplay((n) => n + 1)} className="btn px-4 py-1.5 text-sm">
          ↻ Replay drawing
        </button>
      </div>
      <p key={mode} className="ch-rise max-w-[75ch] text-ink-2">
        {MODES.find((m) => m.id === mode)!.explain}
      </p>

      <div className="rounded-xl border-2 border-line bg-surface p-3 sm:p-4">
        <SeriesPlot
          key={`${mode}-${month}-${replay}`}
          points={points}
          markers={mode !== "anomaly"}
          relative={relative}
          duration={mode === "anomaly" ? 2400 : 1800}
          unit={unit}
          axisLabel={axisLabel}
          label={`${variable.name} for ${region}`}
        />
      </div>

      <dl key={`stats-${mode}-${month}`} className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Tile label="Points in the series" delay={300}>
          <CountUp value={points.length} delay={300} format={(v) => String(Math.round(v))} />
        </Tile>
        <Tile label="Years covered" delay={450}>
          <CountUp value={Math.round(span) + 1} delay={450} format={(v) => String(Math.round(v))} />
        </Tile>
        <Tile label="Lowest" delay={600}>
          <span className="text-2xl">{lowest ? lowest.label : "n/a"}</span>
        </Tile>
        <Tile label="Highest" delay={750}>
          <span className="text-2xl">{highest ? highest.label : "n/a"}</span>
        </Tile>
      </dl>

      <div className="grid gap-3 md:grid-cols-2">
        <p className="ch-rise rounded-xl border-2 border-line-strong bg-surface-2 p-4" style={{ animationDelay: "900ms" }}>
          <strong>Why not use the raw months? </strong>
          The seasonal cycle alone spans {withUnit(sig(range, 2), unit)} between the highest and lowest calendar-month
          average. In this series the points vary by only about {withUnit(sig(sd, 2), unit)} (one standard deviation),
          so a slow drift can show.
        </p>
        {flat ? (
          <p className="ch-rise rounded-xl border-2 border-miss p-4" style={{ animationDelay: "1100ms" }}>
            <strong className="text-miss">Nothing to test here. </strong>
            {variable.name} never changes in {region} (every value is {withUnit(sig(values[0] ?? 0, 2), unit)}). Snow
            depth in a desert is the classic case. Go back and pick another variable or place.
          </p>
        ) : (
          <p
            className={`ch-rise rounded-xl border-2 p-4 ${enough ? "border-good" : "border-miss"}`}
            style={{ animationDelay: "1100ms" }}
          >
            <strong className={enough ? "text-good" : "text-miss"}>{enough ? "✓ Long enough to test. " : "Too short to test. "}</strong>
            {points.length} points over {Math.round(span) + 1} years. The tests here need at least {MIN_POINTS}; climate
            trends are usually judged over 30 years or more.
          </p>
        )}
      </div>
    </div>
  );
}

function Tile({ label, delay, children }: { label: string; delay: number; children: React.ReactNode }) {
  return (
    <div className="ch-pop rounded-xl border-2 border-edge bg-surface p-3" style={{ animationDelay: `${delay}ms` }}>
      <dt className="text-sm text-ink-2">{label}</dt>
      <dd className="display tabular text-3xl">{children}</dd>
    </div>
  );
}
