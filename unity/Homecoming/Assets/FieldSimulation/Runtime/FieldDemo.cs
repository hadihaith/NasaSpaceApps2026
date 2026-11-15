using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        bool demoActive, investigationTest;
        int demoStage;
        FieldCampaign previousCampaign;
        Investigation previousInvestigation;
        FieldSave previousJournal;
        int previousSite;

        void WatchCase(InvestigationKind kind)
        {
            if (demoActive) return;
            previousCampaign = Campaign;
            previousInvestigation = investigation;
            previousJournal = save;
            previousSite = siteIndex;
            demoActive = true;
            demoStage = 0;
            if (kind == InvestigationKind.Soil)
            {
                campaign = FieldCampaign.Open(data, selectedSiteId: GuidedSoilOrigin);
                save = campaign.Journal;
            }
            NewInvestigation(kind);
            StartCoroutine(PlayCase());
        }

        IEnumerator PlayCase()
        {
            yield return null;
            if (investigation.kind == InvestigationKind.Rainfall)
            {
                investigation.originCall = Direction.Rise;
                investigation.otherCall = Direction.Fall;
                if (!StartComparison()) yield break;
                while (demoActive && orbitOpen && orbitProgress < 1) yield return null;
                if (!demoActive || !orbitOpen) yield break;
                FileComparison();
                demoStage = 2;
                yield break;
            }

            FileOrigin(ArchiveRain(data,GuidedSoilOrigin));
            investigation.scenarioCall = Direction.Rise;
            int index = Array.FindIndex(data.regions, region => region.id == GuidedSoilOrigin);
            Select(index);
            Go(View.Lab);
            for (demoStage = 0; demoStage < 2; demoStage++)
            {
                if (demoStage == 0) settings.rain = 100;
                else settings.heat = 4;
                yield return new WaitForSecondsRealtime(1.4f);
                if (!demoActive || view != View.Lab) yield break;
                LaunchScenario();
                yield return null;
                while (demoActive && (view == View.Descent || (view == View.Field && running))) yield return null;
                if (!demoActive || !HasPendingAdvance) yield break;
                CommitScenario();
                if (demoStage == 1)
                {
                    SeekPreview(1);
                    CompareBaseline();
                    yield break;
                }
                yield return new WaitForSecondsRealtime(2.0f);
                if (!demoActive || view != View.Field) yield break;
                Go(View.Lab);
            }
        }

        void DrawDemoControls()
        {
            Card(Box(33,96,415,42),Alpha(Ink,.94f));
            string label = investigation.kind == InvestigationKind.Rainfall ? "Rainfall comparison demo" :
                demoStage == 0 ? "First test · +100% precipitation" : "Follow-up · add 4 °C";
            Text(Box(46,96,278,42),label,19,Mint,strongFont);
            if (Button(Box(327,100,109,34),"Exit demo",false,false,true,17))
            {
                demoActive = false;
                campaign = previousCampaign;
                save = previousJournal;
                investigation = previousInvestigation;
                investigationTest = false;
                Select(previousSite);
                OpenCase();
            }
        }

