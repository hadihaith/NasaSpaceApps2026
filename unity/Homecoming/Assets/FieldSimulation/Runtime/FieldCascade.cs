using System;
using System.Collections.Generic;
using System.Linq;
using static Homecoming.Field.CascadeMetric;

namespace Homecoming.Field
{
    public enum CascadeProvenance { Preview, Committed }
    public enum CascadeStatus { Unavailable, Quiet, Response }
    public enum CascadeKind { Direct, Physical, Educational }
    public enum CascadeDirection { LittleChange, Increase, Decrease }
    public enum CascadeTransition { Onset, ReturnToQuiet, Reversal, Resumed }

    public enum CascadeMetric
    {
        Temperature, Rain, Humidity, Wind, VaporPressureDeficit, ReferenceEt, ActualEt,
        Runoff, Drainage, RootZoneStorage, RootZoneWetnessIndex, DroughtStress, Vegetation, FuelDryness
    }

    public sealed class CascadeResult
    {
        public string rulesVersion, runId, branchId, sourceRevisionId, committedRevisionId, editedSiteId,
            siteId, modelVersion, dataFingerprint, unavailableReason;
        public CascadeProvenance provenance;
        public CascadeStatus status;
        public int startMonth, committedMonths;
        public bool hasPhysicalLedger;
        public ForcingEdit[] edits;
        public CascadeFrame[] frames;
        public CascadeSeries[] series;
        public CascadeEffect[] effects;

        public CascadeSeries Series(CascadeMetric metric) => series[(int)metric];
    }

    public struct CascadeFrame
    {
        public int monthIndex;
        public FieldMonth inherited, edited;
    }

    public sealed class CascadeSeries
    {
        public CascadeMetric metric;
        public CascadeKind kind;
        public string label, valueUnit, deltaUnit, unavailableReason;
        public double tolerance;
        public bool available;
        public int firstMeaningfulMonth = -1;
        public CascadeSample[] samples = Array.Empty<CascadeSample>();
    }

    public struct CascadeSample
    {
        public int monthIndex;
        public double inherited, edited, delta;
        public CascadeDirection direction;
    }

    public sealed class CascadeEffect
    {
        public CascadeMetric metric;
        public CascadeTransition transition;
        public CascadeSample sample;
        public CascadeDirection priorMeaningfulDirection;
        public string explanation;
        public CascadeSampleKey[] inputs;
    }

    public struct CascadeSampleKey
    {
        public CascadeMetric metric;
        public int monthIndex;
    }

    public static class FieldCascade
    {
        public const string RulesVersion = "homecoming-cascade-1";
        const string FluxUnit = "mm/month";
        const string IndexUnit = "index (0–1)";
        const string IndexDeltaUnit = "index points";
        const string MissingLedger = "Both runs need a physical water ledger for every month in this window.";
        static readonly int LastMonth = FieldCalendar.Index(DateTime.MaxValue);

        static readonly (CascadeKind kind, string label, string unit, string deltaUnit, double tolerance,
            Func<FieldMonth, double> value)[] Metrics =
        {
            (CascadeKind.Direct, "Air temperature", "°C", "°C", .01, m => m.weather.temperature),
            (CascadeKind.Direct, "Precipitation", FluxUnit, FluxUnit, .1, m => m.weather.rain),
            (CascadeKind.Direct, "Relative humidity", "%", "percentage points", .01, m => m.weather.humidity),
            (CascadeKind.Direct, "Wind speed", "m/s", "m/s", .01, m => m.weather.wind),
            (CascadeKind.Physical, "Vapor pressure deficit", "kPa", "kPa", .05, m => m.water.vpdKpa),
            (CascadeKind.Physical, "Reference evapotranspiration", FluxUnit, FluxUnit, 1, m => m.water.et0Mm),
            (CascadeKind.Physical, "Actual evapotranspiration", FluxUnit, FluxUnit, 1, m => m.water.actualEtMm),
            (CascadeKind.Physical, "Local overflow", FluxUnit, FluxUnit, 1, m => m.water.runoffMm),
            (CascadeKind.Physical, "Drainage", FluxUnit, FluxUnit, 1, m => m.water.drainageMm),
            (CascadeKind.Physical, "Root-zone storage", "mm", "mm", 1, m => m.water.storageMm),
            (CascadeKind.Educational, "Root-zone wetness index", IndexUnit, IndexDeltaUnit, .01, m => m.land.soil),
            (CascadeKind.Educational, "Drought stress index", IndexUnit, IndexDeltaUnit, .02, m => m.land.drought),
            (CascadeKind.Educational, "Vegetation index", IndexUnit, IndexDeltaUnit, .02, m => m.land.vegetation),
            (CascadeKind.Educational, "Fuel dryness index", IndexUnit, IndexDeltaUnit, .02, m => m.land.fuel)
        };

