using UnityEngine;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        static Material mEyes, mLips, mStubble, mGloves, mVestBinding, mSole;

        /// <summary>
        /// Instructor clothing, skin and equipment materials. Runs at the end of CreateMaterials and re-creates the base
        /// instructor materials with textures. Stylised on purpose: matte skin and fabric, no glossy eyes, no SSS.
        /// Loft UVs are in metres, so every tile value below is the real size of one texture repeat.
        /// </summary>
        static void CreateInstructorMaterials()
        {
            Texture2D micro = Tex("Micro", 256, TexKind.Color, (u, v) =>
                GrayA(0.93f + (Fbm(u, v, 8, 4, 211) - 0.5f) * 0.1f + (Noise(u, v, 128, 128, 213) - 0.5f) * 0.03f,
                      0.78f + 0.22f * Fbm(u, v, 4, 3, 217)));
            Texture2D microN = NormalTex("Micro_N", 256, 0.9f, (u, v) => Fbm(u, v, 32, 3, 219));

            // Skin: soft low-frequency blotches (value and a slight red shift) plus fine pores; alpha breaks up the sheen
            // so the forehead and nose no longer show one broad plastic highlight. 8 cm per repeat.
            Texture2D skin = Tex("InstructorSkin_v3", 256, TexKind.Color, (u, v) =>
            {
                float blotch = Fbm(u, v, 4, 3, 601) - 0.5f, red = Fbm(u, v, 3, 2, 607) - 0.5f, pore = Noise(u, v, 128, 128, 611) - 0.5f;
                float g = 0.96f + blotch * 0.08f + pore * 0.035f;
                return new Color(g * (1f + red * 0.06f), g, g * (1f - red * 0.03f), 0.7f + 0.3f * Fbm(u, v, 8, 3, 613) - Mathf.Max(0f, pore) * 0.2f);
            });
            Texture2D skinN = NormalTex("InstructorSkin_N_v3", 256, 0.35f, (u, v) => Fbm(u, v, 16, 3, 617) * 0.6f + Noise(u, v, 128, 128, 611) * 0.4f);
            mSkin = Lit("InstructorSkin", new Color(0.66f, 0.49f, 0.4f), skin, skinN, 0.08f, 0.48f, normalScale: 0.4f, smoothnessInAlpha: true);
            // Shaved jaw: the same skin, about 7% darker and a touch cooler.
            mStubble = Lit("InstructorStubble", new Color(0.6f, 0.455f, 0.385f), skin, skinN, 0.08f, 0.45f, normalScale: 0.5f, smoothnessInAlpha: true);
            // Eyes are only a small matte-dark lens (iris + lash shadow): no white sclera, which is where uncanny starts.
            mEyes = Lit("InstructorEyes", new Color(0.06f, 0.045f, 0.04f), null, null, 1f, 0.55f);
            mLips = Lit("InstructorLips", new Color(0.48f, 0.3f, 0.26f), skin, skinN, 0.08f, 0.45f, normalScale: 0.3f, smoothnessInAlpha: true);

            // Hair: strands run down the loft (v), so the streaks vary across u (about 400 per metre).
            Texture2D hair = Tex("InstructorHair_v3", 256, TexKind.Color, (u, v) =>
            {
                float s = Noise(u, v, 32, 4, 621) * 0.7f + Noise(u, v, 96, 6, 623) * 0.3f;
                return GrayA(0.88f + (s - 0.5f) * 0.24f, 0.55f + 0.45f * s);
            });
            Texture2D hairN = NormalTex("InstructorHair_N_v3", 256, 0.8f, (u, v) => Noise(u, v, 32, 4, 621) * 0.7f + Noise(u, v, 96, 6, 623) * 0.3f);
            mHair = Lit("InstructorHair", new Color(0.1f, 0.07f, 0.05f), hair, hairN, 0.08f, 0.45f, normalScale: 0.6f, smoothnessInAlpha: true);

            // Uniform twill: a 2 mm diagonal rib at 5 cm per repeat, shared by shirt and trousers in different tones so the
            // figure no longer reads as one dark mass (mid navy shirt over charcoal work trousers).
            Texture2D twill = Tex("InstructorTwill_v3", 256, TexKind.Color, (u, v) =>
                GrayA(0.95f + 0.05f * Mathf.Sin((u + v) * 25f * 2f * Mathf.PI) + (Fbm(u, v, 8, 3, 631) - 0.5f) * 0.05f,
                      0.75f + 0.25f * Fbm(u, v, 4, 2, 633)));
            Texture2D twillN = NormalTex("InstructorTwill_N_v3", 256, 0.5f, (u, v) => 0.5f + 0.5f * Mathf.Sin((u + v) * 25f * 2f * Mathf.PI));
            mShirt = Lit("InstructorShirt", new Color(0.15f, 0.21f, 0.33f), twill, twillN, 0.05f, 0.16f, normalScale: 0.5f, smoothnessInAlpha: true);
            mTrousers = Lit("InstructorTrousers", new Color(0.09f, 0.095f, 0.1f), twill, twillN, 0.05f, 0.16f, normalScale: 0.5f, smoothnessInAlpha: true);

            // Hi-vis knit: 3 mm diamond mesh (10 cells per 3 cm repeat) with holes about 12% darker, so the vest reads as
            // polyester mesh rather than painted plastic.
            float Knit(float u, float v)
            {
                float d = Mathf.Abs(Frac(u * 10f) - 0.5f) + Mathf.Abs(Frac(v * 10f) - 0.5f);
                return Smooth(0.14f, 0.26f, d);
            }
            Texture2D knit = Tex("InstructorVestKnit_v3", 256, TexKind.Color, (u, v) =>
                GrayA(0.88f + 0.12f * Knit(u, v) + (Fbm(u, v, 4, 3, 641) - 0.5f) * 0.04f, 0.6f + 0.4f * Knit(u, v)));
            Texture2D knitN = NormalTex("InstructorVestKnit_N_v3", 256, 0.6f, Knit);
            mVest = Lit("InstructorVest", new Color(0.8f, 0.92f, 0.1f), knit, knitN, 0.03f, 0.22f, normalScale: 0.6f, smoothnessInAlpha: true);
            // Sewn edge binding: a darker, denser tape around the hem and neck.
            mVestBinding = Lit("InstructorVestBinding", new Color(0.5f, 0.56f, 0.08f), micro, microN, 0.3f, 0.25f, normalScale: 0.3f, smoothnessInAlpha: true);

            // Retro-reflective tape: fine glass-bead speckle. True retro-reflection needs a custom shader; a very low
            // emission is the honest approximation so the bands keep a silvery read in the dim emergency corridor.
            Texture2D tape = Tex("InstructorReflectiveTape_v3", 128, TexKind.Color, (u, v) =>
            {
                float bead = Noise(u, v, 64, 64, 651);
                return GrayA(0.97f + (bead - 0.5f) * 0.12f, 0.75f + 0.25f * bead);
            });
            Texture2D tapeN = NormalTex("InstructorReflectiveTape_N_v3", 128, 0.4f, (u, v) => Noise(u, v, 64, 64, 651));
            mReflective = Lit("InstructorReflective", new Color(0.78f, 0.79f, 0.8f), tape, tapeN, 0.05f, 0.6f,
                emission: new Color(0.06f, 0.06f, 0.065f), normalScale: 0.3f, smoothnessInAlpha: true);

            // Semi-gloss HDPE shell, deeper red: the old smoothness 0.6 read as a candy toy hat.
            mHelmet = Lit("InstructorHelmet", new Color(0.62f, 0.05f, 0.035f), micro, microN, 0.4f, 0.42f, normalScale: 0.15f, smoothnessInAlpha: true);
            // Black work gloves (matte, grippy) and a dark brown-grey rubber sole that separates the boot from the floor.
            mGloves = Lit("InstructorGloves", new Color(0.08f, 0.08f, 0.085f), micro, microN, 0.3f, 0.18f, normalScale: 0.5f, smoothnessInAlpha: true);
            mSole = Lit("InstructorSole", new Color(0.16f, 0.14f, 0.12f), micro, microN, 0.3f, 0.15f, normalScale: 0.4f, smoothnessInAlpha: true);
        }
    }
}
