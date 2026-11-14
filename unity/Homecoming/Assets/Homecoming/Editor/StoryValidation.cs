using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Homecoming.Editor
{
    public static class StoryValidation
    {
        const string ScenePath = "Assets/Scenes/Story.unity";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static string PreviewPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../docs/unity-previews"));

        [MenuItem("Homecoming/Validate and render story frames")]
        public static void RenderPreviews()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var director = Object.FindFirstObjectByType<StoryDirector>();
            var stage = director.stage;
            var story = JsonUtility.FromJson<StoryScript>(director.script.text);
            Require(story.prologue.Length == 11 && story.briefing.Length == 1 && story.ending.Length == 2, "All 14 source shots exist");
            Require(story.prologue.Select(s => s.id).Distinct().Count() == 11, "Unique prologue shots");
            foreach (var shot in story.prologue.Concat(story.briefing).Concat(story.ending))
            {
                Require(shot.seconds > 0 && !string.IsNullOrWhiteSpace(shot.description), "Valid shot " + shot.id);
                if (shot.cues != null) foreach (var cue in shot.cues) Require(cue.at >= 0 && cue.at < shot.seconds, "Cue timing " + shot.id);
            }
            foreach (var actor in stage.crew)
            {
                Require(actor.rig != null && actor.clips.Length >= 3, "Original rig and clips " + actor.name);
                Require(actor.rig.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0, "Skinned character " + actor.name);
                actor.Initialize();
            }
            Require(EditorBuildSettings.scenes.Count(s => s.enabled) == 1 && EditorBuildSettings.scenes[0].path == ScenePath, "Story only in build");
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                foreach (var material in renderer.sharedMaterials) Require(material != null && material.shader != null && material.shader.isSupported, "Material " + renderer.name);

            Directory.CreateDirectory(PreviewPath);
            var original = stage.televisionMaterial;
            stage.televisionMaterial = new Material(original);
            stage.screen.sharedMaterial = stage.televisionMaterial;
            typeof(StoryStage).GetField("poster", Private).SetValue(stage, Resources.Load<Texture2D>("Film/apollo11"));
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            stage.filmCamera.targetTexture = rt;
            string[] names = { "home", "signal", "apollo", "reveal", "correction", "party", "jam", "years", "return", "earth", "launch", "lab", "hearing", "deflate" };
            float[] times = { 1, 4.3f, 2, 3.2f, 1.4f, 1.55f, .7f, 2, 2.8f, 3, .78f, 4, 3.2f, 2.6f };
            for (int i = 0; i < names.Length; i++)
            {
                stage.Evaluate(names[i], times[i], false, true);
                stage.filmCamera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(PreviewPath, names[i] + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
                RenderTexture.active = previous;
            }
            stage.filmCamera.targetTexture = null;
            rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(stage.televisionMaterial);
            stage.televisionMaterial = original;
            stage.screen.sharedMaterial = original;
            EditorSceneManager.OpenScene(ScenePath); // Discard sampled poses and preview state.
            Debug.Log("HOMECOMING_VALIDATION_OK: source shots, cues, 3 skinned rigs, materials, build isolation; rendered 14 frames to " + PreviewPath);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Homecoming validation failed: " + message);
        }

        // Optional local player for visual QA. Build output is excluded from version control.
        [MenuItem("Homecoming/Build macOS preview")]
        public static void BuildMacPreview()
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/The Great Homecoming.app"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneOSX, options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Player build failed: " + report.summary.result);
            Debug.Log("HOMECOMING_PLAYER_OK: " + output);
        }

        [MenuItem("Homecoming/Run playback smoke check")]
        public static void Smoke()
        {
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool("HomecomingSmoke", true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void InitializeSmoke()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.isPlaying && !EditorApplication.isCompiling &&
                    SessionState.GetBool("HomecomingSmoke", false) && Time.realtimeSinceStartup > 1)
                    SmokeInPlayMode();
            };
        }
        static void SmokeInPlayMode()
        {
            SessionState.SetBool("HomecomingSmoke", false);
            int result = 0;
            bool waitingForReturn = false;
            string recruit = StorySession.RecruitName;
            bool briefing = StorySession.BriefingComplete, ending = StorySession.EndingComplete;
            try
            {
                var director = Object.FindFirstObjectByType<StoryDirector>();
                var stage = director.stage;
                var story = JsonUtility.FromJson<StoryScript>(director.script.text);
                void Invoke(string method) => typeof(StoryDirector).GetMethod(method, Private).Invoke(director, null);
                string Page() => typeof(StoryDirector).GetField("page", Private).GetValue(director).ToString();
                foreach (var chapter in new[] { "prologue", "briefing", "ending" })
                {
                    director.Play(chapter, true);
                    var shots = chapter == "prologue" ? story.prologue : chapter == "briefing" ? story.briefing : story.ending;
                    for (int i = 0; i < shots.Length; i++)
                    {
                        director.SetShot(i);
                        Require(director.CurrentShot == shots[i].id, "Seek " + shots[i].id);
                        foreach (float time in new[] { 0, shots[i].seconds / 2, shots[i].seconds - .01f })
                            stage.Evaluate(shots[i].id, time, false, true);
                        stage.Evaluate(shots[i].id, shots[i].seconds / 2, true, true);
                    }
                    Invoke("Pause");
                    Require((bool)typeof(StoryDirector).GetField("paused", Private).GetValue(director), "Pause");
                    Invoke("Pause");
                    Invoke("FinishSequence");
                    Require(Page() == (chapter == "prologue" ? "Recruit" : chapter == "briefing" ? "Ready" : "Epilogue"), "Chapter route " + chapter);
                }
                Require(StorySession.EndingComplete == ending && StorySession.BriefingComplete == briefing, "Preview does not change progress");
                StorySession.RecruitName = "QA Cadet";
                director.Play("briefing"); Invoke("FinishSequence");
                Require(StorySession.BriefingComplete && StorySession.RecruitName == "QA Cadet", "Recruit saved through briefing");
                director.Play("ending"); Invoke("FinishSequence");
                Require(StorySession.EndingComplete, "Completion saved");
                Require(!Application.CanStreamedLevelBeLoaded(StorySession.SimulationScene), "No simulation shipped");
                // Verify the real reload, including Awake consuming the pending-hearing request.
                UnityEngine.Events.UnityAction<Scene, LoadSceneMode> loaded = null;
                loaded = (scene, mode) =>
                {
                    SceneManager.sceneLoaded -= loaded;
                    int reloadResult = 0;
                    try
                    {
                        var returned = Object.FindFirstObjectByType<StoryDirector>();
                        Require(returned != null && returned.CurrentShot == "hearing", "Return API starts final hearing after scene reload");
                        Require(!StorySession.ConsumeHearingRequest(), "Return request consumed once");
                        Debug.Log("HOMECOMING_SMOKE_OK: all 14 shots, motion options, pause, chapter routes, save, preview isolation, actual simulation-return scene reload.");
                    }
                    catch (Exception error) { reloadResult = 1; Debug.LogException(error); }
                    FinishSmoke(reloadResult, recruit, briefing, ending);
                };
                SceneManager.sceneLoaded += loaded;
                StorySession.ReturnFromSimulation();
                waitingForReturn = true;
            }
            catch (Exception error) { result = 1; Debug.LogException(error); }
            finally
            {
                if (!waitingForReturn) FinishSmoke(result, recruit, briefing, ending);
            }
        }
        static void FinishSmoke(int result, string recruit, bool briefing, bool ending)
        {
            StorySession.RecruitName = recruit;
            StorySession.BriefingComplete = briefing;
            StorySession.EndingComplete = ending;
            if (Application.isBatchMode) EditorApplication.Exit(result);
            else EditorApplication.ExitPlaymode();
        }
    }
}
