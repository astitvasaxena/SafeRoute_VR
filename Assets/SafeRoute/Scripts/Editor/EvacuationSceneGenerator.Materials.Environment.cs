using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        // Trim and finishes that replace the near-black outline trim.
        static Material mSkirting, mBufferRail;
        // Unlit multiply-blend overlays: fake contact shadow and fire damage that the fire light cannot wash out.
        static Material mContactAO, mContactPool, mSootBand, mSootPlume, mSootPlumeLight, mSootDoor, mSootCeiling, mScorch, mScorchLight, mHeatStain;
        // Lit fire damage.
        static Material mBlister, mDoorBlockedScorched;
        // Neighbouring buildings.
        static Material mFacadeLarge, mContextGlazing;

        /// <summary>Wear, grime, contact shading and fire-damage materials for the building. Runs at the end of CreateMaterials.</summary>
        static void CreateEnvironmentMaterials()
        {
            // Maps owned by the main pass, reused by name so nothing is regenerated twice.
            var micro = (Texture2D)mTrim.GetTexture("_BaseMap");
            var microN = (Texture2D)mTrim.GetTexture("_BumpMap");
            var facade = (Texture2D)mFacade.GetTexture("_BaseMap");
            var facadeN = (Texture2D)mFacade.GetTexture("_BumpMap");

            // ---- Detail maps (URP Lit _DETAIL_MULX2: albedo x 2 x detail, so linear 0.5 is neutral) ----
            // They ride on the world-anchored box UVs: 12 m along a wall or floor, 6 m of height on walls.

            // Walls and dado: dirt above the skirting, scuffs, hand marks at push height, a soft shadow under the ceiling.
            Texture2D wallGrime = DetailTex("WallGrime_D", 512, 1024, (u, v) =>
            {
                float y = v * WallDetailHeight;
                float f = 1f;
                f *= 1f - 0.16f * (1f - Smooth(0.1f, 0.5f, y)) * (0.55f + 0.9f * Noise(u, 0.5f, 48, 1, 401));
                float scuff = Smooth(0.8f, 0.9f, Noise(u, v, 96, 512, 403)) * Smooth(0.1f, 0.14f, y) * (1f - Smooth(0.46f, 0.52f, y));
                f *= 1f - 0.14f * scuff;
                float hands = Smooth(0.78f, 0.86f, Noise(u, 0.5f, 40, 1, 407)) * Smooth(0.92f, 1f, y) * (1f - Smooth(1.3f, 1.4f, y));
                f *= 1f - 0.07f * hands * Fbm(u, v, 48, 2, 409);
                f *= 1f - 0.12f * Smooth(2.7f, 2.98f, y) * (1f - Smooth(2.98f, 3.25f, y));   // fake AO under 2.98 m ceilings
                f *= 1f + (Fbm(u, v, 4, 3, 411) - 0.5f) * 0.08f;                            // roller and touch-up patches
                f *= 1f + (Noise(u, v, 256, 128, 413) - 0.5f) * 0.02f;
                return DetailColor(f, 0.15f);
            });
            // Faint plasterboard joints every 1.2 m and a low waviness so the ceiling lights rake across something.
            Texture2D wallGrimeN = NormalTex("WallGrime_DN", 1024, 1f, (u, v) =>
            {
                float x = u * 12f;
                float joint = Mathf.Min(Frac(x / 1.2f), 1f - Frac(x / 1.2f)) * 1.2f;
                return 0.15f * (1f - Smooth(0f, 0.03f, joint)) + Fbm(u, v, 24, 3, 415) * 0.35f;
            });

            // Corridor vinyl: dirt along the skirting lines, a duller scratched traffic lane down the middle.
            Texture2D floorWear = DetailTex("FloorWear_D", 1024, 1024, (u, v) =>
            {
                float z = v * 12f;   // floor UVs: u = x, v = z (wrapped to 12 m; the lift alcove's z -2..0 lands at 10..12)
                float f = 1f + (Fbm(u, v, 6, 3, 421) - 0.5f) * 0.06f;
                foreach ((float line, float dir) in new[] { (0.1f, 1f), (2.9f, -1f), (3.1f, 1f), (7.85f, -1f), (10.1f, 1f) })
                {
                    float d = (z - line) * dir;   // distance into the room from the wall face
                    if (d > -0.02f && d < 0.2f)
                        f *= 1f - 0.12f * (1f - Smooth(0f, 0.12f, d)) * (0.6f + 0.8f * Noise(u, 0.5f, 96, 1, 423));
                }
                float lane = Smooth(0.7f, 0.4f, Mathf.Abs(z - 1.5f)) * Smooth(0.1f, 0.4f, z) * (1f - Smooth(2.6f, 2.9f, z));
                float scratch = Noise(u, v, 512, 48, 427);
                f *= 1f - lane * (0.035f + 0.03f * Smooth(0.6f, 0.9f, scratch));
                return DetailColor(f, 0f);
            });

            // Ceiling: per-tile tint (0.6 m cells on the world grid), a few yellowed tiles, two old water stains.
            Texture2D ceilingVar = DetailTex("CeilingVar_D", 1024, 1024, (u, v) =>
            {
                int ti = (int)(u * 20f), tj = (int)(v * 20f);
                float f = 1f + (Hash(ti, tj, 431) - 0.5f) * 0.06f;
                Color c = new Color(f, f, f);
                if (Hash(ti, tj, 433) < 0.125f) c *= new Color(0.99f, 0.96f, 0.9f);
                // Stains in metres of the 12 m period (u = x, v = -z): corridor at x 8.4 / z 0.7, classroom at x 4.0 / z 7.2.
                foreach ((float cx, float cy, float r) in new[] { (8.4f, 11.3f, 0.26f), (4f, 4.8f, 0.34f) })
                {
                    float dx = u * 12f - cx, dy = v * 12f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r * (0.85f + 0.3f * Fbm(u, v, 48, 3, 437));
                    float ring = Smooth(0.75f, 0.95f, d) * (1f - Smooth(0.95f, 1.05f, d));
                    float inner = 1f - Smooth(0.6f, 1f, d);
                    c *= Color.Lerp(Color.white, new Color(0.93f, 0.87f, 0.78f), ring * 0.8f);
                    c *= Color.Lerp(Color.white, new Color(0.98f, 0.96f, 0.92f), inner);
                }
                return new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 1f);
            });

            // Facade: splash-back over the plinth and rain streaks below the parapet coping (main building cladding only).
            Texture2D facadeGrime = DetailTex("FacadeGrime_D", 512, 1024, (u, v) =>
            {
                float y = v * WallDetailHeight;
                float f = 1f - 0.12f * (1f - Smooth(0.45f, 1f, y)) * (0.6f + 0.8f * Noise(u, 0.5f, 64, 1, 441));
                float streak = Smooth(0.55f, 0.85f, Noise(u, 0.5f, 160, 1, 443)) * Smooth(2.6f, 3.4f, y) * (1f - Smooth(3.55f, 3.62f, y));
                f *= 1f - 0.16f * streak * (0.7f + 0.3f * Noise(u, v, 160, 64, 445));
                f *= 1f + (Fbm(u, v, 6, 3, 447) - 0.5f) * 0.06f;
                return DetailColor(f, 0.2f);
            });

            // ---- Floor vinyl v3: quieter joints, per-tile tint and directional chip marble laid quarter-turned ----
            Texture2D vinyl3 = Tex("Vinyl_v3", 512, TexKind.Color, (u, v) =>
            {
                int ti = (int)(u * 2f), tj = (int)(v * 2f);
                float lu = Frac(u * 2f), lv = Frac(v * 2f);
                bool turn = ((ti + tj) & 1) == 1;
                float a = turn ? lv : lu, b = turn ? lu : lv;
                int seed = 451 + ti * 7 + tj * 13;
                float chips = Noise(a, b, 6, 24, seed) * 0.7f + Noise(a, b, 12, 48, seed + 1) * 0.3f;
                float g = 0.9f + (Hash(ti, tj, 7) - 0.5f) * 0.08f + (Fbm(u, v, 4, 3, 11) - 0.5f) * 0.04f
                          + (Smooth(0.5f, 0.75f, chips) - 0.3f) * 0.06f;
                float fleck = Noise(u, v, 180, 180, 13);
                if (fleck > 0.82f) g -= 0.14f * (fleck - 0.82f) / 0.18f;
                float grid = GridHeight(u, v, 2, 2, 0.6f, 0.6f, 0.0025f);
                float wear = 0.68f + 0.27f * Fbm(u, v, 3, 4, 23) + (Hash(ti, tj, 29) - 0.5f) * 0.1f;
                return GrayA(Mathf.Lerp(0.8f, g, grid), Mathf.Lerp(0.4f, wear, grid));
            });
            Texture2D vinyl3N = NormalTex("Vinyl_v3_N", 512, 1f, (u, v) => GridHeight(u, v, 2, 2, 0.6f, 0.6f, 0.0025f) + Noise(u, v, 200, 200, 19) * 0.02f);

            mFloor = Lit("Floor", new Color(0.8f, 0.79f, 0.76f), vinyl3, vinyl3N, 1.2f, 0.5f, smoothnessInAlpha: true);
            mLabFloor = Lit("LabFloor", new Color(0.76f, 0.79f, 0.78f), vinyl3, vinyl3N, 1.2f, 0.55f, smoothnessInAlpha: true);
            Detail(mFloor, 1.2f, floorWear, null);
            Detail(mLabFloor, 1.2f, floorWear, null);
            Detail(mWall, 2f, wallGrime, wallGrimeN, WallDetailHeight);
            Detail(mDado, 2f, wallGrime, wallGrimeN, WallDetailHeight);
            Detail(mCeiling, 1.2f, ceilingVar, null);
            Detail(mFacade, 3f, facadeGrime, null, WallDetailHeight);

            // Sit-on coved vinyl skirting in a darker floor tone and a pale PVC buffer rail instead of black outline trim.
            mSkirting = Lit("Skirting", new Color(0.62f, 0.6f, 0.57f), micro, microN, 0.5f, 0.44f, normalScale: 0.35f, smoothnessInAlpha: true);
            mBufferRail = Lit("BufferRail", new Color(0.55f, 0.56f, 0.56f), micro, microN, 0.5f, 0.55f, normalScale: 0.35f, smoothnessInAlpha: true);
            // Painted steel frames: still darker than the walls, no longer an outline.
            mFrame = Lit("DoorFrame", new Color(0.5f, 0.52f, 0.53f), micro, microN, 0.5f, 0.42f, normalScale: 0.35f, smoothnessInAlpha: true);

            // ---- Multiply overlays: RGB is the stain colour, alpha its strength (lerp to white) ----
            Texture2D contactAO = OverlayTex("ContactAO", 64, 256, (u, v) =>
                new Color(0.3f, 0.29f, 0.28f, (1f - v) * (1f - v)));
            Texture2D contactPool = OverlayTex("ContactPool", 128, 128, (u, v) =>
                new Color(0.3f, 0.29f, 0.28f, (1f - v) * (1f - v) * Smooth(0f, 0.2f, u) * Smooth(0f, 0.2f, 1f - u)));

            // Hot-layer soot band on the corridor walls: u = x 0.15..6.5 m, v = y 2.05..2.98 m. Heaviest at Exit A,
            // fading east; a ragged lower edge with a few drips; no hard end.
            Texture2D sootBand = OverlayTex("SootBand", 1024, 256, (u, v) =>
            {
                float x = 0.15f + u * 6.35f, y = 2.05f + v * 0.93f;
                float edge = 2.38f + (Fbm(u, 0.5f, 12, 4, 461) - 0.5f) * 0.16f + (Noise(u, 0.5f, 96, 1, 463) - 0.5f) * 0.04f;
                float a = Smooth(edge - 0.012f, edge + 0.012f, y) * (0.55f + 0.42f * Smooth(edge, 2.98f, y));
                a *= 0.85f + 0.3f * (Fbm(u, v, 24, 3, 465) - 0.5f);
                float drip = Smooth(0.84f, 0.94f, Noise(u, 0.5f, 400, 1, 467));
                float len = 0.04f + 0.14f * Noise(u, 0.5f, 120, 1, 469);
                if (y < edge) a = Mathf.Max(a, drip * 0.45f * Smooth(edge - len, edge, y));
                a *= (1f - 0.65f * Smooth(1.6f, 5.6f, x)) * (1f - Smooth(5.9f, 6.5f, x));
                return new Color(0.06f, 0.055f, 0.05f, Mathf.Clamp01(a));
            });
            // Upward V plume above a burning item: narrow and dense at the source, spreading and dense under the ceiling.
            Texture2D sootPlume = OverlayTex("SootPlume", 256, 512, (u, v) =>
            {
                float px = Mathf.Abs(u - 0.5f);
                float hw = (0.07f + 0.4f * v) * (0.8f + 0.4f * Fbm(u, v, 6, 4, 471));
                float a = (1f - Smooth(hw * 0.5f, hw, px)) * (0.45f + 0.55f * v) * Smooth(0f, 0.1f, v);
                a *= 0.8f + 0.2f * Fbm(u, v, 16, 3, 473);
                a *= 1f - Smooth(0.42f, 0.5f, px);
                return new Color(0.07f, 0.06f, 0.055f, Mathf.Clamp01(a));
            });
            // Corridor ceiling: u = x 0.15..6.5 m, v = z 0.1..2.9 m; heaviest over Exit A, a ring round the fallen tiles.
            Texture2D sootCeiling = OverlayTex("SootCeiling", 512, 256, (u, v) =>
            {
                float x = 0.15f + u * 6.35f, z = 0.1f + v * 2.8f;
                float a = (0.9f - 0.62f * Smooth(0.4f, 5.4f, x)) * (0.75f + 0.5f * (Fbm(u, v, 8, 4, 481) - 0.5f));
                float gx = x - 5.5f, gz = z - 2.17f;
                a = Mathf.Max(a, 0.75f * (1f - Smooth(0.35f, 0.9f, Mathf.Sqrt(gx * gx + gz * gz * 0.6f))));
                a *= 1f - Smooth(5.9f, 6.5f, x);
                return new Color(0.07f, 0.065f, 0.06f, Mathf.Clamp01(a));
            });
            // Floor scorch: charred centre, umber ring with blister speckle, a pale heat-yellowed halo.
            Texture2D scorch = OverlayTex("Scorch", 512, 512, (u, v) => ScorchColor(u, v, true));
            // Heat-stained paint: the same rings without the charred core.
            Texture2D heatStain = OverlayTex("HeatStain", 512, 512, (u, v) => ScorchColor(u, v, false));

            mContactAO = MultiplyMaterial("ContactAO", contactAO, 0.45f);
            mContactPool = MultiplyMaterial("ContactPool", contactPool, 0.5f);
            mSootBand = MultiplyMaterial("SootBand", sootBand, 1f);
            mSootPlume = MultiplyMaterial("SootPlume", sootPlume, 0.9f);
            mSootPlumeLight = MultiplyMaterial("SootPlumeLight", sootPlume, 0.5f);
            mSootDoor = MultiplyMaterial("SootDoor", sootPlume, 0.25f);   // Exit A upper leaves: light, so the red still reads
            mSootCeiling = MultiplyMaterial("SootCeiling", sootCeiling, 1f);
            mScorch = MultiplyMaterial("Scorch", scorch, 0.95f);
            mScorchLight = MultiplyMaterial("ScorchLight", scorch, 0.6f);
            mHeatStain = MultiplyMaterial("HeatStain", heatStain, 0.75f);

            // Blistered paint (alpha-tested, so it takes the wall lighting and its own bubble normals).
            Texture2D blister = Tex("Blister", 512, TexKind.Color, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * (0.8f + 0.4f * Fbm(u, v, 4, 4, 491));
                float t = Smooth(0.2f, 0.85f, r);
                Color c = Color.Lerp(new Color(0.42f, 0.3f, 0.2f), new Color(0.82f, 0.74f, 0.6f), t);
                c *= 0.88f + 0.24f * Noise(u, v, 96, 96, 493);
                c.a = 1f - Smooth(0.86f, 0.9f, r);
                return c;
            });
            Texture2D blisterN = NormalTex("Blister_N", 512, 2.5f, (u, v) => BlisterHeight(u, v, 0.15f, 0.8f));
            mBlister = Lit("BlisterPaint", Color.white, blister, blisterN, 1f, 0.2f, normalScale: 0.8f);
            AlphaTest(mBlister, 0.5f);

            // Exit A leaves: red fire-door paint, intact low down, browned and blistered towards the head (2.2 m texture,
            // leaf UVs in metres from its corner). Still clearly a red, shut door.
            Texture2D doorScorch = Tex("DoorScorch", 512, TexKind.Color, (u, v) =>
            {
                float y = v * 2.2f;
                float edge = 0.95f + (Fbm(u, v, 6, 4, 501) - 0.5f) * 0.5f;
                float heat = Smooth(edge, edge + 0.45f, y);
                float top = Smooth(1.6f, 2.15f, y + (Fbm(u, v, 8, 3, 503) - 0.5f) * 0.3f);
                Color c = Color.Lerp(Color.white, new Color(0.72f, 0.55f, 0.48f), heat);
                c = Color.Lerp(c, new Color(0.4f, 0.33f, 0.3f), top);
                if (heat > 0.3f && Noise(u, v, 160, 160, 505) > 0.78f) c *= 0.82f;
                c *= 0.95f + 0.1f * Fbm(u, v, 32, 3, 507);
                c.a = Mathf.Lerp(0.95f, 0.35f, heat);   // smoothness: heat-dulled paint
                return c;
            });
            Texture2D doorScorchN = NormalTex("DoorScorch_N", 512, 2f, (u, v) =>
                Fbm(u, v, 64, 3, 509) * 0.1f + BlisterHeight(u, v, 0.6f, 1f) * Smooth(1f, 1.5f, v * 2.2f + (Fbm(u, v, 6, 4, 501) - 0.5f) * 0.5f));
            // Fake bounce from the debris fire in front of the leaves (no light to spare: the west pieces are at the URP
            // 8-light limit, and the fire spot aims away from the doors). Mask in linear, stored gamma (sRGB import):
            // full at the floor, gone by ~1.1 m, slightly broken up. Emission (0.35, 0.08, 0.02) linear.
            Texture2D doorBounce = Tex("DoorBounceMask", 128, TexKind.Color, (u, v) =>
            {
                float y = v * 2.2f;
                float mask = 1f - Smooth(0.15f, 1.1f, y + (Fbm(u, v, 4, 3, 513) - 0.5f) * 0.25f);
                float g = Mathf.LinearToGammaSpace(Mathf.Clamp01(mask));
                return new Color(g, g, g, 1f);
            });
            mDoorBlockedScorched = Lit("DoorBlockedScorched", new Color(0.46f, 0.11f, 0.09f), doorScorch, doorScorchN, 2.2f, 0.4f,
                normalScale: 0.6f, smoothnessInAlpha: true,
                emission: new Color(0.35f, 0.08f, 0.02f).gamma, emissionMap: doorBounce);

            // ---- Neighbouring buildings ----
            // Larger precast panels (3 x 1.5 m) for 10-14 m blocks; dark reflective glazing with mullions every 1.5 m.
            mFacadeLarge = Lit("FacadeLarge", new Color(0.8f, 0.77f, 0.72f), facade, facadeN, 6f, 0.18f);
            Texture2D glazing = Tex("ContextGlazing", 256, TexKind.Color, (u, v) =>
            {
                float m = Mathf.Min(Frac(u), 1f - Frac(u)) * 1.5f;            // metres to the mullion line
                float t = Mathf.Abs(v * 1.5f - 0.95f);                         // transom 0.95 m above the band bottom
                float frame = 1f - Smooth(0.035f, 0.045f, Mathf.Min(m, t * 2f));
                Color glass = new Color(0.16f, 0.19f, 0.22f) * (0.92f + 0.16f * Fbm(u, v, 2, 3, 511));
                Color c = Color.Lerp(glass, new Color(0.5f, 0.51f, 0.53f), frame);
                c.a = Mathf.Lerp(1f, 0.45f, frame);
                return c;
            });
            Texture2D glazingN = NormalTex("ContextGlazing_N", 256, 3f, (u, v) =>
            {
                float m = Mathf.Min(Frac(u), 1f - Frac(u)) * 1.5f, t = Mathf.Abs(v * 1.5f - 0.95f);
                return 1f - Smooth(0.035f, 0.045f, Mathf.Min(m, t * 2f));
            });
            mContextGlazing = Lit("ContextGlazing", Color.white, glazing, glazingN, 1.5f, 0.88f, normalScale: 0.5f, smoothnessInAlpha: true);
        }

        /// <summary>Height of wall detail maps (metres per repeat). Walls are at most 5.4 m tall, so 6 m never repeats a band.</summary>
        const float WallDetailHeight = 6f;

        /// <summary>
        /// Adds a URP Lit detail albedo (x2 multiply) and optional detail normal to <paramref name="m"/>. URP multiplies the
        /// detail UV by the already-tiled base UV, so the scale divides the base repeat <paramref name="tile"/> out again:
        /// one detail repeat = 12 m along u and <paramref name="height"/> m along v.
        /// </summary>
        static void Detail(Material m, float tile, Texture2D albedo, Texture2D normal, float height = 12f)
        {
            m.SetTexture("_DetailAlbedoMap", albedo);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(tile / 12f, tile / height));
            m.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
            m.SetFloat("_DetailAlbedoMapScale", 1f);
            m.SetTexture("_DetailNormalMap", normal);
            m.SetFloat("_DetailNormalMapScale", 0.5f);
            m.SetTexture("_DetailMask", null);   // shader default is white: detail everywhere
            SetKeyword(m, "_DETAIL_MULX2", albedo != null || normal != null);
            SetKeyword(m, "_DETAIL_SCALED", false);
            EditorUtility.SetDirty(m);
        }

        /// <summary>Linear grey detail value: 0.5 is neutral; <paramref name="warm"/> tints the darkened parts slightly brown.</summary>
        static Color DetailColor(float f, float warm)
        {
            float g = Mathf.Clamp01(0.5f * f), dirt = 1f - Mathf.Min(f, 1f);
            return new Color(g, g * (1f - warm * dirt), g * (1f - 2f * warm * dirt), 1f);
        }

        /// <summary>Concentric scorch: charred core (optional), umber ring with blister speckle, pale heat halo, soft ragged edge.</summary>
        static Color ScorchColor(float u, float v, bool core)
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * (0.82f + 0.36f * Fbm(u, v, 4, 4, 521));
            Color umber = new Color(0.35f, 0.22f, 0.12f), halo = new Color(0.92f, 0.85f, 0.7f);
            Color c = core ? Color.Lerp(new Color(0.08f, 0.075f, 0.07f), umber, Smooth(0.25f, 0.5f, r)) : Color.Lerp(new Color(0.5f, 0.38f, 0.26f), umber, Smooth(0f, 0.4f, r));
            c = Color.Lerp(c, halo, Smooth(0.5f, 0.75f, r));
            if (r > 0.3f && r < 0.65f && Noise(u, v, 128, 128, 523) > 0.72f) c *= 0.75f;
            c.a = (1f - Smooth(0.78f, 0.98f, r)) * (core ? 1f : 0.85f);
            return c;
        }

        /// <summary>Round paint blisters: bumps where cellular noise peaks, within a mask band.</summary>
        static float BlisterHeight(float u, float v, float threshold, float scale)
        {
            float n = Noise(u, v, 90, 90, 531);
            float bubble = Smooth(threshold + 0.35f, threshold + 0.55f, n + Noise(u, v, 40, 40, 533) * 0.3f);
            return bubble * scale;
        }

        /// <summary>
        /// Unlit multiply overlay (URP Unlit, Blend Multiply = DstColor Zero, _ALPHAMODULATE_ON lerps to white by alpha).
        /// No lighting, so soot and contact shading stay dark next to the fire light. Queue Transparent-10: before smoke.
        /// </summary>
        static Material MultiplyMaterial(string name, Texture2D tex, float strength = 1f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, strength));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 3f);   // Multiply
            m.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.Zero);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            m.SetFloat("_QueueOffset", -10f);
            m.SetOverrideTag("RenderType", "Transparent");
            SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", true);
            SetKeyword(m, "_ALPHAPREMULTIPLY_ON", false);
            SetKeyword(m, "_ALPHAMODULATE_ON", true);
            SetKeyword(m, "_ALPHATEST_ON", false);
            m.renderQueue = (int)RenderQueue.Transparent - 10;
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Turns an opaque Lit material into alpha-tested (base map alpha is coverage, not smoothness).</summary>
        static void AlphaTest(Material m, float cutoff)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", cutoff);
            SetKeyword(m, "_ALPHATEST_ON", true);
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
        }

        /// <summary>Linear (non-sRGB) repeat-wrapped detail map of any size; generated once like <see cref="Tex"/>.</summary>
        static Texture2D DetailTex(string name, int width, int height, System.Func<float, float, Color> pixel) =>
            RectTex(name, width, height, true, TextureWrapMode.Repeat, pixel);

        /// <summary>sRGB clamp-wrapped overlay texture (RGB stain colour, alpha strength); generated once.</summary>
        static Texture2D OverlayTex(string name, int width, int height, System.Func<float, float, Color> pixel) =>
            RectTex(name, width, height, false, TextureWrapMode.Clamp, pixel);

        static Texture2D RectTex(string name, int width, int height, bool linear, TextureWrapMode wrap, System.Func<float, float, Color> pixel)
        {
            string path = $"{TextureFolder}/{name}.png";
            if (!File.Exists(path))
            {
                var colors = new Color32[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        colors[y * width + x] = pixel((x + 0.5f) / width, (y + 0.5f) / height);
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
                tex.SetPixels32(colors);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                // Detail albedo must be linear: an sRGB import turns the neutral 0.5 into ~0.21 and darkens every wall.
                if (importer.textureType != TextureImporterType.Default || importer.sRGBTexture == linear || importer.wrapMode != wrap ||
                    importer.anisoLevel != 8 || importer.alphaIsTransparency || !importer.mipmapEnabled ||
                    importer.filterMode != FilterMode.Trilinear || importer.npotScale != TextureImporterNPOTScale.None)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = !linear;
                    importer.wrapMode = wrap;
                    importer.anisoLevel = 8;
                    importer.alphaIsTransparency = false;
                    importer.mipmapEnabled = true;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
