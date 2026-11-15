"""Evaluate a daily Potomac runoff and routing candidate without changing the monthly screen."""

import argparse
import hashlib
import itertools
import json
import math
from datetime import date, timedelta
from pathlib import Path

import numpy as np
from shapely.geometry import box

import routing

STATION_NAMES = ("Paw Paw", "Hancock", "Point of Rocks")
PARAMETERS = {"PRECTOTCORR": "mm/day", "T2M_MIN": "C", "T2M_MAX": "C"}
SOURCE_FILES = ("rivers.zip", "basins.zip", "gauges.rdb", "streamflow.rdb", "power.json")
FIRST_DATE = date(routing.FIRST_YEAR, 1, 1)
DATES = [FIRST_DATE + timedelta(days=i) for i in range(sum(routing.DAYS))]
DATE_KEYS = [day.strftime("%Y%m%d") for day in DATES]
MONTH_STARTS = tuple(itertools.accumulate([0, *routing.DAYS[:-1]]))
MONTH_DAYS = np.array(routing.DAYS)
CALIBRATION = slice(routing.WARMUP_YEARS * 12, (routing.TRAIN_END_YEAR - routing.FIRST_YEAR + 1) * 12)
TEST = slice(CALIBRATION.stop, len(MONTH_DAYS))
TRAIN_DAYS = slice(MONTH_STARTS[CALIBRATION.start], MONTH_STARTS[CALIBRATION.stop])
TEST_DAYS = slice(TRAIN_DAYS.stop, len(DATES))
LAND_GRID = (
    (100, 200, 350, 500),  # soil capacity, mm
    (1, 2, 4),            # relative wetness exponent for recharge
    (0.75, 1, 1.25),      # reference ET multiplier
    (0.25, 0.5, 0.75),    # fast share of recharge
    (0.25, 0.5, 1),       # daily fast release fraction
)
REACH_GRID = tuple(itertools.product((0, 1, 2, 3), (0, 0.25, 0.5, 0.75)))
BYPASS = (0, 0)
SLOW_RELEASE = 0.03
MELT_RATE = 3.0
ET_WETNESS_THRESHOLD = 0.7
PULSE_DATE = date(2019, 8, 31)
PULSE_M3S = 10.0
PULSE_M3 = PULSE_M3S * routing.SECONDS_PER_DAY
VISIBLE_CHANGE_M3S = 0.01
DELTA_TOLERANCE_M3 = 1e-5


def monthly_mean(daily_m3):
    return np.add.reduceat(daily_m3, MONTH_STARTS) / (MONTH_DAYS * routing.SECONDS_PER_DAY)


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def daily_grid(folder):
    monthly = json.loads((folder / "power.json").read_text())
    expected = {tuple(feature["geometry"]["coordinates"][:2]) for feature in monthly["features"]}
    grid = {}
    for path in sorted((folder / "daily").glob("*.json")):
        response = json.loads(path.read_text())
        coordinates = tuple(response["geometry"]["coordinates"][:2])
        header = response["header"]
        if (
            coordinates in grid
            or header["time_standard"] != "LST"
            or "MERRA2" not in header["sources"]
            or [header["start"], header["end"]] != [DATE_KEYS[0], DATE_KEYS[-1]]
        ):
            raise ValueError(f"Unexpected grid position or source in {path}")
        data = response["properties"]["parameter"]
        for name, units in PARAMETERS.items():
            if response["parameters"][name]["units"] != units or set(data[name]) != set(DATE_KEYS):
                raise ValueError(f"Unexpected units or dates for {name} in {path}")
        values = np.array([[data[name][d] for d in DATE_KEYS] for name in PARAMETERS])
        rain, minimum, maximum = values
        if (
            not np.all(np.isfinite(values))
            or np.any(values == header["fill_value"])
            or np.any(rain < 0)
            or np.any(minimum > maximum)
        ):
            raise ValueError(f"Invalid precipitation or temperature in {path}")
        grid[coordinates] = values
    if expected != grid.keys():
        missing = sorted(expected - grid.keys())
        extra = sorted(grid.keys() - expected)
        raise ValueError(f"Daily POWER grid differs from the screen. Missing centers: {missing}. Extra: {extra}.")
    return grid


