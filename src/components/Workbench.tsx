"use client";

import { useRef, useState } from "react";
import { analyse } from "@/lib/analysis";
import {
  DEMO_CASES,
  REGION_SIZES,
  VARIABLES_BY_ID,
  type DemoCase,
  type RegionSize,
  type ThemeId,
} from "@/lib/catalog";
import { playRound, type Guess, type RoundResult } from "@/lib/game";
import { clamp, formatLatLon, wrapLon } from "@/lib/region";
import type { Mode } from "@/lib/series";
import type { TrendResult } from "@/lib/stats/trend";
import { resetProgress, saveProgress, useProgress } from "@/lib/useProgress";
import { useSeries, type SeriesState } from "@/lib/useSeries";
import { BadgeShelf } from "./BadgeShelf";
import { Briefing } from "./Briefing";
import { CaseBoard } from "./CaseBoard";
import { EvidencePicker } from "./EvidencePicker";
import { Findings, type FindingsPlace } from "./Findings";
import { Hud } from "./Hud";
import { PredictionPanel } from "./PredictionPanel";
import { RegionMap, type SpotId } from "./RegionMap";
import { Scorecard } from "./Scorecard";
import { TimeLens } from "./TimeLens";
import { Toasts, type Toast } from "./Toasts";

interface Spot {
  lat: number;
  lon: number;
  /** A place name from a board case; cleared once the spot is moved */
  name: string | null;
}

interface Investigation {
  variableId: string;
  size: RegionSize;
  a: Spot;
  /** The second place, when comparing */
  b: Spot | null;
  /** The board case being played, or null for the player's own case */
  caseId: string | null;
}

/** A revealed round. result is null when the player skipped the guess. */
interface RoundRecord {
  result: RoundResult | null;
}

const SIZE_LABELS: Record<RegionSize, string> = {
  0: "One spot",
  2: "2° box",
  5: "5° box",
  10: "10° box",
};

const SPOT_COLOR: Record<SpotId, string> = { a: "var(--series-a)", b: "var(--series-b)" };

function fromCase(c: DemoCase): Investigation {
  return {
    variableId: c.variableId,
    size: c.size,
    a: { lat: c.lat, lon: c.lon, name: c.place },
    b: c.compare ? { lat: c.compare.lat, lon: c.compare.lon, name: c.compare.place } : null,
    caseId: c.id,
  };
}

const spotLabel = (spot: Spot) => spot.name ?? formatLatLon(spot.lat, spot.lon);

