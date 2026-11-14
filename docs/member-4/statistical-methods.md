# Member 4 — statistical methods

Implementation: `src/lib/stats/trend.ts`, `src/lib/series.ts`, `src/lib/analysis.ts`.
Standalone demonstration: `/member-4`, implemented by `DetectiveDashboard.tsx`.
These TypeScript names preserve the existing project's structure; they replace the proposed `trends.js` / JSX naming without adding a second statistics engine.

## Challenge mapping

The [official challenge](https://www.spaceappschallenge.org/2026/challenges/earth-system-trend-detective/) asks what changes, where, how much and whether changes are significant.
Variable, units and region come from Member 3's metadata. Member 4 supplies time processing, robust rate, fitted magnitude, conditional inference, diagnostics and scientific explanations. The generated examples do not satisfy the challenge's real NASA-data requirement; they demonstrate and validate this component until the team supplies verified data.

## Estimates and inference

- Time is measured in decimal years. Slopes have input units/year, and display rates multiply by ten for units/decade.
- OLS uses centered sums for the slope, residual sum of squares, standard error and Student t test with n−2 degrees of freedom. The test is two-sided, with a preselected alpha of 0.05 in the dashboard. Its null hypothesis is a zero linear slope. It assumes an appropriate linear mean and independent, homoscedastic, approximately normal residuals for the conventional finite-sample t inference. Model review is mandatory before a headline significance result.
- Theil–Sen is the median of all pairwise slopes; the intercept is median(y − slope × time). It provides a robust descriptive rate. The discrete slope confidence bounds follow the rank-variance order-statistic convention checked against SciPy `theilslopes(method="joint")`. These are conditional slope intervals, not prediction intervals or confidence bands for every plotted value. The previous shaded wedge has been removed.
- Mann–Kendall includes ties and continuity correction, using a normal approximation. It is supplementary, assumes independence, and is not combined with OLS into a new p-value or custom significance rule. The reported tau is S / [n(n−1)/2], i.e. tau-a; with tied values it is not SciPy's tau-b.
- Fitted magnitude is Theil–Sen slope × elapsed years between usable endpoints. It is not the measured last-minus-first change, a prediction, or a causal effect.
- The headline now uses one prespecified OLS test. The old requirement that OLS and Mann–Kendall both pass was a project heuristic, not a validated combined test. `borderline` remains in the legacy type for compatibility, but the new analysis does not generate it.
- Failure to reject zero slope does not prove no change. A p-value is not P(no trend), not P(result caused by chance), and not a measure of environmental importance.

## When significance is not established

All real imported datasets default to descriptive-only output until Member 4 supplies a review matching dataset ID, product version, time treatment, month (where relevant) and selected years. Scientific review is not inferred from provenance or redistribution permission. Simulated alien outcomes cannot receive observed-Earth significance claims.

Inference is also withheld for monthly-anomaly mode, irregular intervals/missing periods, near-zero residual variation, or flagged lag-one residual correlation. The exploratory correlation flag is |r1| > 1.96/sqrt(n). This is a screening heuristic, NOT a calibrated proof that remaining samples are independent. It cannot replace checking higher-order dependence, seasonal residuals, heteroscedasticity, outliers, nonlinearity, instrument changes or selection effects. The ten-point minimum is an application threshold, not a guarantee of adequate power or validity.

The legacy AR(1) sensitivity fields use n_eff = max(3, n(1−r)/(1+r)), where r is positive lag-one residual correlation clipped to [0, 0.99], and inflate the standard error by sqrt((n−2)/(n_eff−2)). They are explicitly exploratory. Negative correlation is not used to narrow uncertainty. This approximation is not the primary test and is not claimed as a generally valid autocorrelation correction. For correlated observations, the current release withholds significance rather than pretending this approximation solves dependence.

The deterministic synthetic examples have a known linear plus seasonal mean with independent Gaussian innovations (except the explicitly correlated example). Only those qualifying examples carry an automatic review. Their results remain educational simulations, never NASA findings. A random finite realization can still trigger the conservative screening flag.

## Calendar processing

- Input dates are consecutive YYYY-MM strings. Missing calendar months must be explicit null entries; duplicates, skipped date entries and non-finite values are rejected. The adapter pads partial first/last years with nulls.
- Annual mode uses the arithmetic mean of twelve complete monthly values. It is an equal-month mean, not a day-weighted annual mean or annual total. Member 3 must supply consistent monthly quantities and Member 4 must decide whether equal-month averaging is scientifically appropriate for that variable. Precipitation totals or rates must not be silently interchanged.
- Fixed-month mode uses one calendar month each year, including a partial endpoint year if that month is present.
- Anomaly mode subtracts each calendar month's available-value mean within the selected period. The baseline changes when the period changes. It removes the mean seasonal cycle for visualization, but does not remove dependence or validate inference. Missing or unbalanced samples can affect the estimated slope. A regression with seasonal terms or a separately validated seasonal test is future dataset-specific work, not claimed as implemented here.
- Missing observations are never filled, treated as zero, or compressed onto new timestamps. Annual years with missing months are omitted and any resulting internal gap blocks inference.

## Comparing regions

`comparePairedSeries` matches exact common timestamps, constructs A−B and fits its OLS trend. This retains contemporaneous spatial covariance. Its inference still requires independent temporal residuals, sufficient common dates, reviewed assumptions and compatible units/variables/time treatment. With incomplete overlap the slope is the common-period difference trend, not necessarily the subtraction of separately fitted full-period slopes.

The retained `compareSlopes` helper is an explicit independent-series approximation and defaults to no p-value. When independence is reviewed it uses Welch–Satterthwaite degrees of freedom, not summed degrees of freedom. Do not use it for neighboring regions merely because it is convenient.

## Multiple comparisons and interpretation

The controls support exploration. Tests across many variables, regions, months or date ranges are not adjusted for multiple testing; selecting a favorable p-value is not confirmatory evidence. Document a prespecified analysis or implement a scientifically selected multiplicity strategy before broad searches. The app does not infer causation, attribute trends to climate drivers, or forecast alien interventions.

## Validation and reproducibility

`scripts/generate-statistical-reference.py` independently generates checked-in reference values using SciPy 1.18.1 and seeded NumPy data. OLS slopes, intercepts, standard errors, p-values and 95% intervals are compared with `linregress`; Theil–Sen results and order-statistic intervals are compared with `theilslopes`. MK uses independent NumPy pair enumeration and SciPy normal tails because SciPy's Kendall tau test uses different continuity conventions. Fixtures include ties, irregular times, noise, a falling series and an outlier.

Tests also cover missing calendar data, short records, invalid input, review matching, correlation flags, paired differences and prevention of scoring unresolved results. Numerical agreement validates implementation, not suitability for any future dataset.

Reproduce: install the project's locked npm dependencies, run `npm test`, `npm run typecheck`, `npm run lint`, and `npm run build`. Python/SciPy are needed only to regenerate reference fixtures, never for browser use.

## Primary methodological references

- [SciPy linregress](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.linregress.html): regression and conventional slope test implementation.
- [SciPy theilslopes](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.theilslopes.html): robust slope, joint intercept and slope intervals; cites Sen (1968).
- [Hirsch, Slack and Smith (1982), USGS](https://pubs.usgs.gov/publication/70011649): seasonal trend analysis and the limitations of serial correlation.
- [Hirsch and Slack (1984), USGS](https://pubs.usgs.gov/publication/70012890): seasonal trend methods with serial dependence. Its full method is not implemented by this release.
- [NIST unequal-variance t-test](https://www.itl.nist.gov/div898/handbook/eda/section3/eda353.htm): Satterthwaite degrees-of-freedom principle; its application to slope standard errors here is an approximation.
- [Challenge-provided SeaWiFS paper](https://ntrs.nasa.gov/api/citations/20090042747/downloads/20090042747.pdf): environmental time-series context. No numerical result or general statistical guarantee from this paper is claimed for the implementation.

The supplied Medium and Statsig pages are supporting explanations, not authorities for the numerical implementation.
