# The Great Homecoming — Unity story

Open **`unity/Homecoming`** in Unity Hub with **6000.6.9f1**. Open
`Assets/Scenes/Story.unity` and press **Play**. This is a complete Unity project,
separate from the existing web app.

The story retains the original 14 shots, timings and dialogue in
`src/homecoming/story.ts`: Apollo 11, the crew's misunderstanding, the party,
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
  character rigs, materials and scene isolation, then renders each shot.
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
