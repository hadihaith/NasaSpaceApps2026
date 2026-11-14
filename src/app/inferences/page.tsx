import type { Metadata } from "next";
import { InferenceExplorer } from "@/components/InferenceExplorer";
import { NextStep } from "@/components/NextStep";
import { VERDICT } from "@/lib/earth/verdict";
import { cityVsRegionWarming, rainAgreement, relationships, seriesSummaries } from "@/lib/earth/inferences";
import { pText, sig, signed } from "@/lib/format";

export const metadata: Metadata = {
  title: "Inferences · The Great Homecoming",
  description:
    "What 45 years of NASA and NOAA records for Phoenix, Arizona do and do not let us conclude, with every trend tested for significance.",
};

function strengthWord(r: number) {
  const a = Math.abs(r);
  return a >= 0.7 ? "strongly" : a >= 0.4 ? "moderately" : a >= 0.2 ? "weakly" : "barely";
}

export default function InferencesPage() {
  const series = seriesSummaries();
  const city = cityVsRegionWarming();
  const rain = rainAgreement();
  const links = relationships();
  const significant = series.filter((s) => s.trend.strength === "significant");

  return (
    <div className="mx-auto max-w-[1180px] px-4 pb-16 sm:px-6">
      <main>
        <header className="mt-10 border-b-[2.5px] border-edge pb-4">
          <p className="chip">Region 01 · Phoenix and central Arizona · 1981–2025</p>
          <h1 className="display mt-3 text-5xl sm:text-6xl">Inferences</h1>
          <p className="mt-3 max-w-[75ch] text-lg text-ink-2">
            What can we actually conclude from {series.length} real records of this desert region? Each record is
            aggregated to one value per complete year. Rates are descriptive until a dataset-specific
            scientific review establishes that a significance test is appropriate. {significant.length} of{" "}
            {series.length} currently have an established significant result.
          </p>
        </header>

        <section aria-labelledby="board-heading" className="mt-10">
          <h2 id="board-heading" className="display text-4xl">
            The trend board
          </h2>
          <p className="mt-2 max-w-[75ch] text-ink-2">
            Pick a record to see its chart and test results. Model, satellite and station records are labelled,
            because they measure different things in different ways.
          </p>
          <div className="mt-6">
            <InferenceExplorer series={series} />
          </div>
        </section>

        <section aria-labelledby="clues-heading" className="mt-16">
          <h2 id="clues-heading" className="display text-4xl">
            Three descriptive comparisons
          </h2>
          <div className="mt-6 grid gap-6 lg:grid-cols-3">
            {city && (
              <article className="card p-5">
                <h3 className="display text-2xl">Is the city warming faster than the region?</h3>
                <p className="mt-3">
                  From {city.from} to {city.to}, the Phoenix Airport station warmed at{" "}
                  <strong className="tabular">{signed(city.cityPerDecade, 2)} °C per decade</strong>, and the
                  surrounding 2° region (NASA MERRA-2) at{" "}
                  <strong className="tabular">{signed(city.regionPerDecade, 2)} °C per decade</strong>.
                </p>
                <p className="mt-1 text-xs text-ink-2">
                  Descriptive least-squares rates. The paired comparison uses common-year differences;
                  its scientific review is pending. These rates differ slightly from the Theil-Sen trend board.
                </p>
                <p className="mt-3">
                  The difference, {signed(city.differencePerDecade, 2)} °C per decade, is{" "}
                  {!city.established ? "not assessed for significance" : city.significant ? "statistically significant" : "not statistically significant"} ({pText(city.p)}
                  ).{" "}
                  {!city.established ? city.reason : city.significant
                    ? city.differencePerDecade > 0
                      ? "On this evidence the city is warming faster than its surroundings."
                      : "On this evidence the city is warming more slowly than its surroundings."
                    : "On this evidence we cannot say the city and the region are warming at different rates."}
                </p>
                <p className="mt-3 text-sm text-ink-2">
                  {city.differencePerDecade > 0
                    ? "A faster-warming city fits the urban heat island idea, but a single station can also change because of new instruments or surroundings."
                    : "Cities are often expected to warm faster than their surroundings (the urban heat island), so this result is worth a closer look. A single station can also change because of new instruments or surroundings."}{" "}
                  These records alone cannot say why.
                </p>
              </article>
            )}

            <article className="card p-5">
              <h3 className="display text-2xl">Do three rain records agree?</h3>
              <p className="mt-3">
                Over {rain.from}–{rain.to}, the years all three cover:
              </p>
              <ul className="mt-2 space-y-2">
                {rain.records.map((r) => (
                  <li key={r.id}>
                    <strong>{r.label}</strong>
                    <span className="block text-sm text-ink-2 tabular">
                      {sig(r.meanPerYear, 3)} mm a year · {signed(r.perDecade, 2)} mm per decade ·{" "}
                      {VERDICT[r.strength].text.toLowerCase()}
                    </span>
                  </li>
                ))}
              </ul>
              <p className="mt-3">
                {new Set(rain.records.map((r) => r.direction)).size === 1
                  ? `All three point the same way (${rain.records[0].direction}).`
                  : "They do not even agree on the direction of change."}{" "}
                {rain.records.some((r) => r.strength === "not-established")
                  ? "Significance is not established for these unreviewed records."
                  : rain.records.some((r) => r.strength === "significant")
                  ? "At least one finds a significant trend over these years."
                  : "None finds a significant trend over these years, so the honest verdict is: not proven."}
              </p>
              <p className="mt-3 text-sm text-ink-2">
                The station sits in the dry low desert; the satellite and model average over a box that includes
                wetter mountains, so their totals are higher. Agreement on the direction of change matters more
                than agreement on the amount.
              </p>
            </article>

            <article className="card p-5">
              <h3 className="display text-2xl">What moves together?</h3>
              <p className="mt-3 text-sm text-ink-2">
                Year-to-year ups and downs, with each record&apos;s long-term trend removed first.
              </p>
              <ul className="mt-2 space-y-3">
                {links.map((l) => (
                  <li key={`${l.a}-${l.b}`}>
                    <strong>
                      {l.aLabel} and {l.bLabel}
                    </strong>
                    <span className="block text-sm text-ink-2 tabular">
                      {l.r >= 0 ? "Rise and fall together" : "Move in opposite directions"},{" "}
                      {strengthWord(l.r)} (r = {signed(l.r, 2)}, {pText(l.p)}, {l.n} years)
                    </span>
                  </li>
                ))}
              </ul>
              <p className="mt-3 text-sm text-ink-2">
                These correlations are descriptive; no reviewed significance test is supplied. Moving together
                is not proof that one causes the other. Both may answer to a third factor, such as a strong monsoon season.
              </p>
            </article>
          </div>
        </section>

        <section aria-labelledby="limits-heading" className="mt-16">
          <h2 id="limits-heading" className="display text-4xl">
            What these records cannot tell you
          </h2>
          <ul className="mt-4 max-w-[80ch] list-disc space-y-2 pl-5 text-ink-2">
            <li>Why something changed. Every test here finds a pattern, not its cause.</li>
            <li>
              Anything outside the 2° box around Phoenix. The same variable can trend the other way in another
              region.
            </li>
            <li>
              Curved or sudden changes. These tests look for a steady, straight-line trend, and a record that
              swings a lot can hide a real trend that a longer record would reveal.
            </li>
            <li>
              Measured soil moisture. Soil wetness here comes from NASA&apos;s MERRA-2 model, not from a sensor in
              the ground.
            </li>
            <li>
              Much from station humidity alone. It covers only 2006–2024, so its trend rests on far fewer years
              than the others, and NOAA does not document how its monthly values are made.
            </li>
          </ul>
        </section>
        <NextStep from="/inferences" />
      </main>

      <footer className="mt-16 border-t-[2.5px] border-edge pt-5 text-sm text-ink-2">
        <p className="max-w-[80ch]">
          Records: NASA MERRA-2 via NASA POWER, NASA GPM IMERG V07 via NASA Giovanni, and NOAA NCEI Global Summary of
          the Month for Phoenix Airport (USW00023183). Full sources, versions and processing are in the project&apos;s{" "}
          <a
            className="underline underline-offset-4"
            href="https://github.com/hadihaith/NasaSpaceApps2026/blob/main/docs/data-sources.md"
          >
            data sources document
          </a>
          .
        </p>
      </footer>
    </div>
  );
}