def catchment_weather(polygon, area_km2, grid):
    rain = np.zeros(len(DATES))
    mean_temp = np.zeros(len(DATES))
    et_reference = np.zeros(len(DATES))
    covered_km2 = 0.0
    # FAO-56 equations 21, 23–25 and 52, with radiation converted to mm/day.
    solar_angle = np.array([math.tau / 365 * day.timetuple().tm_yday for day in DATES])
    declination = 0.409 * np.sin(solar_angle - 1.39)
    distance = 1 + 0.033 * np.cos(solar_angle)
    for (lon, lat), (precipitation, minimum, maximum) in grid.items():
        cell = box(lon - .3125, lat - .25, lon + .3125, lat + .25)
        overlap = routing.area_square_km(polygon.intersection(cell))
        if overlap == 0:
            continue
        covered_km2 += overlap
        weight = overlap / area_km2
        latitude = math.radians(lat)
        sunset = np.arccos(-math.tan(latitude) * np.tan(declination))
        radiation = 1440 / math.pi * 0.0820 * distance * (
            sunset * math.sin(latitude) * np.sin(declination)
            + math.cos(latitude) * np.cos(declination) * np.sin(sunset)
        )
        temperature = (minimum + maximum) / 2
        et = 0.0023 * np.maximum(temperature + 17.8, 0) * np.sqrt(maximum - minimum) * radiation * 0.408
        rain += weight * precipitation
        mean_temp += weight * temperature
        et_reference += weight * et
    if abs(covered_km2 / area_km2 - 1) > .001:
        raise ValueError("POWER cells do not cover the incremental catchment")
    return list(zip(rain.tolist(), mean_temp.tolist(), et_reference.tolist()))


def check_balance(residual, inputs):
    if abs(residual) > 1e-12 * max(1.0, inputs):
        raise ArithmeticError(f"Water ledger residual {residual} exceeds tolerance for input {inputs}")


def runoff(weather, parameters, area_km2):
    """HBV-style snow and soil stores feeding parallel fast and slow runoff reservoirs."""
    capacity, exponent, et_scale, fast_share, fast_release = parameters
    snow = soil = fast = slow = 0.0
    exports = []
    evaporation = []
    for precipitation, temperature, reference_et in weather:
        if temperature < 0:
            snow += precipitation
            liquid = 0.0
        else:
            melt = min(snow, MELT_RATE * temperature)
            snow -= melt
            liquid = precipitation + melt
        recharge = liquid * (soil / capacity) ** exponent
        soil += liquid - recharge
        overflow = max(0, soil - capacity)
        soil -= overflow
        recharge += overflow
        et = min(soil, reference_et * et_scale * min(1, soil / (capacity * ET_WETNESS_THRESHOLD)))
        soil -= et
        evaporation.append(et)
        fast += recharge * fast_share
        slow += recharge * (1 - fast_share)
        fast_out = fast * fast_release
        slow_out = slow * SLOW_RELEASE
        fast -= fast_out
        slow -= slow_out
        exports.append(fast_out + slow_out)
    rain = math.fsum(row[0] for row in weather)
    evap = math.fsum(evaporation)
    stored = math.fsum((snow, soil, fast, slow))
    check_balance(rain - evap - stored - math.fsum(exports), rain)
    mm_to_m3 = area_km2 * 1000
    return np.array(exports) * mm_to_m3, evap * mm_to_m3, stored * mm_to_m3


def route(upstream_m3, parameters):
    lag, retention = parameters
    delayed = np.zeros_like(upstream_m3)
    start = max(0, len(upstream_m3) - lag)
    delayed[lag:] = upstream_m3[:start]
    out, stored, _ = routing.release(delayed, retention)
    in_transit = stored + math.fsum(upstream_m3[start:])
    total = math.fsum(upstream_m3)
    check_balance(total - math.fsum(out) - in_transit, total)
    return np.array(out), in_transit


def mae_m3s(predicted_m3, observed_m3, indexes):
    return float(np.mean(np.abs(predicted_m3[indexes] - observed_m3[indexes]))) / routing.SECONDS_PER_DAY


def calibrate(weather, observed_m3, upstream_m3, area_km2, reaches):
    """Fit the daily MAE on the training slice only, with a separate bypass fit."""
    reference_fit = best_fit = (math.inf, (), BYPASS)
    upstream_options = {options: route(upstream_m3, options)[0] for options in reaches}
    for land in itertools.product(*LAND_GRID):
        local, _, _ = runoff(weather, land, area_km2)
        for reach, incoming in upstream_options.items():
            error = mae_m3s(local + incoming, observed_m3, TRAIN_DAYS)
            candidate = (error, land, reach)
            best_fit = min(best_fit, candidate)
            if reach == BYPASS:
                reference_fit = min(reference_fit, candidate)
    return best_fit, reference_fit


def run_network(local_flows, reach_parameters):
    outflows = []
    reach_storage = []
    upstream = np.zeros(len(local_flows[0]))
    for local, parameters in zip(local_flows, reach_parameters, strict=True):
        incoming, stored = route(upstream, parameters)
        upstream = local + incoming
        outflows.append(upstream)
        reach_storage.append(stored)
    return outflows, reach_storage


