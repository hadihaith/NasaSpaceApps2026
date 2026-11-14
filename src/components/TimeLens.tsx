"use client";

import { MODES, MONTH_NAMES, type Mode } from "@/lib/series";

/** After the reveal: read the same record three ways. */
export function TimeLens({
  mode,
  onMode,
  month,
  onMonth,
  scored,
}: {
  mode: Mode;
  onMode: (mode: Mode) => void;
  month: number;
  onMonth: (month: number) => void;
  scored: boolean;
}) {
  const current = MODES.find((m) => m.id === mode)!;
  return (
    <div className="mt-10">
      <h3 className="display text-2xl">Change the lens</h3>
      <p className="mt-1 max-w-[62ch] text-ink-2">
        The same record, read three ways.
        {scored ? " Your score is already locked in, so experiment freely." : ""}
      </p>
      <div role="radiogroup" aria-label="How time is read" className="mt-3 grid gap-2.5 sm:grid-cols-3">
        {MODES.map((m) => (
          <button
            key={m.id}
            type="button"
            role="radio"
            aria-checked={mode === m.id}
            onClick={() => onMode(m.id)}
            className="tile px-3.5 py-2.5 text-left font-bold"
          >
            {m.label}
          </button>
        ))}
      </div>
      <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2">
        <p className="max-w-[62ch] text-ink-2">{current.explain}</p>
        {mode === "month" && (
          <label className="flex items-center gap-2">
            <span className="text-ink-2">Month</span>
            <select
              value={month}
              onChange={(e) => onMonth(Number(e.target.value))}
              className="rounded-lg border-2 border-edge bg-surface px-2 py-1 font-bold"
            >
              {MONTH_NAMES.map((name, i) => (
                <option key={name} value={i}>
                  {name}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>
    </div>
  );
}
