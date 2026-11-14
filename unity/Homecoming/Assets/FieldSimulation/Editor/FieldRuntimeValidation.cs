using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Field
{
    public static class FieldRuntimeValidation
    {
        [MenuItem("Homecoming/Field simulation/Validate model and campaign")]
        public static void Run()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/FieldSimulation/Data/Climate.json");
            var climate = JsonUtility.FromJson<ClimateData>(source.text);
            ValidateLocalModel(climate);
            ReportHindcast(climate, source.text);

            string directory = Path.Combine(Path.GetTempPath(), "homecoming-field-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                ValidateCampaign(climate, Path.Combine(directory, "field.json"));
                ValidateMigration(climate, Path.Combine(directory, "legacy.json"));
            }
            finally { Directory.Delete(directory, true); }
            Debug.Log("FIELD_RUNTIME_OK: physical ledger, deterministic previews, revision and forcing replay, branches, atomic saves and v1 migration.");
        }

        [Serializable] sealed class ArchivedForcings { public ArchivedSeries[] evidence; }
        [Serializable] sealed class ArchivedSeries
        {
            public string id = null, region = null, key = null, provider = null;
            public ArchivedMonth[] observations = null;
        }
        [Serializable] struct ArchivedMonth { public string date; public float value; public bool hasValue; }

        // Compare standardized anomalies. A MERRA-2 wetness fraction has no supported
        // conversion to millimeters in this teaching bucket. The comparison is a diagnostic
        // with known atmospheric forcings, including shared reanalysis sources.
        static void ReportHindcast(ClimateData climate, string exportedJson)
        {
            var archived = JsonUtility.FromJson<ArchivedForcings>(exportedJson);
            var series = archived.evidence.Where(e => e.region == "phoenix" && e.provider == "reanalysis").ToArray();
            if (series.Length == 0 || series.Any(e => e.observations == null || e.observations.Length == 0))
            {
                Debug.Log("FIELD_HINDCAST: Monthly forcing archive is unavailable.");
                return;
            }

            var columns = series.ToDictionary(e => e.key, e => e.observations.Where(m => m.hasValue)
                .ToDictionary(m => m.date, m => m.value));
            var phoenix = climate.regions.First(r => r.id == "phoenix");
            var inputs = LocalWaterBalance.Inputs(phoenix);
            var first = new DateTime(1991, 1, 1);
            var heldOut = new DateTime(2021, 1, 1);
            var initial = ClimateModel.Initialize(phoenix, FieldCalendar.Index(first));
            var model = new List<double>();
            var proxy = new List<double>();
            double storage = initial.storageMm;
            float previousTemperature = initial.previousTemperatureC;
            for (var day = first; day < new DateTime(2025, 1, 1); day = day.AddMonths(1))
            {
                string date = day.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var weather = new Weather
                {
                    rain = columns["rain"][date], temperature = columns["temperature"][date],
                    humidity = columns["humidity"][date], wind = columns["wind"][date]
                };
                storage = LocalWaterBalance.Step(inputs, phoenix.latitude, day, weather, previousTemperature, storage).storageMm;
                previousTemperature = weather.temperature;
                model.Add(storage);
                proxy.Add(columns["soil"][date]);
            }

            int training = FieldCalendar.Index(heldOut) - FieldCalendar.Index(first);
            double[] StandardizedAnomalies(IReadOnlyList<double> xs)
            {
                var seasonalMeans = Enumerable.Range(0, 12).Select(month =>
                    Enumerable.Range(0, training / 12).Average(year => xs[year * 12 + month])).ToArray();
                var anomalies = xs.Select((value, index) => value - seasonalMeans[index % 12]).ToArray();
                // Pool the training scale across months because the bucket can be empty
                // for every training year in an individual dry-season month.
                double scale = Math.Sqrt(anomalies.Take(training).Average(value => value * value));
                Check(scale > 0, "Nonzero training anomaly scale");
                return anomalies.Select(value => value / scale).ToArray();
            }
            var modeledAnomalies = StandardizedAnomalies(model);
            var proxyAnomalies = StandardizedAnomalies(proxy);
            var verification = Enumerable.Range(training, model.Count - training).Select(i =>
            {
                double observed = proxyAnomalies[i];
                return (model: Math.Abs(modeledAnomalies[i] - observed), climatology: Math.Abs(observed),
                    persistence: Math.Abs(proxyAnomalies[i - 1] - observed));
            }).ToArray();
            double modelMae = verification.Average(r => r.model);
            Check(!double.IsInfinity(modelMae) && !double.IsNaN(modelMae), "Finite hindcast diagnostic");
            Debug.Log(FormattableString.Invariant($"FIELD_HINDCAST: Phoenix 2021–2024, {verification.Length} months. Standardized wetness anomaly MAE: bucket {modelMae:F3}, seasonal climatology {verification.Average(r => r.climatology):F3}, one-month anomaly persistence {verification.Average(r => r.persistence):F3}. MERRA-2 reference {series.First(e => e.key == "soil").id}; shared reanalysis forcings. No conversion to observed storage or estimate of forecast skill."));
        }

        static void ValidateLocalModel(ClimateData climate)
        {
            var phoenix = Array.Find(climate.regions, region => region.id == "phoenix");
            var inputs = LocalWaterBalance.Inputs(phoenix);
            var fixture = new DateTime(2028, 2, 1);
            var weather = new Weather { temperature = 14, humidity = 48, wind = 3, rain = 300 };
            var ledger = LocalWaterBalance.Step(inputs, 33.5, fixture, weather, 12, 100);
            // Independent reference calculation from FAO-56 formulas and the stated bucket process.
            Equal(ledger.et0Mm, 84.59378256618281, "February ET0 (mm)");
            Equal(ledger.vpdKpa, .8899914697335323, "February VPD (kPa)");
            Equal(ledger.drainageMm, 13.125, "Excess storage drainage (mm)");
            Equal(ledger.runoffMm, 165.40621743381718, "Local overflow (mm)");
            Equal(ledger.storageMm, 136.875, "Ending storage (mm)");
            Equal(ledger.BalanceErrorMm, 0, "Water balance closure");

            // src/lib/whatif/model.ts referenceEt with January 16 (same mid-month day).
            var sharedFixture = LocalWaterBalance.ReferenceEt(inputs, 33.5, new DateTime(2026, 1, 1),
                new Weather { temperature = 10, humidity = 45, wind = 3 }, 8);
            Equal(sharedFixture.et0Mm, 69.67008407178646, "Web and Unity ET0 agree for the same inputs");

            weather.rain = 0;
            var dry = LocalWaterBalance.Step(inputs, 33.5, fixture, weather, 12, 0);
            Equal(dry.actualEtMm, 0, "ET with empty bucket and no precipitation");
            Equal(dry.BalanceErrorMm, 0, "Zero rain closure");
            weather.humidity = 100;
            Equal(LocalWaterBalance.ReferenceEt(inputs, 33.5, fixture, weather, 12).vpdKpa, 0, "Saturated VPD");
            Check(!ClimateModel.Initialize(climate.regions.First(region => region.id != "phoenix"), FieldCalendar.StartMonth).hasStorage,
                "Phoenix calibration is never applied to other sites");
            var start = ClimateModel.Initialize(phoenix, FieldCalendar.StartMonth);
            Check(start.hasStorage && start.storageMm >= 0 && start.storageMm <= inputs.soilCapacityMm, "Bounded normal spin-up");
        }

        static void ValidateCampaign(ClimateData climate, string path)
        {
            const string siteId = "phoenix";
            string otherId = climate.regions.First(region => region.id != siteId).id;
            var campaign = FieldCampaign.Open(climate, path, selectedSiteId: siteId);
            string main = campaign.ActiveBranchId;
            var root = campaign.Current;
            var openedBeforeAnyRun = FieldCampaign.Open(climate, path);
            Check(openedBeforeAnyRun.Id == campaign.Id && openedBeforeAnyRun.Current.id == root.id,
                "A new campaign keeps its identity before any advance");
            var first = root.Site(siteId);
            first.current.storageMm = -100;
            Check(campaign.Current.Site(siteId).current.storageMm >= 0, "Revision snapshots are detached");

            var noChange = campaign.Preview(siteId, null);
            var neutral = noChange.Site(siteId);
            Check(Equivalent(neutral.scenario, neutral.reference), "Identical forcing produces zero intervention effect");
            var wetEdit = new ForcingEdit(ForcingControl.Rain, 20, root.nextMonth);
            var wet = campaign.Preview(siteId, new[] { wetEdit }, "wet");
            var wetProjection = wet.Site(siteId);
            var expected = wetProjection.scenario.months[11];
            Check(wetProjection.scenario.months.Length == 12 && expected.Date == new DateTime(2026, 12, 1), "Twelve dated months");
            Check(wetProjection.scenario.months[0].weather.rain > wetProjection.reference.months[0].weather.rain,
                "The patch changes the named site");
            Check(Equivalent(wet.Site(otherId).scenario, wet.Site(otherId).reference), "Other sites receive no forcing patch");
            Check(Equivalent(wetProjection.scenario, campaign.Preview(siteId, new[] { wetEdit }).Site(siteId).scenario),
                "Deterministic 12-month calculation");
            Check(campaign.Current.id == root.id && campaign.Journal.runs == 0, "Discarding a preview does not commit");

            wetProjection.scenario.months[11] = default;
            var end = campaign.Advance(wet);
            Equal(end.Site(siteId).current.storageMm, expected.land.storageMm, "Commit uses original preview bytes");
            Check(end.nextMonth == root.nextMonth + 12 && end.NextDate == new DateTime(2027, 1, 1), "Shared calendar rollover");
            Check(end.Site(otherId).current.elapsed == 12 && end.Site(otherId).ActiveAt(end.nextMonth).rain == 0,
                "Other sites evolve on their own inherited forcing");
            Check(campaign.Advance(wet).id == end.id && campaign.Journal.runs == 1, "Idempotent advance");
            Throws<InvalidOperationException>(() => campaign.Advance(noChange), "A stale preview cannot advance");
            Throws<InvalidOperationException>(() => campaign.Advance(wet, 3), "A duplicate ID cannot change the committed prefix");
            Check(Equivalent(campaign.RunForRevision(end.id).Site(siteId).scenario, wet.Site(siteId).scenario),
                "Committed trajectory is replayable");

            campaign.SelectSite(otherId);
            campaign.SaveEvidence(new[] { "test-finding" });
            campaign.SaveCase(new FieldCaseState
            {
                id = "test-case", branchId = main, sourceRevisionId = root.id, prediction = "Higher storage",
                predictionResultRevisionId = end.id, report = "Filed"
            });
            var resumed = FieldCampaign.Open(climate, path, selectedSiteId: siteId);
            Check(resumed.ActiveBranchId == main && resumed.SelectedSiteId == otherId &&
                resumed.Case.report == "Filed" && resumed.Journal.evidence.Contains("test-finding"), "Restore branch, site, evidence and case");
            Check(resumed.Advance(wet).id == end.id, "Command deduplication survives reload");
            resumed.SelectSite(siteId);
            Throws<IOException>(() => campaign.SelectSite(siteId), "A concurrent stale writer cannot overwrite a newer save");
            var current = resumed.Current;
            var heat = new ForcingEdit(ForcingControl.Heat, 1, current.nextMonth);
            var followUp = resumed.Preview(siteId, new[] { heat });
            var revised = followUp.Site(siteId);
            Check(revised.scenario.months.All(month => month.forcing.rain == 20 && month.forcing.heat == 1),
                "Changing just heat preserves the site's rain setting");
            Equal(revised.scenario.months[0].water.startStorageMm, expected.land.storageMm, "Carry soil storage through reload and site switching");
            Check(revised.reference.months[0].forcing.rain == 20 && revised.reference.months[0].forcing.heat == 0,
                "Reference starts from the same land and uses inherited forcing");
            var summer = resumed.Advance(followUp, 3);
            Check(summer.nextMonth == current.nextMonth + 3 && resumed.RunForRevision(summer.id).committedMonths == 3,
                "Advance only the chosen prefix");
            Equal(summer.Site(siteId).current.storageMm, revised.scenario.months[2].land.storageMm, "Prefix terminal storage");

            var reset = new ForcingEdit(ForcingControl.Rain, 0, summer.nextMonth + 1, summer.nextMonth + 2);
            var timed = resumed.Preview(siteId, new[] { reset }).Site(siteId).scenario;
            Check(timed.months.Take(3).Select(month => month.forcing.rain).SequenceEqual(new[] { 20, 0, 20 }),
                "Monthly override honors its dates and restores inherited forcing when it ends");
            string fork = resumed.Fork(root.id, "Dry branch");
            var dry = resumed.Preview(siteId, new[] { new ForcingEdit(ForcingControl.Rain, -70, root.nextMonth) });
            var dryEnd = resumed.Advance(dry);
            Check(dryEnd.branchId == fork && dryEnd.parentId == root.id, "Fork from selected past revision");
            var dryAgain = resumed.Preview(siteId, new[] { new ForcingEdit(ForcingControl.Rain, 0, dryEnd.nextMonth) });
            resumed.SwitchBranch(main);
            Check(resumed.Current.id == summer.id, "Branch heads are isolated");
            var wetFork = resumed.Fork(end.id, "Wet branch");
            var wetAgain = resumed.Preview(siteId, new[] { new ForcingEdit(ForcingControl.Rain, 0, end.nextMonth) });
            var wetMonth = wetAgain.Site(siteId).scenario.months[0];
            var dryMonth = dryAgain.Site(siteId).scenario.months[0];
            Check(wetMonth.monthIndex == dryMonth.monthIndex && wetMonth.forcing.rain == dryMonth.forcing.rain &&
                wetMonth.water.storageMm > dryMonth.water.storageMm, "Different inherited land under identical new forcing");
            var reopened = FieldCampaign.Open(climate, path);
            Check(reopened.ActiveBranchId == wetFork && reopened.Current.id == end.id &&
                reopened.Branches.Length == 3, "Selected fork restored");
            Throws<InvalidOperationException>(() => reopened.Advance(dryAgain), "Preview from a different branch cannot advance");

            var altered = JsonUtility.FromJson<ClimateData>(JsonUtility.ToJson(climate));
            altered.regions[0].normals[0].rain += 1;
            var incompatible = FieldCampaign.Open(altered, path);
            Check(!incompatible.CanProject && incompatible.RunForRevision(end.id) != null, "Changed input data preserves playback");
            Throws<InvalidOperationException>(() => incompatible.Preview(siteId, null), "Changed data cannot silently continue a revision");
            incompatible.StartNew();
            Check(incompatible.CanProject && incompatible.Id != campaign.Id && incompatible.RunIds.Length == 0, "Explicit new world resets state");
        }

        static void ValidateMigration(ClimateData climate, string path)
        {
            var legacy = new FieldSave { runs = 4 };
            legacy.sites.Add(new SavedLand { id = "phoenix", land = new LandState { soil = .42f, vegetation = .63f, elapsed = 15 } });
            legacy.notes.Add(new FieldNote { id = "drought", region = "phoenix", run = 2 });
            legacy.evidence.Add("legacy-finding");
            var campaign = FieldCampaign.Open(climate, path, legacy, "phoenix");
            var revision = campaign.Current;
            var site = revision.Site("phoenix");
            Check(campaign.MigratedFromV1 && site.migrated && site.legacyElapsed == 15 && site.current.elapsed == 0 &&
                revision.nextMonth == FieldCalendar.StartMonth && !site.current.hasStorage && site.current.soil == .42f &&
                site.ActiveAt(revision.nextMonth).rain == 0, "V1 land preserved without inventing past dates, forcing or storage");
            Check(FieldCampaign.Open(climate, path).Journal.notes.Single().run == 2 &&
                campaign.Journal.evidence.Single() == "legacy-finding" && campaign.Journal.runs == 4,
                "V1 notes and evidence retained");
        }

        static bool Equivalent(FieldTrial first, FieldTrial second) => JsonUtility.ToJson(first) == JsonUtility.ToJson(second);
        static void Equal(double actual, double expected, string task) =>
            Check(Math.Abs(actual - expected) <= 1e-7, $"{task}: expected {expected:R}, got {actual:R}");
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