def pulse_check(local_flows, reach_parameters):
    start = (PULSE_DATE - FIRST_DATE).days
    stop = (date(PULSE_DATE.year + 1, 1, 1) - FIRST_DATE).days
    local_baseline = [flow[:stop] for flow in local_flows]
    base, base_storage = run_network(local_baseline, reach_parameters)
    edited_origin = local_baseline[0].copy()
    edited_origin[start] += PULSE_M3
    edited, edited_storage = run_network([edited_origin, *local_baseline[1:]], reach_parameters)
    expected = np.zeros(stop - start)
    expected[0] = PULSE_M3
    details = []
    for baseline, intervention, parameters in zip(base, edited, reach_parameters, strict=True):
        lag, retain = parameters
        kernel = [0] * lag + [(1 - retain) * retain ** day for day in range(stop - start - lag)]
        expected = np.convolve(expected, kernel)[:stop - start]
        observed = intervention - baseline
        if (
            np.any(np.abs(observed[:start]) > DELTA_TOLERANCE_M3)
            or np.any(observed < -DELTA_TOLERANCE_M3)
            or not np.allclose(observed[start:], expected, rtol=0, atol=DELTA_TOLERANCE_M3)
        ):
            raise ArithmeticError("The paired run disagrees with the analytic pulse response")
        onset = int(np.flatnonzero(expected > DELTA_TOLERANCE_M3)[0]) + start
        peak = int(np.argmax(expected)) + start
        months = []
        for month in range(1, 13):
            index = (PULSE_DATE.year - routing.FIRST_YEAR) * 12 + month - 1
            a = MONTH_STARTS[index]
            b = a + routing.DAYS[index]
            seconds = (b - a) * routing.SECONDS_PER_DAY
            months.append({
                "month": f"{PULSE_DATE.year}-{month:02}",
                "baselineM3s": math.fsum(baseline[a:b]) / seconds,
                "interventionM3s": math.fsum(intervention[a:b]) / seconds,
                "deltaM3s": math.fsum(observed[a:b]) / seconds,
            })
        details.append({
            "firstResponseDay": DATES[onset].isoformat(),
            "peakResponseDay": DATES[peak].isoformat(),
            "peakIncreaseM3s": float(observed[peak]) / routing.SECONDS_PER_DAY,
            "increasePastGaugeByYearEndM3": math.fsum(observed),
            "months": months,
        })
    storage_delta = [b - a for a, b in zip(base_storage, edited_storage)]
    residual = PULSE_M3 - math.fsum(storage_delta) - details[-1]["increasePastGaugeByYearEndM3"]
    if abs(residual) > DELTA_TOLERANCE_M3:
        raise ArithmeticError("The differential network ledger failed")
    return {
        "name": "Paw Paw low-flow augmentation pulse",
        "injectOn": PULSE_DATE.isoformat(),
        "dailyMeanIncreaseM3s": PULSE_M3S,
        "injectedM3": PULSE_M3,
        "absoluteCheckToleranceM3": DELTA_TOLERANCE_M3,
        "analyticResponseMatches": True,
        "terminalAdditionalReachStorageM3": dict(zip(routing.STATIONS, storage_delta)),
        "networkResidualM3": residual,
        "stations": dict(zip(routing.STATIONS, details)),
    }


