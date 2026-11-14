# Operation: The Great Homecoming — science and gameplay plan

Planning handoff, 14 November 2026. This document proposes the investigation, evidence contract and acceptance checks for the demo and release. The repository evidence is pinned at commit `91c5dc4`.

## Challenge and current state

NASA's challenge asks players to select an Earth science variable and location, visualize a long time series, estimate its rate of change and assess statistical significance. The two reviewed cases below cover these questions with a comparison and a single-region investigation. [NASA Space Apps challenge](https://www.spaceappschallenge.org/2026/challenges/earth-system-trend-detective/) · [Challenge objectives, staging copy](https://staging-web-v2.spaceappschallenge.org/2026/challenges/earth-system-trend-detective2/)

- Unity's schema 2 `Climate.json` contains 32 historical records, six simulation regions and two packaged comparisons. The exporter uses the TypeScript historical analysis and review registry, verifies raw checksums and point coordinates, then packages annual, calendar-month and anomaly findings. [Exporter](../scripts/prepare-unity-simulation.mjs) · [Evidence inventory and review](homecoming-evidence.md)
- The browser Homecoming Archive reads schema 1 `earth-archive.json`, offers region, variable, source, period and time-view selection, and files descriptive findings. It compares variables at one location and does not consume the reviewed pair or Phoenix review. The two packs share 31 records with identical observations and raw hashes. Chile is Unity-only, while surface soil wetness is web-only. [Browser Archive](../src/homecoming/Archive.tsx)
- Unity already supports the paired rainfall flight, a report and a stateful Arizona scenario. Its case currently locks calls after filing, and its report puts the paired finding on a separate line. The planned revision step and explicit paired test details require work. [Unity guide](../unity/README.md) · [Case interface](../unity/Homecoming/Assets/FieldSimulation/Runtime/FieldCaseInterface.cs)
- The separate `/detective` preset named `rain-two-ways` uses a 10° box centered at 14°N, 2°E and live requests. It is a different investigation from the pinned Sahel point. The flagship entry must identify and load the pinned case explicitly. [Case catalog](../src/lib/catalog.ts)
- The web Homecoming lab uses illustrative indices. Unity's Arizona model uses a separate water-balance implementation. A shared ET arithmetic fixture exists, while equivalent stateful simulations and a full shared schema remain open. [Web model](../src/homecoming/model.ts) · [Unity model](../unity/Homecoming/Assets/FieldSimulation/Runtime/LocalWaterBalance.cs)

## Flagship case — two places, one measurement

