using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Homecoming.Field
{
    public sealed class FieldWorld : MonoBehaviour
    {
        public Camera fieldCamera;
        public Light sunlight;
        public GameObject globe, stars, shuttlePrefab, roverPrefab, stationPrefab, grassPrefab, rockPrefab;
        public GameObject[] trees;
        public Material landSource, waterSource, skySource, precipitationSource;
        public AudioSource lowRain, heavyRain;
        readonly List<Mesh> meshes = new();
        readonly List<Material> propMaterials = new();
        static readonly Color[] Ground = {
            Hex("#b98255"), Hex("#c4a065"), Hex("#4d7656"), Hex("#93865d"), Hex("#748654"), Hex("#a5825c")
        };
        static readonly Color[] Foliage = {
            Hex("#6b8557"), Hex("#838a45"), Hex("#378764"), Hex("#658352"), Hex("#69974d"), Hex("#638476")
        };
        static readonly Color[] Soil = {
            Hex("#94583b"), Hex("#9b693e"), Hex("#654535"), Hex("#795340"), Hex("#6e5738"), Hex("#85573d")
        };
        static readonly float[] Rivers = { .38f, .55f, 1.25f, .75f, 1f, .62f };
        static readonly Vector3 GroundLook = new(0, 1.7f, 10);
        static readonly Vector3 Landing = new(4.4f, 3.15f, -13.4f);
        static readonly int Wet = Shader.PropertyToID("_Wet"), Wind = Shader.PropertyToID("_Wind"),
            Storm = Shader.PropertyToID("_Weather"), Stress = Shader.PropertyToID("_Stress"),
            Strength = Shader.PropertyToID("_Strength"), Plant = Shader.PropertyToID("_Plant");

        Material land, water, foliage, sky, precip;
        Transform site, waterPlane, shuttle;
        Mesh rainMesh;
        Vector3[] rainVertices;
        Color32[] rainColors;
        Vector3[] drops;
        float[] speeds;
        int region = -1;
        float waterLevel, rainStrength, exposure, animationTime;
        float yaw, pitch;
        Vector3 velocity;
        Quaternion mapRotation;
        float globeRadius;
        public Vector3 RoverPosition { get; private set; }
        public Vector3 StationPosition { get; private set; }

        static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }

        public void Initialize()
        {
            land = new Material(landSource);
            water = new Material(waterSource);
            foliage = new Material(landSource);
            foliage.SetFloat(Plant, 1);
            sky = new Material(skySource);
            precip = new Material(precipitationSource);
            RenderSettings.skybox = sky;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#8aafc0");
            RenderSettings.ambientEquatorColor = Hex("#87958b");
            RenderSettings.ambientGroundColor = Hex("#736557");
            shuttle = Instantiate(shuttlePrefab, transform).transform;
            shuttle.name = "Survey shuttle";
            shuttle.gameObject.SetActive(false);
            shuttle.localScale = Vector3.one * 3.8f;
            globeRadius = globe.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x;
            MakeRain();
        }

        static Vector3 GlobePoint(float latitude, float longitude)
        {
            float lat = latitude * Mathf.Deg2Rad, lon = (longitude + 180) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
        }

        static Quaternion OrbitRotation(float latitude, float longitude) =>
            OrbitRotation(GlobePoint(latitude, longitude));

        static Quaternion OrbitRotation(Vector3 point) =>
            Quaternion.LookRotation(Vector3.back, Vector3.up) *
            Quaternion.Inverse(Quaternion.LookRotation(point, Vector3.up));

        public void SelectRegion(int index, RegionClimate data)
        {
            mapRotation = OrbitRotation(data.latitude, data.longitude);
            if (region == index) return;
            if (site != null) { site.gameObject.SetActive(false); Destroy(site.gameObject); }
            foreach (var mesh in meshes) Destroy(mesh);
            foreach (var material in propMaterials) Destroy(material);
            meshes.Clear(); propMaterials.Clear();
            region = index;
            site = new GameObject(data.name + " survey site").transform;
            site.SetParent(transform);
            BuildGround();
            BuildWater();
            BuildProps();
        }

        public void SurveyOrbit(Vector2 origin, Vector2 destination, float progress)
        {
            SetField(false);
            globe.transform.rotation = Quaternion.Slerp(OrbitRotation(origin.x, origin.y),
                OrbitRotation(destination.x, destination.y), Smooth(progress));
            var center = globe.transform.position;
            float distance = globeRadius * globe.transform.lossyScale.x * 3.45f;
            Aim(center + new Vector3(0, 0, -distance), center, 43);
        }

        public bool ProjectFootprint(Vector2 origin, Vector2 destination, float fraction, out Vector3 screen)
        {
            var a = GlobePoint(origin.x, origin.y);
            var b = GlobePoint(destination.x, destination.y);
            var local = Vector3.Slerp(a, b, fraction).normalized * (globeRadius * 1.002f);
            var point = globe.transform.TransformPoint(local);
            screen = fieldCamera.WorldToScreenPoint(point);
            return screen.z > 0 && Vector3.Dot(point - globe.transform.position, fieldCamera.transform.position - point) > 0;
        }

        float Channel(float z) => Mathf.Sin(z * .047f + .5f) * 5.8f + Mathf.Sin(z * .102f) * 1.7f - 3.6f;
        float ChannelDistance(float x, float z) => Mathf.Abs(x - Channel(z));
        float Height(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * .035f + region * 13 + 72, z * .04f + 23);
            float distance = ChannelDistance(x, z);
            float spread = Mathf.SmoothStep(0, 1, (distance - 1f) / (4.7f * Rivers[region]));
            float baseHeight = Mathf.Lerp(-1.6f + .23f * n, (n - .45f) * 1.3f + .14f, spread);
            float rim = Mathf.Max(Mathf.Abs(x) * 1.05f, z * .7f);
            float hills = Mathf.SmoothStep(0, 1, (rim - 30) / 65);
            float mountains = (n * n * 24 + Mathf.Abs(Mathf.Sin(x * .057f + z * .026f)) * 10) * hills;
            float distant = Mathf.Max(0, 1 - Mathf.Abs(z - 117) / 63) * Mathf.Max(0, 1 - Mathf.Abs(x + 36) / 83);
            return baseHeight + mountains + distant * (10 + n * 34);
        }

        Color LandColor(float x, float z, float y)
        {
            float n = Mathf.PerlinNoise(x * .11f + 29, z * .14f + 86);
            float dampBank = 1 - Mathf.SmoothStep(0, 1, (ChannelDistance(x, z) - 2) / (4 * Rivers[region]));
            Color c = Color.Lerp(Ground[region], Soil[region], dampBank * .7f + n * .26f);
            if (region == 4 && x > 3 && x < 32 && z > -15 && z < 35 && y > -.32f)
            {
                float rows = .5f + .5f * Mathf.Sin((z + x * .14f) * 2.8f);
                c = Color.Lerp(c, Hex("#92a45a"), rows * .59f);
            }
            if (y > 4)
            {
                float grade = Mathf.Clamp01((y - 3) / 23);
                c = Color.Lerp(c, region == 0 || region == 1 ? Hex("#9d695c") : Hex("#50645f"), grade * .86f);
                c = Color.Lerp(c, Hex("#767c77"), Mathf.Clamp01((y - 19) / 17));
            }
            return (c * Mathf.Lerp(.79f, 1.12f, n)).linear;
        }

        void BuildGround()
        {
            // Finer triangulation near the accessible survey area, broader triangles behind it.
            var coords = new List<float>();
            for (int p = -190; p < -60; p += 5) coords.Add(p);
            for (int p = -60; p < 66; p++) coords.Add(p);
            for (int p = 66; p <= 191; p += 5) coords.Add(p);
            int width = coords.Count;
            var positions = new List<Vector3>(width * width * 6);
            var colors = new List<Color>(positions.Capacity);
            var rand = new System.Random(92 + region * 73);
            var grid = new Vector3[width, width];
            for (int z = 0; z < width; z++)
            for (int x = 0; x < width; x++)
            {
                float px = coords[x] + (float)(rand.NextDouble() - .5) * .52f;
                float pz = coords[z] + (float)(rand.NextDouble() - .5) * .52f;
                grid[x, z] = new Vector3(px, Height(px, pz), pz);
            }
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                var mid = (a + b + c) / 3;
                var color = LandColor(mid.x, mid.z, mid.y);
                positions.Add(a); positions.Add(b); positions.Add(c);
                colors.Add(color); colors.Add(color); colors.Add(color);
            }
            for (int z = 0; z < width - 1; z++)
            for (int x = 0; x < width - 1; x++)
            {
                Triangle(grid[x,z], grid[x,z + 1], grid[x + 1,z]);
                Triangle(grid[x + 1,z], grid[x,z + 1], grid[x + 1,z + 1]);
            }
            var mesh = new Mesh { name = "Faceted watershed", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions); mesh.SetColors(colors);
            var indices = new int[positions.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            MeshObject("Watershed", mesh, land, site);
        }

        void BuildWater()
        {
            const int size = 125;
            var positions = new Vector3[size * size];
            var uv = new Vector2[positions.Length];
            var triangles = new int[(size - 1) * (size - 1) * 6];
            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                int i = z * size + x;
                float px = x - 62, pz = z - 48;
                positions[i] = new Vector3(px, 0, pz);
                uv[i] = new Vector2(Height(px, pz), 0);
                if (x == size - 1 || z == size - 1) continue;
                int j = (z * (size - 1) + x) * 6;
                triangles[j] = i; triangles[j + 1] = i + size; triangles[j + 2] = i + 1;
                triangles[j + 3] = i + 1; triangles[j + 4] = i + size; triangles[j + 5] = i + size + 1;
            }
            var mesh = new Mesh { name = "Rising water surface", vertices = positions, uv = uv, triangles = triangles };
            waterPlane = MeshObject("Stream and standing water", mesh, water, site);
        }

        struct BakedPart { public Vector3[] vertices, normals; public int[] triangles; public int material; }
        static List<BakedPart> ReadModel(GameObject prefab)
        {
            var parts = new List<BakedPart>();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var matrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                    normals[i] = matrix.MultiplyVector(normals[i]).normalized;
                }
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var mat = materials[i].name.ToLowerInvariant();
                    parts.Add(new BakedPart { vertices = vertices, normals = normals, triangles = mesh.GetTriangles(i),
                        material = mat.Contains("bark") ? 1 : mat.Contains("dirt") ? 2 : 0 });
                }
            }
            return parts;
        }

        sealed class PropBatch
        {
            readonly List<Vector3> vertices = new(), normals = new();
            readonly List<Color> colors = new();
            readonly List<int> indices = new();
            public void Add(List<BakedPart> parts, Vector3 position, Quaternion rotation, float scale, Color leaf, Color rock, System.Random random)
            {
                var trunk = Hex("#594f3b").linear;
                float variation = .83f + (float)random.NextDouble() * .31f;
                foreach (var part in parts)
                {
                    var color = (part.material == 1 ? trunk : part.material == 2 ? rock : leaf) * variation;
                    int start = vertices.Count;
                    foreach (int i in part.triangles)
                    {
                        var v = part.vertices[i];
                        vertices.Add(position + rotation * (v * scale));
                        normals.Add(rotation * part.normals[i]);
                        color.a = part.material == 1 ? 0 : Mathf.Clamp01(v.y * .9f);
                        colors.Add(color);
                        indices.Add(start++);
                    }
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
                return mesh;
            }
        }

        void BuildProps()
        {
            var random = new System.Random(7102 + region * 200);
            float Rand(float low, float high) => low + (float)random.NextDouble() * (high - low);
            var tree = ReadModel(trees[region]);
            var secondary = ReadModel(region == 0 || region == 1 ? trees[region] : trees[region == 4 ? 2 : 4]);
            var grass = ReadModel(grassPrefab);
            var rock = ReadModel(rockPrefab);
            var plants = new PropBatch();
            var stones = new PropBatch();
            Color leaves = Foliage[region].linear;
            Color rockColor = Color.Lerp(Soil[region], Hex("#8e9387"), .48f).linear;
            void Scatter(PropBatch batch, List<BakedPart> part, float x, float z, float scale, Color color) =>
                batch.Add(part, new Vector3(x, Height(x, z) - .015f, z), Quaternion.Euler(0, Rand(0, 360), 0), scale, color, rockColor, random);
            for (int i = 0; i < 420; i++)
            {
                float x = Rand(-85, 85), z = Rand(-25, 109);
                if (ChannelDistance(x, z) < 5 || (region == 4 && x > 2 && x < 27 && z < 29) ||
                    (z < 22 && z > -17 && Mathf.Abs(x) < 10)) continue;
                if ((region == 0 || region == 1) && i % 3 != 0) continue;
                if (Mathf.PerlinNoise(x * .083f + 53, z * .093f + 69) < .39f) continue;
                float y = Height(x, z);
                if (Mathf.Abs(Height(x + 1, z) - y) + Mathf.Abs(Height(x, z + 1) - y) > 1.15f) continue;
                Scatter(plants, i % 4 == 0 ? secondary : tree, x, z, Rand(1.5f, 4.65f), leaves);
            }
            foreach (var p in new[] { new Vector2(-11, -3), new Vector2(-8, 6), new Vector2(-14, 14), new Vector2(13, 14), new Vector2(18, 9), new Vector2(13, -10) })
                Scatter(plants, tree, p.x, p.y, Rand(2.3f, 3.4f), leaves);
            for (int i = 0; i < 410; i++)
            {
                float x = Rand(-22, 24), z = Rand(-19, 49);
                if (ChannelDistance(x, z) < 1.6f) continue;
                if ((region == 0 || region == 1) && i % 3 != 0) continue;
                Scatter(plants, grass, x, z, Rand(.8f, 2.0f), Color.Lerp(leaves, Hex("#a5ae5f").linear, Rand(.05f,.42f)));
            }
            if (region == 4)
                for (float z = -12; z < 31; z += 1.35f)
                for (float x = 4.4f; x < 24; x += 1.15f)
                {
                    float row = z - x * .14f + Rand(-.14f, .14f);
                    if (ChannelDistance(x, row) > 3.4f)
                        Scatter(plants, grass, x + Rand(-.19f, .19f), row, Rand(.69f, 1.3f), Hex("#8faa56").linear);
                }
            for (int i = 0; i < 115; i++)
            {
                float x = Rand(-23, 22), z = Rand(-23, 56);
                Scatter(stones, rock, x, z, Rand(.75f, 2.75f), rockColor);
            }
            MeshObject("Instanced canopy and ground plants", plants.Mesh("Foliage"), foliage, site);
            MeshObject("Stream boulders", stones.Mesh("Boulders"), land, site);

            RoverPosition = new Vector3(-5.5f, Height(-5.5f, .9f) + .37f, .9f);
            StationPosition = new Vector3(7f, Height(7f, 5.2f) + 1.55f, 5.2f);
            SiteProp(roverPrefab, "Survey rover", RoverPosition - Vector3.up * .37f, 2.8f, 142);
            SiteProp(stationPrefab, "Relay dish", StationPosition - Vector3.up * 1.55f, 3.3f, 227);
        }

        void SiteProp(GameObject prefab, string label, Vector3 position, float scale, float angle)
        {
            var prop = Instantiate(prefab, position, Quaternion.Euler(0, angle, 0), site);
            prop.name = label;
            prop.transform.localScale = Vector3.one * scale;
            var cache = new Dictionary<Material, Material>();
            foreach (var renderer in prop.GetComponentsInChildren<MeshRenderer>())
            {
                var assigned = renderer.sharedMaterials;
                for (int i = 0; i < assigned.Length; i++)
                {
                    var source = assigned[i];
                    if (!cache.TryGetValue(source, out var material))
                    {
                        material = new Material(source);
                        cache.Add(source, material);
                        propMaterials.Add(material);
                        if (source.name.IndexOf("metalRed", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            material.color = Hex("#f3af46");
                    }
                    assigned[i] = material;
                }
                renderer.sharedMaterials = assigned;
            }
        }

        Transform MeshObject(string label, Mesh mesh, Material material, Transform parent)
        {
            meshes.Add(mesh);
            var obj = new GameObject(label);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return obj.transform;
        }

        void MakeRain()
        {
            const int count = 1050;
            var random = new System.Random(969);
            drops = new Vector3[count];
            speeds = new float[count];
            rainVertices = new Vector3[count * 4];
            rainColors = new Color32[count * 4];
            var tris = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                drops[i] = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                speeds[i] = 1 + (float)random.NextDouble() * .47f;
                int v = i * 4, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v + 1; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
                byte alpha = (byte)(60 + random.Next(85));
                rainColors[v] = new Color32(255,255,255,alpha);
                rainColors[v + 1] = rainColors[v];
                rainColors[v + 2] = new Color32(255,255,255,0);
                rainColors[v + 3] = rainColors[v + 2];
            }
            rainMesh = new Mesh { name = "Local precipitation", vertices = rainVertices, colors32 = rainColors, triangles = tris };
            rainMesh.MarkDynamic();
            var obj = new GameObject("Wind-driven rain");
            obj.transform.SetParent(transform);
            obj.AddComponent<MeshFilter>().sharedMesh = rainMesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = precip;
        }

        public void Apply(FieldMonth month, float dt, bool immediate, bool muted, bool inField, bool reduced)
        {
            float precipitation = Mathf.Clamp01(Mathf.Pow(month.weather.rain / 315f, .65f));
            float saturation = Mathf.SmoothStep(0, 1, (month.land.soil - .58f) / .36f);
            float storm = precipitation * .85f + saturation * .15f;
            float blend = immediate ? 1 : 1 - Mathf.Exp(-dt * 2.5f);
            float baseLevel = Mathf.Lerp(-1.43f, -.81f, Rivers[region] / 1.25f);
            waterLevel = Mathf.Lerp(waterLevel, Mathf.Lerp(baseLevel - month.land.drought * .22f, .30f, saturation), blend);
            waterPlane.position = new Vector3(0, waterLevel, 0);
            exposure = Mathf.Lerp(exposure, storm, blend);
            rainStrength = Mathf.Lerp(rainStrength, inField ? precipitation : 0, blend);
            land.SetFloat(Wet, saturation);
            water.SetFloat(Storm, exposure);
            sky.SetFloat(Storm, exposure);
            foliage.SetFloat(Stress, Mathf.Clamp01((.76f - month.land.vegetation) * 1.8f));
            foliage.SetFloat(Wind, reduced ? 0 : month.weather.wind);
            sunlight.intensity = Mathf.Lerp(1.36f, .78f, exposure);
            sunlight.color = Color.Lerp(Hex("#ffe1a5"), Hex("#c7e2f5"), exposure);
            RenderSettings.fogColor = Color.Lerp(Hex("#a1bdbc"), Hex("#7e9fa6"), exposure);
            RenderSettings.fogDensity = Mathf.Lerp(.007f, .022f, exposure);
            lowRain.volume = muted || !inField ? 0 : .24f * Mathf.Lerp(.24f, 1, rainStrength);
            heavyRain.volume = muted || !inField ? 0 : rainStrength * rainStrength * .36f;
            if (!reduced) animationTime += dt;
            PaintRain(reduced ? rainStrength * .46f : rainStrength, month.weather.wind);
        }

        void PaintRain(float strength, float wind)
        {
            precip.SetFloat(Strength, strength);
            if (strength < .015f) return;
            var cam = fieldCamera.transform;
            var side = cam.right * .017f;
            float t = animationTime;
            var center = cam.position + cam.forward * 7;
            for (int i = 0; i < drops.Length; i++)
            {
                var d = drops[i];
                var point = center + new Vector3((Mathf.Repeat(d.x + t * .011f * wind, 1) - .5f) * 31,
                    (Mathf.Repeat(d.y - t * .92f * speeds[i], 1) - .5f) * 19,
                    (d.z - .5f) * 31);
                var tail = point + new Vector3(-.075f - wind * .019f, .35f + .48f * strength, -.03f) * speeds[i];
                int v = i * 4;
                rainVertices[v] = point - side; rainVertices[v + 1] = point + side;
                rainVertices[v + 2] = tail - side; rainVertices[v + 3] = tail + side;
            }
            rainMesh.vertices = rainVertices;
            rainMesh.bounds = new Bounds(center, Vector3.one * 38);
        }

        public void Overview(float t, bool reduced)
        {
            var pose = OverviewPose(t,reduced);
            SetField(true);
            SetCamera(pose,48);
        }

        static Pose OverviewPose(float t, bool reduced)
        {
            float drift = reduced ? 0 : Mathf.Sin(t * .23f) * 2.1f;
            Vector3 position = new(17 + drift, 19, -33);
            return new Pose(position, Quaternion.LookRotation(new Vector3(-7, 1, 17) - position));
        }

        public float TransferDeparture(Pose origin, float fov, float progress)
        {
            SetField(true);
            float t = Smooth(progress);
            Vector3 position = Vector3.Lerp(origin.position,new Vector3(29,137,-58),t);
            position.x += Mathf.Sin(t*Mathf.PI)*9;
            Quaternion high = Quaternion.LookRotation(new Vector3(-7,0,17)-new Vector3(29,137,-58));
            SetCamera(new Pose(position,Quaternion.Slerp(origin.rotation,high,t)),Mathf.Lerp(fov,54,t));
            return Smooth((progress-.64f)/.36f);
        }

        public void TransferOrbit(RegionClimate from, RegionClimate to, float route, float close)
        {
            SetField(false);
            var point = Vector3.Slerp(GlobePoint(from.latitude,from.longitude),GlobePoint(to.latitude,to.longitude),Smooth(route));
            globe.transform.rotation = OrbitRotation(point);
            Vector3 center = globe.transform.position;
            float radius = globeRadius * globe.transform.lossyScale.x;
            float distance = radius * Mathf.Lerp(3.4f,1.12f,close);
            Aim(center + Vector3.back*distance,center,43);
        }

        public float TransferArrival(float labTime, bool reduced, float progress)
        {
            SetField(true);
            float t = Smooth(progress);
            var destination = OverviewPose(labTime,reduced);
            Vector3 high = new(31,129,-49);
            Vector3 position = Vector3.Lerp(high,destination.position,t);
            position.x += Mathf.Sin(t*Mathf.PI)*7;
            Quaternion rotation = Quaternion.Slerp(Quaternion.LookRotation(new Vector3(-7,0,17)-high),destination.rotation,t);
            SetCamera(new Pose(position,rotation),Mathf.Lerp(54,48,t));
            return 1-Smooth(progress/.37f);
        }

        public void RestoreTerrainCamera(Pose pose, float fov)
        {
            SetField(true);
            SetCamera(pose,fov);
        }

        void SetCamera(Pose pose, float fov)
        {
            fieldCamera.transform.SetPositionAndRotation(pose.position,pose.rotation);
            fieldCamera.fieldOfView = fov;
        }

        public float Descend(float seconds, bool reduced)
        {
            if (!reduced && seconds < 1.4f)
            {
                SetField(false);
                float p = Smooth(seconds / 1.4f);
                globe.transform.rotation = Quaternion.Slerp(Quaternion.Euler(13, -15, -3) * mapRotation, mapRotation, p);
                Aim(new Vector3(.9f - p * .9f, .16f - p * .16f, -13.4f + 9.7f * p) + globe.transform.position,
                    globe.transform.position + new Vector3(0, 0, -.55f), 43);
                return Mathf.SmoothStep(0, 1, (seconds - 1.07f) / .33f);
            }

            SetField(true);
            float a = reduced ? 1 : Smooth((seconds - 1.4f) / 4.2f);
            var position = Vector3.Lerp(new Vector3(17, 48, -41), Landing, a);
            position.x += Mathf.Sin(a * Mathf.PI) * 5;
            var look = Vector3.Lerp(new Vector3(0, 0, 17), GroundLook, a);
            Aim(position, look, Mathf.Lerp(53, 60, a));
            shuttle.gameObject.SetActive(!reduced && seconds > 1.68f && seconds < 4.65f);
            shuttle.position = new Vector3(2.6f + 3 * a, Mathf.Lerp(31, 3.8f, a), Mathf.Lerp(-21, 10, a));
            shuttle.rotation = Quaternion.Euler(8 + 9 * (1 - a), -9, -16 * (1 - a));
            return reduced ? 0 : 1 - Mathf.SmoothStep(0, 1, (seconds - 1.4f) / .62f);
        }

        public void Land()
        {
            SetField(true);
            shuttle.gameObject.SetActive(false);
            Aim(Landing, GroundLook, 60);
            Vector3 angles = fieldCamera.transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x > 180 ? angles.x - 360 : angles.x;
            velocity = Vector3.zero;
        }

        public void Explore(float dt, Vector2 movement, Vector2 look, bool reduced)
        {
            yaw += look.x * 2.2f;
            pitch = Mathf.Clamp(pitch - look.y * 2.2f, -66, 67);
            var cam = fieldCamera.transform;
            var direction = Quaternion.Euler(0, yaw, 0) * new Vector3(movement.x, 0, movement.y);
            velocity = Vector3.Lerp(velocity, Vector3.ClampMagnitude(direction, 1) * 5, 1 - Mathf.Exp(-dt * 9));
            Vector3 p = cam.position + velocity * dt;
            p.x = Mathf.Clamp(p.x, -29, 29);
            p.z = Mathf.Clamp(p.z, -26, 39);
            float ground = Mathf.Max(Height(p.x, p.z), waterLevel);
            p.y = Mathf.Lerp(p.y, ground + 2.3f, 1 - Mathf.Exp(-dt * (reduced ? 12 : 5)));
            cam.SetPositionAndRotation(p, Quaternion.Euler(pitch, yaw, 0));
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        void Aim(Vector3 at, Vector3 target, float fov)
        {
            fieldCamera.transform.SetPositionAndRotation(at, Quaternion.LookRotation(target - at));
            fieldCamera.fieldOfView = fov;
        }

        void SetField(bool active)
        {
            if (site != null && site.gameObject.activeSelf != active) site.gameObject.SetActive(active);
            if (globe.activeSelf == active) globe.SetActive(!active);
            if (stars.activeSelf == active) stars.SetActive(!active);
            if (shuttle.gameObject.activeSelf && !active) shuttle.gameObject.SetActive(false);
            fieldCamera.clearFlags = active ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            RenderSettings.fog = active;
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) Destroy(mesh);
            foreach (var material in propMaterials) Destroy(material);
            foreach (var material in new[] { land, water, foliage, sky, precip }) if (material != null) Destroy(material);
            if (rainMesh != null) Destroy(rainMesh);
        }
    }
}
