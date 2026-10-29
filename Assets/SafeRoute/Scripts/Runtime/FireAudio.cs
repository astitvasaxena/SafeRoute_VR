using UnityEngine;

namespace SafeRoute.Evacuation
{
    /// <summary>
    /// Fire sound at the hazard: a looping crackle plus occasional pops with varied pitch and volume. Both are
    /// 3D sources, so they get quieter with distance. Fades out when the fire particle system stops emitting
    /// or is disabled, and fades back in if it restarts.
    /// </summary>
    public class FireAudio : MonoBehaviour
    {
        public AudioSource crackle;
        public AudioSource pops;
        public AudioClip[] popClips;
        [Tooltip("Flames this sound belongs to. Empty = always burning.")]
        public ParticleSystem fire;

        public Vector2 popInterval = new Vector2(1.5f, 5f);
        public Vector2 popVolume = new Vector2(0.3f, 0.8f);
        public Vector2 popPitch = new Vector2(0.85f, 1.2f);
        public float fadeTime = 1.5f;

        float crackleVolume, nextPop;

        bool IsBurning => fire == null || (fire.gameObject.activeInHierarchy && fire.isEmitting && fire.emission.enabled);

        void Start()
        {
            if (crackle != null)
            {
                crackleVolume = crackle.volume;
                crackle.loop = true;
                // Random start point so several fires (or a restart) never sound identical.
                if (crackle.clip != null) crackle.time = Random.Range(0f, crackle.clip.length * 0.9f);
                if (IsBurning)
                {
                    if (!crackle.isPlaying) crackle.Play();
                }
                else
                {
                    // Not alight yet (fire spread): start silent and fade in when it ignites.
                    crackle.volume = 0f;
                    crackle.Stop();
                }
            }
            nextPop = Time.time + Random.Range(popInterval.x, popInterval.y);
        }

        void Update()
        {
            bool burning = IsBurning;

            if (crackle != null)
            {
                float step = crackleVolume / Mathf.Max(0.01f, fadeTime) * Time.deltaTime;
                crackle.volume = Mathf.MoveTowards(crackle.volume, burning ? crackleVolume : 0f, step);
                if (!burning && crackle.volume <= 0f && crackle.isPlaying) crackle.Stop();
                else if (burning && !crackle.isPlaying) crackle.Play();
            }

            if (burning && pops != null && popClips != null && popClips.Length > 0 && Time.time >= nextPop)
            {
                pops.pitch = Random.Range(popPitch.x, popPitch.y);
                AudioClip clip = popClips[Random.Range(0, popClips.Length)];
                if (clip != null) pops.PlayOneShot(clip, Random.Range(popVolume.x, popVolume.y));
                nextPop = Time.time + Random.Range(popInterval.x, popInterval.y);
            }
        }
    }
}
