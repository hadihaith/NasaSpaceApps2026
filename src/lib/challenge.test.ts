import { describe, expect, it } from "vitest";
import {
  chanceOdds,
  challengeStats,
  compareRegions,
  countPairs,
  evidenceLevel,
  fillGaps,
  normalDensity,
  seasonalCycle,
  tDensity,
} from "./challenge";
import { mannKendall } from "./stats/trend";

/** A small deterministic wobble, so tests do not depend on a random seed. */
const wobble = (i: number) => Math.sin(i * 2.3) * 0.4 + Math.cos(i * 0.7) * 0.25;

describe("challengeStats", () => {
  it("calls a steady rise significant on every test", () => {
    const points = Array.from({ length: 40 }, (_, i) => ({ t: 1985 + i, v: 10 + 0.05 * i + wobble(i) }));
    const stats = challengeStats(points)!;
    expect(stats.direction).toBe("rising");
    expect(stats.verdict).toBe("significant");
    expect(stats.ols.slope).toBeGreaterThan(0.03);
    expect(stats.ols.ci[0]).toBeGreaterThan(0);
    expect(stats.change).toBeCloseTo(stats.sen.slope * 39, 10);
  });

  it("finds no trend in a series that only wobbles", () => {
    const points = Array.from({ length: 40 }, (_, i) => ({ t: 1985 + i, v: 3 + wobble(i) }));
    const stats = challengeStats(points)!;
    expect(stats.verdict).toBe("none");
    expect(stats.ols.ci[0]).toBeLessThan(0);
    expect(stats.ols.ci[1]).toBeGreaterThan(0);
  });

  it("returns null for a record that is too short", () => {
    expect(challengeStats([{ t: 2000, v: 1 }, { t: 2001, v: 2 }])).toBeNull();
  });

  it("puts the fitted line inside its confidence band, narrowest at the middle", () => {
    const points = Array.from({ length: 30 }, (_, i) => ({ t: 1990 + i, v: 0.1 * i + wobble(i) }));
    const { band } = challengeStats(points)!;
    for (const b of band) {
      expect(b.lo).toBeLessThan(b.fit);
      expect(b.hi).toBeGreaterThan(b.fit);
    }
    const widths = band.map((b) => b.hi - b.lo);
    expect(widths[20]).toBeLessThan(widths[0]);
    expect(widths[20]).toBeLessThan(widths[40]);
  });
});

describe("countPairs", () => {
  it("matches the Mann-Kendall S statistic", () => {
    const values = [1, 3, 2, 2, 5];
    const pairs = countPairs(values);
    expect(pairs.total).toBe(10);
    expect(pairs.ties).toBe(1);
    expect(pairs.up).toBe(7);
    expect(pairs.down).toBe(2);
    expect(pairs.up - pairs.down).toBe(mannKendall(values).s);
  });
});

describe("densities", () => {
  const integrate = (f: (x: number) => number) => {
    let sum = 0;
    for (let x = -60; x <= 60; x += 0.01) sum += f(x) * 0.01;
    return sum;
  };
  it("integrate to one", () => {
    expect(integrate(normalDensity)).toBeCloseTo(1, 4);
    expect(integrate((x) => tDensity(x, 12))).toBeCloseTo(1, 3);
  });
  it("t approaches the normal for many degrees of freedom", () => {
    expect(tDensity(0.5, 5000)).toBeCloseTo(normalDensity(0.5), 4);
  });
});

describe("seasonalCycle", () => {
  it("averages each calendar month and measures the swing", () => {
    const monthly: (number | null)[] = Array.from({ length: 36 }, (_, i) => (i % 12 === 6 ? 20 : i % 12 === 0 ? -5 : 5));
    monthly[6] = null;
    const { normals, range } = seasonalCycle(monthly);
    expect(normals[6]).toBe(20);
    expect(range).toBe(25);
  });
});

describe("plain-language reading", () => {
  const rising = Array.from({ length: 40 }, (_, i) => ({ t: 1985 + i, v: 10 + 0.05 * i + wobble(i) }));
  const flat = Array.from({ length: 40 }, (_, i) => ({ t: 1985 + i, v: 3 + wobble(i) }));

  it("puts a clear trend at clear evidence or above, and noise below it", () => {
    expect(evidenceLevel(challengeStats(rising)!).level).toBeGreaterThanOrEqual(2);
    expect(evidenceLevel(challengeStats(flat)!).level).toBeLessThan(2);
  });

  it("states odds in words", () => {
    expect(chanceOdds(0.0001)).toBe("less than 1 time in 1,000");
    expect(chanceOdds(0.025)).toBe("about 1 time in 40");
    expect(chanceOdds(0.2)).toBe("about 1 time in 5");
    expect(chanceOdds(0.7)).toBe("about half the time or more");
  });

  it("refuses to test a series that never changes", () => {
    expect(challengeStats(Array.from({ length: 30 }, (_, i) => ({ t: 1990 + i, v: 0 })))).toBeNull();
  });
});

describe("fillGaps", () => {
  it("fills a short gap from the month's normal plus that year's shift", () => {
    // Three years with the same seasonal shape; the middle year runs 2 above normal.
    const shape = [0, 1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1];
    const monthly: (number | null)[] = [...shape, ...shape.map((v) => v + 2), ...shape];
    monthly[12 + 5] = null;
    const { monthly: out, filled, missing } = fillGaps(monthly);
    expect(missing).toBe(1);
    expect(filled).toBe(1);
    // June's normal is 5 (two years). The other months' normals include the warm
    // year (+2/3), so that year runs 4/3 above them: 5 + 4/3.
    expect(out[17]).toBeCloseTo(5 + 4 / 3, 10);
  });

  it("leaves a year with too many gaps alone", () => {
    const monthly: (number | null)[] = Array.from({ length: 24 }, (_, i) => i % 12);
    for (let m = 0; m < 5; m++) monthly[12 + m] = null;
    expect(fillGaps(monthly).filled).toBe(0);
  });
});

describe("compareRegions", () => {
  const up = Array.from({ length: 40 }, (_, i) => ({ t: 1985 + i, v: 0.06 * i + wobble(i) }));
  const down = Array.from({ length: 40 }, (_, i) => ({ t: 1985 + i, v: -0.06 * i + wobble(i + 7) }));
  const flat = Array.from({ length: 40 }, (_, i) => ({ t: 1985 + i, v: wobble(i + 3) }));

  it("recognises opposite trends", () => {
    const c = compareRegions(challengeStats(up)!, challengeStats(down)!, up, down);
    expect(c.kind).toBe("opposite");
    expect(c.gap).toBeGreaterThan(0.08);
    expect(c.difference?.verdict).toBe("significant");
    expect(c.sharedPoints).toBe(40);
  });

  it("recognises a trend in only one place", () => {
    expect(compareRegions(challengeStats(up)!, challengeStats(flat)!, up, flat).kind).toBe("one");
  });
});
