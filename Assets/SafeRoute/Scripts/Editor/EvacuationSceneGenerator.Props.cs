using UnityEngine;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        static void BuildInteriors(Transform p)
        {
            // Classroom 1 (where the trainee starts) gets the scanned Poly Haven desks and chairs; Classroom 2 stays procedural.
            Classroom(Group("Classroom 1", p), 12f, new[] { 2.2f, 3.8f, 5.4f }, realFurniture: true);
            Classroom(Group("Classroom 2", p), 22f, new[] { 14.0f, 15.6f, 17.2f });
            Lab(Group("Lab", p));
            CorridorFixtures(Group("Corridor", p));
        }

        // ---------- Rooms ----------

        /// <summary>Desks face east towards a whiteboard on the partition at x = <paramref name="eastWall"/>.</summary>
        static void Classroom(Transform p, float eastWall, float[] rows, bool realFurniture = false)
        {
            float face = eastWall - InteriorWall / 2f;
            Box("Whiteboard", p, new Vector3(face - 0.012f, 1.5f, 5.5f), new Vector3(0.02f, 1.2f, 2.4f), mWhiteboard, false);
            Box("Whiteboard Frame", p, new Vector3(face - 0.018f, 2.115f, 5.5f), new Vector3(0.03f, 0.04f, 2.48f), mAluminium, false);
            Box("Whiteboard Frame", p, new Vector3(face - 0.018f, 0.885f, 5.5f), new Vector3(0.03f, 0.04f, 2.48f), mAluminium, false);
            Box("Whiteboard Frame", p, new Vector3(face - 0.018f, 1.5f, 4.28f), new Vector3(0.03f, 1.27f, 0.04f), mAluminium, false);
            Box("Whiteboard Frame", p, new Vector3(face - 0.018f, 1.5f, 6.72f), new Vector3(0.03f, 1.27f, 0.04f), mAluminium, false);
            Box("Marker Tray", p, new Vector3(face - 0.05f, 0.9f, 5.5f), new Vector3(0.07f, 0.02f, 1.2f), mAluminium, false);
            Prim(PrimitiveType.Cylinder, "Clock Rim", p, new Vector3(face - 0.02f, 2.5f, 5.5f), new Vector3(0.34f, 0.02f, 0.34f), mDark,
                localRotation: Quaternion.Euler(0f, 0f, 90f));
            Prim(PrimitiveType.Cylinder, "Clock Face", p, new Vector3(face - 0.032f, 2.5f, 5.5f), new Vector3(0.3f, 0.005f, 0.3f), mPaper,
                localRotation: Quaternion.Euler(0f, 0f, 90f));

            Desk(p, "Teacher Desk", new Vector3(eastWall - 1.3f, 0f, 6.7f), new Vector3(0.75f, 0.76f, 1.5f), true);
            Chair(p, new Vector3(eastWall - 0.8f, 0f, 6.7f), Vector3.left, realFurniture);
            StorageCabinet(p, face, 3.65f);

            foreach (float x in rows)
                foreach (float z in new[] { 4.3f, 6.3f })
                {
                    Desk(p, "Desk", new Vector3(x, 0f, z), new Vector3(0.6f, 0.74f, 1.3f), real: realFurniture);
                    Chair(p, new Vector3(x - 0.55f, 0f, z - 0.32f), Vector3.right, realFurniture);
                    Chair(p, new Vector3(x - 0.55f, 0f, z + 0.32f), Vector3.right, realFurniture);
                }
        }

        static void Lab(Transform p)
        {
            const float face = 22f + InteriorWall / 2f;
            Box("Whiteboard", p, new Vector3(face + 0.012f, 1.5f, 5.5f), new Vector3(0.02f, 1.2f, 2.4f), mWhiteboard, false);
            Box("Whiteboard Frame", p, new Vector3(face + 0.018f, 2.115f, 5.5f), new Vector3(0.03f, 0.04f, 2.48f), mAluminium, false);
            Box("Whiteboard Frame", p, new Vector3(face + 0.018f, 0.885f, 5.5f), new Vector3(0.03f, 0.04f, 2.48f), mAluminium, false);
            foreach (float z in new[] { 4.7f, 6.7f }) LabBench(p, new Vector3(27f, 0f, z), 6.4f);
        }

        static void LabBench(Transform p, Vector3 c, float length)
        {
            // Same collider as before; the visible carcass starts above a plinth set 4 cm back, like the lockers.
            ColliderBox("Bench Base", p, c + new Vector3(0f, 0.43f, 0f), new Vector3(length, 0.86f, 0.75f));
            Box("Bench Carcass", p, c + new Vector3(0f, 0.48f, 0f), new Vector3(length, 0.76f, 0.75f), mCabinet, false);
            Box("Bench Plinth", p, c + new Vector3(0f, 0.05f, 0f), new Vector3(length - 0.08f, 0.1f, 0.67f), mTrim, false);
            ChamferBox("Bench Top", p, c + new Vector3(0f, 0.88f, 0f), new Vector3(length + 0.04f, 0.04f, 0.85f), mLabTop, 0.005f);
            float bay = length / 6f;
            for (int i = 0; i < 6; i++)
            {
                float x = c.x - length / 2f + bay * (i + 0.5f);
                foreach (float s in new[] { -1f, 1f })
                {
                    // Drawer over a cupboard door per bay, each with a pull, so the long base reads as joinery.
                    float z = c.z + s * 0.38f, pz = c.z + s * 0.391f;
                    ChamferBox("Drawer Front", p, new Vector3(x, 0.72f, z), new Vector3(bay - 0.04f, 0.18f, 0.01f), mLabTop, 0.002f);
                    ChamferBox("Cupboard Door", p, new Vector3(x, 0.37f, z), new Vector3(bay - 0.04f, 0.5f, 0.01f), mLabTop, 0.002f);
                    Box("Drawer Pull", p, new Vector3(x, 0.76f, pz), new Vector3(0.14f, 0.012f, 0.012f), mChrome, false);
                    Box("Door Pull", p, new Vector3(x + (bay / 2f - 0.1f) * (i % 2 == 0 ? 1f : -1f), 0.5f, pz),
                        new Vector3(0.012f, 0.14f, 0.012f), mChrome, false);
                }
            }
            Box("Sink", p, c + new Vector3(length / 2f - 0.5f, 0.902f, 0f), new Vector3(0.45f, 0.006f, 0.35f), mMetal, false);
            Rod("Tap", p, c + new Vector3(length / 2f - 0.5f, 0.9f, 0.25f), c + new Vector3(length / 2f - 0.5f, 1.15f, 0.25f), 0.012f, mChrome);
            Rod("Tap Spout", p, c + new Vector3(length / 2f - 0.5f, 1.14f, 0.25f), c + new Vector3(length / 2f - 0.5f, 1.12f, 0.1f), 0.01f, mChrome);
            for (int i = 0; i < 3; i++)
            {
                float x = c.x - length / 2f + length / 3f * (i + 0.5f);
                Stool(p, new Vector3(x, 0f, c.z - 0.65f));
                Stool(p, new Vector3(x, 0f, c.z + 0.65f));
            }
        }

        // ---------- Furniture ----------

        /// <summary>
        /// Steel-framed table: eased top with visible edge thickness, square-tube legs and an apron rail frame. The
        /// teacher's version (<paramref name="pedestal"/>) is used from +X, so it gets a drawer pedestal facing +X and the
        /// modesty panel on the class side (-X).
        /// </summary>
        static void Desk(Transform p, string name, Vector3 floor, Vector3 size, bool pedestal = false, bool real = false)
        {
            Transform d = Group(name, p);
            d.localPosition = floor;
            // Scanned single desks (0.71 x 0.55 m, 0.88 m high), two side by side per double desk, facing the board (+X).
            // Scaled 0.92 so the pair fills the 1.3 m footprint exactly (0.65 m each, 0.81 m high); same collider.
            if (real && !pedestal && mDeskModel != null)
            {
                foreach (float s in new[] { -1f, 1f })
                    PlaceModel("SchoolDesk_01", mDeskModel, d, "Desk (Poly Haven)", new Vector3(0f, 0f, s * size.z / 4f),
                        Quaternion.LookRotation(Vector3.right), 0.92f);
                ColliderBox("Collider", d, new Vector3(0f, size.y / 2f, 0f), size);
                return;
            }
            const float top = 0.03f, leg = 0.035f, rail = 0.05f;
            ChamferBox("Top", d, new Vector3(0f, size.y - top / 2f, 0f), new Vector3(size.x, top, size.z), mDesk, 0.006f);
            float lx = size.x / 2f - 0.04f, lz = size.z / 2f - 0.04f, under = size.y - top;
            foreach (float sx in new[] { -lx, lx })
                foreach (float sz in new[] { -lz, lz })
                {
                    if (pedestal && sz > 0f) continue;   // the pedestal carries this end
                    ChamferBox("Leg", d, new Vector3(sx, under / 2f, sz), new Vector3(leg, under, leg), mDark, 0.004f);
                }
            // Apron frame just under the top, between the legs.
            float ry = under - rail / 2f;
            foreach (float sx in new[] { -lx, lx })
                ChamferBox("Frame Rail", d, new Vector3(sx, ry, 0f), new Vector3(0.022f, rail, 2f * lz - leg), mDark, 0.003f);
            foreach (float sz in new[] { -lz, lz })
                ChamferBox("Frame Rail", d, new Vector3(0f, ry, sz), new Vector3(2f * lx - leg, rail, 0.022f), mDark, 0.003f);
            float px = pedestal ? -lx : lx;
            ChamferBox("Modesty Panel", d, new Vector3(px, size.y * 0.6f, 0f), new Vector3(0.012f, size.y * 0.45f, size.z - 0.1f), mCabinet, 0.003f);

            if (pedestal)
            {
                float pw = size.x - 0.05f, pz = size.z / 2f - 0.25f, ph = under - 0.04f;
                ChamferBox("Pedestal", d, new Vector3(0f, 0.02f + ph / 2f, pz), new Vector3(pw, ph, 0.42f), mCabinet, 0.004f);
                Box("Pedestal Plinth", d, new Vector3(0f, 0.01f, pz), new Vector3(pw - 0.04f, 0.02f, 0.38f), mDark, false);
                float fx = pw / 2f + 0.008f;
                foreach (float y in new[] { 0.145f, 0.355f, 0.565f })
                {
                    ChamferBox("Drawer Front", d, new Vector3(fx, y, pz), new Vector3(0.016f, 0.205f, 0.4f), mLabTop, 0.003f);
                    Box("Drawer Pull", d, new Vector3(fx + 0.014f, y + 0.06f, pz), new Vector3(0.012f, 0.012f, 0.12f), mChrome, false);
                }
            }
            ColliderBox("Collider", d, new Vector3(0f, size.y / 2f, 0f), size);
        }

        static void Chair(Transform p, Vector3 floor, Vector3 facing, bool real = false)
        {
            Transform c = Group("Chair", p);
            c.localPosition = floor;
            c.localRotation = Quaternion.LookRotation(facing);
            // Scanned chair (faces its +Z like the group), set 5 cm back so its seat front stops at the desk edge.
            if (real && PlaceModel("SchoolChair_01", mChairModel, c, "Chair (Poly Haven)", new Vector3(0f, 0f, -0.05f), Quaternion.identity) != null)
            {
                ColliderBox("Collider", c, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.9f, 0.45f));
                return;
            }
            // Moulded shell: rounded edges, seat tipped back 3 degrees so the front lip sits slightly higher.
            ChamferBox("Seat", c, new Vector3(0f, 0.46f, 0f), new Vector3(0.42f, 0.035f, 0.42f), mChairBlue, 0.012f,
                localRotation: Quaternion.Euler(-3f, 0f, 0f));
            ChamferBox("Back", c, new Vector3(0f, 0.72f, -0.2f), new Vector3(0.42f, 0.32f, 0.025f), mChairBlue, 0.01f,
                localRotation: Quaternion.Euler(-8f, 0f, 0f));
            foreach (float sx in new[] { -0.18f, 0.18f })
            {
                foreach (float sz in new[] { -0.18f, 0.18f })
                    Rod("Leg", c, new Vector3(sx, 0f, sz), new Vector3(sx, 0.45f, sz), 0.011f, mChrome);
                Rod("Back Post", c, new Vector3(sx, 0.45f, -0.19f), new Vector3(sx, 0.6f, -0.2f), 0.011f, mChrome);
                Rod("Seat Rail", c, new Vector3(sx, 0.43f, -0.19f), new Vector3(sx, 0.43f, 0.19f), 0.009f, mChrome);
            }
            ColliderBox("Collider", c, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.9f, 0.45f));
        }

        /// <summary>Tall two-door cupboard against a partition whose room-side face is at x = <paramref name="face"/>.</summary>
        static void StorageCabinet(Transform p, float face, float z)
        {
            // The carcass keeps the original collider; doors, kickboard and handles are decorative.
            ChamferBox("Storage Cabinet", p, new Vector3(face - 0.26f, 0.95f, z), new Vector3(0.5f, 1.9f, 0.9f), mCabinet, 0.006f, collider: true);
            Box("Cabinet Kickboard", p, new Vector3(face - 0.513f, 0.05f, z), new Vector3(0.006f, 0.1f, 0.88f), mDark, false);
            float front = face - 0.51f;
            foreach (float s in new[] { -1f, 1f })
            {
                ChamferBox("Cabinet Door", p, new Vector3(front - 0.008f, 0.99f, z + s * 0.2225f), new Vector3(0.016f, 1.74f, 0.44f), mCabinet, 0.003f);
                Box("Cabinet Handle", p, new Vector3(front - 0.026f, 1.05f, z + s * 0.03f), new Vector3(0.02f, 0.25f, 0.02f), mChrome, false);
            }
        }

        static void Stool(Transform p, Vector3 floor)
        {
            Transform s = Group("Stool", p);
            s.localPosition = floor;
            Prim(PrimitiveType.Cylinder, "Seat", s, new Vector3(0f, 0.66f, 0f), new Vector3(0.34f, 0.025f, 0.34f), mChairBlue);
            Rod("Pole", s, Vector3.zero, new Vector3(0f, 0.64f, 0f), 0.02f, mChrome);
            Prim(PrimitiveType.Cylinder, "Base", s, new Vector3(0f, 0.01f, 0f), new Vector3(0.38f, 0.01f, 0.38f), mDark);
            Prim(PrimitiveType.Cylinder, "Foot Ring", s, new Vector3(0f, 0.3f, 0f), new Vector3(0.3f, 0.006f, 0.3f), mChrome);
        }

        // ---------- Corridor ----------

        static void CorridorFixtures(Transform p)
        {
            Lockers(p, 21.2f, 25.2f, 0.1f);
            NoticeBoard(p, new Vector3(14.6f, 1.55f, 2.9f), 1.8f, 0.9f);
            Extinguisher(p, new Vector3(14.3f, 0f, 0.1f), Vector3.forward);
            Extinguisher(p, new Vector3(30.6f, 0f, 2.9f), Vector3.back);
            CallPoint(p, new Vector3(31.4f, 1.4f, 2.9f), Vector3.back);
            CallPoint(p, new Vector3(1.0f, 1.4f, 2.9f), Vector3.back);
            Prim(PrimitiveType.Cylinder, "Waste Bin", p, new Vector3(25.7f, 0.3f, 0.35f), new Vector3(0.32f, 0.3f, 0.32f), mCabinet, true);
        }

        /// <summary>Bank of two-tier lockers against the south corridor wall (wall face at z = <paramref name="wallFace"/>).</summary>
        static void Lockers(Transform p, float x0, float x1, float wallFace)
        {
            const float depth = 0.45f, height = 1.8f, door = 0.4f;
            float zc = wallFace + depth / 2f, mid = (x0 + x1) / 2f, front = wallFace + depth;
            Box("Locker Plinth", p, new Vector3(mid, 0.05f, zc - 0.02f), new Vector3(x1 - x0, 0.1f, depth - 0.04f), mTrim, false);
            ChamferBox("Locker Body", p, new Vector3(mid, 0.1f + height / 2f, zc), new Vector3(x1 - x0, height, depth), mLocker, 0.006f, collider: true);
            // Fake contact shading (multiply, no collider): on the floor in front of the recessed plinth and on the wall above the top.
            Decal("Locker Floor Contact", p, new Vector3(mid, 0.002f, front - 0.04f + 0.14f), Vector3.up, Vector3.forward, new Vector2(x1 - x0 + 0.06f, 0.28f), mContactAO);
            Decal("Locker Wall Contact", p, new Vector3(mid, 0.1f + height + 0.1f, wallFace + 0.003f), Vector3.forward, Vector3.up, new Vector2(x1 - x0 + 0.04f, 0.2f), mContactAO);
            int n = Mathf.RoundToInt((x1 - x0) / door);
            for (int i = 0; i < n; i++)
            {
                float x = x0 + door * (i + 0.5f);
                foreach (float y in new[] { 0.55f, 1.45f })
                {
                    ChamferBox("Locker Door", p, new Vector3(x, y, front + 0.005f), new Vector3(door - 0.02f, 0.86f, 0.01f), mCabinet, 0.002f);
                    Box("Locker Handle", p, new Vector3(x + door / 2f - 0.07f, y + 0.1f, front + 0.015f), new Vector3(0.02f, 0.1f, 0.02f), mChrome, false);
                    for (int v = 0; v < 3; v++)
                        Box("Locker Vent", p, new Vector3(x, y + 0.3f - v * 0.03f, front + 0.011f), new Vector3(0.2f, 0.008f, 0.002f), mDark, false);
                }
            }
        }

        static void NoticeBoard(Transform p, Vector3 center, float width, float height)
        {
            ChamferBox("Notice Board Frame", p, center + new Vector3(0f, 0f, -0.015f), new Vector3(width + 0.06f, height + 0.06f, 0.03f), mAluminium, 0.006f);
            Box("Notice Board", p, center + new Vector3(0f, 0f, -0.032f), new Vector3(width, height, 0.008f), mCork, false);
            Vector3[] sheets =
            {
                new Vector3(-0.6f, 0.15f, 3f), new Vector3(-0.25f, 0.2f, -2f), new Vector3(0.15f, 0.12f, 1.5f),
                new Vector3(0.55f, 0.18f, -3f), new Vector3(-0.42f, -0.22f, 2f), new Vector3(0.35f, -0.2f, -1f),
            };
            foreach (Vector3 s in sheets)
                Box("Notice", p, center + new Vector3(s.x, s.y, -0.038f), new Vector3(0.21f, 0.297f, 0.003f), mPaper, false, true, Quaternion.Euler(0f, 0f, s.z));
        }

        static void Extinguisher(Transform p, Vector3 wallFloor, Vector3 outward)
        {
            // Scanned 3.3 kg extinguisher in its floor stand (0.66 m high), back of the stand 2 cm off the wall, with the
            // English label (the Korean original was replaced when the maps were processed); else the primitive version.
            if (PlaceModel("korean_fire_extinguisher_01", mExtinguisherModel, p, "Extinguisher (Poly Haven)", wallFloor + outward * 0.02f,
                    Quaternion.LookRotation(outward) * ExtinguisherModelYaw, wallNormal: outward) != null)
            {
                Sign(p, "Sign - Fire Extinguisher", ExtinguisherLegend, wallFloor + outward * 0.012f + Vector3.up * 1.45f, outward,
                    new Vector2(0.28f, 0.2f), SignRed, Color.white, 40, housingDepth: 0.01f);
                return;
            }
            Vector3 c = wallFloor + outward * 0.11f;
            Box("Extinguisher Bracket", p, wallFloor + outward * 0.01f + Vector3.up * 0.75f, new Vector3(0.12f, 0.08f, 0.12f).With(outward, 0.02f), mDark, false);
            Prim(PrimitiveType.Cylinder, "Extinguisher", p, c + Vector3.up * 0.6f, new Vector3(0.16f, 0.25f, 0.16f), mExtinguisher);
            Prim(PrimitiveType.Sphere, "Extinguisher Top", p, c + Vector3.up * 0.85f, new Vector3(0.16f, 0.08f, 0.16f), mExtinguisher);
            Prim(PrimitiveType.Cylinder, "Extinguisher Valve", p, c + Vector3.up * 0.92f, new Vector3(0.05f, 0.04f, 0.05f), mDark);
            Sign(p, "Sign - Fire Extinguisher", "FIRE\nEXTINGUISHER", wallFloor + outward * 0.012f + Vector3.up * 1.45f, outward,
                new Vector2(0.28f, 0.2f), SignRed, Color.white, 40, housingDepth: 0.01f);
        }

        static void CallPoint(Transform p, Vector3 wallPoint, Vector3 outward)
        {
            // ISO 7010 F005 above the call point (Wikimedia artwork), and the scanned manual pull station (0.14 m high,
            // centred on wallPoint) when present.
            if (SignPictogram("ISO_7010_F005") is Sprite f005)
                PictogramSign(p, "Sign - Fire Alarm Call Point", f005, wallPoint + outward * 0.012f + Vector3.up * 0.22f, outward, 0.15f, SignRed, 0.01f);
            if (PlaceModel("fire_alarm", mCallPointModel, p, "Fire Alarm Call Point (Poly Haven)", wallPoint + Vector3.down * 0.07f,
                    Quaternion.LookRotation(outward) * CallPointModelYaw, wallNormal: outward) != null)
                return;
            Box("Fire Alarm Call Point", p, wallPoint + outward * 0.025f, new Vector3(0.1f, 0.1f, 0.1f).With(outward, 0.05f), mExtinguisher, false);
            Box("Call Point Glass", p, wallPoint + outward * 0.051f, new Vector3(0.06f, 0.06f, 0.06f).With(outward, 0.002f), mPaper, false);
        }

        // Model front (label / lever side) relative to its +Z; set from the orientation renders.
        static readonly Quaternion ExtinguisherModelYaw = Quaternion.identity;
        static readonly Quaternion CallPointModelYaw = Quaternion.identity;

        // ---------- Exterior ----------

        static void BuildExterior(Transform p)
        {
            Box("Ground", p, new Vector3(20f, -0.08f, 1f), new Vector3(110f, 0.1f, 80f), mGround);

            Transform paving = Group("Paving", p);
            FloorArea(paving, "Apron", -2.2f, 34.2f, 8.15f, 10f, mPath, -0.01f);
            FloorArea(paving, "Apron", -2.2f, 34.2f, -8f, -6.15f, mPath, -0.01f);
            FloorArea(paving, "Apron", -2.2f, -0.15f, -6.15f, 8.15f, mPath, -0.01f);
            FloorArea(paving, "Apron", 32.15f, 34.2f, -6.15f, 8.15f, mPath, -0.01f);
            FloorArea(paving, "Path to Assembly Point", 34.2f, 42f, 0.2f, 2.8f, mPath, -0.01f);
            FloorArea(paving, "Assembly Area", 42f, 48f, -1.5f, 4.5f, mPath, -0.01f);
            Box("Kerb", paving, new Vector3(38.1f, 0f, 0.15f), new Vector3(7.8f, 0.1f, 0.1f), mCoping);
            Box("Kerb", paving, new Vector3(38.1f, 0f, 2.85f), new Vector3(7.8f, 0.1f, 0.1f), mCoping);

            // Assembly point floor marking.
            Transform marking = Group("Assembly Point Marking", p);
            const float y = -0.007f;
            NoShadows(Box("Marking", marking, new Vector3(45f, y, -1.125f), new Vector3(5.4f, 0.004f, 0.15f), mAssembly, false));
            NoShadows(Box("Marking", marking, new Vector3(45f, y, 4.125f), new Vector3(5.4f, 0.004f, 0.15f), mAssembly, false));
            NoShadows(Box("Marking", marking, new Vector3(42.375f, y, 1.5f), new Vector3(0.15f, 0.004f, 5.4f), mAssembly, false));
            NoShadows(Box("Marking", marking, new Vector3(47.625f, y, 1.5f), new Vector3(0.15f, 0.004f, 5.4f), mAssembly, false));

            // Entrance canopy over Exit B.
            Transform canopy = Group("Exit B Canopy", p);
            Box("Canopy", canopy, new Vector3(33.475f, 2.85f, 1.5f), new Vector3(2.65f, 0.2f, 4.4f), mCoping, false);
            Box("Canopy Fascia", canopy, new Vector3(34.8f, 2.85f, 1.5f), new Vector3(0.04f, 0.26f, 4.44f), mAluminium, false);
            NoShadows(Box("Canopy Light", canopy, new Vector3(33.4f, 2.735f, 1.5f), new Vector3(0.3f, 0.03f, 1.2f), mLightPanel, false));
            foreach (float z in new[] { -0.45f, 3.45f })
            {
                Rod("Canopy Column", canopy, new Vector3(34.55f, 0f, z), new Vector3(34.55f, 2.75f, z), 0.07f, mAluminium);
                ColliderBox("Column Collider", canopy, new Vector3(34.55f, 1.375f, z), new Vector3(0.14f, 2.75f, 0.14f));
            }

            // Assembly point as a destination: contrasting paver border, bollards, waiting shelter, planting, ground text.
            Transform plaza = Group("Assembly Point", p);
            Box("Border Band", plaza, new Vector3(45f, -0.005f, -1.375f), new Vector3(6f, 0.012f, 0.25f), mConcrete, false);
            Box("Border Band", plaza, new Vector3(45f, -0.005f, 4.375f), new Vector3(6f, 0.012f, 0.25f), mConcrete, false);
            Box("Border Band", plaza, new Vector3(47.875f, -0.005f, 1.5f), new Vector3(0.25f, 0.012f, 5.5f), mConcrete, false);
            foreach (Vector3 b in new[] { new Vector3(42f, 0f, -1.5f), new Vector3(42f, 0f, 4.5f), new Vector3(48f, 0f, -1.5f), new Vector3(48f, 0f, 4.5f),
                         new Vector3(39f, 0f, 0.15f), new Vector3(39f, 0f, 2.85f) })
                Bollard(plaza, b);
            Shelter(plaza, new Vector3(47.2f, 0f, 1.5f));
            Planter(plaza, new Vector3(42.9f, 0f, 5.1f));
            Planter(plaza, new Vector3(47.1f, 0f, 5.1f));
            Planter(plaza, new Vector3(42.9f, 0f, -2.1f));
            FloorMarking(plaza, "Ground Marking - Assembly Point", "ASSEMBLY POINT", new Vector3(43.3f, -0.006f, 1.5f), Vector3.right,
                new Vector2(3.2f, 0.6f), new Color(0.95f, 0.97f, 0.95f), 300, false);

            Transform furniture = Group("Site Furniture", p);
            foreach (Vector3 lamp in new[] { new Vector3(36.5f, 0f, 3.5f), new Vector3(41.2f, 0f, 3.5f), new Vector3(48.6f, 0f, 4.8f), new Vector3(48.6f, 0f, -1.8f) })
                LampPost(furniture, lamp);
            Bench(furniture, new Vector3(45f, 0f, -0.9f), Vector3.forward);
            Bench(furniture, new Vector3(47.2f, 0f, 1.5f), Vector3.left);
            Planter(furniture, new Vector3(35.4f, 0f, -0.9f));
            Planter(furniture, new Vector3(35.4f, 0f, 3.95f));

            // Hedges and neighbouring buildings frame the site and give depth at a distance.
            Transform context = Group("Site Context", p);
            Hedge(Group("Hedge", context), new Vector3(-8f, 0f, 14f), new Vector3(60f, 0f, 14f), 0.9f, 0.8f, 1);
            Hedge(Group("Hedge", context), new Vector3(-8f, 0f, -13f), new Vector3(60f, 0f, -13f), 0.9f, 0.8f, 2);
            Hedge(Group("Hedge", context), new Vector3(-8f, 0f, -13f), new Vector3(-8f, 0f, 14f), 0.9f, 0.8f, 3);
            ContextBuilding(Group("Neighbouring Building", context), new Vector3(24f, 0f, 34f), new Vector3(36f, 14f, 12f), 4);
            ContextBuilding(Group("Neighbouring Building", context), new Vector3(78f, 0f, -20f), new Vector3(16f, 12f, 24f), 3);
            ContextBuilding(Group("Neighbouring Building", context), new Vector3(-22f, 0f, 6f), new Vector3(12f, 10f, 26f), 3);
        }

        /// <summary>
        /// Clipped hedge from <paramref name="a"/> to <paramref name="b"/> (ground points) as overlapping, slightly uneven
        /// rounded blocks, so a long run no longer reads as one straight prism. Deterministic via <see cref="Hash"/>.
        /// </summary>
        static void Hedge(Transform p, Vector3 a, Vector3 b, float height, float depth, int seed)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            int n = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
            float step = length / n;
            Quaternion along = Quaternion.LookRotation(Vector3.Cross(d.normalized, Vector3.up));   // local x runs along the hedge
            for (int i = 0; i < n; i++)
            {
                float h0 = Hash(i, 0, seed), h1 = Hash(i, 1, seed), h2 = Hash(i, 2, seed), h3 = Hash(i, 3, seed);
                // Sizes snap to 5 cm / 2 cm so the chamfered meshes are partly shared.
                float len = Mathf.Round((step + 0.15f + h0 * 0.35f) * 20f) / 20f;
                // Odd sections sit 1 cm off the 2 cm grid so overlapping neighbours never share a top face (z-fighting).
                float h = Mathf.Round((height + (h1 - 0.5f) * 0.16f) * 50f) / 50f + (i & 1) * 0.01f;
                float w = Mathf.Round((depth + (h2 - 0.5f) * 0.12f) * 50f) / 50f;
                Vector3 c = a + d * ((i + 0.5f) / n) + Vector3.up * (h / 2f);
                NoShadows(ChamferBox("Hedge Section", p, c, new Vector3(len, h, w), mHedge, 0.15f,
                    localRotation: along * Quaternion.Euler(0f, (h3 - 0.5f) * 3f, 0f)));
            }
        }

        /// <summary>
        /// Distant neighbouring block: facade box with ribbon windows per storey (reflective glazing with mullions in the
        /// texture over a concrete sill band), full-height piers giving a bay rhythm on all four faces, corner piers, a
        /// concrete base, a parapet cap and a rooftop plant box. Offsets are several cm so they hold up in the depth buffer
        /// at 40-60 m.
        /// </summary>
        static void ContextBuilding(Transform p, Vector3 floor, Vector3 size, int storeys)
        {
            const float band = 1.3f, bay = 3f, pierWidth = 0.4f, sill = 0.15f;
            Vector3 c = floor + Vector3.up * (size.y / 2f);
            NoShadows(ChamferBox("Facade", p, c, size, mFacadeLarge, 0.05f));
            float storey = (size.y - 0.4f) / storeys;
            for (int i = 0; i < storeys; i++)
            {
                float y = 0.6f + i * storey + 0.9f;   // band bottom
                NoShadows(Box("Window Band", p, floor + Vector3.up * (y + band / 2f), new Vector3(size.x + 0.08f, band, size.z + 0.08f), mContextGlazing, false));
                NoShadows(Box("Spandrel Sill", p, floor + Vector3.up * (y - sill / 2f), new Vector3(size.x + 0.16f, sill, size.z + 0.16f), mCoping, false));
            }
            // Piers run right through the block, so one box shows on both opposite faces. They stand 11-12 cm proud of the
            // facade (over the 4-8 cm window bands); the corner piers are separate so the end faces keep their windows.
            int nx = Mathf.Max(2, Mathf.RoundToInt(size.x / bay)), nz = Mathf.Max(2, Mathf.RoundToInt(size.z / bay));
            for (int i = 1; i < nx; i++)
            {
                float x = Mathf.Lerp(-size.x / 2f, size.x / 2f, (float)i / nx);
                NoShadows(Box("Pier", p, c + new Vector3(x, 0f, 0f), new Vector3(pierWidth, size.y, size.z + 0.24f), mFacadeLarge, false));
            }
            for (int i = 1; i < nz; i++)
            {
                float z = Mathf.Lerp(-size.z / 2f, size.z / 2f, (float)i / nz);
                NoShadows(Box("Pier", p, c + new Vector3(0f, 0f, z), new Vector3(size.x + 0.22f, size.y, pierWidth), mFacadeLarge, false));
            }
            const float corner = pierWidth + 0.12f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    NoShadows(Box("Corner Pier", p, c + new Vector3(sx * (size.x / 2f + 0.12f - corner / 2f), 0f, sz * (size.z / 2f + 0.12f - corner / 2f)),
                        new Vector3(corner, size.y, corner), mFacadeLarge, false));
            NoShadows(Box("Base", p, floor + Vector3.up * 0.3f, new Vector3(size.x + 0.34f, 0.6f, size.z + 0.34f), mConcrete, false));
            NoShadows(ChamferBox("Parapet Cap", p, floor + Vector3.up * (size.y - 0.15f), new Vector3(size.x + 0.4f, 0.4f, size.z + 0.4f), mCoping, 0.03f));
            // Rooftop plant room, set back from the parapet so the skyline is not one flat cut.
            NoShadows(Box("Rooftop Plant", p, floor + new Vector3(size.x * 0.18f, size.y + 0.9f, -size.z * 0.12f),
                new Vector3(Mathf.Min(6f, size.x * 0.3f), 1.8f, Mathf.Min(4f, size.z * 0.3f)), mCabinet, false));
        }

        static void LampPost(Transform p, Vector3 floor)
        {
            Rod("Lamp Pole", p, floor, floor + Vector3.up * 4.2f, 0.06f, mDark);
            Box("Lamp Head", p, floor + new Vector3(0f, 4.2f, 0f), new Vector3(0.5f, 0.1f, 0.25f), mDark, false);
            NoShadows(Box("Lamp Lens", p, floor + new Vector3(0f, 4.145f, 0f), new Vector3(0.42f, 0.012f, 0.18f), mLightPanel, false));
            ColliderBox("Lamp Collider", p, floor + Vector3.up * 1.5f, new Vector3(0.14f, 3f, 0.14f));
        }

        static void Bench(Transform p, Vector3 floor, Vector3 facing)
        {
            Transform b = Group("Bench", p);
            b.localPosition = floor;
            b.localRotation = Quaternion.LookRotation(facing);
            for (int i = 0; i < 4; i++)
                ChamferBox("Slat", b, new Vector3(0f, 0.45f, -0.18f + i * 0.12f), new Vector3(1.8f, 0.04f, 0.1f), mDesk, 0.008f);
            for (int i = 0; i < 2; i++)
                ChamferBox("Back Slat", b, new Vector3(0f, 0.65f + i * 0.14f, -0.27f), new Vector3(1.8f, 0.1f, 0.03f), mDesk, 0.008f,
                    localRotation: Quaternion.Euler(-10f, 0f, 0f));
            foreach (float x in new[] { -0.75f, 0.75f })
                ChamferBox("Bench Frame", b, new Vector3(x, 0.4f, -0.05f), new Vector3(0.06f, 0.8f, 0.5f), mDark, 0.01f);
            ColliderBox("Collider", b, new Vector3(0f, 0.4f, -0.05f), new Vector3(1.8f, 0.8f, 0.55f));
        }

        static void Bollard(Transform p, Vector3 floor)
        {
            Prim(PrimitiveType.Cylinder, "Bollard", p, floor + Vector3.up * 0.45f, new Vector3(0.16f, 0.45f, 0.16f), mDark, true);
            Prim(PrimitiveType.Cylinder, "Bollard Band", p, floor + Vector3.up * 0.78f, new Vector3(0.165f, 0.03f, 0.165f), mReflective);
        }

        /// <summary>Open waiting shelter (four posts, roof slab) over the assembly bench.</summary>
        static void Shelter(Transform p, Vector3 floor)
        {
            Transform s = Group("Waiting Shelter", p);
            s.localPosition = floor;
            foreach (float x in new[] { -0.6f, 0.6f })
                foreach (float z in new[] { -1.3f, 1.3f })
                {
                    Rod("Shelter Post", s, new Vector3(x, 0f, z), new Vector3(x, 2.5f, z), 0.05f, mAluminium);
                    ColliderBox("Post Collider", s, new Vector3(x, 1.25f, z), new Vector3(0.1f, 2.5f, 0.1f));
                }
            Box("Shelter Roof", s, new Vector3(0f, 2.55f, 0f), new Vector3(1.6f, 0.1f, 3f), mCoping, false);
            Box("Shelter Fascia", s, new Vector3(-0.8f, 2.55f, 0f), new Vector3(0.03f, 0.16f, 3.02f), mAluminium, false);
        }

        static void Planter(Transform p, Vector3 floor)
        {
            ChamferBox("Planter", p, floor + new Vector3(0f, 0.27f, 0f), new Vector3(1.4f, 0.55f, 0.6f), mConcrete, 0.02f, collider: true);
            // Three overlapping rounded shrubs instead of one block, varied per planter.
            int seed = Mathf.RoundToInt(floor.x * 10f) * 31 + Mathf.RoundToInt(floor.z * 10f);
            for (int i = 0; i < 3; i++)
            {
                float h = 0.28f + Hash(i, 0, seed) * 0.1f;
                NoShadows(ChamferBox("Planting", p, floor + new Vector3((i - 1) * 0.42f, 0.5f + h / 2f, (Hash(i, 1, seed) - 0.5f) * 0.04f),
                    new Vector3(0.52f, h, 0.5f), mHedge, 0.1f, localRotation: Quaternion.Euler(0f, (Hash(i, 2, seed) - 0.5f) * 20f, 0f)));
            }
        }

        /// <summary>Replaces the component of <paramref name="v"/> along the axis of <paramref name="normal"/> with <paramref name="value"/>.</summary>
        static Vector3 With(this Vector3 v, Vector3 normal, float value)
        {
            if (Mathf.Abs(normal.x) > 0.5f) v.x = value;
            else if (Mathf.Abs(normal.y) > 0.5f) v.y = value;
            else v.z = value;
            return v;
        }
    }
}
