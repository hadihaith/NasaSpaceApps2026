# Planetary Lab globe

This module visualizes the simulation state for one selected region. The parent owns the region and the three normalized indices.

## Run the standalone preview

From the repository root, use Node.js 20.19+ or 22.12+.

```sh
npm ci
npm run earth:preview
```

Open http://127.0.0.1:5173. Run `npm run earth:build` to compile the preview into `out/earth-preview/`. The preview uses the existing React dependency, Three.js, React Three Fiber v9, and Vite. The globe has no key or live API dependency.

## Integrate into the parent

```jsx
import { EarthScene } from './earth/EarthScene';

<EarthScene
  regionId={selectedRegion}
  environment={simulationResult.environment}
  onRegionSelect={setSelectedRegion}
/>
```

- Supported placeholder region IDs are `sahel`, `amazon`, `mediterranean`, `south-asia`, and `australia`. Align these IDs and footprints with the simulation catalog in `regions.js` before merging into the game. An unknown ID renders the basemap and region choices with the overlay disabled.
- The accepted fields are `soilMoistureIndex`, `vegetationHealthIndex`, and `droughtStressIndex`. Finite values are clamped to 0–1 and missing values draw as 0.5. All three drive the selected area's illustrative overlay. Selecting another region moves the overlay and turns the camera toward it.
- Copy `public/assets/earth/blue-marble.webp` with the module and serve it at `/assets/earth/blue-marble.webp`. The URL lives in `regions.js` if the parent hosts assets under a different path.
- If the parent has its own layout, set `--earth-viewport-height` on the mount area. The component sizes its canvas to the available width.
- The preview keeps mock state in `EarthPreview.jsx`. The three rendering files have no game state manager.

## Verify

1. Drag the globe and scroll to zoom. Toggle **Idle rotation** to pause it.
2. Select a region from the buttons or click a visible globe marker. The selected footprint and camera focus should follow.
3. Switch between **Dry**, **Healthy**, and **Wet**, then adjust each slider. Amber shows drought stress, green shows vegetation health, and blue shows wet conditions. Watch for smooth changes confined to the selected footprint.
4. Resize the window and use Tab and Enter to change the selected region and keyboard arrows to adjust the controls.
5. Disable WebGL or lose its context. Confirm the 2D map still follows region and slider changes. With reduced motion enabled, the camera and overlay update immediately and idle rotation is disabled.

## Rendering and assets

The scene uses a single 2048 × 1024 WebP texture (238 KiB), simple sphere geometry, a surface shader, a thin atmospheric rim, and small region markers. It caps pixel ratio at 1.5, pauses drawing when the viewport is off-screen, and uses no post-processing, shadows, or external map tile requests. The background grid and orbital trace are decorative. The colored regional overlay is hypothetical and approximate.

The bundled basemap is a WebP conversion of the [Blue Marble whole-Earth mosaic](https://svs.gsfc.nasa.gov/2915/). Credit NASA/Goddard Space Flight Center Scientific Visualization Studio, Reto Stöckli (NASA/GSFC), and NASA Earth Observatory. The educational use of this imagery follows [NASA’s media usage guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/).
