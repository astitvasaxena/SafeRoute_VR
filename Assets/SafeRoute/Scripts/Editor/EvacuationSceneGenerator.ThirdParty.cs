using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        // Real-world textures, models, sign artwork and flipbooks under Assets/SafeRoute/ThirdParty (sources, licences and
        // attribution: ThirdParty/CREDITS.md; fetched and converted by Tools/Assets/fetch_third_party_assets.py).
        // Every use is optional: when a file is missing the procedural material, prop or sign from the other passes stays.
        const string ThirdPartyFolder = "Assets/SafeRoute/ThirdParty";
        const string ThirdPartyMaterialFolder = MaterialFolder + "/ThirdParty";

        enum MapKind { Color, Data, Normal, Sprite, Particle }

        // Flipbook layout used by the flame and smoke particles (Hazards): 4x4 procedural sheets unless the Unity Labs
        // 8x8 sheets are present. flameSheets is the share of the sheet each tongue plays over its life: Flame02's 64
        // frames are ~2 s of simulation, so 0.35 keeps a 0.5-1 s tongue near real speed.
        static int flameTiles = 4, smokeTiles = 4;
        static float flameSheets = 1f;

        // Materials for the Poly Haven models (null when a model or its maps are missing).
        static Material[] mDeskModel, mChairModel, mExtinguisherModel, mCallPointModel;

        /// <summary>Real-world surfaces, model materials and flipbooks. Runs last in CreateMaterials.</summary>
        static void CreateThirdPartyMaterials()
        {
            flameTiles = smokeTiles = 4;
            flameSheets = 1f;
            EnsureFolder(ThirdPartyMaterialFolder);

            // ---- Surfaces: world-anchored box UVs are in metres, so one repeat = the photo's real size. ----
            // The tint keeps each material's tuned palette and brightness: tint = palette (linear) x the mean of the
            // procedural map it replaces / the photo's linear mean (measured from the processed colour maps).
            ApplySurface(mWall, "PolyHaven", "painted_plaster_wall", 2f, new Color(0.406f, 0.367f, 0.354f), new Color(0.86f, 0.85f, 0.81f), 0.85f, normalScale: 0.6f);
            ApplySurface(mDado, "PolyHaven", "painted_plaster_wall", 2f, new Color(0.406f, 0.367f, 0.354f), new Color(0.44f, 0.52f, 0.58f), 0.85f, normalScale: 0.6f);
            ApplySurface(mFloor, "ambientCG", "Tiles110", 1.85f, new Color(0.395f, 0.32f, 0.234f), new Color(0.8f, 0.79f, 0.76f), 0.75f);
            ApplySurface(mLabFloor, "ambientCG", "Tiles110", 1.85f, new Color(0.395f, 0.32f, 0.234f), new Color(0.76f, 0.79f, 0.78f), 0.75f);
            // Published size 3.85 m; 3.6 m makes its 6 x 6 tiles 0.6 m, on the same grid as the lights and ceiling detail.
            ApplySurface(mCeiling, "ambientCG", "OfficeCeiling001", 3.6f, new Color(0.91f, 0.91f, 0.91f), new Color(0.9f, 0.9f, 0.88f), 0.8f);
            ApplySurface(mDoorWood, "PolyHaven", "oak_veneer_01", 1.83f, new Color(0.36f, 0.211f, 0.098f), new Color(0.64f, 0.48f, 0.33f), 0.7f, normalScale: 0.5f);
            ApplySurface(mDesk, "PolyHaven", "oak_veneer_01", 1.83f, new Color(0.36f, 0.211f, 0.098f), new Color(0.76f, 0.62f, 0.46f), 0.7f, normalScale: 0.5f);
            ApplySurface(mConcrete, "PolyHaven", "smooth_concrete_floor", 2f, new Color(0.2f, 0.2f, 0.2f), new Color(0.62f, 0.62f, 0.6f), 0.65f);
            ApplySurface(mCoping, "PolyHaven", "smooth_concrete_floor", 2f, new Color(0.2f, 0.2f, 0.2f), new Color(0.72f, 0.72f, 0.7f), 0.65f);
            ApplySurface(mRoof, "PolyHaven", "smooth_concrete_floor", 2f, new Color(0.2f, 0.2f, 0.2f), new Color(0.42f, 0.42f, 0.41f), 0.65f);
            ApplySurface(mPath, "PolyHaven", "concrete_pavers", 1.92f, new Color(0.14f, 0.115f, 0.089f), new Color(0.7f, 0.67f, 0.62f), 0.6f);
            // Real size not published: 1 m keeps the brushing grain fine on the 1 m lift leaves.
            ApplySurface(mMetal, "ambientCG", "Metal009", 1f, new Color(0.293f, 0.297f, 0.315f), new Color(0.74f, 0.75f, 0.77f), 0.8f, normalScale: 0.5f);
            // Burnt debris keeps the photo's own colour and the 0.6 m repeat its ember crack mask (emission) was tuned to.
            // No roughness map: flat 0.13, the old char's average (0.15 x its ~0.85 albedo-alpha smoothness).
            ApplySurface(mCharAsh, "OpenGameArt", "BurnedWood2", 0.6f, Color.white, null, 1f, smoothness: 0.13f);
            ApplySurface(mEmber, "OpenGameArt", "BurnedWood2", 0.6f, Color.white, null, 1f, smoothness: 0.13f);

            // ---- Model materials (UV-mapped: tile 1). Import size per slot: the gauge glass, the tag and the 0.14 m call
            // point cover a few hundred pixels at most, so 512 is plenty (texture-memory budget). ----
            mDeskModel = ModelMaterials("SchoolDesk_01", ("SchoolDesk_01", "main", false, 1024));
            mChairModel = ModelMaterials("SchoolChair_01", ("SchoolChair_01", "main", false, 1024));
            mExtinguisherModel = ModelMaterials("korean_fire_extinguisher_01",
                ("korean_fire_extinguisher_01_body", "body", false, 1024), ("korean_fire_extinguisher_01_glass", "glass", true, 512),
                ("korean_fire_extinguisher_01_paper", "paper", false, 512));
            mCallPointModel = ModelMaterials("fire_alarm", ("fire_alarm", "main", false, 512));

            // ---- Flipbooks: Unity Labs sheets (checked: 8x8, seamless loops; Flame02 repacked from 16x4) ----
            Texture2D flame = ThirdPartyTex($"{ThirdPartyFolder}/UnityLabs/Flame02/Flame02_8x8.png", MapKind.Particle);
            if (flame != null && mFireParticles != null)
            {
                mFireParticles.SetTexture("_BaseMap", flame);
                EditorUtility.SetDirty(mFireParticles);
                flameTiles = 8;
                flameSheets = 0.35f;
            }
            Texture2D smoke = ThirdPartyTex($"{ThirdPartyFolder}/UnityLabs/WispySmoke01/WispySmoke01_8x8.png", MapKind.Particle);
            if (smoke != null && mSmokeParticles != null)
            {
                // The baked smoke is 3.4x darker (linear mean 0.137 vs 0.462) and 17% denser than SmokePuff_v5, so the
                // base colour rises to keep the tuned tone: (0.36, 0.35, 0.34, 0.7) -> (0.635, 0.62, 0.605, 0.6).
                mSmokeParticles.SetTexture("_BaseMap", smoke);
                mSmokeParticles.SetColor("_BaseColor", new Color(0.635f, 0.62f, 0.605f, 0.6f));
                EditorUtility.SetDirty(mSmokeParticles);
                smokeTiles = 8;
            }
        }

        // ---------- Surfaces ----------

        /// <summary>
        /// Replaces the maps of a procedural Lit material with a photo-scanned set: colour (sRGB), OpenGL normal,
        /// MetallicSmoothness (R metal, A 1 - roughness; linear) and AO when present. <paramref name="metres"/> is one repeat
        /// on the metre box UVs. Any detail map is re-scaled so it keeps its 12 m x height world repeat. Returns false and
        /// leaves the material alone when the colour map is missing.
        /// </summary>
        static bool ApplySurface(Material m, string source, string id, float metres, Color photoMeanLinear, Color? palette,
            float replacedMean, float smoothness = 1f, float normalScale = 1f)
        {
            if (m == null) return false;
            string root = $"{ThirdPartyFolder}/{source}/{id}/{id}";
            Texture2D color = ThirdPartyTex(root + "_Color", MapKind.Color);   // 1024 import cap (2K sources stay on disk)
            if (color == null) return false;

            float oldTile = 1f / Mathf.Max(1e-4f, m.GetTextureScale("_BaseMap").x);
            bool detail = m.IsKeywordEnabled("_DETAIL_MULX2");
            var detailAlbedo = (Texture2D)m.GetTexture("_DetailAlbedoMap");
            var detailNormal = (Texture2D)m.GetTexture("_DetailNormalMap");
            float detailHeight = oldTile / Mathf.Max(1e-4f, m.GetTextureScale("_DetailAlbedoMap").y);

            m.SetTexture("_BaseMap", color);
            m.SetTextureScale("_BaseMap", Vector2.one / metres);
            m.SetColor("_BaseColor", palette.HasValue ? SurfaceTint(palette.Value, replacedMean, photoMeanLinear) : Color.white);
            Texture2D normal = ThirdPartyTex(root + "_NormalGL", MapKind.Normal);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", normalScale);
            SetKeyword(m, "_NORMALMAP", normal != null);
            Texture2D ms = ThirdPartyTex(root + "_MS", MapKind.Data);
            MaskMaps(m, ms, ThirdPartyTex(root + "_AO", MapKind.Data), smoothness);
            if (ms == null)
            {
                // No roughness map (BurnedWood2): a flat smoothness; the photo's alpha is not a smoothness channel.
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                SetKeyword(m, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", false);
            }
            if (detail) Detail(m, metres, detailAlbedo, detailNormal, detailHeight);
            EditorUtility.SetDirty(m);
            return true;
        }

        /// <summary>
        /// Base colour (gamma, as SetColor expects) whose linear value maps the photo's mean onto the old look: palette x
        /// the replaced procedural map's mean. Capped at 1.6x so a dark photo is not pushed past a plausible albedo.
        /// </summary>
        static Color SurfaceTint(Color palette, float replacedMean, Color photoMeanLinear)
        {
            Color target = palette.linear * replacedMean;
            var t = new Color(target.r / photoMeanLinear.r, target.g / photoMeanLinear.g, target.b / photoMeanLinear.b, 1f);
            float peak = Mathf.Max(t.r, Mathf.Max(t.g, t.b));
            if (peak > 1.6f) t = new Color(t.r * 1.6f / peak, t.g * 1.6f / peak, t.b * 1.6f / peak, 1f);
            return t.gamma;
        }

        /// <summary>URP Lit metallic map (R metallic, A smoothness x _Smoothness) and occlusion; null clears them.</summary>
        static void MaskMaps(Material m, Texture2D metallicSmoothness, Texture2D occlusion, float smoothness)
        {
            m.SetTexture("_MetallicGlossMap", metallicSmoothness);
            SetKeyword(m, "_METALLICSPECGLOSSMAP", metallicSmoothness != null);
            if (metallicSmoothness != null)
            {
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                SetKeyword(m, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", false);
            }
            m.SetTexture("_OcclusionMap", occlusion);
            m.SetFloat("_OcclusionStrength", 1f);
            SetKeyword(m, "_OCCLUSIONMAP", occlusion != null);
        }

        // ---------- Models ----------

        /// <summary>
        /// One URP Lit material per FBX material slot, in slot order: (slot name, processed map tag, transparent). The
        /// paper tag is alpha-tested, glass is transparent. Null when the model or any colour map is missing.
        /// </summary>
        static Material[] ModelMaterials(string id, params (string slot, string tag, bool transparent, int maxSize)[] slots)
        {
            string dir = $"{ThirdPartyFolder}/PolyHaven/{id}";
            if (!File.Exists($"{dir}/{id}.fbx")) return null;
            ConfigureModel($"{dir}/{id}.fbx");
            var result = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var (slot, tag, transparent, maxSize) = slots[i];
                string root = $"{dir}/{id}_{tag}";
                Texture2D color = ThirdPartyTex(root + "_Color", MapKind.Color, maxSize);
                if (color == null) return null;
                Material m = Lit($"ThirdParty/{slot}", Color.white, color, ThirdPartyTex(root + "_NormalGL", MapKind.Normal, maxSize), 1f, 1f,
                    transparent: transparent, preserveSpecular: transparent);
                MaskMaps(m, ThirdPartyTex(root + "_MS", MapKind.Data, maxSize), null, 1f);
                if (tag == "paper") AlphaTest(m, 0.5f);
                result[i] = m;
            }
            return result;
        }

        /// <summary>Real-world-scale FBX: metres from the file units, no animation, cameras, lights or embedded materials used.</summary>
        static void ConfigureModel(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            if (importer.globalScale == 1f && importer.useFileUnits && !importer.importAnimation && !importer.importCameras &&
                !importer.importLights && !importer.isReadable && importer.materialImportMode == ModelImporterMaterialImportMode.None)
                return;
            importer.globalScale = 1f;
            importer.useFileUnits = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // the generator assigns its own URP materials
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Places a Poly Haven model under <paramref name="parent"/>, rotated by <paramref name="localRotation"/> and scaled,
        /// with the bottom centre of its bounds (parent space) at <paramref name="anchor"/>; with a
        /// <paramref name="wallNormal"/> (parent space, pointing out of the wall) the anchor is the bottom of its back face
        /// instead. Materials are assigned per submesh. Static, casts shadows, no collider (callers keep their collider
        /// boxes). Returns null when the model or its materials are missing.
        /// </summary>
        static GameObject PlaceModel(string id, Material[] materials, Transform parent, string name, Vector3 anchor,
            Quaternion localRotation, float scale = 1f, Vector3? wallNormal = null)
        {
            if (materials == null) return null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ThirdPartyFolder}/PolyHaven/{id}/{id}.fbx");
            if (asset == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            // Single-mesh FBX files keep the Z-up to Y-up conversion (-90 X, x100 for centimetres) on their root.
            go.transform.localRotation = localRotation * asset.transform.localRotation;
            go.transform.localScale = asset.transform.localScale * scale;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds();
            for (int k = 0; k < renderers.Length; k++)
            {
                Renderer r = renderers[k];
                Mesh mesh = r.GetComponent<MeshFilter>() is MeshFilter mf ? mf.sharedMesh : null;
                var slots = new Material[mesh != null ? mesh.subMeshCount : 1];
                for (int i = 0; i < slots.Length; i++) slots[i] = materials[Mathf.Min(i, materials.Length - 1)];
                r.sharedMaterials = slots;
                // Bounds in the parent's space from the mesh's local bounds corners.
                Bounds local = mesh != null ? mesh.bounds : new Bounds();
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    Vector3 p = parent.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (k == 0 && c == 0) bounds = new Bounds(p, Vector3.zero);
                    else bounds.Encapsulate(p);
                }
            }
            Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (wallNormal.HasValue)
            {
                Vector3 n = wallNormal.Value.normalized;
                float depth = Mathf.Abs(n.x) * bounds.extents.x + Mathf.Abs(n.y) * bounds.extents.y + Mathf.Abs(n.z) * bounds.extents.z;
                pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - new Vector3(n.x, 0f, n.z) * depth;
            }
            go.transform.localPosition = anchor - pivot;
            foreach (Transform t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return go;
        }

        // ---------- Sign artwork ----------

        /// <summary>White-on-transparent pictogram mask from the Wikimedia ISO artwork (null when missing).</summary>
        static Sprite SignPictogram(string file)
        {
            string path = $"{ThirdPartyFolder}/Wikimedia/{file}_Mask.png";
            return ThirdPartyTex(path, MapKind.Sprite) != null ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : null;
        }

        static Sprite ExitPictogram(Arrow arrow) =>
            arrow == Arrow.Left ? SignPictogram("ISO_Exit_-_Left") :
            arrow == Arrow.Right ? SignPictogram("ISO_Exit_-_Right") :
            arrow == Arrow.Up ? SignPictogram("ISO_Exit_-_Up") : null;

        /// <summary>A pictogram image of <paramref name="size"/> px centred at <paramref name="center"/> (px from the face centre).</summary>
        static Image AddPictogram(RectTransform face, Sprite sprite, Vector2 center, Vector2 size, Color color)
        {
            Image image = AddImage(face, "Pictogram", color);
            RectTransform rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = center;
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }

        /// <summary>
        /// Square ISO 7010 sign (pictogram only) on a coloured field, read from the -Z side of its root; optional back face.
        /// Used for the call-point (F005) and assembly-point (E007) signs.
        /// </summary>
        static Transform PictogramSign(Transform p, string name, Sprite sprite, Vector3 facePosition, Vector3 facing, float size,
            Color field, float housingDepth, bool doubleSided = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(p, false);
            root.localPosition = facePosition;
            root.localRotation = Quaternion.LookRotation(-facing);
            root.gameObject.isStatic = true;
            if (housingDepth > 0f)
                Box("Housing", root, new Vector3(0f, 0f, doubleSided ? 0f : housingDepth / 2f), new Vector3(size + 0.01f, size + 0.01f, housingDepth), mSignHousing, false);
            foreach (float side in doubleSided ? new[] { 1f, -1f } : new[] { 1f })
            {
                float z = doubleSided ? -side * (housingDepth / 2f + 0.002f) : -0.002f;
                Canvas canvas = CreateCanvas(root, side > 0f ? "Face" : "Face (back)", new Vector3(0f, 0f, z),
                    side > 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f), new Vector2(size, size), 1f);
                var face = (RectTransform)canvas.transform;
                AddImage(face, "Background", field);
                AddPictogram(face, sprite, Vector2.zero, face.sizeDelta, Color.white);
            }
            return root;
        }

        // ---------- Textures ----------

        /// <summary>
        /// Loads a processed third-party texture (path with or without extension: .png then .jpg) and keeps its importer
        /// right for its use: only colour maps and sprites are sRGB; normal maps are imported as OpenGL normal maps; at most
        /// <paramref name="maxSize"/> px (1024 keeps the scene's texture memory near 90 MB; the 2K plaster and terrazzo
        /// sources stay on disk for higher-end targets). Null when the file is missing.
        /// </summary>
        static Texture2D ThirdPartyTex(string path, MapKind kind, int maxSize = 1024)
        {
            if (!Path.HasExtension(path))
                path = File.Exists(path + ".png") ? path + ".png" : path + ".jpg";
            if (!File.Exists(path)) return null;
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return null;

            var type = kind == MapKind.Normal ? TextureImporterType.NormalMap : kind == MapKind.Sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            bool srgb = kind == MapKind.Color || kind == MapKind.Sprite || kind == MapKind.Particle;
            var wrap = kind == MapKind.Sprite || kind == MapKind.Particle ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            bool alpha = kind == MapKind.Sprite || kind == MapKind.Particle;
            if (importer.textureType != type || (type == TextureImporterType.Default && importer.sRGBTexture != srgb) ||
                importer.wrapMode != wrap || importer.alphaIsTransparency != alpha || !importer.mipmapEnabled ||
                importer.anisoLevel != 8 || importer.filterMode != FilterMode.Trilinear || importer.maxTextureSize != maxSize ||
                (type == TextureImporterType.Sprite && importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = type;
                if (type == TextureImporterType.Default) importer.sRGBTexture = srgb;
                if (type == TextureImporterType.Sprite) importer.spriteImportMode = SpriteImportMode.Single;
                importer.wrapMode = wrap;
                importer.alphaIsTransparency = alpha;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = maxSize;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
