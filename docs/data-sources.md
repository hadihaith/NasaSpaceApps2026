# Earth data sources and provenance

Owner: Member 3 (Earth data). Last updated 2026-11-14.

This is the team's register of the data the game uses: where each number comes from, what it means,
what it can and cannot show, and how to rebuild it. **Every number in `public/data/` is real data. Nothing
is simulated or invented.** Simulation output must never be written into these files.

## 1. Region

| Field | Value |
| --- | --- |
| `regionId` | `region_01` |
| Name | Phoenix and central Arizona |
| Box | 32.5°N to 34.5°N, 113.0°W to 111.0°W (2° × 2°) |
| Contents | Phoenix metro area, irrigated farmland, Sonoran Desert, mountains toward the north-east |
| Period | January 1981 to December 2025 (540 months) |
| Station | Phoenix Airport, GHCN ID `USW00023183`, WMO 72278, 33.4278°N 112.0037°W, 339.2 m |

Why Phoenix: it has the most complete ground record we checked, including humidity and wind, which the
simulation needs. It also has NASA model data with no gaps and a well-known heat story. Fifteen Middle East
stations were checked too, and their NOAA monthly records have no humidity or wind and large gaps.

## 2. Datasets in the game

### 2a. NASA MERRA-2 reanalysis via NASA POWER (area mean over the box)

`kind: "reanalysis"`: output from a NASA model that assimilates observations. It is not a direct
measurement and must be labelled that way in the UI. The challenge names MERRA-2 as an accepted source.

| Series id | POWER parameter | Unit in file | Native unit | Definition |
| --- | --- | --- | --- | --- |
| `region_01.merra2.precipitation` | `PRECTOTCORR` | mm/month | mm/day | Bias-corrected total precipitation, including snow water |
| `region_01.merra2.air_temperature_2m` | `T2M` | °C | °C | Air temperature at 2 m (not land-surface temperature) |
| `region_01.merra2.relative_humidity_2m` | `RH2M` | % | % | Relative humidity at 2 m |
| `region_01.merra2.wind_speed_10m` | `WS10M` | m/s | m/s | Wind speed at 10 m |
| `region_01.merra2.root_zone_soil_wetness` | `GWETROOT` | fraction 0–1 | 1 | Root-zone soil wetness (model quantity) |
| `region_01.merra2.surface_soil_wetness` | `GWETTOP` | fraction 0–1 | 1 | Surface soil wetness (model quantity) |

