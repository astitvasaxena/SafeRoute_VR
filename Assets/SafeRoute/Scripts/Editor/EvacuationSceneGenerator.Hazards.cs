using System.Collections.Generic;
using SafeRoute.Evacuation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        // Built with the fire; wired to the restart sequence and the manager in BuildEvacuationSystem.
        static readonly List<FireDangerZone> fireDangers = new List<FireDangerZone>();
        static FireSpread fireSpread;

        /// <summary>
        /// One burning area of the fire incident. Every patch is built by the same code (BuildFirePatch); the values
        /// below are the only differences. Positions are world coordinates (the Hazards root sits at the origin).
        /// </summary>
        class FirePatch
        {
            public string Name;
            public Vector3 Base;                          // bottom centre of the flames
            public Vector2 Footprint = new Vector2(0.9f, 1.5f); // flame area, x and z (metres)
            public float FlameHeight = 1f;                // 1 = the Exit A door fire (~1 m flames)
            public float Intensity = 1f;                  // flame density; 0 = smoke only
            public float Smoke = 1f;                      // smoke plume density; 0 = none
            public float SmokeDarkness;                   // 0 grey (wood) .. 1 black (plastics, paper)
            public Vector3 SmokeDrift = new Vector3(0.12f, 0f, 0f);
            public bool Embers = true;                    // sparks
            public float Light;                           // own fire light intensity; 0 = lit by a neighbour
            public float LightRange = 3f;
            public Vector3 LightOffset = new Vector3(0f, 0.9f, 0f);
            public float Audio;                           // crackle volume; 0 = heard via a neighbour
            public float DangerRadius;                    // fire contact radius; 0 = decorative only
            public Vector3 DangerOffset;
            public float IgniteAt, GrowTime = 8f;         // seconds after the attempt starts / to full size
            public string Reason;

            // ---- Look only (no effect on positions, timing or danger) ----
            public float Flicker = 1f;                    // tongue agitation: noise frequency x, lifetime / sqrt
            public Color FlameTint = Color.white;         // multiplies the flame colour gradient (fuel colour)
            public Vector3 FlameFlow;                     // extra flame velocity (ceiling rollover)
            public float FlameRoll = 0.15f;               // tongue start-rotation range (radians)
            public float TongueRate = 1f, CoreShare = 1f; // tongue and core density multipliers
            public bool Ash;                              // burning paper flakes
            public float GlowBed;                         // floor glow diameter (metres); 0 = none
            public Vector3 GlowOffset;
            // Fire light shape and flicker (FlickerLight).
            public LightType LightType = LightType.Point;
            public Vector3 LightAim = Vector3.down;       // spot direction
            public float SpotAngle = 120f, InnerSpotAngle = 30f;
            public bool LightShadows;
            public float FlickerSpeed = 7f, FlickerAmount = 0.25f, FlickerFast, FlickerFastSpeed = 23f, LightJitter;
            public Color LightDim = new Color(1f, 0.42f, 0.12f), LightBright = new Color(1f, 0.66f, 0.35f);
        }

        /// <summary>
        /// The incident started at Exit A and is spreading east along the west end of the corridor: stored boxes (south
        /// wall), then a notice board (north wall), then the ceiling (burning in the gap, then the debris that fell to
        /// the floor), while smoke leaks out of the lift shaft. Every danger zone ends at least ~1 m inside the Exit A
        /// warning zone (x 0.15..7.4), so the warning always comes first, and the route to Exit B (x >= 9.8) keeps a wide
        /// margin.
        /// Visibility from the route: the trainee looks west from the corridor about 12-20 s into an attempt, with the
        /// instruction panel covering the view from ~6 degrees below eye level down. So every patch is fully grown by
        /// ~17 s and each one has flames reaching well above ~1 m (boxes, board) or burns overhead (ceiling), on its own
        /// side and height of the corridor, instead of floor-level fires lined up behind the Exit A fire.
        /// </summary>
        static FirePatch[] FirePatches() => new[]
        {
            new FirePatch
            {
                // The only shadowed fire light: a wide spot ~0.25 m in front of the doors aimed east down the corridor, so
                // the corridor walls stop it at the classrooms (the old unshadowed point light leaked through into
                // Classroom 1) and the debris and boxes throw long, gently moving shadows toward the trainee. It replaces
                // the point light (the west corridor pieces are already at the 8-lights-per-object limit).
                Name = "Exit A Fire (origin)", Base = new Vector3(0.75f, 0.08f, 1.5f), Light = 8f, LightRange = 8f,
                LightOffset = new Vector3(-0.35f, 1.25f, 0f), LightType = LightType.Spot, LightAim = new Vector3(1f, -0.2f, 0f),
                SpotAngle = 120f, InnerSpotAngle = 30f, LightShadows = true,
                FlickerSpeed = 5f, FlickerAmount = 0.22f, FlickerFast = 0.1f, FlickerFastSpeed = 19f, LightJitter = 0.04f,
                LightDim = new Color(1f, 0.36f, 0.08f), LightBright = new Color(1f, 0.6f, 0.26f),
                SmokeDarkness = 0.55f, GlowBed = 1.6f, GlowOffset = new Vector3(0.2f, 0f, 0f), // bed ends at the door wall (x 0.15)
                Audio = 0.7f, DangerRadius = 1.4f, DangerOffset = new Vector3(0.15f, 0f, 0f),
                Reason = "fire contact at Exit A",
            },
            // Fire lights of the younger patches sit on the corridor side and are short-ranged, so no orange glow
            // leaks through the corridor wall (z 2.9..3.1) into Classroom 1, where the trainee starts.
            new FirePatch
            {
                // The pile stands against the south wall at x 2.4..3.5. Its flames burn on the pile's east face (x 3.6..3.85),
                // the side facing the trainee coming from the east, so from the route they appear ~11 deg left of the Exit A
                // door instead of in front of it (where they merged with the door fire). They end before the wall end at
                // x 4.0: beside the lift-alcove opening the danger zone (clipped to the corridor) could not cover them.
                // They stay 0.14-0.39 m off the boxes behind them along the view: the fire material's 0.25 m soft-particle
                // fade would fade flames pressed against the boxes. They start at the upper tier (0.8 m) and reach ~2.8 m,
                // so the bright part is above the instruction panel (it covers the view from ~6 deg below eye level down).
                Name = "Stored Boxes Fire", Base = new Vector3(3.725f, 0.8f, 0.55f), Footprint = new Vector2(0.25f, 0.6f),
                FlameHeight = 2f, Intensity = 1.2f, Smoke = 0.8f, SmokeDarkness = 0.75f, Light = 3.4f, LightRange = 2.2f,
                LightOffset = new Vector3(-0.425f, 0.6f, 0.35f), Audio = 0.45f, DangerRadius = 1f, DangerOffset = new Vector3(-0.725f, 0f, 0.05f),
                IgniteAt = 3f, GrowTime = 6f, Reason = "fire contact at the burning storage boxes",
                // Cardboard: vigorous, yellow-rich and bright, with a fast flicker.
                Flicker = 1.1f, FlameTint = new Color(1f, 0.95f, 0.85f), CoreShare = 1.2f,
                FlickerSpeed = 8.5f, FlickerAmount = 0.24f, FlickerFast = 0.1f, FlickerFastSpeed = 23f, LightJitter = 0.04f,
                LightDim = new Color(1f, 0.45f, 0.14f), LightBright = new Color(1f, 0.7f, 0.36f),
            },
            new FirePatch
            {
                // Flames rise across the whole board face from its lower edge (y 1.1) to above its top (~2.3 m).
                Name = "Notice Board Fire", Base = new Vector3(3.8f, 1.1f, 2.79f), Footprint = new Vector2(1.15f, 0.1f),
                FlameHeight = 1f, Intensity = 1.6f, Smoke = 0.5f, SmokeDarkness = 0.8f,
                DangerRadius = 0.8f, DangerOffset = new Vector3(0f, 0f, -0.37f), IgniteAt = 6f, GrowTime = 6f,
                Reason = "fire contact at the burning notice board",
                // Paper on cork: fast, thin, short-lived tongues that shed glowing ash.
                Flicker = 1.6f, FlameTint = new Color(1f, 0.9f, 0.7f), CoreShare = 0.6f, Ash = true,
            },
            new FirePatch
            {
                // Flames licking along the charred ceiling gap (from y 2.6 up to the ceiling at 2.98; soft particles fade the
                // contact): overhead, above the instruction panel and out of reach, so it has no danger zone of its own; the
                // debris it dropped (next patch) does. It burns in the north half of the gap (z 2.05..2.65), so the hanging
                // DANGER - SMOKE sign east of the gap (Signage: x 6.2, z 0.7..2.3; keep these in sync) does not hide it
                // from the corridor.
                Name = "Ceiling Void Fire", Base = new Vector3(5.5f, 2.6f, 2.35f), Footprint = new Vector2(0.55f, 0.6f),
                FlameHeight = 0.5f, Intensity = 1f, Smoke = 0f, Embers = false, IgniteAt = 9f, GrowTime = 5f,
                // Rollover: oxygen-starved, deeper orange tongues that lean and stream east along the gap. Flow x lifetime
                // (0.45 m/s x <= 0.67 s) keeps them inside the charred gap (x 5.19..5.81), clear of the hanging sign at x 6.2.
                FlameFlow = new Vector3(0.45f, 0f, 0f), FlameRoll = 0.6f, FlameTint = new Color(1f, 0.75f, 0.5f), CoreShare = 0.5f,
            },
            new FirePatch
            {
                // Its light sits between the floor debris and the ceiling fire and lights both.
                Name = "Fallen Ceiling Fire", Base = new Vector3(5.5f, 0.04f, 2.05f), Footprint = new Vector2(0.8f, 0.7f),
                FlameHeight = 0.7f, Intensity = 0.85f, Smoke = 0.7f, SmokeDarkness = 0.55f, Light = 1.8f, LightRange = 1.7f,
                LightOffset = new Vector3(0f, 1.66f, -0.6f), DangerRadius = 0.75f, IgniteAt = 11f, GrowTime = 6f,
                Reason = "fire contact at the burning ceiling debris",
                // Smouldering tiles and insulation: a low glowing bed, fewer, redder and lazier tongues, slow deep flicker.
                Flicker = 0.8f, FlameTint = new Color(1f, 0.6f, 0.35f), CoreShare = 1.3f, TongueRate = 0.7f, GlowBed = 1.2f,
                FlickerSpeed = 3.5f, FlickerAmount = 0.18f, FlickerFast = 0.06f, FlickerFastSpeed = 13f, LightJitter = 0.03f,
                LightDim = new Color(1f, 0.3f, 0.06f), LightBright = new Color(1f, 0.5f, 0.2f),
            },
            new FirePatch
            {
                // Thin, cool-grey wisps escaping the door seams (see BuildFirePatch: smoke-only patches).
                Name = "Lift Shaft Smoke", Base = new Vector3(5.5f, 0.9f, -1.75f), Footprint = new Vector2(1.1f, 0.15f),
                Intensity = 0f, Smoke = 0.6f, SmokeDarkness = 0.5f, SmokeDrift = new Vector3(0f, 0f, 0.1f), Embers = false,
                IgniteAt = 12f, GrowTime = 8f,
            },
        };

        static void BuildHazards(Transform p)
        {
            fireDangers.Clear();
            var spreadPatches = new List<FireSpread.Patch>();
            foreach (FirePatch patch in FirePatches())
                spreadPatches.Add(BuildFirePatch(p, patch, out _));
            fireSpread = p.gameObject.AddComponent<FireSpread>();
            fireSpread.patches = spreadPatches.ToArray();

            ExitADebris(Group("Exit A Debris", p));
            StoredBoxes(Group("Stored Boxes (burning)", p));
            BurningNoticeBoard(Group("Notice Board (burning)", p));
            FallenCeiling(Group("Fallen Ceiling (burning)", p));

            // Dark stratified layer banked under the ceiling along the whole fire-affected end of the corridor (hot
            // fire smoke is grey-brown to black; it also backs up the "stay low" message). Flat quads never pierce the
            // ceiling or hang to head height (2.68 m, well above a 1.6-1.8 m headset).
            var layer = Particles("Ceiling Smoke Layer", p, new Vector3(3.3f, 2.68f, 1.5f), mSmokeParticles);
            var main = layer.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 11f);
            // No start speed and a narrow emitter: centre z 1.5 +/- 0.3 plus half-size <= 0.58 (z 0.62..2.38) keeps
            // every quad between the corridor walls (z 0.1..2.9), so no smoke slab shows through into the classrooms.
            // The SmokePuff_v5 puff fills ~75% of its quad (v4: ~45%), so ~25% smaller quads keep the same coverage
            // with less overdraw.
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.85f, 1.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.19f, 0.18f, 0.42f), new Color(0.3f, 0.29f, 0.28f, 0.34f));
            main.maxParticles = 40;
            var emission = layer.emission;
            emission.rateOverTime = 4f;
            var shape = layer.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6.2f, 0.15f, 0.6f);
            Drift(layer, new Vector3(0.06f, 0f, 0f));
            SmokeTint(layer, new Color(0.25f, 0.24f, 0.23f), 0.25f);
            Grow(layer, 0.8f, 1f);
            Flipbook(layer, smokeTiles, 0.6f, true);
            Roll(layer, 0.15f);
            layer.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

            BuildFireDamage(p);
        }

        /// <summary>
        /// Builds one fire patch from its description: a dense flame core, animated tongues, sparks (and paper ash),
        /// a floor glow bed, a one-shot ignition flare, the smoke plume, the flickering light, crackle and the
        /// fire-contact danger zone. Values scale from the Exit A fire (FlameHeight 1, Intensity 1, Smoke 1).
        /// A patch that ignites later starts stopped (no prewarm); FireSpread lights it and grows it. Every emitter
        /// goes into the patch's effects, so it ignites, grows and resets with the patch.
        /// </summary>
        static FireSpread.Patch BuildFirePatch(Transform p, FirePatch f, out ParticleSystem flames)
        {
            Transform root = Group(f.Name, p);
            var effects = new List<ParticleSystem>();
            flames = null;
            bool delayed = f.IgniteAt > 0f;
            float h = Mathf.Sqrt(Mathf.Max(0.1f, f.FlameHeight));
            float area = Mathf.Max(0.05f, f.Footprint.x * f.Footprint.y / (0.9f * 1.5f)); // relative to the Exit A fire

            if (f.Intensity > 0f)
            {
                if (f.GlowBed > 0f)
                {
                    // Warm light pool on the floor under the fire, so the flames look rooted in the fuel. Evenly spaced
                    // particles with triangular fades overlap into a steady glow that grows with the patch's emission.
                    var bed = Particles($"{f.Name} - Glow Bed", root, new Vector3(f.Base.x + f.GlowOffset.x, 0.015f, f.Base.z + f.GlowOffset.z), mFireGlowBed);
                    var bedMain = bed.main;
                    bedMain.startLifetime = 2f;
                    bedMain.startSpeed = 0f;
                    bedMain.startSize = new ParticleSystem.MinMaxCurve(0.95f * f.GlowBed, 1.05f * f.GlowBed);
                    bedMain.startColor = new Color(1f, 1f, 1f, 0.24f);
                    bedMain.maxParticles = 8;
                    var bedEmission = bed.emission;
                    bedEmission.rateOverTime = 3f;
                    var bedShape = bed.shape;
                    bedShape.enabled = false;
                    var bedColour = bed.colorOverLifetime;
                    bedColour.enabled = true;
                    var bedFade = new Gradient();
                    bedFade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
                    bedColour.color = bedFade;
                    var bedRenderer = bed.GetComponent<ParticleSystemRenderer>();
                    bedRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                    bedRenderer.sortingFudge = 20f; // drawn before the flames and smoke above it
                    effects.Add(bed);
                }

                // CORE: a dense, bright bed where fuel meets flame (HDR material, the only flame layer that blooms).
                var shapeScale = new Vector3(f.Footprint.x, f.Footprint.y, 0.1f);
                float coreSize = Mathf.Clamp(Mathf.Max(f.Footprint.x, f.Footprint.y) * 0.6f, 0.35f, 0.85f);
                var core = Particles($"{f.Name} - Flame Core", root, f.Base + new Vector3(0f, 0.04f * h, 0f), mFireCoreParticles);
                var main = core.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f * h, 0.3f * h);
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f * coreSize, 1.1f * coreSize);
                main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
                float coreRate = Mathf.Max(6f, 12f * Mathf.Sqrt(area) * f.Intensity) * f.CoreShare;
                main.maxParticles = Mathf.CeilToInt(coreRate * 0.65f) + 2;
                var emission = core.emission;
                emission.rateOverTime = coreRate;
                var shape = core.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                shape.scale = Vector3.Scale(shapeScale, new Vector3(0.85f, 0.85f, 1f));
                Grow(core, 0.7f, 1.15f);
                CoreGradient(core, 1f);
                Flipbook(core);
                var coreRenderer = core.GetComponent<ParticleSystemRenderer>();
                coreRenderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
                coreRenderer.sortingFudge = -12f;
                effects.Add(core);

                // TONGUES: fewer, larger, animated (frame-blended flipbook: 4x4, or the 8x8 Unity Labs sheet) tongues that widen as they leave the core
                // and narrow to a tip. These stay "the flames" for the danger zone and the audio. Uniform startSize on
                // purpose: FireSpread scales main.startSize, which would only scale X with startSize3D.
                flames = Particles($"{f.Name} - Flames", root, f.Base, mFireParticles);
                main = flames.main;
                float life = 0.95f * h / Mathf.Sqrt(f.Flicker);
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f * h / Mathf.Sqrt(f.Flicker), life);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * h, 1.4f * h);
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f * h, 1f * h);
                main.startRotation = new ParticleSystem.MinMaxCurve(-f.FlameRoll, f.FlameRoll);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.7f), new Color(1f, 0.6f, 0.35f));
                float rate = Mathf.Max(8f, 45f * 0.55f * Mathf.Sqrt(area) * f.Intensity) * f.TongueRate;
                main.maxParticles = Mathf.CeilToInt(rate * life * 1.25f) + 4;
                emission = flames.emission;
                emission.rateOverTime = rate;
                shape = flames.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                shape.scale = shapeScale;
                FlameGradient(flames, f.FlameTint);
                var size = flames.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0.3f)));
                Flipbook(flames, flameTiles, flameSheets, true);
                if (f.FlameFlow != Vector3.zero) Drift(flames, f.FlameFlow);
                var flameRenderer = flames.GetComponent<ParticleSystemRenderer>();
                flameRenderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
                flameRenderer.sortingFudge = -10f; // draw after the smoke so the fire is never hidden by it
                var noise = flames.noise;
                noise.enabled = true;
                noise.strength = 0.45f * h;
                noise.frequency = 1.6f * f.Flicker;
                noise.scrollSpeed = 1.4f;
                effects.Add(flames);

                if (f.Embers)
                {
                    // SPARKS: tiny, hot (bloom) streaks on curly, darting, buoyant paths. Burstiness without emission
                    // bursts (FireSpread scales the rate, not bursts): a random rate between 0 and 2x. The lifetime is
                    // capped so even the fastest spark dies below the ceiling.
                    var sparks = Particles($"{f.Name} - Sparks", root, f.Base + new Vector3(0.05f, 0.12f, 0f), mSparkParticles);
                    main = sparks.main;
                    const float buoyancy = 0.15f;
                    float vMax = 2f * h, accel = buoyancy * 9.81f;
                    float headroom = Mathf.Max(0.3f, CeilingHeight - 0.1f - (f.Base.y + 0.12f));
                    float cap = Mathf.Clamp((Mathf.Sqrt(vMax * vMax + 2f * accel * headroom) - vMax) / accel * 0.85f, 0.3f, 1.2f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f * cap, cap);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * h, vMax);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
                    main.startRotation = 0f;
                    main.startColor = Color.white;
                    main.gravityModifier = -buoyancy;
                    float sparkRate = Mathf.Max(3f, 12f * Mathf.Sqrt(area) * f.Intensity);
                    main.maxParticles = Mathf.CeilToInt(2f * sparkRate * cap) + 2;
                    emission = sparks.emission;
                    emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f, 2f * sparkRate);
                    shape = sparks.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    shape.scale = new Vector3(f.Footprint.x + 0.1f, f.Footprint.y + 0.1f, 0.2f);
                    var sparkColour = sparks.colorOverLifetime;
                    sparkColour.enabled = true;
                    var cooling = new Gradient();
                    cooling.SetKeys(
                        new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f), new GradientColorKey(new Color(0.6f, 0.1f, 0.02f), 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(0f, 1f) });
                    sparkColour.color = cooling;
                    var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
                    sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                    sparkRenderer.velocityScale = 0.08f;
                    sparkRenderer.lengthScale = 2f;
                    sparkRenderer.sortingFudge = -10f;
                    noise = sparks.noise;
                    noise.enabled = true;
                    noise.strength = 1.2f;
                    noise.frequency = 2.5f;
                    noise.scrollSpeed = 1f;
                    noise.octaveCount = 2;
                    effects.Add(sparks);
                }

                if (f.Ash)
                {
                    // Burning paper flakes: lift off with a glowing rim, go dark, tumble and drift down into the corridor.
                    var ash = Particles($"{f.Name} - Ash", root, f.Base + new Vector3(0f, 0.3f, -0.05f), mAshParticles);
                    main = ash.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                    main.startColor = Color.white;
                    main.gravityModifier = 0.03f; // rises ~0.25 m, then sinks slowly
                    main.maxParticles = 16;
                    emission = ash.emission;
                    emission.rateOverTime = 4f;
                    shape = ash.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    shape.scale = new Vector3(f.Footprint.x, f.Footprint.y, 0.4f);
                    shape.randomDirectionAmount = 0.3f;
                    Drift(ash, new Vector3(0.05f, 0f, -0.08f));
                    var ashColour = ash.colorOverLifetime;
                    ashColour.enabled = true;
                    var burnOut = new Gradient();
                    burnOut.SetKeys(
                        new[] { new GradientColorKey(new Color(0.95f, 0.4f, 0.1f), 0f), new GradientColorKey(new Color(0.95f, 0.4f, 0.1f), 0.1f), new GradientColorKey(new Color(0.11f, 0.1f, 0.09f), 0.25f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.05f), new GradientAlphaKey(0.9f, 0.8f), new GradientAlphaKey(0f, 1f) });
                    ashColour.color = burnOut;
                    Roll(ash, 3f);
                    Flipbook(ash);
                    noise = ash.noise;
                    noise.enabled = true;
                    noise.strength = 0.25f;
                    noise.frequency = 0.8f;
                    effects.Add(ash);
                }

                if (delayed)
                {
                    // IGNITION: a one-shot flare of the core as the fuel catches (no brighter than the steady core, so
                    // not a flash). Bursts ignore FireSpread's rate scaling; start sizes are 1.5x because FireSpread
                    // scales them to 0.65 at the moment of ignition.
                    var flare = OneShot($"{f.Name} - Ignition Flare", root, f.Base + new Vector3(0f, 0.04f * h, 0f), mFireCoreParticles,
                        new ParticleSystem.Burst(0f, 6), new ParticleSystem.Burst(0.25f, 4));
                    main = flare.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * h, 1.1f * h);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.3f * 1.5f * 0.8f * coreSize, 1.3f * 1.5f * 1.1f * coreSize);
                    main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
                    main.maxParticles = 10;
                    shape = flare.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    shape.scale = shapeScale;
                    Grow(flare, 0.8f, 1.2f);
                    CoreGradient(flare, 0.8f);
                    Flipbook(flare);
                    var flareRenderer = flare.GetComponent<ParticleSystemRenderer>();
                    flareRenderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    flareRenderer.sortingFudge = -12f;
                    effects.Add(flare);

                    if (f.Smoke > 0f)
                    {
                        // ...and a puff of dark smoke from the fuel.
                        var puff = OneShot($"{f.Name} - Ignition Smoke", root, f.Base + new Vector3(0f, 0.3f * h, 0f), mSmokeParticles,
                            new ParticleSystem.Burst(0.3f, 5));
                        main = puff.main;
                        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
                        main.startSpeed = 0f;
                        main.startSize = new ParticleSystem.MinMaxCurve(0.45f * 1.5f, 0.68f * 1.5f); // ~25% smaller: SmokePuff_v5 fills its quad
                        main.startColor = new Color(0.12f, 0.11f, 0.1f, 0.5f);
                        main.maxParticles = 5;
                        shape = puff.shape;
                        shape.shapeType = ParticleSystemShapeType.Box;
                        shape.rotation = new Vector3(-90f, 0f, 0f);
                        shape.scale = shapeScale;
                        var puffRise = puff.velocityOverLifetime;
                        puffRise.enabled = true;
                        puffRise.space = ParticleSystemSimulationSpace.World;
                        puffRise.x = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 0f));
                        puffRise.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 0f));
                        puffRise.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 0f));
                        SmokeTint(puff, new Color(0.12f, 0.11f, 0.1f), 0.15f);
                        Grow(puff, 0.7f, 1.4f);
                        Flipbook(puff, smokeTiles, 0.3f, true);
                        Roll(puff, 0.15f);
                        effects.Add(puff);
                    }
                }
            }

            if (f.Smoke > 0f)
            {
                // Dark plume that rises in the first third of its life, stops under the ceiling and spreads along it as a
                // ceiling jet (no collision needed); the low air stays clearer ("stay low"), and the flames and signs stay
                // visible (the smoke-proof signs draw over the particles). Darker smoke for plastics and paper.
                bool smokeOnly = f.Intensity <= 0f;
                Vector3 above = smokeOnly ? Vector3.zero : new Vector3(0.15f, 0.82f * f.FlameHeight, 0f);
                var smoke = Particles($"{f.Name} - Smoke", root, f.Base + above, mSmokeParticles);
                var main = smoke.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 8f);
                main.startSpeed = smokeOnly ? new ParticleSystem.MinMaxCurve(0f) : new ParticleSystem.MinMaxCurve(0f, 0.02f);
                float size = Mathf.Lerp(0.6f, 1f, f.Smoke);
                // Smoke-only (lift doors): thinner, cooler-grey wisps escaping the door seams. Sizes are ~25% below the
                // SmokePuff_v4 values: the v5 puff fills ~75% of its quad, so coverage holds with less overdraw.
                main.startSize = smokeOnly ? new ParticleSystem.MinMaxCurve(0.38f, 0.6f) : new ParticleSystem.MinMaxCurve(0.6f * size, 0.98f * size);
                Color dark = new Color(0.08f, 0.075f, 0.07f);
                Color lightA = smokeOnly ? new Color(0.35f, 0.36f, 0.37f) : new Color(0.42f, 0.41f, 0.4f);
                Color lightB = smokeOnly ? new Color(0.42f, 0.43f, 0.44f) : new Color(0.5f, 0.49f, 0.47f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    WithAlpha(Color.Lerp(lightA, dark, f.SmokeDarkness), 0.47f),
                    WithAlpha(Color.Lerp(lightB, dark, f.SmokeDarkness), 0.38f));
                main.maxParticles = Mathf.CeilToInt(45f * f.Smoke);
                var emission = smoke.emission;
                emission.rateOverTime = 6f * f.Smoke;
                var shape = smoke.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                shape.scale = new Vector3(f.Footprint.x * 0.9f, f.Footprint.y * 0.93f, 0.3f);
                // Rise whose integral over the longest life (8 s) equals the headroom to 0.35 m below the ceiling, then
                // lateral spread (drift x4) under the ceiling.
                const float longestLife = 8f;
                float headroom = Mathf.Max(0.2f, CeilingHeight - 0.35f - (f.Base.y + above.y));
                float peak = 2f * headroom / (0.35f * longestLife);
                var rise = smoke.velocityOverLifetime;
                rise.enabled = true;
                rise.space = ParticleSystemSimulationSpace.World;
                rise.x = new ParticleSystem.MinMaxCurve(1f, Spread(f.SmokeDrift.x));
                rise.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, peak), new Keyframe(0.35f, 0f), new Keyframe(1f, 0f)));
                rise.z = new ParticleSystem.MinMaxCurve(1f, Spread(f.SmokeDrift.z));
                SmokeTint(smoke, Color.Lerp(lightA, new Color(0.18f, 0.17f, 0.16f), f.SmokeDarkness), 0.15f);
                Grow(smoke, 0.6f, 2f);
                Flipbook(smoke, smokeTiles, 0.6f, true);
                Roll(smoke, 0.15f);
                effects.Add(smoke);
            }

            if (delayed)
                foreach (ParticleSystem ps in effects)
                {
                    var main = ps.main;
                    main.playOnAwake = false; // FireSpread lights it; no prewarm so it starts small
                    main.prewarm = false;
                }

            var lights = new List<FlickerLight>();
            if (f.Light > 0f)
            {
                // Warm, flickering and short-ranged so the glow stays local. Points are unshadowed (a flickering point
                // shadow is 6 faces per frame); only the Exit A spot casts (soft, 256 px) shadows.
                var glow = new GameObject($"{f.Name} - Light");
                glow.transform.SetParent(root, false);
                glow.transform.localPosition = f.Base + f.LightOffset;
                var light = glow.AddComponent<Light>();
                light.type = f.LightType;
                light.color = Color.Lerp(f.LightDim, f.LightBright, 0.5f);
                light.intensity = f.Light;
                light.range = f.LightRange;
                if (f.LightType == LightType.Spot)
                {
                    glow.transform.localRotation = Quaternion.LookRotation(f.LightAim);
                    light.spotAngle = f.SpotAngle;
                    light.innerSpotAngle = f.InnerSpotAngle;
                }
                light.shadows = f.LightShadows ? LightShadows.Soft : LightShadows.None;
                if (f.LightShadows)
                {
                    light.shadowStrength = 0.85f;
                    light.shadowNearPlane = 0.2f;
                    light.shadowResolution = LightShadowResolution.Medium;
                    // URP reads the resolution tier from its light data (default High = 512 px; it has no setter);
                    // Medium = 256 px is enough for soft fire shadows. Depth/normal bias stay on the pipeline settings.
                    var data = new SerializedObject(light.GetUniversalAdditionalLightData());
                    data.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                light.enabled = !delayed;
                // Two-octave flicker (slow swell + fast flutter, <= ~35% in total), yellower when brighter, and a few
                // centimetres of source wander; each patch has its own speed and colour for its fuel.
                var flicker = glow.AddComponent<FlickerLight>();
                flicker.baseIntensity = f.Light;
                flicker.amplitude = f.Light * f.FlickerAmount;
                flicker.speed = f.FlickerSpeed;
                flicker.fastAmplitude = f.FlickerFast;
                flicker.fastSpeed = f.FlickerFastSpeed;
                flicker.tintWithIntensity = true;
                flicker.dimColor = f.LightDim;
                flicker.brightColor = f.LightBright;
                flicker.jitter = f.LightJitter;
                flicker.intensityScale = delayed ? 0f : 1f;
                lights.Add(flicker);
            }

            if (f.Audio > 0f && flames != null)
                BuildFireAudio(root, flames, f.Base + new Vector3(0.05f, 0.52f, 0f), f.Audio);

            if (f.DangerRadius > 0f && flames != null)
            {
                // Fire contact: a vertical cylinder at floor level. It only counts while these flames burn.
                var danger = new GameObject($"{f.Name} - Danger Zone (fire contact = fail)");
                danger.transform.SetParent(root, false);
                danger.transform.localPosition = new Vector3(f.Base.x + f.DangerOffset.x, 0f, f.Base.z + f.DangerOffset.z);
                var zone = danger.AddComponent<FireDangerZone>();
                zone.fire = flames;
                zone.radius = f.DangerRadius;
                zone.reason = f.Reason;
                fireDangers.Add(zone);
            }

            return new FireSpread.Patch
            {
                name = f.Name, igniteAt = f.IgniteAt, growTime = f.GrowTime,
                effects = effects.ToArray(), lights = lights.ToArray(),
            };
        }

        /// <summary>Generation report: every patch, whether its danger zone is fully wired, and the route clearance check.</summary>
        static string FireReport(Transform[] route)
        {
            if (fireSpread == null || fireSpread.patches == null) return "Fire spread: NO";
            var lines = new List<string> { $"Fire spread: {fireSpread.patches.Length} patches{(fireSpread.manager != null ? "" : " (NOT linked to the manager)")}" };
            foreach (FireSpread.Patch patch in fireSpread.patches)
            {
                FireDangerZone zone = fireDangers.Find(z => z.fire != null && System.Array.IndexOf(patch.effects, z.fire) >= 0);
                string danger = zone == null ? "decorative (no danger zone)"
                    : zone.restart != null && zone.restart.rig != null && zone.body != null && zone.limitTo != null
                        ? $"danger radius {zone.radius} m" : "danger zone NOT wired (check rig/CharacterController)";
                lines.Add($"  {patch.name}: ignites at {patch.igniteAt:0} s, {patch.effects.Length} effects, {patch.lights.Length} light(s), {danger}");
            }
            bool clear = EvacuationManager.ValidateFireClearance(route, fireDangers.ToArray(), EvacuationManager.FireClearance, out string problem);
            lines.Add($"Route clear of fire: {(clear ? $"OK - at least {EvacuationManager.FireClearance} m from every danger zone" : "FAILED - " + problem)}");
            return string.Join("\n", lines);
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // ---------- Fuel and fire damage (decorative, no colliders) ----------

        // Char is ash-flecked (mCharAsh); only fuel that is still burning, where the flames are, keeps the sparse glowing
        // cracks (mEmber), so the props read as charred and smouldering rather than as evenly glowing "magma" blocks.

        /// <summary>Burning and charred debris in front of the door.</summary>
        static void ExitADebris(Transform p)
        {
            Box("Charred Debris", p, new Vector3(0.55f, 0.18f, 0.95f), new Vector3(0.7f, 0.36f, 0.5f), mCharAsh, false, true, Quaternion.Euler(0f, 18f, 0f));
            Box("Charred Debris", p, new Vector3(0.65f, 0.12f, 2.05f), new Vector3(0.55f, 0.24f, 0.6f), mCharAsh, false, true, Quaternion.Euler(0f, -25f, 6f));
            Box("Charred Debris", p, new Vector3(1.3f, 0.06f, 1.4f), new Vector3(0.9f, 0.08f, 0.12f), mCharAsh, false, true, Quaternion.Euler(0f, 40f, 0f));
            Box("Burning Debris", p, new Vector3(0.6f, 0.3f, 1.1f), new Vector3(0.35f, 0.16f, 0.3f), mEmber, false, true, Quaternion.Euler(0f, -14f, 5f));
            Box("Burning Debris", p, new Vector3(0.7f, 0.2f, 1.9f), new Vector3(0.3f, 0.12f, 0.35f), mEmber, false, true, Quaternion.Euler(0f, 32f, -4f));
            Box("Burning Debris", p, new Vector3(1.15f, 0.08f, 1.5f), new Vector3(0.25f, 0.1f, 0.25f), mEmber, false, true, Quaternion.Euler(0f, 8f, 0f));
        }

        /// <summary>Cardboard boxes stacked on a pallet against the south wall: the fuel the fire has reached.</summary>
        static void StoredBoxes(Transform p)
        {
            Box("Pallet", p, new Vector3(2.9f, 0.06f, 0.5f), new Vector3(1f, 0.12f, 0.7f), mCharAsh, false, true, Quaternion.Euler(0f, 3f, 0f));
            Box("Cardboard Box (burnt out)", p, new Vector3(2.6f, 0.33f, 0.45f), new Vector3(0.48f, 0.42f, 0.5f), mCharAsh, false, true, Quaternion.Euler(0f, 6f, -3f));
            Box("Cardboard Box (charred)", p, new Vector3(3.15f, 0.36f, 0.48f), new Vector3(0.52f, 0.48f, 0.5f), mCharAsh, false, true, Quaternion.Euler(0f, -4f, 0f));
            Box("Cardboard Box (charred)", p, new Vector3(2.75f, 0.77f, 0.45f), new Vector3(0.44f, 0.36f, 0.44f), mCharAsh, false, true, Quaternion.Euler(0f, 12f, 4f));
            // Upper tier (stack ~1.3 m tall), so the fire has fuel to climb above the instruction panel's line of sight.
            // Only this tier still smoulders; the lower boxes are burnt through.
            Box("Cardboard Box (burning)", p, new Vector3(3.24f, 0.82f, 0.47f), new Vector3(0.44f, 0.44f, 0.42f), mEmber, false, true, Quaternion.Euler(0f, 5f, 0f));
            Box("Cardboard Box (burning)", p, new Vector3(2.78f, 1.13f, 0.44f), new Vector3(0.36f, 0.36f, 0.36f), mEmber, false, true, Quaternion.Euler(0f, -9f, 0f));
            // Loose boxes on the west side of the pile (its east side carries the flames), clear of the Exit A debris.
            Box("Cardboard Box (collapsed)", p, new Vector3(2f, 0.03f, 1.05f), new Vector3(0.5f, 0.06f, 0.42f), mCharAsh, false, true, Quaternion.Euler(0f, 25f, 2f));
            Box("Cardboard Box", p, new Vector3(1.95f, 0.2f, 0.37f), new Vector3(0.4f, 0.4f, 0.4f), mCork, false, true, Quaternion.Euler(0f, -8f, 0f));
        }

        /// <summary>Paper notice board on the north corridor wall, burning from its lower edge upwards.</summary>
        static void BurningNoticeBoard(Transform p)
        {
            Box("Notice Board Frame", p, new Vector3(3.8f, 1.5f, 2.885f), new Vector3(1.24f, 0.84f, 0.03f), mTrim, false);
            Box("Notice Board (cork)", p, new Vector3(3.8f, 1.62f, 2.868f), new Vector3(1.16f, 0.52f, 0.012f), mCork, false);
            Box("Notice Board (charred)", p, new Vector3(3.8f, 1.23f, 2.868f), new Vector3(1.16f, 0.26f, 0.012f), mCharAsh, false);
            Box("Burn Line", p, new Vector3(3.8f, 1.37f, 2.866f), new Vector3(1.1f, 0.03f, 0.012f), mEmberLine, false);
            foreach (float x in new[] { 3.42f, 3.78f, 4.16f })
                Box("Notice", p, new Vector3(x, 1.68f, 2.861f), new Vector3(0.21f, 0.297f, 0.002f), mPaper, false, true, Quaternion.Euler(0f, 0f, (x - 3.8f) * 9f));
        }

        /// <summary>Heat-damaged ceiling: a gap where tiles fell, one tile hanging from the grid, burning tiles below.</summary>
        static void FallenCeiling(Transform p)
        {
            // North of the corridor sprinkler and the light-panel row (z <= 1.675), so neither sits at the edge of the gap.
            Box("Ceiling Gap", p, new Vector3(5.5f, CeilingHeight - 0.004f, 2.17f), new Vector3(0.62f, 0.004f, 1.08f), mCharAsh, false);
            // Hinged at the gap's west edge (x 5.19) and hanging at 55 degrees; its lowest corner is ~2.5 m above the floor.
            Box("Hanging Ceiling Tile", p, new Vector3(5.36f, 2.73f, 1.95f), new Vector3(0.6f, 0.015f, 0.6f), mCeiling, false, true, Quaternion.Euler(0f, 0f, -55f));
            Box("Fallen Ceiling Tile", p, new Vector3(5.25f, 0.012f, 1.8f), new Vector3(0.6f, 0.016f, 0.6f), mCeiling, false, true, Quaternion.Euler(3f, 23f, 0f));
            Box("Fallen Ceiling Tile (charred)", p, new Vector3(5.8f, 0.015f, 2.3f), new Vector3(0.6f, 0.016f, 0.58f), mCharAsh, false, true, Quaternion.Euler(-3f, -17f, 2f));
            Box("Fallen Ceiling Tile (burning)", p, new Vector3(5.45f, 0.03f, 2.25f), new Vector3(0.45f, 0.02f, 0.4f), mEmber, false, true, Quaternion.Euler(5f, 41f, 0f));
            Box("Burning Insulation", p, new Vector3(5.55f, 0.05f, 2f), new Vector3(0.35f, 0.08f, 0.3f), mEmber, false, true, Quaternion.Euler(0f, 12f, 0f));
            Box("Broken Grid Bar", p, new Vector3(5.6f, 0.02f, 1.95f), new Vector3(1f, 0.012f, 0.024f), mAluminium, false, true, Quaternion.Euler(0f, -28f, 3f));
        }

        static ParticleSystem Particles(string name, Transform parent, Vector3 position, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 0.4f; // fraction of screen height: no near-full-screen quads up close (VR overdraw)
            return ps;
        }

        /// <summary>
        /// A stopped, non-looping system that fires only its <paramref name="bursts"/> when FireSpread plays it at
        /// ignition (Restart's StopEmittingAndClear resets it for the next attempt).
        /// </summary>
        static ParticleSystem OneShot(string name, Transform parent, Vector3 position, Material material, params ParticleSystem.Burst[] bursts)
        {
            var ps = Particles(name, parent, position, material);
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = false;
            main.duration = 1.2f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(bursts);
            return ps;
        }

        /// <summary>Smoke starts at <paramref name="start"/> and lightens a little as it cools and spreads; keeps its body until late in life.</summary>
        static void SmokeTint(ParticleSystem ps, Color start, float fadeIn)
        {
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(Color.Lerp(start, Color.white, 0.25f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
        }

        /// <summary>
        /// Flames cool from pale yellow to dull red; <paramref name="tint"/> shifts them for the fuel. With additive
        /// blending, darker also means fainter.
        /// </summary>
        static void FlameGradient(ParticleSystem ps, Color tint)
        {
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f) * tint, 0f), new GradientColorKey(new Color(1f, 0.62f, 0.25f) * tint, 0.35f), new GradientColorKey(new Color(0.55f, 0.12f, 0.03f) * tint, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
        }

        /// <summary>Flame core: white-hot, cooling to yellow-orange over its short life; fades in and out (peak <paramref name="peak"/>).</summary>
        static void CoreGradient(ParticleSystem ps, float peak)
        {
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.75f, 0.45f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.15f), new GradientAlphaKey(0.8f * peak, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
        }

        /// <summary>2x2 flipbook atlas: each particle keeps one random frame for its whole life.</summary>
        static void Flipbook(ParticleSystem ps)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            // startFrame is normalised over the whole sheet (0.75 = frame 3 of 4), so 0..1 picks any of the 4 frames.
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.9999f);
        }

        // URP particle shaders read the blend frame from TEXCOORD0.zw (UV2) and the blend factor from TEXCOORD1.x
        // (AnimBlend) when _FLIPBOOKBLENDING_ON is set; without these streams they would sample garbage.
        static readonly List<ParticleSystemVertexStream> FlipbookBlendStreams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.AnimBlend,
        };

        /// <summary>
        /// Animated <paramref name="tiles"/> x <paramref name="tiles"/> flipbook: each particle plays
        /// <paramref name="sheets"/> of the sheet over its life from a random start frame (the sheets loop). With
        /// <paramref name="blend"/>, consecutive frames cross-fade (the material needs BlendedFlipbook).
        /// </summary>
        static void Flipbook(ParticleSystem ps, int tiles, float sheets, bool blend)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = tiles;
            sheet.numTilesY = tiles;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(sheets, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.cycleCount = 1;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.9999f);
            if (blend) ps.GetComponent<ParticleSystemRenderer>().SetActiveVertexStreams(FlipbookBlendStreams);
        }

        /// <summary>Random roll of up to +/- <paramref name="radiansPerSecond"/> per particle.</summary>
        static void Roll(ParticleSystem ps, float radiansPerSecond)
        {
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-radiansPerSecond, radiansPerSecond);
        }

        /// <summary>Smoke drift that speeds up to 4x once the plume has stopped rising (lateral spread under the ceiling).</summary>
        static AnimationCurve Spread(float drift) =>
            new AnimationCurve(new Keyframe(0f, drift), new Keyframe(0.4f, drift * 4f), new Keyframe(1f, drift * 4f));

        static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        static void Drift(ParticleSystem ps, Vector3 velocity)
        {
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(velocity.x);
            v.y = new ParticleSystem.MinMaxCurve(velocity.y);
            v.z = new ParticleSystem.MinMaxCurve(velocity.z);
        }
    }
}
