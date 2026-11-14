# Unity story verification

Validated with the installed **Unity 6000.6.9f1** on macOS.

- Story: the **14 shots** and their cue sounds come from `src/homecoming/story.ts`.
  The Unity film extends them with timed multi-line dialogue, fuller descriptions and
  longer shots to read the new lines (see `unity/README.md`, **Dialogue**).
- Lighting: the shot renders here (not `player/`) show the per-shot lighting and film
  grade from `StoryLighting.cs` and `StoryGrade.cs`. Validation also checks dialogue
  format, order and reading time.
- All three GLB character files are byte-identical to the originals.
- Scene validation passed: skinned rigs, animation clips, supported materials,
  valid cue times, and only the Story scene in the build.
- Play Mode smoke check passed: all shots, pause/resume, full/reduced motion,
  chapter transitions, local progress, preview isolation, and an actual scene
  reload through `StorySession.ReturnFromSimulation()` into the final hearing.
- Standalone macOS development player built successfully.
- The player's own frame exporter captured title, dialogue, recruitment,
  laboratory handoff, hearing, epilogue, options and credits. The final title fit
  and recruitment composition were visually checked and corrected.
- The interface now uses Barlow Condensed, Barlow and IBM Plex Mono, with custom
  controls, focus/hover/press feedback and restrained page entrances. Font sources
  and open licenses are bundled in the project.
- The animation pass checks repeatable bone poses after seeking through other
  shots, and measures the switch-press wrist position against the physical prop.
  Gestures blend into idles; report grip, recoil, confetti and balloon follow-through
  are staged from the same shot timeline.

`player/` contains actual game frames, including the development-build watermark.
The other PNGs are scene-camera renders for inspecting individual shots.
`contact-sheet.jpg` combines four actual player frames.

Unity's batch editor emitted a Quick Search indexing exception during startup.
The story smoke check completed successfully afterward. The standalone capture
run logged no story exceptions. This is an editor startup quirk, not a failed
story check.

To regenerate: use the **Homecoming** editor menu described in
[`unity/README.md`](../../unity/README.md). The optional development player can
export its own UI frames when launched with `--homecoming-capture`; captures go
to the system temporary directory's `homecoming-captures` folder. Ordinary play
does not activate that exporter.
