using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        bool branchesOpen, branchComparisonOpen;
        int branchPage, branchMonth;
        ScenarioRun firstBranchRun, secondBranchRun;
        SiteProjection firstProjection, secondProjection;
        string firstBranchName, secondBranchName;
        string branchStatus;

        void DrawBranches()
        {
            if (Campaign == null || showSites) return;
            if (branchComparisonOpen)
            {
                DrawBranchComparison();
                return;
            }
            var revision = Campaign.Current;
            var branches = Campaign.Branches;
            var active = branches.FirstOrDefault(branch => branch.id == Campaign.ActiveBranchId);
            if (Button(Box(497,336,222,41),active == null ? "Branches" : $"Branch · {active.name}",false,branchesOpen, true,18))
            {
                branchesOpen = !branchesOpen;
                branchStatus = null;
            }
            if (!branchesOpen) return;
            Card(Box(54,399,1032,381),Alpha(Ink,.95f));
            Text(Box(75,412,693,48),$"{active?.name} · Next month {revision.NextDate:MMM yyyy}",31,White,headingFont);
            Text(Box(75,461,968,36),"Fork a saved revision, replay a year, or compare two tests that start on the same month.",21,Dim);
            int count = Mathf.Min(3,branches.Length-branchPage);
            for (int i=0;i<count;i++)
            {
                var branch = branches[branchPage+i];
                if (Button(Box(75+i*211,511,198,42),branch.name,false,branch.id == active?.id,true,19))
                    ChangeBranch(() => Campaign.SwitchBranch(branch.id));
            }
            if (Button(Box(726,511,136,42),"Previous",false,false,branchPage > 0,17)) branchPage = Mathf.Max(0,branchPage-3);
            if (Button(Box(874,511,145,42),"More branches",false,false,branchPage+3 < branches.Length,17)) branchPage += 3;
            var previousRun = Campaign.RunForRevision(revision.id);
            bool modelReady = Campaign.CanProject;
            bool guidedFollowup = IsGuidedScenario && investigation.first != null;
            if (Button(Box(75,577,222,42),"Fork current state",false,false,modelReady && !guidedFollowup,19))
                ChangeBranch(() => Campaign.Fork(revision.id,$"Branch {branches.Length+1}"));
            if (Button(Box(310,577,209,42),"Fork before last run",false,false,modelReady && !guidedFollowup && previousRun != null,19))
                ChangeBranch(() => Campaign.Fork(previousRun.sourceRevisionId,$"Branch {branches.Length+1}"));
            if (Button(Box(531,577,227,42),"Replay branch result",false,false,previousRun != null,19))
            {
                scenarioTable = scenarioMethods = false;
                ReplayRun(previousRun.id,Region.id);
            }
            if (Button(Box(771,577,248,42),"Compare branch years",false,false,previousRun != null && branches.Length > 1,19))
                OpenBranchComparison(active,branches,previousRun);
            Text(Box(75,643,964,52),branchStatus ?? (guidedFollowup
                ? "Finish the guided follow-up from its committed revision to unlock case completion."
                : "The branch you select supplies the field lab’s starting land state and weather."),20,Dim,bodyFont,TextAnchor.UpperLeft,true);
        }

        void ChangeBranch(Action action)
        {
            try
            {
                action();
                Select(siteIndex);
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
                        firstBranchName = active.name;
                        secondBranchName = branch.name;
                        branchMonth = 0;
                        branchComparisonOpen = true;
                        return;
                    }
                    if (candidate != null && candidate.startMonth < run.startMonth) break;
                    revisionId = Campaign.GetRevision(revisionId)?.parentId;
                }
            }
            branchStatus = "Commit runs with a common starting month on two branches to compare them here.";
        }

        void DrawBranchComparison()
        {
            Card(Box(54,399,1032,398),Alpha(Ink,.96f));
            if (Button(Box(821,419,244,40),"Back to branches")) branchComparisonOpen = false;
            Text(Box(75,412,729,60),$"Branch comparison · {MonthName(firstProjection.scenario.months[branchMonth])}",33,White,headingFont);
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
