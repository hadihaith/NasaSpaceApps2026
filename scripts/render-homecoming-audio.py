"""Render the original Homecoming score and synthesized prop effects as mono PCM."""
from array import array
from math import exp, pi, sin
from pathlib import Path
from random import Random
import sys
from uuid import NAMESPACE_URL, uuid5
from wave import open as open_wave

ROOT = Path(__file__).resolve().parents[1]
RATE = 24000
BEAT = 60 / 84
NOTES = (293.665, 349.228, 329.628, 440.0)  # D4, F4, E4, A4


def track(seconds):
    return [0.0] * round(seconds * RATE)


def layer(samples, offset, duration, instrument):
    first = round(offset * RATE)
    for i in range(min(round(duration * RATE), len(samples) - first)):
        t = i / RATE
        edge = min(1, t / .007, (duration - t) / .035)
        samples[first + i] += edge * instrument(t)


def wave(hz, t):
    return sin(2 * pi * hz * t)


def reed(hz, t):
    vibrato = t + .00035 * wave(5, t)
    return .20 * (wave(hz, vibrato) + .26 * wave(hz * 3, vibrato) + .11 * wave(hz * 5, vibrato))


def phrase(kind):
    lengths = (1, .5, .5, 2)
    count = 4 if kind in ("welcome", "title") else 3
    at = .32 if kind == "opening" else 0
    samples = track(at + sum(lengths[:count]) * BEAT + .16)
    if kind == "opening":
        noise = Noise(1969)
        layer(samples, .075, .12, lambda t: exp(-39 * t) * (
            .13 * noise.air() + .08 * wave(176, t)))
    for hz, beats in zip(NOTES[:count], lengths):
        tail = beats * BEAT
        if kind == "welcome":
            layer(samples, at, tail, lambda t: reed(hz, t) * (.92 + .08 * wave(3, t)))
        elif kind == "title":
            layer(samples, at, tail + .13, lambda t: exp(-2.4 * t) * (
                .18 * wave(hz, t) + .055 * wave(hz * 2, t) + .04 * wave(hz / 2, t)))
        else:
            gain = .45 if kind == "hearing" else .72
            layer(samples, at, tail + .06, lambda t: gain * exp(-4 * t) * (
                .13 * wave(hz, t) + .034 * wave(hz * 2, t)))
        at += tail
    return samples


def archive():
    samples = track(16 * BEAT)
    noise = Noise(13)
    for beat, hz in zip((0, 3, 6, 8, 11, 14), (73.416, 110, 82.407, 73.416, 87.307, 110)):
        layer(samples, beat * BEAT, .92, lambda t: exp(-3 * t) * (
            .105 * wave(hz, t) + .025 * wave(hz * 2, t)))
    for beat in (1, 2.5, 5, 7.5, 9, 10.5, 13, 15.5):
        layer(samples, beat * BEAT, .19, lambda t: .048 * exp(-13 * t) * noise.air())
    return samples


class Noise:
    def __init__(self, seed):
        self.random = Random(seed)
        self.low = 0

    def next(self):
        value = self.random.uniform(-1, 1)
        self.low += .13 * (value - self.low)
        return value

    def air(self):
        return self.next() - self.low

    def body(self):
        self.next()
        return self.low


def props():
    noise = Noise(1969)
    cannon = track(.66)
    layer(cannon, 0, .31, lambda t: exp(-17 * t) * (.46 * noise.air() + .19 * wave(107, t)))
    layer(cannon, .013, .29, lambda t: exp(-12 * t) * .31 * wave(127 - 108 * t, t))
    for at in (.055, .093, .16, .23, .34, .42):
        layer(cannon, at, .19, lambda t: .10 * exp(-18 * t) * noise.air())

    cough = track(.31)
    for at, gain in ((0, 1), (.072, .56)):
        layer(cough, at, .19, lambda t: gain * exp(-23 * t) * (
            .29 * noise.air() + .19 * wave(182 - 235 * t, t)))

    rattle = track(.38)
    for at in (0, .046, .127, .248):
        layer(rattle, at, .11, lambda t: exp(-32 * t) * (
            .14 * noise.air() + .035 * wave(160, t)))

    projector = track(1.35)
    layer(projector, 0, .085, lambda t: exp(-55 * t) * (.26 * noise.air() + .12 * wave(190, t)))
    layer(projector, .035, 1.28, lambda t: (.046 * wave(63 + 35 * t, t)
        + .023 * wave(119 + 63 * t, t) + .061 * noise.body()) * min(t * 5, 1) * exp(-1.1 * t))

    detent = track(.21)
    for at, gain in ((0, 1), (.043, .47)):
        layer(detent, at, .15, lambda t: gain * exp(-42 * t) * (
            .21 * noise.air() + .17 * wave(155, t) + .094 * wave(1018, t)))

    paper = track(.57)
    layer(paper, 0, .16, lambda t: exp(-24 * t) * (.18 * noise.air() + .12 * wave(133, t)))
    for at in (.047, .079, .132, .193, .252, .331):
        layer(paper, at, .18, lambda t: exp(-16 * t) * (.13 * noise.air() + .022 * wave(670, t)))

    balloon = track(1.68)
    layer(balloon, 0, 1.58, lambda t: exp(-1.34 * t) * (
        (.17 + .064 * wave(13, t)) * wave(880 - 248 * t + 18 * wave(7, t), t)
        + .041 * noise.air() + .044 * wave(636 - 188 * t, t)))
    layer(balloon, 1.43, .23, lambda t: .09 * exp(-14 * t) * noise.air())

    return {
        "party-hit": cannon, "sputter": cough, "rattle": rattle, "projector": projector,
        "detent": detent, "paper": paper, "deflate": balloon,
    }


def write_audio(path, samples):
    pcm = array("h", (round(max(-1, min(1, sample)) * 32767) for sample in samples))
    if sys.byteorder != "little":
        pcm.byteswap()
    with open_wave(str(path), "wb") as output:
        output.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        output.writeframes(pcm.tobytes())


def write_meta(path, body):
    guid = uuid5(NAMESPACE_URL, f"homecoming/{path.name}").hex
    path.with_name(f"{path.name}.meta").write_text(f"fileFormatVersion: 2\nguid: {guid}\n{body}")


def main():
    unity = ROOT / "unity/Homecoming/Assets/Homecoming/Resources/HomecomingAudio"
    unity.mkdir(parents=True, exist_ok=True)
    write_meta(unity, "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n")
    assets = {kind: phrase(kind) for kind in ("opening", "welcome", "title", "hearing")}
    assets.update(props())
    assets["archive"] = archive()
    for name, samples in assets.items():
        filename = f"{name}.wav"
        write_audio(unity / filename, samples)
        write_meta(unity / filename, """AudioImporter:
  externalObjects: {}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    compressionFormat: 0
    quality: 1
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
""")


if __name__ == "__main__":
    main()
