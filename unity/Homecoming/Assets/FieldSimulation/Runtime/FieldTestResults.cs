using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    [Serializable] sealed class SavedHistoricalExample
    {
        public EvidenceSeries left, right;
        public ArchiveRecord leftMetadata, rightMetadata;
        public ArchiveComparison comparison;
    }

    public sealed partial class ScenarioRun
    {
        public string historicalEvidenceJson;
    }

    public sealed partial class FieldSimulation
    {
        bool postTestResults;
        string lastResultsRunId;
        ScenarioRun resultsRun;
        SavedHistoricalExample resultsExample;
        int resultsTab, resultsYear, resultsPage;
        bool resultsDifference;

        void SaveTestResults()
        {
            var preview = PendingPreview;
            if (preview == null) return;
            CheckGuidedRecords();
            if (guidedRain != null && string.IsNullOrEmpty(preview.Run.historicalEvidenceJson))
            {
                preview.Run.historicalEvidenceJson = JsonUtility.ToJson(new SavedHistoricalExample
                {
                    left = guidedKuwait, right = guidedUk,
                    leftMetadata = Exported(guidedKuwait), rightMetadata = Exported(guidedUk),
                    comparison = guidedRain
                });
            }
            if (IsGuidedScenario)
            {
                investigation.committingRunId = preview.Id;
                SaveInvestigation();
            }
            if (!AdvancePreview())
            {
                view = View.Results;
                resultsRun = null;
                return;
            }
            RecoverScenarioCommit();
            OpenTestResults(preview.Id);
        }

        void RetrySaveTestResults()
        {
            view = View.Field;
            SaveTestResults();
        }

        void OpenTestResults(string runId)
        {
            var run = Campaign?.GetRun(runId);
            if (run == null) return;
            lastResultsRunId = Campaign.RunIds.LastOrDefault();
            resultsRun = run;
            resultsExample = string.IsNullOrEmpty(run.historicalEvidenceJson) ? null :
                JsonUtility.FromJson<SavedHistoricalExample>(run.historicalEvidenceJson);
            resultsTab = resultsYear = 0;
            resultsDifference = false;
            Go(View.Results);
        }

        void DrawTestResults()
        {
            Fill(Box(-2200,-2200,6000,5000),Ink);
            if (resultsRun == null)
            {
                Text(Box(64,180,1460,80),"Your test could not be saved",52,White,headingFont);
                Text(Box(64,300,1460,100),SimulationStatus ?? "Retry saving before starting the next test.",26,White,
                    bodyFont,TextAnchor.UpperLeft,true);
                if (Button(Box(64,460,460,62),"Retry saving test",accent:true,icon:"Next")) RetrySaveTestResults();
                return;
            }

            var projection = resultsRun.Site(resultsRun.editedSiteId);
            var final = projection.scenario.months[resultsRun.committedMonths-1];
            string site = data.regions.First(region => region.id == resultsRun.editedSiteId).name;
            Tag(57,28,"TEST SAVED",Mint);
            Text(Box(53,53,1450,60),$"{site} · {MonthName(projection.scenario.months[0])} – {MonthName(final)}",46,White,headingFont);
            string[] tabs = { "Simulation result", "Kuwait / United Kingdom", "T-test & relationship" };
            for (int i = 0; i < tabs.Length; i++)
                if (Button(Box(56+i*496,139,480,48),tabs[i],active:resultsTab == i,size:22)) resultsTab = i;

            if (resultsTab == 0) DrawSavedSimulationResult(projection,final);
            else if (resultsTab == 3) DrawSavedTestList();
            else if (resultsExample?.comparison == null)
                Text(Box(64,260,1460,100),"This saved test has no Kuwait / UK evidence snapshot.",30,White,
                    bodyFont,TextAnchor.UpperLeft,true);
            else if (resultsTab == 1) DrawSavedPlaceComparison();
            else DrawSavedStatisticalTest();

            Fill(Box(56,803,1488,1),Line);
            bool current = Campaign.Current.id == resultsRun.committedRevisionId;
            Text(Box(58,817,1450,30),current
                ? $"Next test starts {Campaign.Current.NextDate:MMM yyyy} with this saved ground and weather."
                : "Viewing an earlier record. Your next test starts from the active saved experiment.",21,Mint,strongFont);
            if (Button(Box(56,854,280,40),"Replay saved result",size:20,icon:"Play"))
            {
                postTestResults = false;
                ReplayRun(resultsRun.id,resultsRun.editedSiteId);
            }
            if (Button(Box(356,854,268,40),"Saved tests",size:20,icon:"Journal"))
            {
                resultsTab = 3;
                resultsPage = 0;
            }
            if (Button(Box(1080,850,464,45),"Next simulation",accent:true,size:25,icon:"Next"))
                OpenWeatherLab(guided:false);
        }

        void DrawSavedTestList()
        {
            var ids = Campaign.RunIds.Reverse().ToArray();
            Text(Box(56,215,1488,55),"Saved tests",44,White,headingFont);
            for (int i = resultsPage; i < Mathf.Min(ids.Length,resultsPage+5); i++)
            {
                var run = Campaign.GetRun(ids[i]);
                var site = data.regions.First(region => region.id == run.editedSiteId);
                var trial = run.Site(run.editedSiteId).scenario;
                float y = 310+(i-resultsPage)*88;
                Text(Box(60,y,1100,39),$"{site.name} · {MonthName(trial.months[0])} – {MonthName(trial.months[run.committedMonths-1])}",27,White,strongFont);
                Text(Box(60,y+40,1100,26),$"Weather edits {run.edits.Length} · {run.committedMonths} months · Saved ground state",19,Dim);
                if (Button(Box(1244,y,300,53),"Open record",size:23,icon:"Chart")) OpenTestResults(run.id);
            }
            if (Button(Box(56,762,180,35),"Previous records",enabled:resultsPage>0,size:19)) resultsPage -= 5;
            if (Button(Box(250,762,180,35),"More records",enabled:resultsPage+5<ids.Length,size:19)) resultsPage += 5;
        }

        void DrawSavedSimulationResult(SiteProjection projection, FieldMonth final)
        {
            var reference = projection.reference.months[resultsRun.committedMonths-1];
            bool physical = final.water.available && reference.water.available;
            float Value(FieldMonth month) => physical ? (float)month.water.storageMm : month.land.soil*100;
            float delta = Value(final)-Value(reference);
            Text(Box(56,219,1488,88),DescribeScenarioOutcome(final,reference,delta,physical),45,White,
                headingFont,TextAnchor.UpperLeft,true);
            string unit = physical ? "mm" : "% wetness index";
            float starting = physical ? (float)projection.scenario.before.storageMm : projection.scenario.before.soil*100;
            Text(Box(60,322,1470,48),$"Starting soil water {starting:0.##} {unit}  →  Saved soil water {Value(final):0.##} {unit}",29,Mint,strongFont);
            Text(Box(60,388,1470,32),$"End of test   Edited weather {Value(final):0.##} {unit}   /   Inherited weather {Value(reference):0.##} {unit}",23,White);
            float maximum = Mathf.Max(1,Enumerable.Range(0,resultsRun.committedMonths)
                .Max(i => Mathf.Max(Value(projection.scenario.months[i]),Value(projection.reference.months[i]))))*1.1f;
            Rect chart = Box(156,492,1328,184);
            for (int i = 0; i < 3; i++)
            {
                float y = chart.yMax-chart.height*i/2;
                Fill(Box(chart.x,y,chart.width,1),Line);
                Text(Box(57,y-12,88,26),(maximum*i/2).ToString("0.#",Invariant),18,Dim,dataFont,TextAnchor.MiddleRight);
            }
            Vector2 At(int i, FieldMonth month) => new(chart.x+chart.width*i/Mathf.Max(1,resultsRun.committedMonths-1),
                chart.yMax-chart.height*Value(month)/maximum);
            for (int i = 1; i < resultsRun.committedMonths; i++)
            {
                DrawChartSegment(At(i-1,projection.reference.months[i-1]),At(i,projection.reference.months[i]),Dim);
                DrawChartSegment(At(i-1,projection.scenario.months[i-1]),At(i,projection.scenario.months[i]),Mint);
            }
            Text(Box(60,444,1450,30),"SOIL WATER   ·   Green = edited weather   /   Gray = inherited weather",20,White,strongFont);
            for (int i = 0; i < resultsRun.committedMonths; i++)
                Text(Box(chart.x+chart.width*i/Mathf.Max(1,resultsRun.committedMonths-1)-40,chart.yMax+14,80,27),
                    projection.scenario.months[i].Date.ToString("MMM",Invariant),18,Dim,dataFont,TextAnchor.MiddleCenter);
            string changes = resultsRun.edits.Length == 0 ? "Inherited weather continued." : string.Join("   ·   ",
                resultsRun.edits.Select(edit => $"{data.controls[(int)edit.control].label} {Signed(edit.value)} {edit.Unit}"));
            Text(Box(60,743,1470,42),changes,22,White,bodyFont,TextAnchor.MiddleLeft,true);
        }

        void DrawSavedPlaceComparison()
        {
            var pair = resultsExample.comparison;
            var a = resultsExample.left;
            var b = resultsExample.right;
            Text(Box(56,215,1488,55),"NASA rainfall records · Kuwait City / London",42,White,headingFont);
            Text(Box(60,278,1460,40),$"1991–2025 · {pair.difference.n} matching years · Average monthly rainfall (mm/month)",24,Dim);
            if (resultsDifference)
            {
                Text(Box(60,349,1460,38),"Kuwait minus United Kingdom · same year, same measurement",26,Mint,strongFont);
                DrawHistoricalChart(pair.difference,Box(154,425,1355,239),includeZero:true,selectedIndex:resultsYear);
            }
            else
            {
                float min = Mathf.Min(a.years.Min(p => p.value),b.years.Min(p => p.value));
                float max = Mathf.Max(a.years.Max(p => p.value),b.years.Max(p => p.value));
                if (max <= min) max = min+1;
                Text(Box(60,349,704,35),"Kuwait City",29,Mint,headingFont);
                Text(Box(834,349,704,35),"London · United Kingdom",29,Aqua,headingFont);
                DrawChart(a,Box(142,425,601,225),min,max,Mint,68,resultsYear);
                DrawChart(b,Box(916,425,601,225),min,max,Aqua,68,resultsYear);
            }
            Text(Box(650,677,300,25),"Year",18,Dim,dataFont,TextAnchor.MiddleCenter);
            var left = pair.left.points[resultsYear];
            var right = pair.right.points[resultsYear];
            if (Button(Box(56,714,54,40),"",enabled:resultsYear>0,icon:"Back")) resultsYear--;
            Text(Box(130,714,1335,40),$"{left.date}   Kuwait {left.value:0.##}   /   UK {right.value:0.##}   /   Difference {left.value-right.value:+0.##;-0.##;0} mm/month",
                23,White,dataFont,TextAnchor.MiddleCenter);
            if (Button(Box(1490,714,54,40),"",enabled:resultsYear+1<pair.left.points.Length,icon:"Next")) resultsYear++;
            if (Button(Box(56,765,405,32),resultsDifference ? "Show both places" : "Show their difference",size:20))
                resultsDifference = !resultsDifference;
            Text(Box(500,766,1035,30),"Historical point estimates · NASA POWER / MERRA-2",19,Dim,dataFont,TextAnchor.MiddleRight);
        }

        void DrawSavedStatisticalTest()
        {
            var pair = resultsExample.comparison;
            bool reviewed = HasReviewedTest(pair.difference);
            var test = reviewed ? pair.difference.test : pair.difference.exploratoryTest;
            Text(Box(56,215,1488,61),"Did the rainfall gap change over time?",44,White,headingFont);
            Text(Box(60,287,1460,44),"Tested record   Kuwait minus UK rainfall, one matched value per year (1991–2025).",24,Dim);
            if (test == null || test.degreesOfFreedom <= 0 || string.IsNullOrEmpty(test.method))
            {
                Text(Box(60,367,1460,80),"The statistical test is unavailable for this saved record.",29,White);
                return;
            }
            string verdict = reviewed ? PairedVerdict(pair.difference) : "Exploratory t-test · assumptions need review";
            Text(Box(60,352,1460,65),verdict,35,Mint,headingFont,TextAnchor.UpperLeft,true);
            Text(Box(60,433,1460,52),$"t({test.degreesOfFreedom}) = {test.tStatistic:0.###}       p = {test.p:G4}       α = {test.alpha:0.##}",34,White,dataFont);
            Text(Box(60,510,1460,40),$"OLS slope {test.slopePerDecade:+0.###;-0.###;0} mm/month per decade    ·    95% CI {test.lowerPerDecade:0.###} to {test.upperPerDecade:0.###}",25,White);
            Text(Box(60,568,1460,40),$"Year-to-year relationship   Pearson r = {pair.pearsonR:0.###}   ·   {pair.left.n} matched years",25,White);
            string relationship = Mathf.Abs(pair.pearsonR) < .3f
                ? "The records show little linear co-movement from year to year."
                : pair.pearsonR > 0 ? "The records tend to rise and fall together." : "The records tend to move in opposite directions.";
            Text(Box(60,628,1460,86),relationship+" The t-test checks whether the rainfall gap changes with time.",23,Dim,
                bodyFont,TextAnchor.UpperLeft,true);
            Text(Box(60,722,1460,66),"NASA observations at Kuwait City and London grid points. These historical tests describe the archived records.",22,Dim,
                bodyFont,TextAnchor.UpperLeft,true);
        }
    }
}
