# Trend Detective

A web tool for the NASA Space Apps 2026 challenge
[Be an Earth System Trend Detective](https://www.spaceappschallenge.org/2026/challenges/earth-system-trend-detective/).

Pick an Earth science variable and a place. The app pulls four decades of NASA
data, plots the time series, and says in plain language whether there is a
trend, how fast it is, and whether it is statistically significant.

## Use it online

Open **https://nasatrenddetective.vercel.app** in any modern browser, on a computer or a phone.
Nothing to install, and no account or API key is needed.

The site redeploys automatically from `main` through GitHub Actions (see [docs/deployment.md](docs/deployment.md)).

## Run it locally

```bash
npm install
npm run dev
```

Open http://localhost:3000. No API keys or accounts are needed.

| Command | What it does |
| --- | --- |
| `npm run dev` | Development server |
| `npm test` | Statistics unit tests (Vitest) |
| `npm run typecheck` | TypeScript check |
| `npm run lint` | ESLint |
| `npm run build` | Production build |

## How the game works

1. **Pick a case** from the board, or open your own case with any variable and place.
2. **Make your call** before seeing any data: rising, falling, or no clear trend.
3. **Open the evidence.** The chart draws in, the verdict appears, and your call is stamped
   spot on (+100 XP), close (+50) or missed (+10). Spot-on calls in a row earn a streak bonus,
   and the first solve of a board case earns +50 and up to three stars.
4. **Change the lens** afterwards to read the same record three ways, without affecting the score.

XP builds through five ranks, from Rookie to Chief Scientist, and seven badges reward good
detective habits, such as correctly calling that nothing is changing. Progress is kept in the
browser's local storage. The rules are pure functions in `src/lib/game.ts`, with tests.

## How it maps to the challenge

| Challenge objective | Where it lives |
| --- | --- |
| Identify variables of interest | `src/lib/catalog.ts`: 18 variables in four themes, each with a plain definition |
| Acquire data for a region | `src/lib/power.ts` and `/api/timeseries`: a point or an area-weighted 2°, 5° or 10° box |
| Generate and visualise a time series | `src/lib/series.ts` (yearly means, deseasonalised monthly anomalies, one month per year) and `src/components/TrendChart.tsx` |
| Rate of change and significance | `src/lib/stats/`: Theil-Sen slope with confidence interval, Mann-Kendall test, least-squares t-test with an autocorrelation correction |
| Same variable, opposite trends in different regions | "Compare with a second place": two places on the globe, one chart, and a test of whether their rates differ (`compareSlopes`) |
| Demonstration examples | Six one-click cases in `DEMO_CASES`, including a Sahel versus Chile rainfall comparison |

## Stack

- **Next.js 16 (App Router) and TypeScript.** One codebase and one deploy for
  the interface and the small API that talks to NASA.
- **NASA POWER** as the data source. It serves MERRA-2 and CERES/SRB variables
  as monthly series for a point or region, from 1981, with no login.
- **Statistics in plain TypeScript**, with no dependencies, tested against
  textbook values. They run in the browser, so changing how time is read
  re-tests instantly.
- **MapLibre GL** globe with NASA GIBS Blue Marble imagery and bundled Natural
  Earth country outlines.
- **D3 scales with hand-written SVG** for the chart.
- **Tailwind CSS 4** with design tokens in `src/app/globals.css`.

## Things worth knowing

- This is Next.js 16. Some conventions differ from older versions; the guides
  are in `node_modules/next/dist/docs/`.
- A trend is called significant only when the Mann-Kendall test and the
  autocorrelation-adjusted regression both give p < 0.05. One without the
  other is reported as mixed evidence. The rule is in `analyzeTrend`.
- When two places are compared, the chart shows each relative to its own
  average so that very different places share one scale. The two places always
  use the same variable and region size.
- POWER limits a regional request to one variable and a 10° box.
- Sunlight and cloud variables join two satellite records at 2001; the app
  warns about this.
- NASA GIBS fails a share of tile requests at random, so globe imagery goes
  through `/api/tiles`, which retries.
- The Hydrology Data Rods service listed in the challenge resources has been
  retired.

## Ideas for what to build next

- More sources behind the same `MonthlySeries` shape: Giovanni or AppEEARS
  (need a free Earthdata token) for MODIS, OMI, AIRS and GPM variables.
- A guided interview, optionally AI-assisted, that turns "what are you curious
  about and where?" into a suggested variable and region.
- Shareable links that encode the investigation in the URL.
- "Scan this place": test every variable at once and rank them by strength of
  evidence.
- A start and end year control, to show how record length affects significance.
