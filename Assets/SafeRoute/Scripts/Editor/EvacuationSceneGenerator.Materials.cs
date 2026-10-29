using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        const string MaterialFolder = "Assets/SafeRoute/Materials";
        const string TextureFolder = "Assets/SafeRoute/Textures";

        // Architecture
        static Material mFloor, mCarpet, mLabFloor, mWall, mDado, mTrim, mFrame, mDoorWood, mDoorBlocked, mGlass, mCeiling,
            mLightPanel, mFacade, mConcrete, mCoping, mRoof, mAluminium;
        // Exterior
        static Material mGround, mPath, mAssembly, mHedge;
        // Props
        static Material mMetal, mChrome, mDark, mDesk, mChairBlue, mLabTop, mCabinet, mWhiteboard, mCork, mPaper, mLocker,
            mSignHousing, mExtinguisher;
        // Safety and hazards
        static Material mRoute, mFire, mEmber, mChar, mHazard, mSafetyYellow, mBarrierRed, mSoot, mSmokeParticles, mFireParticles;
        // Instructor
        static Material mSkin, mHair, mShirt, mTrousers, mVest, mReflective, mHelmet, mBoots;
        // World-space UI that must stay readable in front of nearby walls and signs
        static Material mUIOverlay;

        enum TexKind { Color, Normal, Particle }

        static void CreateMaterials()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(TextureFolder);

            // ---- Textures (generated once; delete the Textures folder to regenerate) ----
            // Superseded maps (Vinyl, Paint, CeilingTile(_N), Wood, BrushedMetal, SmokePuff, Flame) are replaced by the
            // "_v2"/new names below; their old PNGs are simply no longer referenced.
            Texture2D vinylN = NormalTex("Vinyl_N", 512, 2f, (u, v) => GridHeight(u, v, 2, 2, 0.6f, 0.6f, 0.004f) + Noise(u, v, 200, 200, 19) * 0.02f);

            Texture2D carpet = Tex("Carpet", 512, TexKind.Color, (u, v) =>
            {
                float n = Noise(u, v, 256, 256, 23) * 0.5f + Noise(u, v, 128, 128, 29) * 0.3f + Fbm(u, v, 8, 3, 31) * 0.2f;
                float g = (0.72f + n * 0.32f) * Mathf.Lerp(0.85f, 1f, GridHeight(u, v, 2, 2, 0.5f, 0.5f, 0.002f));
                if ((((int)(u * 2f) + (int)(v * 2f)) & 1) == 0) g *= 0.95f;
                return Gray(g);
            });
            Texture2D carpetN = NormalTex("Carpet_N", 512, 1.2f, (u, v) => Noise(u, v, 256, 256, 23));

            Texture2D paintN = NormalTex("Paint_N", 512, 0.6f, (u, v) => Fbm(u, v, 64, 3, 43));

            Texture2D concrete = Tex("Concrete", 512, TexKind.Color, (u, v) =>
            {
                float g = 0.82f + (Fbm(u, v, 4, 5, 59) - 0.5f) * 0.26f;
                float pore = Noise(u, v, 240, 240, 61);
                if (pore > 0.84f) g -= 0.25f * (pore - 0.84f) / 0.16f;
                return Gray(g);
            });
            Texture2D concreteN = NormalTex("Concrete_N", 512, 1.5f, (u, v) =>
                Fbm(u, v, 16, 4, 67) - Mathf.Max(0f, Noise(u, v, 240, 240, 61) - 0.84f) * 3f);

            // Precast facade panels: 2 x 4 panels of 1.5 m x 0.75 m per 3 m texture.
            Texture2D facade = Tex("Facade", 512, TexKind.Color, (u, v) =>
            {
                int pi = (int)(u * 2f), pj = (int)(v * 4f);
                float g = 0.88f + (Hash(pi, pj, 71) - 0.5f) * 0.06f + (Fbm(u, v, 8, 4, 73) - 0.5f) * 0.08f
                          + (Noise(u, v, 96, 3, 79) - 0.5f) * 0.03f;
                return Gray(Mathf.Lerp(0.5f, g, GridHeight(u, v, 2, 4, 1.5f, 0.75f, 0.008f)));
            });
            Texture2D facadeN = NormalTex("Facade_N", 512, 2f, (u, v) => GridHeight(u, v, 2, 4, 1.5f, 0.75f, 0.008f) + Fbm(u, v, 32, 3, 83) * 0.05f);

            // Concrete pavers, running bond: 6 rows x 3 pavers (0.4 m x 0.2 m) per 1.2 m texture.
            Texture2D pavers = Tex("Pavers", 512, TexKind.Color, (u, v) =>
            {
                int row = (int)(v * 6f);
                float cu = u * 3f + (row & 1) * 0.5f;
                float g = 0.86f + (Hash(Wrap(Mathf.FloorToInt(cu), 3), row, 89) - 0.5f) * 0.14f + (Fbm(u, v, 16, 3, 97) - 0.5f) * 0.1f;
                return Gray(Mathf.Lerp(0.5f, g, PaverHeight(u, v)));
            });
            Texture2D paversN = NormalTex("Pavers_N", 512, 2.5f, (u, v) => PaverHeight(u, v) + Fbm(u, v, 32, 2, 101) * 0.04f);

            Texture2D grass = Tex("Grass", 512, TexKind.Color, (u, v) =>
            {
                float g = 0.7f + Fbm(u, v, 6, 5, 103) * 0.35f + (Noise(u, v, 256, 256, 107) - 0.5f) * 0.15f;
                return new Color(g * 0.95f, g, g * 0.85f);
            });
            Texture2D grassN = NormalTex("Grass_N", 512, 1.5f, (u, v) => Noise(u, v, 256, 256, 107) * 0.5f + Fbm(u, v, 32, 2, 109) * 0.5f);

            Texture2D hazard = Tex("HazardStripes", 256, TexKind.Color, (u, v) =>
                Frac((u + v) * 2f) < 0.5f ? new Color(0.95f, 0.75f, 0.05f) : new Color(0.08f, 0.08f, 0.08f));

            Texture2D soot = Tex("Soot", 256, TexKind.Particle, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.4f) * 1.6f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return new Color(1f, 1f, 1f, Mathf.Pow(a, 1.5f) * (0.45f + 0.55f * Fbm(u, v, 6, 4, 151)));
            });

            // ---- v2 textures: alpha carries smoothness (Lit smoothnessInAlpha) so highlights break up ----
            // Shared fine surface breakup for painted / coated / plastic parts that had no texture at all.
            Texture2D micro = Tex("Micro", 256, TexKind.Color, (u, v) =>
                GrayA(0.93f + (Fbm(u, v, 8, 4, 211) - 0.5f) * 0.1f + (Noise(u, v, 128, 128, 213) - 0.5f) * 0.03f,
                      0.78f + 0.22f * Fbm(u, v, 4, 3, 217)));
            Texture2D microN = NormalTex("Micro_N", 256, 0.9f, (u, v) => Fbm(u, v, 32, 3, 219));

            // Vinyl: matte joints, low-frequency scuffed lanes and polished patches.
            Texture2D vinyl2 = Tex("Vinyl_v2", 512, TexKind.Color, (u, v) =>
            {
                int ti = (int)(u * 2f), tj = (int)(v * 2f);
                float g = 0.9f + (Hash(ti, tj, 7) - 0.5f) * 0.05f + (Fbm(u, v, 4, 3, 11) - 0.5f) * 0.06f;
                float fleck = Noise(u, v, 180, 180, 13);
                if (fleck > 0.8f) g -= 0.22f * (fleck - 0.8f) / 0.2f;
                if (Noise(u, v, 140, 140, 17) > 0.86f) g += 0.06f;
                float grid = GridHeight(u, v, 2, 2, 0.6f, 0.6f, 0.004f);
                float wear = 0.68f + 0.27f * Fbm(u, v, 3, 4, 23) + (Hash(ti, tj, 29) - 0.5f) * 0.1f;
                return GrayA(Mathf.Lerp(0.62f, g, grid), Mathf.Lerp(0.35f, wear, grid));
            });

            Texture2D paint2 = Tex("Paint_v2", 512, TexKind.Color, (u, v) =>
                GrayA(0.93f + (Fbm(u, v, 4, 4, 37) - 0.5f) * 0.07f + (Noise(u, v, 256, 256, 41) - 0.5f) * 0.025f,
                      0.8f + 0.2f * Fbm(u, v, 6, 3, 39)));

            // Lay-in ceiling: 24 mm white satin T-bar standing proud, a thin shadow reveal, matte fissured tiles.
            Texture2D ceiling2 = Tex("CeilingTile_v2", 512, TexKind.Color, (u, v) =>
            {
                float e = CeilingJoint(u, v);
                float bar = 1f - Smooth(0.0105f, 0.0125f, e), reveal = 1f - Smooth(0.0155f, 0.0175f, e);
                float g = 0.95f + (Noise(u, v, 64, 64, 47) - 0.5f) * 0.04f;
                float pit = Noise(u, v, 220, 220, 53);
                if (pit > 0.78f) g -= 0.14f * (pit - 0.78f) / 0.22f;
                g = bar * 0.93f + (reveal - bar) * 0.62f + (1f - reveal) * g;
                return GrayA(g, Mathf.Lerp(0.15f, 0.9f, bar));
            });
            Texture2D ceiling2N = NormalTex("CeilingTile_v2_N", 512, 1.5f, (u, v) =>
            {
                float e = CeilingJoint(u, v);
                float bar = 1f - Smooth(0.0105f, 0.0125f, e), tile = Smooth(0.0155f, 0.0175f, e);
                return bar + tile * 0.6f + tile * Noise(u, v, 220, 220, 53) * 0.05f;
            });

            // Desk / bench wood: grain along u (x) as before, plus smoothness in alpha and a grain normal.
            Texture2D wood2 = Tex("Wood_v2", 512, TexKind.Color, (u, v) =>
            {
                float n = Noise(u, v, 4, 48, 113) * 0.6f + Noise(u, v, 8, 160, 127) * 0.4f;
                float rings = WoodRingsU(u, v, n);
                float g = 0.78f + rings * 0.1f + n * 0.14f;
                return new Color(g, g * 0.97f, g * 0.93f, 0.7f + 0.3f * rings);
            });
            Texture2D woodN = NormalTex("Wood_N", 512, 0.8f, (u, v) =>
                WoodRingsU(u, v, Noise(u, v, 4, 48, 113) * 0.6f + Noise(u, v, 8, 160, 127) * 0.4f) * 0.3f
                + Noise(u, v, 8, 160, 127) * 0.7f);

            // Door veneer: vertical grain (door faces have v = height) with a domain-warped, irregular figure.
            Texture2D woodV = Tex("WoodV", 512, TexKind.Color, (u, v) =>
            {
                float n = Noise(u, v, 48, 4, 113) * 0.6f + Noise(u, v, 160, 8, 127) * 0.4f;
                float rings = WoodRingsV(u, v, n);
                float g = 0.78f + rings * 0.1f + n * 0.14f;
                return new Color(g, g * 0.97f, g * 0.93f, 0.7f + 0.3f * rings);
            });
            Texture2D woodVN = NormalTex("WoodV_N", 512, 0.8f, (u, v) =>
                WoodRingsV(u, v, Noise(u, v, 48, 4, 113) * 0.6f + Noise(u, v, 160, 8, 127) * 0.4f) * 0.3f
                + Noise(u, v, 160, 8, 127) * 0.7f);

            // Vertically brushed metal (lift doors, aluminium sections).
            Texture2D brushedV = Tex("BrushedMetalV", 256, TexKind.Color, (u, v) =>
                Gray(0.82f + (Noise(u, v, 256, 4, 131) * 0.6f + Noise(u, v, 128, 8, 137) * 0.4f) * 0.18f));
            Texture2D brushedVN = NormalTex("BrushedMetalV_N", 256, 1.5f, (u, v) => Noise(u, v, 256, 4, 131));

            // Ember emission mask: fbm iso-lines as thin glowing cracks, mostly black so only cracks bloom.
            Texture2D emberCracks = Tex("EmberCracks", 256, TexKind.Color, (u, v) =>
            {
                float crack = 1f - Smooth(0.015f, 0.06f, Mathf.Abs(Fbm(u, v, 6, 4, 163) - 0.5f));
                return Gray(crack * Smooth(0.35f, 0.7f, Fbm(u, v, 3, 2, 167)) * (0.6f + 0.4f * Noise(u, v, 32, 32, 227)));
            });

            // Smoke flipbook: 2x2 atlas of different puffs. Mild baked self-shadow (lighter top) on top of the lit shader.
            Texture2D smoke2 = Tex("SmokePuff_v2", 512, TexKind.Particle, (u, v) =>
            {
                int cell = (int)(u * 2f) + 2 * (int)(v * 2f);
                int seed = 139 + cell * 17;
                float lu = Frac(u * 2f), lv = Frac(v * 2f);
                float dx = (lu - 0.5f) * (0.9f + 0.25f * Hash(cell, 1, 241)), dy = lv - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Fbm(lu, lv, 4, 4, seed);
                float a = Smooth(0f, 1f, 1f - r * 2f / (0.6f + 0.4f * n)) * (1f - Smooth(0.4f, 0.48f, r));
                a *= 0.45f + 0.55f * Fbm(lu, lv, 8, 3, seed + 5);
                float g = Mathf.Lerp(0.72f, 1f, Smooth(-0.35f, 0.4f, dy + (n - 0.5f) * 0.4f));
                return new Color(g, g, g, a);
            });

            // Flame flipbook: 2x2 atlas of upward tongues (wide base at v=0, tip toward v=1), each leaning and flickering differently.
            Texture2D flame2 = Tex("Flame_v2", 512, TexKind.Particle, (u, v) =>
            {
                int cell = (int)(u * 2f) + 2 * (int)(v * 2f);
                int seed = 149 + cell * 13;
                float lu = Frac(u * 2f), lv = Frac(v * 2f);
                float top = 0.72f + 0.2f * Hash(cell, 3, 251);
                float lean = (Hash(cell, 5, 257) - 0.5f) * 0.3f;
                float rawX = (lu - 0.5f) * 2f;
                float x = rawX - lean * lv - (Noise(0f, lv, 1, 4, seed) - 0.5f) * 0.5f * lv;
                float width = Mathf.Lerp(0.8f + 0.1f * Hash(cell, 7, 263), 0.04f, Mathf.Pow(Mathf.Clamp01(lv / top), 0.7f + 0.4f * Hash(cell, 9, 269)))
                              * (0.85f + 0.3f * Noise(lu, lv, 6, 6, seed + 3));
                float edge = Mathf.Clamp01(1f - Mathf.Abs(x) / Mathf.Max(0.01f, width));
                float a = Smooth(0f, 1f, edge) * Smooth(0f, 0.1f, lv) * (1f - Smooth(top * 0.6f, top, lv))
                          * (1f - Smooth(0.9f, 1f, Mathf.Abs(rawX))) * (0.75f + 0.25f * Fbm(lu, lv, 4, 3, seed + 7));
                Color c = Color.Lerp(new Color(1f, 0.38f, 0.06f), new Color(1f, 0.88f, 0.55f), edge * edge * (1f - lv * 0.6f));
                return new Color(c.r, c.g, c.b, a);
            });

            // ---- Palette: warm neutrals, muted steel-blue accent, safety green/red/yellow reserved for guidance ----
            // Paint/coatings are dielectric (metallic 0); only bare metals are metallic 1. Whites stay below ~0.85 sRGB
            // so they keep shading under the tonemapper. Micro-textured materials raise _Smoothness ~10% because the
            // texture alpha (0.78-1) scales it down.
            mFloor = Lit("Floor", new Color(0.8f, 0.79f, 0.76f), vinyl2, vinylN, 1.2f, 0.5f, smoothnessInAlpha: true);
            mCarpet = Lit("Carpet", new Color(0.33f, 0.38f, 0.46f), carpet, carpetN, 1f, 0.08f);
            mLabFloor = Lit("LabFloor", new Color(0.76f, 0.79f, 0.78f), vinyl2, vinylN, 1.2f, 0.55f, smoothnessInAlpha: true);
            mWall = Lit("Wall", new Color(0.86f, 0.85f, 0.81f), paint2, paintN, 2f, 0.15f, normalScale: 0.4f, smoothnessInAlpha: true);
            mDado = Lit("WallDado", new Color(0.44f, 0.52f, 0.58f), paint2, paintN, 2f, 0.32f, normalScale: 0.4f, smoothnessInAlpha: true);
            mTrim = Lit("Trim", new Color(0.17f, 0.18f, 0.2f), micro, microN, 0.5f, 0.45f, normalScale: 0.35f, smoothnessInAlpha: true);
            mFrame = Lit("DoorFrame", new Color(0.36f, 0.38f, 0.4f), micro, microN, 0.5f, 0.42f, normalScale: 0.35f, smoothnessInAlpha: true);
            mDoorWood = Lit("DoorWood", new Color(0.64f, 0.48f, 0.33f), woodV, woodVN, 1f, 0.45f, normalScale: 0.35f, smoothnessInAlpha: true);
            mDoorBlocked = Lit("DoorBlocked", new Color(0.46f, 0.11f, 0.09f), micro, microN, 0.5f, 0.38f, normalScale: 0.35f, smoothnessInAlpha: true);
            // Clear glass: premultiplied blend keeps reflections at full strength while the body stays see-through.
            mGlass = Lit("Glass", new Color(0.82f, 0.88f, 0.88f, 0.12f), null, null, 1f, 0.92f, transparent: true, preserveSpecular: true);
            mCeiling = Lit("CeilingTile", new Color(0.9f, 0.9f, 0.88f), ceiling2, ceiling2N, 1.2f, 0.35f, smoothnessInAlpha: true);
            mLightPanel = Lit("LightPanel", Color.white, null, null, 1f, 0.3f, emission: new Color(1f, 0.97f, 0.92f) * 1.15f);
            mFacade = Lit("Facade", new Color(0.8f, 0.77f, 0.72f), facade, facadeN, 3f, 0.18f);
            mConcrete = Lit("Concrete", new Color(0.62f, 0.62f, 0.6f), concrete, concreteN, 2f, 0.15f);
            mCoping = Lit("Coping", new Color(0.72f, 0.72f, 0.7f), concrete, concreteN, 2f, 0.25f);
            mRoof = Lit("Roof", new Color(0.42f, 0.42f, 0.41f), concrete, concreteN, 2f, 0.1f);
            // Anodised aluminium is a true metal; vertical brushing breaks up the highlight on frames and rails.
            mAluminium = Lit("Aluminium", new Color(0.7f, 0.71f, 0.73f), brushedV, brushedVN, 0.5f, 0.42f, 1f, normalScale: 0.3f);

            mGround = Lit("Ground", new Color(0.36f, 0.48f, 0.25f), grass, grassN, 4f, 0.08f);
            mPath = Lit("Path", new Color(0.7f, 0.67f, 0.62f), pavers, paversN, 1.2f, 0.2f);
            // Painted green lines on the pavers: matte, not emissive; the ASSEMBLY POINT sign carries the meaning.
            mAssembly = Lit("AssemblyPad", new Color(0.2f, 0.46f, 0.3f), micro, microN, 0.5f, 0.25f, normalScale: 0.35f, smoothnessInAlpha: true);
            mHedge = Lit("Hedge", new Color(0.2f, 0.34f, 0.15f), grass, grassN, 1.5f, 0.05f);

            mMetal = Lit("ElevatorMetal", new Color(0.74f, 0.75f, 0.77f), brushedV, brushedVN, 0.5f, 0.5f, 1f, normalScale: 0.4f);
            mChrome = Lit("Chrome", new Color(0.8f, 0.8f, 0.82f), null, null, 1f, 0.75f, 1f);
            mDark = Lit("DarkGrey", new Color(0.12f, 0.12f, 0.13f), micro, microN, 0.5f, 0.38f, normalScale: 0.35f, smoothnessInAlpha: true);
            mDesk = Lit("Desk", new Color(0.76f, 0.62f, 0.46f), wood2, woodN, 1f, 0.42f, normalScale: 0.35f, smoothnessInAlpha: true);
            mChairBlue = Lit("ChairBlue", new Color(0.16f, 0.27f, 0.45f), micro, microN, 0.5f, 0.38f, normalScale: 0.35f, smoothnessInAlpha: true);
            // Dark epoxy-resin lab worktop (typical in school labs) instead of a large glaring white slab.
            mLabTop = Lit("LabTop", new Color(0.17f, 0.18f, 0.19f), micro, microN, 0.5f, 0.6f, normalScale: 0.35f, smoothnessInAlpha: true);
            mCabinet = Lit("Cabinet", new Color(0.56f, 0.59f, 0.62f), micro, microN, 0.5f, 0.35f, normalScale: 0.35f, smoothnessInAlpha: true);
            mWhiteboard = Lit("Whiteboard", new Color(0.88f, 0.89f, 0.88f), null, null, 1f, 0.7f);
            mCork = Lit("Cork", new Color(0.62f, 0.47f, 0.32f), concrete, null, 0.5f, 0.05f);
            mPaper = Lit("Paper", new Color(0.86f, 0.86f, 0.82f), null, null, 1f, 0.1f);
            mLocker = Lit("Locker", new Color(0.42f, 0.5f, 0.56f), micro, microN, 0.5f, 0.38f, normalScale: 0.35f, smoothnessInAlpha: true);
            mSignHousing = Lit("SignHousing", new Color(0.78f, 0.78f, 0.77f), micro, microN, 0.5f, 0.38f, normalScale: 0.35f, smoothnessInAlpha: true);
            mExtinguisher = Lit("ExtinguisherRed", new Color(0.72f, 0.05f, 0.04f), micro, microN, 0.5f, 0.75f, normalScale: 0.25f, smoothnessInAlpha: true);

            // Route chevrons: matte signal-green floor paint/tape (~RAL 6032), no self-illumination.
            mRoute = Lit("SafeRouteGreen", new Color(0.14f, 0.46f, 0.3f), micro, microN, 0.5f, 0.3f, normalScale: 0.35f, smoothnessInAlpha: true);
            mFire = Lit("FireGlow", new Color(1f, 0.42f, 0.08f), null, null, 1f, 0.2f, emission: new Color(1f, 0.38f, 0.06f) * 2.5f);
            // Burning debris: black char whose glow is masked to thin cracks, so only the cracks bloom.
            mEmber = Lit("Ember", new Color(0.07f, 0.06f, 0.05f), concrete, concreteN, 0.6f, 0.08f,
                emission: new Color(1f, 0.34f, 0.06f) * 1.8f, emissionMap: emberCracks);
            mChar = Lit("Charred", new Color(0.06f, 0.055f, 0.05f), concrete, concreteN, 0.6f, 0.08f);
            mHazard = Lit("HazardStripes", Color.white, hazard, null, 0.4f, 0.4f);
            mSafetyYellow = Lit("SafetyYellow", new Color(0.95f, 0.75f, 0.05f), micro, microN, 0.5f, 0.44f, normalScale: 0.35f, smoothnessInAlpha: true);
            mBarrierRed = Lit("BarrierRed", new Color(0.78f, 0.08f, 0.06f), micro, microN, 0.5f, 0.5f, normalScale: 0.35f, smoothnessInAlpha: true);
            mSoot = Lit("Soot", new Color(0.03f, 0.03f, 0.03f, 0.85f), soot, null, 1f, 0.05f, transparent: true);
            // Smoke is lit (picks up the fire light and corridor lights) and mid-grey; startColor sets the final density/tone.
            mSmokeParticles = ParticleMaterial("SmokeParticles", smoke2, new Color(0.6f, 0.59f, 0.57f, 0.7f), false,
                lit: true, softFadeDistance: 0.6f, cameraFadeNear: 0.3f, cameraFadeFar: 1.5f);
            mFireParticles = ParticleMaterial("FireParticles", flame2, Color.white, true,
                softFadeDistance: 0.25f, cameraFadeNear: 0.2f, cameraFadeFar: 0.6f);

            // Instructor: skin, hair and fabric are matte dielectrics; only the hard hat is glossy plastic.
            mSkin = Lit("InstructorSkin", new Color(0.72f, 0.55f, 0.45f), null, null, 1f, 0.32f);
            mHair = Lit("InstructorHair", new Color(0.12f, 0.08f, 0.06f), null, null, 1f, 0.2f);
            mUIOverlay = UIOverlayMaterial();
            mShirt = Lit("InstructorShirt", new Color(0.12f, 0.17f, 0.28f), micro, microN, 0.5f, 0.12f, normalScale: 0.3f, smoothnessInAlpha: true);
            mTrousers = Lit("InstructorTrousers", new Color(0.1f, 0.11f, 0.14f), micro, microN, 0.5f, 0.12f, normalScale: 0.3f, smoothnessInAlpha: true);
            mVest = Lit("InstructorVest", new Color(0.82f, 0.9f, 0.12f), micro, microN, 0.5f, 0.18f, normalScale: 0.3f, smoothnessInAlpha: true);
            // Retro-reflective tape is a dielectric film, not metal.
            mReflective = Lit("InstructorReflective", new Color(0.7f, 0.72f, 0.74f), null, null, 1f, 0.55f);
            mHelmet = Lit("InstructorHelmet", new Color(0.78f, 0.07f, 0.05f), null, null, 1f, 0.6f);
            mBoots = Lit("InstructorBoots", new Color(0.06f, 0.06f, 0.07f), null, null, 1f, 0.35f);

            // Later passes add or refine materials; each lives in its own partial file and may update the ones above.
            CreateEnvironmentMaterials();
            CreateFireMaterials();
            CreateInstructorMaterials();
            CreateSignMaterials();
            CreateThirdPartyMaterials();   // last: replaces maps above only where the ThirdParty files exist
        }

        // ---------- Materials ----------

        /// <param name="tile">Metres covered by one repeat of the texture (box UVs are in metres).</param>
        static Material Lit(string name, Color color, Texture2D map, Texture2D normal, float tile, float smoothness,
            float metallic = 0f, Color? emission = null, bool transparent = false, float normalScale = 1f,
            bool smoothnessInAlpha = false, Texture2D emissionMap = null, bool preserveSpecular = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            m.SetTexture("_BaseMap", map);
            m.SetTextureScale("_BaseMap", Vector2.one / tile);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            // Smoothness = albedo.a * _Smoothness. Not on transparent materials, where alpha is opacity.
            bool alphaSmooth = smoothnessInAlpha && !transparent && map != null;
            m.SetFloat("_SmoothnessTextureChannel", alphaSmooth ? 1f : 0f);
            SetKeyword(m, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", alphaSmooth);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", normalScale);
            SetKeyword(m, "_NORMALMAP", normal != null);
            // Metallic/occlusion maps belong to the third-party pass; cleared here so a material falls back cleanly.
            MaskMaps(m, null, null, smoothness);

            m.SetColor("_EmissionColor", emission ?? Color.black);
            m.SetTexture("_EmissionMap", emissionMap); // shares the base map tiling in URP Lit
            m.globalIlluminationFlags = emission.HasValue ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            SetKeyword(m, "_EMISSION", emission.HasValue);

            m.SetFloat("_Surface", transparent ? 1f : 0f);
            m.SetFloat("_Blend", 0f);
            // Preserve specular (premultiplied RGB): reflections are not faded by alpha. Written explicitly so URP's
            // material validation and the generator agree on every regeneration.
            bool premultiply = transparent && preserveSpecular;
            m.SetFloat("_BlendModePreserveSpecular", premultiply ? 1f : 0f);
            m.SetFloat("_SrcBlend", (float)(transparent && !premultiply ? BlendMode.SrcAlpha : BlendMode.One));
            m.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            m.SetFloat("_ZWrite", transparent ? 0f : 1f);
            m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", transparent);
            SetKeyword(m, "_ALPHAPREMULTIPLY_ON", premultiply);
            m.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
            m.SetShaderPassEnabled("ShadowCaster", !transparent);
            m.SetShaderPassEnabled("DepthOnly", !transparent);

            EditorUtility.SetDirty(m);
            return m;
        }

        /// <param name="lit">Simple Lit (per-pixel diffuse, no specular, no shadows) instead of Unlit.</param>
        /// <param name="softFadeDistance">Soft-particle fade depth in metres (needs the camera depth texture); 0 disables.</param>
        /// <param name="cameraFadeNear">Camera fading: fully transparent closer than this...</param>
        /// <param name="cameraFadeFar">...and fully opaque beyond this; equal values disable it.</param>
        static Material ParticleMaterial(string name, Texture2D texture, Color color, bool additive, bool lit = false,
            float softFadeDistance = 0f, float cameraFadeNear = 0f, float cameraFadeFar = 0f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = lit ? Shader.Find("Universal Render Pipeline/Particles/Simple Lit") : null;
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[SafeRoute] URP Particles/Unlit shader not found; particles will use the default material.");
                return null;
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", true);
            SetKeyword(m, "_ALPHAPREMULTIPLY_ON", false);
            m.SetFloat("_FlipbookBlending", 0f); // flipbook frames are picked per particle, not animated
            SetKeyword(m, "_FLIPBOOKBLENDING_ON", false);

            // Lit smoke: matte, no specular, no shadow receive (cheap and avoids shadow speckle on billboards).
            if (m.HasProperty("_SpecularHighlights"))
            {
                m.SetFloat("_Smoothness", 0f);
                m.SetFloat("_SpecularHighlights", 0f);
                m.SetFloat("_SmoothnessSource", 0f);
                SetKeyword(m, "_SPECGLOSSMAP", false);
                SetKeyword(m, "_SPECULAR_COLOR", false);
                m.SetFloat("_ReceiveShadows", 0f);
                SetKeyword(m, "_RECEIVE_SHADOWS_OFF", true);
            }

            // Soft particles fade where quads intersect walls/floor/ceiling; params as URP's ParticleGUI computes them.
            bool soft = softFadeDistance > 0f;
            m.SetFloat("_SoftParticlesEnabled", soft ? 1f : 0f);
            m.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            m.SetFloat("_SoftParticlesFarFadeDistance", Mathf.Max(softFadeDistance, 0.01f));
            m.SetVector("_SoftParticleFadeParams", soft ? new Vector4(0f, 1f / softFadeDistance, 0f, 0f) : Vector4.zero);
            SetKeyword(m, "_SOFTPARTICLES_ON", soft);

            // Camera fading avoids full-screen quads when the head is inside a particle.
            bool fade = cameraFadeFar > cameraFadeNear;
            m.SetFloat("_CameraFadingEnabled", fade ? 1f : 0f);
            m.SetFloat("_CameraNearFadeDistance", cameraFadeNear);
            m.SetFloat("_CameraFarFadeDistance", fade ? cameraFadeFar : cameraFadeNear + 1f);
            m.SetVector("_CameraFadeParams", fade ? new Vector4(cameraFadeNear, 1f / (cameraFadeFar - cameraFadeNear), 0f, 0f)
                                                  : new Vector4(0f, Mathf.Infinity, 0f, 0f));
            SetKeyword(m, "_FADING_ON", fade || soft); // same rule as URP's ParticleGUI

            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Default UI shader with depth test disabled: the instruction panel is drawn over walls and signs.</summary>
        static Material UIOverlayMaterial()
        {
            string path = $"{MaterialFolder}/UIOverlay.mat";
            Shader shader = Shader.Find("UI/Default");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetKeyword(Material m, string keyword, bool enabled)
        {
            if (enabled) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }

        // ---------- Procedural textures ----------

        static Texture2D Tex(string name, int size, TexKind kind, System.Func<float, float, Color> pixel)
        {
            string path = $"{TextureFolder}/{name}.png";
            if (!File.Exists(path))
            {
                var colors = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        colors[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, kind == TexKind.Normal);
                tex.SetPixels32(colors);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var type = kind == TexKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                var wrap = kind == TexKind.Particle ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                bool particle = kind == TexKind.Particle;
                if (importer.textureType != type || importer.wrapMode != wrap || importer.anisoLevel != 8 ||
                    importer.alphaIsTransparency != particle || !importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear)
                {
                    importer.textureType = type;
                    importer.wrapMode = wrap;
                    importer.anisoLevel = 8;
                    importer.alphaIsTransparency = particle;
                    importer.mipmapEnabled = true;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Tangent-space normal map derived from a tileable height function.</summary>
        static Texture2D NormalTex(string name, int size, float strength, System.Func<float, float, float> height)
        {
            float[] h = null;
            return Tex(name, size, TexKind.Normal, (u, v) =>
            {
                if (h == null)
                {
                    h = new float[size * size];
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            h[y * size + x] = height((x + 0.5f) / size, (y + 0.5f) / size);
                }
                int px = Mathf.FloorToInt(u * size), py = Mathf.FloorToInt(v * size);
                float dx = h[py * size + Wrap(px + 1, size)] - h[py * size + Wrap(px - 1, size)];
                float dy = h[Wrap(py + 1, size) * size + px] - h[Wrap(py - 1, size) * size + px];
                Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            });
        }

        /// <summary>1 on tile faces, 0 in the joints of a grid of <paramref name="cols"/> x <paramref name="rows"/> tiles.</summary>
        static float GridHeight(float u, float v, int cols, int rows, float tileW, float tileH, float joint)
        {
            float gu = Frac(u * cols), gv = Frac(v * rows);
            float e = Mathf.Min(Mathf.Min(gu, 1f - gu) * tileW, Mathf.Min(gv, 1f - gv) * tileH);
            return Smooth(joint * 0.5f, joint * 1.5f, e);
        }

        static float PaverHeight(float u, float v)
        {
            int row = (int)(v * 6f);
            float fu = Frac(u * 3f + (row & 1) * 0.5f), fv = Frac(v * 6f);
            float e = Mathf.Min(Mathf.Min(fu, 1f - fu) * 0.4f, Mathf.Min(fv, 1f - fv) * 0.2f);
            return Smooth(0.003f, 0.012f, e);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)seed * 0xcb1ab31fu;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0xFFFFFFu) / 16777215f;
            }
        }

        /// <summary>Tileable value noise on [0,1) with independent lattice periods.</summary>
        static float Noise(float u, float v, int periodU, int periodV, int seed)
        {
            float x = u * periodU, y = v * periodV;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Wrap(x0, periodU), xb = Wrap(x0 + 1, periodU), ya = Wrap(y0, periodV), yb = Wrap(y0 + 1, periodV);
            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Noise(u, v, period, period, seed + o * 101);
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        static int Wrap(int i, int period)
        {
            int r = i % period;
            return r < 0 ? r + period : r;
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>GLSL-style smoothstep.</summary>
        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static Color Gray(float g) => new Color(g, g, g, 1f);

        /// <summary>Grey with alpha; alpha carries smoothness for Lit(smoothnessInAlpha: true).</summary>
        static Color GrayA(float g, float a) => new Color(g, g, g, a);

        /// <summary>Distance in metres to the nearest joint of the 0.6 m ceiling grid (2 x 2 tiles per texture).</summary>
        static float CeilingJoint(float u, float v)
        {
            float gu = Frac(u * 2f), gv = Frac(v * 2f);
            return Mathf.Min(Mathf.Min(gu, 1f - gu), Mathf.Min(gv, 1f - gv)) * 0.6f;
        }

        /// <summary>Growth rings for grain running along u (desk tops, bench slats).</summary>
        static float WoodRingsU(float u, float v, float n) => Mathf.Sin((v * 24f + n * 3f) * Mathf.PI * 2f) * 0.5f + 0.5f;

        /// <summary>Growth rings for vertical grain, domain-warped so the figure is irregular rather than a regular sine.</summary>
        static float WoodRingsV(float u, float v, float n)
        {
            float w = (Fbm(u, v, 2, 3, 229) - 0.5f) * 0.6f;
            return Mathf.Sin((u * 18f + n * 4f + w * 2f) * Mathf.PI * 2f) * 0.5f + 0.5f;
        }
    }
}
