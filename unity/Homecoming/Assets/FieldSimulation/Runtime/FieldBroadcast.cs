using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        enum BroadcastStep { Interrupt, Instrument, Receiver, Records, Difference, Rehearsal, Contact }
        enum BroadcastHeadline { Unselected, Locations, Difference }

        const string BroadcastPrefs = StorySession.BroadcastProgressKey;
        const float ReplyDelay = 1.65f;
        static readonly Color BroadcastRed = Rgb("#ff906c"), Brass = Rgb("#f3bc68"),
            Studio = Rgb("#081418"), ConsoleFace = Rgb("#16292f"), PrintPaper = Rgb("#ebdfbf"),
            PaperInk = Rgb("#24332e"), PaperGrid = Rgb("#c9bea3"), SahelInk = Rgb("#056c72"),
            ChileInk = Rgb("#9e472e");
        static readonly (string Icon, string Label)[] BroadcastStages =
            { ("Signal","Cut the feed"), ("Location","Tune receivers"), ("Chart","Check evidence"), ("Next","Send to Earth") };
        static readonly string[] BroadcastInstruments = { "Temperature", "Rainfall", "Wind speed" };
        static readonly string[] BroadcastTargets = { "Central Arizona", "Central Sahel", "Central Chile" };
        static readonly string[] BroadcastCoordinates = { "32.5–34.5°N area", "14°N · 6°E", "36°S · 72°W" };
        BroadcastStep broadcast;
        BroadcastHeadline broadcastHeadline;
        Texture2D broadcastEarth;
        AudioSource broadcastAudio;
        string broadcastCorrection;
        float broadcastSince, printSince, receiptSince, broadcastLeverPull;
        int broadcastYear, broadcastDragControl, broadcastLeverControl;
        bool broadcastReply;

        void OpenBroadcast()
        {
            CheckGuidedRecords();
            Go(View.Broadcast);
            broadcastEarth ??= Resources.Load<Texture2D>("Film/earth");
            var saved = PlayerPrefs.GetInt(BroadcastPrefs);
            broadcast = (BroadcastStep)Mathf.Clamp(saved % 10, 0, (int)BroadcastStep.Contact);
            broadcastHeadline = (BroadcastHeadline)Mathf.Clamp(saved / 10, 0, (int)BroadcastHeadline.Difference);
            broadcastSince = printSince = receiptSince = elapsed - 20;
            broadcastCorrection = null;
            broadcastYear = (guidedSahel?.years.Length ?? 1) - 1;
            broadcastReply = broadcast == BroadcastStep.Contact;
            keyboardFocus = false;
        }

        void AdvanceBroadcast(BroadcastStep next, string sound)
        {
            broadcast = next;
            broadcastSince = elapsed;
            broadcastReply = false;
            broadcastCorrection = null;
            ReleaseBroadcastControls();
            keyboardFocus = false;
            PlayerPrefs.SetInt(BroadcastPrefs, (int)next + 10 * (int)broadcastHeadline);
            PlayerPrefs.Save();
            PlayBroadcastSound(sound);
        }

        void ReleaseBroadcastControls()
        {
            if (broadcastDragControl == 0 && broadcastLeverControl == 0) return;
            if (GUIUtility.hotControl == broadcastDragControl || GUIUtility.hotControl == broadcastLeverControl)
                GUIUtility.hotControl = 0;
            broadcastDragControl = broadcastLeverControl = 0;
            broadcastLeverPull = 0;
        }

        void PlayBroadcastSound(string sound)
        {
            if (muted) return;
            var clip = Resources.Load<AudioClip>("HomecomingAudio/" + sound);
            if (clip == null) return;
            if (broadcastAudio == null)
            {
                broadcastAudio = gameObject.AddComponent<AudioSource>();
                broadcastAudio.playOnAwake = false;
                broadcastAudio.spatialBlend = 0;
            }
            broadcastAudio.PlayOneShot(clip, .55f);
        }

        void TickBroadcast()
        {
            if (broadcast != BroadcastStep.Contact || broadcastReply || elapsed - broadcastSince < ReplyDelay) return;
            broadcastReply = true;
            receiptSince = elapsed;
            PlayBroadcastSound("broadcast-reply");
            PlayBroadcastSound("party-hit");
        }

        void DrawBroadcast()
        {
            Fill(Box(0,0,1600,900), Studio);
            Fill(Box(0,0,1600,102), ConsoleFace);
            Fill(Box(0,101,1600,1), Line);
            Tag(55,20,"VESSEL 03 / COMMUNICATIONS", Mint);
            Text(Box(52,41,720,52),"Return to Sender",42,White,headingFont);
            if (guidedRain == null)
            {
                Text(Box(55,230,1130,155),"The paired rainfall archive is unavailable. Return to the field station for the other cases.",
                    35,White,bodyFont,TextAnchor.MiddleLeft,true);
                if (Button(Box(1364,35,183,42),"Field station",icon:"Back")) OpenStation();
                return;
            }

            int chapter = broadcast switch
            {
                BroadcastStep.Interrupt => 0,
                BroadcastStep.Instrument or BroadcastStep.Receiver => 1,
                BroadcastStep.Records or BroadcastStep.Difference => 2,
                _ => 3
            };
            DrawProgress(Box(52,111,1496,70),BroadcastStages,broadcastReply ? BroadcastStages.Length : chapter);

            switch (broadcast)
            {
                case BroadcastStep.Interrupt: DrawBroadcastInterrupt(); break;
                case BroadcastStep.Instrument: DrawBroadcastTuning(false); break;
                case BroadcastStep.Receiver: DrawBroadcastTuning(true); break;
                case BroadcastStep.Records: DrawBroadcastRecords(false); break;
                case BroadcastStep.Difference: DrawBroadcastRecords(true); break;
                case BroadcastStep.Rehearsal: DrawBroadcastRehearsal(); break;
                case BroadcastStep.Contact: DrawBroadcastContact(); break;
            }
            Fill(Box(55,837,1490,1), Line);
            Tag(55,853,"TAB / CHOOSE     ENTER / ACTIVATE     ESC / FIELD STATION", Dim);
            if (Button(Box(1364,35,183,42),"Field station",icon:"Back")) OpenStation();
            if (Button(Box(1195,847,134,39),muted ? "Sound off" : "Sound on",active:!muted,size:17)) ToggleMute();
            if (Button(Box(1342,847,203,39),reduced ? "Reduced motion on" : "Reduced motion off",active:reduced,size:17)) ToggleMotion();
        }

        void BroadcastTitle(string title, string context)
        {
            Text(Box(52,171,1492,79),title,66,White,headingFont);
            Text(Box(56,253,1484,50),context,25,Dim,bodyFont,TextAnchor.UpperLeft,true);
        }

        void DrawBroadcastInterrupt()
        {
            BroadcastTitle("Stop that broadcast.", "Zorvax has waited 57 years for Earth to call. He's about to invite the whole planet.");
            Card(Box(55,328,970,470),ConsoleFace);
            Tag(84,355,"ZORVAX / DRAFT INVITATION",BroadcastRed);
            Text(Box(78,394,886,184),"EARTH IS RUNNING\nOUT OF RAIN.",79,White,headingFont,TextAnchor.UpperLeft,true);
            Text(Box(86,599,870,50),"We have prepared guest rooms.",29,Brass,bodyFont);
            Fill(Box(86,689,898,1),Line);
            Text(Box(86,712,898,54),"NYXEN    Where, exactly, did we check?",26,Mint,strongFont);
            if (DrawBroadcastLever(Box(1050,328,495,470),"ARMED","Cut the feed",BroadcastRed))
                AdvanceBroadcast(BroadcastStep.Instrument,"rattle");
        }

        void DrawBroadcastTuning(bool receiver)
        {
            BroadcastTitle(receiver ? "Find a second witness." : "Trace the headline.",
                receiver ? "Nyxen found another grid point with the same rainfall record and years. Tune to 14°N, 6°E."
                    : "The signal came from one grid point in central Chile. Pick the instrument that checks a claim about rain.");
            DrawBroadcastMap(Box(55,328,872,436),receiver);
            Card(Box(955,328,591,436),ConsoleFace);
            Tag(982,350,receiver ? "RECEIVER B / SEEKING A SECOND GRID POINT" : "RECEIVER A / CENTRAL CHILE",Brass);
            for (int i = 0; i < 3; i++)
            {
                float y = 392 + i * 74;
                string label = receiver ? $"{BroadcastTargets[i]}    {BroadcastCoordinates[i]}" : BroadcastInstruments[i];
                if (!Button(Box(980,y,539,57),label,size:25,icon:receiver ? "Location" : "Next")) continue;
                if (i == 1)
                {
                    AdvanceBroadcast(receiver ? BroadcastStep.Records : BroadcastStep.Receiver,receiver ? "projector" : "detent");
                    printSince = elapsed;
                }
                else
                {
                    broadcastCorrection = receiver
                        ? i == 0 ? "NYXEN    That's an area average. Try the other grid point." : "CADET B    That's our first witness. The new pin is north of the equator."
                        : i == 0 ? "NYXEN    That's a thermometer. We need precipitation." : "CADET B    We can hear the wind. How much rain fell?";
                    PlayBroadcastSound("detent");
                }
            }
            Text(Box(981,627,537,110),broadcastCorrection ?? (receiver
                ? "Match the place and the record. Then compare the same years."
                : "ZORVAX    It sounded more impressive when I left off the coordinates."),
                24,broadcastCorrection == null ? Dim : Brass,bodyFont,TextAnchor.UpperLeft,true);
            Tag(55,787,"HISTORICAL RECORD / NASA POWER · MERRA-2",Mint);
        }

        void DrawBroadcastMap(Rect map, bool showBoth)
        {
            if (broadcastEarth != null && Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(map,broadcastEarth,ScaleMode.StretchToFill);
                Fill(map,Alpha(Studio,.32f));
            }
            else Fill(map,ConsoleFace);
            for (int i = 1; i < 6; i++) Fill(Box(map.x + map.width * i/6,map.y,1,map.height),Alpha(Mint,.23f));
            for (int i = 1; i < 4; i++) Fill(Box(map.x,map.y + map.height * i/4,map.width,1),Alpha(Mint,.23f));
            DrawBroadcastPin(map,guidedChile,"A  CENTRAL CHILE",BroadcastRed);
            if (showBoth) DrawBroadcastPin(map,guidedSahel,"B  CENTRAL SAHEL",Mint);
        }

        void DrawBroadcastPin(Rect map, EvidenceSeries record, string label, Color color)
        {
            var point = Exported(record).footprint.point;
            float x = map.x + (point.longitude + 180)/360 * map.width;
            float y = map.y + (90 - point.latitude)/180 * map.height;
            float pulse = reduced ? 8 : 8 + 5 * (.5f + .5f * Mathf.Sin(elapsed * 4));
            Fill(Box(x-pulse,y-pulse,pulse*2,pulse*2),Alpha(color,.3f));
            Fill(Box(x-3,y-3,6,6),color);
            Fill(Box(x+9,y-17,169,34),Studio);
            Text(Box(x+16,y-17,155,34),label,15,color,dataFont);
        }

        void DrawBroadcastRecords(bool difference)
        {
            BroadcastTitle(difference ? "Pull the third strip." : "Two places. One measure.",
                difference ? "For each year, subtract the Chile value from the Sahel value. Then test the trend in that difference."
                    : $"The receivers found {guidedSahel.years.Length} matching years. Drag the year cursor to compare the same year at both points.");
            Rect printer = Box(55,327,922,486);
            Card(printer,ConsoleFace);
            Fill(Box(73,346,886,16),Studio);

            float age = elapsed - printSince;
            float fraction = reduced ? 1 : Mathf.SmoothStep(0,1,Mathf.Clamp01(age / .48f));
            var paper = Box(84,355,864,440);
            Fill(Box(paper.x,paper.y,paper.width,paper.height*fraction),PrintPaper);
            if (fraction >= 1) DrawBroadcastChart(paper,difference);

            Card(Box(1005,327,540,486),ConsoleFace);
            if (difference)
            {
                var test = guidedRain.difference.test;
                Tag(1032,349,"REVIEWED TEST / SAHEL MINUS CHILE",Mint);
                Text(Box(1028,393,490,64),$"{Signed(test.slopePerDecade,"0.00")} per decade",46,White,headingFont);
                Text(Box(1032,459,489,37),"mm/month · two-sided OLS trend test",21,Dim,bodyFont);
                Text(Box(1032,503,489,62),$"95% interval  {Signed(test.lowerPerDecade,"0.00")} to {Signed(test.upperPerDecade,"0.00")}\np = {test.p.ToString("G3",Invariant)}  ·  threshold {test.alpha.ToString("0.##",Invariant)}",
                    19,White,dataFont,TextAnchor.UpperLeft,true);
                Text(Box(1032,582,488,107),"The rise in the difference is statistically significant. Significance remains unestablished for each individual trend.",
                    24,Mint,bodyFont,TextAnchor.UpperLeft,true);
                if (Button(Box(1031,731,487,55),"Build the broadcast",true,size:24,icon:"Next"))
                    AdvanceBroadcast(BroadcastStep.Rehearsal,"detent");
            }
            else
            {
                Tag(1032,352,"RAINFALL / DESCRIPTIVE TRENDS",Brass);
                Text(Box(1032,390,489,54),$"Sahel  {Signed(guidedRain.left.slopePerDecade,"0.00")}",35,Mint,headingFont);
                Text(Box(1032,449,489,54),$"Chile   {Signed(guidedRain.right.slopePerDecade,"0.00")}",35,BroadcastRed,headingFont);
                Text(Box(1032,505,489,38),"mm/month per decade · Theil–Sen",21,Dim,bodyFont);
                Text(Box(1032,557,483,129),"ZORVAX    So the rain has… chosen a side?\nNYXEN    Let's test the difference.",24,White,bodyFont,TextAnchor.UpperLeft,true);
                if (Button(Box(1031,731,487,55),"Print the difference",true,size:24,icon:"Next"))
                {
                    AdvanceBroadcast(BroadcastStep.Difference,"paper");
                    printSince = elapsed;
                }
            }
        }

        void DrawBroadcastChart(Rect paper, bool difference)
        {
            Rect OnPaper(float x, float y, float width, float height) => Box(paper.x+x,paper.y+y,width,height);
            Tag(paper.x+25,paper.y+17,difference ? "SAHEL MINUS CHILE" : "SAHEL / SOLID MARKERS     CHILE / HOLLOW MARKERS",PaperInk);
            Text(OnPaper(25,46,812,30),$"{Period(guidedSahel)}  /  YEARLY MEAN OF MONTHLY RAINFALL TOTALS  /  mm/month",17,PaperInk,dataFont);
            var sahel = guidedSahel.years;
            var chile = guidedChile.years;
            var delta = guidedRain.difference.points;
            float min = difference ? Mathf.Floor(Mathf.Min(0,delta.Min(p => p.value))/20)*20 : 0;
            float max = difference ? Mathf.Ceil(Mathf.Max(0,delta.Max(p => p.value))/20)*20
                : Mathf.Ceil(Mathf.Max(sahel.Max(p => p.value),chile.Max(p => p.value))/20)*20;
            Rect plot = OnPaper(65,100,768,193);
            int last = sahel.Length - 1;
            float X(int index) => Mathf.Lerp(plot.x,plot.xMax,(float)index/last);
            float Y(float value) => Mathf.Lerp(plot.yMax,plot.y,Mathf.InverseLerp(min,max,value));

            var slider = Box(plot.x,plot.y,plot.width,plot.height+38);
            int id = GUIUtility.GetControlID(FocusType.Passive,slider);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && slider.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = broadcastDragControl = id;
                e.Use();
                broadcastYear = Mathf.RoundToInt(Mathf.InverseLerp(plot.x,plot.xMax,e.mousePosition.x)*last);
            }
            else if (GUIUtility.hotControl == id && broadcastDragControl == id)
            {
                if (e.type == EventType.MouseDrag)
                {
                    broadcastYear = Mathf.RoundToInt(Mathf.InverseLerp(plot.x,plot.xMax,e.mousePosition.x)*last);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = broadcastDragControl = 0;
                    e.Use();
                }
            }

            for (int i = 0; i <= 4; i++)
            {
                float tick = Mathf.Lerp(min,max,i/4f);
                Fill(Box(plot.x,Y(tick),plot.width,1),PaperGrid);
                Text(Box(paper.x+7,Y(tick)-12,46,24),tick.ToString("0",Invariant),15,PaperInk,dataFont,TextAnchor.MiddleRight);
            }
            if (difference) Fill(Box(plot.x,Y(0),plot.width,1),PaperInk);
            Fill(Box(X(broadcastYear)-1,plot.y,2,plot.height),Alpha(PaperInk,.55f));
            for (int i = 0; i <= last; i++)
            {
                if (difference)
                {
                    InkPoint(delta[i].value,i == 0 ? delta[0].value : delta[i-1].value,SahelInk,false);
                }
                else
                {
                    InkPoint(sahel[i].value,i == 0 ? sahel[0].value : sahel[i-1].value,SahelInk,false);
                    InkPoint(chile[i].value,i == 0 ? chile[0].value : chile[i-1].value,ChileInk,true);
                }
                void InkPoint(float value, float previous, Color color, bool hollow)
                {
                    float x = X(i), y = Y(value);
                    if (i != 0) DrawChartSegment(new Vector2(X(i-1),Y(previous)),new Vector2(x,y),color);
                    Fill(Box(x-2,y-2,4,4),color);
                    if (hollow) Fill(Box(x-1,y-1,2,2),PrintPaper);
                    if (i == broadcastYear) DrawButtonEdge(Box(x-6,y-6,12,12),color);
                }
            }
            Text(OnPaper(65,297,190,28),sahel[0].year.ToString(Invariant),16,PaperInk,dataFont);
            Text(OnPaper(643,297,190,28),sahel[last].year.ToString(Invariant),16,PaperInk,dataFont,TextAnchor.MiddleRight);
            Fill(OnPaper(0,346,864,94),PaperGrid);
            if (Button(OnPaper(25,370,56,42),"‹",enabled:broadcastYear>0,size:26)) broadcastYear--;
            if (Button(OnPaper(783,370,56,42),"›",enabled:broadcastYear<last,size:26)) broadcastYear++;
            string readout = difference ? $"Difference {Signed(delta[broadcastYear].value,"0.0")} mm/month"
                : $"Sahel {sahel[broadcastYear].value:0.0}   /   Chile {chile[broadcastYear].value:0.0} mm/month";
            Text(OnPaper(93,370,678,42),$"{sahel[broadcastYear].year}    {readout}",23,PaperInk,dataFont,TextAnchor.MiddleCenter);
        }

        void DrawBroadcastRehearsal()
        {
            BroadcastTitle("You're the producer.", "Choose which finding opens the broadcast. The crew will read the headline you approve.");
            Card(Box(55,325,1030,480),ConsoleFace);
            Tag(82,350,"ZORVAX / WAITING FOR A HEADLINE",Brass);
            if (Button(Box(83,397,974,60),"Earth is running out of rain.",size:25))
            {
                SetBroadcastHeadline(BroadcastHeadline.Unselected);
                broadcastCorrection = "NYXEN    Where did we actually check? Put those two pins in the headline.";
                PlayBroadcastSound("sputter");
            }
            if (Button(Box(83,472,974,60),"Rainfall trends point in opposite directions at two grid points.",
                active:broadcastCorrection == null && broadcastHeadline == BroadcastHeadline.Locations,size:24))
                SetBroadcastHeadline(BroadcastHeadline.Locations);
            if (Button(Box(83,547,974,60),"The Sahel-minus-Chile rainfall difference rose.",
                active:broadcastCorrection == null && broadcastHeadline == BroadcastHeadline.Difference,size:24))
                SetBroadcastHeadline(BroadcastHeadline.Difference);

            Text(Box(84,631,964,66),broadcastCorrection ?? (broadcastHeadline switch
                {
                    BroadcastHeadline.Difference => "NYXEN    Air the paired test. It tells us how the difference changed.",
                    BroadcastHeadline.Locations => "NYXEN    Show both records. Describe the directions and keep the limits in the caption.",
                    _ => "CADET B    Both rolls of paper are ready. Which one goes in front of the camera?"
                }),
                24,broadcastCorrection == null ? Mint : Brass,bodyFont,TextAnchor.UpperLeft,true);
            if (Button(Box(83,731,974,46),"Review the two records",size:20,icon:"Back"))
                AdvanceBroadcast(BroadcastStep.Records,"paper");

            bool ready = broadcastHeadline != BroadcastHeadline.Unselected;
            if (DrawBroadcastLever(Box(1110,325,435,480),ready ? "READY" : "CHECK HEADLINE",
                "Send to Earth",Mint,ready))
                AdvanceBroadcast(BroadcastStep.Contact,"projector");
        }

        void SetBroadcastHeadline(BroadcastHeadline headline)
        {
            broadcastHeadline = headline;
            broadcastCorrection = null;
            PlayerPrefs.SetInt(BroadcastPrefs,(int)broadcast + 10 * (int)headline);
            PlayerPrefs.Save();
            if (headline != BroadcastHeadline.Unselected) PlayBroadcastSound("detent");
        }

        bool DrawBroadcastLever(Rect panel, string status, string action, Color color, bool enabled = true)
        {
            Card(panel,ConsoleFace);
            Fill(Box(panel.x+27,panel.y+31,10,10),color);
            Tag(panel.x+47,panel.y+24,status,color);
            float slotX = panel.center.x, slotY = panel.y + 128;
            Fill(Box(slotX-42,slotY-21,84,200),Line);
            Fill(Box(slotX-36,slotY-15,72,188),Studio);
            Fill(Box(slotX-5,slotY,10,158),PaperGrid);
            Rect input = Box(panel.x+43,panel.y+322,panel.width-86,68);
            bool hover = enabled && input.Contains(Event.current.mousePosition);
            float leverY = slotY+46+95*Mathf.Max(broadcastLeverPull,hover && !reduced ? .18f : 0);
            Rect handle = Box(slotX-82,leverY-21,164,50);
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            bool pulled = false;
            if (enabled && e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = broadcastLeverControl = id;
                e.Use();
            }
            else if (broadcastLeverControl == id && GUIUtility.hotControl == id)
            {
                if (e.type == EventType.MouseDrag)
                {
                    broadcastLeverPull = Mathf.InverseLerp(slotY+46,slotY+141,e.mousePosition.y);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = broadcastLeverControl = 0;
                    pulled = broadcastLeverPull >= .76f;
                    broadcastLeverPull = 0;
                    e.Use();
                }
            }
            DrawChartSegment(new Vector2(slotX,slotY+146),new Vector2(slotX,leverY),color);
            Fill(Box(slotX-79,leverY-15,158,38),Studio);
            Fill(Box(slotX-76,leverY-17,152,33),color);
            Fill(Box(slotX-72,leverY-13,143,3),Alpha(White,.57f));
            Text(Box(panel.x+20,panel.y+303,panel.width-40,17),"DRAG HANDLE DOWN",15,Dim,dataFont,TextAnchor.MiddleCenter);
            return Button(input,action,true,enabled:enabled,size:28,icon:"Next") || pulled;
        }

        void DrawBroadcastContact()
        {
            BroadcastTitle(broadcastReply ? "Earth answered." : "Waiting for Earth…",broadcastReply
                ? "MISSION COMPLETE   /   FIRST CONTACT" : "Signal away. For once, Zorvax has nothing to say.");
            if (!broadcastReply)
            {
                Card(Box(55,327,1490,463),ConsoleFace);
                float length = Mathf.Clamp01((elapsed-broadcastSince)/ReplyDelay);
                Fill(Box(90,557,1416,3),Line);
                Fill(Box(90,557,1416*length,3),Mint);
                return;
            }

            Card(Box(55,326,700,462),ConsoleFace);
            Tag(83,353,"INCOMING TRANSMISSION / EARTH",Mint);
            Text(Box(79,397,652,126),"We heard you.\nIs Tuesday good?",53,White,headingFont,TextAnchor.UpperLeft,true);
            Text(Box(85,550,633,59),"ZORVAX    What year?",27,Brass,strongFont);
            Text(Box(85,606,633,47),"NYXEN    One more question. Where does the rain go?",22,Mint,strongFont);
            if (Button(Box(83,660,644,52),"Take the Weather and soil case",true,size:25,icon:"Next"))
            {
                OpenSoilMission();
                return;
            }
            if (Button(Box(83,728,300,42),"Back to field station",size:21,icon:"Back")) OpenStation();
            if (Button(Box(396,728,331,42),"Replay the broadcast",size:21))
            {
                broadcastHeadline = BroadcastHeadline.Unselected;
                AdvanceBroadcast(BroadcastStep.Interrupt,"detent");
            }

            Rect receipt = Box(780,326,765,462);
            Fill(receipt,PrintPaper);
            Tag(810,352,"TRANSMISSION RECEIPT / NASA POWER · MERRA-2",PaperInk);
            string headline = broadcastHeadline == BroadcastHeadline.Difference
                ? "The Sahel-minus-Chile rainfall difference rose."
                : "Rainfall trends point in opposite directions at two grid points.";
            Text(Box(805,397,707,156),headline,49,PaperInk,headingFont,TextAnchor.UpperLeft,true);
            string scope = broadcastHeadline == BroadcastHeadline.Difference
                ? $"Reviewed paired rate {Signed(guidedRain.difference.test.slopePerDecade,"0.00")} mm/month per decade."
                : $"Sahel {Signed(guidedRain.left.slopePerDecade,"0.00")} / Chile {Signed(guidedRain.right.slopePerDecade,"0.00")} mm/month per decade.\nIndividual significance remains unestablished.";
            Text(Box(810,573,701,92),scope,24,PaperInk,bodyFont,TextAnchor.UpperLeft,true);
            Text(Box(810,695,702,52),$"{Period(guidedSahel)}   /   14°N 6°E  and  36°S 72°W",20,PaperInk,dataFont);

            float t = elapsed-receiptSince;
            if (!reduced && t < 2.6f)
            {
                for (int i = 0; i < 36; i++)
                {
                    float x = 98 + (i*149)%1420;
                    float y = 150 + (i*53)%97 + (42 + (i*83)%200)*t + 53*t*t;
                    float drift = Mathf.Sin(t*2.3f+i)*38;
                    Color color = i%3 == 0 ? BroadcastRed : i%3 == 1 ? Brass : Mint;
                    DrawChartSegment(new Vector2(x+drift,y),new Vector2(x+drift+8,y+9),Alpha(color,1-t/2.6f));
                }
            }
        }
    }
}