Ask whether the 1991–2025 precipitation series changed at the same rate at the central Sahel and central Chile points. Use the pinned NASA POWER `PRECTOTCORR` MERRA-2 records at 14°N, 6°E and 36°S, 72°W. Each has 420 complete monthly values. Convert the source mm/day rates using the actual number of calendar days, then calculate each annual point as the equal-month mean of 12 monthly totals. The resulting unit is **mm/month**, and 35 years are matched. POWER returns point estimates at native grid resolution. Its response does not supply exact cell bounds. [NASA POWER monthly service](https://power.larc.nasa.gov/docs/services/api/temporal/monthly/)

1. Show the locations, variable, years and unit. Ask the player to call rising, falling or little change at each point before the reveal. Retain the existing ±0.1 mm/month per decade definition for little change and show it with the choices.
2. Reveal all 35 values on common year and precipitation axes, with distinct line styles and labels, a synchronized year cursor and a table. Permit flight to be skipped. Show the full series before inviting the player to inspect individual years.
3. Display the descriptive Theil–Sen rates and fitted changes. Offer **Keep my call** and **Revise my call**. Save the original and final calls independently, and allow either choice to proceed.
4. Show the matched annual Sahel-minus-Chile series and the reviewed two-sided OLS test of a zero slope. Explain that the positive paired trend describes the change in the difference between the two product series. File the two component findings and the paired finding together.
5. Present the report with the variable, points, years, aggregation, rates, paired hypothesis, test, interval, status, provenance and player interpretation. Submit it to the hearing and leave the evidence available for later review.

| Finding | Theil–Sen rate per decade | Fitted change over 34 years | Inference |
| --- | ---: | ---: | --- |
| Sahel point | +2.133 mm/month | +7.253 mm/month | Significance not established |
| Chile point | −18.840 mm/month | −64.057 mm/month | Significance not established |
| Sahel minus Chile | +24.645 mm/month | +83.794 mm/month | Reviewed paired OLS test is significant |

The paired OLS estimate is **+22.844 mm/month per decade**, with a 95% slope interval of **+16.362 to +29.326 mm/month per decade**, **p = 3.23 × 10⁻⁸**, and **α = 0.05**. The Theil–Sen and OLS rates are separate estimates. Fitted change is the Theil–Sen slope multiplied by the 34-year span. It is a fitted change and does not equal the difference between the observed endpoint values.

The individual tests are withheld because their residual variance screens flagged concerns. The Sahel review also found curvature, and the Chile review found sensitivity to a possible 2015 step. The paired review examined residuals and sensitivity, with limited diagnostic power at 35 years. Its OLS interval depends on the reviewed assumptions. Africa and Chile use different precipitation correction inputs and spatial scales, so retain the product-specific scope alongside the result. These findings do not establish a common cause. [Dataset review](homecoming-evidence.md#dataset-specific-review) · [MERRA-2 precipitation method](https://journals.ametsoc.org/abstract/journals/clim/30/5/jcli-d-16-0570.1.xml)

## Second case — Phoenix temperature

Offer a separate guided case for complete-year mean 2 m air temperature in the Phoenix and central Arizona POWER area mean, 1981–2025. The request box is 32.5–34.5°N, 113–111°W, with 15 cosine-weighted MERRA-2 cells and 45 complete years. Ask for a direction, reveal the series, permit revision and file the single reviewed finding.

The descriptive Theil–Sen rate is **+0.509 °C per decade**, with **+2.239 °C** fitted over 44 years. The reviewed two-sided OLS estimate is **+0.468 °C per decade**, with a 95% interval of **+0.343 to +0.593 °C per decade** and **p = 2.14 × 10⁻⁹** at α = 0.05. The result describes the archived reanalysis area mean. The review identifies sensitivity and observing-system limitations. [Evidence review](homecoming-evidence.md#dataset-specific-review) · [NASA MERRA-2 system characteristics](https://gmao.gsfc.nasa.gov/gmao-products/merra-2/system-characteristics_merra-2/)

## Arizona soil scenario

Label the scenario and every resulting report **conditional simulation**. Start from the Unity Arizona campaign's 1991–2020 monthly normals and a 20-year spin-up. The teaching model uses a 150 mm uncalibrated bucket, FAO-56-inspired reference evapotranspiration, an estimated temperature range and a representative 600 m elevation. Its synthetic calendar starts in January 2026. Ground imagery is illustrative. [Unity model assumptions](../unity/Homecoming/Assets/FieldSimulation/Runtime/LocalWaterBalance.cs) · [FAO-56](https://www.fao.org/4/x0490e/x0490e06.htm)

File the Arizona rainfall record and predict the end-of-year storage change relative to inherited weather. Set precipitation to +100%, keep the other controls at inherited values, preview 12 months and advance. From the committed state, keep precipitation at +100%, change only temperature to +4 °C, preview and advance another 12 months. Show same-start reference and edited values for each run, with ±1 mm defining little change. Preserve the prediction, both runs, model version, inputs and parent revisions. A player can complete the case after an incorrect prediction by explaining the result.

An independent calculation using the shipped normals and the existing February ledger fixture gives approximately **+23.44 mm** for the first run and **−8.10 mm** for the second, each compared with its own inherited-weather reference. Treat these as candidate fixture values until checked against a clean Unity player. The first run ends near 46.87 mm and the second starts there. Do not describe these values as measured root-zone storage or as a forecast. Other Unity presets currently use illustrative indices. The Potomac routing screen and Phoenix forecast replay remain research checks. [Routing and forecast review](homecoming-routing-forecast.md)

## Open investigation and evidence experience

Allow players to choose available regions, variables, products and annual, calendar-month or anomaly views. They can file a finding and a compatible comparison, or document why a comparison is unavailable. They can continue exploring after the hearing. A connected custom POWER request is descriptive and should retain the exact returned payload and footprint with a saved finding. The Unity custom-location client and save integration remain future work. [Connected adapter](homecoming-evidence.md#connected-custom-locations)

Every evidence view should expose variable definition, product and version, point or area footprint, time standard, date window, aggregation, units, usable points, excluded years, source URL, retrieval date, processing, limitations, raw checksum and finding fingerprint. Point markers use returned coordinates. Area displays use the request box and identify contributing cells when supplied. Preserve actual missing values and zero values distinctly.

Display three inference states explicitly. **Significant** and **not significant** require a matching reviewed test. **Significance not established** includes a reason and no p-value or interval. A short record can show its values even when fewer than ten usable points prevent a slope. Changed dates, mode, month, data, footprint or product invalidate the matching review until reviewed again. Incomplete annual years are excluded, missing periods are visible, and incompatible units, aggregation or spatial support block a paired inference with an actionable explanation.

POWER anomaly views use the fixed 1991–2020 calendar-month baseline when complete. IMERG uses a labeled period-relative baseline. The Phoenix POWER–IMERG cross-check has 333 shared months through September 2025 and 27 complete shared years, 1998–2024. Its descriptive rates are +0.24, +0.43 and −0.58 mm/month per decade for POWER, IMERG and POWER minus IMERG respectively. Show product footprints and sampling differences, and retain an unestablished inference status. [Evidence inventory](homecoming-evidence.md#coverage-and-source-inventory) · [NASA IMERG V07 collection](https://search.earthdata.nasa.gov/search?q=GPM_3IMERGM_07)

Use labeled controls, visible keyboard focus, accessible chart descriptions and complete data tables. Distinguish series with text and line style as well as color. Support keyboard-only case completion, meaningful focus after reveal and skip, screen-reader status announcements, captions, reduced motion, touch and a muted route. Test web against WCAG 2.2 and test Unity's equivalent interaction path. [WCAG 2.2](https://www.w3.org/TR/WCAG22/)

## Shared contracts and parity

Extend the existing exporter to provide both clients with one versioned historical evidence payload derived from the same inputs, analysis and review registry. Preserve dataset IDs, raw hashes, fingerprints, point arrays, dates, units, aggregation, review IDs and status. A finding carries its descriptive estimate and fitted change separately from the OLS test estimate, interval, p-value and hypothesis. A comparison carries ordered left and right dataset IDs and fingerprints, common dates, left-minus-right points and the reviewed difference finding. Define nullable numeric semantics explicitly for Unity's `JsonUtility`.

A case record carries original and revised calls, evidence IDs, interpretation and completion state. A report embeds or pins the exact evidence values and both fingerprints so it remains reproducible after data updates. Scenario records carry campaign, branch, source and committed revision IDs, dates, model and data versions, forcing, reference and edited results. Preserve legacy saves through an explicit migration and keep demo progress isolated from existing campaign state.

Do not infer full numerical parity from the existing shared ET fixture. A shared stateful scenario requires the same monthly equations, inputs, initial state, calendar, precision rules and versioned fixtures in both runtimes. Until that exists, identify each client model and its output on screen and in reports.

Golden checks should compare all 35 matched rainfall dates and values, each difference as Sahel minus Chile, both descriptive rates, the paired OLS slope, p-value and interval, status, provenance and display rounding. Check all 45 Phoenix annual values and its reviewed test separately. Verify raw checksums and leap-month conversions. Exercise incomplete years, missing values, short series, changed review fingerprints, incompatible footprints and products, absent review, and a reviewed result above alpha. Assert no stale test survives a changed scope. Compare numerical values before display rounding with an agreed tolerance appropriate to Unity float serialization, then compare formatted output exactly.

For scenario parity, verify a neutral run has zero difference, a discarded preview leaves the revision unchanged, commit is idempotent, stale previews fail safely, and the second run inherits the first ending state. Compare monthly ledger closure, dates, forcing and final results against a versioned golden fixture. Verify reload, branch isolation and legacy migration. Existing tests and preview assertions provide a starting point. [Historical tests](../src/lib/earth/historical.test.ts) · [Unity validation](../unity/Homecoming/Assets/FieldSimulation/Editor/FieldRuntimeValidation.cs)

## Demo and release handoff

**Player steps**

1. Open the pinned rainfall case and call a direction for the Sahel and Chile.
2. Reveal the matched 1991–2025 series on common axes, inspect a year or the table, and keep or revise each call.
3. Open the paired difference, review its OLS significance result, file all three findings and submit the report to the hearing.
4. Offer Phoenix temperature as the next historical case. Offer the Arizona scenario as a separate labeled activity with the two committed runs described above.

**Claims to freeze**

- Sahel **+2.13**, Chile **−18.84**, and paired Theil–Sen **+24.65 mm/month per decade**. The paired OLS slope is **+22.84**, its 95% interval **+16.36 to +29.33**, and **p = 3.23 × 10⁻⁸**. The individual tests are unavailable.
- Phoenix temperature has 45 complete years, descriptive **+0.509 °C per decade**, reviewed OLS **+0.468 °C per decade**, 95% interval **+0.343 to +0.593**, and **p = 2.14 × 10⁻⁹**.
- The Arizona model's provisional first and second run deltas are approximately **+23.44 mm** and **−8.10 mm** relative to their respective inherited-weather references. Confirm these in the actual player before using them in a recording.

**Release acceptance**

- Web, Unity, filed reports and capture agree on the pinned case identity, 35 dates, plotted values, units, estimates, inference states and provenance. The review is withheld when identity or scope changes.
- A clean player can call, reveal, revise, file, reopen and submit the paired result. Wrong calls and an explained coverage gap remain valid outcomes. Repeated submission produces one report and hearing transition.
- Seeded campaign saves and reports survive demo play, refresh, restart and exit. Offline Unity play reaches the report and hearing. A provider error leaves the pinned case usable and gives connected requests a clear failure state.
- Complete keyboard, screen-reader, touch, captions, muted and reduced-motion checks. Confirm shared axes and tables at the target viewport sizes.
- Run `npm test`, `npm run typecheck`, `npm run lint`, `python -I scripts/process_data.py validate`, `npm run build` and `node scripts/prepare-unity-simulation.mjs --check`. Run Unity model and campaign validation, story playback smoke and `--investigation-preview`, followed by a clean story-first player walkthrough. Record the exact build and evidence fingerprints used for capture.

## Verification performed for this plan

The checked-in pack's 32 raw SHA-256 values matched their source files. The two rainfall points' 840 monthly conversions were checked against raw POWER values and calendar days. Phoenix's 540 temperature months were reproduced from its 15 raw weighted cells to the archive's 0.01 °C rounding. Independent Theil–Sen and SciPy OLS calculations reproduced the rainfall and Phoenix estimates and intervals above. Matched rainfall timestamps and every left-minus-right value agreed with the pack. No build, automated suite or Unity player was run during this planning audit. The existing project plan records prior checks and outstanding clean story-first and keyboard-only verification. [Existing release checks](homecoming-implementation-plan.md#j--verification-and-delivery--p0p1p2--qa--each-owner--start-fixtures-after-a4)
