using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Building fire alarm played from 3D sounders in the corridor (quieter outside). It is ducked while the
    /// instructor speaks so guidance stays intelligible, fades out when the training is complete, cuts out
    /// quickly when an attempt fails, and starts again with a new attempt.
    /// </summary>
    public class AlarmAudio : MonoBehaviour
    {
        public AudioSource[] sounders;
        [Tooltip("Optional: the alarm is turned down while the instructor is speaking.")]
        public InstructorVoice voice;
        [Tooltip("Optional: the alarm follows this manager's completion, failure and restart.")]
        public EvacuationManager manager;

        [Range(0f, 1f)] public float duckedLevel = 0.3f;
        public float duckSpeed = 3f;
        public float fadeOutTime = 4f;
        [Tooltip("Fade when an attempt fails, so the failure sound and message stand out.")]
        public float failFadeTime = 0.5f;

        float[] baseVolumes;
        float duck = 1f, master = 1f, fadeTime;
        bool silencing, stopped;

        void OnEnable()
        {
            if (manager == null) return;
            manager.Completed += Silence;
            manager.Failed += OnFailed;
            manager.AttemptStarted += Restart;
        }

        void OnDisable()
        {
            if (manager == null) return;
            manager.Completed -= Silence;
            manager.Failed -= OnFailed;
            manager.AttemptStarted -= Restart;
        }

        void Start() => CacheVolumes();

        void CacheVolumes()
        {
            if (baseVolumes != null) return;
            baseVolumes = new float[sounders != null ? sounders.Length : 0];
            for (int i = 0; i < baseVolumes.Length; i++)
                if (sounders[i] != null) baseVolumes[i] = sounders[i].volume;
        }

        /// <summary>Fades the alarm out (scenario finished).</summary>
        public void Silence() => Silence(fadeOutTime);

        public void Silence(float time)
        {
            silencing = true;
            fadeTime = time;
        }

        void OnFailed(string reason) => Silence(failFadeTime);

        /// <summary>Starts the alarm again from the beginning (new attempt), all sounders in step.</summary>
        public void Restart()
        {
            CacheVolumes();
            silencing = stopped = false;
            master = duck = 1f;
            for (int i = 0; i < baseVolumes.Length; i++)
            {
                AudioSource s = sounders[i];
                if (s == null) continue;
                s.volume = baseVolumes[i];
                s.Stop();
                s.Play();
            }
        }

        void Update()
        {
            if (baseVolumes == null || stopped) return;
            float dt = Time.deltaTime;
            duck = Mathf.MoveTowards(duck, voice != null && voice.IsSpeaking ? duckedLevel : 1f, duckSpeed * dt);
            if (silencing) master = Mathf.MoveTowards(master, 0f, dt / Mathf.Max(0.01f, fadeTime));

            for (int i = 0; i < baseVolumes.Length; i++)
            {
                AudioSource s = sounders[i];
                if (s == null) continue;
                s.volume = baseVolumes[i] * duck * master;
                if (master <= 0f) s.Stop();
            }
            stopped = master <= 0f;
        }
    }
}
