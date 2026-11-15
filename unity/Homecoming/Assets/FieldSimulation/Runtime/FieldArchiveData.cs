using System;
using System.Linq;
using System.Globalization;
using UnityEngine;

namespace Homecoming.Field
{
    [Serializable] sealed class FieldArchivePack
    {
        public int schemaVersion;
        public ArchiveRecord[] evidence;
        public ArchiveComparison[] comparisons;
    }

    [Serializable] sealed class ArchiveRecord
    {
        public string id, regionLabel, sourceParameter, fingerprint, rawSha256, timeStandard, coverageStart, coverageEnd;
        public ArchiveFootprint footprint;
        public ArchiveProvenance provenance;
        public ArchiveQuality quality;
        public ArchiveTrend finding, anomalyFinding;
        public ArchiveTrend[] monthFindings;
    }

    [Serializable] sealed class ArchiveProvenance
    {
        public string product, sourceUrl, version, retrievedAt, license;
        public string[] processing, limitations;
        public ArchiveSource source;
    }

    [Serializable] sealed class ArchiveSource
    {
        public string parameter, version, nativeUnit;
        public string[] citation;
    }

    [Serializable] sealed class ArchiveQuality
    {
        public int months, missingMonths, longestMissingRunMonths;
        public int[] completeYears, excludedAnnualYears;
    }

    [Serializable] sealed class ArchiveFootprint
    {
        public string type, nativeResolution;
        public int gridCells;
        public ArchivePoint point;
        public float[] bbox;
        public ArchivePoint[] cells;
    }

    [Serializable] sealed class ArchivePoint { public float latitude, longitude, weight; }

    [Serializable] sealed class ArchiveComparison
    {
        public string id, type, question, scope, leftDatasetId, rightDatasetId;
        public ArchiveTrend left, right, difference;
    }

    [Serializable] sealed class ArchiveTrend
    {
        public string id, datasetId, fingerprint, unit, aggregation, mode, status, firstDate, lastDate;
        public string[] reasons;
        public bool hasSlope;
        public int n, startYear, endYear, month;
        public float slopePerDecade, magnitude;
        public ArchiveBaseline baseline;
        public ArchiveTrendPoint[] points;
        public ArchiveTest test;
        public ArchiveReview review;
    }

    [Serializable] struct ArchiveTrendPoint { public string date; public float t, value; }
    [Serializable] sealed class ArchiveBaseline { public string type; public int startYear, endYear; }
    [Serializable] sealed class ArchiveTest
    {
        public string method, hypothesis;
        public float p, alpha, slopePerDecade, lowerPerDecade, upperPerDecade, tStatistic;
        public int degreesOfFreedom;
    }
    [Serializable] sealed class ArchiveReview { public string id, reviewer, rationale; public bool accepted; }

    public sealed partial class FieldSimulation
    {
        FieldArchivePack historicalPack;
        ArchiveComparison guidedRain;
        EvidenceSeries guidedSahel, guidedChile, guidedTemperature;
        bool guidedRecordsChecked;
        ArchiveTrend pairedTrend;
        int archiveMode, archiveMonth, archiveValuePage;
        bool showArchiveValues;
        string archiveRecordId;
        string comparisonScope;

        ArchiveRecord Exported(EvidenceSeries record)
        {
            historicalPack ??= JsonUtility.FromJson<FieldArchivePack>(dataSource.text);
            return historicalPack?.evidence?.FirstOrDefault(e => e.id == record.id);
        }

        static string ArchivePlace(EvidenceSeries record) => record.region switch
        {
            "central-chile" => "Central Chile",
            "custom" => "Custom Earth location",
            _ => null
        };

        ConnectedArchiveEvidence ConnectedFor(EvidenceSeries record) => record == connectedSeries ? connectedArchive :
            record == archiveOrigin?.record ? archiveOrigin?.connected : null;

        ArchiveFootprint DataFootprint(EvidenceSeries record) => ConnectedFor(record)?.footprint ?? Exported(record)?.footprint;

        string FootprintType(EvidenceSeries record)
        {
            var footprint = DataFootprint(record);
            if (!string.IsNullOrEmpty(footprint?.type)) return footprint.type;
            if (record.url?.Contains("/monthly/point?") == true) return "grid-point";
            if (record.url?.Contains("/monthly/regional?") == true || record.url?.Contains("bbox=") == true) return "area";
            return null;
        }

