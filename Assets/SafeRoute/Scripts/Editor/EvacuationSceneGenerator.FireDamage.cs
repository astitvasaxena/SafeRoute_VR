using UnityEngine;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        // Hot-layer soot band on the corridor walls (texture SootBand spans x 0.15..6.5 m, y 2.05 m..ceiling).
        const float SootX0 = 0.15f, SootX1 = 6.5f, SootY0 = 2.05f;

        /// <summary>
        /// Static fire damage round the burning end of the corridor, heaviest round Exit A and lighter where the fire is
        /// younger: the smoke-layer line on the walls and ceiling, V plumes above each fuel source, heat-stained and
        /// blistered paint, floor scorch and soot on the Exit A leaves. All unlit multiply overlays (the fire light cannot
        /// wash them out) plus two alpha-tested blister patches; no colliders, no shadows. Kept clear of the safety signs.
        /// </summary>
        static void BuildFireDamage(Transform p)
        {
            Transform damage = Group("Fire Damage", p);
            float top = CeilingHeight;

            // Hot layer: south wall (z 0.1) broken over the lift-alcove opening (head 2.4 m), north wall (z 2.9) in one piece.
            SootBand(damage, 0.103f, Vector3.forward, SootX0, 4f, SootY0);
            SootBand(damage, 0.103f, Vector3.forward, 4f, SootX1, 2.4f);
            SootBand(damage, 2.897f, Vector3.back, SootX0, SootX1, SootY0);
            // West wall (x 0.15): full strength, around and above the Exit A architrave (z 0.53..2.47, top 2.27 m).
            foreach ((float z0, float z1, float y0) in new[] { (0.1f, 0.53f, SootY0), (0.53f, 2.47f, 2.27f), (2.47f, 2.9f, SootY0) })
            {
                Rect uv = new Rect((z0 - 0.1f) / 2.8f * 0.29f, (y0 - SootY0) / (top - SootY0), (z1 - z0) / 2.8f * 0.29f, (top - y0) / (top - SootY0));
                Decal("Soot Layer", damage, new Vector3(0.153f, (y0 + top) / 2f, (z0 + z1) / 2f), Vector3.right, Vector3.up,
                    new Vector2(z1 - z0, top - y0), mSootBand, uv, Vector3.forward);
            }
            // Ceiling over the whole fire end, fading east, darker round the gap where the tiles fell.
            Decal("Soot Ceiling", damage, new Vector3((SootX0 + SootX1) / 2f, top - 0.0025f, 1.5f), Vector3.down, Vector3.forward,
                new Vector2(SootX1 - SootX0, 2.8f), mSootCeiling, null, Vector3.right);

            // Plumes. Above Exit A: the wide upper part of the V, from the architrave head to the ceiling (behind the sign).
            Decal("Soot Plume", damage, new Vector3(0.152f, (2.27f + top) / 2f, 1.5f), Vector3.right, Vector3.up,
                new Vector2(2.6f, top - 2.27f), mSootPlumeLight, new Rect(0f, 0.55f, 1f, 0.45f));
            // On the Exit A leaves (corridor face x 0.0225): light soot on the upper half only (1.11..2.12 m, the top half
            // of the plume), so the lower leaves keep their red paint and the fake fire bounce of DoorBlockedScorched.
            Decal("Soot on Exit A Doors", damage, new Vector3(0.0275f, 1.615f, 1.5f), Vector3.right, Vector3.up,
                new Vector2(1.5f, 1.01f), mSootDoor, new Rect(0f, 0.5f, 1f, 0.5f));
            // South wall above the stored boxes (x 2.2..3.6, from 0.6 m): on the dado face, the buffer rail and the wall.
            PlumeOnCorridorWall(damage, 2.9f, 1.4f, 0.6f, 0.1f, mSootPlume);
            // North wall above the notice board (younger fire): from the board up.
            Decal("Soot Plume", damage, new Vector3(3.8f, (1.2f + top) / 2f, 2.896f), Vector3.back, Vector3.up,
                new Vector2(1.4f, top - 1.2f), mSootPlumeLight, new Rect(0f, 0.2f, 1f, 0.8f));
            // Thin streak above the lift head (smoke leaking from the shaft), clear of the floor indicator bezel.
            Decal("Soot Streak", damage, new Vector3(5.5f, (2.22f + top) / 2f, -1.896f), Vector3.forward, Vector3.up,
                new Vector2(0.5f, top - 2.22f), mSootPlumeLight, new Rect(0f, 0.35f, 1f, 0.65f));

            // Heat-stained and blistered paint behind the two wall fires (blisters 1.5 mm off the wall, stains 4 mm).
            Decal("Heat Stain", damage, new Vector3(3.8f, 1.62f, 2.896f), Vector3.back, Vector3.up, new Vector2(2f, 1f), mHeatStain);
            Decal("Blistered Paint", damage, new Vector3(3.8f, 1.95f, 2.8985f), Vector3.back, Vector3.up, new Vector2(1.5f, 0.55f), mBlister);
            Decal("Heat Stain", damage, new Vector3(2.9f, 1.6f, 0.104f), Vector3.forward, Vector3.up, new Vector2(1.8f, 0.95f), mHeatStain);
            Decal("Blistered Paint", damage, new Vector3(2.9f, 1.65f, 0.1015f), Vector3.forward, Vector3.up, new Vector2(1.2f, 0.8f), mBlister);

            // Floor scorch under the Exit A debris (heaviest), the pallet and the fallen ceiling tiles.
            Decal("Floor Scorch", damage, new Vector3(0.85f, 0.003f, 1.5f), Vector3.up, Vector3.forward, new Vector2(1.6f, 1.8f), mScorch);
            Decal("Floor Scorch", damage, new Vector3(2.9f, 0.003f, 0.5f), Vector3.up, Vector3.forward, new Vector2(1.3f, 0.9f), mScorchLight);
            Decal("Floor Scorch", damage, new Vector3(5.5f, 0.003f, 2.05f), Vector3.up, Vector3.forward, new Vector2(1f, 1f), mScorchLight);
        }

        /// <summary>One run of the soot band on a corridor wall along X (face at z = <paramref name="z"/>), from <paramref name="y0"/> to the ceiling.</summary>
        static void SootBand(Transform p, float z, Vector3 normal, float x0, float x1, float y0)
        {
            float top = CeilingHeight, span = SootX1 - SootX0, height = top - SootY0;
            var uv = new Rect((x0 - SootX0) / span, (y0 - SootY0) / height, (x1 - x0) / span, (top - y0) / height);
            Decal("Soot Layer", p, WallPoint(true, z, (x0 + x1) / 2f, (y0 + top) / 2f), normal, Vector3.up, new Vector2(x1 - x0, top - y0),
                mSootBand, uv, Vector3.right);
        }

        /// <summary>
        /// Plume on the south corridor wall (face z = <paramref name="wallFace"/>) from <paramref name="y0"/> to the ceiling,
        /// split over the dado (12 mm proud, to 1.05 m), the buffer rail (35 mm, 1.05..1.1 m) and the plain wall above.
        /// </summary>
        static void PlumeOnCorridorWall(Transform p, float x, float width, float y0, float wallFace, Material material)
        {
            float top = CeilingHeight, height = top - y0;
            foreach ((float b0, float b1, float proud) in new[] { (y0, 1.05f, 0.015f), (1.05f, 1.1f, 0.038f), (1.1f, top, 0.003f) })
            {
                if (b1 <= y0) continue;
                float a0 = Mathf.Max(b0, y0);
                var uv = new Rect(0f, (a0 - y0) / height, 1f, (b1 - a0) / height);
                Decal("Soot Plume", p, new Vector3(x, (a0 + b1) / 2f, wallFace + proud), Vector3.forward, Vector3.up, new Vector2(width, b1 - a0), material, uv);
            }
        }
    }
}
