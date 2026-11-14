"use client";

import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore, type FormEvent } from "react";
import { EARTH_REGIONS } from "@/earth/regions";
import { evidenceKey } from "@/lib/earth/member3";
import { Archive } from "./Archive";
import { Cinematic, Confetti } from "./Cinematic";
import { CrewLine, CrewPortrait } from "./Crew";
import { DiscoveryRecord, Lab, TrialSummary } from "./Lab";
import { makeAudio, type Cue } from "./audio";
import { DISCOVERIES, DISCOVERY_BY_ID, REQUIRED_DISCOVERIES, newSave, recordTrial, runTrial, toNormals,
  type ClimateCatalog, type Investigation, type Normals, type Save, type Trial, type WeatherKey } from "./model";
import { ENDING, LAB_INTRO, PROLOGUE } from "./story";

type Stage = "home" | "intro" | "recruit" | "lab-intro" | "lab" | "archive" | "book" | "report" | "ending" | "epilogue";
const KEY = "great-homecoming-save-v1";
const SAVE_EVENT = "homecoming-save";
let currentMemory: string | null | undefined;

function readSave() {
  if (currentMemory !== undefined) return currentMemory;
  try { return window.localStorage.getItem(KEY); } catch { return null; }
}
function writeSave(save: Save | null) {
  const encoded = save ? JSON.stringify(save) : null;
  currentMemory = encoded;
  try { if (encoded) window.localStorage.setItem(KEY, encoded); else window.localStorage.removeItem(KEY); } catch { /* Play continues in memory. */ }
  window.dispatchEvent(new Event(SAVE_EVENT));
}
function subscribe(callback: () => void) {
  const sync = (event: StorageEvent) => { if (event.key !== null && event.key !== KEY) return; currentMemory = undefined; callback(); };
  window.addEventListener(SAVE_EVENT, callback);
  window.addEventListener("storage", sync);
  return () => { window.removeEventListener(SAVE_EVENT, callback); window.removeEventListener("storage", sync); };
}
function parseSave(raw: string | null): Save | null {
  if (!raw) return null;
  try {
    const save = JSON.parse(raw) as Save;
    return save.version === 1 && typeof save.name === "string" && EARTH_REGIONS.some(({ id }) => id === save.region) &&
      Array.isArray(save.trials) && Array.isArray(save.investigations) && save.regions && save.discoveries && save.conditions
      ? { ...save, investigations: save.investigations.filter((entry) => entry.evidenceVersion === 1 && entry.datasetId && entry.productVersion) } : null;
  } catch { return null; }
}

function Insignia() {
  return <span className="hc-insignia" aria-hidden><i /><b>✦</b></span>;
}

