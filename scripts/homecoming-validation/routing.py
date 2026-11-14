"""Screen a three-gauge Potomac routing candidate against held-out USGS discharge.

Dependencies for this offline research script: pyshp, shapely, pyproj.
See docs/homecoming-routing-forecast.md for source URLs and usage.
"""
import argparse
import calendar
import hashlib
import json
import math
import statistics
from collections import defaultdict
from io import BytesIO
from pathlib import Path
from zipfile import ZipFile

import shapefile
from pyproj import Transformer
from shapely import transform
from shapely.geometry import Point, box, shape
from shapely.ops import unary_union

STATIONS = ("01610000", "01613000", "01638500")
EXTENT = (-81, 37, -75, 41)
FIRST_YEAR = 2000
LAST_YEAR = 2024
TRAIN_END_YEAR = 2014
WARMUP_YEARS = 2
MIN_ROUTE_SKILL = 0.05
FRACTIONS = tuple(i / 20 for i in range(1, 21))
RESIDENCE = (0, .10, .25, .40, .55, .70, .85)
SQUARE_MI_TO_SQUARE_KM = 2.589988110336
CFS_TO_CMS = 0.3048 ** 3
SECONDS_PER_DAY = 86400
DAYS = [calendar.monthrange(y, m)[1] for y in range(FIRST_YEAR, LAST_YEAR + 1) for m in range(1, 13)]
PROJECT = Transformer.from_crs("EPSG:4326", "EPSG:5070", always_xy=True).transform
RIVERS = "HydroRIVERS_v10_na_shp/HydroRIVERS_v10_na.shp"
BASINS = "hybas_na_lev12_v1c.shp"


def area_square_km(geometry):
    return transform(geometry, PROJECT, interleaved=False).area / 1e6


def load_shapes(archive_path, shapefile_path, id_field):
    with ZipFile(archive_path) as archive:
        files = [archive.read(str(Path(shapefile_path).with_suffix(ext))) for ext in (".shp", ".shx", ".dbf")]
    with shapefile.Reader(shp=BytesIO(files[0]), shx=BytesIO(files[1]), dbf=BytesIO(files[2])) as reader:
        return {row.record[id_field]: row for row in reader.iterShapeRecords(bbox=EXTENT)}


def station_info(path):
    stations = {}
    for line in path.read_text().splitlines():
        if line.startswith("USGS\t"):
            fields = line.split("\t")
            stations[fields[1]] = (Point(float(fields[7]), float(fields[6])), float(fields[29]) * SQUARE_MI_TO_SQUARE_KM)
    return [stations[site] for site in STATIONS]


def downstream_path(graph, start, end):
    visited = {start}
    result = [start]
    while result[-1] != end:
        following = graph[result[-1]].record.NEXT_DOWN
        if following not in graph or following in visited:
            raise ValueError(f"No acyclic directed path from {start} to {end}")
        visited.add(following)
        result.append(following)
    return result


def build_network(folder):
    rivers = load_shapes(folder / "rivers.zip", RIVERS, "HYRIV_ID")
    basins = load_shapes(folder / "basins.zip", BASINS, "HYBAS_ID")
    stations = station_info(folder / "gauges.rdb")
    sites = []
    for point, gauge_area in stations:
        approximate = point.buffer(.02)
        projected = transform(point, PROJECT, interleaved=False)
        nearby = []
        for reach in rivers.values():
            if abs(reach.record.UPLAND_SKM - gauge_area) > .05 * gauge_area:
                continue
            line = shape(reach.shape.__geo_interface__)
            if approximate.intersects(line):
                distance = transform(line, PROJECT, interleaved=False).distance(projected)
                nearby.append((distance, reach.record.HYRIV_ID, reach.record.HYBAS_L12))
        if not nearby:
            raise ValueError(f"No eligible HydroRIVERS snap for {point}")
        sites.append(min(nearby))

    upstream = defaultdict(list)
    for basin_id, row in basins.items():
        upstream[row.record.NEXT_DOWN].append(basin_id)
    previous = set()
    catchments = []
    geometry = []
    for index, (station, (distance, reach, basin), (_, gauge_area)) in enumerate(
        zip(STATIONS, sites, stations)
    ):
        stack = [basin]
        contributing = set()
        while stack:
            identifier = stack.pop()
            if identifier in contributing:
                raise ValueError("Cycle in HydroBASINS topology")
            contributing.add(identifier)
            stack.extend(upstream[identifier])
        if not previous <= contributing:
            raise ValueError("HydroBASINS catchments are not nested")
        incremental = contributing - previous
        polygon = unary_union([shape(basins[part].shape.__geo_interface__) for part in sorted(incremental)])
        a = area_square_km(polygon)
        total = a + (catchments[-1]["totalAreaKm2"] if index else 0)
        if abs(total - gauge_area) > .03 * gauge_area:
            raise ValueError(f"{station} catchment area disagrees with USGS")
        catchments.append({
            "station": station, "reachId": reach, "basinId": basin, "snapDistanceM": round(distance, 1),
            "incrementalAreaKm2": a, "totalAreaKm2": total, "gaugeAreaKm2": gauge_area,
            "incrementalBasinCount": len(incremental),
            **({
                "riverPathFromPreviousGauge": downstream_path(rivers, sites[index - 1][1], reach),
                "basinPathFromPreviousGauge": downstream_path(basins, sites[index - 1][2], basin),
            } if index else {}),
        })
        geometry.append(polygon)
        previous = contributing
    return catchments, geometry


