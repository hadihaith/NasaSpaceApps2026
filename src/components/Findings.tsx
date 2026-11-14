"use client";

import { useMemo, type ReactNode } from "react";
import { analyse } from "@/lib/analysis";
import type { Variable } from "@/lib/catalog";
import { pValue, sig, signed, withUnit } from "@/lib/format";
import { MONTH_NAMES, type Mode, type MonthlyRecord, type SeriesPoint } from "@/lib/series";
import {
  comparePairedSeries,
  MIN_POINTS,
  type SlopeDifference,
  type TrendResult,
} from "@/lib/stats/trend";
import { TrendChart } from "./TrendChart";

export interface FindingsPlace {
  /** "A" or "B" */
  letter: string;
  /** Reads after "around", e.g. "Svalbard" or "78.2°N, 15.6°E" */
  label: string;
  record: MonthlyRecord & { cells: number };
}

interface Props {
  variable: Variable;
  /** One place, or two to compare */
  places: FindingsPlace[];
  mode: Mode;
  month: number;
  /** Draw the chart in on first appearance */
  animate?: boolean;
  /** Shown beside the verdict, such as the player's scorecard */
  aside?: ReactNode;
  /** Shown between the verdict and the chart, such as the time lens */
  lens?: ReactNode;
}

interface Analysed extends FindingsPlace {
  color: string;
  points: SeriesPoint[];
  trend: TrendResult;
}

const COLORS = ["var(--series-a)", "var(--series-b)"];

export function Findings({ variable, places, mode, month, animate, aside, lens }: Props) {
  const analysed = useMemo(
    () =>
      places.map((place, i) => ({ ...place, color: COLORS[i], ...analyse(place.record, mode, month) })),
    [places, mode, month],
  );

  const short = analysed.filter((p) => !p.trend);
  if (short.length > 0) {
    return (
      <p className="card max-w-[64ch] p-5 text-ink-2">
        Too few usable points around {short.map((p) => p.label).join(" and ")}: a trend test needs
        at least {MIN_POINTS}. Try another place, a larger region, or a different way of reading
        time.
      </p>
    );
  }
  const ready = analysed as Analysed[];
  const comparing = ready.length > 1;
  const gap = comparing ? comparePairedSeries(ready[0].points, ready[1].points, 0.05, { assumptionsReviewed: ready.every((p) => p.record.assumptionsReviewed === true), seasonalAdjustment: mode === "anomaly" }) : null;

  // Two places can sit far apart in absolute terms (polar and tropical
  // temperature, say), so compare each against its own average.
  const relative = comparing || mode === "anomaly";
  const unitText = variable.unit || variable.unitNote || "";
  const axisLabel =
    mode === "anomaly"
      ? `Difference from normal for the month, ${unitText}`
      : comparing
        ? `Difference from each place's own average, ${unitText}`
        : `${variable.name}, ${unitText}`;

  return (
    <div>
      <div className={aside ? "grid gap-8 lg:grid-cols-[minmax(0,1fr)_minmax(300px,360px)] lg:items-start" : ""}>
        {comparing && gap ? (
          <ComparisonVerdict variable={variable} a={ready[0]} b={ready[1]} gap={gap} />
        ) : (
          <Verdict variable={variable} place={ready[0]} />
        )}
        {aside}
      </div>

      {lens}

      <div className="card mt-6 p-4 sm:p-5">
        <TrendChart
          animate={animate}
          series={ready.map((p) => ({
            letter: p.letter,
            label: comparing ? p.label : variable.name,
            color: p.color,
            points: p.points,
            trend: p.trend,
            offset: comparing && mode !== "anomaly" ? p.trend.mean : 0,
          }))}
          unit={variable.unit}
          axisLabel={axisLabel}
          showMarkers={mode !== "anomaly"}
          relative={relative}
        />
      </div>

      <h3 className="display mt-10 text-2xl">The evidence</h3>
      <EvidenceTable variable={variable} places={ready} gap={gap} mode={mode} month={month} />

      <Cautions variable={variable} trends={ready.map((p) => p.trend)} comparing={comparing} />

      <details className="mt-8">
        <summary className="cursor-pointer text-ink-2 underline underline-offset-4 hover:text-ink">
          Show the numbers behind the chart
        </summary>
        <div className="mt-3">
          <button
            type="button"
            onClick={() => downloadCsv(ready, variable)}
            className="rounded-md bg-ink px-3 py-1.5 text-sm text-paper hover:opacity-90"
          >
            Download as CSV
          </button>
        </div>
        <NumbersTable places={ready} unitText={unitText} />
      </details>
    </div>
  );
}

