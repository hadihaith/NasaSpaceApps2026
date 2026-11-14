export const CONTROLS = [
  { key: "heat", label: "Temperature", unit: "°C", min: -5, max: 8, step: 1, color: "#ffad75" },
  { key: "rain", label: "Precipitation", unit: "%", min: -100, max: 150, step: 5, color: "#80d7ff" },
  { key: "humidity", label: "Humidity", unit: " pp", min: -40, max: 40, step: 2, color: "#d3adff" },
  { key: "wind", label: "Wind speed", unit: " m/s", min: -5, max: 15, step: 1, color: "#a9f2c3" },
] as const;

export type Control = typeof CONTROLS[number]["key"];
export type Settings = Record<Control, number>;
export const DEFAULT_SETTINGS: Settings = { heat: 0, rain: 0, humidity: 0, wind: 0 };
export const TIMES = [{ label: "Month", months: 1 }, { label: "Season", months: 3 }, { label: "Year", months: 12 }] as const;
export type Duration = typeof TIMES[number]["months"];
export type Climate = Record<"temperature" | "rain" | "humidity" | "wind" | "soil", number>;
export type WeatherKey = keyof Climate;
export interface RegionalClimate {
  source: string;
  baseline: {
    startYear: number; endYear: number; monthly: Climate[];
    counts: Record<WeatherKey, number[]>;
    seriesIds: Record<WeatherKey, string>;
    units: Record<WeatherKey, string>;
  };
}
export type ClimateCatalog = Record<string, RegionalClimate>;
export type Normals = Record<string, Climate[]>;

const clamp = (v: number, low = 0, high = 1) => Math.min(high, Math.max(low, v));
export const WEATHER_KEYS: WeatherKey[] = ["temperature", "rain", "humidity", "wind", "soil"];

export interface Environment {
  soilMoistureIndex: number;
  vegetationHealthIndex: number;
  droughtStressIndex: number;
  fuelDryness: number;
}
export interface RegionState extends Environment { elapsed: number }
export interface Discovery {
  id: string;
  label: string;
  icon: string;
  quote: string;
  science: string;
  evidence: WeatherKey;
  linked: string[];
}

export const DISCOVERIES: Discovery[] = [
  { id: "drought", label: "Drought stress", icon: "☀", quote: "THE WATER IS LEAVING! WHY CAN’T THE HUMANS TAKE A HINT?", science: "When water losses outpace recharge, root-zone water becomes harder for plants to access. The soil index is a simplified indicator of water available to roots.", evidence: "soil", linked: ["decline", "fire", "recharge"] },
  { id: "decline", label: "Vegetation decline", icon: "♧", quote: "The plants have collectively decided to quit.", science: "Prolonged water stress or waterlogging can reduce plant growth. The fictional vegetation index responds gradually and has no crop- or species-specific calibration.", evidence: "rain", linked: ["drought", "waterlogging", "recovery"] },
  { id: "waterlogging", label: "Waterlogging", icon: "◒", quote: "We’ve successfully invented a very deep lawn.", science: "Persistently wet soil can limit oxygen around roots. Soil type and drainage matter, and this model uses a simplified root-zone water balance.", evidence: "soil", linked: ["decline"] },
  { id: "fire", label: "Elevated fire-weather potential", icon: "△", quote: "I am told that ‘weather potential’ does not mean ‘permission.’", science: "Dry fuels, warm conditions and strong winds can favor fire spread where fuel is present. This index estimates conditions. It does not simulate ignition, fire spread or risk to people.", evidence: "wind", linked: ["drought"] },
  { id: "recharge", label: "Soil water recharge", icon: "↓", quote: "The dirt has refilled itself. No one signed for the delivery.", science: "When precipitation replenishes soil water after a dry period, roots may have access to more water. The model responds faster in soil than in vegetation.", evidence: "soil", linked: ["drought", "recovery"] },
  { id: "recovery", label: "Vegetation recovery", icon: "✿", quote: "WHO AUTHORIZED THE TREES TO GROW BACK?", science: "When growing conditions improve, vegetation can recover, often with a delay. Real recovery depends on species, the severity of damage and other pressures.", evidence: "rain", linked: ["decline", "recharge"] },
  { id: "heat", label: "Heat stress", icon: "↟", quote: "Earth now comes in ‘very, very preheated.’", science: "Warmer air can increase demand for water and push organisms beyond their usual conditions. This flag is relative to the region’s normal climate and is not a health forecast.", evidence: "temperature", linked: ["drought", "fire"] },
  { id: "humidity", label: "Humid heat", icon: "≈", quote: "It’s like being hugged by a suspiciously warm towel.", science: "Humid air makes evaporation less effective when it is already hot. The combination flag is illustrative and does not estimate a heat index or wet-bulb temperature.", evidence: "humidity", linked: ["heat"] },
  { id: "cool", label: "Cool-season slowdown", icon: "✦", quote: "The ecosystem has requested another blanket.", science: "Cooler-than-usual conditions can slow growth. The response depends on what is growing and when. The vegetation model uses a simple seasonal threshold.", evidence: "temperature", linked: ["decline"] },
  { id: "demand", label: "High evaporative demand", icon: "≋", quote: "Humidity has left the meeting without taking minutes.", science: "Warm, dry air and moving air can increase the rate at which surfaces lose water when it is available. Wind and humidity here influence a simplified water-balance term.", evidence: "humidity", linked: ["drought", "fire"] },
  { id: "surplus", label: "Persistent rainfall surplus", icon: "☂", quote: "The rain has requested a permanent desk.", science: "Several wetter-than-normal months can shift soil and plant conditions. The flag tracks sustained input and does not predict a flood.", evidence: "rain", linked: ["waterlogging", "recharge"] },
  { id: "greening", label: "Growth pulse", icon: "✳", quote: "The plants are thriving under our very hostile management.", science: "When extra water arrives under suitable temperatures, vegetation can grow more than usual. A modeled growth pulse need not match a real satellite vegetation record.", evidence: "rain", linked: ["recharge", "recovery"] },
];
export const DISCOVERY_BY_ID = Object.fromEntries(DISCOVERIES.map((entry) => [entry.id, entry]));

