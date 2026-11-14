using System;
using System.Linq;
using System.Globalization;
using UnityEngine;

namespace Homecoming.Field
{
    [Serializable] sealed class FieldArchivePack
    {
        public ArchiveRecord[] evidence;
        public ArchiveComparison[] comparisons;
    }

    [Serializable] sealed class ArchiveRecord
    {
        public string id, regionLabel, sourceParameter, fingerprint, rawSha256, timeStandard;
        public ArchiveFootprint footprint;
        public ArchiveTrend finding, anomalyFinding;
        public ArchiveTrend[] monthFindings;
    }

    [Serializable] sealed class ArchiveFootprint
    {
        public string type, nativeResolution;
        public ArchivePoint point;
        public float[] bbox;
    }

    [Serializable] sealed class ArchivePoint { public float latitude, longitude; }

    [Serializable] sealed class ArchiveComparison
    {
        public string id, type, question, scope, leftDatasetId, rightDatasetId;
        public ArchiveTrend left, right, difference;
    }

    [Serializable] sealed class ArchiveTrend
    {
        public string id, datasetId, unit, aggregation, mode, status, firstDate, lastDate;
        public string[] reasons;
        public bool hasSlope;
        public int n;
        public float slopePerDecade, magnitude;
        public ArchiveTrendPoint[] points;
        public ArchiveTest test;
        public ArchiveReview review;
    }

    [Serializable] struct ArchiveTrendPoint { public string date; public float t, value; }
    [Serializable] sealed class ArchiveTest { public string method; public float p, alpha, lowerPerDecade, upperPerDecade; }
    [Serializable] sealed class ArchiveReview { public string id, reviewer, rationale; }

    public sealed partial class FieldSimulation
    {
        FieldArchivePack historicalPack;
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

        static string ArchivePlace(EvidenceSeries record) => record.region == "central-chile" ? "Central Chile" : null;

        string FootprintType(EvidenceSeries record)
        {
            var footprint = Exported(record)?.footprint;
            if (!string.IsNullOrEmpty(footprint?.type)) return footprint.type;
            if (record.url?.Contains("/monthly/point?") == true) return "grid-point";
            if (record.url?.Contains("/monthly/regional?") == true || record.url?.Contains("bbox=") == true) return "area";
            return null;
        }

        string ArchiveFootprint(EvidenceSeries record)
        {
            var footprint = Exported(record)?.footprint;
            if (footprint?.type == "area-mean" && footprint.bbox is { Length: 4 } bounds)
                return $"{PlaceName(record)} · area {Coordinate(bounds[1],bounds[0])} to {Coordinate(bounds[3],bounds[2])}";
            if (FootprintType(record) == "grid-point" && LocateFootprint(record,out var point))
                return $"{PlaceName(record)} · grid point {Coordinate(point.x,point.y)}";
            return Exported(record)?.regionLabel ?? (record.region == "phoenix"
                ? "Central Arizona · area 32.5–34.5°N, 111–113°W" : PlaceName(record) + " · see source for footprint");
        }

        static string Coordinate(float lat, float lon) => $"{Mathf.Abs(lat):0.###}°{(lat < 0 ? "S" : "N")} {Mathf.Abs(lon):0.###}°{(lon < 0 ? "W" : "E")}";

        bool LocateFootprint(EvidenceSeries record, out Vector2 location)
        {
            var footprint = Exported(record)?.footprint;
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
            var exported = Exported(record);
            target.datasetId = record.id;
            target.rawSha256 = exported?.rawSha256;
            target.fingerprint = exported?.fingerprint;
            var trend = overrideTrend ?? exported?.finding;
            if (trend == null) return;
            target.id = trend.id;
            target.window = $"{trend.firstDate}–{trend.lastDate}";
            target.aggregation = trend.aggregation;
            target.mode = trend.mode;
            target.count = trend.n;
            target.hasRate = trend.hasSlope;
            target.ratePerDecade = trend.slopePerDecade;
            target.fittedChange = trend.magnitude;
            target.significance = trend.status switch
            {
                "significant" => "Statistically significant", "not-significant" => "Not statistically significant",
                _ => "Significance not established"
            };
            target.reason = trend.reasons is { Length: > 0 } ? string.Join(" ",trend.reasons) : null;
            if (!string.IsNullOrEmpty(trend.review?.id))
            {
                target.review = trend.review.id;
                target.reviewer = trend.review.reviewer;
                target.reviewRationale = trend.review.rationale;
            }
            if (string.IsNullOrEmpty(trend.test?.method) || trend.status == "not-established") return;
            target.test = trend.test.method;
            target.hasPValue = target.hasInterval = true;
            target.pValue = trend.test.p;
            target.alpha = trend.test.alpha;
            target.intervalLow = trend.test.lowerPerDecade;
            target.intervalHigh = trend.test.upperPerDecade;
        }

        bool TryGetReviewedPair(out EvidenceSeries first, out EvidenceSeries second, out FiledFinding difference)
        {
            first = second = null;
            difference = null;
            historicalPack ??= JsonUtility.FromJson<FieldArchivePack>(dataSource.text);
            var pair = historicalPack?.comparisons?.FirstOrDefault(c => c.id == "rain-two-ways" && c.type == "historical-comparison");
            if (pair == null) return false;
            var a = data.evidence.FirstOrDefault(r => r.id == pair.leftDatasetId);
            var b = data.evidence.FirstOrDefault(r => r.id == pair.rightDatasetId);
            if (!Usable(a) || !Usable(b) || pair.difference == null || !SameScope(a,b)) return false;
            // Prefer the exact finding/view IDs from the packaged paired analysis.
            if (pair.left.id != Exported(a)?.finding?.id || pair.right.id != Exported(b)?.finding?.id || !Comparable(a,b)) return false;
            first = a;
            second = b;
            comparisonScope = pair.scope;
            pairedTrend = pair.difference;
            difference = MakeFinding(first);
            ApplyArchiveReview(first,difference,pair.difference);
            difference.datasetId = null;
            difference.fingerprint = difference.rawSha256 = null;
            difference.place = PlaceName(a) + " minus " + PlaceName(b);
            difference.footprint = ArchiveFootprint(a) + " / " + ArchiveFootprint(b);
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
