"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import Link from "next/link";
import { ALPHA, chanceOdds, evidenceLevel, type ChallengeStats } from "@/lib/challenge";
import type { Variable } from "@/lib/catalog";
import { pText, sig, signed, withUnit } from "@/lib/format";
import type { Mode, SeriesPoint } from "@/lib/series";
import { DistributionPlot, IntervalPlot, SeriesPlot } from "./charts";
import { CountUp, prefersReducedMotion } from "./hooks";
import { EvidenceMeter, JargonBuster, PlainWords } from "./Plain";

/** When each analysis card appears, ms after the step opens: four tests, the verdict, the comparison. */
const SCHEDULE = [400, 3600, 7600, 11600, 15000, 19000];
const LAST = SCHEDULE.length;

interface Props {
  variable: Variable;
  region: string;
  /** The place on its own, e.g. "Kuwait" */
  placeName: string;
  points: SeriesPoint[];
  mode: Mode;
  stats: ChallengeStats;
  /** What the series is, e.g. "45 yearly averages" */
  seriesLabel: string;
  cells: number;
  onReplay: () => void;
  onGoto: (step: number) => void;
  /** The second-region comparison, shown after the verdict */
  compare: ReactNode;
  /** Open with everything shown and jump straight to the comparison (used by the examples) */
  instant?: boolean;
}

/**
 * Step 4: the analysis plays out one test at a time. The fitted lines draw over
 * the data, then each test gets its own card and picture, and the verdict is
 * stamped last. Remount (change the key) to replay from the start.
 */
