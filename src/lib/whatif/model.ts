/**
 * What-if engine: a small, transparent water-and-heat model for one region.
 *
 * It starts from the region's REAL monthly climate (1991-2020 baselines built from
 * NASA MERRA-2 and the NOAA Phoenix Airport station, see docs/data-sources.md),
 * applies the player's changes, and computes what follows from well-known physics:
 *
 *  - Evaporative demand: FAO-56 Penman-Monteith reference evapotranspiration (ET0),
 *    monthly form, with solar radiation estimated from the day/night temperature range
 *    (Hargreaves radiation formula). Temperature, humidity and wind all act through it.
 *  - Thirst of the air: vapour pressure deficit (VPD) from temperature and humidity.
 *  - Soil water: a single-layer monthly "bucket" that rain fills and evaporation empties.
 *  - Heat: months whose average daily high at the airport reaches 40 °C (104 °F).
 *
 * Everything here is HYPOTHETICAL. It is a teaching model with stated assumptions,
 * not a forecast, and it must never be shown as an observation.
 *
 * Input and output field names follow the team's Contract A (docs: player experiment ->
 * simulation result), so the globe and the game shell can consume the result directly.
 */

export interface Controls {
  /** Added to every month's mean air temperature, °C */
  temperatureDeltaC: number;
  /** Percent change applied to every month's precipitation */
  precipitationChangePct: number;
  /** Percentage points added to every month's relative humidity */
  relativeHumidityDeltaPctPoints: number;
  /** Percent change applied to every month's wind speed */
  windSpeedChangePct: number;
}

export const NO_CHANGE: Controls = {
  temperatureDeltaC: 0,
  precipitationChangePct: 0,
  relativeHumidityDeltaPctPoints: 0,
  windSpeedChangePct: 0,
};

/** Slider limits. The API route enforces the same limits. */
export const LIMITS: Record<keyof Controls, { min: number; max: number; step: number }> = {
  temperatureDeltaC: { min: -3, max: 6, step: 0.5 },
  precipitationChangePct: { min: -80, max: 80, step: 5 },
  relativeHumidityDeltaPctPoints: { min: -20, max: 20, step: 1 },
  windSpeedChangePct: { min: -50, max: 50, step: 5 },
};

export const YEAR_LIMITS = { min: 1, max: 10 };

/** Twelve monthly values, January first. */
export type Monthly = number[];

/** The real monthly climate the model starts from. */
export interface Climate {
  regionId: string;
  latitudeDeg: number;
  elevationM: number;
  /** Area-mean air temperature at 2 m, °C (MERRA-2) */
  temperatureC: Monthly;
  /** Area-mean precipitation, mm/month (MERRA-2) */
  precipitationMm: Monthly;
  /** Area-mean relative humidity at 2 m, % (MERRA-2) */
  relativeHumidityPct: Monthly;
  /** Area-mean wind speed at 10 m, m/s (MERRA-2) */
  windSpeed10mMs: Monthly;
  /** Day-night temperature range, °C (station average daily high minus low) */
  dailyRangeC: Monthly;
  /** Average daily high at the station, °C (NOAA GSOM TMAX) */
  stationHighC: Monthly;
}

/** Assumptions, stated once and shown in the report. */
export const ASSUMPTIONS = {
  soilCapacityMm: 150,
  heatThresholdC: 40,
  albedo: 0.23,
  hargreavesKRs: 0.16,
  spinUpYears: 20,
};

export interface MonthState {
  /** Months since the change began, 1-based */
  step: number;
  month: number;
  temperatureC: number;
  precipitationMm: number;
  et0Mm: number;
  actualEtMm: number;
  soilWaterMm: number;
  vpdKpa: number;
}

