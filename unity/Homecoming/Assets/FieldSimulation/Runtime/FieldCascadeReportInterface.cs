using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        enum CascadeReportPage { Summary, Months, Run, Historical }

        sealed class CascadeArchiveFinding
        {
            public EvidenceSeries record;
            public ArchiveTrend trend;
            public FiledFinding finding;

            public string Label => trend == null ? "Filed finding" : record.key switch
            {
                "rain" => "Precipitation", "temperature" => "Air temperature", "soil" => "Root-zone wetness",
                _ => finding.variable
            };
        }

        CascadeResult cascadeReport;
        CascadeArchiveFinding[] cascadeHistorical;
        FieldTrial cascadeReportTrial;
        string cascadeReportCampaignId;
        CascadeReportPage cascadeReportPage;
        int cascadeReportMonth, cascadeEffectIndex, cascadeMetricPage, cascadeHistoricalIndex, cascadeArchiveValuePage;
        bool cascadeArchiveValues, cascadeArchivePins;

        bool CascadeReportOpen => cascadeReport != null;

        void OpenCascadeReport(CascadeResult result)
        {
            if (result == null) return;
            ReleaseLook();
            if (view == View.Field && running) paused = true;
            cascadeReport = result;
            cascadeReportTrial = view == View.Field ? trial : null;
            cascadeReportCampaignId = Campaign?.Id;
            cascadeReportPage = CascadeReportPage.Summary;
            cascadeReportMonth = view == View.Field && result.frames.Length > 0
                ? Mathf.Clamp(DisplayedMonth,0,result.frames.Length-1) : 0;
            cascadeEffectIndex = Array.FindIndex(result.effects,effect => result.Series(effect.metric).kind != CascadeKind.Direct);
            cascadeEffectIndex = Math.Max(0,cascadeEffectIndex);
            cascadeMetricPage = cascadeHistoricalIndex = cascadeArchiveValuePage = 0;
            cascadeArchiveValues = cascadeArchivePins = false;
            cascadeHistorical = CascadeHistoricalFindings(result.siteId).ToArray();
        }

        void CloseCascadeReport()
        {
            cascadeReport = null;
            cascadeHistorical = null;
            cascadeReportTrial = null;
            cascadeReportCampaignId = null;
        }

        bool IsCurrentReportPreview(CascadeResult result) => result.provenance == CascadeProvenance.Preview &&
            view == View.Field && !string.IsNullOrEmpty(result.runId) && Campaign?.Id == cascadeReportCampaignId &&
            PendingPreview?.Id == result.runId && PendingPreview.BranchId == result.branchId &&
            PendingPreview.SourceRevisionId == result.sourceRevisionId && Region.id == result.siteId;

        bool IsReportCaseRun(CascadeResult result) => !string.IsNullOrEmpty(result.committedRevisionId) &&
            Campaign?.Id == cascadeReportCampaignId && investigation?.kind == InvestigationKind.Soil &&
            investigation.campaignId == cascadeReportCampaignId &&
            (MatchesReportRun(investigation.first,result) || MatchesReportRun(investigation.second,result));

        static bool MatchesReportRun(InvestigationRun filed, CascadeResult result) =>
            filed?.id == result.committedRevisionId && filed.branchId == result.branchId &&
            filed.regionId == result.siteId && (string.IsNullOrEmpty(filed.runId) || filed.runId == result.runId);

        void AdvanceCascadeReport(CascadeResult result)
        {
            if (!IsCurrentReportPreview(result) || !HasPendingAdvance) return;
            var source = Campaign.Current;
            if (source.id != result.sourceRevisionId || Campaign.ActiveBranchId != result.branchId) return;
            CommitScenario();
            if (PendingPreview != null) return;
            var run = Campaign.GetRun(result.runId);
            if (run?.committedRevisionId != null && run.committedRevisionId == LastCommittedRevisionId)
                OpenCascadeReport(FieldCascade.Analyze(run,result.siteId));
        }

        void DrawCascadeReport()
        {
            var result = cascadeReport;
            if (result == null) return;
            Fill(Box(-2200,-2200,6000,5000),Alpha(Ink,.97f));
            var place = data.regions.FirstOrDefault(site => site.id == result.siteId)?.name ?? result.siteId;
            Tag(56,34,"FIELD STATION  /  " + place, Mint);
            Text(Box(52,62,1470,77),"Earth Intelligence Report",63,White,headingFont);

            var preview = result.provenance == CascadeProvenance.Preview;
            var state = preview ? "PREVIEW · UNSAVED" : "COMMITTED";
            Tag(56,148,"HYPOTHETICAL SIMULATION  /  " + state,preview ? Yellow : Mint);
            Tag(1084,148,"HISTORICAL EARTH DATA",Aqua);
            ReportTab(Box(56,180,251,47),CascadeReportPage.Summary,"Response summary");
            ReportTab(Box(318,180,238,47),CascadeReportPage.Months,"Monthly log",result.frames.Length > 0);
            ReportTab(Box(567,180,207,47),CascadeReportPage.Run,"Run details");
            ReportTab(Box(1084,180,460,47),CascadeReportPage.Historical,"Inspect archived findings");

            Card(Box(56,245,1488,539),Panel);
            switch (cascadeReportPage)
            {
                case CascadeReportPage.Historical: DrawCascadeHistorical(); break;
                case CascadeReportPage.Months: DrawCascadeMonthly(result); break;
                case CascadeReportPage.Run: DrawCascadeRunDetails(result); break;
                default: DrawCascadeSummary(result); break;
            }

            Card(Box(56,802,1488,71),Ink);
            if (Button(Box(73,815,224,45),view == View.Field ? "Back to playback" : "Back to case",icon:"Back"))
            {
                CloseCascadeReport();
                return;
            }

            if (preview)
            {
                bool live = IsCurrentReportPreview(result);
                string status = live ? scenarioError ?? SimulationStatus ??
                    (HasPendingAdvance ? "Saving advances this experiment." : "Reach the final month in playback to save.") :
                    "This preview is no longer active.";
                Text(Box(315,815,639,45),status,18,Dim,bodyFont,TextAnchor.MiddleLeft,true);
                if (Button(Box(969,815,232,45),"Discard preview",enabled:live,size:20))
                {
                    CloseCascadeReport();
                    OpenWeatherLab();
                    return;
                }
                if (Button(Box(1214,815,313,45),"Save and advance one year",accent:true,
                    enabled:live && HasPendingAdvance,size:20,icon:"Next"))
                    AdvanceCascadeReport(result);
            }
            else if (IsReportCaseRun(result))
            {
                Text(Box(315,815,686,45),reportStatus ??
                    (investigation.IsReady ? "The case can be filed with the crew." : "Continue the case to file your report."),
                    19,Dim,bodyFont,TextAnchor.MiddleLeft,true);
                if (view == View.Field && Button(Box(990,815,206,45),"Continue case"))
                {
                    CloseCascadeReport();
                    OpenCase();
                    return;
                }
                if (Button(Box(1214,815,313,45),"Submit case to hearing",true,false,
                    investigation.IsReady && !hearingRequested && !demoActive,20))
                    SubmitInvestigation();
            }
        }

        void ReportTab(Rect rect, CascadeReportPage page, string title, bool enabled = true)
        {
            if (Button(rect,title,active:cascadeReportPage == page,enabled:enabled,size:21)) cascadeReportPage = page;
        }

        static string ReportMonth(int month) => FieldCalendar.Date(month).ToString("MMM yyyy",Invariant);
        static string ReportNumber(double n) => n.ToString("0.######",Invariant);
        static string ReportDelta(double delta)
        {
            if (delta == 0) return "0";
            double amount = Math.Abs(delta);
            return (delta > 0 ? "+" : "−") + (amount < .000001 ? "<0.000001" : ReportNumber(amount));
        }
        static string ReportWindow(CascadeResult result) => result.frames.Length == 0 ? "Window unavailable" :
            $"{ReportMonth(result.frames[0].monthIndex)} – {ReportMonth(result.frames[^1].monthIndex)}";

        static string ReportKind(CascadeKind kind) => kind switch
        {
            CascadeKind.Direct => "Weather input",
            CascadeKind.Physical => "Model calculation",
            _ => "Teaching index"
        };

        static string ReportTransition(CascadeEffect effect) => effect.transition switch
        {
            CascadeTransition.ReturnToQuiet => "Within tolerance again",
            CascadeTransition.Reversal => effect.sample.direction == CascadeDirection.Increase ? "Reversed to an increase" : "Reversed to a decrease",
            CascadeTransition.Resumed => effect.sample.direction == CascadeDirection.Increase ? "Increase resumed" : "Decrease resumed",
            _ => effect.sample.direction == CascadeDirection.Increase ? "First increase" : "First decrease"
        };

        void DrawCascadeSummary(CascadeResult result)
        {
            string status = result.status switch
            {
                CascadeStatus.Response => "The model responded",
                CascadeStatus.Quiet => "Quiet response",
                _ => "Comparison unavailable"
            };
            Text(Box(82,261,1434,53),status,41,White,headingFont);
            string qualification = result.status switch
            {
                CascadeStatus.Quiet => "The available modeled outcomes stayed within their display tolerances.",
                CascadeStatus.Unavailable => result.unavailableReason ?? "The matched model results are unavailable.",
                _ => "Both runs start from the same state. Deltas show the effect of the weather edits in this model."
            };
            if (result.frames.Length > 0 && result.status != CascadeStatus.Unavailable && !result.hasPhysicalLedger)
                qualification += " Physical water outputs are unavailable.";
            Text(Box(82,318,1434,42),$"{ReportWindow(result)}  ·  {qualification}",21,Dim,bodyFont,TextAnchor.MiddleLeft,true);
            if (result.status == CascadeStatus.Quiet && result.hasPhysicalLedger)
                DrawQuietWaterResponse(result);
            if (result.effects.Length == 0) return;

            const int pageSize = 6;
            int page = cascadeEffectIndex / pageSize;
            int offset = page * pageSize;
            int remaining = result.effects.Length - offset;
            for (int i = 0; i < Math.Min(remaining,pageSize); i++)
            {
                int index = offset+i;
                var effect = result.effects[index];
                var series = result.Series(effect.metric);
                string label = $"{ReportMonth(effect.sample.monthIndex)}  /  {series.label}  /  {ReportTransition(effect)}";
                if (Button(Box(82,373+i*51,766,44),label,active:index == cascadeEffectIndex,size:18))
                    cascadeEffectIndex = index;
            }

            if (PageCascadeRows(679,page,result.effects.Length,pageSize,"Effect pages",out int next))
            {
                cascadeEffectIndex = next * pageSize;
            }
            DrawCascadeEffect(result,result.effects[cascadeEffectIndex]);
        }

        void DrawQuietWaterResponse(CascadeResult result)
        {
            // Describe the ledger only when it accounts for the applied rain change.
            foreach (var frame in result.frames)
            {
                var a = frame.inherited.water;
                var b = frame.edited.water;
                double rain = b.rainMm - a.rainMm;
                if (rain == 0 || Math.Abs(rain - (b.actualEtMm - a.actualEtMm)) > 1e-7 ||
                    Math.Abs(b.startStorageMm - a.startStorageMm) > 1e-7 ||
                    Math.Abs(b.storageMm - a.storageMm) > 1e-7 ||
                    Math.Abs(b.drainageMm - a.drainageMm) > 1e-7 ||
                    Math.Abs(b.runoffMm - a.runoffMm) > 1e-7) continue;
                Text(Box(82,729,766,43), $"{ReportMonth(frame.monthIndex)} · Rain and actual ET each changed by " +
                    $"{ReportDelta(rain)} mm. End storage matched at {ReportNumber(b.storageMm)} mm.",17,Dim,
                    bodyFont,TextAnchor.UpperLeft,true);
                break;
            }
        }

        void DrawCascadeEffect(CascadeResult result, CascadeEffect effect)
        {
            var series = result.Series(effect.metric);
            var sample = effect.sample;
            Fill(Box(875,372,642,376),Ink);
            Tag(895,386,ReportKind(series.kind).ToUpperInvariant() + "  /  " + ReportMonth(sample.monthIndex).ToUpperInvariant(),Mint);
            Text(Box(895,412,602,44),series.label,32,White,headingFont);
            Text(Box(895,461,602,44),
                $"Inherited  {ReportNumber(sample.inherited)} {series.valueUnit}     Edited  {ReportNumber(sample.edited)} {series.valueUnit}",
                18,White,bodyFont,TextAnchor.MiddleLeft,true);
            Text(Box(895,510,602,29),$"Change  {ReportDelta(sample.delta)} {series.deltaUnit}",21,Aqua,strongFont);
            Text(Box(895,545,602,101),effect.explanation,19,White,bodyFont,TextAnchor.UpperLeft,true);

            string inputs = string.Join(", ",effect.inputs.Select(input =>
                $"{result.Series(input.metric).label} ({ReportMonth(input.monthIndex)})"));
            if (!string.IsNullOrEmpty(inputs))
                Text(Box(895,652,602,47),"Changed model inputs · " + inputs,17,Dim,bodyFont,TextAnchor.UpperLeft,true);
            if (Button(Box(895,704,264,34),"Inspect this month",size:17,icon:"Chart"))
            {
                cascadeReportMonth = sample.monthIndex - result.startMonth;
                int index = Array.FindIndex(result.series,item => item.metric == effect.metric);
                cascadeMetricPage = Math.Max(0,index) / CascadeMetricsPerPage;
                cascadeReportPage = CascadeReportPage.Months;
            }
        }

        const int CascadeMetricsPerPage = 7;

        void DrawCascadeMonthly(CascadeResult result)
        {
            Text(Box(82,258,1436,47),"Inherited and edited weather",36,White,headingFont);
            float monthWidth = Math.Min(180f,1436f/result.frames.Length);
            for (int i = 0; i < result.frames.Length; i++)
                if (Button(Box(82+i*monthWidth,315,monthWidth-6,39),ReportMonth(result.frames[i].monthIndex),
                    active:cascadeReportMonth == i,size:17)) cascadeReportMonth = i;
            var frame = result.frames[cascadeReportMonth];
            Text(Box(89,362,639,34),$"{ReportMonth(frame.monthIndex)}  ·  Change = edited minus inherited",20,Dim);
            Text(Box(766,362,746,34),result.hasPhysicalLedger
                ? $"Start storage · inherited {ReportNumber(frame.inherited.water.startStorageMm)} mm / edited {ReportNumber(frame.edited.water.startStorageMm)} mm"
                : "Physical water ledger unavailable for this comparison",18,Dim,bodyFont,TextAnchor.MiddleRight);

            string[] headings = { "Measurement", "Inherited", "Edited", "Change", "Display tolerance" };
            float[] columns = { 103, 537, 760, 987, 1258 };
            for (int i = 0; i < headings.Length; i++)
                Text(Box(columns[i],398,251,29),headings[i],17,Mint,strongFont);
            int offset = cascadeMetricPage*CascadeMetricsPerPage;
            for (int i = 0; i < Math.Min(CascadeMetricsPerPage,result.series.Length-offset); i++)
            {
                var series = result.series[offset+i];
                int y = 431+i*38;
                Fill(Box(83,y,1434,36),i%2 == 0 ? Ink : ButtonFace);
                Text(Box(103,y,411,36),$"{series.label}  ·  {ReportKind(series.kind)}",17,White);
                if (!series.available)
                {
                    Text(Box(columns[1],y,975,36),series.unavailableReason ?? "Unavailable",17,Dim,
                        bodyFont,TextAnchor.MiddleLeft,true);
                    continue;
                }
                var sample = series.samples[cascadeReportMonth];
                Text(Box(columns[1],y,213,36),$"{ReportNumber(sample.inherited)} {series.valueUnit}",17,White);
                Text(Box(columns[2],y,214,36),$"{ReportNumber(sample.edited)} {series.valueUnit}",17,White);
                Text(Box(columns[3],y,256,36),$"{ReportDelta(sample.delta)} {series.deltaUnit}",17,
                    sample.direction == CascadeDirection.LittleChange ? Dim : Aqua);
                Text(Box(columns[4],y,252,36),$"±{ReportNumber(series.tolerance)} {series.deltaUnit}",17,Dim);
            }
            if (PageCascadeRows(716,cascadeMetricPage,result.series.Length,CascadeMetricsPerPage,"Measurement pages",out int page))
                cascadeMetricPage = page;
            bool samePlayback = view == View.Field && cascadeReportTrial != null &&
                ReferenceEquals(trial,cascadeReportTrial) && Region.id == result.siteId;
            bool canReplay = view == View.Report && IsReportCaseRun(result);
            if ((samePlayback || canReplay) && Button(Box(1247,716,269,40),"View month in playback",size:17,icon:"Play"))
            {
                if (!samePlayback && !ReplayRun(result.runId,result.siteId)) return;
                SeekPreview(cascadeReportMonth);
                CloseCascadeReport();
            }
        }

        bool PageCascadeRows(int y, int page, int count, int pageSize, string label, out int next)
        {
            next = page;
            int pages = (count+pageSize-1)/pageSize;
            if (pages <= 1) return false;
            if (Button(Box(82,y,128,40),"Previous",enabled:page > 0,size:17)) next--;
            Text(Box(219,y,233,40),$"{label}  {page+1} / {pages}",17,Dim,dataFont,TextAnchor.MiddleCenter);
            if (Button(Box(463,y,122,40),"Next",enabled:page+1 < pages,size:17)) next++;
            return next != page;
        }

        void DrawCascadeRunDetails(CascadeResult result)
        {
            Text(Box(82,261,1436,49),ReportWindow(result),36,White,headingFont);
            Text(Box(82,319,1436,41),"Both tracks use the same starting state. The inherited track keeps the saved weather schedule.",
                21,Dim);
            if (result.edits.Length == 0)
                Text(Box(82,374,882,40),"The inherited weather schedule has no edits in this run.",21,White);
            else
            {
                Text(Box(82,365,882,29),"Weather schedule edits",21,Mint,strongFont);
                string edits = string.Join("\n",result.edits.Select(edit =>
                {
                    if ((int)edit.control < 0 || (int)edit.control >= data.controls.Length)
                        return "The edit uses an unrecognized weather control.";
                    string label = data.controls[(int)edit.control].label;
                    string period = edit.endMonthExclusive > edit.startMonth
                        ? $"{ReportMonth(edit.startMonth)} – {ReportMonth(edit.endMonthExclusive-1)}"
                        : $"From {ReportMonth(edit.startMonth)}";
                    string inherited = "";
                    int offset = edit.startMonth - result.startMonth;
                    if (result.siteId == result.editedSiteId && offset >= 0 && offset < result.frames.Length)
                        inherited = Signed(result.frames[offset].inherited.forcing[(int)edit.control]) + " → ";
                    return $"{label}  {inherited}{Signed(edit.value)} {edit.Unit}  ·  {period}";
                }));
                Text(Box(82,399,882,370),edits,19,White,bodyFont,TextAnchor.UpperLeft,true);
            }

            Fill(Box(983,368,534,394),Ink);
            string provenance = $"Run  {result.runId}\nBranch  {result.branchId}\nSource revision  {result.sourceRevisionId}\n" +
                (result.provenance == CascadeProvenance.Committed ? $"Saved revision  {result.committedRevisionId}\n" : "") +
                $"Model  {result.modelVersion}  ·  Effect rules  {result.rulesVersion}\nModel data fingerprint\n{result.dataFingerprint}";
            Text(Box(1001,385,501,227),provenance,16,White,bodyFont,TextAnchor.UpperLeft,true);
            string scope = result.hasPhysicalLedger ?
                "The Arizona model uses 1991–2020 climate normals and an uncalibrated 150 mm soil bucket. Runoff is local overflow." :
                "Physical water values are unavailable. Wetness values, where available, are teaching indices.";
            Text(Box(1001,619,497,122),scope + " Vegetation and fuel dryness are teaching indices. Statistical significance applies to reviewed historical findings.",
                17,Dim,bodyFont,TextAnchor.UpperLeft,true);
        }

        IEnumerable<CascadeArchiveFinding> CascadeHistoricalFindings(string siteId)
        {
            if (string.IsNullOrEmpty(siteId)) yield break;
            CheckGuidedRecords();

            // Keep a case's exact local selection alongside the explicitly labeled archive context.
            var filed = investigation?.origin;
            var record = data.evidence.FirstOrDefault(item => item.region == siteId &&
                item.id == (filed?.datasetId ?? filed?.id) && item.key is ("rain" or "soil" or "temperature"));
            ArchiveTrend selected = null;
            if (record != null)
            {
                var archive = Exported(record);
                if (archive != null)
                    selected = new[] { archive.finding, archive.anomalyFinding }
                        .Concat(archive.monthFindings ?? Array.Empty<ArchiveTrend>()).FirstOrDefault(item => item?.id == filed.id);
                var exact = selected == null ? null : MakeCascadeArchiveFinding(record,selected);
                if (exact != null && MatchesFiledScope(filed,exact.finding))
                    yield return exact;
                else
                {
                    var snapshot = JsonUtility.FromJson<FiledFinding>(JsonUtility.ToJson(filed));
                    WithholdCascadeReview(snapshot,"The filed scope could not be matched to this archive pack.");
                    yield return new CascadeArchiveFinding { record = record, finding = snapshot };
                    selected = null;
                }
            }

            foreach (var key in new[] { "rain", "temperature", "soil" })
            {
                var context = data.evidence.FirstOrDefault(item => item.region == siteId && item.key == key &&
                    item.provider == "reanalysis" && Usable(item));
                if (key == "temperature" && guidedTemperature?.region == siteId)
                    context = guidedTemperature;
                var trend = context == null ? null : Exported(context)?.finding;
                if (trend != null && trend.id != selected?.id)
                    yield return MakeCascadeArchiveFinding(context,trend);
            }
        }

        static bool MatchesFiledScope(FiledFinding original, FiledFinding current) =>
            original.id == current.id && original.fingerprint == current.fingerprint && original.rawSha256 == current.rawSha256 &&
            original.datasetId == current.datasetId && original.mode == current.mode && original.window == current.window &&
            original.product == current.product && original.version == current.version && original.footprint == current.footprint &&
            original.variable == current.variable && original.unit == current.unit && original.aggregation == current.aggregation;

        CascadeArchiveFinding MakeCascadeArchiveFinding(EvidenceSeries record, ArchiveTrend trend)
        {
            var archive = Exported(record);
            var finding = MakeFinding(record,trend);
            finding.unit = trend.unit;
            // CheckGuidedRecords validates the reviewed annual Arizona footprint and complete values.
            // The paired rainfall test belongs to its own matched two-location comparison.
            bool reviewed = record == guidedTemperature && trend == archive.finding && HasReviewedTest(trend) &&
                trend.datasetId == archive.id && trend.unit == record.unit &&
                !string.IsNullOrEmpty(archive.rawSha256) && !string.IsNullOrEmpty(archive.fingerprint) &&
                trend.id == $"historical-v2:{archive.fingerprint}:1981:2025:annual:-";
            if (!reviewed)
                WithholdCascadeReview(finding,finding.reason ?? "No matching reviewed test is available for this exact record and view.");
            return new CascadeArchiveFinding { record = record, trend = trend, finding = finding };
        }

        static void WithholdCascadeReview(FiledFinding finding, string reason)
        {
            finding.status = "not-established";
            finding.significance = "Significance not established";
            finding.reason = reason;
            finding.hasPValue = finding.hasInterval = finding.hasOlsRate = false;
            finding.test = finding.hypothesis = finding.review = finding.reviewer = finding.reviewRationale = null;
        }

        void DrawCascadeHistorical()
        {
            if (cascadeHistorical.Length == 0)
            {
                Text(Box(82,267,1436,62),"There is no matching local record in this archive pack.",28,White);
                return;
            }

            for (int i = 0; i < cascadeHistorical.Length; i++)
            {
                var item = cascadeHistorical[i];
                string label = item.Label + (item.trend == null ? "" : " · " + CascadeArchiveMode(item.trend));
                if (Button(Box(82+i*359,264,350,38),label,active:i == cascadeHistoricalIndex,size:17))
                {
                    cascadeHistoricalIndex = i;
                    cascadeArchiveValuePage = 0;
                    cascadeArchiveValues = cascadeArchivePins = false;
                }
            }
            var entry = cascadeHistorical[cascadeHistoricalIndex];
            var finding = entry.finding;
            Text(Box(82,310,1010,46),finding.variable,33,White,headingFont);
            Text(Box(82,359,1436,31),finding.footprint + "  ·  " + finding.window + "  ·  " + CascadeCoverage(entry),20,Dim);
            Text(Box(82,394,1436,43),$"{finding.product}\n{finding.aggregation}",18,Dim,
                bodyFont,TextAnchor.MiddleLeft,true);

            if (cascadeArchiveValues) DrawExactCascadeArchiveValues(entry);
            else if (cascadeArchivePins) DrawCascadeArchivePins(finding);
            else DrawCascadeArchiveFinding(entry);

            if (Button(Box(82,721,247,42),cascadeArchiveValues ? "Finding summary" : "Inspect archive values",
                enabled:entry.trend?.points is { Length: > 0 },size:19,icon:"Chart"))
            {
                cascadeArchiveValues = !cascadeArchiveValues;
                cascadeArchivePins = false;
            }
            if (Button(Box(342,721,233,42),cascadeArchivePins ? "Finding summary" : "Record provenance",size:19))
            {
                cascadeArchivePins = !cascadeArchivePins;
                cascadeArchiveValues = false;
            }
            if (Button(Box(589,721,214,42),"Open data source",enabled:!string.IsNullOrEmpty(finding.url),size:19))
                Application.OpenURL(finding.url);
            bool filed = investigation?.origin is { } origin && MatchesFiledScope(origin,finding);
            bool canFile = entry.trend?.hasSlope == true && CanFile(entry.record) &&
                (!HasReviewedTest(entry.trend) || finding.hasPValue);
            if (Button(Box(1241,721,275,42),filed ? "Finding filed" : "File this finding",accent:!filed,
                enabled:!filed && canFile,size:19))
                FileOrigin(entry.record,entry.trend);
        }

        void DrawCascadeArchiveFinding(CascadeArchiveFinding entry)
        {
            var finding = entry.finding;
            string description = finding.hasRate
                ? $"Descriptive Theil–Sen  ·  {FindingRate(finding)}  ·  {Signed(finding.fittedChange,"0.###")} {finding.unit} fitted change"
                : "Descriptive rate unavailable";
            Text(Box(82,444,1436,39),description,23,White,strongFont);
            Text(Box(82,493,1436,34),finding.significance,23,finding.hasPValue ? Mint : White,strongFont);
            if (finding.hasPValue)
            {
                Text(Box(82,534,1436,34),TestSummary(finding),21,White);
                Text(Box(82,575,1436,36),finding.hypothesis + "  ·  Review " + finding.review,18,Dim);
                Text(Box(82,615,1436,66),finding.reviewRationale,18,Dim,bodyFont,TextAnchor.UpperLeft,true);
            }
            else
                Text(Box(82,536,1436,71),finding.reason,20,Dim,bodyFont,TextAnchor.UpperLeft,true);
            string context = entry.record.key == "soil"
                ? "This reanalysis wetness fraction has its own scale. The simulation reports bucket storage in millimeters where available."
                : "This finding describes archived values in the area and period shown above.";
            Text(Box(82,672,1436,34),context,18,Dim);
        }

        void DrawExactCascadeArchiveValues(CascadeArchiveFinding entry)
        {
            var mainArchivePage = archiveValuePage;
            archiveValuePage = cascadeArchiveValuePage;
            DrawArchiveTable(entry.record,entry.trend,454);
            cascadeArchiveValuePage = archiveValuePage;
            archiveValuePage = mainArchivePage;
            Text(Box(83,626,1433,48),$"{CascadeArchiveMode(entry.trend)}  ·  {entry.finding.product}  ·  {entry.finding.unit}",
                19,Dim,bodyFont,TextAnchor.MiddleLeft,true);
        }

        void DrawCascadeArchivePins(FiledFinding finding)
        {
            string provenance = $"{finding.product}\nVersion  {finding.version}\nDataset  {finding.datasetId ?? finding.id}\n" +
                $"Finding  {finding.id}\nEvidence fingerprint  {finding.fingerprint ?? "Unavailable"}\nRaw SHA-256  {finding.rawSha256 ?? "Unavailable"}";
            if (!string.IsNullOrEmpty(finding.reviewer)) provenance += "\nReviewer  " + finding.reviewer;
            Text(Box(82,444,1436,240),provenance,19,White,bodyFont,TextAnchor.UpperLeft,true);
        }

        static string CascadeArchiveMode(ArchiveTrend trend) => trend.mode switch
        {
            "annual" => "Annual",
            "anomaly" => "Monthly anomalies",
            "month" when DateTime.TryParseExact(trend.firstDate,"yyyy-MM",Invariant,
                System.Globalization.DateTimeStyles.None,out var first) => first.ToString("MMMM",Invariant),
            _ => "Calendar-month values"
        };

        static string CascadeCoverage(CascadeArchiveFinding entry)
        {
            var finding = entry.finding;
            var id = finding.id.Split(':');
            if (entry.trend == null || id.Length != 6 || !int.TryParse(id[2],out int start) ||
                !int.TryParse(id[3],out int end) || end < start) return $"{finding.count} values";
            int expected = (end-start+1) * (finding.mode == "anomaly" ? 12 : 1);
            string unit = finding.mode == "annual" ? "complete years" : "values";
            return $"{finding.count} / {expected} {unit} in {start}–{end}";
        }
    }
}
