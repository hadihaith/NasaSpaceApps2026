using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        const float StorageTolerance = 1f;
        const float IndexTolerance = .01f;
        bool scenarioTable, scenarioMethods;
        string scenarioError;
        string guidedPreviewId;

        bool guidedWeatherLab;
        bool SoilScenarioPending => investigation?.kind == InvestigationKind.Soil && investigation.second == null;
        bool IsGuidedScenario => guidedWeatherLab && SoilScenarioPending;
        bool GuidedNeedsCaseSetup => IsGuidedScenario && (Region.id != GuidedSoilOrigin ||
            investigation.origin == null || investigation.scenarioCall == Direction.Unset);
        bool HasGuidedPreview() => PendingPreview?.Id != null && PendingPreview.Id == guidedPreviewId;

        string PredictionToleranceLabel()
        {
            var site = Campaign?.Current.Site(GuidedSoilOrigin);
            return ScenarioToleranceLabel(site?.current.hasStorage == true);
        }

        static string ScenarioToleranceLabel(bool physical) =>
            physical ? "Little modeled change means within ±1 mm." : "Little modeled change means within ±1 percentage point.";

        static string SoilOutcome(Direction direction) => direction switch
        {
            Direction.Rise => "wetter",
            Direction.Fall => "drier",
            _ => "little modeled change in soil wetness"
        };

        static string ScenarioResult(InvestigationRun run) => run.result == Direction.LittleChange
            ? $"The model showed {SoilOutcome(run.result)} by the end of {run.endMonth}"
            : $"The model soil was {SoilOutcome(run.result)} by the end of {run.endMonth}";

        string DescribeEdit(InvestigationRun run) => string.Join(", ",Enumerable.Range(0,4)
            .Where(i => run.inherited[i] != run.edited[i]).Select(i =>
                $"{data.controls[i].label} {Signed(run.inherited[i])}{data.controls[i].unit} → {Signed(run.edited[i])}{data.controls[i].unit}"));

        bool NeedsGuidedStartingPoint => IsGuidedScenario && investigation.first != null &&
            Campaign?.Current.id != investigation.first.id;

        bool CanRestoreGuidedStartingPoint => NeedsGuidedStartingPoint && Campaign?.CanProject == true &&
            investigation.campaignId == Campaign.Id && Campaign.GetRevision(investigation.first.id) != null;

        void RestoreGuidedStartingPoint()
        {
            if (!CanRestoreGuidedStartingPoint) return;
            var first = investigation.first;
            ChangeBranch(() =>
            {
                var branch = Campaign.Branches.FirstOrDefault(candidate => candidate.headRevisionId == first.id);
                if (branch == null) Campaign.Fork(first.id,"Arizona follow-up");
                else Campaign.SwitchBranch(branch.id);
            });
            scenarioError = branchStatus;
        }

        string GuidedLaunchBlocker()
        {
            if (Campaign?.CanProject != true) return SimulationStatus ?? "The saved campaign is unavailable.";
            if (!IsGuidedScenario) return null;
            if (Region.id != GuidedSoilOrigin) return "Return to the case to open the lab in Central Arizona.";
            if (investigation.origin == null || investigation.scenarioCall == Direction.Unset)
                return "Go back to your case to save the rainfall finding and make a prediction.";
            var revision = Campaign.Current;
            var first = investigation.first;
            if (first != null && revision.id != first.id) return CanRestoreGuidedStartingPoint
                ? "Resume from the saved end of the rain test."
                : "The saved rain test is unavailable in this campaign.";
            var inherited = revision.Site(Region.id).ActiveAt(revision.nextMonth);
            var edits = FieldCampaign.Patch(inherited, settings, revision.nextMonth);
            if (first == null)
                return edits.Length == 1 && edits[0].control == ForcingControl.Rain
                    ? null : "Set precipitation to a different value to preview the test.";
            return edits.Length == 1 && edits[0].control == ForcingControl.Heat
                ? null : "Set temperature to a different value to preview the test.";
        }

        void LaunchScenario()
        {
            if (GuidedLaunchBlocker() != null) return;
            duration = FieldCalendar.MonthsInPreview;
            scenarioTable = scenarioMethods = false;
            scenarioError = null;
            Launch();
            if (view == View.Descent) focusIndex = 0;
            if (IsGuidedScenario && PendingPreview != null)
                guidedPreviewId = PendingPreview.Id;
        }

        void CommitScenario()
        {
            var preview = PendingPreview;
            if (preview == null) return;
            if (IsGuidedScenario && !HasGuidedPreview())
            {
                scenarioError = "Open a new preview from the guided field lab.";
                return;
            }
            if (IsGuidedScenario)
            {
                investigation.committingRunId = preview.Id;
                SaveInvestigation();
            }
            bool guided = IsGuidedScenario;
            if (!AdvancePreview()) return;
            RecoverScenarioCommit();
            if (guided && !demoActive && !investigationTest) OpenCase();
        }

        void RecoverScenarioCommit()
        {
            if (!SoilScenarioPending || string.IsNullOrEmpty(investigation.committingRunId) ||
                investigation.campaignId != Campaign?.Id) return;
            var run = Campaign?.GetRun(investigation.committingRunId);
            if (string.IsNullOrEmpty(run?.committedRevisionId) || run.editedSiteId != GuidedSoilOrigin ||
                run.committedMonths != FieldCalendar.MonthsInPreview) return;
            var source = Campaign.GetRevision(run.sourceRevisionId);
            var projection = run.Site(run.editedSiteId);
            var end = projection.scenario.Final;
            var reference = projection.reference.Final;
            bool physical = end.water.available && reference.water.available;
            float scale = physical ? 1 : 100;
            float Value(FieldMonth month) => physical ? (float)month.water.storageMm : month.land.soil * scale;
            var result = new InvestigationRun
            {
                id = run.committedRevisionId, runId = run.id, parentId = run.sourceRevisionId, branchId = run.branchId,
                regionId = run.editedSiteId, modelVersion = run.modelVersion, dataFingerprint = run.dataFingerprint,
                startMonth = MonthName(projection.scenario.months[0]), endMonth = MonthName(end),
                metric = physical ? "root-zone storage" : "illustrative root-zone wetness index",
                unit = physical ? "mm" : "percentage points",
                inherited = source.Site(run.editedSiteId).ActiveAt(run.startMonth), edited = end.forcing,
                reference = Value(reference), scenario = Value(end), delta = Value(end)-Value(reference),
                tolerance = physical ? StorageTolerance : IndexTolerance * 100,
                startingStorage = physical ? (float)projection.scenario.before.storageMm : projection.scenario.before.soil * scale,
                endingStorage = Value(end)
            };
            result.result = Sign(result.delta,result.tolerance);
            if (investigation.first == null) investigation.first = result;
            else if (result.parentId == investigation.first.id) investigation.second = result;
            investigation.committingRunId = guidedPreviewId = null;
            SaveInvestigation();
        }

        static string MonthName(FieldMonth month) => month.Date.ToString("MMM yyyy",Invariant);

        void DrawScenarioField()
        {
            if (CascadeReportOpen)
            {
                DrawCascadeReport();
                return;
            }
            if (trial == null || referenceTrial == null) return;
            var current = ScenarioMonth(DisplayedMonth);
            var reference = ReferenceMonth(DisplayedMonth);
            bool physical = current.water.available && reference.water.available;
            float Value(FieldMonth month) => physical ? (float)month.water.storageMm : month.land.soil * 100;
            string unit = physical ? "mm" : "% index";
            string label = physical ? "Root-zone water storage" : "Root-zone wetness index";
            bool atEnd = DisplayedMonth == duration - 1;
            Fade(Box(0,0,1600,218),Alpha(Ink,.93f),0);
            Tag(44,28,physical ? "MODELED SCENARIO" : "ILLUSTRATIVE SCENARIO",Yellow);
            Text(Box(41,55,960,56),Region.name,47,White,headingFont);
            DrawScenarioOutcome(current,reference,Value(current)-Value(reference),physical,atEnd);
            if (openedFromFiledReport && Button(Box(956,33,230,42),"← Homecoming report",false,false,!running && PendingPreview == null))
                StorySession.ReturnToFiledReport();
            var cascade = CurrentScenarioCascade();
            bool savedSoilRun = cascade?.provenance == CascadeProvenance.Committed && investigation?.kind == InvestigationKind.Soil &&
                (MatchesReportRun(investigation.first,cascade) || MatchesReportRun(investigation.second,cascade));
            if (investigation != null && !demoActive && PendingPreview == null &&
                Button(Box(1310,33,245,44),savedSoilRun ? "Continue case" : "Back to case",accent:savedSoilRun,icon:"Back"))
            {
                OpenCase();
                return;
            }
            if (!savedSoilRun && !demoActive && !investigationTest && !running && PendingPreview == null && added.Exists(d => d.id == "drought") &&
                Application.CanStreamedLevelBeLoaded(StorySession.StoryScene) &&
                Button(Box(1046,94,246,44),"Watch crew reaction",true))
                StorySession.PlayCutscene("drought");

            Card(Box(44,695,1511,175),Ink);
            Text(Box(61,701,1099,32),$"{MonthName(current)} · Month {DisplayedMonth+1} of {duration}",23,White,strongFont);
            if (PendingPreview != null || !atEnd)
            {
                if (atEnd)
                {
                    if (Button(Box(1184,739,353,55),HasGuidedPreview() ? "Save test and return to case" : "Save and advance one year",
                        accent:true,size:21,icon:"Next"))
                    {
                        if (running) Finish();
                        CommitScenario();
                        return;
                    }
                }
                else if (Button(Box(1184,739,353,55),duration == 12 ? "Go to year-end result" : "Go to end result",
                    accent:true,size:23,icon:"Last month"))
                {
                    if (running) Finish();
                    SeekPreview(duration-1);
                }
            }
            if (Button(Box(61,743,53,47),"",icon:running && !paused ? "Pause" : "Play")) ToggleScenarioPlayback();
            DrawScenarioTimeline(Box(130,742,1014,49));
            if (Button(Box(1184,809,353,43),PendingPreview != null ? "Discard preview" : "Back to weather lab",icon:"Back"))
            {
                OpenWeatherLab();
                return;
            }
            string status = scenarioError ?? SimulationStatus ?? (PendingPreview != null
                ? atEnd
                    ? HasGuidedPreview() && investigation.first == null
                        ? "Saving carries this rain and soil water into your temperature test."
                        : "Saving advances the experiment to the next year."
                    : "Select a month to compare the two forecasts."
                : "Saved experiment · Select a month to replay the comparison.");
            Text(Box(61,813,1091,34),status,19,Dim);

            if (cascade != null && scenarioCascadeOpen) DrawScenarioCascade(cascade);
            else DrawScenarioWaterMonitor(current,reference,Value,unit,label,physical,cascade != null);
        }

        void DrawScenarioOutcome(FieldMonth current, FieldMonth reference, float delta, bool physical, bool atEnd)
        {
            Rect panel = Box(44,156,850,221);
            Card(panel,Ink);
            Fill(Box(panel.x,panel.y,76,3),Yellow);
            string period = atEnd ? duration == 12 ? "YEAR-END RESULT" : "END RESULT" : "MONTHLY RESULT";
            Tag(67,170,period + "  /  " + MonthName(current).ToUpperInvariant(),Yellow);
            Text(Box(65,200,803,101),DescribeScenarioOutcome(current,reference,delta,physical),
                37,White,headingFont,TextAnchor.UpperLeft,true);
            Text(Box(67,309,803,51),comparing ? "Both forecasts start from the same ground conditions.\nLandscape shows saved weather." :
                "Both forecasts start from the same ground conditions.",
                19,Dim,bodyFont,TextAnchor.MiddleLeft,true);
        }

        static string DescribeScenarioOutcome(FieldMonth current, FieldMonth reference, float delta, bool physical)
        {
            var direction = Sign(delta,physical ? StorageTolerance : IndexTolerance * 100);
            if (direction == Direction.LittleChange) return $"The comparison shows {SoilOutcome(direction)}.";
            var patch = FieldCampaign.Patch(reference.forcing,current.forcing,current.monthIndex);
            string edit = patch.Length == 1 ? patch[0].control switch
            {
                ForcingControl.Rain => "rain change",
                ForcingControl.Heat => "temperature change",
                ForcingControl.Humidity => "humidity change",
                ForcingControl.Wind => "wind change",
                _ => "weather edit"
            } : "weather edit";
            return patch.Length == 0 ? $"The model soil was {SoilOutcome(direction)} under the edited forecast."
                : $"Your {edit} left the model soil {SoilOutcome(direction)}.";
        }

        void DrawScenarioWaterMonitor(FieldMonth current, FieldMonth reference, Func<FieldMonth,float> value,
            string unit, string label, bool physical, bool showCascadeSwitch)
        {
            Rect monitor = Box(928,176,627,390);
            Card(monitor,Panel);
            Fill(Box(monitor.x,monitor.y,76,3),Yellow);
            Text(Box(946,184,591,38),label,30,White,headingFont);
            Text(Box(946,228,591,25),
                $"Edited {value(current).ToString("0.##",Invariant)} {unit}    /    Saved {value(reference).ToString("0.##",Invariant)} {unit}",
                19,White,strongFont);
            if (scenarioMethods)
            {
                string methods = physical ? LocalWaterBalance.Assumptions :
                    "This teaching model uses NASA POWER monthly normals and unitless indices for wetness, vegetation and drought. The landscape illustrates local responses.";
                Text(Box(946,267,591,173),ScenarioToleranceLabel(physical)+" "+methods,
                    18,White,bodyFont,TextAnchor.UpperLeft,true);
            }
            else if (scenarioTable) DrawScenarioValues(current,reference,value,unit);
            else
            {
                float max = physical ? (float)current.water.capacityMm : 100;
                DrawScenarioChart(Box(1005,288,521,114),max,value,unit);
            }
            if (Button(Box(946,466,141,39),scenarioTable ? "Show chart" : "Inspect values",active:scenarioTable,size:17))
            {
                scenarioTable = !scenarioTable;
                scenarioMethods = false;
            }
            if (Button(Box(1094,466,108,39),scenarioMethods ? "Show chart" : "Methods",active:scenarioMethods,size:17))
            {
                scenarioMethods = !scenarioMethods;
                scenarioTable = false;
            }
            if (Button(Box(1209,466,328,39),comparing ? "View edited landscape" : "View saved-weather landscape",
                active:comparing,size:18)) CompareBaseline();
            if (showCascadeSwitch && Button(Box(946,513,591,39),"See weather cascade",icon:"Next"))
                scenarioCascadeOpen = true;
        }

        int timelineControlIndex = -1;

        void DrawScenarioTimeline(Rect rect)
        {
            var evt = Event.current;
            int index = timelineControlIndex = controlIndex++;
            availableControls.Add(index);
            int handle = GUIUtility.GetControlID(FocusType.Passive);
            bool hover = rect.Contains(evt.mousePosition);
            bool focused = keyboardFocus && focusIndex == index;
            EventType type = evt.GetTypeForControl(handle);
            if (type == EventType.MouseDown && evt.button == 0 && hover)
            {
                GUIUtility.hotControl = handle;
                focusIndex = index;
                keyboardFocus = false;
            }
            if (GUIUtility.hotControl == handle && (type == EventType.MouseDown || type == EventType.MouseDrag || type == EventType.MouseUp))
            {
                SeekPreview(Mathf.Clamp(Mathf.FloorToInt((evt.mousePosition.x-rect.x)/rect.width*duration),0,duration-1));
                if (type == EventType.MouseUp) GUIUtility.hotControl = 0;
                evt.Use();
            }
            if (focused && !lookHeld && type == EventType.KeyDown && evt.keyCode is (KeyCode.LeftArrow or KeyCode.RightArrow))
            {
                SeekPreview(Mathf.Clamp(DisplayedMonth+(evt.keyCode == KeyCode.LeftArrow ? -1 : 1),0,duration-1));
                evt.Use();
            }
            Fill(rect,hover ? ButtonHover : ButtonFace);
            DrawButtonEdge(rect,focused ? Yellow : Line);
            float width = rect.width/duration;
            for (int i = 0; i < duration; i++)
            {
                var cell = Box(rect.x+i*width+2,rect.y+3,width-4,rect.height-6);
                bool active = i == DisplayedMonth;
                if (active) Fill(cell,Yellow);
                else if (i != 0) Fill(Box(cell.x-2,cell.y+7,1,cell.height-14),Line);
                Text(cell,ScenarioMonth(i).Date.ToString("MMM",Invariant),18,active ? Ink : White,
                    strongFont,TextAnchor.MiddleCenter);
            }
        }

        void ToggleScenarioPlayback()
        {
            if (running) { paused = !paused; return; }
            bool restart = DisplayedMonth == duration - 1;
            if (restart) SeekPreview(0);
            showResult = false;
            running = true;
            paused = false;
        }

        void DrawScenarioChart(Rect rect, float max, Func<FieldMonth,float> value, string unit)
        {
            for (int i=0;i<3;i++)
            {
                float y = rect.y + i*rect.height/2;
                Fill(Box(rect.x,y,rect.width,1),Line);
                Text(Box(rect.x-54,y-10,46,20),(max*(1-i/2f)).ToString("0.#",Invariant),14,Dim,dataFont,TextAnchor.MiddleRight);
            }
            Vector2 At(int index, float amount) => new(rect.x+rect.width*index/Mathf.Max(1,duration-1),
                Mathf.Lerp(rect.yMax,rect.y,Mathf.Clamp01(amount/max)));
            Text(Box(rect.x-52,rect.y-31,200,22),unit,15,Dim,dataFont);
            var a = At(0,value(ReferenceMonth(0)));
            var b = At(0,value(ScenarioMonth(0)));
            for (int i=1;i<=DisplayedMonth;i++)
            {
                var nextA = At(i,value(ReferenceMonth(i)));
                var nextB = At(i,value(ScenarioMonth(i)));
                DrawChartSegment(a,nextA,Dim);
                DrawChartSegment(b,nextB,Yellow);
                a = nextA;
                b = nextB;
            }
            float cursor = At(DisplayedMonth,0).x;
            Fill(Box(cursor,rect.y,1,rect.height),White);
            Fill(Box(a.x-3,a.y-3,6,6),Dim);
            Fill(Box(b.x-2,b.y-2,4,4),Yellow);
            Text(Box(rect.x,rect.yMax+3,170,22),MonthName(ScenarioMonth(0)),15,Dim,dataFont);
            if (duration > 1)
                Text(Box(rect.xMax-170,rect.yMax+3,170,22),MonthName(ScenarioMonth(duration-1)),15,Dim,dataFont,TextAnchor.MiddleRight);
            Text(Box(rect.x,rect.yMax+28,215,22),"●  Saved weather",15,Dim,dataFont);
            Text(Box(rect.x+221,rect.yMax+28,230,22),"●  Edited weather",15,Yellow,dataFont);
        }

        void DrawScenarioValues(FieldMonth a, FieldMonth b, Func<FieldMonth,float> value, string unit)
        {
            void Row(int i, string title, string values)
            {
                Text(Box(946,259+i*32,270,30),title,17,White);
                Text(Box(1216,259+i*32,321,30),values,16,White,dataFont,TextAnchor.MiddleRight);
            }
            Row(0, "End-of-month soil water", Values(value(a),value(b),unit));
            Row(1,"Precipitation",Values(a.weather.rain,b.weather.rain,"mm"));
            Row(2,"Air temperature",Values(a.weather.temperature,b.weather.temperature,"°C"));
            if (a.water.available && b.water.available)
            {
                Row(3,"Actual evapotranspiration",Values(a.water.actualEtMm,b.water.actualEtMm,"mm"));
                Row(4,"Drainage",Values(a.water.drainageMm,b.water.drainageMm,"mm"));
                Row(5,"Local overflow",Values(a.water.runoffMm,b.water.runoffMm,"mm"));
            }
            else
            {
                Row(3,"Vegetation index",Values(a.land.vegetation*100,b.land.vegetation*100,"%"));
                Row(4,"Drought index",Values(a.land.drought*100,b.land.drought*100,"%"));
            }
        }

        static string Values(double edited, double inherited, string unit) => FormattableString.Invariant($"{edited:0.##} / {inherited:0.##} {unit}");
    }
}
