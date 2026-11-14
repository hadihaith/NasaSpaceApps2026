/**
 * Turning a raw monthly record into the series that gets tested.
 *
 * Most variables swing with the seasons far more than they drift over decades,
 * so a trend only shows once that cycle is taken out. Each mode does this a
 * different way.
 */
import type { Point } from "./stats/trend";

export type Mode = "annual" | "anomaly" | "month";

export const MODES: { id: Mode; label: string; explain: string }[] = [
  {
    id: "annual",
    label: "Yearly averages",
    explain: "One point per year. Averaging all twelve months cancels the seasons.",
  },
  {
    id: "anomaly",
    label: "Monthly, seasons removed",
    explain:
      "One point per month, shown as its difference from what is normal for that month. More points, but neighbouring months resemble each other.",
  },
  {
    id: "month",
    label: "One month each year",
    explain: "Compare the same calendar month across the years, such as every July.",
  },
];

export const MONTH_NAMES = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

export interface SeriesPoint extends Point {
  label: string;
}

export interface MonthlyRecord {
  /** Set only following Member 4's documented dataset review. */
  assumptionsReviewed?: boolean;
  startYear: number;
  monthly: (number | null)[];
}

export function toSeries(record: MonthlyRecord, mode: Mode, month = 6): SeriesPoint[] {
  const { startYear, monthly } = record;
  if (!Number.isInteger(startYear) || !Array.isArray(monthly) ||
      monthly.some((v) => v !== null && !Number.isFinite(v))) {
    throw new RangeError("Use an integer startYear and finite monthly values or null.");
  }
  if (!["annual", "anomaly", "month"].includes(mode)) throw new RangeError("Unknown time mode.");
  if (!Number.isInteger(month) || month < 0 || month > 11) throw new RangeError("month must be 0 through 11.");
  const years = Math.ceil(monthly.length / 12);
  const out: SeriesPoint[] = [];

  if (mode === "annual") {
    for (let y = 0; y < years; y++) {
      const slice = monthly.slice(y * 12, y * 12 + 12);
      // A year with a missing month would be biased toward the seasons it does have.
      if (slice.length !== 12 || slice.some((v) => v === null)) continue;
      const mean = (slice as number[]).reduce((a, b) => a + b, 0) / 12;
      out.push({ t: startYear + y, v: mean, label: String(startYear + y) });
    }
    return out;
  }

  if (mode === "month") {
    for (let y = 0; y < years; y++) {
      const v = monthly[y * 12 + month];
      if (v === null || v === undefined) continue;
      out.push({ t: startYear + y, v, label: `${MONTH_NAMES[month]} ${startYear + y}` });
    }
    return out;
  }

  // Anomalies: subtract each calendar month's average over the whole record.
  const normals = Array.from({ length: 12 }, (_, m) => {
    let sum = 0;
    let n = 0;
    for (let i = m; i < monthly.length; i += 12) {
      const v = monthly[i];
      if (v !== null) {
        sum += v;
        n++;
      }
    }
    return n > 0 ? sum / n : null;
  });
  monthly.forEach((v, i) => {
    const normal = normals[i % 12];
    if (v === null || normal === null) return;
    const year = startYear + Math.floor(i / 12);
    out.push({
      t: year + ((i % 12) + 0.5) / 12,
      v: v - normal,
      label: `${MONTH_NAMES[i % 12].slice(0, 3)} ${year}`,
    });
  });
  return out;
}
