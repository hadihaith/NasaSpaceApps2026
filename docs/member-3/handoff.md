# Member 3 data handoff

Prepared 14 November 2026. This is a technical data/QA delivery for Members 1 and 4, not a scientific approval or a message sent to them.

## Delivered files and commands

- Canonical Phoenix records: `public/data/earth-observations.json` (13 series). Existing raw files remain unchanged and checksum-pinned in `data/raw/manifest.json`.
- Five additional POWER points: unchanged provider responses plus URLs, retrieval times, byte counts and SHA-256 in `data/raw/homecoming/manifest.json`.
- Member 4 imports: `data/processed/member4/*.json` (38 single-variable documents). Each passes the actual `parseDataset` and `analyzeDataset` implementation in `src/lib/member4/dataset.ts`. Each is below 2,000,000 bytes and 2,400 months.
- Release evidence: `public/data/earth-archive.json` (32 NASA records). Six station exports remain local handoff files because the source register has not established their redistribution terms. The pre-existing canonical Phoenix collection, used by Inferences, still contains station records.
- Simulation inputs: `public/homecoming/regional-climate.json` now contains only generated 1991–2020 monthly normals, sample counts, units and versioned series IDs. Historical values are loaded from the evidence exports, not this projection. Phoenix normals come from `public/data/baselines.json`; the five points use the same reference years and method.
- Machine-readable inventory and QA: `dataset-index.json`, `quality-report.json`; concise table: `quality-report.md`.

Offline rebuild and validation:

```sh
python3 scripts/process_data.py build
python3 -I scripts/process_data.py validate
npm test
npm run typecheck
npm run lint
npm run build
```

Refreshing the five point records, deliberately updating their manifest:

```sh
python3 scripts/process_data.py fetch-regions
python3 scripts/process_data.py build
python3 scripts/process_data.py validate
```

`all` refreshes automated sources and the five points, then builds and validates. Giovanni still requires an account-backed manual export and a deliberate update of its code-pinned checksum; existing observations remain reproducible offline. Build never fetches data. CI's existing `validate` step now verifies raw hashes, canonical values and normals, every derived export, the public bundle, and QA reports. Stale files fail validation.

## Contract and identity

`region_01` maps to the game's `phoenix`, a 2° box at 32.5–34.5°N and 113–111°W. The other five game IDs remain unchanged. Their spatial support is one POWER point/grid estimate at the recorded coordinates, not an average over the named broad region. The response gives a point and native 0.5° × 0.625° resolution, not exact selected-cell boundaries; those bounds are explicitly unavailable. Globe highlight circles are illustrative and are not sampling footprints.

The generated `EarthDataset` maps `unit` → `units`, and retains original IDs, provider kind, spatial metadata, source records, DOI where supplied, citations, retrieval time, processing, limitations, nulls, quality flags and raw checksum. Reanalysis stays `reanalysis`. Station and satellite map to Member 4's `observation`, with distinct `providerKind` labels retained. Each export's ID and productVersion include a content/processing fingerprint; changed records or processing require a new review. Exported provenance does not grant statistical approval.

## Units and annual interpretation

| Records | Monthly quantity | Native units → export | Archive annual mode |
| --- | --- | --- | --- |
| All POWER precipitation | Monthly total after conversion | mm/day × calendar days → mm/month | Equal-month mean of twelve monthly totals, mm/month; explicitly **not** annual accumulation |
| IMERG precipitation | Monthly total after conversion | mm/hr × 24 × calendar days → mm/month | Same equal-month mean |
| GSOM precipitation | Monthly total | mm/month unchanged | Local import uses equal-month mean; no station release in new Archive |
| POWER air temperature | Monthly mean | C → °C label only | Equal-month mean, °C |
| GSOM mean/daily high/daily low | Monthly mean, with provider definitions retained | °C unchanged | Equal-month mean, °C |
| POWER/GSOM humidity | Monthly mean; GSOM aggregation undocumented | % unchanged | Equal-month mean, %; station humidity is a cross-check only |
| POWER/GSOM wind | Monthly mean | m/s unchanged | Equal-month mean, m/s |
| POWER soil wetness | Model index | fraction (0–1) unchanged | Equal-month mean of the index |

