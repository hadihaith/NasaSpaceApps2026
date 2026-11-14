using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming
{
    [Serializable] public sealed class StoryCue { public float at; public string name; }
    // One timed caption: "Speaker: words", shown from `at` seconds until the next line or the end of the shot.
    [Serializable] public sealed class StoryLine { public float at; public string text; }
    [Serializable] public sealed class StoryShot
    {
        public string id, description, label, line;
        public float seconds;
        public StoryCue[] cues;
        public StoryLine[] lines;

        // The caption on screen at time t. Timed lines win; a single `line` shows for the whole shot.
        public string LineAt(float t, out float since)
        {
            since = t;
            if (lines == null || lines.Length == 0) return line;
            string current = null;
            foreach (var entry in lines)
            {
                if (entry.at > t) break;
                current = entry.text;
                since = t - entry.at;
            }
            return current;
        }
    }
    [Serializable] public sealed class StoryScript
    {
        public StoryShot[] prologue, briefing, ending;
    }
    // Discovery cutscenes live in Resources/Cutscenes.json, separate from Story.json, because
    // scripts/prepare-unity-story.py regenerates Story.json from the web story.
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
        // Process-local: returning from a future simulation does not invent saved evidence.
        static bool pendingHearing;
        public static void ReturnFromSimulation()
        {
            if (!Application.CanStreamedLevelBeLoaded(StoryScene))
                throw new InvalidOperationException("Add Story.unity to the active Build Profile scene list.");
            pendingHearing = true;
            SceneManager.LoadSceneAsync(StoryScene);
        }
        public static bool ConsumeHearingRequest()
        {
            var pending = pendingHearing;
            pendingHearing = false;
            return pending;
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
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Prefix + "recruit");
            PlayerPrefs.DeleteKey(Prefix + "briefing");
            PlayerPrefs.DeleteKey(Prefix + "ending");
            pendingHearing = false;
            pendingCutscene = null;
            PlayerPrefs.Save();
        }
    }
}
