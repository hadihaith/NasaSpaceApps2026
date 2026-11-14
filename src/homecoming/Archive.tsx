"use client";

import { useEffect, useMemo, useState } from "react";
import { TrendChart } from "@/components/TrendChart";
import { analyzeEvidence, evidenceKey, parseArchive, PROVIDER_LABEL, type EvidenceDataset } from "@/lib/earth/member3";
import { VERDICT } from "@/lib/earth/verdict";
import { MONTH_NAMES, type Mode } from "@/lib/series";
import { EARTH_REGIONS } from "@/earth/regions";
import { CrewLine } from "./Crew";
import type { Investigation, WeatherKey } from "./model";

const VARIABLES: Record<WeatherKey, string> = {
  temperature: "Air temperature", rain: "Precipitation", humidity: "Relative humidity",
  wind: "Wind speed", soil: "Root-zone soil wetness",
};

export function Archive(props: {
  initialRegion: string; initialVariable: WeatherKey; investigations: Investigation[];
  record: (entry: Investigation) => void;
}) {
  const [datasets, setDatasets] = useState<EvidenceDataset[] | null>(null);
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    fetch("/data/earth-archive.json", { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error("Archive unavailable");
        return parseArchive(await response.json());
      })
      .then((data) => { setDatasets(data); setError(false); })
      .catch(() => { if (!controller.signal.aborted) setError(true); });
    return () => controller.abort();
  }, [attempt]);
  if (!datasets) return <section className="hc-section hc-panel hc-loading" role="status">
    {error ? <>The Earth records couldn’t load.<button className="hc-primary" onClick={() => { setError(false); setAttempt((n) => n + 1); }}>Retry</button></> : "Opening the Earth Intelligence Archive…"}
  </section>;
  return <ArchiveExplorer {...props} datasets={datasets} />;
}

