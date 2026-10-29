using UnityEditor;
using UnityEngine;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        // Internally lit sign faces: UI/Default multiplies the (linearised) vertex colour by _Color, so a _Color above 1
        // brightens the face like a lightbox. Values here are LINEAR multipliers; the project is in Linear space, where
        // Material.SetColor treats colours as gamma, so UIGlowMaterial passes them through .gamma.
        // The field stays in gamut and below the bloom threshold (1.0): ISO safety green, no clipping, no neon halo.
        static Material mSignGlow, mSignLegend;

        /// <summary>Illuminated safety-sign and training-UI materials. Runs at the end of CreateMaterials.</summary>
        static void CreateSignMaterials()
        {
            // Green field: SignGreen (0, 0.52, 0.22) is (0, 0.234, 0.040) linear; x 1.6 = (0, 0.375, 0.064) linear,
            // about (0, 166, 72) sRGB on screen. The white legend (1.0) keeps over 3:1 contrast against it.
            mSignGlow = UIGlowMaterial("SignGlow", 1.6f);
            mSignLegend = UIGlowMaterial("SignLegend", 1.0f);   // white legend and pictogram at plain white: never blooms
        }

        /// <summary>UI/Default material whose _Color is the linear multiplier <paramref name="linearIntensity"/>.</summary>
        static Material UIGlowMaterial(string name, float linearIntensity)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find("UI/Default");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.SetColor("_Color", new Color(linearIntensity, linearIntensity, linearIntensity, 1f).gamma);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
