using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Homecoming.Field.Editor
{
    public static class FieldSceneBuilder
    {
        const string Root = "Assets/FieldSimulation";
        const string ScenePath = "Assets/Scenes/Simulation.unity";

        [MenuItem("Homecoming/Field simulation/Build scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var system = new GameObject("Field simulation");
            var controller = system.AddComponent<FieldSimulation>();
            var world = system.AddComponent<FieldWorld>();
            controller.world = world;
            controller.dataSource = Asset<TextAsset>("Data/Climate.json");
            controller.headingFont = Asset<Font>("Art/Fonts/BarlowCondensed-SemiBold.ttf");
            controller.bodyFont = Asset<Font>("Art/Fonts/Barlow-Regular.ttf");
            controller.strongFont = Asset<Font>("Art/Fonts/Barlow-SemiBold.ttf");
            controller.dataFont = Asset<Font>("Art/Fonts/SpaceMono-Regular.ttf");
            controller.commander = Asset<Texture2D>("Art/Zorvax.png");

            var eyes = new GameObject("Orbital and field camera") { tag = "MainCamera" };
            world.fieldCamera = eyes.AddComponent<Camera>();
            world.fieldCamera.nearClipPlane = .06f;
            world.fieldCamera.farClipPlane = 370;
            world.fieldCamera.backgroundColor = new Color(.004f,.008f,.016f);
            world.fieldCamera.allowMSAA = true;
            world.fieldCamera.fieldOfView = 48;
            eyes.transform.SetPositionAndRotation(new Vector3(17,19,-33), Quaternion.Euler(16,-21,0));
            eyes.AddComponent<AudioListener>();
            world.sunlight = new GameObject("Sun").AddComponent<Light>();
            world.sunlight.type = LightType.Directional;
            world.sunlight.transform.rotation = Quaternion.Euler(48, -34, 0);
            world.sunlight.intensity = 1.28f;
            world.sunlight.shadows = LightShadows.Soft;
            world.shuttlePrefab = Model("craft-cargoa");
            world.roverPrefab = Model("rover");
            world.stationPrefab = Model("satellitedish-detailed");
            world.grassPrefab = Model("grass-leafslarge");
            world.rockPrefab = Model("rock-largec");
            world.trees = new[] { Model("cactus-tall"), Model("tree-plateau"), Model("tree-palmdetailedtall"),
                Model("tree-pinetallb-detailed"), Model("tree-oak"), Model("tree-pinerounde") };
            world.landSource = Material("Land");
            world.waterSource = Material("Water");
            world.skySource = Material("Sky");
            world.precipitationSource = Material("Precipitation");
            world.globe = MeshObject("Earth — orbital view", "Earth surface", "Earth");
            var orbital = Material("Orbital");
            orbital.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Homecoming/Resources/Film/earth.png");
            EditorUtility.SetDirty(orbital);
            world.globe.GetComponent<MeshRenderer>().sharedMaterial = orbital;
            world.globe.transform.localScale = Vector3.one * 2.4f;
            world.globe.transform.position = new Vector3(0, 0, -118);
            world.globe.SetActive(false);
            world.stars = MeshObject("Orbital star field", "Star field", "Starlight");
            world.stars.transform.SetPositionAndRotation(world.globe.transform.position, Quaternion.Euler(0,180,0));
            world.stars.SetActive(false);
            world.lowRain = Ambient(system, "Air in the valley", "Valley");
            world.heavyRain = Ambient(system, "Rain on the field", "Rain");
            RenderSettings.skybox = world.skySource;
            RenderSettings.sun = world.sunlight;

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                    .Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            Debug.Log("FIELD_SCENE_OK: Created " + ScenePath);
        }

        static AudioSource Ambient(GameObject root, string label, string clip)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(root.transform, false);
            var source = obj.AddComponent<AudioSource>();
            source.clip = Asset<AudioClip>("Art/" + clip + ".wav");
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0;
            return source;
        }

        static GameObject MeshObject(string label, string mesh, string material)
        {
            var obj = new GameObject(label);
            obj.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Homecoming/Set/" + mesh + ".asset");
            obj.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Homecoming/Set/" + material + ".mat");
            return obj;
        }

        static T Asset<T>(string relative) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(Root + "/" + relative) ??
            throw new InvalidOperationException("Missing field asset: " + relative);

        static GameObject Model(string name) => Asset<GameObject>("Art/Models/" + name + ".glb");
        static Material Material(string name)
        {
            string path = Root + "/Shaders/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Asset<Shader>("Shaders/" + name + ".shader"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        [MenuItem("Homecoming/Field simulation/Build macOS preview")]
        public static void BuildPreview() => BuildInvestigationPlayer(false);

        [MenuItem("Homecoming/Field simulation/Build story-first macOS QA player")]
        public static void BuildStoryFirstPreview() => BuildInvestigationPlayer(true);

        static void BuildInvestigationPlayer(bool storyFirst)
        {
            const string story = "Assets/Scenes/Story.unity";
            string name = storyFirst ? "The Great Homecoming QA.app" : "Homecoming Field.app";
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds", name));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = storyFirst ? new[] { IntroPlayback.ScenePath, story, ScenePath } : new[] { ScenePath, story },
                locationPathName = output, target = BuildTarget.StandaloneOSX, extraScriptingDefines = new[] { "HOMECOMING_FIELD_PREVIEW" }
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Field player build failed: " + report.summary.result);
            Debug.Log("FIELD_PLAYER_OK: " + output);
        }
    }
}
