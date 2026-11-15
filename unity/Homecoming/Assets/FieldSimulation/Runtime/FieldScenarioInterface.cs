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

        bool IsGuidedScenario => investigation?.kind == InvestigationKind.Soil && investigation.second == null;
        bool HasGuidedPreview() => PendingPreview?.Id != null && PendingPreview.Id == guidedPreviewId;

        string PredictionToleranceLabel()
        {
            var site = Campaign?.Current.Site(GuidedSoilOrigin);
            return site?.current.hasStorage == true ? "Little change means within ±1 mm." : "Little change means within ±1 percentage point.";
        }

        static string ScenarioResult(InvestigationRun run) =>
            $"The model shows {Signed(run.delta,"0.##")} {run.unit} in {run.endMonth} versus inherited weather";

        string DescribeEdit(InvestigationRun run) => string.Join(", ",Enumerable.Range(0,4)
            .Where(i => run.inherited[i] != run.edited[i]).Select(i =>
                $"{data.controls[i].label} {Signed(run.inherited[i])}{data.controls[i].unit} → {Signed(run.edited[i])}{data.controls[i].unit}"));

        string GuidedLaunchBlocker()
        {
            if (Campaign?.CanProject != true) return SimulationStatus ?? "The saved campaign is unavailable.";
            if (!IsGuidedScenario) return null;
            if (Region.id != GuidedSoilOrigin) return "Open the Central Arizona lab from this case.";
            if (investigation.origin == null || investigation.scenarioCall == Direction.Unset)
                return "Go back to your case to save the rainfall finding and make a prediction.";
            var revision = Campaign.Current;
            var inherited = revision.Site(Region.id).ActiveAt(revision.nextMonth);
            var edits = FieldCampaign.Patch(inherited, settings, revision.nextMonth);
            var first = investigation.first;
            if (first == null)
                return edits.Length == 1 && edits[0].control == ForcingControl.Rain
                    ? null : "Change the precipitation dial. Leave the other dials at their saved values.";
            if (revision.id != first.id || Campaign.ActiveBranchId != first.branchId)
                return "Choose the experiment used for your first test.";
            return edits.Length == 1 && edits[0].control == ForcingControl.Heat
                ? null : "Change only temperature. Keep the precipitation from your first test.";
        }

        void LaunchScenario()
        {
            if (GuidedLaunchBlocker() != null) return;
            duration = FieldCalendar.MonthsInPreview;
            scenarioTable = scenarioMethods = false;
            scenarioError = null;
            Launch();
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
            if (AdvancePreview()) RecoverScenarioCommit();
        }

        void RecoverScenarioCommit()
        {
            if (!IsGuidedScenario || string.IsNullOrEmpty(investigation.committingRunId) ||
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
            else if (result.parentId == investigation.first.id && result.branchId == investigation.first.branchId) investigation.second = result;
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
            bool physical = current.water.available;
            float Value(FieldMonth month) => physical ? (float)month.water.storageMm : month.land.soil * 100;
            string unit = physical ? "mm" : "% index";
            string label = physical ? "Root-zone storage" : "Root-zone wetness index";
            Fade(Box(0,0,1600,218),Alpha(Ink,.93f),0);
            Tag(44,28,physical ? "CONDITIONAL SCENARIO" : "ILLUSTRATIVE SCENARIO",Mint);
            Text(Box(41,55,900,66),Region.name,50,White,headingFont);
            Text(Box(45,128,980,34),$"{MonthName(current)} · {(comparing ? "Inherited weather" : "Edited weather")} view · Illustrative landscape",23,White);
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

            if (cascade != null && !scenarioWaterChart) DrawScenarioCascade(cascade);
            else DrawScenarioWaterMonitor(current,reference,Value,unit,label,physical,cascade != null);

            Card(Box(44,695,1511,175),Ink);
            Text(Box(61,701,1099,32),$"{MonthName(current)} · Month {DisplayedMonth+1} of {duration}",23,White,strongFont);
            if (Button(Box(61,743,53,47),"",icon:running && !paused ? "Pause" : "Play")) ToggleScenarioPlayback();
            DrawScenarioTimeline(Box(130,742,944,49));
            if (Button(Box(1091,743,53,47),"",icon:"Last month"))
            {
                if (running) Finish();
                SeekPreview(duration-1);
            }
            if (HasPendingAdvance)
            {
                if (Button(Box(1184,739,353,55),"Save and advance one year",accent:true,size:23,icon:"Next")) CommitScenario();
            }
            if (Button(Box(1184,809,353,43),PendingPreview != null ? "Back without saving" : "Back to weather lab",icon:"Back")) OpenWeatherLab();
            string status = scenarioError ?? SimulationStatus ?? (HasPendingAdvance
                ? HasGuidedPreview() && investigation.first != null
                    ? "Save the test, then decide what the monthly soil readings support."
                    : "Saving starts your next test where this one ends."
                : PendingPreview != null ? "Play to the end or skip to the last month to save."
                : savedSoilRun && MatchesReportRun(investigation.first,cascade) ? FirstSoilCrewReaction()
                : "WASD to move · Hold right mouse to look · M for sound");
            Text(Box(61,813,1091,34),status,19,Dim);
        }

        void DrawScenarioWaterMonitor(FieldMonth current, FieldMonth reference, Func<FieldMonth,float> value,
            string unit, string label, bool physical, bool showCascadeSwitch)
        {
            Rect monitor = Box(1026,176,529,showCascadeSwitch ? 438 : 388);
            Card(monitor,Rgb("#172a2b"));
            PanelScrews(monitor);
            BeginWeatherScreen(Box(1040,184,501,315));
            Text(Box(1044,184,493,38),label,29,White,headingFont);
            Text(Box(1044,228,493,25),$"Edited {value(current):0.#} {unit}   /   Inherited {value(reference):0.#} {unit}",19,White,strongFont);
            if (scenarioMethods)
            {
                string methods = physical ? LocalWaterBalance.Assumptions :
                    "This teaching model uses NASA POWER monthly normals and unitless indices for wetness, vegetation and drought. The landscape illustrates local responses. Both lines start from the same saved state.";
                Text(Box(1044,265,493,228),methods,17,Dim,bodyFont,TextAnchor.UpperLeft,true);
            }
            else if (scenarioTable) DrawScenarioValues(current,reference,value,unit);
            else
            {
                var chart = Box(1100,292,426,124);
                float max = physical ? (float)current.water.capacityMm : 100;
                DrawScenarioChart(chart,max,value);
                Text(Box(1044,456,493,36),$"Precipitation {current.weather.rain:0.#} / {reference.weather.rain:0.#} mm",17,Dim,dataFont);
            }
            if (Button(Box(1044,507,133,41),scenarioTable && !scenarioMethods ? "Show chart" : "Inspect values",false,false,true,17))
            {
                scenarioTable = !scenarioTable || scenarioMethods;
                scenarioMethods = false;
            }
            if (Button(Box(1184,507,97,41),scenarioMethods ? "Chart" : "Methods",false,scenarioMethods,true,17))
            {
                scenarioMethods = !scenarioMethods;
                scenarioTable = false;
            }
            if (Button(Box(1288,507,249,41),comparing ? "View edited weather" : "View inherited weather",false,comparing,true,18)) CompareBaseline();
            if (showCascadeSwitch && Button(Box(1044,558,493,39),"Back to weather cascade",icon:"Back"))
                scenarioWaterChart = false;
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
                if (active) Fill(cell,Mint);
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

        void DrawScenarioChart(Rect rect, float max, Func<FieldMonth,float> value)
        {
            for (int i=0;i<3;i++)
            {
                float y = rect.y + i*rect.height/2;
                Fill(Box(rect.x,y,rect.width,1),Line);
                Text(Box(rect.x-54,y-10,46,20),(max*(1-i/2f)).ToString("0.#",Invariant),14,Dim,dataFont,TextAnchor.MiddleRight);
            }
            Vector2 At(int index, float amount) => new(rect.x+rect.width*index/Mathf.Max(1,duration-1),
                Mathf.Lerp(rect.yMax,rect.y,Mathf.Clamp01(amount/max)));
            var a = At(0,value(ReferenceMonth(0)));
            var b = At(0,value(ScenarioMonth(0)));
            for (int i=1;i<duration;i++)
            {
                var nextA = At(i,value(ReferenceMonth(i)));
                var nextB = At(i,value(ScenarioMonth(i)));
                DrawChartSegment(a,nextA,Dim);
                DrawChartSegment(b,nextB,Aqua);
                a = nextA;
                b = nextB;
            }
            float cursor = At(DisplayedMonth,0).x;
            Fill(Box(cursor,rect.y,1,rect.height),Yellow);
            Text(Box(rect.x,rect.yMax+9,rect.width,22),"Inherited weather / Edited weather",15,White,dataFont);
        }

        void DrawScenarioValues(FieldMonth a, FieldMonth b, Func<FieldMonth,float> value, string unit)
        {
            void Row(int i, string title, string values)
            {
                Text(Box(1044,274+i*32,245,30),title,17,White);
                Text(Box(1279,274+i*32,258,30),values,16,Aqua,dataFont,TextAnchor.MiddleRight);
            }
            Row(0, "End-of-month soil water", Values(value(a),value(b),unit));
            Row(1,"Precipitation",Values(a.weather.rain,b.weather.rain,"mm"));
            Row(2,"Air temperature",Values(a.weather.temperature,b.weather.temperature,"°C"));
            if (a.water.available)
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

        static string Values(double edited, double inherited, string unit) => $"{edited:0.#} / {inherited:0.#} {unit}";
    }
}
