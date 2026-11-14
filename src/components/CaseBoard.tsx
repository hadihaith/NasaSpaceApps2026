"use client";

import { DEMO_CASES, THEMES, VARIABLES_BY_ID, type DemoCase } from "@/lib/catalog";
import { StarIcon, ThemeIcon } from "./icons";

interface Props {
  activeCaseId: string | null;
  caseStars: Record<string, number>;
  onOpen: (c: DemoCase) => void;
  onFreePlay: () => void;
}

export function CaseBoard({ activeCaseId, caseStars, onOpen, onFreePlay }: Props) {
  const solved = DEMO_CASES.filter((c) => caseStars[c.id]).length;

  return (
    <section aria-labelledby="board-heading" className="pt-8">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 id="board-heading" className="display text-4xl sm:text-5xl">
            Pick a case
          </h1>
          <p className="mt-2 max-w-[62ch] text-lg text-ink-2">
            Every case is a question about our planet. Make your call, then let four decades of
            NASA data decide whether you were right.
          </p>
        </div>
        <span className="chip tabular">
          {solved}/{DEMO_CASES.length} cases solved
        </span>
      </div>

      <ul className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {DEMO_CASES.map((c) => {
          const variable = VARIABLES_BY_ID[c.variableId];
          const theme = THEMES.find((t) => t.id === variable.theme)!;
          const stars = caseStars[c.id] ?? 0;
          return (
            <li key={c.id}>
              <button
                type="button"
                aria-pressed={c.id === activeCaseId}
                onClick={() => onOpen(c)}
                className="tile flex h-full w-full flex-col p-4 text-left"
              >
                <span className="flex items-center justify-between gap-2">
                  <span className="chip">
                    <ThemeIcon theme={theme.id} className="h-4 w-4" />
                    {theme.label}
                  </span>
                  <span className="text-sm font-bold text-ink-2">{c.difficulty}</span>
                </span>
                <span className="display mt-3 text-2xl">{c.question}</span>
                <span className="mt-1 text-ink-2">
                  {variable.name}, {c.compare ? `${c.place} and ${c.compare.place}` : c.place}
                </span>
                <span className="mt-auto flex items-center gap-1 pt-4">
                  {stars > 0 ? (
                    <>
                      {[1, 2, 3].map((i) => (
                        <StarIcon key={i} filled={i <= stars} className="h-6 w-6" />
                      ))}
                      <span className="sr-only">Solved with {stars} of 3 stars</span>
                    </>
                  ) : (
                    <span className="text-sm font-bold text-ink-2">Not solved yet</span>
                  )}
                </span>
              </button>
            </li>
          );
        })}
        <li>
          <button
            type="button"
            aria-pressed={activeCaseId === null}
            onClick={onFreePlay}
            className="tile tile-dashed flex h-full min-h-40 w-full flex-col justify-center p-4 text-left"
          >
            <span className="display text-2xl">Open your own case</span>
            <span className="mt-1 text-ink-2">
              Any of 18 measurements, anywhere on Earth. Every call still earns XP.
            </span>
          </button>
        </li>
      </ul>
    </section>
  );
}
