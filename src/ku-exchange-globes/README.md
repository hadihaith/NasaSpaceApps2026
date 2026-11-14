# KU Exchange globes

Two animated Earth globes copied from the KU Exchange portal (the `team2` repository, commit
`7973a3a`, plus the then-uncommitted wireframe globe). Nothing here is imported by this app yet.
They are separate from this project's own globe in `src/earth/`, which uses React Three Fiber.

| File | What it is |
|---|---|
| `components/PlanetScene.jsx` | A photoreal 3D Earth in WebGL (three.js): day and night maps, city lights, three cloud layers, ocean glint, an atmosphere rim, stars. It fills the screen behind the page and moves as the page scrolls. |
| `components/PortalGlobeBackground.jsx` | A light wireframe globe in Canvas 2D: graticule, dots at real places, and animated flights along great-circle routes. No libraries beyond React. |
| `partnerUniversities.js` | Data the wireframe globe reads: `partnerUniversityCoordinates`, place name → `[latitude, longitude]`. The other exports (websites, flags, airports) are unused here. |
| `globes.css` | The CSS both globes need: the colour custom properties, `--navbar-height`, `.planet-canvas`, and the `.portal-scene` / `.portal-globe` layout. Collected from several team2 stylesheets. |

The three `.jsx`/`.js` files are byte-for-byte copies; the folder mirrors team2's layout
(`components/` next to `partnerUniversities.js`) so their relative imports resolve unchanged.
`globes.css` and this file were written for this folder.

Neither component has a `'use client'` directive. In the Next.js app router, import them from a
file that has one, since both use hooks, `window` and the DOM.

---

## Wireframe globe: `PortalGlobeBackground`

The simpler one to reuse. Pure Canvas 2D, about 0.2 ms per frame.

### Use

```jsx
'use client'
import PortalGlobeBackground from '@/ku-exchange-globes/components/PortalGlobeBackground'
import '@/ku-exchange-globes/globes.css'

export default function Page() {
  return (
    <div className="portal-scene">
      <PortalGlobeBackground />
      <main>…page content, drawn on top of the globe…</main>
    </div>
  )
}
```

- It takes no props. It must be the **first child** of an element with class `portal-scene`; the
  content after it sits on top. Content with an opaque background hides the globe behind it.
- The globe fills the screen below a sticky top bar of height `--navbar-height`
  (116px in `globes.css`; set it to `0px` with no bar). It stays put while the page scrolls and
  is not resized when the content changes length.
- `.portal-scene` uses `overflow: clip`, not `hidden`: `hidden` would make it a scroll container
  and the globe would stop sticking.

### What it draws

- An orthographic globe tipped 22° towards the viewer, turning 2.4°/s (one turn in 150 s),
  starting with longitude 30°E facing front.
- A graticule every 20°, the outline circle, a dot per place in `partnerUniversityCoordinates`
  (dots fade and shrink towards the rim for depth), and Kuwait (`KUWAIT`, 29.25°N 47.90°E) as a
  brighter dot with a ring.
- Flights between Kuwait and a random place: great-circle arcs lifted off the surface mid-way,
  with a fading trail. 45% are inbound (gold), the rest outbound (cyan). At most 3 at once, a new
  one every 1.7 s, each lasting 3.6 s. A flight disappears where it passes behind the globe.

### Customise

- **Places:** replace `PARTNERS` (any array of `[lat, lon]`) and `KUWAIT` at the top of the
  component, e.g. with NASA centres or study sites.
- **Colours:** read at run time from CSS custom properties on `:root`: `--neon` (dots, Kuwait,
  outbound flights), `--neon-gold` (inbound flights), `--text-muted` (graticule and outline).
  `data-theme="light"` on `<html>` slightly changes line opacity; the component watches that
  attribute with a `MutationObserver` and redraws, so a theme switch needs no prop.
- **Position and size:** CSS custom properties on `.portal-globe`: `--globe-x` and `--globe-y`
  (centre, as fractions of the area) and `--globe-size` (radius, as a fraction of the larger
  side). Defaults put a large globe off the right edge; `globes.css` moves it low and dims it at
  768px and below.
- **Motion:** `TILT`, `DEGREES_PER_SECOND`, `START_LONGITUDE`, `MAX_FLIGHTS`, `SPAWN_EVERY_MS`,
  `FLIGHT_MS`, `TRAIL` at the top of the file.

### Performance and accessibility

- Redraws at most 30 times a second (the globe moves 0.1° a frame, so 60 looks no different) and
  caps the canvas at 1.5× device pixels. Measured in team2 at 1440×900: 0.2 ms median and 0.5 ms
  worst per drawn frame, no dropped frames.
- The graticule's points are computed once; each frame only rotates and projects them.
- Stops its animation loop and observers on unmount. With `prefers-reduced-motion: reduce` it
  draws one still frame and never animates.
- The wrapper is `aria-hidden` and `pointer-events: none`.

---

