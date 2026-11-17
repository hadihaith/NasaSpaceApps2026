# The Great Homecoming — Unity

Open **`unity/Homecoming`** in Unity Hub with **6000.6.9f1**. Open
`Assets/Scenes/Intro.unity` and press **Play**. The Unity macOS player is the Homecoming release.

On another computer, clone the repository and add the `unity/Homecoming` folder
in Unity Hub using **6000.6.9f1**. Allow the first import and Package Manager restore
to finish (internet is needed to download registry dependencies). The repository
includes `Assets/` with its `.meta` files, `ProjectSettings/`, and `Packages/`,
including any embedded packages. Unity regenerates `Library/` and other local
caches; copying those folders or installing the website's Node dependencies is
not required to open the Unity project.

The game opens in a separate 2D scene with the first 17 seconds of the AUK Space Apps title film.
The story scene loads in the background and activates at the cutoff. The 14-shot story follows
Apollo 11, the crew's misunderstanding, the party,
the 57-year wait, Artemis, recruitment, the laboratory introduction, the final
hearing and the deflating balloon. Recruitment and the epilogue retain the
original wording. The existing three skinned GLB characters and their animation
clips are imported using Unity's official glTFast package. The bridge and props
are real, editable scene objects. No generated backgrounds or new character art
are used.

The macOS player presents the original Rec.709 MP4 through AVFoundation in its
game window. The movie lives in `Assets/StreamingAssets/auk-intro.mp4` and is
copied into the build as-is. Unity's Game view draws the decoded frames in a
full-screen pass on the intro camera. Press **Escape** or **Right Arrow** to
skip the movie.
After editing the native player, run `scripts/build-macos-intro-plugin.sh` from
the repository root to rebuild the bundled Apple Silicon and Intel plug-in.

## Play

- Press **R** anywhere to clear all local gameplay saves, reports and settings and restart from the opening video.
- **Start story** begins the prologue, recruitment and lab introduction.
  **Open weather lab** leads directly to the weather controls. Change temperature, rain,
  humidity or wind and choose **Run test · 12 months**. Completed tests save automatically
  and open their results. **Next simulation** starts with the saved ground and weather.
  **View saved tests** opens earlier records and their frozen evidence snapshots.
- After a test, the **Kuwait / United Kingdom** tab shows matched NASA POWER rainfall
  records near Kuwait City and London for 1991–2025. **T-test & relationship** shows
  the OLS slope t-test of the yearly Kuwait-minus-UK difference and the Pearson correlation.
  This test is exploratory and its statistical assumptions await review. The city grid points
  and historical observations are identified on screen. The earlier Earth-call activity has been removed.
- **Story & settings** lets you replay sections and preview the ending now.
  Previewing a chapter does not mark it complete.
- **Space** pauses, **left/right arrows** change shots, **M** toggles sound,
  **C** toggles dialogue captions, and **Esc** pauses, then returns to the menu.
- Scene descriptions and reduced camera motion are available in playback options.
- **Start a new operation** in **Story & settings** asks for confirmation before it clears your local designation, story and broadcast progress, saved experiments, and in-game casebook. Your exported report files and settings stay available.
- During the AUK intro, **Space** pauses, **Right Arrow** or **Esc** skips, and **M** toggles sound.
- The AUK and Apollo clips are bundled. The title card and archival still are available
  when their videos cannot play. Dialogue has captions and uses the packaged voice takes.
- The current spoken takes use local eSpeak NG synthesis. `VoiceTakes.json` lists the
  cue IDs and fallback clips. A licensed primary audio clip can be assigned per cue.

## Field simulation

Open **`Assets/Scenes/Intro.unity`** for the full opening and choose **Open field station** after the briefing. Open **`Assets/Scenes/Story.unity`** to preview the story directly or **`Assets/Scenes/Simulation.unity`** to start at the field station. The curated records and scenarios work offline.

The field station opens on a suggested first case or resumes your current one. The case path highlights the current step and checks off completed steps as you play.

### Play the cases

