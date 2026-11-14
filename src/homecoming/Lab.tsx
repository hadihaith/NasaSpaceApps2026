"use client";

import dynamic from "next/dynamic";
import { useEffect, useId, useRef, useState, type CSSProperties } from "react";
import { DropIcon, ThermometerIcon, WindIcon } from "@/components/icons";
import { EARTH_REGIONS, toCoordinates } from "@/earth/regions";
import { DEFAULT_SETTINGS, CONTROLS, DISCOVERY_BY_ID, TIMES, applyWeather, initialState, settingLabel,
  type Control, type Discovery, type Normals, type Save, type Trial, type TrialFrame } from "./model";
import "./orbit-lab.css";

const EarthScene = dynamic(() => import("@/earth/EarthScene"), { ssr: false });

export type TrialPlayback = { trial: Trial; frames: TrialFrame[]; month: number; startedAt: number };

const TOOLS = {
  heat: { title: "Heat", Icon: ThermometerIcon, left: "Cold", right: "Hot" },
  rain: { title: "Rain", Icon: DropIcon, left: "Dry", right: "Downpour" },
  humidity: { title: "Humidity", Icon: DropIcon, left: "Dry air", right: "Humid air" },
  wind: { title: "Wind", Icon: WindIcon, left: "Calm", right: "Gales" },
} as const;

const STARS = Array.from({ length: 87 }, (_, i) => ({
  left: `${(i * 109.73) % 100}%`, top: `${(13 + i * 72.389) % 100}%`,
  opacity: .15 + (i * 11 % 18) * .04, width: i % 19 === 0 ? 2 : 1,
}));

function Chevron() {
  return <svg aria-hidden="true" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.4"><path d="m4 6 4 4 4-4" /></svg>;
}

export function placeName(id: string) {
  return EARTH_REGIONS.find((region) => region.id === id)?.name ?? id;
}

