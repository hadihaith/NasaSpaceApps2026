"use client";

import { GUESSES, XP_FOR, type Guess } from "@/lib/game";
import { GuessIcon } from "./icons";

interface Props {
  prompt: string;
  places: { letter: string; label: string }[];
  guesses: (Guess | null)[];
  onGuess: (index: number, guess: Guess) => void;
  /** The data has arrived, so the evidence can be opened */
  ready: boolean;
  onReveal: () => void;
  onSkip: () => void;
}

const LETTER_COLOR: Record<string, string> = { A: "var(--series-a)", B: "var(--series-b)" };

/** The player commits to a call before the evidence is shown. */
export function PredictionPanel({ prompt, places, guesses, onGuess, ready, onReveal, onSkip }: Props) {
  const comparing = places.length > 1;
  const complete = guesses.every((g) => g !== null);

  return (
    <section aria-labelledby="call-heading" className="card p-5 sm:p-7">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <h3 id="call-heading" className="display text-3xl sm:text-4xl">
          Make your call
        </h3>
        <div className="flex flex-wrap gap-2 text-ink-2">
          <span className="chip">Spot on +{XP_FOR["spot-on"]}</span>
          <span className="chip">Close +{XP_FOR.close}</span>
          <span className="chip">Miss +{XP_FOR.miss}</span>
        </div>
      </div>
      <p className="mt-2 max-w-[62ch] text-lg text-ink-2">{prompt}</p>

      <div className="mt-6 space-y-6">
        {places.map((place, i) => (
          <div key={place.letter} role="radiogroup" aria-label={`Your call for ${place.label}`}>
            {comparing && (
              <p className="mb-2.5 flex items-center gap-2 font-bold">
                <span
                  className="grid h-7 w-7 place-items-center rounded-full text-sm text-white"
                  style={{ background: LETTER_COLOR[place.letter] }}
                  aria-hidden
                >
                  {place.letter}
                </span>
                {place.label}
              </p>
            )}
            <div className="grid gap-3 sm:grid-cols-3">
              {GUESSES.map((g) => (
                <button
                  key={g.id}
                  type="button"
                  role="radio"
                  aria-checked={guesses[i] === g.id}
                  onClick={() => onGuess(i, g.id)}
                  className="tile flex items-center gap-3 px-4 py-3.5 text-left"
                >
                  <GuessIcon guess={g.id} className="h-9 w-9 shrink-0" />
                  <span className="display text-2xl">{g.label}</span>
                </button>
              ))}
            </div>
          </div>
        ))}
      </div>

      <div className="mt-7 flex flex-wrap items-center gap-x-5 gap-y-3">
        <button
          type="button"
          onClick={onReveal}
          disabled={!complete || !ready}
          className="btn btn-primary px-6 py-3 text-lg"
        >
          {ready ? "Open the evidence" : "Gathering evidence from NASA…"}
        </button>
        {ready && !complete && (
          <span className="text-ink-2">
            {comparing ? "Make a call for both places first." : "Make a call first."}
          </span>
        )}
        <button
          type="button"
          onClick={onSkip}
          disabled={!ready}
          className="text-ink-2 underline underline-offset-4 hover:text-ink disabled:opacity-50"
        >
          Skip the guess and show me
        </button>
      </div>
    </section>
  );
}
