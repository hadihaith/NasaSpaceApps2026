using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        enum SoilInterpretation { MoreAtEnd, LessAtEnd, ConvergedAtEnd, WithinTolerance, Unavailable }

        CascadeResult soilFollowupCascade;
        string soilFollowupCampaignId, soilFollowupRevisionId;
        bool soilInterpretationCorrection;

        void OpenSoilMission()
        {
            LoadInvestigation();
            NewInvestigation(InvestigationKind.Soil);
            OpenCase();
        }

        CascadeResult ReadSoilRun(InvestigationRun filed)
        {
            if (filed == null || Campaign == null || Campaign.Id != investigation.campaignId) return null;
            var run = string.IsNullOrEmpty(filed.runId) ? Campaign.RunForRevision(filed.id) : Campaign.GetRun(filed.runId);
            if (run == null || run.committedRevisionId != filed.id || run.branchId != filed.branchId) return null;
            return FieldCascade.Analyze(run,filed.regionId);
        }

        CascadeResult ReadSoilFollowup()
        {
            string revisionId = investigation.second?.id;
            if (soilFollowupRevisionId != revisionId || soilFollowupCampaignId != Campaign?.Id)
            {
                soilFollowupRevisionId = revisionId;
                soilFollowupCampaignId = Campaign?.Id;
                soilFollowupCascade = ReadSoilRun(investigation.second);
            }
            return soilFollowupCascade;
        }

        void OpenSoilRunReport(InvestigationRun filed)
        {
            var result = filed == investigation.second ? ReadSoilFollowup() : ReadSoilRun(filed);
            if (result != null) OpenCascadeReport(result);
        }

        static CascadeSeries SoilSeries(CascadeResult result)
        {
            var storage = result?.Series(CascadeMetric.RootZoneStorage);
            return storage?.available == true ? storage : result?.Series(CascadeMetric.RootZoneWetnessIndex);
        }

        static SoilInterpretation InterpretSoil(CascadeSeries series)
        {
            if (series?.available != true || series.samples.Length == 0) return SoilInterpretation.Unavailable;
            if (series.firstMeaningfulMonth < 0) return SoilInterpretation.WithinTolerance;
            return series.samples[^1].direction switch
            {
                CascadeDirection.Increase => SoilInterpretation.MoreAtEnd,
                CascadeDirection.Decrease => SoilInterpretation.LessAtEnd,
                _ => SoilInterpretation.ConvergedAtEnd
            };
        }

        static string SoilExplanation(CascadeResult result, CascadeSeries series, SoilInterpretation claim)
        {
            if (claim == SoilInterpretation.Unavailable)
                return "The saved follow-up has no usable paired root-zone readings.";
            if (claim == SoilInterpretation.WithinTolerance)
                return $"The paired {series.label.ToLowerInvariant()} readings stayed within ±{ReportNumber(series.tolerance)} " +
                    $"{series.deltaUnit} across all {series.samples.Length} months.";

            var first = series.samples.First(sample => sample.monthIndex == series.firstMeaningfulMonth);
            var end = series.samples[^1];
            var loss = result.Series(CascadeMetric.ActualEt);
            string context = loss.available ? $" Actual ET changed by {ReportDelta(loss.samples[first.monthIndex-result.startMonth].delta)} " +
                $"{loss.deltaUnit} in {ReportMonth(first.monthIndex)}." : "";
            return $"{series.label} first crossed its tolerance in {ReportMonth(first.monthIndex)} ({ReportDelta(first.delta)} " +
                $"{series.deltaUnit}). It ended {ReportMonth(end.monthIndex)} at {ReportDelta(end.delta)} {series.deltaUnit} " +
                $"relative to inherited weather.{context}";
        }

        bool TryFileSoilInterpretation(SoilInterpretation choice)
        {
            if (investigation?.kind != InvestigationKind.Soil || investigation.first == null || investigation.second == null) return false;
            var result = ReadSoilFollowup();
            var series = SoilSeries(result);
            soilInterpretationCorrection = choice != InterpretSoil(series);
            if (soilInterpretationCorrection) return false;

            investigation.soilInterpretation = SoilExplanation(result,series,choice);
            investigation.soilInterpretationRevisionId = investigation.second.id;
            if (investigation.first.result != investigation.scenarioCall && string.IsNullOrWhiteSpace(investigation.conclusion))
                investigation.conclusion = ScenarioResult(investigation.first) + " after changing only precipitation.";
            SaveInvestigation();
            return true;
        }

        void DrawSoilInterpretation()
        {
            var series = SoilSeries(ReadSoilFollowup());
            var outcome = InterpretSoil(series);
            bool teachingIndex = series?.available == true && series.kind == CascadeKind.Educational;
            string reading = teachingIndex ? "root-zone wetness index" : "root-zone storage";
            Card(Box(56,548,1488,253),Panel);
            Tag(76,563,"INTERPRET THE FOLLOW-UP",Mint);
            Text(Box(76,586,1430,35),$"What do the paired {reading} readings show across the full year?",25,White,strongFont);
            if (investigation.HasSoilInterpretation)
            {
                Text(Box(76,637,1435,64),investigation.soilInterpretation,24,White,bodyFont,TextAnchor.UpperLeft,true);
                Text(Box(76,713,1435,57),SoilCrewReaction(outcome),22,Mint,bodyFont,TextAnchor.UpperLeft,true);
                return;
            }

            Offer(76,633,SoilInterpretation.MoreAtEnd,"The edited run ended higher");
            Offer(808,633,SoilInterpretation.LessAtEnd,"The edited run ended lower");
            Offer(76,681,SoilInterpretation.ConvergedAtEnd,"The runs differed, then converged by year-end");
            Offer(808,681,SoilInterpretation.WithinTolerance,"Every month stayed within tolerance");
            if (outcome == SoilInterpretation.Unavailable)
                Offer(76,729,SoilInterpretation.Unavailable,"The paired readings are unavailable");
            if (soilInterpretationCorrection)
                Text(Box(808,729,709,42),"NYXEN    Check the full year of monthly readings and the tolerance.",19,Yellow);

            void Offer(float x, float y, SoilInterpretation choice, string label)
            {
                if (Button(Box(x,y,709,41),label,size:21)) TryFileSoilInterpretation(choice);
            }
        }

        static string SoilCrewReaction(SoilInterpretation outcome) => outcome switch
        {
            SoilInterpretation.MoreAtEnd => "ZORVAX    The edited reading ended higher. Put both readings in the report.",
            SoilInterpretation.LessAtEnd => "NYXEN    The edited reading ended lower. Send both readings.",
            SoilInterpretation.ConvergedAtEnd => "CADET B    The traces came back together. I marked the earlier change, too.",
            SoilInterpretation.WithinTolerance => "CADET B    The traces stayed close. A quiet result still goes in the log.",
            _ => "NYXEN    Mark the missing comparison so Earth knows what we could read."
        };

        string FirstSoilCrewReaction() => investigation.first.result switch
        {
            Direction.LittleChange => "CADET B    The tracks finished close. Keep that rain setting for test two.",
            var result when result == investigation.scenarioCall => "ZORVAX    Your call held up! Keep the rain setting for test two.",
            _ => "NYXEN    The year ended differently from your call. Check the paired readings and keep going."
        };
    }
}
