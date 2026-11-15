using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation
    {
        CascadeResult scenarioCascadeResult;
        FieldTrial scenarioCascadeTrial;
        bool scenarioCascadeOpen, cascadeTextView;
        int cascadeSelection = -1;

        // The displayed trial identifies the current playback, even while the active experiment changes.
        void BindScenarioCascade(CascadeResult result)
        {
            scenarioCascadeTrial = trial;
            scenarioCascadeResult = result;
            scenarioCascadeOpen = cascadeTextView = false;
            scenarioTable = scenarioMethods = false;
            cascadeSelection = -1;
        }

        CascadeResult CurrentScenarioCascade()
        {
            var preview = PendingPreview;
            if (!ReferenceEquals(scenarioCascadeTrial,trial))
                BindScenarioCascade(preview == null ? null : FieldCascade.Analyze(preview,Region.id));

            if (preview == null && scenarioCascadeResult?.provenance == CascadeProvenance.Preview)
            {
                var saved = Campaign?.GetRun(scenarioCascadeResult.runId);
                scenarioCascadeResult = saved == null ? null : FieldCascade.Analyze(saved,Region.id);
            }
            return scenarioCascadeResult;
        }

        void DrawScenarioCascade(CascadeResult result)
        {
            const int x = 946, width = 591;
            var panel = Box(928,176,627,512);
            Card(panel,Panel);
            Fill(Box(panel.x,panel.y,76,3),Yellow);
            Fill(Box(x,233,width,1),Line);
            Text(Box(x,187,367,47),"Weather cascade",35,White,headingFont);
            if (Button(Box(1381,190,156,38),cascadeTextView ? "Show diagram" : "Text view",active:cascadeTextView,size:18))
                cascadeTextView = !cascadeTextView;

            int cursor = result.startMonth + DisplayedMonth;
            int revealed = 0;
            bool modeledResponseVisible = false;
            while (revealed < result.effects.Length && result.effects[revealed].sample.monthIndex <= cursor)
            {
                modeledResponseVisible |= result.Series(result.effects[revealed].metric).kind != CascadeKind.Direct;
                revealed++;
            }
            if (cascadeSelection >= revealed) cascadeSelection = -1;
            int selected = cascadeSelection;
            if (selected < 0 && revealed > 0)
            {
                selected = revealed - 1;
                while (selected > 0 && result.effects[selected - 1].sample.monthIndex == result.effects[selected].sample.monthIndex)
                    selected--;
            }

            string status = CascadePlaybackStatus(result,modeledResponseVisible,cursor);
            Text(Box(x,237,width,43),status,18,White,bodyFont,TextAnchor.MiddleLeft,true);

            if (Button(Box(x,285,128,36),"Previous",enabled:selected > 0,size:17))
                selected = cascadeSelection = selected - 1;
            if (Button(Box(1409,285,128,36),"Next",enabled:selected >= 0 && selected + 1 < revealed,size:17))
                selected = cascadeSelection = selected + 1;
            Text(Box(1081,285,321,36),selected < 0 ? "No effects revealed" : $"Effect {selected + 1} of {revealed} revealed",
                16,Dim,dataFont,TextAnchor.MiddleCenter);

            if (selected >= 0) DrawCascadeEffect(result,result.effects[selected],Box(x,331,width,300));
            else
            {
                Fill(Box(x,350,width,1),Line);
                string hint = result.status == CascadeStatus.Unavailable ? result.unavailableReason :
                    DisplayedMonth < duration - 1 ? "Play or select a month to check for effects." :
                    "The weather and modeled outcomes stayed within their display thresholds.";
                Text(Box(x,365,width,78),hint,22,Dim,bodyFont,TextAnchor.UpperLeft,true);
            }

            if (Button(Box(x,637,148,40),"Soil water",size:18,icon:"Back")) scenarioCascadeOpen = false;
            if (Button(Box(1101,637,208,40),comparing ? "View edited weather" : "View saved weather",
                active:comparing,size:17)) CompareBaseline();
            if (Button(Box(1317,637,220,40),"Open intelligence report",size:18)) OpenCascadeReport(result);
        }

        string CascadePlaybackStatus(CascadeResult result, bool modeledResponseVisible, int cursor)
        {
            if (result.status == CascadeStatus.Unavailable) return "Cascade comparison unavailable.";
            string month = CascadePlaybackDate(cursor);
            string status = modeledResponseVisible ? $"Modeled response revealed through {month}." :
                $"No modeled effects exceeded their display thresholds through {month}.";
            if (!result.hasPhysicalLedger) return status + " Physical water ledger unavailable.";
            if (result.series.Any(series => !series.available && series.metric != CascadeMetric.RootZoneWetnessIndex))
                return status + " Some outputs are unavailable.";
            return status;
        }

        static string CascadePlaybackDate(int month) => FieldCalendar.Date(month).ToString("MMM yyyy",Invariant);

        static string CascadePlaybackTransition(CascadeEffect effect)
        {
            string direction = effect.sample.direction == CascadeDirection.Increase ? "Higher" : "Lower";
            return effect.transition switch
            {
                CascadeTransition.ReturnToQuiet => "Back within threshold",
                CascadeTransition.Reversal => $"Reversed to {direction.ToLowerInvariant()}",
                CascadeTransition.Resumed => $"Resumed {direction.ToLowerInvariant()}",
                _ => direction
            };
        }

        void DrawCascadeEffect(CascadeResult result, CascadeEffect effect, Rect area)
        {
            var series = result.Series(effect.metric);
            var sample = effect.sample;
            Color tint = sample.direction == CascadeDirection.LittleChange ? Dim : Yellow;
            string category = series.kind switch
            {
                CascadeKind.Direct => "Direct weather",
                CascadeKind.Physical => "Modeled water",
                _ => "Teaching index"
            };
            Fill(Box(area.x,area.y,area.width,1),Line);
            Text(Box(area.x,area.y+3,area.width,25),$"{CascadePlaybackDate(sample.monthIndex)}   ·   {category}",15,Dim,dataFont);
            Text(Box(area.x,area.y+26,area.width-222,38),series.label,27,White,headingFont);
            Text(Box(area.xMax-222,area.y+26,222,38),CascadePlaybackTransition(effect),17,tint,strongFont,TextAnchor.MiddleRight);

            float column = area.width / 3;
            void Reading(int index, string label, string value, Color color)
            {
                float x = area.x + column * index;
                Text(Box(x,area.y+67,column-7,19),label,15,Dim,strongFont);
                Text(Box(x,area.y+88,column-7,24),value,16,color,dataFont);
            }
            Reading(0,"Inherited",$"{sample.inherited.ToString("0.###",Invariant)} {series.valueUnit}",White);
            Reading(1,"Edited",$"{sample.edited.ToString("0.###",Invariant)} {series.valueUnit}",White);
            Reading(2,"Change",$"{CascadePlaybackDelta(sample.delta)} {series.deltaUnit}",tint);

            if (cascadeTextView)
            {
                Text(Box(area.x,area.y+118,area.width,85),effect.explanation,17,White,bodyFont,TextAnchor.UpperLeft,true);
                string inputs = effect.inputs.Length == 0 ? "No changed weather or land inputs in this playback window." :
                    "Changed inputs · " + string.Join("  /  ",effect.inputs.Select(key => CascadePlaybackInput(result,key)));
                if (series.kind == CascadeKind.Direct)
                    inputs = CascadePlaybackDial(result,effect);
                Text(Box(area.x,area.y+207,area.width,93),inputs,16,Dim,bodyFont,TextAnchor.UpperLeft,true);
            }
            else
            {
                DrawCascadeInputDiagram(result,effect,Box(area.x,area.y+116,area.width,96),tint);
                Text(Box(area.x,area.y+216,area.width,84),effect.explanation,17,White,bodyFont,TextAnchor.UpperLeft,true);
            }
        }

        static string CascadePlaybackDelta(double delta)
        {
            if (delta != 0 && Math.Abs(delta) < .001)
                return delta.ToString("+0.##E+0;-0.##E+0",Invariant);
            return delta.ToString("+0.###;-0.###;0",Invariant);
        }

        string CascadePlaybackDial(CascadeResult result, CascadeEffect effect)
        {
            var frame = result.frames[effect.sample.monthIndex - result.startMonth];
            int control = (int)effect.metric;
            int inherited = frame.inherited.forcing[control], edited = frame.edited.forcing[control];
            return $"Dial setting · Inherited {Signed(inherited)}{data.controls[control].unit}  /  Edited {Signed(edited)}{data.controls[control].unit}";
        }

        static string CascadePlaybackInput(CascadeResult result, CascadeSampleKey key)
        {
            var series = result.Series(key.metric);
            var sample = series.samples[key.monthIndex - result.startMonth];
            return $"{series.label}, {CascadePlaybackDate(key.monthIndex)}, {CascadePlaybackDelta(sample.delta)} {series.deltaUnit}";
        }

        void DrawCascadeInputDiagram(CascadeResult result, CascadeEffect effect, Rect area, Color tint)
        {
            Fill(area,Ink);
            if (result.Series(effect.metric).kind == CascadeKind.Direct)
            {
                Text(Box(area.x+12,area.y+14,area.width-24,60),CascadePlaybackDial(result,effect),17,Dim,bodyFont,
                    TextAnchor.MiddleLeft,true);
                return;
            }

            var links = effect.inputs.Where(key => HasCascadePlaybackLink(result,key)).ToArray();
            if (links.Length == 0)
            {
                string message = effect.inputs.Length == 0 ? "No changed inputs in this playback window." :
                    "Changed inputs stayed within display thresholds. See Text view for their values.";
                Text(Box(area.x+12,area.y+4,area.width-24,area.height-8),message,17,Dim,bodyFont,TextAnchor.MiddleLeft,true);
                return;
            }

            float rowHeight = Mathf.Min(22,(area.height - 8) / links.Length);
            float top = area.center.y - links.Length * rowHeight / 2;
            Rect output = Box(area.x+354,area.center.y-26,area.width-366,52);
            Fill(output,ChoiceFace);
            Text(Box(output.x+8,output.y+2,output.width-16,output.height-4),result.Series(effect.metric).label,
                18,White,strongFont,TextAnchor.MiddleCenter,true);
            bool moving = !reduced && running && !paused && effect.sample.monthIndex == result.startMonth + DisplayedMonth;
            float progress = moving ? Mathf.Clamp01((runClock - RunTime * DisplayedMonth / duration) / .24f) : 1;
            for (int i = 0; i < links.Length; i++)
            {
                var key = links[i];
                float y = top + i * rowHeight;
                Text(Box(area.x+11,y,291,rowHeight),
                    $"{result.Series(key.metric).label} · {CascadePlaybackDate(key.monthIndex)}",15,Dim);
                var from = new Vector2(area.x+305,y+rowHeight/2);
                var to = new Vector2(output.x,output.center.y);
                DrawChartSegment(from,to,Alpha(tint,.35f));
                DrawChartSegment(from,Vector2.Lerp(from,to,progress),tint);
            }
        }

        static bool HasCascadePlaybackLink(CascadeResult result, CascadeSampleKey key)
        {
            var series = result.Series(key.metric);
            var sample = series.samples[key.monthIndex - result.startMonth];
            if (sample.direction == CascadeDirection.LittleChange) return false;
            for (int i = result.effects.Length - 1; i >= 0; i--)
            {
                var candidate = result.effects[i];
                if (candidate.metric == key.metric && candidate.sample.monthIndex <= key.monthIndex)
                    return candidate.sample.direction == sample.direction;
            }
            return false;
        }
    }
}
