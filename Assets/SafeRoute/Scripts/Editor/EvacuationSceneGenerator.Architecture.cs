using System.Linq;
using UnityEngine;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        static readonly float[] NorthWindowCenters = { 2.4f, 4.8f, 7.2f, 9.6f, 14.2f, 17f, 19.8f, 24.4f, 27f, 29.6f };
        const float WindowWidth = 1.6f;
        static readonly float[] ClassroomDoors = { 9f, 19f, 26f };   // west edge of each 1.6 m doorway in the corridor wall

        static void BuildArchitecture(Transform p)
        {
            BuildFloors(Group("Floors", p));
            BuildWalls(Group("Walls", p));
            BuildCeilingsAndRoof(Group("Ceilings and Roof", p));
            BuildDoors(Group("Doors", p));
            BuildWindows(Group("Windows", p));
            BuildStairs(Group("Stairwell", p));
            BuildElevator(Group("Elevator", p));
        }

        static void BuildFloors(Transform p)
        {
            FloorArea(p, "Corridor Floor", -0.15f, 32.15f, 0f, 3f, mFloor);
            FloorArea(p, "Classroom 1 Floor", 0f, 12f, 3f, 8f, mCarpet);
            FloorArea(p, "Classroom 2 Floor", 12f, 22f, 3f, 8f, mCarpet);
            FloorArea(p, "Lab Floor", 22f, 32f, 3f, 8f, mLabFloor);
            FloorArea(p, "Elevator Alcove Floor", 4f, 7f, -2f, 0f, mFloor);
            FloorArea(p, "Stairwell Floor", 13f, 20f, -6f, 0f, mConcrete);
        }

        static void BuildWalls(Transform p)
        {
            // Exterior walls: facade outside, parapet above the roof, openings for exits and windows.
            Opening[] north = NorthWindowCenters.Select(c => Opening.Window(c - WindowWidth / 2f, c + WindowWidth / 2f)).ToArray();
            // North/south walls end inside the west/east wall bodies (no coplanar end faces against the cladding).
            Wall(p, true, 8f, -0.15f, 32.15f, ParapetHeight, ExteriorWall, Finish.Interior, Finish.Exterior, true, north);
            Wall(p, true, -6f, -0.15f, 13f, ParapetHeight, ExteriorWall, Finish.Exterior, Finish.None, true);
            Wall(p, true, -6f, 13f, 20f, StairTowerHeight, ExteriorWall, Finish.Exterior, Finish.Interior, true, Opening.Window(15.6f, 17.4f, 2.4f, 4.2f));
            Wall(p, true, -6f, 20f, 32.15f, ParapetHeight, ExteriorWall, Finish.Exterior, Finish.None, true);

            // West/east end walls in three runs (split inside the corridor walls), so the corridor dado returns across them.
            foreach ((float x, bool west) in new[] { (0f, true), (32f, false) })
            {
                Finish outNeg = west ? Finish.Exterior : Finish.Interior, outPos = west ? Finish.Interior : Finish.Exterior;
                Finish corNeg = west ? Finish.Exterior : Finish.Corridor, corPos = west ? Finish.Corridor : Finish.Exterior;
                Wall(p, false, x, -6.15f, 0.1f, ParapetHeight, ExteriorWall, outNeg, outPos, false);
                Wall(p, false, x, 0.1f, 2.9f, ParapetHeight, ExteriorWall, corNeg, corPos, false, Opening.Door(0.6f, 2.4f));
                Wall(p, false, x, 2.9f, 8.15f, ParapetHeight, ExteriorWall, outNeg, outPos, false, Opening.Window(4.4f, 6.6f));
                Coping(p, false, x, -6.15f, 8.15f, ParapetHeight, ExteriorWall);
            }

            // Corner trims close the cladding/plinth/coping junctions at the four building corners.
            foreach (float cx in new[] { -0.17f, 32.17f })
                foreach (float cz in new[] { -6.17f, 8.17f })
                    NoShadows(ChamferBox("Corner Trim", p, new Vector3(cx, (WallBase + ParapetHeight + 0.065f) / 2f, cz),
                        new Vector3(0.075f, ParapetHeight + 0.065f - WallBase, 0.075f), mCoping, 0.006f));

            // Corridor walls: Classroom 1, Classroom 2 and Lab doors (north); elevator alcove and stairwell (south).
            Wall(p, true, 3f, 0.15f, 31.85f, WallHeight, InteriorWall, Finish.Corridor, Finish.Interior, false,
                ClassroomDoors.Select(x => Opening.Door(x, x + 1.6f)).ToArray());
            Wall(p, true, 0f, 0.15f, 31.85f, WallHeight, InteriorWall, Finish.Interior, Finish.Corridor, false,
                Opening.Door(4f, 7f, 2.4f), Opening.Door(15f, 16.6f));

            // Classroom partitions.
            Wall(p, false, 12f, 3.1f, 7.85f, WallHeight, InteriorWall, Finish.Interior, Finish.Interior, false);
            Wall(p, false, 22f, 3.1f, 7.85f, WallHeight, InteriorWall, Finish.Interior, Finish.Interior, false);

            // Elevator alcove x 4..7, z -2..0.
            Wall(p, false, 4f, -2.1f, -0.1f, WallHeight, InteriorWall, Finish.None, Finish.Interior, false);
            Wall(p, false, 7f, -2.1f, -0.1f, WallHeight, InteriorWall, Finish.Interior, Finish.None, false);
            Wall(p, true, -2f, 3.9f, 7.1f, WallHeight, InteriorWall, Finish.None, Finish.Interior, false);

            // Stair tower x 13..20, z -6..0: double height inside, facade where it rises above the main roof.
            Solid(p, false, 13f, -5.85f, -0.1f, WallBase, 3.3f, InteriorWall, Finish.None, Finish.Interior);
            Solid(p, false, 13f, -6.15f, 0.1f, 3.3f, StairTowerHeight, InteriorWall, Finish.Exterior, Finish.Interior);
            Solid(p, false, 20f, -5.85f, -0.1f, WallBase, 3.3f, InteriorWall, Finish.Interior, Finish.None);
            Solid(p, false, 20f, -6.15f, 0.1f, 3.3f, StairTowerHeight, InteriorWall, Finish.Interior, Finish.Exterior);
            Solid(p, true, 0f, 12.9f, 20.1f, WallHeight, StairTowerHeight, InteriorWall, Finish.Interior, Finish.Exterior);
            Coping(p, false, 13f, -6.05f, 0.15f, StairTowerHeight, InteriorWall);
            Coping(p, false, 20f, -6.05f, 0.15f, StairTowerHeight, InteriorWall);
            Coping(p, true, 0f, 12.86f, 20.14f, StairTowerHeight, InteriorWall);
            // Tower corners above the main parapet: cover the cladding junction where the side walls meet the south wall.
            foreach (float cx in new[] { 12.935f, 20.065f })
                NoShadows(ChamferBox("Corner Trim", p, new Vector3(cx, (ParapetHeight + StairTowerHeight + 0.065f) / 2f, -6.1675f),
                    new Vector3(0.15f, StairTowerHeight + 0.065f - ParapetHeight, 0.075f), mCoping, 0.006f));
        }

        static void BuildCeilingsAndRoof(Transform p)
        {
            CeilingArea(p, "Corridor Ceiling", 0.15f, 31.85f, 0.1f, 2.9f, CeilingHeight);
            CeilingArea(p, "Classroom 1 Ceiling", 0.15f, 11.9f, 3.1f, 7.85f, CeilingHeight);
            CeilingArea(p, "Classroom 2 Ceiling", 12.1f, 21.9f, 3.1f, 7.85f, CeilingHeight);
            CeilingArea(p, "Lab Ceiling", 22.1f, 31.85f, 3.1f, 7.85f, CeilingHeight);
            CeilingArea(p, "Elevator Alcove Ceiling", 4.1f, 6.9f, -1.9f, -0.1f, CeilingHeight);
            CeilingArea(p, "Stairwell Ceiling", 13.1f, 19.9f, -5.85f, -0.1f, StairCeilingHeight);

            // Smoke detectors and sprinkler heads, placed between the light panels and clear of the hanging signs.
            Transform services = Group("Ceiling Services", p);
            foreach (float x in new[] { 3.25f, 10.75f, 18.25f, 25.75f })
                CeilingDevice(services, new Vector3(x, CeilingHeight, 1.5f), false);
            foreach (float x in new[] { 5.75f, 15.75f, 20.75f, 30.75f })
                CeilingDevice(services, new Vector3(x, CeilingHeight, 1.5f), true);
            foreach (float x in new[] { 6f, 17f, 27f })
                CeilingDevice(services, new Vector3(x, CeilingHeight, 5.5f), false);
            foreach (float x in new[] { 3f, 9f, 14.5f, 19.5f, 24.5f, 29.5f })
                CeilingDevice(services, new Vector3(x, CeilingHeight, 5.5f), true);
            CeilingDevice(services, new Vector3(14.5f, StairCeilingHeight, -3f), false);

            // A few lay-in tiles sitting slightly out of the grid and an access hatch, so the ceiling is not machine-flat.
            // Cells are on the world 0.6 m grid, clear of the light panels, ceiling devices, sign hangers and the fire end.
            foreach ((float x, float z, float drop, float tiltX, float tiltZ) in new[]
                     { (9.9f, 0.9f, 0.005f, 0.5f, -0.3f), (17.7f, 2.1f, 0.007f, -0.4f, 0.6f), (25.5f, 0.9f, 0.004f, 0.6f, 0.2f),
                       (15.3f, 5.7f, 0.006f, -0.5f, -0.4f), (27.9f, 5.7f, 0.008f, 0.3f, 0.7f) })
            {
                // Built unrotated so its UVs stay on the world grid, then tipped a fraction of a degree.
                GameObject tile = NoShadows(Box("Displaced Ceiling Tile", services, new Vector3(x, CeilingHeight - drop - 0.006f, z),
                    new Vector3(0.59f, 0.012f, 0.59f), mCeiling, false, worldUV: true));
                tile.transform.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
            }
            Transform hatch = Group("Access Hatch", services);
            hatch.localPosition = new Vector3(20.7f, CeilingHeight, 2.1f);
            NoShadows(Box("Hatch Panel", hatch, new Vector3(0f, -0.004f, 0f), new Vector3(0.56f, 0.008f, 0.56f), mPaper, false));
            foreach (float s in new[] { -1f, 1f })
            {
                NoShadows(Box("Hatch Frame", hatch, new Vector3(s * 0.29f, -0.006f, 0f), new Vector3(0.02f, 0.012f, 0.6f), mAluminium, false));
                NoShadows(Box("Hatch Frame", hatch, new Vector3(0f, -0.006f, s * 0.29f), new Vector3(0.56f, 0.012f, 0.02f), mAluminium, false));
            }
            NoShadows(Prim(PrimitiveType.Cylinder, "Hatch Latch", hatch, new Vector3(0f, -0.0095f, 0.22f), new Vector3(0.025f, 0.0015f, 0.025f), mDark));

            Box("Roof Slab", p, new Vector3(16f, 3.15f, 4f), new Vector3(32f, 0.3f, 8f), mRoof, false);
            Box("Roof Slab", p, new Vector3(6.5f, 3.15f, -3f), new Vector3(13f, 0.3f, 6f), mRoof, false);
            Box("Roof Slab", p, new Vector3(26f, 3.15f, -3f), new Vector3(12f, 0.3f, 6f), mRoof, false);
            Box("Stair Tower Roof", p, new Vector3(16.5f, StairCeilingHeight + 0.17f, -3f), new Vector3(7f, 0.3f, 6f), mRoof, false);
            Box("Rooftop Plant", p, new Vector3(6f, 3.85f, 4.5f), new Vector3(2.4f, 1.1f, 1.6f), mCabinet, false);
            Box("Rooftop Plant", p, new Vector3(26f, 3.75f, 5f), new Vector3(1.8f, 0.9f, 1.4f), mCabinet, false);
        }

        static void BuildDoors(Transform p)
        {
            // Architraves stand 5 mm proud of the 35 mm buffer rail (and the exits' of the 50 mm plinth), so trims die into them.
            // Thresholds cover the floor-finish joints; like all trim and ironmongery they have no colliders (nothing to trip
            // the CharacterController). Door stops are 3 cm floor studs where the held-open leaves rest.

            // Classroom doors: steel frames, double leaves held open into the rooms.
            foreach (float x in ClassroomDoors)
            {
                DoorFrame(p, true, 3f, x, x + 1.6f, DoorHeight, InteriorWall, 0.04f);
                DoorLeaf(p, "Door Leaf", new Vector3(x + 0.07f, 0f, 3.13f), 0.72f, -90f, mDoorWood, true, true);
                DoorLeaf(p, "Door Leaf", new Vector3(x + 1.53f, 0f, 3.13f), 0.72f, -90f, mDoorWood, true, true);
                Threshold(p, new Vector3(x + 0.8f, 0.002f, 3f), new Vector3(1.48f, 0.004f, 0.1f));
                DoorStop(p, new Vector3(x + 0.0275f, 0f, 3.75f));
                DoorStop(p, new Vector3(x + 1.5725f, 0f, 3.75f));
            }

            // Stairwell: double fire doors held open into the stairwell (on hold-open releases, hence KEEP CLEAR).
            DoorFrame(p, true, 0f, 15f, 16.6f, DoorHeight, InteriorWall, 0.04f);
            DoorLeaf(p, "Door Leaf", new Vector3(15.07f, 0f, -0.13f), 0.72f, 90f, mDoorWood, true, true, fireDoorSign: true);
            DoorLeaf(p, "Door Leaf", new Vector3(16.53f, 0f, -0.13f), 0.72f, 90f, mDoorWood, true, true, fireDoorSign: true);
            Threshold(p, new Vector3(15.8f, 0.002f, 0f), new Vector3(1.48f, 0.004f, 0.1f));
            DoorStop(p, new Vector3(15.0275f, 0f, -0.75f));
            DoorStop(p, new Vector3(16.5725f, 0f, -0.75f));
            // Intumescent seal in the frame rebate (jambs and head), as on any fire-door set.
            NoShadows(Box("Intumescent Strip", p, new Vector3(15.062f, (DoorHeight - 0.06f) / 2f, 0f), new Vector3(0.004f, DoorHeight - 0.06f, 0.01f), mDark, false));
            NoShadows(Box("Intumescent Strip", p, new Vector3(16.538f, (DoorHeight - 0.06f) / 2f, 0f), new Vector3(0.004f, DoorHeight - 0.06f, 0.01f), mDark, false));
            NoShadows(Box("Intumescent Strip", p, new Vector3(15.8f, DoorHeight - 0.062f, 0f), new Vector3(1.472f, 0.004f, 0.01f), mDark, false));

            // Elevator alcove: cased opening (same vinyl both sides, so no threshold).
            DoorFrame(p, true, 0f, 4f, 7f, 2.4f, InteriorWall, 0.04f);

            // Exit A: fire doors shut against the smoke (impassable), push bars on the corridor side. Heat has browned and
            // blistered the upper paint; the leaves keep their colliders exactly as before.
            DoorFrame(p, false, 0f, 0.6f, 2.4f, DoorHeight, ExteriorWall, 0.06f);
            DoorLeaf(p, "Exit A Door (closed)", new Vector3(0f, 0f, 0.66f), 0.84f, -90f, mDoorBlockedScorched, false, true, pushFace: Vector3.right);
            DoorLeaf(p, "Exit A Door (closed)", new Vector3(0f, 0f, 2.34f), 0.84f, 90f, mDoorBlockedScorched, false, true, pushFace: Vector3.right);
            // Exit thresholds: the outer lip drops over the 10 mm step down to the paving and hides it.
            Threshold(p, new Vector3(-0.02f, -0.0035f, 1.5f), new Vector3(0.36f, 0.017f, 1.68f));
            Threshold(p, new Vector3(32.03f, -0.0035f, 1.5f), new Vector3(0.34f, 0.017f, 1.68f));

            // Exit B: glazed exit doors standing open outwards; their push bars face the doorway (the corridor side when shut).
            DoorFrame(p, false, 32f, 0.6f, 2.4f, DoorHeight, ExteriorWall, 0.06f);
            DoorLeaf(p, "Exit B Door (open)", new Vector3(32.17f, 0f, 0.66f), 0.84f, 0f, mFrame, true, true, 0.42f, pushFace: Vector3.forward);
            DoorLeaf(p, "Exit B Door (open)", new Vector3(32.17f, 0f, 2.34f), 0.84f, 0f, mFrame, true, true, 0.42f, pushFace: Vector3.back);
        }

        /// <summary>Floor-mounted door stop (visual only, 3 cm high).</summary>
        static void DoorStop(Transform p, Vector3 floor) =>
            NoShadows(Prim(PrimitiveType.Cylinder, "Door Stop", p, floor + Vector3.up * 0.015f, new Vector3(0.035f, 0.015f, 0.035f), mDark));

        static void BuildWindows(Transform p)
        {
            foreach (float c in NorthWindowCenters)
                WindowUnit(p, true, 8f, c - WindowWidth / 2f, c + WindowWidth / 2f, 0.9f, 2.4f, ExteriorWall, 1f);
            WindowUnit(p, false, 0f, 4.4f, 6.6f, 0.9f, 2.4f, ExteriorWall, -1f);
            WindowUnit(p, false, 32f, 4.4f, 6.6f, 0.9f, 2.4f, ExteriorWall, 1f);
            WindowUnit(p, true, -6f, 15.6f, 17.4f, 2.4f, 4.2f, ExteriorWall, -1f);
        }

        // ---------- Doors and windows ----------

        static void DoorFrame(Transform p, bool alongX, float at, float from, float to, float height, float wallThickness, float architraveDepth)
        {
            const float w = 0.06f;
            float depth = wallThickness + 0.03f;
            ChamferBox("Door Frame", p, WallPoint(alongX, at, from + w / 2f, height / 2f), WallSize(alongX, w, height, depth), mFrame, 0.004f);
            ChamferBox("Door Frame", p, WallPoint(alongX, at, to - w / 2f, height / 2f), WallSize(alongX, w, height, depth), mFrame, 0.004f);
            ChamferBox("Door Frame", p, WallPoint(alongX, at, (from + to) / 2f, height - w / 2f), WallSize(alongX, to - from, w, depth), mFrame, 0.004f);

            // Eased architrave edges catch a highlight and outline the opening; thin trim on a casting wall needs no shadow.
            foreach (float side in new[] { -1f, 1f })
            {
                float face = at + side * (wallThickness / 2f + architraveDepth / 2f);
                NoShadows(ChamferBox("Architrave", p, WallPoint(alongX, face, from - 0.035f, (height + 0.07f) / 2f), WallSize(alongX, 0.07f, height + 0.07f, architraveDepth), mFrame, 0.004f));
                NoShadows(ChamferBox("Architrave", p, WallPoint(alongX, face, to + 0.035f, (height + 0.07f) / 2f), WallSize(alongX, 0.07f, height + 0.07f, architraveDepth), mFrame, 0.004f));
                NoShadows(ChamferBox("Architrave", p, WallPoint(alongX, face, (from + to) / 2f, height + 0.035f), WallSize(alongX, to - from + 0.14f, 0.07f, architraveDepth), mFrame, 0.004f));

                // Soft contact shading on the floor in front of each architrave foot (inside openings only: floor at y = 0).
                if (!alongX) continue;
                Vector3 away = new Vector3(0f, 0f, side);
                float front = at + side * (wallThickness / 2f + architraveDepth + 0.055f);
                foreach (float along in new[] { from - 0.005f, to + 0.005f })
                    Decal("Architrave Contact", p, WallPoint(true, front, along, 0.002f), Vector3.up, away, new Vector2(0.26f, 0.11f), mContactPool);
            }
        }

        /// <summary>Aluminium threshold plate over a floor-finish joint. Never a collider: it sits in the walking path.</summary>
        static void Threshold(Transform p, Vector3 center, Vector3 size) =>
            NoShadows(ChamferBox("Threshold", p, center, size, mAluminium, 0.0015f));

        /// <summary>
        /// Door leaf hinged at <paramref name="hinge"/>; closed it extends along local +X, rotated by
        /// <paramref name="yaw"/> degrees about Y. Leaves with a vision panel are built around a real glazed opening.
        /// Exit leaves (<paramref name="pushFace"/>: world direction their push side faces) get a push bar instead of levers.
        /// All ironmongery is renderer-only, built in the pivot's space like the kick plate.
        /// </summary>
        static void DoorLeaf(Transform p, string name, Vector3 hinge, float width, float yaw, Material material, bool vision, bool collider,
            float visionWidth = 0.22f, Vector3? pushFace = null, bool fireDoorSign = false)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(p, false);
            pivot.localPosition = hinge;
            pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
            pivot.gameObject.isStatic = true;

            const float h = DoorHeight - 0.07f, t = 0.045f;
            if (vision)
            {
                float g0 = width / 2f - visionWidth / 2f, g1 = width / 2f + visionWidth / 2f;
                const float b0 = 1.05f, b1 = 1.85f;
                Box("Leaf", pivot, new Vector3(width / 2f, b0 / 2f, 0f), new Vector3(width, b0, t), material, collider);
                Box("Leaf", pivot, new Vector3(width / 2f, (b1 + h) / 2f, 0f), new Vector3(width, h - b1, t), material, collider);
                Box("Leaf", pivot, new Vector3(g0 / 2f, (b0 + b1) / 2f, 0f), new Vector3(g0, b1 - b0, t), material, collider);
                Box("Leaf", pivot, new Vector3((g1 + width) / 2f, (b0 + b1) / 2f, 0f), new Vector3(width - g1, b1 - b0, t), material, collider);
                NoShadows(Box("Vision Panel", pivot, new Vector3(width / 2f, (b0 + b1) / 2f, 0f), new Vector3(visionWidth, b1 - b0, 0.01f), mGlass, false));
            }
            else ChamferBox("Leaf", pivot, new Vector3(width / 2f, h / 2f, 0f), new Vector3(width, h, t), material, 0.004f, collider);

            ChamferBox("Kick Plate", pivot, new Vector3(width / 2f, 0.12f, 0f), new Vector3(width - 0.04f, 0.2f, t + 0.004f), mAluminium, 0.0015f);

            // Push side: +1 when local +Z faces the given world direction.
            float push = pushFace.HasValue ? (Vector3.Dot(pivot.rotation * Vector3.forward, pushFace.Value) >= 0f ? 1f : -1f) : -1f;
            if (pushFace.HasValue)
            {
                PushBar(pivot, width, t, push);
                if (vision)
                    Sign(pivot, "Sign - Push Bar To Open", "PUSH BAR TO OPEN", new Vector3(width / 2f, 1.12f, push * 0.0075f), new Vector3(0f, 0f, push),
                        new Vector2(0.2f, 0.06f), SignGreen, Color.white, 16, housingDepth: 0.002f, viewDistance: 1.5f);
            }
            else
                foreach (float side in new[] { -1f, 1f })
                    LeverHandle(pivot, width - 0.1f, t, side);

            // Butt-hinge knuckles on the pull side and an overhead closer on the push side.
            foreach (float y in new[] { 0.25f, 1.05f, 1.85f })
                NoShadows(Rod("Hinge Knuckle", pivot, new Vector3(0.004f, y - 0.05f, -push * t / 2f), new Vector3(0.004f, y + 0.05f, -push * t / 2f), 0.008f, mAluminium));
            NoShadows(ChamferBox("Door Closer", pivot, new Vector3(0.25f, h - 0.08f, push * (t / 2f + 0.0275f)), new Vector3(0.27f, 0.05f, 0.055f), mAluminium, 0.008f));
            NoShadows(Box("Closer Arm", pivot, new Vector3(0.42f, h - 0.035f, push * (t / 2f + 0.03f)), new Vector3(0.25f, 0.012f, 0.02f), mAluminium, false,
                localRotation: Quaternion.Euler(0f, push * 8f, 0f)));

            if (fireDoorSign)
                foreach (float side in new[] { -1f, 1f })
                    Sign(pivot, "Sign - Fire Door", "FIRE DOOR\nKEEP CLEAR", new Vector3(width - 0.12f, 1.6f, side * (t / 2f + 0.002f)), new Vector3(0f, 0f, side),
                        new Vector2(0.15f, 0.1f), FireDoorBlue, Color.white, 18, housingDepth: 0.002f, viewDistance: 1.5f);
        }

        static readonly Color FireDoorBlue = new Color(0.05f, 0.27f, 0.6f);

        /// <summary>Lever on a round rose, with a safety return and a key escutcheon, on the leaf face <paramref name="side"/> (+1/-1 local Z).</summary>
        static void LeverHandle(Transform pivot, float x, float t, float side)
        {
            float f = side * t / 2f;
            Quaternion axis = Quaternion.Euler(90f, 0f, 0f);   // cylinder axis along the face normal
            NoShadows(Prim(PrimitiveType.Cylinder, "Lever Rose", pivot, new Vector3(x, 1.02f, f + side * 0.004f), new Vector3(0.052f, 0.004f, 0.052f), mChrome, localRotation: axis));
            NoShadows(Prim(PrimitiveType.Cylinder, "Lever Spindle", pivot, new Vector3(x, 1.02f, f + side * 0.031f), new Vector3(0.019f, 0.0235f, 0.019f), mChrome, localRotation: axis));
            NoShadows(ChamferBox("Lever", pivot, new Vector3(x - 0.056f, 1.02f, f + side * 0.055f), new Vector3(0.13f, 0.019f, 0.019f), mChrome, 0.006f));
            NoShadows(ChamferBox("Lever Return", pivot, new Vector3(x - 0.112f, 1.02f, f + side * 0.038f), new Vector3(0.019f, 0.019f, 0.03f), mChrome, 0.006f));
            NoShadows(Prim(PrimitiveType.Cylinder, "Escutcheon", pivot, new Vector3(x, 0.88f, f + side * 0.002f), new Vector3(0.035f, 0.002f, 0.035f), mChrome, localRotation: axis));
        }

        /// <summary>Panic push bar across the leaf at 1 m, on stand-off end housings (keeps its shadow: it stands 8 cm proud).</summary>
        static void PushBar(Transform pivot, float width, float t, float side)
        {
            float f = side * t / 2f, x0 = (width - 0.62f) / 2f, x1 = x0 + 0.62f;
            Rod("Push Bar", pivot, new Vector3(x0, 1f, f + side * 0.075f), new Vector3(x1, 1f, f + side * 0.075f), 0.015f, mChrome);
            foreach (float x in new[] { x0, x1 })
                ChamferBox("Push Bar Housing", pivot, new Vector3(x, 1f, f + side * 0.04f), new Vector3(0.06f, 0.07f, 0.08f), mAluminium, 0.01f);
        }

        /// <param name="outward">+1 or -1: direction (along the wall normal) of the exterior.</param>
        static void WindowUnit(Transform p, bool alongX, float at, float from, float to, float sill, float head, float wallThickness, float outward)
        {
            float mid = (from + to) / 2f, w = to - from, h = head - sill, cy = (sill + head) / 2f;
            const float f = 0.05f, d = 0.08f;
            // Frames keep casting: their shadows break up the sun patches on the floor.
            ChamferBox("Window Frame", p, WallPoint(alongX, at, mid, sill + f / 2f), WallSize(alongX, w, f, d), mAluminium, 0.003f);
            ChamferBox("Window Frame", p, WallPoint(alongX, at, mid, head - f / 2f), WallSize(alongX, w, f, d), mAluminium, 0.003f);
            ChamferBox("Window Frame", p, WallPoint(alongX, at, from + f / 2f, cy), WallSize(alongX, f, h, d), mAluminium, 0.003f);
            ChamferBox("Window Frame", p, WallPoint(alongX, at, to - f / 2f, cy), WallSize(alongX, f, h, d), mAluminium, 0.003f);
            ChamferBox("Window Mullion", p, WallPoint(alongX, at, mid, cy), WallSize(alongX, 0.04f, h, d), mAluminium, 0.003f);
            ChamferBox("Window Transom", p, WallPoint(alongX, at, mid, sill + h * 0.72f), WallSize(alongX, w, 0.04f, d), mAluminium, 0.003f);
            NoShadows(Box("Glass", p, WallPoint(alongX, at, mid, cy), WallSize(alongX, w, h, 0.01f), mGlass, true));

            float board = wallThickness / 2f + 0.05f;
            ChamferBox("Window Board", p, WallPoint(alongX, at - outward * board / 2f, mid, sill - 0.015f), WallSize(alongX, w + 0.1f, 0.03f, board), mLabTop, 0.008f);
            float outer = wallThickness / 2f + 0.07f;
            ChamferBox("Window Sill", p, WallPoint(alongX, at + outward * outer / 2f, mid, sill - 0.03f), WallSize(alongX, w + 0.1f, 0.05f, outer), mCoping, 0.008f);
        }

        // ---------- Stairwell and elevator ----------

        static void BuildStairs(Transform p)
        {
            const int steps = 8;
            const float rise = 0.2f, run = 0.35f, x = 18f, width = 2f, zStart = -1.5f;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                float zFront = zStart - run * i;
                Box($"Step {i + 1}", p, new Vector3(x, top / 2f, zFront - run / 2f), new Vector3(width, top, run), mConcrete);
                Box("Nosing", p, new Vector3(x, top + 0.004f, zFront - 0.03f), new Vector3(width, 0.008f, 0.05f), mSafetyYellow, false);
            }
            float zEnd = zStart - run * steps;
            const float wallFace = -5.85f;
            Box("Landing", p, new Vector3(x, steps * rise / 2f, (zEnd + wallFace) / 2f), new Vector3(width, steps * rise, zEnd - wallFace), mConcrete);

            foreach (float side in new[] { x - width / 2f + 0.05f, x + width / 2f - 0.05f })
            {
                Vector3 bottom = new Vector3(side, rise + 0.9f, zStart - run * 0.5f);
                Vector3 top = new Vector3(side, steps * rise + 0.9f, zEnd + run * 0.5f);
                Vector3 end = new Vector3(side, steps * rise + 0.9f, wallFace + 0.1f);
                Rod("Handrail", p, bottom, top, 0.022f, mAluminium);
                Rod("Handrail", p, top, end, 0.022f, mAluminium);
                Rod("Baluster", p, new Vector3(side, rise, bottom.z), bottom, 0.018f, mAluminium);
                Rod("Baluster", p, new Vector3(side, steps * rise, top.z), top, 0.018f, mAluminium);
                Rod("Baluster", p, new Vector3(side, steps * rise, end.z), end, 0.018f, mAluminium);
            }

            // Visual-only balustrade: a cast upstand along the pitch and glass infill under each handrail, so the open
            // sides read as finished while the flight stays visible from the doorway. Steps and landing keep their colliders.
            float pitch = Mathf.Atan2(rise, run), cos = Mathf.Cos(pitch);
            var slope = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);   // local +Z runs down the flight, local +Y square to it
            Vector3 up = slope * Vector3.up;
            float NosingY(float z) => rise + (zStart - z) * rise / run;      // line through the step nosings
            const float railAbove = 0.8f;                                     // handrail height above the nosing line
            foreach (float side in new[] { x - width / 2f + 0.05f, x + width / 2f - 0.05f })
            {
                float z0 = zStart, z1 = zEnd + run;                           // first riser to top nosing
                Vector3 mid = new Vector3(side, NosingY((z0 + z1) / 2f), (z0 + z1) / 2f);
                ChamferBox("Stair Upstand", p, mid + up * -0.085f, new Vector3(0.12f, 0.33f, (z0 - z1) / cos), mConcrete, 0.01f, localRotation: slope);
                ChamferBox("Landing Upstand", p, new Vector3(side, steps * rise - 0.045f, (z1 + wallFace) / 2f), new Vector3(0.12f, 0.25f, z1 - wallFace), mConcrete, 0.01f);

                float g0 = zStart - run * 0.5f, g1 = zEnd + run * 0.5f;       // under the sloped handrail
                float gBottom = 0.05f, gTop = railAbove * cos - 0.03f;
                Vector3 gMid = new Vector3(side, NosingY((g0 + g1) / 2f), (g0 + g1) / 2f);
                NoShadows(Box("Balustrade Glass", p, gMid + up * ((gBottom + gTop) / 2f), new Vector3(0.012f, gTop - gBottom, (g0 - g1) / cos), mGlass, false, localRotation: slope));
                float lBottom = steps * rise + 0.05f, lTop = steps * rise + 0.9f - 0.03f;
                NoShadows(Box("Balustrade Glass", p, new Vector3(side, (lBottom + lTop) / 2f, (g1 + wallFace + 0.1f) / 2f), new Vector3(0.012f, lTop - lBottom, g1 - wallFace - 0.1f), mGlass, false));
            }
        }

        static void BuildElevator(Transform p)
        {
            // Back wall of the alcove faces +Z at z = -1.9. Stainless jambs and head frame doors recessed 55 mm behind them.
            ChamferBox("Elevator Jamb", p, new Vector3(4.84f, 1.1f, -1.86f), new Vector3(0.12f, 2.2f, 0.08f), mMetal, 0.004f);
            ChamferBox("Elevator Jamb", p, new Vector3(6.16f, 1.1f, -1.86f), new Vector3(0.12f, 2.2f, 0.08f), mMetal, 0.004f);
            ChamferBox("Elevator Head", p, new Vector3(5.5f, 2.16f, -1.86f), new Vector3(1.44f, 0.12f, 0.08f), mMetal, 0.004f);
            Box("Elevator Door Left", p, new Vector3(5.2f, 1.05f, -1.885f), new Vector3(0.595f, 2.1f, 0.02f), mMetal, false);
            Box("Elevator Door Right", p, new Vector3(5.8f, 1.05f, -1.885f), new Vector3(0.595f, 2.1f, 0.02f), mMetal, false);
            Box("Elevator Door Gap", p, new Vector3(5.5f, 1.05f, -1.897f), new Vector3(0.008f, 2.1f, 0.002f), mDark, false);   // behind the 5 mm meeting gap
            NoShadows(Box("Landing Sill", p, new Vector3(5.5f, 0.003f, -1.83f), new Vector3(1.3f, 0.006f, 0.1f), mAluminium, false));
            ChamferBox("Floor Indicator Bezel", p, new Vector3(5.5f, 2.42f, -1.885f), new Vector3(0.46f, 0.16f, 0.03f), mMetal, 0.004f);
            Box("Floor Indicator", p, new Vector3(5.5f, 2.42f, -1.865f), new Vector3(0.4f, 0.1f, 0.01f), mDark, false);
            ChamferBox("Call Panel", p, new Vector3(6.55f, 1.15f, -1.89f), new Vector3(0.12f, 0.3f, 0.02f), mMetal, 0.004f);
            Prim(PrimitiveType.Cylinder, "Call Button", p, new Vector3(6.55f, 1.2f, -1.877f), new Vector3(0.035f, 0.004f, 0.035f), mDark,
                localRotation: Quaternion.Euler(90f, 0f, 0f));

            // Out-of-service barrier across the alcove (posts close to the walls so it cannot be bypassed).
            foreach (float x in new[] { 4.24f, 6.76f })
            {
                Rod("Barrier Post", p, new Vector3(x, 0f, -0.35f), new Vector3(x, 1.05f, -0.35f), 0.035f, mHazard);
                ColliderBox("Barrier Post Collider", p, new Vector3(x, 0.525f, -0.35f), new Vector3(0.08f, 1.05f, 0.08f));
            }
            Box("Barrier Rail", p, new Vector3(5.5f, 0.95f, -0.35f), new Vector3(2.52f, 0.12f, 0.04f), mHazard);
            Box("Barrier Rail", p, new Vector3(5.5f, 0.5f, -0.35f), new Vector3(2.52f, 0.12f, 0.04f), mHazard);
        }
    }
}
