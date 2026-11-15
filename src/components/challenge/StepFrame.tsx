import type { ReactNode } from "react";

export const OBJECTIVES = [
  {
    short: "Identify a variable",
    title: "Identify a variable of interest",
    objective: "Pick an Earth science variable that can be investigated for a trend.",
  },
  {
    short: "Acquire regional data",
    title: "Acquire the data for a region",
    objective: "Most trends are regional, so fetch the variable for one particular place.",
  },
  {
    short: "Build the time series",
    title: "Generate and visualise the time series",
    objective: "Turn the record into a series long enough for statistical analysis, and plot it.",
  },
  {
    short: "Test the trend",
    title: "Measure the rate of change and test it",
    objective: "Estimate how fast it changes, then test significance with a t-test, the Mann-Kendall test and confidence intervals.",
  },
] as const;

/** The heading every step shares: which objective this is, and whether it is met yet. */
export function StepFrame({
  index,
  done,
  children,
  footer,
}: {
  index: number;
  done: boolean;
  children: ReactNode;
  footer: ReactNode;
}) {
  const o = OBJECTIVES[index];
  return (
    <section aria-labelledby={`step-${index}-heading`} className="card p-5 sm:p-7">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-bold uppercase tracking-wider text-ink-2">
            Step {index + 1} of 4 · Challenge objective
          </p>
          <h2 id={`step-${index}-heading`} className="display mt-1 text-3xl sm:text-4xl">
            {o.title}
          </h2>
          <p className="mt-2 max-w-[70ch] text-ink-2">{o.objective}</p>
        </div>
        <span
          key={String(done)}
          className={`ch-pill ch-pop shrink-0 ${done ? "text-good" : "text-muted"}`}
          aria-live="polite"
        >
          {done ? "✓ Objective met" : "In progress"}
        </span>
      </div>
      <div className="mt-6">{children}</div>
      <div className="mt-7 flex flex-wrap items-center justify-between gap-3 border-t-2 border-line pt-5">{footer}</div>
    </section>
  );
}
