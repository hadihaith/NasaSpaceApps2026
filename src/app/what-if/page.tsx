import type { Metadata } from "next";
import { WhatIfLab } from "@/components/WhatIfLab";
import { regionClimate } from "@/lib/earth/climate";
import { geminiKey } from "@/lib/whatif/ai";

export const metadata: Metadata = {
  title: "What if · Trend Detective",
  description:
    "Turn the dials on Phoenix's real climate and see what a simple physics model says would follow for soil, air and heat.",
};

export default function WhatIfPage() {
  const { climate, sources } = regionClimate();
  // The AI briefing is optional: it switches on when the server has a Gemini API key.
  const aiEnabled = Boolean(geminiKey());

  return (
    <div className="mx-auto max-w-[1180px] px-4 pb-16 sm:px-6">
      <main>
        <header className="mt-10 border-b-[2.5px] border-edge pb-4">
          <p className="chip">Region 01 · Phoenix and central Arizona</p>
          <h1 className="display mt-3 text-5xl sm:text-6xl">What if…?</h1>
          <p className="mt-3 max-w-[75ch] text-lg text-ink-2">
            Turn the dials on the region&apos;s real climate. A small physics model works out what would follow for
            the soil, the air and the heat, and writes it up with graphs. Everything on this page is hypothetical.
          </p>
        </header>
        <div className="mt-8">
          <WhatIfLab climate={climate} sources={sources} aiEnabled={aiEnabled} />
        </div>
      </main>
    </div>
  );
}
