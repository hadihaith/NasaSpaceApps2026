using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        const string LocalSignificance = "Statistical significance has not been established for this record.";
        const string PairSource = "NASA POWER · MERRA-2 precipitation (PRECTOTCORR)";
        bool comparisonHasOrbit;
        bool comparisonTable, differenceOpen, comparisonMethods, showPredictionEdits;
        int comparisonRow, comparisonYear;

        void ResetComparisonView()
        {
            comparisonTable = differenceOpen = comparisonMethods = showPredictionEdits = false;
            comparisonRow = comparisonYear = 0;
        }

        void OpenComparisonWithoutOrbit()
        {
            Go(View.Report);
            orbitOpen = true;
            comparisonHasOrbit = false;
            ResetComparisonView();
            orbitProgress = 1;
        }

        void LateUpdate()
        {
            if (view != View.Report || !orbitOpen || !comparisonHasOrbit) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (reduced) orbitProgress = 1;
            else if (!orbitPaused) orbitProgress = Mathf.Clamp01(orbitProgress + (float)(now - orbitLastTime) / 3.6f);
            orbitLastTime = now;
            world.SurveyOrbit(orbitStart,orbitEnd,orbitProgress);
        }

        static string PairedVerdict(ArchiveTrend trend) => trend.status switch
        {
            "significant" => "The Kuwait-minus-UK rainfall gap changed significantly over time.",
            "not-significant" => "The test did not find a significant trend in the Kuwait-minus-UK rainfall gap.",
            _ => "Statistical significance has not been established for this comparison."
        };

        static string TrendRate(string subject, float rate, string unit, string period = null)
        {
            string span = period == null ? "" : $" over {period}";
            if (rate == 0) return $"{subject} was flat{span} (0 {unit} per decade).";
            string amount = Mathf.Abs(rate).ToString(Mathf.Abs(rate) < .01f ? "G3" : "0.00",Invariant);
            return $"{subject} {(rate > 0 ? "rose" : "fell")} by {amount} {unit} per decade{span}.";
        }

        static string PairedTestDetails(ArchiveTrend trend)
        {
            var test = HasReviewedTest(trend) ? trend.test : trend.exploratoryTest;
            string statistic = HistoricalTestStatistic(test.degreesOfFreedom,test.tStatistic);
            return $"{test.method} · {statistic}p = {test.p.ToString("G3",Invariant)} · α = {test.alpha.ToString("0.##",Invariant)}\n"+
                $"95% confidence interval: {Signed(test.lowerPerDecade,"0.00")} to {Signed(test.upperPerDecade,"0.00")} {trend.unit} per decade";
        }

        void DrawComparison()
        {
            if (comparisonHasOrbit) DrawGroundTrack();
            else Fill(Box(-2200,-2200,6000,5000),Ink);
            Fade(Box(0,0,1600,190),Alpha(Ink,.95f),0);
            Tag(56,25,"HISTORICAL COMPARISON",Mint);
            Text(Box(53,47,900,49),$"{PlaceName(pairedOrigin)}  /  {PlaceName(pairedTarget)}",43,White,headingFont);
            DrawCaseProgress(Box(979,33,534,73),investigation.kind);
            string measure = pairedDifference != null ? "Precipitation (rain and snow)" : pairedOrigin.variable;
            Text(Box(56,comparisonHasOrbit && demoActive ? 137 : 114,1468,44),$"{measure} · {pairedOrigin.product} · {Period(pairedOrigin)} · {pairedOrigin.years.Length} matched years\n"+
                $"{ArchiveFootprint(pairedOrigin)}   /   {ArchiveFootprint(pairedTarget)}",20,Dim);
            if (Button(Box(56,179,194,42),"Back to case",icon:"Back")) CloseComparison();
            if (comparisonHasOrbit && orbitProgress < 1)
            {
                if (Button(Box(1279,179,50,42),"",icon:orbitPaused ? "Play" : "Pause")) orbitPaused = !orbitPaused;
                if (Button(Box(1343,179,201,42),"Skip flight",true,icon:"Next")) orbitProgress = 1;
                Text(Box(53,762,1450,49),"The two sites share the same measure and observation period.",26,White);
                return;
            }
            if (comparisonHasOrbit && Button(Box(1376,179,168,42),"Replay flight",false,false,!reduced,18))
            {
                orbitProgress = 0;
                orbitPaused = false;
            }
            Card(Box(56,242,1488,137),Alpha(Ink,.95f));
            string verdict = pairedDifference == null
                ? "Statistical significance has not been established for this comparison."
                : PairedVerdict(pairedTrend);
            Text(Box(78,252,1440,69),verdict,32,White,headingFont,TextAnchor.MiddleLeft,true);
            string interpretation = pairedDifference == null ? $"Both records cover {Period(pairedOrigin)}."
                : TrendRate("The difference",(HasReviewedTest(pairedTrend) ? pairedTrend.test : pairedTrend.exploratoryTest).slopePerDecade,pairedTrend.unit,Period(pairedOrigin));
            Text(Box(80,321,1424,42),interpretation,24,Mint,strongFont);
            Fill(Box(0,395,1600,505),Alpha(Ink,.95f));
            var a = MakeFinding(pairedOrigin);
            var b = MakeFinding(pairedTarget);
            float min = Mathf.Min(pairedOrigin.years.Min(v => v.value),pairedTarget.years.Min(v => v.value));
            float max = Mathf.Max(pairedOrigin.years.Max(v => v.value),pairedTarget.years.Max(v => v.value));
            if (max == min) max += 1;
            if (comparisonMethods)
            {
                Card(Box(56,413,1488,296),Panel);
                Text(Box(80,425,1440,37),"See the evidence",26,White,headingFont);
                if (pairedDifference != null)
                {
                    Text(Box(80,466,1440,40),PairedTestDetails(pairedTrend).Replace("\n"," · "),19,White,bodyFont,TextAnchor.MiddleLeft,true);
                    Text(Box(80,510,1440,32),$"{PairSource} · {pairedDifference.hypothesis}",18,Dim);
                    Text(Box(80,545,1440,45),comparisonScope,18,Dim,bodyFont,TextAnchor.UpperLeft,true);
                    Text(Box(80,589,1440,23),$"{a.place} · {a.reviewRationale}",17,Dim);
                    Text(Box(80,613,1440,23),$"{b.place} · {b.reviewRationale}",17,Dim);
                    Text(Box(80,637,1440,23),$"Descriptive paired rate (Theil–Sen) {FindingRate(pairedDifference)}",17,Dim);
                }
                else
                {
                    Text(Box(80,466,1440,44),$"{a.product} · {a.aggregation}",19,Dim,bodyFont,TextAnchor.MiddleLeft,true);
                }
                if (Button(Box(80,667,280,34),"Open source for "+a.place,enabled:!string.IsNullOrEmpty(a.url),size:17))
                    Application.OpenURL(a.url);
                if (Button(Box(373,667,280,34),"Open source for "+b.place,enabled:!string.IsNullOrEmpty(b.url),size:17))
                    Application.OpenURL(b.url);
            }
            else if (differenceOpen)
            {
                Card(Box(56,413,1488,263),Panel);
                Text(Box(80,425,1410,35),$"{a.place} minus {b.place} · {pairedTrend.unit}",26,White,headingFont);
                Text(Box(80,463,1410,30),"Each point subtracts UK rainfall from Kuwait rainfall for the same year.",20,Dim);
                DrawHistoricalChart(pairedTrend,Box(170,511,1346,124),includeZero: true,selectedIndex: comparisonYear);
                Text(Box(687,641,312,27),"Year",15,Dim,dataFont,TextAnchor.MiddleCenter);
            }
            else if (comparisonTable) DrawPairTable(pairedOrigin,pairedTarget);
            else
            {
                string chartMeasure = pairedDifference != null ? "Yearly average of monthly precipitation totals" : pairedOrigin.aggregation;
                Text(Box(81,400,1437,29),$"{chartMeasure} ({a.unit}) · {Period(pairedOrigin)}",18,Dim);
                DrawComparisonCard(56,435,a,pairedOrigin,min,max,Mint);
                DrawComparisonCard(816,435,b,pairedTarget,min,max,Aqua);
            }
            if (!comparisonMethods && !comparisonTable)
            {
                var left = pairedOrigin.years[comparisonYear];
                var right = pairedTarget.years[comparisonYear];
                string difference = pairedTrend == null ? "" : $" · Difference {Signed(pairedTrend.points[comparisonYear].value,"0.###")}";
                if (Button(Box(56,685,46,38),"",enabled:comparisonYear > 0,icon:"Back",tooltip:"Previous year")) comparisonYear--;
                Text(Box(230,685,1120,38),
                    FormattableString.Invariant($"{left.year} · {a.place} {left.value:0.###} · {b.place} {right.value:0.###}{difference} {a.unit}"),
                    19,White,dataFont,TextAnchor.MiddleCenter);
                if (Button(Box(1498,685,46,38),"",enabled:comparisonYear+1 < pairedOrigin.years.Length,icon:"Next",tooltip:"Next year")) comparisonYear++;
            }
            if (pairedDifference != null && investigation.kind == InvestigationKind.Rainfall)
            {
                if (Button(Box(56,783,213,39),showPredictionEdits ? "Hide predictions" : "Change predictions",size:18))
                    showPredictionEdits = !showPredictionEdits;
                if (showPredictionEdits)
                {
                    Text(Box(285,788,123,30),"Kuwait",18,Mint,strongFont);
                    Text(Box(916,788,116,30),"UK",18,Aqua,strongFont);
                    DrawCall(414,783,ref investigation.revisedOriginCall,false,investigation.originCall);
                    DrawCall(1038,783,ref investigation.revisedOtherCall,false,investigation.otherCall);
                }
            }
            if (Button(Box(56,829,196,43),comparisonTable ? "Show charts" : "Inspect values",false,false,true,19))
            {
                comparisonTable = !comparisonTable;
                comparisonMethods = differenceOpen = false;
                comparisonRow = comparisonYear / 5 * 5;
            }
            if (pairedTrend?.points is { Length: > 1 } && Button(Box(267,829,253,43),
                differenceOpen ? "Show site charts" : "View paired difference",false,differenceOpen,true,19))
            {
                differenceOpen = !differenceOpen;
                comparisonTable = comparisonMethods = false;
            }
            if (Button(Box(532,829,217,43),comparisonMethods ? "Show site charts" : "See the evidence",false,comparisonMethods,true,19))
            {
                comparisonMethods = !comparisonMethods;
                comparisonTable = differenceOpen = false;
            }
            if (Button(Box(1112,829,432,43),"Save comparison to report",true,false,!demoActive,23,icon:"Archive"))
            {
                FileComparison();
                CloseComparison();
            }
        }

        void DrawGroundTrack()
        {
            if (Event.current.type != EventType.Repaint) return;
            const int steps = 32;
            for (int i = 0; i < steps; i += 2)
            {
                if (world.ProjectFootprint(orbitStart,orbitEnd,i/(float)steps,out var a) &&
                    world.ProjectFootprint(orbitStart,orbitEnd,(i+1f)/steps,out var b))
                    DrawChartSegment(ScreenToField(a),ScreenToField(b),Aqua);
            }
            DrawFootprintPin(orbitStart,PlaceName(pairedOrigin),Mint);
            DrawFootprintPin(orbitEnd,PlaceName(pairedTarget),Aqua);
        }

        Vector2 ScreenToField(Vector3 point) => new((point.x - fieldX)/fieldScale, (Screen.height - point.y - fieldY)/fieldScale);

        void DrawFootprintPin(Vector2 position, string label, Color color)
        {
            if (!world.ProjectFootprint(position,position,0,out var point)) return;
            Vector2 p = ScreenToField(point);
            if (p.x < 0 || p.x > 1600 || p.y < 0 || p.y > 900) return;
            Fill(Box(p.x-6,p.y-6,12,12),color);
            Fill(Box(p.x-1,p.y-1,2,2),Ink);
            Fill(Box(p.x,p.y,1,21),color);
            Fill(Box(p.x-114,p.y+21,228,31),Alpha(Ink,.94f));
            Text(Box(p.x-114,p.y+21,228,31),label,19,White,strongFont,TextAnchor.MiddleCenter);
        }

        void DrawComparisonCard(float x, float y, FiledFinding finding, EvidenceSeries record, float min, float max, Color tint)
        {
            Card(Box(x,y,728,241),Panel);
            Text(Box(x+18,y+7,692,33),finding.place,28,tint,headingFont);
            string verdict = finding.status switch
            {
                "significant" => "This record had a statistically significant trend.",
                "not-significant" => "The test did not find a statistically significant trend.",
                _ => LocalSignificance
            };
            Text(Box(x+18,y+41,692,27),verdict,19,White,strongFont);
            Text(Box(x+18,y+70,692,28),TrendRate("The descriptive trend",finding.ratePerDecade,finding.unit),19,Dim);
            DrawChart(record,Box(x+88,y+116,613,86),min,max,tint,68,comparisonYear);
            Text(Box(x+260,y+208,208,25),"Year",15,Dim,dataFont,TextAnchor.MiddleCenter);
        }

        void DrawPairTable(EvidenceSeries a,EvidenceSeries b)
        {
            Card(Box(56,413,1488,263),Panel);
            Text(Box(80,425,1410,30),$"Year            {PlaceName(a)}                 {PlaceName(b)}" +
                (pairedTrend == null ? "" : "                Difference") + $"     ·     {a.unit}",20,White,dataFont);
            int count = Mathf.Min(5,a.years.Length-comparisonRow);
            for (int i = 0; i < count; i++)
            {
                var left = a.years[comparisonRow+i];
                var right = b.years[comparisonRow+i];
                string delta = pairedTrend == null ? "" :
                    FormattableString.Invariant($"                    {pairedTrend.points[comparisonRow+i].value,9:0.###}");
                Text(Box(82,468+i*29,1400,26),FormattableString.Invariant(
                    $"{left.year}            {left.value,9:0.###}                        {right.value,9:0.###}{delta}"),19,White,dataFont);
            }
            if (Button(Box(1150,632,160,34),"Previous years",false,false,comparisonRow > 0,17)) comparisonRow -= 5;
            if (Button(Box(1328,632,170,34),"Next years",false,false,comparisonRow+5 < a.years.Length,17)) comparisonRow += 5;
        }
    }
}
