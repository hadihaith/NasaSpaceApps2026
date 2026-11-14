# The Great Homecoming — Unity

Open **`unity/Homecoming`** in Unity Hub with **6000.6.9f1**. Open
`Assets/Scenes/Story.unity` and press **Play**. This is a complete Unity project,
separate from the existing web app.

The story keeps the original 14 shots from `src/homecoming/story.ts` and extends their
dialogue and timing (see **Dialogue** below): Apollo 11, the crew's misunderstanding, the party,
the 57-year wait, Artemis, recruitment, the laboratory introduction, the final
hearing and the deflating balloon. Recruitment and the epilogue retain the
original wording. The existing three skinned GLB characters and their animation
clips are imported using Unity's official glTFast package. The bridge and props
are real, editable scene objects. No generated backgrounds or new character art
are used.

## Play

- **Play transmission** begins the prologue, recruitment and lab introduction.
  **Enter laboratory** opens the field simulation.
- **Chapters / options** lets you replay sections and preview the ending now.
  Previewing a chapter does not mark it complete.
- **Space** pauses, **left/right arrows** change shots, **M** toggles sound,
  **C** toggles dialogue captions, and **Esc** pauses, then returns to the menu.
- Scene descriptions and reduced camera motion are available in playback options.
- Designation, story progress and options are saved locally using `PlayerPrefs`.
  **Start a new operation** clears only this story's designation and progress.
- The original Apollo clip is bundled. Its archival still is used if the video
  decoder is unavailable. Dialogue uses subtitles; no generated voices are included.

## Field simulation

Open **`Assets/Scenes/Story.unity`** for the full story and choose **Enter laboratory** after the briefing. Open **`Assets/Scenes/Simulation.unity`** and press **Play** to start in the lab. The curated records and scenarios work offline.

### Play the cases

- **Two places, one measurement** · Open **Investigation**, select the rainfall case and call the direction at both sites. Fly to the comparison and file the findings. For the 1991–2025 window, the Central Sahel grid point rises by 2.133 mm/month per decade and the Central Chile grid point falls by 18.840 mm/month per decade. These are descriptive Theil–Sen slopes. The reviewed paired difference is +24.645 mm/month per decade with a significant OLS slope test. The two individual trends have significance withheld. Read the mission report and submit it to the hearing.
- **Same weather, different ground** · File the Central Arizona rainfall record and predict which way root-zone water will move under changed precipitation. Set precipitation to +100%, leave the other controls at their inherited values, and preview twelve months. **Advance 12 months** saves the run and turns its ending state into the start of the next run. Return to the lab, change only temperature to +4 °C, then preview and advance again. Check the comparison or file the coverage gap. A wrong prediction can complete the case with an explanation.
- **Free investigation** · Choose a preset site, variable, product and available time view in the Earth archive. File a comparable record or document the gap. Climate projections support the six terrain presets. Central Chile is a historical grid point.

Use **Tab** and **Shift+Tab** to focus actions, **Enter** to activate, and the arrow keys to adjust a focused weather control. **Space** skips the descent or pauses playback. **M** toggles sound. On the ground use **WASD** to move and hold the **right mouse button** to look. The playback controls let you select a month, compare inherited weather, inspect tabular values, or discard a preview. The branch controls fork and replay saved revisions. The Investigation desk includes guided demos.

### Read the numbers

The Arizona scenario uses a 150 mm teaching bucket and FAO-56 reference evapotranspiration, driven by monthly NASA POWER climate normals with a documented estimate of temperature range. The 150 mm capacity is uncalibrated. The model starts from a normals-based spin-up, then carries local storage and weather forward. The other five sites and migrated v1 snapshots use illustrative indices. Ground visuals are illustrative throughout. The paired flight shows a historical comparison. The Potomac routing screen and Phoenix benchmark replay remain research checks. [Historical evidence and provenance](../docs/homecoming-evidence.md) · [Routing and forecast verification](../docs/homecoming-routing-forecast.md)

Campaign data and exported JSON reports use Unity's application data folder. On macOS the default is `~/Library/Application Support/NASA Space Apps Kuwait/The Great Homecoming/`. Use **Open saved reports** on the Investigation desk to locate the exported reports. The campaign keeps the active forcing and land history per branch. Previewing or playing a saved run leaves the branch revision at its last committed month.

### Build and check

Run `node scripts/prepare-unity-simulation.mjs --check` from the repository root to verify the pinned pack. Run without `--check` to regenerate it. **Homecoming → Field simulation → Build scene** recreates `Simulation.unity`. **Homecoming → Build macOS player** builds the complete game with `Story.unity` first and `Simulation.unity` second. **Homecoming → Field simulation → Build macOS preview** produces a local player with direct lab entry.

Run the preview executable with `--investigation-preview /absolute/folder` to verify scenario advancement, synchronized February results, a reviewed historical comparison, JSON export and the hearing. Captures and the synthetic player’s report go to the supplied folder. Use `--field-preview /absolute/folder` for the older illustrative playback check. **Homecoming → Field simulation → Validate model and campaign** checks arithmetic and saved-state behavior.

## Lighting and grade

The film is lit by `StoryLighting.cs`, which builds its rig at runtime (never saved into
`Story.unity`, so rebuilding or hand-editing the scene cannot drift from it). It switches
off the builder's two flat directional lights and uses:

