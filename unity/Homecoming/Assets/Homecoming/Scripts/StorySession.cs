using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming
{
    [Serializable] public sealed class StoryCue { public float at; public string name; }
    public enum StoryEvidence { Scenario, Historical, Mixed }
    public enum SimulationHistory { None, Branches, LinkedRun, EarthRecord }
    [Serializable] public sealed class HomecomingReport
    {
        public string recruit;
        public int discoveries, totalDiscoveries, experiments, investigations;
        public HomecomingReportLink link;
        public HomecomingRunBrief[] recentRuns;
    }
    [Serializable] public sealed class HomecomingRunBrief
    {
        public string site, period, unit;
        public float[] edited, inherited;
    }
    [Serializable] public sealed class HomecomingReportLink
    {
        public string runId, siteId, recordId, findingId, site, variable, scenario, historical;
        public string simulationPeriod, simulationUnit, archivePeriod, archiveUnit;
        public float[] editedWeather, inheritedWeather, archiveValues, archiveTimes;
    }
    [Serializable] public sealed class StoryLine { public float at, end; public string id, text, variant; }
    [Serializable] public sealed class StoryShot
    {
        public string id, description, label, line;
        public float seconds;
        public StoryCue[] cues;
        public StoryLine[] lines;

        // The caption on screen at time t. Timed lines win; a single `line` shows for the whole shot.
        public string LineAt(float t, out float since, StoryEvidence evidence = StoryEvidence.Mixed)
        {
            var line = ActiveLine(t, evidence);
            since = line != null ? t - line.at : t;
            return line?.text ?? this.line;
        }

        public StoryLine ActiveLine(float time, StoryEvidence evidence)
        {
            StoryLine current = null;
            if (lines == null) return current;
            foreach (var entry in lines)
            {
                if (entry.at > time) break;
                // Discovery scenes without an end time keep a line until the next one.
                if (entry.end > entry.at && time >= entry.end) continue;
                if (entry.variant == "scenario" && evidence != StoryEvidence.Scenario ||
                    entry.variant == "historical" && evidence != StoryEvidence.Historical ||
                    entry.variant == "mixed" && evidence != StoryEvidence.Mixed) continue;
                current = entry;
            }
            return current;
        }
    }
    [Serializable] public sealed class StoryScript
    {
        public StoryShot[] prologue, briefing, ending;
    }
    // Discovery cutscenes live in Resources/Cutscenes.json alongside the shared film timeline.
    // To add a cutscene: add an array to the JSON, a field here and a case in Find().
    [Serializable] public sealed class CutsceneScript
    {
        public StoryShot[] drought;
        public StoryShot[] Find(string id) => id == "drought" ? drought : null;
        public static bool IsCutsceneShot(string shotId) => shotId != null && shotId.StartsWith("drought-");
    }

    // The only interface the future simulation needs. No simulation rules live here.
    public static class StorySession
    {
        const string Prefix = "homecoming.unity.";
        public const string StoryScene = "Story";
        public const string SimulationScene = "Simulation";
        public const string BroadcastProgressKey = Prefix + "broadcast.v1";
        public static string RecruitName
        {
            get => PlayerPrefs.GetString(Prefix + "recruit", "");
            set { PlayerPrefs.SetString(Prefix + "recruit", value); PlayerPrefs.Save(); }
        }
        public static bool BriefingComplete
        {
            get => PlayerPrefs.GetInt(Prefix + "briefing", 0) == 1;
            set { PlayerPrefs.SetInt(Prefix + "briefing", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool EndingComplete
        {
            get => PlayerPrefs.GetInt(Prefix + "ending", 0) == 1;
            set { PlayerPrefs.SetInt(Prefix + "ending", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        // Empty for operations saved before the finale update.
        public static string OperationId => PlayerPrefs.GetString(Prefix + "operation", "");
        static HomecomingReport filedReport;
        static bool reportLoaded;
        public static HomecomingReport FiledReport
        {
            get
            {
                if (!reportLoaded)
                {
                    filedReport = JsonUtility.FromJson<HomecomingReport>(PlayerPrefs.GetString(Prefix + "report", ""));
                    reportLoaded = true;
                }
                return filedReport;
            }
        }
        static HomecomingReport pendingReport;
        static SimulationHistory pendingHistory;
        static bool pendingFiledReport;
        // Process-local: returning from a future simulation does not invent saved evidence.
        static bool pendingHearing;
        public static StoryEvidence HearingEvidence { get; private set; } = StoryEvidence.Mixed;
        public static void ReturnFromSimulation(StoryEvidence evidence = StoryEvidence.Mixed, HomecomingReport report = null)
        {
            if (!Application.CanStreamedLevelBeLoaded(StoryScene))
                throw new InvalidOperationException("Add Story.unity to the active Build Profile scene list.");
            HearingEvidence = evidence;
            pendingReport = report;
            pendingHearing = true;
            SceneManager.LoadSceneAsync(StoryScene);
        }
        public static bool ConsumeHearingRequest()
        {
            var pending = pendingHearing;
            pendingHearing = false;
            return pending;
        }
        public static HomecomingReport ConsumeReport()
        {
            var report = pendingReport;
            pendingReport = null;
            return report;
        }
        public static void FileReport(HomecomingReport report)
        {
            if (report == null) return;
            PlayerPrefs.SetString(Prefix + "report", JsonUtility.ToJson(report));
            filedReport = report;
            reportLoaded = true;
        }
        public static void OpenSimulationHistory(string scene, SimulationHistory destination)
        {
            pendingHistory = destination;
            SceneManager.LoadSceneAsync(scene);
        }
        public static SimulationHistory ConsumeHistoryRequest()
        {
            var requested = pendingHistory;
            pendingHistory = SimulationHistory.None;
            return requested;
        }
        public static void ReturnToFiledReport()
        {
            pendingFiledReport = true;
            SceneManager.LoadSceneAsync(StoryScene);
        }
        public static bool ConsumeFiledReportRequest()
        {
            bool requested = pendingFiledReport;
            pendingFiledReport = false;
            return requested;
        }
        // For the future simulation: play a discovery cutscene (for example "drought"), then
        // return to the simulation scene. The simulation keeps its own state across the trip.
        static string pendingCutscene;
        public static void PlayCutscene(string id)
        {
            if (!Application.CanStreamedLevelBeLoaded(StoryScene))
                throw new InvalidOperationException("Add Story.unity to the active Build Profile scene list.");
            pendingCutscene = id;
            SceneManager.LoadSceneAsync(StoryScene);
        }
        public static string ConsumeCutsceneRequest()
        {
            var pending = pendingCutscene;
            pendingCutscene = null;
            return pending;
        }
        internal static void StartNewOperation()
        {
            Field.FieldCampaign.ClearSavedCampaign(Field.FieldCampaign.SavePath);
            PlayerPrefs.DeleteKey(Field.FieldSimulation.SaveKey);
            Field.FieldCaseStore.ClearSavedCases();
            Reset();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Prefix + "recruit");
            PlayerPrefs.DeleteKey(Prefix + "briefing");
            PlayerPrefs.DeleteKey(Prefix + "ending");
            PlayerPrefs.DeleteKey(Prefix + "report");
            filedReport = null;
            reportLoaded = true;
            PlayerPrefs.SetString(Prefix + "operation", Guid.NewGuid().ToString("N"));
            pendingHearing = false;
            pendingHistory = SimulationHistory.None;
            pendingFiledReport = false;
            pendingReport = null;
            PlayerPrefs.DeleteKey(BroadcastProgressKey);
            HearingEvidence = StoryEvidence.Mixed;
            pendingCutscene = null;
            PlayerPrefs.Save();
        }
    }
}
