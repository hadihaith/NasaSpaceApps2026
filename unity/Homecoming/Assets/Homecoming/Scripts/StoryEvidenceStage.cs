using System;
using UnityEngine;

namespace Homecoming
{
    public sealed partial class StoryStage
    {
        public const float MonitorStarts = 3.55f, ArchiveDocks = 5.6f, MonitorChangesPage = 9.1f;
        internal static readonly Vector3 ArchiveSocket = new Vector3(.99f, .77f, 1.24f);
        internal static readonly Vector3 ReaderSwitch = new Vector3(-.60f, .81f, 1.27f);
        static readonly Color EvidencePaper = new Color(.96f, .88f, .74f);
        static readonly Color EvidenceMint = new Color(.47f, .85f, .76f);
        static readonly Color EvidenceOrange = new Color(1, .56f, .31f);
        GameObject evidenceStand, evidenceMonitor;
        Transform cartridge, readerSwitch;
        Material evidenceLampMaterial, evidenceMetal;
        TextMesh monitorKicker, evidenceTitle, evidenceVariable;
        GameObject evidenceBrief, savedRunsPanel;
        TextMesh modelValue, modelLabel, recordValue, recordPeriod;
        readonly TextMesh[] runSites = new TextMesh[4], runPeriods = new TextMesh[4], runValues = new TextMesh[4];
        HomecomingReport displayedFindings;
        int evidencePage = -1;

        public bool EvidenceMonitorVisible => evidenceMonitor != null && evidenceMonitor.activeInHierarchy;

        void BuildEvidenceStand()
        {
            var barrel = cannon.transform.Find("Barrel").GetComponent<Renderer>().sharedMaterial;
            evidenceMetal = new Material(barrel) { name = "Reader enamel", color = new Color(.11f, .23f, .27f) };
            var shell = report.transform.Find("Report paper").GetComponent<Renderer>().sharedMaterial;
            evidenceLampMaterial = new Material(televisionMaterial) { name = "Archive cartridge and reader", mainTexture = null };
            evidenceStand = new GameObject("Hearing evidence reader");
            evidenceStand.transform.SetParent(bridge.transform, false);
            var parent = evidenceStand.transform;
            Part(PrimitiveType.Cube, parent, V(.19f, .36f, 1.06f), V(.84f, .72f, .54f), evidenceMetal);
            Part(PrimitiveType.Cube, parent, V(.19f, .72f, 1.14f), V(1.84f, .12f, .50f), evidenceMetal);
            Part(PrimitiveType.Cube, parent, V(.19f, .32f, 1.335f), V(.66f, .20f, .045f), barrel);
            readerSwitch = Part(PrimitiveType.Cube, parent, ReaderSwitch - V(0, .03f, 0), V(.19f, .07f, .13f), shell).transform;
            Part(PrimitiveType.Cube, parent, ArchiveSocket - V(0, .03f, 0), V(.21f, .065f, .15f), evidenceLampMaterial);
            cartridge = Part(PrimitiveType.Cube, parent, Vector3.zero, V(.23f, .11f, .13f), evidenceLampMaterial).transform;
            Part(PrimitiveType.Cube, cartridge, V(0, .47f, .16f), V(.75f, .16f, .49f), shell);
        }

        void BuildEvidenceMonitor()
        {
            var surface = screen.transform;
            evidenceMonitor = new GameObject("CRT evidence display");
            var display = evidenceMonitor.transform;
            display.SetParent(surface.parent, false);
            display.localPosition = surface.localPosition + Vector3.forward * .014f;
            display.localRotation = surface.localRotation;

            var heading = Resources.Load<Font>("Fonts/Display");
            var body = Resources.Load<Font>("Fonts/Body");
            var mono = Resources.Load<Font>("Fonts/Mono");
            monitorKicker = MonitorLabel(display, "Source", "", mono, 0, .62f, .072f, EvidenceMint, TextAnchor.UpperCenter);
            evidenceTitle = MonitorLabel(display, "Title", "", heading, 0, .49f, .19f, EvidencePaper, TextAnchor.UpperCenter);
            evidenceVariable = MonitorLabel(display, "Subtitle", "", body, 0, .27f, .091f, EvidencePaper, TextAnchor.UpperCenter);

            evidenceBrief = new GameObject("Simulation and Earth record");
            evidenceBrief.transform.SetParent(display, false);
            var brief = evidenceBrief.transform;
            MonitorLabel(brief, "Simulation label", "SAVED SIMULATION", heading, -.55f, .06f, .117f, EvidenceOrange, TextAnchor.UpperCenter);
            modelValue = MonitorLabel(brief, "Weather result", "", body, -.55f, -.11f, .110f, EvidencePaper, TextAnchor.UpperCenter);
            modelLabel = MonitorLabel(brief, "Weather unit and scenario", "", mono, -.55f, -.25f, .066f, EvidencePaper, TextAnchor.UpperCenter);
            MonitorLabel(brief, "Archive label", "HISTORICAL RECORD", heading, .43f, .06f, .117f, EvidenceMint, TextAnchor.UpperCenter);
            recordPeriod = MonitorLabel(brief, "Archive period", "", mono, .43f, -.105f, .069f, EvidencePaper, TextAnchor.UpperCenter);
            recordValue = MonitorLabel(brief, "Archive result", "", body, .43f, -.219f, .086f, EvidencePaper, TextAnchor.UpperCenter);

            savedRunsPanel = new GameObject("Saved runs");
            savedRunsPanel.transform.SetParent(display, false);
            for (int i = 0; i < runSites.Length; i++)
            {
                float x = i % 2 == 0 ? -.55f : .55f;
                float y = i < 2 ? .10f : -.29f;
                runSites[i] = MonitorLabel(savedRunsPanel.transform, "Site", "", heading, x, y, .124f, EvidencePaper, TextAnchor.UpperCenter);
                runPeriods[i] = MonitorLabel(savedRunsPanel.transform, "Period", "", mono, x, y - .136f, .066f, EvidenceMint, TextAnchor.UpperCenter);
                runValues[i] = MonitorLabel(savedRunsPanel.transform, "Final root-zone value", "", body, x, y - .237f, .109f, EvidenceOrange, TextAnchor.UpperCenter);
            }
        }