export interface Indicators {
  /** Mean soil water as a share of the bucket, 0-1, final year */
  soilMoistureIndex: number;
  /** 1 - actual/potential evaporation, 0-1, final year (1 = plants get almost none of the water the air demands) */
  droughtStressIndex: number;
  /** Illustrative 0-1 score combining water supply and heat; not a vegetation measurement */
  vegetationHealthIndex: number;
  /** Mean vapour pressure deficit, kPa, final year */
  vpdKpa: number;
  /** Potential evaporation (ET0), mm/year, final year */
  et0MmPerYear: number;
  /** Precipitation, mm/year */
  precipitationMmPerYear: number;
  /** Months per year with an average daily high of 40 °C or more at the station */
  extremeHeatMonths: number;
  /** Mean air temperature, °C */
  meanTemperatureC: number;
}

export type Severity = "low" | "medium" | "high";

export interface Discovery {
  id: "drought_stress" | "thirsty_air" | "extreme_heat" | "wet_soil" | "cooler_calmer";
  severity: Severity;
}

export interface WhatIfResult {
  regionId: string;
  elapsedMonths: number;
  controls: Controls;
  /** Contract A environment block, 0-1 indices for the globe */
  environment: {
    soilMoistureIndex: number;
    vegetationHealthIndex: number;
    droughtStressIndex: number;
  };
  discoveries: Discovery[];
  baseline: Indicators;
  scenario: Indicators;
  /** Month-by-month path of both runs */
  baselineMonths: MonthState[];
  scenarioMonths: MonthState[];
  /** The 12-month climate the scenario runs on */
  scenarioClimate: { temperatureC: Monthly; precipitationMm: Monthly; relativeHumidityPct: Monthly; windSpeed10mMs: Monthly };
}

// ---------------------------------------------------------------- physics

