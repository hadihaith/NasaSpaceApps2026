# Historical evidence for the Unity field scene

Run `node scripts/prepare-unity-simulation.mjs` from the repository root. The exporter writes `unity/Homecoming/Assets/FieldSimulation/Data/Climate.json` offline from the pinned Member 3 archive, the original raw source files and the supplemental POWER point in central Chile. Run the same command with `--check` to check that the asset is current.

## Unity integration

The root has `schemaVersion: 2` and retains `regions`, `controls`, `discoveries`, `requiredDiscoveries` and `evidence`. The six entries in `regions` still have the existing gameplay normals. Historical locations, including Chile, resolve through each record's `region`, `regionLabel` and `footprint`. Chile is a historical comparison location and has no scenario normals.

- `evidence[]` retains `id`, `region`, `key`, `variable`, `unit`, `provider`, `product`, `version`, `url`, `aggregation`, `years`, `slope` and `hasSlope`. Use the added `sourceParameter`, `definition`, `fingerprint`, `rawSha256`, `footprint`, `provenance`, `quality` and `observations` for inspection and attribution.
- `finding` is the full-coverage annual lens. `monthFindings[0..11]` use January through December. `anomalyFinding` is the seasonal lens, with `baseline` describing the month-of-year reference. Each finding carries `id`, `datasetId`, `fingerprint`, `startYear`, `endYear`, `firstDate`, `lastDate`, `mode`, `month`, `aggregation`, `unit`, `points[]`, `n`, `slopePerDecade`, `magnitude`, `direction`, `status`, `reasons[]`, `test` and `review`. Each point has `date`, `t` in decimal years and `value`. Observations use `date`, `hasValue` and `value`; a missing observation has `hasValue: false` and `value: null`.
- `comparisons[]` contains the guided two-location case `rain-two-ways` and the Phoenix product cross-check `phoenix-rain-cross-check`. Each has `id`, `type`, `question`, `scope`, `leftRegion`, `rightRegion`, `leftDatasetId`, `rightDatasetId`, and findings `left`, `right` and `difference`. The three charts use identical timestamps. The difference uses left minus right on each common date. The historical comparison has no routing meaning.
- Inference `status` is `significant`, `not-significant` or `not-established`. Show `test` only for the first two. It carries the OLS estimate and confidence bounds in input units per decade, plus `alpha`, `p`, `method` and `hypothesis`. The descriptive rate is Theil–Sen and `magnitude` is that slope multiplied by the sampled span. When `hasSlope` is false, there is no descriptive estimate and `direction` is `unavailable`. For Unity's `JsonUtility`, gate nullable number fields by the corresponding status, `hasSlope` or `hasValue`.
- `footprint.type` is `grid-point` or `area-mean`. Draw a marker at the returned POWER sample coordinates for a point, using `footprint.point.longitude` and `footprint.point.latitude`. The response does not identify its source cell bounds. For an area, `footprint.bbox` is the request box, and `footprint.cells` contains POWER cell centers and cosine weights where available. IMERG reports a separate `dataBbox` of contributing cell centers.

The archived values and historical comparison results are calculated in TypeScript. Save selected finding and comparison IDs with their fingerprints or the filed values. Unity uses those values for display and report generation.

## Guided analysis design

