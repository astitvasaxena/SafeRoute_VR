using System.Collections.Generic;
using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Spoken instructor lines. Clips are matched to lines by their exact text, so the voice can only ever
    /// say what the speech bubble and panel subtitle show; a line without a recorded clip is shown as a
    /// subtitle only (and logged once). Sits on a 3D AudioSource at the instructor's head.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class InstructorVoice : MonoBehaviour
    {
        [System.Serializable]
        public class Line
        {
            [TextArea(1, 3)] public string text;
            public AudioClip clip;
        }

        [Tooltip("Recorded lines. The text must match the instructor line exactly (case and punctuation).")]
        public Line[] lines;

        public bool IsSpeaking => source != null && source.isPlaying;

        AudioSource source;
        Dictionary<string, AudioClip> clips;
        readonly HashSet<string> warned = new HashSet<string>();

        void Awake() => Init();

        void Init()
        {
            if (clips != null) return;
            source = GetComponent<AudioSource>();
            clips = new Dictionary<string, AudioClip>();
            if (lines == null) return;
            foreach (Line l in lines)
                if (l != null && !string.IsNullOrEmpty(l.text) && l.clip != null) clips[l.text.Trim()] = l.clip;
        }

        public bool HasClip(string text)
        {
            Init();
            return !string.IsNullOrEmpty(text) && clips.ContainsKey(text.Trim());
        }

        /// <summary>Speaks <paramref name="text"/>, replacing anything being said. Returns the clip length (0 if no clip).</summary>
        public float Play(string text)
        {
            Init();
            source.Stop();
            if (string.IsNullOrEmpty(text)) return 0f;
            if (!clips.TryGetValue(text.Trim(), out AudioClip clip))
            {
                if (warned.Add(text)) Debug.LogWarning($"[SafeRoute] No voice clip for instructor line \"{text}\" - subtitle only.", this);
                return 0f;
            }
            source.clip = clip;
            source.Play();
            return clip.length;
        }

        public void Stop()
        {
            Init();
            source.Stop();
        }
    }
}
