/**
 * The game layer: the player predicts a trend before seeing the evidence, then
 * earns XP, keeps a streak, climbs detective ranks and collects badges.
 *
 * Pure functions only. The browser storage lives in useProgress.ts.
 */
import type { ThemeId } from "./catalog";
import type { TrendResult } from "./stats/trend";

export type Guess = "rising" | "falling" | "none";
export type Verdict = "spot-on" | "close" | "miss";

export const GUESSES: { id: Guess; label: string }[] = [
  { id: "rising", label: "Rising" },
  { id: "falling", label: "Falling" },
  { id: "none", label: "No clear trend" },
];

export const GUESS_LABEL: Record<Guess, string> = {
  rising: "Rising",
  falling: "Falling",
  none: "No clear trend",
};

export const VERDICT_LABEL: Record<Verdict, string> = {
  "spot-on": "Spot on",
  close: "Close call",
  miss: "Missed it",
};

export const XP_FOR: Record<Verdict, number> = { "spot-on": 100, close: 50, miss: 10 };
/** Extra XP the first time a case on the board is solved */
export const CASE_BONUS = 50;
/** Each spot-on call in a row adds this much more, up to the cap */
export const STREAK_STEP = 10;
export const STREAK_CAP = 50;

type TrendShape = Pick<TrendResult, "direction" | "strength">;

export interface Judgement {
  guess: Guess;
  verdict: Verdict;
  /** What the evidence says, in the same words as the choices */
  answer: string;
  xp: number;
}

export function answerFor(trend: TrendShape): string {
  if (trend.direction === "flat" || trend.strength === "not-significant") return "No clear trend";
  const direction = trend.direction === "rising" ? "Rising" : "Falling";
  return trend.strength === "significant" ? direction : `Maybe ${direction.toLowerCase()}`;
}

/**
 * Score one call against the evidence.
 *  - Strong evidence: only the right direction is spot on.
 *  - No clear trend: "no clear trend" is spot on; the way it leans is close.
 *  - Mixed evidence: the lean and "no clear trend" are both close.
 */
export function judge(guess: Guess, trend: TrendShape): Judgement {
  const lean: Guess = trend.direction === "flat" ? "none" : trend.direction;
  let verdict: Verdict;
  if (trend.direction !== "flat" && trend.strength === "significant") {
    verdict = guess === lean ? "spot-on" : "miss";
  } else if (trend.direction !== "flat" && trend.strength === "borderline") {
    verdict = guess === lean || guess === "none" ? "close" : "miss";
  } else {
    verdict = guess === "none" ? "spot-on" : guess === lean ? "close" : "miss";
  }
  return { guess, verdict, answer: answerFor(trend), xp: XP_FOR[verdict] };
}

/* ---------- Ranks ---------- */

export interface Rank {
  name: string;
  xp: number;
}

export const RANKS: Rank[] = [
  { name: "Rookie", xp: 0 },
  { name: "Field Analyst", xp: 200 },
  { name: "Detective", xp: 500 },
  { name: "Senior Detective", xp: 1000 },
  { name: "Chief Scientist", xp: 2000 },
];

export function rankFor(xp: number): { rank: Rank; next: Rank | null; fraction: number } {
  let i = RANKS.length - 1;
  while (i > 0 && xp < RANKS[i].xp) i--;
  const rank = RANKS[i];
  const next = RANKS[i + 1] ?? null;
  const fraction = next ? (xp - rank.xp) / (next.xp - rank.xp) : 1;
  return { rank, next, fraction };
}

/* ---------- Badges ---------- */

export type BadgeId =
  | "first-call"
  | "sharp-eye"
  | "null-hunter"
  | "opposite-ends"
  | "all-rounder"
  | "globetrotter"
  | "case-closed";

export interface Badge {
  id: BadgeId;
  name: string;
  description: string;
}

export const BADGES: Badge[] = [
  { id: "first-call", name: "First call", description: "Make a prediction and open the evidence." },
  { id: "sharp-eye", name: "Sharp eye", description: "Make three spot-on calls in a row." },
  {
    id: "null-hunter",
    name: "Null hunter",
    description: "Correctly call that there is no clear trend. Knowing when nothing is happening is a skill too.",
  },
  {
    id: "opposite-ends",
    name: "Opposite ends",
    description: "Find one measurement rising in one place and falling in another, both with strong evidence.",
  },
  { id: "all-rounder", name: "All-rounder", description: "Investigate heat, water, air, and sun and sky." },
  {
    id: "globetrotter",
    name: "Globetrotter",
    description: "Investigate places in five different 10-degree squares of the map.",
  },
  { id: "case-closed", name: "Case closed", description: "Solve every case on the board." },
];

