"use client";

import type { Ref } from "react";
import { BADGES, rankFor, type Progress } from "@/lib/game";
import { LockIcon, MedalIcon } from "./icons";

/** A modal listing every badge, earned or not, with the player's stats. */
export function BadgeShelf({
  ref,
  progress,
  onReset,
}: {
  ref: Ref<HTMLDialogElement>;
  progress: Progress;
  onReset: () => void;
}) {
  const { rank } = rankFor(progress.xp);
  const accuracy = progress.calls > 0 ? Math.round((progress.spotOn / progress.calls) * 100) : 0;

  return (
    <dialog
      ref={ref}
      aria-labelledby="shelf-heading"
      className="card m-auto max-h-[calc(100dvh-32px)] w-[min(680px,calc(100vw-32px))] overflow-y-auto p-0 text-ink"
    >
      <div className="p-5 sm:p-7">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 id="shelf-heading" className="display text-3xl sm:text-4xl">
              Badge shelf
            </h2>
            <p className="mt-1 text-ink-2">
              {rank.name}, {progress.xp} XP
            </p>
          </div>
          <form method="dialog">
            <button className="btn px-4 py-1.5 text-sm">Close</button>
          </form>
        </div>

        <div className="tabular mt-4 flex flex-wrap gap-2">
          <span className="chip">{progress.calls} calls made</span>
          <span className="chip">{accuracy}% spot on</span>
          <span className="chip">Best streak {progress.bestStreak}</span>
        </div>

        <ul className="mt-6 grid gap-3 sm:grid-cols-2">
          {BADGES.map((badge) => {
            const earned = progress.badges.includes(badge.id);
            return (
              <li
                key={badge.id}
                className={`flex gap-3 rounded-2xl border-[2.5px] p-3.5 ${
                  earned ? "border-edge bg-gold text-[#10242e]" : "border-dashed border-line-strong"
                }`}
              >
                {earned ? (
                  <MedalIcon className="h-8 w-8 shrink-0" />
                ) : (
                  <LockIcon className="h-8 w-8 shrink-0 text-muted" />
                )}
                <div>
                  <p className="font-bold">
                    {badge.name}
                    <span className="sr-only">{earned ? ", earned" : ", not earned yet"}</span>
                  </p>
                  <p className={`text-sm ${earned ? "text-[#3b4f57]" : "text-ink-2"}`}>
                    {badge.description}
                  </p>
                </div>
              </li>
            );
          })}
        </ul>

        <button
          type="button"
          onClick={() => {
            if (window.confirm("Reset your XP, stars and badges? This cannot be undone.")) onReset();
          }}
          className="mt-7 text-sm text-ink-2 underline underline-offset-4 hover:text-ink"
        >
          Reset all progress
        </button>
      </div>
    </dialog>
  );
}
