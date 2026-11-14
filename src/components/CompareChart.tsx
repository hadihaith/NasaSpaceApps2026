"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import { scaleBand, scaleLinear } from "d3-scale";
import { line } from "d3-shape";
import { sig, withUnit } from "@/lib/format";

export interface CompareSeries {
  key: string;
  label: string;
  /** CSS colour; marks only, never text */
  color: string;
  values: number[];
}

const px = (v: number) => Math.round(v * 100) / 100;
const round4 = (v: number) => Math.round(v * 1e4) / 1e4;

interface Props {
  title: string;
  unit: string;
  xLabels: string[];
  series: CompareSeries[];
  kind: "line" | "bar";
  /** Label every n-th x tick (dense monthly runs) */
  tickEvery?: number;
}

/**
 * Today versus what-if on one value axis. Lines carry direct end labels, bars sit side
 * by side with a surface gap, and hovering shows every series' value for that x.
 */
export function CompareChart({ title, unit, xLabels, series, kind, tickEvery = 1 }: Props) {
  const wrap = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(560);
  const [hover, setHover] = useState<number | null>(null);
  const titleId = useId();

  useEffect(() => {
    if (!wrap.current) return;
    const observer = new ResizeObserver(([entry]) => setWidth(Math.max(260, entry.contentRect.width)));
    observer.observe(wrap.current);
    return () => observer.disconnect();
  }, []);

  const labelRoom = kind === "line" ? 64 : 12;
  const margin = { top: 14, right: labelRoom, bottom: 28, left: 48 };
  const height = 230;
  const innerW = width - margin.left - margin.right;
  const innerH = height - margin.top - margin.bottom;
  const n = xLabels.length;

  // Server and browser can differ in the last bits of floating-point maths, so round the
  // data and the pixel positions; otherwise the server-rendered SVG fails to hydrate.
  const geo = useMemo(() => {
    const all = series.flatMap((s) => s.values.map(round4));
    const lo = Math.min(0, ...all);
    const hi = Math.max(...all, 0.0001);
    const yScale = scaleLinear().domain([lo, hi]).nice(5).range([innerH, 0]);
    const y = Object.assign((v: number) => px(yScale(round4(v))), {
      ticks: (k: number) => yScale.ticks(k),
      // One number format for every tick on an axis, e.g. 0, 5, 10 or 0.0, 0.5, 1.0
      tickFormat: (k: number) => yScale.tickFormat(k),
    });
    const band = scaleBand<number>().domain([...Array(n).keys()]).range([0, innerW]).paddingInner(0.25).paddingOuter(0.1);
    const xLine = scaleLinear().domain([0, Math.max(1, n - 1)]).range([0, innerW]);
    const x = (i: number) => px(kind === "bar" ? band(i)! + band.bandwidth() / 2 : xLine(i));
    return { y, band, x };
  }, [series, n, innerW, innerH, kind]);

  const ticks = geo.y.ticks(5);
  const tickText = geo.y.tickFormat(5);

  // End labels for lines: keep them at least 14 px apart so overlapping lines stay readable.
  const endLabelY = useMemo(() => {
    const order = series
      .map((s, k) => ({ k, y: geo.y(s.values[n - 1]) }))
      .sort((a, b) => a.y - b.y);
    for (let i = 1; i < order.length; i++) {
      if (order[i].y - order[i - 1].y < 14) order[i].y = order[i - 1].y + 14;
    }
    const out: number[] = [];
    for (const o of order) out[o.k] = o.y;
    return out;
  }, [series, geo, n]);
  const path = (values: number[]) =>
    line<number>()
      .x((_, i) => geo.x(i))
      .y((v) => geo.y(v))(values) ?? "";

  function onMove(e: React.PointerEvent<SVGRectElement>) {
    const box = e.currentTarget.getBoundingClientRect();
    const px = e.clientX - box.left;
    let best = 0;
    let dist = Infinity;
    for (let i = 0; i < n; i++) {
      const d = Math.abs(geo.x(i) - px);
      if (d < dist) {
        dist = d;
        best = i;
      }
    }
    setHover(best);
  }

  const barW = geo.band.bandwidth() / series.length;
  const shown = unit === "" ? "" : unit;

  return (
    <figure className="min-w-0">
      <figcaption id={titleId} className="font-bold">
        {title}
      </figcaption>
      <ul className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-ink-2" aria-hidden>
        {series.map((s) => (
          <li key={s.key} className="flex items-center gap-1.5">
            <span className="inline-block h-1 w-4 rounded-full" style={{ background: s.color }} />
            {s.label}
          </li>
        ))}
      </ul>

      <div ref={wrap} className="relative mt-2">
        <svg width={width} height={height} role="img" aria-labelledby={titleId} className="block overflow-visible">
          <g transform={`translate(${margin.left},${margin.top})`}>
            {ticks.map((t) => (
              <g key={t} transform={`translate(0,${geo.y(t)})`}>
                <line x2={innerW} stroke="var(--line)" strokeWidth={1} />
                <text x={-8} dy="0.32em" textAnchor="end" className="tabular fill-[var(--ink-2)] text-[11px]">
                  {tickText(t).replace("-", "−")}
                </text>
              </g>
            ))}
            {xLabels.map((label, i) =>
              i % tickEvery === 0 ? (
                <text
                  key={label + i}
                  x={geo.x(i)}
                  y={innerH + 18}
                  textAnchor="middle"
                  className="fill-[var(--ink-2)] text-[11px]"
                >
                  {label}
                </text>
              ) : null,
            )}

            {kind === "bar"
              ? series.map((s, k) =>
                  s.values.map((v, i) => {
                    const x0 = geo.band(i)! + k * barW;
                    const top = geo.y(Math.max(0, v));
                    const h = Math.abs(geo.y(v) - geo.y(0));
                    return (
                      <rect
                        key={`${s.key}-${i}`}
                        x={px(x0 + 1)}
                        y={top}
                        width={px(Math.max(1, barW - 2))}
                        height={px(Math.max(0.5, h))}
                        rx={px(Math.min(4, barW / 3))}
                        fill={s.color}
                        opacity={hover === null || hover === i ? 1 : 0.45}
                      />
                    );
                  }),
                )
              : series.map((s, k) => {
                  const lastY = endLabelY[k];
                  return (
                    <g key={s.key}>
                      <path d={path(s.values)} fill="none" stroke={s.color} strokeWidth={2} strokeLinejoin="round" />
                      <text x={innerW + 6} y={lastY} dy="0.32em" className="fill-[var(--ink)] text-[11px] font-bold">
                        {s.label}
                      </text>
                    </g>
                  );
                })}

            {hover !== null && kind === "line" && (
              <g>
                <line x1={geo.x(hover)} x2={geo.x(hover)} y2={innerH} stroke="var(--ink-2)" strokeDasharray="3 3" />
                {series.map((s) => (
                  <circle
                    key={s.key}
                    cx={geo.x(hover)}
                    cy={geo.y(s.values[hover])}
                    r={4.5}
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
              onPointerLeave={() => setHover(null)}
            />
          </g>
        </svg>

        {hover !== null && (
          <div
            className="pointer-events-none absolute top-0 z-10 rounded-lg border-2 border-edge bg-surface px-2.5 py-1.5 text-xs shadow-[3px_3px_0_var(--drop)]"
            style={{
              left: Math.min(width - 150, Math.max(0, margin.left + geo.x(hover) + 10)),
            }}
          >
            <p className="font-bold">{xLabels[hover]}</p>
            {series.map((s) => (
              <p key={s.key} className="tabular flex items-center gap-1.5">
                <span className="inline-block h-2 w-2 rounded-full" style={{ background: s.color }} />
                {s.label}: {withUnit(sig(s.values[hover], 3), shown)}
              </p>
            ))}
          </div>
        )}
      </div>

      <details className="mt-2 text-sm">
        <summary className="cursor-pointer text-ink-2">Show as a table</summary>
        <div className="mt-2 max-h-56 overflow-auto">
          <table className="tabular w-full text-left">
            <thead>
              <tr className="text-ink-2">
                <th className="py-1 pr-3 font-bold">{kind === "bar" ? "Month" : "Step"}</th>
                {series.map((s) => (
                  <th key={s.key} className="py-1 pr-3 font-bold">
                    {s.label}
                    {shown ? ` (${shown})` : ""}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {xLabels.map((label, i) => (
                <tr key={label + i} className="border-t border-line">
                  <td className="py-1 pr-3">{label}</td>
                  {series.map((s) => (
                    <td key={s.key} className="py-1 pr-3">
                      {sig(s.values[i], 3)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </figure>
  );
}
