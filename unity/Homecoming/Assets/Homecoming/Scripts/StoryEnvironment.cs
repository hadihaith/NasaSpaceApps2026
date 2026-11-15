using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Homecoming
{
    // Practical set dressing and atmospheric effects. Evaluated from the same shot clock
    // as the actors, with one instanced mesh for airborne particles. No scene is rebuilt.
    [DisallowMultipleComponent]
    public sealed class StoryEnvironment : MonoBehaviour
    {
        StoryStage stage;
        Transform dressing, orbit, projectionBeam;
        GameObject planet;
        ReflectionProbe reflection;
        Mesh box, dustMesh;
        Vector3[] vertices;
        Color[] colors;
        readonly List<Object> owned = new List<Object>();
        readonly List<Renderer> instruments = new List<Renderer>();
        readonly List<Renderer> lamps = new List<Renderer>();
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        MaterialPropertyBlock properties;
        Material steel, graphite, paint, rubber, brass, lamp, particle, console, glass;
        bool ready;
        int lastProbeFrame = -1000;
        string lastShot;
        const int DustCount = 54, PuffCount = 10, VictoryArmSparks = 44, VictoryScatter = 30, VictoryTail = 9;
        const int AirParticleCount = DustCount + PuffCount + 2 * VictoryArmSparks + VictoryScatter + VictoryTail;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static float Hash(int index, int channel) => Mathf.Repeat(Mathf.Sin((index + 17) * 12.9898f + channel * 78.233f) * 43758.5453f, 1);
        static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));

        public void Initialize(StoryStage owner)
        {
            if (ready) return;
            stage = owner;
            properties = new MaterialPropertyBlock();
            dressing = Group("Bridge practical details", stage.bridge.transform);
            orbit = Group("Orbital view beyond the glass", transform);
            var floor = stage.bridge.transform.Find("Floor");
            box = floor != null ? floor.GetComponent<MeshFilter>().sharedMesh : null;
            PreserveBridgeMaterials();
            steel = Metal("Satin aluminium", new Color(.34f, .4f, .43f), .72f, .43f);
            graphite = Metal("Graphite housings", new Color(.06f, .09f, .105f), .25f, .3f);
            paint = Metal("Bulkhead enamel", new Color(.105f, .155f, .18f), .25f, .32f);
            rubber = Metal("Braided signal cable", new Color(.025f, .035f, .04f), 0, .2f);
            brass = Metal("Brass fittings", new Color(.45f, .29f, .16f), .7f, .35f);
            lamp = Metal("Diffused practical lamps", new Color(.83f, .72f, .52f), .1f, .2f);
            lamp.SetColor("_EmissionColor", new Color(1, .71f, .37f) * 1.25f);
            console = NewMaterial("Console phosphor", "Shaders/ShipInstrument");
            glass = NewMaterial("Observation glass", "Shaders/ObservationGlass");
            particle = NewMaterial("Air and cannon mist", "Shaders/AirParticle");
            BuildHull(); BuildConsoles(); BuildOrbit(); BuildProjector(); BuildParticles();
            if (floor != null)
            {
                var deck = NewMaterial("Worn metal deck", "Shaders/ShipDeck");
                deck.SetColor("_Color", new Color(.19f, .23f, .25f));
                var floorRenderer = floor.GetComponent<Renderer>();
                if (!originalMaterials.ContainsKey(floorRenderer)) originalMaterials.Add(floorRenderer, floorRenderer.sharedMaterials);
                floorRenderer.sharedMaterial = deck;
                foreach (var item in stage.bridge.GetComponentsInChildren<Transform>(true))
                    if (item.name == "Deck seam" || item.name == "Deck seam ") item.gameObject.SetActive(false);
            }
            var probeObject = Group("Local metal reflections", dressing);
            probeObject.localPosition = V(0, 1.7f, .1f);
            reflection = probeObject.gameObject.AddComponent<ReflectionProbe>();
            reflection.mode = ReflectionProbeMode.Realtime;
            reflection.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            reflection.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            reflection.resolution = 128; reflection.size = V(16, 9, 13);
            reflection.boxProjection = true; reflection.intensity = .7f;
            reflection.clearFlags = ReflectionProbeClearFlags.SolidColor;
            reflection.backgroundColor = new Color(.01f, .02f, .035f);
            ready = true;
        }

        Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            return go.transform;
        }
        Material Metal(string name, Color color, float metallic, float smoothness)
        {
            var material = new Material(Resources.Load<Shader>("Shaders/ShipSurface")) { name = name, hideFlags = HideFlags.HideAndDontSave };
            material.color = color; material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness);
            owned.Add(material); return material;
        }
        void PreserveBridgeMaterials()
        {
            var shader = Resources.Load<Shader>("Shaders/ShipSurface");
            var replacements = new Dictionary<Material, Material>();
            foreach (var renderer in stage.bridge.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer) continue;
                var materials = renderer.sharedMaterials;
                var originals = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null || original.shader.name != "Standard") continue;
                    if (!replacements.TryGetValue(original, out var replacement))
                    {
                        replacement = new Material(shader) { name = original.name + " (story runtime)", hideFlags = HideFlags.HideAndDontSave };
                        replacement.color = original.color;
                        replacement.SetFloat("_Metallic", original.GetFloat("_Metallic"));
                        replacement.SetFloat("_Glossiness", original.GetFloat("_Glossiness"));
                        replacement.SetColor("_EmissionColor", original.IsKeywordEnabled("_EMISSION") ? original.GetColor("_EmissionColor") : Color.black);
                        if (original.mainTexture != null) replacement.mainTexture = original.mainTexture;
                        replacements.Add(original, replacement); owned.Add(replacement);
                    }
                    materials[i] = replacement; changed = true;
                }
                if (changed) { originalMaterials.Add(renderer, originals); renderer.sharedMaterials = materials; }
            }
        }
        Material NewMaterial(string name, string path)
        {
            var shader = Resources.Load<Shader>(path);
            if (shader == null) throw new System.InvalidOperationException("Missing story effect shader: " + path);
            var material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            owned.Add(material); return material;
        }
        Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = Group(name, parent);
            go.localPosition = position; go.localScale = scale;
            go.gameObject.AddComponent<MeshFilter>().sharedMesh = box;
            go.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
        Renderer Surface(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = Group(name, parent);
            go.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
        Mesh Mesh(string name, Vector3[] positions, int[] indices, Vector2[] uv = null)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = positions; mesh.triangles = indices;
            if (uv != null) mesh.uv = uv;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
            return mesh;
        }
        Mesh Quad() => Mesh("Instrument face", new[] { V(-.5f, -.5f, 0), V(.5f, -.5f, 0), V(.5f, .5f, 0), V(-.5f, .5f, 0) },
            new[] { 0, 1, 2, 0, 2, 3 }, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });

        void BuildHull()
        {
            Box("Port inner hull", dressing, V(-6.4f, 2.3f, .1f), V(.2f, 5, 8), paint);
            Box("Starboard inner hull", dressing, V(6.4f, 2.3f, .1f), V(.2f, 5, 8), paint);
            Box("Ceiling lining", dressing, V(0, 4.85f, .1f), V(13, .15f, 8), paint);
            // A square bulkhead with a circular aperture seals the old rectangular gap.
            var points = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i <= 96; i++)
            {
                float a = i * Mathf.PI * 2 / 96;
                float x = Mathf.Cos(a), y = Mathf.Sin(a);
                float outer = 1 / Mathf.Max(Mathf.Abs(x) / (x >= 0 ? 3.4f : 2.35f), Mathf.Abs(y) / 2.35f);
                points.Add(V(x * 1.915f, y * 1.915f, 0)); points.Add(V(x * outer, y * outer, 0));
                if (i < 96) { int n = i * 2; triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 }); }
            }
            var seal = Surface("Sealed observation bulkhead", dressing, Mesh("Window aperture", points.ToArray(), triangles.ToArray()), paint);
            seal.transform.localPosition = V(-1.8f, 2.35f, -3.45f);
            var glassVertices = new List<Vector3> { Vector3.zero };
            var glassTriangles = new List<int>();
            for (int i = 0; i <= 96; i++)
            {
                float a = i * Mathf.PI * 2 / 96;
                glassVertices.Add(V(Mathf.Cos(a) * 1.9f, Mathf.Sin(a) * 1.9f, 0));
                if (i < 96) glassTriangles.AddRange(new[] { 0, i + 1, i + 2 });
            }
            var pane = Surface("Pressure glass", dressing, Mesh("Observation pane", glassVertices.ToArray(), glassTriangles.ToArray()), glass);
            pane.transform.localPosition = V(-1.8f, 2.35f, -3.26f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 4.65f;
                Box("Recessed service panel", dressing, V(x, 2.55f, -3.48f), V(1.25f, 1.4f, .08f), graphite);
                Box("Panel rail", dressing, V(x - .64f, 2.55f, -3.4f), V(.045f, 1.45f, .045f), steel);
                Box("Panel rail", dressing, V(x + .64f, 2.55f, -3.4f), V(.045f, 1.45f, .045f), steel);
                for (int i = 0; i < 9; i++) Box("Vent louvre", dressing, V(x, 2.15f + i * .095f, -3.4f), V(.95f, .035f, .07f), paint);
                var fixture = Box("Ceiling lamp diffuser", dressing, V(side * 3.3f, 4.13f, -.5f), V(.14f, .045f, 2.8f), lamp);
                lamps.Add(fixture.GetComponent<Renderer>());
                Box("Ceiling lamp tray", dressing, V(side * 3.3f, 4.19f, -.5f), V(.26f, .08f, 3), graphite);
                Box("Deck service hatch", dressing, V(side * 3.1f, -.008f, 1.9f), V(1.08f, .025f, .8f), graphite);
                Box("Hatch handle", dressing, V(side * 3.1f, .011f, 2.09f), V(.24f, .018f, .04f), steel);
            }
            Tube("Upper pressure line", new[] { V(-5.7f, 4.08f, -3.25f), V(-4, 4.08f, -3.25f), V(-3.5f, 4.37f, -3.25f), V(3, 4.37f, -3.25f), V(3.5f, 4.08f, -3.25f), V(5.7f, 4.08f, -3.25f) }, .045f, brass);
            Tube("Receiver signal cable", new[] { V(2.98f, 1.25f, -1.8f), V(3.2f, .8f, -1.8f), V(3.55f, .75f, -1.7f), V(3.85f, 1.2f, -1.65f), V(3.95f, 1.5f, -1.65f) }, .024f, rubber);
            Tube("Port conduit", new[] { V(-4.4f, .15f, -3.24f), V(-4.4f, 1.6f, -3.24f), V(-4.2f, 1.85f, -3.24f), V(-4.2f, 3.85f, -3.24f) }, .032f, rubber);
            for (int i = 0; i < 6; i++) Box("Pressure line clamp", dressing, V(-3 + i * 1.1f, 4.37f, -3.22f), V(.09f, .16f, .09f), steel);
        }
        void Tube(string name, Vector3[] path, float radius, Material material)
        {
            var positions = new List<Vector3>(); var indices = new List<int>();
            const int sides = 8;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 direction = (path[Mathf.Min(path.Length - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
                if (right.sqrMagnitude < .1f) right = Vector3.right;
                Vector3 up = Vector3.Cross(right, direction).normalized;
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2 / sides;
                    positions.Add(path[i] + radius * (right * Mathf.Cos(a) + up * Mathf.Sin(a)));
                    if (i < path.Length - 1)
                    {
                        int n = i * sides + j, next = i * sides + (j + 1) % sides;
                        indices.AddRange(new[] { n, n + sides, next, next, n + sides, next + sides });
                    }
                }
            }
            Surface(name, dressing, Mesh(name, positions.ToArray(), indices.ToArray()), material);
        }
        void BuildConsoles()
        {
            var face = Quad();
            for (int side = -1; side <= 1; side += 2)
            {
                var housing = Group(side < 0 ? "Signal receiver instrumentation" : "Cabin instrumentation", dressing);
                housing.localPosition = V(side * 4.35f, 1.74f, -1.8f);
                housing.localRotation = Quaternion.Euler(24, 0, 0);
                Box("Instrument casing", housing, Vector3.zero, V(1.35f, .52f, .12f), graphite);
                var instrument = Surface("Live phosphor display", housing, face, console);
                instrument.transform.localPosition = V(-.11f, 0, .066f);
                instrument.transform.localScale = V(.99f, .37f, 1);
                instruments.Add(instrument);
                for (int i = 0; i < 3; i++)
                {
                    var led = Box("Status lamp", housing, V(.53f, .13f - i * .13f, .075f), V(.05f, .045f, .027f), lamp);
                    lamps.Add(led.GetComponent<Renderer>());
                }
            }
        }
        void BuildOrbit()
        {
            var source = stage.earth.GetComponentInChildren<MeshFilter>(true);
            if (source == null) return;
            var renderer = source.GetComponent<Renderer>();
            planet = Surface("Earth below the ship", orbit, source.sharedMesh, renderer.sharedMaterial).gameObject;
            planet.transform.localPosition = V(-8.2f, -9.3f, -45);
            planet.transform.localScale = Vector3.one * 10;
            planet.transform.localRotation = Quaternion.Euler(-8, 221, 18);
            var halo = NewMaterial("Atmospheric limb", "Shaders/OrbitalLimb");
            var shell = Surface("Sunlit atmospheric rim", planet.transform, source.sharedMesh, halo);
            shell.transform.localScale = Vector3.one * 1.025f;
        }
        void BuildProjector()
        {
            var beamMaterial = NewMaterial("Projector light volume", "Shaders/ProjectorBeam");
            var points = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                points.Add(V(Mathf.Cos(a) * .32f, 0, Mathf.Sin(a) * .32f));
                points.Add(V(Mathf.Cos(a) * .68f, 1.12f, Mathf.Sin(a) * .68f));
                uv.Add(new Vector2(i / 48f, 0)); uv.Add(new Vector2(i / 48f, 1));
                if (i < 48) { int n = i * 2; triangles.AddRange(new[] { n, n + 1, n + 2, n + 1, n + 3, n + 2 }); }
            }
            var beam = Surface("Light through the research chamber", dressing, Mesh("Projection frustum", points.ToArray(), triangles.ToArray(), uv.ToArray()), beamMaterial);
            projectionBeam = beam.transform; projectionBeam.localPosition = V(0, .54f, -.8f);
        }
        void BuildParticles()
        {
            vertices = new Vector3[AirParticleCount * 4]; colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length]; var indices = new int[AirParticleCount * 6];
            for (int i = 0; i < AirParticleCount; i++)
            {
                int n = i * 4, p = i * 6;
                uv[n] = new Vector2(0, 0); uv[n + 1] = new Vector2(1, 0); uv[n + 2] = new Vector2(1, 1); uv[n + 3] = new Vector2(0, 1);
                indices[p] = n; indices[p + 1] = n + 1; indices[p + 2] = n + 2;
                indices[p + 3] = n; indices[p + 4] = n + 2; indices[p + 5] = n + 3;
            }
            dustMesh = Mesh("Air in the practical lights", vertices, indices, uv);
            dustMesh.MarkDynamic();
            var renderer = Surface("Floating dust and cannon puffs", dressing, dustMesh, particle);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        public void Evaluate(string id, float t, bool reduced)
        {
            if (!ready) Initialize(stage != null ? stage : GetComponent<StoryStage>());
            bool onBridge = stage.bridge.activeSelf;
            orbit.gameObject.SetActive(onBridge);
            float faded = id == "years" ? StoryStage.Lapse(t, reduced) : id == "return" || id == "deflate" ? 1 : 0;
            float awakened = id == "launch" ? Smooth(.72f, .9f, t) : 0;
            float power = Mathf.Lerp(1, .22f, faded);
            if (id == "launch") power = Mathf.Lerp(.22f, 1.25f, awakened);
            for (int i = 0; i < instruments.Count; i++)
            {
                properties.Clear(); properties.SetFloat("_Clock", reduced ? 0 : t + i * 1.7f);
                properties.SetFloat("_Power", power); properties.SetFloat("_Channel", i);
                instruments[i].SetPropertyBlock(properties);
            }
            for (int i = 0; i < lamps.Count; i++)
            {
                properties.Clear();
                Color color = i < 2 ? new Color(1, .77f, .48f) : new Color(.42f, .86f, .69f);
                properties.SetColor("_EmissionColor", color * (.15f + power * .45f));
                lamps[i].SetPropertyBlock(properties);
            }
            if (planet != null)
            {
                planet.transform.localRotation = Quaternion.Euler(-8, 221 + (reduced ? 0 : t * .12f), 18);
                var renderer = planet.GetComponent<Renderer>();
                properties.Clear(); properties.SetVector("_SunDir", V(-.5f, .32f, .8f)); renderer.SetPropertyBlock(properties);
            }
            projectionBeam.gameObject.SetActive(id == "lab" || id == "ready");
            projectionBeam.localScale = new Vector3(1, id == "ready" ? 1 : Smooth(.6f, 1.9f, t), 1);
            projectionBeam.GetComponent<Renderer>().sharedMaterial.SetFloat("_Clock", reduced ? 0 : t);
            UpdateAir(id, t, reduced, power);
            if (Application.isPlaying && onBridge && id != lastShot && (lastProbeFrame < 0 || id == "launch" || id == "lab"))
            {
                if (Time.frameCount - lastProbeFrame > 20) { reflection.RenderProbe(); lastProbeFrame = Time.frameCount; }
            }
            lastShot = id;
        }
        void UpdateAir(string id, float t, bool reduced, float power)
        {
            Vector3 right = stage.filmCamera.transform.right, up = stage.filmCamera.transform.up;
            for (int i = 0; i < AirParticleCount; i++)
            {
                Vector3 center; float size, alpha; Color color;
                if (i < DustCount)
                {
                    float time = reduced ? 0 : t;
                    center = V(-4.7f + Hash(i, 0) * 9.4f + Mathf.Sin(time * .21f + i) * .07f,
                        .35f + Mathf.Repeat(Hash(i, 1) * 3.5f + time * .018f, 3.5f), -2.8f + Hash(i, 2) * 4.5f);
                    size = .004f + Hash(i, 3) * .009f;
                    alpha = (.045f + Hash(i, 4) * .04f) * power;
                    color = new Color(.9f, .87f, .74f);
                }
                else if (i < DustCount + PuffCount)
                {
                    int puff = i - DustCount;
                    float at = id == "party" ? .74f : id == "hearing" ? StoryStage.VictoryLaunch : puff < 5 ? .58f : 1.08f;
                    float age = t - at - (puff % 5) * .017f;
                    bool visible = (id == "party" || id == "jam" || id == "hearing") && age > 0 && age < 1.5f && !reduced;
                    float a = Mathf.Max(0, age);
                    center = stage.cannon.transform.TransformPoint(V(0, .26f, .44f)) + V((Hash(i, 0) - .5f) * a * .5f, a * .55f, a * .5f);
                    size = .04f + a * .13f;
                    alpha = visible ? .08f * (1 - a / 1.5f) : 0;
                    color = new Color(.58f, .65f, .7f);
                }
                else if (id == "hearing")
                    VictoryParticle(i - DustCount - PuffCount, t - StoryStage.VictoryLaunch, reduced, out center, out size, out alpha, out color);
                else
                {
                    center = Vector3.zero;
                    size = alpha = 0;
                    color = Color.clear;
                }
                int n = i * 4;
                vertices[n] = center - right * size - up * size; vertices[n + 1] = center + right * size - up * size;
                vertices[n + 2] = center + right * size + up * size; vertices[n + 3] = center - right * size + up * size;
                color.a = alpha;
                colors[n] = colors[n + 1] = colors[n + 2] = colors[n + 3] = color;
            }
            dustMesh.vertices = vertices; dustMesh.colors = colors; dustMesh.RecalculateBounds();
        }

        void VictoryParticle(int index, float age, bool reduced, out Vector3 center, out float size, out float alpha, out Color color)
        {
            const float burst = .40f;
            var crown = V(.08f, 2.96f, .76f);
            float light = Hash(index, 7);
            color = Color.Lerp(new Color(1.35f, .49f, .09f), new Color(1.65f, 1.31f, .55f), light);
            size = .049f + .022f * light;
            alpha = 0;
            center = crown;
            if (age < 0) return;

            if (index >= 2 * VictoryArmSparks + VictoryScatter)
            {
                if (reduced) return;
                float time = age - .024f * (index - 2 * VictoryArmSparks - VictoryScatter);
                float rise = Mathf.Clamp01(time / burst);
                var muzzle = stage.cannon.transform.TransformPoint(V(0, .26f, .44f));
                center = Vector3.Lerp(muzzle, crown, rise) + V(0, .17f * Mathf.Sin(Mathf.PI * rise), 0);
                alpha = time > 0 ? 1 - Smooth(burst, burst + .18f, age) : 0;
                size *= 1.3f;
                return;
            }

            float after = age - burst;
            if (after < 0) return;
            if (index < 2 * VictoryArmSparks)
            {
                float along = index % VictoryArmSparks / (float)(VictoryArmSparks - 1);
                float side = index < VictoryArmSparks ? -1 : 1;
                var tip = V(crown.x + side * along * 1.11f, 2.53f + along * 1.05f, crown.z)
                    + V((Hash(index, 12) - .5f) * .05f, (Hash(index, 13) - .5f) * .06f, (Hash(index, 14) - .5f) * .1f);
                float grow = reduced ? 1 : Smooth(along * .12f, .53f + along * .23f, after);
                center = Vector3.Lerp(crown, tip, grow);
                float shimmer = reduced ? 1 : .88f + .12f * Mathf.Sin(after * 14 + index * .8f);
                alpha = .97f * shimmer * (reduced ? 1 : Smooth(0, .10f, after) * (1 - .38f * Smooth(1.30f, 1.92f, after)));
                return;
            }

            if (reduced) return;
            float angle = Hash(index, 8) * Mathf.PI * 2;
            float speed = .35f + Hash(index, 9) * 1.12f;
            center += V(Mathf.Cos(angle) * speed * after, Mathf.Sin(angle) * speed * after - .24f * after * after,
                (Hash(index, 10) - .5f) * after * .56f);
            size *= .45f + light * .22f;
            alpha = .76f * Smooth(0, .14f, after) * (1 - Smooth(.65f, 1.68f, after));
        }
        public void Teardown()
        {
            foreach (var entry in originalMaterials) if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
            originalMaterials.Clear();
            if (dressing != null) Dispose(dressing.gameObject);
            if (orbit != null) Dispose(orbit.gameObject);
            foreach (var item in owned) if (item != null) Dispose(item);
            owned.Clear(); instruments.Clear(); lamps.Clear(); ready = false;
        }
        static void Dispose(Object item) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        void OnDestroy() { if (ready) Teardown(); }
    }
}
