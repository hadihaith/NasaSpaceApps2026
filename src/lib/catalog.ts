/**
 * The variables a detective can investigate.
 *
 * Every id is a NASA POWER parameter name, checked against the live monthly API.
 * To add a variable, add an entry here: the picker, the API route and the
 * analysis pick it up automatically.
 */

export type ThemeId = "heat" | "water" | "air" | "sky";

export interface Theme {
  id: ThemeId;
  label: string;
  question: string;
}

export const THEMES: Theme[] = [
  { id: "heat", label: "Heat", question: "Is it getting warmer or cooler?" },
  { id: "water", label: "Water", question: "Is it getting wetter or drier?" },
  { id: "air", label: "Air", question: "Is the air itself changing?" },
  { id: "sky", label: "Sun and sky", question: "Is more or less sunlight reaching the ground?" },
];

export interface Variable {
  /** NASA POWER parameter id */
  id: string;
  name: string;
  theme: ThemeId;
  /** Empty for quantities without a unit; see unitNote */
  unit: string;
  /** Explains the scale when there is no unit */
  unitNote?: string;
  /** One or two plain sentences: what is being measured */
  definition: string;
  /** Why a detective would look for a trend in it */
  why: string;
  /** Where the numbers come from */
  origin: string;
  startYear: number;
  /** A known weakness that affects trend analysis */
  caveat?: string;
}

const MERRA2 = "MERRA-2 reanalysis";
const RADIATION = "GEWEX SRB to 2000, CERES SYN1deg from 2001";
const SPLICE_CAVEAT =
  "This record joins two satellite datasets at the start of 2001. A step at that join can look like a trend, so compare 2001 onward on its own before trusting it.";

export const VARIABLES: Variable[] = [
  {
    id: "T2M",
    name: "Air temperature",
    theme: "heat",
    unit: "°C",
    definition:
      "The average temperature of the air two metres above the ground, the height of a weather station thermometer.",
    why: "The most direct sign of a warming or cooling climate.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "T2M_MAX",
    name: "Hottest air temperature",
    theme: "heat",
    unit: "°C",
    definition: "The highest air temperature reached two metres above the ground during the period.",
    why: "Shows whether heat extremes are shifting, which averages can hide.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "T2M_MIN",
    name: "Coldest air temperature",
    theme: "heat",
    unit: "°C",
    definition: "The lowest air temperature reached two metres above the ground during the period.",
    why: "Cold extremes, usually at night, often warm faster than daytime highs.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "TS",
    name: "Surface temperature",
    theme: "heat",
    unit: "°C",
    definition:
      "The temperature of the surface itself: soil, rooftops, ice or the top of the sea. Often called skin temperature.",
    why: "Responds quickly when land dries out, cities grow or ice retreats.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "PRECTOTCORR",
    name: "Rain and snowfall",
    theme: "water",
    unit: "mm/day",
    definition:
      "The average amount of water falling as rain or snow each day, corrected against rain gauges.",
    why: "Shifts in rainfall drive droughts, floods and harvests.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "GWETTOP",
    name: "Surface soil wetness",
    theme: "water",
    unit: "",
    unitNote: "0 is dry, 1 is soaked",
    definition:
      "How wet the top 5 cm of soil is, from 0 (completely dry) to 1 (completely soaked).",
    why: "A fast-reacting signal of drying or wetting land.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "GWETROOT",
    name: "Root zone soil wetness",
    theme: "water",
    unit: "",
    unitNote: "0 is dry, 1 is soaked",
    definition:
      "How wet the top metre of soil is, where plant roots drink, from 0 (dry) to 1 (soaked).",
    why: "Tracks the water that crops and forests can actually use.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "EVLAND",
    name: "Evaporation from land",
    theme: "water",
    unit: "mm/day",
    definition:
      "Water returning from the land to the air each day, by evaporating from soil and passing through plants.",
    why: "Changes when a region gets hotter, drier or greener.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "SNODP",
    name: "Snow depth",
    theme: "water",
    unit: "cm",
    definition: "The depth of snow lying on the ground.",
    why: "Snowpack stores winter water for summer rivers, and shrinks as winters warm.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "QV2M",
    name: "Moisture in the air",
    theme: "air",
    unit: "g/kg",
    definition:
      "Grams of water vapour in each kilogram of air near the ground. Also called specific humidity.",
    why: "Warmer air holds more water vapour, which feeds heavier rain.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "RH2M",
    name: "Relative humidity",
    theme: "air",
    unit: "%",
    definition:
      "How full of water vapour the air is, as a percentage of the most it could hold at that temperature.",
    why: "Falls when air warms faster than moisture can keep up, raising fire risk.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "TQV",
    name: "Water vapour overhead",
    theme: "air",
    unit: "kg/m²",
    definition:
      "All the water vapour in the column of air above one square metre of ground, from the surface to space.",
    why: "A whole-atmosphere view of how much moisture is available for storms.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "WS10M",
    name: "Wind speed",
    theme: "air",
    unit: "m/s",
    definition: "The average speed of the wind ten metres above the ground.",
    why: "Matters for wind power, dust, and how fast land and sea dry out.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "PS",
    name: "Surface pressure",
    theme: "air",
    unit: "kPa",
    definition: "The weight of the air pressing down on the surface.",
    why: "Slow shifts point to changes in large weather patterns.",
    origin: MERRA2,
    startYear: 1981,
  },
  {
    id: "AOD_55",
    name: "Haze and dust",
    theme: "air",
    unit: "",
    unitNote: "optical depth, higher is hazier",
    definition:
      "How much tiny particles such as dust, smoke and pollution dim the view through the atmosphere. Higher means hazier. Called aerosol optical depth.",
    why: "Rises with pollution, fires and dust storms, and falls when air is cleaned up.",
    origin: "CERES SYN1deg",
    startYear: 2001,
  },
  {
    id: "ALLSKY_SFC_SW_DWN",
    name: "Sunlight reaching the ground",
    theme: "sky",
    unit: "MJ/m²/day",
    definition:
      "The solar energy landing on each square metre of ground per day, with clouds and haze included.",
    why: "Dims or brightens as clouds and air pollution change.",
    origin: RADIATION,
    startYear: 1984,
    caveat: SPLICE_CAVEAT,
  },
  {
    id: "ALLSKY_SFC_LW_DWN",
    name: "Heat radiated down by the sky",
    theme: "sky",
    unit: "MJ/m²/day",
    definition:
      "Infrared energy sent down to the ground by the atmosphere and clouds. This is the greenhouse effect seen from below.",
    why: "Grows as the air warms and holds more water vapour and greenhouse gases.",
    origin: RADIATION,
    startYear: 1984,
    caveat: SPLICE_CAVEAT,
  },
  {
    id: "CLOUD_AMT",
    name: "Cloud cover",
    theme: "sky",
    unit: "%",
    definition: "The share of the sky covered by cloud, averaged over day and night.",
    why: "Clouds both cool and warm the surface, so small shifts matter.",
    origin: RADIATION,
    startYear: 1984,
    caveat: SPLICE_CAVEAT,
  },
];

