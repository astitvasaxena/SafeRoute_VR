using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Fatal area around a fire: a vertical cylinder centred on this transform. When the trainee's head or body
    /// (the XR Origin's CharacterController) enters it while the fire is burning, the attempt fails through
    /// <see cref="TrainingRestart"/>. It fires once per attempt: it remembers the attempt number it fired on and is
    /// armed again as soon as the manager starts a new attempt.
    /// </summary>
    public class FireDangerZone : MonoBehaviour
    {
        [Tooltip("Flames that make this zone dangerous. The zone is ignored while they are not burning (empty = always burning).")]
        public ParticleSystem fire;
        [Tooltip("Horizontal distance from the fire that counts as fire contact (metres).")]
        [Min(0.1f)] public float radius = 1.4f;
        [Tooltip("Height of the danger cylinder above this point (metres).")]
        [Min(0.1f)] public float height = 3f;
        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        [Tooltip("Optional trainee body: the XR Origin's CharacterController.")]
        public CharacterController body;
        public TrainingRestart restart;
        [Tooltip("Optional: only points inside this zone count, e.g. the Exit A warning zone, so the cylinder never reaches through walls to the outside.")]
        public EvacuationZone limitTo;
        [Tooltip("Recorded in the training result.")]
        public string reason = "fire contact at Exit A";

        int firedOnAttempt = -1;

        public bool IsArmed => restart == null || restart.manager == null || restart.manager.Attempt != firedOnAttempt;
        public bool IsBurning => fire == null || (fire.gameObject.activeInHierarchy && fire.isEmitting && fire.emission.enabled);

        public bool Contains(Vector3 worldPoint)
        {
            if (limitTo != null && !limitTo.Contains(worldPoint)) return false;
            Vector3 d = worldPoint - transform.position;
            if (d.y < -0.5f || d.y > height) return false;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        void Update() => Evaluate();

        /// <summary>Checks the trainee against the zone; returns true when this call triggered the failure.</summary>
        public bool Evaluate()
        {
            if (restart == null || restart.manager == null || !IsArmed || !IsBurning) return false;
            if (head == null)
            {
                if (Camera.main == null) return false;
                head = Camera.main.transform;
            }

            bool inside = Contains(head.position) || (body != null && body.enabled && Contains(body.transform.TransformPoint(body.center)));
            if (!inside) return false;
            int attempt = restart.manager.Attempt;
            if (!restart.TriggerFailure(reason)) return false;
            firedOnAttempt = attempt;
            return true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.25f, 0f);
            Vector3 c = transform.position;
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Gizmos.DrawLine(p0, p1);
                Gizmos.DrawLine(p0 + Vector3.up * height, p1 + Vector3.up * height);
                if (i % 8 == 0) Gizmos.DrawLine(p0, p0 + Vector3.up * height);
            }
        }
    }
}