- **Rainfall in two places** · The optional case compares Kuwait City and London over 1991–2025. Predict how rainfall changed, inspect the matched years and their difference, then file the findings. The exploratory two-sided OLS slope t-test of the Kuwait-minus-UK difference gives t(33) = −1.233 and p = 0.226. Its assumptions await review. The year-to-year Pearson correlation is −0.040.
- **Is Phoenix warming?** · Make a call, open the 1981–2025 record, and examine the fit and its uncertainty before filing. The descriptive slope is +0.509 °C per decade across 45 complete years. The reviewed OLS estimate is +0.468 °C per decade.
- **Weather and soil** · File the Central Arizona rainfall record and predict how root-zone water will respond. Change only precipitation and preview twelve months. **Save and advance one year** makes the ending ground and weather the start of the next test. Keep that rain setting and change only temperature for the follow-up. Try +100% precipitation and +4 °C from a fresh setup. Check the historical comparison or file the coverage gap, then use the Earth Intelligence Report's monthly log to interpret the follow-up and file the case. A wrong prediction or readings that stay within tolerance can complete the case with an explanation.
- **Your own investigation** · Choose a place, measurement, product and available time view in the Earth archive. File a comparable record or document the gap. Climate projections support the six terrain presets. Central Chile is a historical grid point.
After three discoveries and one completed Earth-data investigation, select **Incoming · Commander Zorvax** on the field station. **Submit findings** plays the final hearing. Nyxen cues the monitor to show the linked simulation and Earth record, followed by up to four recent saved runs. **Read my report** on the final status screen opens the campaign summary and its evidence links.

Use **Tab** and **Shift+Tab** to focus actions, **Enter** to activate, and the arrow keys to adjust a focused weather control or move along the playback timeline. **Space** skips the descent or pauses playback. **M** toggles sound. On the ground use **WASD** to move and hold the **right mouse button** to look. The playback controls let you select a month, compare inherited weather, inspect tabular values, or discard a preview. **Saved experiments** in the weather lab lets you copy an experiment, replay its last run, or compare two runs. The rainfall and soil cases include guided demos.

The weather console shows inherited readings and draft readings on the same
monitor. Drag a dial or focus it and use the arrow keys. **Preview 12 months**
opens the simulation playback. **Save and advance one year** saves the weather
and resulting ground conditions to the active experiment.

**Change site** opens the Earth map. Select a pin to pull back from the terrain,
cross the KU Exchange Earth globe, and descend to the destination. **Space**
skips the flight and **Esc** cancels it.
Reduced motion changes sites immediately.

### Read the numbers

The Arizona scenario uses a 150 mm teaching bucket and FAO-56 reference evapotranspiration, driven by monthly NASA POWER climate normals with a documented estimate of temperature range. The 150 mm capacity is uncalibrated. The model starts from a normals-based spin-up, then carries local storage and weather forward. The other five sites and migrated v1 snapshots use illustrative indices. Ground visuals are illustrative throughout. The paired flight shows a historical comparison. The Potomac routing screen and Phoenix benchmark replay remain research checks. [Historical evidence and provenance](../docs/homecoming-evidence.md) · [Routing and forecast verification](../docs/homecoming-routing-forecast.md)

Campaign data and exported JSON reports use Unity's application data folder. On macOS the default is `~/Library/Application Support/NASA Space Apps Kuwait/The Great Homecoming/`. Use **Open saved reports** on the case picker to locate the exported reports. Each experiment keeps its own weather and land history. A completed weather-lab test saves its full record and final ground state before the next simulation. Replaying a saved result opens its recorded comparison.

In **Weather lab → Saved experiments**, choose **Reset Earth** to start a fresh experiment at the January 2026 baseline. Your casebook, discoveries, and previous experiments stay available. Switch to an earlier experiment to replay its saved run or continue a case from its original conditions.

### Build and check

Run `node scripts/prepare-unity-simulation.mjs --check` from the repository root to verify the pinned pack. Run without `--check` to regenerate it. **Homecoming → Field simulation → Build scene** recreates `Simulation.unity`. **Homecoming → Build macOS player** builds the scenes in launch order `Intro`, `Story`, and `Simulation`. **Homecoming → Field simulation → Build macOS preview** produces a local player with direct lab entry.

Run **Homecoming → Field simulation → Validate model and campaign** and
**Homecoming → Run playback smoke check** before building the release. The latter
enters Play Mode and can be run in batch mode without `-quit`.
Use **Homecoming → Field simulation → Validate reset behavior** to check both campaign reset paths against temporary saves.

