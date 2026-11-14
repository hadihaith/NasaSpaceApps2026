using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming
{
    [RequireComponent(typeof(StorySound))]
    public sealed partial class StoryDirector : MonoBehaviour
    {
        public StoryStage stage;
        public TextAsset script;
        public string simulationScene = StorySession.SimulationScene;
        enum Page { Home, Film, Recruit, Ready, Epilogue, Chapters, Credits, Reset }
        Page page;
        StoryScript story;
        CutsceneScript cutsceneData;
        // Loaded on first use so editor tools that call Play() before Awake() still work.
        CutsceneScript cutscenes
        {
            get
            {
                if (cutsceneData != null) return cutsceneData;
                var text = Resources.Load<TextAsset>("Cutscenes");
                cutsceneData = text != null ? JsonUtility.FromJson<CutsceneScript>(text.text) : new CutsceneScript();
                return cutsceneData;
            }
        }
        StoryShot[] sequence;
        string sequenceName, designation = "";
        int shotIndex, cueIndex;
        float shotTime, pageTime;
        bool paused, preview, captions = true, descriptions, reducedMotion;
        StorySound sound;
        float Scale => Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        public string CurrentShot => page == Page.Film ? sequence[shotIndex].id : page.ToString().ToLowerInvariant();
        public float ShotTime => shotTime;

        void Awake()
        {
            story = JsonUtility.FromJson<StoryScript>(script.text);
            sound = GetComponent<StorySound>();
            stage.Initialize();
            designation = StorySession.RecruitName;
            captions = PlayerPrefs.GetInt("homecoming.unity.captions", 1) == 1;
            descriptions = PlayerPrefs.GetInt("homecoming.unity.descriptions", 0) == 1;
            reducedMotion = PlayerPrefs.GetInt("homecoming.unity.reduced", 0) == 1;
            Application.targetFrameRate = 60;
            var cutscene = StorySession.ConsumeCutsceneRequest();
            if (cutscene != null && cutscenes.Find(cutscene) != null) Play(cutscene, false);
            else if (StorySession.ConsumeHearingRequest()) Play("ending", false);
            else Show(Page.Home);
        }
        void Show(Page next)
        {
            page = next;
            ResetInterfaceFocus();
            pageTime = 0;
            paused = false;
            sound.SetPaused(false);
            sound.StopCue();
            stage.SetShot(CurrentShot);
            stage.Evaluate(CurrentShot, 0, reducedMotion, sound.Muted);
        }
        public void Play(string chapter, bool isPreview = false)
        {
            sequenceName = chapter;
            sequence = chapter == "ending" ? story.ending : chapter == "briefing" ? story.briefing :
                cutscenes.Find(chapter) ?? story.prologue;
            preview = isPreview;
            ResetInterfaceFocus();
            page = Page.Film;
            paused = false;
            sound.SetPaused(false);
            SetShot(0);
        }
        public void SetShot(int index)
        {
            shotIndex = Mathf.Clamp(index, 0, sequence.Length - 1);
            shotTime = 0;
            cueIndex = 0;
            sound.StopCue();
            stage.SetShot(CurrentShot);
            stage.SetPaused(paused);
            stage.Evaluate(CurrentShot, shotTime, reducedMotion, sound.Muted);
        }
        void FinishSequence()
        {
            if (cutscenes.Find(sequenceName) != null)
            {
                // A discovery cutscene: go back to the simulation when it requested the scene,
                // otherwise (preview from Chapters) back to the chapter list.
                if (!preview && Application.CanStreamedLevelBeLoaded(simulationScene)) SceneManager.LoadSceneAsync(simulationScene);
                else Show(Page.Chapters);
            }
            else if (sequenceName == "prologue") Show(Page.Recruit);
            else if (sequenceName == "briefing")
            {
                if (!preview) StorySession.BriefingComplete = true;
                Show(Page.Ready);
            }
            else
            {
                if (!preview) StorySession.EndingComplete = true;
                Show(Page.Epilogue);
            }
        }
        void Advance()
        {
            if (shotIndex + 1 < sequence.Length) SetShot(shotIndex + 1);
            else FinishSequence();
        }
        void Pause()
        {
            paused = !paused;
            sound.SetPaused(paused);
            stage.SetPaused(paused);
        }
        void Update()
        {
            if (page == Page.Film)
            {
                if (Input.GetKeyDown(KeyCode.Space)) Pause();
                if (Input.GetKeyDown(KeyCode.RightArrow)) Advance();
                if (Input.GetKeyDown(KeyCode.LeftArrow)) SetShot(Mathf.Max(0, shotIndex - 1));
                if (Input.GetKeyDown(KeyCode.Escape)) { if (!paused) Pause(); else Show(Page.Home); }
                if (Input.GetKeyDown(KeyCode.M)) sound.SetMuted(!sound.Muted);
                if (Input.GetKeyDown(KeyCode.C)) { captions = !captions; SaveOptions(); }
                if (page != Page.Film) return;
                if (!paused)
                {
                    shotTime += Time.unscaledDeltaTime;
                    var shot = sequence[shotIndex];
                    while (shot.cues != null && cueIndex < shot.cues.Length && shotTime >= shot.cues[cueIndex].at)
                        sound.Cue(shot.cues[cueIndex++].name);
                    if (shotTime >= shot.seconds) Advance();
                }
            }
            else
            {
                pageTime += Time.unscaledDeltaTime;
                if (Input.GetKeyDown(KeyCode.Escape) && page != Page.Home) Show(Page.Home);
            }
            UpdateInterface();
            stage.Evaluate(CurrentShot, page == Page.Film ? shotTime : pageTime, reducedMotion, sound.Muted);
        }
        void OnDestroy() => DisposeInterface();

        void OnApplicationFocus(bool focused)
        {
            if (!focused && page == Page.Film && !paused) Pause();
        }
        void SaveOptions()
        {
            PlayerPrefs.SetInt("homecoming.unity.captions", captions ? 1 : 0);
            PlayerPrefs.SetInt("homecoming.unity.descriptions", descriptions ? 1 : 0);
            PlayerPrefs.SetInt("homecoming.unity.reduced", reducedMotion ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