export function AnalysisStep({ variable, region, placeName, points, mode, stats, seriesLabel, cells, onReplay, onGoto, compare, instant = false }: Props) {
  const [stage, setStage] = useState(() => (instant || prefersReducedMotion() ? LAST : 0));
  const root = useRef<HTMLDivElement>(null);
  const skipped = useRef(false);

  useEffect(() => {
    if (instant) {
      const timer = setTimeout(() => root.current?.querySelector('[data-stage="6"]')?.scrollIntoView({ behavior: "smooth", block: "start" }), 300);
      return () => clearTimeout(timer);
    }
    if (prefersReducedMotion()) return;
    const timers = SCHEDULE.map((ms, i) => setTimeout(() => setStage((s) => Math.max(s, i + 1)), ms));
    return () => timers.forEach(clearTimeout);
  }, [instant]);

  useEffect(() => {
    if (stage < 2 || skipped.current) return;
    root.current?.querySelector(`[data-stage="${stage}"]`)?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [stage]);

  const unit = variable.unit;
  const perDecade = (v: number) => `${withUnit(signed(v * 10, 2), unit)} per decade`;
  const { ols, mk, sen, pairs } = stats;
  const y0 = Math.floor(stats.t0);
  const y1 = Math.floor(stats.t1);
  const moved = stats.direction === "rising" ? "rose" : stats.direction === "falling" ? "fell" : "held steady";
  const cardProps = (i: number) => ({ current: stage === i + 1 && stage < 5 });
  const evidence = evidenceLevel(stats);
  const thing = `${variable.name.toLowerCase()} in ${placeName}`;
  const step = mode === "anomaly" ? "month" : "year";
  // "0.76%" of humidity reads like a share of something; in words it is percentage points.
  const abs = (v: number) => (unit === "%" ? `${sig(Math.abs(v), 2)} percentage points` : withUnit(sig(Math.abs(v), 2), unit));
  const rising = stats.direction === "rising";
  const upDown = rising ? "up" : "down";
  const wobbleRatio = Math.abs(stats.change) / stats.residualSd;
  const tAdj = ols.slope / ols.seAdjusted;
  const [ciLo, ciHi] = ols.ciAdjusted;
  const excludesZero = ciLo > 0 || ciHi < 0;
  const pctUp = Math.round((pairs.up / pairs.total) * 100);
  const pctDown = Math.round((pairs.down / pairs.total) * 100);

  return (
    <div ref={root} className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-ink-2" aria-live="polite">
          {stage < 5 ? `Running test ${Math.max(stage, 1)} of 4…` : stage < LAST ? "Verdict in. A comparison with another region is next…" : "Analysis complete."}
        </p>
        <div className="flex flex-wrap gap-2">
          {stage < LAST && (
            <button
              type="button"
              className="btn px-4 py-1.5 text-sm"
              onClick={() => {
                skipped.current = true;
                setStage(LAST);
              }}
            >
              Show all now
            </button>
          )}
          <button type="button" className="btn px-4 py-1.5 text-sm" onClick={onReplay}>
            ↻ Replay analysis
          </button>
        </div>
      </div>

      <div className="rounded-xl border-2 border-line bg-surface p-3 sm:p-4">
        <SeriesPlot
          points={points}
          markers={mode !== "anomaly"}
          relative={mode === "anomaly"}
          duration={900}
          stats={stats}
          showFit={stage >= 1}
          showBand={stage >= 4}
          unit={unit}
          axisLabel={`${mode === "anomaly" ? "Difference from normal" : variable.name}${unit ? ` (${unit})` : ""}`}
          label={`${variable.name} for ${region} with trend lines`}
        />
      </div>

      {stage >= 1 && (
        <TestCard index={1} title="Rate of change" {...cardProps(0)}>
          <div className="grid gap-4 sm:grid-cols-3">
            <Big label="Theil-Sen rate (robust)">
              <CountUp value={sen.slope * 10} delay={200} duration={1400} format={(v) => withUnit(signed(v, 2), unit)} />
              <small className="block text-base font-normal text-ink-2">per decade</small>
            </Big>
            <Big label="Least-squares rate">
              <CountUp value={ols.slope * 10} delay={400} duration={1400} format={(v) => withUnit(signed(v, 2), unit)} />
              <small className="block text-base font-normal text-ink-2">per decade</small>
            </Big>
            <Big label={`Total change, ${y0}–${y1}`}>
              <CountUp value={stats.change} delay={600} duration={1400} format={(v) => withUnit(signed(v, 2), unit)} />
              <small className="block text-base font-normal text-ink-2">along the Theil-Sen line</small>
            </Big>
          </div>
          <p className="mt-4">
            <span className="ch-formula">slope = Σ(t − t̄)(v − v̄) ÷ Σ(t − t̄)²</span>
          </p>
          <p className="mt-3 text-ink-2">
            The solid line is the least-squares fit, the line that keeps the squared misses smallest. The dashed line is
            the Theil-Sen fit: the median of the slopes between every pair of points, so one odd year cannot swing it.
          </p>
          <PlainWords delay={1200}>
            <p>
              Every 10 years, {thing} goes <strong>{upDown} by about {abs(sen.slope * 10)}</strong>. Over the{" "}
              {y1 - y0 + 1} years from {y0} to {y1}, that adds up to about <strong>{abs(stats.change)}</strong>.
            </p>
            <p>
              For scale, a single {step} usually lands about {abs(stats.residualSd)} above or below the trend line.{" "}
              {wobbleRatio >= 1
                ? `So the total change is about ${sig(wobbleRatio, 2)} times that usual wobble.`
                : "So the total change is smaller than that usual wobble, which makes it hard to see by eye."}
            </p>
          </PlainWords>
        </TestCard>
      )}

      {stage >= 2 && (
        <TestCard index={2} title="t-test on the slope" {...cardProps(1)}>
          <p>
            <span className="ch-formula">
              t = slope ÷ standard error = {sig(ols.slope * 10, 3)} ÷ {sig(ols.se * 10, 3)} = {sig(ols.t, 3)}
            </span>
            <span className="ml-2 text-sm text-ink-2">(per decade, {ols.df} degrees of freedom)</span>
          </p>
          <p className="mt-3 text-ink-2">
            If there were no trend at all, t would usually land near 0, inside the curve below. Landing in a red tail
            would happen less than 5% of the time by chance.
          </p>
          <div className="mt-2">
            <DistributionPlot kind="t" df={ols.df} stat={ols.t} crit={stats.tCrit} statLabel={`t = ${sig(ols.t, 3)}`} />
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-3">
            <Pass ok={ols.p < ALPHA} label={`Ordinary t-test: ${pText(ols.p)}`} />
            <Pass ok={stats.tTestSignificant} label={`Adjusted for autocorrelation: ${pText(ols.pAdjusted)}`} />
          </div>
          <p className="mt-3 text-sm text-ink-2">
            Neighbouring points can resemble each other (lag-1 correlation of the leftovers r = {sig(ols.r1, 2)}), which
            makes a series look more certain than it is. The adjusted test counts {sig(ols.nEff, 3)} effectively
            independent points instead of {stats.n}. The verdict uses the adjusted p-value.
          </p>
          <PlainWords delay={1700}>
            <p>
              <strong>Signal versus noise.</strong> The trend is <strong>{sig(Math.abs(tAdj), 2)} times</strong> bigger than
              its own uncertainty. As a rule of thumb, anything above about 2 is hard to get by luck.
            </p>
            <p>
              If {variable.name.toLowerCase()} were not really changing, random ups and downs would produce a trend this
              strong <strong>{chanceOdds(ols.pAdjusted)}</strong>.{" "}
              {stats.tTestSignificant ? "So luck is a poor explanation." : "So luck could easily explain it."}
            </p>
          </PlainWords>
        </TestCard>
      )}

      {stage >= 3 && (
        <TestCard index={3} title="Mann-Kendall test" {...cardProps(2)}>
          <p className="text-ink-2">
            Compare every point with every later point and only ask: did it go up or down? No straight line or bell
            curve is assumed, so outliers and skewed data do not fool it.
          </p>
          <PairBar up={pairs.up} down={pairs.down} ties={pairs.ties} />
          <p className="mt-3">
            <span className="ch-formula">
              S = ups − downs = {pairs.up.toLocaleString("en-US")} − {pairs.down.toLocaleString("en-US")} ={" "}
              {mk.s.toLocaleString("en-US")}
            </span>{" "}
            <span className="ch-formula">Z = {sig(mk.z, 3)}</span>{" "}
            <span className="ch-formula">Kendall&apos;s τ = {sig(mk.tau, 2)}</span>
          </p>
          <div className="mt-2">
            <DistributionPlot kind="z" stat={mk.z} crit={stats.zCrit} statLabel={`Z = ${sig(mk.z, 3)}`} />
          </div>
          <div className="mt-3">
            <Pass ok={stats.mkSignificant} label={`Mann-Kendall: ${pText(mk.p)}`} />
          </div>
          <PlainWords delay={1700}>
            <p>
              <strong>Count the ups and downs.</strong> We compared every {step} with every later {step} (
              {pairs.total.toLocaleString("en-US")} pairs). The later one was higher <strong>{pctUp}%</strong> of the time
              and lower <strong>{pctDown}%</strong>.
            </p>
            <p>
              If nothing were changing, you would expect close to a 50/50 split, like tossing a coin. A split this
              lopsided would happen by luck <strong>{chanceOdds(mk.p)}</strong>.
            </p>
          </PlainWords>
        </TestCard>
      )}

      {stage >= 4 && (
        <TestCard index={4} title="95% confidence intervals" {...cardProps(3)}>
          <p className="text-ink-2">
            The range of rates the data are consistent with. If a bar stays clear of the dashed zero line, “no change” is
            ruled out at the 5% level. The shaded band on the chart is the same idea for the fitted line itself.
          </p>
          <div className="mt-3">
            <IntervalPlot
              unit={unit}
              rows={[
                { label: "Least squares", estimate: ols.slope * 10, lo: ols.ci[0] * 10, hi: ols.ci[1] * 10 },
                { label: "Least squares, adjusted", estimate: ols.slope * 10, lo: ols.ciAdjusted[0] * 10, hi: ols.ciAdjusted[1] * 10 },
                { label: "Theil-Sen", estimate: sen.slope * 10, lo: sen.ci[0] * 10, hi: sen.ci[1] * 10 },
              ]}
            />
          </div>
          <p className="mt-2 text-sm">
            Adjusted least-squares interval: <strong>{perDecade(ols.ciAdjusted[0])}</strong> to{" "}
            <strong>{perDecade(ols.ciAdjusted[1])}</strong>
            {ols.ciAdjusted[0] > 0 || ols.ciAdjusted[1] < 0 ? ", which excludes zero." : ", which includes zero."}
          </p>
          <PlainWords delay={1600}>
            <p>
              <strong>The likely range.</strong> Our best guess is {perDecade(ols.slope)}, but no record is perfect.
              Allowing for that, the true rate is very likely somewhere between <strong>{perDecade(ciLo)}</strong> and{" "}
              <strong>{perDecade(ciHi)}</strong>.
            </p>
            <p>
              {excludesZero
                ? `Every value in that range is a ${ciLo > 0 ? "rise" : "fall"}, so even the most cautious reading says ${thing} is ${ciLo > 0 ? "rising" : "falling"}.`
                : `That range includes zero, meaning no change at all, so we cannot rule out that ${thing} is holding steady.`}
            </p>
          </PlainWords>
        </TestCard>
      )}

      {stage >= 5 && (
        <section
          data-stage={5}
          aria-labelledby="verdict-heading"
          className="ch-rise rounded-2xl border-[2.5px] border-edge bg-surface-2 p-5 sm:p-7"
        >
          <p className="text-sm font-bold uppercase tracking-wider text-ink-2">Verdict</p>
          <p className="display ch-rise mt-2 text-3xl sm:text-4xl">
            {stats.verdict === "significant"
              ? `Yes: ${thing} is ${rising ? "rising" : "falling"}, by about ${abs(sen.slope * 10)} every 10 years.`
              : stats.verdict === "mixed"
                ? `Maybe: there are signs ${thing} is ${rising ? "rising" : "falling"}, but the two tests disagree.`
                : `No clear change: ${thing} has not moved more than normal ups and downs would.`}
          </p>
          <div className="ch-rise mt-5 max-w-[640px]" style={{ animationDelay: "200ms" }}>
            <p className="text-sm font-bold text-ink-2">How strong is the evidence for a trend?</p>
            <EvidenceMeter evidence={evidence} />
            <p className="mt-2">
              <strong>{evidence.label}.</strong> {evidence.meaning}
            </p>
          </div>
          <p className="mt-6 text-sm font-bold uppercase tracking-wider text-ink-2">The technical verdict</p>
          <h3
            id="verdict-heading"
            className={`stamp ch-stamp mt-4 max-w-full whitespace-normal text-base sm:text-[1.4rem] ${stats.verdict === "significant" ? "text-good" : stats.verdict === "mixed" ? "text-close" : "text-ink-2"}`}
          >
            {stats.verdict === "significant"
              ? `Significant ${stats.direction} trend`
              : stats.verdict === "mixed"
                ? "Mixed evidence"
                : "No significant trend"}
          </h3>
          <p className="mt-5 max-w-[75ch]">
            {variable.name} for {region} {moved} by about <strong>{withUnit(sig(Math.abs(sen.slope * 10), 2), unit)} per decade</strong> from {y0} to {y1} (
            {withUnit(signed(stats.change, 2), unit)} in all).{" "}
            {stats.verdict === "significant"
              ? `Both the autocorrelation-adjusted t-test (${pText(ols.pAdjusted)}) and the Mann-Kendall test (${pText(mk.p)}) say a change this steady is very unlikely to be chance alone.`
              : stats.verdict === "mixed"
                ? `One test finds a trend and the other does not (t-test ${pText(ols.pAdjusted)}, Mann-Kendall ${pText(mk.p)}). Treat it as a possible trend that needs a longer record or a second dataset.`
                : `Neither the t-test (${pText(ols.pAdjusted)}) nor the Mann-Kendall test (${pText(mk.p)}) can rule out chance: ordinary year-to-year variation could produce a rate this size.`}
          </p>
          <p className="mt-2 text-sm text-ink-2">
            Rule: a trend is called significant only when both tests give p &lt; {ALPHA}. A significant trend says the
            change is real in this record, not what caused it.
          </p>
          <JargonBuster />

          <h4 className="display mt-6 text-2xl">The challenge, done</h4>
          <ul className="mt-3 space-y-2">
            {[
              `Variable identified: ${variable.name} (NASA POWER ${variable.id})`,
              `Data acquired for a region: ${region}, ${cells} grid cell${cells === 1 ? "" : "s"}`,
              `Time series built and plotted: ${seriesLabel}, ${y0}–${y1}`,
              `Rate and significance: ${perDecade(sen.slope)}; t-test ${pText(ols.pAdjusted)}; Mann-Kendall ${pText(mk.p)}; 95% interval ${withUnit(signed(ols.ciAdjusted[0] * 10, 2), unit)} to ${perDecade(ols.ciAdjusted[1])}`,
            ].map((text, i) => (
              <li key={text} className="ch-rise flex items-start gap-3" style={{ animationDelay: `${700 + i * 300}ms` }}>
                <svg viewBox="0 0 24 24" className="ch-tick mt-0.5 h-6 w-6 shrink-0" style={{ animationDelay: `${900 + i * 300}ms` }} aria-hidden>
                  <circle cx={12} cy={12} r={11} fill="var(--good)" />
                  <path d="M6.5 12.5l3.5 3.5 7.5-8" pathLength={1} fill="none" stroke="#fff" strokeWidth={2.6} strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                <span>{text}</span>
              </li>
            ))}
          </ul>

          <div className="mt-6 flex flex-wrap gap-3">
            <button type="button" className="btn" onClick={() => onGoto(1)}>
              Try another region
            </button>
            <button type="button" className="btn" onClick={() => onGoto(0)}>
              Try another variable
            </button>
            <Link href="/detective" className="btn btn-primary">
              Make it a game: Investigate <span aria-hidden>→</span>
            </Link>
          </div>
        </section>
      )}

      {stage >= 6 && (
        <div data-stage={6} className="ch-rise scroll-mt-6">
          {compare}
        </div>
      )}
    </div>
  );
}

function TestCard({
  index,
  title,
  children,
  current,
}: {
  index: number;
  title: string;
  children: ReactNode;
  current: boolean;
}) {
  return (
    <section data-stage={index} data-current={current || undefined} aria-labelledby={`test-${index}`} className="card ch-test ch-rise scroll-mt-6 p-5 sm:p-6">
      <h3 id={`test-${index}`} className="display flex items-center gap-3 text-2xl sm:text-3xl">
        <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full border-[2.5px] border-edge bg-gold text-lg text-[#10242e]">
          {index}
        </span>
        {title}
      </h3>
      <div className="mt-4">{children}</div>
    </section>
  );
}

function Big({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="rounded-xl border-2 border-edge bg-surface-2 p-3">
      <p className="text-sm text-ink-2">{label}</p>
      <p className="display tabular text-3xl">{children}</p>
    </div>
  );
}

function Pass({ ok, label }: { ok: boolean; label: string }) {
  return (
    <span className={`ch-pill ch-pop ${ok ? "text-good" : "text-close"}`} style={{ animationDelay: "1500ms" }}>
      {ok ? "✓ Significant" : "✗ Not significant"} · {label}
    </span>
  );
}

function PairBar({ up, down, ties }: { up: number; down: number; ties: number }) {
  const total = up + down + ties || 1;
  return (
    <div className="mt-4">
      <div className="flex justify-between text-sm font-bold">
        <span className="text-series-a">
          ↑ <CountUp value={up} duration={1100} format={(v) => Math.round(v).toLocaleString("en-US")} /> later values higher
        </span>
        <span className="text-series-b">
          <CountUp value={down} duration={1100} format={(v) => Math.round(v).toLocaleString("en-US")} /> lower ↓
        </span>
      </div>
      <div className="mt-1 flex h-7 overflow-hidden rounded-full border-[2.5px] border-edge bg-surface-2">
        <div className="ch-bar h-full bg-series-a" style={{ width: `${(up / total) * 100}%` }} />
        <div className="flex-1" />
        <div className="ch-bar-right h-full bg-series-b" style={{ width: `${(down / total) * 100}%` }} />
      </div>
      <p className="mt-1 text-sm text-ink-2">
        {total.toLocaleString("en-US")} pairs compared{ties ? `, ${ties.toLocaleString("en-US")} tied` : ""}.
      </p>
    </div>
  );
}
