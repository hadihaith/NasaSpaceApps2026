using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        bool branchesOpen, branchComparisonOpen, resetEarthOpen;
        int branchPage, branchMonth;
        ScenarioRun firstBranchRun, secondBranchRun;
        SiteProjection firstProjection, secondProjection;
        string firstBranchName, secondBranchName;
        string branchStatus;

        void DrawBranches()
        {
            if (Campaign == null || showSites || demoActive) return;
            if (Button(Box(260,336,248,41),"Saved experiments",active:branchesOpen,icon:"Archive"))
            {
                branchesOpen = !branchesOpen;
                branchComparisonOpen = resetEarthOpen = false;
                branchStatus = null;
            }
            if (!branchesOpen) return;
            if (resetEarthOpen)
            {
                DrawEarthReset();
                return;
            }
            if (branchComparisonOpen)
            {
                DrawBranchComparison();
                return;
            }
            var revision = Campaign.Current;
            var branches = Campaign.Branches;
            var active = branches.FirstOrDefault(branch => branch.id == Campaign.ActiveBranchId);
            Card(Box(54,399,1017,381),Ink);
            Text(Box(75,412,728,48),$"{ExperimentName(active.name)} · Next month {revision.NextDate:MMM yyyy}",31,White,headingFont);
            if (Button(Box(822,419,227,40),"Reset Earth",icon:"Weather"))
            {
                resetEarthOpen = true;
                branchStatus = null;
                return;
            }
            Text(Box(75,461,968,36),"Each experiment keeps its own weather and starting ground conditions.",21,Dim);
            int count = Mathf.Min(3,branches.Length-branchPage);
            for (int i=0;i<count && branches.Length > 1;i++)
            {
                var branch = branches[branchPage+i];
                if (Button(Box(75+i*223,511,211,42),ExperimentName(branch.name),active:branch.id == active.id,
                    size:19,icon:branch.id == active.id ? "Check" : "Archive"))
                    ChangeBranch(() => Campaign.SwitchBranch(branch.id));
            }
            if (branchPage > 0 && Button(Box(768,511,46,42),"",icon:"Back",tooltip:"Previous experiments")) branchPage -= 3;
            if (branchPage+3 < branches.Length && Button(Box(826,511,46,42),"",icon:"Next",tooltip:"More experiments")) branchPage += 3;
            var previousRun = Campaign.RunForRevision(revision.id);
            bool guidedFollowup = IsGuidedScenario && investigation.first != null;
            float x = 75, y = branches.Length > 1 ? 576 : 518;
            Rect Slot()
            {
                Rect rect = Box(x,y,219,114);
                x += 234;
                return rect;
            }
            if (Campaign.CanProject && !guidedFollowup)
            {
                if (ControlTile(Slot(),"Copy","Copy setup",tooltip:"Copy current experiment"))
                    ChangeBranch(() => Campaign.Fork(revision.id,$"Experiment {branches.Length+1}"));
                if (previousRun != null && ControlTile(Slot(),"Back","Copy before last run"))
                    ChangeBranch(() => Campaign.Fork(previousRun.sourceRevisionId,$"Experiment {branches.Length+1}"));
            }
            if (previousRun != null)
            {
                if (ControlTile(Slot(),"Play","Replay last run"))
                {
                    scenarioTable = scenarioMethods = false;
                    ReplayRun(previousRun.id,Region.id);
                }
                if (branches.Length > 1 && ControlTile(Slot(),"Chart","Compare runs"))
                    OpenBranchComparison(active,branches,previousRun);
            }
            Text(Box(75,y+131,964,52),branchStatus ?? (guidedFollowup
                ? "Use the experiment from your first test to finish this case."
                : "Copy a setup to try different weather from the same starting point."),20,Dim,bodyFont,TextAnchor.UpperLeft,true);
        }

        void DrawEarthReset()
        {
            Card(Box(54,399,1017,381),Ink);
            Text(Box(75,420,960,52),"Reset Earth?",38,White,headingFont);
            string date = FieldCalendar.Date(FieldCalendar.StartMonth).ToString("MMMM yyyy",Invariant);
            Text(Box(75,482,925,100),$"Start a fresh experiment in {date} with baseline weather and ground conditions at every site. Your saved experiments, discoveries and reports stay available.",
                25,White,bodyFont,TextAnchor.UpperLeft,true);
            if (Button(Box(324,608,245,48),"Keep this experiment",icon:"Back"))
            {
                resetEarthOpen = false;
                branchStatus = null;
                return;
            }
            if (Button(Box(75,608,233,48),"Reset Earth",true,icon:"Weather"))
                ChangeBranch(() =>
                {
                    Campaign.ResetEarth();
                    LastCommittedRevisionId = null;
                    RestoreJournal();
                    resetEarthOpen = false;
                });
            if (branchStatus != null) Text(Box(75,676,925,74),branchStatus,20,Dim,bodyFont,TextAnchor.UpperLeft,true);
        }

        static string ExperimentName(string name)
        {
            if (name == "Main") return "Main experiment";
            return name.StartsWith("Branch ",StringComparison.Ordinal) && int.TryParse(name.Substring(7),out _)
                ? "Experiment " + name.Substring(7) : name;
        }

        void ChangeBranch(Action action)
        {
            try
            {
                action();
                int active = Array.FindIndex(Campaign.Branches, branch => branch.id == Campaign.ActiveBranchId);
                if (active >= 0) branchPage = active / 3 * 3;
                int selected = Array.FindIndex(data.regions, region => region.id == Campaign.SelectedSiteId);
                Select(selected >= 0 ? selected : siteIndex);
                SimulationStatus = Campaign.CanProject ? null : IncompatibleWorld;
                branchStatus = null;
            }
            catch (Exception error) when (IsCampaignError(error)) { branchStatus = error.Message; }
        }

        void OpenBranchComparison(FieldBranch active, FieldBranch[] branches, ScenarioRun run)
        {
            foreach (var branch in branches.Where(b => b.id != active.id))
            {
                for (string revisionId = branch.headRevisionId; !string.IsNullOrEmpty(revisionId);)
                {
                    var candidate = Campaign.RunForRevision(revisionId);
                    if (candidate != null && candidate.startMonth == run.startMonth && candidate.id != run.id &&
                        candidate.modelVersion == run.modelVersion && candidate.dataFingerprint == run.dataFingerprint)
                    {
                        firstBranchRun = run;
                        secondBranchRun = candidate;
                        firstProjection = run.Site(Region.id);
                        secondProjection = candidate.Site(Region.id);
                        if (firstProjection == null || secondProjection == null) break;
                        firstBranchName = ExperimentName(active.name);
                        secondBranchName = ExperimentName(branch.name);
                        branchMonth = 0;
                        branchComparisonOpen = true;
                        return;
                    }
                    if (candidate != null && candidate.startMonth < run.startMonth) break;
                    revisionId = Campaign.GetRevision(revisionId)?.parentId;
                }
            }
            branchStatus = "Save a run in each experiment starting in the same month to compare them here.";
        }

        void DrawBranchComparison()
        {
            Card(Box(54,399,1017,398),Ink);
            if (Button(Box(807,419,244,40),"Saved experiments",icon:"Back")) branchComparisonOpen = false;
            Text(Box(75,412,729,60),$"Compare experiments · {MonthName(firstProjection.scenario.months[branchMonth])}",33,White,headingFont);
            int months = Mathf.Min(firstBranchRun.committedMonths,secondBranchRun.committedMonths);
            var a = firstProjection.scenario.months[branchMonth];
            var b = secondProjection.scenario.months[branchMonth];
            bool physical = a.water.available && b.water.available;
            string unit = physical ? "mm root-zone storage" : "% root-zone wetness index";
            float max = physical ? (float)Math.Max(a.water.capacityMm,b.water.capacityMm) : 100;
            Text(Box(75,474,956,40),unit,20,Dim,dataFont);
            DrawBranchValue(firstBranchName,physical ? (float)a.water.storageMm : a.land.soil*100,534,max,Mint);
            DrawBranchValue(secondBranchName,physical ? (float)b.water.storageMm : b.land.soil*100,615,max,Aqua);
            for (int i=0;i<months;i++)
            {
                string label = firstProjection.scenario.months[i].Date.ToString("MMM",Invariant);
                if (Button(Box(75+i*82,713,76,39),label,false,branchMonth == i,true,18)) branchMonth = i;
            }
        }

        void DrawBranchValue(string branch, float value, float y, float max, Color accent)
        {
            Text(Box(75,y,306,36),branch,23,White,strongFont);
            Fill(Box(383,y+12,541,11),Line);
            Fill(Box(383,y+12,541*Mathf.Clamp01(value/max),11),accent);
            Text(Box(939,y,110,36),value.ToString("0.##",Invariant),22,accent,dataFont,TextAnchor.MiddleRight);
        }
    }
}
