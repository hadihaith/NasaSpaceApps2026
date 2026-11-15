"use client";

import { useMemo, useState } from "react";
import { bisector, extent } from "d3-array";
import { scaleLinear } from "d3-scale";
import { area, line } from "d3-shape";
import { normalDensity, tDensity, type ChallengeStats } from "@/lib/challenge";
import { sig, signed } from "@/lib/format";
import type { SeriesPoint } from "@/lib/series";
import { useWidth } from "./hooks";

const byTime = bisector<SeriesPoint, number>((d) => d.t).center;

interface SeriesPlotProps {
  points: SeriesPoint[];
  axisLabel: string;
  unit: string;
  /** Values are differences from normal: draw the zero line and sign the numbers */
  relative?: boolean;
  markers?: boolean;
  /** How long the series takes to draw in, ms */
  duration?: number;
  stats?: ChallengeStats | null;
  showFit?: boolean;
  showBand?: boolean;
  height?: number;
  label: string;
}

/**
 * A time series that draws itself in: points pop in left to right while the
 * connecting line traces after them. Fit lines and the confidence band mount
 * later, each with its own entrance, when the analysis reaches them.
 */
export function SeriesPlot({
  points,
  axisLabel,
  unit,
  relative = false,
  markers = true,
  duration = 1600,
  stats = null,
  showFit = false,
  showBand = false,
  height: fixedHeight,
  label,
}: SeriesPlotProps) {
  const [wrap, width] = useWidth<HTMLDivElement>();
  const [hoverT, setHoverT] = useState<number | null>(null);
  const height = fixedHeight ?? (width < 520 ? 250 : 320);
  const margin = { top: 26, right: 16, bottom: 30, left: 52 };
  const innerW = width - margin.left - margin.right;
  const innerH = height - margin.top - margin.bottom;

  const g = useMemo(() => {
    const values = points.map((p) => p.v);
    if (relative) values.push(0);
    if (stats) for (const b of stats.band) values.push(b.lo, b.hi);
    const [t0, t1] = extent(points, (p) => p.t) as [number, number];
    const [v0, v1] = extent(values) as [number, number];
    const pad = (v1 - v0 || 1) * 0.06;
    const tPad = (t1 - t0 || 1) * 0.015;
    const x = scaleLinear().domain([t0 - tPad, t1 + tPad]).range([0, innerW]);
    // Rain, wetness and similar never go below zero, so neither does their axis.
    const y = scaleLinear().domain([v0 >= 0 ? Math.max(0, v0 - pad) : v0 - pad, v1 + pad]).nice(5).range([innerH, 0]);
    const path = line<SeriesPoint>().x((d) => x(d.t)).y((d) => y(d.v))(points) ?? "";
    const bandPath = stats
      ? area<ChallengeStats["band"][number]>().x((b) => x(b.t)).y0((b) => y(b.lo)).y1((b) => y(b.hi))(stats.band) ?? ""
      : "";
    return { t0, t1, x, y, path, bandPath };
  }, [points, relative, stats, innerW, innerH]);

  const { t0, t1, x, y, path, bandPath } = g;
  const xTicks = x.ticks(Math.max(3, Math.floor(innerW / 70))).filter(Number.isInteger);
  const yTicks = y.ticks(5);
  const yFormat = y.tickFormat(5);
  const fmt = (v: number) => (relative ? signed(v, 3) : sig(v, 3));
  const hovered = hoverT === null ? null : points[byTime(points, hoverT)];
  const tipLeft = hovered ? margin.left + x(hovered.t) : 0;
  const n = points.length;

  const fitEnds = stats && {
    ols: { x1: x(stats.t0), y1: y(stats.ols.intercept + stats.ols.slope * stats.t0), x2: x(stats.t1), y2: y(stats.ols.intercept + stats.ols.slope * stats.t1) },
    sen: { x1: x(stats.t0), y1: y(stats.sen.intercept + stats.sen.slope * stats.t0), x2: x(stats.t1), y2: y(stats.sen.intercept + stats.sen.slope * stats.t1) },
  };

  return (
    <figure className="m-0">
      {stats && (showFit || showBand) && (
        <ul className="mb-2 flex flex-wrap gap-x-5 gap-y-1 text-sm text-ink-2">
          <li className="flex items-center gap-2">
            <span className="inline-block h-2.5 w-2.5 rounded-full bg-series-a" aria-hidden /> Data
          </li>
          {showFit && (
            <>
              <li className="ch-fade flex items-center gap-2">
                <span className="inline-block h-[3px] w-5 rounded-full bg-ink" aria-hidden /> Least-squares line (t-test)
              </li>
              <li className="ch-fade flex items-center gap-2">
                <span className="inline-block w-5 border-t-[3px] border-dashed border-series-b" aria-hidden /> Theil-Sen line
              </li>
            </>
          )}
          {showBand && (
            <li className="ch-fade flex items-center gap-2">
              <span className="inline-block h-3 w-5 rounded-sm bg-series-a/25" aria-hidden /> 95% confidence band
            </li>
          )}
        </ul>
      )}
      <div ref={wrap} className="relative">
        <svg width={width} height={height} role="img" aria-label={`${label}, ${Math.floor(t0)} to ${Math.floor(t1)}`} className="tabular block select-none">
          <text x={0} y={12} fontSize={12} fill="var(--ink-2)">
            {axisLabel}
          </text>
          <g transform={`translate(${margin.left},${margin.top})`}>
            {yTicks.map((tick) => (
              <g key={tick} transform={`translate(0,${y(tick)})`}>
                <line x2={innerW} stroke="var(--line)" />
                <text x={-10} dy="0.32em" textAnchor="end" fontSize={12} fill="var(--muted)">
                  {yFormat(tick)}
                </text>
              </g>
            ))}
            {xTicks.map((tick) => (
              <text key={tick} x={x(tick)} y={innerH + 20} textAnchor="middle" fontSize={12} fill="var(--muted)">
                {tick}
              </text>
            ))}
            {relative && <line x2={innerW} y1={y(0)} y2={y(0)} stroke="var(--line-strong)" strokeDasharray="4 4" />}

            {showBand && bandPath && <path className="ch-fade" d={bandPath} fill="var(--series-a)" fillOpacity={0.18} />}

            <path
              d={path}
              pathLength={1}
              className="ch-draw"
              style={{ animationDuration: `${duration}ms` }}
              fill="none"
              stroke="var(--series-a)"
              strokeWidth={markers ? 1.5 : 1.25}
              strokeOpacity={markers ? 0.55 : 0.8}
              strokeLinejoin="round"
            />
            {markers &&
              points.map((p, i) => (
                <circle
                  key={p.t}
                  className="ch-dot"
                  style={{ animationDelay: `${(i / n) * duration}ms` }}
                  cx={x(p.t)}
                  cy={y(p.v)}
                  r={3.6}
                  fill="var(--series-a)"
                  stroke="var(--surface)"
                  strokeWidth={1.25}
                />
              ))}

            {showFit && fitEnds && (
              <g>
                <line {...fitEnds.ols} pathLength={1} className="ch-draw" stroke="var(--surface)" strokeWidth={7} strokeLinecap="round" />
                <line {...fitEnds.ols} pathLength={1} className="ch-draw" stroke="var(--ink)" strokeWidth={3} strokeLinecap="round" />
                <line
                  {...fitEnds.sen}
                  pathLength={1}
                  className="ch-fade"
                  style={{ animationDelay: "700ms" }}
                  stroke="var(--series-b)"
                  strokeWidth={2.5}
                  strokeDasharray="7 5"
                  strokeLinecap="round"
                />
              </g>
            )}

            {hovered && (
              <g pointerEvents="none">
                <line x1={x(hovered.t)} x2={x(hovered.t)} y2={innerH} stroke="var(--line-strong)" />
                <circle cx={x(hovered.t)} cy={y(hovered.v)} r={5.5} fill="var(--series-a)" stroke="var(--surface)" strokeWidth={2} />
              </g>
            )}
            <rect
              width={Math.max(innerW, 0)}
              height={Math.max(innerH, 0)}
              fill="transparent"
              onPointerMove={(e) => {
                const bounds = e.currentTarget.getBoundingClientRect();
                setHoverT(x.invert(e.clientX - bounds.left));
              }}
              onPointerLeave={() => setHoverT(null)}
            />
          </g>
        </svg>
        {hovered && (
          <div
            className="tabular pointer-events-none absolute top-6 z-10 rounded-md border border-line-strong bg-surface px-3 py-2 text-sm shadow-sm"
            style={{ left: tipLeft, transform: tipLeft > width * 0.6 ? "translateX(calc(-100% - 12px))" : "translateX(12px)" }}
          >
            <div className="text-ink-2">{hovered.label}</div>
            <div className="font-semibold">
              {fmt(hovered.v)} {unit}
            </div>
            {stats && showFit && (
              <div className="text-ink-2">line {fmt(stats.ols.intercept + stats.ols.slope * hovered.t)}</div>
            )}
          </div>
        )}
      </div>
    </figure>
  );
}

