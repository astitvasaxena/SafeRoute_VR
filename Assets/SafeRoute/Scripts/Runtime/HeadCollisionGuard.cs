using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Keeps the tracked head out of solid geometry. Locomotion through the CharacterController already
    /// collides, but moving the head itself (room-scale walking, or the XR Device Simulator's default
    /// WASD "translate HMD" keys) bypasses every collider. When the head would pass into a non-trigger
    /// collider, the XR rig is shifted back so the head stops at the surface, which makes walls and the
    /// restricted-doorway blockers effective for both kinds of movement.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HeadCollisionGuard : MonoBehaviour
    {
        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        [Tooltip("XR Origin root that is moved to correct the head position.")]
        public Transform rig;
        [Tooltip("Smaller than the CharacterController radius so normal locomotion along walls is not disturbed.")]
        public float radius = 0.08f;
        public LayerMask solidLayers = Physics.DefaultRaycastLayers;
        [Tooltip("Larger jumps in one frame are treated as teleports and not corrected.")]
        public float maxStep = 1f;

        Vector3 lastHead;
        bool initialised;
        readonly RaycastHit[] hits = new RaycastHit[16];

        /// <summary>Forget the previous head position (call after deliberately moving the rig).</summary>
        public void ResetTracking() => initialised = false;

        void LateUpdate()
        {
            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }
            if (rig == null) return;

            Vector3 current = head.position;
            if (!initialised)
            {
                lastHead = current;
                initialised = true;
                return;
            }

            Vector3 move = current - lastHead;
            float distance = move.magnitude;
            if (distance > 1e-4f && distance <= maxStep)
            {
                Vector3 dir = move / distance;
                float nearest = float.MaxValue;
                int count = Physics.SphereCastNonAlloc(lastHead, radius, dir, hits, distance, solidLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider c = hits[i].collider;
                    if (c == null || c.transform.IsChildOf(rig)) continue;   // the rig's own CharacterController
                    if (hits[i].distance <= 0f && hits[i].point == Vector3.zero) continue; // started overlapping: ignore
                    nearest = Mathf.Min(nearest, hits[i].distance);
                }

                if (nearest < float.MaxValue)
                {
                    Vector3 allowed = lastHead + dir * Mathf.Max(0f, nearest - 0.01f);
                    Vector3 correction = allowed - current;
                    rig.position += correction;
                    Physics.SyncTransforms();
                    current = head.position;
                }
            }
            lastHead = current;
        }
    }
}
