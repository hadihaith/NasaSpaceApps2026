using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        enum ArchiveFilter { None, Place, Measurement }
        ArchiveFilter archiveFilter;
        Texture2D siteMap;
        static readonly string[] MeasurementIcons = { "Archive", "Rain", "Soil", "Temperature", "Droplet", "Wind" };

        bool DrawArchiveFilters()
        {
            Tag(56,216,"PLACE",Dim);
            Tag(428,216,"MEASUREMENT",Dim);
            int keyIndex = Array.IndexOf(EvidenceKeys,evidenceKey);
            string measurement = keyIndex < 0 ? "All measurements" : EvidenceNames[keyIndex];
            if (Button(Box(56,245,350,52),Region.name,active:archiveFilter == ArchiveFilter.Place,icon:"Choose"))
                archiveFilter = archiveFilter == ArchiveFilter.Place ? ArchiveFilter.None : ArchiveFilter.Place;
            if (Button(Box(428,245,350,52),measurement,active:archiveFilter == ArchiveFilter.Measurement,icon:"Choose"))
                archiveFilter = archiveFilter == ArchiveFilter.Measurement ? ArchiveFilter.None : ArchiveFilter.Measurement;
            if (investigation != null && Button(Box(1301,245,243,52),"Back to case",icon:"Back")) OpenCase();
            if (archiveFilter == ArchiveFilter.Place)
            {
                DrawSitePicker(333,false);
                return true;
            }
            if (archiveFilter != ArchiveFilter.Measurement) return false;
            for (int i = 0; i < MeasurementIcons.Length; i++)
            {
                if (ControlTile(Box(56+i*187,333,171,126),MeasurementIcons[i],
                    i == 0 ? "All measurements" : EvidenceNames[i-1],keyIndex+1 == i))
                {
                    evidenceKey = i == 0 ? null : EvidenceKeys[i-1];
                    evidenceIndex = 0;
                    archiveFilter = ArchiveFilter.None;
                }
            }
            return true;
        }

        void DrawSitePicker(float top, bool fly)
        {
            if (LegacyCapture)
            {
                Card(Box(54,top-7,290,248),Ink);
                for (int i = 0; i < data.regions.Length; i++)
                    if (Button(Box(60,top+i*39,278,35),data.regions[i].name,active:siteIndex == i,size:17))
                    {
                        Select(i);
                        showSites = false;
                    }
                return;
            }

            const float pinSize = 33;
            Card(Box(54,top,1017,365),Ink);
            var map = Box(70,top+16,666,333);
            siteMap ??= Resources.Load<Texture2D>("Film/earth");
            if (siteMap != null)
            {
                GUI.DrawTexture(map,siteMap,ScaleMode.StretchToFill);
                Fill(map,Alpha(Ink,.55f));
            }
            else Fill(map,Panel);
            for (int i = 1; i < 6; i++) Fill(Box(map.x+i*map.width/6,map.y,1,map.height),Alpha(Mint,.23f));
            for (int i = 1; i < 4; i++) Fill(Box(map.x,map.y+i*map.height/4,map.width,1),Alpha(Mint,.23f));
            DrawButtonEdge(map,Line);

            DrawIcon("Location",new Vector2(798,top+71),Mint,3);
            Text(Box(766,top+106,275,51),"Choose a place",32,White,headingFont);
            Text(Box(766,top+162,269,64),fly ? "Select a pin to fly there." : "Select a pin to see its records.",
                23,Dim,bodyFont,TextAnchor.UpperLeft,true);
            Tag(766,top+248,"CURRENT SITE",Dim);
            Text(Box(766,top+277,282,41),Region.name,23,Mint,strongFont);

            for (int i = 0; i < data.regions.Length; i++)
            {
                var region = data.regions[i];
                var position = new Vector2(map.x+((region.longitude+180)/360)*map.width,
                    map.y+((90-region.latitude)/180)*map.height);
                var marker = Box(position.x-pinSize/2,position.y-pinSize/2,pinSize,pinSize);
                bool selected = siteIndex == i;
                Color face = ButtonBackground(marker,selected,false,true);
                var state = ButtonInput(marker,region.name);
                Fill(marker,face);
                DrawButtonEdge(marker,state.focused ? Yellow : Mint);
                DrawIcon("Location",position,selected ? Ink : Mint);
                float labelWidth = region.name.Length > 19 ? 174 : 155;
                float labelX = region.id != GuidedRainOrigin && marker.xMax+3+labelWidth < map.xMax
                    ? marker.xMax+3 : marker.x-labelWidth-3;
                var label = Box(labelX,position.y-12,labelWidth,24);
                Fill(label,Ink);
                Text(Box(label.x+5,label.y,label.width-10,label.height),region.name,16,selected ? Mint : White,strongFont);
                if (!state.pressed) continue;
                if (fly && !selected) BeginSiteTransfer(i);
                else Select(i);
                showSites = false;
                archiveFilter = ArchiveFilter.None;
                evidenceIndex = 0;
                return;
            }
        }

        void DrawArchiveRecord(EvidenceSeries[] records)
        {
            if (records.Length == 0)
            {
                Text(Box(56,378,1300,60),"No matching record for this site. Choose another variable.",27,White);
                return;
            }
            var record = records[Mathf.Clamp(evidenceIndex,0,records.Length-1)];
            if (archiveRecordId != record.id)
            {
                archiveRecordId = record.id;
                archiveMode = archiveMonth = archiveValuePage = 0;
            }
            var metadata = Exported(record);
            Card(Box(56,353,1488,457),Panel);
            Text(Box(84,364,1111,52),record.variable,36,White,headingFont);
            Text(Box(85,417,1386,27),record.product+" · "+ArchiveFootprint(record),19,Dim);
            if (records.Length > 1)
            {
                if (Button(Box(1234,375,45,38),"‹")) evidenceIndex = (evidenceIndex+records.Length-1)%records.Length;
                Text(Box(1281,375,194,38),"Product "+(evidenceIndex+1)+" / "+records.Length,19,Dim,dataFont,TextAnchor.MiddleCenter);
                if (Button(Box(1475,375,45,38),"›")) evidenceIndex = (evidenceIndex+1)%records.Length;
            }
            bool hasModes = metadata?.monthFindings?.Length == 12 && metadata.anomalyFinding != null;
            if (hasModes)
            {
                string[] modes = { "Annual values", "One calendar month", "Monthly anomalies" };
                for (int i=0;i<modes.Length;i++)
                    if (Button(Box(85+i*200,455,190,37),modes[i],false,archiveMode == i,true,17))
                    {
                        archiveMode = i;
                        archiveValuePage = 0;
                    }
                if (archiveMode == 1)
                {
                    if (Button(Box(698,455,44,37),"‹")) archiveMonth = (archiveMonth + 11) % 12;
                    Text(Box(750,455,152,37),Invariant.DateTimeFormat.GetMonthName(archiveMonth+1),19,White,dataFont,TextAnchor.MiddleCenter);
                    if (Button(Box(911,455,44,37),"›")) archiveMonth = (archiveMonth + 1) % 12;
                }
            }
            if (Button(Box(1324,455,191,37),showArchiveValues ? "Show chart" : "Inspect values",false,false,true,17))
                showArchiveValues = !showArchiveValues;
            var selected = SelectArchiveTrend(record);
            if (showArchiveValues) DrawArchiveTable(record,selected);
            else if (selected?.points is { Length: > 1 }) DrawHistoricalChart(selected,Box(165,522,1319,124));
            else if (selected == null && record.years is { Length: > 1 }) DrawChart(record,Box(165,522,1319,124));
            else Text(Box(85,522,1319,50),"Too few values to plot this time view. Use Inspect values to see available dates.",21,Dim);
            var finding = Usable(record) ? MakeFinding(record,selected) : null;
            if (finding != null)
            {
                Text(Box(85,690,1410,32),$"{finding.aggregation} · {finding.window} · {finding.count} values",19,Dim);
                Text(Box(85,724,1132,32),"Theil–Sen · "+FindingRate(finding)+" · "+SignificanceLabel(finding,false),19,White);
                Text(Box(85,756,1132,44),finding.hasPValue ? TestSummary(finding) : finding.reason,
                    17,Dim,bodyFont,TextAnchor.UpperLeft,true);
                bool filed = investigation?.origin?.id == finding.id;
                if (Button(Box(1240,724,275,51),filed ? "Finding filed" : "File this finding",true,false,
                        finding.hasRate && !filed && CanFile(record),22))
                    FileOrigin(record,selected);
            }
            else Text(Box(85,698,1410,41),"The archive has too few usable values for a trend in this view.",21,Dim);
            if (Button(Box(56,828,192,38),"Open data source",false,false,!string.IsNullOrEmpty(record.url),17)) Application.OpenURL(record.url);
            if (investigation == null && Button(Box(264,828,232,38),"Browse cases",icon:"Search")) OpenCasePicker();
            if (!string.IsNullOrEmpty(finding?.reviewer))
                Text(Box(522,829,1018,38),$"Reviewed by {finding.reviewer} · {finding.test}",18,Dim);
        }

        void DrawHistoricalChart(ArchiveTrend trend, Rect chart, bool includeZero = false, int selectedIndex = -1)
        {
            float min = trend.points.Min(p => p.value), max = trend.points.Max(p => p.value);
            if (includeZero) { min = Mathf.Min(min,0); max = Mathf.Max(max,0); }
            if (max <= min) { min -= 1; max += 1; }
            float first = trend.points[0].t, last = trend.points[^1].t;
            for (int i=0;i<3;i++)
            {
                float y = chart.y + i*chart.height/2;
                Fill(Box(chart.x,y,chart.width,1),Line);
                Text(Box(chart.x-83,y-10,73,20),Mathf.Lerp(max,min,i/2f).ToString("0.##",Invariant),14,Dim,dataFont,TextAnchor.MiddleRight);
            }
            Vector2 Plot(ArchiveTrendPoint p) => new(
                Mathf.Lerp(chart.x,chart.xMax,Mathf.InverseLerp(first,last,p.t)),
                Mathf.Lerp(chart.yMax,chart.y,Mathf.InverseLerp(min,max,p.value)));
            if (includeZero) Fill(Box(chart.x,Plot(new ArchiveTrendPoint { value = 0 }).y,chart.width,1),Dim);
            if (selectedIndex >= 0) Fill(Box(Plot(trend.points[selectedIndex]).x,chart.y,2,chart.height),Yellow);
            float maxGap = trend.mode == "anomaly" ? .084f : 1.001f;
            var previous = trend.points[0];
            foreach (var point in trend.points)
            {
                var pos = Plot(point);
                if (point.t > previous.t && point.t-previous.t <= maxGap) DrawChartSegment(Plot(previous),pos,Aqua);
                Fill(Box(pos.x-1,pos.y-1,2,2),White);
                previous = point;
            }
            Text(Box(chart.x,chart.yMax+5,150,27),trend.firstDate,15,Dim,dataFont);
            Text(Box(chart.xMax-150,chart.yMax+5,150,27),trend.lastDate,15,Dim,dataFont,TextAnchor.MiddleRight);
        }

        void DrawArchiveTable(EvidenceSeries record, ArchiveTrend trend, float top = 519)
        {
            const int size = 16;
            int count = trend?.points?.Length ?? record.years?.Length ?? 0;
            int offset = Mathf.Clamp(archiveValuePage*size,0,Mathf.Max(0,count-1));
            for (int i=0;i<Mathf.Min(size,count-offset);i++)
            {
                string date = trend == null ? record.years[offset+i].year.ToString(Invariant) : trend.points[offset+i].date;
                float value = trend == null ? record.years[offset+i].value : trend.points[offset+i].value;
                Text(Box(83+(i/4)*310,top+(i%4)*35,301,30),$"{date,-9}   {value,8:0.###} {record.unit}",17,White,dataFont);
            }
            if (Button(Box(1341,top,174,39),"Previous values",false,false,offset > 0,17)) archiveValuePage--;
            if (Button(Box(1341,top+50,174,39),"Next values",false,false,offset+size < count,17)) archiveValuePage++;
        }
    }
}
