using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        bool comparisonHasOrbit;
        bool comparisonTable, differenceOpen, comparisonMethods;
        int comparisonRow, comparisonYear;

        void ResetComparisonView()
        {
            comparisonTable = differenceOpen = comparisonMethods = false;
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

        void DrawComparison()
        {
            if (comparisonHasOrbit) DrawGroundTrack();
            else Fill(Box(-2200,-2200,6000,5000),Ink);
            Fade(Box(0,0,1600,190),Alpha(Ink,.95f),0);
            Tag(56,31,"HISTORICAL COMPARISON",Mint);
            Text(Box(53,61,1400,59),$"{PlaceName(pairedOrigin)}  /  {PlaceName(pairedTarget)}",47,White,headingFont);
            Text(Box(56,125,1468,46),$"{pairedOrigin.product} · {Period(pairedOrigin)} · {pairedOrigin.years.Length} matched years\n{pairedOrigin.aggregation} · {pairedOrigin.unit}",20,Dim);
            if (Button(Box(56,179,194,42),"Investigation desk")) CloseComparison();
            if (comparisonHasOrbit && orbitProgress < 1)
            {
                if (Button(Box(1156,179,174,42),orbitPaused ? "Resume flight" : "Pause flight")) orbitPaused = !orbitPaused;
                if (Button(Box(1343,179,201,42),"Skip flight",true)) orbitProgress = 1;
                Text(Box(53,762,1450,49),"The two sites share the same measure and observation period.",26,White);
                return;
            }
            if (comparisonHasOrbit && Button(Box(1376,179,168,42),"Replay flight",false,false,!reduced,18))
            {
                orbitProgress = 0;
                orbitPaused = false;
            }
            Fill(Box(0,463,1600,437),Alpha(Ink,.93f));
            var a = MakeFinding(pairedOrigin);
            var b = MakeFinding(pairedTarget);
            float min = Mathf.Min(pairedOrigin.years.Min(v => v.value),pairedTarget.years.Min(v => v.value));
            float max = Mathf.Max(pairedOrigin.years.Max(v => v.value),pairedTarget.years.Max(v => v.value));
            if (max == min) max += 1;
            if (comparisonMethods)
            {
                Card(Box(56,482,1488,200),Panel);
                Text(Box(80,488,1440,44),"Sources and limits",26,White,headingFont);
                Text(Box(80,531,1440,51),comparisonScope,20,Dim,bodyFont,TextAnchor.UpperLeft,true);
                if (pairedDifference != null)
                {
                    Text(Box(80,582,1440,26),$"Sahel · {a.reviewRationale}",17,Dim);
                    Text(Box(80,607,1440,26),$"Chile · {b.reviewRationale}",17,Dim);
                }
                if (Button(Box(80,635,280,38),"Open source for "+a.place,false,false,!string.IsNullOrEmpty(a.url),17))
                    Application.OpenURL(a.url);
                if (Button(Box(373,635,280,38),"Open source for "+b.place,false,false,!string.IsNullOrEmpty(b.url),17))
                    Application.OpenURL(b.url);
            }
            else if (differenceOpen)
            {
                Card(Box(56,482,1488,200),Panel);
                Text(Box(80,488,1410,34),$"{a.place} minus {b.place} · {pairedTrend.unit}",26,White,headingFont);
                DrawHistoricalChart(pairedTrend,Box(170,543,1346,98),includeZero: true,selectedIndex: comparisonYear);
            }
            else if (comparisonTable) DrawPairTable(pairedOrigin,pairedTarget);
            else
            {
                DrawComparisonCard(56,482,a,pairedOrigin,min,max,Mint);
                DrawComparisonCard(816,482,b,pairedTarget,min,max,Aqua);
            }
            if (!comparisonMethods && !comparisonTable)
            {
                var left = pairedOrigin.years[comparisonYear];
                var right = pairedTarget.years[comparisonYear];
                string difference = pairedTrend == null ? "" : $" · Difference {Signed(pairedTrend.points[comparisonYear].value,"0.###")}";
                if (Button(Box(56,688,157,32),"Previous year",false,false,comparisonYear > 0,16)) comparisonYear--;
                Text(Box(230,685,1120,38),
                    FormattableString.Invariant($"{left.year} · {a.place} {left.value:0.###} · {b.place} {right.value:0.###}{difference} {a.unit}"),
                    19,White,dataFont,TextAnchor.MiddleCenter);
                if (Button(Box(1387,688,157,32),"Next year",false,false,comparisonYear+1 < pairedOrigin.years.Length,16)) comparisonYear++;
            }
            if (pairedDifference != null)
            {
                Text(Box(56,724,1488,29),$"Paired Theil–Sen rate {FindingRate(pairedDifference)} · {pairedDifference.significance}",20,White);
                Text(Box(56,752,1488,29),TestSummary(pairedDifference),18,Dim);
                if (investigation.kind == InvestigationKind.Rainfall)
                {
                    Text(Box(56,788,230,30),"Revise calls (optional)",18,White,strongFont);
                    Text(Box(285,788,123,30),"Sahel",18,Mint,strongFont);
                    Text(Box(916,788,116,30),"Chile",18,Aqua,strongFont);
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
            if (Button(Box(532,829,217,43),comparisonMethods ? "Show site charts" : "Sources and limits",false,comparisonMethods,true,19))
            {
                comparisonMethods = !comparisonMethods;
                comparisonTable = differenceOpen = false;
            }
            if (Button(Box(1112,829,432,43),"File historical comparison",true,false,!demoActive,23))
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
            Card(Box(x,y,728,200),Panel);
            Text(Box(x+18,y+4,692,32),finding.place,28,tint,headingFont);
            Text(Box(x+18,y+31,692,23),finding.footprint,17,Dim);
            DrawChart(record,Box(x+88,y+62,613,65),min,max,tint,68,comparisonYear);
            Text(Box(x+18,y+156,692,23),"Descriptive Theil–Sen · "+FindingRate(finding),19,White,strongFont);
            Text(Box(x+18,y+178,692,21),SignificanceLabel(finding,false),17,Dim);
        }

        void DrawPairTable(EvidenceSeries a,EvidenceSeries b)
        {
            Card(Box(56,482,1488,237),Panel);
            Text(Box(80,491,1410,30),$"Year            {PlaceName(a)}                 {PlaceName(b)}" +
                (pairedTrend == null ? "" : "                Difference") + $"     ·     {a.unit}",20,White,dataFont);
            int count = Mathf.Min(5,a.years.Length-comparisonRow);
            for (int i = 0; i < count; i++)
            {
                var left = a.years[comparisonRow+i];
                var right = b.years[comparisonRow+i];
                string delta = pairedTrend == null ? "" :
                    FormattableString.Invariant($"                    {pairedTrend.points[comparisonRow+i].value,9:0.###}");
                Text(Box(82,523+i*29,1400,26),FormattableString.Invariant(
                    $"{left.year}            {left.value,9:0.###}                        {right.value,9:0.###}{delta}"),19,White,dataFont);
            }
            if (Button(Box(1150,675,160,34),"Previous years",false,false,comparisonRow > 0,17)) comparisonRow -= 5;
            if (Button(Box(1328,675,170,34),"Next years",false,false,comparisonRow+5 < a.years.Length,17)) comparisonRow += 5;
        }
    }
}
