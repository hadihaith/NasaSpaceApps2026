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
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnableFromCommandLine()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--homecoming-capture") < 0) return;
            Application.runInBackground = true;
            Debug.Log("Homecoming internal frame capture starting.");
            new GameObject("Development frame capture").AddComponent<StoryCapture>();
        }
        IEnumerator Start()
        {
            var director = FindFirstObjectByType<StoryDirector>();
            var sound = director.GetComponent<StorySound>();
            bool muted = sound.Muted;
            sound.SetMuted(true);
            string output = Path.Combine(Path.GetTempPath(), "homecoming-captures");
            Directory.CreateDirectory(output);
            void Show(string name)
            {
                var field = typeof(StoryDirector).GetField("page", Private);
                typeof(StoryDirector).GetMethod("Show", Private).Invoke(director, new[] { Enum.Parse(field.FieldType, name) });
            }
            IEnumerator Capture(string name)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
                yield return new WaitForSecondsRealtime(.18f);
            }
            yield return new WaitForSecondsRealtime(1);
            Show("Home"); yield return Capture("01-title");
            director.Play("prologue", true);
            director.SetShot(5);
            typeof(StoryDirector).GetField("shotTime", Private).SetValue(director, 1.5f);
            director.stage.Evaluate("party", 1.5f, false, true);
            yield return Capture("02-party");
            director.SetShot(3);
            typeof(StoryDirector).GetField("shotTime", Private).SetValue(director, 2);
            director.stage.Evaluate("correction", 2, false, true);
            yield return Capture("03-dialogue");
            Show("Recruit"); yield return Capture("04-recruitment");
            Show("Ready"); yield return Capture("05-handoff");
            director.Play("ending", true);
            typeof(StoryDirector).GetField("shotTime", Private).SetValue(director, 3.2f);
            director.stage.Evaluate("hearing", 3.2f, false, true);
            yield return Capture("06-hearing");
            Show("Epilogue"); yield return Capture("07-epilogue");
            Show("Chapters"); yield return Capture("08-options");
            Show("Credits"); yield return Capture("09-credits");
            sound.SetMuted(muted);
            Debug.Log("HOMECOMING_CAPTURE_OK: " + output);
            Application.Quit(0);
        }
    }
}
#endif
