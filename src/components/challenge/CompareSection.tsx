"use client";

import { useMemo, useState } from "react";
import {
  challengeStats,
  chanceOdds,
  compareRegions,
  evidenceLevel,
  fillGaps,
  type ChallengeStats,
  type RegionComparison,
} from "@/lib/challenge";
import type { RegionSize, Variable } from "@/lib/catalog";
import { pText, sig, signed, withUnit } from "@/lib/format";
import { MODES, toSeries, type Mode, type SeriesPoint } from "@/lib/series";
import { useSeries } from "@/lib/useSeries";
import { RegionMap } from "../RegionMap";
import { ComparePlot, IntervalPlot } from "./charts";
import { CountUp } from "./hooks";
import { EvidenceMeter, PlainWords } from "./Plain";
import { EXAMPLES, PLACES, placeLabel, type Example, type Place } from "./RegionStep";

interface Props {
  variable: Variable;
  size: RegionSize;
  mode: Mode;
  month: number;
  a: { place: Place; points: SeriesPoint[]; stats: ChallengeStats };
  b: Place | null;
  onB: (place: Place) => void;
  onExample: (example: Example) => void;
}

const COLOR = { A: "var(--series-a)", B: "var(--series-b)" } as const;

/**
 * The same analysis, run on a second place. Same variable, same box size and
 * the same reading of time, so the only thing that differs is where.
 */
export function CompareSection({ variable, size, mode, month, a, b, onB, onExample }: Props) {
  const [flyToken, setFlyToken] = useState(0);
  const series = useSeries(variable.id, b, size);
  const record = series?.status === "ready" ? series.record : null;

  const result = useMemo(() => {
    if (!record) return null;
    const points = toSeries({ ...record, monthly: fillGaps(record.monthly).monthly }, mode, month);
    const stats = challengeStats(points);
    return { points, stats, comparison: stats ? compareRegions(a.stats, stats, a.points, points) : null };
  }, [record, mode, month, a.stats, a.points]);

  const sizeLabel = size === 0 ? "single grid cell" : `${size}° box`;
  const modeLabel = MODES.find((m) => m.id === mode)!.label.toLowerCase();
  const nameA = placeLabel(a.place);
  const nameB = b ? placeLabel(b) : "";
  const pick = (p: Place) => {
    onB({ lat: p.lat, lon: p.lon, name: p.name });
    setFlyToken((n) => n + 1);
  };

  return (
    <section aria-labelledby="compare-heading" className="card p-5 sm:p-7">
      <p className="text-sm font-bold uppercase tracking-wider text-ink-2">Compare regions</p>
      <h3 id="compare-heading" className="display mt-1 text-3xl sm:text-4xl">
        Is the same change happening somewhere else?
      </h3>
      <p className="mt-2 max-w-[75ch] text-ink-2">
        Most trends are regional. Run the exact same analysis on a second place: same variable (
        {variable.name.toLowerCase()}), same {sizeLabel}, same {modeLabel}. Only the place changes.
      </p>

      <div className="mt-5 rounded-2xl border-[2.5px] border-dashed border-edge p-4">
        <p className="font-bold">Ready-made examples: one variable, opposite trends</p>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          {EXAMPLES.map((ex, i) => (
            <button
              key={ex.id}
              type="button"
              className="tile ch-pop p-3 text-left"
              style={{ animationDelay: `${i * 120}ms` }}
              aria-pressed={variable.id === ex.variableId && a.place.name === ex.a.name && b?.name === ex.b.name}
              onClick={() => onExample(ex)}
            >
              <span className="block font-bold">{ex.title}</span>
              <span className="block text-sm text-ink-2">{ex.blurb}</span>
            </button>
          ))}
        </div>
      </div>

      <div className="mt-6 grid gap-5 lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
        <div className="min-w-0">
          <p className="font-bold">Or pick place B, or click the globe</p>
          <div className="mt-2 flex flex-wrap gap-2">
            {PLACES.filter((p) => p.name !== a.place.name).map((p, i) => (
              <button
                key={p.name}
                type="button"
                aria-pressed={p.name === b?.name}
                onClick={() => pick(p)}
                className="tile ch-pop px-3 py-1.5 text-sm font-bold"
                style={{ animationDelay: `${i * 50}ms` }}
              >
                {p.name}
              </button>
            ))}
          </div>
          <div className="mt-4 grid grid-cols-2 gap-3">
            <PlaceBadge letter="A" name={nameA} />
            <PlaceBadge letter="B" name={b ? nameB : "Not chosen yet"} muted={!b} />
          </div>
        </div>
        <div className="h-[260px] min-w-0 overflow-hidden rounded-xl border-[2.5px] border-edge lg:h-[300px]">
          <RegionMap
            spots={[{ id: "a", lat: a.place.lat, lon: a.place.lon }, ...(b ? [{ id: "b" as const, lat: b.lat, lon: b.lon }] : [])]}
            size={size}
            active="b"
            flyToken={flyToken}
            onPick={(lat, lon) => onB({ lat: Math.round(lat * 10) / 10, lon: Math.round(lon * 10) / 10, name: null })}
          />
        </div>
      </div>

      <div className="mt-6" aria-live="polite">
        {!b ? null : series?.status === "loading" ? (
          <div className="ch-pipe-step ch-rise rounded-xl border-2 border-line-strong p-4" data-state="active">
            <span className="ch-pipe-icon" aria-hidden />
            <span className="font-bold">
              Acquiring {variable.name.toLowerCase()} for {nameB} from NASA POWER…
            </span>
          </div>
        ) : series?.status === "error" ? (
          <div role="alert" className="rounded-xl border-2 border-miss p-4">
            <p className="font-bold">The data for {nameB} did not arrive.</p>
            <p className="text-ink-2">{series.error}</p>
            <button type="button" className="btn mt-3" onClick={series.retry}>
              Try again
            </button>
          </div>
        ) : result && (!result.stats || !result.comparison) ? (
          <p className="rounded-xl border-2 border-close p-4">
            {nameB} cannot be tested for this variable: the record is too short there, or the value never changes.
          </p>
        ) : result?.stats && result.comparison ? (
          <CompareResult
            key={`${b.lat},${b.lon},${mode},${month}`}
            variable={variable}
            a={{ name: nameA, points: a.points, stats: a.stats }}
            b={{ name: nameB, points: result.points, stats: result.stats }}
            comparison={result.comparison}
            markers={mode !== "anomaly"}
          />
        ) : null}
      </div>
    </section>
  );
}

