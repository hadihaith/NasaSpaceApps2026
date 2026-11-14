"""Replay station-temperature benchmarks against later NOAA GSOM observations."""
import argparse
import calendar
import csv
import hashlib
import json
import math
import re
import statistics
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from zipfile import ZipFile

START_YEAR = 2015
END_YEAR = 2024
MIN_CLIMATOLOGY_YEARS = 8
DAMPING_MONTHS = 2
REPO = Path(__file__).resolve().parents[2]
GSOM = REPO / "data/raw/noaa_gsom/USW00023183_gsom_1981-01_2025-12.csv"
REPORT_NAME = re.compile(r"CLMPHX_(\d{12})\.txt")
REPORT_PERIOD = re.compile(
    r"(?:CLIMATE SUMMARY FOR THE MONTH OF|PHOENIX WEATHER STATISTICS FOR) ([A-Z]+)\s+(\d{4})"
)
REPORT_TEMPERATURE = re.compile(r"^(?:MEAN|AVERAGE MONTHLY TEMPERATURE)\s+(-?\d+(?:\.\d+)?)\s", re.M)


def month_number(date):
    year, month = (int(part) for part in date[:7].split("-"))
    return year * 12 + month - 1


def month_label(number):
    return f"{number // 12:04}-{number % 12 + 1:02}"


def report_revisions(paths):
    """Use the archived NWS timestamp in the member name, in UTC."""
    unique = {}
    for path in sorted(paths):
        with ZipFile(path) as archive:
            for info in archive.infolist():
                match = REPORT_NAME.fullmatch(info.filename)
                if not match:
                    raise ValueError(f"Unexpected NWS archive member: {info.filename}")
                issued = datetime.strptime(match[1], "%Y%m%d%H%M").replace(tzinfo=timezone.utc)
                raw = archive.read(info).decode("latin1")
                if not re.search(r"^(?:CX\w+ KPSR|TTAA00 KPHX) ", raw, re.M):
                    raise ValueError(f"Unexpected source for {info.filename}")
                period = REPORT_PERIOD.search(raw)
                value = REPORT_TEMPERATURE.search(raw)
                if not period or not value:
                    continue
                value_c = (float(value[1]) - 32) * 5 / 9
                period_month = list(calendar.month_name).index(period[1].title())
                month = month_number(f"{period[2]}-{period_month:02}")
                if month >= month_number(issued.isoformat()):
                    raise ValueError(f"Report includes a month still in progress: {info.filename}")
                key = (issued, month)
                if key in unique and unique[key] != value_c:
                    raise ValueError(f"Conflicting temperatures in {info.filename}")
                unique[key] = value_c
    revisions = sorted((issued, month, value) for (issued, month), value in unique.items())
    if not revisions:
        raise ValueError("No readable monthly NWS temperature reports.")
    return revisions


def verify(revisions, observed):
    results = defaultdict(list)
    issues = []
    for first_month in range(START_YEAR * 12, (END_YEAR + 1) * 12):
        issued = datetime.fromisoformat(month_label(first_month) + "-01T00:00:00+00:00")
        known = {
            month: (valid_from, value)
            for valid_from, month, value in revisions
            if valid_from <= issued and month < first_month
        }
        if not known:
            continue
        climatology = {}
        for calendar_month in range(12):
            values = [value for month, (_, value) in known.items() if month % 12 == calendar_month]
            if len(values) >= MIN_CLIMATOLOGY_YEARS:
                climatology[calendar_month] = statistics.mean(values)
        previous = max(known)
        if previous % 12 not in climatology:
            continue
        available, last_value = known[previous]
        anomaly = last_value - climatology[previous % 12]
        leads = []
        for lead in range(1, 13):
            month = first_month + lead - 1
            if month not in observed or month % 12 not in climatology:
                continue
            actual = observed[month]
            seasonal = climatology[month % 12]
            damped = seasonal + anomaly * math.exp(-(month - previous) / DAMPING_MONTHS)
            leads.append([lead, round(actual, 5), round(last_value, 5), round(seasonal, 5), round(damped, 5)])
            results[lead].append((actual, last_value, seasonal, damped))
        if leads:
            issues.append({
                "issuedAt": issued.isoformat(),
                "lastObservedMonth": month_label(previous),
                "lastReportIssuedAt": available.isoformat(),
                "leads": leads,
            })
    metrics = []
    for lead, rows in sorted(results.items()):
        persistence, climatology, damped = (
            statistics.mean(abs(row[0] - row[index]) for row in rows) for index in (1, 2, 3)
        )
        metrics.append({
            "lead": lead,
            "n": len(rows),
            "persistenceMAE": round(persistence, 5),
            "climatologyMAE": round(climatology, 5),
            "dampedPersistenceMAE": round(damped, 5),
            "dampedSkillVsPersistence": round(1 - damped / persistence, 5) if persistence else None,
            "dampedSkillVsClimatology": round(1 - damped / climatology, 5) if climatology else None,
        })
    return metrics, issues


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archives", type=Path, nargs="+", help="IEM ZIPs of NWS CLMPHX products")
    parser.add_argument("--issues", action="store_true", help="Include dated hindcasts and source timestamps")
    args = parser.parse_args()
    revisions = report_revisions(args.archives)
    with GSOM.open(newline="") as stream:
        observed = {
            month_number(row["DATE"]): float(row["TAVG"])
            for row in csv.DictReader(stream)
            if row["TAVG"] and not row["TAVG_ATTRIBUTES"].split(",")[0]
        }
    metrics, issues = verify(revisions, observed)
    if not metrics:
        raise ValueError("There are no matched hindcasts and independent verification targets.")
    if any(issue["lastReportIssuedAt"] > issue["issuedAt"] for issue in issues):
        raise ValueError("A replay used a report issued after the forecast origin.")
    print(json.dumps({
        "schemaVersion": 1,
        "station": "USW00023183",
        "variable": "monthly mean air temperature",
        "unit": "°C",
        "issueWindow": [START_YEAR, END_YEAR],
        "nArchivedReportRevisions": len(revisions),
        "targetSha256": hashlib.sha256(GSOM.read_bytes()).hexdigest(),
        "archivedInputRevisionsSha256": hashlib.sha256(
            json.dumps([(issued.isoformat(), month_label(month), value) for issued, month, value in revisions]).encode()
        ).hexdigest(),
        "metrics": metrics,
        "publishedForecastLeads": [],
        "withheldReason": "Benchmark replay only. There is no verified ensemble-driven model or calibrated uncertainty.",
        **({"issueColumns": ["lead", "observed", "persistence", "climatology", "dampedPersistence"], "issues": issues}
           if args.issues else {}),
    }, indent=2, ensure_ascii=False, allow_nan=False))


if __name__ == "__main__":
    main()
