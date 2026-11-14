"use client";

import { THEMES, VARIABLES, VARIABLES_BY_ID, type ThemeId } from "@/lib/catalog";
import { ThemeIcon } from "./icons";

interface Props {
  theme: ThemeId;
  onTheme: (theme: ThemeId) => void;
  variableId: string;
  onVariable: (id: string) => void;
}

/** Choosing what to measure in the player's own case. */
export function EvidencePicker({ theme, onTheme, variableId, onVariable }: Props) {
  const selected = VARIABLES_BY_ID[variableId];
  return (
    <section aria-labelledby="evidence-heading" className="card p-5 sm:p-6">
      <h3 id="evidence-heading" className="display text-3xl">
        Choose the evidence
      </h3>

      <div role="tablist" aria-label="Topic" className="mt-4 grid grid-cols-4 gap-2">
        {THEMES.map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            aria-selected={t.id === theme}
            onClick={() => onTheme(t.id)}
            className="tile flex flex-col items-center gap-1 px-1 py-2.5 text-sm font-bold"
          >
            <ThemeIcon theme={t.id} className="h-6 w-6" />
            {t.label}
          </button>
        ))}
      </div>

      <p className="mt-5 font-semibold">{THEMES.find((t) => t.id === theme)?.question}</p>
      <div role="radiogroup" aria-label="Measurement" className="mt-3 grid gap-2.5 sm:grid-cols-2">
        {VARIABLES.filter((v) => v.theme === theme).map((v) => (
          <button
            key={v.id}
            type="button"
            role="radio"
            aria-checked={v.id === variableId}
            onClick={() => onVariable(v.id)}
            className="tile px-3 py-2 text-left"
          >
            <span className="block font-bold">{v.name}</span>
            <span className="block text-sm text-ink-2">since {v.startYear}</span>
          </button>
        ))}
      </div>

      {selected.theme === theme && (
        <div className="mt-5 rounded-xl border-2 border-dashed border-line-strong bg-surface-2 p-4">
          <p className="display text-xl">{selected.name}</p>
          <p className="mt-1 text-ink-2">
            {selected.definition} {selected.why}
          </p>
        </div>
      )}
    </section>
  );
}