The first historical pair is the existing Sahel–central Chile case. The pinned Sahel point (14°N, 6°E) and the central Chile point (36°S, 72°W) use the native-grid NASA POWER point endpoint, corrected MERRA-2 precipitation and the full common 1991–2025 span. The game asks whether the linear slope of paired annual Sahel minus Chile precipitation is zero. Use one two-sided OLS slope test at α = 0.05 if the paired residuals and product scope warrant it. Each year is the mean of twelve monthly precipitation totals in mm/month. The two component trends are contextual and receive their own eligibility decisions. The model estimates trends in this specific NASA POWER product at these locations. [POWER monthly service](https://power.larc.nasa.gov/docs/services/api/temporal/monthly/) · [MERRA-2 precipitation corrections](https://doi.org/10.1175/JCLI-D-16-0570.1)

Review the Phoenix temperature record, 1981–2025, as a separate single-record case on the six-preset path. Its hypothesis is a zero linear trend in complete-year mean 2 m air temperature. Use the full pinned window and the same primary test and alpha. The case has one prespecified test. [MERRA-2 system and production streams](https://gmao.gsfc.nasa.gov/gmao-products/merra-2/system-characteristics_merra-2/)

The connected POWER adapter and all other unreviewed views in the pack are descriptive. No step scans over other periods, modes or variables for p-values. Keep an unavailable test distinguishable from a reviewed result above alpha.

## Coverage and source inventory

| Archive location | Geometry in the source response | Monthly extent | Coverage in the pack |
| --- | --- | --- | --- |
| Central Arizona, POWER | 32.5°–34.5°N, 113°–111°W request box. 15 returned MERRA-2 grid centers at 0.5° × 0.625°. | 1981-01 to 2025-12 | 45 complete years for each of the five game variables |
| Central Arizona, IMERG Final V07 | Same request box. Giovanni returned cell-center bounds of 32.55°–34.45°N and 112.95°–111.05°W at 0.1° × 0.1°. | 1998-01 to 2025-09 | 333 months, 27 complete years. Annual 2025 omitted. |
| Central Sahel, POWER | Returned point at 14°N, 6°E | 1991-01 to 2025-12 | 35 complete years per game variable |
| Amazon, POWER | Returned point at 7°S, 61°W | 1991-01 to 2025-12 | 35 complete years per game variable |
| Eastern Mediterranean, POWER | Returned point at 39.7°N, 22.6°E | 1991-01 to 2025-12 | 35 complete years per game variable |
| South Asia, POWER | Returned point at 24°N, 79°E | 1991-01 to 2025-12 | 35 complete years per game variable |
| Eastern Australia, POWER | Returned point at 27°S, 145°E | 1991-01 to 2025-12 | 35 complete years per game variable |
| Central Chile, POWER | Returned point at 36°S, 72°W | 1991-01 to 2025-12 | 35 complete years of precipitation |

Bundled POWER units are °C for 2 m temperature, mm/month for precipitation, % for relative humidity, m/s for 10 m wind and a fraction for root-zone wetness. For these pinned inputs, each month within the reported extent has data. The exporter checks each raw SHA-256 and validates the returned POWER point coordinates before packaging. `evidence[]` is the per-variable coverage matrix with exact source versions, units, missing counts, licensing, request URLs and checksums. Array-form `bbox` and `dataBbox` fields use `[west, south, east, north]`. POWER cell weights are available before normalization. The source-spatial footprint and rendered terrain radius are independent. The six local station records remain in the Member 3 handoff index under unverified redistribution. [Member 3 quality report](member-3/quality-report.md) · [NASA POWER acknowledgements](https://power.larc.nasa.gov/docs/acknowledgements/)

All POWER anomaly charts in this pack subtract record-specific month-of-year means over the fixed 1991–2020 calendar baseline with 30 values for each month. IMERG coverage begins in 1998, so its seasonal lens uses the available months in the displayed period and identifies that reference as period-relative. Monthly anomalies are descriptive. Annual original-unit means require twelve observations and fixed-month points retain their original calendar years.

## Dataset-specific review

The implementation review used 35 complete common years for the rainfall designs and 45 for Phoenix temperature. The six diagnostics below were computed independently of the production TypeScript with SciPy 1.17.1 and statsmodels 0.15.0. Residual distribution, curvature and variance checks accompany the lag-1 and Ljung–Box dependence checks. Studentized residuals and Cook's distance were used to identify influential observations. The diagnostics have limited power at these sample sizes.

| Record | Lag-1 residual correlation | Ljung–Box lag 4 p | Breusch–Pagan p | Shapiro–Wilk p | Quadratic term p | Maximum absolute externally studentized residual / Cook's D |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Sahel precipitation | −0.104 | 0.101 | 0.024 | 0.392 | 0.002 | 3.14 / 0.43 |
| Chile precipitation | −0.123 | 0.688 | 0.025 | 0.974 | 0.068 | 2.49 / 0.18 |
| Sahel minus Chile | −0.316 | 0.250 | 0.403 | 0.513 | 0.938 | 2.32 / 0.14 |
| Phoenix 2 m temperature | +0.072 | 0.777 | 0.271 | 0.708 | 0.293 | 3.11 / 0.38 |

The individual Sahel and Chile slopes stay descriptive because the residual variance screen flagged both. The Sahel fit also shows curvature, and the Chile regression is sensitive to including a 2015 step. The CMAP/GPCP-based correction over Africa and CPCU-based correction over Chile use different input networks and spatial scales. The paired test addresses trends in the reported product series and inherits those limitations. Single-step sensitivity checks for 2000, 2009, 2010 and 2015 yielded paired step p-values of 0.609, 0.540, 0.704 and 0.463 with linear time in each model. In the paired model, the OLS rate remained +21.65 to +24.04 mm/month per decade when dropping each year in turn. A lag-3 HAC sensitivity interval was +18.02 to +27.66 mm/month per decade. The paired lag-1 residual correlation falls just inside the existing dependence screen. Its ordinary OLS interval is conditional on the reviewed residual assumptions. [MERRA-2 system characteristics](https://gmao.gsfc.nasa.gov/gmao-products/merra-2/system-characteristics_merra-2/) · [MERRA-2 precipitation corrections](https://doi.org/10.1175/JCLI-D-16-0570.1)

For Phoenix temperature, individual checks for potential steps in 1998, 2000, 2009, 2010 and 2015 yielded p-values of 0.737, 0.585, 0.286, 0.956 and 0.179. The larger residual warrants a sensitivity check. The rate remained +0.451 to +0.514 °C per decade when dropping each year in turn. The lag-3 HAC sensitivity interval was +0.357 to +0.578 °C per decade. This review covers the archived MERRA-2 area mean and its linear trend. Additional sources would be required to distinguish local climate from changes in the observing system or to attribute a cause.

| Finding | Theil–Sen rate per decade | Fitted change | Primary OLS slope p | OLS 95% slope interval per decade |
| --- | ---: | ---: | ---: | ---: |
| Sahel precipitation, 1991–2025 | +2.13 mm/month | +7.25 mm/month | Unavailable | Unavailable |
| Chile precipitation, 1991–2025 | −18.84 mm/month | −64.06 mm/month | Unavailable | Unavailable |
| Paired Sahel minus Chile, 1991–2025 | +24.65 mm/month | +83.79 mm/month | 3.23 × 10⁻⁸ | +16.36 to +29.33 mm/month |
| Phoenix 2 m temperature, 1981–2025 | +0.509 °C | +2.239 °C | 2.14 × 10⁻⁹ | +0.343 to +0.593 °C |

The Phoenix precipitation cross-check has 333 common reported calendar months from January 1998 through September 2025. On the 27 complete shared years from 1998–2024 the Theil–Sen rates are +0.24 mm/month per decade for POWER, +0.43 for IMERG, and −0.58 for the paired POWER-minus-IMERG series. Comparing these estimates describes sensitivity to product choice over the selected footprint and period. Satellite-gauge and reanalysis products can share observational inputs. [IMERG Final V07 dataset](https://doi.org/10.5067/GPM/IMERG/3B-MONTH/07)

## Connected custom locations

Request a descriptive finding with `GET /api/timeseries?format=evidence&variable=PRECTOTCORR&lat=33.5&lon=-112&size=2&startYear=1998&endYear=2024&mode=annual`. The other lenses use `mode=month&month=1` for January and `mode=anomaly` for seasonal departures. The result includes the analysed series, dated nullable observations, version and checksum, reported coordinates or weighted returned cells and the selected finding. Each request covers one catalog variable. The server caches successful and in-flight requests by parameter, footprint and dates. Longitudes wrap at the antimeridian, regional requests snap to half-degree edges and boxes near the date line or poles slide inward. Use the returned footprint for display. With no explicit end year the route can try the previous year once if the newest year is unavailable. Requests without `format=evidence` keep the web explorer's native-unit `monthly` array.

The API returns a clear provider or validation error when a request cannot be served. Keep already filed findings linked to the pinned asset or to the exact connected payload that was saved. The Unity client and save integration for custom requests are separate from this data adapter.
