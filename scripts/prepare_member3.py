"""Reproducible Member 3 exports. Called by process_data.py; standard library only."""
import calendar
import hashlib
import json
import math
from pathlib import Path
from datetime import datetime, timezone
from urllib.parse import urlencode

PROCESSING_VERSION = "member3-v1"
POINTS = {
    "sahel": ("Central Sahel", 14, 6),
    "amazon": ("Amazon Basin", -7, -61),
    "mediterranean": ("Eastern Mediterranean", 39.7, 22.6),
    "south-asia": ("South Asia", 24, 79),
    "australia": ("Eastern Australia", -27, 145),
}
GAME_VARIABLES = {
    "air_temperature_2m": "temperature", "precipitation": "rain",
    "relative_humidity_2m": "humidity", "wind_speed_10m": "wind",
    "root_zone_soil_wetness": "soil",
}


def encode(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode()


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False, allow_nan=False).encode()).hexdigest()


def point_url(key):
    _, lat, lon = POINTS[key]
    return "https://power.larc.nasa.gov/api/temporal/monthly/point?" + urlencode({
        "parameters": "T2M,PRECTOTCORR,RH2M,WS10M,GWETROOT", "community": "AG",
        "longitude": lon, "latitude": lat, "start": 1991, "end": 2025, "format": "JSON",
    })


def fetch_points(core):
    root = Path(core.ROOT)
    folder = root / "data/raw/homecoming"
    folder.mkdir(parents=True, exist_ok=True)
    manifest = {"files": []}
    for key in POINTS:
        dest = folder / (key + "_monthly_1991_2025.json")
        url = point_url(key)
        print("Downloading NASA POWER point:", key, flush=True)
        core.download(url, str(dest))
        manifest["files"].append({"regionId": key, "file": str(dest.relative_to(root)), "url": url,
                                  "retrievedUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
                                  "bytes": dest.stat().st_size, "sha256": core.sha256(str(dest))})
        core.time.sleep(1.5)
    (folder / "manifest.json").write_bytes(encode(manifest))


def point_series(core):
    root = Path(core.ROOT)
    manifest = json.loads((root / "data/raw/homecoming/manifest.json").read_text())
    if {e["regionId"] for e in manifest["files"]} != set(POINTS):
        raise ValueError("Point manifest must contain the five documented locations.")
    out = []
    for entry in manifest["files"]:
        key = entry["regionId"]
        path = root / entry["file"]
        if core.sha256(str(path)) != entry["sha256"] or path.stat().st_size != entry["bytes"]:
            raise ValueError("Point raw checksum/size mismatch: " + key)
        if entry["url"] != point_url(key):
            raise ValueError("Point source URL differs from the documented query: " + key)
        body = json.loads(path.read_text())
        name, lat, lon = POINTS[key]
        header = body["header"]
        if body["geometry"]["coordinates"][:2] != [lon, lat]:
            raise ValueError("Unexpected POWER point coordinates: " + key)
        for spec in core.POWER_PARAMS:
            if spec["variable"] not in GAME_VARIABLES:
                continue
            param = spec["param"]
            native = body["parameters"][param]["units"]
            if native != {"°C": "C", "1": "1"}.get(spec["nativeUnit"], spec["nativeUnit"]):
                raise ValueError("Unexpected POWER units: " + param)
            values = body["properties"]["parameter"][param]
            obs = []
            for date in core.months("1991-01", "2025-12"):
                value = values.get(date.replace("-", ""))
                if value is None or value == header["fill_value"] or not math.isfinite(value):
                    value = None
                elif param == "PRECTOTCORR":
                    value = round(value * calendar.monthrange(int(date[:4]), int(date[5:]))[1], 2)
                obs.append({"date": date, "value": value})
            out.append({"id": key + ".merra2." + spec["variable"], "regionId": key,
                        "variable": spec["variable"], "label": spec["label"], "unit": spec["unit"],
                        "kind": "reanalysis", "definition": spec["definition"],
                        "source": {"agency": "NASA", "dataset": "MERRA-2 via NASA POWER Monthly and Annual API",
                                   "version": "POWER API " + header["api"]["version"] + "; source " + ",".join(header["sources"]),
                                   "parameter": param, "nativeUnit": native, "url": entry["url"],
                                   "retrievedUtc": entry["retrievedUtc"], "rawFile": entry["file"],
                                   "citation": [core.POWER_CITATION, core.MERRA2_CITATION],
                                   "license": "NASA open Earth-science data; acknowledge NASA POWER and cite MERRA-2."},
                        "spatial": {"type": "grid-point", "name": name, "requestedCoordinates": {"lat": lat, "lon": lon},
                                    "returnedCoordinates": body["geometry"]["coordinates"], "gridCells": 1,
                                    "nativeResolution": "0.5° latitude x 0.625° longitude",
                                    "footprintNote": "Single POWER point/grid estimate, not an average of the named region. Exact cell bounds are not supplied in this response."},
                        "temporal": {"resolution": "monthly", "start": "1991-01", "end": "2025-12",
                                     "months": len(obs), "missingMonths": sum(o["value"] is None for o in obs),
                                     "timeStandard": header["time_standard"]},
                        "processing": ["Selected one POWER point; no regional averaging. POWER annual month 13 is omitted.",
                                       "Fill values become null; no interpolation."] +
                                      (["Converted mm/day to mm/month using calendar days, including leap years."] if param == "PRECTOTCORR" else []),
                        "caveats": ["Reanalysis, not a direct measurement. Changes in the observing system can affect trends.",
                                    "One grid estimate cannot represent the whole " + name + "."], "observations": obs})
    return out


