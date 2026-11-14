using UnityEngine;
using UnityEngine.Rendering;

namespace Homecoming
{
    // Lighting direction for the film. Each shot gets its own look, evaluated from shot
    // time only, so pausing and seeking stay exact:
    //   1969  warm, hopeful, the television flickering black-and-white light into the room
    //   party coloured lights pulsing on the dance beat, a flash when the cannon fires
    //   2026  the same room gone dim, cold and desaturated, a dying console bulb
    //   Artemis  the broadcast is the main light: cold blue on the crew's faces
    //   switch   dim until the slam, then the bridge snaps back to full warm light
    //   lab      the projector washes the bench in cyan as the miniature Earth rises
    // The rig is created at runtime (never saved into Story.unity), so rebuilding or
    // hand-editing the scene cannot drift from it. The builder's two directional lights
    // are switched off and replaced.
    public sealed class StoryLighting : MonoBehaviour
    {
        static readonly Color Warm = new Color(1f, .85f, .66f), Neutral = new Color(1f, .93f, .84f),
            Cold = new Color(.84f, .87f, .94f), Archive = new Color(.82f, .88f, 1f), Broadcast = new Color(.42f, .66f, 1f),
            SkyBase = new Color(.24f, .29f, .35f), EquatorBase = new Color(.16f, .18f, .21f), GroundBase = new Color(.07f, .07f, .08f);
        static readonly int SunId = Shader.PropertyToID("_SunDir");

        StoryStage stage;
        StoryGrade grade;
        Light key, fill, rim, tv, partyA, partyB, projector;
        Renderer earthSurface;
        MaterialPropertyBlock block;
        bool ready;

        public void Initialize(StoryStage owner)
        {
            if (ready) return;
            stage = owner;
            foreach (var legacy in new[] { "Warm studio key", "Cold window fill" })
            {
                var found = GameObject.Find(legacy);
                if (found != null && found.TryGetComponent<Light>(out var light)) light.enabled = false;
            }
            // Overhead spot: a pool of light on the crew that falls off into the room.
            key = Make("Overhead key", LightType.Spot, new Vector3(.6f, 5.7f, 5.4f), new Vector3(.15f, .9f, .2f), Warm, 16, 64, LightShadows.Soft);
            // Soft wash on the back wall, consoles and window ring so the room never drops to black.
            fill = Make("Set fill", LightType.Spot, new Vector3(0, 5.2f, 7.5f), new Vector3(-.4f, 2.3f, -3.6f), new Color(.8f, .87f, 1f), 18, 72, LightShadows.None);
            // Cold light through the observation window, behind the crew: edges and depth.
            // Kept shallow and shadowless so it outlines the crew without washing the floor.
            rim = Make("Window rim", LightType.Directional, Vector3.zero, Vector3.zero, new Color(.55f, .71f, 1f), 0, 0, LightShadows.None);
            rim.transform.rotation = Quaternion.LookRotation(new Vector3(.2f, -.2f, 1f));
            // The television lights the room it plays in.
            tv = Make("Television spill", LightType.Spot, new Vector3(1.85f, 2.1f, -.7f), new Vector3(.2f, 1.25f, 3.2f), Archive, 8, 118, LightShadows.None);
            partyA = Make("Party light — rose", LightType.Point, new Vector3(-2.4f, 3.4f, 1.7f), Vector3.zero, new Color(1f, .33f, .6f), 8, 0, LightShadows.None);
            partyB = Make("Party light — teal", LightType.Point, new Vector3(2.9f, 3.2f, 1.5f), Vector3.zero, new Color(.25f, .86f, .95f), 8, 0, LightShadows.None);
            projector = Make("Projector glow", LightType.Point, new Vector3(0, 1.4f, -.8f), Vector3.zero, new Color(.35f, .9f, .96f), 5.5f, 0, LightShadows.None);
            if (stage.practical != null) stage.practical.renderMode = LightRenderMode.ForcePixel;

            grade = stage.filmCamera.GetComponent<StoryGrade>();
            if (grade == null) grade = stage.filmCamera.gameObject.AddComponent<StoryGrade>();
            stage.filmCamera.allowHDR = true;
            earthSurface = stage.earth.GetComponentInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = false;
            ready = true;
        }

