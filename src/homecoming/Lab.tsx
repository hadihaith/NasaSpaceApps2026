"use client";

import dynamic from "next/dynamic";
import { useRef, useState, type CSSProperties } from "react";
import { ThermometerIcon, DropIcon, WindIcon, SunIcon } from "@/components/icons";
import { EARTH_REGIONS } from "@/earth/regions";
import { CrewLine } from "./Crew";
import { TerrainChamber } from "./TerrainChamber";
import { CONTROLS, DISCOVERY_BY_ID, TIMES, applyWeather, initialState, settingLabel,
  type Control, type Discovery, type Duration, type Normals, type Save, type Trial, type TrialFrame, type WeatherKey } from "./model";
import "./chamber.css";

const EarthScene = dynamic(() => import("@/earth/EarthScene"), { ssr: false, loading: () => <div className="hc-globe-load">Calibrating the projector…</div> });

export type TrialPlayback = { trial: Trial; frames: TrialFrame[]; month: number };
const ENVIRONMENT = [
  { key: "soilMoistureIndex", label: "Soil moisture", color: "#8edfff", Icon: DropIcon },
  { key: "vegetationHealthIndex", label: "Vegetation health", color: "#b2eca5", Icon: Leaf },
  { key: "droughtStressIndex", label: "Drought stress", color: "#ffbd88", Icon: SunIcon },
] as const;

function Leaf() {
  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.65" strokeLinecap="round" strokeLinejoin="round"><path d="M5 20c2-7 6-10 11-13M8 16C1 8 11 4 20 4c0 8-3 15-12 12Z" /></svg>;
}

function GlobeIcon() {
  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6"><circle cx="12" cy="12" r="9" /><ellipse cx="12" cy="12" rx="4" ry="9" /><path d="M3 12h18" /></svg>;
}

export function placeName(id: string) {
  return EARTH_REGIONS.find((region) => region.id === id)?.name ?? id;
}

