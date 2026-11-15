using System;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        bool casePickerOpen;
        static readonly (string Icon, string Label)[] HistoricalSteps =
            { ("Rise","Predict"), ("Chart","Check data"), ("Journal","Report") };
        static readonly (string Icon, string Label)[] WeatherSteps =
            { ("Archive","Rainfall"), ("Weather","Test weather"), ("Chart","Compare"), ("Soil","Explain"), ("Journal","Report") };
        static readonly (string Icon, string Label)[] OpenSteps =
            { ("Archive","Find a record"), ("Chart","Compare"), ("Journal","Report") };

        static string CaseTitle(InvestigationKind kind) => kind switch
        {
            InvestigationKind.Rainfall => "Rainfall in two places",
            InvestigationKind.Temperature => "Is Phoenix warming?",
            InvestigationKind.Soil => "Weather and soil",
            _ => "Your own investigation"
        };

        void OpenCasePicker()
        {
            OpenCase();
            casePickerOpen = true;
        }

        void DrawInvestigation()
        {
            if (CascadeReportOpen)
            {
                DrawCascadeReport();
                return;
            }
            if (orbitOpen)
            {
                DrawComparison();
                return;
            }
            if (temperatureOpen)
            {
                DrawTemperatureEvidence();
                return;
            }
            if (casePickerOpen || investigation == null)
            {
                DrawCasePicker();
                return;
            }
            Sheet("EARTH SCIENCE CASE", reportDetails ? "Mission report" : CaseTitle(investigation.kind));
            if (reportDetails && investigation != null)
            {
                DrawCaseProgress(Box(979,115,534,73),investigation.kind);
                DrawReportDetails();
                return;
            }
            DrawCaseProgress(Box(979,115,534,73),investigation.kind);
            Text(Box(56,207,1215,71),CaseQuestion,29,Dim,bodyFont,TextAnchor.UpperLeft,true);
            if (!demoActive && Button(Box(1312,212,232,42),"Browse cases",icon:"Search"))
            {
                casePickerOpen = true;
                return;
            }
            switch (investigation.kind)
            {
                case InvestigationKind.Rainfall: DrawRainChecklist(); break;
                case InvestigationKind.Temperature: DrawTemperatureChecklist(); break;
                case InvestigationKind.Soil: DrawSoilChecklist(); break;
                default: DrawFreeChecklist(); break;
            }
            bool reportAvailable = investigation.origin != null &&
                (investigation.kind != InvestigationKind.Soil || investigation.second != null);
            if (reportAvailable && Button(Box(56,820,281,51),"Read mission report",true,size:23,icon:"Journal"))
            {
                reportDetails = true;
            }
            if (!demoActive && investigation.kind is (InvestigationKind.Rainfall or InvestigationKind.Soil) &&
                Button(Box(1355,827,189,43),"Watch demo",icon:"Play"))
                WatchCase(investigation.kind);
        }

        void DrawCasePicker()
        {
            Sheet("FIELD ASSIGNMENTS","Choose a case");
            if (investigation != null && Button(Box(1304,214,240,42),"Back to current case",icon:"Back"))
            {
                casePickerOpen = false;
                return;
            }
            ChooseCase(Box(56,288,728,200),InvestigationKind.Rainfall,"Rain","Compare rainfall in the Sahel and central Chile.");
            ChooseCase(Box(816,288,728,200),InvestigationKind.Temperature,"Temperature",
                "Explore 45 years of Phoenix temperatures.");
            ChooseCase(Box(56,512,728,200),InvestigationKind.Soil,"Soil",
                "Change the weather and see how much water stays in the soil.");
            ChooseCase(Box(816,512,728,200),InvestigationKind.Free,"Search",
                "Look for a trend anywhere in the Earth archive.");
            if (System.IO.Directory.Exists(FieldCaseStore.Folder) &&
                Button(Box(56,824,246,45),"Open saved reports",icon:"Archive"))
                Application.OpenURL(new Uri(FieldCaseStore.Folder + System.IO.Path.DirectorySeparatorChar).AbsoluteUri);
        }

        void ChooseCase(Rect rect, InvestigationKind kind, string icon, string summary)
        {
            bool current = investigation?.kind == kind;
            bool starter = kind == InvestigationKind.Rainfall;
            Color background = ButtonBackground(rect,false,current,true);
            var state = ButtonInput(rect);
            Color tint = kind switch { InvestigationKind.Rainfall => Aqua, InvestigationKind.Temperature => Yellow,
                InvestigationKind.Soil => Rgb("#e6b18f"), _ => Mint };
            Fill(rect,background);
            DrawButtonEdge(rect,state.focused ? Yellow : starter || current ? tint : Line);
            var emblem = Box(rect.x+12,rect.y+12,175,rect.height-24);
            Fill(emblem,Ink);
            for (int i = 1; i <= 5; i++)
            {
                float offset = i*emblem.width/6;
                Fill(Box(emblem.x+offset,emblem.y+1,1,emblem.height-2),Alpha(tint,.12f));
                Fill(Box(emblem.x+1,emblem.y+offset,emblem.width-2,1),Alpha(tint,.12f));
            }
            DrawIcon(icon,emblem.center,tint,5.4f);
            Text(Box(rect.x+215,rect.y+42,rect.width-271,53),CaseTitle(kind),39,White,headingFont);
            Text(Box(rect.x+216,rect.y+110,rect.width-276,66),summary,24,Dim,bodyFont,TextAnchor.UpperLeft,true);
            DrawIcon("Next",new Vector2(rect.xMax-35,rect.center.y),tint,1.3f);
            if (state.pressed)
            {
                if (kind == InvestigationKind.Soil)
                {
                    OpenSoilMission();
                    return;
                }
                if (!current) NewInvestigation(kind);
                OpenCase();
            }
        }

        void DrawCaseProgress(Rect rect, InvestigationKind kind)
        {
            var file = investigation?.kind == kind ? investigation : null;
            var steps = kind switch
            {
                InvestigationKind.Soil => WeatherSteps,
                InvestigationKind.Free => OpenSteps,
                _ => HistoricalSteps
            };
            int stage = file == null ? 0 : kind switch
            {
                InvestigationKind.Soil => file.origin == null ? 0 : file.second == null ? 1 :
                    file.relationship == null ? 2 : file.HasSoilInterpretation ? 4 : 3,
                InvestigationKind.Free => file.origin == null ? 0 : file.relationship == null ? 1 : 2,
                _ => file.origin != null ? 2 : file.originCall != Direction.Unset &&
                    (kind != InvestigationKind.Rainfall || file.otherCall != Direction.Unset) ? 1 : 0
            };
            if (!string.IsNullOrEmpty(file?.submittedUtc)) stage = steps.Length;
            DrawProgress(rect,steps,stage);
        }

        void DrawProgress(Rect rect, (string Icon, string Label)[] steps, int stage)
        {
            float width = rect.width/steps.Length;
            for (int i = 0; i < steps.Length-1; i++)
                Fill(Box(rect.x+width*(i+.5f),rect.y+21,width,2),i < stage ? Mint : Line);
            for (int i = 0; i < steps.Length; i++)
            {
                float x = rect.x+i*width;
                var node = Box(x+width/2-23,rect.y,46,44);
                bool current = i == stage, complete = i < stage;
                Fill(node,current ? Mint : complete ? ChoiceFace : Ink);
                DrawButtonEdge(node,current || complete ? Mint : Line);
                DrawIcon(complete ? "Check" : steps[i].Icon,node.center,current ? Ink : complete ? Mint : Dim,1.25f);
                Text(Box(x,rect.y+46,width,24),steps[i].Label,17,current ? White : Dim,strongFont,TextAnchor.MiddleCenter);
            }
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
            if (done) Text(Box(76,y+8,44,58),"✓",30,Mint,headingFont,TextAnchor.MiddleCenter);
            else DrawIcon("Next",new Vector2(98,y+38),Mint);
            Text(Box(132,y+7,442,60),label,27,White,headingFont);
            Text(Box(569,y+7,621,60),description,20,Dim,bodyFont,TextAnchor.MiddleLeft,true);
        }

        void DrawRainChecklist()
        {
            CheckGuidedRecords();
            if (guidedRain == null)
            {
                Step(324,false,"Rainfall records unavailable","Choose another case to keep playing.");
                return;
            }
            bool madeCalls = investigation.originCall != Direction.Unset && investigation.otherCall != Direction.Unset;
            bool filed = investigation.other != null;
            bool revealed = investigation.callsRevealed || filed;
            Step(324,madeCalls,madeCalls ? "Predictions ready" : "Predict the change",
                madeCalls ? $"Compare rainfall over {Period(guidedSahel)}." : "Pick a direction for each place before you see the data.");
            Card(Box(56,420,728,180),Panel);
            Card(Box(816,420,728,180),Panel);
            Text(Box(80,441,678,54),"Central Sahel",34,White,headingFont);
            Text(Box(840,441,678,54),"Central Chile",34,White,headingFont);
            DrawCall(80,503,ref investigation.originCall,revealed,large:true);
            DrawCall(840,503,ref investigation.otherCall,revealed,large:true);
            if (Button(Box(56,632,311,52),revealed ? "Review the data" : "Check the data",
                !filed,false,madeCalls,23,icon:"Next")) StartComparison();
            if (!madeCalls)
                Text(Box(388,632,1148,52),"Choose one answer for each place to continue.",22,Dim);
            else if (filed)
                Text(Box(388,632,1148,52),"Findings saved. Read your mission report to send them to the crew.",22,Mint);
            if (!revealed)
                Text(Box(56,699,1488,36),"Little change means within ±0.1 mm of monthly rainfall per decade.",19,Dim);
        }

        void DrawCall(float x, float y, ref Direction value, bool locked, Direction original = Direction.Unset, bool large = false)
        {
            for (int i = 1; i <= 3; i++)
            {
                var call = (Direction)i;
                string label = CallName(call);
                bool selected = (value == Direction.Unset ? original : value) == call;
                Rect rect = Box(x+(i-1)*158,y,150,large ? 84 : 40);
                if (locked)
                {
                    if (selected)
                    {
                        Fill(rect,ChoiceFace);
                        if (large) DrawDirectionTile(rect,label,true,false);
                        else
                        {
                            DrawIcon(label,new Vector2(rect.x+23,rect.center.y),Mint);
                            Text(Box(rect.x+43,rect.y,rect.width-43,rect.height),label,18,White,strongFont);
                        }
                    }
                }
                else if (large ? DrawDirectionTile(rect,label,selected,true) : Button(rect,label,active:selected,size:18,icon:label))
                {
                    value = call == original ? Direction.Unset : call;
                    SaveInvestigation();
                }
            }
        }

        bool DrawDirectionTile(Rect rect, string label, bool selected, bool interactive)
        {
            var background = interactive ? ButtonBackground(rect,selected,false,true) : ChoiceFace;
            var state = interactive ? ButtonInput(rect) : (pressed:false,focused:false);
            Fill(rect,background);
            DrawButtonEdge(rect,state.focused ? Yellow : selected ? Mint : Line);
            Color foreground = interactive && selected ? Ink : White;
            DrawIcon(label,new Vector2(rect.center.x,rect.y+30),foreground,2.3f);
            Text(Box(rect.x,rect.yMax-28,rect.width,24),label,18,foreground,strongFont,TextAnchor.MiddleCenter);
            return state.pressed;
        }

        void DrawTemperatureChecklist()
        {
            CheckGuidedRecords();
            if (guidedTemperature == null)
            {
                Step(324,false,"Temperature record unavailable","Choose another case to keep playing.");
                return;
            }
            bool filed = investigation.origin != null;
            bool revealed = investigation.callsRevealed || filed;
            bool madeCall = investigation.originCall != Direction.Unset;
            Step(324,madeCall,madeCall ? "Prediction ready" : "Predict the change",
                madeCall ? "Open the record to see how temperature changed." : "Did the average air temperature rise, fall or change very little?");
            Card(Box(56,420,1488,180),Panel);
            Text(Box(80,441,1420,54),$"Phoenix and central Arizona · {Period(guidedTemperature)}",34,White,headingFont);
            DrawCall(80,503,ref investigation.originCall,revealed,large:true);
            if (Button(Box(56,632,311,52),revealed ? "Review the record" : "Check the record",!filed,false,
                investigation.originCall != Direction.Unset,23,icon:"Next")) RevealTemperature();
            if (filed) Text(Box(388,632,1148,52),"Finding saved. Read your mission report to send it to the crew.",22,Mint);
            if (!revealed) Text(Box(56,699,1488,36),"Little change means within ±0.1 °C per decade.",19,Dim);
        }

        void DrawTemperatureEvidence()
        {
            var record = guidedTemperature;
            var trend = Exported(record).finding;
            var finding = MakeFinding(record,trend);
            Sheet("HISTORICAL RECORD","Phoenix temperature");
            DrawCaseProgress(Box(979,115,534,73),InvestigationKind.Temperature);
            if (Button(Box(56,212,205,42),"Back to case",icon:"Back")) OpenCase();
            if (Button(Box(278,212,202,42),"Open data source",false,false,!string.IsNullOrEmpty(finding.url)))
                Application.OpenURL(finding.url);
            if (Button(Box(1320,212,224,42),showArchiveValues ? "Show chart" : "Inspect values")) showArchiveValues = !showArchiveValues;
            Card(Box(56,277,1488,530),Panel);
            Text(Box(82,294,1420,33),$"{finding.footprint} · {finding.window} · {finding.count} complete years",23,White,strongFont);
            Text(Box(82,333,1420,60),$"{finding.product} · 15 cosine-weighted grid cells\n{finding.aggregation} · {finding.unit}",21,Dim);
            if (showArchiveValues) DrawArchiveTable(record,trend,424);
            else DrawHistoricalChart(trend,Box(165,425,1319,128));
            Text(Box(82,603,1420,34),$"Descriptive Theil–Sen · {FindingRate(finding)} · {Signed(finding.fittedChange,"0.###")} °C fitted over 44 years",23,White);
            Text(Box(82,642,1420,33),$"{SignificanceLabel(finding,false)} · {finding.hypothesis}",22,Mint);
            Text(Box(82,678,1420,32),TestSummary(finding),20,White);
            Text(Box(82,721,1420,62),finding.reviewRationale,20,Dim,bodyFont,TextAnchor.UpperLeft,true);
            if (Button(Box(56,829,221,43),showPredictionEdits ? "Hide prediction" : "Change prediction",size:19))
                showPredictionEdits = !showPredictionEdits;
            if (showPredictionEdits) DrawCall(303,831,ref investigation.revisedOriginCall,false,investigation.originCall);
            if (Button(Box(1192,829,352,43),"File temperature finding",true,false,true,22)) FileTemperature();
        }

        void DrawSoilChecklist()
        {
            if (investigation.origin == null)
            {
                Step(324,false,"Start with rainfall","Open the Arizona rainfall record and save a finding for your report.");
                if (Button(Box(56,424,290,50),"Open rainfall record",accent:true,icon:"Next"))
                {
                    SelectArizona();
                    OpenArchive("rain");
                }
                return;
            }
            if (investigation.first == null)
            {
                Step(324,false,"Predict the change","When you change only precipitation, will the soil hold more or less water after a year?");
                DrawCall(56,424,ref investigation.scenarioCall,HasGuidedPreview(),large:true);
                Text(Box(56,515,1420,29),PredictionToleranceLabel(),19,Dim);
            }
            else if (investigation.second == null)
            {
                Step(324,false,"Run a follow-up test","Change only temperature. Keep the rain and ground state from your first test.");
                if (Button(Box(358,424,250,50),"Inspect first test",icon:"Chart")) OpenSoilRunReport(investigation.first);
                Text(Box(56,502,1488,40),FirstSoilCrewReaction(),22,Mint);
            }
            else
            {
                Step(308,true,"Both tests saved","Compare the edited and inherited tracks for each test.");
                DrawSecondPlace(402);
                DrawSoilInterpretation();
                if (Button(Box(352,820,239,51),"Inspect first test",icon:"Chart")) OpenSoilRunReport(investigation.first);
                if (Button(Box(604,820,245,51),"Inspect follow-up",icon:"Chart")) OpenSoilRunReport(investigation.second);
                if (Button(Box(862,820,255,51),"Revisit weather lab",icon:"Weather")) OpenWeatherLab();
                return;
            }
            if (investigation.scenarioCall != Direction.Unset &&
                Button(Box(56,investigation.first == null ? 566 : 424,286,50),
                    "Open weather lab",accent:true,icon:"Weather"))
            {
                SelectArizona();
                OpenWeatherLab();
            }
        }

        void SelectArizona()
        {
            int index = Array.FindIndex(data.regions, region => region.id == GuidedSoilOrigin);
            if (index >= 0) Select(index);
        }

        void DrawFreeChecklist()
        {
            Step(324,investigation.origin != null,"Find an Earth record",investigation.origin == null ?
                "Choose a place and a measurement, then save a finding for your report." : investigation.origin.place + " · " + FindingRate(investigation.origin));
            if (Button(Box(1203,344,321,40),"Browse Earth archive",investigation.origin == null,icon:"Archive")) OpenArchive(null);
            if (investigation.origin != null) DrawSecondPlace(424);
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
            if (caseInputStyle == null)
            {
                caseInputStyle = new GUIStyle
                {
                    font = bodyFont, fontSize = 21, alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip, padding = new RectOffset(12,12,0,0)
                };
                caseInputStyle.normal.textColor = caseInputStyle.hover.textColor =
                    caseInputStyle.active.textColor = caseInputStyle.focused.textColor = White;
            }
            Fill(rect,ButtonFace);
            DrawButtonEdge(rect,keyboardFocus && focusIndex == index ? Yellow : Line);
            string result = GUI.TextField(rect,value ?? "",maxLength,caseInputStyle);
            if (tabMoved && focusIndex == index) GUI.FocusControl(control);
            if (GUI.GetNameOfFocusedControl() == control)
            {
                keyboardFocus = true;
                focusIndex = index;
            }
            return result;
        }

        GUIStyle caseInputStyle;

        void DrawReportDetails()
        {
            var origin = investigation.origin;
            var other = investigation.other;
            string DirectionText(FiledFinding finding) => !finding.hasRate ? "Undetermined" :
                CallName(Sign(finding.ratePerDecade, 0));
            CaseCard(56,214,728,192,"What changed?",$"{origin.variable} · {DirectionText(origin)}" + (other == null ? "" : $" at {origin.place}, {DirectionText(other).ToLowerInvariant()} at {other.place}"));
            CaseCard(816,214,728,192,"Where?",$"{origin.footprint} · {origin.window}" + (other == null ? "" : $"\n{other.footprint} · {other.window}"));
            string Magnitude(FiledFinding f) => $"{FindingRate(f)} · {Signed(f.fittedChange,"0.###")} {f.unit} fitted";
            CaseCard(56,419,728,185,"How much? · Theil–Sen",Magnitude(origin) + (other == null ? "" : "\n" + Magnitude(other)));
            string SignificanceLine(FiledFinding finding) => (other == null ? "" : finding.place+" · ") + SignificanceLabel(finding,other == null);
            CaseCard(816,419,728,185,"Is it significant?",SignificanceLine(origin) + (other == null ? "" : "\n" + SignificanceLine(other)) +
                (investigation.difference == null ? "" : "\nPaired difference · " + SignificanceLabel(investigation.difference,false)));
            string Calls(Direction original, Direction revision) => revision == Direction.Unset ?
                CallName(original) : $"{CallName(original)} → {CallName(revision)}";
            string extra = investigation.kind == InvestigationKind.Soil && investigation.first != null
                ? $"Prediction: {CallName(investigation.scenarioCall)}. {ScenarioResult(investigation.first)}." +
                    (investigation.second == null ? "" : $" Follow-up: {DescribeEdit(investigation.second)} · {Signed(investigation.second.delta,"0.##")} {investigation.second.unit}.")
                : investigation.kind == InvestigationKind.Rainfall && investigation.other != null
                ? $"Your calls: {Calls(investigation.originCall,investigation.revisedOriginCall)} / " +
                    $"{Calls(investigation.otherCall,investigation.revisedOtherCall)}. " +
                    $"Estimated directions: {CallName(Sign(origin.ratePerDecade,.1f))} / {CallName(Sign(other.ratePerDecade,.1f))}."
                : investigation.kind == InvestigationKind.Temperature
                ? $"Your call: {Calls(investigation.originCall,investigation.revisedOriginCall)}. " +
                    $"Estimated direction: {CallName(Sign(origin.ratePerDecade,.1f))}."
                : investigation.gap ?? investigation.relationship ?? "Check a second place from the investigation desk.";
            Text(Box(56,604,1488,32),extra,20,White,bodyFont,TextAnchor.MiddleLeft,true);
            if (investigation.kind == InvestigationKind.Soil && investigation.relationship != null)
                Text(Box(56,634,1488,49),investigation.HasSoilInterpretation ? investigation.soilInterpretation :
                    investigation.gap ?? investigation.relationship,19,Mint,bodyFont,TextAnchor.MiddleLeft,true);
            if (investigation.difference != null)
            {
                Text(Box(56,636,1488,25),$"Sahel minus Chile · Theil–Sen {Magnitude(investigation.difference)}",19,Mint);
                Text(Box(56,660,1488,25),TestSummary(investigation.difference),18,White);
            }
            else if (origin.hasPValue)
                Text(Box(56,652,1488,27),TestSummary(origin),19,Mint);
            bool missedCall = investigation.first != null && investigation.first.result != investigation.scenarioCall;
            string prompt = missedCall
                ? "Explain why the result differed from your prediction" : "Your conclusion (optional)";
            Text(Box(56,687,1488,29),prompt,22,Dim,strongFont);
            string conclusion = CaseText(Box(56,723,1488,40),investigation.conclusion,300);
            if (conclusion != (investigation.conclusion ?? ""))
            {
                investigation.conclusion = conclusion;
                SaveInvestigation();
            }
            Text(Box(56,775,1488,35),reportStatus ?? (investigation.IsReady ? "The report is ready for the hearing." :
                investigation.kind == InvestigationKind.Soil && !investigation.HasSoilInterpretation
                    ? "Interpret the follow-up in your case before sending the report."
                    : missedCall && string.IsNullOrWhiteSpace(investigation.conclusion)
                        ? "Add a first-test explanation before sending the report."
                        : "Finish the steps in your case to send the report."),22,White);
            if (Button(Box(56,830,233,43),"Back to case",icon:"Back")) reportDetails = false;
            if (Button(Box(1214,830,330,43),"Submit to the hearing",true,false,investigation.IsReady && !hearingRequested && !demoActive,23)) SubmitInvestigation();
        }

        static string SignificanceLabel(FiledFinding finding, bool includeReason = true) => finding.significance + (finding.hasPValue
            ? $" (p = {finding.pValue.ToString("G3",Invariant)})"
            : !includeReason || string.IsNullOrEmpty(finding.reason) ? "" : " · " + finding.reason);

        static string TestSummary(FiledFinding finding)
        {
            if (!finding.hasPValue) return SignificanceLabel(finding);
            string slope = finding.hasOlsRate ? $"slope {Signed(finding.olsRatePerDecade,"0.###")} {finding.unit} per decade · " : "";
            string interval = finding.hasInterval
                ? $"95% CI {Signed(finding.intervalLow,"0.###")} to {Signed(finding.intervalHigh,"0.###")} {finding.unit} per decade · "
                : "";
            return $"Two-sided OLS · {slope}{interval}p = {finding.pValue.ToString("G3",Invariant)} · α = {finding.alpha.ToString("0.##",Invariant)}";
        }
    }
}
