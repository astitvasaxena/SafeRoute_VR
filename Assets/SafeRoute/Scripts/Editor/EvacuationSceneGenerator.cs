using System.IO;
using System.Linq;
using SafeRoute.Evacuation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SafeRoute.EditorTools
{
    /// <summary>
    /// Builds the greybox evacuation scene from Unity primitives. Scene construction only:
    /// all training behaviour lives in the runtime scripts (SafeRoute.Evacuation).
    /// Layout (metres, +X = east, +Z = north): corridor z 0..3 from x 0 (Exit A, west) to
    /// x 32 (Exit B, east); classrooms to the north; elevator alcove and stairwell to the south;
    /// assembly point outside to the east.
    /// </summary>
    public static class EvacuationSceneGenerator
    {
        const string ScenePath = "Assets/Scenes/SafeRoute_Evacuation.unity";
        const string MaterialFolder = "Assets/SafeRoute/Materials";
        const string XriSamples = "Assets/Samples/XR Interaction Toolkit/2.6.4";
        const string SetupPrefabPath = XriSamples + "/Starter Assets/Prefabs/XR Interaction Setup.prefab";
        const string SimulatorPrefabPath = XriSamples + "/XR Device Simulator/XR Device Simulator.prefab";
        const string UrpParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        const float WallHeight = 3f;
        const float WallThickness = 0.2f;
        const float DoorHeight = 2.2f;
        const float PixelsPerMeter = 1000f;

        static readonly Vector3 StartPosition = new Vector3(7.5f, 0f, 6.3f);
        static readonly Vector3 StartLookAt = new Vector3(9.8f, 0f, 4.2f);

        static readonly Color SignRed = new Color(0.7f, 0.05f, 0.05f);
        static readonly Color SignGreen = new Color(0.0f, 0.5f, 0.2f);
        static readonly Color SignDark = new Color(0.12f, 0.12f, 0.14f);

        static Font font;
        static Material mFloor, mWall, mGround, mPath, mDoorBlocked, mMetal, mConcrete, mDesk, mDark;
        static Material mRoute, mFire, mAssembly, mSmokeHaze, mSmokeParticles;

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

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("SafeRoute",
                    ScenePath + " already exists.\nRegenerate and overwrite it? (SafeRoute_Main is never touched.)",
                    "Overwrite", "Cancel"))
                return;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();

            Transform environment = new GameObject("Environment").transform;
            BuildBuilding(Group("Building", environment));
            BuildOutside(Group("Outside", environment));
            BuildSignage(Group("Signage", environment));
            BuildHazards(new GameObject("Hazards").transform);
            EvacuationZone[] zones = BuildZones(new GameObject("Evacuation Zones").transform);
            Transform[] route = BuildRoute(new GameObject("Safe Route").transform, out int exitIndex);

            // Starter Assets setup provides exactly one XR Origin (with locomotion), camera, EventSystem,
            // XR Interaction Manager and Input Action Manager. The simulator prefab adds no duplicates.
            var setup = (GameObject)PrefabUtility.InstantiatePrefab(setupPrefab, scene);
            PrefabUtility.InstantiatePrefab(simulatorPrefab, scene);

            Transform origin = FindChild(setup.transform, "XR Origin (XR Rig)");
            if (origin != null)
            {
                origin.position = StartPosition;
                origin.rotation = Quaternion.LookRotation(Flat(StartLookAt - StartPosition));
            }
            Camera cam = setup.GetComponentInChildren<Camera>(true);

            BuildEvacuationSystem(zones, route, exitIndex, cam != null ? cam.transform : null);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Report(zones, route, origin != null);
        }

        // ---------- Environment ----------

        static void BuildBuilding(Transform p)
        {
            Box("Floor", p, new Vector3(16f, -0.05f, 1f), new Vector3(32.2f, 0.1f, 14.2f), mFloor);

            // Exterior walls; Exit A (west) and Exit B (east) openings at z 0.6..2.4.
            WallX(p, 8f, 0f, 32f);
            WallX(p, -6f, 0f, 32f);
            WallZ(p, 0f, -6f, 8f, 0.6f, 2.4f);
            WallZ(p, 32f, -6f, 8f, 0.6f, 2.4f);

            // Corridor walls with doorways: Classroom 1, Classroom 2, Lab (north); elevator alcove, stairwell (south).
            WallX(p, 3f, 0f, 32f, 9f, 10.6f, 19f, 20.6f, 26f, 27.6f);
            WallX(p, 0f, 0f, 32f, 4f, 7f, 15f, 16.6f);

            WallZ(p, 12f, 3f, 8f);
            WallZ(p, 22f, 3f, 8f);

            // Elevator alcove x 4..7, z -2..0.
            WallZ(p, 4f, -2f, 0f);
            WallZ(p, 7f, -2f, 0f);
            WallX(p, -2f, 4f, 7f);

            // Stairwell x 13..20, z -6..0.
            WallZ(p, 13f, -6f, 0f);
            WallZ(p, 20f, -6f, 0f);

            // Exit A: fire door shut against the smoke (impassable).
            Box("Exit A Door (closed)", p, new Vector3(0.05f, DoorHeight / 2f, 1.5f), new Vector3(0.08f, DoorHeight, 1.8f), mDoorBlocked);

            BuildElevator(Group("Elevator", p));
            BuildStairs(Group("Stairwell Stairs", p));
            BuildFurniture(Group("Classroom 1 Furniture", p));
        }

        static void BuildElevator(Transform p)
        {
            Box("Door Frame", p, new Vector3(5.5f, 1.2f, -1.88f), new Vector3(1.7f, 2.4f, 0.04f), mDark);
            Box("Door Left", p, new Vector3(5.15f, 1.1f, -1.85f), new Vector3(0.69f, 2.2f, 0.05f), mMetal);
            Box("Door Right", p, new Vector3(5.85f, 1.1f, -1.85f), new Vector3(0.69f, 2.2f, 0.05f), mMetal);
            Box("Call Panel", p, new Vector3(6.6f, 1.2f, -1.87f), new Vector3(0.15f, 0.3f, 0.04f), mDark);

            // Barrier across the alcove.
            Box("Barrier Post L", p, new Vector3(4.3f, 0.5f, -0.4f), new Vector3(0.08f, 1f, 0.08f), mDark);
            Box("Barrier Post R", p, new Vector3(6.7f, 0.5f, -0.4f), new Vector3(0.08f, 1f, 0.08f), mDark);
            Box("Barrier Bar", p, new Vector3(5.5f, 0.95f, -0.4f), new Vector3(2.4f, 0.08f, 0.06f), mDoorBlocked);
        }

        static void BuildStairs(Transform p)
        {
            const int steps = 8;
            const float rise = 0.2f, run = 0.35f, x = 18f, width = 2f, zStart = -1.5f;
            for (int i = 0; i < steps; i++)
            {
                float h = (i + 1) * rise;
                Box($"Step {i + 1}", p, new Vector3(x, h / 2f, zStart - run * (i + 0.5f)), new Vector3(width, h, run), mConcrete);
            }
            float zEnd = zStart - run * steps;
            float landingDepth = zEnd - (-5.9f);
            Box("Landing", p, new Vector3(x, steps * rise / 2f, zEnd - landingDepth / 2f), new Vector3(width, steps * rise, landingDepth), mConcrete);
        }

        static void BuildFurniture(Transform p)
        {
            Vector2[] desks = { new Vector2(3f, 4.6f), new Vector2(3f, 6.2f), new Vector2(5f, 4.6f), new Vector2(5f, 6.2f) };
            for (int i = 0; i < desks.Length; i++)
                Box($"Desk {i + 1}", p, new Vector3(desks[i].x, 0.375f, desks[i].y), new Vector3(1.2f, 0.75f, 0.6f), mDesk);
            Box("Whiteboard", p, new Vector3(6f, 1.5f, 7.88f), new Vector3(3f, 1.1f, 0.04f), mWall);
        }

        static void BuildOutside(Transform p)
        {
            Box("Ground", p, new Vector3(20f, -0.08f, 1f), new Vector3(80f, 0.1f, 50f), mGround);
            Box("Path to Assembly", p, new Vector3(37f, -0.02f, 1.5f), new Vector3(10f, 0.04f, 2.4f), mPath);
            Box("Assembly Pad", p, new Vector3(45f, -0.01f, 1.5f), new Vector3(6f, 0.04f, 6f), mAssembly);
            Box("Assembly Sign Pole", p, new Vector3(45f, 1.25f, 4.6f), new Vector3(0.1f, 2.5f, 0.1f), mDark);
        }

        static void BuildSignage(Transform p)
        {
            // Exit A
            Sign(p, "Sign - Exit A Blocked", "EXIT A - BLOCKED", new Vector3(0.13f, 2.6f, 1.5f), Vector3.right, new Vector2(1.8f, 0.35f), SignRed, Color.white, 160);
            Sign(p, "Sign - Smoke Danger", "DANGER: SMOKE\nEXIT A UNAVAILABLE", new Vector3(3.8f, 2.5f, 1.5f), Vector3.right, new Vector2(2.4f, 0.6f), SignRed, Color.white, 180);

            // Direction sign opposite Classroom 1 door (reader faces south: east is on the left).
            Sign(p, "Sign - Directions", "<< EXIT B (SAFE)          EXIT A (BLOCKED) >>", new Vector3(9.8f, 2.0f, 0.13f), Vector3.forward, new Vector2(3.4f, 0.32f), SignDark, Color.white, 140);
            Sign(p, "Sign - Classroom Exit Route", "EXIT ROUTE: TURN LEFT TO EXIT B", new Vector3(9.8f, 2.55f, 3.13f), Vector3.forward, new Vector2(2.6f, 0.3f), SignGreen, Color.white, 130);

            // Exit B
            Sign(p, "Sign - Safe Exit Ahead", "SAFE EXIT B AHEAD", new Vector3(21.5f, 2.5f, 1.5f), Vector3.left, new Vector2(2.4f, 0.4f), SignGreen, Color.white, 180);
            Sign(p, "Sign - Exit B", "EXIT B - SAFE EXIT", new Vector3(31.87f, 2.6f, 1.5f), Vector3.left, new Vector2(1.8f, 0.35f), SignGreen, Color.white, 160);

            // Elevator and stairwell
            Sign(p, "Sign - Elevator", "ELEVATOR OUT OF SERVICE\nDO NOT USE IN A FIRE", new Vector3(5.5f, 1.6f, -1.8f), Vector3.forward, new Vector2(1.5f, 0.45f), SignRed, Color.white, 120);
            Sign(p, "Sign - Stairwell", "STAIRWELL - UPPER FLOORS\nNOT AN EXIT FROM THIS LEVEL", new Vector3(15.8f, 2.6f, 0.13f), Vector3.forward, new Vector2(2.0f, 0.36f), SignDark, Color.white, 120);

            // Room labels (corridor side).
            Sign(p, "Sign - Classroom 1", "CLASSROOM 1", new Vector3(9.8f, 2.6f, 2.87f), Vector3.back, new Vector2(1.5f, 0.3f), SignDark, Color.white, 140);
            Sign(p, "Sign - Classroom 2", "CLASSROOM 2", new Vector3(19.8f, 2.6f, 2.87f), Vector3.back, new Vector2(1.5f, 0.3f), SignDark, Color.white, 140);
            Sign(p, "Sign - Lab", "LAB", new Vector3(26.8f, 2.6f, 2.87f), Vector3.back, new Vector2(1.5f, 0.3f), SignDark, Color.white, 140);

            // Assembly point (reader approaches from the west).
            Sign(p, "Sign - Assembly Point", "ASSEMBLY POINT", new Vector3(45f, 2.3f, 4.6f), Vector3.left, new Vector2(2.0f, 0.5f), SignGreen, Color.white, 220);
        }

        static void BuildHazards(Transform p)
        {
            // Smoke plume at Exit A (URP particle material copy).
            var smoke = new GameObject("Exit A Smoke");
            smoke.transform.SetParent(p, false);
            smoke.transform.position = new Vector3(1.6f, 0.2f, 1.5f);
            var ps = smoke.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.22f, 0.22f, 0.22f, 0.7f);
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.03f;

            var emission = ps.emission;
            emission.rateOverTime = 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(-90f, 0f, 0f); // emit upwards
            shape.scale = new Vector3(3f, 2.6f, 0.3f);

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

            var psRenderer = smoke.GetComponent<ParticleSystemRenderer>();
            psRenderer.sharedMaterial = mSmokeParticles;
            psRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Haze volume so the blockage reads clearly even without particles.
            Box("Smoke Haze", p, new Vector3(1.8f, 1.5f, 1.5f), new Vector3(3.4f, 2.9f, 2.8f), mSmokeHaze, false)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Fire glow at the door.
            Box("Burning Debris 1", p, new Vector3(0.5f, 0.2f, 0.9f), new Vector3(0.6f, 0.4f, 0.5f), mFire);
            Box("Burning Debris 2", p, new Vector3(0.6f, 0.15f, 2.1f), new Vector3(0.5f, 0.3f, 0.6f), mFire);
            Box("Burning Debris 3", p, new Vector3(1.2f, 0.12f, 1.5f), new Vector3(0.4f, 0.24f, 0.4f), mFire);

            var glow = new GameObject("Fire Glow");
            glow.transform.SetParent(p, false);
            glow.transform.position = new Vector3(0.9f, 0.8f, 1.5f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.45f, 0.1f);
            light.intensity = 3f;
            light.range = 7f;
        }

        // ---------- Evacuation data ----------

        static EvacuationZone[] BuildZones(Transform p)
        {
            return new[]
            {
                Zone(p, "Zone - Exit A (blocked)", ZoneType.BlockedExit, "EXIT A BLOCKED - SMOKE",
                    "Exit A is the nearest exit, but it is blocked by dense smoke and fire.\n" +
                    "Smoke is toxic and hides the way out. Do not go through it.\n" +
                    "Turn back and follow the green route to Exit B.",
                    new Vector3(1.9f, 1.5f, 1.5f), new Vector3(3.8f, 3f, 3f)),

                Zone(p, "Zone - Elevator (unavailable)", ZoneType.UnavailableRoute, "ELEVATOR UNAVAILABLE",
                    "Never use an elevator during a fire.\n" +
                    "The power may fail, the shaft can fill with smoke, and the doors may open on the fire floor.\n" +
                    "Use the green route to Exit B.",
                    new Vector3(5.5f, 1.5f, -0.6f), new Vector3(3f, 3f, 2.8f)),

                Zone(p, "Zone - Exit B (safe)", ZoneType.SafeExit, "EXIT B - SAFE EXIT",
                    "Exit B is clear of smoke and fire.\nKeep moving away from the building to the assembly point.",
                    new Vector3(32.5f, 1.5f, 1.5f), new Vector3(2.4f, 3f, 3f)),

                Zone(p, "Zone - Assembly Point", ZoneType.AssemblyPoint, "ASSEMBLY POINT",
                    "You have reached the assembly point.",
                    new Vector3(45f, 1.5f, 1.5f), new Vector3(6f, 3f, 6f)),
            };
        }

        static Transform[] BuildRoute(Transform p, out int exitIndex)
        {
            // Classroom 1 door -> corridor -> Exit B -> assembly point. Never passes Exit A or the elevator.
            Vector3[] points =
            {
                new Vector3(9.8f, 0f, 4.2f),
                new Vector3(9.8f, 0f, 1.5f),
                new Vector3(30.8f, 0f, 1.5f),
                new Vector3(33.8f, 0f, 1.5f),
                new Vector3(45f, 0f, 1.5f),
            };
            exitIndex = 3;

            var waypoints = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                waypoints[i] = new GameObject($"Waypoint {i}").transform;
                waypoints[i].SetParent(p, false);
                waypoints[i].position = points[i];
            }

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                var strip = Box($"Floor Line {i}", p, (a + b) / 2f + Vector3.up * 0.012f, new Vector3(0.2f, 0.01f, Vector3.Distance(a, b)), mRoute, false);
                strip.transform.rotation = Quaternion.LookRotation(b - a);
                strip.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return waypoints;
        }

        static void BuildEvacuationSystem(EvacuationZone[] zones, Transform[] route, int exitIndex, Transform head)
        {
            var root = new GameObject("Evacuation System");
            var manager = root.AddComponent<EvacuationManager>();

            // Instruction panel
            Canvas panelCanvas = CreateCanvas(root.transform, "Instruction Panel", StartPosition + Vector3.up * 1.8f, Quaternion.identity, new Vector2(0.75f, 0.4f), 3f);
            var panel = panelCanvas.gameObject.AddComponent<InstructionPanel>();
            panel.background = AddImage(panelCanvas.transform, "Background", panel.infoColor);
            panel.title = AddText(panelCanvas.transform, "Title", "", 44, Color.white, TextAnchor.UpperLeft, new Vector2(0f, 0.72f), Vector2.one, new Vector2(24f, 10f));
            panel.body = AddText(panelCanvas.transform, "Body", "", 28, Color.white, TextAnchor.UpperLeft, Vector2.zero, new Vector2(1f, 0.72f), new Vector2(24f, 14f));
            panel.body.fontStyle = FontStyle.Normal;
            panel.head = head;

            // Route guide arrow (chevron of three thin boxes pointing along +Z)
            var guideObject = new GameObject("Route Guide");
            guideObject.transform.SetParent(root.transform, false);
            var guide = guideObject.AddComponent<RouteGuide>();

            Transform arrow = Group("Guide Arrow", guideObject.transform);
            arrow.position = StartPosition + Vector3.up;
            ArrowPart("Shaft", arrow, new Vector3(0f, 0f, -0.02f), new Vector3(0.035f, 0.035f, 0.36f), 0f);
            ArrowPart("Head L", arrow, new Vector3(-0.058f, 0f, 0.131f), new Vector3(0.035f, 0.035f, 0.18f), 40f);
            ArrowPart("Head R", arrow, new Vector3(0.058f, 0f, 0.131f), new Vector3(0.035f, 0.035f, 0.18f), -40f);

            Canvas labelCanvas = CreateCanvas(guideObject.transform, "Guide Label", StartPosition + Vector3.up * 1.2f, Quaternion.identity, new Vector2(0.5f, 0.09f), 3f);
            AddImage(labelCanvas.transform, "Background", new Color(0f, 0f, 0f, 0.55f));
            guide.label = AddText(labelCanvas.transform, "Text", "", 56, new Color(0.6f, 1f, 0.6f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero);

            guide.arrow = arrow;
            guide.labelRoot = labelCanvas.transform;
            guide.waypoints = route;
            guide.exitWaypointIndex = exitIndex;
            guide.head = head;

            manager.zones = zones;
            manager.panel = panel;
            manager.routeGuide = guide;
            manager.head = head;
        }

        static void ArrowPart(string name, Transform arrow, Vector3 localPos, Vector3 size, float yaw)
        {
            var part = Box(name, arrow, localPos, size, mRoute, false, false);
            part.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            part.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Report(EvacuationZone[] zones, Transform[] route, bool originPlaced)
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
                $"Route check: {(routeOk ? "OK - avoids Exit A and the elevator" : "FAILED - " + problem)}";
            Debug.Log("[SafeRoute] Scene generated.\n" + msg);
            EditorUtility.DisplayDialog("SafeRoute", msg, "OK");
        }

        static int CountByTypeName(string typeName) =>
            Object.FindObjectsOfType<MonoBehaviour>(true).Count(b => b != null && b.GetType().Name == typeName);

        // ---------- Builders ----------

        static void CreateLighting()
        {
            var sun = new GameObject("Directional Light");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.8f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.6f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.3f);
        }

        static EvacuationZone Zone(Transform p, string objectName, ZoneType type, string displayName, string explanation, Vector3 center, Vector3 size)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(p, false);
            go.transform.position = center;
            var zone = go.AddComponent<EvacuationZone>();
            zone.zoneType = type;
            zone.displayName = displayName;
            zone.explanation = explanation;
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = size;
            return zone;
        }

        static void WallX(Transform p, float z, float x0, float x1, params float[] doors)
        {
            float cursor = x0;
            for (int i = 0; i + 1 < doors.Length; i += 2)
            {
                float d0 = doors[i], d1 = doors[i + 1];
                if (d0 > cursor) Box("Wall", p, new Vector3((cursor + d0) / 2f, WallHeight / 2f, z), new Vector3(d0 - cursor, WallHeight, WallThickness), mWall);
                Box("Lintel", p, new Vector3((d0 + d1) / 2f, (DoorHeight + WallHeight) / 2f, z), new Vector3(d1 - d0, WallHeight - DoorHeight, WallThickness), mWall);
                cursor = d1;
            }
            if (x1 > cursor) Box("Wall", p, new Vector3((cursor + x1) / 2f, WallHeight / 2f, z), new Vector3(x1 - cursor, WallHeight, WallThickness), mWall);
        }

        static void WallZ(Transform p, float x, float z0, float z1, params float[] doors)
        {
            float cursor = z0;
            for (int i = 0; i + 1 < doors.Length; i += 2)
            {
                float d0 = doors[i], d1 = doors[i + 1];
                if (d0 > cursor) Box("Wall", p, new Vector3(x, WallHeight / 2f, (cursor + d0) / 2f), new Vector3(WallThickness, WallHeight, d0 - cursor), mWall);
                Box("Lintel", p, new Vector3(x, (DoorHeight + WallHeight) / 2f, (d0 + d1) / 2f), new Vector3(WallThickness, WallHeight - DoorHeight, d1 - d0), mWall);
                cursor = d1;
            }
            if (z1 > cursor) Box("Wall", p, new Vector3(x, WallHeight / 2f, (cursor + z1) / 2f), new Vector3(WallThickness, WallHeight, z1 - cursor), mWall);
        }

        static GameObject Box(string name, Transform parent, Vector3 localCenter, Vector3 size, Material material, bool collider = true, bool isStatic = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = isStatic;
            return go;
        }

        static void Sign(Transform p, string name, string text, Vector3 position, Vector3 facing, Vector2 sizeMeters, Color background, Color foreground, int fontSize)
        {
            // Canvas front is seen from its -Z side, so point +Z away from the reader.
            Canvas canvas = CreateCanvas(p, name, position, Quaternion.LookRotation(-facing), sizeMeters, 1f);
            AddImage(canvas.transform, "Background", background);
            AddText(canvas.transform, "Text", text, fontSize, foreground, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(20f, 10f));
        }

        static Canvas CreateCanvas(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 sizeMeters, float dynamicPixelsPerUnit)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = dynamicPixelsPerUnit;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMeters * PixelsPerMeter;
            rt.localScale = Vector3.one / PixelsPerMeter;
            rt.SetPositionAndRotation(position, rotation);
            return canvas;
        }

        static Image AddImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text AddText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 padding)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, anchorMin, anchorMax, padding);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 padding)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = padding;
            rt.offsetMax = -padding;
        }

        // ---------- Materials ----------

        static void CreateMaterials()
        {
            EnsureFolder(MaterialFolder);
            mFloor = Lit("Floor", new Color(0.62f, 0.62f, 0.6f));
            mWall = Lit("Wall", new Color(0.86f, 0.85f, 0.8f));
            mGround = Lit("Ground", new Color(0.33f, 0.45f, 0.28f));
            mPath = Lit("Path", new Color(0.55f, 0.55f, 0.52f));
            mDoorBlocked = Lit("DoorBlocked", new Color(0.35f, 0.08f, 0.06f));
            mMetal = Lit("ElevatorMetal", new Color(0.55f, 0.57f, 0.6f));
            mConcrete = Lit("Concrete", new Color(0.5f, 0.5f, 0.5f));
            mDesk = Lit("Desk", new Color(0.55f, 0.42f, 0.28f));
            mDark = Lit("DarkGrey", new Color(0.15f, 0.15f, 0.15f));
            mRoute = Lit("SafeRouteGreen", new Color(0.1f, 0.8f, 0.25f), new Color(0.1f, 0.9f, 0.3f) * 1.5f);
            mFire = Lit("FireGlow", new Color(1f, 0.4f, 0.05f), new Color(1f, 0.35f, 0.05f) * 4f);
            mAssembly = Lit("AssemblyPad", new Color(0.1f, 0.55f, 0.2f), new Color(0.05f, 0.3f, 0.1f));
            mSmokeHaze = Lit("SmokeHaze", new Color(0.12f, 0.12f, 0.12f, 0.55f), null, true);
            mSmokeParticles = SmokeParticleMaterial();
        }

        static Material Lit(string name, Color color, Color? emission = null, bool transparent = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }

            m.SetColor("_BaseColor", color);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SmokeParticleMaterial()
        {
            string path = $"{MaterialFolder}/SmokeParticles.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(UrpParticleMaterialPath);
                if (source == null)
                {
                    Debug.LogWarning("[SafeRoute] URP ParticlesUnlit material not found; smoke particles will use the default material.");
                    return null;
                }
                m = new Material(source);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.3f, 0.3f, 0.3f, 0.6f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

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
    }
}
