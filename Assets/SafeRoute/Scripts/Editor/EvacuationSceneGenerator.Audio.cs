using System.Collections.Generic;
using System.Linq;
using SafeRoute.Evacuation;
using UnityEditor;
using UnityEngine;

namespace SafeRoute.EditorTools
{
    public static partial class EvacuationSceneGenerator
    {
        const string AudioFolder = "Assets/SafeRoute/Audio";
        const string VoiceFolder = AudioFolder + "/Voice";
        const string SfxFolder = AudioFolder + "/SFX";

        // ---------- Instructor lines ----------
        // Every spoken line has a clip Audio/Voice/instructor_<id>.wav with exactly this wording
        // (Tools/Audio/voice_lines.csv). Clips are matched by text, so changing a line here without
        // re-recording it leaves that line as a subtitle only (reported after generation).

        const string LineEmergency = "Emergency detected. Stay calm and follow my instructions.";
        const string LineFollowToExitB = "Follow me towards Exit B.";
        const string LineExitABlockedTurnLeft = "Exit A is blocked by fire. Turn left and follow me towards Exit B.";
        const string LineExitABlocked = "Exit A is blocked. Do not approach the fire.";
        const string LineElevator = "The elevator is unavailable. Do not use it during this emergency.";
        const string LineStairs = "These stairs are not a way out. Stay in the corridor.";
        const string LineRestricted = "Stay away from the restricted classroom and laboratory.";
        const string LineExitBClear = "Exit B is clear. Go through the doors.";
        const string LineExitBReached = "Well done. Keep going to the assembly point.";
        const string LineKeepMoving = "Keep moving along the marked evacuation route.";
        const string LineAssembly = "You have reached the assembly point. Please remain here.";
        const string LineCatchUp = "Stay with me. This way please.";
        const string LineReturn = "Come back to the corridor and follow me towards Exit B.";

        static readonly (string id, string text)[] VoiceLines =
        {
            ("emergency_detected", LineEmergency),
            ("follow_to_exit_b", LineFollowToExitB),
            ("exit_a_blocked_turn_left", LineExitABlockedTurnLeft),
            ("exit_a_blocked", LineExitABlocked),
            ("elevator_unavailable", LineElevator),
            ("stairs_not_exit", LineStairs),
            ("restricted_rooms", LineRestricted),
            ("exit_b_clear", LineExitBClear),
            ("exit_b_reached", LineExitBReached),
            ("keep_moving", LineKeepMoving),
            ("assembly_reached", LineAssembly),
            ("catch_up", LineCatchUp),
            ("return_to_route", LineReturn),
        };

        static readonly List<string> missingAudio = new List<string>();

