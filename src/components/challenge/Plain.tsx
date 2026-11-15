import type { ReactNode } from "react";
import { EVIDENCE_LEVELS, type EvidenceLevel } from "@/lib/challenge";

/** The everyday reading of a result, set apart from the statistics around it. */
export function PlainWords({ children, delay = 0, title = "In plain words" }: { children: ReactNode; delay?: number; title?: string }) {
  return (
    <div className="ch-plain ch-rise mt-4" style={{ animationDelay: `${delay}ms` }}>
      <p className="ch-plain-tag">
        <span aria-hidden>💬</span> {title}
      </p>
      <div className="mt-1 space-y-2 text-[1.05rem] leading-relaxed">{children}</div>
    </div>
  );
}

const SEGMENT_COLORS = ["var(--line-strong)", "var(--close)", "#7fbf6a", "var(--good)", "#0f6a36"];

/** Five steps from "no evidence" to "very strong evidence", with a pointer that slides to the result. */
export function EvidenceMeter({ evidence, compact = false }: { evidence: EvidenceLevel; compact?: boolean }) {
  return (
    <div>
      <div className="ch-meter" role="img" aria-label={`Evidence of a trend: ${evidence.label}, level ${evidence.level + 1} of 5`}>
        {EVIDENCE_LEVELS.map((l, i) => (
          <span
            key={l.label}
            className="ch-meter-seg"
            data-on={i <= evidence.level}
            style={{ ["--seg-color" as string]: SEGMENT_COLORS[evidence.level], animationDelay: `${i * 90}ms` }}
          />
        ))}
        <span className="ch-meter-pointer" style={{ left: `${((evidence.level + 0.5) / 5) * 100}%` }} aria-hidden>
          ▼
        </span>
      </div>
      {!compact && (
        <div className="mt-1 grid grid-cols-5 gap-1 text-center text-[0.7rem] font-bold leading-tight text-ink-2 sm:text-xs">
          {EVIDENCE_LEVELS.map((l, i) => (
            <span key={l.label} className={i === evidence.level ? "text-ink" : undefined}>
              {l.label}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

const JARGON: [string, string][] = [
  ["Trend", "A steady rise or fall over the years, once the normal ups and downs are set aside."],
  ["Rate per decade", "How much the value changes every 10 years, if the trend carries on at the same pace."],
  ["t-test", "Checks whether the slope is big compared with how unsure we are about it. Signal versus noise."],
  ["p-value", "If nothing were really changing, how often would luck alone produce a trend at least this strong? Small means luck is a poor explanation. Below 0.05 (1 time in 20) we call it significant."],
  ["Mann-Kendall test", "Compares every year with every later year and counts ups and downs. It does not need a straight line or a bell curve, so odd years cannot fool it."],
  ["Confidence interval", "The range of rates that fit the data. “95%” means this way of drawing the range catches the true rate 19 times out of 20."],
  ["Autocorrelation", "When one year resembles the next, the record holds less independent information than its length suggests, so we widen the uncertainty."],
  ["Significant", "Unlikely to be chance. It does not mean large or important, and it says nothing about what caused the change."],
];

export function JargonBuster() {
  return (
    <details className="mt-5 rounded-xl border-2 border-line-strong bg-surface p-4">
      <summary className="cursor-pointer font-bold">Jargon buster: what the statistics words mean</summary>
      <dl className="mt-3 grid gap-3 sm:grid-cols-2">
        {JARGON.map(([term, meaning]) => (
          <div key={term}>
            <dt className="font-bold">{term}</dt>
            <dd className="text-ink-2">{meaning}</dd>
          </div>
        ))}
      </dl>
    </details>
  );
}