def quality(series):
    obs = series["observations"]
    dates = [o["date"] for o in obs if o["value"] is not None]
    longest = run = 0
    years = {}
    for point in obs:
        run = run + 1 if point["value"] is None else 0
        longest = max(longest, run)
        years.setdefault(point["date"][:4], []).append(point["value"])
    complete = [int(y) for y, values in years.items() if len(values) == 12 and all(v is not None for v in values)]
    return {"coverage": [obs[0]["date"], obs[-1]["date"]], "cadence": "monthly",
            "firstValue": dates[0] if dates else None, "lastValue": dates[-1] if dates else None,
            "months": len(obs), "missingMonths": sum(o["value"] is None for o in obs),
            "longestMissingRunMonths": longest, "completeYears": complete,
            "excludedAnnualYears": [int(y) for y in years if int(y) not in complete],
            "monthsWithMissingDays": sum(bool(o.get("daysMissing")) for o in obs)}


def export_dataset(core, series, raw_manifest):
    s = series
    fingerprint = digest({"series": s, "processingVersion": PROCESSING_VERSION})
    game_region = "phoenix" if s["regionId"] == "region_01" else s["regionId"]
    spatial = s["spatial"]
    if spatial["type"] == "area-mean":
        b = spatial["bbox"]
        region = "Phoenix and central Arizona; area mean over %s–%s°N, %s–%s°E" % (b["latMin"], b["latMax"], b["lonMin"], b["lonMax"])
    elif spatial["type"] == "station":
        st = spatial["station"]
        region = "%s; station %s at %s°N, %s°E, %s m" % (st["name"], st["id"], st["lat"], st["lon"], st["elevation_m"])
    else:
        c = spatial["requestedCoordinates"]
        region = "%s; POWER point at %s°N, %s°E (not a region mean)" % (spatial["name"], c["lat"], c["lon"])
    source = s["source"]
    total = s["unit"] == "mm/month"
    q = quality(s)
    return {"schemaVersion": 1, "id": s["id"] + "." + PROCESSING_VERSION + "-" + fingerprint[:12],
            "seriesId": s["id"], "regionId": s["regionId"], "gameRegionId": game_region,
            "gameVariable": GAME_VARIABLES.get(s["variable"]), "variable": s["label"],
            "units": s["unit"], "region": region,
            "kind": "reanalysis" if s["kind"] == "reanalysis" else "observation", "providerKind": s["kind"],
            "statistic": "monthly total" if total else "monthly mean/index",
            "annualInterpretation": "equal-month mean of monthly totals (mm/month), not annual accumulation" if total else "equal-month arithmetic mean of twelve complete monthly values",
            "provenance": {"product": source["dataset"], "version": source["version"] + "; " + PROCESSING_VERSION + "; sha256 " + fingerprint,
                           "sourceUrl": source["url"], "retrievedAt": source["retrievedUtc"],
                           "license": source["license"], "redistribution": "unknown" if s["kind"] == "station" else "permitted",
                           "processing": s["processing"] + ["Exported by scripts/prepare_member3.py " + PROCESSING_VERSION + "; canonical values and nulls preserved."],
                           "limitations": s["caveats"] + ["No dataset-specific statistical review has been supplied. Significance is not established."]},
            "source": source, "rawSha256": raw_manifest[source["rawFile"]]["sha256"],
            "spatial": spatial, "temporal": s["temporal"], "definition": s["definition"],
            "quality": q, "reviewStatus": "pending-member-4", "observations": s["observations"]}


