import { Workbench } from "@/components/Workbench";

export default function Home() {
  return (
    <div id="top" className="mx-auto max-w-[1180px] px-4 pb-16 sm:px-6">
      <main>
        <Workbench />
      </main>

      <footer className="mt-16 border-t-[2.5px] border-edge pt-5 text-sm text-ink-2">
        <p className="max-w-[80ch]">
          Data from{" "}
          <a className="underline underline-offset-4" href="https://power.larc.nasa.gov/">
            NASA POWER
          </a>
          , which serves the MERRA-2 reanalysis and CERES and GEWEX SRB satellite records on a
          grid of about half a degree. Globe imagery is Blue Marble from{" "}
          <a
            className="underline underline-offset-4"
            href="https://www.earthdata.nasa.gov/engage/open-data-services-software/earthdata-developer-portal/gibs-api"
          >
            NASA GIBS
          </a>
          . Made for the{" "}
          <a
            className="underline underline-offset-4"
            href="https://www.spaceappschallenge.org/2026/challenges/earth-system-trend-detective/"
          >
            NASA Space Apps 2026 challenge
          </a>
          . Your XP and badges are saved in this browser only. This tool tests for straight-line
          trends only.
        </p>
      </footer>
    </div>
  );
}
