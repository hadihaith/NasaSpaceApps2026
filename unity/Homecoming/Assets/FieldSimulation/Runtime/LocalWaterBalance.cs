using System;

namespace Homecoming.Field
{
    [Serializable] public sealed class WaterBalanceInputs
    {
        public string rangeSource, elevationSource, capacitySource;
        public double elevationM, soilCapacityMm, drainageAboveFraction, monthlyDrainageFraction;
        public double[] dailyRangeC;
    }

    [Serializable] public struct WaterLedger
    {
        public bool available;
        public double capacityMm, startStorageMm, rainMm, et0Mm, actualEtMm, drainageMm, runoffMm, storageMm, vpdKpa;
        public double BalanceErrorMm => startStorageMm + rainMm - actualEtMm - drainageMm - runoffMm - storageMm;
    }

    public static class LocalWaterBalance
    {
        public const int SpinUpYears = 20;
        public const string Label = "Conditional local water-balance scenario";
        public const string IllustrativeLabel = "Illustrative land response (indices)";
        public const string Assumptions = "Phoenix uses 1991–2020 NASA POWER normals, Phoenix Airport's NOAA GSOM daily temperature range as a regional proxy, a 600 m regional elevation assumption and an illustrative 150 mm soil bucket. Solar radiation is estimated from temperature range. Monthly rain fills the bucket, evapotranspiration removes available water, excess above capacity becomes local runoff, then 25% of retained water above 65% capacity drains. All precipitation enters as liquid. The model omits snow, irrigation and river routing. Vegetation and surface-water artwork are illustrative.";
        public const string Initialization = "1991–2020 monthly weather normals repeated for 20 years before the scenario start, starting with a half-full model bucket.";

        public static WaterBalanceInputs Inputs(RegionClimate region)
        {
            var inputs = region.waterBalance;
            // Unity's inline JSON serializer can materialize an empty object for a missing field.
            if (inputs != null && string.IsNullOrEmpty(inputs.rangeSource) &&
                string.IsNullOrEmpty(inputs.elevationSource) && string.IsNullOrEmpty(inputs.capacitySource) &&
                inputs.soilCapacityMm == 0 && (inputs.dailyRangeC == null || inputs.dailyRangeC.Length == 0))
                inputs = null;
            if (inputs == null && region.id == "phoenix")
            {
                // Pinned from public/data/baselines.json, paired 1991–2020 TMAX/TMIN
                // means for USW00023183. Matches src/lib/earth/climate.ts.
                inputs = new WaterBalanceInputs
                {
                    rangeSource = "NOAA GSOM v1 / USW00023183 / 1991–2020 TMAX minus TMIN",
                    elevationSource = "600 m representative regional assumption",
                    capacitySource = "150 mm teaching assumption (uncalibrated)",
                    elevationM = 600,
                    soilCapacityMm = 150,
                    drainageAboveFraction = .65,
                    monthlyDrainageFraction = .25,
                    dailyRangeC = new[] { 12.187, 12.33, 13.287, 13.97, 14.08, 14.45, 12.417, 12.073, 12.624, 13.337, 12.854, 11.82 }
                };
            }
            if (inputs == null) return null;
            if (string.IsNullOrWhiteSpace(inputs.rangeSource) || string.IsNullOrWhiteSpace(inputs.elevationSource) ||
                string.IsNullOrWhiteSpace(inputs.capacitySource) || inputs.dailyRangeC?.Length != 12 ||
                !(inputs.elevationM >= -500 && inputs.elevationM <= 9000) ||
                !(inputs.soilCapacityMm > 0 && inputs.soilCapacityMm <= 2000) ||
                !(inputs.drainageAboveFraction >= 0 && inputs.drainageAboveFraction <= 1) ||
                !(inputs.monthlyDrainageFraction >= 0 && inputs.monthlyDrainageFraction <= 1) ||
                Array.Exists(inputs.dailyRangeC, range => !(range > 0 && range <= 60)) ||
                !(region.latitude >= -90 && region.latitude <= 90))
                throw new ArgumentException($"Invalid water balance inputs for {region.id}.", nameof(region));
            return inputs;
        }

