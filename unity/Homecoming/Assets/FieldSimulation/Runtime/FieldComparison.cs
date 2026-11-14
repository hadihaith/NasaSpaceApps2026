using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        bool comparisonHasOrbit;
        bool comparisonTable, differenceOpen;
        int comparisonRow;

        void OpenComparisonWithoutOrbit()
        {
            Go(View.Report);
            orbitOpen = true;
            comparisonHasOrbit = false;
            comparisonTable = differenceOpen = false;
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
            Text(Box(56,125,1468,30),$"{pairedOrigin.product} · {Period(pairedOrigin)} · {pairedOrigin.aggregation}",20,Dim);
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
            if (differenceOpen)
            {
                Card(Box(56,482,1488,268),Panel);
                Text(Box(80,493,1410,39),$"Paired difference · {a.place} minus {b.place} · {pairedTrend.unit}",26,White,headingFont);
                DrawHistoricalChart(pairedTrend,Box(170,557,1346,147));
            }
            else if (comparisonTable) DrawPairTable(pairedOrigin,pairedTarget);
            else
            {
                DrawComparisonCard(56,482,a,pairedOrigin,min,max,Mint);
                DrawComparisonCard(816,482,b,pairedTarget,min,max,Aqua);
            }
            if (pairedDifference != null)
                Text(Box(56,765,1488,36),$"{a.place} minus {b.place} · {FindingRate(pairedDifference)} · {SignificanceLabel(pairedDifference,false)}",21,White);
            Text(Box(67,801,1450,23),comparisonScope,16,Dim);
            if (Button(Box(56,829,196,43),comparisonTable ? "Show charts" : "Inspect values",false,false,true,19))
            {
                comparisonTable = !comparisonTable || differenceOpen;
                differenceOpen = false;
                comparisonRow = 0;
            }
            if (pairedTrend?.points is { Length: > 1 } && Button(Box(267,829,253,43),
                differenceOpen ? "Show site charts" : "View paired difference",false,differenceOpen,true,19))
            {
                differenceOpen = !differenceOpen;
                comparisonTable = false;
            }
            if (Button(Box(1112,829,432,43),"File as historical comparison",true,false,true,23))
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
            Fill(Box(p.x-6,p.y-6,12,12),color);
            Fill(Box(p.x-1,p.y-1,2,2),Ink);
            Fill(Box(p.x,p.y,1,21),color);
            Fill(Box(p.x-114,p.y+21,228,31),Alpha(Ink,.94f));
            Text(Box(p.x-114,p.y+21,228,31),label,19,White,strongFont,TextAnchor.MiddleCenter);
        }

        void DrawComparisonCard(float x, float y, FiledFinding finding, EvidenceSeries record, float min, float max, Color tint)
        {
            Card(Box(x,y,728,268),Panel);
            Text(Box(x+18,y+10,692,37),finding.place,30,tint,headingFont);
            Text(Box(x+18,y+51,692,25),finding.footprint,19,Dim);
            DrawChart(record,Box(x+88,y+94,613,71),min,max,tint,68);
            Text(Box(x+18,y+196,692,29),FindingRate(finding),23,White,strongFont);
            Text(Box(x+18,y+229,692,27),SignificanceLabel(finding,false),18,Dim);
        }

        void DrawPairTable(EvidenceSeries a,EvidenceSeries b)
        {
            Card(Box(56,482,1488,268),Panel);
            Text(Box(80,495,1410,34),$"Year     {PlaceName(a)}    /    {PlaceName(b)}   ·   {a.unit}",23,White,strongFont);
            int count = Mathf.Min(5,a.years.Length-comparisonRow);
            for (int i = 0; i < count; i++)
            {
                var left = a.years[comparisonRow+i];
                var right = b.years[comparisonRow+i];
                Text(Box(82,539+i*32,1400,29),$"{left.year}      {left.value,9:0.##}      {right.value,9:0.##}",19,Dim,dataFont);
            }
            if (Button(Box(1150,687,160,39),"Previous",false,false,comparisonRow > 0,18)) comparisonRow -= 5;
            if (Button(Box(1328,687,170,39),"Next years",false,false,comparisonRow+5 < a.years.Length,18)) comparisonRow += 5;
        }
    }
}
