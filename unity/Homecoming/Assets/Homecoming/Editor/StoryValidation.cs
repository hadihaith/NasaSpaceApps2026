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
            var director = Object.FindAnyObjectByType<StoryDirector>();
            var stage = director.stage;
            stage.ApplyTypography();
            var story = JsonUtility.FromJson<StoryScript>(director.script.text);
            Require(story.prologue.Length == 11 && story.briefing.Length == 1 && story.ending.Length == 2, "All 14 source shots exist");
            Require(story.prologue.Select(s => s.id).Distinct().Count() == 11, "Unique prologue shots");
            foreach (var shot in story.prologue.Concat(story.briefing).Concat(story.ending))
            {
                Require(shot.seconds > 0 && !string.IsNullOrWhiteSpace(shot.description), "Valid shot " + shot.id);
                if (shot.cues != null) foreach (var cue in shot.cues) Require(cue.at >= 0 && cue.at < shot.seconds, "Cue timing " + shot.id);
                if (shot.lines != null)
                {
                    float previous = -1;
                    foreach (var line in shot.lines)
                    {
                        Require(line.at > previous && line.at < shot.seconds - .8f, "Line timing and reading time " + shot.id);
                        Require(!string.IsNullOrWhiteSpace(line.text) && line.text.IndexOf(':') > 0, "Speaker: line format " + shot.id);
                        previous = line.at;
                    }
                }
            }
            foreach (var actor in stage.crew)
            {
                Require(actor.rig != null && actor.clips.Length >= 3, "Original rig and clips " + actor.name);
                Require(actor.rig.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0, "Skinned character " + actor.name);
                actor.Initialize();
                // Seek through unrelated poses, then return: no cumulative bone or face offsets.
                actor.Pose("party", 1.35f, false);
                var bones = actor.rig.GetComponentsInChildren<Transform>();
                var positions = bones.Select(b => b.localPosition).ToArray();
                var rotations = bones.Select(b => b.localRotation).ToArray();
                actor.Pose("return", 3, false);
                actor.Pose("launch", .75f, false);
                actor.Pose("party", 1.35f, false);
                for (int i = 0; i < bones.Length; i++)
                {
                    Require(Vector3.Distance(bones[i].localPosition, positions[i]) < .0001f, "Deterministic position " + actor.name + "/" + bones[i].name);
                    Require(Quaternion.Angle(bones[i].localRotation, rotations[i]) < .05f, "Deterministic rotation " + actor.name + "/" + bones[i].name);
                }
            }
            Require(Resources.Load<Font>("Fonts/Display") != null && Resources.Load<Font>("Fonts/Body") != null && Resources.Load<Font>("Fonts/Mono") != null, "Custom typography bundled");
            stage.crew[0].Pose("launch", .74f, false);
            var pressHand = stage.crew[0].rig.GetComponentsInChildren<Transform>().First(t => t.name == "hand.L");
            var pressTarget = stage.switchProp.transform.TransformPoint(new Vector3(0, .28f, 0));
            Require(Vector3.Distance(pressHand.position, pressTarget) < .12f, "Wrist reaches the physical switch");
            Require(EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled)?.path == ScenePath, "Story starts the regular build");
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                foreach (var material in renderer.sharedMaterials) Require(material != null && material.shader != null && material.shader.isSupported, "Material " + renderer.name);

            string overridePath = Environment.GetEnvironmentVariable("HOMECOMING_PREVIEW_PATH");
            string outputPath = string.IsNullOrEmpty(overridePath) ? PreviewPath : overridePath;
            Directory.CreateDirectory(outputPath);
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
                File.WriteAllBytes(Path.Combine(outputPath, names[i] + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
                RenderTexture.active = previous;
            }
            stage.filmCamera.targetTexture = null;
            rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(stage.televisionMaterial);
            stage.televisionMaterial = original;
            stage.screen.sharedMaterial = original;
            stage.Lighting.Teardown(); // Runtime-only lights are never left in the editor.
            EditorSceneManager.OpenScene(ScenePath); // Discard sampled poses and preview state.
            Debug.Log("HOMECOMING_VALIDATION_OK: source shots, cues, 3 skinned rigs, deterministic acting, custom fonts, materials, story first in build; rendered 14 frames to " + outputPath);
        }

        // Renders one shot as a dense frame sequence to inspect motion problems frame by frame.
        // Batch: -executeMethod Homecoming.Editor.StoryValidation.RenderShotFrames with
        // HOMECOMING_SHOT (e.g. "return"), HOMECOMING_FROM / HOMECOMING_TO (seconds),
        // HOMECOMING_STEP (seconds) and HOMECOMING_PREVIEW_PATH (output folder).
        [MenuItem("Homecoming/Render frames of one shot")]
        public static void RenderShotFrames()
        {
            string Env(string name, string fallback) => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)) ? fallback : Environment.GetEnvironmentVariable(name);
            string shot = Env("HOMECOMING_SHOT", "return");
            float from = float.Parse(Env("HOMECOMING_FROM", "0"), System.Globalization.CultureInfo.InvariantCulture);
            float to = float.Parse(Env("HOMECOMING_TO", "4.8"), System.Globalization.CultureInfo.InvariantCulture);
            float step = float.Parse(Env("HOMECOMING_STEP", ".1"), System.Globalization.CultureInfo.InvariantCulture);
            string output = Env("HOMECOMING_PREVIEW_PATH", Path.Combine(PreviewPath, "frames-" + shot));
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(ScenePath);
            var stage = Object.FindAnyObjectByType<StoryDirector>().stage;
            stage.ApplyTypography();
            var original = stage.televisionMaterial;
            stage.televisionMaterial = new Material(original);
            stage.screen.sharedMaterial = stage.televisionMaterial;
            typeof(StoryStage).GetField("poster", Private).SetValue(stage, Resources.Load<Texture2D>("Film/apollo11"));
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            stage.filmCamera.targetTexture = rt;
            int index = 0;
            for (float t = from; t <= to + .0001f; t += step, index++)
            {
                stage.Evaluate(shot, t, false, true);
                stage.filmCamera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, shot + "-" + index.ToString("000") + "-" + t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s.png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
                RenderTexture.active = previous;
            }
            stage.filmCamera.targetTexture = null;
            rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(stage.televisionMaterial);
            stage.televisionMaterial = original;
            stage.screen.sharedMaterial = original;
            stage.Lighting.Teardown();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("HOMECOMING_FRAMES_OK: " + index + " frames of " + shot + " to " + output);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Homecoming validation failed: " + message);
        }

        [MenuItem("Homecoming/Build macOS player")]
        public static void BuildMacPreview()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Require(scenes.FirstOrDefault() == ScenePath &&
                scenes.Any(scene => Path.GetFileNameWithoutExtension(scene) == StorySession.SimulationScene),
                "Build starts in Story and includes the laboratory");
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/The Great Homecoming.app"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = output, target = BuildTarget.StandaloneOSX
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
                var director = Object.FindAnyObjectByType<StoryDirector>();
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
                Require(Application.CanStreamedLevelBeLoaded(StorySession.SimulationScene), "Simulation is available at the lab handoff");
                // Verify the real reload, including Awake consuming the pending-hearing request.
                UnityEngine.Events.UnityAction<Scene, LoadSceneMode> loaded = null;
                loaded = (scene, mode) =>
                {
                    SceneManager.sceneLoaded -= loaded;
                    int reloadResult = 0;
                    try
                    {
                        var returned = Object.FindAnyObjectByType<StoryDirector>();
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
