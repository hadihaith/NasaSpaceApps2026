# Homecoming fallback voices

These WAV files contain the team's dialogue synthesized locally with [eSpeak NG 1.52.0](https://github.com/espeak-ng/espeak-ng/tree/1.52.0). Zorvax uses the English UK male variant `m3`, Nyxen uses the English US female variant `f2`, and Cadet B uses the English US female variant `f3`. Each file is trimmed and level matched to fit its Unity cue. The player ships the rendered WAVs and plays them offline.

`VoiceTakes.json` points to these files through `fallbackClip`. To add an approved replacement, set `unityClip` to the new Unity Resources path without its extension. The original fallback still plays if the replacement is unavailable. Review the replacement against the shot's caption window before release.