        public static CascadeResult Analyze(ScenarioPreview preview, string siteId) => Analyze(preview?.Run, siteId);

        public static CascadeResult Analyze(ScenarioRun run, string siteId)
        {
            bool committed = !string.IsNullOrEmpty(run?.committedRevisionId);
            var result = new CascadeResult
            {
                rulesVersion = RulesVersion,
                runId = run?.id,
                branchId = run?.branchId,
                sourceRevisionId = run?.sourceRevisionId,
                committedRevisionId = run?.committedRevisionId ?? "",
                editedSiteId = run?.editedSiteId,
                siteId = siteId,
                modelVersion = run?.modelVersion,
                dataFingerprint = run?.dataFingerprint,
                startMonth = run?.startMonth ?? 0,
                committedMonths = run?.committedMonths ?? 0,
                provenance = committed ? CascadeProvenance.Committed : CascadeProvenance.Preview,
                edits = run?.edits == null ? Array.Empty<ForcingEdit>() : (ForcingEdit[])run.edits.Clone(),
                frames = Array.Empty<CascadeFrame>(),
                effects = Array.Empty<CascadeEffect>(),
                series = Metrics.Select((spec, index) => new CascadeSeries
                {
                    metric = (CascadeMetric)index, kind = spec.kind, label = spec.label, valueUnit = spec.unit,
                    deltaUnit = spec.deltaUnit, tolerance = spec.tolerance
                }).ToArray()
            };
            if (run == null)
                return Unavailable(result, "The scenario run is missing.");
            if (committed ? run.committedMonths <= 0 || string.IsNullOrWhiteSpace(run.committedRevisionId) :
                run.committedMonths != 0)
                return Unavailable(result, "The saved revision and committed month count do not agree.");
            if (string.IsNullOrWhiteSpace(siteId) || run.sites == null)
                return Unavailable(result, "The comparison for this site is missing.");
            // Match ScenarioRun.Site while tolerating empty slots in older snapshots.
            var projection = Array.Find(run.sites, site => site?.siteId == siteId);
            var inherited = projection?.reference;
            var edited = projection?.scenario;
            if (inherited?.months == null || edited?.months == null)
                return Unavailable(result, "Both inherited and edited monthly frames are needed.");
            if (!inherited.before.Equals(edited.before))
                return Unavailable(result, "The inherited and edited runs start from different land states.");

            int count = committed ? run.committedMonths : inherited.months.Length;
            if (count < 1 || count > FieldCalendar.MonthsInPreview ||
                inherited.months.Length < count || edited.months.Length < count ||
                (!committed && edited.months.Length != count))
                return Unavailable(result, "The month window does not fit the paired projections.");
            if (run.startMonth < 0 || run.startMonth > LastMonth - (count - 1))
                return Unavailable(result, "The scenario month window is outside the calendar.");

            var frames = new CascadeFrame[count];
            bool ledgerPresent = true, landPresent = true, legacyLand = true, physicalLand = true;
            for (int i = 0; i < frames.Length; i++)
            {
                int month = run.startMonth + i;
                var a = inherited.months[i];
                var b = edited.months[i];
                if (a.monthIndex != month || b.monthIndex != month)
                    return Unavailable(result, "The paired frames must cover the same consecutive calendar months.");
                frames[i] = new CascadeFrame { monthIndex = month, inherited = a, edited = b };
                ledgerPresent &= HasLedger(a) && HasLedger(b);
                // Elapsed time distinguishes a saved index of zero from a missing land snapshot.
                long elapsed = (long)inherited.before.elapsed + i + 1;
                landPresent &= elapsed > 0 && a.land.elapsed == elapsed && b.land.elapsed == elapsed;
                legacyLand &= !a.land.hasStorage && !b.land.hasStorage;
                physicalLand &= a.land.hasStorage && b.land.hasStorage;
            }
            result.frames = frames;

            foreach (var series in result.series)
            {
                if (series.kind == CascadeKind.Physical && !ledgerPresent)
                    series.unavailableReason = MissingLedger;
                else if (series.kind == CascadeKind.Educational && !landPresent)
                    series.unavailableReason = "Both runs need a saved land state for every month in this window.";
                else if (series.metric == RootZoneWetnessIndex && !legacyLand)
                    series.unavailableReason = "The illustrative wetness index is available only for two illustrative land runs.";
                else if (series.metric == DroughtStress && !legacyLand && !physicalLand)
                    series.unavailableReason = "Drought stress requires the same land model throughout both runs.";
                else
                    ReadSeries(series, frames, Metrics[(int)series.metric].value);
            }

            result.hasPhysicalLedger = result.series.Where(s => s.kind == CascadeKind.Physical).All(s => s.available);
            if (!result.series.Any(s => s.available && s.kind != CascadeKind.Direct))
            {
                result.unavailableReason = "No paired modeled outcome is available for this window.";
                return result;
            }

            var effects = new List<CascadeEffect>();
            foreach (var series in result.series)
                AddEffects(result, series, effects);

            result.effects = effects.OrderBy(effect => effect.sample.monthIndex).ThenBy(effect => effect.metric).ToArray();
            result.status = result.series.Any(s => s.kind != CascadeKind.Direct && s.firstMeaningfulMonth >= 0)
                ? CascadeStatus.Response : CascadeStatus.Quiet;
            return result;
        }