        string ArchiveFootprint(EvidenceSeries record)
        {
            var footprint = DataFootprint(record);
            if (footprint?.type == "area-mean" && footprint.bbox is { Length: 4 } bounds)
                return $"{PlaceName(record)} · area mean {Coordinate(bounds[1],bounds[0])} to {Coordinate(bounds[3],bounds[2])}";
            if (FootprintType(record) == "grid-point" && LocateFootprint(record,out var point))
                return $"{PlaceName(record)} · POWER point {Coordinate(point.x,point.y)}";
            return Exported(record)?.regionLabel ?? (record.region == "phoenix"
                ? "Central Arizona · area 32.5–34.5°N, 111–113°W" : PlaceName(record) + " · see source for footprint");
        }

        static string Coordinate(float lat, float lon) => FormattableString.Invariant(
            $"{Mathf.Abs(lat):0.#####}°{(lat < 0 ? "S" : "N")} {Mathf.Abs(lon):0.#####}°{(lon < 0 ? "W" : "E")}");

        bool LocateFootprint(EvidenceSeries record, out Vector2 location)
        {
            var footprint = DataFootprint(record);
            if (footprint?.type == "grid-point" && footprint.point != null)
            {
                location = new Vector2(footprint.point.latitude,footprint.point.longitude);
                return true;
            }
            // The v1 evidence pack retained the original POWER source URL but omitted footprint metadata.
            if (record.url?.Contains("/monthly/point?") == true &&
                TrySourceCoordinate(record.url,"latitude",out float lat) && TrySourceCoordinate(record.url,"longitude",out float lon))
            {
                location = new Vector2(lat,lon);
                return true;
            }
            location = default;
            return false;
        }

        static bool TrySourceCoordinate(string source, string key, out float value)
        {
            string token = source.Split('?', '&').FirstOrDefault(entry => entry.StartsWith(key+"=",StringComparison.Ordinal));
            return float.TryParse(token?.Substring(key.Length+1),NumberStyles.Float,Invariant,out value);
        }

        bool SameScope(EvidenceSeries first, EvidenceSeries second)
        {
            var a = Exported(first);
            var b = Exported(second);
            string footprint = FootprintType(first);
            return footprint != null && footprint == FootprintType(second) && first.provider == second.provider &&
                a?.timeStandard == b?.timeStandard && a?.sourceParameter == b?.sourceParameter &&
                a?.footprint?.nativeResolution == b?.footprint?.nativeResolution;
        }

        void ApplyArchiveReview(EvidenceSeries record, FiledFinding target, ArchiveTrend overrideTrend = null)
        {
            var connected = ConnectedFor(record);
            var exported = Exported(record);
            target.connectedEvidence = connected;
            target.datasetId = record.id;
            target.rawSha256 = connected?.dataset.rawSha256 ?? exported?.rawSha256;
            target.fingerprint = connected?.finding.fingerprint ?? exported?.fingerprint;
            var trend = overrideTrend ?? connected?.finding ?? exported?.finding;
            if (trend == null) return;
            bool reviewed = HasReviewedTest(trend) && IsReviewedArchiveScope(record,trend);
            target.id = trend.id;
            target.window = $"{trend.firstDate}–{trend.lastDate}";
            target.aggregation = trend.aggregation;
            target.mode = trend.mode;
            target.count = trend.n;
            target.hasRate = trend.hasSlope;
            target.ratePerDecade = trend.slopePerDecade;
            target.fittedChange = trend.magnitude;
            target.status = reviewed ? trend.status : "not-established";
            target.significance = target.status switch
            {
                "significant" => "Statistically significant", "not-significant" => "Not statistically significant",
                _ => "Significance not established"
            };
            target.reason = trend.reasons is { Length: > 0 } ? string.Join(" ",trend.reasons) :
                reviewed ? null : "No matching statistical review is available for this view.";
            if (connected != null)
            {
                var requested = connected.requested;
                target.place = $"Requested {Coordinate(requested.latitude,requested.longitude)}";
                target.footprint = ArchiveFootprint(record);
                return;
            }
            if (!string.IsNullOrEmpty(trend.review?.id))
            {
                target.review = trend.review.id;
                target.reviewer = trend.review.reviewer;
                target.reviewRationale = trend.review.rationale;
            }
            if (!reviewed) return;
            target.test = trend.test.method;
            target.hypothesis = trend.test.hypothesis;
            target.hasPValue = target.hasInterval = true;
            target.pValue = trend.test.p;
            CopyTestStatistic(trend.test,target);
            target.alpha = trend.test.alpha;
            target.hasOlsRate = true;
            target.olsRatePerDecade = trend.test.slopePerDecade;
            target.intervalLow = trend.test.lowerPerDecade;
            target.intervalHigh = trend.test.upperPerDecade;
        }

