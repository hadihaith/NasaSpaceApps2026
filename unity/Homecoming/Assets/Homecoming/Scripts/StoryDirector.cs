using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming
{
    [RequireComponent(typeof(StorySound))]
    public sealed class StoryDirector : MonoBehaviour
    {
        public StoryStage stage;
        public TextAsset script;
        public string simulationScene = StorySession.SimulationScene;
        enum Page { Home, Film, Recruit, Ready, Epilogue, Chapters, Credits, Reset }
        Page page;
        StoryScript story;
        StoryShot[] sequence;
        string sequenceName, designation = "";
        int shotIndex, cueIndex;
        float shotTime, pageTime;
        bool paused, preview, captions = true, descriptions, reducedMotion;
        StorySound sound;
        GUIStyle small, body, title, subtitle, button, quietButton, input, caption, speaker, number;
        readonly Color paper = new Color(.94f, .91f, .83f), ink = new Color(.035f, .055f, .065f);
        readonly Color rust = new Color(.86f, .49f, .29f), muted = new Color(.64f, .69f, .67f);
        bool stylesReady;
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
            if (StorySession.ConsumeHearingRequest()) Play("ending", false);
            else Show(Page.Home);
        }
        void Show(Page next)
        {
            page = next;
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
            sequence = chapter == "ending" ? story.ending : chapter == "briefing" ? story.briefing : story.prologue;
            preview = isPreview;
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
            if (sequenceName == "prologue") Show(Page.Recruit);
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
            stage.Evaluate(CurrentShot, page == Page.Film ? shotTime : pageTime, reducedMotion, sound.Muted);
        }
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
        void Styles()
        {
            if (stylesReady) return;
            GUIStyle Text(int size, Color color, FontStyle weight = FontStyle.Normal)
            {
                var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight, wordWrap = true, richText = false };
                s.normal.textColor = color;
                s.padding = new RectOffset(0, 0, 0, 0);
                return s;
            }
            small = Text(14, muted);
            body = Text(22, paper);
            title = Text(83, paper, FontStyle.Bold);
            subtitle = Text(34, paper, FontStyle.Bold);
            caption = Text(26, paper);
            caption.alignment = TextAnchor.MiddleCenter;
            speaker = Text(14, rust, FontStyle.Bold);
            speaker.alignment = TextAnchor.MiddleCenter;
            number = Text(108, paper, FontStyle.Bold);
            button = new GUIStyle(GUI.skin.button) { fontSize = 19, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(24, 24, 0, 0) };
            button.normal.background = Texture2D.whiteTexture;
            button.normal.textColor = ink;
            button.hover.background = Texture2D.whiteTexture;
            button.hover.textColor = Color.black;
            button.active.background = Texture2D.whiteTexture;
            button.active.textColor = Color.black;
            button.focused.background = Texture2D.whiteTexture;
            button.focused.textColor = ink;
            quietButton = new GUIStyle(button);
            quietButton.normal.background = null;
            quietButton.hover.background = null;
            quietButton.active.background = null;
            quietButton.focused.background = null;
            quietButton.normal.textColor = paper;
            quietButton.hover.textColor = rust;
            quietButton.active.textColor = rust;
            quietButton.focused.textColor = rust;
            input = new GUIStyle(GUI.skin.textField) { fontSize = 26, padding = new RectOffset(15, 15, 16, 10) };
            input.normal.background = Texture2D.whiteTexture;
            input.focused.background = Texture2D.whiteTexture;
            input.normal.textColor = ink;
            input.focused.textColor = ink;
            stylesReady = true;
        }
        void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
        void Text(float x, float y, float width, float height, string text, GUIStyle style) => GUI.Label(new Rect(x, y, width, height), text, style);
        bool Button(float x, float y, float width, string text, bool primary = false)
        {
            var color = GUI.backgroundColor;
            GUI.backgroundColor = paper;
            bool pressed = GUI.Button(new Rect(x, y, width, 56), text, primary ? button : quietButton);
            GUI.backgroundColor = color;
            if (!primary) Fill(new Rect(x + 24, y + 54, width - 48, 1), new Color(.6f, .65f, .64f, .35f));
            if (pressed) sound.Cue("click");
            return pressed;
        }
        void OnGUI()
        {
            Styles();
            float scale = Scale;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) / 2, (Screen.height - 900 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            if (page == Page.Film) FilmGUI();
            else
            {
                if (page == Page.Home) HomeGUI();
                else if (page == Page.Recruit) RecruitGUI();
                else if (page == Page.Ready) ReadyGUI();
                else if (page == Page.Epilogue) EpilogueGUI();
                else if (page == Page.Chapters) ChaptersGUI();
                else if (page == Page.Credits) CreditsGUI();
                else ResetGUI();
                if (page != Page.Credits) Footer();
            }
            GUI.matrix = oldMatrix;
        }
        void Header(string right)
        {
            Fill(new Rect(0, 0, 1600, 96), new Color(ink.r, ink.g, ink.b, .94f));
            Text(64, 32, 600, 30, "I / A / D    INTERGALACTIC AFFECTION DEPARTMENT", small);
            Text(1240, 32, 320, 30, right, small);
        }
        void Footer()
        {
            Fill(new Rect(0, 804, 1600, 96), new Color(ink.r, ink.g, ink.b, .96f));
            Text(64, 842, 420, 30, "NASA SPACE APPS  /  KUWAIT", small);
            if (Button(730, 823, 185, sound.Muted ? "Sound: off" : "Sound: on")) sound.SetMuted(!sound.Muted);
            if (Button(930, 823, 215, "Chapters / options")) Show(Page.Chapters);
            if (Button(1170, 823, 170, "Credits")) Show(Page.Credits);
            if (page != Page.Home && Button(1360, 823, 175, "Main menu")) Show(Page.Home);
        }
        void HomeGUI()
        {
            Fill(new Rect(0, 96, 650, 708), new Color(ink.r, ink.g, ink.b, .96f));
            Header("TRANSMISSION  001 / 1969");
            Fill(new Rect(66, 170, 50, 3), rust);
            Text(64, 194, 540, 25, "SIGNAL INTERCEPTED  /  JULY 1969", small);
            var landingTitle = new GUIStyle(title) { fontSize = 66, wordWrap = false };
            Text(60, 263, 575, 190, "THE GREAT\nHOMECOMING", landingTitle);
            Text(64, 480, 475, 95, "One human reached the Moon.\nThree aliens set the table.", body);
            if (Button(64, 615, 452, "Play transmission     →", true)) Play("prologue");
            if (StorySession.BriefingComplete)
            {
                if (Button(64, 688, 452, "Continue as " + StorySession.RecruitName)) Show(Page.Ready);
            }
            else if (Button(64, 688, 452, "Skip to recruitment")) Show(Page.Recruit);
            Text(1110, 735, 430, 28, "MISSION STATUS: 57 YEARS BEHIND", small);
        }
        void FilmGUI()
        {
            var shot = sequence[shotIndex];
            Fill(new Rect(0, 0, 1600, 72), Color.black);
            Fill(new Rect(0, 782, 1600, 118), Color.black);
            Text(45, 26, 850, 28, "THE GREAT HOMECOMING  /  " + sequenceName.ToUpperInvariant() + (preview ? " PREVIEW" : ""), small);
            if (!string.IsNullOrEmpty(shot.label))
            {
                Text(75, 124, 450, 130, shot.label, number);
                Text(83, 251, 480, 28, shot.label == "1969" ? "THE FIRST TRANSMISSION" : "57 YEARS LATER", small);
            }
            if (shot.id == "title")
            {
                Fill(new Rect(0, 72, 1600, 710), ink);
                Text(250, 255, 1100, 35, "OPERATION", speaker);
                var centered = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter };
                Text(150, 305, 1300, 220, "THE GREAT\nHOMECOMING", centered);
                Fill(new Rect(735, 565, 130, 3), rust);
            }
            bool dialogue = captions && !string.IsNullOrEmpty(shot.line);
            string display = dialogue ? shot.line : descriptions ? shot.description : "";
            if (!string.IsNullOrEmpty(display))
            {
                Fill(new Rect(230, 650, 1140, 111), new Color(.015f, .022f, .028f, .91f));
                int colon = dialogue ? display.IndexOf(':') : -1;
                Text(270, 666, 1060, 25, colon > 0 ? display.Substring(0, colon).ToUpperInvariant() : "SCENE DESCRIPTION", speaker);
                Text(270, 696, 1060, 55, colon > 0 ? display.Substring(colon + 1).Trim() : display, caption);
            }
            if (stage.VideoFallback && shot.id == "apollo") Text(60, 744, 600, 28, "APOLLO 11  /  ARCHIVAL STILL", small);
            float total = 0, elapsed = shotTime;
            for (int i = 0; i < sequence.Length; i++) { total += sequence[i].seconds; if (i < shotIndex) elapsed += sequence[i].seconds; }
            Fill(new Rect(44, 794, 1512, 2), new Color(.2f, .24f, .24f));
            Fill(new Rect(44, 794, 1512 * Mathf.Clamp01(elapsed / total), 2), rust);
            if (Button(24, 818, 175, paused ? "Resume" : "Pause")) Pause();
            if (Button(203, 818, 155, "Previous")) SetShot(Mathf.Max(0, shotIndex - 1));
            if (Button(368, 818, 150, "Next shot")) Advance();
            Text(560, 836, 190, 30, (shotIndex + 1).ToString("00") + " / " + sequence.Length.ToString("00") + "    " + elapsed.ToString("00") + "s", small);
            if (Button(780, 818, 180, sound.Muted ? "Sound: off" : "Sound: on")) sound.SetMuted(!sound.Muted);
            if (Button(970, 818, 195, captions ? "Captions: on" : "Captions: off")) { captions = !captions; SaveOptions(); }
            if (Button(1190, 818, 170, "Skip chapter")) FinishSequence();
            if (Button(1380, 818, 185, "Main menu")) Show(Page.Home);
            if (paused)
            {
                Fill(new Rect(690, 355, 220, 75), new Color(ink.r, ink.g, ink.b, .94f));
                var centered = new GUIStyle(subtitle) { alignment = TextAnchor.MiddleCenter };
                Text(690, 370, 220, 50, "PAUSED", centered);
            }
        }
        void RecruitGUI()
        {
            Header("PERSONNEL  /  RECRUITMENT");
            Fill(new Rect(840, 96, 760, 708), new Color(ink.r, ink.g, ink.b, .97f));
            Text(900, 170, 600, 30, "INCOMING TRANSMISSION", small);
            Text(900, 230, 610, 100, "Welcome aboard,\nrecruit.", subtitle);
            Text(900, 359, 575, 105, "“The party’s been on hold since 1969. Find out what Earth has that we don’t.”", body);
            Text(900, 499, 550, 30, "RECRUIT DESIGNATION", small);
            GUI.SetNextControlName("designation");
            designation = GUI.TextField(new Rect(900, 542, 550, 68), designation, 28, input);
            bool submit = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUI.enabled = !string.IsNullOrWhiteSpace(designation);
            if (Button(900, 638, 550, "Join the operation     →", true) || (submit && GUI.enabled))
            {
                designation = System.Text.RegularExpressions.Regex.Replace(designation.Trim(), @"\s+", " ");
                StorySession.RecruitName = designation;
                sound.Cue("stamp");
                Play("briefing");
            }
            GUI.enabled = true;
            Text(900, 723, 550, 38, "Your designation is saved on this device.", small);
        }
        void ReadyGUI()
        {
            Header("OPERATION  /  LABORATORY");
            Fill(new Rect(44, 546, 690, 225), new Color(ink.r, ink.g, ink.b, .97f));
            Text(76, 572, 600, 30, "BRIEFING COMPLETE", small);
            Text(76, 616, 600, 48, "Your planet. Try the dials.", subtitle);
            bool available = Application.CanStreamedLevelBeLoaded(simulationScene);
            if (available)
            {
                if (Button(76, 686, 600, "Enter laboratory     →", true)) SceneManager.LoadSceneAsync(simulationScene);
            }
            else
            {
                Text(76, 684, 600, 30, "The simulation scene will continue the operation here.", body);
                Text(76, 730, 600, 25, "Story complete up to the laboratory handoff.", small);
            }
            if (Button(1040, 696, 480, "Preview the final hearing")) Play("ending", true);
        }
        void EpilogueGUI()
        {
            Fill(new Rect(0, 96, 1600, 708), new Color(ink.r, ink.g, ink.b, .92f));
            Header("RELOCATION PROGRAM  /  STATUS");
            var center = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter };
            Text(150, 230, 1300, 180, "THE GREAT HOMECOMING", center);
            var centeredBody = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            Text(200, 435, 1200, 45, "STATUS: STILL WAITING FOR HUMANITY", centeredBody);
            Text(200, 515, 1200, 45, "Better Together. Eventually.", centeredBody);
            if (Button(370, 647, 400, "Replay transmission", true)) Play("prologue");
            if (Button(830, 647, 400, "Return to laboratory")) Show(Page.Ready);
            if (preview) Text(650, 746, 400, 26, "ENDING PREVIEW  /  PROGRESS UNCHANGED", small);
        }
        void ChaptersGUI()
        {
            Fill(new Rect(0, 96, 1600, 708), ink);
            Header("CHAPTERS / PLAYBACK");
            Text(85, 160, 700, 70, "The operation, in order.", subtitle);
            if (Button(85, 275, 650, "01    The transmission  /  1969–2026", true)) Play("prologue");
            if (Button(85, 358, 650, "02    Recruitment")) Show(Page.Recruit);
            if (Button(85, 441, 650, "03    The laboratory briefing")) Play("briefing", true);
            if (Button(85, 524, 650, "04    The final hearing  /  preview")) Play("ending", true);
            Text(905, 267, 600, 35, "PLAYBACK OPTIONS", small);
            if (Button(880, 320, 600, captions ? "Dialogue captions: on" : "Dialogue captions: off")) { captions = !captions; SaveOptions(); }
            if (Button(880, 403, 600, descriptions ? "Scene descriptions: on" : "Scene descriptions: off")) { descriptions = !descriptions; SaveOptions(); }
            if (Button(880, 486, 600, reducedMotion ? "Camera motion: reduced" : "Camera motion: full")) { reducedMotion = !reducedMotion; SaveOptions(); }
            Text(905, 592, 550, 72, "Space: pause  ·  ← / →: shots\nM: sound  ·  C: captions  ·  Esc: pause / menu", small);
            if (Button(85, 676, 650, "Start a new operation")) Show(Page.Reset);
        }
        void CreditsGUI()
        {
            Fill(new Rect(0, 0, 1600, 900), ink);
            Header("CREDITS / SOURCE MATERIAL");
            Text(85, 150, 1200, 70, "Operation: The Great Homecoming", subtitle);
            Text(85, 250, 630, 35, "STORY & CHARACTERS", small);
            Text(85, 300, 600, 180, "Commander Zorvax\nDr. Nyxen\nCadet B\n\nOriginal Homecoming story and rigged character assets from this project.", body);
            Text(85, 550, 600, 120, "NASA Space Apps Kuwait\nIntergalactic Affection Department", body);
            Text(860, 250, 630, 35, "ARCHIVAL MATERIAL", small);
            Text(860, 300, 610, 200, "Apollo 11 television: NASA Apollo Lunar Surface Journal.\n\nMoon surface: NASA Goddard CGI Moon Kit.\n\nEarth: existing KU Exchange globe textures bundled with this repository.", body);
            Text(860, 565, 610, 140, "Unity glTFast: Unity Technologies, Apache 2.0.\nOriginal animation clips retained.\nDialogue is presented as subtitles.", body);
            if (Button(85, 765, 350, "Back to main menu", true)) Show(Page.Home);
        }
        void ResetGUI()
        {
            Fill(new Rect(0, 96, 1600, 708), ink);
            Header("PERSONNEL / NEW OPERATION");
            Text(360, 255, 900, 80, "Start a new operation?", subtitle);
            Text(360, 370, 900, 90, "This clears the recruit designation and story progress saved on this device.", body);
            if (Button(360, 540, 400, "Start over", true)) { StorySession.Reset(); designation = ""; Play("prologue"); }
            if (Button(820, 540, 400, "Keep this operation")) Show(Page.Home);
        }
    }
}
