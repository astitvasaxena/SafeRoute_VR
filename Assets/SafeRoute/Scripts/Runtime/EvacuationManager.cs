using System.Collections.Generic;
using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Training logic: watches the user's head position against evacuation zones, explains
    /// why unsafe exits/routes and restricted rooms are unavailable (panel + instructor),
    /// records the exit used and reports the result at the assembly point.
    /// </summary>
    public class EvacuationManager : MonoBehaviour
    {
        // Waypoints sit on the floor; route validation samples at roughly head height.
        const float RouteSampleHeight = 1.5f;
        const float RouteSampleStep = 0.25f;
        /// <summary>Minimum gap between the safe route and the edge of any fire danger zone (metres).</summary>
        public const float FireClearance = 1.5f;

        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        [Tooltip("Zones to evaluate. Found automatically if empty.")]
        public EvacuationZone[] zones;
        public InstructionPanel panel;
        public RouteGuide routeGuide;
        [Tooltip("Optional virtual instructor; speaks short lines when zones are entered.")]
        public InstructorGuide instructor;

        public string briefingTitle = "FIRE ALARM - EVACUATE NOW";
        [TextArea(2, 6)] public string briefing =
            "A fire has been reported.\nFollow the instructor to Exit B.\nDo not use the lift.";
        public string reminderTitle = "EVACUATE VIA EXIT B";
        [TextArea(2, 6)] public string reminder = "Return to the corridor.\nFollow the instructor to Exit B.";
        public string afterExitTitle = "KEEP MOVING";
        [TextArea(2, 6)] public string afterExit = "Move away from the building.\nGo to the assembly point.";
        public string completeLine = "You have reached the assembly point. Please remain here.";
        public string returnLine = "Come back to the corridor and follow me towards Exit B.";

        [Header("Failure (e.g. fire contact)")]
        public string failTitle = "FIRE CONTACT — EVACUATION FAILED";
        [TextArea(2, 6)] public string failMessage = "You moved into the fire near Exit A.\nNever approach fire or smoke.\nUse the safe route to Exit B.";

        /// <summary>Raised once when the trainee reaches the assembly point (e.g. to fade the alarm).</summary>
        public event System.Action Completed;
        /// <summary>Raised once when the current attempt fails; the argument is the reason.</summary>
        public event System.Action<string> Failed;
        /// <summary>Raised when a new attempt starts after a failure (systems reset themselves).</summary>
        public event System.Action AttemptStarted;

        public bool IsComplete { get; private set; }
        public bool IsFailed { get; private set; }
        /// <summary>1 for the first attempt, incremented by every restart.</summary>
        public int Attempt { get; private set; } = 1;
        /// <summary>One result line per finished attempt, e.g. "Attempt 1: FAILED - fire contact at Exit A (0:42)".</summary>
        public List<string> AttemptLog { get; } = new List<string>();
        public string ExitUsed { get; private set; }
        /// <summary>Restricted-area entries of the current attempt, "time zoneObject", in order.</summary>
        public List<string> Violations { get; } = new List<string>();

        readonly List<string> unsafeApproached = new List<string>();
        readonly List<string> restrictedApproached = new List<string>();
        int restrictedAttempts;
        EvacuationZone currentZone;
        float startTime;

        void Start()
        {
            if (zones == null || zones.Length == 0) zones = FindObjectsOfType<EvacuationZone>();
            if (routeGuide != null && !ValidateRoute(routeGuide.waypoints, zones, out string problem))
                Debug.LogError("[SafeRoute] " + problem, this);
            if (routeGuide != null && !ValidateFireClearance(routeGuide.waypoints, FindObjectsOfType<FireDangerZone>(), FireClearance, out string fireProblem))
                Debug.LogError("[SafeRoute] " + fireProblem, this);

            startTime = Time.time;
            ShowPanel(briefingTitle, briefing, InstructionPanel.Tone.Danger);
        }

        void Update()
        {
            if (IsComplete || IsFailed) return;
            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }

            EvacuationZone zone = FindZone(head.position);
            if (zone == currentZone) return;
            EvacuationZone previous = currentZone;
            currentZone = zone;

            if (zone == null)
            {
                if (ExitUsed != null) ShowPanel(afterExitTitle, afterExit, InstructionPanel.Tone.Safe);
                else if (previous != null && (previous.IsUnsafe || previous.zoneType == ZoneType.Restricted))
                {
                    ShowPanel(reminderTitle, reminder, InstructionPanel.Tone.Info);
                    if (instructor != null) instructor.Say(returnLine);
                }
                return;
            }

            switch (zone.zoneType)
            {
                case ZoneType.BlockedExit:
                case ZoneType.UnavailableRoute:
                    if (!unsafeApproached.Contains(zone.displayName)) unsafeApproached.Add(zone.displayName);
                    ShowPanel(zone.displayName, zone.explanation, InstructionPanel.Tone.Danger);
                    if (instructor != null) instructor.Alert(zone.instructorLine);
                    break;
                case ZoneType.Restricted:
                    restrictedAttempts++;
                    if (!restrictedApproached.Contains(zone.displayName)) restrictedApproached.Add(zone.displayName);
                    Violations.Add($"{Time.time - startTime:0.0}s {zone.name}");
                    Debug.LogWarning($"[SafeRoute] Restricted area violation: {zone.name} at {Time.time - startTime:0.0}s", zone);
                    ShowPanel(zone.displayName, zone.explanation, InstructionPanel.Tone.Danger);
                    if (instructor != null) instructor.Alert(zone.instructorLine);
                    break;
                case ZoneType.SafeExit:
                    if (ExitUsed == null) ExitUsed = zone.displayName;
                    ShowPanel(zone.displayName, zone.explanation, InstructionPanel.Tone.Safe);
                    if (instructor != null) instructor.Say(zone.instructorLine);
                    break;
                case ZoneType.AssemblyPoint:
                    Complete();
                    break;
            }
        }

        void ShowPanel(string heading, string message, InstructionPanel.Tone tone, bool pin = false)
        {
            if (panel != null) panel.Show(heading, message, tone, pin);
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
                "Assembly point reached: YES\n" +
                "Exit used: " + (onRoute ? Short(ExitUsed) : "not recorded") + "\n" +
                $"Time taken: {Clock(t)}\n" +
                "Unsafe areas: " + UnsafeSummary() + "\n" +
                "Restricted entries: " + RestrictedSummary();
            AttemptLog.Add($"Attempt {Attempt}: COMPLETED via {(onRoute ? Short(ExitUsed) : "unknown exit")} ({Clock(t)})");

            ShowPanel(Attempt > 1 ? $"TRAINING COMPLETE - ATTEMPT {Attempt}" : "TRAINING COMPLETE", result,
                onRoute ? InstructionPanel.Tone.Safe : InstructionPanel.Tone.Info, pin: true);
            if (instructor != null) instructor.Say(completeLine, true);
            Debug.Log("[SafeRoute] Training complete.\n" + result + "\n" + string.Join("\n", AttemptLog), this);
            Completed?.Invoke();
        }

        /// <summary>
        /// Fails the current attempt (e.g. fire contact): stops route progress and instructor guidance, shows the
        /// failure panel and records the result. Only the first call per attempt counts; returns false otherwise.
        /// </summary>
        public bool Fail(string reason)
        {
            if (IsComplete || IsFailed) return false;
            IsFailed = true;
            if (routeGuide != null) routeGuide.Hide();
            if (instructor != null) instructor.Halt();

            float t = Time.time - startTime;
            AttemptLog.Add($"Attempt {Attempt}: FAILED - {reason} ({Clock(t)})");
            ShowPanel(failTitle, $"{failMessage}\nAttempt {Attempt}: FAILED", InstructionPanel.Tone.Danger, pin: true);
            Debug.LogWarning($"[SafeRoute] Evacuation FAILED (attempt {Attempt}): {reason} at {Clock(t)}.\n" +
                $"Unsafe areas: {UnsafeSummary()}\nRestricted entries: {RestrictedSummary()}", this);
            Failed?.Invoke(reason);
            return true;
        }

        /// <summary>Starts a fresh attempt: clears the per-attempt state and resets the route, instructor and panel.</summary>
        public void BeginNewAttempt()
        {
            Attempt++;
            IsComplete = false;
            IsFailed = false;
            ExitUsed = null;
            Violations.Clear();
            unsafeApproached.Clear();
            restrictedApproached.Clear();
            restrictedAttempts = 0;
            currentZone = null;
            startTime = Time.time;

            if (routeGuide != null) routeGuide.ResetProgress();
            if (instructor != null) instructor.ResetToStart();
            if (panel != null) panel.ResetPanel();
            ShowPanel(briefingTitle, briefing, InstructionPanel.Tone.Danger);
            Debug.Log($"[SafeRoute] Attempt {Attempt} started.", this);
            AttemptStarted?.Invoke();
        }

        string UnsafeSummary() => unsafeApproached.Count > 0 ? string.Join(", ", unsafeApproached.ConvertAll(Short)) : "none";

        string RestrictedSummary() =>
            restrictedAttempts > 0 ? $"{restrictedAttempts} ({string.Join(", ", restrictedApproached.ConvertAll(Short))})" : "none";

        static string Clock(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

        /// <summary>"CLASSROOM 2 - NO ENTRY" -> "CLASSROOM 2".</summary>
        static string Short(string displayName)
        {
            int dash = displayName.IndexOf(" - ", System.StringComparison.Ordinal);
            return dash > 0 ? displayName.Substring(0, dash) : displayName;
        }

        /// <summary>Checks that no route segment passes through a blocked, unavailable or restricted zone.</summary>
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
                        if (z != null && z.BlocksRoute && z.Contains(p))
                        {
                            problem = $"Route segment {i}->{i + 1} passes through {(z.IsUnsafe ? "unsafe" : "restricted")} zone '{z.displayName}'.";
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Checks that every route segment stays at least <paramref name="margin"/> metres (horizontally) outside every
        /// fire danger zone, so a fire patch can never be placed on or next to the safe path.
        /// </summary>
        public static bool ValidateFireClearance(Transform[] waypoints, FireDangerZone[] fires, float margin, out string problem)
        {
            problem = null;
            if (waypoints == null || fires == null) return true;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] == null || waypoints[i + 1] == null) continue;
                Vector3 a = waypoints[i].position, b = waypoints[i + 1].position;
                a.y = b.y = 0f;
                foreach (FireDangerZone f in fires)
                {
                    if (f == null) continue;
                    Vector3 c = f.transform.position;
                    c.y = 0f;
                    Vector3 ab = b - a;
                    float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(c - a, ab) / ab.sqrMagnitude) : 0f;
                    float gap = Vector3.Distance(a + ab * t, c) - f.radius;
                    if (gap < margin)
                    {
                        problem = $"Route segment {i}->{i + 1} passes {gap:0.0} m from fire danger zone '{f.name}' (needs {margin:0.0} m).";
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
