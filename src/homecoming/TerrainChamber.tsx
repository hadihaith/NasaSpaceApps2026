import { useId, type CSSProperties } from "react";
import type { Climate, Environment } from "./model";

const clamp = (value: number) => Math.min(1, Math.max(0, value));
const range = (length: number) => Array.from({ length }, (_, i) => i);

const BIOMES: Record<string, { ridge: string; rock: string; land: string; shape: string; density: number }> = {
  phoenix: { ridge: "M0 194V155L61 149 97 124 147 124 153 139 217 139 243 159 294 164 329 181 399 161 466 161 507 136 558 136 569 153 619 153 659 173 760 151V205Z", rock: "#996643", land: "#ad8053", shape: "scrub", density: 12 },
  sahel: { ridge: "M0 202V160Q72 146 156 159T308 154 469 164 615 151 760 160V208Z", rock: "#ab7c43", land: "#b49a58", shape: "scrub", density: 15 },
  amazon: { ridge: "M0 203V164Q25 153 59 158Q76 142 120 154Q143 131 171 149Q192 139 204 150Q232 132 271 153Q317 138 348 156Q403 133 457 152Q474 135 505 145Q548 128 574 150Q605 135 654 153Q695 127 760 149V205Z", rock: "#356752", land: "#507844", shape: "forest", density: 26 },
  mediterranean: { ridge: "M0 200V163L78 136 119 143 178 114 226 135 264 128 302 152 351 131 399 149 481 134 542 150 616 113 668 145 760 142V210Z", rock: "#6c7977", land: "#8f8f58", shape: "forest", density: 16 },
  "south-asia": { ridge: "M0 206V160L81 133 129 141 170 110 216 133 265 120 319 150 359 127 399 145 451 124 502 144 553 133 622 152 684 127 760 146V212Z", rock: "#437879", land: "#719445", shape: "forest", density: 21 },
  australia: { ridge: "M0 203V161Q52 128 89 149T158 139 239 160 343 155 429 137 529 154 616 135 700 150L760 146V212Z", rock: "#716c59", land: "#a08b52", shape: "forest", density: 18 },
};

// Fixed positions keep the scenery still when the underlying readings advance.
const plants = range(26).map((i) => {
  const depth = .12 + Math.floor(i / 7) * .20 + (i * 13 % 5) * .021;
  return { x: 240 - depth * 126 + (i * 173 % 374), y: 190 + depth * 87, size: (.72 + i * 29 % 56 / 100) * (.70 + depth * .45) };
}).sort((a, b) => a.y - b.y);

function Plant({ kind, vigor, breeze, variant }: { kind: string; vigor: number; breeze: number; variant: number }) {
  return <g className="hc-terrain-plant" style={{ "--sway": `${2 + 7 * breeze}deg`, "--plant-cycle": `${1.5 + (variant % 4) * .43}s`, "--plant-delay": `${-variant * .17}s` } as CSSProperties}>
    <g style={{ transform: `scale(1, ${.41 + .72 * vigor})` }} className="hc-terrain-canopy">
      {kind === "forest" ? <>
        <path d="M-1 1 0-26M0-12l-8-7m8 2 10-7" fill="none" stroke="#5b442b" strokeWidth="2.6" />
        <path d="M-17-23q-2-9 7-12 1-8 9-7 5-5 11 1 10 1 9 12 5 7-3 12-9 4-16-1-11 5-17-5Z" fill="currentColor" />
        <path d="M-15-27q2-8 10-7 4-7 13-4 6 1 7 6-7-3-13 3-8-3-17 2Z" fill="#e5f6ad" opacity=".36" />
        <path d="M-16-24q6 7 16 3 9 5 17-3l-1 5q-7 6-16 1-12 4-16-6Z" fill="#072923" opacity=".36" />
      </> : <>
        <path d="M0 1Q-2-15-17-20M0 1q7-14 16-17M0 1l-2-22M-1-3q-7-5-13-6m16 4 13-3" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" fill="none" />
        <path d="M-1 0q-8-16-9-26M0 0q3-15 8-24" stroke="#e3d283" strokeWidth="1.4" opacity=".63" fill="none" />
      </>}
    </g>
  </g>;
}