        static TextMesh MonitorLabel(Transform panel, string name, string value, Font font, float x, float y,
            float size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(panel, false);
            go.transform.localPosition = new Vector3(x, y, 0);
            var label = go.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 160;
            label.characterSize = size / 16f;
            label.anchor = anchor;
            label.alignment = TextAlignment.Center;
            label.color = color;
            label.text = value;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return label;
        }

        void EvaluateEvidenceReader(string shot, float t, bool reduced, HomecomingReport findings)
        {
            bool evidence = shot == "evidence";
            if (evidence && evidenceStand == null) BuildEvidenceStand();
            if (evidenceStand != null) evidenceStand.SetActive(evidence);
            bool showMonitor = evidence && t >= MonitorStarts;
            if (showMonitor && evidenceMonitor == null) BuildEvidenceMonitor();
            if (evidenceMonitor != null) evidenceMonitor.SetActive(showMonitor);
            if (!evidence) return;

            var hand = crew[2].ReportGrip;
            var carried = hand != null ? hand.position + V(-.06f, .04f, .06f) : ArchiveSocket + Vector3.up * .4f;
            float inserted = reduced ? (t >= ArchiveDocks ? 1 : 0) :
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5.04f, ArchiveDocks, t));
            cartridge.position = Vector3.Lerp(carried, ArchiveSocket, inserted);
            cartridge.rotation = Quaternion.Lerp(Quaternion.Euler(0, 18, -17), Quaternion.identity, inserted);
            evidenceLampMaterial.color = Color.Lerp(new Color(.21f, .67f, .64f), new Color(.28f, 1, .94f), inserted);
            readerSwitch.localPosition = ReaderSwitch - Vector3.up * (.03f + .035f * Pulse(t, MonitorChangesPage, .14f));
            if (!showMonitor) return;

            televisionMaterial.mainTexture = Texture2D.whiteTexture;
            televisionMaterial.color = new Color(.035f, .12f, .15f);
            int page = t >= MonitorChangesPage ? 2 : t >= ArchiveDocks ? 1 : 0;
            if (page == evidencePage && ReferenceEquals(findings, displayedFindings)) return;
            evidencePage = page;
            displayedFindings = findings;
            WriteEvidenceMonitor(page, findings);
        }

        void WriteEvidenceMonitor(int page, HomecomingReport findings)
        {
            bool runsPage = page == 2;
            evidenceBrief.SetActive(!runsPage);
            savedRunsPanel.SetActive(runsPage);
            monitorKicker.text = "I.A.D.  /  HEARING RECORD";
            if (runsPage)
            {
                evidenceTitle.text = "LATEST SAVED RUNS";
                var runs = findings?.recentRuns;
                evidenceVariable.text = runs is { Length: > 0 } ? "Final root-zone results" : "No saved runs to display";
                for (int i = 0; i < runSites.Length; i++)
                {
                    var run = runs != null && i < runs.Length ? runs[i] : null;
                    runSites[i].text = run?.site ?? "";
                    runSites[i].characterSize = (runSites[i].text.Length > 18 ? .104f : .124f) / 16f;
                    runPeriods[i].text = run?.period ?? "";
                    runValues[i].text = run?.edited is { Length: > 0 }
                        ? $"{run.edited[^1]:0.#}{(run.unit == "mm storage" ? " mm storage" : "% wetness index")}" : "";
                }
                return;
            }

            var link = findings?.link;
            evidenceTitle.text = link?.site ?? "EVIDENCE BRIEF";
            evidenceVariable.text = link?.variable ?? "Simulation and Earth history";
            if (link?.editedWeather is { Length: > 0 } && link.inheritedWeather is { Length: > 0 })
            {
                modelValue.text = $"{link.editedWeather[^1]:0.#} / {link.inheritedWeather[^1]:0.#}";
                modelLabel.text = $"{link.simulationUnit} · last month\nEdited / inherited";
            }
            else
            {
                modelValue.text = "No linked run";
                modelLabel.text = "";
            }

            string archive = page == 1 ? link?.historical : null;
            int separator = archive?.IndexOf(" · ", StringComparison.Ordinal) ?? -1;
            recordPeriod.text = separator >= 0 ? archive.Substring(0, separator) : "";
            recordValue.text = separator >= 0 ? archive.Substring(separator + 3).Replace(" per decade", "\nper decade") :
                archive ?? (page == 0 ? "Loading archive…" : "No linked record");
        }
    }
}
