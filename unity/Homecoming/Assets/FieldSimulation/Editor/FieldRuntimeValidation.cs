using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Direction = Homecoming.Field.CascadeDirection;
using Metric = Homecoming.Field.CascadeMetric;
using Transition = Homecoming.Field.CascadeTransition;

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
                ValidateCascade(climate, Path.Combine(directory, "cascade.json"));
                ValidateCascadeLimits(climate);
            }
            finally { Directory.Delete(directory, true); }
            Debug.Log("FIELD_RUNTIME_OK: physical ledger, deterministic previews, revision and forcing replay, branches, atomic saves and v1 migration.");
        }

        static void ValidateCascade(ClimateData climate, string path)
        {
            const string site = "phoenix";
            const Transition onset = Transition.Onset, quiet = Transition.ReturnToQuiet,
                reverse = Transition.Reversal, resume = Transition.Resumed;
            const Direction up = Direction.Increase, down = Direction.Decrease, flat = Direction.LittleChange;
            var campaign = FieldCampaign.Open(climate, path, selectedSiteId: site);
            int start = campaign.Current.nextMonth;
            var rainEdit = new ForcingEdit(ForcingControl.Rain, 100, start);
            var rainPreview = campaign.Preview(site, new[] { rainEdit }, "cascade-rain");
            var rain = FieldCascade.Analyze(rainPreview, site);
            Check(start == 24300 && rain.status == CascadeStatus.Response && rain.hasPhysicalLedger &&
                rain.rulesVersion == FieldCascade.RulesVersion && rain.runId == rainPreview.Id &&
                rain.sourceRevisionId == rainPreview.SourceRevisionId && rain.branchId == rainPreview.BranchId &&
                rain.startMonth == start && rain.editedSiteId == site && rain.siteId == site &&
                rain.provenance == CascadeProvenance.Preview && string.IsNullOrEmpty(rain.committedRevisionId) &&
                rain.committedMonths == 0 && rain.edits.SequenceEqual(new[] { rainEdit }),
                "Cascade preview retains matched run identity, date, and ordered edits");
            Check(!rain.Series(Metric.RootZoneWetnessIndex).available &&
                !string.IsNullOrEmpty(rain.Series(Metric.RootZoneWetnessIndex).unavailableReason),
                "Physical storage does not supply the illustrative wetness series");
            Check(rain.Series(Metric.Rain).valueUnit == "mm/month" &&
                rain.Series(Metric.Humidity).deltaUnit == "percentage points" &&
                rain.Series(Metric.RootZoneStorage).valueUnit == "mm" &&
                rain.Series(Metric.Vegetation).deltaUnit == "index points", "Cascade units match the paired readings");
            CheckTransitions(rain,
                (0, Metric.Rain, onset, up), (0, Metric.ActualEt, onset, up), (0, Metric.RootZoneStorage, onset, up),
                (0, Metric.DroughtStress, onset, down), (0, Metric.Vegetation, onset, up), (0, Metric.FuelDryness, onset, down),
                (3, Metric.RootZoneStorage, quiet, flat), (5, Metric.DroughtStress, quiet, flat),
                (6, Metric.DroughtStress, resume, down), (9, Metric.RootZoneStorage, resume, up));
            CheckPair(rain, Metric.Rain, 0, 38.062000, 76.124001);
            CheckPair(rain, Metric.ActualEt, 0, 27.024932, 43.750686);
            CheckPair(rain, Metric.RootZoneStorage, 0, 34.474418, 55.810665);
            CheckPair(rain, Metric.DroughtStress, 0, .590004, .336258);
            CheckPair(rain, Metric.RootZoneStorage, 11, 23.437350, 46.874701);
            Check(new[] { Metric.ReferenceEt, Metric.VaporPressureDeficit }.All(metric =>
                rain.Series(metric).available && rain.Series(metric).samples.All(sample => sample.delta == 0)),
                "Rain edits alone leave VPD and reference evapotranspiration unchanged");
            CheckInputs(EffectAt(rain, Metric.RootZoneStorage, 0), rain, (0, Metric.Rain), (0, Metric.ActualEt));
            CheckInputs(EffectAt(rain, Metric.DroughtStress, 0), rain, (0, Metric.ActualEt));
            CheckInputs(EffectAt(rain, Metric.FuelDryness, 0), rain, (0, Metric.DroughtStress));
            CheckLedgers(rain);

            string otherSite = climate.regions.First(region => region.id != site).id;
            var other = FieldCascade.Analyze(rainPreview, otherSite);
            Check(other.status == CascadeStatus.Quiet && other.effects.Length == 0 &&
                other.editedSiteId == site && other.siteId == otherSite, "Arizona edits have no effects at another site");
            var detached = FieldCascade.Analyze(rainPreview, site);
            detached.edits[0] = default;
            detached.frames[0] = default;
            detached.Series(Metric.Rain).samples[0] = default;
            EffectAt(detached, Metric.ActualEt, 0).inputs[0] = default;
            var fresh = FieldCascade.Analyze(rainPreview, site);
            Check(fresh.edits[0].Equals(rainEdit) && fresh.frames[0].monthIndex == start &&
                fresh.Series(Metric.Rain).samples[0].monthIndex == start &&
                EffectAt(fresh, Metric.ActualEt, 0).inputs[0].metric == Metric.Rain,
                "Cascade frames, edits, samples and input keys are detached");

            var afterRain = campaign.Advance(rainPreview);
            var heatEdit = new ForcingEdit(ForcingControl.Heat, 4, afterRain.nextMonth);
            var heatPreview = campaign.Preview(site, new[] { heatEdit }, "cascade-heat");
            var heat = FieldCascade.Analyze(heatPreview, site);
            Check(heat.status == CascadeStatus.Response && heat.hasPhysicalLedger && heat.startMonth == 24312 &&
                heat.sourceRevisionId == afterRain.id && heat.branchId == rain.branchId &&
                heat.edits.SequenceEqual(new[] { heatEdit }), "Heat comparison starts at the saved rain revision");
            CheckTransitions(heat,
                (0, Metric.Temperature, onset, up), (0, Metric.VaporPressureDeficit, onset, up),
                (0, Metric.ReferenceEt, onset, up), (0, Metric.ActualEt, onset, up), (0, Metric.RootZoneStorage, onset, down),
                (1, Metric.DroughtStress, onset, up), (2, Metric.FuelDryness, onset, up),
                (3, Metric.ActualEt, reverse, down), (3, Metric.RootZoneStorage, quiet, flat),
                (3, Metric.Vegetation, onset, down), (4, Metric.ActualEt, quiet, flat), (4, Metric.DroughtStress, quiet, flat),
                (6, Metric.DroughtStress, resume, up),
                (9, Metric.ActualEt, reverse, up), (9, Metric.RootZoneStorage, resume, down),
                (9, Metric.DroughtStress, quiet, flat), (9, Metric.FuelDryness, quiet, flat),
                (11, Metric.DroughtStress, resume, up), (11, Metric.Vegetation, quiet, flat),
                (11, Metric.FuelDryness, resume, up));
            CheckPair(heat, Metric.VaporPressureDeficit, 0, .601382, .781431);
            CheckPair(heat, Metric.ReferenceEt, 0, 65.915165, 73.217012);
            CheckPair(heat, Metric.ActualEt, 0, 54.049864, 60.037316);
            CheckPair(heat, Metric.RootZoneStorage, 0, 68.948837, 62.961386);
            var januaryHeat = heat.frames[0];
            var januaryStress = heat.Series(Metric.DroughtStress).samples[0];
            Check(januaryStress.delta == 0, "January warming leaves the fraction of unmet demand unchanged");
            Equal(januaryStress.inherited, 1 - januaryHeat.inherited.water.actualEtMm / januaryHeat.inherited.water.et0Mm,
                "January drought stress agrees with the recorded water ledger");
            CheckPair(heat, Metric.DroughtStress, 1, .033861, .073777);
            CheckPair(heat, Metric.FuelDryness, 2, .327332, .367225);
            CheckPair(heat, Metric.ActualEt, 3, 51.006592, 35.693945);
            CheckPair(heat, Metric.RootZoneStorage, 3, 0, 0);
            CheckPair(heat, Metric.Vegetation, 3, .544563, .517836);
            CheckPair(heat, Metric.ActualEt, 9, 36.911295, 39.094002);
            CheckPair(heat, Metric.RootZoneStorage, 11, 46.874701, 38.769874);
            CheckLedgers(heat);
            Equal(heat.frames[0].edited.water.startStorageMm, rain.frames[11].edited.water.storageMm,
                "Both warming tracks inherit the committed rain storage");
            Check(heat.frames.All(frame => frame.inherited.forcing.rain == 100 && frame.edited.forcing.rain == 100),
                "Both warming tracks use the saved rain setting");
            CheckInputs(EffectAt(heat, Metric.VaporPressureDeficit, 0), heat, (0, Metric.Temperature));
            CheckInputs(EffectAt(heat, Metric.ReferenceEt, 0), heat, (0, Metric.Temperature), (0, Metric.VaporPressureDeficit));
            CheckInputs(EffectAt(heat, Metric.DroughtStress, 1), heat, (1, Metric.ReferenceEt), (1, Metric.ActualEt));
            CheckInputs(EffectAt(heat, Metric.ActualEt, 3), heat, (2, Metric.RootZoneStorage), (3, Metric.ReferenceEt));
            CheckInputs(EffectAt(heat, Metric.Vegetation, 3), heat, (2, Metric.Vegetation), (3, Metric.DroughtStress));
            Check(heat.Series(Metric.Vegetation).samples[2].direction == flat &&
                EffectAt(heat, Metric.ActualEt, 9).priorMeaningfulDirection == down &&
                EffectAt(heat, Metric.ActualEt, 4).priorMeaningfulDirection == down,
                "Changed subthreshold vegetation remains an input and the ET reversal remembers its prior sign");

            // Advance the same calculation by a prefix on a separate branch.
            campaign.Fork(afterRain.id, "Cascade prefix");
            var prefixPreview = campaign.Preview(site, new[] { heatEdit }, "cascade-prefix");
            var prefixHead = campaign.Advance(prefixPreview, 3);
            campaign.SwitchBranch(rain.branchId);
            var afterHeat = campaign.Advance(heatPreview);
            var saved = FieldCampaign.Open(climate, path);
            CheckReplay(saved.RunForRevision(afterRain.id), rain, afterRain.id, 12, site);
            CheckReplay(saved.RunForRevision(afterHeat.id), heat, afterHeat.id, 12, site);
            var prefix = saved.RunForRevision(prefixHead.id);
            var prefixTrail = prefix.Site(site).scenario;
            Check(prefixTrail.months.Length == 12 && prefixHead.nextMonth == heat.startMonth + 3,
                "Partial advance retains the full stored projection and advances the date by three months");
            Equal(prefixHead.Site(site).current.storageMm, prefixTrail.months[2].water.storageMm,
                "Partial advance saves storage from the edited March frame");
            // Changes to uncommitted frames may not change an analysis of the saved prefix.
            prefixTrail.months[3] = default;
            CheckReplay(prefix, heat, prefixHead.id, 3, site);
            Check(FieldCascade.Analyze(prefix, site).effects.Length == 7 &&
                FieldCascade.Analyze(prefixPreview, site).frames.Length == 12, "Only committed months contribute to saved effects");

            var quietCampaign = FieldCampaign.Open(climate, selectedSiteId: site);
            var root = quietCampaign.Current.id;
            var quietEdit = new ForcingEdit(ForcingControl.Rain, 5, start + 5, start + 6);
            var quietPreview = quietCampaign.Preview(site, new[] { quietEdit }, "cascade-quiet");
            var quietResult = FieldCascade.Analyze(quietPreview, site);
            Check(quietResult.status == CascadeStatus.Quiet && quietResult.hasPhysicalLedger &&
                quietResult.series.Where(series => series.kind != CascadeKind.Direct).All(series => series.firstMeaningfulMonth == -1),
                "An applied edit can yield a quiet modeled response");
            CheckTransitions(quietResult, (5, Metric.Rain, onset, up), (6, Metric.Rain, quiet, flat));
            Check(EffectAt(quietResult, Metric.Rain, 6).priorMeaningfulDirection == up,
                "A quiet return keeps the last meaningful sign");
            CheckPair(quietResult, Metric.Rain, 5, 4.175000, 4.383750);
            CheckPair(quietResult, Metric.Rain, 6, 40.944000, 40.944000);
            CheckPair(quietResult, Metric.ActualEt, 5, 4.175000, 4.383750);
            CheckPair(quietResult, Metric.DroughtStress, 5, .982831, .981973);
            CheckPair(quietResult, Metric.RootZoneStorage, 11, 23.437350, 23.437350);
            Check(quietResult.Series(Metric.RootZoneStorage).samples.All(sample => sample.delta == 0),
                "Available zero storage is retained as a reading");
            var neutral = FieldCascade.Analyze(quietCampaign.Preview(site, null), site);
            Check(neutral.status == CascadeStatus.Quiet && neutral.effects.Length == 0 &&
                quietCampaign.Current.id == root && quietCampaign.Journal.runs == 0,
                "No-edit analysis and discarding a quiet preview leave the campaign untouched");
        }

        static void ValidateCascadeLimits(ClimateData climate)
        {
            const string site = "phoenix";
            var legacy = new FieldSave();
            legacy.sites.Add(new SavedLand { id = site, land = new LandState { soil = .42f, vegetation = .63f, elapsed = 15 } });
            var migrated = FieldCampaign.Open(climate, legacy: legacy, selectedSiteId: site);
            int start = migrated.Current.nextMonth;
            var legacyPreview = migrated.Preview(site, new[] { new ForcingEdit(ForcingControl.Rain, 100, start) });
            var illustrative = FieldCascade.Analyze(legacyPreview, site);
            Check(illustrative.status == CascadeStatus.Response && !illustrative.hasPhysicalLedger &&
                illustrative.Series(Metric.RootZoneWetnessIndex).available &&
                illustrative.Series(Metric.RootZoneWetnessIndex).firstMeaningfulMonth == start &&
                illustrative.series.Where(series => series.kind == CascadeKind.Physical)
                    .All(series => !series.available && series.samples.Length == 0 && !string.IsNullOrEmpty(series.unavailableReason)),
                "Migrated Phoenix frames retain illustrative effects when their physical ledgers are absent");

            var campaign = FieldCampaign.Open(climate, selectedSiteId: site);
            var preview = campaign.Preview(site, null);
            CheckUnavailable(FieldCascade.Analyze(preview, "missing-site"), "Missing site");
            CheckUnavailable(FieldCascade.Analyze((ScenarioRun)null, site), "Missing run");
            var missing = preview.Snapshot();
            missing.Site(site).reference = null;
            CheckUnavailable(FieldCascade.Analyze(missing, site), "Missing pair");
            var invalid = preview.Snapshot();
            invalid.committedMonths = 3;
            CheckUnavailable(FieldCascade.Analyze(invalid, site), "Commit count without a revision");
            invalid.committedRevisionId = "invalid-prefix";
            invalid.committedMonths = 13;
            CheckUnavailable(FieldCascade.Analyze(invalid, site), "Invalid commit width");
            invalid = preview.Snapshot();
            invalid.Site(site).scenario.months[1].monthIndex++;
            CheckUnavailable(FieldCascade.Analyze(invalid, site), "Mismatched calendar");
            invalid = preview.Snapshot();
            invalid.Site(site).scenario.before.storageMm += 2;
            CheckUnavailable(FieldCascade.Analyze(invalid, site), "Different initial state");
            invalid = preview.Snapshot();
            invalid.Site(site).scenario.months[1].water = default;
            var incomplete = FieldCascade.Analyze(invalid, site);
            Check(incomplete.status == CascadeStatus.Quiet && !incomplete.hasPhysicalLedger &&
                !incomplete.Series(Metric.RootZoneStorage).available &&
                !incomplete.Series(Metric.RootZoneWetnessIndex).available && incomplete.Series(Metric.Vegetation).available,
                "A missing physical ledger preserves usable saved indices without substituting illustrative wetness");
            for (int i = 0; i < invalid.Site(site).scenario.months.Length; i++)
                invalid.Site(site).scenario.months[i].land = default;
            invalid.Site(site).scenario.months[0].weather.temperature += 2;
            var noLand = FieldCascade.Analyze(invalid, site);
            CheckUnavailable(noLand, "Direct change with missing modeled outcomes");
            Check(noLand.Series(Metric.Temperature).available, "Missing outcomes preserve paired weather readings");

            var changed = preview.Snapshot();
            var pair = changed.Site(site);
            pair.scenario.months[0].water.runoffMm = 1;
            pair.reference.months[1].water.runoffMm = 1;
            pair.scenario.months[2].water.runoffMm = 1.000001;
            pair.scenario.months[3].water.actualEtMm += 2;
            pair.scenario.months[4].water.actualEtMm = double.NaN;
            var threshold = FieldCascade.Analyze(changed, site);
            var runoff = threshold.Series(Metric.Runoff);
            Check(runoff.samples[0].delta == 1 && runoff.samples[1].delta == -1 &&
                runoff.samples[0].direction == Direction.LittleChange && runoff.samples[1].direction == Direction.LittleChange &&
                runoff.firstMeaningfulMonth == start + 2, "Both tolerance boundaries are inclusive before display rounding");
            Check(!threshold.hasPhysicalLedger && !threshold.Series(Metric.ActualEt).available &&
                threshold.Series(Metric.ActualEt).firstMeaningfulMonth == -1 &&
                threshold.Series(Metric.ActualEt).samples.Length == 0 &&
                threshold.effects.All(effect => effect.metric != Metric.ActualEt),
                "A nonfinite reading invalidates its whole series, including an earlier candidate onset");

            var humidClimate = JsonUtility.FromJson<ClimateData>(JsonUtility.ToJson(climate));
            var phoenix = humidClimate.regions.First(region => region.id == site);
            for (int i = 0; i < phoenix.normals.Length; i++) phoenix.normals[i].humidity = 98;
            var humid = FieldCampaign.Open(humidClimate, selectedSiteId: site);
            var inheritedHumidity = new ForcingEdit(ForcingControl.Humidity, 2, start);
            humid.Advance(humid.Preview(site, new[] { inheritedHumidity }), 1);
            var changedHumidity = new ForcingEdit(ForcingControl.Humidity, 40, humid.Current.nextMonth);
            var clamped = FieldCascade.Analyze(humid.Preview(site, new[] { changedHumidity }), site);
            var fuelEffect = clamped.effects.FirstOrDefault(effect => effect.metric == Metric.FuelDryness);
            Check(clamped.Series(Metric.Humidity).samples.All(sample => sample.edited == 100 && sample.delta == 0) &&
                clamped.Series(Metric.DroughtStress).samples.All(sample => sample.delta == 0) &&
                fuelEffect != null && fuelEffect.inputs.All(input => input.metric != Metric.Humidity) &&
                fuelEffect.explanation.Contains("changed humidity dial"),
                "A clamped humidity reading stays quiet while the dial can affect modeled fuel dryness");
            Debug.Log("FIELD_CASCADE_OK: Arizona rain, heat, quiet, saved prefix, causal inputs, thresholds, and missing ledgers.");
        }

        static CascadeEffect EffectAt(CascadeResult result, Metric metric, int offset) =>
            result.effects.Single(effect => effect.metric == metric && effect.sample.monthIndex == result.startMonth + offset);

        static void CheckTransitions(CascadeResult result,
            params (int offset, Metric metric, Transition transition, Direction direction)[] expected)
        {
            var actual = result.effects.Select(effect =>
                (effect.sample.monthIndex - result.startMonth, effect.metric, effect.transition, effect.sample.direction)).ToArray();
            Check(actual.SequenceEqual(expected), $"Unexpected transitions for {result.runId}: {string.Join(", ", actual)}");
        }

        static void CheckInputs(CascadeEffect effect, CascadeResult result, params (int offset, Metric metric)[] expected)
        {
            var actual = effect.inputs.Select(input => (input.monthIndex - result.startMonth, input.metric)).ToArray();
            Check(actual.SequenceEqual(expected) && actual.All(input => result.Series(input.metric).samples[input.Item1].delta != 0),
                $"Changed inputs for {effect.metric} at offset {effect.sample.monthIndex - result.startMonth}");
        }

        static void CheckPair(CascadeResult result, Metric metric, int offset, double inherited, double edited)
        {
            var series = result.Series(metric);
            Check(series.available, $"{metric} fixture is available");
            var sample = series.samples[offset];
            const double rounding = .000002;
            Check(Math.Abs(sample.inherited - inherited) < rounding && Math.Abs(sample.edited - edited) < rounding &&
                sample.delta == sample.edited - sample.inherited && sample.monthIndex == result.startMonth + offset,
                $"{metric} fixture readings at offset {offset}: expected {inherited:R} / {edited:R}, got {sample.inherited:R} / {sample.edited:R}");
        }

        static void CheckLedgers(CascadeResult result)
        {
            Check(result.frames.Length == 12 && result.frames.Select((frame, i) =>
                frame.monthIndex == result.startMonth + i && frame.inherited.monthIndex == frame.monthIndex &&
                frame.edited.monthIndex == frame.monthIndex && frame.inherited.water.available && frame.edited.water.available &&
                Math.Abs(frame.inherited.water.BalanceErrorMm) < 1e-7 &&
                Math.Abs(frame.edited.water.BalanceErrorMm) < 1e-7).All(valid => valid), "Matched dated frames close each water ledger");
            Equal(result.frames[0].inherited.water.startStorageMm, result.frames[0].edited.water.startStorageMm,
                "Matched runs start from the same storage");
        }

        static void CheckReplay(ScenarioRun run, CascadeResult preview, string revision, int months, string site)
        {
            var saved = FieldCascade.Analyze(run, site);
            Check(saved.provenance == CascadeProvenance.Committed && saved.committedRevisionId == revision &&
                saved.runId == run.id && saved.branchId == run.branchId && saved.committedMonths == months &&
                saved.sourceRevisionId == preview.sourceRevisionId && saved.rulesVersion == preview.rulesVersion &&
                saved.modelVersion == preview.modelVersion && saved.dataFingerprint == preview.dataFingerprint &&
                saved.edits.SequenceEqual(preview.edits), "Saved cascade uses the run's recorded pins");
            var projection = run.Site(site);
            Check(saved.frames.Length == months && saved.frames.Select(frame => frame.inherited)
                .SequenceEqual(projection.reference.months.Take(months)) && saved.frames.Select(frame => frame.edited)
                .SequenceEqual(projection.scenario.months.Take(months)), "Saved cascade uses the exact committed frames");
            foreach (var series in saved.series.Where(series => series.available))
            {
                var expected = preview.Series(series.metric);
                for (int i = 0; i < months; i++)
                {
                    Equal(series.samples[i].inherited, expected.samples[i].inherited, $"{series.metric} inherited replay reading");
                    Equal(series.samples[i].edited, expected.samples[i].edited, $"{series.metric} edited replay reading");
                }
            }
            var events = preview.effects.Where(effect => effect.sample.monthIndex < saved.startMonth + months);
            Check(saved.effects.Select(effect => (effect.metric, effect.sample.monthIndex, effect.sample.direction,
                    effect.transition, effect.priorMeaningfulDirection))
                .SequenceEqual(events.Select(effect => (effect.metric, effect.sample.monthIndex, effect.sample.direction,
                    effect.transition, effect.priorMeaningfulDirection))),
                "Saved cascade reproduces the preview effects within the committed window");
        }

        static void CheckUnavailable(CascadeResult result, string task) =>
            Check(result.status == CascadeStatus.Unavailable && result.effects.Length == 0 &&
                !string.IsNullOrEmpty(result.unavailableReason), task);

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