function PlaceBadge({ letter, name, muted = false }: { letter: "A" | "B"; name: string; muted?: boolean }) {
  return (
    <div key={name} className={`ch-pop flex min-w-0 items-center gap-2 rounded-xl border-2 border-edge bg-surface-2 p-2 ${muted ? "opacity-60" : ""}`}>
      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full font-bold text-white" style={{ background: COLOR[letter] }}>
        {letter}
      </span>
      <span className="truncate font-bold">{name}</span>
    </div>
  );
}

interface Side {
  name: string;
  points: SeriesPoint[];
  stats: ChallengeStats;
}

function CompareResult({
  variable,
  a,
  b,
  comparison,
  markers,
}: {
  variable: Variable;
  a: Side;
  b: Side;
  comparison: RegionComparison;
  markers: boolean;
}) {
  const unit = variable.unit;
  const name = variable.name.toLowerCase();
  const abs = (v: number) => (unit === "%" ? `${sig(Math.abs(v), 2)} percentage points` : withUnit(sig(Math.abs(v), 2), unit));
  const dir = (s: ChallengeStats) => (s.direction === "rising" ? "rising" : s.direction === "falling" ? "falling" : "flat");
  const { kind, difference } = comparison;
  const faster = Math.abs(a.stats.sen.slope) >= Math.abs(b.stats.sen.slope) ? a : b;
  const slower = faster === a ? b : a;
  const speedRatio = Math.abs(faster.stats.sen.slope) / Math.max(Math.abs(slower.stats.sen.slope), 1e-12);
  const sigOne = a.stats.verdict === "significant" ? a : b;
  const other = sigOne === a ? b : a;

  const headline = {
    opposite: "Opposite trends",
    "same-different": "Same direction, different speeds",
    "same-similar": "Same direction, similar speed",
    one: `A clear trend only in ${sigOne.name}`,
    neither: "No clear trend in either place",
  }[kind];

  const story = {
    opposite: `${variable.name} is ${dir(a.stats)} in ${a.name} but ${dir(b.stats)} in ${b.name}, and both trends pass both tests. One measurement can move in opposite directions on the same planet, which is why trends have to be checked region by region.`,
    "same-different": `Both places are ${dir(a.stats)}, but ${faster.name} is changing about ${sig(speedRatio, 2)} times as fast as ${slower.name}, and that gap is too steady to be luck.`,
    "same-similar": `Both places are ${dir(a.stats)}, at rates too close to tell apart with this record.`,
    one: `${variable.name} is ${dir(sigOne.stats)} in ${sigOne.name}. In ${other.name} the change is within normal ups and downs.`,
    neither: `In both places ${name} has moved no more than normal ups and downs would.`,
  }[kind];

  const diffP = difference ? Math.max(difference.ols.pAdjusted, difference.mk.p) : NaN;
  const diffReal = difference?.verdict === "significant";

  return (
    <div className="space-y-5">
      <div className="ch-rise">
        <h4
          className={`stamp ch-stamp max-w-full whitespace-normal text-base sm:text-[1.4rem] ${kind === "opposite" ? "text-series-b" : kind === "neither" ? "text-ink-2" : "text-good"}`}
        >
          {headline}
        </h4>
        <p className="mt-4 max-w-[75ch] text-lg">{story}</p>
      </div>

      <div className="rounded-xl border-2 border-line bg-surface p-3 sm:p-4">
        <ComparePlot
          unit={unit}
          markers={markers}
          series={[
            { letter: "A", label: a.name, color: COLOR.A, points: a.points, stats: a.stats },
            { letter: "B", label: b.name, color: COLOR.B, points: b.points, stats: b.stats },
          ]}
        />
        <p className="mt-1 text-sm text-ink-2">
          Each place is shown relative to its own average, so places with very different climates share one scale.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {([["A", a], ["B", b]] as const).map(([letter, side], i) => {
          const ev = evidenceLevel(side.stats);
          const angle = side.stats.direction === "rising" ? -45 : side.stats.direction === "falling" ? 45 : 0;
          return (
            <div key={letter} className="ch-pop rounded-2xl border-[2.5px] border-edge bg-surface-2 p-4" style={{ animationDelay: `${300 + i * 250}ms` }}>
              <div className="flex items-center gap-2">
                <span className="grid h-8 w-8 place-items-center rounded-full font-bold text-white" style={{ background: COLOR[letter] }}>
                  {letter}
                </span>
                <span className="truncate font-bold">{side.name}</span>
              </div>
              <div className="mt-3 flex items-center gap-4">
                <span className="ch-arrow display text-5xl" style={{ color: COLOR[letter], transform: `rotate(${angle}deg)` }} aria-hidden>
                  →
                </span>
                <p className="display tabular text-3xl">
                  <CountUp value={side.stats.sen.slope * 10} delay={500 + i * 250} duration={1300} format={(v) => withUnit(signed(v, 2), unit)} />
                  <small className="block text-base font-normal text-ink-2">per decade, {dir(side.stats)}</small>
                </p>
              </div>
              <div className="mt-3">
                <EvidenceMeter evidence={ev} compact />
                <p className="mt-1 text-sm">
                  <strong>{ev.label}</strong> · t-test {pText(side.stats.ols.pAdjusted)} · Mann-Kendall {pText(side.stats.mk.p)}
                </p>
              </div>
            </div>
          );
        })}
      </div>

      <div className="ch-rise rounded-xl border-2 border-line bg-surface p-3 sm:p-4" style={{ animationDelay: "900ms" }}>
        <p className="font-bold">Rates side by side, with their likely ranges (per decade)</p>
        <IntervalPlot
          unit={unit}
          rows={[a, b].map((s, i) => ({
            label: `${i ? "B" : "A"} · ${s.name}`,
            estimate: s.stats.ols.slope * 10,
            lo: s.stats.ols.ciAdjusted[0] * 10,
            hi: s.stats.ols.ciAdjusted[1] * 10,
          }))}
        />
        <p className="text-sm text-ink-2">
          A bar wholly on one side of the dashed line is a clear rise or fall. Bars on opposite sides are opposite trends.
        </p>
      </div>

      <div className="ch-rise rounded-xl border-2 border-line bg-surface p-4" style={{ animationDelay: "1200ms" }}>
        <p className="font-bold">Is the difference between the two places real?</p>
        <p className="mt-1 text-ink-2">
          We subtract B from A, {difference ? `${comparison.sharedPoints} matching points` : "point by point"}, and run the
          same tests on that difference.
          {difference && ` Its trend: t-test ${pText(difference.ols.pAdjusted)}, Mann-Kendall ${pText(difference.mk.p)}.`}
        </p>
        <PlainWords delay={1600}>
          <p>
            A&apos;s rate minus B&apos;s rate is <strong>{withUnit(signed(comparison.gap * 10, 2), unit)} per decade</strong>
            {kind === "opposite" ? `: one place is going up by ${abs((a.stats.direction === "rising" ? a : b).stats.sen.slope * 10)} while the other goes down by ${abs((a.stats.direction === "rising" ? b : a).stats.sen.slope * 10)} every 10 years.` : "."}
          </p>
          <p>
            {difference
              ? diffReal
                ? `If the two places were really changing the same way, luck would produce a gap this steady ${chanceOdds(diffP)}. So the two places really are changing differently.`
                : `A gap this size could easily come from luck (${chanceOdds(diffP)}), so we cannot say the two places are changing differently.`
              : "The two records share too few dates to test the gap."}
          </p>
        </PlainWords>
      </div>
    </div>
  );
}
