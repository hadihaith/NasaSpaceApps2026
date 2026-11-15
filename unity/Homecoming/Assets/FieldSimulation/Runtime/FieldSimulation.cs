using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Homecoming.Field
{
    public sealed partial class FieldSimulation : MonoBehaviour
    {
        public FieldWorld world;
        public TextAsset dataSource;
        public Font headingFont, bodyFont, strongFont, dataFont;
        public Texture2D commander;

        public const string SaveKey = "homecoming.unity.field.v1";
        enum View { Lab, Descent, Field, Journal, Archive, Report, SiteTransfer, Submission }
        View view;
        ClimateData data;
        FieldSave save;
        FieldCampaign campaign;
        ScenarioPreview pendingPreview;
        FieldTrial referenceTrial;
        bool legacyPlayback;
        int shownMonth;
        bool loading;
        public string SimulationStatus { get; private set; }
        public FieldCampaign Campaign => campaign;
        public ScenarioPreview PendingPreview => pendingPreview;
        public string LastCommittedRevisionId { get; private set; }
        public bool HasPendingAdvance => pendingPreview != null && view == View.Field && !running;
        public int DisplayedMonth => shownMonth;
        int siteIndex = 4, duration = 12, evidenceIndex, noteIndex;
        Forcing settings;
        FieldTrial trial;
        FieldMonth displayed, before;
        float elapsed, labClock, runClock, descentClock, landingVeil;
        const float PullbackEnd = 1.65f, OrbitEntryEnd = 2.55f, OrbitTravelEnd = 4.95f, OrbitExitEnd = 5.9f, TransferEnd = 7.65f;
        int transferTarget = -1;
        Pose transferOrigin;
        float transferFov, transferTime, transferVeil;
        bool transferTerrainReady;
        bool reduced, muted, running, paused, lookHeld, showResult, showTelemetry, comparing;
        readonly List<Discovery> added = new();
        Discovery primary;
        string evidenceKey;
        const float ObservationHold = 2.0f;
        float DescentTime => reduced ? .35f : 5.6f;
        float RunTime => duration == 1 ? 4.0f : duration == 3 ? 6.0f : 10.2f;
        RegionClimate Region => data.regions[siteIndex];
        public bool CanSubmit => save.notes.Count >= data.requiredDiscoveries && save.evidence.Count > 0;
        public bool Running => running;

        void Awake()
        {
            QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 4);
            Application.targetFrameRate = 60;
            data = JsonUtility.FromJson<ClimateData>(dataSource.text);
#if HOMECOMING_FIELD_PREVIEW || UNITY_EDITOR
            var args = Environment.GetCommandLineArgs();
            legacyPlayback = Array.IndexOf(args, "--field-preview") >= 0 && Array.IndexOf(args, "--investigation-preview") < 0;
#endif
            loading = true;
            Load();
            muted = PlayerPrefs.GetInt("homecoming.unity.mute", 0) == 1;
            reduced = PlayerPrefs.GetInt("homecoming.unity.reduced", 0) == 1;
            world.Initialize();
            Select(siteIndex);
            loading = false;
            var history = StorySession.ConsumeHistoryRequest();
            if (history != SimulationHistory.None) OpenHistory(history);
        }

        void Load()
        {
#if HOMECOMING_FIELD_PREVIEW || UNITY_EDITOR
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--investigation-preview") >= 0 || Array.IndexOf(args, "--site-transfer-preview") >= 0)
            {
                campaign = FieldCampaign.Open(data, selectedSiteId: Region.id);
                RestoreJournal();
                return;
            }
#endif
            if (legacyPlayback)
            {
                save = new FieldSave();
                settings = new Forcing(0, 150, 40, 0);
                return;
            }

            FieldSave legacy = null;
            string operation = StorySession.OperationId;
            string legacyJson = string.IsNullOrEmpty(operation) ? PlayerPrefs.GetString(SaveKey, "") : "";
            if (legacyJson.Length > 0)
            {
                try { legacy = JsonUtility.FromJson<FieldSave>(legacyJson); }
                catch (ArgumentException error) { Debug.LogWarning(error.Message); }
            }

            try
            {
                string fileName = string.IsNullOrEmpty(operation) ? "field-campaign-v2.json" : $"field-campaign-{operation}.json";
                campaign = FieldCampaign.Open(data, Path.Combine(Application.persistentDataPath, fileName),
                    legacy, Region.id);
            }
            catch (Exception error) when (IsCampaignError(error))
            {
                // Leave the on-disk campaign intact when Unity cannot read it.
                SimulationStatus = "Campaign unavailable. " + error.Message;
                Debug.LogError(SimulationStatus);
                save = new FieldSave();
                return;
            }
            RestoreJournal();
            if (!campaign.CanProject)
                SimulationStatus = "The saved world uses different simulation inputs. Recorded runs are available for replay.";
            int savedIndex = Array.FindIndex(data.regions, region => region.id == campaign.SelectedSiteId);
            if (savedIndex >= 0) siteIndex = savedIndex;
        }

        static bool IsCampaignError(Exception error) => error is IOException || error is UnauthorizedAccessException ||
            error is InvalidOperationException || error is ArgumentException;

        void RestoreJournal()
        {
            save = campaign.Journal;
            save.notes.RemoveAll(note => !data.regions.Any(region => region.id == note.region) ||
                !data.discoveries.Any(discovery => discovery.id == note.id));
            save.evidence.RemoveAll(id => !data.evidence.Any(record => record.id == id));
        }

        void Persist()
        {
#if HOMECOMING_FIELD_PREVIEW || UNITY_EDITOR
            if (capture) return;
#endif
            if (campaign == null) return;
            try { campaign.SaveEvidence(save.evidence); }
            catch (Exception error) when (IsCampaignError(error))
            {
                RestoreJournal();
                SimulationStatus = error.Message;
            }
        }

        public void Select(int index)
        {
            if (index < 0 || index >= data.regions.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (view == View.SiteTransfer || !TrySelectSite(index)) return;
            ApplySelectedSite(index);
        }

        bool TrySelectSite(int index)
        {
            if (campaign != null && !loading && campaign.SelectedSiteId != data.regions[index].id)
            {
                try { campaign.SelectSite(data.regions[index].id); }
                catch (Exception error) when (IsCampaignError(error))
                {
                    SimulationStatus = error.Message;
                    return false;
                }
            }
            return true;
        }

        void ApplySelectedSite(int index)
        {
            siteIndex = index;
            world.SelectRegion(index, Region);
            displayed = SavedSiteFrame(Region);
            if (campaign != null) settings = displayed.forcing;
            world.Overview(labClock, reduced);
            world.Apply(displayed, 0, true, muted, false, reduced);
        }

        FieldMonth SavedSiteFrame(RegionClimate region)
        {
            if (campaign != null)
            {
                var revision = campaign.Current;
                var site = revision.Site(region.id);
                return StartFrame(region,site.current,revision.nextMonth,site.ActiveAt(revision.nextMonth));
            }
            var land = save.sites.Find(site => site.id == region.id)?.land ?? ClimateModel.Initial(region);
            return StartFrame(region,land,FieldCalendar.StartMonth+land.elapsed,default);
        }

        FieldMonth StartFrame(RegionClimate region, LandState land, int month, Forcing forcing)
        {
            var normal = ClimateModel.Normal(region, month);
            return new FieldMonth { land = land, normal = normal, weather = ClimateModel.ApplyForcing(normal, forcing),
                monthIndex = month, forcing = forcing };
        }

        bool BeginSiteTransfer(int index)
        {
            if (view != View.Lab || index == siteIndex || index < 0 || index >= data.regions.Length) return false;
            showSites = branchesOpen = branchComparisonOpen = false;
            if (reduced)
            {
                Select(index);
                return siteIndex == index;
            }
            var camera = world.fieldCamera;
            transferOrigin = new Pose(camera.transform.position,camera.transform.rotation);
            transferFov = camera.fieldOfView;
            transferTarget = index;
            transferTime = transferVeil = 0;
            transferTerrainReady = false;
            Go(View.SiteTransfer);
            return true;
        }

        void TickSiteTransfer(float time)
        {
            transferTime = Mathf.Min(TransferEnd,transferTime+time);
            if (transferTime < PullbackEnd)
            {
                transferVeil = world.TransferDeparture(transferOrigin,transferFov,transferTime/PullbackEnd);
                return;
            }
            if (transferTime < OrbitExitEnd)
            {
                float route = Mathf.InverseLerp(OrbitEntryEnd,OrbitTravelEnd,transferTime);
                float entry = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(PullbackEnd,OrbitEntryEnd,transferTime));
                float exit = Mathf.SmoothStep(0,1,Mathf.InverseLerp(OrbitTravelEnd,OrbitExitEnd,transferTime));
                world.TransferOrbit(Region,data.regions[transferTarget],route,Mathf.Max(entry,exit));
                transferVeil = Mathf.Max(entry*entry,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,exit)));
                return;
            }
            if (!transferTerrainReady)
            {
                var destination = data.regions[transferTarget];
                world.SelectRegion(transferTarget,destination);
                world.Apply(SavedSiteFrame(destination),0,true,muted,false,reduced);
                transferTerrainReady = true;
            }
            transferVeil = world.TransferArrival(labClock,reduced,Mathf.InverseLerp(OrbitExitEnd,TransferEnd,transferTime));
            if (transferTime >= TransferEnd) FinishSiteTransfer(true);
        }

        void FinishSiteTransfer(bool arrive)
        {
            if (view != View.SiteTransfer) return;
            int destination = transferTarget;
            view = View.Lab;
            transferTarget = -1;
            transferVeil = 0;
            focusIndex = 0;
            if (arrive && TrySelectSite(destination))
            {
                ApplySelectedSite(destination);
                return;
            }
            if (transferTerrainReady) world.SelectRegion(siteIndex,Region);
            world.RestoreTerrainCamera(transferOrigin,transferFov);
            world.Apply(displayed,0,true,muted,false,reduced);
        }

        public void Launch()
        {
            if (view != View.Lab) return;
            if (legacyPlayback)
            {
                var land = save.sites.Find(site => site.id == Region.id)?.land ?? ClimateModel.Initial(Region);
                before = StartFrame(Region,land,FieldCalendar.StartMonth+land.elapsed,default);
                trial = ClimateModel.Run(Region, land, settings, duration);
                referenceTrial = null;
            }
            else
            {
                if (campaign == null) return;
                try
                {
                    var revision = campaign.Current;
                    var site = revision.Site(Region.id);
                    if (site == null) throw new InvalidOperationException("This site is unavailable in the saved campaign.");
                    var inherited = site.ActiveAt(revision.nextMonth);
                    var preview = campaign.Preview(Region.id, FieldCampaign.Patch(inherited, settings, revision.nextMonth));
                    var projection = preview.Site(Region.id);
                    pendingPreview = preview;
                    trial = projection.scenario;
                    referenceTrial = projection.reference;
                    before = StartFrame(Region,site.current,revision.nextMonth,inherited);
                    SimulationStatus = null;
                }
                catch (Exception error) when (IsCampaignError(error))
                {
                    SimulationStatus = error.Message;
                    return;
                }
            }
            displayed = before;
            shownMonth = 0;
            added.Clear();
            primary = null;
            running = true;
            paused = false;
            showResult = false;
            showTelemetry = false;
            comparing = false;
            descentClock = runClock = 0;
            view = View.Descent;
        }

        void Arrive()
        {
            view = View.Field;
            landingVeil = 0;
            world.Land();
        }

        void Finish()
        {
            running = false;
            shownMonth = duration - 1;
            displayed = trial.months[shownMonth];
            if (legacyPlayback)
            {
                save.runs++;
                var previous = save.sites.Find(s => s.id == Region.id);
                if (previous == null) save.sites.Add(new SavedLand { id = Region.id, land = trial.Final.land });
                else previous.land = trial.Final.land;
                foreach (var id in trial.findings)
                {
                    if (save.notes.Exists(note => note.id == id)) continue;
                    save.notes.Add(new FieldNote { id = id, region = Region.id, run = save.runs });
                    added.Add(data.discoveries.First(d => d.id == id));
                }
            }
            trial.findings = ClimateModel.Findings(trial.before, trial.months, duration);
            SetPrimary(trial.findings);
            showResult = true;
        }

        void SetPrimary(List<string> findings)
        {
            // Show visible ground conditions before the downstream vegetation response.
            primary = added.Find(d => d.id == "waterlogging" || d.id == "drought") ?? added.FirstOrDefault();
            if (primary != null || findings.Count == 0) return;
            string id = findings.Contains("waterlogging") ? "waterlogging" : findings[0];
            primary = data.discoveries.FirstOrDefault(d => d.id == id);
        }

        public bool AdvancePreview()
        {
            if (!HasPendingAdvance) return false;
            try
            {
                int previousRun = save.runs;
                var revision = campaign.Advance(pendingPreview, duration);
                LastCommittedRevisionId = revision.id;
                pendingPreview = null;
                RestoreJournal();
                foreach (var note in save.notes.Where(note => note.run > previousRun))
                    added.Add(data.discoveries.First(d => d.id == note.id));
                SetPrimary(trial.findings);
                showResult = true;
                SimulationStatus = null;
                return true;
            }
            catch (Exception error) when (IsCampaignError(error))
            {
                SimulationStatus = error.Message;
                return false;
            }
        }

        public void CancelPreview()
        {
            if (view == View.Field || view == View.Descent) Go(View.Lab);
        }

        public bool ReplayRun(string runId, string siteId)
        {
            var run = campaign?.GetRun(runId);
            var projection = run?.Site(siteId);
            int index = Array.FindIndex(data.regions, region => region.id == siteId);
            if (projection == null || index < 0) return false;
            ReleaseLook();
            pendingPreview = null;
            siteIndex = index;
            world.SelectRegion(index, Region);
            trial = projection.scenario;
            referenceTrial = projection.reference;
            var first = trial.months[0];
            var inherited = referenceTrial.months[0];
            before = new FieldMonth { land = trial.before, normal = first.normal, monthIndex = first.monthIndex,
                weather = inherited.weather, forcing = inherited.forcing };
            duration = run.committedMonths;
            running = paused = comparing = showResult = false;
            added.Clear();
            trial.findings = ClimateModel.Findings(trial.before, trial.months, duration);
            SetPrimary(trial.findings);
            Arrive();
            SeekPreview(0);
            return true;
        }

        public void SeekPreview(int index)
        {
            if (view != View.Field || trial == null || index < 0 || index >= duration) return;
            shownMonth = index;
            displayed = comparing && referenceTrial != null ? referenceTrial.months[index] : trial.months[index];
            runClock = RunTime * index / duration;
            if (running) paused = true;
        }

        public FieldMonth ScenarioMonth(int index) => trial.months[index];
        public FieldMonth ReferenceMonth(int index) => referenceTrial.months[index];

        void CompareBaseline()
        {
            comparing = !comparing;
            displayed = comparing ? referenceTrial?.months[shownMonth] ?? before : trial.months[shownMonth];
            if (comparing) showResult = false;
        }

        void Go(View next)
        {
            if (view == View.SiteTransfer) return;
            if (view == View.Submission && submissionVoice != null) submissionVoice.Stop();
            ReleaseLook();
            paused = false;
            hotDial = -1;
            focusIndex = 0;
            view = next;
            switch (next)
            {
                case View.Lab:
                    pendingPreview = null;
                    running = false;
                    if (campaign != null)
                    {
                        int selected = Array.FindIndex(data.regions, region => region.id == campaign.SelectedSiteId);
                        if (selected >= 0) siteIndex = selected;
                    }
                    Select(siteIndex);
                    break;
                case View.Journal: noteIndex = Mathf.Max(0, save.notes.FindIndex(n => n.id == primary?.id)); break;
                case View.Archive: evidenceIndex = 0; break;
            }
        }

        void OpenArchive(string key)
        {
            evidenceKey = key;
            Go(View.Archive);
        }

        void ReleaseLook()
        {
            lookHeld = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
#if HOMECOMING_FIELD_PREVIEW || UNITY_EDITOR
            if (capture && Time.captureFramerate > 0) dt = 1f / Time.captureFramerate;
#endif
            elapsed += dt;
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
            if (view == View.SiteTransfer)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) FinishSiteTransfer(false);
                else if (Input.GetKeyDown(KeyCode.Space)) FinishSiteTransfer(true);
                else TickSiteTransfer(dt);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (lookHeld) ReleaseLook();
                else if (view == View.Descent) Arrive();
                else if (view == View.Field && showResult) showResult = false;
                else if (view == View.Field && running) paused = !paused;
                else if (view != View.Lab) Go(View.Lab);
                else if (showSites) showSites = false;
            }

            switch (view)
            {
                case View.Lab:
                    labClock += dt;
                    world.Overview(labClock, reduced);
                    if (legacyPlayback && !keyboardNavigation && Input.GetKeyDown(KeyCode.Return)) Launch();
                    break;
                case View.Descent:
                    descentClock += dt;
                    landingVeil = world.Descend(descentClock, reduced);
                    if (descentClock >= DescentTime || Input.GetKeyDown(KeyCode.Space)) Arrive();
                    break;
                case View.Field:
                    if (running)
                    {
                        if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
                        if (!paused)
                        {
                            runClock += dt;
                            float time = Mathf.Clamp01(runClock / RunTime) * duration;
                            int month = Mathf.Min(duration - 1, Mathf.FloorToInt(time));
                            shownMonth = month;
                            var frames = comparing && referenceTrial != null ? referenceTrial.months : trial.months;
                            displayed = Interpolate(month == 0 ? before : frames[month - 1], frames[month], Mathf.Clamp01(time - month));
                            if (runClock >= RunTime + ObservationHold) Finish();
                        }
                    }
                    if (Input.GetKeyDown(KeyCode.Tab)) showTelemetry = !showTelemetry;
                    if (Input.GetMouseButtonDown(1) && Application.isFocused)
                    {
                        lookHeld = true;
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    if (lookHeld && (!Input.GetMouseButton(1) || !Application.isFocused)) ReleaseLook();
                    Vector2 movement = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                    Vector2 look = lookHeld ? new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) : Vector2.zero;
                    if (!paused) world.Explore(dt, movement, look, reduced);
                    break;
            }
            world.Apply(displayed, dt, false, muted, view == View.Field || (view == View.Descent && descentClock >= 1.4f), reduced);
        }

        static FieldMonth Interpolate(FieldMonth a, FieldMonth b, float t)
        {
            t = Mathf.SmoothStep(0, 1, t);
            var value = b;
            value.land.soil = Mathf.Lerp(a.land.soil, b.land.soil, t);
            value.land.vegetation = Mathf.Lerp(a.land.vegetation, b.land.vegetation, t);
            value.land.drought = Mathf.Lerp(a.land.drought, b.land.drought, t);
            value.weather.rain = Mathf.Lerp(a.weather.rain, b.weather.rain, t);
            value.weather.temperature = Mathf.Lerp(a.weather.temperature, b.weather.temperature, t);
            value.weather.humidity = Mathf.Lerp(a.weather.humidity, b.weather.humidity, t);
            value.weather.wind = Mathf.Lerp(a.weather.wind, b.weather.wind, t);
            return value;
        }

        void ToggleMute()
        {
            muted = !muted;
            PlayerPrefs.SetInt("homecoming.unity.mute", muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        void ToggleMotion()
        {
            reduced = !reduced;
            PlayerPrefs.SetInt("homecoming.unity.reduced", reduced ? 1 : 0);
            PlayerPrefs.Save();
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            hotDial = -1;
            if (lookHeld) ReleaseLook();
        }
        void OnDisable() { if (lookHeld) ReleaseLook(); }
        void OnDestroy()
        {
            if (fade != null) Destroy(fade);
            if (sideFade != null) Destroy(sideFade);
            if (dialFace != null) Destroy(dialFace);
        }
    }
}