def release_files(core):
    root = Path(core.ROOT)
    manifest = core.load_manifest()
    points_manifest = json.loads((root / "data/raw/homecoming/manifest.json").read_text())
    manifest.update({e["file"]: e for e in points_manifest["files"]})
    collection = json.loads((root / "public/data/earth-observations.json").read_text())
    baselines = json.loads((root / "public/data/baselines.json").read_text())
    all_series = collection["series"] + point_series(core)
    # Reject physically invalid values, broken calendars and inconsistent missing counts
    # before producing either downloadable evidence or simulation baselines.
    for s in all_series:
        if [o["date"] for o in s["observations"]] != core.months(s["temporal"]["start"], s["temporal"]["end"]):
            raise ValueError("Nonconsecutive source calendar: " + s["id"])
        lo, hi = core.RANGES[s["variable"]]
        if s["variable"] == "precipitation" and s["regionId"] != "region_01":
            # The Phoenix-only 600 mm screen is unsuitable for tropical monsoons.
            # This broad input screen is not a regional climatological validation.
            hi = 3000
        if any(o["value"] is not None and (not math.isfinite(o["value"]) or not lo <= o["value"] <= hi) for o in s["observations"]):
            raise ValueError("Invalid physical value: " + s["id"])
        if sum(o["value"] is None for o in s["observations"]) != s["temporal"]["missingMonths"]:
            raise ValueError("Incorrect missing-month count: " + s["id"])
    datasets = [export_dataset(core, s, manifest) for s in all_series]
    outputs = {}
    for s, d in zip(all_series, datasets):
        outputs["data/processed/member4/" + s["id"] + ".json"] = d
    # NOAA exports are handed off locally; unknown redistribution is never expanded into the Archive.
    outputs["public/data/earth-archive.json"] = {"schemaVersion": 1, "datasets": [d for d in datasets if d["provenance"]["redistribution"] == "permitted"]}
    normals = {b["seriesId"]: b for b in baselines["baselines"]}
    catalog = {}
    for s, d in zip(all_series, datasets):
        if s["kind"] != "reanalysis" or s["variable"] not in GAME_VARIABLES:
            continue
        b = normals.get(s["id"])
        if b is None:
            b = {"period": {"start": "1991", "end": "2020"}, "monthly": core.climatology(s, 1991, 2020)}
        key = d["gameRegionId"]
        cat = catalog.setdefault(key, {"source": s["source"]["url"], "baseline": {"startYear": 1991, "endYear": 2020,
                                       "monthly": [{} for _ in range(12)], "counts": {}, "seriesIds": {}, "units": {}}})
        variable = GAME_VARIABLES[s["variable"]]
        for m, value in enumerate(b["monthly"]):
            if value["mean"] is None or value["n"] < 20:
                raise ValueError("Incomplete simulation normal: " + s["id"])
            cat["baseline"]["monthly"][m][variable] = value["mean"]
        cat["baseline"]["counts"][variable] = [m["n"] for m in b["monthly"]]
        cat["baseline"]["seriesIds"][variable] = d["id"]
        cat["baseline"]["units"][variable] = s["unit"]
    outputs["public/homecoming/regional-climate.json"] = catalog
    outputs["docs/member-3/quality-report.json"] = {"processingVersion": PROCESSING_VERSION,
        "note": "Cross-source monthly correlations are sanity checks and may reflect shared seasonality, not matching trends or causation.",
        "series": [{"seriesId": d["seriesId"], "datasetId": d["id"], "kind": d["providerKind"], "units": d["units"],
                    "spatial": d["spatial"], "quality": d["quality"], "processing": d["provenance"]["processing"],
                    "limitations": d["provenance"]["limitations"], "redistribution": d["provenance"]["redistribution"],
                    "reviewStatus": d["reviewStatus"]} for d in datasets]}
    outputs["docs/member-3/dataset-index.json"] = {"datasets": [{"id": d["id"], "seriesId": d["seriesId"],
        "path": "data/processed/member4/" + d["seriesId"] + ".json", "inArchive": d["provenance"]["redistribution"] == "permitted",
        "reviewStatus": d["reviewStatus"]} for d in datasets]}
    table = ["# Member 3 quality summary", "", "Generated from pinned inputs. Full footprints, flags, conversions and limitations are in `quality-report.json`; IDs and import paths are in `dataset-index.json`. All scientific reviews are pending.", "",
             "| Series | Kind | Units | Coverage | Missing / months | Longest gap | Complete years | Excluded annual years |",
             "| --- | --- | --- | --- | --- | --- | --- | --- |"]
    for d in datasets:
        q = d["quality"]
        table.append("| %s | %s | %s | %s–%s | %s / %s | %s months | %s | %s |" % (
            d["seriesId"], d["providerKind"], d["units"], *q["coverage"], q["missingMonths"], q["months"],
            q["longestMissingRunMonths"], len(q["completeYears"]), ", ".join(map(str, q["excludedAnnualYears"])) or "None"))
    table += ["", "Monthly cross-source correlations are QA checks; shared seasonality is not evidence of matching trends or causation.",
              "Station humidity is a cross-check only: its monthly aggregation is undocumented. The new public Archive excludes station exports with unconfirmed redistribution.", ""]
    outputs["docs/member-3/quality-report.md"] = "\n".join(table)
    return outputs