/* ---------- Verdicts ---------- */

interface Reading {
  level: 1 | 2 | 3;
  levelLabel: string;
  /** "is rising", "may be falling" or "shows no clear trend" */
  phrase: string;
  kind: "trend" | "maybe" | "none" | "unavailable";
}

function read(trend: TrendResult): Reading {
  if (!trend.inference.established) return { level: 1, levelLabel: "Significance not established", phrase: "has an estimated trend requiring review", kind: "unavailable" };
  const verb = trend.direction === "rising" ? "rising" : "falling";
  if (trend.direction === "flat" || trend.strength === "not-significant") {
    return { level: 1, levelLabel: "Little evidence of a trend", phrase: "shows no clear trend", kind: "none" };
  }
  if (trend.strength === "borderline") {
    return { level: 2, levelLabel: "Mixed evidence", phrase: `may be ${verb}`, kind: "maybe" };
  }
  return { level: 3, levelLabel: "Strong evidence of a trend", phrase: `is ${verb}`, kind: "trend" };
}

function Meter({ level, letter, color }: { level: number; letter?: string; color?: string }) {
  return (
    <span className="flex items-center gap-1.5" aria-hidden>
      {letter && (
        <span
          className="mr-1 grid h-6 w-6 place-items-center rounded-full text-xs font-bold text-white"
          style={{ background: color }}
        >
          {letter}
        </span>
      )}
      {[1, 2, 3].map((i) => (
        <span
          key={i}
          className={`h-3 w-7 rounded-sm border border-ink ${i <= level ? "bg-ink" : "bg-transparent"}`}
        />
      ))}
    </span>
  );
}

function Verdict({ variable, place }: { variable: Variable; place: Analysed }) {
  const { trend } = place;
  const reading = read(trend);
  const rate = withUnit(sig(Math.abs(trend.sen.slope * 10)), variable.unit);
  const total = withUnit(signed(trend.sen.slope * trend.spanYears), variable.unit);
  const lean = trend.direction === "rising" ? "upward" : "downward";
  const subject = `${variable.name} around ${place.label}`;
  const firstYear = Math.floor(place.points[0].t);

  let headline: string;
  let detail: string;
  if (reading.kind === "unavailable") {
    headline = `Significance not established for ${subject}.`;
    detail = `The estimated robust rate is ${rate} per decade (${trend.direction}). ${trend.inference.reasons.join(" ")}`;
  } else if (reading.kind === "none") {
    headline = `${subject} shows no clear trend.`;
    detail =
      trend.direction === "flat"
        ? "The best estimate of the change is zero."
        : `The best estimate leans ${lean}, by ${rate} per decade, but the primary OLS test does not reject a zero slope under its assumptions. This does not prove that no change occurred.`;
  } else if (reading.kind === "maybe") {
    headline = `${subject} ${reading.phrase}, by about ${rate} per decade.`;
    detail =
      "The two tests do not agree firmly enough to call it. Treat this as a lead worth following, not a finding: try a larger region or a different way of reading time.";
  } else {
    headline = `${subject} ${reading.phrase} by ${rate} per decade.`;
    detail = `The primary OLS slope test rejects a zero slope under its reviewed assumptions. The robust fitted change since ${firstYear} is ${total}; this is not an endpoint difference or a causal claim.`;
  }

  return (
    <div aria-live="polite">
      <p className="display max-w-[24ch] text-[clamp(2rem,4.6vw,3.6rem)] text-balance">{headline}</p>
      <div className="mt-5 flex flex-wrap items-center gap-x-4 gap-y-2">
        <Meter level={reading.level} />
        <span className="font-semibold">{reading.levelLabel}</span>
      </div>
      <p className="mt-3 max-w-[62ch] text-lg text-ink-2">{detail}</p>
    </div>
  );
}