def evaluate(folder):
    grid = daily_grid(folder)
    catchments, polygons = routing.build_network(folder)
    upstream = np.zeros(len(DATES))
    local_flows = []
    reach_parameters = []
    rain_m3 = evap_m3 = storage_m3 = 0.0
    for index, (node, polygon) in enumerate(zip(catchments, polygons, strict=True)):
        observed, estimates = routing.daily_discharge(folder / "streamflow.rdb", node["station"])
        observed_m3 = np.array(list(itertools.chain.from_iterable(observed))) * routing.SECONDS_PER_DAY
        area = node["incrementalAreaKm2"]
        weather = catchment_weather(polygon, area, grid)
        reaches = REACH_GRID if index else (BYPASS,)
        fit, reference = calibrate(
            weather[:TRAIN_DAYS.stop], observed_m3[:TRAIN_DAYS.stop], upstream[:TRAIN_DAYS.stop], area, reaches
        )
        _, land, reach = fit
        local, evap, land_storage = runoff(weather, land, area)
        incoming, reach_storage = route(upstream, reach)
        predicted = local + incoming
        bypass_local, _, _ = runoff(weather, reference[1], area)
        bypass = bypass_local + upstream
        observed_monthly = monthly_mean(observed_m3)
        normal_flow = np.mean(observed_monthly[CALIBRATION].reshape(-1, 12), axis=0)
        error = float(np.mean(np.abs(monthly_mean(predicted)[TEST] - observed_monthly[TEST])))
        climatology_error = float(np.mean(np.abs(normal_flow - observed_monthly[TEST].reshape(-1, 12))))
        bypass_error = float(np.mean(np.abs(monthly_mean(bypass)[TEST] - observed_monthly[TEST])))
        season_skill = 1 - error / climatology_error
        route_skill = 1 - error / bypass_error if index else None
        daily_error = mae_m3s(predicted, observed_m3, TEST_DAYS)
        bypass_daily_error = mae_m3s(bypass, observed_m3, TEST_DAYS) if index else None
        lag, retention = reach
        timing_supported = not index or (
            (lag > 0 or retention > 0) and daily_error < bypass_daily_error
        )
        node.update({
            "name": STATION_NAMES[index],
            "estimatedApprovedDischargeDays": estimates,
            "landParameters": land,
            "sameMonthInflowLandParameters": reference[1] if index else None,
            "reachLagDays": lag,
            "reachDailyRetentionFraction": retention,
            "fittingDailyMAE_m3s": fit[0],
            "withheldDailyMAE_m3s": daily_error,
            "withheldSameDayMAE_m3s": bypass_daily_error,
            "verificationMAE_m3s": error,
            "climatologyMAE_m3s": climatology_error,
            "sameMonthInflowMAE_m3s": bypass_error if index else None,
            "skillVsClimatology": season_skill,
            "skillVsSameMonthInflow": route_skill,
            "dailyTimingCriterionMet": timing_supported if index else None,
            "passesScreen": season_skill >= routing.MIN_ROUTE_SKILL
            and (index == 0 or route_skill >= routing.MIN_ROUTE_SKILL and timing_supported),
        })
        upstream = predicted
        rain_m3 += math.fsum(row[0] for row in weather) * area * 1000
        evap_m3 += evap
        storage_m3 += land_storage + reach_storage
        local_flows.append(local)
        reach_parameters.append(reach)

    outlet = math.fsum(upstream)
    residual = math.fsum((rain_m3, -evap_m3, -storage_m3, -outlet))
    check_balance(residual, rain_m3)
    pulse = pulse_check(local_flows, reach_parameters)
    visible = all(
        max(month["deltaM3s"] for month in station["months"]) >= VISIBLE_CHANGE_M3S
        for station in pulse["stations"].values()
    )
    passes = visible and all(node["passesScreen"] for node in catchments)
    return {
        "schemaVersion": 1,
        "purpose": "Research diagnostic of daily runoff and lag-reservoir routing",
        "modelTimeStep": "day",
        "screenMetric": "MAE of calendar-month mean discharge, m3/s",
        "warmup": ["2000-01", "2001-12"],
        "training": ["2002-01", "2014-12"],
        "withheld": ["2015-01", "2024-12"],
        "withheldMonths": TEST.stop - TEST.start,
        "withheldDays": TEST_DAYS.stop - TEST_DAYS.start,
        "minimumMAESkill": routing.MIN_ROUTE_SKILL,
        "minimumMonthlyDisplayChangeM3s": VISIBLE_CHANGE_M3S,
        "landParameterOrder": ["capacityMm", "rechargeExponent", "referenceETMultiplier",
                               "fastRechargeShare", "fastDailyReleaseFraction"],
        "inputSha256": {str(path.relative_to(folder)): sha256(path) for path in
                        [*(folder / name for name in SOURCE_FILES), *sorted((folder / "daily").glob("*.json"))]},
        "catchments": catchments,
        "networkLedger": {
            "precipitationInputM3": rain_m3,
            "evapotranspirationM3": evap_m3,
            "terminalStorageM3": storage_m3,
            "outletExportM3": outlet,
            "residualM3": residual,
        },
        "pulse": pulse,
        "pulseMeetsMonthlyDisplayThreshold": visible,
        "routePassesScreen": passes,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sources", type=Path, help="Baseline input folder plus daily/*.json POWER grid-center files")
    folder = parser.parse_args().sources
    missing = [name for name in SOURCE_FILES if not (folder / name).is_file()]
    if not (folder / "daily").is_dir():
        missing.append("daily/")
    if missing:
        parser.error(f"Missing inputs in {folder}: {', '.join(missing)}. See docs/homecoming-routing-forecast.md.")
    try:
        audit = evaluate(folder)
    except (OSError, KeyError, ValueError) as error:
        parser.error(f"Could not use the source data. {error}\nSee docs/homecoming-routing-forecast.md.")
    print(json.dumps(audit, indent=2, allow_nan=False))


if __name__ == "__main__":
    main()