const DAYS = [31, 28.25, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
const MID_DOY = [15, 46, 74, 105, 135, 166, 196, 227, 258, 288, 319, 349];

/** Saturation vapour pressure, kPa (FAO-56 eq. 11). */
export function satVapourPressure(tC: number): number {
  return 0.6108 * Math.exp((17.27 * tC) / (tC + 237.3));
}

/** Extraterrestrial radiation, MJ m-2 day-1 (FAO-56 eqs. 21-25). */
export function extraterrestrialRadiation(latDeg: number, dayOfYear: number): number {
  const phi = (latDeg * Math.PI) / 180;
  const dr = 1 + 0.033 * Math.cos((2 * Math.PI * dayOfYear) / 365);
  const delta = 0.409 * Math.sin((2 * Math.PI * dayOfYear) / 365 - 1.39);
  const ws = Math.acos(Math.max(-1, Math.min(1, -Math.tan(phi) * Math.tan(delta))));
  return ((24 * 60) / Math.PI) * 0.082 * dr *
    (ws * Math.sin(phi) * Math.sin(delta) + Math.cos(phi) * Math.cos(delta) * Math.sin(ws));
}

/**
 * FAO-56 Penman-Monteith reference evapotranspiration for one month, mm/day.
 * Radiation from the Hargreaves formula Rs = kRs * sqrt(Tmax - Tmin) * Ra.
 */
export function referenceEt(args: {
  meanC: number;
  rangeC: number;
  prevMeanC: number;
  rhPct: number;
  wind10mMs: number;
  latDeg: number;
  elevationM: number;
  dayOfYear: number;
}): number {
  const { meanC, rangeC, prevMeanC, rhPct, wind10mMs, latDeg, elevationM, dayOfYear } = args;
  const tmax = meanC + rangeC / 2;
  const tmin = meanC - rangeC / 2;
  const es = (satVapourPressure(tmax) + satVapourPressure(tmin)) / 2;
  const ea = (Math.min(100, Math.max(1, rhPct)) / 100) * es;
  const slope = (4098 * satVapourPressure(meanC)) / (meanC + 237.3) ** 2;
  const pressure = 101.3 * ((293 - 0.0065 * elevationM) / 293) ** 5.26;
  const gamma = 0.000665 * pressure;
  const u2 = Math.max(0.5, wind10mMs * (4.87 / Math.log(67.8 * 10 - 5.42)));

  const ra = extraterrestrialRadiation(latDeg, dayOfYear);
  const rs = ASSUMPTIONS.hargreavesKRs * Math.sqrt(Math.max(0, rangeC)) * ra;
  const rso = (0.75 + 2e-5 * elevationM) * ra;
  const rns = (1 - ASSUMPTIONS.albedo) * rs;
  const sigma = 4.903e-9;
  const rnl =
    sigma * (((tmax + 273.16) ** 4 + (tmin + 273.16) ** 4) / 2) *
    (0.34 - 0.14 * Math.sqrt(ea)) *
    (1.35 * Math.min(1, rs / rso) - 0.35);
  const rn = rns - rnl;
  const g = 0.14 * (meanC - prevMeanC);

  const et0 =
    (0.408 * slope * (rn - g) + gamma * (900 / (meanC + 273)) * u2 * (es - ea)) /
    (slope + gamma * (1 + 0.34 * u2));
  return Math.max(0, et0);
}

// ---------------------------------------------------------------- the run

function perturb(climate: Climate, c: Controls) {
  return {
    temperatureC: climate.temperatureC.map((t) => t + c.temperatureDeltaC),
    precipitationMm: climate.precipitationMm.map((p) => Math.max(0, p * (1 + c.precipitationChangePct / 100))),
    relativeHumidityPct: climate.relativeHumidityPct.map((rh) =>
      Math.min(100, Math.max(1, rh + c.relativeHumidityDeltaPctPoints)),
    ),
    windSpeed10mMs: climate.windSpeed10mMs.map((w) => Math.max(0, w * (1 + c.windSpeedChangePct / 100))),
  };
}

function monthlyEt0(climate: Climate, forced: ReturnType<typeof perturb>): number[] {
  return forced.temperatureC.map((t, m) =>
    referenceEt({
      meanC: t,
      rangeC: climate.dailyRangeC[m],
      prevMeanC: forced.temperatureC[(m + 11) % 12],
      rhPct: forced.relativeHumidityPct[m],
      wind10mMs: forced.windSpeed10mMs[m],
      latDeg: climate.latitudeDeg,
      elevationM: climate.elevationM,
      dayOfYear: MID_DOY[m],
    }) * DAYS[m],
  );
}

/** One month of the bucket: rain in, evaporation out at a rate that falls as the soil dries. */
function bucketStep(soil: number, rain: number, et0: number) {
  const cap = ASSUMPTIONS.soilCapacityMm;
  const available = soil + rain;
  const actual = Math.min(available, et0 * Math.min(1, available / cap));
  return { soil: Math.min(cap, available - actual), actual };
}

function run(climate: Climate, forced: ReturnType<typeof perturb>, et0: number[], startSoil: number, months: number) {
  const out: MonthState[] = [];
  let soil = startSoil;
  for (let i = 0; i < months; i++) {
    const m = i % 12;
    const step = bucketStep(soil, forced.precipitationMm[m], et0[m]);
    soil = step.soil;
    const t = forced.temperatureC[m];
    const range = climate.dailyRangeC[m];
    const es = (satVapourPressure(t + range / 2) + satVapourPressure(t - range / 2)) / 2;
    out.push({
      step: i + 1,
      month: m + 1,
      temperatureC: t,
      precipitationMm: forced.precipitationMm[m],
      et0Mm: et0[m],
      actualEtMm: step.actual,
      soilWaterMm: soil,
      vpdKpa: es * (1 - forced.relativeHumidityPct[m] / 100),
    });
  }
  return out;
}

function indicators(months: MonthState[], climate: Climate, c: Controls): Indicators {
  const last = months.slice(-12);
  const mean = (xs: number[]) => xs.reduce((a, b) => a + b, 0) / xs.length;
  const et0 = last.reduce((a, s) => a + s.et0Mm, 0);
  const aet = last.reduce((a, s) => a + s.actualEtMm, 0);
  const soilMoistureIndex = mean(last.map((s) => s.soilWaterMm)) / ASSUMPTIONS.soilCapacityMm;
  const droughtStressIndex = et0 > 0 ? 1 - aet / et0 : 0;
  const extremeHeatMonths = climate.stationHighC.filter((h) => h + c.temperatureDeltaC >= ASSUMPTIONS.heatThresholdC).length;
  // Illustrative only: water supply, reduced when more months pass the heat threshold.
  const vegetationHealthIndex = Math.max(0, Math.min(1, (1 - droughtStressIndex) * (1 - 0.5 * (extremeHeatMonths / 12))));
  return {
    soilMoistureIndex,
    droughtStressIndex,
    vegetationHealthIndex,
    vpdKpa: mean(last.map((s) => s.vpdKpa)),
    et0MmPerYear: et0,
    precipitationMmPerYear: last.reduce((a, s) => a + s.precipitationMm, 0),
    extremeHeatMonths,
    meanTemperatureC: mean(last.map((s) => s.temperatureC)),
  };
}

function discover(base: Indicators, scen: Indicators): Discovery[] {
  const found: Discovery[] = [];
  const soilRatio = base.soilMoistureIndex > 0 ? scen.soilMoistureIndex / base.soilMoistureIndex : 1;
  if (soilRatio <= 0.8) found.push({ id: "drought_stress", severity: soilRatio <= 0.6 ? "high" : "medium" });
  if (soilRatio >= 1.25) found.push({ id: "wet_soil", severity: soilRatio >= 1.5 ? "high" : "medium" });
  const vpdRatio = scen.vpdKpa / base.vpdKpa;
  if (vpdRatio >= 1.15) found.push({ id: "thirsty_air", severity: vpdRatio >= 1.3 ? "high" : "medium" });
  const heat = scen.extremeHeatMonths - base.extremeHeatMonths;
  if (heat >= 1) found.push({ id: "extreme_heat", severity: heat >= 2 ? "high" : "medium" });
  if (scen.et0MmPerYear <= base.et0MmPerYear * 0.9) found.push({ id: "cooler_calmer", severity: "low" });
  return found;
}

/** Run the baseline and the what-if side by side for `years` years. */
export function simulate(climate: Climate, controls: Controls, years: number): WhatIfResult {
  const months = Math.round(Math.min(YEAR_LIMITS.max, Math.max(YEAR_LIMITS.min, years)) * 12);
  const baseForced = perturb(climate, NO_CHANGE);
  const baseEt0 = monthlyEt0(climate, baseForced);

  // Spin the bucket up on today's climate so both runs start from the same, settled soil.
  const spin = run(climate, baseForced, baseEt0, ASSUMPTIONS.soilCapacityMm / 2, ASSUMPTIONS.spinUpYears * 12);
  const startSoil = spin[spin.length - 1].soilWaterMm;

  const scenForced = perturb(climate, controls);
  const scenEt0 = monthlyEt0(climate, scenForced);
  const baselineMonths = run(climate, baseForced, baseEt0, startSoil, months);
  const scenarioMonths = run(climate, scenForced, scenEt0, startSoil, months);

  const baseline = indicators(baselineMonths, climate, NO_CHANGE);
  const scenario = indicators(scenarioMonths, climate, controls);

  return {
    regionId: climate.regionId,
    elapsedMonths: months,
    controls,
    environment: {
      soilMoistureIndex: scenario.soilMoistureIndex,
      vegetationHealthIndex: scenario.vegetationHealthIndex,
      droughtStressIndex: scenario.droughtStressIndex,
    },
    discoveries: discover(baseline, scenario),
    baseline,
    scenario,
    baselineMonths,
    scenarioMonths,
    scenarioClimate: scenForced,
  };
}

/** Clamp and round untrusted input to the slider grid. */
export function sanitizeControls(input: Partial<Record<keyof Controls, unknown>>): Controls {
  const out = { ...NO_CHANGE };
  for (const key of Object.keys(LIMITS) as (keyof Controls)[]) {
    const raw = Number(input[key]);
    if (!Number.isFinite(raw)) continue;
    const { min, max, step } = LIMITS[key];
    out[key] = Math.min(max, Math.max(min, Math.round(raw / step) * step));
  }
  return out;
}

export function sanitizeYears(input: unknown): number {
  const raw = Math.round(Number(input));
  return Number.isFinite(raw) ? Math.min(YEAR_LIMITS.max, Math.max(YEAR_LIMITS.min, raw)) : 3;
}