function ComparisonVerdict({
  variable,
  a,
  b,
  gap,
}: {
  variable: Variable;
  a: Analysed;
  b: Analysed;
  gap: SlopeDifference;
}) {
  const ra = read(a.trend);
  const rb = read(b.trend);
  const perDecade = (t: TrendResult) => `${withUnit(signed(t.sen.slope * 10), variable.unit)} per decade`;
  const opposite =
    ra.kind !== "none" && rb.kind !== "none" && a.trend.direction !== b.trend.direction;

  let headline: string;
  if (ra.kind === "unavailable" || rb.kind === "unavailable") {
    headline = "Regional rates are descriptive; significance is not established.";
  } else if (ra.kind === "none" && rb.kind === "none") {
    headline = `${variable.name} shows no clear trend in either place.`;
  } else if (ra.phrase === rb.phrase) {
    const faster = Math.abs(a.trend.sen.slope) >= Math.abs(b.trend.sen.slope) ? a : b;
    headline = gap.significant
      ? `${variable.name} ${ra.phrase} in both places, faster around ${faster.label}.`
      : `${variable.name} ${ra.phrase} in both places, with no clear difference in rate.`;
  } else {
    const sameWay = ra.kind !== "none" && rb.kind !== "none" && !opposite;
    headline = `${variable.name} ${ra.phrase} around ${a.label} ${sameWay ? "and" : "but"} ${rb.phrase} around ${b.label}.`;
  }

  const gapSentence = !gap.established
    ? `Difference-test significance is not established. ${gap.reason}`
    : `The OLS test on paired A minus B values ${gap.significant ? "rejects" : "does not reject"} a zero slope (p ${pEquals(gap.p)}), conditional on its assumptions.`;

  return (
    <div aria-live="polite">
      <p className="display max-w-[30ch] text-[clamp(1.8rem,4vw,3.1rem)] text-balance">{headline}</p>
      <ul className="mt-5 space-y-2">
        {[
          { place: a, reading: ra },
          { place: b, reading: rb },
        ].map(({ place, reading }) => (
          <li key={place.letter} className="flex flex-wrap items-center gap-x-4 gap-y-1">
            <Meter level={reading.level} letter={place.letter} color={place.color} />
            <span>
              <span className="font-semibold">{place.label}:</span> {reading.levelLabel.toLowerCase()},{" "}
              <span className="tabular">{perDecade(place.trend)}</span>
            </span>
          </li>
        ))}
      </ul>
      <p className="mt-3 max-w-[66ch] text-lg text-ink-2">
        {opposite && "Same measurement, opposite directions: where you look changes the answer. "}
        {gapSentence}
      </p>
    </div>
  );
}

/* ---------- Evidence ---------- */

interface EvidenceRow {
  measure: string;
  /** One result per place, or a single result spanning all places */
  results: string[] | string;
  meaning: string;
}