Leap years are used for rainfall conversion. True zero stays zero; fill values become null; annual POWER month 13 is discarded. Annual mode omits any year without twelve valid months. Anomaly mode uses available calendar-month averages over the selected years and remains descriptive. Fixed-month mode preserves a partial endpoint year when that month exists. Day weighting or annual accumulation would be a different analysis configuration requiring review. `/inferences` already sums complete-year precipitation to mm/year; its units differ deliberately from Archive equal-month means.

IMERG ends September 2025: 333 months, 27 complete years, 1998–2024. The complete-year comparison overlap with MERRA-2 is 1998–2024. Station wind has 36 missing months; humidity has 314 and undocumented monthly completeness/aggregation. Missing or partial endpoint months are padded with null by Member 4, not treated as zero. Every export states its actual period and footprint.

## Source and access decisions

The central register remains `docs/data-sources.md`. NASA POWER/MERRA-2 records carry the NASA POWER acknowledgement and Gelaro et al. MERRA-2 citation. IMERG retains its product DOI, Huffman et al. citation and the Giovanni acknowledgement. The public Archive renders citations and provenance. POWER point requests require no credentials; no numerical rate limit is assumed. Downloads are sequential with retries. Giovanni extraction requires Earthdata Login; no token is bundled.

NASA reuse permission follows the existing central register's verified open-data decision; this handoff does not infer permission from public accessibility. The current NASA Earthdata policy page returned 403 to the research tool during this change, so that fetch is not new permission evidence. NOAA GSOM's [official metadata](https://www.ncei.noaa.gov/metadata/geoportal/rest/metadata/item/gov.noaa.ncdc:C00946/html) confirms the product citation, DOI 10.7289/V5QV3JJ5 and liability conditions, but does not supply an explicit redistribution license. Keep UNKNOWN pending a product-specific determination; do not infer it from NOAA's general content policy. No permission request was sent.

## Scientific review still required

All 38 exports have `reviewStatus: pending-member-4`. No real record has `assumptionsReviewed` set. Member 4 must assess the exact ID/version, variable, period, time mode/calendar month, footprint and processing, plus gaps, dependence, linearity, variance, outliers, discontinuities and test selection. Data QA and numerical fixture tests are not that assessment.

The Archive uses `analyzeDataset` without a review: descriptive direction/rate remain visible, p-values are unavailable, and significance stays unestablished. A short record still exposes its usable values without inventing a test. Filing descriptive evidence can satisfy the narrative report; it awards no significance-related score. Filed evidence stores ID/version, source kind, years, time mode/month and the actual descriptive verdict. Unversioned earlier evidence must be re-filed because its data/configuration cannot be verified.

The final Member 4 review outcome and any approved configurations remain pending. No team approval has been inferred. Satellite/reanalysis history cannot validate simulated vegetation or prove causal effects. MODIS/AppEEARS remains deferred; no vegetation observations were invented.

## QA and verification

Tests compare a Phoenix precipitation month with area-weighted raw POWER cells and an IMERG month with Giovanni's original mm/hr CSV, including leap February 2000. All 13 Phoenix exports preserve their canonical observation arrays and source footprints. Tests exercise the actual Member 4 validator on all 38 files, partial IMERG endpoints, nulls, equal-month precipitation means, normals, exclusion of generated/unknown-rights records, review status and versioned evidence filing.

Existing monthly cross-source checks are retained: temperature r=0.997, POWER–station rain r=0.821, humidity r=0.955, POWER–IMERG rain r=0.967 and IMERG–station rain r=0.844. Shared seasonality can inflate these correlations; they do not establish matching trends or causation. Tropical point rainfall uses a broad 0–3,000 mm/month physical screen because Phoenix's 600 mm/month local screen is unsuitable for monsoons. This is an input sanity screen, not a climatological or inferential review.

Verification on this checkout: 80 Vitest tests pass, typecheck and lint pass, isolated Python data/release validation passes, and the production build passes. The build retains the existing Atkinson Hyperlegible fallback-font warning. An initial sandboxed build failed because Turbopack could not bind its worker port; a permitted build with a fresh compiler cache passed.

Browser QA verified recruitment, generated lab normals, a discovery, Phoenix reanalysis, IMERG source selection and September 2025 endpoint, exclusion of the incomplete annual year, filing descriptive evidence, report unlock, a South Asia point and a short three-year record with readable values. Statistical review remains unestablished throughout. The existing development server was reused and not stopped or reconfigured.
