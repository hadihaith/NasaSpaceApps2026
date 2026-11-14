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
