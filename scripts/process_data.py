#!/usr/bin/env python3
"""
Earth-observation data pipeline for Operation: The Great Homecoming (Member 3).

Three steps, each runnable on its own:

    python3 scripts/process_data.py fetch     # download raw files into data/raw/
    python3 scripts/process_data.py build     # data/raw/ -> public/data/*.json
    python3 scripts/process_data.py validate  # check the JSON the game will load

`python3 scripts/process_data.py all` runs the three in order.

Standard library only (Python 3.9+), so any teammate can run it without
installing anything. No credentials are needed for the sources used here:
NASA POWER and the NOAA NCEI Access Data Service are both open.

Rules this script follows (see docs/data-sources.md):
- Raw downloads are kept unchanged in data/raw/ with a manifest (URL, time, sha256).
- Missing months stay missing (null). Nothing is filled or interpolated.
- Units are converted only where stated, and every conversion is recorded in
  the series' "processing" list.
- Reanalysis (model) output and station measurements are kept as separate
  series and labelled with "kind", so the game never presents one as the other.
"""

import argparse
import calendar
import hashlib
import json
import math
import os
import statistics
import sys
import time
import urllib.parse
import urllib.request
from datetime import datetime, timezone

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(ROOT, "data", "raw")
OUT = os.path.join(ROOT, "public", "data")
SCHEMA_VERSION = "1.0.0"
USER_AGENT = "great-homecoming-data-pipeline/1.0 (hackathon; python-urllib)"

# ---------------------------------------------------------------------------
# Region and record definitions. regions.json is generated from this.
# ---------------------------------------------------------------------------

START = "1981-01"  # MERRA-2 in POWER starts in 1981
END = "2025-12"    # last complete year at the time of download

REGION = {
    "regionId": "region_01",
    "name": "Phoenix and central Arizona",
    "shortName": "Phoenix",
    "country": "United States",
    "description": (
        "A 2 x 2 degree box around Phoenix in the Sonoran Desert. It holds the "
        "Phoenix metro area, irrigated farmland and open desert, with mountains "
        "toward the north-east corner."
    ),
    # Edges on whole/half degrees so they line up with the MERRA-2 grid.
    "bbox": {"latMin": 32.5, "latMax": 34.5, "lonMin": -113.0, "lonMax": -111.0},
    "center": {"lat": 33.5, "lon": -112.0},
}

STATION = {
    "id": "USW00023183",
    "name": "PHOENIX AIRPORT, AZ US",
    "lat": 33.4278,
    "lon": -112.00365,
    "elevation_m": 339.2,
    "wmoId": "72278",
    "network": "GHCN-Daily (GSN station)",
}

# NASA POWER parameters -> game variable names.
# POWER's monthly API returns one value per month plus month "13" (annual).
POWER_PARAMS = [
    {
        "param": "PRECTOTCORR",
        "variable": "precipitation",
        "label": "Precipitation (rain and snow)",
        "nativeUnit": "mm/day",
        "unit": "mm/month",
        "definition": "Bias-corrected total precipitation reaching the surface, including the water in snow.",
    },
    {
        "param": "T2M",
        "variable": "air_temperature_2m",
        "label": "Air temperature at 2 m",
        "nativeUnit": "°C",
        "unit": "°C",
        "definition": "Monthly mean air temperature 2 metres above the ground. This is AIR temperature, not land-surface (skin) temperature.",
    },
    {
        "param": "RH2M",
        "variable": "relative_humidity_2m",
        "label": "Relative humidity at 2 m",
        "nativeUnit": "%",
        "unit": "%",
        "definition": "Monthly mean relative humidity 2 metres above the ground.",
    },
    {
        "param": "WS10M",
        "variable": "wind_speed_10m",
        "label": "Wind speed at 10 m",
        "nativeUnit": "m/s",
        "unit": "m/s",
        "definition": "Monthly mean wind speed 10 metres above the ground.",
    },
    {
        "param": "GWETROOT",
        "variable": "root_zone_soil_wetness",
        "label": "Root-zone soil wetness",
        "nativeUnit": "1",
        "unit": "fraction (0-1)",
        "definition": "Wetness of the soil in the root zone, from 0 (completely dry) to 1 (saturated). A model quantity, not a satellite soil-moisture retrieval.",
    },
    {
        "param": "GWETTOP",
        "variable": "surface_soil_wetness",
        "label": "Surface soil wetness",
        "nativeUnit": "1",
        "unit": "fraction (0-1)",
        "definition": "Wetness of the top few centimetres of soil, from 0 (dry) to 1 (saturated). A model quantity.",
    },
]