export function Workbench() {
  const progress = useProgress();
  const [inv, setInv] = useState<Investigation>(() => fromCase(DEMO_CASES[0]));
  const [theme, setTheme] = useState<ThemeId>(VARIABLES_BY_ID[DEMO_CASES[0].variableId].theme);
  const [mode, setMode] = useState<Mode>("annual");
  const [month, setMonth] = useState(6);
  /** Which place a click on the globe moves */
  const [target, setTarget] = useState<SpotId>("a");
  const [flyToken, setFlyToken] = useState(0);
  const [draft, setDraft] = useState<{ key: string; guesses: (Guess | null)[] } | null>(null);
  const [rounds, setRounds] = useState<Record<string, RoundRecord>>({});
  const [toasts, setToasts] = useState<Toast[]>([]);
  const caseFile = useRef<HTMLElement>(null);
  const shelf = useRef<HTMLDialogElement>(null);

  const variable = VARIABLES_BY_ID[inv.variableId];
  const activeCase = DEMO_CASES.find((c) => c.id === inv.caseId) ?? null;

  const seriesA = useSeries(inv.variableId, inv.a, inv.size) as SeriesState;
  const seriesB = useSeries(inv.variableId, inv.b, inv.size);
  const states = seriesB ? [seriesA, seriesB] : [seriesA];
  const failed = states.find((s) => s.status === "error");
  const loading = states.some((s) => s.status === "loading");

  const spots = [
    { id: "a" as const, ...inv.a },
    ...(inv.b ? [{ id: "b" as const, ...inv.b }] : []),
  ];
  const labels = spots.map((s) => ({ letter: s.id.toUpperCase(), label: spotLabel(s) }));

  const places: FindingsPlace[] = [];
  if (seriesA.status === "ready") places.push({ ...labels[0], record: seriesA.record });
  if (inv.b && seriesB?.status === "ready") places.push({ ...labels[1], record: seriesB.record });

  // A call belongs to one measurement at one set of places. Change any of
  // those and there is a fresh call to make.
  const callKey = [inv.variableId, inv.size, ...spots.map((s) => `${s.lat},${s.lon}`)].join("|");
  const round = rounds[callKey];
  const guesses = draft?.key === callKey ? draft.guesses : spots.map(() => null);

  const scrollToCaseFile = () =>
    caseFile.current?.scrollIntoView({ behavior: "smooth", block: "start" });

  const openCase = (c: DemoCase) => {
    setInv(fromCase(c));
    setTheme(VARIABLES_BY_ID[c.variableId].theme);
    setMode("annual");
    setTarget("a");
    setFlyToken((n) => n + 1);
    scrollToCaseFile();
  };

  const openOwnCase = () => {
    setInv((prev) => ({ ...prev, caseId: null }));
    scrollToCaseFile();
  };

  const moveSpot = (id: SpotId, lat: number, lon: number) => {
    const moved: Spot = {
      lat: Math.round(clamp(lat, -90, 90) * 10) / 10,
      lon: Math.round(wrapLon(lon) * 10) / 10,
      name: null,
    };
    setInv((prev) => ({ ...prev, [id]: moved, caseId: null }));
  };

  const addSecondPlace = () => {
    // Start B a little east of A; the next globe click puts it where it belongs.
    setInv((prev) => ({
      ...prev,
      b: { lat: prev.a.lat, lon: Math.round(wrapLon(prev.a.lon + 30) * 10) / 10, name: null },
      caseId: null,
    }));
    setTarget("b");
    setFlyToken((n) => n + 1);
  };

  const removeSecondPlace = () => {
    setInv((prev) => ({ ...prev, b: null, caseId: null }));
    setTarget("a");
  };

  const makeGuess = (index: number, guess: Guess) => {
    const next = [...guesses];
    next[index] = guess;
    setDraft({ key: callKey, guesses: next });
  };

  const notify = (items: Omit<Toast, "id">[]) => {
    const stamped = items.map((item, i) => ({ ...item, id: Date.now() + i }));
    setToasts((prev) => [...prev, ...stamped]);
    for (const toast of stamped) {
      setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== toast.id)), 6000);
    }
  };

  const reveal = (skip: boolean) => {
    if (loading || failed || places.length !== spots.length) return;
    const trends = places.map((p) => analyse(p.record, mode, month).trend);
    const called = guesses.filter((g): g is Guess => g !== null);
    // A skipped guess, or a series too short to test, opens the evidence without scoring.
    if (skip || called.length !== spots.length || trends.some((t) => t === null)) {
      setRounds((prev) => ({ ...prev, [callKey]: { result: null } }));
      return;
    }
    const result = playRound(progress, {
      guesses: called,
      trends: trends as TrendResult[],
      caseId: inv.caseId,
      totalCases: DEMO_CASES.length,
      theme: variable.theme,
      spots,
    });
    saveProgress(result.progress);
    setRounds((prev) => ({ ...prev, [callKey]: { result } }));

    const news = [
      ...(result.promotedTo
        ? [{ title: `Promoted to ${result.promotedTo.name}`, body: `You reached ${result.promotedTo.xp} XP.` }]
        : []),
      ...result.newBadges.map((b) => ({ title: `Badge earned: ${b.name}`, body: b.description })),
    ];
    // Let the stamp land before announcing anything else.
    if (news.length > 0) setTimeout(() => notify(news), 2000);
  };

  const prompt =
    spots.length > 1
      ? `Since ${variable.startYear}, has ${variable.name.toLowerCase()} been rising, falling, or showing no clear trend in each place?`
      : `Since ${variable.startYear}, has ${variable.name.toLowerCase()} around ${labels[0].label} been rising, falling, or showing no clear trend?`;

  return (
    <>
      <Hud progress={progress} onOpenBadges={() => shelf.current?.showModal()} />

      <CaseBoard
        activeCaseId={inv.caseId}
        caseStars={progress.caseStars}
        onOpen={openCase}
        onFreePlay={openOwnCase}
      />

      <section ref={caseFile} aria-labelledby="casefile-heading" className="mt-16 scroll-mt-20">
        <div className="flex flex-wrap items-end justify-between gap-3 border-b-[2.5px] border-edge pb-3">
          <h2 id="casefile-heading" className="display text-4xl">
            {activeCase ? "Case file" : "Your own case"}
          </h2>
          {activeCase && (progress.caseStars[activeCase.id] ?? 0) > 0 && (
            <span className="chip">Solved before. Play again to improve your stars.</span>
          )}
        </div>

        <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
          {activeCase ? (
            <Briefing
              demo={activeCase}
              variable={variable}
              onCustomize={() => setInv((prev) => ({ ...prev, caseId: null }))}
            />
          ) : (
            <EvidencePicker
              theme={theme}
              onTheme={setTheme}
              variableId={inv.variableId}
              onVariable={(id) => setInv((prev) => ({ ...prev, variableId: id, caseId: null }))}
            />
          )}

          <section aria-labelledby="scene-heading" className="card flex min-w-0 flex-col p-5 sm:p-6">
            <h3 id="scene-heading" className="display text-3xl">
              The scene
            </h3>
            <p className="mt-2 text-ink-2">
              {inv.b
                ? `Clicking the globe moves place ${target.toUpperCase()}. Press A or B to switch.`
                : "Click the globe to move the investigation, or type coordinates. A bigger box averages out local quirks."}
            </p>

            <div className="mt-4 space-y-3">
              {spots.map((spot) => (
                <div key={spot.id} className="flex flex-wrap items-end gap-x-3 gap-y-2">
                  {inv.b && (
                    <button
                      type="button"
                      aria-pressed={target === spot.id}
                      aria-label={`Globe clicks move place ${spot.id.toUpperCase()}`}
                      onClick={() => setTarget(spot.id)}
                      className={`mb-0.5 grid h-10 w-10 place-items-center rounded-full border-[2.5px] border-edge font-bold text-white ${
                        target === spot.id ? "shadow-[3px_3px_0_var(--drop)]" : "opacity-70"
                      }`}
                      style={{ background: SPOT_COLOR[spot.id] }}
                    >
                      {spot.id.toUpperCase()}
                    </button>
                  )}
                  <CoordinateInput
                    label="Latitude"
                    value={spot.lat}
                    min={-90}
                    max={90}
                    onCommit={(lat) => moveSpot(spot.id, lat, spot.lon)}
                  />
                  <CoordinateInput
                    label="Longitude"
                    value={spot.lon}
                    min={-180}
                    max={180}
                    onCommit={(lon) => moveSpot(spot.id, spot.lat, lon)}
                  />
                  {spot.name && <span className="pb-2 font-bold">{spot.name}</span>}
                  {spot.id === "b" && (
                    <button
                      type="button"
                      onClick={removeSecondPlace}
                      className="pb-2 text-ink-2 underline underline-offset-4 hover:text-ink"
                    >
                      Remove
                    </button>
                  )}
                </div>
              ))}

              <div className="flex flex-wrap items-center gap-x-4 gap-y-3 pt-1">
                <div role="radiogroup" aria-label="Region size" className="seg">
                  {REGION_SIZES.map((s) => (
                    <button
                      key={s}
                      type="button"
                      role="radio"
                      aria-checked={inv.size === s}
                      onClick={() => setInv((prev) => ({ ...prev, size: s, caseId: null }))}
                    >
                      {SIZE_LABELS[s]}
                    </button>
                  ))}
                </div>
                {!inv.b && (
                  <button type="button" onClick={addSecondPlace} className="btn px-4 py-1.5 text-sm">
                    Compare with a second place
                  </button>
                )}
              </div>
            </div>

            <div className="mt-4 h-[360px] overflow-hidden rounded-xl border-[2.5px] border-edge lg:h-auto lg:min-h-[380px] lg:flex-1">
              <RegionMap
                spots={spots}
                size={inv.size}
                active={target}
                flyToken={flyToken}
                onPick={(lat, lon) => moveSpot(target, lat, lon)}
              />
            </div>
          </section>
        </div>

        <div className="mt-8">
          {failed?.status === "error" ? (
            <div role="alert" className="card max-w-[64ch] p-5 sm:p-6">
              <p className="display text-3xl">The evidence did not arrive.</p>
              <p className="mt-2 text-ink-2">{failed.error}</p>
              <button type="button" onClick={failed.retry} className="btn btn-primary mt-4">
                Try again
              </button>
            </div>
          ) : round && places.length === spots.length ? (
            <Findings
              key={callKey}
              animate
              variable={variable}
              places={places}
              mode={mode}
              month={month}
              aside={round.result ? <Scorecard result={round.result} places={labels} /> : undefined}
              lens={
                <TimeLens
                  mode={mode}
                  onMode={setMode}
                  month={month}
                  onMonth={setMonth}
                  scored={round.result !== null}
                />
              }
            />
          ) : (
            <PredictionPanel
              prompt={prompt}
              places={labels}
              guesses={guesses}
              onGuess={makeGuess}
              ready={!loading}
              onReveal={() => reveal(false)}
              onSkip={() => reveal(true)}
            />
          )}
        </div>
      </section>

      <BadgeShelf
        ref={shelf}
        progress={progress}
        onReset={() => {
          resetProgress();
          setRounds({});
          setDraft(null);
        }}
      />
      <Toasts toasts={toasts} />
    </>
  );
}

function CoordinateInput({
  label,
  value,
  min,
  max,
  onCommit,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  onCommit: (value: number) => void;
}) {
  // Uncontrolled and re-keyed on value, so typing is free and outside changes still show.
  const commit = (raw: string) => {
    const parsed = Number(raw);
    if (raw.trim() !== "" && Number.isFinite(parsed) && parsed !== value) onCommit(parsed);
  };
  return (
    <label className="flex flex-col text-sm font-bold text-ink-2">
      {label}
      <input
        key={value}
        type="number"
        inputMode="decimal"
        step={0.1}
        min={min}
        max={max}
        defaultValue={value}
        onBlur={(e) => commit(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter") commit(e.currentTarget.value);
        }}
        className="tabular mt-1 w-24 rounded-lg border-[2.5px] border-edge bg-surface px-2 py-1.5 text-base font-normal text-ink"
      />
    </label>
  );
}