def precipitation(path, catchment, area):
    response = json.loads(path.read_text())
    if response["header"]["sources"] != ["MERRA2"] or response["parameters"]["PRECTOTCORR"]["units"] != "mm/day":
        raise ValueError("Unexpected NASA POWER precipitation product or units")
    weighted = [0.0] * len(DAYS)
    covered = 0
    for feature in response["features"]:
        lon, lat = feature["geometry"]["coordinates"][:2]
        cell = box(lon - .3125, lat - .25, lon + .3125, lat + .25)
        overlap = area_square_km(catchment.intersection(cell))
        if overlap == 0:
            continue
        covered += overlap
        values = feature["properties"]["parameter"]["PRECTOTCORR"]
        for month, days in enumerate(DAYS):
            key = f"{FIRST_YEAR + month // 12:04}{1 + month % 12:02}"
            value = values[key]
            if not math.isfinite(value) or value < 0:
                raise ValueError(f"Missing or invalid precipitation for {key}")
            weighted[month] += value * days * overlap / area
    if abs(covered / area - 1) > .001:
        raise ValueError(f"POWER cells cover {covered / area:.1%} of the catchment")
    return weighted


def discharge(path, station):
    months = defaultdict(dict)
    estimates = 0
    for line in path.read_text().splitlines():
        fields = line.split("\t")
        if fields[:2] != ["USGS", station]:
            continue
        _, _, day, value, flag = fields
        if flag not in {"A", "A:e"}:
            raise ValueError(f"{station} {day} has unapproved discharge")
        year, month, dom = map(int, day.split("-"))
        position = (year - FIRST_YEAR) * 12 + month - 1
        if dom in months[position] or not math.isfinite(float(value)) or float(value) < 0:
            raise ValueError(f"Duplicate or invalid discharge at {station} {day}")
        months[position][dom] = float(value) * CFS_TO_CMS
        estimates += flag == "A:e"
    if any(sorted(months[m]) != list(range(1, day_count + 1)) for m, day_count in enumerate(DAYS)):
        raise ValueError(f"{station} has incomplete months")
    return [math.fsum(months[m].values()) * SECONDS_PER_DAY for m in range(len(DAYS))], estimates


def release(inputs, fraction_retained):
    """Linear reservoir, empty at initialization. Export and terminal storage are in cubic metres."""
    if not (0 <= fraction_retained < 1):
        raise ValueError("Retention must be in [0, 1)")
    store = 0.0
    outflow = []
    for volume in inputs:
        if not math.isfinite(volume) or volume < 0:
            raise ValueError("Input volume must be finite and nonnegative")
        available = store + volume
        store = available * fraction_retained
        outflow.append(available - store)
    residual = math.fsum(inputs) - math.fsum(outflow) - store
    if abs(residual) > 1e-12 * max(1, math.fsum(inputs)):
        raise ArithmeticError("Water ledger failed")
    return outflow, store, residual


def mae(predictions, observations, indexes):
    return statistics.mean(
        abs(predictions[i] - observations[i]) / (DAYS[i] * SECONDS_PER_DAY) for i in indexes
    )


