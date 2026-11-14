export type Cue = "click" | "radio" | "celebrate" | "scan" | "discover" | "quiet" | "change" | "stamp" | "voice" | "alarm" | "orbit" | "party-hit" | "sputter" | "deflate";

const phrases: Record<Cue, [number, number, number, OscillatorType][]> = {
  click: [[0, 650, .055, "sine"]],
  radio: [[0, 221, .08, "square"], [.09, 348, .07, "triangle"], [.21, 620, .18, "sine"]],
  celebrate: [[0, 392, .19, "triangle"], [.11, 494, .20, "triangle"], [.22, 587, .22, "triangle"], [.34, 784, .47, "triangle"], [.34, 196, .44, "sine"]],
  scan: [[0, 108, .30, "sine"], [.20, 177, .25, "sine"], [.43, 288, .33, "triangle"], [.68, 524, .26, "sine"]],
  discover: [[0, 330, .24, "triangle"], [.10, 495, .27, "triangle"], [.21, 660, .34, "triangle"], [.34, 988, .51, "sine"]],
  quiet: [[0, 369, .13, "sine"], [.15, 294, .27, "sine"]],
  change: [[0, 350, .17, "sine"], [.16, 523, .27, "triangle"]],
  orbit: [[0, 73.42, .9, "sine"], [.14, 110, .85, "triangle"], [.32, 146.83, .68, "sine"]],
  "party-hit": [[0, 49, .22, "triangle"], [.028, 144, .13, "sine"]],
  sputter: [[0, 180, .10, "sawtooth"], [.1, 97, .12, "triangle"]],
  deflate: [[0, 685, .48, "triangle"], [.16, 373, .23, "sine"]],
  stamp: [[0, 107, .22, "triangle"], [.12, 440, .17, "sine"], [.23, 660, .28, "triangle"]],
  voice: [[0, 288, .065, "triangle"], [.065, 390, .057, "triangle"], [.14, 242, .06, "sine"]],
  alarm: [[0, 218, .17, "triangle"], [.21, 175, .21, "triangle"]],
};

export function makeAudio() {
  let context: AudioContext | undefined;
  let output: GainNode | undefined;
  let bed: GainNode | undefined;
  function tone(time: number, hz: number, length: number, wave: OscillatorType, gain: number, target: AudioNode, pitchBend = 1) {
    if (!context) return;
    const osc = context.createOscillator();
    const env = context.createGain();
    osc.type = wave;
    osc.frequency.setValueAtTime(hz, time);
    if (pitchBend !== 1) osc.frequency.exponentialRampToValueAtTime(hz * pitchBend, time + length);
    env.gain.setValueAtTime(.0001, time);
    env.gain.exponentialRampToValueAtTime(gain, time + (length > .6 ? .16 : .014));
    env.gain.exponentialRampToValueAtTime(.0001, time + length);
    osc.connect(env).connect(target);
    osc.start(time);
    osc.stop(time + length + .02);
  }
  return {
    async enable() {
      if (!context) {
        context = new AudioContext();
        output = context.createGain();
        output.connect(context.destination);
        bed = context.createGain();
        bed.gain.value = .16;
        bed.connect(output);
        [55, 82.41, 110.25].forEach((hz, n) => {
          const hum = context!.createOscillator();
          const level = context!.createGain();
          hum.frequency.value = hz;
          level.gain.value = .08 / (n + 1);
          hum.connect(level).connect(bed!);
          hum.start();
        });
      }
      await context.resume();
      output!.gain.setTargetAtTime(.68, context.currentTime, .04);
    },
    mute() { if (context && output) output.gain.setTargetAtTime(0, context.currentTime, .035); },
    close() { void context?.close(); context = undefined; },
    cue(name: Cue) {
      if (context?.state !== "running" || !output) return;
      const time = context.currentTime;
      for (const [offset, hz, length, wave] of phrases[name]) {
        tone(time + offset, hz, length, wave, wave === "square" || wave === "sawtooth" ? .027 : .10, output, name === "deflate" ? .38 : 1);
      }
      if (name === "radio" || name === "celebrate" || name === "stamp" || name === "party-hit" || name === "sputter") {
        const length = name === "radio" ? .46 : name === "party-hit" ? .47 : .26;
        const buffer = context.createBuffer(1, Math.ceil(context.sampleRate * length), context.sampleRate);
        for (let i = 0; i < buffer.length; i++) buffer.getChannelData(0)[i] = Math.random() * 2 - 1;
        const noise = context.createBufferSource();
        noise.buffer = buffer;
        const filter = context.createBiquadFilter();
        filter.type = "bandpass";
        filter.frequency.value = name === "radio" ? 1550 : 520;
        const gain = context.createGain();
        gain.gain.setValueAtTime(name === "radio" ? .08 : .16, time);
        gain.gain.exponentialRampToValueAtTime(.0001, time + length);
        noise.connect(filter).connect(gain).connect(output);
        noise.start();
      }
    },
  };
}
