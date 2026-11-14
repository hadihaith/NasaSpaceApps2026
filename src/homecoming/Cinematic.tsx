"use client";

import { useEffect, useState, type CSSProperties } from "react";
import { CrewPortrait, CREW } from "./Crew";
import type { Beat } from "./story";
import type { Cue } from "./audio";

const CHIPS = ["#ffca68", "#ff83bc", "#7fe7f1", "#bbf5aa", "#b69aff"];

export function Confetti({ kind = "fall" }: { kind?: "fall" | "burst" }) {
  return <div className={`hc-confetti hc-confetti-${kind}`} aria-hidden>
    {Array.from({ length: 42 }, (_, i) => <i key={i} style={{
      "--x": `${(i * 17.11) % 100}%`, "--dx": `${Math.sin(i * 5) * 50}vw`, "--dy": `${Math.cos(i * 3.7) * 53}vh`,
      "--delay": `${(i % 9) * .073}s`, "--spin": `${(i % 8 - 4) * 145}deg`, "--chip": CHIPS[i % CHIPS.length],
    } as CSSProperties} />)}
  </div>;
}

export function Cinematic({ beats, complete, cue, sound }: { beats: Beat[]; complete: () => void; cue: (name: Cue) => void; sound: boolean }) {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);
  const beat = beats[index];
  const { shot, speaker, caption, label } = beat;
  const next = () => index + 1 < beats.length ? setIndex(index + 1) : complete();

  useEffect(() => {
    if (beat.cue) cue(beat.cue);
  }, [beat, cue]);

  useEffect(() => {
    if (paused) return;
    const timeout = window.setTimeout(() => {
      if (index + 1 < beats.length) setIndex(index + 1);
      else complete();
    }, beat.ms);
    return () => window.clearTimeout(timeout);
  }, [beat, beats.length, complete, index, paused]);

  useEffect(() => {
    function keydown(event: KeyboardEvent) {
      if (event.key === "Escape") complete();
      if (event.code !== "Space" || event.repeat || (event.target instanceof Element && event.target.closest("button, a"))) return;
      event.preventDefault();
      setPaused((value) => !value);
    }
    window.addEventListener("keydown", keydown);
    return () => window.removeEventListener("keydown", keydown);
  }, [complete]);

  const onBridge = ["bridge", "party", "dust", "artemis", "window", "button", "hearing"].includes(shot);
  return (
    <main className={`hc-film hc-shot-${shot} ${paused ? "hc-paused" : ""}`} aria-label="Story cinematic">
      <div className="hc-film-picture" key={shot}>
        {shot === "moon" && <div className="hc-film-moon" role="img" aria-label="The Moon, NASA imagery" />}
        {shot === "apollo" && <div className="hc-apollo"><video key={String(paused)} src="/homecoming/apollo11.mp4" poster="/homecoming/apollo11.jpg" autoPlay={!paused} muted={!sound} playsInline /><div className="hc-scanlines" /></div>}
        {onBridge && <div className="hc-bridge-plate" />}
        {shot === "bridge" && <div className="hc-tv-feed"><video src="/homecoming/apollo11.mp4" poster="/homecoming/apollo11.jpg" autoPlay={!paused} playsInline muted /><div className="hc-scanlines" /></div>}
        {shot === "artemis" && <div className="hc-tv-feed hc-artemis"><span>EARTH NEWS</span><b>ARTEMIS</b><small>HUMANS RETURNING TO THE MOON</small></div>}
        {shot === "window" && <div className="hc-window-earth" />}
        {shot === "dust" && <div className="hc-dust" />}
        {shot === "button" && <div className="hc-red-button"><i /><b>DO NOT SLAM</b></div>}
        {shot === "party" && <><div className="hc-party-banner">{label ?? "WELCOME HOME, HUMANS!"}</div><Confetti /></>}
        {shot === "lab" && <><div className="hc-lab-plate" /><div className="hc-film-globe" /></>}
        {shot === "hearing" && <div className="hc-hearing-stamp">OFFICIAL<br />GOVERNMENT<br />HEARING</div>}
        {(["bridge", "party", "dust", "artemis", "window", "lab", "hearing"] as string[]).includes(shot) && <div className="hc-film-crew">
          {(["commander", "scientist", "cadet"] as const).map((id) => <CrewPortrait key={id} id={id} className={`${speaker === id ? "hc-speaking" : ""} hc-film-${id}`} />)}
        </div>}
        {shot === "title" && <div className="hc-title-card"><span>Intergalactic Affection Department</span><h1>THE GREAT<br /><em>HOMECOMING</em></h1><p>{caption}</p><b>57 years behind schedule</b></div>}
        {shot === "black" && <div className="hc-silent-card">{label}</div>}
      </div>
      {label && !["title", "black", "party"].includes(shot) && <div className="hc-feed-label"><i />{label}</div>}
      <div className="hc-film-bottom">
        {caption && shot !== "title" && <div className="hc-subtitles" key={index} aria-live="polite">{speaker && <b style={{ color: CREW[speaker].color }}>{CREW[speaker].name}</b>}<p>{caption}</p></div>}
        <div className="hc-film-controls">
          <div className="hc-film-progress" aria-label={`Scene ${index + 1} of ${beats.length}`}>
            {beats.map((_, i) => <i key={i} className={i <= index ? "is-past" : ""} />)}
          </div>
          <div className="hc-row"><button type="button" onClick={() => setPaused(!paused)}>{paused ? "Resume" : "Pause"}</button><button type="button" onClick={next}>Next <span aria-hidden>→</span></button><button type="button" onClick={complete}>Skip cinematic</button></div>
        </div>
      </div>
    </main>
  );
}
