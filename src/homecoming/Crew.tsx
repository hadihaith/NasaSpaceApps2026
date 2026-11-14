"use client";

import type { CSSProperties, ReactNode } from "react";
import "./crew.css";

export const CREW = {
  commander: { name: "Commander Zorvax", asset: "commander-zorvax", color: "#f7b0fc" },
  scientist: { name: "Dr. Nyxen", asset: "dr-nyxen", color: "#8ce8f5" },
  cadet: { name: "Cadet B", asset: "alien-student", color: "#ffcf75" },
  bogos: { name: "Bogos Binted", asset: "bogos-binted", color: "#a3ebc4" },
} as const;

export type CrewId = keyof typeof CREW;

export function CrewPortrait({ id, className = "", decorative = false }: {
  id: CrewId; className?: string; decorative?: boolean;
}) {
  const { name, color, asset } = CREW[id];
  return (
    <div className={`hc-portrait ${className}`} style={{ "--crew": color } as CSSProperties}>
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img className="hc-crew-image" src={`/homecoming/crew/${asset}.webp`} alt={decorative ? "" : name}
        width={520} height={576} decoding="async" />
    </div>
  );
}

export function CrewLine({ id, children }: { id: CrewId; children: ReactNode }) {
  return (
    <div className="hc-line">
      <CrewPortrait id={id} className="hc-avatar" decorative />
      <div>
        <strong style={{ color: CREW[id].color }}>{CREW[id].name}</strong>
        <p>{children}</p>
      </div>
    </div>
  );
}
