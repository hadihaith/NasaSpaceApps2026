"use client";

import { useState } from "react";
import { THEMES, VARIABLES, VARIABLES_BY_ID, type ThemeId } from "@/lib/catalog";

/** Step 1: browse the variables by theme and read what each one measures. */
export function VariableStep({ variableId, onVariable }: { variableId: string; onVariable: (id: string) => void }) {
  const selected = VARIABLES_BY_ID[variableId];
  const [theme, setTheme] = useState<ThemeId>(selected.theme);
  const inTheme = VARIABLES.filter((v) => v.theme === theme);
  const activeTheme = THEMES.find((t) => t.id === theme)!;

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,6fr)_minmax(0,5fr)]">
      <div className="min-w-0">
        <p className="text-ink-2">
          NASA POWER offers {VARIABLES.length} variables here, in {THEMES.length} themes. Start with a question.
        </p>
        <div role="tablist" aria-label="Themes" className="mt-3 flex flex-wrap gap-2">
          {THEMES.map((t) => (
            <button
              key={t.id}
              type="button"
              role="tab"
              aria-selected={theme === t.id}
              onClick={() => setTheme(t.id)}
              className="tile px-4 py-2 text-sm font-bold"
            >
              {t.label}
            </button>
          ))}
        </div>
        <p key={theme} className="ch-rise display mt-5 text-2xl">
          {activeTheme.question}
        </p>
        <div key={`grid-${theme}`} className="mt-3 grid gap-3 sm:grid-cols-2">
          {inTheme.map((v, i) => (
            <button
              key={v.id}
              type="button"
              aria-pressed={v.id === variableId}
              onClick={() => onVariable(v.id)}
              className="tile ch-pop flex min-h-[84px] flex-col items-start p-3 text-left"
              style={{ animationDelay: `${i * 70}ms` }}
            >
              <span className="font-bold">{v.name}</span>
              <span className="mt-1 text-sm text-ink-2">{v.why}</span>
            </button>
          ))}
        </div>
      </div>

      <aside key={variableId} className="ch-rise min-w-0 rounded-2xl border-[2.5px] border-edge bg-surface-2 p-5" aria-live="polite">
        <p className="text-sm font-bold uppercase tracking-wider text-ink-2">Your variable</p>
        <h3 className="display mt-1 text-3xl">{selected.name}</h3>
        <p className="mt-1 font-mono text-sm text-ink-2">
          NASA POWER id {selected.id}
          {selected.unit ? ` · ${selected.unit}` : selected.unitNote ? ` · ${selected.unitNote}` : ""}
        </p>
        <dl className="mt-4 space-y-3">
          <div>
            <dt className="text-sm font-bold text-ink-2">What it measures</dt>
            <dd>{selected.definition}</dd>
          </div>
          <div>
            <dt className="text-sm font-bold text-ink-2">Why look for a trend</dt>
            <dd>{selected.why}</dd>
          </div>
          <div>
            <dt className="text-sm font-bold text-ink-2">Where the numbers come from</dt>
            <dd>
              {selected.origin}, monthly from {selected.startYear}
            </dd>
          </div>
        </dl>
        {selected.caveat && (
          <p className="mt-4 rounded-xl border-2 border-close p-3 text-sm">
            <strong className="text-close">Watch out: </strong>
            {selected.caveat}
          </p>
        )}
      </aside>
    </div>
  );
}
