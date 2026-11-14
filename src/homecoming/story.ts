import type { Cue } from "./audio";

export type ShotId = "signal" | "apollo" | "reveal" | "correction" | "party" | "jam" |
  "years" | "return" | "earth" | "launch" | "title" | "lab" | "hearing" | "deflate";

export interface Shot {
  id: ShotId;
  seconds: number;
  description: string;
  label?: string;
  line?: string;
  cues?: ReadonlyArray<readonly [number, Cue]>;
}

export const PROLOGUE: Shot[] = [
  { id: "signal", seconds: 4.5, description: "A pinprick in the dark becomes the Moon as the camera races toward it.", cues: [[.08, "radio"], [2.9, "orbit"]] },
  { id: "apollo", seconds: 5.7, label: "1969", description: "Apollo 11 television footage shows Neil Armstrong in the lunar module's shadow." },
  { id: "reveal", seconds: 4, description: "The picture shrinks into a television. On an alien ship, Zorvax springs forward to greet the astronaut.", line: "Zorvax: They’re coming!", cues: [[1.3, "orbit"], [2.35, "voice"]] },
  { id: "correction", seconds: 2.6, description: "Nyxen holds up a finger. Zorvax is already turning toward the party cannon.", line: "Nyxen: That’s one.", cues: [[.45, "quiet"]] },
  { id: "party", seconds: 4, description: "The cadet fires the party cannon. Foil strips and balloons fly as the crew dances beneath a welcome banner.", cues: [[.72, "party-hit"], [1, "celebrate"]] },
  { id: "jam", seconds: 1.7, description: "One last streamer lodges in the cannon. The cannon coughs.", cues: [[.58, "sputter"], [1.08, "sputter"]] },
  { id: "years", seconds: 3.8, label: "2026", description: "The same banner sags. In the same room, the crew is still waiting.", cues: [[.18, "sputter"]] },
  { id: "return", seconds: 4.2, description: "The television reports the Artemis missions. The crew turns to watch. Zorvax stares in disbelief.", line: "Zorvax: The Moon? Again?", cues: [[.18, "radio"], [2, "quiet"]] },
  { id: "earth", seconds: 4, description: "Through the window, Earth rolls into the light. Zorvax takes a long look at the planet humans keep coming home to.", cues: [[.3, "orbit"]] },
  { id: "launch", seconds: 2, description: "Zorvax slams a red switch. The lights kick on.", cues: [[.74, "stamp"]] },
  { id: "title", seconds: 2.8, description: "Operation: The Great Homecoming.", cues: [[.14, "discover"]] },
];

export const LAB_INTRO: Shot[] = [
  { id: "lab", seconds: 6.4, description: "Nyxen switches on the projector. A miniature Earth hangs over the research bench. The scanner sweeps across it.", line: "Nyxen: Your planet. Try the dials.", cues: [[.45, "scan"], [1.5, "orbit"], [4.4, "voice"]] },
];

export const ENDING: Shot[] = [
  { id: "hearing", seconds: 5.3, description: "Zorvax presents your report and reaches for the party cannon. Nyxen points to the word SIMULATION.", line: "Nyxen: We ran a simulation.", cues: [[.6, "stamp"], [3.1, "quiet"]] },
  { id: "deflate", seconds: 3.5, description: "The last party balloon deflates with a squeak. The crew is still waiting.", cues: [[.84, "deflate"]] },
];
