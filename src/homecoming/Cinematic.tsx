"use client";

import dynamic from "next/dynamic";
import { Component, useCallback, useEffect, useRef, useState, type CSSProperties, type ReactNode } from "react";
import type { Shot } from "./story";
import type { Cue } from "./audio";

const FilmStage = dynamic(() => import("./FilmStage").then((module) => module.FilmStage), { ssr: false });
const CHIPS = ["#ffca68", "#ff83bc", "#7fe7f1", "#bbf5aa", "#b69aff"];

export function Confetti({ kind = "fall" }: { kind?: "fall" | "burst" }) {
  return <div className={`hc-confetti hc-confetti-${kind}`} aria-hidden>
    {Array.from({ length: 42 }, (_, i) => <i key={i} style={{
      "--x": `${(i * 17.11) % 100}%`, "--dx": `${Math.sin(i * 5) * 50}vw`, "--dy": `${Math.cos(i * 3.7) * 53}vh`,
      "--delay": `${(i % 9) * .073}s`, "--spin": `${(i % 8 - 4) * 145}deg`, "--chip": CHIPS[i % CHIPS.length],
    } as CSSProperties} />)}
  </div>;
}

class FilmBoundary extends Component<{ children: ReactNode; onFailure: () => void }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  componentDidCatch() { this.props.onFailure(); }
  render() { return this.state.failed ? null : this.props.children; }
}

export function Cinematic({ shots, complete, cue, sound, toggleSound }: {
  shots: Shot[]; complete: () => void; cue: (name: Cue) => void; sound: boolean; toggleSound: () => void;
}) {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);
  const [transcript, setTranscript] = useState(false);
  const [reduced] = useState(() => typeof window !== "undefined" && matchMedia("(prefers-reduced-motion: reduce)").matches);
  const [playing, setPlaying] = useState(false);
  const [failed, setFailed] = useState(false);
  const video = useRef<HTMLVideoElement>(null);
  const time = useRef(0);
  const bar = useRef<HTMLDivElement>(null);
  const shot = shots[index];
  const total = shots.reduce((sum, item) => sum + item.seconds, 0);
  const pastTime = shots.slice(0, index).reduce((sum, item) => sum + item.seconds, 0);

  useEffect(() => {
    if (!playing && !failed) return;
    const duration = shot.seconds;
    let frame = 0;
    let last = performance.now();
    const advance = (now: number) => {
      const before = time.current;
      if (!paused && !transcript) {
        time.current = Math.min(duration, before + Math.min((now - last) / 1000, .1));
        for (const [at, name] of shot.cues ?? []) if (before < at && time.current >= at) cue(name);
      }
      last = now;
      if (bar.current) bar.current.style.width = `${(pastTime + time.current) / total * 100}%`;
      if (time.current < duration) frame = requestAnimationFrame(advance);
      else if (index < shots.length - 1) { time.current = 0; setIndex(index + 1); }
      else complete();
    };
    frame = requestAnimationFrame(advance);
    return () => cancelAnimationFrame(frame);
  }, [complete, cue, failed, index, pastTime, paused, playing, shot, shots.length, total, transcript]);

  useEffect(() => {
    const tape = video.current;
    if (tape && shot.id === "apollo") tape.currentTime = 0;
  }, [shot.id]);
  useEffect(() => {
    const tape = video.current;
    if (!tape) return;
    tape.muted = !sound || shot.id !== "apollo";
    if ((!playing && !failed) || paused || transcript || !["apollo", "reveal"].includes(shot.id)) tape.pause();
    else if (tape.paused) void tape.play().catch(() => { tape.muted = true; void tape.play().catch(() => {}); });
  }, [failed, paused, playing, shot.id, sound, transcript]);

  const setReady = useCallback(() => setPlaying(true), []);
  const fail = useCallback(() => setFailed(true), []);
  const next = () => {
    if (index === shots.length - 1) complete();
    else { time.current = 0; setIndex(index + 1); }
  };

  useEffect(() => {
    function keydown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        if (transcript) setTranscript(false);
        else complete();
      }
      if (event.code !== "Space" || event.repeat || (event.target instanceof Element && event.target.closest("button, a"))) return;
      event.preventDefault();
      setPaused((value) => !value);
    }
    window.addEventListener("keydown", keydown);
    return () => window.removeEventListener("keydown", keydown);
  }, [complete, transcript]);

  const stage = ["apollo", "signal", "earth", "title", "deflate", "lab"].includes(shot.id) ? shot.id : "bridge";
  return <main className={`hc-film hc-shot-${shot.id}`} aria-label="The Great Homecoming cinematic">
    <div className={`hc-film-stage hc-film-set-${stage}`} aria-hidden>
      <video className={`hc-film-video ${failed && shot.id === "apollo" ? "hc-film-video-visible" : ""}`} ref={video} src="/homecoming/apollo11.mp4" poster="/homecoming/apollo11.jpg" preload="auto" playsInline muted={!sound} />
      {!failed && <FilmBoundary onFailure={fail}><FilmStage shot={shot} time={time} video={video} ready={setReady} reduced={reduced} failed={fail} /></FilmBoundary>}
      {failed && shot.id === "signal" && <div className="hc-film-moon-fallback" />}
      <div className="hc-film-grade" />
    </div>
    {shot.label && <div className="hc-film-date" key={shot.label}>{shot.label}</div>}
    {shot.id === "title" && <div className="hc-title-card"><span>Operation</span><h1>THE GREAT<br /><em>HOMECOMING</em></h1></div>}
    <p className="sr-only" aria-live="polite">{shot.description}</p>
    {failed && <p className="hc-film-warning" role="status">3D is unavailable. You can read the story or skip to the game.</p>}
    {transcript && <section className="hc-film-transcript" aria-label="Scene description"><p>{shot.description}</p>{shot.line && <p>{shot.line}</p>}</section>}
    <div className="hc-film-controls">
      <div className="hc-film-progress" aria-label={`Shot ${index + 1} of ${shots.length}`}><div ref={bar} /></div>
      <div className="hc-row">
        <button type="button" onClick={() => setPaused(!paused)}>{paused ? "Resume" : "Pause"}</button>
        <button type="button" onClick={next}>Next <span aria-hidden>→</span></button>
        <button type="button" aria-pressed={sound} onClick={toggleSound}>Sound {sound ? "on" : "off"}</button>
        <button type="button" aria-pressed={transcript} onClick={() => setTranscript(!transcript)}>{transcript ? "Hide description" : "Scene description"}</button>
        <button type="button" onClick={complete}>Skip cinematic</button>
      </div>
    </div>
  </main>;
}
