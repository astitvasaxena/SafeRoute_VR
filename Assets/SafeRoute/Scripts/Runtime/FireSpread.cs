using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// One fire incident made of several patches that catch fire one after another and grow, so the fire visibly
    /// spreads from its origin. A patch that has not ignited yet emits nothing, so its danger zone, sound and light
    /// stay inactive (they all follow the patch's flames). Every new attempt starts the spread again from the origin.
    /// </summary>
    public class FireSpread : MonoBehaviour
    {
        [System.Serializable]
        public class Patch
        {
            public string name;
            [Tooltip("Seconds after the start of an attempt when this patch catches fire (0 = burning from the start).")]
            [Min(0f)] public float igniteAt;
            [Tooltip("Seconds from catching fire to full size.")]
            [Min(0.1f)] public float growTime = 8f;
            [Tooltip("Flames, smoke and embers of this patch (they start small and grow).")]
            public ParticleSystem[] effects;
            public FlickerLight[] lights;

            [System.NonSerialized] public float[] baseRates;
            [System.NonSerialized] public ParticleSystem.MinMaxCurve[] baseSizes;
            public float Growth { get; set; }
        }

        [Tooltip("Restarts the spread for every new attempt.")]
        public EvacuationManager manager;
        public Patch[] patches;

        float startTime;

        void OnEnable()
        {
            if (manager != null) manager.AttemptStarted += Restart;
        }

        void OnDisable()
        {
            if (manager != null) manager.AttemptStarted -= Restart;
        }

        void Start() => Restart();

        void Update() => Tick(Time.time - startTime);

        /// <summary>Puts every delayed patch back out; the origin (igniteAt 0) keeps burning.</summary>
        public void Restart()
        {
            startTime = Time.time;
            if (patches == null) return;
            foreach (Patch patch in patches)
            {
                if (patch == null) continue;
                CacheBase(patch);
                if (patch.igniteAt > 0f)
                {
                    patch.Growth = 0f;
                    foreach (ParticleSystem ps in patch.effects)
                        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                else patch.Growth = 1f;
                Apply(patch);
            }
        }

        /// <summary>Advances the spread to <paramref name="elapsed"/> seconds after the attempt started.</summary>
        public void Tick(float elapsed)
        {
            if (patches == null) return;
            foreach (Patch patch in patches)
            {
                if (patch == null) continue;
                CacheBase(patch);
                float growth = patch.igniteAt <= 0f ? 1f : Mathf.Clamp01((elapsed - patch.igniteAt) / patch.growTime);
                bool ignite = elapsed >= patch.igniteAt && patch.Growth <= 0f;
                // A small but visible start (15%), then an eased growth to full size.
                patch.Growth = elapsed < patch.igniteAt ? 0f : Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, growth));
                if (ignite)
                    foreach (ParticleSystem ps in patch.effects)
                        if (ps != null && !ps.isPlaying) ps.Play(true);
                Apply(patch);
            }
        }

        static void CacheBase(Patch patch)
        {
            if (patch.effects == null || (patch.baseRates != null && patch.baseRates.Length == patch.effects.Length)) return;
            patch.baseRates = new float[patch.effects.Length];
            patch.baseSizes = new ParticleSystem.MinMaxCurve[patch.effects.Length];
            for (int i = 0; i < patch.effects.Length; i++)
            {
                if (patch.effects[i] == null) continue;
                patch.baseRates[i] = patch.effects[i].emission.rateOverTimeMultiplier;
                patch.baseSizes[i] = patch.effects[i].main.startSize;
            }
        }

        /// <summary>
        /// Scales a size curve. startSizeMultiplier alone would only scale the upper constant of a "random between two
        /// constants" range, so both ends are scaled explicitly.
        /// </summary>
        static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve c, float k)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.TwoConstants: return new ParticleSystem.MinMaxCurve(c.constantMin * k, c.constantMax * k);
                case ParticleSystemCurveMode.Constant: return new ParticleSystem.MinMaxCurve(c.constant * k);
                default:
                    c.curveMultiplier *= k; // curve modes: the multiplier scales the whole curve
                    return c;
            }
        }

        static void Apply(Patch patch)
        {
            if (patch.effects != null)
                for (int i = 0; i < patch.effects.Length; i++)
                {
                    ParticleSystem ps = patch.effects[i];
                    if (ps == null) continue;
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier = patch.baseRates[i] * patch.Growth;
                    var main = ps.main;
                    main.startSize = Scaled(patch.baseSizes[i], Mathf.Lerp(0.5f, 1f, patch.Growth));
                }
            if (patch.lights != null)
                foreach (FlickerLight light in patch.lights)
                    if (light != null) light.intensityScale = patch.Growth;
        }
    }
}