#if HOMECOMING_FIELD_PREVIEW || UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartSiteTransferValidation()
        {
            var args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args,"--site-transfer-preview");
            if (flag < 0 || flag+1 >= args.Length) return;
            var field = FindAnyObjectByType<FieldSimulation>();
            if (field != null) field.StartCoroutine(field.ValidateSiteTransfers(args[flag+1]));
        }

        IEnumerator ValidateSiteTransfers(string output)
        {
            investigationTest = caseLoaded = capture = muted = true;
            reduced = false;
            Application.runInBackground = true;
            Directory.CreateDirectory(output);
            yield return null;
            Select(0);
            var camera = world.fieldCamera;
            var start = camera.transform.position;
            var startRotation = camera.transform.rotation;
            var revision = Campaign.Current.id;
            var branch = Campaign.ActiveBranchId;
            var startSite = Campaign.SelectedSiteId;
            settings.heat = 3;
            CheckInvestigation(!BeginSiteTransfer(0) && BeginSiteTransfer(3) && !BeginSiteTransfer(4),
                "The transfer did not reject the current site or overlapping input");
            TickSiteTransfer(0);
            CheckInvestigation(Vector3.Distance(camera.transform.position,start) < .001f &&
                Quaternion.Angle(camera.transform.rotation,startRotation) < .01f && !world.globe.activeSelf,
                "Departure did not start at the current terrain camera pose");
            TickSiteTransfer(OrbitExitEnd+.3f);
            CheckInvestigation(transferTerrainReady && !world.globe.activeSelf && Campaign.SelectedSiteId == startSite,
                "The destination preview changed the committed site before arrival");
            FinishSiteTransfer(false);
            CheckInvestigation(view == View.Lab && Campaign.SelectedSiteId == startSite && settings.heat == 3 &&
                Campaign.Current.id == revision && Vector3.Distance(camera.transform.position,start) < .001f,
                "Cancel did not restore the originating terrain, draft and camera");

            CheckInvestigation(BeginSiteTransfer(3),"The cinematic transfer could not start");
            yield return new WaitUntil(() => transferTime >= .9f);
            yield return CaptureInvestigation(output,"01-terrain-pullback");
            yield return new WaitUntil(() => transferTime >= 3.65f);
            CheckInvestigation(world.globe.activeSelf && world.stars.activeSelf && transferVeil == 0 &&
                Campaign.SelectedSiteId == startSite,"Orbital travel did not reveal the clear globe or committed early");
            yield return CaptureInvestigation(output,"02-globe-transit");
            yield return new WaitUntil(() => transferTime >= 6.7f);
            CheckInvestigation(transferTerrainReady && !world.globe.activeSelf,"Terrain was not visible on final approach");
            yield return CaptureInvestigation(output,"03-destination-descent");
            yield return new WaitUntil(() => view == View.Lab);
            CheckInvestigation(Campaign.SelectedSiteId == data.regions[3].id && settings.heat == 0,
                "Arrival did not select the destination or restore its own inherited weather");
            yield return CaptureInvestigation(output,"04-destination-lab");

            for (int from = 0; from < data.regions.Length; from++)
            for (int to = 0; to < data.regions.Length; to++)
            {
                if (from == to) continue;
                Select(from);
                CheckInvestigation(BeginSiteTransfer(to),"Could not begin a preset transfer");
                TickSiteTransfer(OrbitEntryEnd);
                var origin = new Vector2(Region.latitude,Region.longitude);
                var target = data.regions[to];
                var destination = new Vector2(target.latitude,target.longitude);
                bool Centered(Vector2 point) => world.ProjectFootprint(point,point,0,out var projected) &&
                    Vector2.Distance(new Vector2(projected.x,projected.y),new Vector2(Screen.width,Screen.height)*.5f) < 2;
                CheckInvestigation(Centered(origin),"The origin was not centered at orbital entry");
                TickSiteTransfer(OrbitTravelEnd-OrbitEntryEnd+.01f);
                CheckInvestigation(Centered(destination),"The destination was not centered at orbital exit");
                FinishSiteTransfer(true);
                CheckInvestigation(Campaign.SelectedSiteId == target.id && Campaign.Current.id == revision && Campaign.ActiveBranchId == branch,
                    "Skipping a preset transfer changed campaign history or selected the wrong site");
            }
            reduced = true;
            CheckInvestigation(BeginSiteTransfer(0) && view == View.Lab && Campaign.SelectedSiteId == startSite &&
                Campaign.Current.id == revision && Campaign.RunIds.Length == 0 && !world.globe.activeSelf,
                "Reduced motion did not select the site immediately without committing a scenario");
            Debug.Log("SITE_TRANSFER_OK: cinematic frames, all 30 preset routes, geographic centering, camera continuity, cancel, skip, reduced motion and campaign isolation.");
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartInvestigationValidation()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args,"--investigation-preview");
            if (flag < 0 || flag+1 >= args.Length) return;
            var field = FindAnyObjectByType<FieldSimulation>();
            if (field != null)
            {
                field.StartCoroutine(field.ValidateInvestigation(args[flag+1]));
                return;
            }

            // A story-first QA build reaches the field through the normal recruitment and lab controls.
            void OnFieldLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != StorySession.SimulationScene) return;
                SceneManager.sceneLoaded -= OnFieldLoaded;
                var controller = FindAnyObjectByType<FieldSimulation>();
                CheckInvestigation(controller != null && StorySession.BriefingComplete,
                    "The story-first check did not enter the laboratory after the briefing");
                Debug.Log("INVESTIGATION_STORY_ENTRY_OK: reached the laboratory after the story briefing.");
                controller.StartCoroutine(controller.ValidateInvestigation(args[flag+1]));
            }
            SceneManager.sceneLoaded += OnFieldLoaded;
        }

        IEnumerator ValidateInvestigation(string output)
        {
            investigationTest = caseLoaded = true;
            capture = true;
            muted = true;
            reduced = false;
            Application.runInBackground = true;
            Directory.CreateDirectory(output);
            campaign = FieldCampaign.Open(data,selectedSiteId: GuidedSoilOrigin);
            save = Campaign.Journal;
            Select(Array.FindIndex(data.regions,r => r.id == GuidedSoilOrigin));
            OpenCase();
            NewInvestigation(InvestigationKind.Soil);
            FileOrigin(ArchiveRain(data,GuidedSoilOrigin));
            CheckInvestigation(investigation.origin?.hasRate == true && investigation.origin.footprint.Contains("32.5°N") &&
                investigation.origin.footprint.Contains("111°W") && !investigation.IsReady,"Missing historical record, area footprint or initial report gate");
            investigation.scenarioCall = Direction.Fall;
            Go(View.Lab);
            var beforeId = Campaign.Current.id;
            settings.rain = 100;
            yield return CaptureInvestigation(output,"00-weather-console-draft");
            LaunchScenario();
            CheckInvestigation(PendingPreview != null && Campaign.Current.id == beforeId,"Starting the preview changed the committed world");
            yield return new WaitForSecondsRealtime(.8f);
            yield return CaptureInvestigation(output,"01-orbital-descent");
            Arrive();
            SeekPreview(1);
            var scenario = ScenarioMonth(1);
            var reference = ReferenceMonth(1);
            CheckInvestigation(scenario.Date == reference.Date && scenario.weather.rain > reference.weather.rain,"February comparison is inconsistent");
            CompareBaseline();
            CheckInvestigation(displayed.weather.rain == reference.weather.rain,"The camera and chart used different February values");
            CompareBaseline();
            yield return new WaitForSecondsRealtime(.5f);
            yield return CaptureInvestigation(output,"02-february-scenario");
            Finish();
            CheckInvestigation(HasPendingAdvance && Campaign.Current.id == beforeId,"Finished playback did not leave the edited forecast pending");
            yield return CaptureInvestigation(output,"02b-scenario-pending");
            CommitScenario();
            CheckInvestigation(investigation.first?.id == Campaign.Current.id && investigation.first.parentId == beforeId &&
                investigation.first.edited.rain == 100 && investigation.first.result != investigation.scenarioCall,"First result or prediction outcome is missing");
            Go(View.Lab);
            CheckInvestigation(settings.rain == 100,"Follow-up lost inherited rain");
            var afterRain = Campaign.Current.id;
            settings.heat = 4;
            LaunchScenario();
            Arrive();
            Finish();
            CommitScenario();
            var second = investigation.second;
            CheckInvestigation(second?.parentId == afterRain && second.parentId == investigation.first.id && second.edited.heat == 4 &&
                second.edited.rain == 100 && Mathf.Approximately(second.startingStorage,investigation.first.endingStorage),"Follow-up lost the first run's state");
            CheckInvestigation(!FindPair(),"The Arizona area unexpectedly matched a point");
            FileGap("The archive has only one MERRA-2 area averaging this variable.");
            CheckInvestigation(!investigation.IsReady,"A wrong prediction had no explanation");
            investigation.conclusion = "Additional precipitation left more water in the model bucket at the end of year one.";
            CheckInvestigation(investigation.IsReady,"An explained wrong prediction could not complete the scenario case");
            var linked = BuildHomecomingReport().link;
            CheckInvestigation(linked?.runId == investigation.first.runId && linked.recordId == investigation.origin.datasetId &&
                linked.siteId == GuidedSoilOrigin && linked.scenario.Contains("Final modeled"),
                "The report did not link the saved rainfall scenario and the filed Earth record");
            savedCases[investigation.kind] = investigation;
            OpenCase();
            reportDetails = true;
            yield return new WaitForSecondsRealtime(.5f);
            yield return CaptureInvestigation(output,"03-scenario-report");

            NewInvestigation(InvestigationKind.Free);
            var sahel = ArchiveRain(data,GuidedRainOrigin);
            var january = Exported(sahel)?.monthFindings?[0];
            CheckInvestigation(january?.hasSlope == true && january.mode == "month","Calendar-month view was not exported");
            FileOrigin(sahel,january);
            CheckInvestigation(investigation.origin?.id == january.id && !FindPair(),"The free case filed the wrong view or matched it to annual data");
            FileGap("This view compares January values across years. The available cross-location analysis uses annual values.");
            CheckInvestigation(investigation.IsReady,"The explicitly unresolved free comparison could not complete");

            NewInvestigation(InvestigationKind.Temperature);
            CheckInvestigation(!RevealTemperature(),"Temperature evidence opened before a call");
            investigation.originCall = Direction.Fall;
            CheckInvestigation(RevealTemperature(),"The reviewed Phoenix record could not open");
            yield return CaptureInvestigation(output,"08-phoenix-temperature");
            investigation.revisedOriginCall = Direction.Rise;
            FileTemperature();
            var temperature = investigation.origin;
            CheckInvestigation(investigation.IsReady && investigation.revisedOriginCall == Direction.Rise &&
                temperature.count == 45 && temperature.window == "1981–2025" && temperature.unit == "°C" &&
                temperature.status == "significant" && temperature.hasOlsRate && temperature.hasInterval &&
                Mathf.Abs(temperature.ratePerDecade - .50877193f) < .0001f &&
                Mathf.Abs(temperature.olsRatePerDecade - .46767677f) < .0001f &&
                Mathf.Abs(temperature.intervalLow - .34259101f) < .0001f &&
                Mathf.Abs(temperature.intervalHigh - .59276253f) < .0001f &&
                Mathf.Abs(temperature.pValue - 2.14409205e-9f) < 1e-12f &&
                !string.IsNullOrEmpty(temperature.fingerprint) && !string.IsNullOrEmpty(temperature.hypothesis),
                "Filed Phoenix temperature evidence differs from the reviewed record");

            NewInvestigation(InvestigationKind.Rainfall);
            investigation.originCall = Direction.Rise;
            investigation.otherCall = Direction.Fall;
            CheckInvestigation(StartComparison(),"The rainfall comparison could not start");
            yield return new WaitForSecondsRealtime(1.6f);
            yield return CaptureInvestigation(output,"04-historical-flight");
            reduced = true;
            yield return new WaitForSecondsRealtime(.5f);
            CheckInvestigation(orbitProgress >= 1,"Reduced motion did not reach the same comparison");
            yield return CaptureInvestigation(output,"05-rainfall-comparison");
            differenceOpen = true;
            yield return CaptureInvestigation(output,"06-paired-difference");
            investigation.revisedOriginCall = Direction.LittleChange;
            FileComparison();
            var difference = investigation.difference;
            CheckInvestigation(investigation.IsReady && investigation.comparisonId == "rain-two-ways" &&
                investigation.originCall == Direction.Rise && investigation.revisedOriginCall == Direction.LittleChange &&
                investigation.origin.count == 35 && investigation.other.count == 35 && difference?.count == 35 &&
                investigation.origin.status == "not-established" && !investigation.origin.hasPValue &&
                investigation.other.status == "not-established" && !investigation.other.hasPValue &&
                investigation.origin.unit == "mm/month" && difference.unit == "mm/month" &&
                investigation.other.place == "Central Chile" &&
                Mathf.Abs(investigation.origin.ratePerDecade - 2.1333333f) < .001f &&
                Mathf.Abs(investigation.other.ratePerDecade + 18.8401515f) < .001f &&
                difference.status == "significant" && difference.hasPValue && difference.hasOlsRate && difference.hasInterval &&
                Mathf.Abs(difference.ratePerDecade - 24.6453125f) < .001f &&
                Mathf.Abs(difference.olsRatePerDecade - 22.8439846f) < .001f &&
                Mathf.Abs(difference.intervalLow - 16.3621244f) < .001f &&
                Mathf.Abs(difference.intervalHigh - 29.3258448f) < .001f &&
                Mathf.Abs(difference.pValue - 3.2300391e-8f) < 1e-11f,
                "Filed rainfall evidence or prediction history differs from the reviewed comparison");
            CloseComparison();
            reportDetails = true;
            yield return new WaitForSecondsRealtime(.5f);
            yield return CaptureInvestigation(output,"07-historical-report");
            yield return new WaitForSecondsRealtime(.4f);

            // Commit two more real model runs in the disposable QA campaign to verify all four slots.
            foreach (int site in new[] { 2, 4 })
            {
                var changes = new[] { new ForcingEdit(ForcingControl.Rain, 35, Campaign.Current.nextMonth) };
                Campaign.Advance(Campaign.Preview(data.regions[site].id, changes));
            }
            RestoreJournal();
            var summary = BuildHomecomingReport();
            CheckInvestigation(summary.recentRuns.Length == 4 && summary.recentRuns[0].site == data.regions[4].name &&
                summary.recentRuns[1].site == data.regions[2].name && summary.recentRuns[3].edited.Length == 12 &&
                summary.link?.editedWeather?.Length == 12 && summary.link.archiveValues.Length > 1 &&
                summary.link.archiveValues.Length == summary.link.archiveTimes.Length,
                "The monitor does not have the four saved runs and exact historical series");

            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onHearing = null;
            onHearing = (scene, mode) =>
            {
                SceneManager.sceneLoaded -= onHearing;
                FinishInvestigationValidation(scene, output);
            };
            SceneManager.sceneLoaded += onHearing;
            OpenSubmission();
            yield return CaptureInvestigation(output, "08b-findings-submission");
            SubmitInvestigation(output);
            CheckInvestigation(hearingRequested && File.Exists(Path.Combine(output,investigation.id+".json")),"Report export or story submission failed");
        }

        static IEnumerator CaptureInvestigation(string output,string name)
        {
            yield return new WaitForEndOfFrame();
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(output,name+".png"),screenshot.EncodeToPNG()); }
            finally { Destroy(screenshot); }
        }

        static void CheckInvestigation(bool condition,string reason)
        {
            if (!condition) throw new InvalidOperationException("INVESTIGATION_VALIDATION_FAILED: "+reason);
        }

        static void FinishInvestigationValidation(Scene scene, string output)
        {
            var director = FindAnyObjectByType<StoryDirector>();
            CheckInvestigation(scene.name == StorySession.StoryScene && director?.CurrentShot == "hearing" &&
                StorySession.HearingEvidence == StoryEvidence.Mixed,
                "The report did not open the hearing");
            director.StartCoroutine(CaptureValidatedHearing(director, output));
        }

        static IEnumerator CaptureValidatedHearing(StoryDirector director, string output)
        {
            float deadline = Time.realtimeSinceStartup + 48;
            yield return new WaitUntil(() => director.ShotTime >= StoryStage.VictoryLaunch + .25f || Time.realtimeSinceStartup >= deadline);
            CheckInvestigation(director.CurrentShot == "hearing", "The party cannon cue did not play in the hearing");
            yield return CaptureInvestigation(output, "08c-victory-launch");
            yield return new WaitUntil(() => director.ShotTime >= 4.52f || Time.realtimeSinceStartup >= deadline);
            CheckInvestigation(director.CurrentShot == "hearing", "The hearing cut away before the victory fireworks formed");
            yield return CaptureInvestigation(output, "08d-victory-fireworks");
            yield return new WaitUntil(() => director.CurrentShot == "evidence" && director.ShotTime >= 1.3f || Time.realtimeSinceStartup >= deadline);
            CheckInvestigation(director.CurrentShot == "evidence", "The hearing skipped the monitor scene");
            yield return CaptureInvestigation(output, "09a-monitor-setup");
            yield return new WaitUntil(() => director.ShotTime >= 4.9f || Time.realtimeSinceStartup >= deadline);
            yield return CaptureInvestigation(output, "09-historical-hearing");
            yield return new WaitUntil(() => director.ShotTime >= 7.25f || Time.realtimeSinceStartup >= deadline);
            CheckInvestigation(director.stage.EvidenceMonitorVisible && !director.stage.report.activeSelf,
                "The evidence brief did not appear on the monitor alone");
            yield return CaptureInvestigation(output, "09b-linked-history-on-monitor");
            yield return new WaitUntil(() => director.ShotTime >= 10.85f || Time.realtimeSinceStartup >= deadline);
            CheckInvestigation(director.CurrentShot == "evidence" && director.ShotTime >= StoryStage.MonitorChangesPage,
                "The hearing did not hold on the four-run page");
            CheckInvestigation(director.stage.EvidenceMonitorVisible && !director.stage.report.activeSelf,
                "The four-run brief did not appear on the monitor alone");
            yield return CaptureInvestigation(output, "09c-four-saved-simulations");
            yield return new WaitUntil(() => director.CurrentShot == "epilogue" || Time.realtimeSinceStartup >= deadline);
            CheckInvestigation(director.CurrentShot == "epilogue" && StorySession.FiledReport is { experiments: 4, investigations: >= 2, link: not null, recentRuns: { Length: 4 } },
                "The personalized report did not become available after the story");
            yield return new WaitForSecondsRealtime(.6f);
            yield return CaptureInvestigation(output, "10-finale-status");
            director.ShowFiledReport();
            CheckInvestigation(director.CurrentShot == "report", "The personalized report did not open from the final status");
            yield return CaptureInvestigation(output, "11-homecoming-report");
            Debug.Log("INVESTIGATION_PLAYBACK_OK: scenario playback, reviewed Earth records, report export and the animated hearing with four committed simulation results.");
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
#endif
    }
}