export function toNormals(record: RegionalClimate): Climate[] {
  const b = record.baseline;
  if (!b || b.startYear !== 1991 || b.endYear !== 2020 || b.monthly.length !== 12 || b.units.rain !== "mm/month" ||
      WEATHER_KEYS.some((key) => b.counts[key]?.length !== 12 || b.counts[key].some((n) => n < 20)) ||
      b.monthly.some((month) => WEATHER_KEYS.some((key) => !Number.isFinite(month[key])))) {
    throw new Error("Expected documented 1991–2020 monthly normals and precipitation in mm/month.");
  }
  return b.monthly;
}

export function initialState(normals: Climate[]): RegionState {
  return { elapsed: 0, soilMoistureIndex: normals[0].soil, vegetationHealthIndex: .74, droughtStressIndex: 0, fuelDryness: .2 };
}

export interface Trial {
  number: number;
  region: string;
  duration: Duration;
  settings: Settings;
  before: RegionState;
  after: RegionState;
  weather: Climate;
  active: string[];
  discovered: string[];
  outcome: "discovery" | "change" | "quiet";
}
export interface Investigation {
  evidenceVersion?: 1;
  datasetId?: string;
  productVersion?: string;
  mode?: "annual" | "anomaly" | "month";
  month?: number;
  providerKind?: "station" | "satellite" | "reanalysis";
  inferenceStatus?: "not-established";
  region: string;
  dataset: string;
  variable: string;
  period: string;
  verdict: string;
  slope: string;
  test: string;
}
export interface Save {
  version: 1;
  name: string;
  guided: boolean;
  region: string;
  conditions: Settings;
  duration: Duration;
  regions: Record<string, RegionState>;
  trials: Trial[];
  discoveries: Record<string, Trial>;
  investigations: Investigation[];
  ending: boolean;
}
export const newSave = (name: string): Save => ({
  version: 1, name, guided: false, region: "phoenix", conditions: DEFAULT_SETTINGS, duration: 3, regions: {},
  trials: [], discoveries: {}, investigations: [], ending: false,
});
export const REQUIRED_DISCOVERIES = 3;

