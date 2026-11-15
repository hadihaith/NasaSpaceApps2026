import type { Metadata } from "next";
import { ChallengeWalkthrough } from "@/components/challenge/ChallengeWalkthrough";
import { NextStep } from "@/components/NextStep";
import "@/components/challenge/challenge.css";

export const metadata: Metadata = {
  title: "Step by step · The Great Homecoming",
  description:
    "The Trend Detective challenge in four animated steps: choose a variable, acquire NASA data for a region, build the time series, and test the trend with a t-test, Mann-Kendall and confidence intervals.",
};

export default function ChallengePage() {
  return (
    <div className="mx-auto max-w-[1180px] px-4 pb-16 sm:px-6">
      <main>
        <header className="pt-10">
          <p className="text-sm font-bold uppercase tracking-wider text-ink-2">Be an Earth System Trend Detective</p>
          <h1 className="display mt-2 text-5xl sm:text-6xl">The challenge, step by step</h1>
          <p className="mt-3 max-w-[70ch] text-lg text-ink-2">
            Four steps, in the order the challenge sets them: pick a variable, fetch real NASA data for one region, turn
            it into a time series, then measure the rate of change and test whether it is statistically significant.
          </p>
        </header>
        <ChallengeWalkthrough />
        <NextStep from="/challenge" />
      </main>
      <footer className="mt-16 border-t-[2.5px] border-edge pt-5 text-sm text-ink-2">
        <p className="max-w-[80ch]">
          Data from{" "}
          <a className="underline underline-offset-4" href="https://power.larc.nasa.gov/">
            NASA POWER
          </a>{" "}
          (MERRA-2 reanalysis and CERES/GEWEX SRB satellite records), fetched live. The tests look for straight-line
          trends only, and a significant trend does not say what caused it.
        </p>
      </footer>
    </div>
  );
}
