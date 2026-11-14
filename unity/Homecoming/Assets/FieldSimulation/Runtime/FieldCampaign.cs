using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Homecoming.Field
{
    [Serializable] public sealed class FieldSave
    {
        public int runs;
        public List<SavedLand> sites = new();
        public List<FieldNote> notes = new();
        public List<string> evidence = new();
    }

    [Serializable] public sealed class SavedLand { public string id; public LandState land; }
    [Serializable] public sealed class FieldNote { public string id, region; public int run; }

    public enum ForcingControl { Heat, Rain, Humidity, Wind }

    [Serializable] public struct ForcingEdit
    {
        public ForcingControl control;
        public int value, startMonth, endMonthExclusive;

        public ForcingEdit(ForcingControl control, int value, int startMonth, int endMonthExclusive = 0)
        {
            this.control = control;
            this.value = value;
            this.startMonth = startMonth;
            this.endMonthExclusive = endMonthExclusive;
        }

        public string Unit => control switch
        {
            ForcingControl.Heat => "°C",
            ForcingControl.Rain => "%",
            ForcingControl.Humidity => "percentage points",
            _ => "m/s"
        };
    }

    [Serializable] public sealed class FieldSiteState
    {
        public string id, initialization;
        public LandState initial, current;
        public ForcingEdit[] schedule;
        public bool migrated;
        public int legacyElapsed;

        public Forcing ActiveAt(int month)
        {
            Forcing result = default;
            foreach (var edit in schedule)
                if (edit.startMonth <= month && (edit.endMonthExclusive == 0 || month < edit.endMonthExclusive))
                    result[(int)edit.control] = edit.value;
            return result;
        }
    }

    [Serializable] public sealed class WorldRevision
    {
        public string id, parentId, branchId, modelVersion, dataFingerprint;
        public int nextMonth;
        public FieldSiteState[] sites;
        public DateTime NextDate => FieldCalendar.Date(nextMonth);
        public FieldSiteState Site(string id) => Array.Find(sites, site => site.id == id);
    }

    [Serializable] public sealed class FieldBranch
    {
        public string id, name, headRevisionId;
    }

    [Serializable] public sealed class SiteProjection
    {
        public string siteId;
        public FieldTrial reference, scenario;
    }

    [Serializable] public sealed class ScenarioRun
    {
        public string id, branchId, sourceRevisionId, committedRevisionId, editedSiteId, modelVersion, dataFingerprint;
        public int startMonth, committedMonths;
        public ForcingEdit[] edits;
        public SiteProjection[] sites;
        public SiteProjection Site(string id) => Array.Find(sites, site => site.siteId == id);
    }

    // Expose copies for charting and playback. Advancing consumes the original calculation.
    public sealed class ScenarioPreview
    {
        internal readonly ScenarioRun Run;
        internal ScenarioPreview(ScenarioRun run) => Run = run;
        public string Id => Run.id;
        public string BranchId => Run.branchId;
        public string SourceRevisionId => Run.sourceRevisionId;
        public int StartMonth => Run.startMonth;
        public SiteProjection Site(string siteId) => FieldCampaign.Copy(Run.Site(siteId));
        public ScenarioRun Snapshot() => FieldCampaign.Copy(Run);
    }

    [Serializable] public sealed class FieldCaseState
    {
        public string id, branchId, sourceRevisionId, prediction, predictionResultRevisionId, report;
    }

    [Serializable] sealed class FieldCampaignFile
    {
        public int version;
        public string id, activeBranchId, selectedSiteId;
        public bool migratedFromV1;
        public FieldSave journal;
        public List<WorldRevision> revisions;
        public List<FieldBranch> branches;
        public List<ScenarioRun> runs;
        public FieldCaseState caseState;
    }

    public sealed class FieldCampaign
    {
        const int SchemaVersion = 2;
        readonly ClimateData climate;
        readonly string path, fingerprint;
        string savedText;
        FieldCampaignFile campaign;

        FieldCampaign(ClimateData climate, string path)
        {
            // Pin the numeric input snapshot for this process.
            this.climate = Copy(climate);
            this.path = path;
            fingerprint = Fingerprint(this.climate);
        }

        public string Id => campaign.id;
        public string ActiveBranchId => campaign.activeBranchId;
        public string SelectedSiteId => campaign.selectedSiteId;
        public bool MigratedFromV1 => campaign.migratedFromV1;
        public FieldSave Journal => Copy(campaign.journal);
        public FieldCaseState Case => string.IsNullOrEmpty(campaign.caseState?.id) ? null : Copy(campaign.caseState);
        public WorldRevision Current => Copy(Head);
        public FieldBranch[] Branches => campaign.branches.Select(Copy).ToArray();
        public bool CanProject => Head.modelVersion == ClimateModel.Version && Head.dataFingerprint == fingerprint;
        WorldRevision Head => Revision(Branch(campaign.activeBranchId).headRevisionId);

        public WorldRevision GetRevision(string id) => Copy(Revision(id));
        public ScenarioRun GetRun(string id) => Copy(campaign.runs.Find(run => run.id == id));
        public ScenarioRun RunForRevision(string revisionId) => Copy(campaign.runs.Find(run => run.committedRevisionId == revisionId));
        public string[] RunIds => campaign.runs.Select(run => run.id).ToArray();

        public static FieldCampaign Open(ClimateData climate, string path = null, FieldSave legacy = null,
            string selectedSiteId = null)
        {
            var store = new FieldCampaign(climate, path);
            store.savedText = path != null && File.Exists(path) ? File.ReadAllText(path) : null;
            if (store.savedText != null)
            {
                store.campaign = JsonUtility.FromJson<FieldCampaignFile>(store.savedText);
                store.Validate();
            }
            else
            {
                store.campaign = store.Create(legacy, selectedSiteId);
                if (path != null) store.Write(store.campaign);
            }
            return store;
        }

        public static ForcingEdit[] Patch(Forcing inherited, Forcing draft, int fromMonth)
        {
            var edits = new List<ForcingEdit>();
            for (int i = 0; i < 4; i++)
                if (draft[i] != inherited[i])
                    edits.Add(new ForcingEdit((ForcingControl)i, draft[i], fromMonth));
            return edits.ToArray();
        }

        public ScenarioPreview Preview(string siteId, IReadOnlyList<ForcingEdit> edits, string commandId = null)
        {
            var source = Head;
            if (!CanProject)
                throw new InvalidOperationException("The saved world uses different simulation inputs. Its recorded runs can still be replayed.");
            if (source.Site(siteId) == null) throw new ArgumentException("Unknown simulation site.", nameof(siteId));
            var patch = edits?.ToArray() ?? Array.Empty<ForcingEdit>();
            foreach (var edit in patch) ValidateEdit(edit, source.nextMonth);

            var run = new ScenarioRun
            {
                id = string.IsNullOrWhiteSpace(commandId) ? NewId() : commandId,
                branchId = ActiveBranchId, sourceRevisionId = source.id, startMonth = source.nextMonth,
                editedSiteId = siteId, edits = patch, modelVersion = source.modelVersion, dataFingerprint = source.dataFingerprint,
                sites = new SiteProjection[source.sites.Length]
            };
            for (int i = 0; i < source.sites.Length; i++)
            {
                var site = source.sites[i];
                var region = Array.Find(climate.regions, r => r.id == site.id);
                var reference = ClimateModel.Project(region, site.current, source.nextMonth, site.ActiveAt);
                var scenario = reference;
                if (site.id == siteId && patch.Length > 0)
                {
                    var changed = Copy(site);
                    changed.schedule = site.schedule.Concat(patch).ToArray();
                    scenario = ClimateModel.Project(region, site.current, source.nextMonth, changed.ActiveAt);
                }
                run.sites[i] = new SiteProjection { siteId = site.id, reference = reference, scenario = scenario };
            }
            return new ScenarioPreview(run);
        }

        public WorldRevision Advance(ScenarioPreview preview, int months = FieldCalendar.MonthsInPreview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            if (months < 1 || months > FieldCalendar.MonthsInPreview) throw new ArgumentOutOfRangeException(nameof(months));
            var launch = preview.Run;
            var duplicate = campaign.runs.Find(run => run.id == launch.id);
            if (duplicate != null)
            {
                if (duplicate.sourceRevisionId != launch.sourceRevisionId || duplicate.branchId != launch.branchId ||
                    duplicate.editedSiteId != launch.editedSiteId || duplicate.committedMonths != months ||
                    !duplicate.edits.SequenceEqual(launch.edits))
                    throw new InvalidOperationException("This advance command ID is already in use.");
                return GetRevision(duplicate.committedRevisionId);
            }
            var source = Head;
            if (launch.branchId != ActiveBranchId || launch.sourceRevisionId != source.id || !CanProject ||
                launch.modelVersion != source.modelVersion || launch.dataFingerprint != source.dataFingerprint)
                throw new InvalidOperationException("The preview is stale. Calculate again from the current revision or fork from the earlier one.");

            var next = Copy(campaign);
            var committed = Copy(source);
            committed.id = NewId();
            committed.branchId = launch.branchId;
            committed.parentId = source.id;
            committed.nextMonth = source.nextMonth + months;
            foreach (var site in committed.sites)
            {
                site.current = launch.Site(site.id).scenario.months[months - 1].land;
                if (site.id == launch.editedSiteId)
                    site.schedule = site.schedule.Concat(launch.edits).ToArray();
            }

            var record = Copy(launch);
            record.committedMonths = months;
            record.committedRevisionId = committed.id;

            next.journal.runs++;
            // Keep the v1 journal view working. The revision and run own dated land state.
            var previous = next.journal.sites.Find(s => s.id == launch.editedSiteId);
            if (previous == null)
            {
                previous = new SavedLand { id = launch.editedSiteId };
                next.journal.sites.Add(previous);
            }
            previous.land = committed.Site(launch.editedSiteId).current;
            var trial = record.Site(launch.editedSiteId).scenario;
            foreach (var finding in ClimateModel.Findings(trial.before, trial.months, months))
                if (Array.Exists(climate.discoveries, item => item.id == finding) &&
                    !next.journal.notes.Exists(note => note.id == finding))
                    next.journal.notes.Add(new FieldNote { id = finding, region = launch.editedSiteId, run = next.journal.runs });

            next.revisions.Add(committed);
            next.branches.Find(b => b.id == launch.branchId).headRevisionId = committed.id;
            next.runs.Add(record);
            Write(next);
            return Copy(committed);
        }

        public string Fork(string sourceRevisionId, string name)
        {
            var revision = Revision(sourceRevisionId);
            if (revision == null) throw new ArgumentException("Unknown revision.", nameof(sourceRevisionId));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A branch name is required.", nameof(name));
            var next = Copy(campaign);
            string id = NewId();
            next.branches.Add(new FieldBranch { id = id, name = name.Trim(), headRevisionId = revision.id });
            next.activeBranchId = id;
            Write(next);
            return id;
        }

        public void SwitchBranch(string branchId)
        {
            if (Branch(branchId) == null) throw new ArgumentException("Unknown branch.", nameof(branchId));
            if (branchId == ActiveBranchId) return;
            var next = Copy(campaign);
            next.activeBranchId = branchId;
            Write(next);
        }

        public void SelectSite(string siteId)
        {
            if (siteId == SelectedSiteId) return;
            if (Head.Site(siteId) == null) throw new ArgumentException("Unknown simulation site.", nameof(siteId));
            var next = Copy(campaign);
            next.selectedSiteId = siteId;
            Write(next);
        }

        public void SaveEvidence(IEnumerable<string> evidence)
        {
            var next = Copy(campaign);
            foreach (string id in evidence)
                if (!next.journal.evidence.Contains(id)) next.journal.evidence.Add(id);
            if (next.journal.evidence.Count != campaign.journal.evidence.Count) Write(next);
        }

        public void SaveCase(FieldCaseState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.id) || Branch(state.branchId) == null ||
                Revision(state.sourceRevisionId) == null ||
                (!string.IsNullOrEmpty(state.predictionResultRevisionId) &&
                    !campaign.runs.Exists(run => run.branchId == state.branchId && run.sourceRevisionId == state.sourceRevisionId &&
                        run.committedRevisionId == state.predictionResultRevisionId)))
                throw new ArgumentException("The case prediction must reference its source revision and any matching committed result.", nameof(state));
            var next = Copy(campaign);
            next.caseState = Copy(state);
            Write(next);
        }

        public void StartNew(string selectedSiteId = null) => Write(Create(null, selectedSiteId ?? SelectedSiteId));

        FieldCampaignFile Create(FieldSave legacy, string selectedSiteId)
        {
            var journal = Copy(legacy) ?? new FieldSave();
            journal.sites ??= new List<SavedLand>();
            journal.notes ??= new List<FieldNote>();
            journal.evidence ??= new List<string>();
            journal.sites.RemoveAll(site => site == null || site.land.elapsed < 0 ||
                !Array.Exists(climate.regions, r => r.id == site.id));
            // The migrated snapshot is retained even when a current catalog lacks these entries.
            journal.notes.RemoveAll(note => note == null);
            var branch = new FieldBranch { id = NewId(), name = "Main", headRevisionId = NewId() };
            var root = new WorldRevision
            {
                id = branch.headRevisionId, branchId = branch.id, nextMonth = FieldCalendar.StartMonth,
                modelVersion = ClimateModel.Version, dataFingerprint = fingerprint,
                sites = climate.regions.Select(region => InitializeSite(region, journal)).ToArray()
            };
            return new FieldCampaignFile
            {
                version = SchemaVersion, id = NewId(), migratedFromV1 = legacy != null,
                activeBranchId = branch.id,
                selectedSiteId = root.Site(selectedSiteId)?.id ?? root.sites[0].id,
                journal = journal, runs = new List<ScenarioRun>(),
                revisions = new List<WorldRevision> { root }, branches = new List<FieldBranch> { branch }
            };
        }

        static FieldSiteState InitializeSite(RegionClimate region, FieldSave journal)
        {
            var legacy = journal.sites.Find(site => site.id == region.id);
            var state = legacy?.land ?? ClimateModel.Initialize(region, FieldCalendar.StartMonth);
            // V1 wetness was illustrative, with no supported conversion to a storage depth.
            // Retain the index trajectory for sites that already have one.
            string initialization = legacy != null ? "Migrated v1 illustrative land snapshot. January 2026 assigned; prior elapsed months retained as metadata." :
                state.hasStorage ? LocalWaterBalance.Initialization : "January root-zone soil-wetness normal and illustrative land indices.";
            state.elapsed = 0;
            return new FieldSiteState
            {
                id = region.id, initial = state, current = state, initialization = initialization,
                migrated = legacy != null, legacyElapsed = legacy?.land.elapsed ?? 0, schedule = Array.Empty<ForcingEdit>()
            };
        }

        void ValidateEdit(ForcingEdit edit, int nextMonth)
        {
            int index = (int)edit.control;
            if (index < 0 || index >= 4 || edit.startMonth < nextMonth ||
                (edit.endMonthExclusive != 0 && edit.endMonthExclusive <= edit.startMonth))
                throw new ArgumentException("A forcing edit must select a control and a valid future interval.", nameof(edit));
            var bounds = climate.controls[index];
            if (edit.value < bounds.min || edit.value > bounds.max)
                throw new ArgumentOutOfRangeException(nameof(edit), "A forcing edit is outside the control limits.");
        }

        void Validate()
        {
            if (campaign?.version != SchemaVersion || campaign.journal?.sites == null || campaign.journal.notes == null ||
                campaign.journal.evidence == null || campaign.revisions == null || campaign.branches == null ||
                campaign.runs == null || campaign.revisions.Count == 0 || campaign.branches.Count == 0 ||
                Branch(campaign.activeBranchId) == null || Head?.sites == null || Head.sites.Length == 0)
                throw new InvalidDataException("The Homecoming campaign file is invalid or has an unsupported version.");
        }

        FieldBranch Branch(string id) => campaign.branches.Find(branch => branch.id == id);
        WorldRevision Revision(string id) => campaign.revisions.Find(revision => revision.id == id);
        static string NewId() => Guid.NewGuid().ToString("N");

        static string Fingerprint(ClimateData climate)
        {
            var input = new ClimateData
            {
                controls = climate.controls,
                regions = climate.regions.Select(region => new RegionClimate
                {
                    id = region.id, latitude = region.latitude, longitude = region.longitude,
                    normals = region.normals, waterBalance = LocalWaterBalance.Inputs(region)
                }).ToArray()
            };
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(input)))).Replace("-", "").ToLowerInvariant();
        }

        internal static T Copy<T>(T value) where T : class =>
            value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        void Write(FieldCampaignFile next)
        {
            if (path != null)
            {
                string serialized = JsonUtility.ToJson(next);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                using var writeLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                string current = File.Exists(path) ? File.ReadAllText(path) : null;
                if (savedText != current) throw new IOException("The campaign was updated by another session. Reopen it before saving.");
                string temp = path + "." + NewId() + ".tmp";
                try
                {
                    using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(serialized);
                        file.Write(bytes, 0, bytes.Length);
                        file.Flush(true);
                    }
                    if (current == null) File.Move(temp, path);
                    else File.Replace(temp, path, null);
                    savedText = serialized;
                }
                finally
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
            }
            campaign = next;
        }
    }
}
