using UnityEngine;
using UnityEngine.UI;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// World-space panel that lazily follows the user's view and shows the briefing,
    /// warnings and the final training result. It is pulled in front of walls so it is
    /// never hidden inside geometry in narrow corridors.
    /// Layout: a tone-coloured header bar (title and tone label), the body, the instructor subtitle strip and a
    /// status footer, stacked down from the panel's top edge and sized to their content, so short messages cover
    /// little of the view.
    /// </summary>
    public class InstructionPanel : MonoBehaviour
    {
        public enum Tone { Info, Danger, Safe }

        [Tooltip("User head (XR camera). Falls back to Camera.main if empty.")]
        public Transform head;
        public Text title;
        public Text body;
        public Image background;
        [Tooltip("Optional subtitle strip for the instructor's spoken lines (hidden when empty).")]
        public GameObject subtitleRoot;
        public Text subtitle;

        [Header("Layout (canvas px; the canvas pivot is its top edge and the panel grows downwards)")]
        [Tooltip("Header bar behind the title, coloured by tone. Without it the whole background is coloured instead.")]
        public Image header;
        [Tooltip("Small right-aligned label in the header naming the tone (e.g. WARNING).")]
        public Text toneLabel;
        [Tooltip("Footer row (route distance, restart countdown); collapsed while its text is empty.")]
        public RectTransform footer;
        [Tooltip("Footer text. Shared with RouteGuide.label; SetStatus writes to it.")]
        public Text status;
        public float headerHeight = 84f;
        [Tooltip("Space above and below the body text.")]
        public float bodyPadding = 20f;
        [Tooltip("Space above the subtitle line (holds the INSTRUCTOR caption) and below it.")]
        public float subtitleTop = 50f;
        public float subtitleBottom = 16f;
        public float footerHeight = 58f;
        public Color panelColor = new Color(0.075f, 0.08f, 0.09f, 1f);
        public string infoLabel = "GUIDANCE";
        public string dangerLabel = "WARNING";
        public string safeLabel = "SAFE";

        public float distance = 1.8f;
        public float heightOffset = 0.25f;
        [Tooltip("Panel re-centres when it drifts this many degrees from the view direction.")]
        public float followAngle = 30f;
        public float followSpeed = 3f;
        [Tooltip("Colliders that push the panel towards the user (walls). Triggers are ignored.")]
        public LayerMask occluders = Physics.DefaultRaycastLayers;
        public float minDistance = 0.6f;
        public float wallClearance = 0.15f;

        // Tone colours of the header bar (or of the whole background when there is no header). Opaque: see-through
        // panels lose contrast against bright walls and smoke.
        public Color infoColor = new Color(0.16f, 0.22f, 0.33f, 1f);
        public Color dangerColor = new Color(0.66f, 0.07f, 0.05f, 1f);
        public Color safeColor = new Color(0.0f, 0.42f, 0.19f, 1f);

        [Tooltip("Hide the panel this many seconds after the last message so it does not cover the way ahead " +
                 "(0 = always visible). It stays visible while the instructor is speaking and for pinned messages.")]
        public float hideAfter = 10f;

        bool placed;
        Vector3 baseScale;
        bool following = true;
        Canvas canvas;
        float shownAt;
        bool pinned, speaking;
        // Layout cache: the stack is re-measured only when what it shows changes (no per-frame text layout).
        bool layoutDirty = true;
        bool laidOutFooter;
        string laidOutBody;

        /// <summary>Shows what the instructor is saying; null or empty hides the strip.</summary>
        public void ShowSubtitle(string line)
        {
            bool visible = !string.IsNullOrEmpty(line);
            if (subtitle != null) subtitle.text = visible ? line : "";
            if (subtitleRoot != null && subtitleRoot.activeSelf != visible) subtitleRoot.SetActive(visible);
            speaking = visible;
            layoutDirty = true;
        }

        /// <summary>Writes the footer status line (e.g. the restart countdown); empty collapses the footer.</summary>
        public void SetStatus(string text)
        {
            if (status != null) status.text = text ?? "";
        }

        /// <summary>Clears the subtitle and pin and re-places the panel in front of the user (new attempt).</summary>
        public void ResetPanel()
        {
            pinned = false;
            ShowSubtitle(null);
            placed = false;
            following = true;
        }

        /// <summary>Shows a message. A pinned message (e.g. the final report) never auto-hides.</summary>
        public void Show(string heading, string message, Tone tone, bool pin = false)
        {
            shownAt = Time.time;
            pinned = pin;
            if (title != null) title.text = heading;
            if (body != null) body.text = message;
            Color toneColor = tone switch
            {
                Tone.Danger => dangerColor,
                Tone.Safe => safeColor,
                _ => infoColor
            };
            if (header != null)
            {
                header.color = toneColor;
                if (background != null) background.color = panelColor;
            }
            else if (background != null) background.color = toneColor;
            if (toneLabel != null)
                toneLabel.text = tone switch
                {
                    Tone.Danger => dangerLabel,
                    Tone.Safe => safeLabel,
                    _ => infoLabel
                };
            following = true;
            layoutDirty = true;
        }

        /// <summary>
        /// Stacks header, body, subtitle strip (while visible) and footer (while it has text) from the top edge and
        /// sizes the canvas to fit. Children are top-anchored rects stretched horizontally; only their heights change.
        /// </summary>
        public void RefreshLayout()
        {
            layoutDirty = false;
            laidOutBody = body != null ? body.text : null;
            laidOutFooter = status != null && !string.IsNullOrEmpty(status.text);
            if (header == null) return; // legacy fixed layout

            float y = headerHeight;
            Place(header.rectTransform, 0f, headerHeight);
            if (body != null)
            {
                bool hasBody = !string.IsNullOrEmpty(body.text);
                float h = hasBody ? Mathf.Ceil(body.preferredHeight) : 0f;
                Place(body.rectTransform, y + bodyPadding, h);
                if (hasBody) y += h + 2f * bodyPadding;
            }
            if (subtitleRoot != null && subtitleRoot.activeSelf && subtitle != null)
            {
                float h = Mathf.Ceil(subtitle.preferredHeight);
                Place(subtitle.rectTransform, subtitleTop, h);
                Place((RectTransform)subtitleRoot.transform, y, subtitleTop + h + subtitleBottom);
                y += subtitleTop + h + subtitleBottom;
            }
            if (footer != null)
            {
                if (footer.gameObject.activeSelf != laidOutFooter) footer.gameObject.SetActive(laidOutFooter);
                if (laidOutFooter)
                {
                    Place(footer, y, footerHeight);
                    y += footerHeight;
                }
            }
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, y);
        }

        static void Place(RectTransform rt, float top, float height)
        {
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -top);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        }

        void LateUpdate()
        {
            // The footer text is also written by RouteGuide, so its empty/non-empty state is polled (cheap).
            if (layoutDirty || (status != null && string.IsNullOrEmpty(status.text) == laidOutFooter)
                || (body != null && !ReferenceEquals(body.text, laidOutBody)))
                RefreshLayout();

            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }

            if (canvas == null) canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                bool visible = hideAfter <= 0f || pinned || speaking || Time.time - shownAt < hideAfter;
                if (canvas.enabled != visible) canvas.enabled = visible;
            }

            Vector3 forward = Flat(head.forward);
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 offset = forward * distance + Vector3.up * heightOffset;
            float reach = ClearDistance(head.position, offset);
            Vector3 target = head.position + offset * (reach / offset.magnitude);

            // Pulled in front of a nearby wall: shrink in proportion so the panel keeps the same apparent size.
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            float scale = Mathf.Clamp(Vector3.Distance(transform.position, head.position) / offset.magnitude, 0.2f, 1f);
            transform.localScale = baseScale * scale;

            if (!placed)
            {
                transform.position = target;
                placed = true;
            }

            Vector3 toPanel = Flat(transform.position - head.position);
            if (Vector3.Angle(forward, toPanel) > followAngle || Mathf.Abs(toPanel.magnitude - Flat(target - head.position).magnitude) > 0.6f
                || ClearDistance(head.position, transform.position - head.position) < (transform.position - head.position).magnitude - 0.01f)
                following = true;

            if (following)
            {
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * followSpeed);
                if ((transform.position - target).sqrMagnitude < 0.0025f) following = false;
            }

            transform.rotation = Quaternion.LookRotation(transform.position - head.position);
        }

        /// <summary>Distance along <paramref name="offset"/> that stays in front of the nearest wall.</summary>
        float ClearDistance(Vector3 origin, Vector3 offset)
        {
            float length = offset.magnitude;
            if (length < 1e-4f) return length;
            if (Physics.Raycast(origin, offset / length, out RaycastHit hit, length + wallClearance, occluders, QueryTriggerInteraction.Ignore))
                return Mathf.Clamp(hit.distance - wallClearance, Mathf.Min(minDistance, length), length);
            return length;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