## 3D Earth: `PlanetScene`

The heavier, more striking one. It is KU Exchange's home page background.

### Props

```jsx
const canvasRef = useRef(null)
const onReady = useCallback(() => setStatus('ready'), [])
const onError = useCallback(() => setStatus('error'), [])

<PlanetScene canvasRef={canvasRef} theme="dark" onReady={onReady} onError={onError} />
```

| Prop | Meaning |
|---|---|
| `canvasRef` | A ref created by the parent (`useRef(null)`). The component renders its own `<canvas className="planet-canvas">` and attaches the ref to it. |
| `theme` | `'dark'` (night side, city lights, stars) or `'light'` (sunlit face, clearer blue oceans, no stars). Changing it cross-fades over 1.2 s. |
| `onReady()` | Called once, after the first frame is drawn. KU Exchange shows a loader until then. |
| `onError(error)` | Called if three.js or a texture fails to load. |

`onReady` and `onError` are dependencies of the scene's setup effect: pass stable functions
(`useCallback`), or the whole scene is torn down and rebuilt on every render.

### What it needs from the page

- **three.js r0.143 from unpkg, by URL.** The component loads it with
  `import('https://unpkg.com/three@0.143.0/…')`. `EffectComposer.js` in turn imports the bare
  name `three`, so the page needs this import map in `<head>`:

  ```html
  <script type="importmap">
    {
      "imports": {
        "three": "https://unpkg.com/three@0.143.0/build/three.module.js",
        "three/examples/jsm/": "https://unpkg.com/three@0.143.0/examples/jsm/"
      }
    }
  </script>
  ```

- **Textures, by URL:** NASA Blue Marble (day), Black Marble (city lights) and a water mask from
  `unpkg.com/three-globe@2.31.0/example/img/` (public domain), and a cloud texture from
  `api.getlayers.ai/…/planet-clouds.png`, a third-party host that could disappear: download it
  and serve it from `public/` before relying on it.
- **CSS:** `.planet-canvas` in `globes.css` fixes the canvas full screen behind the page
  (`z-index: 0`); give the page content `position: relative; z-index: 1`.

### Using it in this Next.js project

It does not drop in as-is. Turbopack bundles imports, so the `import('https://…')` calls are
likely to fail at build time. Two ways to run it:

1. **In a Vite page**, like this repo's `earth-preview/`. Vite leaves full-URL dynamic imports for
   the browser to load, so the component runs unchanged once the page has the import map above.
2. **On this project's npm `three` (0.187.0)**, the cleaner option for the main app. Change the
   four imports to `import('three')` and
   `import('three/examples/jsm/postprocessing/EffectComposer.js')` (likewise `RenderPass.js` and
   `ShaderPass.js`; all three are in `node_modules/three/examples/jsm/postprocessing/`). Then
   update what changed in three.js since r143:
   - `renderer.outputEncoding = THREE.sRGBEncoding` → `renderer.outputColorSpace = THREE.SRGBColorSpace`
   - `dayTexture.encoding = THREE.sRGBEncoding` → `dayTexture.colorSpace = THREE.SRGBColorSpace`
   - Lighting became physically based by default in r155, so the ambient and directional light
     intensities (0.5 and 0.7 and their light-mode ramps in `render`) will likely look different;
     scale them until it matches.

   The shader patches hook only `void main() {` and `#include <dithering_fragment>`, and both
   still exist in 0.187's standard material, but this port has **not been run**. Check the
   browser console for shader compile errors after the change.

### Behaviour

- **Scene:** a sphere with a custom shader on `MeshStandardMaterial` (night side, city lights
  with a soft halo, terrain relief from the day map, ocean glint and deep-water tint, an
  antialiased rim), 2–3 cloud shells turning at different speeds, a star field and a scatter
  of soft atmosphere motes. A final full-screen pass adds the sky, rim light and halo around the globe.
- **Scroll:** the globe's position, scale and rotation follow the page's scroll progress through
  the keyframes `STOPS_X`, `STOPS_Y` and `STOPS_S` (it swings from one side of the screen to the other
  as the page scrolls). For a still, centred globe, set each to a single stop such as
  `[{ p: 0, v: 0 }]`.
- **Tuning:** `CONFIG` at the top holds colours, planet radius, spin, tilt, cloud heights and
  opacities, and star and mote counts.

### Performance and accessibility

- Touch devices get a lighter scene (half the stars, two cloud layers, cheaper shader paths) and
  a 1.5× pixel cap; elsewhere 2×.
- If frames run slower than about 50 fps after start-up, it lowers its render resolution in
  steps (down to 55%); it never steps back up, so it cannot oscillate.
- Noise the shaders need is baked once into textures; there is no multisampling (rims are
  antialiased in the shaders).
- `prefers-reduced-motion: reduce` stops the spin and cloud drift and makes theme switches
  instant. Scroll still moves the globe.
- On unmount it cancels the loop and disposes every texture, geometry, material, render target
  and the renderer.
