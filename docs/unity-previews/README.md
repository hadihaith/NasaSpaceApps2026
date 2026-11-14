# Unity story verification

Validated with the installed **Unity 6000.6.9f1** on macOS.

- Original source comparison: all **14 shots**, dialogue, descriptions, cue timing
  and shot durations match `src/homecoming/story.ts`.
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