# NOAA GSOM elements -> game variable names. Units are those NCEI reports with units=metric.
GSOM_ELEMENTS = [
    {
        "element": "PRCP",
        "variable": "precipitation",
        "label": "Precipitation (station gauge)",
        "unit": "mm/month",
        "definition": "Total precipitation measured at the station in the month. NCEI sets the month to missing if more than 5 days are missing or flagged.",
    },
    {
        "element": "TAVG",
        "variable": "air_temperature_mean",
        "label": "Mean air temperature (station)",
        "unit": "°C",
        "definition": "(Mean of daily maximum + mean of daily minimum) / 2. Missing if more than 5 days, or more than 3 days in a row, are missing.",
    },
    {
        "element": "TMAX",
        "variable": "air_temperature_daily_max_mean",
        "label": "Average daily high (station)",
        "unit": "°C",
        "definition": "Average of the daily maximum air temperatures in the month.",
    },
    {
        "element": "TMIN",
        "variable": "air_temperature_daily_min_mean",
        "label": "Average daily low (station)",
        "unit": "°C",
        "definition": "Average of the daily minimum air temperatures in the month.",
    },
    {
        "element": "AWND",
        "variable": "wind_speed",
        "label": "Average wind speed (station)",
        "unit": "m/s",
        "definition": "Average of the daily average wind speeds in the month.",
    },
    {
        "element": "RHAV",
        "variable": "relative_humidity",
        "label": "Average relative humidity (station)",
        "unit": "%",
        "definition": "Monthly value of GHCN-Daily element RHAV (average relative humidity for the day, percent).",
        "caveat": (
            "RHAV is defined for daily data in the GHCN-Daily readme (v3.35), but it is not in NCEI's "
            "GSOM data-type list or GSOM description document, so the exact monthly aggregation "
            "and completeness rule are not documented. Record starts in 2006."
        ),
    },
]

# Files that need an Earthdata Login, so `fetch` cannot download them. They are
# downloaded by hand (steps in docs/data-sources.md) and pinned here by checksum,
# so `build` refuses to run if the file is missing or different.
MANUAL_FILES = [
    {
        "file": "data/raw/giovanni/g4.areaAvgTimeSeries.GPM_3IMERGM_07_precipitation.19980101-20250930.113W_32N_111W_34N.csv",
        "source": "NASA Giovanni v4.40 (Time Series, Area-Averaged)",
        "url": (
            "https://giovanni.gsfc.nasa.gov/giovanni/#service=ArAvTs&starttime=1998-01-01T00:00:00Z"
            "&endtime=2025-09-30T23:59:59Z&bbox=-113,32.5,-111,34.5&data=GPM_3IMERGM_07_precipitation"
        ),
        "retrievedUtc": "2026-11-14T09:33:00Z",
        "bytes": 11679,
        "sha256": "245ee785582ca84335f0dd2e2ad6fd4ee0f5e65df856d1c15e98706ccb091690",
        "access": "Earthdata Login with the 'NASA GESDISC DATA ARCHIVE' application approved",
    },
]

IMERG = {
    "file": MANUAL_FILES[0]["file"],
    "variable": "precipitation",
    "label": "Precipitation (satellite, GPM IMERG)",
    "nativeUnit": "mm/hr",
    "unit": "mm/month",
    "definition": (
        "IMERG Final Run merged satellite-gauge precipitation estimate, the variable GES DISC recommends "
        "for general use. Microwave and infrared satellite estimates, calibrated with monthly rain-gauge analyses."
    ),
}

IMERG_CITATION = (
    "Huffman, G. J., Stocker, E. F., Bolvin, D. T., Nelkin, E. J., & Tan, J. (2023). GPM IMERG Final "
    "Precipitation L3 1 month 0.1 degree x 0.1 degree V07 [Dataset]. NASA Goddard Earth Sciences Data and "
    "Information Services Center. https://doi.org/10.5067/GPM/IMERG/3B-MONTH/07"
)
GIOVANNI_ACK = (
    "Analyses and visualizations used in this product were produced with the Giovanni online data system, "
    "developed and maintained by the NASA GES DISC."
)

POWER_BASE = "https://power.larc.nasa.gov/api/temporal/monthly/regional"
NCEI_BASE = "https://www.ncei.noaa.gov/access/services/data/v1"

