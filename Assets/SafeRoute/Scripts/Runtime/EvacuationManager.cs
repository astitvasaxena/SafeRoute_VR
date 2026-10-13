using System.Collections.Generic;
using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Training logic: watches the user's head position against evacuation zones, explains
    /// why unsafe exits/routes are unavailable, records the exit used and reports the result
    /// at the assembly point.
    /// </summary>
    public class EvacuationManager : MonoBehaviour
    {
        // Waypoints sit on the floor; route validation samples at roughly head height.
        const float RouteSampleHeight = 1.5f;
        const float RouteSampleStep = 0.25f;

        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        [Tooltip("Zones to evaluate. Found automatically if empty.")]
        public EvacuationZone[] zones;
        public InstructionPanel panel;
        public RouteGuide routeGuide;

        public string briefingTitle = "FIRE ALARM - EVACUATE NOW";
        [TextArea(2, 6)] public string briefing =
            "A fire has been reported in the building.\nLeave the classroom calmly and follow the green arrow and floor line to Exit B.\nDo not use the elevator.";
        public string reminderTitle = "EVACUATE VIA EXIT B";
        [TextArea(2, 6)] public string reminder = "Follow the green arrow and floor line to Exit B.";
        public string afterExitTitle = "KEEP MOVING";
        [TextArea(2, 6)] public string afterExit = "Move away from the building and go to the assembly point.";

        public bool IsComplete { get; private set; }
        public string ExitUsed { get; private set; }

        readonly List<string> unsafeApproached = new List<string>();
        EvacuationZone currentZone;
        float startTime;

        void Start()
        {
            if (zones == null || zones.Length == 0) zones = FindObjectsOfType<EvacuationZone>();
            if (routeGuide != null && !ValidateRoute(routeGuide.waypoints, zones, out string problem))
                Debug.LogError("[SafeRoute] " + problem, this);

            startTime = Time.time;
            ShowPanel(briefingTitle, briefing, InstructionPanel.Tone.Danger);
        }

        void Update()
        {
            if (IsComplete) return;
            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }

            EvacuationZone zone = FindZone(head.position);
            if (zone == currentZone) return;
            currentZone = zone;

            if (zone == null)
            {
                if (ExitUsed != null) ShowPanel(afterExitTitle, afterExit, InstructionPanel.Tone.Safe);
                else if (unsafeApproached.Count > 0) ShowPanel(reminderTitle, reminder, InstructionPanel.Tone.Info);
                return;
            }

            switch (zone.zoneType)
            {
                case ZoneType.BlockedExit:
                case ZoneType.UnavailableRoute:
                    if (!unsafeApproached.Contains(zone.displayName)) unsafeApproached.Add(zone.displayName);
                    ShowPanel(zone.displayName, zone.explanation, InstructionPanel.Tone.Danger);
                    break;
                case ZoneType.SafeExit:
                    if (ExitUsed == null) ExitUsed = zone.displayName;
                    ShowPanel(zone.displayName, zone.explanation, InstructionPanel.Tone.Safe);
                    break;
                case ZoneType.AssemblyPoint:
                    Complete();
                    break;
            }
        }

        void ShowPanel(string heading, string message, InstructionPanel.Tone tone)
        {
            if (panel != null) panel.Show(heading, message, tone);
        }

        EvacuationZone FindZone(Vector3 point)
        {
            foreach (EvacuationZone z in zones)
                if (z != null && z.Contains(point)) return z;
            return null;
        }

        void Complete()
        {
            IsComplete = true;
            if (routeGuide != null) routeGuide.Hide();

            float t = Time.time - startTime;
            bool onRoute = ExitUsed != null;
            string result =
                "Reached assembly point: Yes\n" +
                "Exit used: " + (onRoute ? ExitUsed : "none recorded (left the building off the marked route)") + "\n" +
                $"Time taken: {(int)t / 60}:{(int)t % 60:00}\n" +
                "Unsafe areas approached: " + (unsafeApproached.Count > 0 ? string.Join(", ", unsafeApproached) : "none");

            ShowPanel("TRAINING COMPLETE", result, onRoute ? InstructionPanel.Tone.Safe : InstructionPanel.Tone.Info);
            Debug.Log("[SafeRoute] Training complete.\n" + result, this);
        }

        /// <summary>Checks that no route segment passes through a blocked or unavailable zone.</summary>
        public static bool ValidateRoute(Transform[] waypoints, EvacuationZone[] zones, out string problem)
        {
            problem = null;
            if (waypoints == null || waypoints.Length < 2)
            {
                problem = "Route needs at least two waypoints.";
                return false;
            }

            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] == null || waypoints[i + 1] == null)
                {
                    problem = $"Route waypoint {i} or {i + 1} is missing.";
                    return false;
                }

                Vector3 a = waypoints[i].position, b = waypoints[i + 1].position;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / RouteSampleStep));
                for (int s = 0; s <= steps; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)steps) + Vector3.up * RouteSampleHeight;
                    foreach (EvacuationZone z in zones)
                    {
                        if (z != null && z.IsUnsafe && z.Contains(p))
                        {
                            problem = $"Route segment {i}->{i + 1} passes through unsafe zone '{z.displayName}'.";
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }
}