function EvidenceTable({
  variable,
  places,
  gap,
  mode,
  month,
}: {
  variable: Variable;
  places: Analysed[];
  gap: SlopeDifference | null;
  mode: Mode;
  month: number;
}) {
  const comparing = places.length > 1;
  const first = places[0].trend;
  const each = (f: (p: Analysed) => string) => places.map(f);
  const perDecade = (slope: number) => `${withUnit(signed(slope * 10), variable.unit)} per decade`;
  const years = (p: Analysed) =>
    `${Math.floor(p.points[0].t)} to ${Math.floor(p.points[p.points.length - 1].t)}`;
  const pointKind =
    mode === "annual"
      ? "yearly averages"
      : mode === "anomaly"
        ? "monthly values with the seasons removed"
        : `${MONTH_NAMES[month]} values`;

  const rows: EvidenceRow[] = [
    {
      measure: "Rate of change",
      results: each((p) => perDecade(p.trend.sen.slope)),
      meaning:
        "The typical change between any two points in the record (the Theil-Sen slope). One freak year barely moves it.",
    },
    {
      measure: "Plausible range for that rate",
      results: each((p) => p.trend.inference.established ? `${signed(p.trend.sen.ci[0] * 10)} to ${perDecade(p.trend.sen.ci[1])}` : "Not reported — assumptions unresolved"),
      meaning: "Conditional 95% interval for the robust slope; only reported after assumption review. This is not a prediction interval.",
    },
    {
      measure: "Mann-Kendall test",
      results: each((p) => p.trend.inference.established ? `p ${pEquals(p.trend.mannKendall.p)}, tau ${signed(p.trend.mannKendall.tau)}` : "Not reported — assumptions unresolved"),
      meaning:
        "Asks only whether later values tend to be higher or lower than earlier ones. This is a supplementary asymptotic rank test assuming independent observations; it does not determine the headline verdict.",
    },
    {
      measure: "Straight-line fit",
      results: each((p) => `${perDecade(p.trend.ols.slope)}${p.trend.inference.established ? `, p ${pEquals(p.trend.ols.p)}` : ", significance not established"}`),
      meaning: comparing
        ? "A least-squares line with a t-test on its slope."
        : `A least-squares line with a t-test on its slope. The line accounts for ${Math.round(first.ols.r2 * 100)}% of the ups and downs (R²).`,
    },
    {
      measure: "Exploratory AR(1) sensitivity",
      results: each((p) => p.trend.inference.established ? `p ${pEquals(p.trend.ols.pAdjusted)}` : "Not reported — assumptions unresolved"),
      meaning: "An approximate effective-sample-size sensitivity calculation, not a validated correction or the primary test.",
    },
  ];

  if (gap) {
    rows.push({
      measure: "Gap between the two rates",
      results: `${perDecade(gap.difference)} (A minus B), p ${pEquals(gap.p)}`,
      meaning:
        "Fits the paired A minus B series on common timestamps. Spatial covariance is retained; temporal independence still requires review.",
    });
  }

  rows.push({
    measure: "Change over the whole record",
    results: each(
      (p) => `${withUnit(signed(p.trend.sen.slope * p.trend.spanYears), variable.unit)}, ${years(p)}`,
    ),
    meaning: comparing
      ? "The rate multiplied by the length of the record."
      : `The rate multiplied by the length of the record. The average over that time was ${withUnit(sig(first.mean, 3), variable.unit)}${mode === "anomaly" ? " relative to normal" : ""}.`,
  });

  if (comparing && mode !== "anomaly") {
    rows.push({
      measure: "Average over the record",
      results: each((p) => withUnit(sig(p.trend.mean, 3), variable.unit)),
      meaning: "The chart shows each place relative to this, so two very different places fit on one scale.",
    });
  }

  rows.push({
    measure: "Data used",
    results: each((p) =>
      comparing
        ? `${p.trend.n} points, ${p.record.cells} grid ${p.record.cells === 1 ? "cell" : "cells"}`
        : `${p.trend.n} ${pointKind}`,
    ),
    meaning: comparing
      ? `Each point is one of the ${pointKind}. Regions are averaged with area weighting. Source: ${variable.origin}.`
      : places[0].record.cells > 1
        ? `Averaged over ${places[0].record.cells} grid cells, weighted by area. Source: ${variable.origin}.`
        : `From the single grid cell nearest the point. Source: ${variable.origin}.`,
  });

  return (
    <div className="mt-3 overflow-x-auto">
      <table className="w-full min-w-[640px] border-collapse text-left text-[0.95rem]">
        <thead>
          <tr className="border-b border-line-strong text-sm text-ink-2">
            <th className="w-[20%] py-2 pr-4 font-normal">Measure</th>
            {comparing ? (
              places.map((p) => (
                <th key={p.letter} className="w-[21%] py-2 pr-4 font-normal">
                  <span className="flex items-center gap-2">
                    <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: p.color }} aria-hidden />
                    <span>
                      <span className="font-semibold text-ink">{p.letter}</span> {p.label}
                    </span>
                  </span>
                </th>
              ))
            ) : (
              <th className="w-[28%] py-2 pr-4 font-normal">Result</th>
            )}
            <th className="py-2 font-normal">What it tells you</th>
          </tr>
        </thead>
        <tbody className="tabular align-top">
          {rows.map((row) => (
            <tr key={row.measure} className="border-b border-line">
              <th scope="row" className="py-3 pr-4 font-semibold">
                {row.measure}
              </th>
              {typeof row.results === "string" ? (
                <td colSpan={places.length} className="py-3 pr-4">
                  {row.results}
                </td>
              ) : (
                row.results.map((result, i) => (
                  <td key={places[i].letter} className="py-3 pr-4">
                    {result}
                  </td>
                ))
              )}
              <td className="py-3 text-ink-2">{row.meaning}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Cautions({
  variable,
  trends,
  comparing,
}: {
  variable: Variable;
  trends: TrendResult[];
  comparing: boolean;
}) {
  const notes: string[] = [...new Set(trends.flatMap((t) => t.inference.reasons))];
  if (variable.caveat) notes.push(variable.caveat);
  const span = Math.min(...trends.map((t) => t.spanYears));
  if (span < 30) {
    notes.push(
      `This record covers ${Math.round(span)} years. Natural swings that last a decade or more can pass for a trend in a record this short.`,
    );
  }
  if (trends.some((t) => t.mannKendall.p < t.alpha && t.ols.r2 < 0.2)) {
    notes.push(
      "The direction is consistent but a straight line fits poorly. The change may have happened in a step or a curve, so look at the shape before quoting the rate.",
    );
  }
  if (comparing) {
    notes.push(
      "Opposite trends do not need opposite causes. One shift in winds or ocean temperatures can bring more rain to one region and less to another.",
    );
  }
  notes.push("Significance is conditional on a statistical model; it is not the probability a trend is true, proof of causation, or a measure of practical importance. Repeated exploratory tests are not adjusted for multiple comparisons.");

  return (
    <div className="card mt-8 max-w-[72ch] p-5">
      <h3 className="display text-xl">Before you close the case</h3>
      <ul className="mt-1 list-disc space-y-1 pl-5 text-ink-2">
        {notes.map((note) => (
          <li key={note}>{note}</li>
        ))}
      </ul>
    </div>
  );
}

/* ---------- Numbers ---------- */

/** The places' points lined up by time, for the table and the CSV. */
function alignByTime(places: Analysed[]) {
  const rows = new Map<number, { t: number; label: string; values: (number | null)[] }>();
  places.forEach((place, i) => {
    for (const point of place.points) {
      let row = rows.get(point.t);
      if (!row) {
        row = { t: point.t, label: point.label, values: places.map(() => null) };
        rows.set(point.t, row);
      }
      row.values[i] = point.v;
    }
  });
  return [...rows.values()].sort((x, y) => x.t - y.t);
}

function NumbersTable({ places, unitText }: { places: Analysed[]; unitText: string }) {
  const rows = alignByTime(places);
  const comparing = places.length > 1;
  return (
    <div className="mt-3 max-h-72 max-w-lg overflow-y-auto border border-line bg-surface">
      <table className="tabular w-full text-sm">
        <thead className="sticky top-0 bg-surface text-left text-ink-2">
          <tr>
            <th className="px-3 py-1.5 font-normal">Time</th>
            {places.map((p) => (
              <th key={p.letter} className="px-3 py-1.5 text-right font-normal">
                {comparing ? `${p.letter} ${p.label}` : unitText}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.t} className="border-t border-line">
              <td className="px-3 py-1">{row.label}</td>
              {row.values.map((v, i) => (
                <td key={places[i].letter} className="px-3 py-1 text-right">
                  {v === null ? "" : sig(v, 4)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function pEquals(p: number | null): string {
  if (p === null || !Number.isFinite(p)) return "not established";
  const text = pValue(p);
  return text.startsWith("<") ? text : `= ${text}`;
}

function downloadCsv(places: Analysed[], variable: Variable) {
  const comparing = places.length > 1;
  const clean = (text: string) => text.replaceAll(",", "");
  const meta = [
    `# ${variable.name} (${variable.unit || variable.unitNote}). Source: NASA POWER, ${variable.origin}.`,
    ...places.map((p) => `# ${comparing ? `${p.letter}: ` : ""}around ${clean(p.label)}`),
  ];
  const columns = places.map((p) => (comparing ? `${variable.id}_${p.letter}` : variable.id));
  const header = ["time", "decimal_year", ...columns].join(",");
  const rows = alignByTime(places).map((row) =>
    [row.label, row.t.toFixed(4), ...row.values.map((v) => v ?? "")].join(","),
  );
  const blob = new Blob([[...meta, header, ...rows].join("\n")], { type: "text/csv" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = `${variable.id}-trend-detective.csv`;
  link.click();
  URL.revokeObjectURL(link.href);
}
