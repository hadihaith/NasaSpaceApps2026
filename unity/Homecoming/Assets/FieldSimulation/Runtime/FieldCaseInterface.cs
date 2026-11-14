using System;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        void DrawInvestigation()
        {
            if (orbitOpen)
            {
                DrawComparison();
                return;
            }
            Sheet("EARTH SYSTEM TREND DETECTIVE", reportDetails ? "Mission report" : "Investigation desk");
            if (reportDetails && investigation != null)
            {
                DrawReportDetails();
                return;
            }
            if (Button(Box(56,215,482,53),"Two places, one measurement",false,investigation?.kind == InvestigationKind.Rainfall,true,23) &&
                investigation?.kind != InvestigationKind.Rainfall) NewInvestigation(InvestigationKind.Rainfall);
            if (Button(Box(559,215,482,53),"Same weather, different ground",false,investigation?.kind == InvestigationKind.Soil,true,23) &&
                investigation?.kind != InvestigationKind.Soil) NewInvestigation(InvestigationKind.Soil);
            if (Button(Box(1062,215,482,53),"Free investigation",false,investigation?.kind == InvestigationKind.Free,true,23) &&
                investigation?.kind != InvestigationKind.Free) NewInvestigation(InvestigationKind.Free);
            if (investigation == null && !demoActive && Button(Box(56,558,230,41),"Watch rainfall demo",false,false,investigation == null,19))
                WatchCase(InvestigationKind.Rainfall);
            if (investigation == null && !demoActive && Button(Box(559,558,230,41),"Watch soil demo",false,false,investigation == null,19))
                WatchCase(InvestigationKind.Soil);
            if (investigation == null)
            {
                CaseCard(56,291,482,261,"Historical case","Compare the same rainfall measure at two sites. Make a direction call for each, then compare your calls with the Earth record.");
                CaseCard(559,291,482,261,"Scenario case","Predict how root-zone water will respond. Preview a year, advance it, then test one weather edit from the resulting soil state.");
                CaseCard(1062,291,482,261,"Open case","Choose a site, variable and product from the archive. File comparable evidence or describe why the comparison is out of reach.");
                return;
            }
            Text(Box(56,292,1488,54),CaseQuestion,30,White,headingFont,TextAnchor.MiddleLeft,true);
            switch (investigation.kind)
            {
                case InvestigationKind.Rainfall: DrawRainChecklist(); break;
                case InvestigationKind.Soil: DrawSoilChecklist(); break;
                default: DrawFreeChecklist(); break;
            }
            if (Button(Box(56,830,253,43),"Read mission report",true,false,investigation.origin != null,21))
            {
                reportDetails = true;
            }
            if (!demoActive && Button(Box(1000,830,176,43),"Watch demo",false,false,investigation.kind != InvestigationKind.Free,19))
                WatchCase(investigation.kind);
            if (Button(Box(1190,830,354,43),"Open saved reports",false,false,System.IO.Directory.Exists(FieldCaseStore.Folder),18))
                Application.OpenURL(new Uri(FieldCaseStore.Folder + System.IO.Path.DirectorySeparatorChar).AbsoluteUri);
        }

        void CaseCard(float x, float y, float width, float height, string label, string body)
        {
            Card(Box(x,y,width,height),Panel);
            Text(Box(x+20,y+14,width-40,42),label,32,White,headingFont);
            Text(Box(x+20,y+65,width-40,height-72),body,24,Dim,bodyFont,TextAnchor.UpperLeft,true);
        }

        void Step(float y, bool done, string label, string description)
        {
            Card(Box(56,y,1488,77),Panel);
            Text(Box(76,y+8,44,58),done ? "✓" : "·",30,done ? Mint : Yellow,headingFont,TextAnchor.MiddleCenter);
            Text(Box(132,y+7,442,60),label,27,White,headingFont);
            Text(Box(569,y+7,621,60),description,20,Dim,bodyFont,TextAnchor.MiddleLeft,true);
        }

        void DrawRainChecklist()
        {
            bool supported = FindPair();
            if (!supported)
            {
                Step(375,false,"Open matched records","This build has no matching rainfall pair. The archive still supports a free investigation.");
                return;
            }
            bool madeCalls = investigation.originCall != Direction.Unset && investigation.otherCall != Direction.Unset;
            Step(364,madeCalls,"Make a call","Guess the direction of the historical rainfall trend at each site. “Little change” means within ±0.1 mm/month per decade.");
            Text(Box(77,469,722,27),PlaceName(pairedOrigin),23,White,strongFont);
            Text(Box(817,469,722,27),PlaceName(pairedTarget),23,White,strongFont);
            DrawCall(77,504,ref investigation.originCall,investigation.other != null);
            DrawCall(817,504,ref investigation.otherCall,investigation.other != null);
            Step(571,investigation.other != null,"Check the two sites",$"Compare the same rainfall measure over {Period(pairedOrigin)}.");
            if (Button(Box(1203,591,321,40),"Fly to the comparison",true,false,madeCalls,21)) StartComparison();
            Step(659,investigation.IsReady,"Submit report","File both rates, the fitted change, and the significance decision for each record.");
        }

        void DrawCall(float x, float y, ref Direction value, bool locked)
        {
            for (int i = 1; i <= 3; i++)
            {
                var call = (Direction)i;
                if (Button(Box(x+(i-1)*146,y,137,40),CallName(call),false,value == call,!locked,18))
                {
                    value = call;
                    SaveInvestigation();
                }
            }
        }

        void DrawSoilChecklist()
        {
            Step(364,investigation.origin != null,"Open the Arizona rainfall record","File a historical rainfall finding, including its significance status.");
            if (Button(Box(1203,384,321,40),"Open rainfall record",true,false,true,21))
            {
                int index = Array.FindIndex(data.regions, region => region.id == GuidedSoilOrigin);
                if (index >= 0) Select(index);
                OpenArchive("rain");
            }
            Step(452,investigation.scenarioCall != Direction.Unset,"Make a prediction",$"At the end of the first year, will root-zone water rise or fall relative to inherited weather? {PredictionToleranceLabel()}");
            DrawCall(1067,529,ref investigation.scenarioCall,investigation.first != null || HasGuidedPreview());
            Step(578,investigation.first != null && investigation.second != null,"Run the two tests",
                investigation.first == null ? "Change rainfall, preview twelve months, then advance." :
                investigation.second == null ? "Change one weather input from the new land state, then advance again." :
                $"{ScenarioResult(investigation.first)} · Follow-up {Signed(investigation.second.delta,"0.##")} {investigation.second.unit}.");
            if (Button(Box(1203,598,321,40),"Open field lab",true,false,investigation.origin != null && investigation.scenarioCall != Direction.Unset,21))
            {
                int index = Array.FindIndex(data.regions, region => region.id == GuidedSoilOrigin);
                if (index >= 0) Select(index);
                Go(View.Lab);
            }
            DrawSecondPlace(666);
        }

        void DrawFreeChecklist()
        {
            Step(364,investigation.origin != null,"Open record",investigation.origin == null ?
                "Choose a site, variable and product and file its historical rate." : investigation.origin.place + " · " + FindingRate(investigation.origin));
            if (Button(Box(1203,384,321,40),"Browse Earth archive",true,false,true,21)) OpenArchive(null);
            DrawSecondPlace(452);
            Step(640,investigation.IsReady,"Submit report","Bring the rate, footprint, fitted change and significance status to the hearing.");
        }

        void DrawSecondPlace(float y)
        {
            bool hasOrigin = investigation.origin != null;
            bool found = hasOrigin && FindPair();
            bool located = found && LocateFootprint(pairedOrigin,out _) && LocateFootprint(pairedTarget,out _);
            Step(y,investigation.relationship != null,"Check another place",investigation.relationship ?? (found
                ? $"Inspect the matching {PlaceName(pairedTarget)} record."
                : hasOrigin ? "File a reason the record could not be compared." : "File the first record to search for a comparison."));
            if (found)
            {
                if (Button(Box(1203,y+20,321,40),located ? "Fly to the comparison" : "Compare the records",false,false,true,21))
                {
                    if (located) StartComparison();
                    else OpenComparisonWithoutOrbit();
                }
            }
            else if (hasOrigin && investigation.relationship == null)
            {
                transientGap ??= "The archive has no second site with a matching footprint type, product and years for this view.";
                if (Button(Box(1084,y+80,440,38),"File coverage gap",false,false,!string.IsNullOrWhiteSpace(transientGap),18))
                {
                    FileGap(transientGap);
                }
                transientGap = CaseText(Box(56,y+82,1013,35),transientGap,240);
            }
        }

        string CaseText(Rect rect, string value, int maxLength)
        {
            int index = controlIndex++;
            availableControls.Add(index);
            string control = "case-text-" + index;
            GUI.SetNextControlName(control);
            var style = new GUIStyle(GUI.skin.textField) { font = bodyFont, fontSize = 21, wordWrap = false };
            string result = GUI.TextField(rect,value ?? "",maxLength,style);
            if (tabMoved && focusIndex == index) GUI.FocusControl(control);
            if (GUI.GetNameOfFocusedControl() == control)
            {
                keyboardFocus = true;
                focusIndex = index;
            }
            return result;
        }

        void DrawReportDetails()
        {
            var origin = investigation.origin;
            var other = investigation.other;
            string DirectionText(FiledFinding finding) => !finding.hasRate ? "Undetermined" :
                CallName(Sign(finding.ratePerDecade, 0));
            CaseCard(56,214,728,192,"What changed?",$"{origin.variable} · {DirectionText(origin)}" + (other == null ? "" : $" at {origin.place}, {DirectionText(other).ToLowerInvariant()} at {other.place}"));
            CaseCard(816,214,728,192,"Where?",$"{origin.footprint} · {origin.window}" + (other == null ? "" : $"\n{other.footprint} · {other.window}"));
            string Magnitude(FiledFinding f) => $"{FindingRate(f)} · {Signed(f.fittedChange,"0.###")} {f.unit} fitted over the record";
            CaseCard(56,419,728,185,"How much?",Magnitude(origin) + (other == null ? "" : "\n" + Magnitude(other)));
            string SignificanceLine(FiledFinding finding) => (other == null ? "" : finding.place+" · ") + SignificanceLabel(finding,other == null);
            CaseCard(816,419,728,185,"Is it significant?",SignificanceLine(origin) + (other == null ? "" : "\n" + SignificanceLine(other)));
            string extra = investigation.kind == InvestigationKind.Soil && investigation.first != null
                ? $"Prediction: {CallName(investigation.scenarioCall)}. {ScenarioResult(investigation.first)}." +
                    (investigation.second == null ? "" : $" Follow-up: {DescribeEdit(investigation.second)} · {Signed(investigation.second.delta,"0.##")} {investigation.second.unit}.")
                : investigation.kind == InvestigationKind.Rainfall && investigation.other != null
                ? $"Your calls: {CallName(investigation.originCall)} / {CallName(investigation.otherCall)}. " +
                    $"Estimated directions: {CallName(Sign(origin.ratePerDecade,.1f))} / {CallName(Sign(other.ratePerDecade,.1f))}."
                : investigation.gap ?? investigation.relationship ?? "Check a second place from the investigation desk.";
            Text(Box(56,610,1488,42),extra,21,White,bodyFont,TextAnchor.MiddleLeft,true);
            if (investigation.kind == InvestigationKind.Soil && investigation.relationship != null)
                Text(Box(56,652,1488,27),investigation.gap ?? investigation.relationship,19,Mint);
            if (investigation.difference != null)
                Text(Box(56,652,1488,27),$"Paired difference ({origin.place} minus {other.place}) · {FindingRate(investigation.difference)} · {SignificanceLabel(investigation.difference)}",19,Mint);
            string prompt = investigation.first != null && investigation.first.result != investigation.scenarioCall
                ? "Explain why the result differed from your prediction" : "Your conclusion (optional)";
            Text(Box(56,687,1488,29),prompt,22,Dim,strongFont);
            string conclusion = CaseText(Box(56,723,1488,40),investigation.conclusion,300);
            if (conclusion != (investigation.conclusion ?? ""))
            {
                investigation.conclusion = conclusion;
                SaveInvestigation();
            }
            Text(Box(56,775,1488,35),reportStatus ?? (investigation.IsReady ? "The report is ready for the hearing." : "Finish the steps on the investigation desk to submit."),22,White);
            if (Button(Box(56,830,280,43),"Back to investigation")) reportDetails = false;
            if (Button(Box(1214,830,330,43),"Submit to the hearing",true,false,investigation.IsReady && !hearingRequested && !demoActive,23)) SubmitInvestigation();
        }

        static string SignificanceLabel(FiledFinding finding, bool includeReason = true) => finding.significance + (finding.hasPValue
            ? $" (p = {finding.pValue.ToString("G3",Invariant)})"
            : !includeReason || string.IsNullOrEmpty(finding.reason) ? "" : " · " + finding.reason);
    }
}
