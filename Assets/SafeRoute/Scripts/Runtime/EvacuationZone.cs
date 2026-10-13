using UnityEngine;

namespace SafeRoute.Evacuation
{
    public enum ZoneType
    {
        BlockedExit,      // exit made unusable by a hazard (smoke, fire)
        UnavailableRoute, // route that must not be used in a fire (elevator)
        SafeExit,         // designated safe exit
        AssemblyPoint     // completes the exercise
    }

    /// <summary>
    /// Box volume describing an evacuation-relevant area. Detection is done against the
    /// user's head position, so it works for both simulator head movement and locomotion.
    /// Independent of how the environment was built: tag any building model with these.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EvacuationZone : MonoBehaviour
    {
        public ZoneType zoneType;
        public string displayName = "Zone";
        [TextArea(2, 6)] public string explanation;

        BoxCollider box;

        public bool IsUnsafe => zoneType == ZoneType.BlockedExit || zoneType == ZoneType.UnavailableRoute;

        public bool Contains(Vector3 worldPoint)
        {
            if (box == null) box = GetComponent<BoxCollider>();
            Vector3 local = transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnDrawGizmos()
        {
            if (box == null) box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = zoneType switch
            {
                ZoneType.BlockedExit => Color.red,
                ZoneType.UnavailableRoute => new Color(1f, 0.5f, 0f),
                ZoneType.SafeExit => Color.green,
                _ => Color.cyan
            };
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
