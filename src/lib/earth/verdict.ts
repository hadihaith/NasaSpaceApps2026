/** Plain-language verdicts for a trend's strength, shared by server and client components. */
export const VERDICT = {
  significant: { text: "Significant trend", tone: "text-good" },
  borderline: { text: "Mixed evidence", tone: "text-close" },
  "not-significant": { text: "No clear trend", tone: "text-ink-2" },
  "not-established": { text: "Not enough evidence to test", tone: "text-ink-2" },
} as const;
