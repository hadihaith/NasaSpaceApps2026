using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Field
{
    public static class FieldResetValidation
    {
        [MenuItem("Homecoming/Field simulation/Validate reset behavior")]
        public static void Run()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/FieldSimulation/Data/Climate.json");
            var climate = JsonUtility.FromJson<ClimateData>(source.text);
            string directory = Path.Combine(Path.GetTempPath(), "homecoming-reset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try { ValidateReset(climate, Path.Combine(directory, "field.json")); }
            finally { Directory.Delete(directory, true); }
            Debug.Log("FIELD_RESET_OK: baseline reset, migrated journal and case preservation, old-run replay, stale writers and full campaign reset.");
        }

        static void ValidateReset(ClimateData climate, string path)
        {
            const string siteId = "phoenix";
            var legacy = new FieldSave { runs = 4 };
            legacy.sites.Add(new SavedLand { id = siteId, land = new LandState { soil = .42f, vegetation = .63f, elapsed = 15 } });
            legacy.notes.Add(new FieldNote { id = "drought", region = siteId, run = 2 });
            legacy.evidence.Add("legacy-finding");
            var campaign = FieldCampaign.Open(climate, path, legacy, siteId);
            string main = campaign.ActiveBranchId;
            var original = campaign.Current;
            var edit = new ForcingEdit(ForcingControl.Rain, 20, original.nextMonth);
            var preview = campaign.Preview(siteId, new[] { edit });
            var committed = campaign.Advance(preview, 3);
            var run = campaign.GetRun(preview.Id);
            var caseState = new FieldCaseState
            {
                id = "completed-case", branchId = main, sourceRevisionId = original.id,
                predictionResultRevisionId = committed.id, prediction = "More water", report = "Filed"
            };
            campaign.SaveCase(caseState);
            string fork = campaign.Fork(committed.id, "Soil follow-up");
            var stalePreview = campaign.Preview(siteId, null);
            var staleWriter = FieldCampaign.Open(climate, path);
            int runCount = campaign.Journal.runs;
            var changedClimate = JsonUtility.FromJson<ClimateData>(JsonUtility.ToJson(climate));
            changedClimate.regions[0].normals[0].rain += 1;
            var baseline = FieldCampaign.Open(changedClimate).Current;
            campaign = FieldCampaign.Open(changedClimate, path);
            Check(!campaign.CanProject, "The old experiment keeps its original simulation inputs");

            campaign.ResetEarth();
            var restored = FieldCampaign.Open(changedClimate, path);
            var fresh = restored.Current;
            Check(restored.CanProject && restored.Id == campaign.Id && restored.SelectedSiteId == siteId && restored.MigratedFromV1 &&
                restored.ActiveBranchId != fork && restored.ActiveBranchId != main && restored.ActiveBranchId == fresh.branchId &&
                string.IsNullOrEmpty(fresh.parentId) && fresh.nextMonth == FieldCalendar.StartMonth,
                "Reset Earth adds a fresh January baseline in the same campaign");
            Check(fresh.sites.Select(site => JsonUtility.ToJson(site)).SequenceEqual(baseline.sites.Select(site => JsonUtility.ToJson(site))),
                "All sites reset land, elapsed time, migration state and weather schedules to the current baseline");
            Check(restored.Journal.runs == runCount && restored.Journal.sites.Count == 0 &&
                restored.Journal.evidence.Contains("legacy-finding") && restored.Journal.notes.Any(note => note.id == "drought" && note.run == 2) &&
                JsonUtility.ToJson(restored.Case) == JsonUtility.ToJson(caseState), "Reset Earth preserves saved achievements and case references");
            Check(restored.RunIds.SequenceEqual(new[] { run.id }) &&
                JsonUtility.ToJson(restored.GetRun(run.id)) == JsonUtility.ToJson(run) &&
                restored.GetRevision(restored.Case.sourceRevisionId).Site(siteId).migrated &&
                restored.RunForRevision(restored.Case.predictionResultRevisionId).branchId == restored.Case.branchId,
                "Earlier projections, branches and all referenced revisions remain available after reload");
            Throws<InvalidOperationException>(() => restored.Advance(stalePreview), "A pending preview cannot advance the reset Earth");
            Throws<IOException>(() => staleWriter.ResetEarth(), "A stale writer cannot overwrite an Earth reset");
            Check(staleWriter.ActiveBranchId == fork, "A failed Earth reset keeps the in-memory branch");
            restored.SwitchBranch(fork);
            Check(!restored.CanProject && restored.Current.id == committed.id && restored.Current.Site(siteId).ActiveAt(committed.nextMonth).rain == edit.value,
                "The prior experiment remains available with its saved terrain and weather");
            restored.SwitchBranch(fresh.branchId);
            var next = restored.Advance(restored.Preview(siteId, null), 1);
            Check(next.parentId == fresh.id && next.nextMonth == FieldCalendar.StartMonth + 1 && restored.Journal.runs == runCount + 1,
                "A fresh experiment advances independently and keeps cumulative run numbering");

            FieldCampaign.ClearSavedCampaign(path);
            FieldCampaign.ClearSavedCampaign(path);
            Check(!File.Exists(path), "An operation reset clears the campaign and can be retried");
            Throws<IOException>(() => restored.ResetEarth(), "An old campaign writer cannot recreate an erased operation");
            var newOperation = FieldCampaign.Open(changedClimate, path);
            Check(newOperation.Id != campaign.Id && newOperation.Branches.Length == 1 && newOperation.RunIds.Length == 0 &&
                newOperation.Case == null && newOperation.Journal.runs == 0 && newOperation.Journal.notes.Count == 0 &&
                newOperation.Journal.evidence.Count == 0, "A new operation starts with a clean campaign and journal");
        }

        static void Check(bool passed, string task)
        {
            if (!passed) throw new InvalidOperationException(task);
        }

        static void Throws<T>(Action action, string task) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(task);
        }
    }
}
