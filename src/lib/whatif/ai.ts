/** Server-side settings for the optional AI briefing (Google Gemini). Never import from client code. */

/** The Gemini API key, read from GEMINI_API_KEY or GOOGLE_API_KEY. */
export function geminiKey(): string | undefined {
  return process.env.GEMINI_API_KEY || process.env.GOOGLE_API_KEY || undefined;
}

// Stable model on the Gemini API free tier (checked 2026-11-14). Google retires models
// often, so GEMINI_MODEL switches it without a code change.
export const GEMINI_MODEL = process.env.GEMINI_MODEL || "gemini-3.8-flash";