        Light Make(string name, LightType type, Vector3 position, Vector3 target, Color color, float range, float angle, LightShadows shadows)
        {
            var go = new GameObject(name + " (runtime)") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            if (type == LightType.Spot) go.transform.LookAt(target);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.shadows = shadows;
            light.shadowStrength = .85f;
            light.shadowBias = .03f;
            light.shadowNormalBias = .3f;
            light.renderMode = LightRenderMode.ForcePixel;
            if (type != LightType.Directional) light.range = range;
            if (type == LightType.Spot) { light.spotAngle = angle; light.innerSpotAngle = angle * .4f; }
            return light;
        }

        public void Teardown()
        {
            foreach (var light in new[] { key, fill, rim, tv, partyA, partyB, projector })
                if (light != null) { if (Application.isPlaying) Destroy(light.gameObject); else DestroyImmediate(light.gameObject); }
            ready = false;
        }
        // DontSave objects are not cleaned up with the scene on their own.
        void OnDestroy() { if (Application.isPlaying) Teardown(); }

        static float Smooth(float from, float to, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));
        static float Pulse(float t, float at, float width) => Mathf.Exp(-Mathf.Pow((t - at) / width, 2));
        // Deterministic picture-tube flicker in [-1, 1].
        static float Flicker(float t) => .5f * Mathf.Sin(t * 23f) + .3f * Mathf.Sin(t * 57f + 1.3f) + .2f * Mathf.Sin(t * 91f + .7f);

