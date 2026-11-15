"use client";

import { useMemo, useRef, useState } from "react";
import { challengeStats, fillGaps } from "@/lib/challenge";
import { VARIABLES_BY_ID, type RegionSize } from "@/lib/catalog";
import { MONTH_NAMES, MODES, toSeries, type Mode } from "@/lib/series";
import { useSeries } from "@/lib/useSeries";
import { AnalysisStep } from "./AnalysisStep";
import { CompareSection } from "./CompareSection";
import { EXAMPLES, PLACES, RegionStep, placeLabel, regionLabel, type Example, type Place } from "./RegionStep";
import { SeriesStep } from "./SeriesStep";
import { OBJECTIVES, StepFrame } from "./StepFrame";
import { VariableStep } from "./VariableStep";

interface Request {
  variableId: string;
  lat: number;
  lon: number;
  size: RegionSize;
  name: string | null;
}

const keyOf = (r: { variableId: string; lat: number; lon: number; size: RegionSize }) =>
  `${r.variableId}|${r.lat}|${r.lon}|${r.size}`;

/**
 * The challenge as four steps, in the order the brief lists them. Each step
 * unlocks the next; the rail at the top fills as objectives are met.
 */
export function ChallengeWalkthrough() {
  const [step, setStep] = useState(0);
  const [dir, setDir] = useState<"forward" | "back">("forward");
  const [variableId, setVariableId] = useState("T2M");
  const [place, setPlace] = useState<Place>(PLACES[0]);
  const [size, setSize] = useState<RegionSize>(5);
  const [flyToken, setFlyToken] = useState(0);
  const [requested, setRequested] = useState<Request | null>(null);
  const [mode, setMode] = useState<Mode>("annual");
  const [month, setMonth] = useState(6);
  const [replay, setReplay] = useState(0);
  /** The second place for the comparison, run with the same variable, size and lens */
  const [compare, setCompare] = useState<Place | null>(null);
  /** Set by an example, so step 4 opens on the comparison instead of replaying every test */
  const [instant, setInstant] = useState(false);
  const top = useRef<HTMLDivElement>(null);

  const series = useSeries(requested?.variableId ?? variableId, requested, requested?.size ?? 0);
  const current = requested !== null && keyOf(requested) === keyOf({ variableId, lat: place.lat, lon: place.lon, size });
  const raw = current && series?.status === "ready" ? series.record : null;
  // Short gaps are filled once here, so every step reads the same record.
  const gaps = useMemo(() => (raw ? fillGaps(raw.monthly) : null), [raw]);
  const record = useMemo(() => (raw && gaps ? { ...raw, monthly: gaps.monthly } : null), [raw, gaps]);
  const dataVariable = VARIABLES_BY_ID[requested?.variableId ?? variableId];
  const region = requested ? regionLabel(requested, requested.size) : regionLabel(place, size);

  const points = useMemo(() => (record ? toSeries(record, mode, month) : []), [record, mode, month]);
  const stats = useMemo(() => challengeStats(points), [points]);

  const available = [true, true, record !== null, record !== null && stats !== null];
  const loadingData = current && series?.status === "loading";
  const met = [step > 0, record !== null, step > 2 && stats !== null, step === 3 && stats !== null];

  const loadExample = (ex: Example) => {
    setVariableId(ex.variableId);
    setPlace(ex.a);
    setSize(ex.size);
    setRequested({ variableId: ex.variableId, lat: ex.a.lat, lon: ex.a.lon, size: ex.size, name: ex.a.name });
    setCompare(ex.b);
    setMode("annual");
    setInstant(true);
    setFlyToken((n) => n + 1);
    setDir(step < 3 ? "forward" : "back");
    setStep(3);
    top.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  };

  const goTo = (i: number) => {
    if (i === step) return;
    if (!available[i] && !(i >= 2 && loadingData)) return;
    setInstant(false);
    setDir(i > step ? "forward" : "back");
    setStep(i);
    top.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  };

  const seriesLabel =
    mode === "annual"
      ? `${points.length} yearly averages`
      : mode === "month"
        ? `${points.length} ${MONTH_NAMES[month]} values, one per year`
        : `${points.length} monthly values with the seasons removed`;

  const back = (i: number) => (
    <button type="button" className="btn" onClick={() => goTo(i)}>
      <span aria-hidden>←</span> Back
    </button>
  );

  return (
    <div ref={top} className="scroll-mt-4">
      <nav aria-label="Challenge steps" className="card mt-8 px-3 pb-4 pt-5 sm:px-6">
        <ol className="ch-rail">
          <span className="ch-rail-track" aria-hidden />
          <span className="ch-rail-fill" aria-hidden style={{ width: `${(step / 3) * 75}%` }} />
          {OBJECTIVES.map((o, i) => {
            const state = i === step ? "current" : !available[i] ? "locked" : i < step || met[i] ? "done" : "open";
            return (
              <li key={o.short} className="min-w-0">
                <button
                  type="button"
                  className="ch-node w-full"
                  data-state={state}
                  disabled={!available[i]}
                  aria-current={i === step ? "step" : undefined}
                  onClick={() => goTo(i)}
                >
                  <span className="ch-node-dot">{state === "done" ? "✓" : i + 1}</span>
                  <span className="ch-node-label">{o.short}</span>
                </button>
              </li>
            );
          })}
        </ol>

        <ul className="mt-4 flex flex-wrap justify-center gap-2 text-sm" aria-label="Your investigation so far">
          <li key={variableId} className="chip ch-pop">
            Variable: {VARIABLES_BY_ID[variableId].name}
          </li>
          {step >= 1 && (
            <li key={`${place.lat},${place.lon},${size}`} className="chip ch-pop">
              Region: {placeLabel(place)}
              {size ? ` · ${size}° box` : ""}
            </li>
          )}
          {record && (
            <li key={`data-${keyOf(requested!)}`} className="chip ch-pop">
              Data: {record.monthly.length} months
            </li>
          )}
          {step >= 2 && record && (
            <li key={`mode-${mode}-${month}`} className="chip ch-pop">
              Series: {MODES.find((m) => m.id === mode)!.label}
            </li>
          )}
        </ul>
        {step < 3 && (
          <div className="mt-4 flex flex-wrap items-center justify-center gap-2 border-t-2 border-line pt-4 text-sm">
            <span className="font-bold">In a hurry? Load an example with opposite trends:</span>
            {EXAMPLES.map((ex) => (
              <button key={ex.id} type="button" className="btn px-3 py-1 text-sm" onClick={() => loadExample(ex)}>
                {ex.title}
              </button>
            ))}
          </div>
        )}
      </nav>

      <div key={step} className="ch-panel mt-6" data-dir={dir}>
        {step === 0 && (
          <StepFrame
            index={0}
            done
            footer={
              <>
                <span className="text-ink-2">Chosen: {VARIABLES_BY_ID[variableId].name}</span>
                <button type="button" className="btn btn-primary" onClick={() => goTo(1)}>
                  Next: choose a region <span aria-hidden>→</span>
                </button>
              </>
            }
          >
            <VariableStep variableId={variableId} onVariable={setVariableId} />
          </StepFrame>
        )}

        {step === 1 && (
          <StepFrame
            index={1}
            done={met[1]}
            footer={
              <>
                {back(0)}
                <button type="button" className="btn btn-primary" disabled={!available[2]} onClick={() => goTo(2)}>
                  {available[2] ? "Next: build the time series" : "Acquire the data to continue"} <span aria-hidden>→</span>
                </button>
              </>
            }
          >
            <RegionStep
              variable={VARIABLES_BY_ID[variableId]}
              place={place}
              size={size}
              flyToken={flyToken}
              filledMonths={gaps?.filled ?? 0}
              onPlace={(p, fly) => {
                setPlace(p);
                if (p.size !== undefined) setSize(p.size);
                if (fly) setFlyToken((n) => n + 1);
              }}
              onSize={setSize}
              series={requested ? series : null}
              current={current}
              onAcquire={() => {
                if (current && series?.status === "error") series.retry();
                else {
                  setRequested({ variableId, lat: place.lat, lon: place.lon, size, name: place.name });
                  setInstant(false);
                }
              }}
            />
          </StepFrame>
        )}

        {step === 2 && record && (
          <StepFrame
            index={2}
            done={stats !== null}
            footer={
              <>
                {back(1)}
                <button type="button" className="btn btn-primary" disabled={!available[3]} onClick={() => goTo(3)}>
                  Next: test the trend <span aria-hidden>→</span>
                </button>
              </>
            }
          >
            <SeriesStep
              variable={dataVariable}
              record={record}
              region={region}
              points={points}
              mode={mode}
              month={month}
              onMode={setMode}
              onMonth={setMonth}
            />
          </StepFrame>
        )}

        {step === 3 && record && stats && (
          <StepFrame
            index={3}
            done
            footer={
              <>
                {back(2)}
                <span className="text-ink-2">All four objectives met for this case.</span>
              </>
            }
          >
            <AnalysisStep
              key={`${keyOf(requested!)}|${mode}|${month}|${replay}|${instant}`}
              variable={dataVariable}
              region={region}
              placeName={placeLabel(requested!)}
              instant={instant}
              compare={
                <CompareSection
                  variable={dataVariable}
                  size={requested!.size}
                  mode={mode}
                  month={month}
                  a={{ place: requested!, points, stats }}
                  b={compare}
                  onB={setCompare}
                  onExample={loadExample}
                />
              }
              points={points}
              mode={mode}
              stats={stats}
              seriesLabel={seriesLabel}
              cells={record.cells}
              onReplay={() => {
                setInstant(false);
                setReplay((n) => n + 1);
              }}
              onGoto={goTo}
            />
          </StepFrame>
        )}

        {step >= 2 && !record && (
          <section className="card p-5 sm:p-7" aria-live="polite">
            {series?.status === "error" ? (
              <div role="alert">
                <p className="display text-3xl">The data did not arrive.</p>
                <p className="mt-2 text-ink-2">{series.error}</p>
                <button type="button" className="btn btn-primary mt-4" onClick={series.retry}>
                  Try again
                </button>
              </div>
            ) : (
              <div className="ch-pipe-step" data-state="active">
                <span className="ch-pipe-icon" aria-hidden />
                <span>
                  <span className="display block text-3xl">Acquiring NASA data…</span>
                  <span className="text-ink-2">
                    {dataVariable.name} for {region}, monthly since {dataVariable.startYear}.
                  </span>
                </span>
              </div>
            )}
          </section>
        )}
      </div>
    </div>
  );
}
