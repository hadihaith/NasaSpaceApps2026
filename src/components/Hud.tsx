"use client";

import { BADGES, rankFor, type Progress } from "@/lib/game";
import { FlameIcon, LogoIcon, MedalIcon } from "./icons";

/** The always-visible player bar: rank, XP toward the next rank, streak, badges. */
export function Hud({ progress, onOpenBadges }: { progress: Progress; onOpenBadges: () => void }) {
  const { rank, next, fraction } = rankFor(progress.xp);
  return (
    <header className="sticky top-0 z-30 -mx-4 border-b-[2.5px] border-edge bg-paper/90 px-4 py-2.5 backdrop-blur sm:-mx-6 sm:px-6">
      <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
        <a href="#top" className="flex items-center gap-2">
          <LogoIcon className="h-9 w-9" />
          <span className="display text-2xl sm:text-3xl">Trend Detective</span>
        </a>

        <div className="flex items-center gap-3">
          <div className="w-44 sm:w-52">
            <div className="flex items-baseline justify-between gap-2 text-xs whitespace-nowrap sm:text-sm">
              <span className="font-bold">{rank.name}</span>
              <span className="tabular text-ink-2">
                {progress.xp}
                {next ? `/${next.xp}` : ""} XP
              </span>
            </div>
            <div
              role="progressbar"
              aria-label={next ? `XP toward ${next.name}` : "Top rank reached"}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={Math.round(fraction * 100)}
              className="mt-1 h-3 overflow-hidden rounded-full border-2 border-edge bg-surface"
            >
              <div
                className="h-full bg-gold transition-[width] duration-700"
                style={{ width: `${Math.max(fraction * 100, 2)}%` }}
              />
            </div>
          </div>

          <span
            className="chip"
            title="Spot-on calls in a row"
            aria-label={`${progress.streak} spot-on calls in a row`}
          >
            <FlameIcon className={`h-4 w-4 ${progress.streak > 0 ? "text-[var(--series-b)]" : "text-muted"}`} />
            <span className="tabular">{progress.streak}</span>
          </span>

          <button type="button" onClick={onOpenBadges} className="btn px-3 py-1 text-sm">
            <MedalIcon className="h-4 w-4" />
            <span className="tabular">
              {progress.badges.length}/{BADGES.length}
            </span>
            <span className="sr-only">badges. Open the badge shelf</span>
          </button>
        </div>
      </div>
    </header>
  );
}