def evaluate(folder):
    catchments, geometry = build_network(folder)
    split = (TRAIN_END_YEAR - FIRST_YEAR + 1) * 12
    training = range(WARMUP_YEARS * 12, split)
    withheld = range(split, len(DAYS))
    upstream = [0.0] * len(DAYS)
    total_rain = 0.0
    total_unrouted = 0.0
    total_stored = 0.0
    for i, (node, polygon) in enumerate(zip(catchments, geometry)):
        actual, estimated_days = discharge(folder / "streamflow.rdb", node["station"])
        area = node["incrementalAreaKm2"]
        rain = [depth * area * 1000 for depth in precipitation(folder / "power.json", polygon, area)]
        fit = []
        for fraction in FRACTIONS:
            runoff = [v * fraction for v in rain]
            for land_retention in RESIDENCE:
                local, land_store, land_residual = release(runoff, land_retention)
                for reach_retention in (RESIDENCE if i else (0,)):
                    incoming, reach_store, reach_residual = release(upstream, reach_retention)
                    predicted = [a + b for a, b in zip(local, incoming)]
                    fit.append((mae(predicted, actual, training), fraction, land_retention, reach_retention,
                                predicted, land_store, reach_store, land_residual + reach_residual))
        train_error, fraction, land, reach, predicted, land_store, reach_store, residual = min(
            fit, key=lambda candidate: candidate[:4]
        )
        # A separately fitted zero-retention reach is a stronger benchmark than
        # bypassing the reach reservoir with the routed model's land parameters.
        reference = min((candidate for candidate in fit if candidate[3] == 0), key=lambda c: c[:4])
        normal_flow = [
            statistics.mean(actual[m] / (DAYS[m] * SECONDS_PER_DAY) for m in training if m % 12 == month)
            for month in range(12)
        ]
        climatology = [normal_flow[m % 12] * days * SECONDS_PER_DAY for m, days in enumerate(DAYS)]
        error = mae(predicted, actual, withheld)
        season_error = mae(climatology, actual, withheld)
        unrouted_error = mae(reference[4], actual, withheld)
        season_skill = 1 - error / season_error if season_error else None
        route_skill = 1 - error / unrouted_error if unrouted_error else None
        node.update({
            "estimatedApprovedDischargeDays": estimated_days,
            "rainfallRunoffFraction": fraction, "landRetentionFraction": land, "reachRetentionFraction": reach,
            "fittingMAE_m3s": train_error, "verificationMAE_m3s": error,
            "climatologyMAE_m3s": season_error, "sameMonthInflowMAE_m3s": unrouted_error if i else None,
            "skillVsClimatology": season_skill, "skillVsSameMonthInflow": route_skill if i else None,
            "localWaterRetainedAtEndM3": land_store, "riverWaterRetainedAtEndM3": reach_store,
            "reservoirBalanceResidualM3": residual,
            "passesScreen": season_skill is not None and season_skill >= MIN_ROUTE_SKILL
                and (i == 0 or route_skill is not None and route_skill >= MIN_ROUTE_SKILL),
        })
        upstream = predicted
        total_rain += math.fsum(rain)
        total_unrouted += math.fsum(rain) * (1 - fraction)
        total_stored += land_store + reach_store
    outflow = math.fsum(upstream)
    balance = total_rain - total_unrouted - total_stored - outflow
    if abs(balance) > 1e-12 * total_rain:
        raise ArithmeticError("Network water ledger failed")
    return catchments, {
        "precipitationInputM3": total_rain,
        "precipitationNotAssignedToRunoffM3": total_unrouted,
        "terminalStorageM3": total_stored,
        "outletExportM3": outflow,
        "residualM3": balance,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sources", type=Path, help="Folder with downloaded archives and observed data")
    folder = parser.parse_args().sources
    catchments, ledger = evaluate(folder)
    hashes = {
        name: hashlib.sha256((folder / name).read_bytes()).hexdigest()
        for name in ("rivers.zip", "basins.zip", "gauges.rdb", "streamflow.rdb", "power.json")
    }
    print(json.dumps({
        "schemaVersion": 1,
        "inputSha256": hashes,
        "warmup": ["2000-01", "2001-12"],
        "training": ["2002-01", "2014-12"],
        "withheld": ["2015-01", "2024-12"],
        "withheldMonths": (LAST_YEAR - TRAIN_END_YEAR) * 12,
        "minimumMAESkill": MIN_ROUTE_SKILL,
        "catchments": catchments,
        "networkLedger": ledger,
        "released": False,
        "withheldReason": "Research screening model. Player routing requires verified links and a runoff model suitable for perturbation.",
    }, indent=2, allow_nan=False))


if __name__ == "__main__":
    main()
