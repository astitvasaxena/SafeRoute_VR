using System.IO;
using System.Linq;
using SafeRoute.Evacuation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SafeRoute.EditorTools
{
    /// <summary>
    /// Builds the evacuation training scene procedurally. The building is a detailed placeholder
    /// (to be replaced by the real college model); all training behaviour lives in the runtime
    /// scripts (SafeRoute.Evacuation) and in the "Training" hierarchy, which does not depend on
    /// the building geometry.
    ///
    /// Partial class files:
    ///   .Materials     procedural textures and URP materials (one palette)
    ///   .Geometry      box meshes with metre-scaled UVs, walls with openings and surface finishes
    ///   .Architecture  floors, walls, ceilings, doors, windows, stairwell, elevator, roof
    ///   .Props         furniture, corridor fixtures and exterior details
    ///   .Signage       signs and world-space UI helpers
    ///   .Hazards       fire and smoke
    ///   .Training      zones, restricted areas, safe route markers, instructor, evacuation system
    ///   .Lighting      sun, ambient, fog, interior lights, reflection probes, post-processing
    ///
    /// Layout (metres, +X = east, +Z = north): corridor z 0..3 from Exit A (x 0, west) to Exit B
    /// (x 32, east); Classroom 1, Classroom 2 and Lab to the north; elevator alcove and stairwell
    /// to the south; assembly point outside to the east.
    /// </summary>
    public static partial class EvacuationSceneGenerator
    {
        const string ScenePath = "Assets/Scenes/SafeRoute_Evacuation.unity";
        const string XriSamples = "Assets/Samples/XR Interaction Toolkit/2.6.4";
        const string SetupPrefabPath = XriSamples + "/Starter Assets/Prefabs/XR Interaction Setup.prefab";
        const string SimulatorPrefabPath = XriSamples + "/XR Device Simulator/XR Device Simulator.prefab";

        // Building dimensions (metres).
        const float WallHeight = 3f;          // floor to underside of roof slab
        const float CeilingHeight = 2.98f;    // underside of ceiling tiles
        const float ParapetHeight = 3.6f;
        const float StairTowerHeight = 5.4f;
        const float StairCeilingHeight = 4.78f;
        const float InteriorWall = 0.2f;
        const float ExteriorWall = 0.3f;
        const float DoorHeight = 2.2f;
        const float PixelsPerMeter = 1000f;

        static readonly Vector3 StartPosition = new Vector3(7.5f, 0f, 6.3f);
        static readonly Vector3 StartLookAt = new Vector3(9.8f, 0f, 4.2f);
        static readonly Vector3 InstructorStart = new Vector3(9.5f, 0f, 4.7f);

        static Font font;

        [MenuItem("SafeRoute/Generate Evacuation Scene")]
        public static void Generate()
        {
            var setupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SetupPrefabPath);
            var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
            if (setupPrefab == null || simulatorPrefab == null)
            {
                EditorUtility.DisplayDialog("SafeRoute",
                    "XR Interaction Toolkit 2.6.4 samples not found:\n" + SetupPrefabPath + "\n" + SimulatorPrefabPath +
                    "\n\nImport Starter Assets and XR Device Simulator first.", "OK");
                return;
            }

            // Batch mode (automated builds/tests) cannot show dialogs; interactively the overwrite is always confirmed.
            if (!Application.isBatchMode)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("SafeRoute",
                        ScenePath + " already exists.\nRegenerate and overwrite it? (SafeRoute_Main is never touched.)",
                        "Overwrite", "Cancel"))
                    return;
            }

            Scene scene;
            EvacuationZone[] zones;
            Transform[] route;
            Transform origin;
            InstructorGuide instructor;
            int restrictedCount;
            try
            {
                EditorUtility.DisplayProgressBar("SafeRoute", "Creating textures and materials", 0.1f);
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                missingAudio.Clear();
                CreateMaterials();

                EditorUtility.DisplayProgressBar("SafeRoute", "Building environment", 0.4f);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                boxMeshes.Clear();
                CreateLighting(new GameObject("Lighting").transform);

                Transform environment = new GameObject("Environment (placeholder building - replace with college model)").transform;
                BuildArchitecture(Group("Architecture", environment));
                BuildInteriors(Group("Interiors", environment));
                BuildExterior(Group("Exterior", environment));
                BuildInteriorLights(Group("Interior Lighting", environment));
                BuildSignage(Group("Signage", environment));
                BuildHazards(new GameObject("Hazards").transform);

                EditorUtility.DisplayProgressBar("SafeRoute", "Building training systems", 0.7f);
                Transform training = new GameObject("Training").transform;
                EvacuationZone[] restricted = BuildRestrictedAreas(Group("Restricted Areas", training));
                restrictedCount = restricted.Length;
                zones = BuildZones(Group("Evacuation Zones", training)).Concat(restricted).ToArray();
                route = BuildRoute(Group("Safe Route", training), out int exitIndex);

                // Starter Assets setup provides exactly one XR Origin (with locomotion), camera, EventSystem,
                // XR Interaction Manager and Input Action Manager. The simulator prefab adds no duplicates.
                var setup = (GameObject)PrefabUtility.InstantiatePrefab(setupPrefab, scene);
                PrefabUtility.InstantiatePrefab(simulatorPrefab, scene);

                origin = FindChild(setup.transform, "XR Origin (XR Rig)");
                if (origin != null)
                {
                    origin.position = StartPosition;
                    origin.rotation = Quaternion.LookRotation(Flat(StartLookAt - StartPosition));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(origin);
                }
                Camera cam = setup.GetComponentInChildren<Camera>(true);
                ConfigureCamera(cam);
                Transform head = cam != null ? cam.transform : null;

                instructor = BuildInstructor(Group("Instructor", training), route, head);
                BuildEvacuationSystem(training, zones, route, exitIndex, head, origin, instructor);

                EditorUtility.DisplayProgressBar("SafeRoute", "Saving scene", 0.95f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Report(zones, route, origin != null, instructor, restrictedCount);
        }

        static void Report(EvacuationZone[] zones, Transform[] route, bool originPlaced, InstructorGuide instructor, int restrictedCount)
        {
            bool routeOk = EvacuationManager.ValidateRoute(route, zones, out string problem);
            string msg =
                $"Saved {ScenePath}\n" +
                $"Cameras: {Object.FindObjectsOfType<Camera>(true).Length}\n" +
                $"XR Origins: {CountByTypeName("XROrigin")}\n" +
                $"EventSystems: {Object.FindObjectsOfType<EventSystem>(true).Length}\n" +
                $"XR Interaction Managers: {CountByTypeName("XRInteractionManager")}\n" +
                $"Input Action Managers: {CountByTypeName("InputActionManager")}\n" +
                $"XR Device Simulators: {CountByTypeName("XRDeviceSimulator")}\n" +
                $"XR Origin placed at start: {(originPlaced ? "yes" : "NO - not found")}\n" +
                $"Instructor: {(instructor != null ? $"yes, at {instructor.transform.position} ({instructor.cues.Length} cues, {instructor.path.Length} path points)" : "NO")}\n" +
                $"Restricted warning triggers: {restrictedCount}, solid doorway blockers: {restrictedBlockers}\n" +
                $"Head collision guard: {(Object.FindObjectOfType<HeadCollisionGuard>() is HeadCollisionGuard g && g.rig != null ? "yes" : "NO")}\n" +
                FireReport(route) + "\n" +
                $"Renderers: {Object.FindObjectsOfType<Renderer>(true).Length}, lights: {Object.FindObjectsOfType<Light>(true).Length}\n" +
                AudioReport() + "\n" +
                $"Route check: {(routeOk ? "OK - avoids Exit A, the lift and restricted areas" : "FAILED - " + problem)}";
            Debug.Log("[SafeRoute] Scene generated.\n" + msg);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("SafeRoute", msg, "OK");
        }

        static int CountByTypeName(string typeName) =>
            Object.FindObjectsOfType<MonoBehaviour>(true).Count(b => b != null && b.GetType().Name == typeName);

        // ---------- Helpers ----------

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
