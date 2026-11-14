using System;
using System.Collections.Generic;
using UnityEngine;

namespace Homecoming.Field
{
    [Serializable] public sealed class ClimateData
    {
        public RegionClimate[] regions;
        public ClimateControl[] controls;
        public Discovery[] discoveries;
        public EvidenceSeries[] evidence;
        public int requiredDiscoveries;
    }

    [Serializable] public sealed class RegionClimate
    {
        public string id, name;
        public float latitude, longitude;
        public Weather[] normals;
        public WaterBalanceInputs waterBalance;
    }

    [Serializable] public sealed class ClimateControl
    {
        public string label, unit, color;
        public int min, max, step;
    }

    [Serializable] public sealed class Discovery { public string id, label, quote, science, evidence; }
    [Serializable] public sealed class EvidenceSeries
    {
        public string id, region, key, variable, unit, provider, product, version, url, aggregation;
        public YearPoint[] years;
        public float slope;
        public bool hasSlope;
    }
    [Serializable] public struct YearPoint { public int year; public float value; }
    [Serializable] public struct Weather { public float temperature, rain, humidity, wind, soil; }
    [Serializable] public struct LandState
    {
        public float soil, vegetation, drought, fuel;
        public int elapsed;
        public bool hasStorage;
        public double storageMm;
        public float previousTemperatureC;
    }

    [Serializable] public struct Forcing
    {
        public int heat, rain, humidity, wind;
        public Forcing(int heat, int rain, int humidity, int wind) =>
            (this.heat, this.rain, this.humidity, this.wind) = (heat, rain, humidity, wind);
        public int this[int i]
        {
            get => i == 0 ? heat : i == 1 ? rain : i == 2 ? humidity : wind;
            set
            {
                switch (i)
                {
                    case 0: heat = value; break;
                    case 1: rain = value; break;
                    case 2: humidity = value; break;
                    case 3: wind = value; break;
                }
            }
        }
    }

    [Serializable] public struct FieldMonth
    {
        public int monthIndex;
        public DateTime Date => FieldCalendar.Date(monthIndex);
        public LandState land;
        public Weather weather;
        public Weather normal;
        public Forcing forcing;
        public WaterLedger water;
    }

    [Serializable] public sealed class FieldTrial
    {
        public LandState before;
        public FieldMonth[] months;
        public List<string> findings = new();
        public FieldMonth Final => months[months.Length - 1];
    }

    public static class ClimateModel
    {
        public const string Version = "homecoming-local-1";

        public static LandState Initial(RegionClimate region) =>
            new() { soil = region.normals[0].soil, vegetation = .74f, fuel = .2f };

        // Original teaching model entry point, retained for the media capture and other callers.
        public static FieldTrial Run(RegionClimate region, LandState before, Forcing settings, int duration)
        {
            if (duration != 1 && duration != 3 && duration != 12)
                throw new ArgumentOutOfRangeException(nameof(duration));
            return Project(region, before, FieldCalendar.StartMonth + before.elapsed, _ => settings, duration);
        }

        public static LandState Initialize(RegionClimate region, int startMonth)
        {
            var date = FieldCalendar.Date(startMonth);
            var initial = Initial(region);
            initial.soil = Normal(region, startMonth).soil;
            var inputs = LocalWaterBalance.Inputs(region);
            if (inputs == null) return initial;

            initial.hasStorage = true;
            initial.storageMm = inputs.soilCapacityMm / 2;
            int first = startMonth - LocalWaterBalance.SpinUpYears * 12;
            initial.previousTemperatureC = Normal(region, first - 1).temperature;
            for (int month = first; month < startMonth; month++)
            {
                var weather = Normal(region, month);
                initial.storageMm = LocalWaterBalance.Step(inputs, region.latitude, FieldCalendar.Date(month),
                    weather, initial.previousTemperatureC, initial.storageMm).storageMm;
                initial.previousTemperatureC = weather.temperature;
            }
            initial.soil = (float)(initial.storageMm / inputs.soilCapacityMm);
            return initial;
        }

        public static Weather Normal(RegionClimate region, int monthIndex)
        {
            if (region.normals == null || region.normals.Length != 12)
                throw new ArgumentException($"{region.id} requires twelve monthly normals.", nameof(region));
            var normal = region.normals[FieldCalendar.Date(monthIndex).Month - 1];
            if (!Finite(normal.temperature) || normal.temperature < -100 || normal.temperature > 65 ||
                !Finite(normal.rain) || normal.rain < 0 || !Finite(normal.wind) || normal.wind < 0 ||
                !(normal.humidity >= 0 && normal.humidity <= 100) || !(normal.soil >= 0 && normal.soil <= 1))
                throw new ArgumentException($"{region.id} has an invalid monthly normal.", nameof(region));
            return normal;
        }

        static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);

        public static Weather ApplyForcing(Weather normal, Forcing settings)
        {
            normal.temperature += settings.heat;
            normal.rain = Mathf.Max(0, normal.rain * (1 + settings.rain / 100f));
            normal.humidity = Mathf.Clamp(normal.humidity + settings.humidity, 0, 100);
            normal.wind = Mathf.Max(0, normal.wind + settings.wind);
            return normal;
        }