export function Lab({ save, normals, busy, playback, run, update }: {
  save: Save; normals: Normals; busy: boolean; playback: TrialPlayback | null;
  run: () => void; update: (patch: Partial<Save>) => void;
}) {
  const [activeControl, setActiveControl] = useState<Control | null>(null);
  const [inspection, setInspection] = useState<{ trial: Trial; month: number } | null>(null);
  const [readingsOpen, setReadingsOpen] = useState(false);
  const panelId = useId();
  const adjustment = useRef<HTMLDivElement>(null);
  const region = EARTH_REGIONS.find(({ id }) => id === save.region) ?? EARTH_REGIONS[0];
  const regionNormals = normals[save.region];
  const selected = save.regions[save.region] ?? initialState(regionNormals);
  const previous = save.trials.findLast((trial) => trial.region === save.region);
  const warmUp = !save.guided;
  const warming = save.conditions.heat > 0;
  const preview = CONTROLS.some(({ key }) => save.conditions[key] !== (previous?.settings[key] ?? 0));
  const sequence = playback?.trial.region === save.region && (busy || playback.trial.number === previous?.number) ? playback : null;
  const inspected = inspection?.trial === sequence?.trial ? inspection?.month : undefined;
  const viewingRun = sequence && (busy || !preview || inspected !== undefined);
  const month = viewingRun ? inspected ?? sequence.month : 0;
  const frame = viewingRun ? sequence.frames[month] : undefined;
  const environment = frame?.state ?? selected;
  const nextWeather = applyWeather(regionNormals[selected.elapsed % 12], save.conditions);
  const weather = busy && month === 0 ? nextWeather : frame?.weather ?? (preview ? nextWeather : previous?.weather ?? regionNormals[0]);
  const settings = viewingRun && !busy ? (month === 0 ? DEFAULT_SETTINGS : sequence.trial.settings) : save.conditions;
  const duration = sequence?.trial.duration ?? save.duration;
  const control = CONTROLS.find(({ key }) => key === activeControl);

  useEffect(() => {
    if (activeControl) adjustment.current?.querySelector<HTMLButtonElement>("[data-adjust]:enabled")?.focus();
  }, [activeControl]);

  function changeRegion(id: string) {
    if (busy) return;
    setInspection(null);
    setActiveControl(null);
    update({ region: id });
  }

  function changeCondition(key: Control, value: number) {
    setInspection(null);
    update({ conditions: { ...save.conditions, [key]: value } });
  }

  function startRun() {
    setInspection(null);
    setActiveControl(null);
    setReadingsOpen(false);
    run();
  }

  function inspect(monthToView: number) {
    if (!sequence) return;
    setActiveControl(null);
    setInspection({ trial: sequence.trial, month: monthToView });
  }

  return <section className="hc-orbit-lab" data-running={busy ? "" : undefined}
    onKeyDown={(event) => {
      if (event.key === "Escape") {
        if (control) document.getElementById(`${panelId}-${control.key}`)?.focus();
        else if (readingsOpen) document.getElementById(`${panelId}-readings`)?.focus();
        setActiveControl(null);
        setReadingsOpen(false);
      }
    }}>
    <h1 className="hc-orbit-sr">Planetary Persuasion Device</h1>
    <div className="hc-orbit-space" aria-hidden="true">
      {STARS.map((star, index) => <i key={index} style={star} />)}
    </div>
    <div className="hc-orbit-world">
      <EarthScene regionId={save.region} environment={environment} onRegionSelect={changeRegion}
        presentation="orbit" flight={{ weather, settings, running: busy, pulse: sequence?.startedAt ?? 0 }} />
    </div>
    <header className="hc-orbit-location">
      <span className="hc-orbit-caption">Simulated Earth</span>
      <label className="hc-orbit-region">
        <span className="hc-orbit-sr">Region</span>
        <span aria-hidden="true">{region.name} <Chevron /></span>
        <select value={save.region} disabled={busy} onChange={(event) => changeRegion(event.target.value)}>
          {EARTH_REGIONS.map(({ id, name }) => <option key={id} value={id}>{name}</option>)}
        </select>
      </label>
      <small>{toCoordinates(region)}</small>
    </header>
    <div className="hc-orbit-right">
      <button type="button" id={`${panelId}-readings`} className="hc-orbit-plain" aria-expanded={readingsOpen} disabled={busy} onClick={() => {
        setActiveControl(null);
        setReadingsOpen(!readingsOpen);
      }}>Readings <Chevron /></button>
      {readingsOpen && !busy && <dl className="hc-orbit-readings">
        {[
          ["Temperature", `${weather.temperature.toFixed(1)} °C`], ["Rain", `${weather.rain.toFixed(0)} mm/mo`],
          ["Humidity", `${weather.humidity.toFixed(0)}%`], ["Wind", `${weather.wind.toFixed(1)} m/s`],
          ["Soil moisture", `${Math.round(environment.soilMoistureIndex * 100)}%`],
          ["Vegetation health", `${Math.round(environment.vegetationHealthIndex * 100)}%`],
          ["Drought stress", `${Math.round(environment.droughtStressIndex * 100)}%`],
        ].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}
        <div><dt>Climate baseline</dt><dd>1991–2020</dd></div>
      </dl>}
    </div>
    <div className="hc-orbit-bottom">
      {sequence && (busy || !preview || inspected !== undefined) && <div className="hc-orbit-playback">
        <div className="hc-orbit-playback-label">
          <span>{busy ? "Simulating" : inspected === 0 ? "Before run" : inspected !== undefined ? "Playback" : "Run complete"}</span>
          <b>{month} / {duration} mo</b>
        </div>
        <div className="hc-orbit-ticks" role="group" aria-label="Experiment timeline">
          {sequence.frames.map((_, index) => <button key={index} type="button" disabled={busy} title={index ? `Month ${index}` : "Before the experiment"}
            aria-label={index ? `View month ${index}` : "Before the experiment"} aria-current={index === month ? "step" : undefined}
            data-past={index <= month ? "" : undefined} onClick={() => inspect(index)}><i /></button>)}
        </div>
      </div>}
      {busy ? <span className="hc-orbit-sr" role="status">Running experiment</span> : <>
        {warmUp && !control && !sequence && <p className="hc-orbit-coach">{warming ? "Run the calibration." : "Raise the heat to calibrate."}</p>}
        {control && <div className="hc-orbit-adjust" ref={adjustment} id={panelId} style={{ "--control-color": control.color } as CSSProperties}>
          <div className="hc-orbit-adjust-top"><strong>{control.label}</strong>
            <button type="button" aria-label="Close adjustment" onClick={() => {
              setActiveControl(null);
              document.getElementById(`${panelId}-${control.key}`)?.focus();
            }}>×</button>
          </div>
          <div className="hc-orbit-adjust-value">
            <button type="button" aria-label={`Decrease ${control.label.toLowerCase()}`} disabled={save.conditions[control.key] <= control.min}
              onClick={() => changeCondition(control.key, Math.max(control.min, save.conditions[control.key] - control.step))}>−</button>
            <output aria-live="polite">{settingLabel(control.key, save.conditions[control.key])}</output>
            <button type="button" data-adjust aria-label={`Increase ${control.label.toLowerCase()}`} disabled={save.conditions[control.key] >= control.max}
              onClick={() => changeCondition(control.key, Math.min(control.max, save.conditions[control.key] + control.step))}>+</button>
          </div>
          <div className="hc-orbit-presets" aria-label={`${control.label} presets`}>
            {[{ label: TOOLS[control.key].left, value: control.min }, { label: "Normal", value: 0 }, { label: TOOLS[control.key].right, value: control.max }]
              .map(({ label, value }) => <button key={label} type="button" aria-pressed={save.conditions[control.key] === value} onClick={() => changeCondition(control.key, value)}>{label}</button>)}
          </div>
        </div>}
        <div className="hc-orbit-dock">
          <div className="hc-orbit-tools" role="group" aria-label="Weather controls">
            {CONTROLS.map(({ key, label, color }) => {
              const { title, Icon } = TOOLS[key];
              return <button type="button" key={key} id={`${panelId}-${key}`} className="hc-orbit-tool" disabled={warmUp && key !== "heat"}
                title={label} aria-label={label} aria-expanded={activeControl === key}
                aria-controls={activeControl === key ? panelId : undefined} data-changed={save.conditions[key] !== 0 ? "" : undefined}
                style={{ "--control-color": color } as CSSProperties} onClick={() => {
                  setReadingsOpen(false);
                  setActiveControl((open) => open === key ? null : key);
                }}>
                <Icon />
                <span>{title}</span>
                {save.conditions[key] !== 0 && <i aria-hidden="true" />}
              </button>;
            })}
          </div>
          <div className="hc-orbit-run-group">
            <label className="hc-orbit-duration">
              <span className="hc-orbit-sr">Experiment duration</span>
              <select value={save.duration} onChange={(event) => {
                const time = TIMES.find(({ months }) => months === Number(event.target.value));
                if (time) update({ duration: time.months });
              }}>{TIMES.map(({ months, label }) => <option key={months} value={months}>{label}</option>)}</select>
              <Chevron />
            </label>
            <button type="button" className="hc-orbit-run" aria-label="Run experiment" disabled={warmUp && !warming} onClick={startRun}>
              <span aria-hidden="true">▶</span> Run
            </button>
          </div>
        </div>
      </>}
    </div>
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
