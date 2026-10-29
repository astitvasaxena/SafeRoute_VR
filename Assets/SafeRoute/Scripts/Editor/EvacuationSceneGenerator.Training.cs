using System.Collections.Generic;
using SafeRoute.Evacuation;
using UnityEngine;
using UnityEngine.UI;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        // ---------- Zones ----------

        static EvacuationZone[] BuildZones(Transform p)
        {
            return new[]
            {
                // The lift zone is the alcove plus a 0.2 m strip of corridor in front of it (z -2..0.3); listed first so
                // that strip shows the lift warning, while the rest of the corridor shows the Exit A fire warning.
                Zone(p, "Zone - Elevator (unavailable)", ZoneType.UnavailableRoute, "LIFT - DO NOT USE",
                    "Never use a lift in a fire.\nIt can stop or fill with smoke.\nFollow the instructor to Exit B.",
                    LineElevator,
                    new Vector3(5.5f, 1.5f, -0.85f), new Vector3(3f, 3f, 2.3f)),
                // Covers the whole fire-affected west end (x 0.15..7.4): every fire patch's danger zone ends at least ~1 m
                // inside it, so the warning always comes first. Still 1.6 m short of the Classroom 1 door and the route.
                Zone(p, "Zone - Exit A (blocked)", ZoneType.BlockedExit, "EXIT A BLOCKED - FIRE",
                    "Fire is spreading from Exit A.\nSmoke is toxic. Never walk into it.\nTurn back and follow the instructor.",
                    LineExitABlocked,
                    new Vector3(3.775f, 1.5f, 1.5f), new Vector3(7.25f, 3f, 2.8f)),
                Zone(p, "Zone - Exit B (safe)", ZoneType.SafeExit, "EXIT B - SAFE EXIT",
                    "Exit B is clear of smoke and fire.\nKeep going to the assembly point.",
                    LineExitBReached,
                    new Vector3(32.5f, 1.5f, 1.5f), new Vector3(2.4f, 3f, 3f)),
                Zone(p, "Zone - Assembly Point", ZoneType.AssemblyPoint, "ASSEMBLY POINT",
                    "You have reached the assembly point.", null,
                    new Vector3(45f, 1.5f, 1.5f), new Vector3(6f, 3f, 6f)),
            };
        }

        static EvacuationZone Zone(Transform p, string objectName, ZoneType type, string displayName, string explanation, string instructorLine, Vector3 center, Vector3 size)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(p, false);
            go.transform.localPosition = center;
            var zone = go.AddComponent<EvacuationZone>();
            zone.zoneType = type;
            zone.displayName = displayName;
            zone.explanation = explanation;
            zone.instructorLine = instructorLine;
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = size;
            return zone;
        }

        // ---------- Restricted areas ----------

        static int restrictedBlockers;

        static EvacuationZone[] BuildRestrictedAreas(Transform p)
        {
            restrictedBlockers = 0;
            var zones = new List<EvacuationZone>();
            const string roomText = "This room is not on your route.\nDo not enter rooms in an evacuation.\nReturn to the corridor and follow the instructor to Exit B.";
            const string roomLine = LineRestricted;
            // North rooms: corridor-side wall face at z = 2.9, corridor towards -Z.
            RestrictedArea(p, zones, "CLASSROOM 2", roomText, roomLine, 19f, 20.6f, 2.9f, -1f,
                new Vector3(17f, 1.5f, 5.475f), new Vector3(9.8f, 3f, 4.75f));
            RestrictedArea(p, zones, "LAB", roomText, roomLine, 26f, 27.6f, 2.9f, -1f,
                new Vector3(26.975f, 1.5f, 5.475f), new Vector3(9.75f, 3f, 4.75f));
            // Stairwell: corridor-side wall face at z = 0.1, corridor towards +Z.
            RestrictedArea(p, zones, "STAIRWELL",
                "The stairs only lead to upper floors.\nThey are not a way out.\nReturn to the corridor route.",
                LineStairs, 15f, 16.6f, 0.1f, 1f,
                new Vector3(16.5f, 2.4f, -2.975f), new Vector3(6.8f, 4.8f, 5.75f));
            return zones.ToArray();
        }

        /// <summary>
        /// Solid, invisible BoxCollider across the doorway on the corridor side (stops CharacterController
        /// locomotion; HeadCollisionGuard stops head movement), a visible belt barrier just behind it, and two
        /// trigger zones used only for warnings: the strip of corridor in front of the door and the area itself.
        /// </summary>
        static void RestrictedArea(Transform root, List<EvacuationZone> zones, string area, string explanation, string line,
            float doorFrom, float doorTo, float wallFace, float corridorDir, Vector3 areaCenter, Vector3 areaSize)
        {
            Transform p = Group($"{area} (restricted)", root);
            string title = $"{area} - NO ENTRY";
            float mid = (doorFrom + doorTo) / 2f;

            zones.Add(Zone(p, $"Zone - {area} doorway (warning trigger)", ZoneType.Restricted, title, explanation, line,
                new Vector3(mid, 1.5f, wallFace + corridorDir * 0.4f), new Vector3(doorTo - doorFrom + 1f, 3f, 0.8f)));
            zones.Add(Zone(p, $"Zone - {area} interior (warning trigger)", ZoneType.Restricted, title, explanation, line, areaCenter, areaSize));

            ColliderBox("Doorway Blocker (solid, invisible)", p, new Vector3(mid, 1.25f, wallFace + corridorDir * 0.18f),
                new Vector3(doorTo - doorFrom + 0.2f, 2.5f, 0.3f));
            restrictedBlockers++;

            float zb = wallFace + corridorDir * 0.12f;
            foreach (float x in new[] { doorFrom + 0.12f, doorTo - 0.12f })
            {
                Prim(PrimitiveType.Cylinder, "Stanchion Base", p, new Vector3(x, 0.015f, zb), new Vector3(0.3f, 0.015f, 0.3f), mDark);
                Rod("Stanchion Post", p, new Vector3(x, 0f, zb), new Vector3(x, 0.95f, zb), 0.025f, mChrome);
                Prim(PrimitiveType.Sphere, "Stanchion Cap", p, new Vector3(x, 0.97f, zb), Vector3.one * 0.065f, mChrome);
            }
            Box("Barrier Belt", p, new Vector3(mid, 0.9f, zb), new Vector3(doorTo - doorFrom - 0.24f, 0.05f, 0.01f), mBarrierRed, false);
            Vector3 facing = new Vector3(0f, 0f, corridorDir);
            Sign(p, "Sign - No Entry", "NO ENTRY", new Vector3(mid, 1.03f, zb + corridorDir * 0.008f), facing, new Vector2(0.7f, 0.2f),
                SignRed, Color.white, 120, housingDepth: 0.01f, viewDistance: 1.5f);
        }

        // ---------- Safe route ----------

        static Transform[] BuildRoute(Transform p, out int exitIndex)
        {
            // Classroom 1 door -> corridor -> Exit B -> assembly point. Never passes Exit A, the lift or restricted areas.
            // Intermediate points on straight runs are cue points for the instructor.
            Vector3[] points =
            {
                new Vector3(9.8f, 0f, 4.2f),   // inside Classroom 1, at the door
                new Vector3(9.8f, 0f, 1.5f),   // corridor: turn left (east)
                new Vector3(16.0f, 0f, 1.5f),  // stairwell opening (restricted)
                new Vector3(20.0f, 0f, 1.5f),  // Classroom 2 door (restricted)
                new Vector3(27.0f, 0f, 1.5f),  // Lab door (restricted)
                new Vector3(30.8f, 0f, 1.5f),  // approaching Exit B
                new Vector3(33.8f, 0f, 1.5f),  // outside Exit B
                new Vector3(38.5f, 0f, 1.5f),  // path
                new Vector3(45f, 0f, 1.5f),    // assembly point
            };
            exitIndex = 6;

            var waypoints = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                waypoints[i] = new GameObject($"Waypoint {i}").transform;
                waypoints[i].SetParent(p, false);
                waypoints[i].localPosition = points[i];
            }

            // Painted floor chevrons every 2 m (no continuous neon strip).
            Transform markers = Group("Route Markers", p);
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                Vector3 dir = (b - a).normalized;
                float length = Vector3.Distance(a, b);
                for (float d = 1f; d < length - 0.4f; d += 2f)
                {
                    Vector3 c = a + dir * d;
                    Chevron(markers, c + Vector3.up * SurfaceY(c, c), dir);
                }
            }

            // Low-level photoluminescent wayfinding plaques on the corridor walls (pointing east to Exit B).
            foreach (float x in new[] { 12.5f, 16.8f, 23.5f, 29.4f })
                Sign(markers, "Wayfinding Plaque", "EXIT B", new Vector3(x, 0.45f, 2.9f - 0.03f), Vector3.back, new Vector2(0.5f, 0.16f),
                    SignGreen, Color.white, 80, Arrow.Right, 0.012f, 1.5f);
            foreach (float x in new[] { 11.6f, 18.4f, 27.6f, 30.4f })
                Sign(markers, "Wayfinding Plaque", "EXIT B", new Vector3(x, 0.45f, 0.1f + 0.03f), Vector3.forward, new Vector2(0.5f, 0.16f),
                    SignGreen, Color.white, 80, Arrow.Left, 0.012f, 1.5f);

            FloorMarking(markers, "Floor Marking - Exit B", "EXIT B", new Vector3(12.4f, 0.004f, 0.75f), Vector3.right, new Vector2(1.3f, 0.45f), new Color(0.52f, 0.6f, 0.54f), 220);
            return waypoints;
        }

        /// <summary>Top of the walking surface: building floor (0) or outside paving (-0.01).</summary>
        static float SurfaceY(Vector3 a, Vector3 b) => (a.x + b.x) / 2f > 32.2f ? -0.01f : 0f;

        static void Chevron(Transform p, Vector3 position, Vector3 direction)
        {
            Transform c = Group("Chevron", p);
            c.localPosition = position;
            c.localRotation = Quaternion.LookRotation(direction);
            c.gameObject.isStatic = true;
            // Flush painted mark: 2 mm thick, bottom face on the floor (culled from above, so no z-fighting).
            NoShadows(Box("Chevron Bar", c, new Vector3(-0.14f, 0.001f, -0.11f), new Vector3(0.09f, 0.002f, 0.36f), mRoute, false, true, Quaternion.Euler(0f, 51f, 0f)));
            NoShadows(Box("Chevron Bar", c, new Vector3(0.14f, 0.001f, -0.11f), new Vector3(0.09f, 0.002f, 0.36f), mRoute, false, true, Quaternion.Euler(0f, -51f, 0f)));
        }

        // ---------- Instructor ----------

        /// <summary>
        /// Procedural placeholder instructor, deliberately stylised (a restrained sim-game NPC, not a photoreal human):
        /// jointed legs and arms, a restrained face (brows, soft eye sockets, small matte eyes, mouth line), black work
        /// gloves, a hi-vis vest built as a garment (braces, binding, zip, retro-reflective tape) over a navy shirt and
        /// charcoal work trousers, safety boots, a red warden's hard hat with harness, an "INSTRUCTOR" back label and a
        /// "FOLLOW ME" name tag. A rigged human (Mixamo, Asset Store, Ready Player Me) would be an external dependency
        /// needing approval; InstructorGuide is body-agnostic, so one could replace this body and its placeholder animator.
        /// </summary>
        static InstructorGuide BuildInstructor(Transform p, Transform[] route, Transform head)
        {
            var start = new GameObject("Instructor Start").transform;
            start.SetParent(p, false);
            start.localPosition = InstructorStart;
            var path = new Transform[route.Length + 1];
            path[0] = start;
            route.CopyTo(path, 1);

            var root = new GameObject("Instructor (placeholder body)").transform;
            root.SetParent(p, false);
            root.localPosition = InstructorStart;
            root.localRotation = Quaternion.LookRotation(Flat(StartPosition - InstructorStart));

            Transform body = Group("Body", root);

            // Lofted rings are (y, half-width x, half-depth z, z offset of the centre), listed top to bottom. Torso-like
            // sections use a superellipse (power 2.6) so the back is flatter than a capsule; limbs stay elliptical.
            const float Trunk = 2.6f;
            var pelvis = new[] { new Vector4(0.99f, 0.15f, 0.104f, 0f), new Vector4(0.9f, 0.165f, 0.112f, 0f), new Vector4(0.8f, 0.163f, 0.11f, 0f), new Vector4(0.72f, 0.125f, 0.092f, 0f) };
            Part("Hips", body, Loft("Hips", pelvis, 20, Trunk), mTrousers);
            Part("Belt", body, Loft("Belt", Shell(pelvis, 0.97f, 0.925f, 0.007f), 20, Trunk), mBoots);
            NoShadows(OnSurface("Belt Buckle", body, pelvis, Trunk, 0.007f, 0f, 0.965f, 0.93f, true, 0.06f, 0.008f, 0.004f, mReflective));
            // Belt pouches at the back hips: they break up the silhouette in the trainee's usual rear view.
            foreach (float x in new[] { -0.1f, 0.1f })
                NoShadows(ChamferBox("Belt Pouch", body, new Vector3(x, 0.925f, SurfaceZ(pelvis, 0.925f, x, 0.007f, Trunk, false) - 0.0145f),
                    new Vector3(0.06f, 0.08f, 0.035f), mBoots, 0.008f, false, false, Quaternion.Euler(0f, SurfaceYaw(pelvis, 0.925f, x, 0.007f, Trunk, false), 0f)));

            // Legs: hip pivot -> loose work-trouser thigh with a cargo pocket, knee pivot -> shin ending in a hem over a lofted
            // safety boot (rubber sole, toe cap, ankle shaft). Floor is knee y -0.47 (bottom of the sole). The thighs stay
            // 6 mm apart at the crotch and only swing about X, so they never intersect.
            Transform legL = Limb("Leg L", body, new Vector3(-0.1f, 0.92f, 0f)), legR = Limb("Leg R", body, new Vector3(0.1f, 0.92f, 0f));
            Transform kneeL = null, kneeR = null;
            Mesh thigh = Loft("Thigh", new[] { new Vector4(0.02f, 0.066f, 0.072f, 0f), new Vector4(-0.04f, 0.097f, 0.101f, 0.005f), new Vector4(-0.2f, 0.083f, 0.088f, 0.003f), new Vector4(-0.42f, 0.064f, 0.068f, 0f), new Vector4(-0.47f, 0.061f, 0.064f, 0.005f) });
            Mesh shin = Loft("Shin", new[] { new Vector4(0.03f, 0.063f, 0.067f, 0.005f), new Vector4(-0.1f, 0.064f, 0.071f, -0.008f), new Vector4(-0.28f, 0.052f, 0.055f, 0f), new Vector4(-0.33f, 0.055f, 0.058f, 0.002f), new Vector4(-0.355f, 0.06f, 0.064f, 0.004f) });
            // Boot shaft sits inside the trouser hem; the foot is lofted along Z (toe first) and laid flat on the sole.
            Mesh bootShaft = Loft("Boot Shaft", new[] { new Vector4(-0.31f, 0.046f, 0.05f, -0.004f), new Vector4(-0.37f, 0.047f, 0.052f, -0.004f), new Vector4(-0.42f, 0.049f, 0.056f, 0f) }, 14);
            // Foot rings are (z, half-width, half-height, -centre height); power 3 squares off the toe cap.
            var foot = new[]
            {
                new Vector4(0.178f, 0f, 0f, -0.012f), new Vector4(0.165f, 0.03f, 0.02f, -0.017f), new Vector4(0.13f, 0.047f, 0.03f, -0.026f),
                new Vector4(0.08f, 0.052f, 0.038f, -0.032f), new Vector4(0.03f, 0.049f, 0.05f, -0.043f), new Vector4(-0.02f, 0.046f, 0.058f, -0.05f),
                new Vector4(-0.06f, 0.044f, 0.05f, -0.043f), new Vector4(-0.08f, 0.035f, 0.04f, -0.035f), new Vector4(-0.088f, 0f, 0f, -0.03f),
            };
            Mesh bootFoot = Loft("Boot", foot, 16, 3f);
            foreach (Transform leg in new[] { legL, legR })
            {
                float outward = leg == legL ? -1f : 1f;
                Part("Thigh", leg, thigh, mTrousers);
                // Cargo pocket tilted with the thigh taper (5 degrees), with its flap 2 mm proud.
                GameObject pocket = NoShadows(ChamferBox("Cargo Pocket", leg, new Vector3(outward * 0.071f, -0.22f, 0f), new Vector3(0.03f, 0.15f, 0.09f),
                    mTrousers, 0.006f, false, false, Quaternion.Euler(0f, 0f, -outward * 5f)));
                NoShadows(ChamferBox("Pocket Flap", pocket.transform, new Vector3(outward * 0.002f, 0.062f, 0f), new Vector3(0.032f, 0.032f, 0.096f), mTrousers, 0.004f, false, false));
                Transform knee = Limb("Knee", leg, new Vector3(0f, -0.45f, 0f));
                Part("Shin", knee, shin, mTrousers);
                Part("Boot Shaft", knee, bootShaft, mBoots);
                // Loft local y -> knee +Z, local z -> knee -Y: the upper's flat bottom sits on the sole top (y -0.448).
                PartAt("Boot", knee, bootFoot, mBoots, new Vector3(0f, -0.446f, 0f), Quaternion.Euler(90f, 0f, 0f));
                ChamferBox("Boot Sole", knee, new Vector3(0f, -0.459f, 0.045f), new Vector3(0.112f, 0.022f, 0.275f), mSole, 0.006f, false, false);
                foreach (float z in new[] { 0.09f, 0.075f, 0.06f })   // instep, in front of the shaft
                {
                    Vector4 r = RingAt(foot, z);
                    float top = -0.446f + 0.97f * r.z - r.w + 0.0015f;   // instep top, slightly lower at the lace ends
                    NoShadows(Rod("Boot Lace", knee, new Vector3(-0.02f, top, z), new Vector3(0.02f, top, z), 0.0022f, mHair, false));
                }
                if (leg == legL) kneeL = knee; else kneeR = knee;
            }

            // Torso pivot (turns when looking back) carries the shirt, vest, head and arms.
            Transform torso = Limb("Torso", body, new Vector3(0f, 0.95f, 0f));
            var chest = new[]
            {
                new Vector4(0.59f, 0.058f, 0.055f, -0.01f), new Vector4(0.575f, 0.09f, 0.07f, -0.008f), new Vector4(0.54f, 0.16f, 0.09f, -0.005f),
                new Vector4(0.5f, 0.17f, 0.105f, 0f), new Vector4(0.42f, 0.162f, 0.113f, 0.01f), new Vector4(0.3f, 0.152f, 0.106f, 0.006f),
                new Vector4(0.15f, 0.14f, 0.098f, 0f), new Vector4(0.03f, 0.14f, 0.1f, 0f), new Vector4(-0.06f, 0.125f, 0.095f, 0f),
            };
            Part("Shirt", torso, Loft("Shirt", chest, 20, Trunk), mShirt);
            Part("Collar", torso, Loft("Collar", new[] { new Vector4(0.6f, 0.064f, 0.06f, -0.006f), new Vector4(0.55f, 0.08f, 0.075f, -0.004f) }, 14), mShirt);
            Part("Neck", torso, Loft("Neck", new[] { new Vector4(0.66f, 0.05f, 0.048f, 0.005f), new Vector4(0.54f, 0.054f, 0.052f, 0f) }, 10), mSkin);
            // Shoulder caps live on the torso (not the arms, so they never swing away) and fill the notch between the
            // narrow chest top and the upper arm; a flat top carries the epaulette.
            Mesh shoulderCap = Loft("Shoulder", new[] { new Vector4(0.57f, 0.035f, 0.04f, 0f), new Vector4(0.56f, 0.05f, 0.054f, 0f), new Vector4(0.52f, 0.062f, 0.058f, 0f), new Vector4(0.47f, 0.05f, 0.05f, 0f) }, 14);
            foreach (float side in new[] { -1f, 1f })
            {
                PartAt("Shoulder", torso, shoulderCap, mShirt, new Vector3(side * 0.19f, 0f, 0f), Quaternion.identity);
                NoShadows(ChamferBox("Epaulette", torso, new Vector3(side * 0.19f, 0.5715f, 0f), new Vector3(0.065f, 0.005f, 0.04f), mShirt, 0.0015f, false, false));
                NoShadows(ChamferBox("Epaulette Button", torso, new Vector3(side * 0.165f, 0.5745f, 0f), new Vector3(0.009f, 0.003f, 0.009f), mReflective, 0.001f, false, false));
                // Shirt collar point folding down over the vest neck.
                Vector3 a = new Vector3(side * 0.028f, 0.592f, 0.058f), b = new Vector3(side * 0.05f, 0.562f, 0.085f), d = b - a;
                NoShadows(ChamferBox("Collar Point", torso, (a + b) / 2f, new Vector3(0.035f, 0.004f, d.magnitude), mShirt, 0.0015f, false, false,
                    Quaternion.LookRotation(d, Vector3.Cross(d, Vector3.right))));
            }
            // Hi-vis vest (EN ISO 20471 Class 2 layout): an open shell 12 mm over the shirt with two reflective hoops, braces
            // (full length at the front, above the upper hoop at the back so the back label stays clear) joined over the
            // shoulders, darker edge binding and a front zip line.
            const float VestGrow = 0.012f, TapeGrow = 0.016f, BraceGrow = VestGrow + 0.0005f;
            Part("Hi-Vis Vest", torso, Loft("Hi-Vis Vest", Shell(chest, 0.575f, 0.08f, VestGrow), 20, Trunk, false, false), mVest);
            Part("Vest Hem", torso, Loft("Vest Hem", Shell(chest, 0.095f, 0.08f, VestGrow + 0.003f), 20, Trunk, false, false), mVestBinding);
            Part("Vest Neck Binding", torso, Loft("Vest Neck Binding", Shell(chest, 0.585f, 0.565f, VestGrow + 0.003f), 20, Trunk, false, false), mVestBinding);
            Part("Reflective Band", torso, Loft("Reflective Band", Shell(chest, 0.44f, 0.4f, TapeGrow), 20, Trunk), mReflective);
            Part("Reflective Band", torso, Loft("Reflective Band", Shell(chest, 0.2f, 0.16f, TapeGrow), 20, Trunk), mReflective);
            foreach (float x in new[] { -0.085f, 0.085f })
            {
                // Braces sit 1.5 mm under the hoops' surface, so the hoops cover them where they cross (no z-fighting).
                SurfaceStrip("Reflective Brace", torso, chest, Trunk, BraceGrow, x, 0.575f, 0.2f, true, 0.05f, 0.002f, 0.0008f, mReflective);
                SurfaceStrip("Reflective Brace", torso, chest, Trunk, BraceGrow, x, 0.575f, 0.44f, false, 0.05f, 0.002f, 0.0008f, mReflective);
                float zFront = SurfaceZ(chest, 0.575f, x, BraceGrow, Trunk, true), zBack = SurfaceZ(chest, 0.575f, x, BraceGrow, Trunk, false);
                NoShadows(ChamferBox("Reflective Brace", torso, new Vector3(x, 0.58f, (zFront + zBack) / 2f), new Vector3(0.05f, 0.003f, zFront - zBack + 0.004f),
                    mReflective, 0.0008f, false, false, Quaternion.Euler(0f, 0f, -Mathf.Sign(x) * 20f)));   // over the shoulder, outer edge lower
            }
            SurfaceStrip("Vest Zip", torso, chest, Trunk, VestGrow, 0f, 0.565f, 0.09f, true, 0.012f, 0.002f, 0.0005f, mBoots);
            OnSurface("Radio", torso, chest, Trunk, TapeGrow, -0.1f, 0.54f, 0.45f, true, 0.055f, 0.03f, 0.008f, mBoots);
            float radioZ = SurfaceZ(chest, 0.54f, -0.115f, TapeGrow, Trunk, true) + 0.022f;
            NoShadows(Rod("Radio Antenna", torso, new Vector3(-0.115f, 0.535f, radioZ), new Vector3(-0.115f, 0.62f, radioZ), 0.005f, mBoots, false));

            // Head: lofted skull (faces +Z) with a squarer adult jaw, a brow ridge over softly recessed eye sockets, small
            // matte-dark eyes (no white sclera, which is where a stylised face turns uncanny), brows, a mouth line, a lofted
            // nose, ear hints, a shaved-jaw shade and short hair below the hard hat. All children of the head pivot.
            Transform headPivot = Limb("Head", torso, new Vector3(0f, 0.6f, 0f));
            var skull = new[]
            {
                new Vector4(0.26f, 0f, 0f, -0.01f), new Vector4(0.25f, 0.035f, 0.05f, -0.01f), new Vector4(0.225f, 0.065f, 0.085f, -0.008f),
                new Vector4(0.19f, 0.078f, 0.097f, -0.005f), new Vector4(0.165f, 0.0795f, 0.0978f, -0.002f),
                new Vector4(0.152f, 0.0802f, 0.1f, 0.003f),      // brow ridge: front pushed 5 mm, back unchanged
                new Vector4(0.135f, 0.079f, 0.093f, -0.002f),    // eye line: front recessed 7 mm under the brow
                new Vector4(0.11f, 0.077f, 0.095f, 0.003f), new Vector4(0.07f, 0.072f, 0.088f, 0.01f), new Vector4(0.035f, 0.066f, 0.07f, 0.024f),
                new Vector4(0.01f, 0.048f, 0.05f, 0.032f), new Vector4(-0.002f, 0f, 0f, 0.03f),
            };
            Part("Head", headPivot, Loft("Head", skull, 24), mSkin);
            // Shaved jaw: a 1.2 mm shell from the nose base down, a few percent darker and cooler than the skin.
            const float JawGrow = 0.0012f;
            Part("Stubble", headPivot, Loft("Stubble", Shell(skull, 0.085f, 0.004f, JawGrow), 24), mStubble);
            // Hair: shifted back so it only shows at the sides and nape; its front stays inside the face.
            Part("Hair", headPivot, Loft("Hair", new[] { new Vector4(0.18f, 0.084f, 0.099f, -0.016f), new Vector4(0.13f, 0.083f, 0.1f, -0.015f), new Vector4(0.085f, 0.074f, 0.088f, -0.006f) }, 16), mHair);
            // Nose: narrow bridge between the eyes to a rounded tip ~18 mm proud, closing at the nostril base.
            NoShadows(Part("Nose", headPivot, Loft("Nose", new[]
            {
                new Vector4(0.143f, 0.0055f, 0.003f, 0.093f), new Vector4(0.115f, 0.0075f, 0.008f, 0.097f), new Vector4(0.097f, 0.011f, 0.011f, 0.105f),
                new Vector4(0.088f, 0.013f, 0.01f, 0.1f), new Vector4(0.083f, 0f, 0f, 0.095f),
            }, 10), mSkin));
            Mesh eye = Loft("Eye", new[] { new Vector4(0.006f, 0f, 0f, 0f), new Vector4(0.004f, 0.008f, 0.0045f, 0f), new Vector4(0f, 0.011f, 0.006f, 0f), new Vector4(-0.004f, 0.008f, 0.0045f, 0f), new Vector4(-0.006f, 0f, 0f, 0f) }, 12);
            foreach (float side in new[] { -1f, 1f })
            {
                float ex = side * 0.031f, bx = side * 0.033f, mx = side * 0.0105f;
                NoShadows(PartAt("Eye", headPivot, eye, mEyes, new Vector3(ex, 0.135f, SurfaceZ(skull, 0.135f, ex, 0f, 2f, true) - 0.002f),
                    Quaternion.Euler(0f, SurfaceYaw(skull, 0.135f, ex, 0f, 2f, true), 0f)));
                // Brows on the ridge, outer ends a touch lower for a calm expression.
                NoShadows(ChamferBox("Eyebrow", headPivot, new Vector3(bx, 0.149f, SurfaceZ(skull, 0.149f, bx, 0f, 2f, true) + 0.001f), new Vector3(0.034f, 0.007f, 0.007f),
                    mHair, 0.0025f, false, false, Quaternion.Euler(0f, SurfaceYaw(skull, 0.149f, bx, 0f, 2f, true), -side * 4f)));
                // Mouth line in two halves so it follows the curve of the face.
                NoShadows(ChamferBox("Mouth", headPivot, new Vector3(mx, 0.062f, SurfaceZ(skull, 0.062f, mx, JawGrow, 2f, true)), new Vector3(0.023f, 0.004f, 0.004f),
                    mLips, 0.0015f, false, false, Quaternion.Euler(0f, SurfaceYaw(skull, 0.062f, mx, JawGrow, 2f, true), 0f)));
                NoShadows(ChamferBox("Ear", headPivot, new Vector3(side * 0.081f, 0.125f, -0.008f), new Vector3(0.014f, 0.052f, 0.03f), mSkin, 0.006f, false, false));
            }
            // Hard hat: dome lathe flaring to a brim with a longer front peak (brim centre pushed +Z), closed underneath;
            // semi-gloss HDPE with a centre crest and two side ribs.
            var hat = new[]
            {
                new Vector4(0.305f, 0f, 0f, 0f), new Vector4(0.3f, 0.05f, 0.058f, 0.002f), new Vector4(0.285f, 0.085f, 0.098f, 0.004f),
                new Vector4(0.26f, 0.105f, 0.123f, 0.006f), new Vector4(0.225f, 0.114f, 0.134f, 0.006f), new Vector4(0.19f, 0.116f, 0.137f, 0.006f),
                new Vector4(0.177f, 0.121f, 0.145f, 0.012f), new Vector4(0.172f, 0.13f, 0.161f, 0.026f), new Vector4(0.166f, 0.131f, 0.162f, 0.026f),
                new Vector4(0.163f, 0.124f, 0.153f, 0.024f), new Vector4(0.168f, 0.1f, 0.12f, 0.006f),
            };
            Part("Hard Hat", headPivot, Loft("Hard Hat", hat, 20), mHelmet);
            SurfaceRib("Hat Crest", headPivot, hat, 0f, 0.225f, 0.02f, 0.008f, mHelmet);
            SurfaceRib("Hat Rib", headPivot, hat, -0.045f, 0.225f, 0.007f, 0.005f, mHelmet);
            SurfaceRib("Hat Rib", headPivot, hat, 0.045f, 0.225f, 0.007f, 0.005f, mHelmet);
            // Dark headband of the suspension harness showing under the brim, so the hat sits on the head instead of perching.
            NoShadows(Part("Hat Harness", headPivot, Loft("Hat Harness", new[] { new Vector4(0.17f, 0.087f, 0.105f, 0f), new Vector4(0.155f, 0.086f, 0.104f, 0.002f) }, 20, 2f, false, false), mBoots));

            // Arms: shoulder pivot -> tapered upper arm with a rounded deltoid, elbow pivot -> forearm + gloved hand.
            Transform armL = Limb("Arm L", torso, new Vector3(-0.22f, 0.5f, 0f)), armR = Limb("Arm R", torso, new Vector3(0.22f, 0.5f, 0f));
            Transform elbowL = null, elbowR = null;
            Mesh upperArm = Loft("Upper Arm", new[] { new Vector4(0.04f, 0f, 0f, 0f), new Vector4(0.03f, 0.04f, 0.042f, 0f), new Vector4(0f, 0.056f, 0.056f, 0f), new Vector4(-0.1f, 0.05f, 0.053f, 0f), new Vector4(-0.3f, 0.042f, 0.044f, 0f), new Vector4(-0.33f, 0.04f, 0.042f, 0f) });
            var forearmRings = new[] { new Vector4(0.02f, 0.042f, 0.043f, 0f), new Vector4(-0.08f, 0.044f, 0.046f, 0f), new Vector4(-0.235f, 0.031f, 0.029f, 0f) };
            Mesh forearm = Loft("Forearm", forearmRings);
            Mesh shirtCuff = Loft("Shirt Cuff", Shell(forearmRings, -0.18f, -0.207f, 0.003f), 12, 2f, false, true);
            // Black work glove: a flattened mitten (palm facing the thigh) from a cuff over the wrist to rounded fingertips.
            // Honest about the missing finger articulation, and authentic for a fire warden.
            var gloveRings = new[]
            {
                new Vector4(-0.205f, 0.035f, 0.034f, 0f), new Vector4(-0.24f, 0.033f, 0.036f, 0.002f), new Vector4(-0.27f, 0.021f, 0.046f, 0.005f),
                new Vector4(-0.32f, 0.019f, 0.045f, 0.008f), new Vector4(-0.36f, 0.016f, 0.04f, 0.011f), new Vector4(-0.39f, 0.012f, 0.03f, 0.012f),
                new Vector4(-0.405f, 0f, 0f, 0.01f),
            };
            Mesh glove = Loft("Glove", gloveRings, 12, 2.4f);
            Mesh gloveCuff = Loft("Glove Cuff", Shell(gloveRings, -0.208f, -0.232f, 0.003f), 12, 2.4f, false, true);
            Mesh thumb = Loft("Glove Thumb", new[] { new Vector4(0f, 0.011f, 0.012f, 0f), new Vector4(-0.03f, 0.0095f, 0.0105f, 0f), new Vector4(-0.045f, 0.007f, 0.008f, 0f), new Vector4(-0.052f, 0f, 0f, 0f) }, 10);
            foreach (Transform arm in new[] { armL, armR })
            {
                float inward = arm == armL ? 1f : -1f;   // towards the body (+X for the left arm)
                Part("Upper Arm", arm, upperArm, mShirt);
                Transform elbow = Limb("Elbow", arm, new Vector3(0f, -0.3f, 0f));
                Part("Forearm", elbow, forearm, mShirt);
                NoShadows(Part("Shirt Cuff", elbow, shirtCuff, mShirt));
                Part("Glove", elbow, glove, mGloves);
                NoShadows(Part("Glove Cuff", elbow, gloveCuff, mBoots));
                // Thumb on the front edge of the palm, pointing down and forward.
                NoShadows(PartAt("Glove Thumb", elbow, thumb, mGloves, new Vector3(inward * 0.008f, -0.26f, 0.036f), Quaternion.Euler(-30f, 0f, inward * 8f)));
                if (arm == armL) elbowL = elbow; else elbowR = elbow;
            }

            // Back label: the trainee mostly sees the instructor from behind. Sits 6 mm off the vest back, between the bands.
            float backZ = SurfaceZ(chest, 0.33f, 0f, VestGrow, Trunk, false) - 0.006f;
            Canvas label = CreateCanvas(torso, "Back Label", new Vector3(0f, 0.33f, backZ), Quaternion.identity, new Vector2(0.34f, 0.09f), TextPixelsPerUnit(3f));
            WarnIfOverflows(AddText(label.transform, "Text", "INSTRUCTOR", 48, new Color(0.06f, 0.08f, 0.14f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero), "Instructor Back Label");

            // Name tag (visible whenever the instructor is not speaking) and speech bubble; both billboarded.
            // Sized for the 3-4 m lead distance: 88 px tag (63 mm caps, ~20 px at 3 m), 96 px line (69 mm caps, ~21 px at 3 m).
            Canvas tag = CreateCanvas(root, "Name Tag", new Vector3(0f, 2.0f, 0f), Quaternion.identity, new Vector2(1.5f, 0.18f), TextPixelsPerUnit(3f));
            AddImage(tag.transform, "Background", new Color(0.06f, 0.1f, 0.2f, 1f));
            WarnIfOverflows(AddText(tag.transform, "Text", "INSTRUCTOR - FOLLOW ME", 88, new Color(0.95f, 0.95f, 0.95f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(12f, 4f)), "Instructor Name Tag");

            // Bottom edge at 1.87 m, just above the hard hat (~1.84 m).
            Canvas bubble = CreateCanvas(root, "Speech Bubble", new Vector3(0f, 2.15f, 0f), Quaternion.identity, new Vector2(1.7f, 0.5f), TextPixelsPerUnit(3f));
            AddImage(bubble.transform, "Border", new Color(0.06f, 0.1f, 0.2f, 1f));
            AddImage(bubble.transform, "Background", new Color(0.98f, 0.98f, 0.96f, 1f), Vector2.zero, Vector2.one, new Vector2(6f, 6f));
            AddImage(bubble.transform, "Header", SignNavy, new Vector2(0f, 0.8f), Vector2.one, Vector2.zero);
            AddText(bubble.transform, "Header Text", "INSTRUCTOR", 60, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0.8f), Vector2.one, new Vector2(24f, 2f));

            var guide = root.gameObject.AddComponent<InstructorGuide>();
            guide.head = head;
            guide.path = path;
            guide.cues = InstructorCues();
            guide.speechRoot = bubble.transform;
            guide.nameTag = tag.transform;
            guide.speechText = FixedText(AddText(bubble.transform, "Line", "", 96, new Color(0.04f, 0.06f, 0.12f), TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(1f, 0.8f), new Vector2(24f, 8f)));
            foreach (string line in SpokenLines) WarnIfOverflows(guide.speechText, "Speech Bubble", line, allowWrap: true);
            guide.catchUpLine = LineCatchUp;
            guide.voice = BuildInstructorVoice(root);

            var animator = root.gameObject.AddComponent<InstructorPlaceholderAnimator>();
            animator.guide = guide;
            animator.body = body;
            animator.torso = torso;
            animator.headPivot = headPivot;
            animator.leftLeg = legL;
            animator.rightLeg = legR;
            animator.leftKnee = kneeL;
            animator.rightKnee = kneeR;
            animator.leftArm = armL;
            animator.rightArm = armR;
            animator.leftElbow = elbowL;
            animator.rightElbow = elbowR;
            return guide;
        }

        /// <summary>Cue point indices: 0 = instructor start, then route waypoint i at index i + 1. Every cue with a title is repeated in the main panel.</summary>
        static InstructorGuide.Cue[] InstructorCues() => new[]
        {
            // Start: wait for the opening line to finish before walking off.
            new InstructorGuide.Cue { pointIndex = 0, hold = 4.5f, line = LineEmergency },
            new InstructorGuide.Cue
            {
                pointIndex = 1, hold = 0.8f, line = LineFollowToExitB,
                title = "LEAVE THE CLASSROOM", detail = "Go through the door.\nThen turn left to Exit B.",
            },
            new InstructorGuide.Cue
            {
                pointIndex = 2, hold = 4f, line = LineExitABlockedTurnLeft,
                title = "TURN LEFT TO EXIT B", detail = "Exit A is blocked by fire.\nUse Exit B, the safe exit.",
                pointDirection = Vector3.right,
            },
            new InstructorGuide.Cue
            {
                pointIndex = 3, waitForTrainee = false, hold = 0f, line = LineStairs,
                title = "STAIRS ARE NOT AN EXIT", detail = "The stairs only go up.\nStay in the corridor.",
            },
            new InstructorGuide.Cue
            {
                pointIndex = 4, waitForTrainee = false, hold = 0f, line = LineRestricted,
                title = "STAY IN THE CORRIDOR", detail = "Do not enter any room.\nFollow the instructor.",
            },
            new InstructorGuide.Cue
            {
                pointIndex = 6, hold = 1f, line = LineExitBClear, pointDirection = Vector3.right,
                title = "EXIT B - GO OUTSIDE", detail = "Exit B is clear.\nGo through the doors.",
            },
            new InstructorGuide.Cue
            {
                // On the outside path, not at the doors: the Exit B zone already says LineExitBReached there.
                pointIndex = 8, waitForTrainee = false, hold = 0f, line = LineKeepMoving,
                title = "KEEP MOVING", detail = "Move away from the building.\nGo to the assembly point.",
            },
            // Assembly point: the EvacuationManager says LineAssembly when the trainee arrives (no duplicate cue).
        };

        static Transform Limb(string name, Transform parent, Vector3 localPosition)
        {
            Transform t = Group(name, parent);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>Non-static, collider-free renderer for a pivot-local body mesh (the pivots animate).</summary>
        static GameObject Part(string name, Transform parent, Mesh mesh, Material material) =>
            MeshBox(name, parent, Vector3.zero, Vector3.zero, material, mesh, false, false, null);

        /// <summary>
        /// Tube through rings (y, half-width x, half-depth z, centre z offset), listed top to bottom so the profile runs down
        /// the outside (a ring may step inward, e.g. under a hat brim). Superellipse cross-section of <paramref name="power"/>
        /// (2 = ellipse), smooth normals, metre UVs, optional flat caps. A ring of zero radius closes to a rounded pole.
        /// </summary>
        static Mesh Loft(string name, Vector4[] rings, int segments = 12, float power = 2f, bool capTop = true, bool capBottom = true)
        {
            int n = rings.Length, row = segments + 1;
            float e = 2f / power;
            var pts = new Vector3[n, row];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < row; j++)
                {
                    float t = (j % segments) * 2f * Mathf.PI / segments, c = Mathf.Cos(t), s = Mathf.Sin(t);
                    pts[i, j] = new Vector3(rings[i].y * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e), rings[i].x,
                        rings[i].w + rings[i].z * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e));
                }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Tri(int a, int b, int c)
            {
                // Wind each triangle to face its vertex normals (robust to rings that step inward).
                bool front = Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), normals[a] + normals[b] + normals[c]) >= 0f;
                tris.AddRange(front ? new[] { a, b, c } : new[] { a, c, b });
            }

            float v = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) v += Vector3.Distance(pts[i, 0], pts[i - 1, 0]);
                float perimeter = 2f * Mathf.PI * Mathf.Sqrt((rings[i].y * rings[i].y + rings[i].z * rings[i].z) / 2f);
                for (int j = 0; j < row; j++)
                {
                    // Normal from the ring tangent and the profile tangent; poles (zero radius) point straight up or down.
                    Vector3 around = pts[i, (j + 1) % segments] - pts[i, (j + segments - 1) % segments];
                    Vector3 along = pts[Mathf.Min(i + 1, n - 1), j] - pts[Mathf.Max(i - 1, 0), j];
                    Vector3 nrm = Vector3.Cross(around, along);
                    if (nrm.sqrMagnitude < 1e-12f) nrm = i < n / 2 ? Vector3.up : Vector3.down;
                    verts.Add(pts[i, j]);
                    normals.Add(nrm.normalized);
                    uvs.Add(new Vector2(perimeter * j / segments, -v));
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = i * row + j, b = a + 1, c = a + row, d = c + 1;
                    Tri(a, b, c);
                    Tri(b, d, c);
                }

            void Cap(int i, Vector3 nrm)
            {
                if (rings[i].y < 1e-4f || rings[i].z < 1e-4f) return;
                int centre = verts.Count;
                verts.Add(new Vector3(0f, rings[i].x, rings[i].w));
                normals.Add(nrm);
                uvs.Add(new Vector2(0f, rings[i].w));
                for (int j = 0; j < row; j++)
                {
                    verts.Add(pts[i, j]);
                    normals.Add(nrm);
                    uvs.Add(new Vector2(pts[i, j].x, pts[i, j].z));
                }
                for (int j = 0; j < segments; j++) Tri(centre, centre + 1 + j, centre + 2 + j);
            }
            if (capTop) Cap(0, Vector3.up);
            if (capBottom) Cap(n - 1, Vector3.down);

            return FinishMesh($"Loft {name}", verts, normals, uvs, tris);
        }

        /// <summary>Ring interpolated at height <paramref name="y"/> (rings monotonic in y, top first; clamped at the ends).</summary>
        static Vector4 RingAt(Vector4[] rings, float y)
        {
            if (y >= rings[0].x) return rings[0];
            for (int i = 1; i < rings.Length; i++)
                if (y >= rings[i].x)
                    return Vector4.Lerp(rings[i], rings[i - 1], Mathf.InverseLerp(rings[i].x, rings[i - 1].x, y));
            return rings[rings.Length - 1];
        }

        /// <summary>Rings of a layer worn over <paramref name="rings"/> between two heights, grown by <paramref name="grow"/> metres.</summary>
        static Vector4[] Shell(Vector4[] rings, float yTop, float yBottom, float grow)
        {
            var result = new List<Vector4> { RingAt(rings, yTop) };
            foreach (Vector4 r in rings)
                if (r.x < yTop && r.x > yBottom) result.Add(r);
            result.Add(RingAt(rings, yBottom));
            for (int i = 0; i < result.Count; i++) result[i] += new Vector4(0f, grow, grow, 0f);
            return result.ToArray();
        }

        /// <summary>Front (or back) surface z of a lofted section at height <paramref name="y"/> and offset <paramref name="x"/>.</summary>
        static float SurfaceZ(Vector4[] rings, float y, float x, float grow, float power, bool front)
        {
            Vector4 r = RingAt(rings, y);
            float a = r.y + grow, k = (r.z + grow) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Min(1f, Mathf.Abs(x) / a), power)), 1f / power);
            return front ? r.w + k : r.w - k;
        }

        /// <summary>Thin chamfered piece lying on a lofted surface between two heights (tape, strap, buckle, radio).</summary>
        static GameObject OnSurface(string name, Transform parent, Vector4[] rings, float power, float grow, float x, float yTop, float yBottom,
            bool front, float width, float depth, float chamfer, Material material)
        {
            var a = new Vector3(x, yBottom, SurfaceZ(rings, yBottom, x, grow, power, front));
            var b = new Vector3(x, yTop, SurfaceZ(rings, yTop, x, grow, power, front));
            Vector3 up = (b - a).normalized, outward = Vector3.Cross(Vector3.right, up) * (front ? 1f : -1f);
            return ChamferBox(name, parent, (a + b) / 2f + outward * (depth / 2f), new Vector3(width, (b - a).magnitude, depth), material, chamfer,
                false, false, Quaternion.LookRotation(outward, up));
        }

        /// <summary><see cref="OnSurface"/> split at every ring height in between, so a long strip follows the curve.</summary>
        static void SurfaceStrip(string name, Transform parent, Vector4[] rings, float power, float grow, float x, float yTop, float yBottom,
            bool front, float width, float depth, float chamfer, Material material)
        {
            float top = yTop;
            foreach (Vector4 r in rings)
                if (r.x < top && r.x > yBottom)
                {
                    NoShadows(OnSurface(name, parent, rings, power, grow, x, top, r.x, front, width, depth, chamfer, material));
                    top = r.x;
                }
            NoShadows(OnSurface(name, parent, rings, power, grow, x, top, yBottom, front, width, depth, chamfer, material));
        }

        /// <summary>Yaw (degrees) that turns a part's +Z to the front (or back) surface normal of a lofted section at (x, y).</summary>
        static float SurfaceYaw(Vector4[] rings, float y, float x, float grow, float power, bool front)
        {
            const float d = 0.002f;
            float slope = (SurfaceZ(rings, y, x + d, grow, power, front) - SurfaceZ(rings, y, x - d, grow, power, front)) / (2f * d);
            return -Mathf.Atan(slope) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Raised rib running front to back over the top of an elliptical (power 2) loft at a fixed x, from
        /// <paramref name="yMin"/> at the back to <paramref name="yMin"/> at the front. Built from straight pieces between
        /// the rings, which is exactly the loft surface at x = 0 and within a millimetre or two elsewhere.
        /// </summary>
        static void SurfaceRib(string name, Transform parent, Vector4[] rings, float x, float yMin, float width, float height, Material material)
        {
            var front = new List<Vector3>();
            var back = new List<Vector3>();
            foreach (Vector4 r in rings)
            {
                if (r.x < yMin) break;
                if (Mathf.Abs(x) >= r.y && !(r.y < 1e-4f && Mathf.Abs(x) < 1e-4f)) continue;
                float k = r.y < 1e-4f ? 0f : r.z * Mathf.Sqrt(1f - (x / r.y) * (x / r.y));
                front.Add(new Vector3(x, r.x, r.w + k));
                back.Add(new Vector3(x, r.x, r.w - k));
            }
            back.Reverse();
            var pts = new List<Vector3>(back);
            foreach (Vector3 q in front)
                if (pts.Count == 0 || (q - pts[pts.Count - 1]).sqrMagnitude > 1e-8f) pts.Add(q);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1], along = (b - a).normalized, up = Vector3.Cross(along, Vector3.right);
                // 1 mm sunk into the shell; pieces overlap a little so the joints stay closed on the curve.
                NoShadows(ChamferBox(name, parent, (a + b) / 2f + up * (height / 2f - 0.001f), new Vector3(width, height, (b - a).magnitude + height),
                    material, Mathf.Min(width, height) * 0.3f, false, false, Quaternion.LookRotation(along, up)));
            }
        }

        /// <summary><see cref="Part"/> with its own pivot-local position and rotation.</summary>
        static GameObject PartAt(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject go = Part(name, parent, mesh, material);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            return go;
        }

        /// <summary>Important text never shrinks: fixed size, wraps, overflows rather than truncates.</summary>
        static Text FixedText(Text text)
        {
            text.resizeTextForBestFit = false;
            text.lineSpacing = 1.05f;
            return text;
        }

        // ---------- Evacuation system (panel, route distance, manager, head guard) ----------

        /// <summary>Every recorded instructor line (the subtitle strip and speech bubble must fit the longest in 2 lines).</summary>
        static readonly string[] SpokenLines =
        {
            LineEmergency, LineFollowToExitB, LineExitABlockedTurnLeft, LineExitABlocked, LineElevator, LineStairs, LineRestricted,
            LineExitBClear, LineExitBReached, LineKeepMoving, LineAssembly, LineCatchUp, LineReturn,
        };

        /// <summary>Generation-time check that every panel message fits its band (warnings only, nothing shrinks).</summary>
        static void CheckPanelText(InstructionPanel panel, Text footer, EvacuationZone[] zones, InstructorGuide instructor, EvacuationManager manager)
        {
            var titles = new List<string> { manager.briefingTitle, manager.reminderTitle, manager.afterExitTitle, "TRAINING COMPLETE" };
            // Worst-case completion report (EvacuationManager.Complete): every unsafe area and restricted room visited.
            var bodies = new List<string>
            {
                manager.briefing, manager.reminder, manager.afterExit,
                "Assembly point reached: YES\nExit used: EXIT B\nTime taken: 12:34\nUnsafe areas: EXIT A BLOCKED, LIFT\nRestricted entries: 12 (CLASSROOM 2, LAB, STAIRWELL)",
            };
            foreach (EvacuationZone z in zones)
            {
                titles.Add(z.displayName);
                bodies.Add(z.explanation);
            }
            if (instructor != null && instructor.cues != null)
                foreach (InstructorGuide.Cue cue in instructor.cues)
                    if (!string.IsNullOrEmpty(cue.title))
                    {
                        titles.Add(cue.title);
                        bodies.Add(cue.detail);
                    }
            // The panel sizes itself to its content at runtime, so the checks run against the largest allowed blocks:
            // titles on one line beside the tone label, bodies within MaxBodyLines (authored lines may wrap at the
            // narrower width), instructor lines within 2 lines, the footer on one line.
            SetHeight(panel.body.rectTransform, LinesHeight(panel.body, MaxBodyLines));
            SetHeight(panel.subtitle.rectTransform, LinesHeight(panel.subtitle, 2));
            foreach (string t in titles) WarnIfOverflows(panel.title, "Instruction Panel", t);
            foreach (string b in bodies) WarnIfOverflows(panel.body, "Instruction Panel", b, allowWrap: true);
            foreach (string line in SpokenLines) WarnIfOverflows(panel.subtitle, "Instruction Panel", line, allowWrap: true);
            foreach (string label in new[] { panel.infoLabel, panel.dangerLabel, panel.safeLabel }) WarnIfOverflows(panel.toneLabel, "Instruction Panel", label);
            WarnIfOverflows(footer, "Instruction Panel", "Assembly point: 100 m");
            WarnIfOverflows(footer, "Instruction Panel", "Restarting in 10 s");
        }

        /// <summary>Most body lines the panel is laid out for (the worst-case completion report wraps to 6).</summary>
        const int MaxBodyLines = 6;

        /// <summary>Height of <paramref name="lines"/> lines in <paramref name="text"/>'s font, size and line spacing.</summary>
        static float LinesHeight(Text text, int lines)
        {
            string original = text.text;
            var sample = new System.Text.StringBuilder("Ag");
            for (int i = 1; i < lines; i++) sample.Append("\nAg");
            text.text = sample.ToString();
            float height = Mathf.Ceil(text.preferredHeight);
            text.text = original;
            return height;
        }

        static void SetHeight(RectTransform rt, float height) => rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);

        /// <summary>Top-anchored row stretched across its parent minus <paramref name="inset"/> each side (InstructionPanel stacks these).</summary>
        static RectTransform TopRow(RectTransform rt, float inset, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-2f * inset, height);
            rt.anchoredPosition = new Vector2(0f, -top);
            return rt;
        }

        static void BuildEvacuationSystem(Transform parent, EvacuationZone[] zones, Transform[] route, int exitIndex, Transform head, Transform rig, InstructorGuide instructor)
        {
            var root = new GameObject("Evacuation System");
            root.transform.SetParent(parent, false);
            var manager = root.AddComponent<EvacuationManager>();

            // Instruction panel: 1.30 m wide at 1.5 m ahead, top edge 7 degrees below eye level (under the instructor's
            // head). It grows downwards with its content (InstructionPanel.RefreshLayout), so a two-line message spans
            // about 7-19 degrees down and 47 across, instead of a fixed 1.52 x 0.635 m block (54 x 23 degrees). At that
            // distance 1 canvas px (1 mm) of cap height is ~0.6 screen px at 1080p: title 54 px bold, body 52 px regular
            // (as large as before), subtitle 46, footer 40, tone label and caption 32 bold.
            // Stack from the top, canvas px: header 84 | body (20 padding) | subtitle strip (caption + up to 2 lines) | footer 58.
            // 1320 px: the title rect must still fit the failure title "FIRE CONTACT — EVACUATION FAILED" (~1053 px at 54 px bold).
            const float panelWidth = 1320f, pad = 32f, toneWidth = 190f;   // tone label fits "GUIDANCE" at 32 px bold
            var bodyColor = new Color(0.9f, 0.92f, 0.94f);   // cool off-white: less halation than pure white on OLED headsets
            // Raster em ~1.5x the on-screen em (body 52 px raster vs ~34 px on screen); the scale compensation keeps this when pulled in.
            Canvas panelCanvas = CreateCanvas(root.transform, "Instruction Panel", StartPosition + Vector3.up * 1.0f, Quaternion.identity,
                new Vector2(panelWidth, 400f) / PixelsPerMeter, 1f);
            ((RectTransform)panelCanvas.transform).pivot = new Vector2(0.5f, 1f);   // placed by its top edge
            var panel = panelCanvas.gameObject.AddComponent<InstructionPanel>();
            panel.distance = 1.5f;
            panel.heightOffset = -Mathf.Tan(7f * Mathf.Deg2Rad) * 1.5f;
            panel.followAngle = 18f;
            panel.followSpeed = 4f;
            panel.infoColor = new Color(0.16f, 0.22f, 0.33f, 1f);
            panel.dangerColor = new Color(0.66f, 0.07f, 0.05f, 1f);
            panel.safeColor = new Color(0.0f, 0.42f, 0.19f, 1f);
            panel.panelColor = new Color(0.075f, 0.08f, 0.09f, 1f);
            panel.headerHeight = 84f;
            panel.bodyPadding = 20f;
            panel.subtitleTop = 50f;
            panel.subtitleBottom = 16f;
            panel.footerHeight = 58f;
            // Hairline edge instead of a white frame; the neutral charcoal body leaves the severity to the header bar.
            AddImage(panelCanvas.transform, "Border", new Color(1f, 1f, 1f, 0.12f));
            panel.background = AddImage(panelCanvas.transform, "Background", panel.panelColor, Vector2.zero, Vector2.one, new Vector2(2f, 2f));
            panel.header = AddImage(panelCanvas.transform, "Header", panel.dangerColor);
            TopRow(panel.header.rectTransform, 0f, 0f, panel.headerHeight);
            panel.title = FixedText(AddText(panel.header.transform, "Title", "", 54, Color.white, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero));
            panel.title.rectTransform.offsetMin = new Vector2(pad, 8f);
            panel.title.rectTransform.offsetMax = new Vector2(-(pad + toneWidth), -8f);
            panel.toneLabel = AddText(panel.header.transform, "Tone Label", "", 32, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleRight,
                new Vector2(1f, 0f), Vector2.one, Vector2.zero);
            panel.toneLabel.rectTransform.offsetMin = new Vector2(-(pad + toneWidth), 8f);
            panel.toneLabel.rectTransform.offsetMax = new Vector2(-pad, -8f);
            panel.body = FixedText(AddText(panelCanvas.transform, "Body", "", 52, bodyColor, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Normal));
            panel.body.lineSpacing = 1.1f;
            TopRow(panel.body.rectTransform, pad, panel.headerHeight + panel.bodyPadding, 120f);

            // Instructor subtitle strip: darker band, hi-vis caption above the line, hidden until the instructor speaks.
            Image strip = AddImage(panelCanvas.transform, "Instructor Subtitle", new Color(0.035f, 0.04f, 0.045f, 1f));
            TopRow(strip.rectTransform, 2f, 300f, 170f);
            Text caption = AddText(strip.transform, "Label", "INSTRUCTOR", 32, SignYellow, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero);
            TopRow(caption.rectTransform, pad - 2f, 10f, 40f);   // ends at subtitleTop (50)
            WarnIfOverflows(caption, "Instruction Panel");
            panel.subtitle = FixedText(AddText(strip.transform, "Line", "", 46, bodyColor, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Normal));
            TopRow(panel.subtitle.rectTransform, pad - 2f, panel.subtitleTop, 110f);
            panel.subtitleRoot = strip.gameObject;

            // Footer: route distance (RouteGuide) or the restart countdown, under a faint divider; collapses while empty.
            var footerObject = new GameObject("Footer", typeof(RectTransform));
            footerObject.transform.SetParent(panelCanvas.transform, false);
            panel.footer = TopRow((RectTransform)footerObject.transform, 2f, 400f, panel.footerHeight);
            Image divider = AddImage(panel.footer, "Divider", new Color(1f, 1f, 1f, 0.15f));
            TopRow(divider.rectTransform, pad - 2f, 0f, 1f);
            Text distance = FixedText(AddText(panel.footer, "Route Distance", "", 40, new Color(0.7f, 0.74f, 0.78f), TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, new Vector2(pad - 2f, 4f), FontStyle.Normal));
            panel.status = distance;
            panel.head = head;
            foreach (Graphic g in panelCanvas.GetComponentsInChildren<Graphic>(true)) g.material = mUIOverlay;
            panelCanvas.sortingOrder = 10;
            CheckPanelText(panel, distance, zones, instructor, manager);
            strip.gameObject.SetActive(false);

            // Route progress (distance to Exit B / assembly point) shown in the panel footer; no floating arrow.
            var guideObject = new GameObject("Route Guide");
            guideObject.transform.SetParent(root.transform, false);
            var guide = guideObject.AddComponent<RouteGuide>();
            guide.label = distance;
            guide.waypoints = route;
            guide.exitWaypointIndex = exitIndex;
            guide.head = head;

            var headGuard = root.AddComponent<HeadCollisionGuard>();
            headGuard.head = head;
            headGuard.rig = rig;

            manager.zones = zones;
            manager.panel = panel;
            manager.routeGuide = guide;
            manager.head = head;
            manager.instructor = instructor;
            manager.completeLine = LineAssembly;
            manager.returnLine = LineReturn;
            if (instructor != null) instructor.panel = panel;

            BuildAlarm(parent, manager, instructor != null ? instructor.voice : null);

            // Fire contact: failure sequence and an in-scene restart. The scene is not reloaded, so the XR Origin and
            // XR Device Simulator stay intact; the rig is moved back to the start and every system resets itself.
            var restart = root.AddComponent<TrainingRestart>();
            restart.manager = manager;
            restart.rig = rig;
            restart.head = head;
            restart.headGuard = headGuard;
            restart.screenTint = BuildScreenTint(root.transform, head);
            BuildImpactAudio(restart);
            // Every fire patch's danger zone feeds the same restart sequence; the manager and the restart's own guard
            // make sure overlapping zones can only fail the attempt once. Each cylinder is clipped to the Exit A warning
            // zone (inside the corridor), so it can't reach through walls or the closed doors, and the trainee is always
            // warned before contact.
            EvacuationZone exitAZone = System.Array.Find(zones, z => z.zoneType == ZoneType.BlockedExit);
            var body = rig != null ? rig.GetComponent<CharacterController>() : null;
            foreach (FireDangerZone danger in fireDangers)
            {
                danger.restart = restart;
                danger.head = head;
                danger.body = body;
                danger.limitTo = exitAZone;
            }
            if (fireSpread != null) fireSpread.manager = manager; // the spread starts again with every new attempt

            foreach (string t in new[] { manager.failTitle, restart.restartTitle, "TRAINING COMPLETE - ATTEMPT 12" })
                WarnIfOverflows(panel.title, "Instruction Panel", t);
            foreach (string b in new[] { manager.failMessage + "\nAttempt 12: FAILED", restart.restartMessage })
                WarnIfOverflows(panel.body, "Instruction Panel", b, allowWrap: true);
            panel.RefreshLayout();   // scene-time state: header only (no message yet); the briefing re-lays it out on Start
        }

        /// <summary>
        /// Full-view tint for the burn wash and the restart fade, re-parented to the XR camera at runtime. Drawn over the
        /// world (overlay material) but under the instruction panel (sorting order 10), so the failure message stays readable.
        /// </summary>
        static ScreenTint BuildScreenTint(Transform parent, Transform head)
        {
            Canvas canvas = CreateCanvas(parent, "Screen Tint (burn flash / restart fade)", Vector3.zero, Quaternion.identity, new Vector2(1.4f, 1.4f), 1f);
            canvas.sortingOrder = 9;
            var tint = canvas.gameObject.AddComponent<ScreenTint>();
            tint.head = head;
            tint.image = AddImage(canvas.transform, "Tint", Color.clear);
            tint.image.material = mUIOverlay;
            canvas.enabled = false;
            return tint;
        }
    }
}
