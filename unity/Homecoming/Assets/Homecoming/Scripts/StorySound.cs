using System.Collections.Generic;
using UnityEngine;

namespace Homecoming
{
    [DefaultExecutionOrder(-100), RequireComponent(typeof(AudioSource))]
    public sealed class StorySound : MonoBehaviour
    {
        readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        AudioSource source, room;
        public bool Muted { get; private set; }
        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = .32f;
            room = gameObject.AddComponent<AudioSource>();
            room.playOnAwake = false;
            room.loop = true;
            var data = new float[48000 * 3];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / 48000f;
                data[i] = .032f * Mathf.Sin(t * 2 * Mathf.PI * 60) + .012f * Mathf.Sin(t * 2 * Mathf.PI * 80);
            }
            room.clip = AudioClip.Create("Ship ventilation", data.Length, 1, 48000, false);
            room.clip.SetData(data, 0);
            room.volume = .22f;
            room.Play();
            SetMuted(PlayerPrefs.GetInt("homecoming.unity.mute", 0) == 1);
        }
        public void SetMuted(bool mute)
        {
            Muted = mute;
            if (source != null) source.mute = mute;
            if (room != null) room.mute = mute;
            PlayerPrefs.SetInt("homecoming.unity.mute", mute ? 1 : 0);
        }
        public void SetPaused(bool paused)
        {
            if (paused) { source.Pause(); room.Pause(); }
            else { source.UnPause(); room.UnPause(); }
        }
        public void StopCue() { if (source != null) source.Stop(); }
        public void Cue(string name)
        {
            if (Muted || name == "voice") return; // Dialogue is captioned; no synthetic character voices.
            if (!sounds.TryGetValue(name, out var clip))
            {
                int rate = 48000;
                float length = name == "hover" ? .045f : name == "click" ? .07f : name == "celebrate" ? 1.1f : name == "deflate" ? 1.3f : name == "radio" ? .55f : name == "alarm" ? .9f : .28f;
                var samples = new float[(int)(length * rate)];
                var random = new System.Random(1969 + name.Length);
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = i / (float)rate, p = t / length;
                    float noise = (float)random.NextDouble() * 2 - 1;
                    float envelope = Mathf.Min(t * 120, 1) * Mathf.Pow(1 - p, 2);
                    float f = name == "quiet" ? 140 : name == "discover" ? 440 + p * 550 : 230 + p * 110;
                    float wave = Mathf.Sin(2 * Mathf.PI * f * t);
                    if (name == "hover") wave = Mathf.Sin(2 * Mathf.PI * 880 * t) * .1f;
                    else if (name == "click") wave = Mathf.Sin(2 * Mathf.PI * 560 * t) * .22f;
                    else if (name == "stamp" || name == "party-hit" || name == "sputter")
                        wave = noise * .6f + Mathf.Sin(2 * Mathf.PI * (90 - p * 45) * t) * .4f;
                    else if (name == "radio") wave = noise * .3f * (.5f + .5f * Mathf.Sin(t * 220));
                    else if (name == "alarm")
                    {
                        // Two-tone ship klaxon for discovery alerts.
                        float tone = Mathf.Repeat(p * 4, 1) < .5f ? 740 : 554;
                        wave = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * tone * t)) * .22f + Mathf.Sin(2 * Mathf.PI * tone * 2 * t) * .08f;
                        envelope = Mathf.Min(t * 60, 1) * (1 - Mathf.Pow(p, 4));
                    }
                    else if (name == "deflate") wave = Mathf.Sin(2 * Mathf.PI * (650 - p * 420) * t) * .18f + noise * .08f;
                    else if (name == "celebrate")
                    {
                        float note = new[] { 261.63f, 329.63f, 392f, 523.25f }[Mathf.Min(3, (int)(p * 4))];
                        wave = Mathf.Sin(2 * Mathf.PI * note * t) * .4f;
                    }
                    samples[i] = wave * envelope * .36f;
                }
                clip = AudioClip.Create(name, samples.Length, 1, rate, false);
                clip.SetData(samples, 0);
                sounds.Add(name, clip);
            }
            source.PlayOneShot(clip);
        }
        void OnDestroy()
        {
            foreach (var clip in sounds.Values) Destroy(clip);
            if (room != null && room.clip != null) Destroy(room.clip);
        }
    }
}
