using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    [Serializable] sealed class ArchiveVariableCatalog
    {
        public ArchiveVariableTheme[] themes;
        public ArchiveVariable[] variables;
    }

    [Serializable] sealed class ArchiveVariableTheme { public string id, label; }
    [Serializable] sealed class ArchiveVariable { public string id, key, name, theme, definition, origin, unit; public int startYear; }

    public sealed partial class FieldSimulation
    {
        enum ArchiveFilter { None, Place, Measurement }
        ArchiveFilter archiveFilter;
        Texture2D siteMap;
        string archivePlaceId, archiveLatitude, archiveLongitude;
        bool showArchiveEvidence, archiveOfflineOnly, archiveWalkthrough;
        string archiveVerdictId;
        string ArchivePlaceId => LegacyCapture ? Region.id : archivePlaceId ?? Region.id;
        string ArchivePlaceName => ArchivePlaceId == "central-chile" ? "Central Chile" :
            data.regions.FirstOrDefault(site => site.id == ArchivePlaceId)?.name ?? ArchivePlaceId;
        ArchiveVariableCatalog archiveCatalog;
        string archiveTheme = "water", draftArchiveVariable;

        ArchiveVariableCatalog ArchiveCatalog => archiveCatalog ??=
            DecodeArchiveJson<ArchiveVariableCatalog>(Resources.Load<TextAsset>("FieldArchiveVariables")?.text);

        ArchiveVariable ArchiveVariableFor(string key) =>
            ArchiveCatalog?.variables?.FirstOrDefault(item => item.key == key);

        static readonly string[] ArchiveSteps = { "Measure", "Place", "Series", "Verdict", "Compare" };

        void DrawArchiveProgress()
        {
            int step = archiveOrigin != null ? 4 : archiveFilter switch
            {
                ArchiveFilter.Measurement => 0,
                ArchiveFilter.Place => 1,
                _ => !archiveWalkthrough || archiveVerdictId != null ? 3 : 2
            };
            for (int i = 0; i < ArchiveSteps.Length; i++)
            {
                float x = 967+i*116;
                Text(Box(x,160,105,28),ArchiveSteps[i],18,i == step ? White : Dim,strongFont,TextAnchor.MiddleCenter);
                Fill(Box(x,193,105,i == step ? 3 : 1),i == step ? Yellow : Line);
            }
        }

        void ResetArchiveView(string key)
        {
            archiveWalkthrough = key == null;
            archiveVerdictId = null;
            InvalidateConnectedArchive();
            archiveOrigin = null;
            archivePlaceId = Region.id;
            archiveRequestedPoint = null;
            archiveLatitude = archiveLongitude = "";
            archiveSize = 0;
            archiveRecordId = null;
            archiveMode = archiveMonth = 0;
            archiveOfflineOnly = key != null;
            evidenceKey = key;
            draftArchiveVariable = key;

            var savedFinding = investigation?.origin?.connectedEvidence;
            if (key != null || savedFinding?.requested == null) return;
            string parameter = savedFinding.dataset?.source?.parameter;
            string savedKey = ArchiveKey(parameter);
            var point = new Vector2(savedFinding.requested.latitude,savedFinding.requested.longitude);
            string mode = savedFinding.finding?.mode;
            int month = savedFinding.finding?.month ?? 1;
            if (!TryAcceptConnectedArchive(savedFinding,point,parameter,savedFinding.size,out var series,mode,month)) return;
            SetArchivePoint(point);
            archiveSize = savedFinding.size;
            archiveMode = mode == "month" ? 1 : mode == "anomaly" ? 2 : 0;
            archiveMonth = mode == "month" ? Mathf.Clamp(month-1,0,11) : 0;
            evidenceKey = savedKey;
            archiveVerdictId = savedFinding.finding.id;
            connectedArchive = savedFinding;
            connectedSeries = series;
        }

        void SelectArchivePlace(string place)
        {
            archiveVerdictId = null;
            InvalidateConnectedArchive();
            archivePlaceId = place;
            archiveRequestedPoint = null;
            archiveLatitude = archiveLongitude = "";
            archiveSize = 0;
            archiveRecordId = null;
            archiveFilter = ArchiveFilter.None;
            evidenceIndex = 0;
            archiveMode = archiveMonth = 0;
            if (place == "central-chile" && evidenceKey == null) evidenceKey = "rain";
        }

        void SetArchivePoint(Vector2 point)
        {
            InvalidateConnectedArchive();
            archiveRequestedPoint = point;
            archiveLatitude = point.x.ToString("R",Invariant);
            archiveLongitude = point.y.ToString("R",Invariant);
            evidenceKey ??= EvidenceKeys[0];
            archiveFilter = ArchiveFilter.None;
        }

        bool DrawArchiveFilters()
        {
            Tag(56,216,"PLACE",Dim);
            Tag(428,216,"MEASUREMENT",Dim);
            bool custom = archiveRequestedPoint.HasValue;
            string place = archiveRequestedPoint is Vector2 point ? Coordinate(point.x,point.y) : ArchivePlaceName;
            string measurement = ArchiveVariableFor(evidenceKey)?.name ?? "All measurements";
            if (Button(Box(56,245,350,52),place,active:archiveFilter == ArchiveFilter.Place,
                enabled:evidenceKey != null || archiveOfflineOnly,icon:"Location"))
                archiveFilter = archiveFilter == ArchiveFilter.Place ? ArchiveFilter.None : ArchiveFilter.Place;
            if (Button(Box(428,245,350,52),measurement,active:archiveFilter == ArchiveFilter.Measurement,icon:"Choose"))
            {
                draftArchiveVariable = evidenceKey;
                archiveTheme = ArchiveVariableFor(evidenceKey)?.theme ?? archiveTheme;
                archiveFilter = archiveFilter == ArchiveFilter.Measurement ? ArchiveFilter.None : ArchiveFilter.Measurement;
            }
            if (!archiveOfflineOnly && (custom || (evidenceKey != null && archiveFilter == ArchiveFilter.Place)))
            {
                Tag(800,216,"COVERAGE",Dim);
                for (int i = 0; i < ConnectedArchiveSizes.Length; i++)
                {
                    int size = ConnectedArchiveSizes[i];
                    if (Button(Box(800+i*105,245,99,52),size == 0 ? "Point" : $"{size}° box",active:archiveSize == size,size:19) &&
                        archiveSize != size)
                    {
                        archiveSize = size;
                        if (archiveFilter == ArchiveFilter.Place) InvalidateConnectedArchive();
                        else StartArchiveRequest();
                    }
                }
            }
            if (investigation != null && Button(Box(1301,245,243,52),"Back to case",icon:"Back")) OpenCase();
            if (archiveFilter == ArchiveFilter.Place)
            {
                DrawArchiveMap();
                return true;
            }
            if (archiveFilter != ArchiveFilter.Measurement) return false;
            DrawArchiveVariables();
            return true;
        }

        void DrawArchiveVariables()
        {
            var catalog = ArchiveCatalog;
            Text(Box(56,329,1000,45),"Choose a measurement",30,White,headingFont);
            if (catalog == null)
            {
                Text(Box(56,389,1400,42),"The measurement list is unavailable. Offline records are still available.",20,Dim);
            }
            else
            {
                for (int i = 0; i < catalog.themes.Length; i++)
                {
                    var theme = catalog.themes[i];
                    if (Button(Box(56+i*188,394,177,45),theme.label,active:archiveTheme == theme.id))
                        archiveTheme = theme.id;
                }
                var visible = catalog.variables.Where(item => item.theme == archiveTheme).ToArray();
                for (int i = 0; i < visible.Length; i++)
                {
                    var variable = visible[i];
                    if (Button(Box(56+(i%2)*363,459+(i/2)*62,349,52),variable.name,
                        active:draftArchiveVariable == variable.key,size:19))
                        draftArchiveVariable = variable.key;
                }
                var selected = ArchiveVariableFor(draftArchiveVariable);
                if (selected != null)
                {
                    Card(Box(815,394,729,251),Panel);
                    Text(Box(843,414,672,59),selected.name,31,White,headingFont);
                    Text(Box(843,478,672,64),selected.definition,22,White,bodyFont,TextAnchor.UpperLeft,true);
                    Text(Box(843,565,672,54),$"{selected.origin} · Monthly from {selected.startYear} · {selected.unit}",18,Dim,
                        bodyFont,TextAnchor.UpperLeft,true);
                }
                if (Button(Box(815,672,314,48),"Choose a place",accent:true,enabled:selected != null,icon:"Next"))
                {
                    if (evidenceKey != selected.key)
                    {
                        InvalidateConnectedArchive();
                        evidenceKey = selected.key;
                        evidenceIndex = 0;
                        archiveMode = archiveMonth = 0;
                    }
                    archiveOfflineOnly = false;
                    archiveFilter = ArchiveFilter.Place;
                }
            }
            if (Button(Box(56,714,282,44),"Browse offline records",icon:"Archive"))
            {
                SelectArchivePlace(ArchivePlaceId);
                archiveOfflineOnly = true;
                evidenceKey = null;
                archiveFilter = ArchiveFilter.Place;
            }
        }

        void DrawEarthMap(Rect map)
        {
            siteMap ??= Resources.Load<Texture2D>("Film/earth");
            if (siteMap != null)
            {
                GUI.DrawTexture(map,siteMap,ScaleMode.StretchToFill);
                Fill(map,Alpha(Ink,.55f));
            }
            else Fill(map,Panel);
            for (int i = 1; i < 6; i++) Fill(Box(map.x+i*map.width/6,map.y,1,map.height),Alpha(Mint,.23f));
            for (int i = 1; i < 4; i++) Fill(Box(map.x,map.y+i*map.height/4,map.width,1),Alpha(Mint,.23f));
            DrawButtonEdge(map,Line);
        }

        static Vector2 MapPosition(Rect map, Vector2 point) =>
            new(map.x+(point.y+180)/360*map.width,map.y+(90-point.x)/180*map.height);

        static Vector2 MapCoordinate(Rect map, Vector2 cursor) =>
            new(Mathf.Lerp(90,-90,(cursor.y-map.y)/map.height),Mathf.Lerp(-180,180,(cursor.x-map.x)/map.width));

        void DrawMapSelection(Rect map, Vector2 point)
        {
            var pos = MapPosition(map,point);
            Fill(Box(pos.x-10,pos.y-1,6,2),Yellow);
            Fill(Box(pos.x+4,pos.y-1,6,2),Yellow);
            Fill(Box(pos.x-1,pos.y-10,2,6),Yellow);
            Fill(Box(pos.x-1,pos.y+4,2,6),Yellow);
        }

        void DrawAreaOutline(Rect map, float[] bounds, Color color)
        {
            var northWest = MapPosition(map,new Vector2(bounds[3],bounds[0]));
            var southEast = MapPosition(map,new Vector2(bounds[1],bounds[2]));
            var rect = Rect.MinMaxRect(northWest.x,northWest.y,southEast.x,southEast.y);
            Fill(rect,Alpha(color,.23f));
            DrawButtonEdge(rect,color);
        }

        bool DrawMapPlace(Rect map, Vector2 point, string label, bool selected, bool area, bool labelLeft = false)
        {
            var position = MapPosition(map,point);
            const float marker = 28;
            float padding = area ? 7 : marker / 2;
            float labelWidth = label.Length > 19 ? 172 : 144;
            bool left = labelLeft || position.x+padding+labelWidth+3 > map.xMax;
            float labelX = left ? position.x-padding-3-labelWidth : position.x+padding+3;
            labelX = Mathf.Clamp(labelX,map.x,map.xMax-labelWidth);
            var labelRect = Box(labelX,Mathf.Clamp(position.y-12,map.y,map.yMax-24),labelWidth,24);
            var hitbox = Rect.MinMaxRect(Mathf.Min(labelRect.x,position.x-padding),Mathf.Min(labelRect.y,position.y-padding),
                Mathf.Max(labelRect.xMax,position.x+padding),Mathf.Max(labelRect.yMax,position.y+padding));
            var state = ButtonInput(hitbox,label);
            if (!area)
            {
                Fill(Box(position.x-marker/2,position.y-marker/2,marker,marker),Ink);
                DrawIcon("Location",position,selected ? Yellow : Mint);
            }
            Fill(labelRect,Ink);
            DrawButtonEdge(labelRect,state.focused ? Yellow : selected ? Mint : Line);
            Text(Box(labelRect.x+5,labelRect.y,labelRect.width-10,labelRect.height),label,16,selected ? Mint : White,strongFont);
            return state.pressed;
        }

        void DrawArchiveMap()
        {
            Card(Box(54,332,1490,426),Ink);
            var map = Box(70,350,782,391);
            DrawEarthMap(map);
            foreach (var place in data.evidence.GroupBy(record => record.region))
            {
                var offline = place.FirstOrDefault(item => item.key == evidenceKey);
                var record = offline ?? place.First();
                if (!LocateArchiveRecord(record,out var center)) continue;
                bool selected = !archiveRequestedPoint.HasValue && record.region == ArchivePlaceId;
                string label = PlaceName(record);
                bool pressed;
                var bounds = DataFootprint(record)?.bbox;
                if (bounds is { Length: 4 })
                {
                    DrawAreaOutline(map,bounds,selected ? Yellow : Mint);
                    pressed = DrawMapPlace(map,center,label + " area",selected,true);
                }
                else pressed = DrawMapPlace(map,center,label,selected,false,record.region == GuidedRainOrigin);
                if (pressed)
                {
                    if (archiveOrigin != null) SelectArchivePeer(record,center);
                    else if (archiveOfflineOnly)
                    {
                        if (offline != null || evidenceKey == null) SelectArchivePlace(record.region);
                        else archiveError = "This measurement has no offline record at that place.";
                    }
                    else
                    {
                        SetArchivePoint(center);
                        StartArchiveRequest();
                    }
                    return;
                }
            }
            if (archiveOrigin != null)
            {
                DrawArchiveComparisonMarkers(map);
                if (archiveOrigin.connected == null)
                {
                    Text(Box(887,357,624,45),"Choose an offline place B",29,White,headingFont);
                    Text(Box(887,415,612,86),archivePairError ?? "Select a labeled place with a matching record.",
                        22,Dim,bodyFont,TextAnchor.UpperLeft,true);
                    return;
                }
            }
            else if (archiveOfflineOnly)
            {
                Text(Box(887,357,624,45),"Choose an offline record",30,White,headingFont);
                Text(Box(887,415,612,86),archiveError ?? "Select a labeled place to open its archived record.",
                    22,Dim,bodyFont,TextAnchor.UpperLeft,true);
                if (Button(Box(887,534,320,44),"Request a NASA POWER record",size:19,icon:"Next"))
                {
                    archiveOfflineOnly = false;
                    if (evidenceKey == null) archiveFilter = ArchiveFilter.Measurement;
                }
                return;
            }
            var cursor = Event.current.mousePosition;
            if (GUI.Button(map,GUIContent.none,GUIStyle.none))
            {
                var draft = MapCoordinate(map,cursor);
                archiveLatitude = draft.x.ToString("R",Invariant);
                archiveLongitude = draft.y.ToString("R",Invariant);
            }

            Text(Box(887,357,310,45),archiveOrigin == null ? "Choose a place" : "Choose place B",33,White,headingFont);
            if (archiveOrigin == null && Button(Box(1207,358,302,43),"Browse offline records",icon:"Archive",size:18))
            {
                archiveOfflineOnly = true;
                return;
            }
            Text(Box(887,415,612,62),archiveOrigin == null ?
                "Click the map or enter coordinates. Select a label to use a known place." :
                "Use the same measurement and coverage at another place.",
                22,Dim,bodyFont,TextAnchor.UpperLeft,true);
            Tag(888,507,"LATITUDE  (−90 TO 90)",Dim);
            Tag(1178,507,"LONGITUDE  (−180 TO 180)",Dim);
            // Text fields consume Return before a later IMGUI button can see it.
            bool confirmKey = keyboardFocus && focusIndex >= controlIndex && focusIndex <= controlIndex+2 &&
                Event.current.type == EventType.KeyDown && Event.current.keyCode is (KeyCode.Return or KeyCode.KeypadEnter);
            if (confirmKey) Event.current.Use();
            archiveLatitude = CaseText(Box(888,537,276,46),archiveLatitude,18,focusOnRepaint:true);
            archiveLongitude = CaseText(Box(1178,537,325,46),archiveLongitude,18,focusOnRepaint:true);
            bool validLatitude = float.TryParse(archiveLatitude,NumberStyles.Float,Invariant,out float latitude);
            bool validLongitude = float.TryParse(archiveLongitude,NumberStyles.Float,Invariant,out float longitude);
            bool canUse = validLatitude && validLongitude && ValidArchivePoint(latitude,longitude);
            var draftPoint = new Vector2(latitude,longitude);
            var returned = canUse && archiveRequestedPoint is Vector2 requested && requested.Equals(draftPoint)
                ? connectedArchive?.footprint : null;
            if (returned?.type == "area-mean") DrawAreaOutline(map,returned.bbox,Aqua);
            if (returned?.type == "grid-point")
                DrawIcon("Location",MapPosition(map,new Vector2(returned.point.latitude,returned.point.longitude)),Aqua);
            if (canUse) DrawMapSelection(map,draftPoint);
            bool confirmed = Button(Box(888,605,304,47),returned != null ? "Back to record" : "Read at these coordinates",
                accent:true,enabled:canUse,size:21);
            if (canUse && (confirmed || confirmKey))
            {
                if (returned != null) archiveFilter = ArchiveFilter.None;
                else
                {
                    SetArchivePoint(draftPoint);
                    StartArchiveRequest();
                }
            }
            if (returned != null)
                Text(Box(888,669,615,56),"The cross marks the selected coordinates. Blue marks the returned POWER footprint.",19,Dim,
                    bodyFont,TextAnchor.UpperLeft,true);
        }

        void DrawConnectedArchive()
        {
            if (connectedSeries != null)
            {
                DrawArchiveRecord(new[] { connectedSeries });
                return;
            }
            Card(Box(56,333,1488,292),Panel);
            var point = archiveRequestedPoint.Value;
            string state = archiveRequest != null ? "Requesting NASA POWER data…" :
                archiveError ?? "Request a NASA POWER record";
            Text(Box(83,355,1413,57),state,38,White,headingFont);
            string requestScope = archiveSize == 0 ? "POWER point" : $"{archiveSize}° POWER area";
            string variable = ArchiveVariableFor(evidenceKey)?.name ?? ArchiveParameter(evidenceKey);
            string lens = archiveMode == 1 ? Invariant.DateTimeFormat.GetMonthName(archiveMonth+1) :
                archiveMode == 2 ? "Monthly anomalies" : "Annual values";
            Text(Box(84,422,1408,29),$"{variable} · {Coordinate(point.x,point.y)} · {requestScope} · {lens}",22,White);
            if (archiveRequest != null)
                Text(Box(84,470,1398,36),"NASA POWER can take up to 45 seconds to respond.",20,Dim);
            if (archiveRequest != null)
            {
                if (Button(Box(84,547,211,46),"Cancel request",size:21))
                {
                    CancelArchiveRequest();
                    archiveError = null;
                }
            }
            else if (Button(Box(84,547,211,46),archiveError == null ? "Request record" : "Retry request",accent:true,size:21))
                StartArchiveRequest();
            string placeAction = archiveOrigin == null ? "Choose another place" : "Choose place B";
            if (Button(Box(311,547,253,46),placeAction,icon:"Location",size:19))
            {
                InvalidateConnectedArchive();
                archiveFilter = ArchiveFilter.Place;
            }
            if (archiveOrigin == null && Button(Box(580,547,253,46),"Browse offline records",icon:"Archive",size:19))
            {
                SelectArchivePlace(ArchivePlaceId);
                evidenceKey = null;
                archiveOfflineOnly = true;
                archiveFilter = ArchiveFilter.Place;
            }
        }

        void DrawArchiveRecord(EvidenceSeries[] records)
        {
            if (records.Length == 0)
            {
                Text(Box(56,378,1300,60),"No matching record for this place. Choose another measurement.",27,White);
                return;
            }
            var record = records[Mathf.Clamp(evidenceIndex,0,records.Length-1)];
            if (archiveRecordId != record.id)
            {
                archiveRecordId = record.id;
                if (ConnectedFor(record) == null) archiveMode = archiveMonth = 0;
                archiveValuePage = 0;
                showArchiveValues = showArchiveEvidence = false;
            }
            var metadata = Exported(record);
            var trend = ConnectedFor(record)?.finding ?? SelectArchiveTrend(record);
            var finding = record.years is { Length: > 0 } ? MakeFinding(record,trend) : null;
            bool showVerdict = !archiveWalkthrough || (finding != null && finding.id == archiveVerdictId);
            Card(Box(56,330,1488,481),Panel);
            Text(Box(84,343,1430,52),showVerdict ? ArchiveVerdict(finding) : "Read the time series",34,White,headingFont);
            string window = trend?.n == 0 ? "No complete years" : finding?.window ?? "Date range unavailable";
            Text(Box(85,397,1410,28),$"{record.variable} · {ArchiveFootprint(record)} · {window}",22,White,strongFont);
            if (showVerdict) Text(Box(85,428,1410,34),ArchiveRate(finding),24,Mint);
            else Text(Box(85,428,1410,34),record.product,20,Dim);
            if (records.Length > 1)
            {
                if (Button(Box(981,475,42,37),"‹",tooltip:"Previous record")) evidenceIndex = (evidenceIndex+records.Length-1)%records.Length;
                Text(Box(1029,475,139,37),$"Record {evidenceIndex+1} / {records.Length}",16,Dim,dataFont,TextAnchor.MiddleCenter);
                if (Button(Box(1174,475,42,37),"›",tooltip:"Next record")) evidenceIndex = (evidenceIndex+1)%records.Length;
            }
            bool connected = ConnectedFor(record) != null;
            if ((connected || (metadata?.monthFindings?.Length == 12 && metadata.anomalyFinding != null)) &&
                DrawArchiveModes(connected)) return;
            string inspect = ConnectedFor(record) == null ? "Inspect values" : "Inspect monthly values";
            if (Button(Box(1232,475,284,37),showArchiveValues ? "Show chart" : inspect,size:17))
            {
                showArchiveValues = !showArchiveValues;
                showArchiveEvidence = false;
            }
            if (showArchiveEvidence) DrawArchiveProvenance(record,metadata,finding);
            else DrawArchivePlot(record,trend);

            bool filed = finding != null && investigation?.origin?.id == finding.id;
            bool canFile = finding?.hasRate == true && CanFile(record);
            bool fileRain = !archiveWalkthrough && investigation?.kind == InvestigationKind.Soil && canFile;
            var next = Box(1218,746,296,43);
            if (!showVerdict)
            {
                if (Button(next,"Test the trend",accent:true,enabled:finding != null,size:21,icon:"Next"))
                {
                    archiveVerdictId = finding.id;
                    showArchiveValues = false;
                }
            }
            else if (fileRain)
            {
                if (Button(next,filed ? "Continue case" : "Save rainfall finding",accent:true,size:21,icon:"Next"))
                {
                    if (!filed) FileOrigin(record,trend);
                    if (investigation.origin?.id == finding.id) OpenCase();
                    return;
                }
            }
            else
            {
                if (Button(next,"Compare another place",accent:true,enabled:trend?.points is { Length: > 0 },size:21,icon:"Next"))
                {
                    BeginArchiveComparison(record,trend);
                    return;
                }
                if (Button(Box(1280,828,264,40),filed ? "Finding filed" : "File this finding",
                    enabled:!filed && canFile,size:21))
                    FileOrigin(record,trend);
            }
            if (showVerdict && Button(Box(56,828,212,40),showArchiveEvidence ? "Back to chart" : "See the evidence",size:19))
            {
                showArchiveEvidence = !showArchiveEvidence;
                showArchiveValues = false;
            }
            if (Button(Box(282,828,207,40),"Open data source",enabled:!string.IsNullOrEmpty(record.url),size:19))
                Application.OpenURL(record.url);
            if (investigation == null && Button(Box(504,828,232,40),"Browse cases",icon:"Search")) OpenCasePicker();
        }

        bool DrawArchiveModes(bool connected)
        {
            int previousMode = archiveMode, previousMonth = archiveMonth;
            string[] modes = { "Annual values", "One calendar month", "Monthly anomalies" };
            for (int i=0;i<modes.Length;i++)
                if (Button(Box(85+i*195,475,185,37),modes[i],active:archiveMode == i,size:17))
                {
                    archiveMode = i;
                    archiveValuePage = 0;
                }
            if (archiveMode == 1 && Button(Box(686,475,40,37),"‹",tooltip:"Previous month"))
            {
                archiveMonth = (archiveMonth + 11) % 12;
                archiveValuePage = 0;
            }
            if (archiveMode == 1)
                Text(Box(733,475,140,37),Invariant.DateTimeFormat.GetMonthName(archiveMonth+1),18,White,dataFont,TextAnchor.MiddleCenter);
            if (archiveMode == 1 && Button(Box(880,475,40,37),"›",tooltip:"Next month"))
            {
                archiveMonth = (archiveMonth + 1) % 12;
                archiveValuePage = 0;
            }
            if (archiveMode == previousMode && archiveMonth == previousMonth) return false;
            archiveVerdictId = null;
            showArchiveEvidence = false;
            if (!connected) return false;
            StartArchiveRequest();
            return true;
        }

        static string ArchiveVerdict(FiledFinding finding) => finding?.hasPValue == true ? finding.status switch
        {
            "significant" => "The test found a statistically significant trend.",
            "not-significant" => "The test did not find a statistically significant trend.",
            _ => "Statistical significance has not been established for this record."
        } : "Statistical significance has not been established for this record.";

        static string ArchiveRate(FiledFinding finding)
        {
            if (finding?.hasRate != true) return "Too few usable values to estimate a trend in this view.";
            float slope = finding.ratePerDecade;
            if (slope == 0) return $"The estimated rate of change is 0 {finding.unit} per decade.";
            string rate = Mathf.Abs(slope) < .001f ? "less than 0.001" : Mathf.Abs(slope).ToString("0.###",Invariant);
            return $"The record {(slope > 0 ? "rose" : "fell")} by an estimated {rate} {finding.unit} per decade.";
        }

        void DrawArchivePlot(EvidenceSeries record, ArchiveTrend trend)
        {
            int count = trend?.n ?? record.years?.Length ?? 0;
            var observations = ConnectedFor(record)?.observations;
            bool monthly = showArchiveValues && observations != null;
            if (showArchiveValues) DrawArchiveTable(record,trend,525,monthly ? observations : null);
            else
            {
                Text(Box(85,517,900,27),$"Value ({record.unit})",16,Dim,strongFont);
                if (trend?.points is { Length: > 1 }) DrawHistoricalChart(trend,Box(165,555,1319,123));
                else if (trend == null && record.years is { Length: > 1 }) DrawChart(record,Box(165,555,1319,123));
                else Text(Box(85,552,1340,49),"Too few values to draw a line. Open Inspect values for the available dates.",21,Dim);
            }
            string scope = monthly ? $"Monthly observations · {observations.Length} months" :
                $"{trend?.aggregation ?? record.aggregation} · {count} values";
            Text(Box(85,708,1430,27),scope,18,Dim);
            string pattern = monthly ? "A dash marks a missing month." : showArchiveValues ? "Empty dates are omitted." :
                "The line connects consecutive dates. Gaps show missing values.";
            Text(Box(85,753,1120,29),pattern,17,Dim);
        }

        void DrawArchiveProvenance(EvidenceSeries record, ArchiveRecord metadata, FiledFinding finding)
        {
            var connected = ConnectedFor(record);
            var dataset = connected?.dataset;
            var source = dataset?.provenance ?? metadata?.provenance;
            var quality = dataset?.quality ?? metadata?.quality;
            var footprint = DataFootprint(record);
            string begin = dataset?.temporal.start ?? metadata?.coverageStart;
            string end = dataset?.temporal.end ?? metadata?.coverageEnd;
            string time = dataset?.temporal.timeStandard ?? metadata?.timeStandard;
            string parameter = dataset?.source.parameter ?? metadata?.sourceParameter;
            string coverage = quality == null ? "Monthly coverage is unavailable." :
                $"Monthly coverage {begin} to {end} · {quality.missingMonths} missing months · " +
                $"{quality.completeYears?.Length ?? 0} complete years{(string.IsNullOrEmpty(time) ? "" : $" · {time}")}";
            string spatial = footprint?.gridCells > 0 ? $" · {footprint.gridCells} {(footprint.gridCells == 1 ? "cell" : "cells")}" : "";
            if (!string.IsNullOrEmpty(footprint?.nativeResolution)) spatial += $" · {footprint.nativeResolution}";
            Text(Box(84,521,1411,26),$"{record.product}{spatial}",19,White,strongFont);
            Text(Box(84,550,1411,25),coverage,17,Dim);
            if (connected != null)
            {
                var request = dataset.spatial.requestedCoordinates;
                Text(Box(84,578,1411,23),$"User request {Coordinate(connected.requested.latitude,connected.requested.longitude)} · " +
                    $"POWER query {Coordinate(request.lat,request.lon)} · {parameter} · Retrieved {source.retrievedAt}",17,Dim);
            }
            else Text(Box(84,578,1411,23),$"{parameter} · Archived {source?.retrievedAt}",17,Dim);
            string method = source?.processing is { Length: > 0 } ? string.Join(" ",source.processing) : "";
            string context = source?.limitations is { Length: > 0 } ? source.limitations[0] : "";
            Text(Box(84,607,1408,54),$"Theil–Sen rate. {method} {context}",17,Dim,bodyFont,TextAnchor.UpperLeft,true);
            string test = finding?.hasPValue == true ? $"{TestSummary(finding)} · {finding.reviewer}\n{finding.hypothesis}" :
                (finding?.reason ?? "No reviewed test is available for this record.");
            Text(Box(84,667,1409,58),test,17,Dim,bodyFont,TextAnchor.UpperLeft,true);
            Text(Box(84,743,1110,47),record.version,16,Dim,bodyFont,TextAnchor.UpperLeft,true);
        }
        void DrawHistoricalChart(ArchiveTrend trend, Rect chart, bool includeZero = false, int selectedIndex = -1,
            ArchiveTrend overlay = null)
        {
            float min = trend.points.Min(p => p.value), max = trend.points.Max(p => p.value);
            if (overlay != null)
            {
                min = Mathf.Min(min,overlay.points.Min(p => p.value));
                max = Mathf.Max(max,overlay.points.Max(p => p.value));
            }
            if (includeZero) { min = Mathf.Min(min,0); max = Mathf.Max(max,0); }
            if (max <= min) { min -= 1; max += 1; }
            float first = trend.points[0].t, last = trend.points[^1].t;
            for (int i=0;i<3;i++)
            {
                float y = chart.y + i*chart.height/2;
                Fill(Box(chart.x,y,chart.width,1),Line);
                Text(Box(chart.x-83,y-10,73,20),Mathf.Lerp(max,min,i/2f).ToString("0.##",Invariant),14,Dim,dataFont,TextAnchor.MiddleRight);
            }
            Vector2 Plot(ArchiveTrendPoint p) => new(
                Mathf.Lerp(chart.x,chart.xMax,Mathf.InverseLerp(first,last,p.t)),
                Mathf.Lerp(chart.yMax,chart.y,Mathf.InverseLerp(min,max,p.value)));
            if (includeZero) Fill(Box(chart.x,Plot(new ArchiveTrendPoint { value = 0 }).y,chart.width,1),Dim);
            if (selectedIndex >= 0) Fill(Box(Plot(trend.points[selectedIndex]).x,chart.y,2,chart.height),Yellow);
            float maxGap = trend.mode == "anomaly" ? .084f : 1.001f;
            PlotLine(trend.points,Aqua);
            if (overlay != null) PlotLine(overlay.points,Yellow);
            void PlotLine(ArchiveTrendPoint[] points, Color color)
            {
                var previous = points[0];
                foreach (var point in points)
                {
                    var pos = Plot(point);
                    if (point.t > previous.t && point.t-previous.t <= maxGap) DrawChartSegment(Plot(previous),pos,color);
                    Fill(Box(pos.x-1,pos.y-1,2,2),White);
                    previous = point;
                }
            }
            Text(Box(chart.x,chart.yMax+5,150,27),trend.firstDate,15,Dim,dataFont);
            Text(Box(chart.xMax-150,chart.yMax+5,150,27),trend.lastDate,15,Dim,dataFont,TextAnchor.MiddleRight);
        }

        void DrawArchiveTable(EvidenceSeries record, ArchiveTrend trend, float top = 519, ArchiveObservation[] observations = null)
        {
            const int size = 16;
            int count = observations?.Length ?? trend?.points?.Length ?? record.years?.Length ?? 0;
            int offset = Mathf.Clamp(archiveValuePage*size,0,Mathf.Max(0,count-1));
            for (int i=0;i<Mathf.Min(size,count-offset);i++)
            {
                int index = offset+i;
                string date = observations != null ? observations[index].date :
                    trend == null ? record.years[index].year.ToString(Invariant) : trend.points[index].date;
                float value = observations != null ? observations[index].value :
                    trend == null ? record.years[index].value : trend.points[index].value;
                string display = observations != null && !observations[index].hasValue ? "—" :
                    value.ToString("0.###",Invariant) + " " + record.unit;
                Text(Box(83+(i/4)*310,top+(i%4)*35,301,30),$"{date,-9}   {display}",17,White,dataFont);
            }
            if (Button(Box(1341,top,174,39),"Previous values",false,false,offset > 0,17)) archiveValuePage--;
            if (Button(Box(1341,top+50,174,39),"Next values",false,false,offset+size < count,17)) archiveValuePage++;
        }
    }
}
