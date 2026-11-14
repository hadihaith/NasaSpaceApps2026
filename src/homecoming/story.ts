import type { CrewId } from "./Crew";
import type { Cue } from "./audio";

type Shot = "moon" | "apollo" | "bridge" | "party" | "dust" | "artemis" | "window" | "button" | "title" | "lab" | "hearing" | "black";
export interface Beat {
  shot: Shot;
  caption?: string;
  speaker?: CrewId;
  label?: string;
  cue?: Cue;
  ms: number;
}

export const PROLOGUE: Beat[] = [
  { shot: "moon", label: "Unknown signal", cue: "radio", ms: 4100 },
  { shot: "apollo", label: "Apollo 11 · July 1969 · NASA archival footage", ms: 9000 },
  { shot: "bridge", speaker: "commander", label: "1969 · Somewhere in the galaxy", caption: "THEY DID IT! THEY FINALLY LEFT EARTH!", cue: "voice", ms: 2900 },
  { shot: "bridge", speaker: "scientist", caption: "Sir, that’s one human.", cue: "voice", ms: 2350 },
  { shot: "bridge", speaker: "commander", caption: "THE FIRST OF MANY!", cue: "voice", ms: 2300 },
  { shot: "party", speaker: "cadet", caption: "I’ve read the cannon manual. It’s mostly warnings.", cue: "celebrate", ms: 3400 },
  { shot: "party", label: "WELCOME HOME, HUMANS!", caption: "Party cannon status: lodged in ceiling.", cue: "alarm", ms: 2900 },
  { shot: "black", label: "Fifty-seven years later…", ms: 2400 },
  { shot: "dust", label: "2026 · Same ship. Same decorations.", caption: "The balloons have seniority now.", ms: 3100 },
  { shot: "artemis", speaker: "commander", label: "Earth news · Artemis lunar exploration", caption: "THEY’RE GOING BACK TO THE MOON?!", cue: "radio", ms: 3300 },
  { shot: "artemis", speaker: "scientist", caption: "They’re planning more lunar exploration, sir.", cue: "voice", ms: 3000 },
  { shot: "dust", speaker: "commander", caption: "WHY WON’T THEY JUST COME HERE?", cue: "voice", ms: 2900 },
  { shot: "window", speaker: "commander", caption: "I think I’ve figured it out.", cue: "voice", ms: 2600 },
  { shot: "window", speaker: "scientist", caption: "Please don’t.", cue: "voice", ms: 1900 },
  { shot: "window", speaker: "commander", caption: "THEY LOVE THEIR PLANET.", cue: "voice", ms: 2400 },
  { shot: "dust", speaker: "commander", caption: "THEN WE MUST MAKE THEM LOVE OURS MORE.", cue: "voice", ms: 3000 },
  { shot: "button", speaker: "cadet", caption: "That button says ‘do not slam.’", cue: "alarm", ms: 2400 },
  { shot: "title", label: "Operation: The Great Homecoming", caption: "Better Together. Love Thy Human.", cue: "stamp", ms: 4400 },
];

export const LAB_INTRO: Beat[] = [
  { shot: "lab", label: "Research bay · Planetary Persuasion Device", speaker: "commander", caption: "BEHOLD! THE PLANETARY PERSUASION DEVICE!", cue: "celebrate", ms: 3200 },
  { shot: "lab", speaker: "scientist", caption: "It’s an environmental simulation system.", cue: "voice", ms: 2800 },
  { shot: "lab", speaker: "commander", caption: "THAT’S WHAT I SAID.", cue: "voice", ms: 2300 },
  { shot: "lab", speaker: "scientist", caption: "Change the conditions, advance time and observe what happens. Earth itself is safely out of reach.", cue: "voice", ms: 5600 },
];

export const ENDING: Beat[] = [
  { shot: "hearing", label: "Alien Government · Emergency presentation", speaker: "commander", caption: "AFTER 57 YEARS, WE HAVE FINALLY MADE EARTH UNINHABITABLE!", cue: "celebrate", ms: 4000 },
  { shot: "hearing", speaker: "scientist", caption: "Sir, these were computer simulations.", cue: "voice", ms: 2800 },
  { shot: "hearing", speaker: "commander", caption: "…", ms: 2400 },
  { shot: "hearing", speaker: "scientist", caption: "We haven’t actually changed anything on Earth.", cue: "voice", ms: 3000 },
  { shot: "hearing", speaker: "commander", caption: "SO… WE NEED MORE RESEARCH.", cue: "voice", ms: 3150 },
];
