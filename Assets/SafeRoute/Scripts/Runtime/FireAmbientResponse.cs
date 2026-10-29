using System.Collections.Generic;
using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Presentation only: the hot smoke layer dims nearby ceiling luminaires and turns them amber as the fire patches
    /// around them grow. Reads <see cref="FlickerLight.intensityScale"/> (set by FireSpread) and never writes to the
    /// fire, so spread timing, danger zones and restarts are unaffected; a restart brightens the lights again on its own.
    /// </summary>
    public class FireAmbientResponse : MonoBehaviour
    {
        [Tooltip("Fire lights whose growth drives the response. Empty = every FlickerLight within searchRadius (XZ) at Start.")]
        public FlickerLight[] drivers;
        [Min(0f)] public float searchRadius = 3f;
        public Light[] lights;
        [Tooltip("Emissive light panels dimmed with the lights (via a property block; the material is not changed).")]
        public Renderer[] panels;
        [Range(0f, 1f)] public float minLightFactor = 0.5f;
        public float smokeKelvin = 2900f;
        [Range(0f, 1f)] public float minPanelFactor = 0.35f;
        public Color panelSmokeTint = new Color(1f, 0.82f, 0.62f);
        [Tooltip("Seconds for a full change; smooths the step when a patch ignites (30%) or an attempt restarts.")]
        [Min(0.01f)] public float responseTime = 2f;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        float[] baseIntensity, baseKelvin;
        Color[] baseEmission;
        MaterialPropertyBlock block;
        float current, applied = -1f;

        void Start()
        {
            if (drivers == null || drivers.Length == 0)
            {
                var found = new List<FlickerLight>();
                Vector2 here = new Vector2(transform.position.x, transform.position.z);
                foreach (FlickerLight f in FindObjectsByType<FlickerLight>(FindObjectsSortMode.None))
                    if (Vector2.Distance(here, new Vector2(f.transform.position.x, f.transform.position.z)) <= searchRadius)
                        found.Add(f);
                drivers = found.ToArray();
            }
            if (drivers.Length == 0)
            {
                enabled = false; // no fire nearby: leave the lights exactly as built
                return;
            }

            lights ??= new Light[0];
            panels ??= new Renderer[0];
            baseIntensity = new float[lights.Length];
            baseKelvin = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null)
                {
                    baseIntensity[i] = lights[i].intensity;
                    baseKelvin[i] = lights[i].colorTemperature;
                }
            baseEmission = new Color[panels.Length];
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] != null && panels[i].sharedMaterial != null)
                    baseEmission[i] = panels[i].sharedMaterial.GetColor(EmissionId);
            block = new MaterialPropertyBlock();
            // Start from the built look: FireSpread may not have reset the drivers' scales yet this frame.
            current = applied = 0f;
        }

        void Update()
        {
            current = Mathf.MoveTowards(current, Target(), Time.deltaTime / responseTime);
            if (Mathf.Abs(current - applied) > 0.002f) Apply(current);
        }

        /// <summary>Mean growth of the nearby fires, so the dimming deepens as each patch catches.</summary>
        float Target()
        {
            float sum = 0f;
            int n = 0;
            foreach (FlickerLight f in drivers)
                if (f != null) { sum += f.intensityScale; n++; }
            return n == 0 ? 0f : Mathf.SmoothStep(0f, 1f, sum / n);
        }

        void Apply(float k)
        {
            applied = k;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].intensity = baseIntensity[i] * Mathf.Lerp(1f, minLightFactor, k);
                lights[i].colorTemperature = Mathf.Lerp(baseKelvin[i], smokeKelvin, k);
            }
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;
                if (k <= 0.002f)
                {
                    panels[i].SetPropertyBlock(null); // back on the shared material (keeps SRP batching)
                    continue;
                }
                block.Clear();
                block.SetColor(EmissionId, baseEmission[i] * Mathf.Lerp(1f, minPanelFactor, k) * Color.Lerp(Color.white, panelSmokeTint, k));
                panels[i].SetPropertyBlock(block);
            }
        }
    }
}
