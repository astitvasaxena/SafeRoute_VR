using UnityEngine;
using UnityEngine.UI;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Full-view colour overlay attached in front of the XR camera: a short warm flash and tint when the trainee
    /// is burned (no injury imagery) and a fade to dark and back for the restart. The canvas is disabled while
    /// fully clear, so it costs nothing during normal training.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class ScreenTint : MonoBehaviour
    {
        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        public Image image;
        [Tooltip("Distance in front of the eyes (must be beyond the camera's near clip plane).")]
        public float distance = 0.25f;

        public Color flashColor = new Color(1f, 0.8f, 0.55f, 0.9f);
        public Color burnColor = new Color(0.85f, 0.3f, 0.06f, 0.4f);
        public float flashTime = 0.8f;
        public Color darkColor = new Color(0.03f, 0.02f, 0.015f, 1f);

        Canvas canvas;
        Color from, to;
        float elapsed, duration;
        bool attached;

        public bool IsClear => image == null || image.color.a <= 0.001f;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            if (image != null) image.color = Color.clear;
            canvas.enabled = false;
        }

        void Attach()
        {
            if (attached) return;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;
            // Parented to the camera so the overlay never lags behind head movement.
            transform.SetParent(head, false);
            transform.localPosition = Vector3.forward * distance;
            transform.localRotation = Quaternion.identity;
            attached = true;
        }

        /// <summary>Warm flash that settles into a light orange tint.</summary>
        public void Burn()
        {
            Attach();
            if (image != null) image.color = flashColor;
            FadeTo(burnColor, flashTime);
        }

        /// <summary>Fades from the current colour to <paramref name="target"/> (Color.clear to remove the overlay).</summary>
        public void FadeTo(Color target, float time)
        {
            Attach();
            if (image == null) return;
            from = image.color;
            to = target;
            elapsed = 0f;
            duration = Mathf.Max(0.01f, time);
            if (canvas == null) canvas = GetComponent<Canvas>();
            canvas.enabled = true;
        }

        void Update()
        {
            if (image == null || canvas == null || !canvas.enabled) return;
            if (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                image.color = Color.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            }
            else if (IsClear) canvas.enabled = false;
        }
    }
}