        public void Evaluate(string id, float t, bool reduced)
        {
            if (!ready) Initialize(stage != null ? stage : GetComponent<StoryStage>());
            float flicker = reduced ? 0 : Flicker(t);

            // The 1969 bridge is the reference look; other shots depart from it.
            float keyI = 1.35f, fillI = .6f, rimI = .6f, tvI = 1.1f * (1 + .18f * flicker), practicalI = 1.1f, party = 0, projectorI = 0, ambient = 1;
            Color keyColor = Warm, tvColor = Archive;
            float exposure = 1.45f, saturation = 1.05f, contrast = 1.05f, vignette = .32f, bloom = .55f, grain = .022f;
            Color tint = new Color(1.03f, 1f, .95f);

            switch (id)
            {
                case "home": case "recruit":
                    keyI = .9f; fillI = .5f; rimI = .6f; tvI = .7f * (1 + .12f * flicker); practicalI = .8f; ambient = .85f;
                    exposure = 1.4f; saturation = .95f; vignette = .42f; break;
                case "signal":
                    exposure = 1.1f; saturation = .9f; vignette = .5f; bloom = .6f; tint = new Color(.96f, .99f, 1.04f); break;
                case "apollo":
                    exposure = 1.2f; saturation = .9f; vignette = .55f; bloom = .35f; grain = .05f; break;
                case "party":
                {
                    float on = Smooth(.68f, .9f, t);
                    float beat = reduced ? .5f : .5f + .5f * Mathf.Sin((t - .72f) * 7.8f);
                    keyI = 1.45f; party = on; partyA.intensity = 2.6f * on * (.25f + .75f * beat); partyB.intensity = 2.6f * on * (1 - .75f * beat);
                    exposure += reduced ? 0 : .55f * Pulse(t, .76f, .09f);
                    saturation = 1.15f; tint = new Color(1.05f, .99f, .96f); break;
                }
                case "jam":
                {
                    float fade = (1 - Smooth(.15f, 1.6f, t)) * .65f;
                    party = fade; partyA.intensity = 2.2f * fade; partyB.intensity = 2.2f * fade;
                    keyI = 1.25f;
                    exposure += reduced ? 0 : .22f * (Pulse(t, .6f, .07f) + Pulse(t, 1.1f, .07f)); break;
                }
                case "years": case "deflate": case "epilogue":
                {
                    // The same room 57 years on: dim, cold and faded. The console bulb is dying.
                    bool dying = !reduced && Mathf.Sin(t * 7.3f) + Mathf.Sin(t * 13.1f + 2) > 1.35f;
                    keyI = .62f; keyColor = Cold; fillI = .4f; rimI = .45f; tvI = .22f; tvColor = new Color(.7f, .72f, .7f);
                    practicalI = dying ? .1f : .34f; ambient = .75f;
                    exposure = 1.35f; saturation = .58f; contrast = .98f; vignette = .55f; bloom = .4f;
                    tint = new Color(.95f, .98f, 1.04f);
                    if (id != "years") { keyI = .7f; saturation = .64f; } break;
                }
                case "return":
                {
                    // The Artemis broadcast becomes the main light on the crew.
                    float signal = Smooth(.1f, .55f, t);
                    keyI = .45f; keyColor = Cold; fillI = .35f; rimI = .4f; practicalI = .3f; ambient = .7f;
                    tvColor = Broadcast; tvI = 1.7f * signal * (1 + .12f * flicker);
                    exposure = 1.35f; saturation = .8f; vignette = .5f; bloom = .6f; tint = new Color(.94f, .98f, 1.05f); break;
                }
                case "earth":
                    exposure = 1.4f; saturation = .85f; contrast = .97f; vignette = .45f; bloom = .6f; tint = new Color(.99f, 1f, 1.01f); break;
                case "launch": case "title":
                {
                    // Dim like 2026 until the slam at 0.74 s, then every light kicks on.
                    float on = id == "title" ? 1 : Smooth(.72f, .86f, t);
                    keyI = Mathf.Lerp(.42f, 1.75f, on); keyColor = Color.Lerp(Cold, Warm, on);
                    fillI = Mathf.Lerp(.35f, .7f, on); rimI = Mathf.Lerp(.4f, .7f, on); practicalI = Mathf.Lerp(.3f, 3.2f, on); tvI = Mathf.Lerp(.2f, .9f, on);
                    ambient = Mathf.Lerp(.75f, 1.05f, on);
                    saturation = Mathf.Lerp(.6f, 1.12f, on); vignette = Mathf.Lerp(.55f, .3f, on);
                    exposure = 1.35f + (reduced || id == "title" ? 0 : .7f * Pulse(t, .8f, .12f));
                    tint = Color.Lerp(new Color(.95f, .98f, 1.04f), new Color(1.04f, 1f, .94f), on); break;
                }
                case "lab": case "ready":
                {
                    float rise = id == "ready" ? 1 : Smooth(.6f, 1.9f, t);
                    keyI = .6f; fillI = .35f; rimI = .5f; practicalI = .55f; tvI = .2f; ambient = .75f;
                    projectorI = 1.5f * rise * (reduced ? 1 : 1 + .06f * Mathf.Sin(t * 9));
                    exposure = 1.4f; saturation = .95f; vignette = .45f; bloom = .8f; tint = new Color(.95f, 1.02f, 1.04f); break;
                }
                case "hearing":
                    keyI = 1.15f; keyColor = Neutral; fillI = .55f; rimI = .55f; tvI = .3f; practicalI = .8f; ambient = .85f;
                    exposure = 1.4f + (reduced ? 0 : .25f * Pulse(t, .64f, .08f)); saturation = .92f; vignette = .4f; break;
            }

            bool onBridge = stage.bridge.activeSelf;
            key.enabled = onBridge; fill.enabled = onBridge; rim.enabled = onBridge; tv.enabled = onBridge && tvI > .01f;
            key.intensity = keyI; key.color = keyColor;
            fill.intensity = fillI;
            rim.intensity = rimI;
            tv.intensity = tvI; tv.color = tvColor;
            partyA.enabled = partyB.enabled = onBridge && party > .001f;
            projector.enabled = onBridge && projectorI > .01f;
            projector.intensity = projectorI;
            if (stage.projection != null) projector.transform.position = stage.projection.transform.position + new Vector3(0, .1f, .5f);
            if (stage.practical != null) { stage.practical.enabled = onBridge; stage.practical.intensity = practicalI; }

            RenderSettings.ambientSkyColor = SkyBase * ambient;
            RenderSettings.ambientEquatorColor = EquatorBase * ambient;
            RenderSettings.ambientGroundColor = GroundBase * ambient;

            grade.exposure = exposure; grade.saturation = saturation; grade.contrast = contrast;
            grade.vignette = vignette; grade.bloomIntensity = bloom; grade.grain = grain; grade.tint = tint;

            // Earth rolls from night into daylight as the camera closes in.
            if (earthSurface != null && id == "earth")
            {
                float dawn = reduced ? 1 : Smooth(.2f, 3.8f, t);
                Vector3 sun = Vector3.Slerp(new Vector3(-.75f, .15f, -.65f).normalized, new Vector3(-.6f, .35f, .72f).normalized, dawn);
                earthSurface.GetPropertyBlock(block);
                block.SetVector(SunId, sun);
                earthSurface.SetPropertyBlock(block);
            }
        }
    }
}
