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
                return "File the historical record and make a prediction from the case desk.";
            var revision = Campaign.Current;
            var inherited = revision.Site(Region.id).ActiveAt(revision.nextMonth);
            var edits = FieldCampaign.Patch(inherited, settings, revision.nextMonth);
            var first = investigation.first;
            if (first == null)
                return edits.Length == 1 && edits[0].control == ForcingControl.Rain
                    ? null : "Change precipitation while keeping the other three inputs at their inherited values.";
            if (revision.id != first.id || Campaign.ActiveBranchId != first.branchId)
                return "Return to the branch and revision saved on your first test.";
            return edits.Length == 1 ? null : "Change exactly one inherited input for the follow-up test.";
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
            if (openedFromFiledReport && Button(Box(956,33,230,42),"← Homecoming report",false,false,!running && PendingPreview == null))
                StorySession.ReturnToFiledReport();
            if (Button(Box(1202,33,164,42),muted ? "Sound off" : "Sound on")) ToggleMute();
            if (Button(Box(1380,33,175,42),reduced ? "Motion reduced" : "Motion full")) ToggleMotion();
            if (Button(Box(1309,94,246,44),"Investigation desk",false,false,!HasPendingAdvance && !running)) OpenCase();
            if (!demoActive && !investigationTest && !running && PendingPreview == null && added.Exists(d => d.id == "drought") &&
                Application.CanStreamedLevelBeLoaded(StorySession.StoryScene) &&
                Button(Box(1046,94,246,44),"Watch crew reaction",true))
                StorySession.PlayCutscene("drought");

            Rect monitor = Box(1026,176,529,388);
            Card(monitor,Rgb("#172a2b"));
            PanelScrews(monitor);
            BeginWeatherScreen(Box(1040,184,501,315));
            Text(Box(1044,184,493,38),label,29,White,headingFont);
            Text(Box(1044,228,493,25),$"Edited {Value(current):0.#} {unit}   /   Inherited {Value(reference):0.#} {unit}",19,White,strongFont);
            if (scenarioMethods)
            {
                string methods = physical ? LocalWaterBalance.Assumptions :
                    "This teaching model uses NASA POWER monthly normals and unitless indices for wetness, vegetation and drought. The landscape illustrates local responses. Both lines start from the same saved state.";
                Text(Box(1044,265,493,228),methods,17,Dim,bodyFont,TextAnchor.UpperLeft,true);
            }
            else if (scenarioTable) DrawScenarioValues(current,reference,Value,unit);
            else
            {
                var chart = Box(1100,292,426,124);
                float max = physical ? (float)current.water.capacityMm : 100;
                DrawScenarioChart(chart,max,Value);
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

            Card(Box(44,660,1511,210),Alpha(Ink,.93f));
            Text(Box(61,672,990,32),$"{MonthName(current)} · Month {DisplayedMonth+1} of {duration}",23,White,strongFont);
            for (int i = 0; i < duration; i++)
            {
                var month = ScenarioMonth(i);
                if (Button(Box(61+i*82,713,77,39),month.Date.ToString("MMM",Invariant),false,DisplayedMonth == i,true,18)) SeekPreview(i);
            }
            if (Button(Box(61,779,147,42),running && !paused ? "Pause" : "Play")) ToggleScenarioPlayback();
            if (Button(Box(220,779,153,42),"Restart",false,false,true,18)) SeekPreview(0);
            if (Button(Box(385,779,165,42),"Go to last month",false,false,true,18))
            {
                if (running) Finish();
                SeekPreview(duration-1);
            }
            if (Button(Box(563,779,247,42),PendingPreview != null ? "Discard preview" : "Return to field lab",false,false,true,18)) Go(View.Lab);
            if (HasPendingAdvance)
            {
                Rect guard = Box(1162,766,375,62);
                Card(guard,Rgb("#4a3920"));
                for (int i = 0; i < 5; i++)
                {
                    Fill(Box(guard.x+5,guard.y+5+i*11,5,7),Yellow);
                    Fill(Box(guard.xMax-10,guard.y+5+i*11,5,7),Yellow);
                }
                if (Button(Box(1176,773,347,48),"Advance & save 12 months",false,false,true,24)) CommitScenario();
            }
            string status = scenarioError ?? SimulationStatus ?? (PendingPreview != null
                ? "Advance saves this result so the next test starts from its final month." : "Saved scenario replay");
            Text(Box(61,833,1478,27),status,18,Dim);
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