- a shadow-casting overhead spot that pools light on the crew and falls off into the room;
- a cold rim light through the observation window;
- the television as a real light source (flickering black-and-white in 1969, cold blue for Artemis);
- rose and teal party lights on the dance beat, and a cyan projector glow in the lab;
- per-shot ambient level and a darker trilight base.

Every shot has its own look, evaluated from shot time like the acting, so pause and seek
stay exact: 1969 warm and hopeful; 2026 dim, cold and desaturated with a dying console bulb;
the switch slam snapping every light back on; Earth rolling from night into daylight
(`_SunDir` on `Homecoming/Planet`, with a brighter, sun-aware atmosphere).

`StoryGrade.cs` (shader `Resources/Shaders/FilmGrade.shader`) is the camera's finishing pass:
bloom, exposure, filmic tone mapping, grade, vignette and grain. The IMGUI interface draws
after it, so captions and menus are never graded. Tune a shot's look in
`StoryLighting.Evaluate`, or the defaults on the `StoryGrade` component.

## Dialogue

A shot can carry timed lines instead of a single `line`:

```json
"lines": [
  { "at": 0.3, "text": "Nyxen: Sir, that’s one human." },
  { "at": 1.75, "text": "Zorvax: The first of many!" }
]
```

Each line shows from `at` until the next line or the end of the shot, eases in, and takes the
speaker's caption colour (Zorvax orange, Nyxen mint, Cadet B paper). Validation requires the
`Speaker: words` form, increasing times, and at least 0.8 s of reading time before the shot ends.

The Unity film extends the web story: it restores the crew's motivation (they think humans
leaving Earth means humans coming to them; Artemis shows humans keep going home; Zorvax sets
out to find what Earth has that their planet doesn't) and adds a line or exchange to most shots.
`scripts/prepare-unity-story.py` keeps these Unity-authored `lines`, the longer shot lengths and
the fuller descriptions when it re-syncs from `src/homecoming/story.ts`.

## Discovery cutscenes

Short comic scenes that play when the player makes a discovery. Scripts are in
`docs/cutscene-scripts.md`; the shot data is in
`Homecoming/Assets/Homecoming/Resources/Cutscenes.json` (kept separate from `Story.json`,
which `prepare-unity-story.py` regenerates).

- **Preview:** Play → **Chapters / options** → *Discovery cutscenes / preview*.
- **From the simulation:** call `Homecoming.StorySession.PlayCutscene("drought");`. The story
  scene plays it, then loads `Simulation` again.
- **Done:** 1, *The Welcome Baskets* (`drought`). Camera: `StoryStage.EvaluateCamera`;
  props and the red alert: `StoryStage.EvaluateCutscene`; acting: `StoryActor.Pose`.
- **Adding one:** add an array to `Cutscenes.json`, a field and a case in `CutsceneScript`
  (`StorySession.cs`), shot ids with a shared prefix, and that prefix in
  `CutsceneScript.IsCutsceneShot`.

## Edit and verify

- **Story text:** `Homecoming/Assets/Homecoming/Resources/Story.json`.
- **Set:** expand `HOMEcoming — film set` in `Story.unity`. Materials and mesh
  assets are in `Assets/Homecoming/Set`.
- **Cameras and blocking:** `StoryStage.cs` and `StoryActor.cs`.
- **Playback:** `StoryDirector.cs`. **Interface:** `StoryInterface.cs`, with bundled
  Barlow Condensed, Barlow and IBM Plex Mono fonts in `Resources/Fonts`.
- The interface uses custom controls, keyboard focus and hover/press feedback.
  Page entrances and signal graphics respect reduced motion.
- Character gestures blend with breathing idles. Shot acting, camera easing,
  switch contact, cannon recoil, confetti drag and balloon follow-through are
  evaluated from shot time, so pausing and seeking remain deterministic.
- **Homecoming → Rebuild story scene** recreates the set from
  `StoryProjectBuilder.cs`. This replaces scene edits, so use it deliberately.
- **Homecoming → Validate and render story frames** checks all shots, cue times,
  character rigs, materials and that Story is the first enabled build scene,
  then renders each shot.
- **Homecoming → Run playback smoke check** enters Play Mode and exercises shot
  seeking, reduced motion, pause, chapter transitions, saving and the return API.
  It restores the previous story save afterwards.

Run `python3 scripts/prepare-unity-story.py` from the repository root to sync the
original story and bundled media into Unity again. It does not rebuild the scene.

The project uses the Built-in Render Pipeline. The official glTFast **6.21.0**
package is pinned in `Packages/manifest.json` and restored from Unity's registry
by Package Manager, along with the other dependencies. Keep the manifest and
`packages-lock.json` in Git. Downloaded package contents, Unity caches, local
editor settings and build output are ignored. Keep `Assets/`, `ProjectSettings/`
and asset `.meta` files in Git so scene references remain intact.

## Source material

- Story and rigs: this repository's Homecoming implementation and `characters/`.
- Apollo 11: [NASA Apollo Lunar Surface Journal](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a11/video11.html).
- Moon maps: [NASA Goddard CGI Moon Kit](https://svs.gsfc.nasa.gov/4720/).
- Earth: existing KU Exchange globe textures in `public/homecoming/planets/`.
- Importer: [Unity glTFast](https://github.com/Unity-Technologies/com.unity.cloud.gltfast).

- Field models: [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) and [Space Kit](https://kenney.nl/assets/space-kit), CC0.
- Field typography: Barlow, Barlow Condensed and Space Mono, SIL Open Font License. License files are bundled with the fonts.
