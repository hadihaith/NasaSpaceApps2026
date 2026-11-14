import { describe, expect, it } from "vitest";
import { regionClimate } from "../earth/climate";
import {
  NO_CHANGE,
  extraterrestrialRadiation,
  sanitizeControls,
  sanitizeYears,
  satVapourPressure,
  simulate,
} from "./model";

const { climate } = regionClimate();

describe("physics against FAO-56 worked values", () => {
  it("saturation vapour pressure matches the FAO-56 table", () => {
    // FAO-56 Annex 2, Table 2.3: 25 °C -> 3.168 kPa, 10 °C -> 1.228 kPa
    expect(satVapourPressure(25)).toBeCloseTo(3.168, 2);
    expect(satVapourPressure(10)).toBeCloseTo(1.228, 2);
  });

  it("extraterrestrial radiation matches FAO-56 Example 8", () => {
    // 20°S on 3 September (day 246): Ra = 32.2 MJ m-2 day-1
    expect(extraterrestrialRadiation(-20, 246)).toBeCloseTo(32.2, 0);
  });
});

describe("what-if engine on the real Phoenix climate", () => {
  it("reads a complete 12-month climate", () => {
    for (const key of ["temperatureC", "precipitationMm", "relativeHumidityPct", "windSpeed10mMs", "dailyRangeC"] as const) {
      expect(climate[key]).toHaveLength(12);
      expect(climate[key].every(Number.isFinite)).toBe(true);
    }
  });

  it("gives desert-like demand and supply with no change", () => {
    const r = simulate(climate, NO_CHANGE, 3);
    // Precipitation is the MERRA-2 baseline (about 318 mm/year for 1991-2020).
    expect(r.baseline.precipitationMmPerYear).toBeGreaterThan(250);
    expect(r.baseline.precipitationMmPerYear).toBeLessThan(400);
    // Reference ET in the Sonoran Desert is several times the rainfall.
    expect(r.baseline.et0MmPerYear).toBeGreaterThan(1200);
    expect(r.baseline.et0MmPerYear).toBeLessThan(2600);
    expect(r.baseline.droughtStressIndex).toBeGreaterThan(0.5);
  });

  it("changes nothing when nothing is changed", () => {
    const r = simulate(climate, NO_CHANGE, 5);
    expect(r.scenario).toEqual(r.baseline);
    expect(r.discoveries).toEqual([]);
    expect(r.elapsedMonths).toBe(60);
  });

  it("dries the land and the air when it is hotter and drier", () => {
    const r = simulate(
      climate,
      { temperatureDeltaC: 3, precipitationChangePct: -40, relativeHumidityDeltaPctPoints: -5, windSpeedChangePct: 10 },
      5,
    );
    expect(r.scenario.et0MmPerYear).toBeGreaterThan(r.baseline.et0MmPerYear);
    expect(r.scenario.soilMoistureIndex).toBeLessThan(r.baseline.soilMoistureIndex);
    expect(r.scenario.droughtStressIndex).toBeGreaterThan(r.baseline.droughtStressIndex);
    expect(r.scenario.vpdKpa).toBeGreaterThan(r.baseline.vpdKpa);
    const ids = r.discoveries.map((d) => d.id);
    expect(ids).toContain("drought_stress");
    expect(ids).toContain("thirsty_air");
    expect(ids).toContain("extreme_heat");
  });

  it("wets the soil when it rains much more", () => {
    const r = simulate(climate, { ...NO_CHANGE, precipitationChangePct: 80 }, 3);
    expect(r.scenario.soilMoistureIndex).toBeGreaterThan(r.baseline.soilMoistureIndex);
    expect(r.discoveries.map((d) => d.id)).toContain("wet_soil");
  });

  it("raises demand with wind and lowers it with humidity", () => {
    const windy = simulate(climate, { ...NO_CHANGE, windSpeedChangePct: 50 }, 1);
    const humid = simulate(climate, { ...NO_CHANGE, relativeHumidityDeltaPctPoints: 20 }, 1);
    expect(windy.scenario.et0MmPerYear).toBeGreaterThan(windy.baseline.et0MmPerYear);
    expect(humid.scenario.et0MmPerYear).toBeLessThan(humid.baseline.et0MmPerYear);
  });

  it("keeps every index between 0 and 1", () => {
    for (const c of [
      { temperatureDeltaC: 6, precipitationChangePct: -80, relativeHumidityDeltaPctPoints: -20, windSpeedChangePct: 50 },
      { temperatureDeltaC: -3, precipitationChangePct: 80, relativeHumidityDeltaPctPoints: 20, windSpeedChangePct: -50 },
    ]) {
      const { environment } = simulate(climate, c, 10);
      for (const v of Object.values(environment)) {
        expect(v).toBeGreaterThanOrEqual(0);
        expect(v).toBeLessThanOrEqual(1);
      }
    }
  });
});

describe("input sanitising", () => {
  it("clamps and snaps controls to the slider grid", () => {
    expect(sanitizeControls({ temperatureDeltaC: 99, precipitationChangePct: -7, windSpeedChangePct: "x" })).toEqual({
      temperatureDeltaC: 6,
      precipitationChangePct: -5,
      relativeHumidityDeltaPctPoints: 0,
      windSpeedChangePct: 0,
    });
  });

  it("clamps years", () => {
    expect(sanitizeYears(0)).toBe(1);
    expect(sanitizeYears(50)).toBe(10);
    expect(sanitizeYears("abc")).toBe(3);
  });
});
