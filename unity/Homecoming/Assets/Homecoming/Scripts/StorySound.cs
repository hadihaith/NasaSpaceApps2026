using System.Collections.Generic;
using UnityEngine;

namespace Homecoming
{
    // A small original soundscape, synthesised locally: ventilation, engine bed, warm
    // instrumental cues and physical impacts. No voice synthesis or external downloads.
    [DefaultExecutionOrder(-100), RequireComponent(typeof(AudioSource))]
    public sealed class StorySound : MonoBehaviour
    {
        const int Rate = 24000;
        readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        readonly List<AudioClip> generated = new List<AudioClip>();
        readonly List<AudioSource> sources = new List<AudioSource>();
        readonly AudioClip[] themes = new AudioClip[4];
        AudioSource source, controls, room, air, scoreA, scoreB;
        StoryStage stage;
        string currentShot = "home";
        int theme = -1;
        bool aIsTarget = true, paused;
        float quietUntil;
        public bool Muted { get; private set; }

        void Awake()
        {
            controls = GetComponent<AudioSource>();
            Configure(controls, .42f, 0);
            source = NewSource("Spatial props", Vector3.zero, .6f, .25f); source.minDistance = 4;
            room = NewSource("Hull resonance", new Vector3(0, -.7f, -1), .36f, .35f);
            air = NewSource("Cabin ventilation", new Vector3(-3.5f, 3.8f, -1.8f), .24f, .25f);
            scoreA = NewSource("Score A", Vector3.zero, 0, 0);
            scoreB = NewSource("Score B", Vector3.zero, 0, 0);
            room.clip = MakeAmbience(false); air.clip = MakeAmbience(true);
            room.loop = air.loop = true;
            room.Play(); air.Play();
            for (int i = 0; i < themes.Length; i++) themes[i] = MakeScore(i);
            SetMuted(PlayerPrefs.GetInt("homecoming.unity.mute", 0) == 1);
        }
        void Configure(AudioSource target, float volume, float spatial)
        {
            target.playOnAwake = false; target.volume = volume; target.spatialBlend = spatial;
            target.dopplerLevel = 0; target.minDistance = 3; target.maxDistance = 24;
            target.rolloffMode = AudioRolloffMode.Logarithmic;
            sources.Add(target);
        }
        AudioSource NewSource(string name, Vector3 position, float volume, float spatial)
        {
            var go = new GameObject(name + " audio"); go.transform.SetParent(transform, false); go.transform.localPosition = position;
            var audioSource = go.AddComponent<AudioSource>(); Configure(audioSource, volume, spatial); return audioSource;
        }
        AudioClip Clip(string name, float[] samples, int channels)
        {
            var clip = AudioClip.Create(name, samples.Length / channels, channels, Rate, false);
            clip.SetData(samples, 0); generated.Add(clip); return clip;
        }
        AudioClip MakeAmbience(bool ventilation)
        {
            const int seconds = 8;
            var samples = new float[Rate * seconds];
            var random = new System.Random(ventilation ? 57 : 1969);
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)random.NextDouble() * 2 - 1;
                filtered += (noise - filtered) * (ventilation ? .025f : .006f);
                float periodic = Mathf.Sin(t * Mathf.PI * 2 * 47) * .016f + Mathf.Sin(t * Mathf.PI * 2 * 71) * .009f;
                float value = ventilation ? filtered * .16f : filtered * .12f + periodic;
                // Crossfade the noise into a clean loop, avoiding a click at the seam.
                float edge = Mathf.Min(1, Mathf.Min(t, seconds - t) / .08f);
                samples[i] = value * edge;
            }
            return Clip(ventilation ? "Ventilation behind the consoles" : "Distant hull and engine resonance", samples, 1);
        }
        static float Note(float midi) => 440 * Mathf.Pow(2, (midi - 69) / 12);
        AudioClip MakeScore(int mood)
        {
            const int seconds = 16;
            var samples = new float[Rate * seconds * 2];
            int[] roots = mood == 1 ? new[] { 45, 41, 38, 40 } : mood == 3 ? new[] { 48, 53, 55, 60 } : new[] { 48, 45, 41, 43 };
            for (int i = 0; i < Rate * seconds; i++)
            {
                float t = i / (float)Rate, local = t % 4;
                int bar = Mathf.Min(3, (int)(t / 4));
                bool minor = mood == 1 && bar != 1 || mood != 1 && bar == 1;
                float root = roots[bar], third = root + (minor ? 3 : 4), fifth = root + 7;
                float envelope = Mathf.SmoothStep(0, 1, local / .55f) * Mathf.SmoothStep(0, 1, (4 - local) / .8f);
                float pad = (Mathf.Sin(t * 2 * Mathf.PI * Note(root)) + .7f * Mathf.Sin(t * 2 * Mathf.PI * Note(third)) +
                    .6f * Mathf.Sin(t * 2 * Mathf.PI * Note(fifth))) * .035f * envelope;
                float bass = Mathf.Sin(t * 2 * Mathf.PI * Note(root - 12)) * .023f * envelope;
                float pluck = 0;
                if (bar == 0 || bar == 3 || mood == 2)
                {
                    int beat = (int)(local / .5f);
                    float age = local % .5f;
                    float pitch = root + 12 + (beat % 4 == 0 ? 0 : beat % 4 == 1 ? (minor ? 3 : 4) : beat % 4 == 2 ? 7 : 12);
                    float frequency = Note(pitch);
                    pluck = (Mathf.Sin(age * 2 * Mathf.PI * frequency) + .18f * Mathf.Sin(age * 4 * Mathf.PI * frequency)) *
                        Mathf.Exp(-age * 7) * Mathf.Min(1, age * 200) * (mood == 1 ? .013f : .027f);
                }
                float spread = Mathf.Sin(t * Mathf.PI * .5f) * .06f;
                samples[i * 2] = (pad + bass + pluck) * (1 + spread);
                samples[i * 2 + 1] = (pad + bass + pluck * .8f) * (1 - spread);
            }
            float peak = 0;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            float gain = .45f / Mathf.Max(.001f, peak);
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            return Clip("Homecoming instrumental theme " + mood, samples, 2);
        }
        public void SetShot(string id, StoryStage owner)
        {
            stage = owner; currentShot = id;
            quietUntil = 0;
            int next = id == "years" || id == "return" || id == "deflate" || id == "epilogue" ? 1 :
                id == "earth" || id == "launch" || id == "title" ? 2 : id == "lab" || id == "ready" ? 3 : 0;
            if (next == theme) return;
            theme = next;
            aIsTarget = !aIsTarget;
            var target = aIsTarget ? scoreA : scoreB;
            target.clip = themes[next]; target.loop = true; target.volume = 0; target.Play();
            if (paused) target.Pause();
        }
        public void Evaluate(string id, float t, bool dialogue)
        {
            if (source == null || paused) return;
            bool space = id == "signal" || id == "earth";
            bool quiet = Time.unscaledTime < quietUntil || id == "deflate";
            float duck = dialogue ? .67f : 1;
            float score = (id == "title" ? .42f : id == "party" ? .3f : .21f) * duck * (quiet ? .2f : 1);
            scoreA.volume = Mathf.MoveTowards(scoreA.volume, aIsTarget ? score : 0, Time.unscaledDeltaTime * .3f);
            scoreB.volume = Mathf.MoveTowards(scoreB.volume, aIsTarget ? 0 : score, Time.unscaledDeltaTime * .3f);
            room.volume = Mathf.MoveTowards(room.volume, space ? .015f : quiet ? .15f : id == "launch" && t > .74f ? .5f : .36f, Time.unscaledDeltaTime);
            air.volume = Mathf.MoveTowards(air.volume, space ? 0 : .24f, Time.unscaledDeltaTime);
        }
        public void SetMuted(bool mute)
        {
            Muted = mute;
            foreach (var audioSource in sources) if (audioSource != null) audioSource.mute = mute;
            PlayerPrefs.SetInt("homecoming.unity.mute", mute ? 1 : 0);
        }
        public void SetPaused(bool value)
        {
            paused = value;
            foreach (var audioSource in sources)
            {
                if (audioSource == null || audioSource == controls) continue;
                if (value) audioSource.Pause(); else audioSource.UnPause();
            }
        }
        public void StopCue() { if (source != null) source.Stop(); }
        public void Cue(string name)
        {
            if (Muted || name == "voice") return;
            if (name == "quiet") { quietUntil = Time.unscaledTime + .6f; return; }
            if (!sounds.TryGetValue(name, out var clip))
            {
                float length = name == "hover" ? .035f : name == "click" ? .065f : name == "party-hit" ? .65f : name == "stamp" ? .48f :
                    name == "deflate" ? 1.35f : name == "radio" ? .85f : name == "orbit" ? 1.25f : name == "celebrate" ? 1.2f : name == "alarm" ? .9f : .45f;
                var data = new float[(int)(length * Rate)]; var random = new System.Random(1969 + name.Length);
                float lowNoise = 0, midNoise = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)Rate, p = t / length, noise = (float)random.NextDouble() * 2 - 1;
                    lowNoise += (noise - lowNoise) * .035f;
                    midNoise += (noise - midNoise) * .17f;
                    float envelope = Mathf.Min(t * 180, 1) * Mathf.Pow(1 - p, 2), value;
                    if (name == "hover") value = Mathf.Sin(2 * Mathf.PI * 880 * t) * .035f;
                    else if (name == "click") value = midNoise * .12f + Mathf.Sin(2 * Mathf.PI * 560 * t) * .06f;
                    else if (name == "stamp")
                        value = Mathf.Sin(2 * Mathf.PI * (88 * t - 38 * t * t)) * Mathf.Exp(-t * 16) * .52f +
                            midNoise * Mathf.Exp(-t * 32) * .3f + noise * Mathf.Exp(-Mathf.Pow((t - .11f) / .012f, 2)) * .06f;
                    else if (name == "party-hit") value = lowNoise * Mathf.Exp(-t * 7) * 1.3f + noise * Mathf.Exp(-t * 30) * .25f;
                    else if (name == "sputter") value = lowNoise * (.7f + .3f * Mathf.Sin(t * 96)) * .6f;
                    else if (name == "radio") value = (midNoise - lowNoise) * (.3f + .7f * Mathf.Pow(Mathf.Sin(t * 17), 2)) * .2f;
                    else if (name == "orbit") value = lowNoise * Mathf.Sin(p * Mathf.PI) * .3f + Mathf.Sin(2 * Mathf.PI * (90 * t + 90 * t * t)) * .025f;
                    else if (name == "deflate") value = midNoise * .18f + Mathf.Sin(2 * Mathf.PI * (520 * t - 165 * t * t)) * .025f;
                    else if (name == "alarm") value = Mathf.Sin(2 * Mathf.PI * (Mathf.Repeat(p * 4, 1) < .5f ? 740 : 554) * t) * .12f;
                    else if (name == "celebrate" || name == "discover")
                    {
                        float pitch = new[] { 261.63f, 329.63f, 392, 523.25f }[Mathf.Min(3, (int)(p * 4))];
                        value = Mathf.Sin(2 * Mathf.PI * pitch * t) * .08f + Mathf.Sin(2 * Mathf.PI * pitch * .5f * t) * .04f;
                    }
                    else value = Mathf.Sin(2 * Mathf.PI * (240 * t + 85 * t * t)) * .06f + lowNoise * .1f;
                    data[i] = value * envelope;
                }
                clip = Clip(name, data, 1); sounds.Add(name, clip);
            }
            bool interfaceSound = name == "click" || name == "hover";
            if (!interfaceSound && stage != null)
            {
                source.transform.position = name == "party-hit" || name == "sputter" ? stage.cannon.transform.position :
                    name == "stamp" && currentShot == "launch" ? stage.switchProp.transform.position : name == "scan" ? stage.projection.transform.position :
                    stage.television.transform.position;
            }
            (interfaceSound ? controls : source).PlayOneShot(clip);
        }
        void OnDestroy()
        {
            foreach (var clip in generated) if (clip != null) Destroy(clip);
        }
    }
}
