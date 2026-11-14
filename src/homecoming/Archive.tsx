"use client";

import { useMemo, useState } from "react";
import { TrendChart, type ChartSeries } from "@/components/TrendChart";
import { toSeries, type Mode } from "@/lib/series";
import { analyzeTrend } from "@/lib/stats/trend";
import { EARTH_REGIONS } from "@/earth/regions";
import { CrewLine } from "./Crew";
import type { ClimateCatalog, Investigation, WeatherKey } from "./model";
import IMERG from "./imerg.json";

const VARIABLES: Record<WeatherKey, { label: string; unit: string }> = {
  temperature: { label: "Air temperature", unit: "°C" },
  rain: { label: "Precipitation", unit: "mm/month" },
  humidity: { label: "Relative humidity", unit: "%" },
  wind: { label: "Wind speed", unit: "m/s" },
  soil: { label: "Root-zone soil wetness", unit: "fraction" },
};
const color = "#91edff";
type ArchiveMode = Extract<Mode, "annual" | "anomaly">;

function toAnalysis(values: Record<string, number | null>, start: number, end: number, mode: ArchiveMode) {
  const monthly = Array.from({ length: (end - start + 1) * 12 }, (_, i) =>
    values[`${start + Math.floor(i / 12)}${String(i % 12 + 1).padStart(2, "0")}`] ?? null);
  const points = toSeries({ startYear: start, monthly }, mode);
  return { points, trend: analyzeTrend(points) };
}

