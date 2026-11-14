"use client";

import { useState, type CSSProperties, type ReactNode } from "react";

export const CREW = {
  commander: { name: "Commander Zorvax", short: "Zorvax", file: "commander-zorvax.png", color: "#f7b0fc", mark: "CZ" },
  scientist: { name: "Dr. Nyxen", short: "Nyxen", file: "dr-nyxen.png", color: "#8ce8f5", mark: "DN" },
  cadet: { name: "Cadet B", short: "Cadet B", file: "cadet-b.png", color: "#ffcf75", mark: "CB" },
} as const;

export type CrewId = keyof typeof CREW;

export function CrewPortrait({ id, className = "" }: { id: CrewId; className?: string }) {
  const { name, color, file, mark } = CREW[id];
  const [loaded, setLoaded] = useState(false);
  return (
    <div className={`hc-portrait ${className}`} style={{ "--crew": color } as CSSProperties}>
      {!loaded && <div className="hc-placeholder" aria-label={`${name} placeholder, ${file}`}>
        <div className="hc-placeholder-icon" aria-hidden>{mark}</div>
        <strong>{name}</strong><small>{file}</small>
      </div>}
      {/* These public URLs are the character drop-in interface. */}
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={`/characters/${file}`} alt={name} className={loaded ? "is-loaded" : ""} onLoad={() => setLoaded(true)} onError={() => setLoaded(false)} />
    </div>
  );
}

export function CrewLine({ id, children }: { id: CrewId; children: ReactNode }) {
  return <div className="hc-line"><CrewPortrait id={id} className="hc-avatar" /><div><strong style={{ color: CREW[id].color }}>{CREW[id].name}</strong><p>{children}</p></div></div>;
}
