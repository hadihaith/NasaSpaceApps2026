import { describe, expect, it } from "vitest";
import {
  CASE_BONUS,
  judge,
  NEW_PROGRESS,
  playRound,
  rankFor,
  type Guess,
  type Progress,
  type Round,
} from "./game";
import type { TrendResult } from "./stats/trend";

type T = Pick<TrendResult, "direction" | "strength">;
const strongUp: T = { direction: "rising", strength: "significant" };
const strongDown: T = { direction: "falling", strength: "significant" };
const maybeUp: T = { direction: "rising", strength: "borderline" };
const noneUp: T = { direction: "rising", strength: "not-significant" };

const round = (guesses: Guess[], trends: T[], extra: Partial<Round> = {}): Round => ({
  guesses,
  trends,
  caseId: null,
  totalCases: 6,
  theme: "heat",
  spots: guesses.map(() => ({ lat: 10, lon: 10 })),
  ...extra,
});

describe("judge", () => {
  it("only rewards the right direction when the evidence is strong", () => {
    expect(judge("rising", strongUp).verdict).toBe("spot-on");
    expect(judge("none", strongUp).verdict).toBe("miss");
    expect(judge("falling", strongUp).verdict).toBe("miss");
  });

  it("rewards calling no trend when there is none, and half rewards its lean", () => {
    expect(judge("none", noneUp).verdict).toBe("spot-on");
    expect(judge("rising", noneUp).verdict).toBe("close");
    expect(judge("falling", noneUp).verdict).toBe("miss");
  });

  it("treats mixed evidence as close either way", () => {
    expect(judge("rising", maybeUp).verdict).toBe("close");
    expect(judge("none", maybeUp).verdict).toBe("close");
    expect(judge("falling", maybeUp).verdict).toBe("miss");
    expect(judge("rising", maybeUp).answer).toBe("Maybe rising");
  });
});

describe("rankFor", () => {
  it("finds the rank and the progress toward the next one", () => {
    expect(rankFor(0).rank.name).toBe("Rookie");
    expect(rankFor(199).rank.name).toBe("Rookie");
    expect(rankFor(200).rank.name).toBe("Field Analyst");
    expect(rankFor(350).fraction).toBeCloseTo(0.5, 10);
    expect(rankFor(5000).next).toBeNull();
  });
});

describe("playRound", () => {
  it("scores a first spot-on call and awards the first badge", () => {
    const r = playRound(NEW_PROGRESS, round(["rising"], [strongUp]));
    expect(r.overall).toBe("spot-on");
    expect(r.xpGained).toBe(100);
    expect(r.progress.streak).toBe(1);
    expect(r.newBadges.map((b) => b.id)).toEqual(["first-call"]);
    expect(r.stars).toBeNull();
  });

  it("grows a streak bonus and resets it on a miss", () => {
    let p: Progress = NEW_PROGRESS;
    const gains: number[] = [];
    for (let i = 0; i < 3; i++) {
      const r = playRound(p, round(["rising"], [strongUp]));
      gains.push(r.xpGained);
      p = r.progress;
    }
    expect(gains).toEqual([100, 110, 120]);
    expect(p.badges).toContain("sharp-eye");
    const miss = playRound(p, round(["falling"], [strongUp]));
    expect(miss.progress.streak).toBe(0);
    expect(miss.progress.bestStreak).toBe(3);
  });

  it("gives a case bonus once and keeps the best stars", () => {
    const first = playRound(NEW_PROGRESS, round(["falling"], [strongUp], { caseId: "arctic" }));
    expect(first.stars).toBe(1);
    expect(first.xpGained).toBe(10 + CASE_BONUS);
    const second = playRound(first.progress, round(["rising"], [strongUp], { caseId: "arctic" }));
    expect(second.stars).toBe(3);
    expect(second.xpGained).toBe(100);
    expect(second.progress.caseStars.arctic).toBe(3);
    const third = playRound(second.progress, round(["falling"], [strongUp], { caseId: "arctic" }));
    expect(third.progress.caseStars.arctic).toBe(3);
  });

  it("scores both places in a comparison and spots opposite trends", () => {
    const r = playRound(NEW_PROGRESS, round(["rising", "rising"], [strongUp, strongDown]));
    expect(r.judgements.map((j) => j.verdict)).toEqual(["spot-on", "miss"]);
    expect(r.overall).toBe("close");
    expect(r.newBadges.map((b) => b.id)).toContain("opposite-ends");
  });

  it("awards the null hunter for a correct call of no trend", () => {
    const r = playRound(NEW_PROGRESS, round(["none"], [noneUp]));
    expect(r.newBadges.map((b) => b.id)).toContain("null-hunter");
  });

  it("reports a promotion when XP crosses a rank", () => {
    const p = { ...NEW_PROGRESS, xp: 150 };
    expect(playRound(p, round(["rising"], [strongUp])).promotedTo?.name).toBe("Field Analyst");
  });

  it("does not award a badge twice", () => {
    const first = playRound(NEW_PROGRESS, round(["none"], [noneUp]));
    const again = playRound(first.progress, round(["none"], [noneUp]));
    expect(again.newBadges).toEqual([]);
  });
});
