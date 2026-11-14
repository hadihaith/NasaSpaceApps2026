/**
 * The written what-if report, built only from the engine's numbers.
 * The page shows it as is; the optional AI briefing is given the same facts.
 */
import { ASSUMPTIONS, type Discovery, type WhatIfResult } from "./model";

const fmt = (x: number, d = 1) => {
  const s = Math.abs(x).toFixed(d);
  return x < 0 ? `−${s}` : s;
};
const signedFmt = (x: number, d = 1) => (x > 0 ? `+${fmt(x, d)}` : fmt(x, d));
/** Lower-case only the first letter, so units such as °C survive. */
const lowerFirst = (s: string) => s.charAt(0).toLowerCase() + s.slice(1);
const pct = (now: number, base: number) => (base === 0 ? 0 : ((now - base) / base) * 100);

export const DISCOVERY_INFO: Record<Discovery["id"], { title: string; blurb: string }> = {
  drought_stress: {
    title: "Soil drought",
    blurb: "The root zone holds much less water than it does today.",
  },
  wet_soil: {
    title: "Soggier soil",
    blurb: "Extra rain outpaces evaporation and the soil stays wetter.",
  },
  thirsty_air: {
    title: "Thirsty air",
    blurb: "Hotter or drier air pulls water out of soil and plants faster.",
  },
  extreme_heat: {
    title: "More 40 °C months",
    blurb: "More months reach an average daily high of 40 °C (104 °F).",
  },
  cooler_calmer: {
    title: "Gentler evaporation",
    blurb: "The air demands less water, easing stress on plants and soil.",
  },
};

export interface WhatIfReport {
  headline: string;
  findings: string[];
  caveats: string[];
}

export function describeControls(r: WhatIfResult): string {
  const c = r.controls;
  const parts: string[] = [];
  if (c.temperatureDeltaC) parts.push(`air ${signedFmt(c.temperatureDeltaC)} °C`);
  if (c.precipitationChangePct) parts.push(`rain ${signedFmt(c.precipitationChangePct, 0)}%`);
  if (c.relativeHumidityDeltaPctPoints) parts.push(`humidity ${signedFmt(c.relativeHumidityDeltaPctPoints, 0)} points`);
  if (c.windSpeedChangePct) parts.push(`wind ${signedFmt(c.windSpeedChangePct, 0)}%`);
  return parts.length ? parts.join(", ") : "no change";
}

export function buildReport(r: WhatIfResult): WhatIfReport {
  const b = r.baseline;
  const s = r.scenario;
  const years = r.elapsedMonths / 12;
  const soilChange = pct(s.soilMoistureIndex, b.soilMoistureIndex);
  const et0Change = pct(s.et0MmPerYear, b.et0MmPerYear);
  const vpdChange = pct(s.vpdKpa, b.vpdKpa);

  const headline =
    describeControls(r) === "no change"
      ? "Nothing changed, so the region stays as it is today."
      : `With ${describeControls(r)}, after ${years} year${years === 1 ? "" : "s"}: ` +
        (r.discoveries.length
          ? r.discoveries.map((d) => lowerFirst(DISCOVERY_INFO[d.id].title)).join(", ") + "."
          : "no big shift past the thresholds this model watches.");

  const findings = [
    `Rain: ${fmt(b.precipitationMmPerYear, 0)} mm a year today, ${fmt(s.precipitationMmPerYear, 0)} mm in the what-if.`,
    `Evaporative demand (reference evapotranspiration, FAO-56): ${fmt(b.et0MmPerYear, 0)} mm a year today, ` +
      `${fmt(s.et0MmPerYear, 0)} mm in the what-if (${signedFmt(et0Change, 0)}%). The air could evaporate about ` +
      `${fmt(s.et0MmPerYear / Math.max(1, s.precipitationMmPerYear), 1)} times the rain that falls.`,
    `Soil water in the final year: ${fmt(b.soilMoistureIndex * 100, 0)}% of the root zone's capacity today, ` +
      `${fmt(s.soilMoistureIndex * 100, 0)}% in the what-if (${signedFmt(soilChange, 0)}%).`,
    `Drought stress (share of the air's demand the land cannot supply): ${fmt(b.droughtStressIndex, 2)} today, ` +
      `${fmt(s.droughtStressIndex, 2)} in the what-if.`,
    `Vapour pressure deficit, how thirsty the air is: ${fmt(b.vpdKpa, 2)} kPa today, ${fmt(s.vpdKpa, 2)} kPa ` +
      `(${signedFmt(vpdChange, 0)}%).`,
    `Months with an average daily high of ${ASSUMPTIONS.heatThresholdC} °C or more at Phoenix Airport: ` +
      `${b.extremeHeatMonths} today, ${s.extremeHeatMonths} in the what-if.`,
  ];

  const caveats = [
    "Hypothetical: this is a simplified teaching model run on the region's real 1991–2020 climate, not a forecast and not an observation.",
    `Changes are applied evenly to every month. Real changes would vary by season and from year to year.`,
    `Soil water uses one ${ASSUMPTIONS.soilCapacityMm} mm bucket for the whole region; real soils, irrigation and city surfaces differ.`,
    "Temperature, rain, humidity and wind are changed independently here; in reality they are linked, so some combinations are unrealistic.",
    "The vegetation score is illustrative: it combines water supply and heat, but it is not a measured plant index.",
  ];

  return { headline, findings, caveats };
}
