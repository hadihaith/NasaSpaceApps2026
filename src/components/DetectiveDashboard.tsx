"use client";

import { useState } from "react";
import { analyzeDataset, parseDataset, type EarthDataset } from "@/lib/member4/dataset";
import { EXAMPLES, exampleReview } from "@/lib/member4/examples";
import { MONTH_NAMES, MODES, type Mode } from "@/lib/series";
import { TrendChart } from "./TrendChart";

/** Member 4's standalone view. Imported files stay in this browser. */
export function DetectiveDashboard() {
  const [dataset, setDataset] = useState<EarthDataset>(EXAMPLES[0]);
  const [mode, setMode] = useState<Mode>("annual");
  const [month, setMonth] = useState(0);
  const [startYear, setStartYear] = useState(1980);
  const [endYear, setEndYear] = useState(2019);
  const [error, setError] = useState("");
  const firstYear = Number(dataset.observations[0].date.slice(0, 4));
  const lastYear = Number(dataset.observations.at(-1)!.date.slice(0, 4));
  const result = analyzeDataset(dataset, mode, month, { startYear, endYear },
    exampleReview(dataset, mode, month, startYear, endYear));
  const trend = result.trend;
  const generated = dataset.kind === "synthetic" || dataset.kind === "simulation";
  const selectDataset = (next: EarthDataset) => {
    setDataset(next);
    setStartYear(Number(next.observations[0].date.slice(0, 4)));
    setEndYear(Number(next.observations.at(-1)!.date.slice(0, 4)));
    setError("");
  };
  const format = (n: number) => Number.isFinite(n) ? n.toPrecision(4) : "Unavailable";
  return (
    <main className="mx-auto max-w-6xl space-y-6 p-6 sm:p-10 [&_label]:min-w-0 [&_select]:w-full [&_select]:min-w-0 [&_input]:w-full [&_input]:min-w-0">
      <header className="space-y-3">
        <p className="text-sm uppercase tracking-widest">Earth Intelligence Archive · Member 4</p>
        <h1 className="display text-4xl sm:text-5xl">Investigate a changing Earth</h1>
        <p className="max-w-3xl text-lg text-ink-2">Explore a time series, estimate its rate of change, and check what the evidence supports.</p>
      </header>
      <div className="card p-4" role="status">
        <strong>{generated ? "Generated example — not Earth observations" : `${dataset.kind === "reanalysis" ? "Reanalysis" : "Observations"} — supplied dataset`}</strong>
        <p>{dataset.variable} · {dataset.region} · {dataset.units}</p>
      </div>
      <section aria-label="Investigation controls" className="card grid gap-4 p-5 sm:grid-cols-2 lg:grid-cols-3">
        <label className="grid gap-1">Teaching example
          <select className="rounded border p-2" value={EXAMPLES.includes(dataset) ? dataset.id : "imported"} onChange={(e) => selectDataset(EXAMPLES.find((d) => d.id === e.target.value)!)}>
            {!EXAMPLES.includes(dataset) && <option value="imported" disabled>Imported: {dataset.id}</option>}
            {EXAMPLES.map((d) => <option key={d.id} value={d.id}>{d.id.replaceAll("-", " ")}</option>)}
          </select>
        </label>
        <label className="grid gap-1">Time treatment
          <select className="rounded border p-2" value={mode} onChange={(e) => setMode(e.target.value as Mode)}>
            {MODES.map((m) => <option key={m.id} value={m.id}>{m.label}</option>)}
          </select>
        </label>
        {mode === "month" && <label className="grid gap-1">Calendar month
          <select className="rounded border p-2" value={month} onChange={(e) => setMonth(Number(e.target.value))}>
            {MONTH_NAMES.map((name, i) => <option key={name} value={i}>{name}</option>)}
          </select>
        </label>}
        <label className="grid gap-1">Start year
          <select className="rounded border p-2" value={startYear} onChange={(e) => setStartYear(Number(e.target.value))}>
            {Array.from({ length: endYear - firstYear + 1 }, (_, i) => firstYear + i).map((year) => <option key={year}>{year}</option>)}
          </select>
        </label>
        <label className="grid gap-1">End year
          <select className="rounded border p-2" value={endYear} onChange={(e) => setEndYear(Number(e.target.value))}>
            {Array.from({ length: lastYear - startYear + 1 }, (_, i) => startYear + i).map((year) => <option key={year}>{year}</option>)}
          </select>
        </label>
        <label className="grid gap-1">Load Member 3’s JSON locally
          <input type="file" accept=".json,application/json" onChange={async (e) => {
            const file = e.target.files?.[0];
            if (!file) return;
            try {
              if (file.size > 2_000_000) throw new Error("This module accepts JSON files up to 2 MB.");
              selectDataset(parseDataset(JSON.parse(await file.text())));
            } catch (cause) { setError(cause instanceof Error ? cause.message : "Could not read the dataset."); }
            e.target.value = "";
          }} />
        </label>
      </section>
      {error && <p role="alert" className="card p-4">{error}</p>}
      <section aria-label="Analysis results" className="space-y-4" aria-live="polite">
        <h2 className="display text-3xl">{!trend || !trend.inference.established ? "Significance not established" : trend.strength === "significant" ? "Evidence of a linear trend" : "No statistically significant linear trend detected"}</h2>
        <p>Period: {startYear}–{endYear} · {result.points.length} usable points · {result.missingMonths} missing months</p>
        {trend && <>
          <dl className="grid gap-4 sm:grid-cols-3">
            <div className="card p-4"><dt>Robust rate (Theil–Sen)</dt><dd className="text-2xl">{format(trend.sen.slope * 10)} {dataset.units}/decade</dd></div>
            <div className="card p-4"><dt>Fitted change over selected span</dt><dd className="text-2xl">{format(trend.magnitude)} {dataset.units}</dd></div>
            <div className="card p-4"><dt>Primary OLS p-value</dt><dd className="text-2xl">{trend.inference.established ? format(trend.ols.p) : "Not reported"}</dd></div>
          </dl>
          <div className="card p-4"><TrendChart series={[{ letter: "A", label: dataset.variable, color: "var(--series-a)", points: result.points, trend, offset: 0 }]}
            unit={dataset.units} axisLabel={`${mode === "anomaly" ? "Monthly anomaly" : dataset.variable} (${dataset.units})`}
            showMarkers={mode !== "anomaly"} relative={mode === "anomaly"} valueLabel={generated ? "Generated values" : dataset.kind === "reanalysis" ? "Reanalysis values" : "Observations"} /></div>
          <p>The robust rate and fitted change are descriptive estimates. The significance test concerns the OLS slope ({format(trend.ols.slope * 10)} {dataset.units}/decade), conditional on the reviewed model.</p>
          {trend.inference.established && <p>OLS 95% slope interval: {format(trend.ols.ci[0] * 10)} to {format(trend.ols.ci[1] * 10)} {dataset.units}/decade. A p-value does not give the probability that a trend is true or identify its cause.</p>}
        </>}
        {!trend && <p>Select a longer period to estimate a trend. This module requires at least ten usable points.</p>}
        {result.warnings.length > 0 && <ul className="list-disc space-y-1 pl-6">{result.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul>}
        <p className="text-sm text-ink-2">Exploring several periods is useful for learning, but selecting the lowest p-value is not a confirmatory test. No multiple-testing adjustment is applied.</p>
      </section>
      <details className="card p-5"><summary className="cursor-pointer font-semibold">Provenance and numerical evidence</summary>
        <p className="mt-3">{dataset.provenance.product} · version {dataset.provenance.version} · {dataset.provenance.retrievedAt}</p>
        <p>Source: {dataset.provenance.sourceUrl}</p><p>Terms: {dataset.provenance.license} · Redistribution: {dataset.provenance.redistribution}</p>
        <p>Processing: {dataset.provenance.processing.join("; ")}</p><p>Limitations: {dataset.provenance.limitations.join("; ")}</p>
        <div className="mt-4 max-h-72 overflow-auto"><table className="w-full text-left"><thead><tr><th>Time</th><th>Value ({dataset.units})</th></tr></thead>
          <tbody>{result.points.map((p) => <tr key={p.t}><td>{p.label}</td><td>{format(p.v)}</td></tr>)}</tbody></table></div>
      </details>
    </main>
  );
}