- Product: NASA POWER Monthly and Annual API, version **v2.10.0** (from the response header), source `MERRA2`.
- Grid: 0.5° latitude × 0.625° longitude. **15 grid cells** fall in the box.
- Coverage: 540 of 540 months for every parameter, with no missing values.
- Access: no account or key. No numeric rate limit is published; POWER can return HTTP 429 when requests come too fast.
- Licence: NASA data are open with no restrictions on use (NASA Earthdata *Data Use and Citation Guidance*). Acknowledge
  NASA POWER (https://power.larc.nasa.gov/docs/acknowledgements/) and cite MERRA-2:
  Gelaro et al. (2017), *J. Climate* 30, 5419–5454, https://doi.org/10.1175/JCLI-D-16-0758.1

### 2b. NASA GPM IMERG satellite precipitation via Giovanni (area mean over the box)

`kind: "satellite"`: satellite precipitation estimates (microwave and infrared), merged with and calibrated
against rain gauges.

| Field | Value |
| --- | --- |
| Series id | `region_01.imerg.precipitation` |
| Product | GPM IMERG Final Run, monthly, **`GPM_3IMERGM` V07**, variable `GPM_3IMERGM_07_precipitation` (recommended for general use) |
| DOI | https://doi.org/10.5067/GPM/IMERG/3B-MONTH/07 |
| Grid | 0.1° × 0.1°. Giovanni averaged the cells inside 112.95–111.05°W, 32.55–34.45°N (grid-limited box) |
| Period | 1998-01 to 2025-09 (333 months, none missing). The latest Final Run month available on 2026-11-14 was 2025-09 |
| Unit | mm/month in the file. The native unit is mm/hr (monthly mean rate), multiplied by 24 × days in month |
| Tool | Giovanni v4.40, *Time Series, Area-Averaged* |
| Access | Earthdata Login with the **NASA GESDISC DATA ARCHIVE** application approved. Guests cannot download |

- Citation: Huffman, G. J., Stocker, E. F., Bolvin, D. T., Nelkin, E. J., & Tan, J. (2023). *GPM IMERG Final Precipitation
  L3 1 month 0.1 degree x 0.1 degree V07* [Dataset]. NASA GES DISC. https://doi.org/10.5067/GPM/IMERG/3B-MONTH/07
- Acknowledgement (GES DISC asks for this): "Analyses and visualizations used in this product were produced with the Giovanni
  online data system, developed and maintained by the NASA GES DISC."
- Caveats: the record starts in 1998, so its trend covers a different period from MERRA-2 and the station. Satellite estimates are
  weakest for light rain and over mountains.

**How to re-download it** (needs a team member's Earthdata Login; `fetch` can't do this step):
1. Sign in at https://giovanni.gsfc.nasa.gov and open the URL in `MANUAL_FILES` in `scripts/process_data.py`. It pre-fills
   the plot type, dates, box and variable. Keep units on **mm/hr**.
2. Click **Plot Data**. When it finishes, open **Downloads → CSV**.
3. Save the file unchanged into `data/raw/giovanni/` with Giovanni's file name.
4. Run `build`. It refuses to run if the file's SHA-256 differs from the pinned value. If you intend to download newer
   data, update the pin and the retrieval date.

### 2c. NOAA Global Summary of the Month, Phoenix Airport (one station)

`kind: "station"`: ground measurements at one urban airport.

| Series id | GSOM element | Unit | First – last value | Missing months | Definition (NCEI) |
| --- | --- | --- | --- | --- | --- |
| `…station.USW00023183.precipitation` | `PRCP` | mm/month | 1981-01 – 2025-12 | 0 | Monthly total; missing if > 5 days missing or flagged |
| `…air_temperature_mean` | `TAVG` | °C | 1981-01 – 2025-12 | 0 | (mean daily max + mean daily min) / 2 |
| `…air_temperature_daily_max_mean` | `TMAX` | °C | 1981-01 – 2025-12 | 0 | Average of daily highs |
| `…air_temperature_daily_min_mean` | `TMIN` | °C | 1981-01 – 2025-12 | 0 | Average of daily lows |
| `…wind_speed` | `AWND` | m/s | 1984-01 – 2025-12 | 36 (all 1981–83) | Average of daily average wind speeds |
| `…relative_humidity` | `RHAV` | % | 2006-01 – 2024-12 | 314 | See caveat below |

- Product: Global Summary of the Month, Version 1, via the NCEI Access Data Service
  (`dataset=global-summary-of-the-month`, `units=metric`). Element definitions are from the
  *GSOM/GSOY Dataset Description Document* v1.0.2 (2020-02-19). Units are from the NCEI dataset metadata service.
- **RHAV caveat:** RHAV is defined in the GHCN-Daily readme (v3.35) as "average relative humidity for the
  day (percent)". It is **not** in NCEI's GSOM data-type list or description document, so how NCEI turns daily
  values into the monthly value, and its completeness rule, are **undocumented**. Use the MERRA-2 humidity for
  analysis, and treat the station humidity as a cross-check.
- `daysMissing` on an observation copies GSOM's count of missing or flagged days in that month (1–5).
- Access: the Access Data Service needed **no token** (verified 2026-11-14); its rate limits are not published.
  The CDO Web Services v2 API would need a free token and is limited to 5 requests/s, 10,000/day,
  1,000 results per request and 10 years per monthly request. We don't use it.
- Licence: NCEI says the data are free of charge. No explicit licence or redistribution terms were found
  (UNKNOWN; ask ncei.info@noaa.gov if it matters). Cite: Lawrimore, J. H., Ray, R., Applequist, S., Korzeniewski, B.,
  Menne, M. J. (2016). *Global Summary of the Month (GSOM), Version 1.* NOAA NCEI. Station USW00023183.

## 3. Processing (`scripts/process_data.py`)

```bash
python3 scripts/process_data.py all
```

This uses the Python standard library only, with no credentials. Run the steps separately with `fetch`, `build` and `validate`.

1. **fetch** saves each response unchanged in `data/raw/` and records URL, time, size and SHA-256 in `data/raw/manifest.json`.
2. **build** refuses to run if any raw file's checksum changed. Then it:
   - averages the MERRA-2 cells weighted by cos(latitude), keeping a month only if at least half the weight has data;
   - converts precipitation from mm/day to mm/month by multiplying by the days in that month (leap years included);
   - drops POWER's month "13" (annual value);
   - leaves missing months as `null`, with **no filling or interpolation**;
   - writes `public/data/earth-observations.json`, `regions.json` and `baselines.json`.
3. **validate** checks the contract fields, gap-free monthly dates, physical ranges, `missingMonths` counts, file size
   (< 1 MB) and cross-checks the two independent sources.

### Validation result (2026-11-14)

All 13 series passed. Monthly values compared between independent sources (difference = second − first):

| Pair | Variable | Months | Correlation r | Mean difference |
| --- | --- | --- | --- | --- |
| MERRA-2 box vs station | Air temperature | 540 | 0.997 | +5.08 °C |
| MERRA-2 box vs station | Precipitation | 540 | 0.821 | −12.26 mm/month |
| MERRA-2 box vs station | Relative humidity | 226 | 0.955 | −6.92 % |
| MERRA-2 box vs IMERG box | Precipitation | 333 | 0.967 | +0.94 mm/month |
| IMERG box vs station | Precipitation | 333 | 0.844 | −11.82 mm/month |

Mean annual precipitation, 1998–2024: IMERG 306.5 mm, MERRA-2 295.3 mm, station 164.7 mm. The satellite and
model estimates agree closely. This also confirms both unit conversions (mm/hr and mm/day to mm/month).

The station differences make physical sense. The station is a hot, low (339 m) city, while the box includes cooler,
wetter mountains, and MERRA-2 cells average about 50 km. The 1991–2020 station figures match NOAA's published
Phoenix normals closely: rain 183.5 mm/year, July average high 41.4 °C.

## 4. Data contract for the dashboard (Member 4) and the game (Member 1)

`earth-observations.json` → `series[]`. Each series has the agreed fields plus provenance:

```jsonc
{
  "id": "region_01.merra2.precipitation",   // unique; use this to pick a series
  "regionId": "region_01",
  "variable": "precipitation",              // same name for station and reanalysis; tell them apart with kind
  "unit": "mm/month",
  "kind": "reanalysis",                     // "reanalysis" | "station" | "satellite"
  "source": { "agency": "NASA", "dataset": "...", "version": "...", "citation": ["..."], "url": "..." },
  "spatial": { "type": "area-mean", "bbox": {...}, "gridCells": 15 },
  "temporal": { "start": "1981-01", "end": "2025-12", "months": 540, "missingMonths": 0 },
  "caveats": ["..."],
  "observations": [ { "date": "1981-01", "value": 25.51 }, { "date": "1981-02", "value": null } ]
}
```

- Dates are `YYYY-MM`. Months run without gaps from `start` to `end`. **`value: null` means missing. Never treat it as 0.**
- Remove the seasonal cycle before testing a trend: yearly means, or monthly anomalies. This is the method in the challenge's
  SeaWiFS reference paper (Acker et al.): monthly anomalies, then least-squares regression, significant at p < 0.05.
- The station humidity record covers only 2006–2024 (19 years), which is short for a trend test. Say so in the UI.

`baselines.json` (for the simulation, Member 2) holds a monthly mean, standard deviation and count per series, over
1991–2020 (the WMO standard normal period). Where a record starts later (station humidity 2006–2024, IMERG
1998–2025), the period is stated in `period.note`. Use the reanalysis baselines for the region and the station baselines for one city point.

## 5. What these data can and cannot support

**Can:**
- Describe how temperature, precipitation, humidity, wind and model soil wetness changed in this box since 1981,
  and test whether a linear trend is statistically significant.
- Show that a single city station and the regional average agree month to month but differ in level.

**Cannot:**
- Prove *why* something changed. Correlation between series is not causation.
- Separate urban heat-island warming from regional warming using the station alone. Compare it with the box mean.
- Speak for places outside the box, or for land-surface (skin) temperature, which is a different quantity from air temperature.
- Give a trustworthy humidity trend from the station (19 years, undocumented monthly method).
- Turn the model's soil-wetness index into measured soil moisture.

No trend results are claimed in this document. Member 4's analysis produces them.

## 6. Resource audit (challenge-provided resources)

Verified on 2026-11-14 from each provider's pages. **UNKNOWN** = not stated on the pages we could read.

| Resource | Used? | Access | Verified limits and terms |
| --- | --- | --- | --- |
| NASA Giovanni | **Yes** (GPM IMERG precipitation) | Earthdata Login (team account set up, GES DISC app approved) | Guests can plot but not download (~4 time points). Area-averaged time series export as CSV. GES DISC asks to be acknowledged |
| NASA AppEEARS | Tried (MODIS vegetation index): request queued, skipped for now | Earthdata Login | Point requests return CSV; area requests return GeoTIFF/NetCDF only. Download links last 48 h; API token about 48 h. Quotas UNKNOWN |
| Hydrology Time Series | No | UNKNOWN | The GES DISC tools page lists GLDAS, NLDAS, MERRA-2, SMERGE, GRACE-DA and IMERG, but its viewer link opens the GES DISC homepage. Treated as unavailable |
| NOAA Climate Data Online | **Yes** (GSOM via Access Data Service) | No token for the Access Data Service | CDO API v2: token, 5 req/s, 10,000/day, 1,000 results, 10-year monthly range. Free; licence UNKNOWN |
| SeaWiFS chlorophyll paper (Acker et al., NASA GES DISC) | Method reference | Open PDF | Monthly anomalies, then least-squares regression; ANOVA F-test, p < 0.05; R² reported |
| KNMI Climate Explorer | Manual cross-check only | Human check; account for some features | Data licence UNKNOWN. The site blocks automated traffic, so **do not script it**. Cite KNMI Climate Explorer and the original data source |
| Statistical test articles (Medium; Statsig) | Background | Open | The Medium article was not readable by our tools. Statsig covers t-test, z-test, chi-square, Bonferroni (A/B-test focus) |
| CSA RADARSAT Constellation Mission | No | EODMS; "public and vetted users" | Dataset record: Open Government Licence – Canada. Each image product has its own User Licence. Radar scenes, not time series |
| GEO.ca | No | UNKNOWN | Licence UNKNOWN (page fetched was navigation only) |
| SWOT | No | UNKNOWN | Launched December 2022, so under 4 years of data. Too short for trend tests |
| CSA ecosystems page | Background | Open | Overview of satellite ecosystem monitoring (RCM, SMOS, SWOT) |
| CSA SAR Python tutorial | No | Open (modified MIT licence) | Plots a single RCM scene (BC wildfires). Not a time series |
| NASA POWER (not on the list) | **Yes** | No account | Serves MERRA-2, which the challenge names. No numeric rate limit published |

## 7. Skipped for now: vegetation index

The team decided on 2026-11-14 to ship without a vegetation index. An AppEEARS area request
(`region_01_phoenix_MOD13A3_061`, request file `data/raw/appeears/region_01_MOD13A3-request.json`) was submitted the
same day, but it was still queued and could take days. No faster route exists:
- Giovanni has no NDVI or EVI variables.
- ORNL DAAC's MODIS subset service has no MOD13A3, and its 250 m product is limited to 10 dates and about 200 × 200 km per request.
- Raw MOD13A3 granules are HDF4 files, which the standard-library pipeline can't read.

If the AppEEARS request completes in time, add its per-month statistics CSV as a `kind: "satellite"` series:

| Variable | Product | Route | Notes |
| --- | --- | --- | --- |
| Vegetation index | MODIS/Terra `MOD13A3` V061, 1 km monthly, from 2000-02 | AppEEARS | Scale factor 0.0001, fill −3000. Use the quality layer to drop bad pixels. kind `satellite` |

This becomes another series with `kind: "satellite"`; the contract does not change.

## 8. Member 3 integration delivery (14 November 2026)

See [the implementation handoff](member-3/handoff.md), [quality summary](member-3/quality-report.md),
[full quality report](member-3/quality-report.json), and [versioned dataset index](member-3/dataset-index.json).
The original 13-series Phoenix collection remains canonical. Five additional point requests are now saved
unchanged in `data/raw/homecoming/` with a separate manifest, checksums and actual retrieval times. They
are individual POWER grid estimates, not broad-region averages. Build converts their precipitation rates
using calendar days and generates consistent 1991–2020 simulation normals.

`process_data.py build` now also generates 38 Member 4-compatible files in `data/processed/member4/`,
a 32-record NASA Archive bundle, and the game's normal-only climate catalog. `validate` verifies these
outputs against pinned raw inputs and fails on stale derived files. Station exports retain UNKNOWN
redistribution and remain outside the new Archive bundle. See the handoff for the exact release scope.

The Archive explicitly uses Member 4's equal-month annual mean; rainfall remains mm/month, not mm/year.
The Inferences page continues to use complete-year rainfall sums. All real-data scientific reviews remain
pending; QA does not establish statistical significance. The handoff specifies the remaining Member 4 review.