        static CascadeResult Unavailable(CascadeResult result, string reason)
        {
            result.unavailableReason = reason;
            foreach (var series in result.series)
                series.unavailableReason = reason;
            return result;
        }

        static bool HasLedger(FieldMonth month) =>
            month.land.hasStorage && month.water.available &&
            month.water.capacityMm > 0 && Finite(month.water.capacityMm);

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        static void ReadSeries(CascadeSeries series, CascadeFrame[] frames, Func<FieldMonth, double> read)
        {
            var samples = new CascadeSample[frames.Length];
            int firstMeaningfulMonth = -1;
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                double a = read(frame.inherited), b = read(frame.edited), delta = b - a;
                if (!Finite(a) || !Finite(b) || !Finite(delta))
                {
                    series.unavailableReason = "A paired reading is missing or non-finite in this window.";
                    return;
                }

                var direction = delta > series.tolerance ? CascadeDirection.Increase :
                    delta < -series.tolerance ? CascadeDirection.Decrease : CascadeDirection.LittleChange;
                samples[i] = new CascadeSample
                {
                    monthIndex = frame.monthIndex, inherited = a, edited = b, delta = delta, direction = direction
                };
                if (firstMeaningfulMonth == -1 && direction != CascadeDirection.LittleChange)
                    firstMeaningfulMonth = frame.monthIndex;
            }

            series.available = true;
            series.samples = samples;
            series.firstMeaningfulMonth = firstMeaningfulMonth;
        }

        static void AddEffects(CascadeResult result, CascadeSeries series, List<CascadeEffect> effects)
        {
            var previous = CascadeDirection.LittleChange;
            var lastMeaningful = CascadeDirection.LittleChange;
            foreach (var sample in series.samples)
            {
                if (sample.direction == previous) continue;

                CascadeTransition transition;
                if (sample.direction == CascadeDirection.LittleChange)
                    transition = CascadeTransition.ReturnToQuiet;
                else if (lastMeaningful == CascadeDirection.LittleChange)
                    transition = CascadeTransition.Onset;
                else if (lastMeaningful != sample.direction)
                    transition = CascadeTransition.Reversal;
                else
                    transition = CascadeTransition.Resumed;

                var frame = result.frames[sample.monthIndex - result.startMonth];
                var inputs = new List<CascadeSampleKey>();
                string calculation = Explain(result, series.metric, frame, inputs);
                string comparison = sample.direction == CascadeDirection.Increase ? "Higher in the edited run. " :
                    sample.direction == CascadeDirection.Decrease ? "Lower in the edited run. " :
                    "The paired readings are within tolerance. ";
                effects.Add(new CascadeEffect
                {
                    metric = series.metric, transition = transition, sample = sample,
                    priorMeaningfulDirection = lastMeaningful, explanation = comparison + calculation,
                    inputs = inputs.OrderBy(key => key.monthIndex).ThenBy(key => key.metric).ToArray()
                });

                previous = sample.direction;
                if (previous != CascadeDirection.LittleChange) lastMeaningful = previous;
            }
        }

