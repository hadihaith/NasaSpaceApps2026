/**
 * The real monthly climate of region_01 (Phoenix and central Arizona), read from
 * public/data/baselines.json, which scripts/process_data.py builds from NASA MERRA-2
 * and NOAA station records. 1991-2020 means. See docs/data-sources.md.
 */
import baselines from "../../../public/data/baselines.json";
import type { Climate, Monthly } from "../whatif/model";

const REGION = "region_01";
const STATION = "USW00023183";

function monthly(seriesId: string): Monthly {
  const entry = baselines.baselines.find((b) => b.seriesId === seriesId);
  if (!entry) throw new Error(`Baseline ${seriesId} is missing from baselines.json`);
  const values = entry.monthly.map((m) => m.mean);
  if (values.length !== 12 || values.some((v) => v === null || !Number.isFinite(v))) {
    throw new Error(`Baseline ${seriesId} does not have 12 monthly means`);
  }
  return values as number[];
}

export interface ClimateSource {
  label: string;
  seriesId: string;
  period: string;
}

export function regionClimate(): { climate: Climate; sources: ClimateSource[] } {
  const highs = monthly(`${REGION}.station.${STATION}.air_temperature_daily_max_mean`);
  const lows = monthly(`${REGION}.station.${STATION}.air_temperature_daily_min_mean`);
  const ids = {
    temperature: `${REGION}.merra2.air_temperature_2m`,
    precipitation: `${REGION}.merra2.precipitation`,
    humidity: `${REGION}.merra2.relative_humidity_2m`,
    wind: `${REGION}.merra2.wind_speed_10m`,
  };
  const climate: Climate = {
    regionId: REGION,
    latitudeDeg: 33.5,
    // Rough middle of the box: low desert near 340 m, mountains toward 2,000 m.
    elevationM: 600,
    temperatureC: monthly(ids.temperature),
    precipitationMm: monthly(ids.precipitation),
    relativeHumidityPct: monthly(ids.humidity),
    windSpeed10mMs: monthly(ids.wind),
    dailyRangeC: highs.map((h, i) => h - lows[i]),
    stationHighC: highs,
  };
  const period = (id: string) => {
    const p = baselines.baselines.find((b) => b.seriesId === id)?.period;
    return p ? `${p.start}-${p.end}` : "";
  };
  const sources: ClimateSource[] = [
    { label: "Air temperature (NASA MERRA-2, region mean)", seriesId: ids.temperature, period: period(ids.temperature) },
    { label: "Precipitation (NASA MERRA-2, region mean)", seriesId: ids.precipitation, period: period(ids.precipitation) },
    { label: "Relative humidity (NASA MERRA-2, region mean)", seriesId: ids.humidity, period: period(ids.humidity) },
    { label: "Wind speed at 10 m (NASA MERRA-2, region mean)", seriesId: ids.wind, period: period(ids.wind) },
    {
      label: "Daily high and low (NOAA GSOM, Phoenix Airport)",
      seriesId: `${REGION}.station.${STATION}.air_temperature_daily_max_mean`,
      period: period(`${REGION}.station.${STATION}.air_temperature_daily_max_mean`),
    },
  ];
  return { climate, sources };
}
