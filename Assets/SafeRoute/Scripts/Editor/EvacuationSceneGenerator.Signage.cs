using UnityEngine;
using UnityEngine.UI;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        static readonly Color SignRed = new Color(0.72f, 0.06f, 0.05f);
        static readonly Color SignGreen = new Color(0.0f, 0.52f, 0.22f);
        static readonly Color SignNavy = new Color(0.1f, 0.15f, 0.26f);
        static readonly Color SignYellow = new Color(0.98f, 0.8f, 0.05f);

        enum Arrow { None, Left, Right, Up }

        // Signs in or next to the smoke draw after the particles (sortingOrder 0), like illuminated signage that stays
        // legible in smoke. They keep the normal ZTest, so walls and doors still hide them.
        const int SmokeProofSortingOrder = 5;

        static void BuildSignage(Transform p)
        {
            // Two visual languages: permanent building signs (illuminated green exit lightboxes, small navy room plates)
            // and temporary incident notices put up for this fire (yellow/black, taped on or on a floor A-frame).
            // viewDistance: where the sign is usually read from (text raster size).

            // Exit lightboxes (ISO 7010 E001/E002 style), above the exit doors and on the way to them. Legends are sized
            // for the distance they are read from (at least the pre-lightbox sign sizes): door 170, corridor 140, room 112 px.
            // Above Exit B: lower edge 2.42 m (clear of the bulkhead, top 2.38 m), top 2.93 m (ceiling 2.98 m).
            ExitSign(p, "Sign - Exit B", "EXIT B", new Vector3(31.85f, 2.675f, 1.5f), Vector3.left, Arrow.Up, 0.48f, 6f, legendSize: 170);
            // Opposite the Classroom 1 door (reader faces south, so east is on the left); above head height.
            ExitSign(p, "Sign - Exit B (left)", "EXIT B", new Vector3(10.9f, 2.15f, 0.1f), Vector3.forward, Arrow.Left, 0.45f, 3f, legendSize: 140);
            // Inside Classroom 1, above its door (reader faces south, so east is on the left): the legend spells out the
            // turn, since a bare left arrow over a door reads as "the exit is to the left of this door".
            ExitSign(p, "Sign - Classroom Exit Route", "EXIT B - TURN LEFT", new Vector3(9.8f, 2.6f, 3.1f), Vector3.forward, Arrow.Left, 0.36f, 4f, legendSize: 112);
            // Double-sided lightboxes hanging from the corridor ceiling: the green front faces people walking east, the
            // red back reads "EXIT A BLOCKED" for anyone heading west towards the fire. Lower edge 2.22 m (as before).
            foreach (float x in new[] { 13.25f, 23.25f, 28.25f })
                ExitSign(p, "Hanging Sign - Exit B", "EXIT B", new Vector3(x, 2.48f, 1.5f), Vector3.left, Arrow.Up, 0.5f, 5f, CeilingHeight,
                    legendSize: 160, minWidth: 1.6f, backLegend: "EXIT A BLOCKED", backSize: 170);

            // Temporary notices for this incident. Over the blocked exit, taped below its door head.
            // Full size (as the original exit sign): the blocked exit must read as dangerous from the corridor turn.
            TempNotice(p, "Sign - Exit A Blocked", "EXIT A - BLOCKED", null, new Vector3(0.15f + 0.003f, 2.6f, 1.5f), Vector3.right,
                new Vector2(1.5f, 0.36f), 140, 0, viewDistance: 5f, sortingOrder: SmokeProofSortingOrder);
            // Opposite the Classroom 1 door, pointing west to the blocked exit (reader faces south); wall x 7.9-9.1.
            TempNotice(p, "Sign - Exit A Blocked (right)", "EXIT A BLOCKED", null, new Vector3(8.5f, 1.6f, 0.1f + 0.003f), Vector3.forward,
                new Vector2(1.2f, 0.3f), 96, 0, Arrow.Right, 2.5f);
            // Overhead in the smoke near Exit A, facing people walking west; smoke-proof.
            HangingSign(p, "Hanging Sign - Smoke", new Vector3(6.2f, 2.4f, 1.5f), Vector3.right, new Vector2(1.5f, 0.4f),
                "DANGER - SMOKE", SignRed, Arrow.None, null, SignRed, Arrow.None, 140, CeilingHeight, 5f, SmokeProofSortingOrder);
            // Floor-standing A-frame against the north wall, just east of the Exit A warning zone (x <= 7.4) and west
            // of the route (Classroom 1 door at x 9-10.6); no collider, so it never blocks locomotion.
            AFrameNotice(p, "Sign - Danger Smoke (A-frame)", "DANGER\nSMOKE", "FIRE AT EXIT A - KEEP BACK", new Vector3(7.75f, 0f, 2.4f), Vector3.right,
                new Vector2(0.6f, 0.42f), 100, 30, 4f, SmokeProofSortingOrder);
            // Lift: full-size notice on a board clamped to the out-of-service barrier at the alcove mouth (rails at
            // z -0.35, top 1.01 m), in the corridor sightline. Visual only (no collider) and within the rail plane.
            Box("Lift Notice Board", p, new Vector3(5.5f, 1.21f, -0.35f), new Vector3(1.46f, 0.64f, 0.02f), mDark, false);
            TempNotice(p, "Sign - Elevator", "LIFT OUT OF SERVICE\nDO NOT USE IN A FIRE", null, new Vector3(5.5f, 1.32f, -0.338f), Vector3.forward,
                new Vector2(1.4f, 0.38f), 90, 0, viewDistance: 3f, sortingOrder: SmokeProofSortingOrder);
            // Also taped across the lift doors themselves, in the lift-shaft smoke.
            TempNotice(p, "Sign - Elevator (Lift Doors)", "LIFT OUT\nOF SERVICE", "DO NOT USE IN A FIRE", new Vector3(5.5f, 1.5f, -1.875f + 0.003f), Vector3.forward,
                new Vector2(0.48f, 0.32f), 56, 26, viewDistance: 3f, sortingOrder: SmokeProofSortingOrder);

            // Room plates beside the latch side of each door at eye level, flush on the wall (corridor side), clear of
            // the 70 mm architrave.
            Sign(p, "Sign - Classroom 1", "CLASSROOM 1", new Vector3(10.6f + 0.4f, 1.55f, 2.9f - 0.006f), Vector3.back, new Vector2(0.58f, 0.16f), SignNavy, Color.white, 64,
                housingDepth: 0.006f, viewDistance: 2f);
            Sign(p, "Sign - Classroom 2", "CLASSROOM 2", new Vector3(20.6f + 0.4f, 1.55f, 2.9f - 0.006f), Vector3.back, new Vector2(0.58f, 0.16f), SignNavy, Color.white, 64,
                housingDepth: 0.006f, viewDistance: 2f);
            Sign(p, "Sign - Lab", "LAB", new Vector3(27.6f + 0.4f, 1.55f, 2.9f - 0.006f), Vector3.back, new Vector2(0.58f, 0.16f), SignNavy, Color.white, 64,
                housingDepth: 0.006f, viewDistance: 2f);
            // Stairwell: the "why not this way" message over the stair doors, above the emergency bulkhead (top 2.38 m);
            // lower edge 2.43 m, sized to read from the corridor.
            Sign(p, "Sign - Stairwell", "STAIRS - UPPER FLOORS ONLY\nNOT AN EXIT", new Vector3(15.8f, 2.6f, 0.1f + 0.02f), Vector3.forward,
                new Vector2(1.4f, 0.32f), SignNavy, Color.white, 80, housingDepth: 0.02f, viewDistance: 3f);

            // Outside.
            PostSign(p, "Sign - To Assembly Point", new Vector3(35.6f, 0f, 3.3f), 2.1f, Vector3.left, new Vector2(1.5f, 0.36f),
                "ASSEMBLY POINT", SignGreen, Arrow.Up, 100, false, 6f);
            PostSign(p, "Sign - Assembly Point", new Vector3(45f, 0f, 4.9f), 2.0f, Vector3.left, new Vector2(1.0f, 0.75f),
                "ASSEMBLY\nPOINT", SignGreen, Arrow.None, 150, true, 8f);
            // ISO 7010 E007 pictogram on the same post, below the panel (bottom edge 1.62 m), both sides.
            if (SignPictogram("ISO_7010_E007") is Sprite e007)
                PictogramSign(p, "Sign - Assembly Point (E007)", e007, new Vector3(45f, 1.35f, 4.9f) + Vector3.left * 0.06f, Vector3.left, 0.5f,
                    SignGreen, 0.02f, doubleSided: true);
        }

        // ---------- Sign builders ----------

        /// <summary>
        /// Wall sign whose face lies at <paramref name="facePosition"/>, readable by someone standing in direction
        /// <paramref name="facing"/>; the housing extends behind the face towards the wall.
        /// </summary>
        static Transform Sign(Transform p, string name, string text, Vector3 facePosition, Vector3 facing, Vector2 size,
            Color background, Color foreground, int fontSize, Arrow arrow = Arrow.None, float housingDepth = 0.035f,
            float viewDistance = 4f, int sortingOrder = 0)
        {
            // Fire-equipment signs are portrait with a pictogram (the landscape text-only version wrapped "EXTINGUISH/ER").
            if (text == ExtinguisherLegend) return FireEquipmentSign(p, name, text, facePosition, facing, housingDepth, viewDistance);

            var root = new GameObject(name).transform;
            root.SetParent(p, false);
            root.localPosition = facePosition;
            root.localRotation = Quaternion.LookRotation(-facing);
            root.gameObject.isStatic = true;
            if (housingDepth > 0f)
                Box("Housing", root, new Vector3(0f, 0f, housingDepth / 2f), new Vector3(size.x + 0.03f, size.y + 0.03f, housingDepth), mSignHousing, false);
            SignFace(root, text, size, background, foreground, fontSize, arrow, viewDistance, sortingOrder);
            return root;
        }

        const string ExtinguisherLegend = "FIRE\nEXTINGUISHER";

        /// <summary>
        /// Maintained exit sign in the ISO 7010 E001/E002 style: a lightbox whose green face glows (HDR material, so it
        /// blooms on HDR quality tiers), with a white running figure entering a door, an arrow and the legend, all drawn
        /// from UI rects. The face width follows the legend. <paramref name="wallPoint"/> is on the wall (the box stands
        /// out from it); with a <paramref name="ceiling"/> the box hangs centred on it from two rods instead.
        /// <paramref name="legendSize"/> (px; 0: 0.31 x height) sets the legend, <paramref name="minWidth"/> (m) widens the
        /// box with the content centred, and a hanging box can carry a red <paramref name="backLegend"/> on its back.
        /// </summary>
        static Transform ExitSign(Transform p, string name, string legend, Vector3 wallPoint, Vector3 facing, Arrow arrow,
            float height = 0.2f, float viewDistance = 6f, float ceiling = 0f, int sortingOrder = 0, int legendSize = 0,
            float minWidth = 0f, string backLegend = null, int backSize = 0)
        {
            const float depth = 0.05f;
            bool hanging = ceiling > 0f;
            var root = new GameObject(name).transform;
            root.SetParent(p, false);
            root.localPosition = wallPoint;
            root.localRotation = Quaternion.LookRotation(-facing);
            root.gameObject.isStatic = true;

            float faceZ = hanging ? -depth / 2f : -depth;
            Canvas canvas = CreateCanvas(root, "Face", new Vector3(0f, 0f, faceZ - 0.002f), Quaternion.identity, new Vector2(1f, height), TextPixelsPerUnit(viewDistance));
            canvas.sortingOrder = sortingOrder;
            var face = (RectTransform)canvas.transform;
            float h = height * PixelsPerMeter, pad = 0.12f * h, gap = 0.07f * h, figure = 0.84f * h;
            float arrowSize = arrow != Arrow.None ? 0.62f * h : 0f;
            // The official ISO arrow + running-man artwork (Wikimedia, as a white mask on this green field) replaces the
            // drawn arrow and figure when present. It carries its own margin, so it runs to the box edge.
            Sprite pictogram = ExitPictogram(arrow);
            float pictogramWidth = pictogram != null ? h * pictogram.rect.width / pictogram.rect.height : 0f;
            Image field = AddImage(face, "Background", SignGreen);
            Text text = AddText(face, "Text", legend, legendSize > 0 ? legendSize : Mathf.RoundToInt(0.31f * h), Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero);
            float textWidth = Mathf.Ceil(text.preferredWidth) + 4f;
            float lead = pictogram != null ? 0f : pad;
            float symbols = pictogram != null ? pictogramWidth + gap : (arrowSize > 0f ? arrowSize + gap : 0f) + figure + gap;
            float w = lead + symbols + textWidth + pad;
            float margin = lead;
            if (w < minWidth * PixelsPerMeter)
            {
                margin += (minWidth * PixelsPerMeter - w) / 2f;
                w = minWidth * PixelsPerMeter;
            }
            face.sizeDelta = new Vector2(w, h);

            // Left to right: [arrow] [figure + door] [legend]; mirrored for a right arrow. The figure runs towards the arrow.
            bool towardsRight = arrow == Arrow.Right;
            float cursor = margin;
            float Slot(float width)
            {
                float c = cursor + width / 2f;
                cursor += width + gap;
                return towardsRight ? w - c : c;
            }
            if (pictogram != null)
                AddPictogram(face, pictogram, new Vector2(Slot(pictogramWidth) - w / 2f, 0f), new Vector2(pictogramWidth, h), Color.white);
            else
            {
                if (arrowSize > 0f) AddArrow(face, arrow, new Vector2(Slot(arrowSize) / w, 0.5f), arrowSize, Color.white);
                RunningFigure(face, new Vector2(Slot(figure) - w / 2f, 0f), figure, towardsRight ? 1f : -1f, Color.white);
            }
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(textWidth, h - pad);
            textRect.anchoredPosition = new Vector2(Slot(textWidth) - w / 2f, 0f);
            WarnIfOverflows(text, name);
            foreach (Graphic g in face.GetComponentsInChildren<Graphic>(true)) g.material = g == field ? mSignGlow : mSignLegend;

            // White polycarbonate body: the lit face covers its front.
            float wm = w / PixelsPerMeter;
            NoShadows(ChamferBox("Lightbox", root, new Vector3(0f, 0f, hanging ? 0f : -depth / 2f), new Vector3(wm + 0.02f, height + 0.02f, depth), mSignHousing, 0.005f));
            if (hanging && backLegend != null)
            {
                // Back face (printed, unlit), read by people walking the other way; SignFace checks its fit.
                Transform back = Group("Side B", root);
                back.localPosition = new Vector3(0f, 0f, depth / 2f);
                back.localRotation = Quaternion.Euler(0f, 180f, 0f);
                SignFace(back, backLegend, new Vector2(wm, height), SignRed, Color.white, backSize > 0 ? backSize : Mathf.RoundToInt(0.31f * h),
                    Arrow.None, viewDistance, sortingOrder);
            }
            if (hanging && ceiling - wallPoint.y > height / 2f)
                foreach (float x in new[] { -0.4f * wm, 0.4f * wm })
                    Rod("Hanger", root, new Vector3(x, height / 2f, 0f), new Vector3(x, ceiling - wallPoint.y, 0f), 0.006f, mAluminium);
            return root;
        }

        /// <summary>
        /// ISO 7010-style running figure entering a door frame, drawn from rotated bars and a round head, inside a
        /// <paramref name="size"/> square centred on <paramref name="center"/>. <paramref name="dir"/> +1 runs right, -1 left.
        /// </summary>
        static void RunningFigure(RectTransform parent, Vector2 center, float size, float dir, Color color)
        {
            var go = new GameObject("Running Figure", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = center;

            // Authored in units on a grid where the figure and door span x -32..52, y -32..46 (centre 10, 7).
            float u = size / 86f;
            Vector2 P(float x, float y) => new Vector2((x - 10f) * dir * u, (y - 7f) * u);
            // Door frame the figure runs into.
            Stroke(rt, P(24f, -32f), P(24f, 40f), 5f * u, color);
            Stroke(rt, P(50f, -32f), P(50f, 40f), 5f * u, color);
            Stroke(rt, P(24f, 40f), P(50f, 40f), 5f * u, color);
            // Torso leaning into the run, arms and legs in two segments each.
            Stroke(rt, P(5f, 23f), P(-6f, 0f), 12f * u, color);
            Stroke(rt, P(5f, 23f), P(17f, 14f), 7f * u, color);
            Stroke(rt, P(17f, 14f), P(26f, 22f), 7f * u, color);
            Stroke(rt, P(5f, 23f), P(-8f, 18f), 7f * u, color);
            Stroke(rt, P(-8f, 18f), P(-16f, 8f), 7f * u, color);
            Stroke(rt, P(-6f, 0f), P(12f, -8f), 9f * u, color);
            Stroke(rt, P(12f, -8f), P(8f, -28f), 9f * u, color);
            Stroke(rt, P(8f, -28f), P(16f, -28f), 7f * u, color);
            Stroke(rt, P(-6f, 0f), P(-12f, -16f), 9f * u, color);
            Stroke(rt, P(-12f, -16f), P(-28f, -22f), 9f * u, color);
            Dot(rt, P(13f, 38f), 16f * u, color);
        }

        /// <summary>
        /// Fire-equipment sign in the ISO 7010 F001 style: portrait red face with a white keyline, an extinguisher
        /// pictogram in the top 60 % and the legend on two lines below it.
        /// </summary>
        static Transform FireEquipmentSign(Transform p, string name, string legend, Vector3 facePosition, Vector3 facing, float housingDepth, float viewDistance)
        {
            var size = new Vector2(0.22f, 0.3f);
            var root = new GameObject(name).transform;
            root.SetParent(p, false);
            root.localPosition = facePosition;
            root.localRotation = Quaternion.LookRotation(-facing);
            root.gameObject.isStatic = true;
            if (housingDepth > 0f)
                Box("Housing", root, new Vector3(0f, 0f, housingDepth / 2f), new Vector3(size.x + 0.01f, size.y + 0.01f, housingDepth), mSignHousing, false);

            Canvas canvas = CreateCanvas(root, "Face", new Vector3(0f, 0f, -0.002f), Quaternion.identity, size, TextPixelsPerUnit(viewDistance));
            var face = (RectTransform)canvas.transform;
            AddImage(face, "Background", SignRed);
            float w = size.x * PixelsPerMeter, h = size.y * PixelsPerMeter;
            const float inset = 9f, keyline = 3f;
            Bar(face, new Vector2(0f, h / 2f - inset), new Vector2(w - 2f * inset + keyline, keyline), 0f, Color.white);
            Bar(face, new Vector2(0f, -h / 2f + inset), new Vector2(w - 2f * inset + keyline, keyline), 0f, Color.white);
            Bar(face, new Vector2(-w / 2f + inset, 0f), new Vector2(keyline, h - 2f * inset), 0f, Color.white);
            Bar(face, new Vector2(w / 2f - inset, 0f), new Vector2(keyline, h - 2f * inset), 0f, Color.white);

            // ISO 7010 F001 artwork (extinguisher and flames) when present, in the square above the legend.
            if (SignPictogram("ISO_7010_F001") is Sprite f001)
            {
                float side = w - 2f * inset - 2f * keyline;
                AddPictogram(face, f001, new Vector2(0f, h / 2f - inset - keyline - side / 2f), new Vector2(side, side), Color.white);
                WarnIfOverflows(AddText(face, "Text", legend, 24, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1f, 0.38f), new Vector2(14f, 12f)), name);
                return root;
            }
            // Extinguisher, authored in px on a 1:1 grid, scaled 0.9 and lowered 16 px into the space above the legend.
            Vector2 P(float x, float y) => new Vector2(x * 0.9f, y * 0.9f - 16f);
            float s = 0.9f;
            Bar(face, P(-12f, 35f), new Vector2(56f, 104f) * s, 0f, Color.white);                 // cylinder
            Dot(face, P(-12f, 87f), 56f * s, Color.white);                                       // shoulder
            Bar(face, P(-12f, 40f), new Vector2(34f, 30f) * s, 0f, SignRed);                    // label panel
            Bar(face, P(-12f, 118f), new Vector2(18f, 18f) * s, 0f, Color.white);                 // neck
            Bar(face, P(-6f, 130f), new Vector2(36f, 12f) * s, 0f, Color.white);                  // valve head
            Stroke(face, P(0f, 138f), P(30f, 146f), 8f * s, Color.white);                         // lever
            Stroke(face, P(12f, 128f), P(32f, 108f), 8f * s, Color.white);                        // hose
            Stroke(face, P(32f, 108f), P(36f, 30f), 8f * s, Color.white);
            Stroke(face, P(36f, 30f), P(36f, 14f), 12f * s, Color.white);                         // nozzle

            Text text = AddText(face, "Text", legend, 24, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1f, 0.38f), new Vector2(14f, 12f));
            WarnIfOverflows(text, name);
            return root;
        }

        /// <summary>
        /// Temporary incident notice on a wall or door: a laminated yellow sheet with a black diagonal hazard border,
        /// black legend (and optional smaller <paramref name="detail"/> line), taped at the top corners.
        /// </summary>
        static Transform TempNotice(Transform p, string name, string text, string detail, Vector3 wallPoint, Vector3 facing, Vector2 size,
            int fontSize, int detailSize, Arrow arrow = Arrow.None, float viewDistance = 3f, int sortingOrder = 0)
        {
            var root = new GameObject(name).transform;
            root.SetParent(p, false);
            root.localPosition = wallPoint;
            root.localRotation = Quaternion.LookRotation(-facing);
            root.gameObject.isStatic = true;
            NoticeFace(root, name, text, detail, size, fontSize, detailSize, arrow, viewDistance, sortingOrder);
            return root;
        }

        /// <summary>Warden's free-standing A-frame (two hinged boards, no collider) carrying a notice on its front board.</summary>
        static void AFrameNotice(Transform p, string name, string text, string detail, Vector3 floor, Vector3 facing, Vector2 size,
            int fontSize, int detailSize, float viewDistance, int sortingOrder)
        {
            Transform root = Group(name, p);
            root.localPosition = floor;
            root.localRotation = Quaternion.LookRotation(-facing);
            const float length = 0.95f, tilt = 13f, thickness = 0.02f;
            float width = size.x + 0.08f, top = length * Mathf.Cos(tilt * Mathf.Deg2Rad);
            foreach (float side in new[] { 1f, -1f })   // +1: front board, its foot towards the reader (local -Z)
            {
                Quaternion r = Quaternion.Euler(side * tilt, 0f, 0f);
                Vector3 center = new Vector3(0f, top, 0f) + r * new Vector3(0f, -length / 2f, 0f);
                ChamferBox("Board", root, center, new Vector3(width, length, thickness), mDark, 0.004f, localRotation: r);
                if (side > 0f)
                {
                    Transform sheet = Group("Notice", root);
                    sheet.localPosition = center + r * new Vector3(0f, 0.12f, -thickness / 2f - 0.001f);
                    sheet.localRotation = r;
                    NoticeFace(sheet, name, text, detail, size, fontSize, detailSize, Arrow.None, viewDistance, sortingOrder);
                }
            }
            ChamferBox("Hinge Cap", root, new Vector3(0f, top + 0.01f, 0f), new Vector3(width, 0.03f, 0.05f), mDark, 0.006f);
        }

        /// <summary>Notice sheet on the -Z side of <paramref name="root"/> (its plane at z 0, backing behind it).</summary>
        static void NoticeFace(Transform root, string owner, string text, string detail, Vector2 size, int fontSize, int detailSize,
            Arrow arrow, float viewDistance, int sortingOrder)
        {
            var ink = new Color(0.06f, 0.06f, 0.06f);
            NoShadows(Box("Laminate", root, new Vector3(0f, 0f, 0.001f), new Vector3(size.x, size.y, 0.002f), mPaper, false));
            foreach (float side in new[] { -1f, 1f })
                NoShadows(Box("Tape", root, new Vector3(side * (size.x / 2f - 0.015f), size.y / 2f - 0.012f, -0.0012f), new Vector3(0.07f, 0.024f, 0.0004f),
                    mPaper, false, localRotation: Quaternion.Euler(0f, 0f, side * -38f)));

            Canvas canvas = CreateCanvas(root, "Face", new Vector3(0f, 0f, -0.0006f), Quaternion.identity, size, TextPixelsPerUnit(viewDistance));
            canvas.sortingOrder = sortingOrder;
            var face = (RectTransform)canvas.transform;
            Vector2 px = size * PixelsPerMeter;
            float border = Mathf.Min(0.04f, 0.15f * size.y) * PixelsPerMeter;

            // Hazard border: 45-degree black stripes clipped to the sheet, then the plain yellow field over the middle.
            Image hazard = AddImage(face, "Hazard Border", SignYellow);
            hazard.gameObject.AddComponent<RectMask2D>();
            float stripe = border * 0.7f, step = stripe * 2f * 1.41421f, length = (px.y + stripe) * 1.41421f;
            for (float x = -(px.x + px.y) / 2f; x <= (px.x + px.y) / 2f; x += step)
                Bar(hazard.rectTransform, new Vector2(x, 0f), new Vector2(stripe, length), -45f, ink);
            AddImage(face, "Field", SignYellow, Vector2.zero, Vector2.one, new Vector2(border, border));

            float inset = border * 1.3f;
            float detailHeight = detail != null ? Mathf.Ceil(detailSize * 1.3f) : 0f;
            float textRight = px.x - inset;
            if (arrow != Arrow.None)
            {
                float arrowPx = Mathf.Min((px.y - 2f * inset) * 0.8f, 300f);
                AddArrow(face, arrow, new Vector2((px.x - inset - arrowPx / 2f) / px.x, 0.5f), arrowPx, ink);
                textRight -= arrowPx + inset * 0.5f;
            }
            Text headline = AddText(face, "Text", text, fontSize, ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero);
            headline.rectTransform.offsetMin = new Vector2(inset, inset + detailHeight);
            headline.rectTransform.offsetMax = new Vector2(textRight - px.x, -inset);
            WarnIfOverflows(headline, owner);
            if (detail != null)
            {
                Text small = AddText(face, "Detail", detail, detailSize, ink, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1f, 0f), Vector2.zero);
                small.rectTransform.offsetMin = new Vector2(inset, inset);
                small.rectTransform.offsetMax = new Vector2(-inset, inset + detailHeight);
                WarnIfOverflows(small, owner);
            }
        }

        static void HangingSign(Transform p, string name, Vector3 center, Vector3 facingA, Vector2 size, string textA, Color backgroundA, Arrow arrowA,
            string textB, Color backgroundB, Arrow arrowB, int fontSize, float ceiling, float viewDistance = 4f, int sortingOrder = 0)
        {
            var root = new GameObject(name).transform;
            root.SetParent(p, false);
            root.localPosition = center;
            root.localRotation = Quaternion.LookRotation(-facingA);
            root.gameObject.isStatic = true;
            const float depth = 0.05f;
            Box("Housing", root, Vector3.zero, new Vector3(size.x + 0.03f, size.y + 0.03f, depth), mSignHousing, false);
            Transform a = Group("Side A", root);
            a.localPosition = new Vector3(0f, 0f, -depth / 2f);
            SignFace(a, textA, size, backgroundA, Color.white, fontSize, arrowA, viewDistance, sortingOrder);
            if (textB != null)
            {
                Transform b = Group("Side B", root);
                b.localPosition = new Vector3(0f, 0f, depth / 2f);
                b.localRotation = Quaternion.Euler(0f, 180f, 0f);
                SignFace(b, textB, size, backgroundB, Color.white, fontSize, arrowB, viewDistance, sortingOrder);
            }
            if (ceiling - center.y > size.y / 2f)
                foreach (float x in new[] { -0.4f * size.x, 0.4f * size.x })
                    Rod("Hanger", root, new Vector3(x, size.y / 2f, 0f), new Vector3(x, ceiling - center.y, 0f), 0.006f, mAluminium);
        }

        static void PostSign(Transform p, string name, Vector3 floor, float centerHeight, Vector3 facing, Vector2 size, string text,
            Color background, Arrow arrow, int fontSize, bool doubleSided, float viewDistance = 4f)
        {
            Transform root = Group(name, p);
            root.localPosition = floor;
            Rod("Post", root, Vector3.zero, Vector3.up * (centerHeight + size.y / 2f), 0.04f, mDark);
            ColliderBox("Post Collider", root, Vector3.up * centerHeight / 2f, new Vector3(0.08f, centerHeight, 0.08f));
            HangingSign(root, "Panel", Vector3.up * centerHeight + facing * 0.06f, facing, size, text, background, arrow,
                doubleSided ? text : null, background, arrow, fontSize, centerHeight, viewDistance);
        }

        /// <summary>Painted floor graphic: text plus an arrow pointing along <paramref name="direction"/>.</summary>
        static void FloorMarking(Transform p, string name, string text, Vector3 position, Vector3 direction, Vector2 size, Color color, int fontSize,
            bool arrow = true, float viewDistance = 2.5f)
        {
            Canvas canvas = CreateCanvas(p, name, position, Quaternion.LookRotation(Vector3.down, direction), size, TextPixelsPerUnit(viewDistance));
            if (arrow) AddArrow(canvas.transform, Arrow.Up, new Vector2(0.84f, 0.5f), size.y * PixelsPerMeter * 0.85f, color);
            WarnIfOverflows(AddText(canvas.transform, "Text", text, fontSize, color, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(arrow ? 0.68f : 1f, 1f), Vector2.zero), name);
        }

        /// <summary>Face of a sign on the -Z side of <paramref name="root"/>.</summary>
        static void SignFace(Transform root, string text, Vector2 size, Color background, Color foreground, int fontSize, Arrow arrow,
            float viewDistance = 4f, int sortingOrder = 0)
        {
            Canvas canvas = CreateCanvas(root, "Face", new Vector3(0f, 0f, -0.002f), Quaternion.identity, size, TextPixelsPerUnit(viewDistance));
            canvas.sortingOrder = sortingOrder;
            AddImage(canvas.transform, "Background", background);
            float heightPx = size.y * PixelsPerMeter;
            Vector2 textMin = Vector2.zero, textMax = Vector2.one;
            if (arrow != Arrow.None)
            {
                float arrowPx = Mathf.Min(heightPx * 0.72f, 300f);
                float fraction = (arrowPx + heightPx * 0.2f) / (size.x * PixelsPerMeter);
                bool right = arrow == Arrow.Right;
                AddArrow(canvas.transform, arrow, new Vector2(right ? 1f - fraction / 2f : fraction / 2f, 0.5f), arrowPx, foreground);
                if (right) textMax = new Vector2(1f - fraction, 1f);
                else textMin = new Vector2(fraction, 0f);
            }
            WarnIfOverflows(AddText(canvas.transform, "Text", text, fontSize, foreground, TextAnchor.MiddleCenter, textMin, textMax,
                new Vector2(heightPx * 0.08f, heightPx * 0.06f)), root.parent != null ? $"{root.parent.name}/{root.name}" : root.name);
        }

        // ---------- World-space UI ----------

        /// <summary>
        /// Canvas dynamicPixelsPerUnit for text read from about <paramref name="viewDistance"/> metres. Legacy font atlases
        /// have no mipmaps, so glyphs are rasterised at about 1.5x their on-screen size there: at 1080p and 60 degrees VFOV
        /// (~935 px per radian) an em of F canvas px (F mm) is 0.935 F / d px on screen and F * dppu px in the atlas.
        /// Much larger rasters minify and shimmer in VR; much smaller ones blur when walking up close.
        /// </summary>
        static float TextPixelsPerUnit(float viewDistance) => Mathf.Clamp(1.4f / Mathf.Max(viewDistance, 0.1f), 0.25f, 1.5f);

        static Canvas CreateCanvas(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector2 sizeMeters, float dynamicPixelsPerUnit)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = dynamicPixelsPerUnit;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMeters * PixelsPerMeter;
            rt.localScale = Vector3.one / PixelsPerMeter;
            rt.localPosition = localPosition;
            rt.localRotation = localRotation;
            return canvas;
        }

        static Image AddImage(Transform parent, string name, Color color) => AddImage(parent, name, color, Vector2.zero, Vector2.one, Vector2.zero);

        static Image AddImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 padding)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, anchorMin, anchorMax, padding);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Text at a fixed size (no best fit, so sizes are deterministic); <see cref="WarnIfOverflows"/> checks the fit.
        /// Bold by default (titles, sign legends, labels); running text (panel body, subtitles, footer) passes Normal, so
        /// weight carries the hierarchy (Arial has no medium weight and legacy Text no letter-spacing).
        /// </summary>
        static Text AddText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 padding,
            FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, anchorMin, anchorMax, padding);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Generation-time fit check (instead of silently shrinking). Measures <paramref name="sample"/> (the longest
        /// expected runtime string) or the current text. Without <paramref name="allowWrap"/> every authored line must fit
        /// on one line; the wrapped block must always fit the rect height.
        /// </summary>
        static bool WarnIfOverflows(Text text, string owner, string sample = null, bool allowWrap = false)
        {
            string original = text.text;
            if (sample != null) text.text = sample;
            Rect rect = text.rectTransform.rect;
            float width = text.preferredWidth, height = text.preferredHeight;
            string measured = text.text;
            text.text = original;
            bool fits = height <= rect.height + 0.5f && (allowWrap || width <= rect.width + 0.5f);
            if (!fits)
                Debug.LogWarning($"[SafeRoute] Text does not fit on '{owner}/{text.name}': needs {width:0} x {height:0} px " +
                    $"(wrapped), has {rect.width:0} x {rect.height:0}. Shorten it or enlarge the panel: \"{measured.Replace("\n", " / ")}\"");
            return fits;
        }

        /// <summary>Arrow drawn from UI rectangles (no dependency on font glyphs).</summary>
        static void AddArrow(Transform parent, Arrow arrow, Vector2 anchor, float size, Color color)
        {
            var go = new GameObject("Arrow", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.Euler(0f, 0f, arrow == Arrow.Up ? 90f : arrow == Arrow.Left ? 180f : 0f);

            float stroke = size * 0.17f, head = size * 0.5f;
            Vector2 tip = new Vector2(size * 0.38f, 0f);
            Bar(rt, new Vector2(-size * 0.08f, 0f), new Vector2(size * 0.74f, stroke), 0f, color);
            Bar(rt, tip + new Vector2(-0.7071f, 0.7071f) * (head / 2f - stroke * 0.35f), new Vector2(head, stroke), -45f, color);
            Bar(rt, tip + new Vector2(-0.7071f, -0.7071f) * (head / 2f - stroke * 0.35f), new Vector2(head, stroke), 45f, color);
        }

        static void Bar(RectTransform parent, Vector2 position, Vector2 size, float angle, Color color)
        {
            var go = new GameObject("Bar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>Bar from <paramref name="a"/> to <paramref name="b"/> with square caps (half the width past each end, so joints close).</summary>
        static void Stroke(RectTransform parent, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a;
            Bar(parent, (a + b) / 2f, new Vector2(d.magnitude + width, width), Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, color);
        }

        static Sprite circle;

        /// <summary>Filled circle (the editor's built-in UI knob sprite; no imported asset).</summary>
        static void Dot(RectTransform parent, Vector2 position, float diameter, Color color)
        {
            if (circle == null) circle = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var go = new GameObject("Dot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(diameter, diameter);
            rt.anchoredPosition = position;
            var image = go.AddComponent<Image>();
            image.sprite = circle;
            image.color = color;
            image.raycastTarget = false;
        }

        static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 padding)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = padding;
            rt.offsetMax = -padding;
        }
    }
}
