#if HOMECOMING_FIELD_PREVIEW || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        bool capture;

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "--field-preview");
            if (flag < 0 || flag + 1 >= args.Length) yield break;
            capture = true;
            bool video = Array.IndexOf(args, "--field-video") >= 0;
            Application.runInBackground = true;
            var path = args[flag + 1];
            Directory.CreateDirectory(path);
            save = new FieldSave();
            reduced = false;
            muted = true;
            Select(4);
            var sample = ClimateModel.Run(Region, ClimateModel.Initial(Region), new Forcing(0,150,40,0),12);
            if (!sample.findings.Contains("waterlogging") || !sample.findings.Contains("surplus") ||
                Mathf.Abs(sample.Final.land.soil - .94139945f) > .00001f ||
                Mathf.Abs(sample.Final.land.vegetation - .4695169f) > .00001f || CanSubmit)
                throw new InvalidOperationException("Wet example or initial report gate failed.");
            var dry = ClimateModel.Run(data.regions[0], ClimateModel.Initial(data.regions[0]), new Forcing(8,-100,-40,15),12);
            if (!dry.findings.SequenceEqual(new[] { "drought", "decline", "fire", "heat", "demand" }) ||
                Mathf.Abs(dry.Final.land.soil - .000112305f) > .00001f ||
                data.evidence.Any(e => e.years.Any(point => point.year < 1900)))
                throw new InvalidOperationException("Dry example or archive data failed.");

            if (video) Time.captureFramerate = 24;
            int frame = 0;
            float intro = 0, tail = 0;
            Action<string> still = name => ScreenCapture.CaptureScreenshot(Path.Combine(path,name+".png"));
            bool lab = false, orbit = false, approach = false, field = false, result = false;
            while (tail < 2.8f)
            {
                if (view == View.Lab)
                {
                    intro += Time.deltaTime;
                    if (intro >= 1.7f) Launch();
                }
                if (showResult) tail += Time.deltaTime;
                yield return new WaitForEndOfFrame();
                string moment = null;
                if (view == View.Lab && !lab) { moment = "01-lab"; lab = true; }
                if (view == View.Descent && descentClock >= .7f && !orbit) { moment = "02-orbit"; orbit = true; }
                if (view == View.Descent && descentClock >= 3.3f && !approach) { moment = "03-descent"; approach = true; }
                if (view == View.Field && runClock >= RunTime + 1.25f && !field) { moment = "04-field"; field = true; }
                if (showResult && !result) { moment = "05-waterlogging"; result = true; }
                if (video)
                {
                    string frameName = "frame-" + (frame++).ToString("00000");
                    still(moment == null ? frameName : frameName + "-" + moment);
                }
                else if (moment != null) still(moment);
                yield return null;
            }

            if (save.runs != 1 || save.notes.Count < 2 || CanSubmit || primary?.id != "waterlogging")
                throw new InvalidOperationException("Waterlogging playback or discovery recording failed.");
            Go(View.Journal);
            yield return new WaitForEndOfFrame();
            still("06-journal");
            yield return null;
            OpenArchive("soil");
            yield return new WaitForEndOfFrame();
            still("07-archive");
            yield return null;
            var evidence = data.evidence.First(e => e.region == Region.id && e.key == "soil");
            save.evidence.Add(evidence.id);
            Select(0);
            settings = new Forcing(8,-100,-40,15);
            Go(View.Lab);
            Launch();
            Arrive();
            displayed = trial.Final;
            Finish();
            if (!CanSubmit || save.runs != 2 || save.sites.Count != 2)
                throw new InvalidOperationException("Report gate or regional land persistence failed.");
            Go(View.Report);
            yield return new WaitForEndOfFrame();
            still("08-report");
            yield return null;
            Time.captureFramerate = 0;
            SceneManager.sceneLoaded += VerifyHearing;
            StorySession.ReturnFromSimulation();
        }

        static void VerifyHearing(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= VerifyHearing;
            if (scene.name != StorySession.StoryScene || FindAnyObjectByType<StoryDirector>()?.CurrentShot != "hearing")
                throw new InvalidOperationException("The report did not open the final hearing.");
            Debug.Log("FIELD_PLAYBACK_OK: landing, wet and dry runs, archive, regional state, report gate and Story handoff.");
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
    }
}
#endif