        static void CopyTestStatistic(ArchiveTest test, FiledFinding target)
        {
            if (test?.degreesOfFreedom > 0 && float.IsFinite(test.tStatistic))
            {
                target.tStatistic = test.tStatistic;
                target.degreesOfFreedom = test.degreesOfFreedom;
            }
        }

        static bool HasReviewedTest(ArchiveTrend trend) => trend?.review?.accepted == true &&
            trend.status is ("significant" or "not-significant") && !string.IsNullOrEmpty(trend.test?.method) &&
            trend.test.alpha is > 0 and <= 1 && trend.test.p is >= 0 and <= 1 &&
            (trend.test.p < trend.test.alpha) == (trend.status == "significant") &&
            float.IsFinite(trend.test.slopePerDecade) && float.IsFinite(trend.test.lowerPerDecade) &&
            float.IsFinite(trend.test.upperPerDecade) && trend.test.lowerPerDecade <= trend.test.upperPerDecade;

        bool IsReviewedArchiveScope(EvidenceSeries record, ArchiveTrend trend)
        {
            CheckGuidedRecords();
            return (record == guidedTemperature && trend == Exported(record)?.finding) ||
                (record == guidedSahel && trend == guidedRain?.difference);
        }

        static bool MatchesCompleteAnnual(EvidenceSeries record, ArchiveTrend trend, int start, int end)
        {
            int count = end - start + 1;
            if (trend?.mode != "annual" || trend.unit != record.unit || trend.aggregation != record.aggregation ||
                !trend.hasSlope || trend.n != count || record.years.Length != count || trend.points?.Length != count) return false;
            for (int i = 0; i < count; i++)
            {
                var point = trend.points[i];
                int year = start + i;
                if (point.date != year.ToString(Invariant) || point.t != year ||
                    record.years[i].year != year || point.value != record.years[i].value) return false;
            }
            return true;
        }

