using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        // Keep the step IDs used by earlier broadcast saves.
        enum BroadcastStep
        {
            Interrupt = 0, Instrument = 1, Receiver = 2, Records = 3, Difference = 4,
            Rehearsal = 5, Contact = 6, ChileReading = 7, SahelReading = 8, ChilePlace = 9, ChileSeries = 10
        }
        enum BroadcastHeadline { Unselected, Locations, Difference }
        enum BroadcastLens { Annual, Month, Anomaly }
        enum BroadcastChart { Chile, Sahel, Overlay, Difference }

        const string BroadcastPrefs = StorySession.BroadcastProgressKey;
        const int BroadcastSaveOffset = 100;
        const float ReplyDelay = 1.65f;
        static readonly Color BroadcastRed = Rgb("#ff906c"), Brass = Rgb("#f3bc68"),
            Studio = Rgb("#091821"), ConsoleFace = Rgb("#102632"), PrintPaper = Rgb("#ebdfbf"),
            PaperInk = Rgb("#24332e"), PaperGrid = Rgb("#c9bea3"), SahelInk = Rgb("#056c72"),
            ChileInk = Rgb("#9e472e");
        static readonly (string Icon, string Label)[] BroadcastStages =
        {
            ("Signal","Cut the feed"), ("Rain","Choose variable"), ("Location","Place A"), ("Chart","Time series"),
            ("Check","Local verdict"), ("Location","Place B"), ("Chart","Compare"), ("Next","Send to Earth")
        };
        const string LocalSignificance = "Statistical significance has not been established for this record.";
        const string PairSource = "NASA POWER · MERRA-2 precipitation (PRECTOTCORR)";
        BroadcastStep broadcast;
        BroadcastHeadline broadcastHeadline;
        BroadcastLens broadcastLens;
        Texture2D broadcastEarth;
        AudioSource broadcastAudio;
        string broadcastCorrection;
        float broadcastSince, printSince, receiptSince, broadcastLeverPull;
        int broadcastYear, broadcastMonth, broadcastDragControl, broadcastLeverControl;
        bool broadcastReply, broadcastEvidence;

        void OpenBroadcast()
        {
            CheckGuidedRecords();
            Go(View.Broadcast);
            broadcastEarth ??= Resources.Load<Texture2D>("Film/earth");
            var saved = PlayerPrefs.GetInt(BroadcastPrefs);
            // Older saves used one digit per field. New saves have room for additional steps.
            int stepWidth = saved < BroadcastSaveOffset ? 10 : BroadcastSaveOffset;
            int progress = saved < BroadcastSaveOffset ? saved : saved - BroadcastSaveOffset;
            broadcast = (BroadcastStep)Mathf.Clamp(progress % stepWidth,0,(int)BroadcastStep.ChileSeries);
            broadcastHeadline = (BroadcastHeadline)Mathf.Clamp(progress / stepWidth,0,(int)BroadcastHeadline.Difference);
            broadcastSince = printSince = receiptSince = elapsed - 20;
            broadcastEvidence = false;
            broadcastCorrection = null;
            broadcastLens = BroadcastLens.Annual;
            broadcastMonth = 0;
            broadcastYear = (guidedSahel?.years.Length ?? 1) - 1;
            broadcastReply = broadcast == BroadcastStep.Contact;
            keyboardFocus = false;
        }

        void AdvanceBroadcast(BroadcastStep next, string sound)
        {
            broadcast = next;
            broadcastSince = elapsed;
            broadcastReply = broadcastEvidence = false;
            if (next is BroadcastStep.Interrupt or BroadcastStep.Receiver) SelectBroadcastLens(BroadcastLens.Annual);
            if (next is BroadcastStep.ChileSeries or BroadcastStep.SahelReading or BroadcastStep.Records or BroadcastStep.Difference)
                printSince = elapsed;
            broadcastCorrection = null;
            ReleaseBroadcastControls();
            keyboardFocus = false;
            SaveBroadcastProgress();
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
            Fill(Box(-2200,-2200,6000,5000),Studio);
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
                BroadcastStep.Instrument => 1,
                BroadcastStep.ChilePlace => 2,
                BroadcastStep.ChileSeries => 3,
                BroadcastStep.ChileReading => 4,
                BroadcastStep.Receiver or BroadcastStep.SahelReading => 5,
                BroadcastStep.Records or BroadcastStep.Difference => 6,
                _ => 7
            };
            DrawProgress(Box(52,111,1496,70),BroadcastStages,broadcastReply ? BroadcastStages.Length : chapter);

            switch (broadcast)
            {
                case BroadcastStep.Interrupt: DrawBroadcastInterrupt(); break;
                case BroadcastStep.Instrument: DrawBroadcastVariable(); break;
                case BroadcastStep.ChilePlace: DrawBroadcastPlace(false); break;
                case BroadcastStep.ChileSeries: DrawBroadcastLocal(false,false); break;
                case BroadcastStep.ChileReading: DrawBroadcastLocal(false,true); break;
                case BroadcastStep.Receiver: DrawBroadcastPlace(true); break;
                case BroadcastStep.SahelReading: DrawBroadcastLocal(true,true); break;
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

        void DrawBroadcastVariable()
        {
            BroadcastTitle("Choose a NASA measurement","Which record can check Zorvax’s rain alert?");
            Card(Box(55,327,915,436),ConsoleFace);
            DrawIcon("Rain",new Vector2(128,419),Mint,2f);
            Text(Box(185,358,740,80),"Precipitation",54,White,headingFont);
            Text(Box(188,449,700,56),"How much rain and snow fell.",29,Mint,strongFont);
            Text(Box(188,515,700,44),"NASA POWER · MERRA-2",22,Dim);
            if (Button(Box(188,641,734,55),"Use precipitation",true,size:23,icon:"Next"))
                AdvanceBroadcast(BroadcastStep.ChilePlace,"detent");

            Card(Box(995,327,550,436),ConsoleFace);
            Text(Box(1023,365,490,61),"Other measurements",34,White,headingFont);
            Text(Box(1023,447,490,101),"Explore temperature, wind and other variables in the Earth archive.",24,Dim,
                bodyFont,TextAnchor.UpperLeft,true);
            if (Button(Box(1023,641,494,55),"Choose in the archive",size:22,icon:"Archive"))
            {
                OpenArchive(null);
                archiveFilter = ArchiveFilter.Measurement;
            }
        }

        void DrawBroadcastPlace(bool sahel)
        {
            var record = sahel ? guidedSahel : guidedChile;
            BroadcastTitle(sahel ? "Choose a second point" : "Choose the first point",sahel
                ? "Compare Chile with a point across the Atlantic using the same record settings."
                : "Start where the rain alert came from.");
            DrawBroadcastMap(Box(55,327,888,444),sahel);

            Card(Box(972,327,573,444),ConsoleFace);
            Tag(1000,349,sahel ? "PLACE B / OFFLINE RECORD" : "PLACE A / OFFLINE RECORD",Mint);
            Text(Box(1000,382,517,47),ArchiveFootprint(record),29,White,headingFont);
            Text(Box(1000,445,516,132),$"Precipitation (rain and snow) · {record.unit}\nNASA POWER · MERRA-2\n"+
                $"Yearly averages · {Period(record)} · {record.years.Length} years",21,Dim,bodyFont,TextAnchor.UpperLeft,true);
            if (Button(Box(1000,678,516,54),sahel ? "Use the Sahel point" : "Use the Chile point",true,size:23,icon:"Next"))
                AdvanceBroadcast(sahel ? BroadcastStep.SahelReading : BroadcastStep.ChileSeries,"paper");
        }

        ArchiveTrend BroadcastLocalTrend(bool sahel)
        {
            if (sahel) return guidedRain.left;
            var record = Exported(guidedChile);
            return broadcastLens switch
            {
                BroadcastLens.Month => record.monthFindings[broadcastMonth],
                BroadcastLens.Anomaly => record.anomalyFinding,
                _ => guidedRain.right
            };
        }

        void SelectBroadcastLens(BroadcastLens lens)
        {
            broadcastLens = lens;
            broadcastYear = BroadcastLocalTrend(false).points.Length-1;
            ReleaseBroadcastControls();
        }

        string BroadcastLensLabel(ArchiveTrend trend) => trend.mode switch
        {
            "month" => Invariant.DateTimeFormat.GetMonthName(broadcastMonth+1) + " rainfall, one value per year",
            "anomaly" => "Monthly rainfall relative to each month’s 1991–2020 average",
            _ => "Yearly average of monthly precipitation totals"
        };

        void DrawBroadcastLocal(bool sahel, bool verdict)
        {
            var record = sahel ? guidedSahel : guidedChile;
            var trend = BroadcastLocalTrend(sahel);
            string place = (sahel ? "B · " : "A · ") + PlaceName(record);
            if (verdict)
            {
                Text(Box(53,181,1493,62),LocalSignificance,36,White,headingFont);
                Text(Box(56,244,1486,41),TrendRate("The descriptive trend",trend.slopePerDecade,trend.unit),27,Mint,strongFont);
            }
            else BroadcastTitle("Read the Chile rainfall record","Use the year cursor to see how much fell.");

            DrawBroadcastPaper(sahel ? BroadcastChart.Sahel : BroadcastChart.Chile,trend);
            Card(Box(1005,327,540,486),ConsoleFace);
            Tag(1032,350,place.ToUpperInvariant(),Mint);
            Text(Box(1032,384,486,72),$"{ArchiveFootprint(record)}\n{trend.n} observations · {trend.firstDate} to {trend.lastDate}",
                22,White,bodyFont,TextAnchor.UpperLeft,true);
            if (broadcastEvidence)
            {
                Text(Box(1032,476,486,68),PairSource,19,Dim,bodyFont,TextAnchor.UpperLeft,true);
                Text(Box(1032,532,486,114),"Theil–Sen describes the trend in the archived observations. The source has complete coverage for these points.",
                    20,White,bodyFont,TextAnchor.UpperLeft,true);
            }
            else if (verdict)
            {
                Text(Box(1032,487,486,103),sahel
                    ? "Both points have the same 35 years of data. Put their records on one scale."
                    : broadcastLens == BroadcastLens.Annual
                        ? "Compare with a second grid point to see whether the records changed at the same rate."
                        : "The reviewed comparison uses yearly averages. Return to the annual verdict to choose a second point.",
                    23,Dim,bodyFont,TextAnchor.UpperLeft,true);
            }
            else if (Exported(record) is { monthFindings: { Length: 12 }, anomalyFinding: not null })
            {
                if (Button(Box(1032,476,145,39),"Annual",active:broadcastLens == BroadcastLens.Annual,size:17))
                    SelectBroadcastLens(BroadcastLens.Annual);
                if (Button(Box(1183,476,149,39),"One month",active:broadcastLens == BroadcastLens.Month,size:17))
                    SelectBroadcastLens(BroadcastLens.Month);
                if (Button(Box(1338,476,180,39),"Monthly anomalies",active:broadcastLens == BroadcastLens.Anomaly,size:17))
                    SelectBroadcastLens(BroadcastLens.Anomaly);
                if (broadcastLens == BroadcastLens.Month)
                {
                    if (Button(Box(1032,534,45,38),"",icon:"Back",tooltip:"Previous calendar month"))
                    {
                        broadcastMonth = (broadcastMonth+11)%12;
                        SelectBroadcastLens(BroadcastLens.Month);
                    }
                    Text(Box(1088,532,376,41),Invariant.DateTimeFormat.GetMonthName(broadcastMonth+1),22,White,strongFont,TextAnchor.MiddleCenter);
                    if (Button(Box(1473,534,45,38),"",icon:"Next",tooltip:"Next calendar month"))
                    {
                        broadcastMonth = (broadcastMonth+1)%12;
                        SelectBroadcastLens(BroadcastLens.Month);
                    }
                }
            }

            if (verdict && Button(Box(1032,669,244,43),broadcastEvidence ? "Back to verdict" : "See the evidence",size:18))
                broadcastEvidence = !broadcastEvidence;
            if (verdict && !sahel && Button(Box(1286,669,232,43),"Change time series",size:18))
                AdvanceBroadcast(BroadcastStep.ChileSeries,"detent");
            string action = sahel ? "Compare records" : !verdict ? "See the trend"
                : broadcastLens == BroadcastLens.Annual ? "Choose a second point" : "See the annual verdict";
            if (Button(Box(1032,731,484,55),action,true,size:23,icon:"Next"))
            {
                if (sahel) AdvanceBroadcast(BroadcastStep.Records,"paper");
                else if (!verdict) AdvanceBroadcast(BroadcastStep.ChileReading,"detent");
                else if (broadcastLens == BroadcastLens.Annual) AdvanceBroadcast(BroadcastStep.Receiver,"projector");
                else
                {
                    SelectBroadcastLens(BroadcastLens.Annual);
                    broadcastEvidence = false;
                    keyboardFocus = false;
                }
            }
        }

        Vector2 BroadcastMapPoint(Rect map, EvidenceSeries record)
        {
            var point = Exported(record).footprint.point;
            return new Vector2(map.x + (point.longitude+180)/360*map.width,map.y + (90-point.latitude)/180*map.height);
        }

        void DrawBroadcastMapSurface(Rect map)
        {
            Fill(map,ConsoleFace);
            if (broadcastEarth != null && Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(map,broadcastEarth,ScaleMode.StretchToFill);
                Fill(map,Alpha(Studio,.32f));
            }
        }

        void DrawBroadcastMap(Rect map, bool sahel)
        {
            DrawBroadcastMapSurface(map);
            for (int i = 1; i < 6; i++) Fill(Box(map.x + map.width * i/6,map.y,1,map.height),Alpha(Mint,.23f));
            for (int i = 1; i < 4; i++) Fill(Box(map.x,map.y + map.height * i/4,map.width,1),Alpha(Mint,.23f));
            if (sahel)
            {
                var start = BroadcastMapPoint(map,guidedChile);
                var end = BroadcastMapPoint(map,guidedSahel);
                const int dashes = 14;
                for (int i = 0; i < dashes; i++)
                    DrawChartSegment(Vector2.Lerp(start,end,i/(float)dashes),Vector2.Lerp(start,end,(i+.55f)/dashes),Brass,3);
            }
            DrawBroadcastPin(map,guidedChile,sahel ? "A  CHILE · RAIN FELL" : "A  CENTRAL CHILE",BroadcastRed,!sahel);
            if (sahel) DrawBroadcastPin(map,guidedSahel,"B  CENTRAL SAHEL",Mint,true);
        }

        void DrawBroadcastPin(Rect map, EvidenceSeries record, string label, Color color, bool current)
        {
            var point = BroadcastMapPoint(map,record);
            float x = point.x, y = point.y;
            float pulse = reduced || !current ? 8 : 8 + 5 * (.5f + .5f * Mathf.Sin(elapsed * 4));
            Fill(Box(x-pulse,y-pulse,pulse*2,pulse*2),Alpha(color,.3f));
            Fill(Box(x-3,y-3,6,6),color);
            float labelWidth = label.Length*9+17;
            Fill(Box(x+9,y-17,labelWidth,34),Studio);
            Text(Box(x+16,y-17,labelWidth-10,34),label,15,color,dataFont);
        }

        void DrawBroadcastPairMap()
        {
            Rect map = Box(1032,380,246,123);
            DrawBroadcastMapSurface(map);
            DrawBroadcastPin(map,guidedChile,"A",BroadcastRed,false);
            DrawBroadcastPin(map,guidedSahel,"B",Mint,false);
            Text(Box(1294,391,224,47),$"{guidedRain.difference.n} matched years",22,White,strongFont);
            Text(Box(1294,443,224,44),"Same variable, product and unit",18,Dim,bodyFont,TextAnchor.UpperLeft,true);
        }

        void DrawBroadcastRecords(bool difference)
        {
            var paired = guidedRain.difference;
            Text(Box(53,181,1493,62),PairedVerdict(paired),36,White,headingFont,TextAnchor.MiddleLeft,true);
            Text(Box(56,244,1486,41),TrendRate("The difference",paired.test.slopePerDecade,paired.unit,Period(guidedSahel)),
                27,Mint,strongFont);

            string footprints = $"{ArchiveFootprint(guidedChile)}   /   {ArchiveFootprint(guidedSahel)}";
            Text(Box(56,286,1486,30),footprints,19,Dim);
            DrawBroadcastPaper(difference ? BroadcastChart.Difference : BroadcastChart.Overlay,
                difference ? paired : guidedRain.left,difference ? null : guidedRain.right);

            Card(Box(1005,327,540,486),ConsoleFace);
            if (broadcastEvidence)
            {
                Tag(1032,350,"REVIEWED TEST / SAHEL MINUS CHILE",Mint);
                Text(Box(1032,390,486,98),PairedTestDetails(paired),21,White,bodyFont,TextAnchor.UpperLeft,true);
                Text(Box(1032,500,486,70),PairSource+"\nYearly averages of monthly precipitation totals.",20,Dim,bodyFont,TextAnchor.UpperLeft,true);
                Text(Box(1032,569,486,95),"MERRA-2 uses different inputs to correct precipitation in Africa and Chile. This test covers the two archived grid points.",
                    18,Dim,bodyFont,TextAnchor.UpperLeft,true);
            }
            else
            {
                Tag(1032,350,"TWO LOCAL RECORDS / DESCRIPTIVE TRENDS",Brass);
                DrawBroadcastPairMap();
                Text(Box(1032,518,486,32),TrendRate("A · Chile",guidedRain.right.slopePerDecade,paired.unit),
                    20,BroadcastRed);
                Text(Box(1032,551,486,32),TrendRate("B · Sahel",guidedRain.left.slopePerDecade,paired.unit),
                    20,Mint);
                Text(Box(1032,595,486,64),"Statistical significance has not been established for either local record.",
                    21,White,bodyFont,TextAnchor.UpperLeft,true);
            }

            if (difference && Button(Box(1032,669,244,43),"Show both records",size:18))
                AdvanceBroadcast(BroadcastStep.Records,"paper");
            if (Button(Box(1286,669,230,43),broadcastEvidence ? "Show local trends" : "See the evidence",size:18))
                broadcastEvidence = !broadcastEvidence;
            if (Button(Box(1032,731,484,55),difference ? "Choose the broadcast claim" : "Show the matched difference",
                true,size:23,icon:"Next"))
                AdvanceBroadcast(difference ? BroadcastStep.Rehearsal : BroadcastStep.Difference,difference ? "detent" : "paper");
        }

        void DrawBroadcastPaper(BroadcastChart chart, ArchiveTrend trend, ArchiveTrend other = null)
        {
            Rect paper = Box(84,355,864,440);
            Card(Box(55,327,922,486),ConsoleFace);
            Fill(Box(73,346,886,16),Studio);
            float age = elapsed-printSince;
            float fraction = reduced ? 1 : Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.48f));
            Fill(Box(paper.x,paper.y,paper.width,paper.height*fraction),PrintPaper);
            if (fraction >= 1) DrawBroadcastChart(paper,chart,trend,other);
        }

        void DrawBroadcastChart(Rect paper, BroadcastChart chart, ArchiveTrend trend, ArchiveTrend other)
        {
            Rect OnPaper(float x, float y, float width, float height) => Box(paper.x+x,paper.y+y,width,height);
            string title = chart switch
            {
                BroadcastChart.Chile => "Central Chile",
                BroadcastChart.Sahel => "Central Sahel",
                BroadcastChart.Overlay => "B · Sahel (solid)  /  A · Central Chile (hollow)",
                _ => "Sahel minus Chile (B − A)"
            };
            Text(OnPaper(25,13,812,29),title,24,PaperInk,strongFont);
            Text(OnPaper(25,46,812,30),$"{BroadcastLensLabel(trend)} ({trend.unit})",18,PaperInk,bodyFont);
            var points = trend.points;
            var overlay = other?.points;
            float low = Mathf.Min(0,points.Min(p => p.value));
            float high = Mathf.Max(points.Max(p => p.value),overlay?.Max(p => p.value) ?? 0);
            float min = Mathf.Floor(low/20)*20;
            float max = Mathf.Ceil(high/20)*20;
            if (max <= min) max = min+20;
            Rect plot = OnPaper(65,100,768,193);
            int last = points.Length-1;
            broadcastYear = Mathf.Clamp(broadcastYear,0,last);
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
            if (min < 0 && max > 0) Fill(Box(plot.x,Y(0),plot.width,1),PaperInk);
            Fill(Box(X(broadcastYear)-1,plot.y,2,plot.height),Alpha(PaperInk,.55f));
            for (int i = 0; i <= last; i++)
            {
                InkPoint(points[i].value,points[Mathf.Max(i-1,0)].value,
                    chart == BroadcastChart.Chile ? ChileInk : SahelInk,false);
                if (overlay != null) InkPoint(overlay[i].value,overlay[Mathf.Max(i-1,0)].value,ChileInk,true);
                void InkPoint(float value, float previous, Color color, bool hollow)
                {
                    float x = X(i), y = Y(value);
                    if (i != 0) DrawChartSegment(new Vector2(X(i-1),Y(previous)),new Vector2(x,y),color);
                    Fill(Box(x-2,y-2,4,4),color);
                    if (hollow) Fill(Box(x-1,y-1,2,2),PrintPaper);
                    if (i == broadcastYear) DrawButtonEdge(Box(x-6,y-6,12,12),color);
                }
            }
            Text(OnPaper(65,297,190,28),points[0].date,16,PaperInk,dataFont);
            Text(OnPaper(643,297,190,28),points[last].date,16,PaperInk,dataFont,TextAnchor.MiddleRight);
            Text(OnPaper(270,297,350,28),trend.mode == "annual" ? "Year" : "Date",17,PaperInk,bodyFont,TextAnchor.MiddleCenter);
            string caption = chart switch
            {
                BroadcastChart.Overlay => "Both records are shown in mm/month on the same scale.",
                BroadcastChart.Difference => $"{points.Length} matching years. Each point subtracts Chile from the Sahel for that year.",
                _ => ""
            };
            Text(OnPaper(25,321,814,24),caption,18,PaperInk,bodyFont);
            Fill(OnPaper(0,350,864,90),PaperGrid);
            if (Button(OnPaper(25,370,56,42),"",enabled:broadcastYear>0,icon:"Back",tooltip:"Previous observation")) broadcastYear--;
            if (Button(OnPaper(783,370,56,42),"",enabled:broadcastYear<last,icon:"Next",tooltip:"Next observation")) broadcastYear++;
            string readout = chart switch
            {
                BroadcastChart.Overlay => $"Sahel {points[broadcastYear].value:0.0}   /   Chile {overlay[broadcastYear].value:0.0}",
                BroadcastChart.Difference => $"Difference {Signed(points[broadcastYear].value,"0.0")}",
                _ => points[broadcastYear].value.ToString("0.0",Invariant)
            };
            Text(OnPaper(93,370,678,42),$"{points[broadcastYear].date}    {readout} {trend.unit}",23,PaperInk,dataFont,TextAnchor.MiddleCenter);
        }

        void DrawBroadcastRehearsal()
        {
            BroadcastTitle("You're the producer.", "Choose the claim that the statistical test supports. The crew will send it to Earth.");
            Card(Box(55,325,1030,480),ConsoleFace);
            Tag(82,350,"ZORVAX / WAITING FOR A HEADLINE",Brass);
            if (Button(Box(83,397,974,60),"Earth is running out of rain.",size:25))
            {
                SetBroadcastHeadline(BroadcastHeadline.Unselected);
                broadcastCorrection = "NYXEN    We checked two grid points. Keep the claim to those records.";
                PlayBroadcastSound("sputter");
            }
            if (Button(Box(83,472,974,60),"Each grid point had a statistically significant rainfall trend.",size:24))
            {
                SetBroadcastHeadline(BroadcastHeadline.Unselected);
                broadcastCorrection = "NYXEN    We can describe each local trend. The statistical test used the yearly difference.";
                PlayBroadcastSound("sputter");
            }
            if (Button(Box(83,547,974,60),PairedVerdict(guidedRain.difference),
                active:broadcastHeadline == BroadcastHeadline.Difference,size:22))
                SetBroadcastHeadline(BroadcastHeadline.Difference);

            Text(Box(84,631,964,76),broadcastCorrection ?? (broadcastHeadline == BroadcastHeadline.Difference
                ? $"NYXEN    Send the paired finding for {Period(guidedSahel)}. Give Earth the years and both points."
                : "CADET B    Which claim matches the test we ran?"),
                24,broadcastCorrection == null ? Mint : Brass,bodyFont,TextAnchor.UpperLeft,true);
            if (Button(Box(83,731,974,46),"Review the evidence",size:20,icon:"Back"))
                AdvanceBroadcast(BroadcastStep.Records,"paper");

            bool ready = broadcastHeadline == BroadcastHeadline.Difference;
            if (DrawBroadcastLever(Box(1110,325,435,480),ready ? "READY" : "CHECK HEADLINE",
                "Send to Earth",Mint,ready))
                AdvanceBroadcast(BroadcastStep.Contact,"projector");
        }

        void SetBroadcastHeadline(BroadcastHeadline headline)
        {
            broadcastHeadline = headline;
            broadcastCorrection = null;
            SaveBroadcastProgress();
            if (headline != BroadcastHeadline.Unselected) PlayBroadcastSound("detent");
        }

        void SaveBroadcastProgress()
        {
            PlayerPrefs.SetInt(BroadcastPrefs,BroadcastSaveOffset + (int)broadcast + BroadcastSaveOffset*(int)broadcastHeadline);
            PlayerPrefs.Save();
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
                broadcastMonth = 0;
                AdvanceBroadcast(BroadcastStep.Interrupt,"detent");
                return;
            }

            Rect receipt = Box(780,326,765,462);
            Fill(receipt,PrintPaper);
            Tag(810,352,"TRANSMISSION RECEIPT / NASA POWER · MERRA-2",PaperInk);
            bool sentPair = broadcastHeadline == BroadcastHeadline.Difference;
            string headline = sentPair
                ? PairedVerdict(guidedRain.difference)
                : "Rainfall trends point in opposite directions at two grid points.";
            Text(Box(805,397,707,149),headline,39,PaperInk,headingFont,TextAnchor.UpperLeft,true);
            string scope = sentPair
                ? TrendRate("The difference",guidedRain.difference.test.slopePerDecade,guidedRain.difference.unit)
                : $"Sahel {Signed(guidedRain.left.slopePerDecade,"0.00")} / Chile {Signed(guidedRain.right.slopePerDecade,"0.00")} mm/month per decade.\nStatistical significance has not been established for either local record.";
            Text(Box(810,566,701,113),scope,24,PaperInk,bodyFont,TextAnchor.UpperLeft,true);
            LocateFootprint(guidedSahel,out var sahel);
            LocateFootprint(guidedChile,out var chile);
            Text(Box(810,688,702,68),$"{Period(guidedSahel)}\nSahel {Coordinate(sahel.x,sahel.y)} · Central Chile {Coordinate(chile.x,chile.y)}",
                20,PaperInk,dataFont,TextAnchor.UpperLeft,true);

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