        public static FieldTrial Project(RegionClimate region, LandState before, int startMonth,
            Func<int, Forcing> forcing, int duration = 12)
        {
            if (duration < 1 || duration > 12) throw new ArgumentOutOfRangeException(nameof(duration));
            var months = new FieldMonth[duration];
            var state = before;
            var inputs = state.hasStorage ? LocalWaterBalance.Inputs(region) : null;
            if (state.hasStorage && inputs == null)
                throw new InvalidOperationException($"{region.id} requires water balance parameters to continue its saved state.");
            for (int i = 0; i < duration; i++)
            {
                int monthIndex = startMonth + i;
                var normal = Normal(region, monthIndex);
                var settings = forcing(monthIndex);
                var weather = ApplyForcing(normal, settings);
                WaterLedger water = default;
                float dryAir = -settings.humidity / 100f;
                float evaporation = EvaporationIndex(settings);
                float relativeSoil;
                if (inputs == null)
                {
                    // The original illustrative response uses the NASA root-zone wetness index.
                    float target = Mathf.Clamp01(normal.soil + .57f * Supply(weather, normal) - 2.35f * evaporation);
                    state.soil = Mathf.Lerp(state.soil, target, .50f);
                    relativeSoil = state.soil - normal.soil;
                    state.drought = Mathf.Clamp01(-relativeSoil * 2.8f + Mathf.Max(0, evaporation) * 1.65f);
                }
                else
                {
                    water = LocalWaterBalance.Step(inputs, region.latitude, FieldCalendar.Date(monthIndex),
                        weather, state.previousTemperatureC, state.storageMm);
                    state.storageMm = water.storageMm;
                    state.soil = (float)(state.storageMm / inputs.soilCapacityMm);
                    state.drought = water.et0Mm > 0 ? (float)(1 - water.actualEtMm / water.et0Mm) : 0;
                    relativeSoil = state.soil - .5f; // Illustrative vegetation response about a half-full bucket.
                }
                float wet = WetnessIndex(state, relativeSoil);
                float hot = Mathf.Clamp01((weather.temperature - 30) / 13) * Mathf.Clamp01(settings.heat / 5f);
                float cold = Mathf.Clamp01((8 - weather.temperature) / 12) * Mathf.Clamp01(-settings.heat / 4f);
                float vegetation = Mathf.Clamp01(.75f - .51f * state.drought - .40f * wet - .14f * hot - .14f * cold + .37f * Mathf.Max(0, relativeSoil) * (1 - wet));
                state.vegetation = Mathf.Lerp(state.vegetation, vegetation, .25f);
                state.fuel = Mathf.Lerp(state.fuel, Mathf.Clamp01(.16f + .85f * state.drought + .8f * dryAir), .42f);
                state.elapsed++;
                state.previousTemperatureC = weather.temperature;
                months[i] = new FieldMonth { monthIndex = monthIndex, land = state, weather = weather,
                    normal = normal, forcing = settings, water = water };
            }
            return new FieldTrial { before = before, months = months, findings = Findings(before, months, duration) };
        }

        static float EvaporationIndex(Forcing settings) =>
            .012f * settings.heat + .10f * (-settings.humidity / 100f) + .007f * settings.wind;

        static float Supply(Weather weather, Weather normal) => (weather.rain - normal.rain) / (normal.rain + 27);
        static float WetnessIndex(LandState land, float relativeSoil) =>
            Mathf.Clamp01(Mathf.Min(land.soil - .70f, relativeSoil - .035f) * 4);

        public static List<string> Findings(LandState before, FieldMonth[] months, int count)
        {
            int drought = 0, saturated = 0, heat = 0, humid = 0, fire = 0, cool = 0, demand = 0, surplus = 0;
            for (int i = 0; i < count; i++)
            {
                var (state, weather, normal, settings) = (months[i].land, months[i].weather, months[i].normal, months[i].forcing);
                if (state.drought > .44f) drought++;
                if (WetnessIndex(state, state.soil - (state.hasStorage ? .5f : normal.soil)) > .31f) saturated++;
                if (Mathf.Clamp01((weather.temperature - 30) / 13) * Mathf.Clamp01(settings.heat / 5f) > .36f) heat++;
                if (weather.temperature > 26 && weather.humidity > 70 && settings.heat >= 2) humid++;
                if (weather.wind > 7 && state.fuel > .53f && state.vegetation > .29f && weather.temperature > 19) fire++;
                if (Mathf.Clamp01((8 - weather.temperature) / 12) * Mathf.Clamp01(-settings.heat / 4f) > .38f) cool++;
                if (EvaporationIndex(settings) > .10f && weather.humidity < 45) demand++;
                if (Supply(weather, normal) > .48f) surplus++;
            }

            var findings = new List<string>();
            var end = months[count - 1].land;
            void Flag(bool applies, string id) { if (applies) findings.Add(id); }
            Flag(drought >= 1 && end.drought > .36f, "drought");
            Flag(end.vegetation < .51f && (before.vegetation - end.vegetation > .055f || drought + saturated >= 1), "decline");
            Flag(saturated >= 2 && end.soil > .72f, "waterlogging");
            Flag(fire >= 1, "fire");
            Flag(before.drought >= .29f && end.soil - before.soil > .14f, "recharge");
            Flag(before.vegetation < .62f && end.vegetation - before.vegetation > .09f, "recovery");
            Flag(heat >= 1, "heat");
            Flag(humid >= 1, "humidity");
            Flag(cool >= 1, "cool");
            Flag(demand >= 1, "demand");
            Flag(surplus >= 3, "surplus");
            Flag(end.vegetation > .80f && end.vegetation - before.vegetation > .035f, "greening");
            return findings;
        }
    }
}