        void CheckGuidedRecords()
        {
            if (guidedRecordsChecked) return;
            guidedRecordsChecked = true;
            historicalPack ??= JsonUtility.FromJson<FieldArchivePack>(dataSource.text);
            if (historicalPack?.schemaVersion != 2) return;
            var temperature = data.evidence.FirstOrDefault(r => r.region == GuidedSoilOrigin && r.key == "temperature" &&
                r.provider == "reanalysis" && r.unit == "°C" && Usable(r));
            if (temperature != null)
            {
                var metadata = Exported(temperature);
                var finding = metadata?.finding;
                if (metadata?.sourceParameter == "T2M" &&
                    metadata.footprint?.type == "area-mean" && metadata.footprint.bbox != null &&
                    metadata.footprint.bbox.SequenceEqual(new[] { -113f, 32.5f, -111f, 34.5f }) &&
                    MatchesCompleteAnnual(temperature,finding,1981,2025) && HasReviewedTest(finding) &&
                    finding.datasetId == metadata.id && !string.IsNullOrEmpty(metadata.rawSha256) &&
                    !string.IsNullOrEmpty(metadata.fingerprint) && finding.id == $"historical-v2:{metadata.fingerprint}:1981:2025:annual:-" &&
                    finding.review.id == "phoenix-temperature-review-v1")
                    guidedTemperature = temperature;
            }
            var pair = historicalPack.comparisons?.FirstOrDefault(c => c.id == "rain-two-ways" && c.type == "historical-comparison");
            if (pair == null) return;
            var a = data.evidence.FirstOrDefault(r => r.id == pair.leftDatasetId);
            var b = data.evidence.FirstOrDefault(r => r.id == pair.rightDatasetId);
            if (!Comparable(a,b) || a.region != "sahel" || b.region != "central-chile" ||
                a.unit != "mm/month" || a.key != "rain" || a.provider != "reanalysis" || !SameScope(a,b)) return;
            var left = Exported(a);
            var right = Exported(b);
            if (left?.sourceParameter != "PRECTOTCORR" || left.footprint?.point == null || right?.footprint?.point == null ||
                left.footprint.point.latitude != 14 || left.footprint.point.longitude != 6 ||
                right.footprint.point.latitude != -36 || right.footprint.point.longitude != -72 ||
                pair.left?.id != left.finding?.id || pair.right?.id != right.finding?.id ||
                !MatchesCompleteAnnual(a,pair.left,1991,2025) || !MatchesCompleteAnnual(b,pair.right,1991,2025) ||
                left.finding?.status != "not-established" || right.finding?.status != "not-established") return;
            var delta = pair.difference;
            if (!HasReviewedTest(delta) || delta.review.id != "rain-two-ways-paired-review-v1" ||
                delta.mode != "annual" || delta.unit != a.unit || !delta.hasSlope || delta.n != 35 || delta.points?.Length != 35) return;
            for (int i = 0; i < delta.points.Length; i++)
            {
                var point = delta.points[i];
                if (point.date != pair.left.points[i].date || point.t != pair.left.points[i].t ||
                    !float.IsFinite(point.value) || Mathf.Abs(point.value - (a.years[i].value - b.years[i].value)) > .00002f) return;
            }
            guidedRain = pair;
            guidedSahel = a;
            guidedChile = b;
        }

        bool TryGetReviewedPair(out EvidenceSeries first, out EvidenceSeries second, out FiledFinding difference)
        {
            CheckGuidedRecords();
            first = guidedSahel;
            second = guidedChile;
            difference = null;
            if (guidedRain == null) return false;
            comparisonScope = guidedRain.scope;
            pairedTrend = guidedRain.difference;
            difference = MakeFinding(first,pairedTrend);
            difference.datasetId = null;
            difference.fingerprint = difference.rawSha256 = null;
            difference.place = PlaceName(first) + " minus " + PlaceName(second);
            difference.footprint = ArchiveFootprint(first) + " / " + ArchiveFootprint(second);
            return true;
        }

        EvidenceSeries FindFiledRecord(FiledFinding finding) => finding == null || (finding.mode != null && finding.mode != "annual") ? null :
            data.evidence.FirstOrDefault(r => r.id == (finding.datasetId ?? finding.id));

        void DrawDescentFootprint()
        {
            var record = ArchiveRain(data,Region.id);
            if (record == null) return;
            if (LocateFootprint(record,out var point))
            {
                DrawFootprintPin(point,$"{Region.name} grid point",Mint);
                return;
            }
            var bounds = Exported(record)?.footprint?.bbox;
            if (bounds?.Length != 4) return;
            var southWest = new Vector2(bounds[1],bounds[0]);
            var northWest = new Vector2(bounds[3],bounds[0]);
            var northEast = new Vector2(bounds[3],bounds[2]);
            var southEast = new Vector2(bounds[1],bounds[2]);
            void Edge(Vector2 start,Vector2 end)
            {
                if (world.ProjectFootprint(start,end,0,out var a) && world.ProjectFootprint(start,end,1,out var b))
                    DrawChartSegment(ScreenToField(a),ScreenToField(b),Mint);
            }
            Edge(southWest,northWest);
            Edge(northWest,northEast);
            Edge(northEast,southEast);
            Edge(southEast,southWest);
            Tag(837,434,"HISTORICAL RAINFALL FOOTPRINT",Mint);
        }

        ArchiveTrend SelectArchiveTrend(EvidenceSeries record)
        {
            var export = Exported(record);
            return archiveMode == 1 && export?.monthFindings?.Length == 12 ? export.monthFindings[archiveMonth] :
                archiveMode == 2 ? export?.anomalyFinding : export?.finding;
        }

    }
}
