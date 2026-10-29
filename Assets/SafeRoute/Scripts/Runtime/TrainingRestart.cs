using System.Collections;
using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Failure and restart sequence (e.g. fire contact): impact sound, burn tint and a failure message, then
    /// "Simulation restarting...", a fade to dark, the XR rig returned to its starting pose and a new attempt
    /// started in the same scene. The scene is deliberately not reloaded, so the XR Origin and the XR Device
    /// Simulator are never torn down.
    /// </summary>
    public class TrainingRestart : MonoBehaviour
    {
        public EvacuationManager manager;
        [Tooltip("XR Origin root moved back to the start. Its CharacterController is disabled while it is moved.")]
        public Transform rig;
        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        public HeadCollisionGuard headGuard;
        public ScreenTint screenTint;

        [Header("Sound")]
        public AudioSource impactSource;
        public AudioClip impactClip;

        [Header("Sequence (seconds)")]
        [Min(0f)] public float failureHold = 4f;
        [Min(0.1f)] public float fadeOutTime = 1.2f;
        [Min(0f)] public float restartHold = 1.5f;
        [Min(0.1f)] public float fadeInTime = 1f;
        public string restartTitle = "SIMULATION RESTARTING...";
        [TextArea(2, 4)] public string restartMessage = "Returning to the classroom.\nFollow the instructor to Exit B.";

        public bool IsRestarting { get; private set; }

        Vector3 startPosition, startForward;
        Quaternion startRotation;
        bool startCaptured;

        void Start() => CaptureStart();

        /// <summary>Remembers the rig's pose as the starting point of every attempt.</summary>
        public void CaptureStart()
        {
            if (rig == null) return;
            startPosition = rig.position;
            startRotation = rig.rotation;
            startForward = Flat(rig.forward);
            startForward = startForward.sqrMagnitude > 1e-4f ? startForward.normalized : Vector3.forward;
            startCaptured = true;
        }

        /// <summary>Fails the current attempt and restarts. Ignored (false) while restarting, after completion or after a failure.</summary>
        public bool TriggerFailure(string reason)
        {
            if (IsRestarting || manager == null || !manager.Fail(reason)) return false;
            if (impactSource != null && impactClip != null) impactSource.PlayOneShot(impactClip);
            if (screenTint != null) screenTint.Burn();
            if (Application.isPlaying) // the timed sequence only exists in Play mode
            {
                IsRestarting = true;
                StartCoroutine(Sequence());
            }
            return true;
        }

        IEnumerator Sequence()
        {
            // The countdown runs alongside and only displays the time left; the waits below are unchanged.
            Coroutine countdown = StartCoroutine(Countdown(failureHold + fadeOutTime + restartHold));
            yield return new WaitForSeconds(failureHold);
            if (manager.panel != null) manager.panel.Show(restartTitle, restartMessage, InstructionPanel.Tone.Info, true);
            if (screenTint != null) screenTint.FadeTo(screenTint.darkColor, fadeOutTime);
            yield return new WaitForSeconds(fadeOutTime + restartHold);

            StopCoroutine(countdown);
            ReturnRigToStart();
            manager.BeginNewAttempt();
            if (manager.panel != null) manager.panel.SetStatus(""); // the route distance takes the footer back
            yield return null; // let tracking, the head guard and the panel settle at the new position before fading in
            if (screenTint != null) screenTint.FadeTo(Color.clear, fadeInTime);
            IsRestarting = false;
        }

        /// <summary>Whole-second countdown to the restart in the panel footer (the string is rebuilt once per second).</summary>
        IEnumerator Countdown(float seconds)
        {
            float end = Time.time + seconds;
            int shown = -1;
            while (manager.panel != null)
            {
                int left = Mathf.Max(1, Mathf.CeilToInt(end - Time.time));
                if (left != shown)
                {
                    shown = left;
                    manager.panel.SetStatus($"Restarting in {left} s");
                }
                yield return null;
            }
        }

        /// <summary>
        /// Puts the trainee's head back at the start point, facing the start direction. The head may be offset from
        /// the rig (room-scale or simulator head movement), so the rig is turned about the head, then shifted.
        /// </summary>
        public void ReturnRigToStart()
        {
            if (rig == null || !startCaptured) return;
            var controller = rig.GetComponent<CharacterController>();
            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controllerWasEnabled) controller.enabled = false; // an enabled CharacterController would undo the move

            Transform h = head != null ? head : Camera.main != null ? Camera.main.transform : null;
            if (h != null && h.IsChildOf(rig))
            {
                Vector3 forward = Flat(h.forward);
                if (forward.sqrMagnitude > 1e-4f)
                    rig.RotateAround(h.position, Vector3.up, Vector3.SignedAngle(forward.normalized, startForward, Vector3.up));
                Vector3 offset = Flat(h.position - rig.position);
                rig.position = new Vector3(startPosition.x - offset.x, startPosition.y, startPosition.z - offset.z);
            }
            else rig.SetPositionAndRotation(startPosition, startRotation);

            Physics.SyncTransforms();
            if (controllerWasEnabled) controller.enabled = true;
            if (headGuard != null) headGuard.ResetTracking();
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
