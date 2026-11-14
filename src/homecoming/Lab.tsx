"use client";

import dynamic from "next/dynamic";
import type { CSSProperties } from "react";
import { ThermometerIcon, DropIcon, WindIcon } from "@/components/icons";
import { EARTH_REGIONS } from "@/earth/regions";
import { CrewLine } from "./Crew";
import { CONTROLS, DISCOVERY_BY_ID, TIMES, initialState, settingLabel,
  type Control, type Discovery, type Duration, type Normals, type Save, type Trial, type WeatherKey } from "./model";

const EarthScene = dynamic(() => import("@/earth/EarthScene"), { ssr: false, loading: () => <div className="hc-globe-load">Calibrating the projector…</div> });

export function placeName(id: string) {
  return EARTH_REGIONS.find((region) => region.id === id)?.name ?? id;
}

export function Lab({ save, normals, busy, run, update, openArchive }: {
  save: Save; normals: Normals; busy: boolean; run: () => void; update: (patch: Partial<Save>) => void; openArchive: (variable?: WeatherKey) => void;
}) {
  const selected = save.regions[save.region] ?? initialState(normals[save.region]);
  const previous = save.trials.findLast((trial) => trial.region === save.region);
  const weather = previous?.weather ?? normals[save.region][0];
  const warmUp = !save.guided;
  const warming = warmUp && save.conditions.heat > 0;
  const environment = { ...selected, droughtStressIndex: Math.min(1, selected.droughtStressIndex + Math.max(0, save.conditions.heat) * .025) };

  function changeRegion(id: string) {
    if (!busy) update({ region: id });
  }
  function changeCondition(key: Control, value: number) {
    update({ conditions: { ...save.conditions, [key]: value } });
  }

  return <section className="hc-section hc-lab">
    <div className="hc-section-heading hc-lab-heading">
      <div><span className="hc-kicker"><i /> RESEARCH BAY</span><h1>Planetary Persuasion Device</h1><p>Adjust conditions, advance time and see what Earth does.</p><p>Starting climate: 1991–2020 monthly normals. Rain uses monthly totals.</p></div>
      <span className="hc-status"><i /> {warmUp ? "Calibration" : "Sandbox"} <b>·</b> {save.trials.at(-1)?.number ?? 0} runs</span>
    </div>
    <div className="hc-lab-layout">
      <div className="hc-visual-column">
        <div className={`hc-globe-frame ${busy ? "hc-globe-running" : ""}`}>
          <EarthScene regionId={save.region} environment={environment} onRegionSelect={changeRegion} presentation="homecoming" />
          {busy && <div className="hc-run-interstitial" role="status"><i /><strong>Advancing {save.duration} {save.duration === 1 ? "month" : "months"}</strong></div>}
        </div>
        <div className="hc-panel hc-meters" aria-label="Latest environment">
          <div className="hc-row hc-between"><b>{placeName(save.region)}</b><span className="hc-kicker">{selected.elapsed} simulated months</span></div>
          <div className="hc-meter-grid">
            {([
              ["Soil moisture", selected.soilMoistureIndex, "#73ddff"],
              ["Vegetation health", selected.vegetationHealthIndex, "#a9f4b7"],
              ["Drought stress", selected.droughtStressIndex, "#ffb17a"],
            ] as const).map(([label, value, color]) => <div key={label}><div><span>{label}</span><b>{Math.round(value * 100)}%</b></div><meter min={0} max={1} value={value} aria-label={label} style={{ "--meter": color } as CSSProperties} /></div>)}
          </div>
          <p>Latest simulated air temperature <strong>{weather.temperature.toFixed(1)} °C</strong> · humidity <strong>{weather.humidity.toFixed(0)}%</strong> · wind <strong>{weather.wind.toFixed(1)} m/s</strong></p>
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
              <input type="range" aria-label={label} min={min} max={max} step={step} value={save.conditions[key]} disabled={locked || busy} onChange={(event) => changeCondition(key, +event.target.value)} />
            </label>;
          })}
        </div>
        <div className="hc-time"><b>Advance time</b><div className="hc-segment" role="group" aria-label="Advance time">
          {TIMES.map(({ label, months }) => <button key={months} type="button" aria-pressed={save.duration === months} disabled={busy} onClick={() => update({ duration: months as Duration })}>{label}</button>)}
        </div></div>
        <button type="button" disabled={busy || (warmUp && !warming)} className="hc-primary hc-run" onClick={run}>{busy ? "Running experiment…" : "Run experiment"} <span aria-hidden>↗</span></button>
        <button type="button" className="hc-text-button" disabled={busy || warmUp} onClick={() => update({ conditions: { heat: 0, rain: 0, humidity: 0, wind: 0 } })}>Reset sliders</button>
      </aside>
    </div>
    <div className="hc-panel hc-crew-dispatch" aria-live="polite">
      {warmUp ? warming
        ? <><CrewLine id="commander">WE’VE DONE IT! A DEGREE OF SUCCESS!</CrewLine><CrewLine id="scientist">You changed the temperature. Now run the experiment to see how the model responds.</CrewLine></>
        : <CrewLine id="scientist">Welcome, {save.name}. Let’s begin with temperature. Increase it to wake up the simulator.</CrewLine>
      : previous ? <><CrewLine id={previous.outcome === "quiet" ? "scientist" : "commander"}>{previous.outcome === "quiet" ? "No major event. Check the measurements. Quiet results still tell us something." : previous.active.includes("recovery") ? DISCOVERY_BY_ID.recovery.quote : previous.outcome === "discovery" ? "NOTIFY THE ENCYCLOPEDIA. USE THE NICE STAMP." : "The planet has changed its tactics again."}</CrewLine>
        {!!previous.active.length && <div className="hc-tag-row">{previous.active.map((id) => <span key={id}>{DISCOVERY_BY_ID[id].label}</span>)}</div>}</>
        : <><CrewLine id="scientist">Rainfall, humidity and wind are online. Try different conditions and watch what you learn.</CrewLine><CrewLine id="cadet">I turned off the confetti sprinklers. Probably.</CrewLine></>}
      {!!Object.keys(save.discoveries).length && <button className="hc-text-button" type="button" onClick={() => openArchive()}>Investigate real Earth data →</button>}
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