        static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) missingAudio.Add(path);
            return clip;
        }

        /// <summary>
        /// AudioSource with a custom inverse-distance rolloff that reaches silence at <paramref name="maxDistance"/>
        /// (Unity's logarithmic mode never fades out completely, so distant sounds would leak everywhere).
        /// </summary>
        static AudioSource Source(GameObject go, AudioClip clip, float volume, float minDistance, float maxDistance, bool loop, float spatialBlend = 1f)
        {
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.volume = volume;
            s.loop = loop;
            s.playOnAwake = loop && clip != null;
            s.spatialBlend = spatialBlend;
            s.dopplerLevel = 0f;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.rolloffMode = AudioRolloffMode.Custom;
            float[] distances = new[] { 0f, minDistance, minDistance * 1.5f, minDistance * 2f, minDistance * 3f, minDistance * 5f, maxDistance * 0.7f }
                .Where(d => d <= maxDistance * 0.7f).Distinct().OrderBy(d => d).ToArray();
            var keys = distances.Select(d => new Keyframe(d / maxDistance, Mathf.Min(1f, minDistance / Mathf.Max(d, minDistance)))).ToList();
            keys.Add(new Keyframe(1f, 0f));
            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
            return s;
        }

        // ---------- Instructor voice ----------

        /// <summary>Voice source at mouth height. Mostly 3D (comes from the instructor) with a little 2D so it never becomes inaudible.</summary>
        static InstructorVoice BuildInstructorVoice(Transform instructorRoot)
        {
            var go = new GameObject("Voice (3D audio)");
            go.transform.SetParent(instructorRoot, false);
            go.transform.localPosition = new Vector3(0f, 1.62f, 0.08f);
            AudioSource source = Source(go, null, 1f, 2f, 30f, false, 0.85f);
            source.priority = 16;
            var voice = go.AddComponent<InstructorVoice>();
            voice.lines = VoiceLines.Select(l => new InstructorVoice.Line { text = l.text, clip = LoadClip($"{VoiceFolder}/instructor_{l.id}.wav") }).ToArray();
            return voice;
        }

        // ---------- Fire ----------

        /// <summary>Crackle and pops at a fire. Smaller fires (lower <paramref name="volume"/>, 0.7 = Exit A) are also audible over a shorter distance.</summary>
        static void BuildFireAudio(Transform p, ParticleSystem flames, Vector3 position, float volume)
        {
            var go = new GameObject("Fire Audio (3D)");
            go.transform.SetParent(p, false);
            go.transform.localPosition = position;
            var fire = go.AddComponent<FireAudio>();
            fire.fire = flames;
            float reach = Mathf.Clamp01(volume / 0.7f);
            fire.crackle = Source(go, LoadClip(SfxFolder + "/fire_crackle_loop.wav"), volume, 1.5f, Mathf.Lerp(14f, 22f, reach), true);
            fire.pops = Source(go, null, Mathf.Min(1f, volume * 1.15f), 1.5f, Mathf.Lerp(12f, 18f, reach), false);
            fire.popClips = Enumerable.Range(1, 4).Select(i => LoadClip($"{SfxFolder}/fire_pop_{i:00}.wav")).Where(c => c != null).ToArray();
        }

        /// <summary>2D "fire contact" impact played by the restart sequence (heard at full level wherever the trainee is).</summary>
        static void BuildImpactAudio(TrainingRestart restart)
        {
            restart.impactClip = LoadClip(SfxFolder + "/fire_contact_burst.wav");
            restart.impactSource = Source(restart.gameObject, null, 0.9f, 1f, 10f, false, 0f);
            restart.impactSource.priority = 8;
        }

        // ---------- Alarm and acoustics ----------

        /// <summary>Three ceiling sounders along the corridor plus a reverb zone covering the building interior.</summary>
        static void BuildAlarm(Transform p, EvacuationManager manager, InstructorVoice voice)
        {
            Transform root = Group("Fire Alarm (3D audio)", p);
            AudioClip clip = LoadClip(SfxFolder + "/alarm_temporal3_loop.wav");
            var alarm = root.gameObject.AddComponent<AlarmAudio>();
            alarm.manager = manager;
            alarm.voice = voice;
            alarm.sounders = new[] { 6f, 16f, 26f }.Select(x =>
            {
                var sounder = new GameObject("Alarm Sounder");
                sounder.transform.SetParent(root, false);
                sounder.transform.localPosition = new Vector3(x, CeilingHeight - 0.15f, 1.5f);
                return Source(sounder, clip, 0.22f, 2.5f, 22f, true);
            }).ToArray();

            var reverb = new GameObject("Interior Reverb Zone");
            reverb.transform.SetParent(p, false);
            reverb.transform.localPosition = new Vector3(16f, 1.5f, 2.5f);
            var zone = reverb.AddComponent<AudioReverbZone>();
            zone.reverbPreset = AudioReverbPreset.Room;
            zone.minDistance = 15f;
            zone.maxDistance = 18f;
        }

        static string AudioReport() =>
            $"Audio: {VoiceLines.Length - missingAudio.Count(m => m.StartsWith(VoiceFolder))}/{VoiceLines.Length} voice clips, " +
            $"{(missingAudio.Any(m => m.StartsWith(SfxFolder)) ? "SFX MISSING" : "fire + alarm SFX found")}" +
            (missingAudio.Count > 0 ? "\nMissing (subtitles/silence instead):\n  " + string.Join("\n  ", missingAudio.Distinct()) : "");
    }
}
