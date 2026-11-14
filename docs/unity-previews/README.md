# Unity story verification

The cinematic pass adds the orbital window, practical bridge details, live
instruments, metal reflections, projector beam, drifting air and selective focus.
Player captures were reviewed as well as editor camera renders. Display-space
contrast and grain retain the dim hull's detail. The new score's four waveforms
were checked for finite, non-clipping samples, and atmospheric replay was checked
against the same shot time. The existing discovery cutscene and simulation-return
path are included in the playback smoke check.

Zorvax's Artemis reaction now respects the GLB's facial frame range (0–1).
The former value of 70 stretched the GPU-skinned face across the screen.
Both complaint performances are checked at 30 samples per second for five seconds,
including their return to idle. Twelve standalone-player frames of shot 8/11 were
visually reviewed; `player/cinema-return.png` shows the corrected reaction.

Validated with the installed **Unity 6000.6.9f1** on macOS.

- Story: the **14 shots** and their cue sounds come from `src/homecoming/story.ts`.
  The Unity film extends them with timed multi-line dialogue, fuller descriptions and
  longer shots to read the new lines (see `unity/README.md`, **Dialogue**).
- Lighting: the shot renders here (not `player/`) show the per-shot lighting and film
  grade from `StoryLighting.cs` and `StoryGrade.cs`. Validation also checks dialogue
  format, order and reading time.
- All three GLB character files are byte-identical to the originals.
- Scene validation passed: skinned rigs, animation clips, supported materials,
  valid cue times, and Story first in the build with the laboratory available.
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
