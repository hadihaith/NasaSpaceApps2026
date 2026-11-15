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
            SkyBase = new Color(.3f, .35f, .42f), EquatorBase = new Color(.2f, .22f, .26f), GroundBase = new Color(.09f, .09f, .1f);
        static readonly int SunId = Shader.PropertyToID("_SunDir");

        StoryStage stage;
        StoryGrade grade;
        Light key, fill, rim, tv, partyA, partyB, projector, cabinBounce;
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
            cabinBounce = Make("Cabin diffuse bounce", LightType.Directional, Vector3.zero, Vector3.zero, new Color(.69f, .78f, .86f), 0, 0, LightShadows.None);
            cabinBounce.transform.rotation = Quaternion.LookRotation(new Vector3(.15f, -.18f, -1));
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
            foreach (var light in new[] { key, fill, rim, tv, partyA, partyB, projector, cabinBounce })
                if (light != null) { if (Application.isPlaying) Destroy(light.gameObject); else DestroyImmediate(light.gameObject); }
            ready = false;
        }
        // DontSave objects are not cleaned up with the scene on their own.
        void OnDestroy() { if (Application.isPlaying) Teardown(); }

        static float Smooth(float from, float to, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));
        static float Pulse(float t, float at, float width) => Mathf.Exp(-Mathf.Pow((t - at) / width, 2));
        // Deterministic picture-tube flicker in [-1, 1].
        static float Flicker(float t) => .5f * Mathf.Sin(t * 23f) + .3f * Mathf.Sin(t * 57f + 1.3f) + .2f * Mathf.Sin(t * 91f + .7f);

        // Everything a shot's lighting needs, so two looks can be blended (the 2026 time-lapse).
        struct Look
        {
            public float key, fill, rim, tv, practical, party, projector, ambient;
            public float exposure, saturation, contrast, vignette, bloom, grain;
            public Color keyColor, tvColor, tint;
            public static Look Lerp(Look a, Look b, float t) => new Look
            {
                key = Mathf.Lerp(a.key, b.key, t), fill = Mathf.Lerp(a.fill, b.fill, t), rim = Mathf.Lerp(a.rim, b.rim, t),
                tv = Mathf.Lerp(a.tv, b.tv, t), practical = Mathf.Lerp(a.practical, b.practical, t),
                party = Mathf.Lerp(a.party, b.party, t), projector = Mathf.Lerp(a.projector, b.projector, t),
                ambient = Mathf.Lerp(a.ambient, b.ambient, t), exposure = Mathf.Lerp(a.exposure, b.exposure, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t), contrast = Mathf.Lerp(a.contrast, b.contrast, t),
                vignette = Mathf.Lerp(a.vignette, b.vignette, t), bloom = Mathf.Lerp(a.bloom, b.bloom, t),
                grain = Mathf.Lerp(a.grain, b.grain, t), keyColor = Color.Lerp(a.keyColor, b.keyColor, t),
                tvColor = Color.Lerp(a.tvColor, b.tvColor, t), tint = Color.Lerp(a.tint, b.tint, t),
            };
        }

        // 1969: a bright, warm bridge with the television flickering into the room.
        static Look Bright(float flicker) => new Look
        {
            key = 2.3f, fill = 1.1f, rim = .75f, tv = 1.3f * (1 + .18f * flicker), practical = 1.6f, ambient = 1.15f,
            exposure = 1.65f, saturation = 1.05f, contrast = 1.05f, vignette = .28f, bloom = .55f, grain = .022f,
            keyColor = Warm, tvColor = Archive, tint = new Color(1.03f, 1f, .95f),
        };

        // 2026: the same room, cooler and faded, but still clearly lit. The console bulb is dying.
        static Look Faded(float t, bool reduced)
        {
            bool dying = !reduced && Mathf.Sin(t * 7.3f) + Mathf.Sin(t * 13.1f + 2) > 1.35f;
            return new Look
            {
                key = 1.15f, fill = .75f, rim = .55f, tv = .3f, practical = dying ? .15f : .5f, ambient = .95f,
                exposure = 1.55f, saturation = .62f, contrast = .98f, vignette = .45f, bloom = .4f, grain = .03f,
                keyColor = Cold, tvColor = new Color(.7f, .72f, .7f), tint = new Color(.96f, .98f, 1.03f),
            };
        }

        public void Evaluate(string id, float t, bool reduced)
        {
            if (!ready) Initialize(stage != null ? stage : GetComponent<StoryStage>());
            float flicker = reduced ? 0 : Flicker(t);
            float beat = .5f;
            var look = Bright(flicker);

            switch (id)
            {
                case "home": case "recruit":
                    look.key = 1.7f; look.fill = .95f; look.rim = .7f; look.tv = .9f * (1 + .12f * flicker); look.practical = 1.2f;
                    look.ambient = 1.05f; look.exposure = 1.6f; look.saturation = .97f; look.vignette = .38f; break;
                case "signal":
                    look.exposure = 1.25f; look.saturation = .9f; look.vignette = .5f; look.bloom = .6f; look.tint = new Color(.96f, .99f, 1.04f); break;
                case "apollo":
                    look.tv = 1.6f * (1 + .18f * flicker); look.exposure = 1.5f; look.saturation = .9f; look.vignette = .42f;
                    look.bloom = .45f; look.grain = .05f; break;
                case "party":
                {
                    look.party = Smooth(.68f, .9f, t);
                    beat = reduced ? .5f : .5f + .5f * Mathf.Sin((t - .72f) * 7.8f);
                    look.key = 1.9f; look.saturation = 1.05f; look.tint = new Color(1.05f, .99f, .96f);
                    look.exposure += reduced ? 0 : .5f * Pulse(t, .76f, .09f); break;
                }
                case "jam":
                    look.party = (1 - Smooth(.15f, 1.6f, t)) * .65f;
                    look.key = 2.2f;
                    break;
                case "years":
                {
                    float lapse = StoryStage.Lapse(t, reduced);
                    var welcome = Bright(0);
                    welcome.party = 1; welcome.key = 2.4f; welcome.saturation = 1.15f;
                    welcome.tint = new Color(1.05f, .99f, .96f);
                    look = Look.Lerp(welcome, Faded(t, reduced), lapse);
                    // A visible sweep of passing days, settling before the 2026 dialogue.
                    if (!reduced && t >= .25f && t < 2.7f)
                    {
                        float sweep = .5f + .5f * Mathf.Sin((t - .25f) * 13f);
                        look.exposure *= Mathf.Lerp(.55f, 1.18f, sweep);
                        look.rim *= Mathf.Lerp(.35f, 1.8f, sweep);
                    }
                    break;
                }
                case "deflate": case "epilogue":
                    look = Faded(t, reduced); look.key = 1.3f; look.saturation = .66f; break;
                case "return":
                {
                    // The Artemis broadcast leads, washing the crew in blue.
                    float signal = Smooth(.1f, .55f, t);
                    look = Faded(t, reduced);
                    look.key = 1.0f; look.fill = .8f; look.rim = .5f; look.practical = .5f; look.ambient = .9f;
                    look.tvColor = Broadcast; look.tv = 2.2f * signal * (1 + .12f * flicker);
                    look.saturation = .82f; look.bloom = .6f; look.tint = new Color(.95f, .98f, 1.04f); break;
                }
                case "earth":
                    look.exposure = 1.5f; look.saturation = .85f; look.contrast = .97f; look.vignette = .45f; look.bloom = .6f;
                    look.tint = new Color(.99f, 1f, 1.01f); break;
                case "launch": case "title":
                {
                    // 2026 until the slam at 0.74 s, then every light kicks on, brighter than ever.
                    float on = id == "title" ? 1 : Smooth(.73f, .76f, t);
                    var lit = Bright(flicker);
                    lit.key = 2.2f; lit.practical = 1.85f; lit.fill = 1.05f; lit.saturation = 1.05f; lit.bloom = .3f;
                    look = Look.Lerp(Faded(t, reduced), lit, on);
                    look.exposure += reduced || id == "title" ? 0 : .12f * Pulse(t, .8f, .12f); break;
                }
                case "lab": case "ready":
                {
                    float rise = id == "ready" ? 1 : Smooth(.6f, 1.9f, t);
                    look.key = 1.2f; look.fill = .85f; look.rim = .6f; look.practical = .9f; look.tv = .3f; look.ambient = .95f;
                    look.projector = 1.8f * rise * (reduced ? 1 : 1 + .06f * Mathf.Sin(t * 9));
                    look.exposure = 1.6f; look.saturation = .97f; look.vignette = .4f; look.bloom = .8f;
                    look.tint = new Color(.96f, 1.02f, 1.03f); break;
                }
                case "evidence":
                    float scan = Smooth(StoryStage.MonitorStarts, StoryStage.MonitorStarts + .4f, t);
                    look.key = 1.9f; look.fill = 1.0f; look.rim = .8f; look.practical = 1.15f;
                    look.tvColor = Broadcast; look.tv = .3f + .8f * scan;
                    look.exposure = 1.5f; look.saturation = .94f; look.vignette = .32f; look.bloom = .52f;
                    break;
                case "reaction":
                case "hearing":
                    look.key = 2.0f; look.keyColor = Neutral; look.fill = 1.05f; look.rim = .65f; look.tv = .4f; look.practical = 1.2f;
                    look.ambient = 1.05f; look.saturation = .94f; look.vignette = .36f;
                    look.exposure = 1.6f + (reduced ? 0 : .25f * Pulse(t, .64f, .08f));
                    if (id == "hearing" && !reduced)
                        look.party = .55f * Pulse(t, StoryStage.VictoryLaunch + .45f, .40f);
                    break;
            }

            float keyI = look.key, fillI = look.fill, rimI = look.rim, tvI = look.tv, practicalI = look.practical;
            float party = look.party, projectorI = look.projector, ambient = look.ambient;
            Color keyColor = look.keyColor, tvColor = look.tvColor, tint = look.tint;
            float exposure = look.exposure, saturation = look.saturation, contrast = look.contrast;
            float vignette = look.vignette, bloom = look.bloom, grain = look.grain;
            partyA.intensity = 1.3f * party * (.25f + .75f * beat);
            partyB.intensity = 1.3f * party * (1 - .75f * beat);

            bool onBridge = stage.bridge.activeSelf;
            key.enabled = onBridge; fill.enabled = onBridge; rim.enabled = onBridge; tv.enabled = onBridge && tvI > .01f;
            cabinBounce.enabled = onBridge;
            cabinBounce.intensity = .36f * ambient;
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
            // Player builds do not rebuild the editor's ambient SH automatically when
            // these gradient colours change. Supply the diffuse bounce explicitly.
            var bounce = new SphericalHarmonicsL2();
            bounce.AddAmbientLight(EquatorBase * (ambient * .8f));
            bounce.AddDirectionalLight(Vector3.up, SkyBase * ambient, .32f);
            bounce.AddDirectionalLight(Vector3.down, GroundBase * ambient, .18f);
            RenderSettings.ambientProbe = bounce;

            grade.exposure = exposure; grade.saturation = saturation; grade.contrast = contrast;
            grade.vignette = vignette; grade.bloomIntensity = bloom; grade.grain = grain; grade.tint = tint;
            grade.shotTime = t;
            grade.animateGrain = !reduced;
            grade.focusStrength = 0;
            grade.focusRange = 1.65f;
            if (id == "recruit") { grade.focusDistance = 4.6f; grade.focusStrength = .8f; }
            else if (id == "correction") { grade.focusDistance = 3.9f; grade.focusStrength = .64f; }
            else if (id == "launch") { grade.focusDistance = 2.7f; grade.focusRange = 1.1f; grade.focusStrength = .72f; }
            else if (id == "hearing") { grade.focusDistance = 7.4f; grade.focusStrength = .35f; }
            else if (id == "deflate") { grade.focusDistance = 3.2f; grade.focusRange = 1.2f; grade.focusStrength = .72f; }

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
