import { createHash } from "node:crypto";
import type { Mode } from "../series";
import { analyzeTrend, comparePairedSeries, MIN_POINTS, type Point, type TrendResult } from "../stats/trend";
import { analyzeEvidence, type EvidenceDataset } from "./member3";

export const HISTORICAL_SCHEMA_VERSION = 2;
export const PRIMARY_ALPHA = 0.05;

export interface DateWindow {
  startYear: number;
  endYear: number;
}

export interface HistoricalReview {
  id: string;
  reviewer: string;
  rationale: string;
  accepted: boolean;
}

function identityFields(dataset: EvidenceDataset) {
  return {
    id: dataset.id,
    version: dataset.provenance.version,
    variable: dataset.variable,
    parameter: dataset.source.parameter,
    sourceVersion: dataset.source.version,
    sourceUrl: dataset.provenance.sourceUrl,
    timeStandard: dataset.temporal.timeStandard,
    rawSha256: dataset.rawSha256,
    unit: dataset.units,
    annualAggregation: dataset.annualInterpretation,
    footprint: dataset.spatial,
    observations: dataset.observations,
  };
}

function fingerprintFor(dataset: EvidenceDataset) {
  return createHash("sha256").update(JSON.stringify(identityFields(dataset))).digest("hex");
}

function findingId(fingerprints: string[], { startYear, endYear }: DateWindow,
  mode: Mode, month = 0) {
  return [`historical-v${HISTORICAL_SCHEMA_VERSION}`, ...fingerprints, startYear, endYear,
    mode, mode === "month" ? month + 1 : "-"].join(":");
}

const NORMAL_PERIOD = { startYear: 1991, endYear: 2020 };

function monthMeans(dataset: EvidenceDataset, { startYear, endYear }: DateWindow) {
  const means = Array.from({ length: 12 }, () => ({ sum: 0, count: 0 }));
  for (const { date, value } of dataset.observations) {
    const year = Number(date.slice(0, 4));
    if (year < startYear || year > endYear || value === null) continue;
    const cell = means[Number(date.slice(5)) - 1];
    cell.sum += value;
    cell.count++;
  }
  return {
    counts: means.map(({ count }) => count),
    means: means.map(({ count, sum }) => count ? sum / count : null),
  };
}

function baselineFor(dataset: EvidenceDataset, window: DateWindow) {
  const normal = monthMeans(dataset, NORMAL_PERIOD);
  if (normal.counts.every((n) => n === 30)) {
    return { ...NORMAL_PERIOD, ...normal, type: "fixed" };
  }
  return { ...window, ...monthMeans(dataset, window), type: "period-relative" };
}

type Reviews = Readonly<Record<string, HistoricalReview>>;

export function analyzeHistoricalRecord(dataset: EvidenceDataset,
  window: DateWindow, mode: Mode = "annual", month = 0, reviews: Reviews = {}) {
  const fingerprint = fingerprintFor(dataset);
  const id = findingId([fingerprint], window, mode, month);
  const review = reviews[id] ?? null;
  const baseline = mode === "anomaly" ? baselineFor(dataset, window) : null;
  const { points, trend } = analyzeEvidence(dataset, mode, window.startYear, window.endYear, month, review ? {
    ...window, datasetId: dataset.id, productVersion: dataset.provenance.version, mode, month,
    reviewer: review.reviewer, rationale: review.rationale, assumptionsReviewed: review.accepted,
  } : undefined, baseline?.means);
  const dates = datesFor(points, mode, month);
  const aggregation = mode === "annual" ? dataset.annualInterpretation
    : mode === "month" ? "Same calendar month across years"
    : baseline?.type === "fixed" ? "Monthly departures from the fixed calendar-month normal"
    : "Monthly departures from calendar-month means over the displayed period";
  return {
    id, datasetId: dataset.id, fingerprint, startYear: window.startYear, endYear: window.endYear,
    mode, month: mode === "month" ? month + 1 : null,
    aggregation, unit: dataset.units, baseline, ...summarize(points, dates, trend, review, "Zero linear slope in this record."),
  };
}

interface ComparisonDesign extends DateWindow {
  id: string;
  type: "historical-comparison" | "product-cross-check";
  question: string;
  scope: string;
}

