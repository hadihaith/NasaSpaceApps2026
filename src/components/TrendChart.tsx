"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { bisector, extent } from "d3-array";
import { scaleLinear } from "d3-scale";
import { line } from "d3-shape";
import { sig, signed } from "@/lib/format";
import type { SeriesPoint } from "@/lib/series";
import type { TrendResult } from "@/lib/stats/trend";

export interface ChartSeries {
  /** "A" or "B": the direct label at the end of the trend line when comparing */
  letter: string;
  /** Legend text */
  label: string;
  /** CSS colour, e.g. "var(--series-a)" */
  color: string;
  points: SeriesPoint[];
  trend: TrendResult;
  /** Subtracted from every value before plotting, to put two places on one scale */
  offset: number;
}

interface Props {
  /** One place, or two when comparing */
  series: ChartSeries[];
  /** Shown above the value axis */
  axisLabel: string;
  unit: string;
  /** Draw a marker on every point; off for dense monthly series */
  showMarkers: boolean;
  /** Values are differences from a baseline: draw the zero line and sign the numbers */
  relative: boolean;
  /** Draw the lines in the first time the chart appears */
  animate?: boolean;
}

const byTime = bisector<SeriesPoint, number>((d) => d.t).center;

export function TrendChart({ series, axisLabel, unit, showMarkers, relative, animate = false }: Props) {
  const wrap = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(720);
  const [hoverT, setHoverT] = useState<number | null>(null);
  const comparing = series.length > 1;

  // Lines draw in on the first appearance only; later redraws (a new lens) are instant.
  const [intro, setIntro] = useState(animate);
  useEffect(() => {
    if (!intro) return;
    const timer = setTimeout(() => setIntro(false), 2600);
    return () => clearTimeout(timer);
  }, [intro]);
  const drawIn = intro ? "draw-in" : undefined;
  const drawInLate = intro ? "draw-in-late" : undefined;
  const fadeInLate = intro ? "fade-in-late" : undefined;

  useEffect(() => {
    if (!wrap.current) return;
    const observer = new ResizeObserver(([entry]) => setWidth(Math.max(280, entry.contentRect.width)));
    observer.observe(wrap.current);
    return () => observer.disconnect();
  }, []);

  const margin = { top: 28, right: comparing ? 30 : 18, bottom: 30, left: 54 };
  const height = width < 520 ? 280 : 360;
  const innerW = width - margin.left - margin.right;
  const innerH = height - margin.top - margin.bottom;

  const geometry = useMemo(() => {
    const allT = series.flatMap((s) => s.points.map((d) => d.t));
    const [t0, t1] = extent(allT) as [number, number];
    const values: number[] = relative ? [0] : [];

    const drawn = series.map((s) => {
      const [s0, s1] = extent(s.points, (d) => d.t) as [number, number];
      const trendAt = (t: number) => s.trend.sen.intercept + s.trend.sen.slope * t - s.offset;
      // The slope's 95% range, drawn as a wedge pivoting on the middle of the record.
      const mid = (s0 + s1) / 2;
      const wedge = [s0, s1].map((t) => ({
        t,
        lo: trendAt(mid) + s.trend.sen.ci[0] * (t - mid),
        hi: trendAt(mid) + s.trend.sen.ci[1] * (t - mid),
      }));
      for (const d of s.points) values.push(d.v - s.offset);
      for (const w of wedge) values.push(w.lo, w.hi);
      return { ...s, s0, s1, trendAt, wedge };
    });

    const [v0, v1] = extent(values) as [number, number];
    const pad = (v1 - v0 || 1) * 0.06;
    const tPad = (t1 - t0) * 0.015;
    const x = scaleLinear().domain([t0 - tPad, t1 + tPad]).range([0, innerW]);
    const y = scaleLinear().domain([v0 - pad, v1 + pad]).nice(5).range([innerH, 0]);

    const paths = drawn.map((s) =>
      line<SeriesPoint>()
        .x((d) => x(d.t))
        .y((d) => y(d.v - s.offset))(s.points),
    );

    // Keep the end-of-line letters from sitting on top of each other.
    const labelY = drawn.map((s) => y(s.trendAt(s.s1)));
    if (labelY.length === 2 && Math.abs(labelY[0] - labelY[1]) < 15) {
      const upper = labelY[0] <= labelY[1] ? 0 : 1;
      const centre = (labelY[0] + labelY[1]) / 2;
      labelY[upper] = centre - 7.5;
      labelY[1 - upper] = centre + 7.5;
    }

    return { t0, t1, x, y, drawn, paths, labelY };
  }, [series, relative, innerW, innerH]);

  const { t0, t1, x, y, drawn, paths, labelY } = geometry;
  const xTicks = x.ticks(Math.max(3, Math.floor(innerW / 64))).filter(Number.isInteger);
  const yTicks = y.ticks(5);
  const yFormat = y.tickFormat(5);
  const number = (v: number) => (relative ? signed(v, 3) : sig(v, 3));

  // The point of each series nearest the pointer.
  const active =
    hoverT === null
      ? null
      : drawn.map((s) => {
          const point = s.points[byTime(s.points, hoverT)];
          return { s, point };
        });
  const anchor = active?.[0].point ?? null;
  const tooltipLeft = anchor ? margin.left + x(anchor.t) : 0;
  const tooltipOnLeft = tooltipLeft > width * 0.6;

  const onMove = (event: React.PointerEvent<SVGRectElement>) => {
    const bounds = event.currentTarget.getBoundingClientRect();
    setHoverT(x.invert(event.clientX - bounds.left));
  };

  return (
    <figure className="m-0">
      <ul className="mb-2 flex flex-wrap gap-x-5 gap-y-1 text-sm text-ink-2">
        {comparing &&
          series.map((s) => (
            <li key={s.letter} className="flex items-center gap-2">
              <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: s.color }} aria-hidden />
              <span>
                <span className="font-semibold text-ink">{s.letter}</span> {s.label}
              </span>
            </li>
          ))}
        <li className="flex items-center gap-2">
          <LegendLine color={comparing ? "var(--ink-2)" : series[0].color} thin />
          {comparing ? "Measured" : `Measured ${series[0].label.toLowerCase()}`}
        </li>
        <li className="flex items-center gap-2">
          <LegendLine color={comparing ? "var(--ink-2)" : series[0].color} />
          Trend line
        </li>
        <li className="flex items-center gap-2">
          <span
            className="inline-block h-3 w-5 rounded-sm opacity-25"
            style={{ background: comparing ? "var(--ink-2)" : series[0].color }}
            aria-hidden
          />
          Range the trend could plausibly lie in (95%)
        </li>
      </ul>

      <div ref={wrap} className="relative">
        <svg
          width={width}
          height={height}
          role="img"
          aria-label={`${series.map((s) => s.label).join(" and ")} from ${Math.floor(t0)} to ${Math.floor(t1)}, with trend lines. The same numbers are in the table below.`}
          className="tabular block select-none"
        >
          <text x={0} y={12} fontSize={12} fill="var(--ink-2)">
            {axisLabel}
          </text>
          <g transform={`translate(${margin.left},${margin.top})`}>
            {yTicks.map((tick) => (
              <g key={tick} transform={`translate(0,${y(tick)})`}>
                <line x2={innerW} stroke="var(--line)" strokeWidth={1} />
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
            {relative && <line x2={innerW} y1={y(0)} y2={y(0)} stroke="var(--line-strong)" strokeWidth={1} />}

            {drawn.map((s) => (
              <polygon
                key={s.letter}
                className={fadeInLate}
                fill={s.color}
                opacity={0.15}
                points={[
                  [x(s.wedge[0].t), y(s.wedge[0].lo)],
                  [x(s.wedge[1].t), y(s.wedge[1].lo)],
                  [x(s.wedge[1].t), y(s.wedge[1].hi)],
                  [x(s.wedge[0].t), y(s.wedge[0].hi)],
                ]
                  .map((p) => p.join(","))
                  .join(" ")}
              />
            ))}

            {drawn.map((s, i) => (
              <g key={s.letter}>
                <path
                  d={paths[i] ?? ""}
                  pathLength={1}
                  className={drawIn}
                  fill="none"
                  stroke={s.color}
                  strokeWidth={1.5}
                  strokeOpacity={0.7}
                  strokeLinejoin="round"
                />
                {showMarkers &&
                  s.points.map((d) => (
                    <circle
                      key={d.t}
                      className={fadeInLate}
                      cx={x(d.t)}
                      cy={y(d.v - s.offset)}
                      r={3}
                      fill={s.color}
                      stroke="var(--surface)"
                      strokeWidth={1.25}
                    />
                  ))}
              </g>
            ))}

            {/* Trend lines sit on top, with a surface-coloured halo so they read against the data. */}
            {drawn.map((s, i) => {
              const ends = { x1: x(s.s0), y1: y(s.trendAt(s.s0)), x2: x(s.s1), y2: y(s.trendAt(s.s1)) };
              return (
                <g key={s.letter}>
                  <line {...ends} pathLength={1} className={drawInLate} stroke="var(--surface)" strokeWidth={6} strokeLinecap="round" />
                  <line {...ends} pathLength={1} className={drawInLate} stroke={s.color} strokeWidth={3} strokeLinecap="round" />
                  {comparing && (
                    <text className={fadeInLate} x={ends.x2 + 10} y={labelY[i]} dy="0.35em" fontSize={13} fontWeight={700} fill="var(--ink)">
                      {s.letter}
                    </text>
                  )}
                </g>
              );
            })}

            {active && anchor && (
              <g pointerEvents="none">
                <line x1={x(anchor.t)} x2={x(anchor.t)} y2={innerH} stroke="var(--line-strong)" strokeWidth={1} />
                {active.map(({ s, point }) => (
                  <circle
                    key={s.letter}
                    cx={x(point.t)}
                    cy={y(point.v - s.offset)}
                    r={5.5}
                    fill={s.color}
                    stroke="var(--surface)"
                    strokeWidth={2}
                  />
                ))}
              </g>
            )}

            <rect
              width={innerW}
              height={innerH}
              fill="transparent"
              onPointerMove={onMove}
              onPointerLeave={() => setHoverT(null)}
            />
          </g>
        </svg>

        {active && anchor && (
          <div
            className="tabular pointer-events-none absolute top-8 z-10 rounded-md border border-line-strong bg-surface px-3 py-2 text-sm shadow-sm"
            style={{
              left: tooltipLeft,
              transform: tooltipOnLeft ? "translateX(calc(-100% - 12px))" : "translateX(12px)",
            }}
          >
            <div className="text-ink-2">{anchor.label}</div>
            {active.map(({ s, point }) => (
              <div key={s.letter} className="flex items-baseline gap-2 whitespace-nowrap">
                <span
                  className="inline-block h-2.5 w-2.5 shrink-0 translate-y-px rounded-full"
                  style={{ background: s.color }}
                  aria-hidden
                />
                {comparing && <span className="font-semibold">{s.letter}</span>}
                <span className="font-semibold">
                  {number(point.v - s.offset)} {unit}
                </span>
                <span className="text-ink-2">trend {number(s.trendAt(point.t))}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </figure>
  );
}

function LegendLine({ color, thin = false }: { color: string; thin?: boolean }) {
  return (
    <span
      className="inline-block w-5 rounded-full"
      style={{ background: color, height: thin ? 1.5 : 3, opacity: thin ? 0.7 : 1 }}
      aria-hidden
    />
  );
}
