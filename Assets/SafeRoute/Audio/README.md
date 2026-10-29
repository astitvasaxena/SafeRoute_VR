# SafeRoute VR – audio

All audio here was produced offline on the development PC. Nothing was downloaded and no online or paid service was used.

## Instructor voice: `Voice/instructor_<id>.wav` (13 clips, PLACEHOLDERS)

| What | Status |
|------|--------|
| Source | Synthetic text-to-speech, made with the Windows built-in offline voice "Microsoft Mark (en-US)" by `Tools/Audio/generate_voice_placeholders.ps1`, then trimmed and loudness-matched by `Tools/Audio/process_audio.py voice`. |
| Quality | Clear and intelligible, but robotic. **These are not natural recordings by a professional voice artist.** |
| Licensing | Generated with a voice that ships with Windows. Fine for a private or educational prototype. **Check Microsoft's terms before any public or commercial release**, or replace the clips (recommended). |
| Wording check | Each clip was checked with offline Windows speech recognition, and all 13 matched their line exactly. |

The exact wording of each line is in `Tools/Audio/voice_lines.csv`. The same wording is in `EvacuationSceneGenerator.Audio.cs` (`Line...` constants).

**How to replace with real recordings:** record each line *exactly as written*. Save it as a mono WAV with the same file name, then run *SafeRoute > Generate Evacuation Scene* again. Clips are matched to subtitles by their exact text. If you change a line's wording without re-recording it, that line shows as a subtitle only, and the generator reports it as missing.

## Fire and alarm: `SFX/` (procedurally synthesised)

| File | What it is |
|------|------------|
| `fire_crackle_loop.wav` | 12 s seamless loop: low roar, airy hiss and random crackles |
| `fire_pop_01..04.wav` | Short pops/snaps, played at random intervals with random pitch and volume |
| `alarm_temporal3_loop.wav` | 4 s loop of the international "temporal-3" evacuation pattern (ISO 8201), 950 Hz tone with soft edges |
| `fire_contact_burst.wav` | 1.4 s non-graphic "fire contact" impact (flame whoosh, soft thump, crackles), played once when the trainee walks into the fire |

- Source: synthesised by `Tools/Audio/process_audio.py sfx`. This is original work with no third-party recordings, so there is no licence restriction.
- Quality: believable, but synthetic. For more realism, replace them with licensed field recordings (for example CC0 recordings) under the same file names.
- Levels: peaks at −3 to −6 dBFS, so there is no clipping. The loops have no audible seam.

## How it is used in the scene

| Sound | Source | Behaviour |
|-------|--------|-----------|
| Instructor voice | `InstructorVoice` at the instructor's mouth | 85 % 3D, so it comes from the instructor but never becomes inaudible. Plays one line at a time. Warnings interrupt; other lines wait for the current one. The same line is not repeated within 6 s. The speech bubble and panel subtitle show the same text. |
| Fire crackle + pops | `FireAudio` at the Exit A fire | Fully 3D and silent beyond about 22 m. Fades out if the flames stop emitting. |
| Fire alarm | `AlarmAudio`, 3 ceiling sounders in the corridor | Fully 3D and quiet outside. Ducked to 30 % while the instructor speaks. Fades out when the training is complete or an attempt fails, and starts again for a new attempt. |
| Room acoustics | `AudioReverbZone` (Room preset) | Covers the building interior only. |
| Fire contact impact | `TrainingRestart` (2D) | Plays once when the attempt fails. At the same moment the alarm fades out over 0.5 s and the instructor's voice stops. Both restart with the new attempt. |
