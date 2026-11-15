# Homecoming fallback voices

These WAV files contain the team's dialogue synthesized locally with [eSpeak NG 1.52.0](https://github.com/espeak-ng/espeak-ng/tree/1.52.0). Zorvax uses the English UK male variant `m3`, Nyxen uses the English US female variant `f2`, and Cadet B uses the English US female variant `f3`. Each file is trimmed and level matched to fit its Unity cue. The player ships the rendered WAVs and plays them offline.

`VoiceTakes.json` selects the ElevenLabs takes in `HomecomingAudio/VoiceElevenLabs` first and uses `fallbackClip` if a take is unavailable. The new performances were generated in Chrome with Eleven v4 on November 15, 2026. Eldrin (Crisp British Baritone) plays Zorvax, Alicia (Polished Global Anchor) plays Nyxen, and Jade (Upbeat and Natural) plays Cadet B. Finley (Articulate Anchor) plays Earth in `HomecomingAudio/broadcast-reply.wav`.

The browser exports were 44.1 kHz MP3s. The Unity assets are mono PCM WAVs, trimmed, level matched, and fitted to the caption windows. All dialogue plays offline.