/** Two validated datasets with the same measurement and annual time treatment. */
export function analyzeHistoricalComparison(design: ComparisonDesign, leftData: EvidenceDataset, rightData: EvidenceDataset,
  reviews: Reviews = {}) {
  const sameType = leftData.providerKind === rightData.providerKind &&
    leftData.source.parameter === rightData.source.parameter &&
    leftData.provenance.product === rightData.provenance.product &&
    Boolean(leftData.temporal.timeStandard) &&
    leftData.temporal.timeStandard === rightData.temporal.timeStandard &&
    leftData.spatial.type === rightData.spatial.type &&
    leftData.spatial.nativeResolution === rightData.spatial.nativeResolution;
  if (leftData.units !== rightData.units || leftData.gameVariable !== rightData.gameVariable ||
      !leftData.gameVariable ||
      leftData.annualInterpretation !== rightData.annualInterpretation ||
      (design.type === "historical-comparison" && !sameType) ||
      (design.type === "product-cross-check" && (!leftData.spatial.bbox ||
        JSON.stringify(leftData.spatial.bbox) !== JSON.stringify(rightData.spatial.bbox)))) {
    throw new Error("Comparison requires compatible variables, units, aggregation and footprints.");
  }
  const left = analyzeHistoricalRecord(leftData, design, "annual", 0, reviews);
  const right = analyzeHistoricalRecord(rightData, design, "annual", 0, reviews);
  const fingerprints = [left.fingerprint, right.fingerprint];
  const id = findingId(fingerprints, design, "annual");
  const review = design.type === "historical-comparison" ? reviews[id] ?? null : null;
  const asPoints = (finding: typeof left) => finding.points.map(({ t, value }) => ({ t, v: value }));
  const pair = comparePairedSeries(asPoints(left), asPoints(right), PRIMARY_ALPHA, {
    assumptionsReviewed: review?.accepted === true && Boolean(review.reviewer.trim() && review.rationale.trim()) &&
      left.n === design.endYear - design.startYear + 1 && right.n === left.n,
  });
  const coverageReason = pair.points.length === design.endYear - design.startYear + 1 ? null
    : "The paired test requires complete annual coverage across the pinned window.";
  const dates = datesFor(pair.points, "annual");
  const commonTimes = new Set(pair.points.map(({ t }) => t));
  // The displayed site rates also use the common dates, even if a future revision has a coverage gap.
  function onCommonDates(finding: typeof left) {
    if (finding.n === commonTimes.size) return finding;
    const points = asPoints(finding).filter(({ t }) => commonTimes.has(t));
    return {
      ...finding,
      id: [id, "common", finding.fingerprint].join(":"),
      ...summarize(points, dates, analyzeTrend(points), null, "Zero linear slope in this record."),
    };
  }
  return {
    ...design,
    leftRegion: leftData.gameRegionId, rightRegion: rightData.gameRegionId,
    leftDatasetId: leftData.id, rightDatasetId: rightData.id,
    left: onCommonDates(left), right: onCommonDates(right),
    monthlyOverlap: design.type === "product-cross-check" ? commonMonths(leftData, rightData) : null,
    difference: {
      id, leftDatasetId: leftData.id, rightDatasetId: rightData.id,
      fingerprints, startYear: design.startYear, endYear: design.endYear,
      mode: "annual", month: null, unit: left.unit, aggregation: left.aggregation, baseline: null,
      ...summarize(pair.points, dates, pair.trend, review, "Zero linear slope in the paired left-minus-right record.",
        coverageReason ? [coverageReason] : []),
    },
  };
}

function commonMonths(a: EvidenceDataset, b: EvidenceDataset) {
  const other = new Set(b.observations.filter(({ value }) => value !== null).map(({ date }) => date));
  const dates = a.observations.filter(({ date, value }) => value !== null && other.has(date)).map(({ date }) => date);
  return { firstDate: dates[0] ?? null, lastDate: dates.at(-1) ?? null, n: dates.length };
}

function datesFor(points: Point[], mode: Mode, month = 0) {
  return points.map(({ t }) => {
    const year = Math.floor(t);
    if (mode === "annual") return String(year);
    const monthNumber = mode === "month" ? month + 1 : Math.floor((t - year) * 12) + 1;
    return `${year}-${String(monthNumber).padStart(2, "0")}`;
  });
}

function summarize(points: Point[], dates: string[], trend: TrendResult | null,
  review: HistoricalReview | null, hypothesis: string, additionalReasons: string[] = []) {
  const established = trend?.inference.established === true;
  const reasons = trend?.inference.reasons ?? [`Fewer than ${MIN_POINTS} usable points.`];
  return {
    firstDate: dates[0] ?? null,
    lastDate: dates.at(-1) ?? null,
    points: points.map(({ t, v }, i) => ({ date: dates[i], t, value: v })),
    n: points.length,
    hasSlope: trend !== null,
    slopePerDecade: trend ? trend.sen.slope * 10 : null,
    magnitude: trend?.magnitude ?? null,
    direction: trend?.direction ?? "unavailable",
    status: established ? trend.ols.p < PRIMARY_ALPHA ? "significant" : "not-significant" : "not-established",
    reasons: [...additionalReasons, ...(review && !review.accepted ? [review.rationale] : []), ...reasons],
    test: established ? {
      method: "Two-sided OLS slope t-test",
      hypothesis,
      alpha: trend.alpha,
      p: trend.ols.p,
      slopePerDecade: trend.ols.slope * 10,
      lowerPerDecade: trend.ols.ci[0] * 10,
      upperPerDecade: trend.ols.ci[1] * 10,
    } : null,
    review,
  };
}