export function runTrial(save: Save, normals: Climate[], duration: Duration): Trial {
  const settings = { ...save.conditions };
  const before = save.regions[save.region] ?? initialState(normals);
  const after = { ...before };
  const months = { heat: 0, drought: 0, saturated: 0, fire: 0, humid: 0, cool: 0, demand: 0, surplus: 0 };
  let weather = normals[before.elapsed % 12];
  for (let i = 0; i < duration; i++) {
    const base = normals[(before.elapsed + i) % 12];
    weather = {
      temperature: base.temperature + settings.heat,
      rain: Math.max(0, base.rain * (1 + settings.rain / 100)),
      humidity: clamp(base.humidity + settings.humidity, 0, 100),
      wind: Math.max(0, base.wind + settings.wind),
      soil: base.soil,
    };
    const dryAir = -settings.humidity / 100;
    const evaporation = .012 * settings.heat + .10 * dryAir + .007 * settings.wind;
    const waterSupply = (weather.rain - base.rain) / (base.rain + 27);
    const soilTarget = clamp(base.soil + .57 * waterSupply - 2.35 * evaporation);
    after.soilMoistureIndex += .50 * (soilTarget - after.soilMoistureIndex);
    const relativeSoil = after.soilMoistureIndex - base.soil;
    const dry = clamp(-relativeSoil * 2.8 + Math.max(0, evaporation) * 1.65);
    const wet = clamp(Math.min(after.soilMoistureIndex - .70, relativeSoil - .035) * 4);
    const heatStress = clamp((weather.temperature - 30) / 13) * clamp(settings.heat / 5);
    const coldStress = clamp((8 - weather.temperature) / 12) * clamp(-settings.heat / 4);
    const vegetationTarget = clamp(.75 - .51 * dry - .40 * wet - .14 * heatStress - .14 * coldStress + .37 * Math.max(0, relativeSoil) * (1 - wet));
    after.vegetationHealthIndex += .25 * (vegetationTarget - after.vegetationHealthIndex);
    after.droughtStressIndex = dry;
    after.fuelDryness += .42 * (clamp(.16 + .85 * dry + .8 * dryAir) - after.fuelDryness);
    after.elapsed++;
    if (dry > .44) months.drought++;
    if (wet > .31) months.saturated++;
    if (heatStress > .36) months.heat++;
    if (weather.temperature > 26 && weather.humidity > 70 && settings.heat >= 2) months.humid++;
    // Recently dried vegetation can provide fuel. A largely bare region cannot.
    if (weather.wind > 7 && after.fuelDryness > .53 && after.vegetationHealthIndex > .29 && weather.temperature > 19) months.fire++;
    if (coldStress > .38) months.cool++;
    if (evaporation > .10 && weather.humidity < 45) months.demand++;
    if (waterSupply > .48) months.surplus++;
  }
  const active = [
    months.drought >= 1 && after.droughtStressIndex > .36 && "drought",
    after.vegetationHealthIndex < .51 && (before.vegetationHealthIndex - after.vegetationHealthIndex > .055 || months.drought + months.saturated >= 1) && "decline",
    months.saturated >= 2 && after.soilMoistureIndex > .72 && "waterlogging",
    months.fire >= 1 && "fire",
    before.droughtStressIndex >= .29 && after.soilMoistureIndex - before.soilMoistureIndex > .14 && "recharge",
    before.vegetationHealthIndex < .62 && after.vegetationHealthIndex - before.vegetationHealthIndex > .09 && "recovery",
    months.heat >= 1 && "heat",
    months.humid >= 1 && "humidity",
    months.cool >= 1 && "cool",
    months.demand >= 1 && "demand",
    months.surplus >= 3 && "surplus",
    after.vegetationHealthIndex > .80 && after.vegetationHealthIndex - before.vegetationHealthIndex > .035 && "greening",
  ].filter((id): id is string => Boolean(id));
  const discovered = active.filter((id) => !(id in save.discoveries));
  const previous = save.trials.findLast((trial) => trial.region === save.region);
  const changed = active.length > 0 || Boolean(previous?.active.length) ||
    Math.abs(after.vegetationHealthIndex - before.vegetationHealthIndex) >= .12 ||
    Math.abs(after.soilMoistureIndex - before.soilMoistureIndex) >= .18;
  return { number: (save.trials.at(-1)?.number ?? 0) + 1, region: save.region, duration, settings, before, after, weather,
    active, discovered, outcome: discovered.length ? "discovery" : changed ? "change" : "quiet" };
}

export function recordTrial(save: Save, trial: Trial): Save {
  return { ...save, guided: true, regions: { ...save.regions, [trial.region]: trial.after }, trials: [...save.trials.slice(-99), trial],
    discoveries: { ...save.discoveries, ...Object.fromEntries(trial.discovered.map((id) => [id, trial])) } };
}

export function settingLabel(key: Control, value: number) {
  return `${value > 0 ? "+" : ""}${value}${CONTROLS.find((control) => control.key === key)?.unit}`;
}