export function Homecoming() {
  const raw = useSyncExternalStore(subscribe, readSave, () => null);
  const save = useMemo(() => parseSave(raw), [raw]);
  const [requestedStage, setStage] = useState<Stage>("home");
  const stage = !save && !["home", "intro", "recruit"].includes(requestedStage) ? "home" : requestedStage;
  const [sound, setSound] = useState(false);
  const [designation, setDesignation] = useState("");
  const [climate, setClimate] = useState<ClimateCatalog | null>(null);
  const [dataError, setDataError] = useState(false);
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [busy, setBusy] = useState(false);
  const [reward, setReward] = useState<Trial | null>(null);
  const [archiveTarget, setArchiveTarget] = useState<{ region: string; variable: WeatherKey }>({ region: "phoenix", variable: "temperature" });
  const [reset, setReset] = useState(false);
  const [credits, setCredits] = useState(false);
  const [showArchiveInvite, setShowArchiveInvite] = useState(false);
  const [message, setMessage] = useState("");
  const [audio] = useState(makeAudio);
  const runTimer = useRef<number | null>(null);
  const messageTimer = useRef<number | null>(null);
  const main = useRef<HTMLDivElement>(null);
  const modal = useRef<HTMLDialogElement>(null);
  const openModal = Boolean(reward || reset || credits || showArchiveInvite);

  useEffect(() => {
    const controller = new AbortController();
    fetch("/homecoming/regional-climate.json", { signal: controller.signal })
      .then((response) => { if (!response.ok) throw new Error("Climate records unavailable"); return response.json() as Promise<ClimateCatalog>; })
      .then((data) => {
        for (const { id } of EARTH_REGIONS) toNormals(data[id]);
        setClimate(data); setDataError(false);
      })
      .catch(() => { if (!controller.signal.aborted) setDataError(true); });
    return () => controller.abort();
  }, [loadAttempt]);
  const normals = useMemo<Normals | null>(() => climate ? Object.fromEntries(Object.entries(climate).map(([id, data]) => [id, toNormals(data)])) : null, [climate]);

  useEffect(() => () => {
    if (runTimer.current !== null) window.clearTimeout(runTimer.current);
    if (messageTimer.current !== null) window.clearTimeout(messageTimer.current);
    audio.close();
  }, [audio]);
  useEffect(() => { main.current?.scrollIntoView({ block: "start" }); }, [stage]);
  useEffect(() => {
    const element = modal.current;
    if (!element) return;
    if (openModal && !element.open) element.showModal();
    if (!openModal && element.open) element.close();
  }, [openModal]);

  const cue = useCallback((name: Cue) => audio.cue(name), [audio]);
  const showRecruitment = useCallback(() => setStage("recruit"), []);
  const showLab = useCallback(() => setStage("lab"), []);
  const finishStory = useCallback(() => {
    const latest = parseSave(readSave());
    if (latest) writeSave({ ...latest, ending: true });
    setStage("epilogue");
  }, []);

  function soundToggle() {
    if (sound) { audio.mute(); setSound(false); return; }
    void audio.enable().then(() => { setSound(true); cue("radio"); }).catch(() => setMessage("Sound is unavailable on this device."));
  }
  function begin(withSound: boolean) {
    if (withSound) {
      void audio.enable().then(() => { setSound(true); cue("radio"); }).catch(() => {});
    }
    setStage("intro");
  }
  function notify(text: string) {
    setMessage(text);
    if (messageTimer.current !== null) window.clearTimeout(messageTimer.current);
    messageTimer.current = window.setTimeout(() => setMessage(""), 3900);
  }
  function recruit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const name = designation.trim().replace(/\s+/g, " ").slice(0, 28);
    if (!name) return;
    writeSave(newSave(name));
    cue("stamp");
    setStage("lab-intro");
  }
  function update(patch: Partial<Save>) {
    const latest = parseSave(readSave());
    if (latest) writeSave({ ...latest, ...patch });
  }
  function navigate(to: Stage) { cue("click"); setStage(to); }
  function archive(variable: WeatherKey = "temperature", region = save?.region ?? "phoenix") {
    setShowArchiveInvite(false); setReward(null);
    setArchiveTarget({ region, variable });
    navigate("archive");
  }
  function run() {
    if (!save || !normals || busy || runTimer.current !== null) return;
    const trial = runTrial(save, normals[save.region], save.duration);
    const first = Object.keys(save.discoveries).length === 0 && trial.discovered.length > 0;
    setBusy(true);
    cue("scan");
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    runTimer.current = window.setTimeout(() => {
      runTimer.current = null;
      const latest = parseSave(readSave());
      setBusy(false);
      if (!latest || latest.name !== save.name || latest.trials.at(-1)?.number !== save.trials.at(-1)?.number) return;
      const next = recordTrial(latest, trial);
      writeSave(next);
      if (trial.discovered.length) { setReward(trial); cue("discover"); }
      else { cue(trial.outcome === "quiet" ? "quiet" : "change"); notify(save.guided ? trial.outcome === "quiet" ? "Experiment logged. No major event." : "Experiment logged. Conditions changed." : "Calibration complete. All controls online."); }
      if (first) setShowArchiveInvite(true);
      if (Object.keys(next.discoveries).length >= REQUIRED_DISCOVERIES && save.investigations.length && Object.keys(save.discoveries).length < REQUIRED_DISCOVERIES) notify("Zorvax says your evidence is ready to submit.");
    }, reduced ? 140 : 1100);
  }
  function recordInvestigation(entry: Investigation) {
    const latest = parseSave(readSave());
    if (!latest) return;
    const duplicate = latest.investigations.some((item) => evidenceKey(item) === evidenceKey(entry));
    if (duplicate) return;
    writeSave({ ...latest, investigations: [...latest.investigations, entry] });
    cue("stamp");
    notify(Object.keys(latest.discoveries).length >= REQUIRED_DISCOVERIES ? "Evidence filed. Zorvax is ready to submit findings." : "Investigation filed for the final report.");
  }
  function dismissModal() {
    if (reward) setReward(null);
    else if (showArchiveInvite) setShowArchiveInvite(false);
    else { setReset(false); setCredits(false); }
  }
  function resetGame() {
    if (runTimer.current !== null) window.clearTimeout(runTimer.current);
    runTimer.current = null;
    writeSave(null);
    setBusy(false); setReward(null); setReset(false); setShowArchiveInvite(false); setDesignation(""); setMessage("");
    setStage("home");
  }

  const discoveries = save ? Object.keys(save.discoveries).length : 0;
  const canSubmit = discoveries >= REQUIRED_DISCOVERIES && Boolean(save?.investigations.length);
  const inFilm = ["intro", "lab-intro", "ending"].includes(stage);
  return <div className={`hc-app hc-stage-${stage}`}>
    {!inFilm && <header className="hc-topbar">
      <button className="hc-brand" type="button" disabled={busy} onClick={() => navigate("home")}><Insignia /><span>Intergalactic<br />Affection Department</span></button>
      {save && !["home", "epilogue"].includes(stage) && <nav aria-label="Ship navigation">
        <button type="button" aria-current={stage === "lab" ? "page" : undefined} disabled={busy} onClick={() => navigate("lab")}>Laboratory</button>
        <button type="button" aria-current={stage === "archive" ? "page" : undefined} disabled={busy || discoveries < 1} title={discoveries ? undefined : "Unlock with your first discovery"} onClick={() => archive()}>Earth Archive</button>
        <button type="button" aria-current={stage === "book" ? "page" : undefined} disabled={busy} onClick={() => navigate("book")}>Encyclopedia <b>{discoveries}</b></button>
        <button type="button" aria-current={stage === "report" ? "page" : undefined} disabled={busy} onClick={() => navigate("report")}>Report{save.ending ? " ✓" : ""}</button>
      </nav>}
      <div className="hc-top-actions"><button type="button" className="hc-sound" aria-pressed={sound} onClick={soundToggle}><span aria-hidden>{sound ? "◖))" : "◖×"}</span> Sound {sound ? "on" : "off"}</button></div>
    </header>}
    <div ref={main}>
      {stage === "home" && <main className="hc-home">
        <div className="hc-home-art" />
        <div className="hc-landing-inner"><div className="hc-landing-copy">
          <p className="hc-kicker hc-transmission"><i /> SIGNAL INTERCEPTED · JULY 1969</p>
          <h1><span>THE GREAT</span><em>HOMECOMING</em></h1>
          <p>One human went to the Moon. An alien welcome party made <em>several</em> assumptions.</p>
          <div className="hc-home-actions"><button type="button" className="hc-primary" onClick={() => begin(!save || sound)}><span aria-hidden>▶</span> Play transmission</button>
          <button type="button" className="hc-secondary" onClick={() => navigate(save ? "lab" : "recruit")}>{save ? "Return to the lab" : "Skip to recruitment"} <span aria-hidden>→</span></button></div>
        </div><div className="hc-landing-bottom"><span>NASA SPACE APPS KUWAIT</span><span><i /> MISSION STATUS <b>57 YEARS BEHIND</b></span></div></div>
      </main>}
      {stage === "intro" && <Cinematic beats={PROLOGUE} complete={save ? showLab : showRecruitment} cue={cue} sound={sound} />}
      {stage === "lab-intro" && <Cinematic beats={LAB_INTRO} complete={showLab} cue={cue} sound={sound} />}
      {stage === "ending" && <Cinematic beats={ENDING} complete={finishStory} cue={cue} sound={sound} />}
      {stage === "recruit" && <main className="hc-recruit hc-section"><div className="hc-panel hc-recruit-panel">
        <div className="hc-recruit-art"><CrewPortrait id="commander" /></div>
        <div className="hc-recruit-copy"><span className="hc-kicker"><i /> INCOMING TRANSMISSION</span><h1>Welcome aboard, recruit.</h1>
          <p>“We love humans. Humans love Earth. This is a problem. Your job is to study what makes their planet so special and how we might make ours more appealing.”</p>
          <form onSubmit={recruit}><label htmlFor="designation">Recruit designation</label><input id="designation" required maxLength={28} value={designation} onChange={(event) => setDesignation(event.target.value)} placeholder="Enter your alien name" autoComplete="off" />
            <button type="submit" className="hc-primary" disabled={!designation.trim()}>Join the operation <span aria-hidden>→</span></button></form>
          <small>Progress saves in this browser. No account needed.</small></div>
      </div></main>}
      {stage === "lab" && save && <main>{normals ? <Lab save={save} normals={normals} busy={busy} run={run} update={update} openArchive={archive} />
        : <div className="hc-section hc-panel hc-loading" role="status">{dataError ? <>The climate records couldn’t load. <button className="hc-primary" onClick={() => setLoadAttempt((n) => n + 1)}>Retry</button></> : "Calibrating historical climate baselines…"}</div>}</main>}
      {stage === "archive" && save && <main><Archive key={`${archiveTarget.region}/${archiveTarget.variable}`} initialRegion={archiveTarget.region} initialVariable={archiveTarget.variable} investigations={save.investigations} record={recordInvestigation} /></main>}
      {stage === "book" && save && <main className="hc-section">
        <div className="hc-section-heading"><span className="hc-kicker"><i /> FIELD RECORDS</span><h1>Galactic Encyclopedia</h1><p>{discoveries} of {DISCOVERIES.length} phenomena recorded. Discoveries can happen in any order.</p></div>
        <div className="hc-book-grid">{DISCOVERIES.map((entry) => save.discoveries[entry.id]
          ? <DiscoveryRecord key={entry.id} entry={entry} trial={save.discoveries[entry.id]} save={save} investigate={() => archive(entry.evidence, save.discoveries[entry.id].region)} />
          : <div className="hc-panel hc-record hc-undiscovered" key={entry.id}><span className="hc-record-icon" aria-hidden>◇</span><h2>Undiscovered</h2></div>)}</div>
        <details className="hc-panel hc-trial-log"><summary>Experiment log · {save.trials.length} recent runs</summary>
          {save.trials.length ? [...save.trials].reverse().map((trial) => <TrialSummary key={trial.number} trial={trial} />) : <p>No experiments logged yet.</p>}</details>
      </main>}
      {stage === "report" && save && <main className="hc-section hc-report">
        <div className="hc-section-heading"><span className="hc-kicker"><i /> RELOCATION PROGRAM</span><h1>The Great Homecoming Report</h1><p>Filed by Agent {save.name}</p></div>
        <div className="hc-panel hc-report-card">
          <div className="hc-report-stats">
            {[[`${discoveries}/${DISCOVERIES.length}`, "Discoveries"], [save.trials.at(-1)?.number ?? 0, "Experiments"], [save.investigations.length, "Earth investigations"], [0, "Humans relocated"]].map(([value, label]) => <div key={label}><b>{value}</b><span>{label}</span></div>)}
          </div>
          {discoveries > 0 && <div className="hc-tag-row">{Object.keys(save.discoveries).map((id) => <span key={id}>{DISCOVERY_BY_ID[id]?.label}</span>)}</div>}
          {save.investigations.length > 0 && <div className="hc-report-evidence"><h2>Filed Earth evidence</h2>{save.investigations.map((item, i) => <div key={i}><b>{item.variable} · {item.region}</b><span>{item.period} · {item.dataset}</span><p>{item.verdict} · {item.slope}</p><small>{item.test}</small></div>)}</div>}
          <CrewLine id="commander">{canSubmit ? `AGENT ${save.name.toUpperCase()}! OUTSTANDING PROGRESS. HUMAN ARRIVAL IS PRACTICALLY GUARANTEED.` : "I’M TOLD THE GOVERNMENT NOW REQUIRES EVIDENCE."}</CrewLine>
          <p>{canSubmit ? "Scientific confidence in this assessment: nonexistent." : `Discover any ${REQUIRED_DISCOVERIES} phenomena and file at least one real-data investigation to submit your report.`}</p>
          <div className="hc-row"><button type="button" className="hc-primary" disabled={!canSubmit} onClick={() => { cue("stamp"); setStage("ending"); }}>{save.ending ? "Replay final hearing" : "Submit findings"} →</button><button type="button" className="hc-secondary" onClick={showLab}>Return to laboratory</button></div>
        </div>
      </main>}
      {stage === "epilogue" && <main className="hc-epilogue"><div><Insignia /><h1>The Great Homecoming</h1><p><i /> STATUS · STILL WAITING FOR HUMANITY</p><em>Better Together. Eventually.</em><div className="hc-row"><button className="hc-primary" onClick={showLab}>Continue experimenting</button><button className="hc-secondary" onClick={() => setReset(true)}>Start over</button></div></div></main>}
    </div>
    {!inFilm && <footer className="hc-footer"><span>Intergalactic Affection Department</span><div><button onClick={() => setCredits(true)}>Credits & data</button>{save && <button disabled={busy} onClick={() => setReset(true)}>Start over</button>}</div></footer>}
    {message && <div className="hc-toast" role="status">{message}</div>}
    <dialog className="hc-modal" ref={modal} onCancel={(event) => { event.preventDefault(); dismissModal(); }} aria-labelledby="homecoming-dialog-title">
      {reward ? <><Confetti kind="burst" /><span className="hc-kicker">✦ NEW DISCOVERY</span><h2 id="homecoming-dialog-title">{DISCOVERY_BY_ID[reward.discovered[0]].label}</h2>
        <CrewLine id="commander">“{DISCOVERY_BY_ID[reward.discovered[0]].quote}”</CrewLine>
        {reward.discovered.length > 1 && <p>Also recorded {reward.discovered.slice(1).map((id) => DISCOVERY_BY_ID[id].label).join(", ")}.</p>}
        <TrialSummary trial={reward} /><div className="hc-row"><button className="hc-primary" onClick={() => setReward(null)}>{showArchiveInvite ? "Continue" : "Back to lab"}</button>
          <button className="hc-secondary" onClick={() => { setReward(null); setShowArchiveInvite(false); navigate("book"); }}>Open encyclopedia</button></div></>
      : showArchiveInvite ? <><span className="hc-kicker">INCOMING TRANSMISSION</span><h2 id="homecoming-dialog-title">The Earth Archive is open.</h2>
        <CrewLine id="scientist">Before we declare victory, let’s compare the simulation with actual observations.</CrewLine>
        <CrewLine id="commander">OBSERVATIONS? I OBSERVED THE PLANET LOOKING TERRIBLE.</CrewLine>
        <div className="hc-row"><button className="hc-primary" onClick={() => { const recent = save?.trials.at(-1); archive(recent ? DISCOVERY_BY_ID[recent.discovered[0]]?.evidence : undefined); }}>Investigate Earth data →</button><button className="hc-secondary" onClick={dismissModal}>Later</button></div></>
      : reset ? <><h2 id="homecoming-dialog-title">Start a new operation?</h2><p>This clears the recruit and progress saved in this browser.</p><div className="hc-row"><button className="hc-primary" onClick={resetGame}>Start over</button><button className="hc-secondary" onClick={dismissModal}>Keep playing</button></div></>
      : credits ? <><span className="hc-kicker">NASA SPACE APPS KUWAIT</span><h2 id="homecoming-dialog-title">Credits & data</h2>
        <p>Historical records are NASA POWER MERRA-2 reanalysis and NASA GPM IMERG satellite precipitation estimates. Climate experiments use a simplified educational model.</p>
        <ul><li><a href="https://power.larc.nasa.gov/" target="_blank" rel="noreferrer">NASA POWER climate data</a></li><li><a href="https://doi.org/10.5067/GPM/IMERG/3B-MONTH/07" target="_blank" rel="noreferrer">GPM IMERG Final Run V07</a></li><li><a href="https://www.nasa.gov/wp-content/uploads/static/history/alsj/a11/video11.html" target="_blank" rel="noreferrer">Apollo Lunar Surface Journal · archival television</a></li><li><a href="https://svs.gsfc.nasa.gov/5187/" target="_blank" rel="noreferrer">NASA GSFC · lunar imagery</a></li></ul>
        <button className="hc-primary" onClick={dismissModal}>Close credits</button></> : null}
    </dialog>
  </div>;
}