function ArchiveExplorer({ datasets, initialRegion, initialVariable, investigations, record }: {
  datasets: EvidenceDataset[]; initialRegion: string; initialVariable: WeatherKey;
  investigations: Investigation[]; record: (entry: Investigation) => void;
}) {
  const [region, setRegion] = useState(initialRegion);
  const [variable, setVariable] = useState(initialVariable);
  const [source, setSource] = useState("reanalysis");
  const [start, setStart] = useState<number | null>(null);
  const [end, setEnd] = useState<number | null>(null);
  const [mode, setMode] = useState<Mode>("annual");
  const [month, setMonth] = useState(0);
  const [compare, setCompare] = useState<WeatherKey | "">("");
  const choices = useMemo(() => datasets.filter((d) => d.gameRegionId === region && d.gameVariable === variable), [datasets, region, variable]);
  const dataset = useMemo(() => choices.find((d) => d.providerKind === source) ?? choices[0], [choices, source]);
  const earliest = Number(dataset.temporal.start.slice(0, 4));
  const latest = Number(dataset.temporal.end.slice(0, 4));
  const from = Math.max(earliest, Math.min(latest, start ?? earliest));
  const to = Math.max(from, Math.min(latest, end ?? latest));
  const result = useMemo(() => analyzeEvidence(dataset, mode, from, to, month), [dataset, mode, from, to, month]);
  const { points, trend } = result;
  const second = datasets.find((d) => d.gameRegionId === region && d.gameVariable === compare && d.providerKind === "reanalysis");
  const comparison = useMemo(() => second ? analyzeEvidence(second, mode,
    Math.max(from, Number(second.temporal.start.slice(0, 4))), Math.min(to, Number(second.temporal.end.slice(0, 4))), month) : null,
  [second, from, to, mode, month]);
  const period = `${from}–${to}`;
  const verdict = trend ? VERDICT[trend.strength].text : "Insufficient data for inference";
  const slope = trend ? `${(trend.sen.slope * 10).toFixed(3)} ${dataset.units} / decade` : "Unavailable";
  const entry: Investigation = {
    evidenceVersion: 1, datasetId: dataset.id, productVersion: dataset.provenance.version,
    mode, month: mode === "month" ? month : undefined, providerKind: dataset.providerKind,
    inferenceStatus: "not-established", region: dataset.region, dataset: dataset.provenance.product,
    variable: VARIABLES[variable], period, verdict, slope,
    test: `${timeLabel(mode, variable, month)}. Significance not established; p-value unavailable. Scientific review pending.`,
  };
  const saved = investigations.some((item) => evidenceKey(item) === evidenceKey(entry));
  const years = Array.from({ length: latest - earliest + 1 }, (_, i) => earliest + i);
  const resetPeriod = () => { setStart(null); setEnd(null); };
  return <section className="hc-section hc-archive" aria-labelledby="archive-title">
    <div className="hc-section-heading"><span className="hc-kicker"><i /> REAL EARTH DATA</span><h1 id="archive-title">Earth Intelligence Archive</h1><p>Inspect historical records and file descriptive evidence for your report.</p></div>
    <div className="hc-archive-layout">
      <aside className="hc-panel hc-archive-controls">
        <h2>Set your investigation</h2>
        <label>Region<select aria-label="Region" value={region} onChange={(event) => { setRegion(event.target.value); resetPeriod(); }}>
          {EARTH_REGIONS.filter((r) => datasets.some((d) => d.gameRegionId === r.id)).map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
        </select></label>
        <label>Variable<select aria-label="Variable" value={variable} onChange={(event) => { setVariable(event.target.value as WeatherKey); resetPeriod(); }}>
          {Object.entries(VARIABLES).map(([key, label]) => <option key={key} value={key}>{label}</option>)}
        </select></label>
        <label>Dataset<select aria-label="Dataset" value={dataset.providerKind} onChange={(event) => { setSource(event.target.value); resetPeriod(); }}>
          {choices.map((d) => <option key={d.id} value={d.providerKind}>{PROVIDER_LABEL[d.providerKind]}</option>)}
        </select></label>
        <div className="hc-range"><label>From<select aria-label="From" value={from} onChange={(e) => setStart(+e.target.value)}>{years.filter((y) => y <= to).map((y) => <option key={y}>{y}</option>)}</select></label>
          <label>To<select aria-label="To" value={to} onChange={(e) => setEnd(+e.target.value)}>{years.filter((y) => y >= from).map((y) => <option key={y}>{y}</option>)}</select></label></div>
        <label>Time view<select aria-label="Time view" value={mode} onChange={(e) => setMode(e.target.value as Mode)}>
          <option value="annual">Yearly equal-month means</option><option value="anomaly">Monthly anomalies</option><option value="month">One calendar month per year</option>
        </select></label>
        {mode === "month" && <label>Calendar month<select aria-label="Calendar month" value={month} onChange={(e) => setMonth(+e.target.value)}>{MONTH_NAMES.map((name, i) => <option key={name} value={i}>{name}</option>)}</select></label>}
        <label>Compare a second variable<select aria-label="Compare a second variable" value={compare} onChange={(e) => setCompare(e.target.value as WeatherKey | "")}><option value="">None</option>{Object.entries(VARIABLES).filter(([key]) => key !== variable).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
        <button type="button" className="hc-primary" disabled={!points.length || saved} onClick={() => record(entry)}>{saved ? "Evidence filed ✓" : "File descriptive evidence"}</button>
        <p>Filing a record does not certify a significant trend.</p>
      </aside>
      <div className="hc-archive-body">
        <EvidencePanel dataset={dataset} result={result} mode={mode} variable={variable} month={month} />
        {comparison && second && compare && compare !== variable && <EvidencePanel dataset={second} result={comparison} mode={mode} variable={compare} month={month} />}
        <div className="hc-panel hc-archive-dialogue"><CrewLine id="scientist">The simulator is hypothetical. These records describe Earth. Statistical significance needs a review of this exact dataset and analysis.</CrewLine></div>
      </div>
    </div>
  </section>;
}

function timeLabel(mode: Mode, variable: WeatherKey, month: number) {
  if (mode === "anomaly") return "Monthly anomalies relative to each calendar month’s available-value mean in the selected years";
  if (mode === "month") return `${MONTH_NAMES[month]} values only`;
  return variable === "rain" ? "Equal-month mean of monthly precipitation totals, not annual accumulation" : "Equal-month annual mean, using twelve complete months";
}

function EvidencePanel({ dataset, result, mode, variable, month }: {
  dataset: EvidenceDataset; result: ReturnType<typeof analyzeEvidence>; mode: Mode; variable: WeatherKey; month: number;
}) {
  const { trend, points, startYear, endYear, missingMonths } = result;
  const excluded = dataset.quality.excludedAnnualYears.filter((y) => y >= startYear && y <= endYear);
  return <article className="hc-panel hc-chart">
    <span className="hc-kicker">{PROVIDER_LABEL[dataset.providerKind]}</span><h2>{dataset.variable}</h2>
    <p>{dataset.region}</p><p>Source coverage: {dataset.temporal.start} to {dataset.temporal.end}. Selected years: {startYear}–{endYear}.</p>
    <p>{timeLabel(mode, variable, month)}. Units: {dataset.units}.</p>
    {trend && <TrendChart series={[{ letter: "A", label: dataset.variable, color: "#91edff", points, trend, offset: 0 }]}
      axisLabel={`${VARIABLES[variable]} (${dataset.units})`} unit={dataset.units} showMarkers={mode !== "anomaly"} relative={mode === "anomaly"} animate
      valueLabel={PROVIDER_LABEL[dataset.providerKind]} />}
    <div className="hc-stat-grid"><div><small>Descriptive direction</small><strong>{trend?.direction ?? "Unavailable"}</strong></div>
      <div><small>Theil–Sen rate</small><strong>{trend ? `${(trend.sen.slope * 10).toFixed(3)} ${dataset.units} / decade` : "Unavailable"}</strong></div>
      <div><small>Statistical inference</small><strong>{trend ? VERDICT[trend.strength].text : "Insufficient data for inference"}</strong></div></div>
    <p className="hc-stat-note">{points.length} usable points; {missingMonths} missing months in the selected calendar window. Missing values are never replaced by zero.
      {mode === "annual" && excluded.length > 0 && ` Incomplete years excluded: ${excluded.join(", ")}.`}</p>
    <p className="hc-stat-note">{result.warnings.join(" ")} P-value: unavailable until a matching scientific review and diagnostics support inference. A fitted direction does not establish a cause.</p>
    <details><summary>Values used in this analysis</summary><div style={{ maxHeight: 240, overflow: "auto" }}>
      {points.length ? <table style={{ width: "100%", textAlign: "left", fontVariantNumeric: "tabular-nums" }}><thead><tr><th style={{ width: "40%" }}>Time</th><th>Value ({dataset.units})</th></tr></thead><tbody>{points.map((p) => <tr key={p.t}><td>{p.label}</td><td>{p.v.toFixed(3)}</td></tr>)}</tbody></table> : <p>No complete values for this time view. Choose another period or monthly view.</p>}
    </div></details>
    <details style={{ overflowWrap: "anywhere" }}><summary>Source, processing and limitations</summary>
      <p>{dataset.definition}</p><p>{dataset.provenance.product} · {dataset.provenance.version}</p>
      <p>Retrieved: {dataset.provenance.retrievedAt}. Time convention: {dataset.temporal.timeStandard ?? "Monthly source dates"}.</p>
      <p>Spatial support: {dataset.spatial.type}. {dataset.spatial.nativeResolution} {dataset.spatial.footprintNote}</p>
      <p>Longest missing run: {dataset.quality.longestMissingRunMonths} months. Months with reported missing/flagged days: {dataset.quality.monthsWithMissingDays}.</p>
      <ul>{[...dataset.provenance.processing, ...dataset.provenance.limitations].map((text, i) => <li key={i}>{text}</li>)}</ul>
      <p>{dataset.provenance.license} Redistribution: {dataset.provenance.redistribution}.</p>
      {dataset.source.citation.map((citation) => <p key={citation}>{citation}</p>)}
      <p>Dataset ID: {dataset.id}. Raw SHA-256: {dataset.rawSha256}.</p>
    </details>
    <a className="hc-source" href={dataset.provenance.sourceUrl} target="_blank" rel="noreferrer">Open source record ↗</a>
  </article>;
}
