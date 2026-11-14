using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Homecoming.Editor
{
    public static class StoryProjectBuilder
    {
        const string Root = "Assets/Homecoming";
        const string Generated = Root + "/Set";
        static Material navy, metal, copper, cream, dark, glass, amber, red, cyan, bannerMat;
        static Font font;
        static Mesh roundedBox;
        static Transform set;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        [MenuItem("Homecoming/Rebuild story scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            roundedBox = RoundedBox();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            navy = Mat("Paint - deep naval blue", "#283940", .14f, .28f);
            metal = Mat("Brushed aluminium", "#748181", .65f, .4f);
            copper = Mat("Aged copper", "#956347", .52f, .32f);
            cream = Mat("Warm enamel", "#C6BBA2", .12f, .3f);
            dark = Mat("Rubber and graphite", "#141D21", .04f, .16f);
            glass = Mat("Monitor glass", "#070D10", 0, .6f);
            amber = Mat("Amber indicator", "#C99148", 0, .1f, true);
            red = Mat("Switch enamel", "#AC4936", .14f, .28f);
            cyan = Mat("Projector phosphor", "#659698", 0, .1f, true);
            bannerMat = Mat("Faded welcome bunting", "#874E46", 0, .04f);

            set = new GameObject("HOMEcoming — film set").transform;
            var stage = set.gameObject.AddComponent<StoryStage>();
            stage.bridge = Group("Bridge — editable scenery", set).gameObject;
            Bridge(stage.bridge.transform);
            stage.stars = Group("Stars", set).gameObject;
            Starfield(stage.stars.transform);
            stage.moon = Planet("Moon", "moon-albedo", set, 1.17f, false, true);
            stage.earth = Planet("Earth", "earth", set, 1.23f, false, false);
            stage.projection = Planet("Earth projector", "earth", stage.bridge.transform, .7f, true, false);

            var cameraGo = new GameObject("Film camera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.004f, .008f, .014f);
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 250;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            cameraGo.AddComponent<AudioListener>();
            stage.filmCamera = camera;
            Light("Warm studio key", LightType.Directional, V(-3, 6, 4), new Color(1, .87f, .7f), 1.65f, V(35, -32, 0));
            Light("Cold window fill", LightType.Directional, V(3, 4, -3), new Color(.45f, .65f, .75f), .75f, V(30, 145, 0));
            stage.practical = Light("Console practical", LightType.Point, V(1.8f, 2.9f, .5f), new Color(1, .62f, .3f), 1.5f, Vector3.zero);
            stage.practical.range = 8;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.3f, .36f, .4f);
            RenderSettings.ambientEquatorColor = new Color(.21f, .24f, .25f);
            RenderSettings.ambientGroundColor = new Color(.12f, .11f, .1f);
            RenderSettings.fog = false;

            var tv = Group("Television — 1969 receiver", stage.bridge.transform);
            tv.localPosition = V(1.95f, 2.05f, -1.5f);
            Box("Cabinet", tv, Vector3.zero, V(2.9f, 2.05f, .6f), navy);
            Box("Copper bezel", tv, V(-.12f, .06f, .34f), V(2.62f, 1.8f, .1f), copper);
            Box("Screen gasket", tv, V(-.12f, .06f, .405f), V(2.42f, 1.62f, .05f), dark);
            var screenMaterial = Unlit("Archival screen", Color.white);
            screenMaterial.shader = Shader.Find("Homecoming/Television");
            screenMaterial.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Resources/Film/apollo11.jpg");
            EditorUtility.SetDirty(screenMaterial);
            stage.televisionMaterial = screenMaterial;
            stage.screen = Quad("Broadcast surface", tv, V(-.12f, .06f, .44f), V(2.3f, 1.5f, 1), screenMaterial).GetComponent<Renderer>();
            stage.monitorTitle = Label("Broadcast title", "ARTEMIS", tv, V(-.12f, .23f, .46f), .21f, new Color(.88f, .9f, .83f));
            stage.monitorSubtitle = Label("Broadcast strapline", "BACK TO THE MOON", tv, V(-.12f, -.2f, .46f), .075f, new Color(.7f, .75f, .7f));
            stage.monitorTitle.gameObject.SetActive(false);
            stage.monitorSubtitle.gameObject.SetActive(false);
            for (int i = 0; i < 2; i++)
                Sphere("Tuning dial", tv, V(1.26f, .4f - i * .6f, .38f), V(.15f, .15f, .09f), cream);
            for (int i = 0; i < 9; i++) Box("Speaker slot", tv, V(-.85f + i * .18f, -.89f, .36f), V(.085f, .025f, .035f), dark);
            Box("TV pedestal", tv, V(0, -1.55f, -.07f), V(.22f, 1.2f, .27f), metal);
            stage.television = tv.gameObject.AddComponent<VideoPlayer>();
            stage.television.source = VideoSource.VideoClip;
            stage.television.clip = AssetDatabase.LoadAssetAtPath<VideoClip>(Root + "/Resources/Film/apollo11.mp4");
            stage.television.playOnAwake = false;

            stage.banner = Group("Welcome home — the same banner", stage.bridge.transform).gameObject;
            stage.banner.transform.localPosition = V(-.25f, 3.55f, -1.1f);
            Ribbon(stage.banner.transform);
            Label("Welcome lettering", "WELCOME HOME!", stage.banner.transform, V(0, 0, .035f), .18f, new Color(.91f, .82f, .62f));
            stage.cannon = Group("Party cannon", stage.bridge.transform).gameObject;
            stage.cannon.transform.localPosition = V(.8f, .88f, .57f);
            var barrel = Cylinder("Barrel", stage.cannon.transform, V(0, .1f, 0), V(.35f, .48f, .35f), copper);
            barrel.localRotation = Quaternion.Euler(70, 0, 0);
            var muzzle = Cylinder("Black muzzle", stage.cannon.transform, V(0, .26f, .44f), V(.31f, .025f, .31f), dark);
            muzzle.localRotation = barrel.localRotation;
            Cylinder("Base", stage.bridge.transform, V(.8f, .22f, .57f), V(.64f, .1f, .64f), dark);
            Cylinder("Support", stage.bridge.transform, V(.8f, .5f, .57f), V(.1f, .24f, .1f), metal);
            stage.balloon = Sphere("Last party balloon", stage.bridge.transform, V(.15f, 1.9f, .5f), V(.48f, .62f, .48f), red).gameObject;
            Line("Balloon string", stage.balloon.transform, new[] { V(0, -.45f, 0), V(.06f, -.8f, 0), V(-.08f, -1.15f, 0), V(0, -1.5f, 0) }, .007f, cream);

            stage.switchProp = Group("Red launch switch", stage.bridge.transform).gameObject;
            stage.switchProp.transform.localPosition = V(-.05f, .88f, 1.0f);
            Box("Switch housing", stage.switchProp.transform, Vector3.zero, V(.85f, .25f, .6f), metal);
            Cylinder("Red pushbutton", stage.switchProp.transform, V(0, .19f, 0), V(.3f, .065f, .3f), red);
            stage.report = Group("Submitted report", stage.bridge.transform).gameObject;
            stage.report.transform.localPosition = V(-.5f, 1.18f, 1.22f);
            stage.report.transform.localRotation = Quaternion.Euler(-12, -8, -4);
            Box("Report paper", stage.report.transform, Vector3.zero, V(.92f, .7f, .04f), cream);
            Label("Report heading", "SIMULATION", stage.report.transform, V(0, .13f, .025f), .075f, new Color(.22f, .28f, .29f));
            Label("Report subheading", "THE GREAT HOMECOMING", stage.report.transform, V(0, -.1f, .025f), .031f, new Color(.22f, .28f, .29f));

            var confetti = Group("Foil confetti", stage.bridge.transform);
            var paperColors = new[] { "#C39154", "#9D5551", "#6C999A", "#CBC2A2", "#748286" };
            var paperMats = paperColors.Select((c, i) => Mat("Foil " + i, c, .25f, .32f)).ToArray();
            stage.confetti = new Transform[70];
            for (int i = 0; i < stage.confetti.Length; i++)
                stage.confetti[i] = Box("Foil " + i, confetti, Vector3.zero, V(.035f, .11f, .009f), paperMats[i % paperMats.Length]);

            var crew = Group("Crew — original rigs and animation", stage.bridge.transform);
            var names = new[] { "commander-zorvax", "dr-nyxen", "alien-student" };
            float[] heights = { 2.9f, 3.03f, 2.35f };
            stage.crew = new StoryActor[3];
            for (int i = 0; i < names.Length; i++)
            {
                string path = Root + "/Resources/Crew/" + names[i] + ".glb";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) throw new InvalidOperationException("Character did not import: " + path);
                var actorRoot = Group(names[i], crew);
                actorRoot.localScale = Vector3.one * (2.2f / heights[i] * (i == 2 ? .8f : 1));
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(asset, actorRoot);
                var actor = actorRoot.gameObject.AddComponent<StoryActor>();
                actor.role = i;
                actor.rig = rig;
                actor.clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
                if (!actor.clips.Any(c => c.name == "Idle")) throw new InvalidOperationException("Missing Idle animation in " + path);
                stage.crew[i] = actor;
                foreach (var renderer in rig.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
                actor.Initialize();
                actor.Pose("home", 1, true);
                Debug.Log(names[i] + " imported with " + actor.clips.Length + " original animation clips.");
            }
            var directorGo = new GameObject("STORY — playback and scene handoff");
            var director = directorGo.AddComponent<StoryDirector>();
            director.stage = stage;
            director.script = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Resources/Story.json");
            var story = JsonUtility.FromJson<StoryScript>(director.script.text);
            if (story.prologue.Length != 11 || story.briefing.Length != 1 || story.ending.Length != 2)
                throw new InvalidOperationException("The source story must have 11 prologue, 1 briefing and 2 ending shots.");

            // Set an intentional, lit starting composition in the editor without preparing video.
            stage.bridge.SetActive(true);
            stage.moon.SetActive(false);
            stage.earth.SetActive(false);
            stage.projection.SetActive(false);
            stage.report.SetActive(false);
            stage.switchProp.SetActive(false);
            stage.banner.transform.localRotation = Quaternion.Euler(0, 0, -9);
            foreach (var foil in stage.confetti) foil.gameObject.SetActive(false);
            camera.transform.position = V(-.4f, 2.15f, 9.8f);
            camera.transform.LookAt(V(.3f, 1.55f, -.2f));
            camera.fieldOfView = 36;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            PlayerSettings.companyName = "NASA Space Apps Kuwait";
            PlayerSettings.productName = "The Great Homecoming";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, true);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Story.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Story.unity", true) };
            Selection.activeGameObject = directorGo;
            Debug.Log("HOMECOMING_BUILD_OK: Story.unity, 14 shots, 3 animated character rigs; no simulation scene.");
        }

        static Material Mat(string name, string hex, float metallic, float smooth, bool emission = false)
        {
            string path = Generated + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            ColorUtility.TryParseHtmlString(hex, out var color);
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smooth);
            if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .6f); }
            EditorUtility.SetDirty(material);
            return material;
        }
        static Material Unlit(string name, Color color)
        {
            string path = Generated + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Unlit/Texture")); AssetDatabase.CreateAsset(material, path); }
            // Unlit/Texture ignores tint; Unlit/Transparent is avoided for opaque CRT depth.
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }
        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        static Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }
        static Transform Box(string n, Transform p, Vector3 x, Vector3 s, Material m)
        {
            var t = Primitive(n, PrimitiveType.Cube, p, x, s, m);
            t.GetComponent<MeshFilter>().sharedMesh = roundedBox;
            return t;
        }
        static Mesh RoundedBox()
        {
            const string path = Generated + "/Soft metal edges.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null) return saved;
            var mesh = new Mesh { name = "Soft metal edges" };
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var indices = new List<int>();
            float[] grid = { -.5f, -.49f, -.465f, -.445f, .445f, .465f, .49f, .5f };
            var faces = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var normal in faces)
            {
                Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > .5f ? Vector3.forward : Vector3.up);
                Vector3 v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                for (int y = 0; y < grid.Length; y++)
                    for (int x = 0; x < grid.Length; x++)
                    {
                        Vector3 p = normal * .5f + u * grid[x] + v * grid[y];
                        Vector3 core = new Vector3(Mathf.Clamp(p.x, -.445f, .445f), Mathf.Clamp(p.y, -.445f, .445f), Mathf.Clamp(p.z, -.445f, .445f));
                        Vector3 n = (p - core).normalized;
                        vertices.Add(core + n * .055f); normals.Add(n);
                        if (x + 1 < grid.Length && y + 1 < grid.Length)
                        {
                            int i = start + y * grid.Length + x;
                            indices.AddRange(new[] { i, i + 1, i + grid.Length, i + 1, i + grid.Length + 1, i + grid.Length });
                        }
                    }
            }
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        static Transform Cylinder(string n, Transform p, Vector3 x, Vector3 s, Material m) => Primitive(n, PrimitiveType.Cylinder, p, x, s, m);
        static Transform Sphere(string n, Transform p, Vector3 x, Vector3 s, Material m) => Primitive(n, PrimitiveType.Sphere, p, x, s, m);
        static GameObject Quad(string n, Transform p, Vector3 x, Vector3 s, Material m)
        {
            var t = Primitive(n, PrimitiveType.Quad, p, x, s, m);
            t.localRotation = Quaternion.Euler(0, 180, 0);
            return t.gameObject;
        }
        static TextMesh Label(string name, string text, Transform parent, Vector3 pos, float size, Color color)
        {
            var t = Group(name, parent);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0, 180, 0);
            var mesh = t.gameObject.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 80;
            mesh.characterSize = size * 10 / 80;
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.GetComponent<Renderer>().sharedMaterial = font.material;
            return mesh;
        }
        static Light Light(string name, LightType type, Vector3 pos, Color color, float intensity, Vector3 angles)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.eulerAngles = angles;
            var light = go.AddComponent<Light>();
            light.type = type; light.color = color; light.intensity = intensity;
            light.shadows = type == LightType.Directional ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = .55f;
            return light;
        }
        static void Bridge(Transform root)
        {
            Box("Floor", root, V(0, -.16f, -.6f), V(13, .25f, 9), navy);
            for (int i = -5; i <= 5; i++) Box("Deck seam", root, V(i, -.028f, -.6f), V(.012f, .006f, 9), dark);
            for (int i = -3; i <= 3; i++) Box("Deck seam", root, V(0, -.028f, i), V(13, .006f, .014f), dark);
            Box("Port wall", root, V(-5.6f, 2.2f, -3.8f), V(3, 4.7f, .4f), navy);
            Box("Starboard wall", root, V(4.3f, 2.2f, -3.8f), V(5.6f, 4.7f, .4f), navy);
            Box("Upper lintel", root, V(0, 4.8f, -3.8f), V(13, 1.3f, .5f), navy);
            Box("Lower bulkhead", root, V(-1.8f, .05f, -3.8f), V(4.7f, .4f, .5f), navy);
            Ring("Observation window — outer rim", root, V(-1.8f, 2.35f, -3.5f), 2.05f, 2.25f, copper);
            Ring("Observation window — inner gasket", root, V(-1.8f, 2.35f, -3.4f), 1.94f, 2.05f, dark);
            Ring("Observation window — aluminium edge", root, V(-1.8f, 2.35f, -3.3f), 1.91f, 1.95f, metal);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                Sphere("Window fastener", root, V(-1.8f + Mathf.Cos(angle) * 2.15f, 2.35f + Mathf.Sin(angle) * 2.15f, -3.23f), V(.06f, .06f, .04f), metal);
            }
            for (int i = 0; i < 3; i++)
            {
                var rib = Box("Hull rib", root, V(-5.7f + i * 5.7f, 4.35f, -1), V(.18f, .18f, 7), metal);
                rib.localRotation = Quaternion.Euler(0, 0, i == 0 ? -12 : 12);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var console = Group(side < 0 ? "Port console" : "Starboard console", root);
                console.localPosition = V(side * 4.35f, .72f, -1.6f);
                Box("Console body", console, Vector3.zero, V(2, 1.4f, 1.4f), navy);
                var top = Box("Control fascia", console, V(0, .76f, .07f), V(2.08f, .15f, 1.5f), metal);
                top.localRotation = Quaternion.Euler(-12, 0, 0);
                for (int j = 0; j < 6; j++)
                {
                    Box("Key", console, V(-.7f + j * .27f, .89f, .43f), V(.11f, .04f, .18f), j % 3 == 0 ? amber : cream);
                    Box("Readout", console, V(-.62f + j * .25f, .99f, -.3f), V(.12f, .05f, .17f), j % 2 == 0 ? cyan : dark);
                }
                Label("Console stencil", side < 0 ? "SIGNAL / 01" : "RELOCATION / 57", console, V(0, .25f, .711f), .09f, new Color(.65f, .7f, .65f));
            }
            var projector = Group("Research projector", root);
            projector.localPosition = V(0, .22f, -.8f);
            Cylinder("Projector base", projector, Vector3.zero, V(1.7f, .2f, 1.7f), dark);
            Cylinder("Projector rim", projector, V(0, .24f, 0), V(1.4f, .04f, 1.4f), copper);
            Cylinder("Projection lens", projector, V(0, .29f, 0), V(.92f, .025f, .92f), cyan);
            Label("Hull identification", "I / A / D", root, V(4.2f, 3.6f, -3.56f), .23f, new Color(.5f, .56f, .56f));
            Label("Hull stencil", "INTERGALACTIC\nAFFECTION DEPARTMENT", root, V(4.2f, 2.95f, -3.56f), .08f, new Color(.5f, .56f, .56f));
        }
        static void Line(string name, Transform parent, Vector3[] points, float width, Material mat)
        {
            var go = Group(name, parent).gameObject;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.positionCount = points.Length; line.SetPositions(points);
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = mat;
        }
        static void Ring(string name, Transform parent, Vector3 position, float inner, float outer, Material material)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i <= 96; i++)
            {
                float angle = i * Mathf.PI * 2 / 96;
                vertices.Add(V(Mathf.Cos(angle) * inner, Mathf.Sin(angle) * inner, 0));
                vertices.Add(V(Mathf.Cos(angle) * outer, Mathf.Sin(angle) * outer, 0));
                if (i < 96) { int k = i * 2; triangles.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            MeshProp(name, parent, position, mesh, material);
        }
        static void Ribbon(Transform parent)
        {
            var mesh = new Mesh { name = "Welcome banner silhouette" };
            mesh.vertices = new[] { V(-1.85f, .23f, 0), V(1.85f, .23f, 0), V(1.7f, 0, 0), V(1.85f, -.23f, 0), V(-1.85f, -.23f, 0), V(-1.7f, 0, 0) };
            mesh.triangles = new[] { 0, 5, 2, 0, 2, 1, 5, 4, 3, 5, 3, 2 };
            mesh.RecalculateNormals();
            MeshProp("Welcome banner cloth", parent, Vector3.zero, mesh, bannerMat);
            Line("Top stitching", parent, new[] { V(-1.6f, .19f, .015f), V(1.6f, .19f, .015f) }, .012f, cream);
            Line("Bottom stitching", parent, new[] { V(-1.6f, -.19f, .015f), V(1.6f, -.19f, .015f) }, .012f, cream);
        }
        static void MeshProp(string name, Transform parent, Vector3 position, Mesh mesh, Material material)
        {
            string path = Generated + "/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); }
            var t = Group(name, parent); t.localPosition = position;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = saved;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        static GameObject Planet(string name, string texture, Transform parent, float radius, bool hologram, bool moon)
        {
            string path = Generated + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Homecoming/Planet")); AssetDatabase.CreateAsset(mat, path); }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Resources/Film/" + texture + (texture == "moon-albedo" ? ".jpg" : ".png"));
            mat.SetTexture("_NightTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Resources/Film/earth-night.png"));
            mat.SetFloat("_Hologram", hologram ? 1 : 0);
            mat.SetFloat("_IsMoon", moon ? 1 : 0);
            EditorUtility.SetDirty(mat);
            var mesh = new Mesh { name = name + " sphere" };
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            const int columns = 96, rows = 64;
            for (int y = 0; y <= rows; y++)
            {
                float latitude = y * Mathf.PI / rows;
                for (int x = 0; x <= columns; x++)
                {
                    float longitude = x * Mathf.PI * 2 / columns;
                    vertices.Add(V(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude)) * radius);
                    uv.Add(new Vector2(x / (float)columns, 1 - y / (float)rows));
                    if (x < columns && y < rows)
                    {
                        int i = y * (columns + 1) + x;
                        indices.AddRange(new[] { i, i + 1, i + columns + 1, i + 1, i + columns + 2, i + columns + 1 });
                    }
                }
            }
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals();
            var root = Group(name, parent);
            root.localPosition = V(0, 1.2f, 0);
            MeshProp(name + " surface", root, Vector3.zero, mesh, mat);
            return root.gameObject;
        }
        static void Starfield(Transform parent)
        {
            var material = Unlit("Starlight", Color.white);
            // Single mesh, fixed seed, all stars behind the ship and planets.
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var random = new System.Random(1969);
            for (int i = 0; i < 420; i++)
            {
                float x = (float)random.NextDouble() * 180 - 90, y = (float)random.NextDouble() * 100 - 45;
                float z = -55 - (float)random.NextDouble() * 25, r = .025f + (float)random.NextDouble() * .055f;
                int k = vertices.Count;
                vertices.AddRange(new[] { V(x - r, y - r, z), V(x + r, y - r, z), V(x + r, y + r, z), V(x - r, y + r, z) });
                triangles.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            var mesh = new Mesh { name = "Stars" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            MeshProp("Star field", parent, Vector3.zero, mesh, material);
        }
    }
}
