using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Procedural motion for the primitive placeholder instructor: walk cycle with knee bend scaled by
    /// speed, arm swing, body bob, idle breathing, a torso/head turn to look back at the trainee, a
    /// beckoning wave while waiting and a pointing arm at route decisions. This is readable placeholder
    /// motion, not realistic human animation; remove this component when a rigged character with an
    /// Animator is used.
    /// </summary>
    public class InstructorPlaceholderAnimator : MonoBehaviour
    {
        public InstructorGuide guide;
        public Transform body, torso, headPivot;
        public Transform leftLeg, rightLeg, leftKnee, rightKnee;
        public Transform leftArm, rightArm, leftElbow, rightElbow;

        public float strideLength = 1.3f;
        public float legSwing = 28f;
        public float kneeBend = 45f;
        public float armSwing = 22f;
        public float bobHeight = 0.03f;

        float phase, pointWeight, beckonWeight, lookWeight;
        Vector3 bodyRest;

        void Start()
        {
            if (body != null) bodyRest = body.localPosition;
        }

        void LateUpdate()
        {
            if (guide == null) return;
            float dt = Time.deltaTime;
            float speed = guide.CurrentSpeed;
            float walk = Mathf.Clamp01(speed / Mathf.Max(0.01f, guide.walkSpeed));

            phase += 2f * Mathf.PI * speed / strideLength * dt;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);

            // Legs: hip swing, knee bends while the leg swings forward.
            Swing(leftLeg, -s * legSwing * walk);
            Swing(rightLeg, s * legSwing * walk);
            Swing(leftKnee, Mathf.Max(0f, c) * kneeBend * walk);
            Swing(rightKnee, Mathf.Max(0f, -c) * kneeBend * walk);

            float breathe = Mathf.Sin(Time.time * 1.6f) * 0.004f;
            if (body != null) body.localPosition = bodyRest + Vector3.up * (Mathf.Abs(c) * bobHeight * walk + breathe);

            // Look back over the shoulder (torso + head) while walking, or face the trainee when standing.
            lookWeight = Mathf.MoveTowards(lookWeight, guide.IsLookingBack ? 1f : 0f, dt * 3f);
            float yawToTrainee = 0f;
            if (guide.Head != null)
            {
                Vector3 toHead = transform.InverseTransformPoint(guide.Head.position);
                yawToTrainee = Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
            }
            float headYaw = Mathf.Clamp(yawToTrainee, -70f, 70f) * Mathf.Lerp(1f - walk * 0.8f, 1f, lookWeight);
            float torsoYaw = Mathf.Clamp(yawToTrainee - headYaw, -45f, 45f) * lookWeight;
            if (torso != null) torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(0f, torsoYaw, 0f), dt * 6f);
            if (headPivot != null) headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation, Quaternion.Euler(0f, headYaw, 0f), dt * 6f);

            // Left arm: swing, or a raised beckoning wave towards the instructor while waiting.
            beckonWeight = Mathf.MoveTowards(beckonWeight, guide.IsBeckoning ? 1f : 0f, dt * 3f);
            float wave = Mathf.Sin(Time.time * 6f);
            if (leftArm != null)
                leftArm.localRotation = Quaternion.Slerp(Quaternion.Euler(s * armSwing * walk, 0f, 0f), Quaternion.Euler(-70f, 0f, 25f), beckonWeight);
            if (leftElbow != null)
                leftElbow.localRotation = Quaternion.Slerp(Quaternion.Euler(-15f * walk, 0f, 0f), Quaternion.Euler(-60f - 35f * wave, 0f, 0f), beckonWeight);

            // Right arm: swing, or extended towards the route while pointing.
            pointWeight = Mathf.MoveTowards(pointWeight, guide.IsPointing ? 1f : 0f, dt * 3f);
            Quaternion point = Quaternion.identity;
            if (guide.IsPointing && rightArm != null && rightArm.parent != null)
            {
                Vector3 dirLocal = rightArm.parent.InverseTransformDirection(guide.PointDirection);
                dirLocal.y = 0.2f;
                point = Quaternion.FromToRotation(Vector3.down, dirLocal.normalized);
            }
            if (rightArm != null)
                rightArm.localRotation = Quaternion.Slerp(Quaternion.Euler(-s * armSwing * walk, 0f, 0f), point, pointWeight);
            if (rightElbow != null)
                rightElbow.localRotation = Quaternion.Slerp(Quaternion.Euler(-15f * walk, 0f, 0f), Quaternion.identity, pointWeight);
        }

        static void Swing(Transform t, float degrees)
        {
            if (t != null) t.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }
    }
}
