#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Homecoming
{
    // Development-player render capture; exports only this game's own framebuffer.
    // Inactive during ordinary play and excluded from release players.
    public sealed class StoryCapture : MonoBehaviour
    {
        public static bool Exporting { get; private set; }
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnableFromCommandLine()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--homecoming-capture") < 0) return;
            Application.runInBackground = true;
            Exporting = true;
            Debug.Log("Homecoming internal frame capture starting.");
            new GameObject("Development frame capture").AddComponent<StoryCapture>();
        }
        IEnumerator Start()
        {
            var director = FindAnyObjectByType<StoryDirector>();
            var sound = director.GetComponent<StorySound>();
            bool muted = sound.Muted;
            sound.SetMuted(true);
            string output = Path.Combine(Path.GetTempPath(), "homecoming-captures");
            Directory.CreateDirectory(output);
            void Show(string name)
            {
                var field = typeof(StoryDirector).GetField("page", Private);
                typeof(StoryDirector).GetMethod("Show", Private).Invoke(director, new[] { Enum.Parse(field.FieldType, name) });
                typeof(StoryDirector).GetField("pageTime", Private).SetValue(director, 1.3f);
            }
            IEnumerator Capture(string name)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
                yield return new WaitForSecondsRealtime(.18f);
            }
            var script = JsonUtility.FromJson<StoryScript>(director.script.text);
            void Frame(string id, float time)
            {
                var chapter = id == "hearing" || id == "deflate" ? "ending" : id == "lab" ? "briefing" : "prologue";
                var shots = chapter == "ending" ? script.ending : chapter == "briefing" ? script.briefing : script.prologue;
                director.Play(chapter, true);
                director.SetShot(Array.FindIndex(shots, shot => shot.id == id));
                typeof(StoryDirector).GetField("paused", Private).SetValue(director, true);
                typeof(StoryDirector).GetField("shotTime", Private).SetValue(director, time);
                director.stage.SetPaused(true);
                director.stage.Evaluate(id, time, false, true);
            }
            yield return new WaitForSecondsRealtime(1);
            Show("Home"); yield return Capture("01-title");
            Frame("opening", 2.4f);
            director.stage.FailIntro();
            yield return Capture("opening");
            Frame("party", 1.5f);
            yield return Capture("02-party");
            Frame("correction", 2);
            yield return Capture("03-dialogue");
            Show("Recruit"); yield return Capture("04-recruitment");
            Show("Ready"); yield return Capture("05-handoff");
            Frame("hearing", 3.2f);
            yield return Capture("06-hearing");
            Show("Epilogue"); yield return Capture("07-epilogue");
            Show("Chapters"); yield return Capture("08-options");
            Show("Credits"); yield return Capture("09-credits");
            foreach (var (name, time) in new[] { ("years", 4.2f), ("earth", 3.4f), ("lab", 3.2f), ("launch", .74f) })
            {
                Frame(name, time);
                yield return Capture("cinema-" + name);
            }
            // Contact frames inspect anticipation, contact and follow-through without playing by eye alone.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--homecoming-motion-frames") >= 0)
            {
                foreach (var name in new[] { "reveal", "correction", "party", "return", "launch", "hearing", "deflate" })
                {
                    string chapter = name == "hearing" || name == "deflate" ? "ending" : "prologue";
                    director.Play(chapter, true);
                    var shots = chapter == "ending" ? script.ending : script.prologue;
                    int index = Array.FindIndex(shots, shot => shot.id == name);
                    director.SetShot(index);
                    typeof(StoryDirector).GetField("paused", Private).SetValue(director, true);
                    director.stage.SetPaused(true);
                    for (int frame = 0; frame < 12; frame++)
                    {
                        float time = (frame + .5f) * shots[index].seconds / 12;
                        typeof(StoryDirector).GetField("shotTime", Private).SetValue(director, time);
                        director.stage.Evaluate(name, time, false, true);
                        yield return Capture("motion-" + name + "-" + frame.ToString("00"));
                    }
                }
            }
            sound.SetMuted(muted);
            Exporting = false;
            Debug.Log("HOMECOMING_CAPTURE_OK: " + output);
            Application.Quit(0);
        }
    }
}
#endif
