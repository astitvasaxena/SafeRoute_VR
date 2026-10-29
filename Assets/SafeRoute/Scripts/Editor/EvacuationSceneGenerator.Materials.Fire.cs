using UnityEngine;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        static Material mFireCoreParticles, mSparkParticles, mAshParticles, mFireGlowBed, mCharAsh, mEmberLine;

        /// <summary>Flame, smoke, spark and burning-fuel materials. Runs at the end of CreateMaterials.</summary>
        static void CreateFireMaterials()
        {
            // Flame tongue flipbook: 4x4 atlas of ONE tongue evolving over 16 frames (played over each particle's life,
            // frame-blended). Turbulence scrolls up through the tongue and tears the tip into licks; the noise scrolls by
            // whole periods per cycle, so frame 15 runs seamlessly into frame 0. Hot core near white, faint red edges
            // (additive red over the beige walls otherwise reads as pink). The base fades in over the bottom 20% of the cell
            // and narrows towards it, so no hard horizontal cut shows where the quad ends.
            Texture2D flame4 = Tex("Flame_v4", 1024, TexKind.Particle, (u, v) =>
            {
                float t = SheetFrame(u, v, 4) / 16f;
                float lu = Frac(u * 4f), lv = Frac(v * 4f);
                const int seed = 331;
                float x = (lu - 0.5f) * 2f;
                x -= (Fbm(lu * 0.5f + 0.25f, lv * 2f - t, 4, 3, seed) - 0.5f) * 1.1f * lv;
                x -= Mathf.Sin((t + lv * 0.5f) * Mathf.PI * 2f) * 0.12f * lv; // slow sway of the whole tongue
                const float top = 0.94f;
                float width = Mathf.Lerp(0.46f, 0.02f, Mathf.Pow(Mathf.Clamp01(lv / top), 0.8f)) * (0.55f + 0.45f * Smooth(0f, 0.2f, lv));
                float edge = Mathf.Clamp01(1f - Mathf.Abs(x) / width);
                float tear = Smooth(0.35f, 0.6f, Fbm(lu * 3f, lv * 3f - t * 2f, 6, 3, seed + 11) + (1f - lv) * 0.45f);
                float body = Smooth(0f, 0.2f, lv) * (1f - Smooth(top * 0.75f, top, lv)) * (1f - Smooth(0.85f, 1f, Mathf.Abs(lu - 0.5f) * 2f));
                float heat = edge * (1f - lv * 0.8f);
                Color c = heat < 0.42f
                    ? Color.Lerp(new Color(0.9f, 0.3f, 0.05f), new Color(1f, 0.7f, 0.25f), Smooth(0.15f, 0.42f, heat))
                    : Color.Lerp(new Color(1f, 0.7f, 0.25f), new Color(1f, 0.95f, 0.8f), Smooth(0.45f, 0.8f, heat));
                return new Color(c.r, c.g, c.b, Mathf.Clamp01(Mathf.Pow(heat, 0.7f) * 1.25f * body * tear));
            });

            // Flame core: 2x2 atlas of wide, dense, elliptical beds where fuel meets flame (white-yellow root, orange
            // ragged top). The ellipse sits low in the cell, falls off faster below its centre than above, and the fbm
            // only tears its upper half; a radial mask takes alpha to 0 well inside every cell edge, so the HDR additive
            // core never shows a flat bottom or straight sides on its vertical billboards.
            Texture2D flameCore = Tex("FlameCore_v2", 512, TexKind.Particle, (u, v) =>
            {
                int cell = (int)(u * 2f) + 2 * (int)(v * 2f);
                int seed = 347 + cell * 19;
                float lu = Frac(u * 2f), lv = Frac(v * 2f);
                float dy = lv - 0.4f;
                float d = Mathf.Sqrt((lu - 0.5f) * (lu - 0.5f) * 4.84f + dy * dy * (dy < 0f ? 6.76f : 2.56f));
                d += (Fbm(lu, lv, 4, 3, seed) - 0.5f) * 0.45f * Smooth(0.3f, 0.7f, lv);
                float a = (1f - Smooth(0.2f, 0.8f, d)) * (0.7f + 0.3f * Fbm(lu, lv, 8, 3, seed + 3))
                          * (1f - Smooth(0.4f, 0.5f, Mathf.Max(Mathf.Abs(lu - 0.5f), Mathf.Abs(lv - 0.5f))));
                Color c = Color.Lerp(new Color(1f, 0.97f, 0.85f), new Color(1f, 0.6f, 0.2f), Smooth(0.2f, 0.75f, lv));
                return new Color(c.r, c.g, c.b, a * 0.8f);
            });

            // Spark: small soft dot; the renderer stretches it along the velocity.
            Texture2D sparkDot = Tex("SparkDot_v1", 64, TexKind.Particle, (u, v) =>
            {
                float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                float a = 1f - Smooth(0f, 1f, r);
                return new Color(1f, 1f, 1f, a * a);
            });

            // Paper ash: 2x2 atlas of ragged flakes (tinted by the particle colour: glowing rim first, then dark).
            Texture2D ashFlake = Tex("AshFlake_v1", 128, TexKind.Particle, (u, v) =>
            {
                int cell = (int)(u * 2f) + 2 * (int)(v * 2f);
                float lu = Frac(u * 2f) - 0.5f, lv = Frac(v * 2f) - 0.5f;
                float ragged = 0.3f + 0.14f * Fbm(u, v, 8, 3, 379 + cell * 7);
                float a = 1f - Smooth(ragged - 0.05f, ragged, Mathf.Max(Mathf.Abs(lu), Mathf.Abs(lv) * 1.3f));
                return new Color(1f, 1f, 1f, a);
            });

            // Glow bed under the floor fires: soft radial falloff with a little break-up.
            Texture2D glowBed = Tex("FireGlowBed_v1", 128, TexKind.Particle, (u, v) =>
            {
                float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                float a = Mathf.Pow(1f - Smooth(0f, 1f, r), 1.5f) * (0.8f + 0.2f * Fbm(u, v, 4, 3, 383));
                return new Color(1f, 1f, 1f, a);
            });

            // Smouldering fuel: thin, sparse cracks (about 15-20% of the surface) with soft halos, instead of an even
            // crack web on every face (the "magma cube" look).
            Texture2D emberCracks2 = Tex("EmberCracks_v2", 256, TexKind.Color, (u, v) =>
            {
                float crack = 1f - Smooth(0.008f, 0.035f, Mathf.Abs(Fbm(u, v, 6, 4, 163) - 0.5f));
                float mask = Smooth(0.55f, 0.8f, Fbm(u, v, 3, 2, 167));
                float smoulder = 0.15f * Smooth(0.6f, 0.9f, Fbm(u, v, 2, 3, 173));
                return Gray(Mathf.Clamp01(crack * mask * (0.6f + 0.4f * Noise(u, v, 32, 32, 227)) + smoulder));
            });

            // Char with patches of grey-white ash; alpha carries smoothness (charred cardboard has a slight sheen, ash none).
            Texture2D charAsh = Tex("CharAsh_v1", 512, TexKind.Color, (u, v) =>
            {
                float ash = Smooth(0.6f, 0.8f, Fbm(u, v, 4, 4, 389));
                float g = Mathf.Lerp(0.05f + 0.03f * Noise(u, v, 64, 64, 397), 0.32f, ash);
                return GrayA(g, 1f - ash * 0.6f);
            });
            Texture2D charAshN = NormalTex("CharAsh_N", 256, 2f, (u, v) => Fbm(u, v, 8, 4, 401) - Smooth(0.6f, 0.8f, Fbm(u, v, 4, 4, 389)) * 0.3f);

            mEmber = Lit("Ember", Color.white, charAsh, charAshN, 0.6f, 0.15f, smoothnessInAlpha: true,
                emission: new Color(1f, 0.3f, 0.05f) * 1.3f, emissionMap: emberCracks2);
            mCharAsh = Lit("CharredAsh", Color.white, charAsh, charAshN, 0.6f, 0.15f, smoothnessInAlpha: true);
            // The notice board's burn front glows along its whole length (a sparse crack mask would leave it dark).
            mEmberLine = Lit("EmberLine", new Color(0.07f, 0.06f, 0.05f), null, null, 1f, 0.1f, emission: new Color(1f, 0.32f, 0.06f) * 1.6f);

            // SetColor converts gamma to linear in this Linear project, so HDR targets are given in linear and converted.
            // Additive flame tongues stay at white (below bloom); only the dense core and sparks cross the bloom threshold.
            mFireParticles = BlendedFlipbook(ParticleMaterial("FireParticles", flame4, Color.white, true,
                softFadeDistance: 0.25f, cameraFadeNear: 0.2f, cameraFadeFar: 0.6f));
            mFireCoreParticles = ParticleMaterial("FireCoreParticles", flameCore, new Color(1.9f, 1.45f, 1f).gamma, true,
                softFadeDistance: 0.2f, cameraFadeNear: 0.2f, cameraFadeFar: 0.6f);
            mSparkParticles = ParticleMaterial("SparkParticles", sparkDot, new Color(3f, 2f, 1.2f).gamma, true,
                cameraFadeNear: 0.1f, cameraFadeFar: 0.3f);
            // Unlit, so a flake's glowing rim reads as hot; short camera fade so flakes near the trainee stay visible.
            mAshParticles = ParticleMaterial("AshParticles", ashFlake, Color.white, false, cameraFadeNear: 0.15f, cameraFadeFar: 0.4f);
            // Floor glow: additive, no soft-particle fade (the quad lies on the floor, which would fade it out entirely).
            // Same hue as the fire lights once converted to linear (~1 : 0.23 : 0.03).
            mFireGlowBed = ParticleMaterial("FireGlowBed", glowBed, new Color(0.62f, 0.3f, 0.08f), true, cameraFadeNear: 0.3f, cameraFadeFar: 1f);
            // Smoke flipbook: 4x4 atlas of one billowing puff whose lobes roll and whose erosion scrolls through 16 frames
            // (loops). Seven large, overlapping lobes (r 0.3-0.38, offsets 0.08-0.12) are summed and accumulated softly,
            // density = 1 - exp(-k * sum), so they merge into one body instead of separate peaked discs ("cotton balls").
            // Low-frequency fbm (period 2, 3 octaves) erodes it without speckle. The puff fills ~75% of the cell with
            // ~20%-wide soft edges, and a radial mask takes alpha to 0 before every cell border.
            Texture2D smoke5 = Tex("SmokePuff_v5", 1024, TexKind.Particle, (u, v) =>
            {
                float t = SheetFrame(u, v, 4) / 16f;
                float lu = Frac(u * 4f), lv = Frac(v * 4f);
                const int seed = 359;
                float sum = 0f, lobeY = 0f;
                for (int i = 0; i < 7; i++)
                {
                    float angle = Hash(i, 1, seed) * Mathf.PI * 2f;
                    float dist = i == 0 ? 0f : 0.08f + 0.04f * Hash(i, 2, seed);
                    float orbit = (t + Hash(i, 4, seed)) * Mathf.PI * 2f;
                    float cx = 0.5f + Mathf.Cos(angle) * dist + Mathf.Cos(orbit) * 0.02f;
                    float cy = 0.5f + Mathf.Sin(angle) * dist * 0.85f + Mathf.Sin(orbit) * 0.02f;
                    float r = i == 0 ? 0.38f : 0.3f + 0.06f * Hash(i, 3, seed);
                    float dx = lu - cx, dy = lv - cy;
                    float disc = 1f - Smooth(0f, 1f, Mathf.Sqrt(dx * dx + dy * dy) / r);
                    sum += disc;
                    lobeY += disc * dy / r;
                }
                lobeY /= Mathf.Max(sum, 1e-4f);
                float density = 1f - Mathf.Exp(-0.75f * sum);
                float n = Fbm(lu, lv - t, 2, 3, seed + 7);
                float a = Mathf.Clamp01(density * (0.35f + 0.9f * n) - (1f - n) * 0.2f * (1f - density));
                a *= 1f - Smooth(0.34f, 0.5f, Mathf.Sqrt((lu - 0.5f) * (lu - 0.5f) + (lv - 0.5f) * (lv - 0.5f)));
                float g = Mathf.Lerp(0.55f, 0.9f, Smooth(-0.3f, 0.35f, (lv - 0.5f) + lobeY * 0.3f));
                return new Color(g, g, g, Mathf.Clamp01(a * 0.85f));
            });
            // Every smoke system uses the 4x4 sheet (Flipbook(ps, 4, ...) in Hazards). Darker base: the plume pools right
            // under the downward ceiling spots, and a light base turned it near-white there instead of sooty grey.
            mSmokeParticles = BlendedFlipbook(ParticleMaterial("SmokeParticles", smoke5, new Color(0.36f, 0.35f, 0.34f, 0.7f), false,
                lit: true, softFadeDistance: 0.6f, cameraFadeNear: 0.3f, cameraFadeFar: 1.5f));
        }

        /// <summary>Frame index (0 = top-left, row-major as the particle texture-sheet module plays it) of an n x n atlas.</summary>
        static int SheetFrame(float u, float v, int n) => (int)(u * n) + n * (n - 1 - (int)(v * n));

        /// <summary>
        /// Turns on flipbook frame blending (URP particle shaders: _FLIPBOOKBLENDING_ON). The renderers using it must
        /// supply the UV2 and AnimBlend vertex streams (see FlipbookBlendStreams in Hazards).
        /// </summary>
        static Material BlendedFlipbook(Material m)
        {
            if (m == null) return null;
            m.SetFloat("_FlipbookBlending", 1f);
            SetKeyword(m, "_FLIPBOOKBLENDING_ON", true);
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }
    }
}
