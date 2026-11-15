using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming
{
    // A bespoke mission-poster interface. Native IMGUI control skins are never drawn.
    public sealed partial class StoryDirector
    {
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var color); return color; }
        readonly Color paper = Hex("#F4E9CF"), ink = Hex("#091821"), panel = Hex("#102632");
        readonly Color orange = Hex("#FF9D52"), mint = Hex("#99CEBD"), muted = Hex("#94AAA9");
        readonly Dictionary<string, float> hoverStates = new Dictionary<string, float>();
        readonly Dictionary<(Font, int, Color, TextAnchor, bool), GUIStyle> textStyles = new Dictionary<(Font, int, Color, TextAnchor, bool), GUIStyle>();
        Font displayFont, bodyFont, monoFont;
        GUIStyle micro, body, display, heading, captionStyle, inputStyle, buttonStyle, mono;
        Texture2D white, disc, gradient;
        bool interfaceReady, focusRecruit;
        string hoveredControl, operationResetError;
        Page operationResetReturnPage;
        float lastPointerActivity, interfaceClock, uiOpacity = 1;
        Vector2 lastPointer;

        void ResetInterfaceFocus()
        {
            focusRecruit = true;
            lastPointerActivity = Time.unscaledTime;
            hoverStates.Clear();
            hoveredControl = null;
        }
        void UpdateInterface()
        {
            interfaceClock += Time.unscaledDeltaTime;
            var pointer = (Vector2)Input.mousePosition;
            if ((pointer - lastPointer).sqrMagnitude > 4 || Input.anyKeyDown) lastPointerActivity = Time.unscaledTime;
            lastPointer = pointer;
        }
        static float Ease(float value) => 1 - Mathf.Pow(1 - Mathf.Clamp01(value), 3);
        float Enter(float delay = 0) => reducedMotion ? 1 : Ease((pageTime - delay) / .65f);
        GUIStyle Style(Font font, int size, Color color, TextAnchor align = TextAnchor.UpperLeft, bool wrap = false)
        {
            var key = (font, size, color, align, wrap);
            if (textStyles.TryGetValue(key, out var cached)) return cached;
            var style = new GUIStyle { font = font, fontSize = size, alignment = align, wordWrap = wrap, richText = false };
            style.normal.textColor = color;
            textStyles.Add(key, style);
            return style;
        }
        void EnsureInterface()
        {
            if (interfaceReady) return;
            displayFont = Resources.Load<Font>("Fonts/Display");
            bodyFont = Resources.Load<Font>("Fonts/Body");
            monoFont = Resources.Load<Font>("Fonts/Mono");
            if (displayFont == null || bodyFont == null || monoFont == null)
                throw new InvalidOperationException("Homecoming's Display, Body and Mono font assets must be included.");
            micro = Style(monoFont, 12, muted);
            mono = Style(monoFont, 14, paper);
            body = Style(bodyFont, 24, paper, TextAnchor.UpperLeft, true);
            display = Style(displayFont, 135, paper);
            heading = Style(displayFont, 65, paper);
            captionStyle = Style(bodyFont, 28, paper, TextAnchor.MiddleLeft, true);
            buttonStyle = Style(bodyFont, 23, paper, TextAnchor.MiddleLeft);
            inputStyle = Style(bodyFont, 29, paper, TextAnchor.MiddleLeft);
            inputStyle.padding = new RectOffset(21, 21, 0, 0);
            inputStyle.clipping = TextClipping.Clip;
            inputStyle.hover.textColor = inputStyle.active.textColor = inputStyle.focused.textColor = paper;
            GUI.skin.settings.selectionColor = new Color(.16f, .36f, .4f);
            GUI.skin.settings.cursorColor = orange.linear;
            white = Texture2D.whiteTexture;
            disc = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            for (int y = 0; y < 96; y++) for (int x = 0; x < 96; x++)
            {
                float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(48, 48));
                disc.SetPixel(x, y, new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(46.5f, 48, r))));
            }
            disc.Apply();
            gradient = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < 256; i++) gradient.SetPixel(i, 0, new Color(1, 1, 1, 1 - Mathf.SmoothStep(.05f, 1, i / 255f)));
            gradient.Apply();
            interfaceReady = true;
        }
        void DisposeInterface()
        {
            if (disc != null) Destroy(disc);
            if (gradient != null) Destroy(gradient);
        }
        void TintTexture(Rect rect, Texture texture, Color color)
        {
            var previous = GUI.color;
            color.a *= uiOpacity * previous.a;
            GUI.color = color.linear;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }
        void Fill(Rect rect, Color color) => TintTexture(rect, white, color);
        void Rule(float x, float y, float width, Color color, float thickness = 1) => Fill(new Rect(x, y, width, thickness), color);
        void Dot(float x, float y, float radius, Color color) => TintTexture(new Rect(x - radius, y - radius, radius * 2, radius * 2), disc, color);
        void Stroke(Vector2 start, Vector2 end, Color color, float thickness = 1)
        {
            var matrix = GUI.matrix;
            Vector2 delta = end - start;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            Fill(new Rect(start.x, start.y - thickness / 2, delta.magnitude, thickness), color);
            GUI.matrix = matrix;
        }
        void Arc(Vector2 center, float radius, float start, float sweep, Color color, float thickness = 1)
        {
            const int steps = 52;
            for (int i = 0; i < steps; i++)
            {
                float a = (start + sweep * i / steps) * Mathf.Deg2Rad, b = (start + sweep * (i + 1) / steps) * Mathf.Deg2Rad;
                Stroke(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, color, thickness);
            }
        }
        void Frame(Rect rect, Color color, float corner = 12)
        {
            Rule(rect.x, rect.y, corner, color); Rule(rect.xMax - corner, rect.y, corner, color);
            Rule(rect.x, rect.yMax, corner, color); Rule(rect.xMax - corner, rect.yMax, corner, color);
            Fill(new Rect(rect.x, rect.y, 1, corner), color); Fill(new Rect(rect.xMax, rect.y, 1, corner), color);
            Fill(new Rect(rect.x, rect.yMax - corner, 1, corner), color); Fill(new Rect(rect.xMax, rect.yMax - corner, 1, corner), color);
        }
        void Label(Rect rect, string text, GUIStyle style, Color? color = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color previous = style.normal.textColor;
            if (color.HasValue) style.normal.textColor = color.Value;
            var guiColor = GUI.color;
            GUI.color = new Color(1, 1, 1, guiColor.a * uiOpacity);
            style.Draw(rect, new GUIContent(text), false, false, false, false);
            GUI.color = guiColor;
            style.normal.textColor = previous;
        }
        void Text(float x, float y, float w, float h, string text, GUIStyle style, Color? color = null) => Label(new Rect(x, y, w, h), text, style, color);
        void Badge(float x, float y, string text, Color color)
        {
            float width = micro.CalcSize(new GUIContent(text)).x + 32;
            Fill(new Rect(x, y, width, 29), new Color(color.r, color.g, color.b, .11f));
            Fill(new Rect(x, y, 2, 29), color);
            Text(x + 13, y + 6, width - 20, 22, text, micro, color);
        }
        void Arrow(float x, float y, Color color, float size = 20)
        {
            Stroke(new Vector2(x - size / 2, y), new Vector2(x + size / 2, y), color, 2);
            Stroke(new Vector2(x + size / 2 - 7, y - 7), new Vector2(x + size / 2, y), color, 2);
            Stroke(new Vector2(x + size / 2 - 7, y + 7), new Vector2(x + size / 2, y), color, 2);
        }
        float Hover(string id, bool over)
        {
            hoverStates.TryGetValue(id, out float amount);
            if (Event.current.type == EventType.Repaint)
            {
                amount = Mathf.MoveTowards(amount, over ? 1 : 0, reducedMotion ? 1 : Time.unscaledDeltaTime * 7);
                hoverStates[id] = amount;
            }
            return amount;
        }
        bool Action(string id, Rect rect, string text, bool primary = false, string tag = null, bool enabled = true)
        {
            bool over = enabled && rect.Contains(Event.current.mousePosition);
            bool focused = GUI.GetNameOfFocusedControl() == id;
            float hover = Hover(id, over || focused);
            bool held = over && Input.GetMouseButton(0);
            float offset = held ? 3 : -hover * 2;
            var draw = new Rect(rect.x, rect.y + offset, rect.width, rect.height);
            Color face = primary ? Color.Lerp(orange, paper, hover * .18f) : Color.Lerp(panel, Hex("#1D3B46"), hover);
            Color textColor = primary ? ink : paper;
            if (!enabled) { face = panel; textColor = muted; }
            if (primary) Fill(new Rect(draw.x + 4, rect.y + 5, draw.width, draw.height), Hex("#5C3F29"));
            Fill(draw, face);
            // Cut corners and an inset rail distinguish controls from stock Unity rectangles.
            for (int i = 0; i < 8; i++) Fill(new Rect(draw.xMax - 8 + i, draw.y, 8 - i, i + 1), ink);
            Fill(new Rect(draw.x, draw.y + 12, 3, draw.height - 24), primary ? ink : Color.Lerp(muted, orange, hover));
            if (!primary) Rule(draw.x + 18, draw.yMax - 1, draw.width - 36, new Color(muted.r, muted.g, muted.b, .28f + hover * .5f));
            Text(draw.x + 22 + hover * 4, draw.y + 1, draw.width - 86, draw.height - 2, text, buttonStyle, textColor);
            if (tag != null) Text(draw.xMax - 74, draw.y + draw.height / 2 - 8, 60, 22, tag, micro, textColor);
            else Arrow(draw.xMax - 31 + hover * 3, draw.center.y, textColor, 18);
            GUI.SetNextControlName(id);
            bool oldEnabled = GUI.enabled; GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = oldEnabled;
            if (over && hoveredControl != id && Event.current.type == EventType.Repaint)
            {
                hoveredControl = id;
                sound.Cue("hover");
            }
            if (clicked) sound.Cue("click");
            return clicked;
        }
        bool Link(string id, Rect rect, string text, Color? color = null)
        {
            bool over = rect.Contains(Event.current.mousePosition);
            float hover = Hover(id, over || GUI.GetNameOfFocusedControl() == id);
            if (hover > .01f)
            {
                Fill(new Rect(rect.x-6,rect.y,rect.width+6,rect.height),new Color(panel.r,panel.g,panel.b,hover));
                Rule(rect.x-6,rect.yMax-2,(rect.width+6)*hover,orange,2);
            }
            Text(rect.x + hover * 3, rect.y, rect.width, rect.height, text, mono, Color.Lerp(color ?? muted, paper, hover));
            GUI.SetNextControlName(id);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (clicked) sound.Cue("click");
            return clicked;
        }
        void Insignia(float x, float y, float radius, Color color)
        {
            Arc(new Vector2(x, y), radius, -70, 280, color, 1.5f);
            Arc(new Vector2(x, y), radius * .7f, 20, 260, color, 1);
            Stroke(new Vector2(x - radius * 1.15f, y + radius * .6f), new Vector2(x + radius * 1.15f, y - radius * .6f), color, 2);
            Dot(x, y, radius * .18f, color);
            Dot(x + radius * .84f, y - radius * .58f, radius * .1f, paper);
        }
        void Signal(float x, float y, bool animate = true)
        {
            for (int i = 0; i < 16; i++)
            {
                float height = 3 + (animate && !reducedMotion ? (Mathf.Sin(interfaceClock * 3 + i * .8f) * .5f + .5f) * (5 + i % 4 * 3) : i % 4 * 3);
                Fill(new Rect(x + i * 5, y - height / 2, 2, height), i < 11 ? mint : new Color(mint.r, mint.g, mint.b, .3f));
            }
        }
        void Backdrop(bool opaque = false)
        {
            if (opaque) Fill(new Rect(0, 0, 1600, 900), ink);
            else if (page == Page.Home)
            {
                Fill(new Rect(0, 0, 560, 900), new Color(ink.r, ink.g, ink.b, .96f));
                TintTexture(new Rect(560, 0, 535, 900), gradient, new Color(ink.r, ink.g, ink.b, .96f));
            }
            Fill(new Rect(0, 814, 1600, 86), new Color(ink.r, ink.g, ink.b, .96f));
        }
        void Header(string location)
        {
            Insignia(72, 56, 24, orange);
            Text(114, 35, 600, 24, "INTERGALACTIC AFFECTION", micro, paper);
            Text(114, 57, 600, 24, "DEPARTMENT   /   I.A.D.", micro);
            Dot(1260, 49, 3, mint);
            Text(1276, 39, 285, 24, location, micro);
            Rule(46, 99, 1508, new Color(muted.r, muted.g, muted.b, .23f));
        }
        void Footer()
        {
            Rule(46, 814, 1508, new Color(muted.r, muted.g, muted.b, .23f));
            Text(48, 843, 450, 25, "NASA SPACE APPS   /   KUWAIT", micro);
            if (Link("footer-sound", new Rect(826, 836, 150, 35), sound.Muted ? "AUDIO  OFF" : "AUDIO  ON")) sound.SetMuted(!sound.Muted);
            if (Link("footer-chapters", new Rect(1002, 836, 190, 35), "STORY & SETTINGS")) Show(Page.Chapters);
            if (Link("footer-credits", new Rect(1235, 836, 100, 35), "CREDITS")) Show(Page.Credits);
            if (page != Page.Home && Link("footer-menu", new Rect(1392, 836, 165, 35), "MAIN MENU")) Show(Page.Home);
        }
        void OnGUI()
        {
            EnsureInterface();
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            float scale = Scale;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) / 2, (Screen.height - 900 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            uiOpacity = 1;
            if (page == Page.Film) FilmGUI();
            else
            {
                Backdrop(page == Page.Chapters || page == Page.Credits || page == Page.Reset || page == Page.Epilogue || page == Page.Report);
                if (page == Page.Home) HomeGUI();
                else if (page == Page.Recruit) RecruitGUI();
                else if (page == Page.Ready) ReadyGUI();
                else if (page == Page.Epilogue) EpilogueGUI();
                else if (page == Page.Report) ReportGUI();
                else if (page == Page.Chapters) ChaptersGUI();
                else if (page == Page.Credits) CreditsGUI();
                else ResetGUI();
                uiOpacity = 1;
                Footer();
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
        void HomeGUI()
        {
            Header("VESSEL 03  /  IN ORBIT");
            float enter = Enter();
            uiOpacity = enter;
            Badge(58, 153 + (1 - enter) * 20, "SIGNAL INTERCEPTED / JULY 1969", mint);
            Text(56, 207 + (1 - enter) * 32, 640, 80, "THE GREAT", Style(displayFont, 68, paper));
            Text(50, 265 + (1 - enter) * 45, 810, 155, "HOMECOMING", Style(displayFont, 142, paper));
            Rule(60, 430, 57, orange, 3);
            Text(137, 420, 460, 30, "AN INTERGALACTIC MISUNDERSTANDING", micro, orange);
            uiOpacity = Enter(.12f);
            Text(60, 478, 485, 85, "One human reached the Moon.\nThree aliens set the table.", body);
            if (StorySession.BriefingComplete)
            {
                if (Action("continue", new Rect(60, 600, 470, 72), "Continue mission", true) || TakeSubmitKey("continue")) Show(Page.Ready);
                if (Link("replay", new Rect(62, 706, 390, 40), "REPLAY FROM START  →")) ConfirmOperationReset();
            }
            else if (!string.IsNullOrWhiteSpace(designation))
            {
                if (Action("replay", new Rect(60, 600, 470, 72), "Replay from start", true) || TakeSubmitKey("replay"))
                    ConfirmOperationReset();
                if (Link("skip", new Rect(62, 706, 390, 40), "RETURN TO RECRUITMENT  →")) Show(Page.Recruit);
            }
            else
            {
                if (Action("start", new Rect(60, 600, 470, 72), "Start story", true) || TakeSubmitKey("start")) Play("prologue");
                if (Link("skip", new Rect(62, 706, 390, 40), "SKIP TO RECRUITMENT  →")) Show(Page.Recruit);
            }
            uiOpacity = Enter(.22f);
            Frame(new Rect(1090, 650, 443, 113), new Color(mint.r, mint.g, mint.b, .5f), 14);
            Text(1115, 669, 360, 25, "HUMAN ARRIVAL", micro);
            Text(1115, 698, 360, 45, "57 YEARS OVERDUE", Style(displayFont, 38, paper));
            Signal(1430, 686);
            uiOpacity = 1;
        }
        void FilmGUI()
        {
            var shot = sequence[shotIndex];
            Fill(new Rect(0, 0, 1600, 58), ink);
            Fill(new Rect(0, 794, 1600, 106), ink);
            if (shot.id == "apollo") Badge(63, 88, "APOLLO 11  /  1969", orange);
            if (shot.id == "years")
            {
                float lapse = StoryStage.Lapse(shotTime, reducedMotion);
                int year = 1969 + Mathf.RoundToInt(57 * lapse);
                Badge(63, 88, lapse < 1 ? "TIME PASSING  /  " + year : "57 YEARS LATER  /  2026", orange);
                Fill(new Rect(63, 126, 290, 4), new Color(paper.r, paper.g, paper.b, .2f));
                Fill(new Rect(63, 126, 290 * lapse, 4), orange);
            }
            if (shot.id == "return")
                Badge(63, 88, shotTime < 3.2f ? "ARTEMIS II  /  CREWED LUNAR FLYBY" : "ARTEMIS II  /  CREW RETURNED TO EARTH", mint);
            if (shot.id == "lab")
                Badge(63, 88, shotTime < 4.2f ? "EARTH SIMULATION" : "EARTH SIMULATION  /  ADJUST WEATHER, THEN PREVIEW", mint);
            if (shot.id == "hearing") Badge(63, 88, "GOVERNMENT HEARING", orange);
            if (shot.id == "deflate" && shotTime >= 1.8f)
                Fill(new Rect(0, 0, 1600, 900), new Color(0, 0, 0, Ease((shotTime - 1.8f) / 1.4f)));
            if (shot.id == "title")
            {
                Fill(new Rect(0, 0, 1600, 794), Color.black);
                Text(200, 279, 1200, 35, "OPERATION", Style(monoFont, 14, mint, TextAnchor.MiddleCenter));
                float reveal = reducedMotion ? 1 : Ease(shotTime / .8f);
                uiOpacity = reveal;
                Text(140, 337 + (1 - reveal) * 35, 1320, 170, "THE GREAT HOMECOMING", Style(displayFont, 126, paper, TextAnchor.MiddleCenter));
                Rule(740, 548, 120 * reveal, orange, 3);
                uiOpacity = 1;
            }
            string spoken = shot.LineAt(shotTime, out float lineAge, StorySession.HearingEvidence);
            var activeLine = shot.ActiveLine(shotTime, StorySession.HearingEvidence);
            if (activeLine != null && lineAge > sound.CaptionDuration(activeLine.id, activeLine.end - activeLine.at)) spoken = null;
            bool dialogue = captions && !string.IsNullOrEmpty(spoken);
            string line = dialogue ? spoken : descriptions ? shot.description : "";
            if (!string.IsNullOrEmpty(line))
            {
                // A new line eases in, so a reply reads as a reply rather than a jump cut.
                uiOpacity = dialogue && !reducedMotion ? Ease(lineAge / .18f) : 1;
                int colon = dialogue ? line.IndexOf(':') : -1;
                string name = colon > 0 ? line.Substring(0, colon).ToUpperInvariant() : dialogue ? "SOUND" : "SCENE DESCRIPTION";
                string words = colon > 0 ? line.Substring(colon + 1).Trim() : line;
                float width = Mathf.Clamp(captionStyle.CalcSize(new GUIContent(words)).x + 130, 520, 1110);
                float height = captionStyle.CalcHeight(new GUIContent(words), width - 90) + 61;
                float x = (1600 - width) / 2, y = 769 - height;
                Fill(new Rect(x + 3, y + 4, width, height), new Color(0, 0, 0, .3f));
                Fill(new Rect(x, y, width, height), new Color(ink.r, ink.g, ink.b, .97f));
                // One accent per speaker: Zorvax orange, Nyxen mint, Cadet B paper.
                Color accent = name == "NYXEN" ? mint : name == "CADET B" ? paper : orange;
                Fill(new Rect(x, y, 3, height), accent);
                Text(x + 25, y + 12, width - 50, 22, name, micro, accent);
                Text(x + 25, y + 37, width - 50, height - 44, words, captionStyle);
                Frame(new Rect(x, y, width, height), new Color(muted.r, muted.g, muted.b, .28f), 6);
                uiOpacity = 1;
            }
            float total = 0, elapsed = shotTime;
            for (int i = 0; i < sequence.Length; i++) { total += sequence[i].seconds; if (i < shotIndex) elapsed += sequence[i].seconds; }
            float gap = 5, segment = (1508 - gap * (sequence.Length - 1)) / sequence.Length;
            for (int i = 0; i < sequence.Length; i++)
            {
                float x = 46 + (segment + gap) * i;
                Rule(x, 807, segment, Hex("#26404A"), 2);
                float fill = i < shotIndex ? 1 : i == shotIndex ? Mathf.Clamp01(shotTime / shot.seconds) : 0;
                if (fill > 0) Rule(x, 807, segment * fill, orange, 2);
            }
            bool pointerNearTransport = Event.current.mousePosition.y > 770;
            float visibility = paused || pointerNearTransport ? 1 : Mathf.Lerp(.36f, 1, 1 - Mathf.Clamp01((Time.unscaledTime - lastPointerActivity - 2) / .8f));
            uiOpacity = visibility;
            if (Link("pause", new Rect(48, 842, 120, 35), paused ? "RESUME" : "PAUSE", paper)) Pause();
            if (Link("previous", new Rect(192, 842, 66, 35), "←")) SetShot(Mathf.Max(0, shotIndex - 1));
            if (Link("next", new Rect(274, 842, 100, 35), "NEXT →")) Advance();
            Text(411, 843, 290, 25, (shotIndex + 1).ToString("00") + " / " + sequence.Length.ToString("00") + "   " + elapsed.ToString("00") + "s", micro);
            if (Link("sound", new Rect(785, 842, 160, 35), sound.Muted ? "AUDIO OFF" : "AUDIO ON")) sound.SetMuted(!sound.Muted);
            if (Link("captions", new Rect(977, 842, 180, 35), captions ? "CAPTIONS ON" : "CAPTIONS OFF")) { captions = !captions; SaveOptions(); }
            if (Link("skip-chapter", new Rect(1198, 842, 175, 35), "SKIP CHAPTER")) FinishSequence();
            if (Link("menu", new Rect(1432, 842, 126, 35), "MENU")) Show(Page.Home);
            uiOpacity = 1;
            if (stage.VideoFallback && shot.id == "apollo") Badge(62, 737, "APOLLO 11 / ARCHIVAL STILL", muted);
            bool showPause = paused;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            showPause &= !StoryCapture.Exporting;
#endif
            if (showPause)
            {
                Fill(new Rect(652, 335, 296, 112), ink);
                Frame(new Rect(652, 335, 296, 112), orange, 17);
                Text(652, 347, 296, 68, "ON HOLD", Style(displayFont, 54, paper, TextAnchor.MiddleCenter));
                Text(652, 413, 296, 24, "SPACE TO RESUME", Style(monoFont, 11, muted, TextAnchor.MiddleCenter));
            }
        }
        void RecruitGUI()
        {
            // Solid dark right panel, with a small border rail rather than a default form card.
            Fill(new Rect(815, 100, 785, 714), new Color(ink.r, ink.g, ink.b, .98f));
            Header("PERSONNEL / ENLISTMENT");
            uiOpacity = Enter();
            Badge(884, 151, "INCOMING / COMMANDER ZORVAX", orange);
            Text(878, 211 + (1 - Enter()) * 25, 650, 156, "A SUSPICIOUSLY\nGENEROUS JOB OFFER.", Style(displayFont, 60, paper));
            Text(884, 394, 572, 94, "“The party’s been on hold since 1969. Find out what Earth has that we don’t.”", body);
            Text(884, 524, 530, 25, "01 / RECRUIT DESIGNATION", micro, mint);
            var rect = new Rect(884, 568, 600, 70);
            bool focused = GUI.GetNameOfFocusedControl() == "designation";
            bool submit = SubmitKey && !string.IsNullOrWhiteSpace(designation);
            if (submit) Event.current.Use();
            Fill(rect, panel);
            Rule(rect.x, rect.yMax, rect.width, focused ? orange : muted, focused ? 2 : 1);
            Fill(new Rect(rect.x, rect.y + 16, 3, 38), orange);
            GUI.SetNextControlName("designation");
            designation = GUI.TextField(rect, designation, 28, inputStyle);
            if (string.IsNullOrEmpty(designation) && !focused) Text(rect.x + 21, rect.y + 21, 520, 38, "Your alien name", body, muted);
            if (focusRecruit && Event.current.type == EventType.Repaint) { GUI.FocusControl("designation"); focusRecruit = false; }
            bool valid = !string.IsNullOrWhiteSpace(designation);
            if (Action("recruit", new Rect(884, 676, 600, 70), "JOIN THE OPERATION", true, null, valid) || (submit && valid))
            {
                designation = System.Text.RegularExpressions.Regex.Replace(designation.Trim(), @"\s+", " ");
                StorySession.RecruitName = designation;
                sound.Cue("stamp");
                Play("briefing");
            }
            Text(884, 771, 600, 25, "SAVED ON THIS DEVICE. NO PAPERWORK. MOSTLY.", micro);
            uiOpacity = 1;
            Frame(new Rect(65, 620, 364, 131), new Color(mint.r, mint.g, mint.b, .5f), 14);
            Text(86, 641, 320, 24, "COMMANDING OFFICER", micro, mint);
            Text(84, 669, 325, 48, "ZORVAX", Style(displayFont, 42, paper));
            Text(86, 724, 320, 24, "RELOCATION PROGRAM / 1969–", micro);
        }
        static bool SubmitKey => Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

        static bool TakeSubmitKey(string control)
        {
            if (!SubmitKey) return false;
            string focused = GUI.GetNameOfFocusedControl();
            if (!string.IsNullOrEmpty(focused) && focused != control) return false;
            Event.current.Use();
            return true;
        }

        void ReadyGUI()
        {
            Header("COMMUNICATIONS / BROADCAST DECK");
            uiOpacity = Enter();
            Fill(new Rect(46, 527, 700, 248), new Color(ink.r, ink.g, ink.b, .96f));
            Badge(70, 549, "FIRST CONTACT / YOUR ASSIGNMENT", mint);
            Text(67, 596, 650, 76, "EARTH ON THE LINE.", Style(displayFont, 57, paper));
            bool available = Application.CanStreamedLevelBeLoaded(simulationScene);
            if (available)
            {
                bool submit = TakeSubmitKey("enter-broadcast");
                if (Action("enter-broadcast", new Rect(70, 690, 630, 62), "INVESTIGATE THE RAIN ALERT", true) || submit)
                    StorySession.OpenBroadcast(simulationScene);
                if (Action("enter-lab", new Rect(1044, 699, 488, 62), "Open field station")) SceneManager.LoadSceneAsync(simulationScene);
            }
            else
            {
                Text(70, 688, 625, 58, "The simulation scene will continue the operation here.", body);
                Text(70, 752, 625, 22, "AWAITING LABORATORY CONNECTION", micro, mint);
            }
            if (StorySession.EndingComplete && StorySession.FiledReport != null)
            {
                if (Action("open-report", new Rect(1044, 621, 488, 62), "Read my report")) ShowFiledReport();
            }
            uiOpacity = 1;
        }
        void EpilogueGUI()
        {
            Header("RELOCATION / FINAL STATUS");
            uiOpacity = Enter();
            Insignia(800, 202, 45, orange);
            Text(200, 294, 1200, 40, "THE GREAT HOMECOMING", Style(monoFont, 16, mint, TextAnchor.MiddleCenter));
            Text(150, 356, 1300, 120, "STILL WAITING FOR HUMANITY", Style(displayFont, 94, paper, TextAnchor.MiddleCenter));
            Text(200, 507, 1200, 60, "Better Together. Eventually.", Style(bodyFont, 31, paper, TextAnchor.MiddleCenter));
            Rule(745, 599, 110, orange, 3);
            if (preview)
            {
                if (Action("replay", new Rect(363, 659, 410, 68), "Replay prologue", true)) Play("prologue");
                if (Action("back-chapters", new Rect(825, 659, 410, 68), "Back to chapters")) Show(Page.Chapters);
            }
            else
            {
                bool hasReport = StorySession.FiledReport != null;
                if (hasReport && Action("filed-report", new Rect(172, 660, 378, 68), "Read my report", true)) ShowFiledReport();
                if (Action("return-lab", new Rect(611, 660, 378, 68), "Continue experimenting", !hasReport))
                {
                    if (Application.CanStreamedLevelBeLoaded(simulationScene)) SceneManager.LoadSceneAsync(simulationScene);
                    else Show(Page.Ready);
                }
                if (Action("epilogue-reset", new Rect(1050, 660, 378, 68), "Replay from start")) ConfirmOperationReset();
            }
            uiOpacity = 1;
        }
        void ReportGUI()
        {
            var report = StorySession.FiledReport;
            if (report == null) return;
            Header("RELOCATION / FILED REPORT");
            Text(57, 120, 1488, 82, "THE GREAT HOMECOMING REPORT", Style(displayFont, 69, paper));
            Text(60, 204, 1450, 36, "An entirely objective assessment of humanity’s relocation prospects.", body);
            Text(62, 245, 1400, 26, string.IsNullOrWhiteSpace(report.recruit) ? "FILED BY THE FIELD AGENT" : $"FILED BY AGENT {report.recruit.ToUpperInvariant()}", micro, mint);
            DrawReportStat(62, $"{report.discoveries}/{report.totalDiscoveries}", "DISCOVERIES");
            DrawReportStat(436, report.experiments.ToString(), "EXPERIMENTS");
            DrawReportStat(810, report.investigations.ToString(), "EARTH INVESTIGATIONS");
            DrawReportStat(1184, "0", "HUMANS RELOCATED");
            Text(63, 415, 1420, 25, "COMMANDER’S ASSESSMENT", micro, orange);
            Text(63, 443, 1450, 38, "“Outstanding progress. Human arrival is practically guaranteed.”", Style(bodyFont, 30, paper));
            Text(63, 489, 1440, 31, "Scientific confidence in this assessment: nonexistent.", body, muted);
            var link = report.link;
            bool canLoad = Application.CanStreamedLevelBeLoaded(simulationScene);
            if (!string.IsNullOrEmpty(link?.runId))
            {
                Badge(62, 552, link.site.ToUpperInvariant() + "  /  " + link.variable.ToUpperInvariant(), mint);
                Text(63, 593, 720, 28, "MODELED EXPERIMENT", micro);
                Text(63, 624, 720, 60, link.scenario, body);
                Text(810, 593, 720, 28, "HISTORICAL EARTH RECORD", micro);
                Text(810, 624, 720, 60, link.historical, body);
                if (canLoad && Action("replay-linked", new Rect(62, 699, 338, 52), "Replay linked experiment"))
                    StorySession.OpenSimulationHistory(simulationScene, SimulationHistory.LinkedRun);
                if (canLoad && Action("open-earth-record", new Rect(810, 699, 338, 52), "Open linked Earth record"))
                    StorySession.OpenSimulationHistory(simulationScene, SimulationHistory.EarthRecord);
            }
            else Text(63, 569, 1440, 36, "No filed Earth record matches a weather variable tested at the same site.", body, muted);
            if (canLoad && Action("open-history", new Rect(62, 759, 338, 42), "Simulation history"))
                StorySession.OpenSimulationHistory(simulationScene, SimulationHistory.Branches);
            if (Action("back-epilogue", new Rect(1184, 759, 338, 42), "Back to final status")) Show(Page.Epilogue);
        }
        void DrawReportStat(float x, string value, string label)
        {
            Fill(new Rect(x, 295, 340, 94), panel);
            Text(x + 17, 300, 300, 51, value, Style(displayFont, 48, paper));
            Text(x + 18, 356, 300, 27, label, micro, mint);
        }
        bool Chapter(string id, int index, float y, string name, string detail)
        {
            var rect = new Rect(60, y, 730, 90);
            bool over = rect.Contains(Event.current.mousePosition);
            float hover = Hover(id, over || GUI.GetNameOfFocusedControl() == id);
            Fill(rect, Color.Lerp(panel, Hex("#203D48"), hover));
            Text(79, y + 16, 72, 63, index.ToString("00"), Style(displayFont, 46, orange));
            Text(169 + hover * 5, y + 13, 580, 37, name, Style(displayFont, 34, paper));
            Text(171 + hover * 5, y + 55, 560, 24, detail, micro);
            Arrow(754 + hover * 3, y + 44, paper);
            GUI.SetNextControlName(id);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (clicked) sound.Cue("click");
            return clicked;
        }
        bool Toggle(string id, float y, string name, string detail, bool on)
        {
            var rect = new Rect(904, y, 624, 90);
            bool over = rect.Contains(Event.current.mousePosition);
            float hover = Hover(id, over || GUI.GetNameOfFocusedControl() == id);
            Text(904 + hover * 3, y + 9, 480, 35, name, Style(bodyFont, 25, paper));
            Text(905, y + 48, 485, 26, detail, micro);
            var track = new Rect(1451, y + 20, 62, 28);
            Fill(track, on ? mint : Hex("#2C414A"));
            Fill(new Rect(track.x + (on ? 36 : 4), track.y + 4, 22, 20), ink);
            Rule(904, y + 88, 624, Hex("#28404A"));
            GUI.SetNextControlName(id);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (clicked) sound.Cue("click");
            return clicked;
        }
        void ChaptersGUI()
        {
            Header("MISSION FILE / PLAYBACK");
            Text(55, 151, 900, 95, "THE OPERATION, IN ORDER.", Style(displayFont, 73, paper));
            Text(61, 252, 730, 28, "FOUR CHAPTERS. ONE VERY LONG MISUNDERSTANDING.", micro, mint);
            int prologueSeconds = 0;
            foreach (var entry in story.prologue) prologueSeconds += Mathf.RoundToInt(entry.seconds * 10);
            if (Chapter("chapter-one", 1, 309, "THE TRANSMISSION", "1969 → 2026 / " + Mathf.RoundToInt(prologueSeconds / 10f) + " SECONDS")) Play("prologue");
            if (Chapter("chapter-two", 2, 417, "RECRUITMENT", "YOUR FIRST DAY AT THE DEPARTMENT")) Show(Page.Recruit);
            if (Chapter("chapter-three", 3, 525, "THE LABORATORY BRIEFING", "YOUR PLANET. TRY THE DIALS.")) Play("briefing", true);
            if (Chapter("chapter-four", 4, 633, "THE FINAL HEARING", "ENDING PREVIEW / NO PROGRESS REQUIRED")) Play("ending", true);
            Text(904, 252, 624, 28, "PLAYBACK SETTINGS", micro, orange);
            if (Toggle("toggle-captions", 308, "Dialogue captions", "KEEP THE CREW'S LINES ON SCREEN", captions)) { captions = !captions; SaveOptions(); }
            if (Toggle("toggle-descriptions", 417, "Scene descriptions", "DESCRIBE ACTION BETWEEN LINES", descriptions)) { descriptions = !descriptions; SaveOptions(); }
            if (Toggle("toggle-motion", 526, "Reduced motion", "STEADY CAMERAS / QUIETER MOVEMENT", reducedMotion)) { reducedMotion = !reducedMotion; SaveOptions(); }
            Text(905, 649, 624, 62, "SPACE  PAUSE     ← / →  SHOTS\nM  AUDIO        C  CAPTIONS     ESC  MENU", micro);
            Text(904, 722, 624, 24, "DISCOVERY CUTSCENES / PREVIEW", micro, orange);
            if (Link("cutscene-drought", new Rect(904, 750, 624, 35), "01  THE WELCOME BASKETS  (SOIL DROUGHT)  →")) Play("drought", true);
            if (Link("reset", new Rect(62, 755, 580, 35), "START A NEW OPERATION", orange))
                ConfirmOperationReset();
        }
        void CreditsGUI()
        {
            Header("THE PEOPLE / THE MATERIAL");
            Text(56, 148, 1100, 96, "CREDITS & SOURCE MATERIAL", Style(displayFont, 73, paper));
            Badge(62, 272, "CREW & STORY", orange);
            Text(60, 327, 650, 120, "COMMANDER ZORVAX\nDR. NYXEN  /  CADET B", Style(displayFont, 45, paper));
            Text(62, 467, 650, 100, "Original Homecoming story and rigged character assets from this project.\nNASA Space Apps Kuwait.", body);
            Text(62, 619, 650, 115, "TYPE / Barlow Condensed, Barlow, IBM Plex Mono.\nOpen Font License files are bundled with the fonts.\nIMPORT / Unity glTFast, Apache 2.0.", micro);
            Badge(870, 272, "ARCHIVAL MATERIAL", mint);
            Text(870, 329, 650, 225, "Apollo 11 television — NASA Apollo Lunar Surface Journal.\n\nMoon surface — NASA Goddard CGI Moon Kit.\n\nEarth — the existing KU Exchange globe textures.", body);
            Text(870, 619, 620, 82, "Original character clips are blended with authored acting. Dialogue is presented as subtitles.", body);
            if (Link("credits-back", new Rect(62, 760, 400, 35), "←  BACK TO TRANSMISSION", orange)) Show(Page.Home);
        }
        void ConfirmOperationReset()
        {
            operationResetError = null;
            operationResetReturnPage = page;
            Show(Page.Reset);
        }

        void ResetGUI()
        {
            Header("PERSONNEL / NEW OPERATION");
            Insignia(800, 226, 44, orange);
            Text(260, 323, 1080, 115, "REPLAY FROM START?", Style(displayFont, 83, paper, TextAnchor.MiddleCenter));
            Text(300, 461, 1000, 138,
                "This clears mission progress, saved experiments, and your casebook.",
                Style(bodyFont, 26, paper, TextAnchor.MiddleCenter, true));
            if (Action("reset-confirm", new Rect(365, 633, 410, 68), "Clear progress and replay", true))
            {
                try { StorySession.StartNewOperation(); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    operationResetError = "The operation could not be reset. " + error.Message;
                    return;
                }
                designation = "";
                Play("prologue");
                return;
            }
            if (Action("reset-cancel", new Rect(825, 633, 410, 68), "Keep progress")) Show(operationResetReturnPage);
            if (operationResetError != null)
                Text(300, 711, 1000, 85, operationResetError, Style(bodyFont, 22, orange, TextAnchor.MiddleCenter, true));
        }
    }
}