/**
 * A sampling distribution under "no trend", with the 5% rejection tails shaded
 * and the observed statistic sliding out from zero to where it actually lands.
 */
export function DistributionPlot({
  kind,
  df = 30,
  stat,
  crit,
  statLabel,
}: {
  kind: "t" | "z";
  df?: number;
  stat: number;
  crit: number;
  statLabel: string;
}) {
  const [wrap, width] = useWidth<HTMLDivElement>(480);
  const height = 170;
  const margin = { top: 30, right: 14, bottom: 26, left: 14 };
  const innerW = width - margin.left - margin.right;
  const innerH = height - margin.top - margin.bottom;
  const density = kind === "t" ? (v: number) => tDensity(v, df) : normalDensity;
  const range = Math.min(Math.max(4, Math.abs(stat) * 1.15 + 0.6, crit * 1.6), 12);
  const x = scaleLinear().domain([-range, range]).range([0, innerW]);
  const y = scaleLinear().domain([0, density(0) * 1.08]).range([innerH, 0]);
  const samples = Array.from({ length: 241 }, (_, i) => -range + (2 * range * i) / 240);
  const curve = line<number>().x((v) => x(v)).y((v) => y(density(v)))(samples) ?? "";
  const tail = area<number>().x((v) => x(v)).y0(innerH).y1((v) => y(density(v)));
  const left = tail(samples.filter((v) => v <= -crit).concat(-crit)) ?? "";
  const right = tail([crit, ...samples.filter((v) => v >= crit)]) ?? "";
  const clamped = Math.max(-range, Math.min(range, stat));
  const off = clamped !== stat;
  const sx = x(clamped);
  const significant = Math.abs(stat) > crit;
  const color = significant ? "var(--good)" : "var(--close)";

  return (
    <div ref={wrap}>
      <svg width={width} height={height} className="tabular block" role="img" aria-label={`${statLabel} compared with the ${kind === "t" ? "t" : "normal"} distribution expected when there is no trend`}>
        <g transform={`translate(${margin.left},${margin.top})`}>
          <path className="ch-fade" d={left} fill="var(--miss)" fillOpacity={0.22} />
          <path className="ch-fade" d={right} fill="var(--miss)" fillOpacity={0.22} />
          <path d={curve} pathLength={1} className="ch-draw" fill="none" stroke="var(--ink)" strokeWidth={2} />
          <line x2={innerW} y1={innerH} y2={innerH} stroke="var(--line-strong)" />
          {[-crit, crit].map((c) => (
            <g key={c} className="ch-fade">
              <line x1={x(c)} x2={x(c)} y1={innerH} y2={y(density(c)) - 14} stroke="var(--miss)" strokeDasharray="3 3" />
              <text x={x(c)} y={innerH + 17} fontSize={11} textAnchor="middle" fill="var(--miss)">
                {c < 0 ? "−" : "+"}
                {crit.toFixed(2)}
              </text>
            </g>
          ))}
          <text x={x(0)} y={innerH + 17} fontSize={11} textAnchor="middle" fill="var(--muted)">
            0
          </text>
          <g className="ch-slide" style={{ ["--from" as string]: `${x(0) - sx}px` }}>
            <g transform={`translate(${sx},0)`}>
              <line y1={-8} y2={innerH} stroke={color} strokeWidth={3} />
              <circle cy={-8} r={5} fill={color} />
              <text
                y={-16}
                fontSize={12}
                fontWeight={800}
                textAnchor={sx > innerW * 0.8 ? "end" : sx < innerW * 0.2 ? "start" : "middle"}
                fill="var(--ink)"
              >
                {statLabel}
                {off ? (stat > 0 ? " →" : " ←") : ""}
              </text>
            </g>
          </g>
        </g>
      </svg>
      <p className="mt-1 flex items-center gap-2 text-sm text-ink-2">
        <span className="inline-block h-3 w-5 rounded-sm bg-miss/30" aria-hidden />
        Red tails beyond ±{crit.toFixed(2)}: where chance alone lands only 5% of the time
      </p>
    </div>
  );
}

