using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        const string GuidedRainOrigin = "sahel";
        const string GuidedSoilOrigin = "phoenix";
        const string HistoricalLink = "Historical comparison of matched records";
        const string NoComparison = "Comparable evidence unavailable";
        Investigation investigation;
        bool caseLoaded, hearingRequested;
        string reportStatus, transientGap;
        bool reportDetails, temperatureOpen;
        EvidenceSeries pairedOrigin, pairedTarget;
        FiledFinding pairedDifference;
        bool orbitOpen, orbitPaused;
        float orbitProgress;
        double orbitLastTime;
        Vector2 orbitStart, orbitEnd;

        void LoadInvestigation()
        {
            if (caseLoaded) return;
            caseLoaded = true;
            if (!LegacyCapture)
            {
                investigation = FieldCaseStore.Load();
                RestoreFiledTest();
                RecoverScenarioCommit();
            }
        }

        bool LegacyCapture
        {
            get
            {
#if HOMECOMING_FIELD_PREVIEW || UNITY_EDITOR
                return legacyPlayback;
#else
                return false;
#endif
            }
        }

        string CaseQuestion => investigation?.kind switch
        {
            InvestigationKind.Rainfall => "Did rainfall change at the same rate at the Sahel and central Chile points?",
            InvestigationKind.Temperature => "How did 2 m air temperature change across Phoenix and central Arizona?",
            InvestigationKind.Soil => "How does a year of changed rainfall affect root-zone water? What happens when you make one more weather edit?",
            _ => "What changed in the Earth record, and where can you check it?"
        };

        void NewInvestigation(InvestigationKind kind)
        {
            investigation = demoActive || investigationTest ? null : FieldCaseStore.Find(kind);
            investigation ??= new Investigation { kind = kind };
            RestoreFiledTest();
            if (kind == InvestigationKind.Soil && investigation.campaignId != Campaign?.Id)
            {
                investigation.first = investigation.second = null;
                investigation.scenarioCall = Direction.Unset;
                investigation.committingRunId = null;
                investigation.campaignId = Campaign?.Id;
            }
            reportDetails = temperatureOpen = false;
            transientGap = reportStatus = null;
            RecoverScenarioCommit();
            SaveInvestigation();
        }

        void RestoreFiledTest()
        {
            var difference = investigation?.difference;
            if (investigation?.kind != InvestigationKind.Rainfall || difference?.hasPValue != true || difference.hasOlsRate)
                return;
            CheckGuidedRecords();
            if (guidedRain == null || difference.id != guidedRain.difference.id ||
                investigation.origin?.id != guidedRain.left.id || investigation.other?.id != guidedRain.right.id) return;
            var test = guidedRain.difference.test;
            difference.hasOlsRate = true;
            difference.olsRatePerDecade = test.slopePerDecade;
            difference.hypothesis = test.hypothesis;
            difference.status = guidedRain.difference.status;
            investigation.comparisonId = guidedRain.id;
        }

        void SaveInvestigation()
        {
#if HOMECOMING_FIELD_PREVIEW || UNITY_EDITOR
            if (capture) return;
#endif
            if (investigation != null && !demoActive && !investigationTest) FieldCaseStore.Save(investigation);
        }

        static bool Usable(EvidenceSeries record) => record != null && record.hasSlope && record.years is { Length: > 1 };
        static string Period(EvidenceSeries record) => $"{record.years[0].year}–{record.years[^1].year}";
        static string FindingRate(FiledFinding finding) => finding?.hasRate == true
            ? $"{Signed(finding.ratePerDecade, "0.###")} {finding.unit} per decade"
            : "Rate unavailable";

        static EvidenceSeries ArchiveRain(ClimateData climate, string site) => climate.evidence.FirstOrDefault(record =>
            record.region == site && record.key == "rain" && record.provider == "reanalysis" && Usable(record));

        string PlaceName(EvidenceSeries record)
        {
            string label = ArchivePlace(record);
            return string.IsNullOrEmpty(label) ? data.regions.FirstOrDefault(r => r.id == record.region)?.name ?? record.region : label;
        }

        FiledFinding MakeFinding(EvidenceSeries record, ArchiveTrend trend = null)
        {
            var finding = new FiledFinding
            {
                id = record.id, variable = record.variable, place = PlaceName(record), footprint = ArchiveFootprint(record),
                product = record.product, version = record.version, provider = record.provider, url = record.url,
                unit = record.unit, aggregation = record.aggregation, window = Period(record), count = record.years.Length,
                hasRate = record.hasSlope, ratePerDecade = record.slope * 10,
                fittedChange = record.slope * (record.years[^1].year - record.years[0].year),
                significance = "Significance not established", status = "not-established", test = "Theil–Sen descriptive slope",
                reason = "No matching statistical review is available for this view."
            };
            ApplyArchiveReview(record, finding, trend);
            return finding;
        }

        static Direction Sign(float value, float tolerance) => value > tolerance ? Direction.Rise :
            value < -tolerance ? Direction.Fall : Direction.LittleChange;

        static string CallName(Direction value) => value switch
        {
            Direction.Rise => "Rise", Direction.Fall => "Fall", Direction.LittleChange => "Little change", _ => "Awaiting your call"
        };

        bool CanFile(EvidenceSeries record)
        {
            if (!Usable(record)) return false;
            return investigation?.kind switch
            {
                InvestigationKind.Rainfall or InvestigationKind.Temperature => false,
                InvestigationKind.Soil => record.region == GuidedSoilOrigin && record.key == "rain" && record.provider == "reanalysis",
                _ => true
            };
        }

        void FileOrigin(EvidenceSeries record, ArchiveTrend trend = null)
        {
            if (!CanFile(record)) return;
            if (investigation == null) NewInvestigation(InvestigationKind.Free);
            var finding = MakeFinding(record,trend);
            if (!finding.hasRate) return;
            if (investigation.origin?.id != finding.id || investigation.origin?.window != finding.window ||
                investigation.origin?.aggregation != finding.aggregation)
            {
                investigation.other = investigation.difference = null;
                investigation.relationship = investigation.relationshipScope = investigation.gap = null;
            }
            investigation.origin = finding;
            if (data.evidence.Any(e => e.id == record.id) && !save.evidence.Contains(record.id))
            {
                save.evidence.Add(record.id);
                Persist();
            }
            SaveInvestigation();
        }

        static bool Comparable(EvidenceSeries a, EvidenceSeries b) => Usable(a) && Usable(b) &&
            a.region != b.region && a.key == b.key && a.product == b.product && a.unit == b.unit &&
            a.aggregation == b.aggregation && a.years.Select(p => p.year).SequenceEqual(b.years.Select(p => p.year));

        bool FindPair()
        {
            pairedDifference = null;
            pairedTrend = null;
            comparisonScope = null;
            if (investigation.kind == InvestigationKind.Rainfall)
                return TryGetReviewedPair(out pairedOrigin, out pairedTarget, out pairedDifference);
            pairedOrigin = FindFiledRecord(investigation.origin);
            pairedTarget = data.evidence.FirstOrDefault(record => Comparable(pairedOrigin, record) && SameScope(pairedOrigin, record));
            return Comparable(pairedOrigin, pairedTarget) && SameScope(pairedOrigin, pairedTarget);
        }

        void FileComparison()
        {
            if (pairedOrigin == null || pairedTarget == null) return;
            if (investigation.kind == InvestigationKind.Rainfall)
            {
                if (pairedDifference?.hasPValue != true) return;
                investigation.origin = MakeFinding(pairedOrigin);
                investigation.comparisonId = guidedRain.id;
            }
            if (investigation.origin == null) return;
            investigation.other = MakeFinding(pairedTarget);
            investigation.difference = pairedDifference;
            investigation.gap = null;
            investigation.relationship = HistoricalLink;
            investigation.relationshipScope = comparisonScope ?? "Matched archived measure, product, time treatment, footprint type and years.";
            SaveInvestigation();
        }

        void FileTemperature()
        {
            if (investigation.kind != InvestigationKind.Temperature || !investigation.callsRevealed ||
                guidedTemperature == null) return;
            investigation.origin = MakeFinding(guidedTemperature);
            SaveInvestigation();
            OpenCase();
        }

        bool RevealTemperature()
        {
            CheckGuidedRecords();
            if (investigation.kind != InvestigationKind.Temperature ||
                investigation.originCall == Direction.Unset || guidedTemperature == null) return false;
            Go(View.Report);
            temperatureOpen = investigation.callsRevealed = true;
            archiveValuePage = 0;
            showArchiveValues = false;
            SaveInvestigation();
            return true;
        }

        void FileGap(string reason)
        {
            if (investigation.origin == null || string.IsNullOrWhiteSpace(reason)) return;
            investigation.other = investigation.difference = null;
            investigation.relationship = NoComparison;
            investigation.relationshipScope = null;
            investigation.gap = reason.Trim();
            SaveInvestigation();
        }

        void OpenCase()
        {
            LoadInvestigation();
            Go(View.Report);
            orbitOpen = reportDetails = temperatureOpen = false;
        }

        bool StartComparison()
        {
            if (!FindPair()) return false;
            if (investigation.kind == InvestigationKind.Rainfall)
            {
                if (investigation.originCall == Direction.Unset || investigation.otherCall == Direction.Unset) return false;
                investigation.callsRevealed = true;
                SaveInvestigation();
            }
            if (!LocateFootprint(pairedOrigin,out orbitStart) || !LocateFootprint(pairedTarget,out orbitEnd))
            {
                OpenComparisonWithoutOrbit();
                return true;
            }
            Go(View.Report);
            orbitOpen = true;
            comparisonHasOrbit = true;
            ResetComparisonView();
            orbitPaused = false;
            orbitProgress = reduced ? 1 : 0;
            orbitLastTime = Time.realtimeSinceStartupAsDouble;
            return true;
        }

        void CloseComparison()
        {
            orbitOpen = false;
            world.Overview(labClock, reduced);
            OpenCase();
        }

        void SubmitInvestigation(string outputFolder = null)
        {
            if (investigation?.IsReady != true || hearingRequested || demoActive) return;
            if (!Application.CanStreamedLevelBeLoaded(StorySession.StoryScene))
            {
                reportStatus = "The hearing scene is missing from this build.";
                return;
            }
            try
            {
                investigation.submittedUtc = DateTime.UtcNow.ToString("O", Invariant);
                FieldCaseStore.Export(new FieldReport
                {
                    caseFile = investigation, question = CaseQuestion, recruit = StorySession.RecruitName,
                    submittedUtc = investigation.submittedUtc
                },outputFolder);
                SaveInvestigation();
                hearingRequested = true;
                var evidence = investigation.first != null || investigation.second != null
                    ? StoryEvidence.Mixed : StoryEvidence.Historical;
                StorySession.ReturnFromSimulation(evidence);
            }
            catch (Exception error) when (error is System.IO.IOException || error is UnauthorizedAccessException || error is InvalidOperationException)
            {
                hearingRequested = false;
                reportStatus = "The report could not be submitted. " + error.Message;
            }
        }
    }
}