export function TerrainChamber({ region, weather, state }: { region: string; weather: Climate; state: Environment }) {
  const uid = useId().replace(/:/g, "");
  const ref = (name: string) => `url(#${uid}-${name})`;
  const biome = BIOMES[region] ?? BIOMES.phoenix;
  const rain = weather.rain <= 0 ? 0 : clamp(.06 + Math.sqrt(weather.rain / 360) * .84);
  const humidity = clamp((weather.humidity - 20) / 78);
  const heat = clamp((weather.temperature - 2) / 38);
  const heatColor = `color-mix(in oklab, #8edfff ${Math.round((1 - heat) * 100)}%, #ffad69)`;
  const wind = clamp(weather.wind / 18);
  const moisture = clamp(state.soilMoistureIndex);
  const vigor = clamp(state.vegetationHealthIndex);
  const drought = clamp(state.droughtStressIndex);
  const top = "M237 182H666L548 273H113Z";
  const face = "M113 273H548V370H113Z";
  const side = "M548 273 666 182V276L548 370Z";
  const cloud = "M-88 0c-15-3-13-20 1-24 2-17 19-24 34-17 10-21 49-25 62-4 20-16 49-3 52 15 29-2 39 25 10 30Z";

  return <div className="hc-terrain" aria-hidden="true">
    <svg className="hc-terrain-scene" viewBox="0 0 760 395" preserveAspectRatio="xMidYMax slice">
      <defs>
        <linearGradient id={`${uid}-sky`} x2="0" y2="1"><stop stopColor="#07182b" /><stop offset=".62" stopColor={`hsl(${218 - heat * 39} 40% ${16 + heat * 4}%)`} /><stop offset="1" stopColor="#0a1624" /></linearGradient>
        <radialGradient id={`${uid}-halo`}><stop stopColor={heatColor} stopOpacity=".55" /><stop offset=".44" stopColor={heatColor} stopOpacity=".22" /><stop offset="1" stopColor={heatColor} stopOpacity="0" /></radialGradient>
        <linearGradient id={`${uid}-surface`} x2=".47" y2="1"><stop stopColor={biome.land} /><stop offset="1" stopColor={`hsl(${23 + moisture * 47} ${31 + moisture * 12}% ${35 - moisture * 11}%)`} /></linearGradient>
        <linearGradient id={`${uid}-earth`} x2=".23" y2="1"><stop stopColor="#9b714f" /><stop offset=".34" stopColor="#75543e" /><stop offset="1" stopColor="#342c32" /></linearGradient>
        <linearGradient id={`${uid}-water`} x2="0" y2="1"><stop stopColor="#6bb9b1" stopOpacity=".77" /><stop offset="1" stopColor="#1e607d" stopOpacity=".58" /></linearGradient>
        <linearGradient id={`${uid}-cloud`} x2="0" y2="1"><stop stopColor={rain > .55 ? "#b0c5ce" : "#e8f3ef"} /><stop offset="1" stopColor={rain > .55 ? "#587b92" : "#7799a8"} /></linearGradient>
        <pattern id={`${uid}-grid`} width="30" height="30" patternUnits="userSpaceOnUse"><path d="M30 0H0V30" stroke="#75c6d8" strokeOpacity=".11" strokeWidth=".8" fill="none" /></pattern>
        <pattern id={`${uid}-soilgrain`} width="28" height="14" patternUnits="userSpaceOnUse"><path d="m2 3 2 1m9 5h2m8-5 3-1m-17 8 2-1" stroke="#ffe4b0" strokeWidth="1" strokeLinecap="round" opacity=".24" /></pattern>
        <clipPath id={`${uid}-top`}><path d={top} /></clipPath>
        <clipPath id={`${uid}-earth-cut`}><path d={face} /><path d={side} /></clipPath>
      </defs>
      <path d="M0 0h760v395H0Z" fill={ref("sky")} />
      <path d="M0 0h760v395H0Z" fill={ref("grid")} />
      <ellipse cx="550" cy="103" rx={142 + heat * 76} ry="132" fill={ref("halo")} opacity={1 - rain * .45} />
      <g opacity={1 - rain * .62}>
        <circle cx="550" cy="103" r="47" stroke="#efd9a4" strokeDasharray="2 5" strokeOpacity=".39" fill="none" />
        <circle cx="550" cy="103" r={18 + heat * 17} fill={heatColor} />
        <circle cx="550" cy="103" r={19 + heat * 17} stroke="#fff7e0" strokeOpacity=".8" fill="none" />
      </g>
      <path d={biome.ridge} fill={biome.rock} opacity=".3" />
      <path d={biome.ridge} fill="none" stroke={biome.rock} strokeWidth="1" opacity=".68" />
      <path d="M0 199Q113 169 247 188T498 179 760 183V238H0Z" fill="#0a2433" opacity=".83" />
      <path d="M0 185h760M0 218h760M0 255h760M0 315h760M0 380h760M379 185 54 395m325-210L214 395m165-210v210m0-210 166 210m-166-210 326 210" stroke="#85ccce" strokeOpacity=".15" strokeWidth="1" fill="none" />
      <path d="M249 191h453L568 386H66Z" fill="#010915" opacity=".55" />

      <path d={face} fill={ref("earth")} />
      <path d={side} fill="#4b3f3a" />
      <g clipPath={ref("earth-cut")}>
        <path d="M108 287q78-9 151 0t169 3l123-2 118-91v23l-118 92q-95 3-171-2t-151 0-122 1Z" fill="#553a35" opacity=".58" />
        <path d="M108 305q49-7 98 1t136 0 210 2l117-88m-559 109q56-4 111 4t174-4 157 3l116-89m-558 109q89-13 184-2t257-3l119-93" stroke="#f5c78f" strokeOpacity=".32" fill="none" />
        <path d="M110 270h558v101H110Z" fill={ref("soilgrain")} />
        <g className="hc-terrain-water" style={{ transform: `translateY(${(1 - moisture) * 108}px)`, opacity: .42 + moisture * .56 }}>
          <path d="M111 264q52 5 101 1t109 0 99 0 129-1l118-90v220H110Z" fill={ref("water")} />
          <path d="M111 264q52 5 101 1t109 0 99 0 129-1l118-90" stroke="#a1faea" strokeWidth="1.25" strokeOpacity=".7" fill="none" />
        </g>
        {range(9).map((i) => <path key={i} d={`M${138 + 46 * i} 270q-9 15-3 25l-8 ${11 + i % 3 * 8}m8-${11 + i % 3 * 8} 6 11 2 12m-8-23-13 7-4 10`} fill="none" stroke="#f3d2a4" strokeOpacity={.25 + vigor * .45} strokeWidth="1.15" />)}
      </g>

      <path d={top} fill={ref("surface")} stroke="#d9bd7a" strokeOpacity=".78" strokeWidth="1" />
      <g clipPath={ref("top")}>
        <path d="M108 186h559v91H108Z" fill={ref("soilgrain")} />
        <path d="M195 187q40 23 2 32t-48 55m165-93q-20 21 11 32t-12 60m99-94q10 26 46 23t28 73m97-95q-37 19-15 41t-15 52" stroke="#f6e2a0" opacity=".21" strokeWidth="1.2" fill="none" />
        <path d="m163 264 29-7 3-9 36-3 16-12 27 1 15-11 19 4 31-14m-108 32 13 9-6 13 17 9m63-9 25-13 29 1 16-13 31 6 12-11 24 2 30-16m-66 25 14 12-8 13m63-80 5 14-17 10 5 10 22 6 9 13 36 2m-50-32 25-8 10 6 25-7 9 6" fill="none" stroke="#492821" strokeWidth="1.8" opacity={.12 + drought * .84} />
        <g fill="#81f0e3" opacity={clamp((moisture - .6) * 2.8)}>
          <path d="M194 239q31-10 45 0t-27 11q-47 1-18-11Zm196-32q41-11 48 1t-31 9q-39-3-17-10Zm100 48q18-15 50-13t6 12q-19 10-56 1Z" />
          <path d="M202 240q12-4 23 0m174-32 21-3m85 50 29-7" stroke="#e2fff1" strokeWidth="1" fill="none" />
        </g>
      </g>
      <path d="M113 273H548L666 182M113 273v97h435l118-94v-94m-118 91v97" fill="none" stroke="#caa87a" strokeOpacity=".7" strokeWidth="1" />
      <g style={{ color: `hsl(${34 + vigor * 87} ${42 + vigor * 16}% ${35 + vigor * 21}%)` }}>
        {plants.map(({ x, y, size }, i) => (i * 17 % 26 < biome.density) && <g key={i} transform={`translate(${x} ${y}) scale(${size})`}>
          <ellipse cx="2" cy="1" rx="10" ry="2.4" fill="#162920" opacity=".55" />
          <Plant kind={biome.shape} vigor={vigor} breeze={wind} variant={i} />
        </g>)}
      </g>

      <g className="hc-terrain-rain" style={{ "--rain-time": `${.86 - rain * .36}s`, "--rain-drift": `${5 + wind * 26}px` } as CSSProperties}>
        {range(42).map((i) => <path key={i} d={`M${179 + i * 89 % 418} ${118 + i * 31 % 58}l${-1 - wind * 4} ${-8 - rain * 9}`} stroke="#a5edff" strokeWidth="1.25" strokeLinecap="round" opacity={clamp((rain - i / 44) * 4) * .82} style={{ animationDelay: `${-.07 * i}s` }} />)}
      </g>
      <g className="hc-terrain-wind" style={{ opacity: clamp(wind * 1.6 - .13), "--wind-time": `${2.8 - wind * 1.8}s` } as CSSProperties} fill="none" stroke="#cbf3dc" strokeWidth="1.1">
        <path d="M93 129h100q18 0 18-12M152 158h130q23 0 23-16m146 51h123q21 0 25-11m-275-45h154m71 21h112q24 0 27-13M83 220h87" />
      </g>
      <g opacity={.11 + humidity * .40 + rain * .47}>
        <g className="hc-terrain-cloud" style={{ "--cloud-time": `${10 - 6 * wind}s` } as CSSProperties}>
          <use href={`#${uid}-cloud-shape`} transform="translate(273 127) scale(1 .65)" />
          <use href={`#${uid}-cloud-shape`} transform="translate(435 113) scale(1.08 .82)" />
          <use href={`#${uid}-cloud-shape`} transform="translate(573 138) scale(.68 .58)" opacity={humidity} />
        </g>
        <defs><path id={`${uid}-cloud-shape`} d={cloud} fill={ref("cloud")} /></defs>
      </g>
      <g className="hc-terrain-annotations" stroke="#aaced0" strokeWidth=".8" fill="none">
        <path d="M102 106h58l16 22m462 103h-16l-21 16M101 328h50l21-10" opacity=".75" />
        <circle cx="176" cy="128" r="2" /><circle cx="601" cy="247" r="2" /><circle cx="172" cy="318" r="2" />
        <g fill="#dcf3ec" stroke="none" fontSize="10" letterSpacing="1.25">
          <text x="28" y="110">ATMOSPHERE</text><text x="644" y="234">SURFACE</text><text x="28" y="332">ROOT ZONE</text>
        </g>
      </g>
    </svg>
  </div>;
}
