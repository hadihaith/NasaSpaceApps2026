"use client";

import { GUESS_LABEL, VERDICT_LABEL, type RoundResult, type Verdict } from "@/lib/game";
import { StarIcon, VerdictIcon } from "./icons";

const VERDICT_COLOR: Record<Verdict, string> = {
  "spot-on": "var(--good)",
  close: "var(--close)",
  miss: "var(--miss)",
};

/** How the player's call compared with the evidence, and what it earned. */
export function Scorecard({
  result,
  places,
}: {
  result: RoundResult;
  places: { letter: string; label: string }[];
}) {
  const comparing = places.length > 1;
  return (
    <section aria-labelledby="score-heading" className="card relative p-5">
      <div className="flex items-start justify-between gap-3">
        <h3 id="score-heading" className="display text-2xl">
          Your call
        </h3>
        <span className="stamp stamp-in" style={{ color: VERDICT_COLOR[result.overall] }}>
          {VERDICT_LABEL[result.overall]}
        </span>
      </div>

      <ul className="mt-4 space-y-3">
        {result.judgements.map((j, i) => (
          <li key={places[i].letter} className="flex items-start gap-3">
            <span
              className="mt-0.5 grid h-7 w-7 shrink-0 place-items-center rounded-full border-2 border-edge"
              style={{ color: VERDICT_COLOR[j.verdict] }}
            >
              <VerdictIcon verdict={j.verdict} className="h-4 w-4" />
              <span className="sr-only">{VERDICT_LABEL[j.verdict]}</span>
            </span>
            <span>
              {comparing && <span className="font-bold">{places[i].label}: </span>}
              You said <span className="font-bold">{GUESS_LABEL[j.guess].toLowerCase()}</span>. The
              evidence says <span className="font-bold">{j.answer.toLowerCase()}</span>.
            </span>
          </li>
        ))}
      </ul>

      <dl className="tabular mt-5 space-y-1 border-t-2 border-dashed border-line-strong pt-3 text-sm">
        {result.lines.map((line, i) => (
          <div key={i} className="flex justify-between gap-4">
            <dt className="text-ink-2">{line.label}</dt>
            <dd className="font-bold">+{line.xp} XP</dd>
          </div>
        ))}
      </dl>

      <div className="mt-4 flex items-center justify-between gap-3">
        {result.stars !== null ? (
          <span className="flex items-center gap-1" aria-label={`${result.stars} of 3 stars`}>
            {[1, 2, 3].map((i) => (
              <StarIcon key={i} filled={i <= result.stars!} className="h-7 w-7" />
            ))}
          </span>
        ) : (
          <span />
        )}
        <span className="pop-in display rounded-full border-[2.5px] border-edge bg-gold px-4 py-1 text-2xl text-[#10242e]">
          +{result.xpGained} XP
        </span>
      </div>
    </section>
  );
}
