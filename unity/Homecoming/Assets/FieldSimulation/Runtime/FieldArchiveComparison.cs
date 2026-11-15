using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Homecoming.Field
{
    [Serializable] sealed class ArchivePairRequest
    {
        public string leftId, rightId, mode;
        public int month;
        public ArchiveTrendPoint[] left, right;
    }

    [Serializable] sealed class ArchivePairAnalysis
    {
        public int schemaVersion, n, month;
        public string leftId, rightId, mode, firstDate, lastDate;
        public ArchiveExploratoryFit left, right, difference;
    }

    [Serializable] sealed class ArchiveExploratoryFit
    {
        public string outcome;
        public float ratePerDecade, magnitude, alpha, adjustedP, adjustedT, mkP, mkS, mkZ, lowerPerDecade, upperPerDecade;
        public bool hasAdjustedT;
    }

    public sealed partial class FieldSimulation
    {
        sealed class ArchiveReading
        {
            public EvidenceSeries record;
            public ArchiveTrend trend;
            public ConnectedArchiveEvidence connected;
            public string place;
        }

        ArchiveReading archiveOrigin, archivePeer;
        ArchiveTrend archiveMatchedLeft, archiveMatchedRight, archiveDelta;
        ArchivePairAnalysis archivePairAnalysis;
        FiledFinding archiveReviewedDifference;
        UnityWebRequest archivePairRequest;
        string archivePairError;
        bool archivePairDetails;

        void ClearArchivePair()
        {
            CancelArchivePair();
            archivePeer = null;
            archiveMatchedLeft = archiveMatchedRight = archiveDelta = null;
            archiveReviewedDifference = null;
            archivePairAnalysis = null;
            archivePairError = null;
            archivePairDetails = false;
        }

        void CancelArchivePair()
        {
            if (archivePairRequest == null) return;
            archivePairRequest.Abort();
            archivePairRequest.Dispose();
            archivePairRequest = null;
        }

        ArchiveReading TakeArchiveReading(EvidenceSeries record, ArchiveTrend trend)
        {
            var connected = ConnectedFor(record);
            return new ArchiveReading
            {
                record = record, trend = trend, connected = connected,
                place = connected == null ? PlaceName(record) :
                    Coordinate(connected.requested.latitude,connected.requested.longitude)
            };
        }

        void BeginArchiveComparison(EvidenceSeries record, ArchiveTrend trend)
        {
            if (trend?.points is not { Length: > 0 }) return;
            var reading = TakeArchiveReading(record,trend);
            InvalidateConnectedArchive();
            archiveOrigin = reading;
            archiveRequestedPoint = null;
            archiveLatitude = archiveLongitude = "";
            archiveFilter = ArchiveFilter.Place;
        }

        void ReturnToArchiveOrigin()
        {
            var origin = archiveOrigin;
            InvalidateConnectedArchive();
            archiveOrigin = null;
            archiveFilter = ArchiveFilter.None;
            archiveRecordId = origin.record.id;
            if (origin.connected != null)
            {
                var point = origin.connected.requested;
                archiveRequestedPoint = new Vector2(point.latitude,point.longitude);
                archiveLatitude = point.latitude.ToString("R",Invariant);
                archiveLongitude = point.longitude.ToString("R",Invariant);
                connectedArchive = origin.connected;
                connectedSeries = origin.record;
                return;
            }
            archiveRequestedPoint = null;
            archivePlaceId = origin.record.region;
            var records = ArchiveRecordsFor(archivePlaceId);
            evidenceIndex = Math.Max(0,Array.IndexOf(records,origin.record));
        }

        EvidenceSeries[] ArchiveRecordsFor(string place) =>
            data.evidence.Where(e => e.region == place && (evidenceKey == null || e.key == evidenceKey)).ToArray();

        bool CompatibleArchiveRecord(EvidenceSeries other, ArchiveTrend trend)
        {
            var origin = archiveOrigin;
            if (other == null || trend == null || other.id == origin.record.id || other.product != origin.record.product ||
                other.key != origin.record.key || trend.unit != origin.trend.unit || trend.mode != origin.trend.mode ||
                trend.month != origin.trend.month || trend.aggregation != origin.trend.aggregation ||
                DataFootprint(origin.record)?.nativeResolution != DataFootprint(other)?.nativeResolution ||
                FootprintType(origin.record) != FootprintType(other))
                return false;
            if (trend.mode == "anomaly" && (trend.baseline?.type != origin.trend.baseline?.type ||
                trend.baseline?.startYear != origin.trend.baseline?.startYear || trend.baseline?.endYear != origin.trend.baseline?.endYear))
                return false;
            var connected = ConnectedFor(other);
            if (origin.connected != null)
            {
                var a = origin.connected.dataset;
                return connected != null && connected.size == origin.connected.size &&
                    !SameArchiveFootprint(origin.connected.footprint,connected.footprint) &&
                    trend.startYear == origin.trend.startYear && trend.endYear == origin.trend.endYear &&
                    !string.IsNullOrEmpty(a.temporal.timeStandard) && a.temporal.timeStandard == connected.dataset.temporal.timeStandard &&
                    a.source.parameter == connected.dataset.source.parameter && a.source.nativeUnit == connected.dataset.source.nativeUnit &&
                    a.source.version == connected.dataset.source.version;
            }
            return connected == null && other.region != origin.record.region && SameScope(origin.record,other) &&
                SameArchiveExtent(DataFootprint(origin.record),DataFootprint(other));
        }

        static bool SameArchiveExtent(ArchiveFootprint a, ArchiveFootprint b) => a?.type == "grid-point" ||
            (a?.bbox is { Length: 4 } x && b?.bbox is { Length: 4 } y &&
                Mathf.Approximately(x[2]-x[0],y[2]-y[0]) && Mathf.Approximately(x[3]-x[1],y[3]-y[1]));

        static bool SameArchiveFootprint(ArchiveFootprint a, ArchiveFootprint b) => a.type == "grid-point"
            ? a.point.latitude == b.point.latitude && a.point.longitude == b.point.longitude
            : a.bbox.SequenceEqual(b.bbox);

        void SelectArchivePeer(EvidenceSeries record, Vector2 center)
        {
            if (archiveOrigin.connected != null)
            {
                SetArchivePoint(center);
                StartArchiveRequest();
            }
            else
            {
                var other = data.evidence.FirstOrDefault(e => e.region == record.region &&
                    CompatibleArchiveRecord(e,SelectArchiveTrend(e)));
                if (other == null)
                {
                    archivePairError = "That place has no offline record with the same coverage and measurement.";
                    return;
                }
                CompareArchiveRecord(other,SelectArchiveTrend(other));
                archiveFilter = ArchiveFilter.None;
            }
        }

        void CompareArchiveRecord(EvidenceSeries other, ArchiveTrend trend)
        {
            ClearArchivePair();
            if (!CompatibleArchiveRecord(other,trend))
            {
                archivePairError = "Choose a different place with matching source, coverage, and time settings.";
                return;
            }

            archivePeer = TakeArchiveReading(other,trend);
            var rightByDate = trend.points.ToDictionary(p => p.date,StringComparer.Ordinal);
            var left = archiveOrigin.trend.points.Where(p => rightByDate.ContainsKey(p.date)).ToArray();
            var right = left.Select(p => rightByDate[p.date]).ToArray();
            archiveMatchedLeft = MatchedArchiveTrend(archiveOrigin.trend,left);
            archiveMatchedRight = MatchedArchiveTrend(trend,right);
            archiveDelta = MatchedArchiveTrend(archiveOrigin.trend,left.Zip(right,(a,b) =>
                new ArchiveTrendPoint { date = a.date, t = a.t, value = a.value-b.value }).ToArray());

            CheckGuidedRecords();
            if (guidedRain != null && archiveOrigin.record == guidedKuwait && other == guidedUk &&
                archiveOrigin.trend.id == guidedRain.left.id && trend.id == guidedRain.right.id &&
                archiveDelta.points.Select(p => p.date).SequenceEqual(guidedRain.difference.points.Select(p => p.date)))
            {
                archiveDelta = guidedRain.difference;
                archiveReviewedDifference = MakeFinding(guidedKuwait,archiveDelta);
            }
            else if (archiveOrigin.connected != null && archiveDelta.n >= 10) StartArchivePair();
        }

        static ArchiveTrend MatchedArchiveTrend(ArchiveTrend source, ArchiveTrendPoint[] points)
        {
            var matched = new ArchiveTrend
            {
                unit = source.unit, mode = source.mode, aggregation = source.aggregation, points = points,
                n = points.Length, firstDate = points.FirstOrDefault().date, lastDate = points.LastOrDefault().date
            };
            // Theil–Sen uses the median slope between observations. It remains descriptive
            // when the optional test service is unavailable.
            if (points.Length < 10) return matched;
            var slopes = new double[points.Length*(points.Length-1)/2];
            int index = 0;
            for (int i = 0; i < points.Length-1; i++)
                for (int j = i+1; j < points.Length; j++)
                    slopes[index++] = ((double)points[j].value-points[i].value) / ((double)points[j].t-points[i].t);
            Array.Sort(slopes);
            matched.slopePerDecade = (float)(5*(slopes[(index-1)/2]+slopes[index/2]));
            matched.hasSlope = true;
            return matched;
        }

        void StartArchivePair()
        {
            try
            {
                var uri = new UriBuilder(ArchiveEndpoint);
                uri.Path = uri.Path.TrimEnd('/') + "/compare";
                var payload = new ArchivePairRequest
                {
                    leftId = archiveOrigin.trend.id, rightId = archivePeer.trend.id, mode = archiveOrigin.trend.mode,
                    month = archiveMonth+1, left = archiveMatchedLeft.points, right = archiveMatchedRight.points
                };
                archivePairRequest = new UnityWebRequest(uri.Uri,UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload))),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = 15
                };
                archivePairRequest.SetRequestHeader("Content-Type","application/json");
                archivePairRequest.SendWebRequest();
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or UriFormatException)
            {
                CancelArchivePair();
                archivePairError = "The exploratory test service is unavailable.";
            }
        }

        void TickArchivePair()
        {
            if (archivePairRequest == null || !archivePairRequest.isDone) return;
            using var request = archivePairRequest;
            archivePairRequest = null;
            if (view != View.Archive || archiveOrigin == null || archivePeer == null) return;
            var result = request.result == UnityWebRequest.Result.Success
                ? DecodeArchiveJson<ArchivePairAnalysis>(request.downloadHandler.text) : null;
            if (result?.schemaVersion != 1 || result.leftId != archiveOrigin.trend.id || result.rightId != archivePeer.trend.id ||
                result.mode != archiveDelta.mode || result.month != archiveMonth+1 || result.n != archiveDelta.n ||
                result.firstDate != archiveDelta.firstDate || result.lastDate != archiveDelta.lastDate)
            {
                archivePairError = "The exploratory test service is unavailable.";
                return;
            }
            archivePairAnalysis = result;
        }

        void DrawArchiveComparison()
        {
            if (Button(Box(56,219,218,42),"Back to place A",icon:"Back"))
            {
                ReturnToArchiveOrigin();
                return;
            }
            string lens = archiveOrigin.trend.mode switch
            {
                "month" => Invariant.DateTimeFormat.GetMonthName(archiveOrigin.trend.month),
                "anomaly" => "Monthly anomalies",
                _ => "Annual values"
            };
            var footprint = DataFootprint(archiveOrigin.record)?.type == "area-mean" ? "Area mean" : "POWER point";
            Text(Box(293,219,934,42),$"{archiveOrigin.record.variable} · {footprint} · {lens} · {archiveOrigin.record.unit}",20,White,strongFont);
            bool mapOpen = archiveFilter == ArchiveFilter.Place;
            if (Button(Box(1287,219,257,42),mapOpen && archivePeer != null ? "Back to comparison" : "Choose place B",
                active:mapOpen,icon:"Location"))
                archiveFilter = mapOpen && archivePeer != null ? ArchiveFilter.None : ArchiveFilter.Place;

            Text(Box(56,270,725,41),"A   " + archiveOrigin.place,24,Aqua,strongFont);
            string peer = archivePeer?.place ?? (archiveRequestedPoint is Vector2 point ? Coordinate(point.x,point.y) : "Choose a place");
            Text(Box(804,270,720,41),"B   " + peer,24,Yellow,strongFont);

            if (archiveFilter == ArchiveFilter.Place)
            {
                DrawArchiveMap();
                return;
            }
            if (archivePeer == null)
            {
                if (archiveRequestedPoint.HasValue && connectedSeries == null) DrawConnectedArchive();
                else
                {
                    Card(Box(56,333,1488,160),Panel);
                    Text(Box(84,351,1410,56),archivePairError ?? "Choose a second place to compare.",27,White,headingFont);
                    if (Button(Box(84,426,244,43),"Choose place B",accent:true,icon:"Location")) archiveFilter = ArchiveFilter.Place;
                }
                return;
            }

            Card(Box(56,330,1488,489),Panel);
            string headline = archiveReviewedDifference?.status switch
            {
                "significant" => "The test found a statistically significant trend in A minus B.",
                "not-significant" => "The test did not find a statistically significant trend in A minus B.",
                _ => "Statistical significance has not been established for this difference."
            };
            if (archiveDelta.n == 0) headline = "The records have no usable dates in common.";
            Text(Box(83,342,1423,50),headline,31,White,headingFont);
            string window = archiveDelta.n == 0 ? "No matched values" :
                $"{archiveDelta.firstDate}–{archiveDelta.lastDate} · {archiveDelta.n} matched values";
            float? deltaRate = archiveReviewedDifference?.hasOlsRate == true ? archiveReviewedDifference.olsRatePerDecade :
                archiveDelta.hasSlope ? archiveDelta.slopePerDecade : null;
            Text(Box(84,396,1409,25),deltaRate.HasValue
                ? $"{TrendRate("A minus B",deltaRate.Value,archiveDelta.unit)}  {window}" : $"{window} · {archiveDelta.unit}",20,Dim);
            string left = PairedArchiveRate(archivePairAnalysis?.left,archiveMatchedLeft,archiveOrigin.trend);
            string right = PairedArchiveRate(archivePairAnalysis?.right,archiveMatchedRight,archivePeer.trend);
            Text(Box(84,427,704,33),"A   "+left,21,Aqua);
            Text(Box(804,427,704,33),"B   "+right,21,Yellow);

            if (archivePairDetails) DrawArchivePairDetails();
            else if (archiveDelta.n > 1)
            {
                Text(Box(84,467,1410,25),$"A and B · same scale ({archiveDelta.unit})",17,Dim,strongFont);
                DrawHistoricalChart(archiveMatchedLeft,Box(165,504,1319,92),overlay:archiveMatchedRight);
                Text(Box(84,633,1410,27),$"A minus B · {archiveDelta.unit}",18,Dim,strongFont);
                DrawHistoricalChart(archiveDelta,Box(165,671,1319,91),includeZero:true);
            }
            else Text(Box(84,480,1410,55),"Choose another place to find more shared dates.",21,Dim);

            if (Button(Box(56,831,235,40),archivePairDetails ? "Back to charts" : "See the evidence",size:19))
                archivePairDetails = !archivePairDetails;
            if (Button(Box(307,831,222,40),"Show both places",icon:"Location",size:19)) archiveFilter = ArchiveFilter.Place;

            if (archivePairRequest != null) Text(Box(563,831,943,40),"Running exploratory tests…",18,Dim);
            else if (archivePairError != null) Text(Box(563,831,943,40),archivePairError,18,Dim);
            else if (archiveReviewedDifference == null && archiveDelta.n is > 0 and < 10)
                Text(Box(563,831,943,40),"Too few matched values for an exploratory test.",18,Dim);
            else if (!string.IsNullOrEmpty(archivePairAnalysis?.difference?.outcome))
                Text(Box(563,831,943,40),ExploratoryArchiveOutcome(archivePairAnalysis.difference),18,Dim);
        }

        static string ExploratoryArchiveOutcome(ArchiveExploratoryFit fit) => fit.outcome switch
        {
            "both" => "Both exploratory checks flagged a trend in A minus B.",
            "mixed" => "The exploratory checks gave mixed results.",
            "constant" => "A minus B stayed constant across the matched dates.",
            _ => "Neither exploratory check flagged a trend in A minus B."
        };

        static string PairedArchiveRate(ArchiveExploratoryFit fit, ArchiveTrend matched, ArchiveTrend full)
        {
            if (!string.IsNullOrEmpty(fit?.outcome)) return $"{Signed(fit.ratePerDecade,"0.###")} {full.unit} per decade";
            if (matched.hasSlope) return $"{Signed(matched.slopePerDecade,"0.###")} {matched.unit} per decade";
            return full.hasSlope ? $"Full record {Signed(full.slopePerDecade,"0.###")} {full.unit} per decade" : "Rate unavailable";
        }

        void DrawArchivePairDetails()
        {
            Text(Box(84,469,1423,38),"A   " + ArchiveFootprint(archiveOrigin.record),20,Aqua);
            Text(Box(84,507,1423,38),"B   " + ArchiveFootprint(archivePeer.record),20,Yellow);
            Text(Box(84,550,1423,32),archiveOrigin.record.product + " · " + archiveOrigin.trend.aggregation,19,White);
            Text(Box(84,584,1423,38),"Common dates only. Missing values were left out.",18,Dim);
            string details = archiveReviewedDifference != null ? TestSummary(archiveReviewedDifference) :
                "Theil–Sen rates. Exploratory tests use autocorrelation-adjusted OLS and Mann–Kendall.";
            Text(Box(84,624,1423,36),details,18,White);
            var fit = archivePairAnalysis?.difference;
            if (fit?.outcome is "both" or "mixed" or "neither")
            {
                string t = fit.hasAdjustedT ? $"t = {fit.adjustedT.ToString("G3",Invariant)} · " : "";
                Text(Box(84,667,1423,60),ExploratoryArchiveOutcome(fit)+"\n"+FormattableString.Invariant(
                    $"Adjusted OLS {t}p = {fit.adjustedP:G3} · Mann–Kendall S = {fit.mkS:0} (p = {fit.mkP:G3}) · Adjusted OLS 95% interval {fit.lowerPerDecade:0.###} to {fit.upperPerDecade:0.###} {archiveDelta.unit} per decade"),
                    17,Dim,bodyFont,TextAnchor.UpperLeft,true);
            }
            else if (fit?.outcome == "constant") Text(Box(84,667,1423,32),ExploratoryArchiveOutcome(fit),18,Dim);
            if (Button(Box(84,756,228,38),"Open source A",enabled:!string.IsNullOrEmpty(archiveOrigin.record.url)))
                Application.OpenURL(archiveOrigin.record.url);
            if (Button(Box(326,756,228,38),"Open source B",enabled:!string.IsNullOrEmpty(archivePeer.record.url)))
                Application.OpenURL(archivePeer.record.url);
        }

        bool LocateArchiveRecord(EvidenceSeries record, out Vector2 center)
        {
            var bounds = DataFootprint(record)?.bbox;
            if (bounds is { Length: 4 })
            {
                center = new Vector2((bounds[1]+bounds[3])/2,(bounds[0]+bounds[2])/2);
                return true;
            }
            return LocateFootprint(record,out center);
        }

        void DrawArchiveComparisonMarkers(Rect map)
        {
            DrawMarker(archiveOrigin,"A",Aqua);
            DrawMarker(archivePeer,"B",Yellow);

            void DrawMarker(ArchiveReading reading, string letter, Color color)
            {
                if (reading == null || !LocateArchiveRecord(reading.record,out var center)) return;
                var bounds = DataFootprint(reading.record)?.bbox;
                if (bounds is { Length: 4 }) DrawAreaOutline(map,bounds,color);
                else DrawIcon("Location",MapPosition(map,center),color);
                var place = MapPosition(map,center);
                var marker = Box(Mathf.Clamp(place.x-9,map.x,map.xMax-18),Mathf.Clamp(place.y-36,map.y,map.yMax-20),18,20);
                Fill(marker,Ink);
                Text(marker,letter,16,color,strongFont,TextAnchor.MiddleCenter);
            }
        }
    }
}
