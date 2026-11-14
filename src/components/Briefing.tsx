"use client";

import { THEMES, type DemoCase, type Variable } from "@/lib/catalog";
import { ThemeIcon } from "./icons";

/** The setup of a board case: the story and the evidence to look at. */
export function Briefing({
  demo,
  variable,
  onCustomize,
}: {
  demo: DemoCase;
  variable: Variable;
  onCustomize: () => void;
}) {
  const theme = THEMES.find((t) => t.id === variable.theme)!;
  return (
    <section aria-labelledby="briefing-heading" className="card flex flex-col p-5 sm:p-6">
      <div className="flex flex-wrap gap-2">
        <span className="chip">
          <ThemeIcon theme={theme.id} className="h-4 w-4" />
          {theme.label}
        </span>
        <span className="chip">{demo.difficulty}</span>
      </div>
      <h3 id="briefing-heading" className="display mt-4 text-3xl sm:text-4xl">
        {demo.question}
      </h3>
      <p className="mt-3 max-w-[60ch] text-lg text-ink-2">{demo.briefing}</p>

      <div className="mt-5 rounded-xl border-2 border-dashed border-line-strong bg-surface-2 p-4">
        <p className="text-sm font-bold text-ink-2">The evidence</p>
        <p className="display mt-1 text-xl">{variable.name}</p>
        <p className="mt-1 text-ink-2">{variable.definition}</p>
      </div>

      <button
        type="button"
        onClick={onCustomize}
        className="mt-5 self-start text-ink-2 underline underline-offset-4 hover:text-ink"
      >
        Change the evidence yourself
      </button>
    </section>
  );
}