export interface IntervalRow {
  label: string;
  estimate: number;
  lo: number;
  hi: number;
}

/** Slope estimates with 95% intervals, set against a dashed "no change" line at zero. */
export function IntervalPlot({ rows, unit }: { rows: IntervalRow[]; unit: string }) {
  const [wrap, width] = useWidth<HTMLDivElement>(520);
  const labelW = width < 480 ? 0 : 170;
  const rowH = width < 480 ? 54 : 40;
  const margin = { top: 14, right: 22, bottom: 30, left: labelW + 20 };
  const innerW = width - margin.left - margin.right;
  const height = margin.top + margin.bottom + rows.length * rowH;
  const all = rows.flatMap((r) => [r.lo, r.hi, r.estimate]).concat(0);
  const [lo, hi] = extent(all) as [number, number];
  const pad = (hi - lo || 1) * 0.08;
  const x = scaleLinear().domain([lo - pad, hi + pad]).nice(5).range([0, innerW]);

  return (
    <div ref={wrap}>
      <svg width={width} height={height} className="tabular block" role="img" aria-label={`95% confidence intervals for the rate of change, in ${unit} per decade`}>
        <g transform={`translate(${margin.left},${margin.top})`}>
          {x.ticks(width < 480 ? 3 : 5).map((tick) => (
            <g key={tick}>
              <line x1={x(tick)} x2={x(tick)} y2={rows.length * rowH} stroke="var(--line)" />
              <text x={x(tick)} y={rows.length * rowH + 18} fontSize={11} textAnchor="middle" fill="var(--muted)">
                {signed(tick, 2)}
              </text>
            </g>
          ))}
          <line x1={x(0)} x2={x(0)} y1={-6} y2={rows.length * rowH} stroke="var(--miss)" strokeWidth={2} strokeDasharray="5 4" />
          {rows.map((r, i) => {
            const cy = i * rowH + rowH / 2 + (labelW ? 0 : 8);
            const excludesZero = r.lo > 0 || r.hi < 0;
            const color = excludesZero ? "var(--good)" : "var(--close)";
            return (
              <g key={r.label}>
                <text x={labelW ? -12 : 0} y={labelW ? cy : cy - 16} dy="0.35em" fontSize={12} textAnchor={labelW ? "end" : "start"} fill="var(--ink-2)">
                  {r.label}
                </text>
                <g className="ch-grow-x" style={{ animationDelay: `${300 + i * 350}ms` }}>
                  <line x1={x(r.lo)} x2={x(r.hi)} y1={cy} y2={cy} stroke={color} strokeWidth={5} strokeLinecap="round" />
                  <line x1={x(r.lo)} x2={x(r.lo)} y1={cy - 7} y2={cy + 7} stroke={color} strokeWidth={2.5} />
                  <line x1={x(r.hi)} x2={x(r.hi)} y1={cy - 7} y2={cy + 7} stroke={color} strokeWidth={2.5} />
                </g>
                <circle className="ch-dot" style={{ animationDelay: `${i * 350}ms` }} cx={x(r.estimate)} cy={cy} r={6} fill="var(--surface)" stroke="var(--ink)" strokeWidth={2.5} />
              </g>
            );
          })}
          <text x={x(0) + 6} y={-1} fontSize={11} fontWeight={700} fill="var(--miss)">
            no change
          </text>
        </g>
      </svg>
    </div>
  );
}

