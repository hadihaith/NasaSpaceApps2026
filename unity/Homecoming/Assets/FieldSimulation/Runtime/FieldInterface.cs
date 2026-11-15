using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        static readonly Color Ink = Rgb("#09171c"), Panel = Rgb("#10252b"), Line = Rgb("#36555b"),
            White = Rgb("#f2f4e5"), Dim = Rgb("#b3c8c8"), Mint = Rgb("#a6e9c7"),
            Yellow = Rgb("#ffcf79"), Aqua = Rgb("#75d8e2");
        static readonly Color ButtonFace = Rgb("#142d35"), ButtonHover = Rgb("#244751"),
            ButtonPressed = Rgb("#0e2229"), ChoiceFace = Rgb("#285354"), ChoiceHover = Rgb("#356366"),
            AccentHover = Rgb("#c1f5d8"), AccentPressed = Rgb("#82cba7"), DisabledFace = Rgb("#18282d");
        static readonly string[] Sites = { "Sonoran Desert", "Semi-arid savanna", "Tropical rainforest", "Coastal scrub", "Monsoon floodplain", "Eastern woodlands" };
        static readonly string[] Durations = { "1 month", "3 months", "12 months" };
        static readonly int[] Months = { 1, 3, 12 };
        static readonly string[] EvidenceKeys = { "rain", "soil", "temperature", "humidity", "wind" };
        static readonly string[] EvidenceNames = { "Rain", "Soil moisture", "Temperature", "Humidity", "Wind" };
        static readonly string[] WeatherIcons = { "Temperature", "Rain", "Droplet", "Wind" };
        GUIStyle text;
        Texture2D fade, sideFade;
        Texture2D dialFace;
        bool showSites, weatherLabOpen, labOptionsOpen;
        int hotSlider = -1;
        int hotDial = -1;
        float dialDrag;
        int dialStart;
        int focusIndex, controlIndex;
        readonly List<int> availableControls = new();
        bool tabMoved;
        Vector2 journalScroll;
        float fieldScale, fieldX, fieldY;
        static CultureInfo Invariant => CultureInfo.InvariantCulture;

        static Color Rgb(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static Color Alpha(Color c, float a) => new(c.r, c.g, c.b, a);
        static Rect Box(float x, float y, float w, float h) => new(x, y, w, h);
        static string Signed(float n, string format = "0") => (n > 0 ? "+" : "") + n.ToString(format, Invariant);
        static string Rate(float n) => Mathf.RoundToInt(n * 100).ToString(Invariant) + "%";

        void OnGUI()
        {
            if (data == null || save == null) return;
            if (view != View.Broadcast) ReleaseBroadcastControls();
            EnsureStyles();
            LoadInvestigation();
            if (!LegacyCapture && view != View.Report) orbitOpen = false;
            tabMoved = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab &&
                (!LegacyCapture || view != View.Field) && availableControls.Count > 0;
            if (tabMoved)
            {
                int position = keyboardFocus ? availableControls.IndexOf(focusIndex) : -1;
                if (position < 0) position = Event.current.shift ? 0 : availableControls.Count - 1;
                int delta = Event.current.shift ? -1 : 1;
                focusIndex = availableControls[(position + delta + availableControls.Count) % availableControls.Count];
                keyboardFocus = true;
                GUI.FocusControl("");
                Event.current.Use();
            }
            availableControls.Clear();
            controlIndex = 0;
            fieldScale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            fieldX = (Screen.width - 1600 * fieldScale) / 2;
            fieldY = (Screen.height - 900 * fieldScale) / 2;
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(fieldX, fieldY, 0), Quaternion.identity, new Vector3(fieldScale, fieldScale, 1));
            switch (view)
            {
                case View.Lab: DrawLab(); break;
                case View.Descent: DrawDescent(); break;
                case View.Field: if (LegacyCapture) DrawField(); else DrawScenarioField(); break;
                case View.Journal: DrawJournal(); break;
                case View.Archive: DrawArchive(); break;
                case View.Report: if (LegacyCapture) DrawReport(); else DrawInvestigation(); break;
                case View.SiteTransfer: DrawSiteTransfer(); break;
                case View.Submission: DrawSubmission(); break;
                case View.Broadcast: DrawBroadcast(); break;
            }
            if (demoActive) DrawDemoControls();
            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
            {
                Vector2 mouse = Event.current.mousePosition;
                var tip = Box(Mathf.Clamp(mouse.x-116,16,1352),Mathf.Clamp(mouse.y-47,12,850),232,36);
                Card(tip,Ink);
                Text(tip,GUI.tooltip,17,White,strongFont,TextAnchor.MiddleCenter);
            }
            GUI.matrix = matrix;
        }

        void EnsureStyles()
        {
            if (text != null) return;
            text = new GUIStyle { clipping = TextClipping.Clip };
            fade = new Texture2D(1, 96, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 96; y++) fade.SetPixel(0, y, new Color(1, 1, 1, (y / 95f) * (y / 95f)));
            fade.Apply();
            sideFade = new Texture2D(96, 96, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 96; x++)
            for (int y = 0; y < 96; y++)
            {
                float edge = Mathf.SmoothStep(0,1,y / 19f) * Mathf.SmoothStep(0,1,(95-y) / 19f);
                sideFade.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(1-x/95f,2)*edge));
            }
            sideFade.Apply();
        }

        void Fill(Rect rect, Color c)
        {
            var old = GUI.color;
            GUI.color = GuiTint(c);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Fade(Rect rect, Color c, float angle)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = GuiTint(c);
            var uv = angle == 180 ? new Rect(0,1,1,-1) : new Rect(0,0,1,1);
            GUI.DrawTextureWithTexCoords(rect, angle == 90 ? sideFade : fade, uv);
            GUI.color = old;
        }

        static Color GuiTint(Color color) => QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;

        void Text(Rect rect, string content, int size, Color c, Font font = null, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            text.font = font ?? bodyFont;
            text.fontSize = size;
            text.normal.textColor = c;
            text.alignment = anchor;
            text.wordWrap = wrap;
            text.Draw(rect, new GUIContent(content), false, false, false, false);
        }

        void Tag(float x, float y, string content, Color c) => Text(Box(x,y,720,23),content,15,c,dataFont);
        void Card(Rect r, Color background)
        {
            Fill(r, background);
            Fill(Box(r.x,r.y,r.width,1), Alpha(Line,.92f));
            Fill(Box(r.x,r.yMax-1,r.width,1), Alpha(Line,.80f));
        }

        bool Button(Rect rect, string label, bool accent = false, bool active = false, bool enabled = true,
            int size = 20, string icon = null, string tooltip = null)
        {
            var background = ButtonBackground(rect,accent,active,enabled);
            Color foreground = enabled ? accent ? Ink : White : Dim;
            var state = ButtonInput(rect, tooltip ?? (string.IsNullOrEmpty(label) ? icon : null), enabled);
            Fill(rect, background);
            DrawButtonEdge(rect, !enabled ? Line : state.focused ? Yellow : accent ? AccentPressed : active ? Mint : Line);
            if (icon == null) Text(rect,label,size,foreground,strongFont,TextAnchor.MiddleCenter);
            else
            {
                float x = string.IsNullOrEmpty(label) ? rect.center.x : rect.x + 23;
                DrawIcon(icon,new Vector2(x,rect.center.y),foreground);
                if (!string.IsNullOrEmpty(label))
                    Text(Box(rect.x+43,rect.y,rect.width-54,rect.height),label,size,foreground,strongFont);
            }
            return state.pressed;
        }

        Color ButtonBackground(Rect rect, bool accent, bool active, bool enabled)
        {
            if (!enabled) return DisabledFace;
            bool hover = rect.Contains(Event.current.mousePosition);
            if (hover && Input.GetMouseButton(0)) return accent ? AccentPressed : ButtonPressed;
            if (accent) return hover ? AccentHover : Mint;
            if (active) return hover ? ChoiceHover : ChoiceFace;
            return hover ? ButtonHover : ButtonFace;
        }

        void DrawButtonEdge(Rect rect, Color edge)
        {
            Fill(Box(rect.x,rect.y,rect.width,1),edge);
            Fill(Box(rect.x,rect.yMax-2,rect.width,2),edge);
            Fill(Box(rect.x,rect.y,1,rect.height),edge);
            Fill(Box(rect.xMax-1,rect.y,1,rect.height),edge);
        }

        (bool pressed, bool focused) ButtonInput(Rect rect, string tooltip = null, bool enabled = true)
        {
            int index = controlIndex++;
            if (enabled) availableControls.Add(index);
            bool focused = enabled && focusIndex == index && keyboardFocus;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && enabled;
            bool pressed = GUI.Button(rect, tooltip == null ? GUIContent.none : new GUIContent("",tooltip), GUIStyle.none);
            GUI.enabled = wasEnabled;
            if (pressed) { focusIndex = index; keyboardFocus = false; }
            if (wasEnabled && focused && Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter ||
                    (LegacyCapture && Event.current.keyCode == KeyCode.Space)))
            {
                Event.current.Use();
                pressed = true;
            }
            return (pressed,focused);
        }

        bool keyboardFocus;
        bool keyboardNavigation => keyboardFocus || !LegacyCapture;

        void Top(string screen)
        {
            Fade(Box(0,0,1600,156), Alpha(Ink,.90f), 0);
            Fill(Box(36,31,4,38), Mint);
            Text(Box(50,27,300,31),"HOMECOMING",28,White,headingFont);
            Tag(51,57,screen,Mint);
            if (view == View.Field || view == View.Descent || demoActive) return;
            if (StorySession.FiledReport != null && Button(Box(688,32,226,39),
                openedFromFiledReport ? "← Homecoming report" : "Homecoming report")) StorySession.ReturnToFiledReport();
            if (!LegacyCapture)
            {
                if (view == View.Lab && !weatherLabOpen)
                {
                    if (Button(Box(1509,28,55,49),"",active:labOptionsOpen,icon:"Settings")) labOptionsOpen = !labOptionsOpen;
                }
                else if (Button(Box(1354,32,210,42),"Field station",icon:"Back"))
                {
                    OpenStation();
                }
                return;
            }
            if (Button(Box(934,32,144,39),"Flight deck",false,view == View.Lab)) Go(View.Lab);
            if (Button(Box(1086,32,144,39),"Field journal",false,view == View.Journal)) Go(View.Journal);
            if (Button(Box(1238,32,144,39),"Earth archive",false,view == View.Archive)) OpenArchive(null);
            if (Button(Box(1390,32,174,39),LegacyCapture ? "Mission report" : "Investigation",false,view == View.Report))
            {
                if (LegacyCapture) Go(View.Report);
                else OpenCase();
            }
        }

        void OpenStation()
        {
            weatherLabOpen = labOptionsOpen = false;
            Go(View.Lab);
        }

        void OpenWeatherLab()
        {
            weatherLabOpen = true;
            labOptionsOpen = false;
            Go(View.Lab);
        }

        void DrawStation()
        {
            Top("FIELD STATION");
            Rect welcome = Box(54,172,667,451);
            Card(welcome,Ink);
            Fill(Box(welcome.x,welcome.y,76,3),Mint);
            var kind = investigation?.kind ?? InvestigationKind.Rainfall;
            Tag(87,203,investigation == null ? "YOUR FIRST ASSIGNMENT" : "CURRENT CASE",Mint);
            Text(Box(83,236,610,80),investigation == null ? "Investigate Earth" : CaseTitle(kind),62,White,headingFont);
            Text(Box(87,331,580,68),investigation == null
                ? "Guess how rainfall changed in two places, then check the records."
                : CaseQuestion,27,Dim,bodyFont,TextAnchor.UpperLeft,true);
            DrawCaseProgress(Box(93,406,574,70),kind);
            if (Button(Box(87,498,429,59),investigation == null ? "Start the rainfall case" : "Continue case",
                accent:true,size:27,icon:"Next"))
            {
                if (investigation == null) NewInvestigation(InvestigationKind.Rainfall);
                OpenCase();
            }
            if (Button(Box(87,570,216,37),"Browse all cases",icon:"Search")) OpenCasePicker();

            if (ControlTile(Box(54,648,157,114),"Weather","Weather lab")) OpenWeatherLab();
            if (ControlTile(Box(224,648,157,114),"Archive","Earth archive")) OpenArchive(null);
            if (ControlTile(Box(394,648,157,114),"Journal","Field journal")) Go(View.Journal);
            if (ControlTile(Box(564,648,157,114),"Signal","Return to Sender")) OpenBroadcast();
            if (ReadyForFinale && Button(Box(768,172,352,55),"Incoming · Commander Zorvax",accent:true,icon:"Next"))
                OpenSubmission();

            if (labOptionsOpen)
            {
                Card(Box(1184,86,380,162),Ink);
                if (Button(Box(1204,105,340,49),muted ? "Sound off" : "Sound on",active:!muted)) ToggleMute();
                if (Button(Box(1204,171,340,49),reduced ? "Reduced motion on" : "Reduced motion off",active:reduced)) ToggleMotion();
            }
        }

        bool ControlTile(Rect rect, string icon, string label, bool active = false, string tooltip = null)
        {
            var background = ButtonBackground(rect,false,active,true);
            var state = ButtonInput(rect,tooltip);
            Fill(rect,background);
            DrawButtonEdge(rect,state.focused ? Yellow : active ? Mint : Line);
            DrawIcon(icon,new Vector2(rect.center.x,rect.y+42),active ? White : Mint,2.4f);
            Text(Box(rect.x+3,rect.yMax-40,rect.width-6,33),label,20,White,strongFont,TextAnchor.MiddleCenter);
            return state.pressed;
        }

        void DrawIcon(string icon, Vector2 center, Color color, float scale = 1)
        {
            if (Event.current.type != EventType.Repaint) return;
            void Stroke(float x, float y, float endX, float endY) =>
                DrawChartSegment(center + scale*new Vector2(x,y),center + scale*new Vector2(endX,endY),
                    color,Mathf.Clamp(scale*1.6f,2,6));
            void Square(float x,float y,float width,float height)
            {
                Stroke(x,y,x+width,y); Stroke(x,y+height,x+width,y+height);
                Stroke(x,y,x,y+height); Stroke(x+width,y,x+width,y+height);
            }
            switch (icon)
            {
                case "Back":
                case "Next":
                    int d = icon == "Back" ? -1 : 1;
                    Stroke(-9,0,9,0); Stroke(2*d,-6,9*d,0); Stroke(9*d,0,2*d,6);
                    break;
                case "Choose":
                    Stroke(-6,-3,0,3); Stroke(0,3,6,-3);
                    break;
                case "Search":
                    Square(-9,-9,12,12); Stroke(4,4,10,10);
                    break;
                case "Location":
                    Stroke(-8,-7,8,-7); Stroke(-8,-7,-7,1); Stroke(8,-7,7,1);
                    Stroke(-7,1,0,9); Stroke(0,9,7,1);
                    Fill(Box(center.x-2*scale,center.y-4*scale,4*scale,4*scale),color);
                    break;
                case "Settings":
                    for (int i = 0; i < 3; i++)
                    {
                        float y = -7+i*7;
                        Stroke(-9,y,9,y);
                        Fill(Box(center.x+(i == 1 ? 2 : -7),center.y+y-3,5,6),color);
                    }
                    break;
                case "Weather":
                    Stroke(-10,7,-3,-6); Stroke(-3,-6,2,1); Stroke(2,1,6,-4); Stroke(6,-4,10,7);
                    Stroke(-10,7,10,7);
                    break;
                case "Archive":
                    Square(-9,-7,18,15); Stroke(-9,-2,9,-2); Stroke(-4,2,4,2);
                    break;
                case "Journal":
                    Square(-8,-9,16,18); Stroke(-3,-4,4,-4); Stroke(-3,1,4,1); Stroke(-3,5,4,5);
                    break;
                case "Rain":
                    Stroke(-9,-6,-2,-6); Stroke(1,-8,8,-8);
                    Stroke(-8,3,-5,-1); Stroke(-2,8,1,4); Stroke(4,2,7,-2); Stroke(7,8,10,4);
                    break;
                case "Droplet":
                    Stroke(0,-10,-7,0); Stroke(-7,0,-6,6); Stroke(-6,6,0,9);
                    Stroke(0,9,6,6); Stroke(6,6,7,0); Stroke(7,0,0,-10);
                    break;
                case "Wind":
                    Stroke(-10,-5,9,-5); Stroke(-6,0,10,0); Stroke(-10,5,4,5);
                    break;
                case "Temperature":
                    Square(-3,-10,6,15); Square(-6,3,12,7); Stroke(0,5,0,-5);
                    break;
                case "Soil":
                    Stroke(-10,-6,10,-6); Stroke(-10,1,10,1); Stroke(-10,8,10,8);
                    Stroke(-6,-6,-6,1); Stroke(4,1,4,8);
                    break;
                case "Chart":
                    Stroke(-9,-9,-9,8); Stroke(-9,8,10,8);
                    Stroke(-5,3,-1,-2); Stroke(-1,-2,3,0); Stroke(3,0,9,-8);
                    break;
                case "Check":
                    Stroke(-8,0,-2,6); Stroke(-2,6,9,-6);
                    break;
                case "Copy":
                    Square(-5,-4,14,13); Stroke(-9,-8,5,-8); Stroke(-9,-8,-9,5);
                    break;
                case "Signal":
                    Stroke(-7,8,0,-7); Stroke(0,-7,7,8); Stroke(-5,4,5,4);
                    Stroke(-5,-9,-9,-5); Stroke(5,-9,9,-5);
                    break;
                case "Play":
                    Stroke(-6,-8,8,0); Stroke(8,0,-6,8); Stroke(-6,8,-6,-8);
                    break;
                case "Pause":
                    Stroke(-4,-8,-4,8); Stroke(4,-8,4,8);
                    break;
                case "Last month":
                    Stroke(-8,-7,4,0); Stroke(4,0,-8,7); Stroke(-8,7,-8,-7); Stroke(8,-8,8,8);
                    break;
                case "Rise":
                    Stroke(-8,6,8,-6); Stroke(1,-6,8,-6); Stroke(8,-6,8,1);
                    break;
                case "Fall":
                    Stroke(-8,-6,8,6); Stroke(1,6,8,6); Stroke(8,6,8,-1);
                    break;
                case "Little change":
                    Stroke(-8,0,8,0);
                    break;
            }
        }

        void DrawLab()
        {
            if (!LegacyCapture && !weatherLabOpen)
            {
                DrawStation();
                return;
            }
            Top(LegacyCapture ? "FIELD SIMULATION" : "WEATHER LAB");
            Fade(Box(0,70,1020,428),Alpha(Ink,.69f),90);
            Tag(54,147,LegacyCapture ? "LANDING SITE  /  " + (siteIndex + 1).ToString("00") : "LANDING SITE",Mint);
            Text(Box(49,171,1040,109),Region.name,93,White,headingFont);
            Tag(55,291,Coordinates(Region) + "   /   " + Sites[siteIndex].ToUpperInvariant(),White);
            if (Button(Box(54,336,192,41),showSites ? "Close map" : "Change site",icon:"Location"))
            {
                showSites = !showSites;
                branchesOpen = branchComparisonOpen = false;
            }
            if (!LegacyCapture)
            {
                DrawBranches();
                if (!demoActive && IsGuidedScenario && Button(Box(523,336,193,41),"Back to case",icon:"Back")) OpenCase();
            }
            if (showSites)
            {
                DrawSitePicker(392,!LegacyCapture);
            }

            if (LegacyCapture)
            {
                Rect card = Box(1152,119,412,669);
                Card(card,Alpha(Ink,.93f));
                Fill(Box(card.x,card.y,54,3),Mint);
                Tag(1180,144,"SCENARIO",Mint);
                Text(Box(1180,171,355,53),"Weather controls",42,White,headingFont);
                Text(Box(1180,232,355,36),"Changes from the regional monthly normal",19,Dim);
                for (int i = 0; i < data.controls.Length; i++) Slider(i,1180,278+i*75,356);
                Tag(1180,589,"SIMULATED TIME",Dim);
                for (int i = 0; i < Months.Length; i++)
                    if (Button(Box(1180+i*120,620,115,40),Durations[i],false,duration == Months[i],true,17)) duration = Months[i];
                if (Button(Box(1180,687,355,57),"Begin descent   ↘",true,false,true,27)) Launch();
                if (Button(Box(1180,751,355,26),"Reset weather controls",false,false,true,16)) settings = default;
            }
            else DrawWeatherConsole();

            Fade(Box(0,772,1600,128),Alpha(Ink,.96f),180);
            Tag(54,836,"1991–2020 NASA POWER NORMALS   /   CONDITIONAL SCENARIOS",White);
            if (LegacyCapture)
            {
                if (Button(Box(1134,828,210,34),reduced ? "Motion reduced" : "Motion full",false,false,true,16)) ToggleMotion();
                if (Button(Box(1355,828,209,34),muted ? "Sound off" : "Sound on",false,false,true,16)) ToggleMute();
            }
        }

        void DrawSiteTransfer()
        {
            var destination = data.regions[transferTarget];
            if (transferVeil > 0)
                Fill(Box(0,0,1600,900),Alpha(Rgb("#adcfd5"),transferVeil));

            if (transferTime >= PullbackEnd && transferTime < OrbitExitEnd)
            {
                var from = new Vector2(Region.latitude,Region.longitude);
                var to = new Vector2(destination.latitude,destination.longitude);
                DrawTransferTrack(from,to);
                DrawFootprintPin(from,Region.name,White);
                DrawFootprintPin(to,destination.name,Mint);
            }

            Fade(Box(0,0,1600,208),Alpha(Ink,.96f),0);
            Fade(Box(0,682,1600,218),Alpha(Ink,.98f),180);
            Tag(53,34,"HOMECOMING  /  SITE TRANSFER",Mint);
            Text(Box(51,59,1225,56),Region.name+" → "+destination.name,44,White,headingFont);
            Text(Box(54,114,930,32),Coordinates(Region)+"     →     "+Coordinates(destination),19,Dim,dataFont);

            string phase = transferTime < PullbackEnd ? "Climbing from the survey site" :
                transferTime < OrbitEntryEnd ? "Establishing Earth orbit" :
                transferTime < OrbitTravelEnd ? "Crossing the globe" : "Approaching the next survey site";
            Text(Box(54,757,900,46),phase,28,White,headingFont);
            float progress = transferTime / TransferEnd;
            Fill(Box(54,825,965,3),Line);
            Fill(Box(54,825,965*progress,3),Mint);

            bool cancel = Button(Box(1095,797,202,49),"Cancel  /  Esc",false,false,true,21);
            bool skip = Button(Box(1311,797,235,49),"Skip flight  /  Space",true,false,true,21);
            if (cancel || skip) FinishSiteTransfer(skip);
        }

        void DrawTransferTrack(Vector2 from, Vector2 to)
        {
            if (Event.current.type != EventType.Repaint) return;
            const int segments = 40;
            for (int i = 0; i < segments; i+=2)
                if (world.ProjectFootprint(from,to,i/(float)segments,out var a) &&
                    world.ProjectFootprint(from,to,(i+.7f)/segments,out var b))
                    DrawChartSegment(ScreenToField(a),ScreenToField(b),Mint);
        }

        void DrawWeatherConsole()
        {
            Rect console = Box(1089,118,475,684);
            Card(console,Rgb("#203338"));
            Fill(Box(console.x+3,console.y+3,console.width-6,console.height-6),Rgb("#15272a"));
            PanelScrews(console);
            Tag(1114,136,"WEATHER LAB",Mint);
            Text(Box(1113,159,432,47),"Change the weather",39,White,headingFont);
            Text(Box(1115,205,430,27),"Drag a dial or use Tab and the arrow keys.",19,Dim);
            if (dialFace == null) dialFace = MakeDialFace();

            for (int i = 0; i < data.controls.Length; i++)
                DrawWeatherDial(i,1116+(i%2)*219,239+(i/2)*183);

            string blocker = GuidedLaunchBlocker();
            Text(Box(1117,605,417,62),blocker ?? $"Preview a year starting {Campaign.Current.NextDate:MMM yyyy}. Save the result after it plays.",
                18,Dim,bodyFont,TextAnchor.MiddleLeft,true);
            if (Button(Box(1116,678,419,51),"Preview 12 months",true,false,blocker == null,25)) LaunchScenario();
            if (Button(Box(1116,743,419,36),"Reset weather to saved values",false,false,Campaign != null,18)) Select(siteIndex);
            if (!showSites && !branchesOpen && !branchComparisonOpen) DrawWeatherMonitor();
        }

        void DrawWeatherMonitor()
        {
            Rect bezel = Box(54,462,852,288);
            Card(bezel,Rgb("#172a2b"));
            PanelScrews(bezel);
            Rect screen = Box(75,482,809,246);
            BeginWeatherScreen(screen);
            // The lab loads the source revision's next month into displayed.
            int month = displayed.monthIndex;
            var inheritedSettings = displayed.forcing;
            var normal = ClimateModel.Normal(Region,month);
            var inherited = ClimateModel.ApplyForcing(normal,inheritedSettings);
            var draft = ClimateModel.ApplyForcing(normal,settings);
            Tag(95,492,"LOCAL WEATHER MONITOR  /  "+FieldCalendar.Date(month).ToString("MMM yyyy",Invariant).ToUpperInvariant(),Mint);
            Text(Box(96,526,753,34),"Inherited weather → Draft weather",26,White,headingFont);

            for (int i = 0; i < data.controls.Length; i++)
            {
                var control = data.controls[i];
                float x = 96+(i%2)*389, y = 574+(i/2)*62;
                string unit = i switch { 0 => "°C", 1 => "mm/month", 2 => "% RH", _ => "m/s" };
                float WeatherValue(Weather weather) => i switch
                {
                    0 => weather.temperature, 1 => weather.rain, 2 => weather.humidity, _ => weather.wind
                };
                float beforeValue = WeatherValue(inherited), afterValue = WeatherValue(draft);
                double delta = Math.Round(afterValue-beforeValue,2);
                string dialChange = inheritedSettings[i] == settings[i] ? "" :
                    $"   ·   Dial {Signed(inheritedSettings[i])} → {Signed(settings[i])}{control.unit}";
                Text(Box(x,y,363,24),$"{control.label} ({unit}){dialChange}",16,Dim,strongFont);
                Text(Box(x,y+21,183,34),FormattableString.Invariant(
                    $"{beforeValue:0.##} → {afterValue:0.##}"),17,White,dataFont);
                string change = "Δ " + delta.ToString("+0.##;-0.##;0",Invariant);
                if (i == 2) change += " percentage points";
                Text(Box(x+184,y+21,179,34),change,17,delta == 0 ? Dim : Mint,
                    i == 2 ? strongFont : dataFont,TextAnchor.MiddleRight);
            }
            DrawWeatherBeam(Box(861,505,20,192));
        }

        void BeginWeatherScreen(Rect rect)
        {
            Fill(rect,Rgb("#081819"));
            for (float y = rect.y+5; y < rect.yMax; y+=6)
                Fill(Box(rect.x,y,rect.width,1),Alpha(Mint,.065f));
            Fill(Box(rect.x,rect.y,rect.width,2),Alpha(Mint,.3f));
            Fill(Box(rect.x,rect.yMax-2,rect.width,2),Alpha(Mint,.3f));
        }

        void DrawWeatherBeam(Rect tube)
        {
            if (Event.current.type != EventType.Repaint) return;
            Fill(tube,Ink);
            Fill(Box(tube.center.x-3,tube.y+17,6,tube.height-34),Alpha(Aqua,.18f));
            Fill(Box(tube.center.x-1,tube.y+17,2,tube.height-34),Aqua);
            float phase = reduced ? .44f : Mathf.Repeat(elapsed*.48f,1);
            float y = Mathf.Lerp(tube.y+18,tube.yMax-18,phase);
            Fill(Box(tube.center.x-5,y-4,10,8),Alpha(Aqua,.42f));
            Fill(Box(tube.center.x-2,y-2,4,4),White);
            Fill(Box(tube.x+2,tube.y+3,tube.width-4,12),Line);
            Fill(Box(tube.x+2,tube.yMax-15,tube.width-4,12),Line);
            Fill(Box(tube.x+5,tube.y+10,tube.width-10,3),Mint);
            Fill(Box(tube.x+5,tube.yMax-13,tube.width-10,3),Mint);
        }

        void DrawWeatherDial(int control, float x, float y)
        {
            var definition = data.controls[control];
            Color tint = Rgb(definition.color);
            Rect cell = Box(x,y,202,174);
            Vector2 center = new Vector2(x+101,y+103);
            Rect target = Box(center.x-57,center.y-57,114,114);
            int index = controlIndex++;
            availableControls.Add(index);
            bool focused = focusIndex == index && keyboardFocus;
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && target.Contains(evt.mousePosition))
            {
                Vector2 pointer = evt.mousePosition - center;
                focusIndex = index;
                keyboardFocus = true;
                if (pointer.magnitude > 33)
                {
                    float angle = Mathf.Atan2(pointer.x,-pointer.y)*Mathf.Rad2Deg;
                    float value = Mathf.Lerp(definition.min,definition.max,Mathf.InverseLerp(-135,135,angle));
                    settings[control] = DialValue(definition,value);
                }
                hotDial = control;
                dialStart = settings[control];
                dialDrag = 0;
                evt.Use();
            }
            if (hotDial == control && evt.type == EventType.MouseDrag && evt.button == 0)
            {
                dialDrag += evt.delta.x - evt.delta.y;
                settings[control] = DialValue(definition,dialStart+dialDrag*definition.step/7f);
                evt.Use();
            }
            if (hotDial == control && evt.rawType == EventType.MouseUp) hotDial = -1;
            if (focused && evt.type == EventType.KeyDown &&
                evt.keyCode is (KeyCode.LeftArrow or KeyCode.RightArrow or KeyCode.DownArrow or KeyCode.UpArrow))
            {
                int direction = evt.keyCode is (KeyCode.RightArrow or KeyCode.UpArrow) ? 1 : -1;
                settings[control] = DialValue(definition,settings[control]+direction*definition.step);
                evt.Use();
            }

            Card(cell,Rgb("#102024"));
            if (focused)
            {
                Fill(Box(x,y,cell.width,2),Yellow);
                Fill(Box(x,y,2,cell.height),Yellow);
            }
            DrawIcon(WeatherIcons[control],new Vector2(x+27,y+17),tint);
            Text(Box(x+48,y+4,146,26),definition.label,19,White,strongFont);
            Text(Box(x+6,y+27,190,23),Signed(settings[control])+" "+definition.unit.TrimStart(),19,tint,dataFont,TextAnchor.MiddleCenter);
            GUI.DrawTexture(target,dialFace);
            float fraction = Mathf.InverseLerp(definition.min,definition.max,settings[control]);
            Vector2 needle = DialDirection(Mathf.Lerp(-135,135,fraction));
            DrawChartSegment(center+needle*17,center+needle*33,focused ? Yellow : tint);
            Text(Box(x+8,y+144,57,25),Signed(definition.min),14,Dim,dataFont);
            Text(Box(x+137,y+144,57,25),Signed(definition.max),14,Dim,dataFont,TextAnchor.MiddleRight);
        }

        static int DialValue(ClimateControl definition, float value) =>
            Mathf.Clamp(Mathf.RoundToInt(value/definition.step)*definition.step,definition.min,definition.max);

        static Vector2 DialDirection(float angle)
        {
            float radians = angle*Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians),-Mathf.Cos(radians));
        }

        static Texture2D MakeDialFace()
        {
            const int size = 128;
            var face = new Texture2D(size,size,TextureFormat.RGBA32,false) { hideFlags = HideFlags.HideAndDontSave };
            var colors = new Color32[size*size];
            Color engraved = Rgb("#9bb8b4"), rim = Rgb("#0d191c"), grip = Rgb("#61706b"), top = Rgb("#3d4a46");
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x+.5f-size/2f,y+.5f-size/2f);
                float radius = point.magnitude;
                if (radius >= 63) continue;
                float angle = Mathf.Atan2(point.x,point.y);
                float degrees = angle*Mathf.Rad2Deg;
                Color pixel;
                if (radius > 48)
                {
                    bool tick = Mathf.Abs(degrees) <= 136 && Mathf.Abs(degrees/15-Mathf.Round(degrees/15)) < .08f;
                    pixel = tick && radius < 60 ? engraved : rim;
                }
                else
                {
                    float shade = .68f + .24f*point.normalized.y;
                    if (radius > 37) shade += .13f*Mathf.Cos(angle*26);
                    pixel = (radius > 35 ? grip : top)*shade;
                    pixel.a = 1;
                }
                colors[y*size+x] = pixel;
            }
            face.SetPixels32(colors);
            face.Apply(false,true);
            return face;
        }

        void PanelScrews(Rect panel)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                float x = (corner & 1) == 0 ? panel.x+9 : panel.xMax-16;
                float y = corner < 2 ? panel.y+9 : panel.yMax-16;
                Fill(Box(x,y,7,7),Rgb("#61716b"));
                Fill(Box(x+1,y+3,5,1),Ink);
            }
        }

        void Slider(int i,float x,float y,float width)
        {
            var c = data.controls[i];
            var tint = Rgb(c.color);
            Text(Box(x,y,250,30),c.label,22,White,strongFont);
            Text(Box(x+227,y,129,30),Signed(settings[i])+c.unit,18,tint,dataFont,TextAnchor.MiddleRight);
            var track = Box(x+4,y+45,width-8,3);
            var target = Box(x,y+27,width,34);
            int index = controlIndex++;
            availableControls.Add(index);
            bool focused = focusIndex == index && keyboardFocus;
            bool hover = target.Contains(Event.current.mousePosition);
            int change = settings[i];
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && hover)
            {
                focusIndex = index; keyboardFocus = false; hotSlider = i;
            }
            if (hotSlider == i && (evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag || evt.type == EventType.MouseUp) && evt.button == 0)
            {
                change = Mathf.RoundToInt(Mathf.Lerp(c.min,c.max,Mathf.InverseLerp(track.x,track.xMax,evt.mousePosition.x))/c.step)*c.step;
                if (evt.type == EventType.MouseUp) hotSlider = -1;
                evt.Use();
            }
            if (focused && evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow))
            {
                change += evt.keyCode == KeyCode.RightArrow ? c.step : -c.step;
                evt.Use();
            }
            settings[i] = Mathf.Clamp(change,c.min,c.max);
            Fill(track,Line);
            float zero = Mathf.Lerp(track.x,track.xMax,Mathf.InverseLerp(c.min,c.max,0));
            float selection = Mathf.Lerp(track.x,track.xMax,Mathf.InverseLerp(c.min,c.max,settings[i]));
            Fill(Box(Mathf.Min(zero,selection),track.y,Mathf.Abs(zero-selection),3),tint);
            Fill(Box(zero-1,track.y-3,2,9),Dim);
            Fill(Box(selection-5,track.y-5,10,13),focused ? Yellow : hover ? White : tint);
        }

        void DrawDescent()
        {
            Top("DESCENT");
            Fade(Box(0,680,1600,220),Alpha(Ink,.93f),180);
            Tag(54,736,descentClock < 1.4f ? "ORBITAL APPROACH" : "APPROACHING FIELD SITE",Mint);
            Text(Box(50,757,900,90),Region.name,73,White,headingFont);
            Tag(55,845,Coordinates(Region),White);
            if (Button(Box(1394,816,168,44),"Skip descent")) Arrive();
            if (descentClock < 1.1f)
            {
                Fill(Box(774,449,52,1),Alpha(Mint,.85f));
                Fill(Box(799,424,1,51),Alpha(Mint,.85f));
                Fill(Box(797,447,6,6),Mint);
                if (LegacyCapture) Tag(837,434,"TARGET  /  "+Coordinates(Region),Mint);
                else DrawDescentFootprint();
            }
            if (landingVeil > .001f) Fill(Box(0,0,1600,900),Alpha(Rgb("#c6e1dc"),landingVeil));
        }

        void DrawField()
        {
            Fade(Box(0,0,1600,197),Alpha(Ink,.89f),0);
            Fade(Box(0,733,1600,167),Alpha(Ink,.94f),180);
            Tag(42,33,paused ? "FIELD PLAYBACK PAUSED" : running ? "FIELD PLAYBACK" : comparing ? "BEFORE THIS RUN" : "FIELD OBSERVATION",Mint);
            Text(Box(40,55,770,62),Region.name,48,White,headingFont);
            Tag(44,119,Coordinates(Region),White);

            Card(Box(1166,30,396,75),Alpha(Ink,.83f));
            float p = running ? Mathf.Clamp01(runClock/RunTime) : 1;
            Text(Box(1181,38,245,27),comparing ? "BASELINE FOR THIS RUN" : "SIMULATED MONTH",13,Dim,dataFont);
            if (!comparing)
            {
                Text(Box(1378,39,166,24),Mathf.Clamp(Mathf.CeilToInt(p*duration),1,duration).ToString("00") + " / " + duration.ToString("00"),16,Mint,dataFont,TextAnchor.MiddleRight);
                Fill(Box(1181,77,365,3),Line);
                Fill(Box(1181,77,365*p,3),Mint);
            }
            DrawMetric(43,798,192,"ROOT-ZONE WATER",displayed.land.soil,Aqua);
            DrawMetric(247,798,192,"VEGETATION",displayed.land.vegetation,Mint);
            DrawMetric(451,798,192,"DROUGHT STRESS",displayed.land.drought,Yellow);
            Tag(44,867,"WASD  MOVE   /   RIGHT MOUSE  LOOK   /   TAB  WEATHER",White);
            if (showTelemetry) DrawLiveWeather();
            if (running)
            {
                if (Button(Box(1392,117,170,37),paused ? "Resume   /   Space" : "Pause   /   Space",false,false,true,17)) paused = !paused;
            }
            else
            {
                if (Button(Box(1353,117,209,37),comparing ? "Show modeled weather" : "Compare before run",false,comparing,true,17)) CompareBaseline();
                if (!comparing && !showResult && Button(Box(1380,163,182,37),"Show result",false,false,true,17)) showResult = true;
                if (Button(Box(1378,837,184,42),"Back to lab")) Go(View.Lab);
            }
            if (view != View.Field) return;
            if (!showResult)
            {
                WorldTag(world.RoverPosition + Vector3.up * 1.2f, "SURVEY ROVER");
                WorldTag(world.StationPosition + Vector3.up * 1.05f, "RELAY DISH");
                return;
            }
            DrawResult();
        }

        void DrawMetric(float x,float y,float w,string name,float value,Color color)
        {
            Card(Box(x,y,w,58),Alpha(Ink,.81f));
            Text(Box(x+10,y+5,w-20,22),name,12,Dim,dataFont);
            Text(Box(x+10,y+25,w-20,23),Rate(value),22,color,headingFont);
            Fill(Box(x+59,y+37,w-70,2),Line);
            Fill(Box(x+59,y+37,(w-70)*Mathf.Clamp01(value),2),color);
        }

        void DrawLiveWeather()
        {
            Card(Box(42,158,364,132),Alpha(Ink,.88f));
            Tag(57,169,"MODELED WEATHER",Mint);
            Text(Box(57,204,163,28),displayed.weather.temperature.ToString("0.0",Invariant)+" °C",23,White,headingFont);
            Text(Box(222,204,179,28),displayed.weather.rain.ToString("0",Invariant)+" mm/month",23,Aqua,headingFont);
            Text(Box(57,248,163,28),displayed.weather.humidity.ToString("0",Invariant)+"% humidity",23,White,headingFont);
            Text(Box(222,248,178,28),displayed.weather.wind.ToString("0.0",Invariant)+" m/s wind",23,White,headingFont);
        }

        void WorldTag(Vector3 point,string label)
        {
            Vector3 v = world.fieldCamera.WorldToScreenPoint(point);
            if (v.z < 1 || v.z > 55) return;
            float x = (v.x - fieldX)/fieldScale, y = (Screen.height - v.y - fieldY)/fieldScale;
            if (x < 120 || x > 1480 || y < 155 || y > 744) return;
            Fill(Box(x-66,y-34,132,25),Alpha(Ink,.82f));
            Text(Box(x-66,y-34,132,25),label,11,White,dataFont,TextAnchor.MiddleCenter);
            Fill(Box(x,y-9,1,13),Mint);
        }

        void DrawResult()
        {
            Rect r=Box(42,503,588,267);
            Card(r,Alpha(Ink,.94f));
            Fill(Box(r.x,r.y,58,3),Yellow);
            Tag(60,522,added.Count > 0 ? "NEW DISCOVERY" : primary == null ? "TRIAL COMPLETE" : "OBSERVED AGAIN",Yellow);
            string title = primary?.label ?? "No discovery triggered";
            Text(Box(58,545,555,62),title,title.Length > 27 ? 39 : 53,White,headingFont);
            if (commander != null && primary != null)
                GUI.DrawTexture(Box(56,610,56,56),commander,ScaleMode.ScaleToFit);
            if (primary != null)
            {
                Text(Box(121,612,481,18),"COMMANDER ZORVAX",12,Mint,dataFont);
                Text(Box(121,633,481,33),"“"+primary.quote+"”",19,Dim,bodyFont,TextAnchor.MiddleLeft,true);
            }
            else
                Text(Box(60,609,537,55),"Try changing one weather control and run the same site again.",21,Dim);
            if (trial.findings.Count > 1 && primary != null)
            {
                string also = string.Join(" · ",trial.findings.Where(id => id != primary.id).Select(id => data.discoveries.First(d => d.id == id).label));
                Text(Box(60,672,545,27),also,16,Mint);
            }
            if (Button(Box(60,712,160,40),"Keep exploring",true,false,true,17)) showResult = false;
            if (Button(Box(230,712,157,40),"Field journal",false,false,true,17)) Go(View.Journal);
            if (Button(Box(397,712,207,40),"Earth archive",false,false,true,17)) OpenArchive(primary?.evidence);
        }

        void Sheet(string tag,string title)
        {
            Fill(Box(-2200,-2200,6000,5000),Alpha(Ink,.89f));
            Top(tag);
            Text(Box(53,111,1469,92),title,75,White,headingFont);
        }

        void OpenSubmission()
        {
            reportStatus = null;
            Go(View.Submission);
            if (muted) return;
            var clip = Resources.Load<AudioClip>("HomecomingAudio/VoiceFallback/submission.01");
            if (clip == null) return;
            if (submissionVoice == null)
            {
                submissionVoice = gameObject.AddComponent<AudioSource>();
                submissionVoice.playOnAwake = false;
            }
            submissionVoice.PlayOneShot(clip);
        }

        AudioSource submissionVoice;

        void DrawSubmission()
        {
            Fill(Box(-2200,-2200,6000,5000), Alpha(Ink,.96f));
            Fill(Box(54,82,1490,2), Line);
            Tag(55,44,"INTERGALACTIC AFFECTION DEPARTMENT",Mint);
            if (commander != null) GUI.DrawTexture(Box(65,151,615,625),commander,ScaleMode.ScaleToFit);
            Tag(726,205,"INCOMING TRANSMISSION  /  COMMANDER ZORVAX",Yellow);
            string recruit = string.IsNullOrWhiteSpace(StorySession.RecruitName) ? "AGENT!" : $"AGENT {StorySession.RecruitName.ToUpperInvariant()}!";
            Text(Box(719,248,800,110),recruit,recruit.Length > 24 ? 56 : 72,White,headingFont);
            Text(Box(726,374,744,146),"“Agent! I believe we have enough evidence to begin the Great Homecoming!”",
                36,White,bodyFont,TextAnchor.UpperLeft,true);
            if (reportStatus != null)
                Text(Box(726,520,744,82),reportStatus,23,Yellow,bodyFont,TextAnchor.MiddleLeft,true);
            if (Button(Box(726,616,372,58),"Submit findings   →",true,false,ReadyForFinale && !hearingRequested,26))
            {
                investigation = FinaleCase;
                if (submissionVoice != null) submissionVoice.Stop();
                SubmitInvestigation();
            }
            if (Button(Box(726,698,372,50),"Continue experimenting")) Go(View.Lab);
        }

        void DrawJournal()
        {
            Sheet("DISCOVERY ENCYCLOPEDIA","Field journal");
            Tag(57,203,save.notes.Count + " / " + data.discoveries.Length + " PHENOMENA DISCOVERED",Mint);
            if (save.notes.Count == 0)
            {
                Text(Box(57,291,1100,50),"Complete a field run to record your first discovery.",28,White);
                return;
            }
            journalScroll = GUI.BeginScrollView(Box(52,256,455,560),journalScroll,Box(0,0,431,save.notes.Count*47));
            for (int i = 0; i < save.notes.Count; i++)
            {
                var item = data.discoveries.First(d => d.id == save.notes[i].id);
                if (Button(Box(0,i*47,423,42),item.label,false,noteIndex == i,true,19)) noteIndex = i;
            }
            GUI.EndScrollView();
            var note = save.notes[noteIndex];
            var entry = data.discoveries.First(d => d.id == note.id);
            var region = data.regions.First(r => r.id == note.region);
            Card(Box(528,255,1008,350),Panel);
            Tag(561,285,region.name.ToUpperInvariant()+"   /   RUN "+note.run.ToString("000"),Mint);
            Text(Box(558,322,933,72),entry.label,57,White,headingFont);
            Text(Box(562,407,885,94),entry.science,25,Dim,bodyFont,TextAnchor.UpperLeft,true);
            if (Button(Box(562,522,284,50),"Open Earth evidence   →",true))
            {
                Select(System.Array.IndexOf(data.regions,region));
                OpenArchive(entry.evidence);
            }
        }

        void DrawArchive()
        {
            Sheet("HISTORICAL EARTH DATA","Earth archive");
            if (LegacyCapture)
            {
                Tag(56,206,"ANNUAL VALUES  /  DESCRIPTIVE EVIDENCE",Mint);
                for (int i=0;i<data.regions.Length;i++)
                    if (Button(Box(56+i*250,247,240,40),data.regions[i].name,false,siteIndex == i,true,18))
                    {
                        Select(i); evidenceIndex = 0;
                    }
                for (int i=0;i<EvidenceKeys.Length;i++)
                    if (Button(Box(56+i*197,298,188,37),EvidenceNames[i],false,evidenceKey == EvidenceKeys[i],true,17))
                    {
                        evidenceKey = EvidenceKeys[i]; evidenceIndex = 0;
                    }
            }
            else if (DrawArchiveFilters()) return;
            var records = data.evidence.Where(e => e.region == Region.id && (evidenceKey == null || e.key == evidenceKey)).ToArray();
            if (!LegacyCapture)
            {
                DrawArchiveRecord(records);
                return;
            }
            if (records.Length == 0)
            {
                Text(Box(56,378,1300,60),"No matching record for this site. Choose another variable.",27,White);
                return;
            }
            var record = records[evidenceIndex];
            Card(Box(56,353,1488,452),Panel);
            Text(Box(84,371,1050,51),record.variable,37,White,headingFont);
            Tag(85,423,record.product,Mint);
            if (records.Length > 1)
            {
                if (Button(Box(1234,375,45,38),"‹")) evidenceIndex = (evidenceIndex+records.Length-1)%records.Length;
                Text(Box(1281,375,194,38),"Record "+(evidenceIndex+1)+" / "+records.Length,19,Dim,dataFont,TextAnchor.MiddleCenter);
                if (Button(Box(1475,375,45,38),"›")) evidenceIndex = (evidenceIndex+1)%records.Length;
            }
            DrawChart(record,Box(173,474,1319,178));
            Text(Box(86,696,1363,29),record.aggregation,19,Dim);
            if (record.hasSlope)
                Text(Box(86,735,986,37),"Theil–Sen slope  "+Signed(record.slope,"0.###")+" "+record.unit+" per year   /   Significance not established",20,White);
            bool filed = save.evidence.Contains(record.id);
            if (Button(Box(1241,738,270,47),filed ? "Evidence filed" : "File this evidence",true,false,!filed && record.years.Length > 1))
            {
                save.evidence.Add(record.id);
                Persist();
            }
            if (Button(Box(56,828,200,38),"Open data source",false,false,true,17)) Application.OpenURL(record.url);
            Tag(282,835,"HISTORICAL RECORDS ARE STORED SEPARATELY FROM SIMULATION RESULTS",Dim);
        }

        void DrawChart(EvidenceSeries record,Rect r, float? minimum = null, float? maximum = null, Color? color = null,
            float tickWidth = 86, int selectedIndex = -1)
        {
            if (record.years.Length < 2) return;
            int first = record.years[0].year, last = record.years[^1].year;
            float min = minimum ?? record.years.Min(point => point.value), max = maximum ?? record.years.Max(point => point.value);
            if (max <= min) { min -= 1; max += 1; }
            for (int i = 0;i < 4;i++)
            {
                float y = r.y + r.height*i/3;
                Fill(Box(r.x,y,r.width,1),Line);
                Text(Box(r.x-tickWidth-12,y-12,tickWidth,24),Mathf.Lerp(max,min,i/3f).ToString("0.##",Invariant),14,Dim,dataFont,TextAnchor.MiddleRight);
            }
            Vector2 prev = default;
            int lastYear = int.MinValue;
            if (selectedIndex >= 0)
            {
                float x = Mathf.Lerp(r.x,r.xMax,Mathf.InverseLerp(first,last,record.years[selectedIndex].year));
                Fill(Box(x,r.y,2,r.height),Yellow);
            }
            foreach (var p in record.years)
            {
                var v = new Vector2(Mathf.Lerp(r.x,r.xMax,Mathf.InverseLerp(first,last,p.year)),Mathf.Lerp(r.yMax,r.y,Mathf.InverseLerp(min,max,p.value)));
                if (p.year == lastYear + 1)
                {
                    DrawChartSegment(prev, v, color ?? Aqua);
                }
                Fill(Box(v.x-2,v.y-2,4,4),White);
                prev = v; lastYear = p.year;
            }
            Text(Box(r.x,r.yMax+6,110,25),first.ToString(),15,Dim,dataFont);
            Text(Box(r.xMax-110,r.yMax+6,110,25),last.ToString(),15,Dim,dataFont,TextAnchor.MiddleRight);
        }

        void DrawChartSegment(Vector2 start, Vector2 end, Color color, float thickness = 2)
        {
            if (Event.current.type != EventType.Repaint) return;
            // Translate in GUI layout coordinates, then rotate about the translated origin.
            var previous = GUI.matrix;
            var delta = end - start;
            var transform = Matrix4x4.TRS(start, Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg), Vector3.one);
            GUI.matrix = previous * transform;
            Fill(Box(0,-thickness/2,delta.magnitude,thickness),color);
            GUI.matrix = previous;
        }

        void DrawReport()
        {
            Sheet("FINAL HEARING","Mission report");
            string recruit = StorySession.RecruitName;
            if (!string.IsNullOrWhiteSpace(recruit)) Text(Box(57,207,1000,34),"Prepared by "+recruit,23,Dim);
            Card(Box(56,272,705,315),Panel);
            Card(Box(786,272,758,315),Panel);
            Tag(81,299,"FIELD DISCOVERIES",Mint);
            Tag(811,299,"FILED EARTH EVIDENCE",Mint);
            Text(Box(80,328,655,92),save.notes.Count+" found",74,White,headingFont);
            Text(Box(809,328,657,92),save.evidence.Count+" filed",74,White,headingFont);
            Text(Box(81,444,653,87),string.Join(" · ",save.notes.Select(n => data.discoveries.First(d => d.id == n.id).label)),22,Dim,bodyFont,TextAnchor.UpperLeft,true);
            Text(Box(811,444,704,87),string.Join(" · ",save.evidence.Select(id => data.evidence.First(e => e.id == id).variable).Distinct()),22,Dim,bodyFont,TextAnchor.UpperLeft,true);
            Text(Box(57,633,1410,44),CanSubmit ? "Your report is ready for the hearing." : "Discover "+data.requiredDiscoveries+" phenomena and file at least one historical Earth record.",25,White);
            if (Button(Box(56,709,311,54),"Submit findings   →",true,false,CanSubmit,24)) StorySession.ReturnFromSimulation();
        }

        static string Coordinates(RegionClimate r) => $"{Mathf.Abs(r.latitude):0.#}° {(r.latitude < 0 ? "S" : "N")}   /   {Mathf.Abs(r.longitude):0.#}° {(r.longitude < 0 ? "W" : "E")}";
    }
}
