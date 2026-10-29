using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Virtual instructor that leads the trainee along the safe route from in front: walks a couple of
    /// metres ahead, looks back periodically, stops and beckons when the trainee falls behind, stops at
    /// cue points to explain turns, hazards and decisions (speech bubble + instruction panel), and turns
    /// to face the trainee when warning them.
    /// Body-agnostic: drives only this transform, a speech bubble/name tag and an optional Animator
    /// ("Speed" float), so the primitive placeholder can be swapped for a real character.
    /// </summary>
    public class InstructorGuide : MonoBehaviour
    {
        [System.Serializable]
        public class Cue
        {
            [Tooltip("Path point index: 0 = start position, then the route waypoints in order.")]
            public int pointIndex;
            [Tooltip("Wait here until the trainee is close before speaking and moving on.")]
            public bool waitForTrainee = true;
            [Tooltip("Seconds to stay at the point after speaking.")]
            public float hold = 1.5f;
            [Tooltip("Short line shown in the speech bubble.")]
            public string line;
            [Tooltip("Instruction panel title/body (critical instructions are always repeated in the panel).")]
            public string title;
            [TextArea(2, 5)] public string detail;
            [Tooltip("World direction to point while speaking (zero = no pointing).")]
            public Vector3 pointDirection;
        }

        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        [Tooltip("Start position followed by the safe-route waypoints, in order.")]
        public Transform[] path;
        public Cue[] cues;
        public InstructionPanel panel;

        [Header("Movement")]
        public float walkSpeed = 1.3f;
        public float acceleration = 2.5f;
        public float turnSpeed = 6f;
        [Tooltip("Preferred distance ahead of the trainee, measured along the route.")]
        public float leadDistance = 2.2f;
        [Tooltip("Stop and wait when the trainee is this far behind along the route.")]
        public float maxLead = 4f;
        [Tooltip("A cue fires once the trainee is within this distance.")]
        public float waitRadius = 3.5f;
        [Tooltip("While walking, glance back at the trainee this often (seconds).")]
        public float lookBackInterval = 4f;

        [Header("Speech")]
        public Transform speechRoot;
        public Text speechText;
        [Tooltip("Shown above the head whenever the speech bubble is hidden.")]
        public Transform nameTag;
        public float speechDuration = 7f;
        public string catchUpLine = "Stay with me. This way please.";
        public float catchUpInterval = 8f;
        public float alertDuration = 3f;

        [Header("Voice")]
        [Tooltip("Optional spoken lines. The speech bubble and panel subtitle always show the line being spoken.")]
        public InstructorVoice voice;
        [Tooltip("The same line is not repeated within this many seconds.")]
        public float repeatCooldown = 6f;
        [Tooltip("A normal line waiting for the current one to finish is dropped after this many seconds.")]
        public float queueTimeout = 6f;

        [Header("Optional character")]
        [Tooltip("Optional Animator of a real character model; receives a 'Speed' float.")]
        public Animator animator;

        public float CurrentSpeed { get; private set; }
        public bool IsPointing => pointTimer > 0f;
        public Vector3 PointDirection { get; private set; }
        /// <summary>Standing still and calling the trainee towards it.</summary>
        public bool IsBeckoning { get; private set; }
        /// <summary>Glancing back over the shoulder while walking.</summary>
        public bool IsLookingBack => lookBackTimer > 0f;
        public Transform Head => head;

        int target = 1;         // next path point
        int cueChecked = -1;    // last path point whose cue was handled
        float holdTimer, alertTimer, pointTimer, speechTimer, waitTimer, lookBackTimer, nextLookBack, lastCatchUp = -100f;
        string pendingLine;
        float pendingTime;
        readonly Dictionary<string, float> lastSaid = new Dictionary<string, float>();
        bool speedParam, halted;
        float[] cumulative;
        Quaternion startRotation = Quaternion.identity;

        /// <summary>Frozen and silent after a failed attempt, until <see cref="ResetToStart"/>.</summary>
        public bool IsHalted => halted;

        void Start()
        {
            cumulative = new float[path != null ? path.Length : 0];
            for (int i = 1; i < cumulative.Length; i++)
                cumulative[i] = cumulative[i - 1] + FlatDistance(path[i - 1].position, path[i].position);

            if (path != null && path.Length > 0) transform.position = path[0].position;
            startRotation = transform.rotation;

            if (animator != null)
                foreach (AnimatorControllerParameter p in animator.parameters)
                    if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) speedParam = true;

            if (speechRoot != null) speechRoot.gameObject.SetActive(false);
            nextLookBack = lookBackInterval;
        }

        /// <summary>
        /// Says a line (speech bubble, panel subtitle and voice). A line already being said, or said within
        /// <see cref="repeatCooldown"/>, is ignored. Without <paramref name="interrupt"/> it waits for the current
        /// spoken line to finish instead of cutting it off.
        /// </summary>
        public void Say(string line, bool interrupt = false)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (lastSaid.TryGetValue(line, out float said) && Time.time - said < repeatCooldown) return;
            if (!interrupt && voice != null && voice.IsSpeaking)
            {
                pendingLine = line;
                pendingTime = Time.time;
                return;
            }
            Speak(line);
        }

        /// <summary>Stops, turns to the trainee, beckons and warns them (interrupts the current line).</summary>
        public void Alert(string line)
        {
            Say(line, true);
            alertTimer = alertDuration;
        }

        /// <summary>Stops walking and talking at once (training failed): voice, queued line, bubble and subtitle.</summary>
        public void Halt()
        {
            halted = true;
            pendingLine = null;
            if (voice != null) voice.Stop();
            speechTimer = holdTimer = alertTimer = pointTimer = lookBackTimer = waitTimer = 0f;
            CurrentSpeed = 0f;
            IsBeckoning = false;
            if (speechRoot != null) speechRoot.gameObject.SetActive(false);
            if (nameTag != null) nameTag.gameObject.SetActive(true);
            if (panel != null) panel.ShowSubtitle(null);
            if (speedParam) animator.SetFloat("Speed", 0f);
        }

        /// <summary>Back to the start point with every cue pending again (new attempt).</summary>
        public void ResetToStart()
        {
            Halt();
            halted = false;
            target = 1;
            cueChecked = -1;
            nextLookBack = lookBackInterval;
            lastCatchUp = -100f;
            lastSaid.Clear();
            if (path != null && path.Length > 0 && path[0] != null) transform.SetPositionAndRotation(path[0].position, startRotation);
        }

        void Speak(string line)
        {
            pendingLine = null;
            lastSaid[line] = Time.time;
            if (speechText != null) speechText.text = line;
            if (speechRoot != null) speechRoot.gameObject.SetActive(true);
            float spoken = voice != null ? voice.Play(line) : 0f;
            speechTimer = Mathf.Max(speechDuration, spoken + 1.5f);
            if (panel != null) panel.ShowSubtitle(line);
        }

        void Update()
        {
            if (halted || path == null || path.Length < 2) return;
            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }

            float dt = Time.deltaTime;
            bool wasSpeaking = speechTimer > 0f;
            holdTimer -= dt;
            alertTimer -= dt;
            pointTimer -= dt;
            speechTimer -= dt;
            lookBackTimer -= dt;

            // A queued line is said once the current spoken line has finished (dropped if it is out of date).
            if (pendingLine != null && Time.time - pendingTime > queueTimeout) pendingLine = null;
            if (pendingLine != null && (voice == null || !voice.IsSpeaking)) Speak(pendingLine);

            bool speaking = speechTimer > 0f;
            if (wasSpeaking && !speaking && panel != null) panel.ShowSubtitle(null);
            if (speechRoot != null && speechRoot.gameObject.activeSelf != speaking) speechRoot.gameObject.SetActive(speaking);
            if (nameTag != null && nameTag.gameObject.activeSelf == speaking) nameTag.gameObject.SetActive(!speaking);

            float traineeProgress = ProgressOf(head.position);
            float myProgress = ProgressOf(transform.position);
            float gapAlong = myProgress - traineeProgress;
            float gap = FlatDistance(transform.position, head.position);
            bool traineeAhead = gapAlong < -0.5f;
            bool arrived = target >= path.Length;

            // Cue at the point just reached: wait for the trainee, speak, hold.
            int reached = target - 1;
            if (reached > cueChecked)
            {
                Cue cue = FindCue(reached);
                if (cue == null) cueChecked = reached;
                else if (!cue.waitForTrainee || gap <= waitRadius || traineeAhead)
                {
                    FireCue(cue);
                    cueChecked = reached;
                }
            }

            float desired = 0f;
            bool waitingForCue = reached > cueChecked;
            if (!arrived && !waitingForCue && holdTimer <= 0f && alertTimer <= 0f)
                desired = traineeAhead ? walkSpeed * 1.2f
                    : walkSpeed * Mathf.Clamp01((maxLead - gapAlong) / Mathf.Max(0.01f, maxLead - leadDistance));

            // Trainee has fallen behind (or wandered off): turn round, beckon and call them.
            bool behind = !arrived && !traineeAhead && desired <= 0.01f && gap > waitRadius;
            waitTimer = behind ? waitTimer + dt : 0f;
            IsBeckoning = alertTimer > 0f || waitTimer > 1f;
            if (waitTimer > 1.5f && Time.time - lastCatchUp > catchUpInterval)
            {
                Say(catchUpLine);
                lastCatchUp = Time.time;
            }

            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, desired, acceleration * dt);

            // Periodic glance back while walking.
            if (CurrentSpeed > 0.3f)
            {
                nextLookBack -= dt;
                if (nextLookBack <= 0f)
                {
                    lookBackTimer = 1.2f;
                    nextLookBack = lookBackInterval;
                }
            }

            Vector3 facing;
            if (!arrived && CurrentSpeed > 0.01f)
            {
                Vector3 goal = path[target].position;
                Vector3 pos = Vector3.MoveTowards(transform.position, goal, CurrentSpeed * dt);
                transform.position = pos;
                if (FlatDistance(pos, goal) < 0.02f) target++;
                facing = Flat(goal - pos);
                // Turn into the next segment slightly before reaching a corner.
                if (target < path.Length && FlatDistance(pos, goal) < 0.6f)
                    facing = Vector3.Lerp(facing.normalized, Flat(path[target].position - goal).normalized, 1f - FlatDistance(pos, goal) / 0.6f);
            }
            else facing = Flat(head.position - transform.position);

            if (IsPointing && CurrentSpeed < 0.3f)
                facing = Vector3.Lerp(Flat(head.position - transform.position).normalized, PointDirection, 0.5f);
            if (facing.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), dt * turnSpeed);

            Billboard(speechRoot);
            Billboard(nameTag);

            if (speedParam) animator.SetFloat("Speed", CurrentSpeed);
        }

        void Billboard(Transform t)
        {
            if (t == null || !t.gameObject.activeSelf) return;
            Vector3 away = Flat(t.position - head.position);
            if (away.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(away);
        }

        void FireCue(Cue cue)
        {
            Say(cue.line);
            if (panel != null && !string.IsNullOrEmpty(cue.title)) panel.Show(cue.title, cue.detail, InstructionPanel.Tone.Info);
            holdTimer = cue.hold;
            if (cue.pointDirection.sqrMagnitude > 1e-4f)
            {
                PointDirection = Flat(cue.pointDirection).normalized;
                pointTimer = Mathf.Max(cue.hold, 2.5f);
            }
        }

        Cue FindCue(int pointIndex)
        {
            if (cues == null) return null;
            foreach (Cue c in cues)
                if (c != null && c.pointIndex == pointIndex) return c;
            return null;
        }

        /// <summary>Distance along the path of the closest point on the path to <paramref name="p"/>.</summary>
        float ProgressOf(Vector3 p)
        {
            float best = float.MaxValue, progress = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = Flat(path[i].position), b = Flat(path[i + 1].position), q = Flat(p);
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
                float d = (a + ab * t - q).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    progress = cumulative[i] + ab.magnitude * t;
                }
            }
            return progress;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;

        void OnDrawGizmosSelected()
        {
            if (path == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < path.Length - 1; i++)
                if (path[i] != null && path[i + 1] != null) Gizmos.DrawLine(path[i].position, path[i + 1].position);
        }
    }
}
