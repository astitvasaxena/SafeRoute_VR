using UnityEngine;
using UnityEngine.UI;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Small world-space panel that lazily follows the user's view and shows the briefing,
    /// warnings and the final training result.
    /// </summary>
    public class InstructionPanel : MonoBehaviour
    {
        public enum Tone { Info, Danger, Safe }

        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        public Text title;
        public Text body;
        public Image background;

        public float distance = 1.8f;
        public float heightOffset = 0.25f;
        [Tooltip("Panel re-centres when it drifts this many degrees from the view direction.")]
        public float followAngle = 30f;
        public float followSpeed = 3f;

        public Color infoColor = new Color(0.08f, 0.16f, 0.32f, 0.88f);
        public Color dangerColor = new Color(0.45f, 0.05f, 0.05f, 0.9f);
        public Color safeColor = new Color(0.05f, 0.32f, 0.12f, 0.9f);

        bool placed;
        bool following = true;

        public void Show(string heading, string message, Tone tone)
        {
            if (title != null) title.text = heading;
            if (body != null) body.text = message;
            if (background != null)
                background.color = tone switch
                {
                    Tone.Danger => dangerColor,
                    Tone.Safe => safeColor,
                    _ => infoColor
                };
            following = true;
        }

        void LateUpdate()
        {
            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }

            Vector3 forward = Flat(head.forward);
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 target = head.position + forward * distance + Vector3.up * heightOffset;

            if (!placed)
            {
                transform.position = target;
                placed = true;
            }

            Vector3 toPanel = Flat(transform.position - head.position);
            if (Vector3.Angle(forward, toPanel) > followAngle || Mathf.Abs(toPanel.magnitude - distance) > 0.6f)
                following = true;

            if (following)
            {
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * followSpeed);
                if ((transform.position - target).sqrMagnitude < 0.0025f) following = false;
            }

            transform.rotation = Quaternion.LookRotation(transform.position - head.position);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