        public static WaterLedger Step(WaterBalanceInputs inputs, double latitude, DateTime month,
            Weather weather, double previousTemperatureC, double storageMm)
        {
            if (!(storageMm >= 0 && storageMm <= inputs.soilCapacityMm) || !(weather.rain >= 0) ||
                float.IsInfinity(weather.rain) || !(weather.wind >= 0) || float.IsInfinity(weather.wind) ||
                !(weather.humidity >= 0 && weather.humidity <= 100) ||
                !(weather.temperature >= -100 && weather.temperature <= 80) ||
                !(previousTemperatureC >= -100 && previousTemperatureC <= 80))
                throw new ArgumentOutOfRangeException(nameof(weather), "Weather and initial storage must be within the modeled range.");

            var (et0Mm, vpdKpa) = ReferenceEt(inputs, latitude, month, weather, previousTemperatureC);
            double available = storageMm + weather.rain;
            double capacity = inputs.soilCapacityMm;
            double actualEt = Math.Min(available, et0Mm * Math.Min(1, available / capacity));
            double remaining = available - actualEt;
            double runoff = Math.Max(0, remaining - capacity);
            // Overflow is local excess, with no sub-month timing or channel transport.
            double retained = Math.Min(capacity, remaining);
            double drainage = Math.Max(0, retained - capacity * inputs.drainageAboveFraction) * inputs.monthlyDrainageFraction;
            return new WaterLedger
            {
                available = true, capacityMm = capacity, startStorageMm = storageMm, rainMm = weather.rain,
                et0Mm = et0Mm, actualEtMm = actualEt, drainageMm = drainage,
                runoffMm = runoff, storageMm = retained - drainage, vpdKpa = vpdKpa
            };
        }

        static double SaturationVapourPressure(double temperature) =>
            .6108 * Math.Exp(17.27 * temperature / (temperature + 237.3));

        // Monthly form of the repo's FAO-56-inspired reference ET calculation.
        // https://www.fao.org/4/x0490e/x0490e08.htm
        public static (double et0Mm, double vpdKpa) ReferenceEt(WaterBalanceInputs inputs, double latitude,
            DateTime month, Weather weather, double previousTemperatureC)
        {
            const double radiationCoefficient = .16, albedo = .23;
            double temperature = weather.temperature;
            double range = inputs.dailyRangeC[month.Month - 1];
            double high = temperature + range / 2;
            double low = temperature - range / 2;
            double es = (SaturationVapourPressure(high) + SaturationVapourPressure(low)) / 2;
            double ea = Math.Max(1, Math.Min(100, weather.humidity)) / 100 * es;
            double vpd = es - ea;
            double slope = 4098 * SaturationVapourPressure(temperature) / Math.Pow(temperature + 237.3, 2);
            double pressure = 101.3 * Math.Pow((293 - .0065 * inputs.elevationM) / 293, 5.26);
            double gamma = .000665 * pressure;
            double u2 = Math.Max(.5, weather.wind * (4.87 / Math.Log(67.8 * 10 - 5.42)));
            int days = DateTime.DaysInMonth(month.Year, month.Month);
            double angle = 2 * Math.PI * (month.AddDays((days - 1) / 2).DayOfYear) / (DateTime.IsLeapYear(month.Year) ? 366 : 365);
            double phi = latitude * Math.PI / 180;
            double inverseDistance = 1 + .033 * Math.Cos(angle);
            double declination = .409 * Math.Sin(angle - 1.39);
            double sunset = Math.Acos(Math.Max(-1, Math.Min(1, -Math.Tan(phi) * Math.Tan(declination))));
            double ra = Math.Max(0, 24 * 60 / Math.PI * .082 * inverseDistance *
                (sunset * Math.Sin(phi) * Math.Sin(declination) + Math.Cos(phi) * Math.Cos(declination) * Math.Sin(sunset)));
            double rso = (.75 + 2e-5 * inputs.elevationM) * ra;
            double rs = Math.Min(rso, radiationCoefficient * Math.Sqrt(range) * ra);
            double cloudFactor = rso > 0 ? Math.Max(0, 1.35 * rs / rso - .35) : 0;
            double rnl = 4.903e-9 * (Math.Pow(high + 273.16, 4) + Math.Pow(low + 273.16, 4)) / 2 *
                (.34 - .14 * Math.Sqrt(ea)) * cloudFactor;
            double rn = (1 - albedo) * rs - rnl;
            double g = .14 * (temperature - previousTemperatureC);
            double dailyEt0 = (.408 * slope * (rn - g) + gamma * 900 / (temperature + 273) * u2 * vpd) /
                (slope + gamma * (1 + .34 * u2));
            return (Math.Max(0, dailyEt0) * days, vpd);
        }
    }
}
