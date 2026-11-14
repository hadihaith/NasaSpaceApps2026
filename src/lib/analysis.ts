import { toSeries, type Mode, type MonthlyRecord } from "./series";
import { analyzeTrend } from "./stats/trend";

/** One place's record, read through the chosen lens and tested for a trend. */
export function analyse(record: MonthlyRecord, mode: Mode, month: number) {
  const points = toSeries(record, mode, month);
  return { points, trend: analyzeTrend(points, 0.05, { assumptionsReviewed: record.assumptionsReviewed, seasonalAdjustment: mode === "anomaly" }) };
}
