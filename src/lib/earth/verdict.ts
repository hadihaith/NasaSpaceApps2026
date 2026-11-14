import type { Strength } from "../stats/trend";

/** Keep unavailable inference separate from a reviewed, nonsignificant test. */
export const VERDICT = {
  significant: { text: "Significant trend", tone: "text-good" },
  borderline: { text: "Mixed evidence", tone: "text-close" },
  "not-significant": { text: "No clear trend", tone: "text-ink-2" },
  "not-established": { text: "Significance not established", tone: "text-ink-2" },
} as const satisfies Record<Strength, { text: string; tone: string }>;