export const VARIABLES_BY_ID: Record<string, Variable> = Object.fromEntries(
  VARIABLES.map((v) => [v.id, v]),
);

/** Region sizes offered, in degrees of latitude and longitude. 0 is a single grid cell. */
export const REGION_SIZES = [0, 2, 5, 10] as const;
export type RegionSize = (typeof REGION_SIZES)[number];

/** A demonstration investigation a newcomer can open with one click. */
export interface DemoCase {
  id: string;
  question: string;
  place: string;
  variableId: string;
  lat: number;
  lon: number;
  size: RegionSize;
  /** A second place to set against the first */
  compare?: { place: string; lat: number; lon: number };
  /** How tricky the call is, shown on the case card */
  difficulty: "Easy" | "Medium" | "Hard";
  /** The story that sets up the case, without giving the answer away */
  briefing: string;
}

export const DEMO_CASES: DemoCase[] = [
  {
    id: "arctic",
    question: "Is the Arctic warming?",
    difficulty: "Easy",
    briefing:
      "Svalbard sits halfway between mainland Norway and the North Pole. People who live there say the winters are not what they used to be. Check the air temperature since 1981.",
    place: "Svalbard",
    variableId: "T2M",
    lat: 78.2,
    lon: 15.6,
    size: 5,
  },
  {
    id: "sahel",
    question: "Is rain returning to the Sahel?",
    difficulty: "Medium",
    briefing:
      "The Sahel, the belt of land south of the Sahara, suffered devastating droughts in the 1970s and 1980s. Has the rain come back since?",
    place: "the central Sahel",
    variableId: "PRECTOTCORR",
    lat: 14,
    lon: 2,
    size: 10,
  },
  {
    id: "mediterranean",
    question: "Is southern Spain drying out?",
    difficulty: "Medium",
    briefing:
      "Andalusia grows much of the world's olive oil, and its farmers depend on water held in the soil. Look at the root zone, where the trees drink.",
    place: "Andalusia",
    variableId: "GWETROOT",
    lat: 37.5,
    lon: -4.5,
    size: 5,
  },
  {
    id: "delhi",
    question: "Are Delhi's nights heating up?",
    difficulty: "Easy",
    briefing:
      "Delhi is one of the largest cities on Earth. Night is when a city should cool down. Are the coldest hours of the day getting warmer?",
    place: "Delhi",
    variableId: "T2M_MIN",
    lat: 28.6,
    lon: 77.2,
    size: 0,
  },
  {
    id: "rain-two-ways",
    question: "Is rain changing the same way in the Sahel and Chile?",
    difficulty: "Hard",
    briefing:
      "Same planet, same measurement, two places on opposite sides of the Atlantic. Make a call for each one. You need both right for three stars.",
    place: "the central Sahel",
    variableId: "PRECTOTCORR",
    lat: 14,
    lon: 2,
    size: 10,
    compare: { place: "central Chile", lat: -36, lon: -72 },
  },
  {
    id: "china-haze",
    question: "Has the haze over eastern China cleared?",
    difficulty: "Hard",
    briefing:
      "Smog over the North China Plain made headlines for years. Did the skies clear? Careful: this satellite record starts in 2001, so it is shorter than the others.",
    place: "the North China Plain",
    variableId: "AOD_55",
    lat: 36,
    lon: 116,
    size: 5,
  },
];
