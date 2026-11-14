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
        static void StartInvestigationValidation()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args,"--investigation-preview");
            if (flag < 0 || flag+1 >= args.Length) return;
            var field = FindAnyObjectByType<FieldSimulation>();
            if (field != null) field.StartCoroutine(field.ValidateInvestigation(args[flag+1]));
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
            FileComparison();
            CheckInvestigation(investigation.IsReady && investigation.difference?.hasPValue == true &&
                investigation.difference.ratePerDecade > 0 && investigation.difference.pValue > 0 &&
                investigation.other.place == "Central Chile","Paired findings or review were not filed");
            CloseComparison();
            reportDetails = true;
            yield return new WaitForSecondsRealtime(.5f);
            yield return CaptureInvestigation(output,"07-historical-report");
            yield return new WaitForSecondsRealtime(.4f);

            SceneManager.sceneLoaded += FinishInvestigationValidation;
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

        static void FinishInvestigationValidation(Scene scene,LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= FinishInvestigationValidation;
            CheckInvestigation(scene.name == StorySession.StoryScene && FindAnyObjectByType<StoryDirector>()?.CurrentShot == "hearing",
                "The report did not open the hearing");
            Debug.Log("INVESTIGATION_PLAYBACK_OK: two advances, inherited state, February scrubbing, wrong prediction, unresolved monthly view, comparison flight, reviewed paired finding, report export and hearing.");
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
#endif
    }
}