export function Lab({ save, normals, busy, playback, run, update, openArchive }: {
  save: Save; normals: Normals; busy: boolean; playback: TrialPlayback | null;
  run: () => void; update: (patch: Partial<Save>) => void; openArchive: (variable?: WeatherKey) => void;
}) {
  const [view, setView] = useState<"terrain" | "globe">("terrain");
  const [inspection, setInspection] = useState<{ trial: Trial; month: number } | null>(null);
  const monitor = useRef<HTMLDivElement>(null);
  const regionNormals = normals[save.region];
  const selected = save.regions[save.region] ?? initialState(regionNormals);
  const previous = save.trials.findLast((trial) => trial.region === save.region);
  const warmUp = !save.guided;
  const warming = warmUp && save.conditions.heat > 0;
  const preview = !busy && CONTROLS.some(({ key }) => save.conditions[key] !== (previous?.settings[key] ?? 0));
  const sequence = playback?.trial.region === save.region && (busy || playback.trial.number === previous?.number) ? playback : null;
  const inspected = inspection && sequence && inspection.trial === sequence.trial ? inspection.month : null;
  const viewingRun = Boolean(sequence && (busy || !preview || inspected !== null));
  const month = viewingRun && sequence ? inspected ?? sequence.month : 0;
  const frame = viewingRun ? sequence?.frames[month] : undefined;
  const environment = frame?.state ?? selected;
  const weather = frame?.weather ?? (preview ? applyWeather(regionNormals[selected.elapsed % 12], save.conditions) : previous?.weather ?? regionNormals[0]);
  const baseline = viewingRun ? sequence?.trial.before : undefined;
  const duration = viewingRun && sequence ? sequence.trial.duration : save.duration;
  const readouts = [
    { Icon: ThermometerIcon, label: "Air temperature", value: weather.temperature.toFixed(1), unit: "°C", tone: "heat" },
    { Icon: DropIcon, label: "Precipitation", value: weather.rain.toFixed(0), unit: "mm/mo", tone: "rain" },
    { Icon: DropIcon, label: "Humidity", value: weather.humidity.toFixed(0), unit: "%", tone: "humidity" },
    { Icon: WindIcon, label: "Wind", value: weather.wind.toFixed(1), unit: "m/s", tone: "wind" },
  ];

  function changeRegion(id: string) {
    if (busy) return;
    setInspection(null);
    update({ region: id });
  }
  function changeCondition(key: Control, value: number) {
    setInspection(null);
    update({ conditions: { ...save.conditions, [key]: value } });
  }
  function startRun() {
    setInspection(null);
    setView("terrain");
    if (monitor.current && monitor.current.getBoundingClientRect().top < 0) monitor.current.scrollIntoView({ block: "start" });
    run();
  }

  return <section className="hc-section hc-lab">
    <div className="hc-section-heading hc-lab-heading">
      <div><span className="hc-kicker"><i /> RESEARCH BAY</span><h1>Planetary Persuasion Device</h1><p>Change the weather. Watch the landscape respond.</p></div>
      <span className="hc-status"><i /> {warmUp ? "Calibration" : "Sandbox"} <b>·</b> Run {String(save.trials.at(-1)?.number ?? 0).padStart(3, "0")}</span>
    </div>
    <div className="hc-lab-layout">
      <div className="hc-chamber" ref={monitor} data-view={view}>
        <div className="hc-chamber-bar">
          <div><span className="hc-chamber-eyebrow"><i /> REGIONAL SIMULATION</span><h2>{placeName(save.region)}</h2></div>
          <div className="hc-view-toggle" role="group" aria-label="Simulation view">
            <button type="button" aria-pressed={view === "terrain"} onClick={() => setView("terrain")}><Leaf /> Terrain</button>
            <button type="button" aria-pressed={view === "globe"} onClick={() => setView("globe")}><GlobeIcon /> Globe</button>
          </div>
        </div>
        <div className="hc-chamber-window">
          {view === "terrain" ? <TerrainChamber region={save.region} state={environment} weather={weather} />
            : <div className="hc-globe-frame"><EarthScene regionId={save.region} environment={environment} onRegionSelect={changeRegion} presentation="homecoming" /></div>}
        </div>
        <div className="hc-weather-strip" aria-label="Simulated weather">
          {readouts.map(({ Icon, label, value, unit, tone }) => <div key={label} className={`hc-weather-reading hc-weather-${tone}`}><Icon /><div><span>{label}</span><strong>{value}<small> {unit}</small></strong></div></div>)}
        </div>
        <div className="hc-playback">
          <span className="hc-screen-reader" role="status">{busy ? `Running ${duration} simulated ${duration === 1 ? "month" : "months"}` : ""}</span>
          <div className="hc-playback-label"><span className={busy ? "hc-playback-live" : undefined}><i /> {busy ? "RUNNING" : inspected !== null && inspected === 0 ? "BEFORE RUN" : inspected !== null ? "PLAYBACK" : preview ? "WEATHER PREVIEW" : previous ? "RUN COMPLETE" : "BASELINE"}</span>
            <b>{viewingRun ? `Month ${month} / ${duration}` : `${selected.elapsed} elapsed · ${save.duration} queued`}</b>
          </div>
          <div className="hc-months" role="group" aria-label="Experiment timeline">
            {Array.from({ length: (sequence?.trial.duration ?? duration) + 1 }, (_, index) => <button key={index} type="button" aria-label={index === 0 ? "Before the experiment" : `View month ${index}`}
              title={index === 0 ? "Before the experiment" : `Month ${index}`} aria-current={viewingRun && month === index ? "step" : undefined}
              data-complete={viewingRun && index <= month ? "" : undefined} disabled={busy || !sequence}
              onClick={() => sequence && setInspection({ trial: sequence.trial, month: index })}>{index}</button>)}
          </div>
          {!busy && inspected !== null && <button type="button" className="hc-exit-playback" onClick={() => setInspection(null)}>{preview ? "Back to weather preview" : "Back to result"}</button>}
        </div>
      </div>
      <aside className="hc-panel hc-lab-controls">
        <div className="hc-control-heading"><span className="hc-kicker">SIMULATION ONLY</span><h2>Set conditions</h2></div>
        <label className="hc-region-select">Region<select value={save.region} onChange={(event) => changeRegion(event.target.value)} disabled={busy}>{EARTH_REGIONS.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        <div className="hc-sliders">
          {CONTROLS.map(({ key, label, min, max, step, color }) => {
            const Icon = key === "heat" ? ThermometerIcon : key === "wind" ? WindIcon : DropIcon;
            const locked = warmUp && key !== "heat";
            return <label key={key} className={`hc-slider ${locked ? "hc-locked" : ""}`} style={{ "--slider": color, "--value": `${(save.conditions[key] - min) / (max - min) * 100}%` } as CSSProperties}>
              <span><Icon />{label}</span><output>{locked ? "Locked" : settingLabel(key, save.conditions[key])}</output>
              <input type="range" aria-label={label} aria-valuetext={settingLabel(key, save.conditions[key])} min={min} max={max} step={step} value={save.conditions[key]} disabled={locked || busy} onChange={(event) => changeCondition(key, +event.target.value)} />
            </label>;
          })}
        </div>
        <div className="hc-time"><b>Advance time</b><div className="hc-segment" role="group" aria-label="Advance time">
          {TIMES.map(({ label, months }) => <button key={months} type="button" aria-pressed={save.duration === months} disabled={busy} onClick={() => update({ duration: months as Duration })}>{label}</button>)}
        </div></div>
        <button type="button" disabled={busy || (warmUp && !warming)} className="hc-primary hc-run" onClick={startRun}>{busy ? "Running experiment…" : "Run experiment"} <span aria-hidden>{busy ? "◉" : "▶"}</span></button>
        <button type="button" className="hc-text-button" disabled={busy || warmUp} onClick={() => { setInspection(null); update({ conditions: { heat: 0, rain: 0, humidity: 0, wind: 0 } }); }}>Reset sliders</button>
      </aside>
      <section className="hc-terrain-metrics" aria-label="Surface and root zone">
        {ENVIRONMENT.map(({ key, label, color, Icon }) => {
          const value = environment[key];
          const change = baseline ? Math.round((value - baseline[key]) * 100) : null;
          const points = viewingRun ? sequence?.frames.slice(0, month + 1).map(({ state }, index) => `${4 + index * 88 / duration},${27 - state[key] * 23}`).join(" ") : undefined;
          return <div className="hc-terrain-metric" key={key} style={{ "--reading": color, "--level": value } as CSSProperties}>
            <div className="hc-metric-label"><Icon /><span>{label}</span></div>
            <div className="hc-metric-main"><b>{Math.round(value * 100)}<small>%</small></b>{change !== null && <span className="hc-metric-change" aria-label={`${change > 0 ? "Up" : change < 0 ? "Down" : "No change"}, ${Math.abs(change)} percentage points since start`}>{change > 0 ? "↑ +" : change < 0 ? "↓ −" : "– "}{Math.abs(change)} pp</span>}</div>
            {points && month > 0 && <svg className="hc-metric-trace" viewBox="0 0 96 32" aria-hidden="true"><path d="M4 28h88" /><polyline points={points} /></svg>}
            <div className="hc-metric-track" aria-hidden="true"><i /></div>
          </div>;
        })}
      </section>
    </div>
    {!busy && <div className="hc-panel hc-crew-dispatch" aria-live="polite">
      {warmUp ? warming
        ? <><CrewLine id="commander">WE’VE DONE IT! A DEGREE OF SUCCESS!</CrewLine><CrewLine id="scientist">You changed the temperature. Now run the experiment to see how the model responds.</CrewLine></>
        : <CrewLine id="scientist">Welcome, {save.name}. Let’s begin with temperature. Increase it to wake up the simulator.</CrewLine>
      : previous ? <><CrewLine id={previous.outcome === "quiet" ? "scientist" : "commander"}>{previous.outcome === "quiet" ? "No major event. Check the measurements. Quiet results still tell us something." : previous.active.includes("recovery") ? DISCOVERY_BY_ID.recovery.quote : previous.outcome === "discovery" ? "NOTIFY THE ENCYCLOPEDIA. USE THE NICE STAMP." : "The planet has changed its tactics again."}</CrewLine>
        {!!previous.active.length && <div className="hc-tag-row">{previous.active.map((id) => <span key={id}>{DISCOVERY_BY_ID[id].label}</span>)}</div>}</>
        : <><CrewLine id="scientist">Rainfall, humidity and wind are online. Try different conditions and watch what you learn.</CrewLine><CrewLine id="cadet">I turned off the confetti sprinklers. Probably.</CrewLine></>}
      {!!Object.keys(save.discoveries).length && <button className="hc-text-button" type="button" onClick={() => openArchive()}>Investigate real Earth data →</button>}
    </div>}
  </section>;
}

export function TrialSummary({ trial }: { trial: Trial }) {
  const duration = TIMES.find((time) => time.months === trial.duration)?.label;
  return <div className="hc-trial-summary"><span>{placeName(trial.region)} · {duration?.toLowerCase()} · run {String(trial.number).padStart(3, "0")}</span>
    <div className="hc-tag-row">{CONTROLS.map(({ key, label }) => <span key={key}>{label} <b>{settingLabel(key, trial.settings[key])}</b></span>)}</div></div>;
}

export function DiscoveryRecord({ entry, trial, save, investigate }: { entry: Discovery; trial: Trial; save: Save; investigate: () => void }) {
  return <article className="hc-panel hc-record">
    <span className="hc-record-icon" aria-hidden>{entry.icon}</span><h2>{entry.label}</h2><blockquote>“{entry.quote}”</blockquote><p>{entry.science}</p>
    <TrialSummary trial={trial} />
    {entry.linked.some((id) => save.discoveries[id]) && <p className="hc-related">Related discoveries <b>{entry.linked.filter((id) => save.discoveries[id]).map((id) => DISCOVERY_BY_ID[id].label).join(" · ")}</b></p>}
    <button className="hc-text-button" type="button" onClick={investigate}>Inspect relevant Earth data →</button>
  </article>;
}