export function Archive({ climate, initialRegion, initialVariable, investigations, record }: {
  climate: ClimateCatalog; initialRegion: string; initialVariable: WeatherKey; investigations: Investigation[]; record: (entry: Investigation) => void;
}) {
  const [region, setRegion] = useState(initialRegion);
  const [variable, setVariable] = useState(initialVariable);
  const [source, setSource] = useState<"power" | "imerg">("power");
  const [start, setStart] = useState<number | null>(null);
  const [end, setEnd] = useState<number | null>(null);
  const [mode, setMode] = useState<ArchiveMode>("annual");
  const [compare, setCompare] = useState<WeatherKey | "">("");
  const actualSource = region === "phoenix" && variable === "rain" ? source : "power";
  const title = VARIABLES[variable].label;
  const unit = VARIABLES[variable].unit;
  const dataset = actualSource === "imerg" ? "NASA GPM IMERG Final V07" : "NASA POWER · MERRA-2";
  const recordValues = useMemo<Record<string, number | null>>(() => actualSource === "imerg"
    ? Object.fromEntries(IMERG.observations.map((o) => [o.date.replace("-", ""), o.value]))
    : climate[region].series[variable], [actualSource, climate, region, variable]);
  const years = Object.keys(recordValues).map((date) => Number(date.slice(0, 4)));
  const earliest = Math.min(...years);
  const latest = Math.max(...years);
  const from = Math.max(earliest, Math.min(latest, start ?? earliest));
  const to = Math.max(from, Math.min(latest, end ?? latest));
  const { points, trend } = useMemo(() => toAnalysis(recordValues, from, to, mode), [recordValues, from, to, mode]);
  const compareData = useMemo(() => compare && compare !== variable
    ? toAnalysis(climate[region].series[compare], from, to, mode) : null, [climate, compare, from, to, mode, region, variable]);
  const place = EARTH_REGIONS.find((item) => item.id === region)?.name ?? region;
  const significant = trend?.strength === "significant";
  const verdict = trend
    ? significant ? `Significant ${trend.direction} trend` : trend.strength === "borderline" ? "Mixed evidence" : "No clear trend"
    : "Too little data";
  const slope = trend ? `${trend.sen.slope > 0 ? "+" : ""}${(trend.sen.slope * 10).toFixed(3)} ${unit} / decade` : "—";
  const p = (value: number) => value < .001 ? "<.001" : value.toFixed(3);
  const test = trend ? `Mann–Kendall p ${p(trend.mannKendall.p)} · adjusted regression p ${p(trend.ols.pAdjusted)}` : "";
  const period = `${from}–${to}`;
  const saved = investigations.some((entry) => entry.region === place && entry.variable === title && entry.period === period && entry.dataset === dataset && entry.test === test);
  const chart: ChartSeries[] = trend ? [{ letter: "A", label: title, color, points, trend, offset: 0 }] : [];
  const yearOptions = Array.from({ length: latest - earliest + 1 }, (_, i) => earliest + i);

  return <section className="hc-section hc-archive" aria-labelledby="archive-title">
    <div className="hc-section-heading"><span className="hc-kicker"><i /> REAL EARTH DATA</span><h1 id="archive-title">Earth Intelligence Archive</h1><p>Test a trend with historical records and file the evidence for your report.</p></div>
    <div className="hc-archive-layout">
      <aside className="hc-panel hc-archive-controls">
        <h2>Set your investigation</h2>
        <label>Region<select aria-label="Region" value={region} onChange={(event) => { setRegion(event.target.value); setStart(null); setEnd(null); }}>
          {EARTH_REGIONS.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select></label>
        <label>Variable<select aria-label="Variable" value={variable} onChange={(event) => { setVariable(event.target.value as WeatherKey); setStart(null); setEnd(null); }}>
          {Object.entries(VARIABLES).map(([key, value]) => <option value={key} key={key}>{value.label}</option>)}
        </select></label>
        {variable === "rain" && region === "phoenix" && <label>Dataset<select aria-label="Dataset" value={actualSource} onChange={(event) => { setSource(event.target.value as typeof source); setStart(null); setEnd(null); }}><option value="power">NASA POWER reanalysis</option><option value="imerg">GPM IMERG satellite estimates</option></select></label>}
        <div className="hc-range"><label>From<select aria-label="From" value={from} onChange={(event) => setStart(+event.target.value)}>{yearOptions.filter((year) => year <= to).map((year) => <option key={year}>{year}</option>)}</select></label>
        <label>To<select aria-label="To" value={to} onChange={(event) => setEnd(+event.target.value)}>{yearOptions.filter((year) => year >= from).map((year) => <option key={year}>{year}</option>)}</select></label></div>
        <label>Time view<select aria-label="Time view" value={mode} onChange={(event) => setMode(event.target.value as ArchiveMode)}><option value="annual">Yearly means</option><option value="anomaly">Monthly anomalies</option></select></label>
        <label>Compare a second variable<select aria-label="Compare a second variable" value={compare} onChange={(event) => setCompare(event.target.value as typeof compare)}><option value="">None</option>{Object.entries(VARIABLES).filter(([key]) => key !== variable).map(([key, item]) => <option value={key} key={key}>{item.label}</option>)}</select></label>
        <button type="button" className="hc-primary" disabled={!trend || saved} onClick={() => record({ region: place, dataset, variable: title, period, verdict, slope, test })}>{saved ? "Evidence filed ✓" : "File investigation"}</button>
      </aside>
      <div className="hc-archive-body">
        <div className="hc-panel hc-chart">
          <div className="hc-row hc-between"><div><span className="hc-kicker">{actualSource === "imerg" ? "SATELLITE ESTIMATE" : "REANALYSIS"}</span><h2>{title}</h2></div><span className="hc-status">{dataset}</span></div>
          <p>{place} · {period}</p>
          {trend ? <TrendChart series={chart} axisLabel={`${title} (${unit})`} unit={unit} showMarkers={mode === "annual"} relative={mode === "anomaly"} animate /> : <div className="hc-empty">Choose a wider period. A trend test needs at least 10 plotted observations.</div>}
          <div className="hc-stat-grid"><div><small>Direction</small><strong>{trend?.direction ?? "—"}</strong></div><div><small>Theil–Sen slope</small><strong>{slope}</strong></div><div><small>Trend evidence</small><strong>{verdict}</strong></div></div>
          {trend && <><p className="hc-stat-note">{test} · {trend.n} points. Both tests must meet p &lt; 0.05.</p>
            <p className="hc-stat-note">{significant ? "Both tests support the fitted direction over the selected period." : "The selected data don’t give strong evidence of a sustained direction."} This analysis cannot attribute a cause. {mode === "anomaly" && "Month-specific averages are removed over the selected period."}</p>
          </>}
          <a className="hc-source" href={actualSource === "imerg" ? IMERG.source.url : climate[region].source} target="_blank" rel="noreferrer">Open source record ↗</a>
        </div>
        {compareData?.trend && compare && <div className="hc-panel hc-chart"><span className="hc-kicker">NASA POWER · MERRA-2</span><h2>{VARIABLES[compare].label}</h2>
          <TrendChart series={[{ letter: "B", label: VARIABLES[compare].label, color: "#ffc575", offset: 0, ...compareData, trend: compareData.trend }]}
            axisLabel={VARIABLES[compare].label} unit={VARIABLES[compare].unit} showMarkers={mode === "annual"} relative={mode === "anomaly"} /></div>}
        <div className="hc-panel hc-archive-dialogue">
          <CrewLine id="scientist">{trend && !significant ? "There’s a fitted line, but its direction remains uncertain." : "The globe is our experiment. These are records of Earth. See what the data actually support."}</CrewLine>
          {trend && !significant && <CrewLine id="commander">{trend.direction === "falling" ? "SO IT’S GOING DOWN SECRETLY?" : "I MOVE THAT WE PROMOTE THE TREND TO CLASSIFIED."}</CrewLine>}
        </div>
      </div>
    </div>
  </section>;
}
