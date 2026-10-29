using NUnit.Framework;
using SafeRoute.Evacuation;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SafeRoute.EditorTests
{
    /// <summary>
    /// EditMode tests for the fire-contact failure and restart logic (Window > General > Test Runner > EditMode).
    /// The Test Runner runs them in its own temporary scene and restores the open scenes afterwards; every object a
    /// test creates is destroyed again in TearDown. The timed sequence itself (coroutine, fades, sounds) needs Play
    /// mode and is covered by the manual checklist.
    /// </summary>
    public class FireContactTests
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

        static EvacuationManager MakeManager()
        {
            var manager = Make<EvacuationManager>("Manager");
            manager.zones = new EvacuationZone[0];
            return manager;
        }

        /// <summary>Zone at the origin wired to a restart controller and manager.</summary>
        static FireDangerZone MakeZone(EvacuationManager manager, Transform head)
        {
            var restart = Make<TrainingRestart>("Restart");
            restart.manager = manager;
            var zone = Make<FireDangerZone>("Fire Danger Zone");
            zone.restart = restart;
            zone.head = head;
            return zone;
        }

        [Test]
        public void Zone_IsAVerticalCylinderWithConfigurableRadius()
        {
            var zone = Make<FireDangerZone>("Zone");
            zone.radius = 1.4f;
            zone.height = 3f;
            Assert.IsTrue(zone.Contains(new Vector3(1f, 1.6f, 0.5f)));
            Assert.IsFalse(zone.Contains(new Vector3(1.5f, 1.6f, 0f)), "outside the radius");
            Assert.IsFalse(zone.Contains(new Vector3(0f, 3.5f, 0f)), "above the zone");
            zone.radius = 2f;
            Assert.IsTrue(zone.Contains(new Vector3(1.5f, 1.6f, 0f)), "radius change takes effect");
        }

        [Test]
        public void Zone_FiresOnceAndRearmsOnNewAttempt()
        {
            EvacuationManager manager = MakeManager();
            var head = New("Head").transform;
            head.position = new Vector3(0.5f, 1.6f, 0f);
            FireDangerZone zone = MakeZone(manager, head);

            Assert.IsTrue(zone.Evaluate(), "first contact triggers");
            Assert.IsTrue(manager.IsFailed);
            Assert.IsFalse(zone.Evaluate(), "no repeat while still inside");
            Assert.AreEqual(1, manager.AttemptLog.Count);
            StringAssert.Contains("FAILED", manager.AttemptLog[0]);

            manager.BeginNewAttempt();
            Assert.IsTrue(zone.IsArmed, "re-armed by the new attempt");
            Assert.IsFalse(manager.IsFailed);
            Assert.AreEqual(2, manager.Attempt);
        }

        [Test]
        public void Zone_IgnoredWhileFireIsNotBurning()
        {
            EvacuationManager manager = MakeManager();
            var head = New("Head").transform;
            head.position = new Vector3(0.2f, 1.6f, 0f);
            FireDangerZone zone = MakeZone(manager, head);
            zone.fire = Make<ParticleSystem>("Flames");
            zone.fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Assert.IsFalse(zone.IsBurning);
            Assert.IsFalse(zone.Evaluate());
            Assert.IsFalse(manager.IsFailed);
            Assert.IsTrue(zone.IsArmed);
        }

        [Test]
        public void Zone_DetectsCharacterControllerBodyEvenIfHeadIsOutside()
        {
            EvacuationManager manager = MakeManager();
            var head = New("Head").transform;
            head.position = new Vector3(5f, 1.6f, 0f);
            FireDangerZone zone = MakeZone(manager, head);
            zone.body = Make<CharacterController>("Body");
            zone.body.transform.position = new Vector3(0.6f, 0f, 0f);

            Assert.IsTrue(zone.Evaluate());
        }

        [Test]
        public void Manager_FailCountsOncePerAttempt()
        {
            EvacuationManager manager = MakeManager();
            Assert.IsTrue(manager.Fail("test"));
            Assert.IsFalse(manager.Fail("test again"));
            Assert.AreEqual(1, manager.AttemptLog.Count);

            manager.BeginNewAttempt();
            Assert.IsTrue(manager.Fail("second attempt"));
            Assert.AreEqual(2, manager.AttemptLog.Count);
            StringAssert.StartsWith("Attempt 2: FAILED", manager.AttemptLog[1]);
        }

        [Test]
        public void Restart_ReturnsHeadToStartFacingStartDirection_AndReenablesController()
        {
            var rig = Make<CharacterController>("XR Origin").transform;
            rig.SetPositionAndRotation(new Vector3(7.5f, 0f, 6.3f), Quaternion.Euler(0f, 120f, 0f));
            var head = New("Camera").transform;
            head.SetParent(rig, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);
            var restart = Make<TrainingRestart>("Restart");
            restart.rig = rig;
            restart.head = head;
            restart.CaptureStart();
            Vector3 startForward = rig.forward;

            // Trainee walked off, turned, and moved their head away from the rig centre (room-scale / simulator).
            rig.SetPositionAndRotation(new Vector3(1f, 0f, 1.5f), Quaternion.Euler(0f, -30f, 0f));
            head.localPosition = new Vector3(0.4f, 1.6f, -0.3f);
            head.localRotation = Quaternion.Euler(10f, 50f, 0f);

            restart.ReturnRigToStart();

            Assert.AreEqual(7.5f, head.position.x, 1e-3f);
            Assert.AreEqual(6.3f, head.position.z, 1e-3f);
            Assert.AreEqual(0f, rig.position.y, 1e-4f);
            Vector3 flat = head.forward;
            flat.y = 0f;
            Assert.Less(Vector3.Angle(flat, startForward), 0.1f);
            Assert.IsTrue(rig.GetComponent<CharacterController>().enabled);
        }

        [Test]
        public void Instructor_HaltsAndResetsToStart()
        {
            var start = New("Start").transform;
            start.position = new Vector3(9.5f, 0f, 4.7f);
            var next = New("Waypoint").transform;
            next.position = new Vector3(9.8f, 0f, 1.5f);
            var guide = Make<InstructorGuide>("Instructor");
            guide.path = new[] { start, next };
            guide.transform.position = new Vector3(20f, 0f, 1.5f);

            guide.Halt();
            Assert.IsTrue(guide.IsHalted);
            Assert.AreEqual(0f, guide.CurrentSpeed);

            guide.ResetToStart();
            Assert.IsFalse(guide.IsHalted);
            Assert.AreEqual(start.position, guide.transform.position);
        }

        [Test]
        public void RouteGuideAndPanel_ResetForNewAttempt()
        {
            var route = Make<RouteGuide>("Route");
            route.arrow = New("Arrow").transform;
            route.Hide();
            Assert.IsFalse(route.arrow.gameObject.activeSelf);
            route.ResetProgress();
            Assert.IsTrue(route.arrow.gameObject.activeSelf);

            var panel = Make<InstructionPanel>("Panel");
            panel.subtitleRoot = New("Subtitle");
            panel.subtitle = panel.subtitleRoot.AddComponent<Text>();
            panel.ShowSubtitle("Exit A is blocked. Do not approach the fire.");
            Assert.IsTrue(panel.subtitleRoot.activeSelf);
            panel.ResetPanel();
            Assert.IsFalse(panel.subtitleRoot.activeSelf);
            Assert.AreEqual("", panel.subtitle.text);
        }
    }
}
