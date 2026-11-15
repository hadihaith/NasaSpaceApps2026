using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Homecoming.Field
{
    [Serializable] sealed class ConnectedArchiveEvidence
    {
        public int schemaVersion;
        public ConnectedArchiveDataset dataset;
        public ArchiveFootprint footprint;
        public ArchiveObservation[] observations;
        public ArchiveTrend finding;
        public ArchivePoint requested;
        public int size;
    }

    [Serializable] sealed class ConnectedArchiveDataset
    {
        public string id, variable, units, annualInterpretation, providerKind, definition, rawSha256;
        public ArchiveProvenance provenance;
        public ArchiveSource source;
        public ConnectedArchiveSpatial spatial;
        public ConnectedArchiveTemporal temporal;
        public ArchiveQuality quality;
    }

    [Serializable] sealed class ConnectedArchiveSpatial
    {
        public string type, nativeResolution;
        public int gridCells;
        public ConnectedArchiveCoordinates requestedCoordinates;
    }

    [Serializable] sealed class ConnectedArchiveCoordinates { public float lat, lon; }
    [Serializable] sealed class ConnectedArchiveTemporal { public string start, end, timeStandard; }
    [Serializable] struct ArchiveObservation { public string date; public bool hasValue; public float value; }
    [Serializable] sealed class ArchiveProblem { public string error; }

    sealed partial class FiledFinding
    {
        public ConnectedArchiveEvidence connectedEvidence;
    }

    public sealed partial class FieldSimulation
    {
        const string DefaultArchiveEndpoint = "https://nasatrenddetective.vercel.app/api/timeseries";
        static readonly string[] ArchiveParameters = { "PRECTOTCORR", "GWETROOT", "T2M", "RH2M", "WS10M" };
        static readonly int[] ConnectedArchiveSizes = { 0, 2, 5, 10 };
        UnityWebRequest archiveRequest;
        Vector2? archiveRequestedPoint;
        int archiveSize;
        string archiveError;
        ConnectedArchiveEvidence connectedArchive;
        EvidenceSeries connectedSeries;

        static string ArchiveEndpoint
        {
            get
            {
                string endpoint = Environment.GetEnvironmentVariable("HOMECOMING_ARCHIVE_ENDPOINT");
                return string.IsNullOrWhiteSpace(endpoint) ? DefaultArchiveEndpoint : endpoint;
            }
        }

        static string ArchiveParameter(string key)
        {
            int index = Array.IndexOf(EvidenceKeys,key);
            return index < 0 ? key : ArchiveParameters[index];
        }

        static string ArchiveKey(string parameter)
        {
            int index = Array.IndexOf(ArchiveParameters,parameter);
            return index < 0 ? parameter : EvidenceKeys[index];
        }

        string ArchiveTimeMode => archiveMode == 1 ? "month" : archiveMode == 2 ? "anomaly" : "annual";

        static Uri ArchiveRequestUri(string endpoint, Vector2 point, string parameter, int size,
            string mode = "annual", int month = 1, ArchiveTrend matchWindow = null)
        {
            var uri = new UriBuilder(endpoint);
            if (uri.Scheme is not ("https" or "http") || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new ArgumentException("Configure an HTTP or HTTPS timeseries URL without a query or fragment.");
            uri.Query = FormattableString.Invariant(
                $"format=evidence&mode={mode}&month={month}&variable={Uri.EscapeDataString(parameter)}&lat={ArchiveRequestCoordinate(point.x):0.##}&lon={ArchiveQueryLongitude(point.y):0.##}&size={size}");
            if (matchWindow != null)
                uri.Query = uri.Query.TrimStart('?') + FormattableString.Invariant($"&startYear={matchWindow.startYear}&endYear={matchWindow.endYear}");
            return uri.Uri;
        }

        void StartArchiveRequest()
        {
            string parameter = ArchiveParameter(evidenceKey);
            if (archiveRequestedPoint is not Vector2 point || string.IsNullOrEmpty(parameter)) return;
            InvalidateConnectedArchive();
            try
            {
                var url = ArchiveRequestUri(ArchiveEndpoint,point,parameter,archiveSize,ArchiveTimeMode,archiveMonth+1,
                    archiveOrigin?.connected == null ? null : archiveOrigin.trend);
                archiveRequest = UnityWebRequest.Get(url);
                archiveRequest.timeout = 45;
                archiveRequest.SendWebRequest();
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or UriFormatException)
            {
                CancelArchiveRequest();
                archiveError = "The archive connection could not start.";
            }
        }

        void TickArchiveRequest()
        {
            TickArchivePair();
            if (archiveRequest == null || !archiveRequest.isDone) return;
            using var request = archiveRequest;
            archiveRequest = null;
            if (view != View.Archive || archiveRequestedPoint is not Vector2 point) return;
            if (request.result != UnityWebRequest.Result.Success)
            {
                var problem = DecodeArchiveJson<ArchiveProblem>(request.downloadHandler?.text);
                archiveError = problem?.error?.Contains("no usable months",StringComparison.OrdinalIgnoreCase) == true
                    ? "NASA POWER found no usable months at this location."
                    : request.responseCode switch
                    {
                        504 => "NASA POWER took too long to respond.",
                        502 => "NASA POWER could not supply this record.",
                        > 0 => "The archive service could not supply this record.",
                        _ when request.error?.Contains("timed out",StringComparison.OrdinalIgnoreCase) == true =>
                            "The archive request timed out.",
                        _ => "Could not reach the archive service."
                    };
                return;
            }
            var evidence = DecodeArchiveJson<ConnectedArchiveEvidence>(request.downloadHandler.text);
            if (!TryAcceptConnectedArchive(evidence,point,ArchiveParameter(evidenceKey),archiveSize,out var series,
                ArchiveTimeMode,archiveMonth+1))
            {
                archiveError = "The archive service returned a record we could not read for this selection.";
                return;
            }
            connectedArchive = evidence;
            connectedSeries = series;
            if (archiveOrigin != null) CompareArchiveRecord(series,evidence.finding);
        }

        void CancelArchiveRequest()
        {
            if (archiveRequest == null) return;
            archiveRequest.Abort();
            archiveRequest.Dispose();
            archiveRequest = null;
        }

        void InvalidateConnectedArchive()
        {
            CancelArchiveRequest();
            ClearArchivePair();
            connectedArchive = null;
            connectedSeries = null;
            archiveError = null;
            archiveValuePage = 0;
            showArchiveValues = showArchiveEvidence = false;
        }

        static T DecodeArchiveJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (ArgumentException) { return null; }
        }

        static bool ValidArchivePoint(float latitude, float longitude) =>
            float.IsFinite(latitude) && float.IsFinite(longitude) && Mathf.Abs(latitude) <= 90 && Mathf.Abs(longitude) <= 180;

        // Send coordinates at the endpoint's cache-key precision and retain the player's original selection separately.
        static float ArchiveRequestCoordinate(float coordinate) => (float)(Math.Floor(
            double.Parse(coordinate.ToString("R",CultureInfo.InvariantCulture),CultureInfo.InvariantCulture) * 100d + .5) / 100d);

        static float ArchiveQueryLongitude(float coordinate)
        {
            float longitude = ArchiveRequestCoordinate(coordinate);
            return longitude == 180 ? -180 : longitude;
        }

        static bool TryAcceptConnectedArchive(ConnectedArchiveEvidence evidence, Vector2 point, string parameter, int size,
            out EvidenceSeries record, string mode = "annual", int month = 1)
        {
            record = null;
            var dataset = evidence?.dataset;
            var trend = evidence?.finding;
            var spatial = dataset?.spatial;
            var source = dataset?.provenance;
            var requested = spatial?.requestedCoordinates;
            var footprint = evidence?.footprint;
            if (evidence?.schemaVersion != 2 || !ValidArchivePoint(point.x,point.y) || !ConnectedArchiveSizes.Contains(size) ||
                string.IsNullOrEmpty(parameter) || dataset?.source?.parameter != parameter || requested == null ||
                !ValidArchivePoint(requested.lat,requested.lon) ||
                Mathf.Abs(requested.lat-ArchiveRequestCoordinate(point.x)) > .00002f ||
                Mathf.Abs(requested.lon-ArchiveQueryLongitude(point.y)) > .00002f ||
                footprint == null || footprint.type != spatial.type ||
                string.IsNullOrWhiteSpace(dataset.id) || string.IsNullOrWhiteSpace(dataset.variable) ||
                string.IsNullOrWhiteSpace(dataset.units) || string.IsNullOrWhiteSpace(source?.product) ||
                string.IsNullOrWhiteSpace(source.version) || !Uri.TryCreate(source.sourceUrl,UriKind.Absolute,out var sourceUri) ||
                sourceUri.Scheme != Uri.UriSchemeHttps || dataset.rawSha256?.Length != 64 ||
                trend == null || string.IsNullOrWhiteSpace(trend.id) || trend.fingerprint?.Length != 64 ||
                trend.datasetId != dataset.id || trend.mode != mode || trend.unit != dataset.units ||
                (mode == "month" && trend.month != month) || (mode == "anomaly" && string.IsNullOrEmpty(trend.baseline?.type)) ||
                (mode == "annual" && trend.aggregation != dataset.annualInterpretation) || string.IsNullOrWhiteSpace(trend.aggregation) ||
                !HasConnectedFootprint(footprint,spatial.gridCells,size) || !HasConnectedCoverage(evidence))
                return false;

            // Connected readings are exploratory. Reviews are only resolved from the pinned pack.
            trend.status = "not-established";
            trend.review = null;
            trend.test = null;
            evidence.requested = new ArchivePoint { latitude = point.x, longitude = point.y };
            evidence.size = size;
            footprint.nativeResolution = spatial.nativeResolution;
            footprint.gridCells = spatial.gridCells;
            record = new EvidenceSeries
            {
                id = dataset.id, region = "custom", key = ArchiveKey(parameter),
                variable = dataset.variable, product = source.product, provider = dataset.providerKind, version = source.version,
                url = source.sourceUrl, unit = trend.unit, aggregation = trend.aggregation, hasSlope = trend.hasSlope,
                slope = trend.slopePerDecade / 10, years = trend.points.Select(p => new YearPoint { year = (int)p.t, value = p.value }).ToArray()
            };
            return true;
        }

        static bool HasConnectedFootprint(ArchiveFootprint footprint, int cells, int size)
        {
            if (size == 0)
                return footprint.type == "grid-point" && cells == 1 && footprint.point != null &&
                    ValidArchivePoint(footprint.point.latitude,footprint.point.longitude);
            var bounds = footprint.bbox;
            return footprint.type == "area-mean" && bounds is { Length: 4 } &&
                ValidArchivePoint(bounds[1],bounds[0]) && ValidArchivePoint(bounds[3],bounds[2]) &&
                bounds[2]-bounds[0] == size && bounds[3]-bounds[1] == size &&
                cells > 0 && footprint.cells?.Length == cells && footprint.cells.All(cell => cell != null &&
                    ValidArchivePoint(cell.latitude,cell.longitude) && float.IsFinite(cell.weight) && cell.weight > 0);
        }

        static bool HasConnectedCoverage(ConnectedArchiveEvidence evidence)
        {
            var dataset = evidence.dataset;
            var trend = evidence.finding;
            var observations = evidence.observations;
            var quality = dataset.quality;
            if (trend.startYear < 1900 || trend.endYear > 2200 || trend.endYear < trend.startYear ||
                observations?.Length != (trend.endYear-trend.startYear+1)*12 ||
                dataset.temporal == null || dataset.temporal.start != observations[0].date || dataset.temporal.end != observations[^1].date ||
                trend.points?.Length != trend.n || quality?.completeYears == null ||
                (trend.hasSlope && (!float.IsFinite(trend.slopePerDecade) || !float.IsFinite(trend.magnitude))))
                return false;

            var complete = new List<int>();
            var excluded = new List<int>();
            var dates = new List<string>();
            int missing = 0, missingRun = 0, longestMissingRun = 0, missingInYear = 0;
            for (int i = 0; i < observations.Length; i++)
            {
                var observation = observations[i];
                int year = trend.startYear + i/12;
                int month = i%12 + 1;
                if (observation.date != FormattableString.Invariant($"{year:D4}-{month:D2}")) return false;
                if (observation.hasValue)
                {
                    if (!float.IsFinite(observation.value)) return false;
                    missingRun = 0;
                    if (trend.mode == "anomaly" || (trend.mode == "month" && trend.month == month)) dates.Add(observation.date);
                }
                else
                {
                    missing++;
                    missingInYear++;
                    longestMissingRun = Math.Max(longestMissingRun,++missingRun);
                }
                if (month != 12) continue;
                (missingInYear == 0 ? complete : excluded).Add(year);
                missingInYear = 0;
            }
            if (quality.missingMonths != missing || quality.longestMissingRunMonths != longestMissingRun ||
                !quality.completeYears.SequenceEqual(complete) || !(quality.excludedAnnualYears ?? Array.Empty<int>()).SequenceEqual(excluded))
                return false;
            if (trend.mode == "annual") dates.AddRange(complete.Select(year => year.ToString(Invariant)));
            if (trend.n != dates.Count) return false;
            for (int i = 0; i < trend.n; i++)
            {
                var value = trend.points[i];
                if (value.date != dates[i] || !float.IsFinite(value.t) || !float.IsFinite(value.value) ||
                    (i > 0 && value.t <= trend.points[i-1].t))
                    return false;
            }
            return trend.n == 0 || (trend.firstDate == trend.points[0].date && trend.lastDate == trend.points[^1].date);
        }
    }
}
