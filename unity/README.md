# The Great Homecoming — Unity story

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

- **Play transmission** begins the prologue, followed by recruitment and the lab
  introduction. The story then stops at the laboratory handoff.
- **Chapters / options** lets you replay sections and preview the ending now.
  Previewing a chapter does not mark it complete.
- **Space** pauses, **left/right arrows** change shots, **M** toggles sound,
  **C** toggles dialogue captions, and **Esc** pauses, then returns to the menu.
- Scene descriptions and reduced camera motion are available in playback options.
- Designation, story progress and options are saved locally using `PlayerPrefs`.
  **Start a new operation** clears only this story's designation and progress.
- The original Apollo clip is bundled. Its archival still is used if the video
  decoder is unavailable. Dialogue uses subtitles; no generated voices are included.

## Add the simulation later

No simulation scene, climate model, sliders, archive, evidence or invented results
are included. The ending can be previewed independently.

1. Create **`Assets/Scenes/Simulation.unity`** in this project.
2. Add it to the active **Build Profile → Scene List**, after `Story.unity`.
3. The laboratory's **Enter laboratory** button becomes available automatically.
4. Read `Homecoming.StorySession.RecruitName` in that scene.
5. When the simulation has actually earned its report, call:

   ```csharp
   Homecoming.StorySession.ReturnFromSimulation();
   ```

   This loads `Story` and plays the final hearing, then the epilogue. The story
   does not determine whether experiments or evidence meet the future game's rules.

The destination scene name is exposed on the `StoryDirector` component. Keep
the story scene named **Story** unless you also update `StorySession.StoryScene`.

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
  character rigs, materials and scene isolation, then renders each shot.
- **Homecoming → Render frames of one shot** renders a single shot as a frame sequence
  (default: the Artemis shot every 0.1 s) into `docs/unity-previews/frames-<shot>/`, to
  inspect motion glitches frame by frame. In batch mode, set `HOMECOMING_SHOT`,
  `HOMECOMING_FROM`, `HOMECOMING_TO`, `HOMECOMING_STEP` and `HOMECOMING_PREVIEW_PATH`.
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