def build_release(core):
    for name, value in release_files(core).items():
        path = Path(core.ROOT) / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(value.encode() if isinstance(value, str) else encode(value))
    print("Built Member 3 contract exports, Archive, simulation normals, and quality report.")


def validate_release(core):
    # Validate the canonical values against the pinned raw records as well as validating
    # derived exports. Changing both a source JSON and its export cannot bypass this.
    root = Path(core.ROOT)
    manifest = core.load_manifest()
    expected_series = [core.build_power_series(p, manifest) for p in core.POWER_PARAMS]
    expected_series.append(core.build_imerg_series(manifest))
    station_path = root / "data/raw/noaa_gsom" / ("%s_gsom_%s_%s.csv" % (core.STATION["id"], core.START, core.END))
    rows = core.read_gsom(str(station_path))
    expected_series += [core.build_gsom_series(s, rows, manifest[str(station_path.relative_to(root))]) for s in core.GSOM_ELEMENTS]
    actual = json.loads((root / "public/data/earth-observations.json").read_text())
    normals = json.loads((root / "public/data/baselines.json").read_text())
    if actual["series"] != expected_series or normals["baselines"] != core.build_baselines(expected_series):
        raise ValueError("Canonical observations/baselines differ from pinned raw inputs; run build.")
    for name, expected in release_files(core).items():
        path = Path(core.ROOT) / name
        encoded = expected.encode() if isinstance(expected, str) else encode(expected)
        if not path.exists() or path.read_bytes() != encoded:
            raise ValueError("Stale or changed release file; run build: " + name)
        if name.startswith("data/processed/member4/") and (path.stat().st_size > 2_000_000 or len(expected["observations"]) > 2400):
            raise ValueError("Member 4 import limit exceeded: " + name)
    print("Member 3 release files match pinned raw inputs and canonical records.")
