using System.Collections.Generic;
using NUnit.Framework;
using SafeRoute.Evacuation;
using UnityEngine;

namespace SafeRoute.EditorTests
{
    /// <summary>
    /// EditMode tests for the multi-patch fire (Window > General > Test Runner > EditMode): ignition timeline and
    /// growth, reset for a new attempt, a single failure from overlapping danger zones, and the route clearance check.
    /// Objects are destroyed in TearDown; the open scene is not modified.
    /// </summary>
    public class FireSpreadTests
    {
        static readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        static GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        static T Make<T>(string name) where T : Component => New(name).AddComponent<T>();

        static Transform At(string name, float x, float z)
        {
            Transform t = New(name).transform;
            t.position = new Vector3(x, 0f, z);
            return t;
        }

        [Test]
        public void Spread_DelayedPatchStaysOutUntilItsTime_ThenGrowsAndResets()
        {
            var flames = Make<ParticleSystem>("Flames");
            var emission = flames.emission;
            emission.rateOverTime = 20f;
            FlickerLight light = Make<FlickerLight>("Light");
            var origin = new FireSpread.Patch { name = "Origin", igniteAt = 0f, effects = new ParticleSystem[0], lights = new FlickerLight[0] };
            var delayed = new FireSpread.Patch { name = "Boxes", igniteAt = 10f, growTime = 5f, effects = new[] { flames }, lights = new[] { light } };
            var spread = Make<FireSpread>("Spread");
            spread.patches = new[] { origin, delayed };

            spread.Restart();
            Assert.AreEqual(1f, origin.Growth, "the origin burns from the start");
            Assert.AreEqual(0f, delayed.Growth);
            Assert.AreEqual(0f, light.intensityScale);
            Assert.AreEqual(0f, flames.emission.rateOverTimeMultiplier, 1e-4f);

            spread.Tick(9.9f);
            Assert.AreEqual(0f, delayed.Growth, "not before its ignition time");
            spread.Tick(10f);
            Assert.AreEqual(0.3f, delayed.Growth, 1e-3f, "starts small");
            spread.Tick(12.5f);
            Assert.That(delayed.Growth, Is.GreaterThan(0.3f).And.LessThan(1f), "growing");
            spread.Tick(15f);
            Assert.AreEqual(1f, delayed.Growth, 1e-4f, "full size after its grow time");
            Assert.AreEqual(1f, light.intensityScale, 1e-4f);
            Assert.AreEqual(20f, flames.emission.rateOverTimeMultiplier, 1e-3f);

            spread.Restart();
            Assert.AreEqual(0f, delayed.Growth, "a new attempt puts it out again");
            Assert.AreEqual(1f, origin.Growth);
        }

        [Test]
        public void OverlappingDangerZones_FailTheAttemptOnlyOnce()
        {
            var manager = Make<EvacuationManager>("Manager");
            manager.zones = new EvacuationZone[0];
            var restart = Make<TrainingRestart>("Restart");
            restart.manager = manager;
            Transform head = New("Head").transform;
            head.position = new Vector3(2.6f, 1.6f, 1f);

            var zones = new[] { Make<FireDangerZone>("Exit A Fire"), Make<FireDangerZone>("Boxes Fire") };
            zones[0].transform.position = new Vector3(0.9f, 0f, 1.5f);
            zones[0].radius = 1.8f;
            zones[1].transform.position = new Vector3(2.9f, 0f, 0.55f);
            zones[1].radius = 1f;
            foreach (FireDangerZone z in zones)
            {
                z.restart = restart;
                z.head = head;
            }

            Assert.IsTrue(zones[0].Contains(head.position) && zones[1].Contains(head.position), "head is in both zones");
            Assert.IsTrue(zones[0].Evaluate());
            Assert.IsFalse(zones[1].Evaluate(), "the second zone cannot fail the same attempt again");
            Assert.IsFalse(zones[0].Evaluate());
            Assert.AreEqual(1, manager.AttemptLog.Count);

            manager.BeginNewAttempt();
            Assert.IsTrue(zones[1].IsArmed && zones[0].IsArmed);
            Assert.IsTrue(zones[1].Evaluate(), "either zone can fail the next attempt");
            StringAssert.StartsWith("Attempt 2: FAILED", manager.AttemptLog[1]);
        }

        [Test]
        public void FireClearance_PassesForWestEndFires_FailsForAFireOnTheRoute()
        {
            Transform[] route = { At("Door", 9.8f, 4.2f), At("Corridor", 9.8f, 1.5f), At("Stairs", 16f, 1.5f) };
            var ceilingFire = Make<FireDangerZone>("Fallen Ceiling Fire");
            ceilingFire.transform.position = new Vector3(5.5f, 0f, 2.05f);
            ceilingFire.radius = 0.75f;

            Assert.IsTrue(EvacuationManager.ValidateFireClearance(route, new[] { ceilingFire }, EvacuationManager.FireClearance, out string problem), problem);

            var onRoute = Make<FireDangerZone>("Misplaced Fire");
            onRoute.transform.position = new Vector3(12f, 0f, 2.2f);
            onRoute.radius = 0.75f;
            Assert.IsFalse(EvacuationManager.ValidateFireClearance(route, new[] { ceilingFire, onRoute }, EvacuationManager.FireClearance, out problem));
            StringAssert.Contains("Misplaced Fire", problem);
        }
    }
}
