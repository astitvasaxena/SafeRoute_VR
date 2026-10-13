using UnityEngine;
using UnityEngine.UI;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Floating arrow in front of the user that points at the next safe-route waypoint,
    /// with a distance label. Waypoints must only lie on the safe route.
    /// </summary>
    public class RouteGuide : MonoBehaviour
    {
        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        [Tooltip("Ordered safe-route waypoints, placed on the floor.")]
        public Transform[] waypoints;
        [Tooltip("Index of the waypoint at the safe exit; later waypoints lead to the assembly point.")]
        public int exitWaypointIndex;
        public string exitName = "Exit B";
        public string assemblyName = "Assembly point";

        public Transform arrow;
        public Transform labelRoot;
        public Text label;

        public float reachRadius = 1.5f;
        public float forwardDistance = 1.2f;
        public float heightOffset = -0.55f;
        public float turnSpeed = 8f;

        int index;
        bool hidden;

        public void Hide()
        {
            hidden = true;
            if (arrow != null) arrow.gameObject.SetActive(false);
            if (labelRoot != null) labelRoot.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (hidden || arrow == null || waypoints == null || waypoints.Length == 0) return;
            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }

            Vector3 headPos = head.position;
            for (int i = index; i < waypoints.Length - 1; i++)
                if (FlatDistance(headPos, waypoints[i].position) < reachRadius) index = i + 1;

            Vector3 forward = head.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;

            Vector3 pos = headPos + forward * forwardDistance + Vector3.up * heightOffset;
            arrow.position = pos;
            Vector3 dir = waypoints[index].position - pos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f)
                arrow.rotation = Quaternion.Slerp(arrow.rotation, Quaternion.LookRotation(dir), Time.deltaTime * turnSpeed);

            if (labelRoot != null)
            {
                labelRoot.position = pos + Vector3.up * 0.2f;
                labelRoot.rotation = Quaternion.LookRotation(labelRoot.position - headPos);
            }

            if (label != null)
            {
                bool toExit = index <= exitWaypointIndex;
                int target = toExit ? exitWaypointIndex : waypoints.Length - 1;
                label.text = $"{(toExit ? exitName : assemblyName)}: {DistanceTo(headPos, target):0} m";
            }
        }

        float DistanceTo(Vector3 from, int target)
        {
            float d = FlatDistance(from, waypoints[index].position);
            for (int i = index; i < target; i++)
                d += FlatDistance(waypoints[i].position, waypoints[i + 1].position);
            return d;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
