using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Smooth noise flicker for fire light. The defaults give a single slow octave at a fixed colour and position;
    /// the optional fast octave, colour coupling and position wander make a flame read as organic.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        public float baseIntensity = 3f;
        public float amplitude = 0.8f;
        public float speed = 7f;
        [Tooltip("0..1, set by FireSpread while a fire grows. At 0 the light is switched off (frees a per-object light slot).")]
        [Range(0f, 1f)] public float intensityScale = 1f;

        [Header("Optional detail (0 / off = plain flicker)")]
        [Tooltip("Second, faster noise octave, as a fraction of baseIntensity.")]
        public float fastAmplitude = 0f;
        public float fastSpeed = 23f;
        [Tooltip("Lerp the light colour from dimColor to brightColor with the flicker (real flames turn yellower as they flare).")]
        public bool tintWithIntensity = false;
        public Color dimColor = new Color(1f, 0.42f, 0.12f);
        public Color brightColor = new Color(1f, 0.66f, 0.35f);
        [Tooltip("Metres of smooth position wander around the start position (keep <= 0.05 on shadowed lights).")]
        public float jitter = 0f;

        const float JitterSpeed = 1.6f; // slow enough that moving shadows never read as camera motion

        Light lightSource;
        float seed;
        Vector3 home;

        void Awake()
        {
            lightSource = GetComponent<Light>();
            seed = Random.value * 100f;
            home = transform.localPosition;
        }

        void Update()
        {
            if (lightSource == null) lightSource = GetComponent<Light>();
            bool lit = intensityScale > 0.001f;
            if (lightSource.enabled != lit) lightSource.enabled = lit;
            if (!lit) return;
            float t = Time.time;
            float n = Mathf.PerlinNoise(t * speed, seed) - 0.5f;
            float fast = fastAmplitude > 0f ? (Mathf.PerlinNoise(t * fastSpeed, seed + 17.3f) - 0.5f) * 2f * fastAmplitude * baseIntensity : 0f;
            float swing = n * 2f * amplitude + fast;
            lightSource.intensity = Mathf.Max(0f, baseIntensity + swing) * intensityScale;

            if (tintWithIntensity)
            {
                // swing / range is -1..1 (Perlin rarely reaches the ends), mapped to 0..1 between the two colours.
                float range = amplitude + fastAmplitude * baseIntensity;
                float k = range > 1e-4f ? Mathf.Clamp01(0.5f + 0.5f * swing / range) : 0.5f;
                lightSource.color = Color.Lerp(dimColor, brightColor, k);
            }

            if (jitter > 0f)
            {
                float j = t * JitterSpeed;
                transform.localPosition = home + jitter * 2f * new Vector3(
                    Mathf.PerlinNoise(j, seed + 31f) - 0.5f,
                    Mathf.PerlinNoise(j, seed + 47f) - 0.5f,
                    Mathf.PerlinNoise(j, seed + 63f) - 0.5f);
            }
        }
    }
}
