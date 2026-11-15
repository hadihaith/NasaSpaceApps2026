# Downstream routing and forecast verification

## Potomac River screening

HydroRIVERS v1.0 and level-12 HydroBASINS v1c agree on the downstream order **Paw Paw → Hancock → Point of Rocks**. The gauge positions were snapped to the nearest reach within 0.02° and 5% of USGS drainage area. For each downstream stop, the contributing area excludes every HydroBASIN already assigned upstream. Because each gauge falls within a level-12 polygon, the footprints extend to polygon outlets. The cumulative areas exceed the reported USGS gauge areas by 0.7–1.5%.

| Gauge | USGS station | HydroRIVERS reach | Incremental area (km²) | Cumulative area (km²) | Reported area (km²) |
| --- | --- | --- | ---: | ---: | ---: |
| Paw Paw | 01610000 | 70581123 | 8,223 | 8,223 | 8,104 |
| Hancock | 01613000 | 70576833 | 2,400 | 10,622 | 10,526 |
| Point of Rocks | 01638500 | 70587709 | 14,549 | 25,171 | 24,996 |

The Hancock snap was 427 m from the gauge. Matching contributing area distinguishes the main stem from nearby tributary reaches. Between Paw Paw and Hancock, the catchment includes Cacapon River tributary inflow. Between Hancock and Point of Rocks, tributaries include the Shenandoah. USGS documents effects from hydropower and operation of Savage River Reservoir and Jennings Randolph Lake, mostly at low flow, and historical revisions to gauge datums and drainage areas. All 9,132 days per gauge were available for the common window with USGS approval, including 283, 322 and 371 estimated days at the respective gauges. The screening model aggregates those measurements without attempting to reproduce operations, ice or snowmelt. [HydroRIVERS](https://www.hydrosheds.org/products/hydrorivers) · [HydroBASINS](https://www.hydrosheds.org/products/hydrobasins) · [USGS Potomac station records](https://pubs.usgs.gov/wdr/WDR-WV-01-1/pdf/potomac.pdf) · [USGS Hancock station remarks](https://waterdata.usgs.gov/nwis/uv?legacy=1&site_no=01613000) · [USGS Point of Rocks station remarks](https://waterdata.usgs.gov/nwis/uv?agency_cd=USGS&legacy=1&site_no=01638500)

NASA POWER returned 30 native-resolution MERRA-2 cells for corrected precipitation. Each cell's mm/day rate was converted to mm/month using calendar days, including leap days. Area-weighted overlap in EPSG:5070 gave each incremental catchment its own forcing. The routing audit applies a fitted fraction of precipitation to each local linear reservoir and passes its release, plus upstream inflow, downstream. Each downstream reach can retain part of its incoming water for later months. Volume accounting covers precipitation assigned to runoff, precipitation outside the runoff model, storage, and outlet export. January 2000–December 2001 warmed the reservoirs. January 2002–December 2014 fitted parameters and climatology. January 2015–December 2024 supplied 120 withheld months per gauge. The screen required at least 5% improvement in monthly mean discharge MAE over the fitted calendar-month climatology and, downstream, a separately fitted alternative that delivers upstream inflow in the same month. [NASA POWER monthly service](https://power.larc.nasa.gov/docs/services/api/temporal/monthly/)

| Gauge | Screened model MAE (m³/s) | Seasonal baseline MAE (m³/s) | Same-month inflow MAE (m³/s) |
| --- | ---: | ---: | ---: |
| Paw Paw | 56.76 | 48.74 | |
| Hancock | 70.80 | 60.09 | 70.80 |
| Point of Rocks | 149.22 | 138.60 | 149.22 |

Both fitted downstream reaches released all incoming water within the month. These fits offer no evidence for a submonthly travel time. The network ledger closed to the script’s relative tolerance of 10⁻¹². The basin-specific runoff transformation and downstream perturbation remain unvalidated. The playable globe path uses the compatible historical rainfall comparison. Game scenarios describe local conditional results.

### Reproduce the Potomac screen

Save the following responses to a local folder. The script reports the exact input hashes, selected river and basin paths, fit parameters, MAEs and ledger. The large HydroSHEDS archives remain with the provider and use the HydroSHEDS v1 license. See the official terms for distribution requirements. [HydroSHEDS license, appendix A](https://data.hydrosheds.org/file/technical-documentation/HydroSHEDS_TechDoc_v1_4.pdf)

| Filename | Source |
| --- | --- |
| `rivers.zip` | [North America HydroRIVERS v1.0](https://data.hydrosheds.org/file/hydrorivers/HydroRIVERS_v10_na_shp.zip) |
| `basins.zip` | [North America level-12 HydroBASINS v1c](https://data.hydrosheds.org/file/hydrobasins/standard/hybas_na_lev12_v1c.zip) |
| `gauges.rdb` | [USGS site metadata](https://waterservices.usgs.gov/nwis/site/?format=rdb&sites=01610000,01613000,01638500&siteOutput=expanded) |
| `streamflow.rdb` | [USGS daily means 2000–2024](https://waterservices.usgs.gov/nwis/dv/?format=rdb&sites=01610000,01613000,01638500&startDT=2000-01-01&endDT=2024-12-31&parameterCd=00060&statCd=00003) |
| `power.json` | [NASA POWER native-grid corrected precipitation 2000–2024](https://power.larc.nasa.gov/api/temporal/monthly/regional?parameters=PRECTOTCORR&community=AG&longitude-min=-80.25&longitude-max=-76.75&latitude-min=37.75&latitude-max=40.25&start=2000&end=2024&format=JSON) |

```sh
python -m pip install 'shapely>=2.1' 'pyshp>=3' pyproj
python scripts/homecoming-validation/routing.py /path/to/downloads
```

NASA and USGS may revise their responses. The routing screen used downloads from 2026-11-14. The SHA-256 digests were `47c2e30041b1a6e07ba3c1d82f07c70f2bfdd376a263153699902cf96ea006ea` for HydroRIVERS, `687c3e54f59b5d8d4548af6d8f72f762bf86e3c4ceadf6cb56302d8142ed3911` for HydroBASINS, `23fcb25fc8c4b5fc32e0164e7495605977ad72d257d6384313b1246ebdc857c2` for sites, `69dc3bc1f6353c99d65a216b15f686fc2f8ef5b610598a3837249bcb242f2849` for discharge and `292f1e842e51f836e458834ded5db1ddd66441e94f3c37287c3f5d4a763623f7` for precipitation. The RDB payloads can include changing retrieval metadata.

## Potomac daily routing candidate

**Release decision · Fail.** The daily model improved monthly flow estimates against climatology at Paw Paw, Hancock and Point of Rocks. At Hancock, the delayed model's monthly error was 1.36% higher than the independently fitted same-month inflow alternative's error. The release demo keeps the historical comparison flight. The [versioned research audit](../scripts/homecoming-validation/fixtures/potomac-daily-v1.audit.json) records the failed gate and paired scenario values.

### Evaluation design

Replaying the original screen with the 2026-11-15 downloads reproduced every published MAE to two decimals, both zero-retention reach fits, the approved and estimated gauge day counts, and a zero network balance residual. The HydroSHEDS archive hashes matched the original screen. The three API payload hashes differed. The versioned audit pins the raw bytes of the new downloads.

The candidate uses the same incremental catchments, gauge observations and calendar windows. January 2000–December 2001 initializes empty stores, January 2002–December 2014 sets parameters and climatology, and January 2015–December 2024 verifies the frozen fits over 120 months and 3,653 days per gauge. The accepted metric remains the mean absolute error (MAE) of calendar-month mean discharge in m³/s. Skill is `1 - model MAE / reference MAE`. Every gauge needs at least 5% skill against its training-period calendar-month mean, and every downstream gauge needs at least 5% against the same-month inflow alternative. A daily reach also needs a nonzero delay or retention and lower withheld daily MAE than the bypass. The pulse must match the analytic checks and increase at least one monthly mean at each gauge by 0.01 m³/s or more, the chosen display resolution.

The candidate replaces the constant runoff fraction with a daily snow and soil-water model. It follows the snowmelt and wetness-dependent recharge structure of HBV, with two parallel linear stores for fast runoff and slower baseflow. Area overlap on the baseline grid gives each catchment separate precipitation, temperature and Hargreaves reference evapotranspiration (ET). The model uses the midpoint of daily minimum and maximum temperature for rain/snow partitioning at 0 °C and melts stored snow at up to 3 mm per °C per day. Actual ET decreases as the soil store drops below 70% of capacity. The slow store releases 3% of its contents per day. A channel link delays upstream volume by a whole number of days and then retains a fixed fraction. Daily routing is justified by Potomac field studies that found travel on the scale of days. Those tracer studies give context for the time step. The candidate's fitted discharge response can differ from tracer travel time. [HBV snow and soil equations](https://hess.copernicus.org/articles/16/3315/2012/hess-16-3315-2012.pdf) · [FAO-56 reference ET equations](https://www.fao.org/4/x0490e/x0490e07.htm) · [USACE lag routing](https://www.hec.usace.army.mil/confluence/hmsdocs/hmstrm/channel-flow/lag-model) · [USACE linear storage](https://www.hec.usace.army.mil/confluence/hmsdocs/hmstrm/transform/clark-unit-hydrograph-model) · [USGS Potomac travel-time study](https://doi.org/10.3133/wsp2257)

One grid search per gauge minimizes daily training MAE, working from Paw Paw downstream. It considers 324 local parameter sets, with storage capacities of 100–500 mm, recharge exponents of 1, 2 or 4, reference ET multipliers of 0.75, 1 or 1.25, and three settings each for the fraction of recharge that enters the fast store and the fraction that store releases each day. The two downstream searches use delays of 0–3 days and retentions of 0%, 25%, 50% or 75% per day. The same-month alternative uses the same simulated upstream series, passes it through on the same day so the incoming volume stays in its original month, and independently refits the local runoff parameters on the same training window. Withheld gauge flows score the runs after fitting.

### Withheld gauge results

| Gauge | Monthly model MAE (m³/s) | Climatology | Same-month inflow | Skill vs. climatology | Skill vs. same-month | Daily model / bypass MAE (m³/s) | Result |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Paw Paw | 33.57 | 48.74 | | +31.12% | | 42.38 / — | Pass |
| Hancock | 35.99 | 60.09 | 35.50 | +40.11% | −1.36% | 46.26 / 50.50 | Fail |
| Point of Rocks | 64.38 | 138.60 | 75.89 | +53.55% | +15.17% | 95.17 / 99.80 | Pass |

Hancock's link selected a one-day delay and 25% daily retention. The link to Point of Rocks selected a two-day delay and zero retention. The daily improvement at both links supports evaluating timing on the daily grid. Hancock's weaker monthly result controls the release decision. The baseline network accounted for 658.03 billion m³ of input as 465.64 billion m³ of ET, 5.83 billion m³ of terminal storage and 186.56 billion m³ of outlet export. The unrounded residual was −0.0000134 m³, within the relative tolerance of 10⁻¹².

### Paw Paw low-flow augmentation pulse

In a same-start simulation, adding water at Paw Paw raises the modeled flow there in August and at both downstream gauges in September. The intervention adds 10 m³/s to the August 31, 2019 daily mean at the Paw Paw model boundary, equivalent to 864,000 m³. The date puts the pulse on a month boundary so the display can distinguish the two months. Catchment weather, local runoff and initial stores are identical in both runs. Potomac operators use upstream reservoir releases to augment low flow. This scenario represents the amount already arriving at Paw Paw, with no model of the reservoir or the river above that gauge. [ICPRB Potomac drought operations report](https://www.potomacriver.org/wp-content/uploads/2014/12/ICPRB03-6.pdf)

| Gauge | First and peak response day | Peak daily mean increase (m³/s) | August mean increase (m³/s) | September mean increase (m³/s) |
| --- | --- | ---: | ---: | ---: |
| Paw Paw | 2019-08-31 | 10.00 | 0.323 | 0 |
| Hancock | 2019-09-01 | 7.50 | 0 | 0.333 |
| Point of Rocks | 2019-09-03 | 7.50 | 0 | 0.333 |

The Hancock store spreads the extra water over several days and lowers the peak. The fitted Point of Rocks link shifts that response two more days. All 864,000 m³ passes each gauge by year-end within numerical tolerance. A separate geometric impulse calculation matches the paired runs to 10⁻⁵ m³ per day, including the zero response ahead of the pulse and the positive downstream change. The additional network ledger closes to 2.33 × 10⁻¹⁰ m³. These values describe a model edit and its numerical checks. The test uses a synthetic input boundary and supplies no observed release-response estimate, weather forecast, intraday arrival time or historical trend verdict.

### Player path

In a fresh, isolated macOS player, **Investigate the rain alert** opened **Return to Sender**. An incorrect measurement choice showed an inline correction. The correct choices revealed each local trend, marked its map pin and advanced from Chile to the Sahel. **Compare records** opened the matched time series, with test details under **See the evidence**. The supported claim enabled **Send to Earth**, and the receipt's primary action opened **Weather and soil**. The playthrough used the keyboard. The missing paired-archive branch was checked in source and offers **Field station** to return to the other cases.

A future downstream entry can offer **Follow the modeled change** after a qualifying route and its effect data both load. The first result should give the same-start flow change and use the shared month to open the next gauge, with numeric values and methods under **See the evidence**. If the audit fails, or if the route or effect data is missing, the default action is **Investigate the rain alert**. In a flow the player has already opened, show “The downstream comparison is unavailable” with that same action. The research audit records `routePassesScreen: false`. The verifier named the missing files in an empty-folder test and the missing grid center in a partial-data test, with no audit output in either case. The release uses the bundled historical entry directly.

### Next test

Isolate the Hancock link using the Cacapon River gauge (01611500). First compare observed Paw Paw inflow plus separately measured Cacapon flow with the observed Hancock hydrograph. Subtract the Cacapon contributing area from the ungauged lateral catchment before modeling the remainder. Compare the fitted daily reach with a same-day reach on untouched dated release events, using operator release logs and USGS Barnum discharge (01595800) to identify upstream release changes. Score onset day, peak daily increment and event volume against a pre-event flow baseline on dry periods selected before fitting. Keep the measured-boundary diagnostic separate from an end-to-end rerun with simulated inflows and the existing monthly MAE gate. The historic 2015–2024 scores remain a comparison window, so reserve new events or years before any model selection. [USGS Cacapon gauge](https://waterdata.usgs.gov/monitoring-location/USGS-01611500/) · [USGS Barnum gauge](https://waterdata.usgs.gov/monitoring-location/USGS-01595800/)

### Reproduce the daily candidate

Start with the five monthly screen inputs. Fetch one daily NASA POWER point per native-grid center, without elevation correction. The 30 daily responses were retrieved on 2026-11-15 using POWER Daily API v2.10.3. The records cover 2000-01-01–2024-12-31 in POWER local solar time. USGS observations are approved daily means (parameter 00060, statistic 00003), including estimated values as in the original screen. The daily comparison aligns by reported date. POWER and gauge day windows can differ by hours, limiting interpretation of short travel times. Both land stores and reach parameters are lumped, so regulation, ice, terrain-dependent snow, flow-dependent travel and withdrawals are unresolved. The audit records the SHA-256 digest of each downloaded payload. The replay used Python 3.13.9. [NASA POWER Daily API](https://power.larc.nasa.gov/docs/services/api/temporal/daily/) · [USGS daily values](https://waterservices.usgs.gov/docs/dv-service/)

```sh
sources=/path/to/downloads
mkdir -p "$sources/daily"
jq -r '.features[].geometry.coordinates[:2] | @tsv' "$sources/power.json" |
while IFS="$(printf '\t')" read -r lon lat; do
    curl -fsSL --retry 2 "https://power.larc.nasa.gov/api/temporal/daily/point?parameters=PRECTOTCORR,T2M_MIN,T2M_MAX&community=AG&longitude=$lon&latitude=$lat&start=20000101&end=20241231&format=JSON&time-standard=LST" \
        -o "$sources/daily/$lon,$lat.json" || exit 1
done
python -m pip install numpy==2.5.4 pyproj==3.8.0 pyshp==3.1.8 shapely==2.2.0
python -B scripts/homecoming-validation/routing.py "$sources"
python -B scripts/homecoming-validation/potomac_daily.py "$sources" > /tmp/potomac-daily-audit.json
python -B -m unittest discover -s scripts/homecoming-validation -v
```

## Phoenix temperature benchmark replay

At 00:00 UTC on the first of each month from January 2015 through December 2024, replay picks the last archived NWS `CLMPHX` report version available for each completed month. Each issue has 12 target months beginning with the issue month. Persistence reuses the last reported temperature. Climatology uses the mean of all published reports for the target calendar month, with at least eight years per calendar month. Damped persistence adds the last observed anomaly to climatology and decays it with a fixed two-month e-folding time. No fit to the verification targets was used. The later NOAA GSOM station record supplies the 1,440 withheld site-month outcomes across all leads. Each individual lead has 120 matched outcomes. Months flagged for missing days are skipped. The same target month can appear in scores for different leads. [NWS archive format and timestamps](https://mesonet.agron.iastate.edu/nws/text.php) · [NOAA GSOM format](https://www.ncei.noaa.gov/pub/data/cdo/documentation/GSOM_documentation.pdf)

| Lead (months) | Persistence MAE (°C) | Climatology MAE (°C) | Damped MAE (°C) | Skill against climatology |
| --- | ---: | ---: | ---: | ---: |
| 1 | 7.40 | 1.30 | 1.34 | −3.23% |
| 3 | 12.76 | 1.29 | 1.28 | +1.40% |
| 6 | 13.98 | 1.25 | 1.25 | +0.34% |
| 12 | 3.96 | 1.26 | 1.26 | +0.03% |

Climatology was as accurate or better at five of twelve leads. At all leads the MAE difference was below 0.05 °C in absolute terms. The date-aware replay verifies the benchmark path. It supplies no calibrated intervals or skill evidence for a stateful land or weather model. NASA also retrospectively reprocessed the bundled IMERG V07 series, so its observation dates alone cannot establish historical availability. No initialized forecast leads are published. [NASA IMERG V07 release history](https://gpm.nasa.gov/resources/documents/imerg-v07-release-notes)

### Reproduce the benchmark replay

Download NWS product `CLMPHX` via the Iowa Environmental Mesonet ZIP endpoint using the five consecutive date ranges shown here. Keep the member names, which carry the original UTC issuance timestamps.

```sh
mkdir -p /tmp/phoenix-climate-reports
for years in 2000:2005 2005:2010 2010:2015 2015:2020 2020:2026; do
    first="${years%:*}"
    next="${years#*:}"
    curl -fsSL "https://mesonet.agron.iastate.edu/cgi-bin/afos/retrieve.py?pil=CLMPHX&cccc=KPSR&sdate=$first-01-01&edate=$next-01-01&fmt=zip&limit=1000" \
        -o "/tmp/phoenix-climate-reports/$first.zip"
done
python3 scripts/homecoming-validation/forecast.py /tmp/phoenix-climate-reports/*.zip --issues
```

The script uses the checked-in GSOM CSV. Its SHA-256 was `3da7d716488163e8011737214a250c42e4505eac872f73f6ce54a6d75078a12e`. The normalized NWS inputs contained 366 report versions with SHA-256 `b8d32897ed39f397adf161bd48b60b216512ef599629816668b7039bd96eee11`.