/* ---------- Progress ---------- */

export interface Progress {
  xp: number;
  /** Spot-on calls in a row */
  streak: number;
  bestStreak: number;
  calls: number;
  spotOn: number;
  /** Best stars (1 to 3) earned on each board case */
  caseStars: Record<string, number>;
  badges: BadgeId[];
  themes: ThemeId[];
  /** 10-degree map squares visited, as "row,col" */
  cells: string[];
}

export const NEW_PROGRESS: Progress = {
  xp: 0,
  streak: 0,
  bestStreak: 0,
  calls: 0,
  spotOn: 0,
  caseStars: {},
  badges: [],
  themes: [],
  cells: [],
};

export interface Round {
  /** One guess per place, in place order (A, then B) */
  guesses: Guess[];
  trends: TrendShape[];
  /** The board case being played, or null for the player's own case */
  caseId: string | null;
  totalCases: number;
  theme: ThemeId;
  spots: { lat: number; lon: number }[];
}

export interface ScoreLine {
  label: string;
  xp: number;
}

export interface RoundResult {
  progress: Progress;
  judgements: Judgement[];
  overall: Verdict;
  lines: ScoreLine[];
  xpGained: number;
  /** Stars for a board case; null for the player's own case */
  stars: number | null;
  newBadges: Badge[];
  promotedTo: Rank | null;
}

const LETTERS = ["A", "B"];

export function playRound(before: Progress, round: Round): RoundResult {
  const judgements = round.guesses.map((guess, i) => judge(guess, round.trends[i]));
  const lines: ScoreLine[] = [];
  let { streak, bestStreak, spotOn } = before;

  judgements.forEach((j, i) => {
    const who = judgements.length > 1 ? ` on ${LETTERS[i]}` : "";
    lines.push({ label: `${VERDICT_LABEL[j.verdict]}${who}`, xp: j.xp });
    if (j.verdict === "spot-on") {
      streak += 1;
      spotOn += 1;
      bestStreak = Math.max(bestStreak, streak);
      const bonus = Math.min((streak - 1) * STREAK_STEP, STREAK_CAP);
      if (bonus > 0) lines.push({ label: `${streak} in a row`, xp: bonus });
    } else {
      streak = 0;
    }
  });

  const overall: Verdict = judgements.every((j) => j.verdict === "spot-on")
    ? "spot-on"
    : judgements.every((j) => j.verdict === "miss")
      ? "miss"
      : "close";

  const caseStars = { ...before.caseStars };
  let stars: number | null = null;
  if (round.caseId) {
    stars = overall === "spot-on" ? 3 : overall === "close" ? 2 : 1;
    if (!(round.caseId in before.caseStars)) {
      lines.push({ label: "First time closing this case", xp: CASE_BONUS });
    }
    caseStars[round.caseId] = Math.max(stars, before.caseStars[round.caseId] ?? 0);
  }

  const xpGained = lines.reduce((sum, line) => sum + line.xp, 0);
  const cells = new Set(before.cells);
  for (const s of round.spots) cells.add(`${Math.floor(s.lat / 10)},${Math.floor(s.lon / 10)}`);
  const themes = before.themes.includes(round.theme) ? before.themes : [...before.themes, round.theme];

  const progress: Progress = {
    xp: before.xp + xpGained,
    streak,
    bestStreak,
    calls: before.calls + judgements.length,
    spotOn,
    caseStars,
    badges: before.badges,
    themes,
    cells: [...cells],
  };

  const [a, b] = round.trends;
  const opposite =
    round.trends.length === 2 &&
    round.trends.every((t) => t.strength === "significant" && t.direction !== "flat") &&
    a.direction !== b.direction;

  const earned: Record<BadgeId, boolean> = {
    "first-call": progress.calls > 0,
    "sharp-eye": progress.bestStreak >= 3,
    "null-hunter": judgements.some((j) => j.verdict === "spot-on" && j.guess === "none"),
    "opposite-ends": opposite,
    "all-rounder": progress.themes.length >= 4,
    globetrotter: progress.cells.length >= 5,
    "case-closed": Object.keys(progress.caseStars).length >= round.totalCases,
  };
  const newBadges = BADGES.filter((badge) => earned[badge.id] && !before.badges.includes(badge.id));
  progress.badges = [...before.badges, ...newBadges.map((badge) => badge.id)];

  const rankBefore = rankFor(before.xp).rank;
  const rankAfter = rankFor(progress.xp).rank;

  return {
    progress,
    judgements,
    overall,
    lines,
    xpGained,
    stars,
    newBadges,
    promotedTo: rankAfter !== rankBefore ? rankAfter : null,
  };
}
