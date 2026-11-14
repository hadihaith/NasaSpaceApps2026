/** Server-side settings for the optional AI briefing (Google Gemini). Never import from client code. */

/** The Gemini API key, read from GEMINI_API_KEY or GOOGLE_API_KEY. */
export function geminiKey(): string | undefined {
  return process.env.GEMINI_API_KEY || process.env.GOOGLE_API_KEY || undefined;
}

// Checked 2026-11-14 with the team's key: gemini-3.5-flash answered in about 5 s, while
// gemini-3.8-flash accepted requests but never replied. Google retires and rebalances models
// often, so GEMINI_MODEL switches it without a code change.
export const GEMINI_MODEL = process.env.GEMINI_MODEL || "gemini-3.5-flash";

/** Give up on the model after this long, so a stalled request never leaves the page hanging. */
export const GEMINI_TIMEOUT_MS = 30_000;