export interface CompareSeries {
  letter: "A" | "B";
  label: string;
  color: string;
  points: SeriesPoint[];
  stats: ChallengeStats;
}

/**
 * Two places on one chart. Each is drawn relative to its own average, so a
 * desert and a rainforest share a scale. A draws first, then B, then both
 * Theil-Sen lines.
 */
export function ComparePlot({ series, unit, markers }: { series: CompareSeries[]; unit: string; markers: boolean }) {
  const [wrap, width] = useWidth<HTMLDivElement>();
  const height = width < 520 ? 260 : 330;
  const margin = { top: 26, right: 28, bottom: 30, left: 52 };
  const innerW = width - margin.left - margin.right;
  const innerH = height - margin.top - margin.bottom;
  const drawn = series.map((s) => ({ ...s, mean: s.stats.mean }));
  const allT = drawn.flatMap((s) => s.points.map((p) => p.t));
  const allV = drawn.flatMap((s) => s.points.map((p) => p.v - s.mean)).concat(0);
  const [t0, t1] = extent(allT) as [number, number];
  const [v0, v1] = extent(allV) as [number, number];
  const pad = (v1 - v0 || 1) * 0.06;
  const x = scaleLinear().domain([t0 - (t1 - t0) * 0.015, t1 + (t1 - t0) * 0.015]).range([0, innerW]);
  const y = scaleLinear().domain([v0 - pad, v1 + pad]).nice(5).range([innerH, 0]);
  const yFormat = y.tickFormat(5);

  return (
    <figure className="m-0">
      <ul className="mb-2 flex flex-wrap gap-x-5 gap-y-1 text-sm text-ink-2">
        {drawn.map((s) => (
          <li key={s.letter} className="flex items-center gap-2">
            <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: s.color }} aria-hidden />
            <span>
              <strong className="text-ink">{s.letter}</strong> {s.label}
            </span>
          </li>
        ))}
        <li className="flex items-center gap-2">
          <span className="inline-block h-[3px] w-5 rounded-full bg-ink-2" aria-hidden /> Trend lines (Theil-Sen)
        </li>
      </ul>
      <div ref={wrap}>
        <svg width={width} height={height} className="tabular block" role="img" aria-label={`${drawn.map((s) => s.label).join(" and ")}, each relative to its own average, with trend lines`}>
          <text x={0} y={12} fontSize={12} fill="var(--ink-2)">
            {width < 520 ? "Vs own average" : "Difference from each place\u2019s own average"}
            {unit ? ` (${unit})` : ""}
          </text>
          <g transform={`translate(${margin.left},${margin.top})`}>
            {y.ticks(5).map((tick) => (
              <g key={tick} transform={`translate(0,${y(tick)})`}>
                <line x2={innerW} stroke="var(--line)" />
                <text x={-10} dy="0.32em" textAnchor="end" fontSize={12} fill="var(--muted)">
                  {yFormat(tick)}
                </text>
              </g>
            ))}
            {x.ticks(Math.max(3, Math.floor(innerW / 70))).filter(Number.isInteger).map((tick) => (
              <text key={tick} x={x(tick)} y={innerH + 20} textAnchor="middle" fontSize={12} fill="var(--muted)">
                {tick}
              </text>
            ))}
            <line x2={innerW} y1={y(0)} y2={y(0)} stroke="var(--line-strong)" strokeDasharray="4 4" />
            {drawn.map((s, k) => {
              const d = line<SeriesPoint>().x((p) => x(p.t)).y((p) => y(p.v - s.mean))(s.points) ?? "";
              const delay = k * 900;
              return (
                <g key={s.letter}>
                  <path d={d} pathLength={1} className="ch-draw" style={{ animationDelay: `${delay}ms`, animationDuration: "1400ms" }} fill="none" stroke={s.color} strokeWidth={1.5} strokeOpacity={0.6} />
                  {markers &&
                    s.points.map((p, i) => (
                      <circle key={p.t} className="ch-dot" style={{ animationDelay: `${delay + (i / s.points.length) * 1400}ms` }} cx={x(p.t)} cy={y(p.v - s.mean)} r={3} fill={s.color} stroke="var(--surface)" strokeWidth={1} />
                    ))}
                </g>
              );
            })}
            {drawn.map((s) => {
              const at = (t: number) => s.stats.sen.intercept + s.stats.sen.slope * t - s.mean;
              const ends = { x1: x(s.stats.t0), y1: y(at(s.stats.t0)), x2: x(s.stats.t1), y2: y(at(s.stats.t1)) };
              return (
                <g key={`trend-${s.letter}`}>
                  <line {...ends} pathLength={1} className="ch-draw" style={{ animationDelay: "2000ms" }} stroke="var(--surface)" strokeWidth={7} strokeLinecap="round" />
                  <line {...ends} pathLength={1} className="ch-draw" style={{ animationDelay: "2000ms" }} stroke={s.color} strokeWidth={3.5} strokeLinecap="round" />
                  <text className="ch-fade" style={{ animationDelay: "2800ms" }} x={ends.x2 + 8} y={ends.y2} dy="0.35em" fontSize={13} fontWeight={800} fill="var(--ink)">
                    {s.letter}
                  </text>
                </g>
              );
            })}
          </g>
        </svg>
      </div>
    </figure>
  );
}
