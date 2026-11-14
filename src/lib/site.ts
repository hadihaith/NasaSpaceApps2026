/**
 * The site's sections, in the order a visitor should go through them.
 * The navbar, the How-to page and the "Next step" cards all read this list,
 * so reordering or renaming a section here updates all three.
 */
export interface Section {
  href: "/" | "/detective" | "/inferences" | "/what-if";
  /** Short navbar label */
  label: string;
  /** Longer title for cards and the guide */
  title: string;
  /** One line: what the visitor gets out of it */
  summary: string;
  /** What to do there, step by step */
  steps: string[];
  /** Real observations, or a hypothetical model */
  kind: "story" | "real" | "hypothetical";
}

export const SECTIONS: Section[] = [
  {
    href: "/",
    label: "Play",
    title: "Play the story",
    summary: "Join an alien crew, run climate experiments and earn discoveries.",
    steps: [
      "Press Play transmission to watch the opening, or skip to recruitment.",
      "Enter an alien name to join the crew. Progress saves in this browser.",
      "In the laboratory, pick a region, then raise the heat to calibrate the device.",
      "Change heat, rain, humidity and wind, choose how long to run, and run the experiment.",
      "Each discovery opens the Earth Archive: real NASA records for the same region, with trend tests.",
      "With three discoveries and one archive investigation, submit your report to see the ending.",
    ],
    kind: "story",
  },
  {
    href: "/detective",
    label: "Investigate",
    title: "Investigate a trend",
    summary: "Pick any variable and place, guess the trend, then test it on 40 years of NASA data.",
    steps: [
      "Open a case from the board, or choose your own variable and click a place on the globe.",
      "Make your call before you see the data: rising, falling or no clear trend.",
      "Reveal the evidence: the chart, the rate of change and whether it is significant.",
      "Try another place, or compare two places on one chart.",
    ],
    kind: "real",
  },
  {
    href: "/inferences",
    label: "Inferences",
    title: "See what the data show",
    summary: "What 13 real records for Phoenix, Arizona do and do not let us conclude.",
    steps: [
      "Pick a record on the trend board to see its chart, rate of change and test results.",
      "Read the three tested questions: city versus region, three rain records, and what moves together.",
      "Finish with what these records cannot tell you.",
    ],
    kind: "real",
  },
  {
    href: "/what-if",
    label: "What if",
    title: "Run a what-if",
    summary: "Change Phoenix's real climate and see what a physics model says would follow.",
    steps: [
      "Start from a preset, or move the temperature, rain, humidity and wind dials.",
      "Choose how many years to run.",
      "Read the report, the before-and-after numbers and the four graphs.",
      "Optionally ask for an AI-written briefing of the result.",
    ],
    kind: "hypothetical",
  },
];

export const HOW_TO = { href: "/how-to", label: "How to" } as const;

export const KIND_LABEL: Record<Section["kind"], string> = {
  story: "Story game",
  real: "Real data",
  hypothetical: "Hypothetical model",
};

/** Which section a path belongs to ("/detective/x" belongs to "/detective"). */
export function sectionIndex(pathname: string): number {
  return SECTIONS.findIndex((s) => (s.href === "/" ? pathname === "/" : pathname.startsWith(s.href)));
}