For end-to-end QA, **Homecoming → Field simulation → Build story-first macOS QA player**
places a separate app in `Builds/`. In an isolated project copy, use a different
Player Settings product name and bundle identifier to give QA its own campaign
and preferences. Launch the QA executable with `--investigation-preview /absolute/folder`,
then finish or skip the prologue, join as a test recruit, and enter the lab. The
harness exercises two consecutive Arizona scenarios, the matched rainfall
comparison, a revised prediction, JSON export and the historical hearing. It emits
`INVESTIGATION_STORY_ENTRY_OK` and `INVESTIGATION_PLAYBACK_OK` and writes the
captures and report to the supplied folder. The direct-lab preview also accepts
`--investigation-preview`. Use `--field-preview /absolute/folder` for the older
illustrative playback check. Package the story-first `.app` from **Build macOS player**.

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

### Bridge atmosphere and sound

`StoryEnvironment.cs` adds the sealed observation window, Earth below the ship,
pressure lines, ventilation panels, live console instruments, practical lamps and
worn metal deck at runtime. The original scene layout is preserved. A local reflection
probe gives the metal its surrounding colours. The projector has a light volume,
and a single small mesh handles drifting dust and the cannon's air puffs.
The bridge uses bundled PBR surface shaders for its runtime lighting; tools restore
the original material assignments when the generated dressing is torn down.

Close-ups use selective background focus; wide shots, the archival television and
planet shots retain clarity. Focus, instrument motion, air effects and grain use the
shot clock, so seeking and pausing remain repeatable. Reduced motion steadies the
instruments and removes the transient air puffs. The scene explicitly supplies its
ambient diffuse lighting in player builds as well as in the editor.
Contrast and grain are applied in display space to retain detail in the dim hull.

`StorySound.cs` provides ventilation and hull resonance, spatial prop effects and four
original instrumental themes generated locally. Music crossfades between hopeful,
waiting, determined and laboratory moods, and ducks beneath dialogue. The balloon
ending deliberately goes quieter. Interface sounds have a separate channel, so menu
clicks remain responsive while the film is paused. The existing sound toggle mutes
every layer. The original score and prop cues are generated locally.

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

Unity owns the authored timeline in `Resources/Story.json`. Keep line IDs stable across
the timeline and `Resources/VoiceTakes.json` so individual performances can be replaced.

## Discovery cutscenes

Short comic scenes that can play after a discovery. Scripts are in
`docs/cutscene-scripts.md`. The Unity shot data lives in
`Homecoming/Assets/Homecoming/Resources/Cutscenes.json`.

- **Preview:** Play → **Story & settings** → *Discovery cutscenes / preview*.
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
- **Homecoming → Render frames of one shot** renders a single shot as a frame sequence
  (default: the return shot every 0.1 s) into `docs/unity-previews/frames-<shot>/`, to
  inspect motion glitches frame by frame. In batch mode, set `HOMECOMING_SHOT`,
  `HOMECOMING_FROM`, `HOMECOMING_TO`, `HOMECOMING_STEP` and `HOMECOMING_PREVIEW_PATH`.
- **Homecoming → Run playback smoke check** enters Play Mode and exercises shot
  seeking, reduced motion, pause, chapter transitions, saving and the return API.
  It restores the previous story save afterwards.

Run `python3 scripts/prepare-unity-story.py` from the repository root to sync
crew models and planet textures. Edit the Unity timeline in `Resources/Story.json`.

The project uses the Built-in Render Pipeline. The official glTFast **6.21.0**
package is pinned in `Packages/manifest.json` and restored from Unity's registry
by Package Manager, along with the other dependencies. Keep the manifest and
`packages-lock.json` in Git. Downloaded registry packages inside `Library/`, Unity
caches, local editor settings and build output are ignored. Embedded package source
inside `Packages/` is included in version control. Keep `Assets/`, `ProjectSettings/`
and asset `.meta` files in Git so scene references remain intact.

## Source material

- Story and rigs: this repository's Homecoming implementation and `characters/`.
- Apollo 11: [NASA Apollo Lunar Surface Journal](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a11/video11.html).
- Moon maps: [NASA Goddard CGI Moon Kit](https://svs.gsfc.nasa.gov/4720/).
- Earth: existing KU Exchange globe textures in `public/homecoming/planets/`.
- Importer: [Unity glTFast](https://github.com/Unity-Technologies/com.unity.cloud.gltfast).

- Field models: [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) and [Space Kit](https://kenney.nl/assets/space-kit), CC0.
- Field typography: Barlow, Barlow Condensed and Space Mono, SIL Open Font License. License files are bundled with the fonts.
