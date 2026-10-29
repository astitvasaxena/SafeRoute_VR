using SafeRoute.Evacuation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        const string SettingsFolder = "Assets/SafeRoute/Settings";
        const string VolumeProfilePath = SettingsFolder + "/SafeRouteEvacuation_Volume.asset";
        // Colour temperatures: cool daylight at the windows, neutral-warm LED panels inside (fire is ~1900 K in Hazards).
        // The lamps sit 300 K warmer than before to offset the cooler white balance (-6), so they still read neutral
        // while the ambient fill and daylight shift cool against the warm fire.
        const float SunKelvin = 6200f, InteriorKelvin = 4000f, LabKelvin = 4500f, AlcoveKelvin = 3800f;

        /// <summary>Realtime only (no baking needed): sun, trilight ambient, distance fog and restrained post-processing.</summary>
        static void CreateLighting(Transform p)
        {
            // Sun from the north-east so daylight falls through the classroom windows and the open Exit B.
            var sun = new GameObject("Sun");
            sun.transform.SetParent(p, false);
            sun.transform.rotation = Quaternion.Euler(42f, 205f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.useColorTemperature = true;
            light.colorTemperature = SunKelvin;
            light.intensity = 1.8f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f; // full-strength window shadows keep the sunlit floor patches crisp
            RenderSettings.sun = light;

            // Unoccluded trilight fill, ~10% below its previous level and cooler (blue-grey sky, neutral equator), so
            // direct light dominates and the shadow side of the room reads cool against the warm fire. Still high
            // enough that nothing indoors goes murky (darkest fill ~0.26 linear before the +0.4 EV exposure).
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.345f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.258f, 0.262f, 0.266f);
            // Ground colour stands in for light bounced off the floor: the only light the ceilings get (the ceiling
            // spots point down), so it is kept above the equator level or ceilings would read darker than floors.
            RenderSettings.ambientGroundColor = new Color(0.305f, 0.295f, 0.285f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.79f, 0.85f);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 220f;

            var volume = new GameObject("Post-processing (Global Volume)");
            volume.transform.SetParent(p, false);
            var v = volume.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 1f;
            v.sharedProfile = VolumeProfileAsset();
        }

        static VolumeProfile VolumeProfileAsset()
        {
            EnsureFolder(SettingsFolder);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            // Every grade below is baked into the colour-grading LUT, and bloom and vignette are already part of the
            // uber pass, so this look adds no per-pixel passes.
            // ACES: filmic toe/shoulder and highlight desaturation (fire rolls toward yellow-white) in the same LUT pass.
            VolumeSetting<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            // Threshold just under the LED panels (1.15) and emergency lenses (1.2 linear): they gain a soft halo, while lit
            // walls and signs (well below 1) never bloom. Low intensity keeps fire and panels from smearing in a headset.
            var bloom = VolumeSetting<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.65f);
            var colour = VolumeSetting<ColorAdjustments>(profile);
            // ACES darkens midtones (~0.4 EV vs Neutral): exposure stays lifted, slightly lower than before (+0.5) so
            // the scene reads less "videogame bright". A gentle contrast and a small desaturation give a documentary
            // look; mid-grey is the contrast pivot, so lit walls, signs and the instructor keep their brightness.
            colour.postExposure.Override(0.4f);
            colour.contrast.Override(10f);
            colour.saturation.Override(-6f);
            // Cooler white balance (keep it at -8 or warmer, or the interior reads clinical); see the lamp kelvins above.
            VolumeSetting<WhiteBalance>(profile).temperature.Override(-6f);
            // Split tone: a touch of blue in the shadows, a touch of warmth in the highlights (where fire and lamps live).
            // No offsets (w = 0), so shadow and highlight levels are unchanged.
            var smh = VolumeSetting<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.97f, 0.99f, 1.04f, 0f));
            smh.highlights.Override(new Vector4(1.03f, 1f, 0.97f, 0f));
            // Faint static vignette: settles the eye toward the centre without visibly darkening the view edges in
            // a headset. Set intensity to 0 if it is noticeable in VR.
            var vignette = VolumeSetting<Vignette>(profile);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.45f);
            vignette.rounded.Override(false);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T VolumeSetting<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>();
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static void ConfigureCamera(Camera cam)
        {
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.dithering = true;                              // hides banding in smooth gradients on the R11G11B10 buffer
            data.requiresDepthOption = CameraOverrideOption.On; // depth texture for soft particles
            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            cam.farClipPlane = 300f;                            // fog ends at 220 m; the ground is ~110 m across
            PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
        }

        // ---------- Interior lighting ----------

        static void BuildInteriorLights(Transform p)
        {
            Transform fixtures = Group("Light Fixtures", p), lights = Group("Ceiling Lights", p), probes = Group("Reflection Probes", p);

            var corridorPanels = new Renderer[12];
            for (int k = 0; k < 12; k++) corridorPanels[k] = LightPanel(fixtures, new Vector3(2f + 2.5f * k, CeilingHeight, 1.5f), new Vector2(1.2f, 0.3f));
            foreach (float x in new[] { 2.2f, 4.8f, 7.4f, 10f, 13.6f, 16.2f, 18.8f, 21.2f, 23.6f, 26.2f, 28.8f, 31.2f })
                foreach (float z in new[] { 4.4f, 6.6f })
                    LightPanel(fixtures, new Vector3(x, CeilingHeight, z), new Vector2(0.6f, 0.6f));
            LightPanel(fixtures, new Vector3(5.5f, CeilingHeight, -1f), new Vector2(0.6f, 0.6f));
            LightPanel(fixtures, new Vector3(16.5f, StairCeilingHeight, -3f), new Vector2(0.6f, 0.6f));

            // Downward spots in the plane of the fixtures (same count as before): light pools on floor and desks,
            // no hotspots on the ceiling. Corridor spots sit on panels; room spots at the centre of each panel cluster.
            foreach (float x in new[] { 4.5f, 9.5f, 14.5f, 19.5f, 24.5f, 29.5f })
            {
                Light corridor = CeilingLight(lights, "Corridor Light", new Vector3(x, CeilingHeight, 1.5f), 5.5f, 7f, InteriorKelvin);
                if (x > 5f) continue;
                // The one spot inside the smoke layer (x 0.2..6.4): it and the panels at x 2 and 4.5 dim and turn amber as
                // the stored boxes and fallen ceiling (its drivers, found within 3 m at Start) burn, while the Exit B end
                // stays bright. The Exit A fire burns from the start, so it lies outside the radius and adds no step.
                var response = corridor.gameObject.AddComponent<FireAmbientResponse>();
                response.lights = new[] { corridor };
                response.panels = new[] { corridorPanels[0], corridorPanels[1] };
            }
            foreach (float x in new[] { 3.5f, 8.7f, 14.9f, 20f, 24.9f, 30f })
                CeilingLight(lights, "Room Light", new Vector3(x, CeilingHeight, 5.5f), 6.5f, 8f, x > 22f ? LabKelvin : InteriorKelvin);
            CeilingLight(lights, "Stairwell Light", new Vector3(16.5f, StairCeilingHeight, -3f), 12f, 10f, InteriorKelvin);
            CeilingLight(lights, "Elevator Alcove Light", new Vector3(5.5f, CeilingHeight, -1f), 3.5f, 4.5f, AlcoveKelvin);
            BuildEmergencyLighting(Group("Emergency Lighting", p));

            Probe(probes, "Corridor Probe", new Vector3(16f, 1.5f, 1.5f), new Vector3(32.2f, 3f, 3f));
            Probe(probes, "Classroom 1 Probe", new Vector3(6f, 1.5f, 5.5f), new Vector3(11.8f, 3f, 4.8f));
            Probe(probes, "Classroom 2 Probe", new Vector3(17f, 1.5f, 5.5f), new Vector3(9.8f, 3f, 4.8f));
            Probe(probes, "Lab Probe", new Vector3(27f, 1.5f, 5.5f), new Vector3(9.8f, 3f, 4.8f));
            Probe(probes, "Stairwell Probe", new Vector3(16.5f, 2.4f, -3f), new Vector3(7f, 4.8f, 6f));
            Probe(probes, "Elevator Alcove Probe", new Vector3(5.5f, 1.5f, -1f), new Vector3(3f, 3f, 2f));
            // Low-importance catch-all so no interior pixel falls back to the (never baked) sky reflection.
            Probe(probes, "Building Fallback Probe", new Vector3(16f, 1.5f, 1f), new Vector3(32.6f, 6.8f, 14.4f), 0, false);
        }

        static Renderer LightPanel(Transform p, Vector3 ceilingPoint, Vector2 size)
        {
            GameObject panel = NoShadows(Box("Light Panel", p, ceilingPoint + Vector3.down * 0.0125f, new Vector3(size.x, 0.025f, size.y), mLightPanel, false));
            NoShadows(Box("Light Panel Frame", p, ceilingPoint + Vector3.down * 0.006f, new Vector3(size.x + 0.05f, 0.012f, size.y + 0.05f), mAluminium, false));
            return panel.GetComponent<Renderer>();
        }

        // ---------- Emergency lighting ----------

        /// <summary>
        /// Maintained emergency luminaires along the escape route (BS 5266 / EN 1838 practice: at each exit door, at the
        /// stairs and at changes of direction). Emissive meshes only, no Light components: the west corridor pieces
        /// already receive the URP Forward limit of 8 lights, and the ceiling spots light these walls anyway. The lenses
        /// (1.2 linear) sit just above the bloom threshold (1.0), so they glow softly without blooming into blobs; the
        /// charge LED is a faint glint at the same level. Lit() treats emission as gamma (Linear project), so the linear
        /// targets are passed through .gamma.
        /// </summary>
        static void BuildEmergencyLighting(Transform p)
        {
            // Local materials (no shared fields), so other passes can add their own emergency or sign materials freely.
            Material body = Lit("EmergencyBody", new Color(0.86f, 0.86f, 0.84f), null, null, 1f, 0.55f);       // white polycarbonate
            Material lens = Lit("EmergencyLens", new Color(0.95f, 0.95f, 0.93f), null, null, 1f, 0.75f,
                emission: (new Color(1f, 0.98f, 0.95f) * 1.2f).gamma);                                                // opal diffuser, lit (maintained)
            Material led = Lit("EmergencyChargeLed", new Color(0.1f, 0.5f, 0.15f), null, null, 1f, 0.8f,
                emission: (new Color(0.15f, 1f, 0.3f) * 1.2f).gamma);                                                 // 5 mm "charging" indicator

            // Bulkheads 2.32 m up: between the door heads (2.2 m) and the signs above them (lower edge ~2.42 m).
            const float y = 2.32f;
            foreach (float door in ClassroomDoors)
            {
                float x = door + 0.8f;
                Bulkhead(p, body, lens, led, new Vector3(x, y, 2.9f), Vector3.back);    // corridor side of each classroom door
                Bulkhead(p, body, lens, led, new Vector3(x, y, 3.1f), Vector3.forward); // room side: the way out of the room
            }
            Bulkhead(p, body, lens, led, new Vector3(15.8f, y, 0.1f), Vector3.forward);    // stairwell door
            Bulkhead(p, body, lens, led, new Vector3(31.85f, y, 1.5f), Vector3.left);      // above Exit B
            // Twin-spot opposite the stairwell, where the corridor meets the stairs: high on the wall, heads angled down.
            TwinSpot(p, body, lens, led, new Vector3(15.8f, 2.72f, 2.9f), Vector3.back);
        }

        /// <summary>Wall bulkhead: chamfered body, opal lens strip and a green charge LED. Origin on the wall face.</summary>
        static void Bulkhead(Transform p, Material body, Material lens, Material led, Vector3 wallPoint, Vector3 outward)
        {
            Transform root = EmergencyRoot(p, "Emergency Bulkhead", wallPoint, outward);
            NoShadows(ChamferBox("Body", root, new Vector3(0f, 0f, 0.025f), new Vector3(0.36f, 0.12f, 0.05f), body, 0.012f));
            NoShadows(ChamferBox("Lens", root, new Vector3(0f, 0f, 0.054f), new Vector3(0.3f, 0.072f, 0.012f), lens, 0.004f));
            NoShadows(Box("Charge LED", root, new Vector3(0.155f, -0.04f, 0.0515f), new Vector3(0.005f, 0.005f, 0.004f), led, false));
        }

        /// <summary>Twin-spot: wall box with two lamp heads angled down and out, lit lenses.</summary>
        static void TwinSpot(Transform p, Material body, Material lens, Material led, Vector3 wallPoint, Vector3 outward)
        {
            Transform root = EmergencyRoot(p, "Emergency Twin Spot", wallPoint, outward);
            NoShadows(ChamferBox("Body", root, new Vector3(0f, 0f, 0.04f), new Vector3(0.32f, 0.11f, 0.08f), body, 0.01f));
            NoShadows(Box("Charge LED", root, new Vector3(0f, -0.035f, 0.0815f), new Vector3(0.005f, 0.005f, 0.004f), led, false));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 aim = new Vector3(0.45f * side, -0.6f, 1f).normalized;
                Vector3 head = new Vector3(0.11f * side, -0.01f, 0.1f);
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, aim); // cylinder axis = local up
                NoShadows(Prim(PrimitiveType.Cylinder, "Lamp Head", root, head, new Vector3(0.07f, 0.03f, 0.07f), body, false, true, rot));
                NoShadows(Prim(PrimitiveType.Cylinder, "Lamp Lens", root, head + aim * 0.0305f, new Vector3(0.056f, 0.002f, 0.056f), lens, false, true, rot));
            }
        }

        static Transform EmergencyRoot(Transform p, string name, Vector3 wallPoint, Vector3 outward)
        {
            var root = new GameObject(name).transform;
            root.SetParent(p, false);
            root.localPosition = wallPoint;
            root.localRotation = Quaternion.LookRotation(outward); // local +z points out of the wall
            root.gameObject.isStatic = true;
            return root;
        }

        /// <summary>Wide downward spot just below a ceiling fixture: lights floor and walls, never the ceiling itself.</summary>
        static Light CeilingLight(Transform p, string name, Vector3 ceilingPoint, float intensity, float range, float kelvin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.localPosition = ceilingPoint + Vector3.down * 0.05f;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 150f;
            light.innerSpotAngle = 90f; // broad full-strength core keeps wall signs between fixtures lit
            light.color = Color.white;
            light.useColorTemperature = true;
            light.colorTemperature = kelvin;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        static void Probe(Transform p, string name, Vector3 center, Vector3 size, int importance = 1, bool boxProjection = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.localPosition = center;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            // Spread the start-up captures over a few frames instead of one long hitch.
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.size = size;
            probe.boxProjection = boxProjection;
            probe.importance = importance;
            probe.blendDistance = 0.5f;
            probe.resolution = 128;
        }
    }
}