        static string Explain(CascadeResult result, CascadeMetric metric, CascadeFrame frame, List<CascadeSampleKey> inputs)
        {
            int offset = frame.monthIndex - result.startMonth;
            var a = frame.inherited;
            var b = frame.edited;
            bool physical = a.land.hasStorage && b.land.hasStorage;

            void Add(CascadeMetric input, int index)
            {
                var series = result.Series(input);
                if (index >= 0 && series.available && series.samples[index].delta != 0)
                    inputs.Add(new CascadeSampleKey { metric = input, monthIndex = series.samples[index].monthIndex });
            }

            void Current(CascadeMetric input) => Add(input, offset);
            void Prior(CascadeMetric input) => Add(input, offset - 1);

            string IllustrativeEvaporationDrivers()
            {
                if (EvaporationScore(a.forcing) == EvaporationScore(b.forcing)) return "";
                var dials = new List<string>();
                if (a.forcing.heat != b.forcing.heat) { Current(Temperature); dials.Add("heat"); }
                if (a.forcing.humidity != b.forcing.humidity) { Current(Humidity); dials.Add("humidity"); }
                if (a.forcing.wind != b.forcing.wind) { Current(Wind); dials.Add("wind"); }
                return " The evaporation score used changed settings for " + string.Join(", ", dials) + ".";
            }

            switch (metric)
            {
                case Temperature:
                    return "The resolved heat dial adds to the shared monthly temperature normal.";
                case Rain:
                    return "The resolved rain dial multiplies the shared monthly precipitation normal.";
                case Humidity:
                    return "The resolved humidity dial adds to the shared monthly normal, bounded from 0% to 100%.";
                case Wind:
                    return "The resolved wind dial adds to the shared monthly normal, with a floor of 0 m/s.";
                case VaporPressureDeficit:
                    Current(Temperature);
                    Current(Humidity);
                    return "Vapor pressure deficit uses temperature and humidity with the fixed daily temperature range and a humidity floor.";
                case ReferenceEt:
                    Prior(Temperature);
                    Current(Temperature);
                    Current(Humidity);
                    Current(Wind);
                    Current(VaporPressureDeficit);
                    return "Modeled atmospheric drying demand depends on temperature, humidity, wind and vapor pressure deficit. The soil heat term also uses the previous month's temperature.";
                case ActualEt:
                    Prior(RootZoneStorage);
                    Current(Rain);
                    Current(ReferenceEt);
                    return "Modeled water loss scales with atmospheric drying demand and is capped by available water from start storage and rain." +
                        (ExhaustsWater(a.water) && ExhaustsWater(b.water) ? " Both runs used all available water this month." : "") +
                        (b.water.startStorageMm < a.water.startStorageMm ? " The edited run started this month with less stored water." : "");
                case Runoff:
                case Drainage:
                    Prior(RootZoneStorage);
                    Current(Rain);
                    Current(ActualEt);
                    return metric == Runoff
                        ? "Local overflow is water remaining above bucket capacity after actual evapotranspiration."
                        : "Drainage removes a fraction of retained water above the storage threshold after actual evapotranspiration.";
                case RootZoneStorage:
                    Prior(RootZoneStorage);
                    Current(Rain);
                    Current(ActualEt);
                    Current(Runoff);
                    Current(Drainage);
                    return "End-of-month storage adds rain to start storage and subtracts actual evapotranspiration, drainage and local overflow.";
                case RootZoneWetnessIndex:
                    Prior(RootZoneWetnessIndex);
                    Current(Rain);
                    return "Illustrative wetness blends the previous month's wetness toward a bounded target set by rain and the illustrative evaporation score." +
                        IllustrativeEvaporationDrivers();
                case DroughtStress:
                    if (!physical)
                    {
                        Current(RootZoneWetnessIndex);
                        return "Illustrative drought stress uses this month's wetness and the illustrative evaporation score." +
                            IllustrativeEvaporationDrivers();
                    }
                    Current(ReferenceEt);
                    Current(ActualEt);
                    return "Drought stress is the fraction of reference evapotranspiration unmet by actual evapotranspiration, with zero stress when reference evapotranspiration is zero.";
                case Vegetation:
                    Prior(Vegetation);
                    Current(physical ? RootZoneStorage : RootZoneWetnessIndex);
                    Current(DroughtStress);
                    string thermal = "";
                    if (VegetationTemperatureTerms(a) != VegetationTemperatureTerms(b))
                    {
                        Current(Temperature);
                        thermal = " The temperature and heat setting changed the direct hot or cold term.";
                    }
                    return "The vegetation index carries over from the previous month and responds to current drought stress and soil wetness." + thermal;
                case FuelDryness:
                    Prior(FuelDryness);
                    Current(DroughtStress);
                    if (a.forcing.humidity != b.forcing.humidity)
                    {
                        Current(Humidity);
                        return "The fuel dryness index uses prior fuel dryness, current drought stress and the changed humidity dial. The dial contributes directly even when relative humidity is clamped.";
                    }
                    return "The fuel dryness index carries over from the previous month and responds to drought stress and the humidity dial.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(metric));
            }
        }

        static bool ExhaustsWater(WaterLedger water) =>
            Math.Abs(water.actualEtMm - (water.startStorageMm + water.rainMm)) < 1e-7;

        // Compare the dial and temperature terms in ClimateModel.Project to identify changed inputs.
        // Metric readings always come from the recorded frames.
        static float EvaporationScore(Forcing forcing) =>
            .012f * forcing.heat + .10f * (-forcing.humidity / 100f) + .007f * forcing.wind;

        static (float hot, float cold) VegetationTemperatureTerms(FieldMonth month)
        {
            float Bound(float value) => Math.Max(0f, Math.Min(1f, value));
            return (Bound((month.weather.temperature - 30) / 13) * Bound(month.forcing.heat / 5f),
                Bound((8 - month.weather.temperature) / 12) * Bound(-month.forcing.heat / 4f));
        }
    }
}