POWER_CITATION = (
    "NASA Langley Research Center (LaRC) POWER Project, Monthly and Annual API "
    "(https://power.larc.nasa.gov). Acknowledgement guidance: https://power.larc.nasa.gov/docs/acknowledgements/"
)
MERRA2_CITATION = (
    "Gelaro, R., et al. (2017). The Modern-Era Retrospective Analysis for Research and "
    "Applications, Version 2 (MERRA-2). Journal of Climate, 30, 5419-5454. "
    "https://doi.org/10.1175/JCLI-D-16-0758.1"
)
GSOM_CITATION = (
    "Lawrimore, J. H., Ray, R., Applequist, S., Korzeniewski, B., Menne, M. J. (2016). "
    "Global Summary of the Month (GSOM), Version 1. NOAA National Centers for Environmental "
    "Information. Station USW00023183."
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def months(start, end):
    """Every 'YYYY-MM' from start to end inclusive."""
    y, m = int(start[:4]), int(start[5:7])
    ey, em = int(end[:4]), int(end[5:7])
    out = []
    while (y, m) <= (ey, em):
        out.append("%04d-%02d" % (y, m))
        m += 1
        if m == 13:
            y, m = y + 1, 1
    return out


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def download(url, dest, attempts=4):
    """Download url to dest, retrying politely on failure. Returns bytes written."""
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    last = None
    for i in range(attempts):
        try:
            with urllib.request.urlopen(req, timeout=180) as r:
                data = r.read()
            tmp = dest + ".part"
            with open(tmp, "wb") as f:
                f.write(data)
            os.replace(tmp, dest)
            return len(data)
        except Exception as e:  # network errors, HTTP 429/5xx
            last = e
            wait = 5 * (i + 1)
            print("  retry %d after error: %s (waiting %ds)" % (i + 1, e, wait))
            time.sleep(wait)
    raise RuntimeError("Failed to download %s: %s" % (url, last))


def rel(path):
    return os.path.relpath(path, ROOT)


def round_or_none(v, nd=2):
    return None if v is None else round(v, nd)


# ---------------------------------------------------------------------------
# fetch
# ---------------------------------------------------------------------------


def power_url(param):
    b = REGION["bbox"]
    q = {
        "parameters": param,
        "community": "AG",
        "start": START[:4],
        "end": END[:4],
        "format": "JSON",
        "latitude-min": b["latMin"],
        "latitude-max": b["latMax"],
        "longitude-min": b["lonMin"],
        "longitude-max": b["lonMax"],
    }
    return POWER_BASE + "?" + urllib.parse.urlencode(q)


def gsom_url():
    q = {
        "dataset": "global-summary-of-the-month",
        "stations": STATION["id"],
        "startDate": START + "-01",
        "endDate": END + "-%02d" % calendar.monthrange(int(END[:4]), int(END[5:7]))[1],
        "dataTypes": ",".join(e["element"] for e in GSOM_ELEMENTS),
        "units": "metric",
        "format": "csv",
        "includeAttributes": "true",
        "includeStationName": "true",
        "includeStationLocation": "1",
    }
    return NCEI_BASE + "?" + urllib.parse.urlencode(q)


def cmd_fetch(_args):
    os.makedirs(os.path.join(RAW, "nasa_power"), exist_ok=True)
    os.makedirs(os.path.join(RAW, "noaa_gsom"), exist_ok=True)
    jobs = [(gsom_url(), os.path.join(RAW, "noaa_gsom", "%s_gsom_%s_%s.csv" % (STATION["id"], START, END)), "NOAA NCEI GSOM")]
    for p in POWER_PARAMS:
        name = "%s_%s_monthly_%s_%s.json" % (REGION["regionId"], p["param"], START[:4], END[:4])
        jobs.append((power_url(p["param"]), os.path.join(RAW, "nasa_power", name), "NASA POWER"))

    manifest = {"retrievedBy": "scripts/process_data.py fetch", "files": []}
    for m in MANUAL_FILES:
        if os.path.exists(os.path.join(ROOT, m["file"])):
            manifest["files"].append(dict(m, manual=True))
        else:
            print("NOTE: manual file %s is missing; download it as described in docs/data-sources.md" % m["file"])
    for url, dest, source in jobs:
        print("Downloading %s -> %s" % (source, rel(dest)))
        n = download(url, dest)
        manifest["files"].append({
            "file": rel(dest),
            "source": source,
            "url": url,
            "retrievedUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
            "bytes": n,
            "sha256": sha256(dest),
        })
        time.sleep(1.5)  # stay well under any request-rate limit

    with open(os.path.join(RAW, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)
    print("Wrote %s (%d files)" % (rel(os.path.join(RAW, "manifest.json")), len(manifest["files"])))


# ---------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------


def load_manifest():
    path = os.path.join(RAW, "manifest.json")
    if not os.path.exists(path):
        sys.exit("No data/raw/manifest.json. Run `fetch` first.")
    with open(path) as f:
        m = json.load(f)
    # Manual files are pinned in code; that pin wins over whatever the manifest says.
    for pin in MANUAL_FILES:
        m["files"] = [e for e in m["files"] if e["file"] != pin["file"]] + [dict(pin, manual=True)]
    by_file = {e["file"]: e for e in m["files"]}
    for e in m["files"]:
        if not os.path.exists(os.path.join(ROOT, e["file"])):
            sys.exit("Missing raw file %s. %s" % (e["file"], "Download it by hand (see docs/data-sources.md)." if e.get("manual") else "Run `fetch`."))
        actual = sha256(os.path.join(ROOT, e["file"]))
        if actual != e["sha256"]:
            sys.exit("Checksum mismatch for %s: raw file changed since download." % e["file"])
    return by_file


def build_power_series(spec, manifest):
    name = "%s_%s_monthly_%s_%s.json" % (REGION["regionId"], spec["param"], START[:4], END[:4])
    path = os.path.join(RAW, "nasa_power", name)
    entry = manifest[rel(path)]
    with open(path) as f:
        body = json.load(f)

    header = body["header"]
    fill = header.get("fill_value", -999.0)
    meta = body["parameters"][spec["param"]]
    features = body["features"]
    dates = months(START, END)

    # Area-weighted mean: grid cells shrink with latitude, so weight by cos(lat).
    sums = [0.0] * len(dates)
    weights = [0.0] * len(dates)
    total_w = 0.0
    cells = []
    for feat in features:
        lon, lat = feat["geometry"]["coordinates"][:2]
        cells.append([lon, lat])
        w = math.cos(math.radians(lat))
        total_w += w
        values = feat["properties"]["parameter"][spec["param"]]
        for i, d in enumerate(dates):
            v = values.get(d.replace("-", ""))
            if v is None or v == fill or not math.isfinite(v):
                continue
            sums[i] += w * v
            weights[i] += w

    processing = [
        "Area-weighted mean of %d MERRA-2 grid cells inside the region box, weighted by cos(latitude)." % len(features),
        "A month is kept only if cells holding at least half the total weight have data; otherwise null.",
        "POWER's month '13' (annual value) is dropped.",
    ]
    obs = []
    for i, d in enumerate(dates):
        v = sums[i] / weights[i] if weights[i] > 0 and weights[i] >= total_w / 2 else None
        if v is not None and spec["param"] == "PRECTOTCORR":
            v = v * calendar.monthrange(int(d[:4]), int(d[5:7]))[1]
        obs.append({"date": d, "value": round_or_none(v, 3 if spec["unit"].startswith("fraction") else 2)})
    if spec["param"] == "PRECTOTCORR":
        processing.append("Converted from mm/day (monthly mean daily rate) to mm/month by multiplying by the number of days in that month.")

    missing = sum(1 for o in obs if o["value"] is None)
    return {
        "id": "%s.merra2.%s" % (REGION["regionId"], spec["variable"]),
        "regionId": REGION["regionId"],
        "variable": spec["variable"],
        "label": spec["label"],
        "unit": spec["unit"],
        "kind": "reanalysis",
        "kindNote": "Model reanalysis that assimilates observations. Not a direct measurement.",
        "definition": spec["definition"],
        "source": {
            "agency": "NASA",
            "dataset": "MERRA-2 via NASA POWER Monthly and Annual API",
            "version": "POWER API %s; source %s" % (header.get("api", {}).get("version", "UNKNOWN"), ",".join(header.get("sources", []))),
            "parameter": spec["param"],
            "parameterLongName": meta.get("longname"),
            "nativeUnit": meta.get("units"),
            "url": entry["url"],
            "retrievedUtc": entry["retrievedUtc"],
            "rawFile": entry["file"],
            "citation": [POWER_CITATION, MERRA2_CITATION],
            "license": "NASA Earth science data: open, no restrictions on use (NASA Earthdata data use guidance). Acknowledge NASA POWER and cite MERRA-2.",
        },
        "spatial": {
            "type": "area-mean",
            "bbox": REGION["bbox"],
            "gridCells": len(features),
            "gridCellCenters": cells,
            "nativeResolution": "0.5° latitude x 0.625° longitude",
        },
        "temporal": {"resolution": "monthly", "start": START, "end": END, "months": len(obs), "missingMonths": missing,
                     "timeStandard": header.get("time_standard")},
        "processing": processing,
        "caveats": [
            "Each value averages over roughly 50 km grid cells, so it is smoother than any single place and cooler than the Phoenix urban core.",
            "Reanalysis can shift when the observing system changes; check large jumps against the station record.",
        ],
        "observations": obs,
    }


def read_gsom(path):
    import csv
    with open(path, newline="") as f:
        return list(csv.DictReader(f))


def build_gsom_series(spec, rows, entry):
    by_date = {r["DATE"]: r for r in rows}
    obs = []
    for d in months(START, END):
        r = by_date.get(d)
        raw = (r or {}).get(spec["element"], "").strip()
        o = {"date": d, "value": float(raw) if raw else None}
        attrs = (r or {}).get(spec["element"] + "_ATTRIBUTES", "")
        first = attrs.split(",")[0].strip() if attrs else ""
        if raw and first.isdigit() and int(first) > 0:
            o["daysMissing"] = int(first)  # 1-5 days missing or flagged in that month (GSOM rule)
        obs.append(o)

    present = [o for o in obs if o["value"] is not None]
    caveats = [
        "A single airport station in a large city. Its trend mixes regional climate with urban growth (urban heat island) and any instrument or siting changes.",
        "A point measurement: do not compare its values directly with the area-mean reanalysis without saying so.",
    ]
    if spec.get("caveat"):
        caveats.append(spec["caveat"])
    return {
        "id": "%s.station.%s.%s" % (REGION["regionId"], STATION["id"], spec["variable"]),
        "regionId": REGION["regionId"],
        "variable": spec["variable"],
        "label": spec["label"],
        "unit": spec["unit"],
        "kind": "station",
        "kindNote": "Ground measurements from one weather station.",
        "definition": spec["definition"],
        "source": {
            "agency": "NOAA",
            "dataset": "Global Summary of the Month (GSOM)",
            "version": "1 (NCEI Access Data Service, dataset global-summary-of-the-month)",
            "parameter": spec["element"],
            "url": entry["url"],
            "retrievedUtc": entry["retrievedUtc"],
            "rawFile": entry["file"],
            "citation": [GSOM_CITATION],
            "license": "Free of charge from NCEI. No explicit licence or redistribution terms found; cite as above.",
        },
        "spatial": {"type": "station", "station": STATION},
        "temporal": {
            "resolution": "monthly",
            "start": START,
            "end": END,
            "months": len(obs),
            "missingMonths": len(obs) - len(present),
            "firstValue": present[0]["date"] if present else None,
            "lastValue": present[-1]["date"] if present else None,
        },
        "processing": [
            "Values as published by NCEI with units=metric; no unit conversion.",
            "Months NCEI left blank are null. daysMissing (1-5) is copied from the GSOM attribute field when present.",
        ],
        "caveats": caveats,
        "observations": obs,
    }


def build_imerg_series(manifest):
    entry = manifest[IMERG["file"]]
    with open(os.path.join(ROOT, IMERG["file"]), encoding="latin-1") as f:
        lines = f.read().splitlines()

    header = {}
    data_start = None
    for i, line in enumerate(lines):
        if line.startswith("time,"):
            data_start = i + 1
            break
        if ":," in line:
            k, v = line.split(":,", 1)
            header[k.strip()] = v.strip().strip('"')
    if data_start is None:
        sys.exit("Giovanni CSV has no 'time,' header row: %s" % IMERG["file"])
    fill = float(next(v for k, v in header.items() if k.startswith("Fill Value")))

    values = {}
    for line in lines[data_start:]:
        if not line.strip():
            continue
        t, v = line.split(",", 1)
        values[t[:7]] = float(v)

    dates = sorted(values)
    start, end = dates[0], dates[-1]
    obs = []
    for d in months(start, end):
        v = values.get(d)
        if v is None or v == fill or not math.isfinite(v):
            obs.append({"date": d, "value": None})
            continue
        hours = 24 * calendar.monthrange(int(d[:4]), int(d[5:7]))[1]
        obs.append({"date": d, "value": round(v * hours, 2)})

    lon_min, lat_min, lon_max, lat_max = [float(x) for x in header["Data Bounding Box"].split(",")]
    return {
        "id": "%s.imerg.%s" % (REGION["regionId"], IMERG["variable"]),
        "regionId": REGION["regionId"],
        "variable": IMERG["variable"],
        "label": IMERG["label"],
        "unit": IMERG["unit"],
        "kind": "satellite",
        "kindNote": "Satellite precipitation estimates merged with and calibrated against rain gauges.",
        "definition": IMERG["definition"],
        "source": {
            "agency": "NASA",
            "dataset": "GPM IMERG Final Run, monthly (GPM_3IMERGM), via NASA Giovanni",
            "version": "V07 (Giovanni v4.40)",
            "parameter": "GPM_3IMERGM_07_precipitation",
            "nativeUnit": IMERG["nativeUnit"],
            "doi": "10.5067/GPM/IMERG/3B-MONTH/07",
            "url": entry["url"],
            "retrievedUtc": entry["retrievedUtc"],
            "rawFile": entry["file"],
            "access": entry.get("access"),
            "citation": [IMERG_CITATION, GIOVANNI_ACK],
            "license": "NASA data: open, no restrictions on use (NASA Earthdata data use guidance). GPM is a joint NASA-JAXA mission; cite the dataset DOI.",
        },
        "spatial": {
            "type": "area-mean",
            "bbox": REGION["bbox"],
            "dataBbox": {"latMin": lat_min, "latMax": lat_max, "lonMin": lon_min, "lonMax": lon_max},
            "nativeResolution": "0.1° x 0.1°",
            "method": "Giovanni area average over the 0.1° cells inside the box (Giovanni excludes fill values).",
        },
        "temporal": {"resolution": "monthly", "start": start, "end": end, "months": len(obs),
                     "missingMonths": sum(1 for o in obs if o["value"] is None)},
        "processing": [
            "Area average computed by Giovanni; CSV saved unchanged (checksum pinned in scripts/process_data.py).",
            "Converted from mm/hr (monthly mean rate) to mm/month by multiplying by 24 x the number of days in that month.",
        ],
        "caveats": [
            "Starts in 1998, 17 years after the MERRA-2 and station records, so its trends cover a different period.",
            "Satellite rain estimates are least reliable for light rain and over complex terrain such as the mountains in the box.",
        ],
        "observations": obs,
    }


def climatology(series, start_year, end_year):
    """Mean, sd and count for each calendar month within [start_year, end_year]."""
    buckets = [[] for _ in range(12)]
    for o in series["observations"]:
        y, m = int(o["date"][:4]), int(o["date"][5:7])
        if start_year <= y <= end_year and o["value"] is not None:
            buckets[m - 1].append(o["value"])
    out = []
    for m, vals in enumerate(buckets, start=1):
        out.append({
            "month": m,
            "mean": round(statistics.mean(vals), 3) if vals else None,
            "sd": round(statistics.stdev(vals), 3) if len(vals) > 1 else None,
            "n": len(vals),
        })
    return out


def build_baselines(series_list):
    """Monthly reference climate for the simulation. 1991-2020 is the WMO standard normal period."""
    out = []
    for s in series_list:
        years = sorted({int(o["date"][:4]) for o in s["observations"] if o["value"] is not None})
        start, end = 1991, 2020
        period_note = "WMO standard normal period 1991-2020."
        if years and years[0] > 1991:
            start, end = years[0], years[-1]
            period_note = "Record starts in %d, so the full available period %d-%d is used instead of 1991-2020." % (start, start, end)
        months_clim = climatology(s, start, end)
        means = [m["mean"] for m in months_clim]
        annual = None
        agg = "sum" if s["unit"] == "mm/month" else "mean"
        if all(v is not None for v in means):
            annual = round(sum(means), 1) if agg == "sum" else round(statistics.mean(means), 3)
        out.append({
            "seriesId": s["id"],
            "variable": s["variable"],
            "label": s["label"],
            "unit": s["unit"],
            "kind": s["kind"],
            "period": {"start": str(start), "end": str(end), "note": period_note},
            "monthly": months_clim,
            "annual": {"value": annual, "unit": "mm/year" if agg == "sum" else s["unit"], "aggregation": agg},
        })
    return out


def write_json(path, obj, compact_key="observations"):
    """Pretty JSON, but each observation on one line so files stay small and diffable."""
    placeholder = {}

    def strip(o):
        if isinstance(o, dict):
            r = {}
            for k, v in o.items():
                if k == compact_key and isinstance(v, list):
                    token = "__OBS_%d__" % len(placeholder)
                    placeholder[token] = v
                    r[k] = token
                else:
                    r[k] = strip(v)
            return r
        if isinstance(o, list):
            return [strip(x) for x in o]
        return o

    text = json.dumps(strip(obj), indent=2, ensure_ascii=False)
    for token, items in placeholder.items():
        lines = ",\n".join("        " + json.dumps(i, ensure_ascii=False, separators=(",", ":")) for i in items)
        text = text.replace('"%s"' % token, "[\n" + lines + "\n      ]")
    with open(path, "w", encoding="utf-8") as f:
        f.write(text + "\n")


def cmd_build(_args):
    manifest = load_manifest()
    os.makedirs(OUT, exist_ok=True)
    generated = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    series = [build_power_series(p, manifest) for p in POWER_PARAMS]
    series.append(build_imerg_series(manifest))
    gsom_path = os.path.join(RAW, "noaa_gsom", "%s_gsom_%s_%s.csv" % (STATION["id"], START, END))
    rows = read_gsom(gsom_path)
    series += [build_gsom_series(e, rows, manifest[rel(gsom_path)]) for e in GSOM_ELEMENTS]

    observations = {
        "schemaVersion": SCHEMA_VERSION,
        "generatedUtc": generated,
        "generatedBy": "scripts/process_data.py build",
        "notes": [
            "Real Earth data only. Nothing in this file is simulated or invented.",
            "Each series has the agreed contract fields (regionId, variable, unit, source.agency/dataset/version, observations[{date, value}]) plus provenance.",
            "date is 'YYYY-MM'. Months run without gaps from temporal.start to temporal.end; value is null where data are missing. Never fill nulls with zero.",
            "kind tells you what a series is: 'reanalysis' = NASA model output that assimilates observations (MERRA-2); 'satellite' = satellite estimates (GPM IMERG, merged with rain gauges); 'station' = ground measurements from one weather station.",
            "Series can cover different periods (IMERG starts in 1998). Read temporal.start and temporal.end for each one.",
            "Correlation between series does not show that one causes the other.",
        ],
        "series": series,
    }
    write_json(os.path.join(OUT, "earth-observations.json"), observations)

    regions = {
        "schemaVersion": SCHEMA_VERSION,
        "generatedUtc": generated,
        "regions": [dict(REGION, **{
            "stations": [STATION],
            "seriesIds": [s["id"] for s in series],
            "period": {"start": START, "end": END},
        })],
    }
    write_json(os.path.join(OUT, "regions.json"), regions)

    baselines = {
        "schemaVersion": SCHEMA_VERSION,
        "generatedUtc": generated,
        "regionId": REGION["regionId"],
        "notes": [
            "Monthly reference climate for initialising the simulation. month 1 = January.",
            "mean/sd/n are computed from the same series as earth-observations.json over the stated period.",
            "Prefer the reanalysis (area-mean) baselines for the region; station baselines describe one urban point.",
        ],
        "baselines": build_baselines(series),
    }
    write_json(os.path.join(OUT, "baselines.json"), baselines)
    for name in ("earth-observations.json", "regions.json", "baselines.json"):
        p = os.path.join(OUT, name)
        print("Wrote %s (%.1f KB)" % (rel(p), os.path.getsize(p) / 1024))
    release_module().build_release(sys.modules[__name__])


# ---------------------------------------------------------------------------
# validate
# ---------------------------------------------------------------------------

RANGES = {
    "precipitation": (0, 600),
    "air_temperature_2m": (-30, 50),
    "air_temperature_mean": (-30, 50),
    "air_temperature_daily_max_mean": (-25, 55),
    "air_temperature_daily_min_mean": (-35, 45),
    "relative_humidity_2m": (0, 100),
    "relative_humidity": (0, 100),
    "wind_speed_10m": (0, 30),
    "wind_speed": (0, 30),
    "root_zone_soil_wetness": (0, 1),
    "surface_soil_wetness": (0, 1),
}
MAX_BYTES = 1_000_000


def pearson(a, b):
    ma, mb = statistics.mean(a), statistics.mean(b)
    num = sum((x - ma) * (y - mb) for x, y in zip(a, b))
    den = math.sqrt(sum((x - ma) ** 2 for x in a) * sum((y - mb) ** 2 for y in b))
    return num / den


def cmd_validate(_args):
    errors, warnings = [], []
    path = os.path.join(OUT, "earth-observations.json")
    with open(path) as f:
        data = json.load(f)
    if os.path.getsize(path) > MAX_BYTES:
        errors.append("earth-observations.json is larger than %d bytes" % MAX_BYTES)

    with open(os.path.join(OUT, "regions.json")) as f:
        regions = {r["regionId"]: r for r in json.load(f)["regions"]}

    ids = set()
    by_id = {}
    for s in data["series"]:
        sid = s.get("id", "?")
        for key in ("id", "regionId", "variable", "unit", "kind", "source", "observations", "temporal"):
            if key not in s:
                errors.append("%s: missing field %s" % (sid, key))
        for key in ("agency", "dataset", "version"):
            if not s.get("source", {}).get(key) or "TO_BE_SELECTED" in str(s["source"].get(key)):
                errors.append("%s: source.%s not set" % (sid, key))
        if sid in ids:
            errors.append("duplicate id %s" % sid)
        ids.add(sid)
        by_id[sid] = s
        if s["regionId"] not in regions:
            errors.append("%s: unknown regionId %s" % (sid, s["regionId"]))
        if s["kind"] not in ("reanalysis", "station", "satellite"):
            errors.append("%s: unexpected kind %s" % (sid, s["kind"]))

        expected = months(s["temporal"]["start"], s["temporal"]["end"])
        got = [o["date"] for o in s["observations"]]
        if got != expected:
            errors.append("%s: dates are not a gap-free monthly run %s..%s" % (sid, expected[0], expected[-1]))
        lo, hi = RANGES.get(s["variable"], (-math.inf, math.inf))
        bad = [o for o in s["observations"] if o["value"] is not None and not (lo <= o["value"] <= hi)]
        if bad:
            errors.append("%s: %d values outside plausible range [%s, %s], e.g. %s" % (sid, len(bad), lo, hi, bad[0]))
        nulls = sum(1 for o in s["observations"] if o["value"] is None)
        if nulls != s["temporal"]["missingMonths"]:
            errors.append("%s: missingMonths says %d but %d values are null" % (sid, s["temporal"]["missingMonths"], nulls))
        if nulls > 0.2 * len(got):
            warnings.append("%s: %d of %d months missing" % (sid, nulls, len(got)))

    # Cross-checks between independent sources. These are sanity checks, not science results.
    def paired(a_id, b_id):
        a = {o["date"]: o["value"] for o in by_id[a_id]["observations"] if o["value"] is not None}
        b = {o["date"]: o["value"] for o in by_id[b_id]["observations"] if o["value"] is not None}
        common = sorted(set(a) & set(b))
        return [a[d] for d in common], [b[d] for d in common]

    region = "region_01"
    station = "%s.station.%s." % (region, STATION["id"])
    checks = [
        ("MERRA-2 vs station", region + ".merra2.air_temperature_2m", station + "air_temperature_mean", 0.95),
        ("MERRA-2 vs station", region + ".merra2.precipitation", station + "precipitation", 0.5),
        ("MERRA-2 vs station", region + ".merra2.relative_humidity_2m", station + "relative_humidity", 0.8),
        ("MERRA-2 vs IMERG", region + ".merra2.precipitation", region + ".imerg.precipitation", 0.7),
        ("IMERG vs station", region + ".imerg.precipitation", station + "precipitation", 0.5),
    ]
    print("\nCross-checks between independent sources (monthly values; difference = second minus first):")
    for name, a_id, b_id, min_r in checks:
        if a_id in by_id and b_id in by_id:
            a, b = paired(a_id, b_id)
            r = pearson(a, b)
            diff = statistics.mean(y - x for x, y in zip(a, b))
            print("  %-19s %-22s n=%3d  r=%.3f  mean diff=%+.2f %s" % (name, by_id[a_id]["variable"], len(a), r, diff, by_id[a_id]["unit"]))
            if r < min_r:
                warnings.append("%s vs %s: correlation %.2f below %.2f, check units or alignment" % (a_id, b_id, r, min_r))

    print("\n%d series checked." % len(data["series"]))
    for w in warnings:
        print("WARNING:", w)
    for e in errors:
        print("ERROR:", e)
    if errors:
        sys.exit(1)
    release_module().validate_release(sys.modules[__name__])
    print("Validation passed.")


def release_module():
    # Explicit path also works with Python's isolated (-I) mode in CI.
    import importlib.util
    spec = importlib.util.spec_from_file_location("prepare_member3", os.path.join(ROOT, "scripts", "prepare_member3.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("step", choices=["fetch", "fetch-regions", "build", "validate", "all"])
    args = ap.parse_args()
    if args.step in ("fetch", "all"):
        cmd_fetch(args)
    if args.step in ("fetch-regions", "all"):
        release_module().fetch_points(sys.modules[__name__])
    if args.step in ("build", "all"):
        cmd_build(args)
    if args.step in ("validate", "all"):
        cmd_validate(args)


if __name__ == "__main__":
    main()
