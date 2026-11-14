import type { Metadata } from "next";
import Link from "next/link";
import { NextStep } from "@/components/NextStep";
import { KIND_LABEL, SECTIONS } from "@/lib/site";

export const metadata: Metadata = {
  title: "How to · The Great Homecoming",
  description:
    "Where to start and what to do in each section: the story game, trend investigations, Phoenix inferences and the what-if model.",
};

const PATHS = [
  {
    question: "I want to play",
    answer: "Start with the story. The aliens send you to real Earth data along the way.",
    href: "/",
    cta: "Play the story",
  },
  {
    question: "I want to test real data",
    answer: "Pick any variable and place, guess the trend, then check it on 40 years of NASA data.",
    href: "/detective",
    cta: "Investigate a trend",
  },
  {
    question: "I want to change the climate",
    answer: "Turn the dials on Phoenix's real climate and read what a physics model says would follow.",
    href: "/what-if",
    cta: "Run a what-if",
  },
] as const;

const TERMS = [
  {
    term: "Trend",
    meaning: "A steady rise or fall over the years, after the seasons are averaged out.",
  },
  {
    term: "Rate per decade",
    meaning:
      "How fast the value changes, from the Theil-Sen slope: the middle of the slopes between every pair of years, so one odd year cannot swing it.",
  },
  {
    term: "Significant trend",
    meaning: "The statistical test says the trend is unlikely to be chance alone (p < 0.05).",
  },
  {
    term: "No clear trend",
    meaning: "The test cannot rule out chance. A longer record might change that.",
  },
  {
    term: "Mixed evidence",
    meaning: "The tests disagree, so the trend is shown but not called significant.",
  },
  {
    term: "Significance not established",
    meaning:
      "The rate is shown, but the test's assumptions (for example, that years are independent) have not been checked for this record, so no significance claim is made.",
  },
] as const;

export default function HowToPage() {
  return (
    <div className="mx-auto max-w-[1180px] px-4 pb-16 sm:px-6">
      <main>
        <header className="mt-10 border-b-[2.5px] border-edge pb-4">
          <p className="chip">Start here</p>
          <h1 className="display mt-3 text-5xl sm:text-6xl">How to use this site</h1>
          <p className="mt-3 max-w-[75ch] text-lg text-ink-2">
            This site has four sections, numbered in the order to visit them in the bar at the top. Start with the
            story, then look at real NASA records, then experiment. Every section ends with a link to the next one.
          </p>
        </header>

        <section aria-labelledby="paths-heading" className="mt-10">
          <h2 id="paths-heading" className="display text-4xl">
            Where do you want to start?
          </h2>
          <ul className="mt-6 grid gap-5 md:grid-cols-3" role="list">
            {PATHS.map((path) => (
              <li key={path.href} className="card flex flex-col p-5">
                <h3 className="display text-2xl">{path.question}</h3>
                <p className="mt-2 flex-1 text-ink-2">{path.answer}</p>
                <Link href={path.href} className="btn btn-primary mt-4 self-start">
                  {path.cta} <span aria-hidden>→</span>
                </Link>
              </li>
            ))}
          </ul>
        </section>

        <section aria-labelledby="route-heading" className="mt-16">
          <h2 id="route-heading" className="display text-4xl">
            The full route
          </h2>
          <ol className="mt-6 space-y-5" role="list">
            {SECTIONS.map((section, i) => (
              <li key={section.href} className="card grid gap-4 p-5 sm:grid-cols-[auto_1fr] sm:p-6">
                <span
                  aria-hidden
                  className="display grid h-14 w-14 place-items-center rounded-full border-[2.5px] border-edge bg-gold text-2xl text-[#10242e]"
                >
                  {i + 1}
                </span>
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-3">
                    <h3 className="display text-3xl">
                      <span className="sr-only">Step {i + 1}: </span>
                      {section.title}
                    </h3>
                    <span className="chip">{KIND_LABEL[section.kind]}</span>
                  </div>
                  <p className="mt-1 text-ink-2">{section.summary}</p>
                  <ol className="mt-3 list-decimal space-y-1 pl-5">
                    {section.steps.map((step) => (
                      <li key={step}>{step}</li>
                    ))}
                  </ol>
                  <Link href={section.href} className="btn mt-4">
                    Open {section.label} <span aria-hidden>→</span>
                  </Link>
                </div>
              </li>
            ))}
          </ol>
        </section>

        <section aria-labelledby="kinds-heading" className="mt-16">
          <h2 id="kinds-heading" className="display text-4xl">
            Real data or simulation?
          </h2>
          <div className="mt-6 grid gap-5 md:grid-cols-2">
            <div className="card p-5">
              <p className="chip">{KIND_LABEL.real}</p>
              <p className="mt-3">
                <strong>Investigate</strong>, <strong>Inferences</strong> and the game&apos;s <strong>Earth Archive</strong>{" "}
                use measured or reanalysed records: NASA MERRA-2 through NASA POWER, NASA GPM IMERG satellite rainfall,
                and NOAA weather-station data.
              </p>
            </div>
            <div className="card p-5">
              <p className="chip">{KIND_LABEL.hypothetical}</p>
              <p className="mt-3">
                The game&apos;s <strong>laboratory</strong> and <strong>What if</strong> are simulations. They start from
                real climate but show what a simple model says <em>would</em> happen. They are not forecasts and never
                change or predict the real Earth.
              </p>
            </div>
          </div>
        </section>

        <section aria-labelledby="terms-heading" className="mt-16">
          <h2 id="terms-heading" className="display text-4xl">
            Reading the results
          </h2>
          <dl className="mt-6 grid gap-x-8 gap-y-4 md:grid-cols-2">
            {TERMS.map(({ term, meaning }) => (
              <div key={term} className="border-l-[3px] border-gold pl-4">
                <dt className="font-bold">{term}</dt>
                <dd className="mt-1 text-ink-2">{meaning}</dd>
              </div>
            ))}
          </dl>
          <p className="mt-6 max-w-[75ch] text-ink-2">
            Moving together is not the same as causing. When two records rise and fall together, both may be answering
            to something else, such as a strong monsoon season.
          </p>
        </section>

        <section aria-labelledby="help-heading" className="mt-16">
          <h2 id="help-heading" className="display text-4xl">
            Getting help
          </h2>
          <ul className="mt-4 max-w-[80ch] list-disc space-y-2 pl-5">
            <li>
              Use the <strong>Ask the app</strong> button in the bottom-right corner of any page to ask the assistant about
              the site, its data or its science. It can make mistakes, so check its answers against the pages.
            </li>
            <li>Your game progress, XP and badges are saved only in this browser. No account is needed.</li>
            <li>Lost? The numbered bar at the top always shows which section you are in and what comes next.</li>
          </ul>
        </section>

        <NextStep from="/how-to" />
      </main>
    </div>
  );
}
