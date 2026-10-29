# SafeRoute VR: Fire Emergency and Evacuation Training Simulator

### A simple explanation of what we built, how it works and how to explain it

> **How to read the status labels in this document**
>
> | Label | Meaning |
> |-------|---------|
> | ✅ **Implemented** | The code exists, and the scene file in the project already contains it. |
> | 🆕 **Upgrade (28 Oct 2026)** | Audio, readability and graphics upgrade. The scene was regenerated, and one full Play-mode run (Exit B to the assembly point) completed without errors, according to Unity's log. Individual checks (how it sounds and looks) were not recorded. |
> | 🧪 **Fire contact (28 Oct 2026)** | Fire contact failure and restart. Unity's log shows one Play-mode run: attempt 1 failed by fire contact at 0:38, then attempt 2 completed via Exit B in 1:12. The EditMode tests are written; no record of them being run. |
> | 🔥 **New, not yet run in Unity (29 Oct 2026)** | Fire spread across several areas. The code compiles, and automated EditMode tests are written. The scene has **not been regenerated** with it yet, so nobody has seen it in Play mode. |
> | 🟡 **Partial** | It works, but it is a simplified placeholder. |
> | 📋 **Planned** | Future work. Not built. |

---

## 1. Project Introduction

**SafeRoute VR** is a virtual reality (VR) training program. It teaches a person what to do when there is a **fire in a college building**.

The trainee "stands" inside a classroom. A fire alarm starts and a virtual **instructor** (a fire-warden character) tells them to evacuate. The trainee must:

1. leave the classroom,
2. **not** use the nearest exit (Exit A), because it is blocked by fire and smoke,
3. **not** use the lift (elevator),
4. **not** go into other rooms or up the stairs,
5. follow the safe route to **Exit B**,
6. walk to the **assembly point** outside.

At the end, the program shows a **result report**. It lists which exit they used, how long they took, and whether they went near dangerous or restricted areas.

**Problem it solves:** in a real fire, people panic and often do the wrong thing. They walk into smoke, use the lift, or go back for belongings. Real fire drills are rare, take a lot of organising, and can never contain real fire or smoke. SafeRoute VR lets a student practise the *correct decisions* in a realistic but completely safe environment, as many times as they want.

---

## 2. Why We Made It

Why is VR useful for fire-safety training?

- **Safe:** you can show fire and smoke without any danger.
- **Learning by doing:** people remember what they *did* far better than what they *read*. Walking the route yourself builds "muscle memory".
- **Realistic decisions:** the closest exit is blocked, just like in many real fires. The trainee learns that the nearest exit is not always the safe one.
- **Repeatable and measurable:** every run is the same, and the program records mistakes (for example "entered the Lab"). That makes it fair for assessment.
- **Cheap:** no need to stop classes for a full drill.
- **Explains the "why":** every warning tells the trainee *why* something is unsafe (for example "a lift can stop or fill with smoke"), not just "don't go there".

---

## 3. How We Built It (step by step)

This order comes from the project's Git history and files.

| Step | What we did | Evidence in the project |
|------|-------------|------------------------|
| 1 | Created a **Unity 2022.3** project from the **URP** (Universal Render Pipeline) template | Git commit `e5b9f88` "Initial Unity 2022.3 URP template" |
| 2 | Added the **XR packages**: XR Interaction Toolkit 2.6.4, OpenXR, XR Plugin Management. Configured OpenXR (Oculus Touch controller profile) and imported the toolkit's **Starter Assets** and **XR Device Simulator** samples | Commit `2dee04a`, `Packages/manifest.json`, `Assets/XR/`, `Assets/Samples/` |
| 3 | Made a first test scene, `SafeRoute_Main.unity`: just an XR rig (the player), a light and a post-processing volume | `Assets/Scenes/SafeRoute_Main.unity` (left untouched) |
| 4 | Wrote the **training scripts** (zones, manager, instruction panel, route guide) and a **scene generator** that builds the whole evacuation scene automatically | Commit `0ff827a` "Checkpoint: SafeRoute evacuation scripts, generated materials and scene" |
| 5 | Improved it (uncommitted work): split the generator into topic files; added the **virtual instructor**, **restricted areas** with barriers, the **head-collision guard**, detailed building geometry, procedural textures and better lighting | `Assets/SafeRoute/Scripts/...` (modified/untracked in Git) |
| 6 | **Upgrade (28 Oct 2026):** spoken instructor voice with subtitles, fire and alarm sounds, more realistic materials, lighting, fire and smoke, plus readability improvements | New files `InstructorVoice.cs`, `FireAudio.cs`, `AlarmAudio.cs`, `EvacuationSceneGenerator.Audio.cs`, `Assets/SafeRoute/Audio/`, `Tools/Audio/` |
| 7 | **Teacher requirement (28 Oct 2026):** walking into the fire fails the attempt (burn effect, "FIRE CONTACT — EVACUATION FAILED"), then the simulation restarts from the classroom | New files `FireDangerZone.cs`, `TrainingRestart.cs`, `ScreenTint.cs`, `Editor/Tests/FireContactTests.cs` |
| 8 | **Teacher requirement (29 Oct 2026):** the fire spreads from Exit A to several nearby areas over time, each with its own size, smoke, light and danger zone | New `FireSpread.cs`, `Editor/Tests/FireSpreadTests.cs`; the hazard generator now describes every fire as a "fire patch" |
| 9 | **Realism upgrade (30 Oct 2026, not yet regenerated in Unity):** used-building grime and wear, contact shading, door hardware, fire damage (soot bands and plumes, scorched Exit A doors), layered animated flames with sparks and sooty smoke, organic fire-light flicker, emergency luminaires, illuminated ISO-style exit signs, a more detailed instructor (face, gloves, vest), and a compact instruction panel with a severity header and a restart countdown. Only appearance changed: fire timings, danger zones, routes and logic are unchanged. | New `Materials.Environment/Fire/Instructor/Signs.cs`, `FireDamage.cs`, `FireAmbientResponse.cs`; changes in the generator, `FlickerLight.cs`, `InstructionPanel.cs`, `TrainingRestart.cs` |
| 10 | **Real-world assets (30 Oct 2026, not yet regenerated in Unity):** free scanned textures (painted plaster walls, terrazzo floor, office ceiling, oak, concrete, paving, brushed steel, burnt wood), scanned school desks and chairs in Classroom 1, a real fire extinguisher (its Korean label replaced by an English one) and a manual call point, the official ISO exit/fire-equipment/assembly pictograms, and filmed flame and smoke flipbooks. Every asset is free to use (CC0, public domain, or CC-BY with credit) and listed with its source in `ThirdParty/CREDITS.md`. If the files are missing, the generator falls back to the procedural versions. | `Assets/SafeRoute/ThirdParty/`, `Tools/Assets/fetch_third_party_assets.py`, new `EvacuationSceneGenerator.ThirdParty.cs` |

### The big idea: the scene is *generated by code*

We did **not** build the building by hand, dragging objects into the scene. We wrote an **editor script**, `EvacuationSceneGenerator`, which adds a menu item **SafeRoute → Generate Evacuation Scene**. Clicking it:

1. creates textures and materials (colours and surfaces),
2. builds the building: floors, walls with door and window holes, ceilings, doors, stairs, lift, roof,
3. adds furniture, signs, lights, fire and smoke,
4. adds the training system: zones, route, instructor, instruction panel and sounds,
5. places the XR player rig and the XR Device Simulator,
6. saves everything as `Assets/Scenes/SafeRoute_Evacuation.unity`,
7. shows a **report** (how many cameras and XR rigs there are, whether the route check passed, which audio clips are present, and so on).

**Why do it this way?** If we want to change something (say, move a door), we change one number in code and regenerate. Everything stays consistent. It also keeps the *training logic* separate from the *building*. Later we can swap in our real college building and keep all the training scripts.

---

## 4. Technologies Used

| Technology | What it is (simple words) | How we use it |
|-----------|---------------------------|---------------|
| **Unity 2022.3.62f3** | A game engine: software for building 3D interactive worlds | The whole project |
| **C#** | The programming language Unity uses | All scripts (about 20 files) |
| **URP 14** (Universal Render Pipeline) | Unity's rendering system, which decides how things are drawn: lighting, shadows, materials. Fast enough for VR | Lighting, materials, post-processing (tonemapping, bloom) |
| **XR Interaction Toolkit 2.6.4** | Unity's ready-made VR toolkit: the player rig (camera, hands), movement and interaction | We use its **"XR Interaction Setup"** prefab, which gives the XR Origin (player), locomotion (moving) and the input managers |
| **XR Device Simulator** | Lets you test VR **without a headset**: keyboard and mouse pretend to be the headset and controllers | Our main testing tool (we have no physical headset) |
| **OpenXR 1.14 + XR Plugin Management** | A standard that lets one app work on many headsets (Quest, Index, etc.) | Configured, ready for a real headset |
| **Input System 1.14** | Unity's newer input system (keyboard, controllers) | Used by the XR toolkit and the simulator |
| **Unity UI (uGUI) world-space canvases** | 2D text and images placed *in the 3D world* | Signs, instruction panel, speech bubble, subtitles |
| **Particle System** | Many small images (particles) that together look like fire or smoke | Flames, embers, smoke |
| **Unity Audio (3D AudioSource, Reverb Zone)** | Sound that comes from a position in 3D space and gets quieter with distance | Instructor voice, fire, alarm (🆕) |
| **Git** | Version control (saves checkpoints of the project) | Three commits so far |
| **Python + NumPy** | Programming language plus a maths library | 🆕 Offline tool that synthesises the fire and alarm sounds and cleans up the voice clips |
| **Windows offline text-to-speech** | The speech engine built into Windows | 🆕 Made the *placeholder* instructor voice clips |

TextMeshPro is installed as a package but **not used**. We use the simpler built-in UI Text.

---

## 5. Project Architecture

### Main folders

```
SafeRoute_VR/
├── Assets/
│   ├── Scenes/
│   │   ├── SafeRoute_Evacuation.unity   ← the training scene (generated by code)
│   │   └── SafeRoute_Main.unity         ← first simple XR test scene
│   ├── SafeRoute/                       ← OUR project files
│   │   ├── Scripts/Runtime/             ← scripts that run while playing
│   │   ├── Scripts/Editor/              ← the scene generator (runs only in the Unity Editor)
│   │   ├── Materials/                   ← generated surface materials (walls, floor, ...)
│   │   ├── Textures/                    ← generated texture images
│   │   ├── Audio/                       ← 🆕 voice + fire/alarm sounds (+ README)
│   │   └── Settings/                    ← post-processing profile
│   ├── Samples/XR Interaction Toolkit/  ← Unity's Starter Assets + Device Simulator
│   ├── Settings/                        ← URP quality settings (from the template)
│   └── XR/                              ← OpenXR configuration
├── Packages/manifest.json               ← list of Unity packages
└── Tools/Audio/                         ← 🆕 scripts that make/clean the audio files
```

### Runtime scripts (run during Play mode)

| Script | Job in one sentence |
|--------|--------------------|
| `EvacuationManager.cs` | The **brain**. Checks which zone the trainee's head is in, shows warnings, records mistakes, and produces the final report. |
| `EvacuationZone.cs` | An invisible **box** that marks an area (blocked exit, lift, safe exit, assembly point, restricted room) with its message. |
| `InstructionPanel.cs` | The floating **instruction board** in front of the trainee (title, message, subtitles, distance). |
| `RouteGuide.cs` | Works out the **distance** along the safe route to Exit B, then to the assembly point. |
| `InstructorGuide.cs` | The **virtual instructor's behaviour**: walks ahead, waits, talks, warns, points. |
| `InstructorPlaceholderAnimator.cs` | Makes the placeholder body **move** (walking legs, arm swing, beckoning wave, pointing). |
| `InstructorVoice.cs` | 🆕 Plays the **spoken voice clip** that matches the line being shown. |
| `FireAudio.cs` | 🆕 Fire **crackling and popping** sound at the fire. |
| `AlarmAudio.cs` | 🆕 Building **fire alarm** sound. Turns down while the instructor talks and fades out at the end. |
| `FireDangerZone.cs` | 🧪 An invisible **cylinder around a fire** (radius set in the Inspector, e.g. 1.4 m at Exit A). Entering it while that fire burns counts as fire contact. 🔥 Every dangerous fire patch has one. |
| `FireSpread.cs` | 🔥 Makes the fire **spread**. Each fire patch has an ignition time; at that time it catches fire small and grows to full size over several seconds (flames, smoke, light). It starts again from Exit A on every new attempt. |
| `TrainingRestart.cs` | 🧪 The **failure and restart sequence**: burn sound, failure message, fade to dark, move the player back to the start, start a new attempt. |
| `ScreenTint.cs` | 🧪 A see-through colour layer just in front of the eyes, used for the **warm burn flash** and the **fade to dark** (no injury pictures). |
| `HeadCollisionGuard.cs` | Stops the trainee's **head from passing through walls** (important in the simulator). |
| `FlickerLight.cs` | Makes the fire's light **flicker** naturally. |

### Editor scripts (the scene generator)

One big class, `EvacuationSceneGenerator`, split into topic files ("partial class"):

| File | Builds |
|------|--------|
| `EvacuationSceneGenerator.cs` | The menu item, the overall build order, saving, and the final report |
| `.Materials.cs` | Procedural textures (vinyl floor, paint, concrete, wood, smoke and flame images) and materials |
| `.Geometry.cs` | Helper functions: boxes with correct texture scale, 🆕 bevelled boxes, walls with openings |
| `.Architecture.cs` | Floors, walls, ceilings, doors, windows, stairwell, lift, roof |
| `.Props.cs` | Desks, chairs, lockers, lab benches, extinguishers, outdoor benches, hedges, nearby buildings |
| `.Signage.cs` | Exit signs, room labels, hanging signs, floor markings |
| `.Hazards.cs` | Fire, smoke, embers, burning debris, soot, fire light, 🆕 fire sound |
| `.Training.cs` | Zones, restricted areas, safe route, instructor, instruction panel |
| `.Lighting.cs` | Sun, ambient light, ceiling lights, reflection probes, post-processing, camera settings |
| `.Audio.cs` | 🆕 The instructor's lines, voice/fire/alarm sound sources, reverb |
| `.ThirdParty.cs` | 🆕 Swaps in the real-world textures, models, sign artwork and flipbooks from `Assets/SafeRoute/ThirdParty/` when they are present (otherwise the procedural ones stay) |

---

## 6. How the Project Works: the trainee's journey

The building layout (top view):

```
 NORTH   [ Classroom 1 (START) ][ Classroom 2 (NO ENTRY) ][ Lab (NO ENTRY) ]
 Exit A  ===================== CORRIDOR (32 m long) =======================  Exit B ──► path ──► ASSEMBLY POINT
 (FIRE)    [ Lift alcove ]        [ Stairwell (NO ENTRY) ]                  (safe)             (45 m east)
 SOUTH
```

**Step 1: Start.** ✅ The trainee starts inside **Classroom 1**, looking at the door. The panel shows **"FIRE ALARM - EVACUATE NOW: A fire has been reported. Follow the instructor to Exit B. Do not use the lift."** The instructor stands nearby. Their speech bubble says "Emergency detected. Stay calm and follow my instructions." 🆕 This line is also *spoken*, and the alarm sounds.

**Step 2: Leave the classroom.** ✅ The instructor walks to the door and says "Follow me towards Exit B." In the corridor they stop, **point east**, and explain: "Exit A is blocked by fire. Turn left and follow me towards Exit B."

**Step 2b: The fire is spreading.** 🔥 The fire started at Exit A and spreads east along the west end of the corridor while the trainee evacuates:

| Time after start | Fire patch | What you see |
|---|---|---|
| 0 s | **Exit A doorway** (origin) | Largest fire: tall flames, embers, grey smoke, strong orange light, loud crackle |
| 3 s | **Stored cardboard boxes** on a pallet (south wall, about 3 m from Exit A) | Flames climbing the side of the box pile facing you, up to about 2.8 m; darker smoke, its own light and quieter crackle |
| 6 s | **Notice board** on the north wall | The whole board burning from its bottom edge to above its top; black smoke |
| 9 s | **Ceiling** (about 5.5 m from Exit A) | Flames licking along a charred gap in the ceiling, overhead (decorative: too high to touch) |
| 11 s | **Fallen ceiling tiles** under the gap | Smouldering flames on the floor, a tile hanging down, and an orange glow |
| 12 s | **Lift shaft** | Smoke leaking out of the lift (smoke only, no flames, so not dangerous to touch) |

> Why these positions and times? A Play-mode recording showed that a trainee only looks back west for a few seconds, about 15–20 s into an attempt, while the instruction panel covers the lower half of the view. So every fire is fully grown by about 17 s, and each one is either on its own side of the corridor and burning above the panel (boxes on the left, notice board on the right) or overhead (ceiling). This avoids small floor-level fires lined up behind the Exit A fire.

Each patch starts small and grows over 7–10 s. A smoke layer under the ceiling and soot stains cover the whole west end. **Everything stays west of the Classroom 1 door**, so the safe route east to Exit B is never affected.

**Step 3: Blocked exit or hazard (if the trainee goes the wrong way).** ✅ If the trainee walks west towards **Exit A**, they enter the *blocked-exit zone*. 🔥 It now covers the whole burning west end of the corridor, from Exit A to just past the lift:
- the panel turns **red**: "EXIT A BLOCKED - FIRE: Fire is spreading from Exit A. Smoke is toxic. Never walk into it. Turn back…"
- the instructor **stops, turns to face them, waves them back** and says "Exit A is blocked. Do not approach the fire."
- this is **recorded** as an "unsafe area" for the report.

The same thing happens at the **lift** ("The elevator is unavailable. Do not use it during this emergency.").

**Step 3b: Going into the fire (fatal mistake).** 🧪 If the trainee ignores the warning and walks into **any burning patch** (within its danger radius: Exit A 1.4 m, boxes 1.0 m, notice board 0.8 m, ceiling debris 0.75 m), the attempt **fails**. A patch that hasn't caught fire yet, and the lift smoke, never cause a failure.
- a short **warm flash** fills the view, then a light orange tint stays (a simple colour effect, no injury pictures),
- a short **fire "whoosh" sound** plays, the alarm fades out, and the instructor stops talking and walking,
- the panel turns red: **"FIRE CONTACT — EVACUATION FAILED: You moved into the fire at Exit A. Never approach fire or smoke. … Attempt 1: FAILED"**,
- the result is **recorded** ("Attempt 1: FAILED - fire contact at Exit A (0:42)").

After about 4 seconds the panel says **"SIMULATION RESTARTING..."** and the view fades to dark. The trainee is moved back to the classroom start. The instructor, route guidance, panel and alarm reset, the view fades back in, and **attempt 2** begins with the normal briefing. It can only happen once per attempt.

> Why can you get so close? The Exit A *warning* zone covers the whole burning end of the corridor, and every danger zone lies inside it. So the trainee is always warned (red panel and instructor) before reaching any fire, and a danger zone can never reach through a wall. When several fires are close together, only the first contact counts: the attempt fails once.

**Step 4: Restricted areas.** ✅ Classroom 2, the Lab and the Stairwell have a **barrier belt and a "NO ENTRY" sign**, plus an **invisible solid wall**, so the trainee physically cannot walk in. If they try, the panel turns red, the instructor warns them, and it is recorded as a **restricted entry** with a timestamp.

**Step 5: Follow the safe route.** ✅ The instructor walks about 2 m **ahead**. They look back now and then, and **stop and beckon** ("Stay with me. This way please.") if the trainee falls behind. Painted **chevron arrows** on the floor and green **EXIT B signs** show the way. The panel's footer shows the distance, for example "Exit B: 14 m". Passing the stairs and Classroom 2, the instructor reminds them that the stairs are not an exit and to stay in the corridor.

**Step 6: Exit B.** ✅ At Exit B the instructor says "Exit B is clear. Go through the doors." When the trainee steps into the Exit B zone, the program records **"Exit used: EXIT B"**. Outside, the panel says **KEEP MOVING**.

**Step 7: Assembly point and result.** ✅ When the trainee reaches the assembly point, the training **ends**. The instructor says "You have reached the assembly point. Please remain here." The panel shows the **TRAINING COMPLETE** report:

```
Assembly point reached: YES
Exit used: EXIT B
Time taken: 1:42
Unsafe areas: none            (or e.g. "EXIT A BLOCKED, LIFT")
Restricted entries: none      (or e.g. "2 (CLASSROOM 2, LAB)")
```

The same report is also written to Unity's Console. 🆕 The alarm fades out.

> Note: the program does **not** give a score or pass/fail grade. It shows the facts (exit, time, mistakes) for the trainer to judge.

---

## 7. Important Features (and their status)

| Feature | Status | Explanation |
|---------|--------|-------------|
| **Evacuation zones** | ✅ | Invisible boxes. The manager checks whether the **head position** is inside one, using simple maths (`Contains()`) rather than physics triggers, so it works with every kind of movement. |
| **Restricted areas** | ✅ | 3 areas, each with: a warning zone at the door, a zone for the room itself, an invisible **solid** blocker, a visible belt barrier and a NO ENTRY sign. |
| **Safe route + route check** | ✅ | 9 waypoints from the classroom to the assembly point. `ValidateRoute()` checks every 25 cm of the route to prove it never passes through a dangerous or restricted zone. It runs during generation and again at the start of Play. |
| **Route guidance** | ✅ | Floor chevrons every 2 m, wall wayfinding plaques, exit signs, and the distance in the panel. (The older floating 3D arrow was replaced by the distance text.) |
| **Instructor** | ✅ / 🟡 | Behaviour is complete: lead, wait, talk, warn, point, look back. The **body is a placeholder** made of simple shapes with code-driven animation, not a real human character. 🆕 The body shape was improved with bevelled and tapered pieces. |
| **Instructor voice** | 🆕 | 13 spoken lines. **The clips are placeholders** made with Windows text-to-speech (robotic, not a real voice actor). One line plays at a time, warnings interrupt, and the same line is never repeated within 6 s. |
| **Subtitles** | 🆕 | Whatever the instructor says appears in the speech bubble **and** in a subtitle strip on the panel. Voice clips are matched to the *exact same text*, so voice and subtitle can never disagree. |
| **Fire and smoke** | ✅ / 🆕 | Particle flames, embers and smoke, burning debris, soot stains, a flickering orange light. 🆕 Made more realistic: lighter and less dense smoke that no longer hides the signs, upright flames, debris that glows only in its cracks. |
| **Fire and alarm sounds** | 🆕 | Fire crackle and pops (3D, at the fire), and an international "temporal-3" alarm pattern from three ceiling sounders. The sounds are **synthesised by our own script**, not recorded. |
| **Instruction panel** | ✅ / 🆕 | Follows your view, is pulled in front of walls so it never hides inside one, and changes colour (blue = info, red = danger, green = safe). 🆕 Bigger text, solid background, subtitle strip, and it hides itself 10 s after a message so it doesn't block your view (the final report stays). |
| **Head collision guard** | ✅ | In the simulator you can move the "headset" straight through walls. This script pushes the player back so walls and barriers really stop you. |
| **Completion report** | ✅ | Exit used, time, unsafe areas, restricted entries. After a restart the title says which attempt it was (e.g. "TRAINING COMPLETE - ATTEMPT 2"), and the Console log lists every attempt. |
| **Fire contact = failure + restart** | 🧪 | A configurable danger cylinder around the fire. It checks both the head and the body (CharacterController), only while the fire is burning, and only once per attempt. A burn flash, sound and failure panel are followed by an **in-scene restart**, with no scene reload, so the XR rig and simulator keep working. |
| **Graphics** | ✅ / 🆕 | Procedural textures with "normal maps" (fake surface bumps). 🆕 More realistic metal/paint values, bevelled edges, ceiling sprinklers and smoke detectors, better furniture, ceiling-mounted lights, more natural colour (ACES tonemapping). **The building is still a placeholder**, not our real college. |
| **Real headset support** | 📋 | OpenXR is configured, but it was **never tested on a real headset**. |

---

## 8. How the Main Scripts Work Together

Think of it like a **school drill team**:

- `EvacuationZone`s are the **chalk marks on the floor** ("danger here", "exit here").
- `EvacuationManager` is the **referee**. Every frame it asks: "Which marked area is the trainee's head in?"
- `InstructorGuide` is the **fire warden** who walks with you and talks.
- `InstructionPanel` is the **notice board** in front of your eyes.
- `RouteGuide` is the **map** that says how far it is.

### Who talks to whom

```
                 every frame: "where is the head?"
 Trainee head ─────────────────────────────► EvacuationManager ──checks──► EvacuationZone[] (boxes)
                                                │   │   │
                         panel.Show(...) ◄──────┘   │   └──► routeGuide.Hide() at the end
                                                    │
                     instructor.Alert/Say(...) ◄────┘        Completed event ──► AlarmAudio (fade out)
                                │
 InstructorGuide ── Say ──► speech bubble + panel.ShowSubtitle() + InstructorVoice.Play()
       │  walks along the route waypoints, fires "cues" at certain points
       └── cue ──► panel.Show(title, detail)

 RouteGuide ──► writes "Exit B: 14 m" into the panel footer
 InstructorPlaceholderAnimator ──reads──► InstructorGuide (speed, pointing, beckoning) ──► moves arms/legs
 HeadCollisionGuard ──► pushes the XR rig back if the head would go through a wall
 FireAudio ──watches──► the flame particle system (fades out if the fire stops)

 Fire contact (🧪):
 FireDangerZone ──head/body inside & fire burning──► TrainingRestart.TriggerFailure()
      └► EvacuationManager.Fail()  → instructor.Halt(), route hidden, red panel, result recorded, "Failed" event → AlarmAudio fades
      └► burn sound + ScreenTint flash → wait → "SIMULATION RESTARTING..." → fade to dark
      └► move XR rig to start → EvacuationManager.BeginNewAttempt()
            → route, instructor, panel reset, "AttemptStarted" event → AlarmAudio restarts → fade in
```

### In simple steps

1. **`EvacuationManager.Update()`** runs every frame. It finds the zone containing the head.
2. If the zone **changed**:
   - **Blocked exit / lift** → red panel + `instructor.Alert(...)` + record "unsafe area".
   - **Restricted** → red panel + warning + record "restricted entry" with time.
   - **Safe exit** → green panel + remember "Exit used".
   - **Assembly point** → `Complete()`: build the report, show it (pinned), say the final line, raise the `Completed` event.
3. **`InstructorGuide`** has a list of **cues**: "when I reach waypoint N, say this line and show this panel message". It waits for the trainee before important cues.
4. **`Say()`** (normal) waits for the current spoken line to finish. **`Alert()`** (urgent) interrupts it. This avoids overlapping voices.
5. **`InstructorVoice`** keeps a table *text → audio clip*. It only plays a clip whose text matches exactly. If no clip exists, the line is shown as a subtitle only, and a warning is logged.
6. 🧪 **Fire contact:** `FireDangerZone` notices the trainee inside its cylinder and calls `TrainingRestart`. This calls `EvacuationManager.Fail()`, which only works once per attempt. After the failure sequence, `BeginNewAttempt()` clears the attempt's data (exit used, mistakes, timer) and tells every system to reset. The zone remembers which attempt number it fired on, so it is automatically ready again for the next attempt.

**Why restart inside the scene instead of reloading it?** Reloading would destroy and rebuild the XR rig and the XR Device Simulator, which can break input or create duplicates. Moving the player back and resetting each system is safer and faster.

**Important design choice:** the training scripts never refer to specific walls or furniture. They only know about zones, waypoints and the trainee's head. So when we replace the placeholder building with our real college model, we only need to place new zone boxes and waypoints.

---

## 9. How We Tested It

Being honest about the evidence:

| Test | Result | Evidence |
|------|--------|----------|
| Scene generation | ✅ Ran successfully before the upgrade | The generated scene file exists (saved 14 Oct 2026) and contains the full building and training system |
| Built-in route check | ✅ Built into the generator and Play mode | `ValidateRoute()` logs an error if the route crosses a danger zone. Its result appears in the generation report. |
| Play-mode testing with the **XR Device Simulator** | ⚠️ This is the intended way of testing (WASD/mouse to move the simulated headset). Several features exist because of problems found this way: `HeadCollisionGuard` (walking through walls in the simulator) and the panel's "pull in front of walls" logic. | **No saved test results or recordings** exist in the project, so we can't prove *which* scenarios passed. |
| 🆕 Compilation of the new code | ✅ Passed | Compiled with Unity's own C# compiler (offline) and by the Unity Editor itself, with no errors |
| 🆕 Voice clips say the right words | ✅ Passed, 13/13 | Each clip was checked with offline Windows speech recognition |
| 🆕 Audio levels | ✅ | No clipping (peaks ≤ −3 dB), similar loudness across voice lines, seamless loops |
| 🆕 Code review | ✅ | Independent reviews checked logic, rendering and readability; the problems found were fixed |
| 🆕 Regeneration after the upgrade | ✅ Passed (28 Oct, 17:05) | Unity log: "Audio: 13/13 voice clips, fire + alarm SFX found", "Route check: OK", 1 XR Origin, 1 camera, 1 simulator. One sign warning: the "FIRE EXTINGUISHER" sign text is slightly too big for its plate. |
| 🆕 One full Play-mode run with the simulator | ✅ Completed | Unity log: "Training complete. Exit used: EXIT B, Time taken: 1:10, Unsafe areas: EXIT A BLOCKED, LIFT, Restricted entries: none", with no script errors. (How the sound and visuals looked was not recorded.) |
| 🧪 Fire contact: automated EditMode tests | ⚠️ **Written, not yet run** | `Assets/SafeRoute/Scripts/Editor/Tests/FireContactTests.cs`, 8 tests: zone shape and radius, fires once and re-arms, ignored while the fire is out, detects the body, `Fail()` once per attempt, rig returned to the start (with a head offset), instructor reset, route and panel reset. Run them in **Window → General → Test Runner → EditMode**. |
| 🧪 Fire contact in Play mode | ✅ One run (28 Oct, 18:31 regeneration) | Unity log: generation report "Fire contact fail + restart: yes, radius 1.4 m". In play: "Evacuation FAILED (attempt 1): fire contact at Exit A at 0:38", "Attempt 2 started", "Training complete … Attempt 1: FAILED - fire contact at Exit A (0:38), Attempt 2: COMPLETED via EXIT B (1:12)" |
| 🔥 Fire spread: automated EditMode tests | ⚠️ **Written, not yet run** | `Editor/Tests/FireSpreadTests.cs`: a delayed patch stays out until its time, starts small, grows to full size, and resets for a new attempt; overlapping danger zones fail the attempt only once; the route-clearance check passes for the real fire positions and fails for a fire placed on the route |
| 🔥 Fire spread in Play mode | ❌ **Not tested yet** | Needs the scene to be regenerated first |
| Real VR headset | ❌ Not tested | We have no headset |

### Manual Play-mode checklist (to do after regenerating the scene)

1. **SafeRoute → Generate Evacuation Scene**, confirm overwrite. Check the report: route check **OK**, **13/13 voice clips**, fire + alarm SFX found.
2. Press Play. You should hear the alarm, see the red briefing, and hear the instructor say "Emergency detected…"
3. Follow the instructor. Each spoken line should match the subtitle exactly. Voices should never overlap.
4. Walk towards Exit A. You should get a red warning and the "Exit A is blocked…" voice, and the fire should get louder as you approach.
5. Try the lift, Classroom 2, the Lab and the stairs. You should be stopped by the barrier and hear the correct warning.
6. Check that text is readable in the **Game view** (not just the Scene view), and that the panel hides after about 10 s of silence.
7. Reach the assembly point. You should see the report, hear the final line, and the alarm should fade out.

**Fire contact checklist** (🧪 after regenerating; the report should say "Fire contact fail + restart: yes, radius 1.4 m"):
1. Follow the safe route without approaching the fire. Nothing fails, and the training completes as before.
2. Press Play again, walk west past the Exit A warning, and keep going into the fire.
3. The warm flash, the fire sound and "FIRE CONTACT — EVACUATION FAILED … Attempt 1: FAILED" appear **once**. The alarm stops, the instructor goes silent, and the Console shows one "Evacuation FAILED" warning.
4. After about 4 s: "SIMULATION RESTARTING...", a fade to dark, then you are back in Classroom 1 facing the door. The instructor is at the start, the alarm is on, and the briefing is shown.
5. Complete the fresh attempt via Exit B to the assembly point. The report title says "TRAINING COMPLETE - ATTEMPT 2", and the Console lists both attempts.
6. Optional: fail while the instructor is talking. The voice should cut out cleanly, and the next attempt should start with "Emergency detected…"

**Fire spread checklist** (🔥 after regenerating; the report lists 6 fire patches and "Route clear of fire: OK"):
1. Press Play and watch west from the corridor. At first only Exit A burns. Then the boxes (about 3 s), the notice board (about 6 s), the ceiling (about 9 s), the ceiling debris on the floor (about 11 s) and the lift smoke (about 12 s) start small and grow.
2. Check the look: different flame heights, darker smoke from the boxes and the board, warm flickering light on nearby walls, and the signs and panel text still readable.
3. Listen: the crackle gets louder near Exit A and the boxes, and is silent before the boxes ignite.
4. Walk into each burning patch on separate attempts (boxes, board, ceiling debris). Each should fail once with its own reason in the Console, for example "fire contact at the burning storage boxes".
5. Stand in the lift smoke: nothing fails. Walk into a patch before it has ignited: nothing fails.
6. After a restart, the spread starts again from Exit A only.
7. Follow the safe route to Exit B and the assembly point. It is completely unaffected by the fire.

---

## 10. Current Limitations

- **Placeholder building:** a procedurally built generic college block, not our real college.
- **Real-world assets are partial:** only Classroom 1 has scanned desks and chairs (Classroom 2 and the lab are still procedural), and three textures have no published real-world size (brushed steel) or were adjusted to fit the scene (ceiling tiles scaled 6% to the 0.6 m grid, concrete turned grey). The burnt-wood texture is CC-BY and must stay credited.
- **Placeholder instructor:** a stylised mannequin with code-driven animation. It is not a realistic human, and its face has no eyes or lip movement.
- **Robotic voice:** the voice clips are Windows text-to-speech placeholders. They should be re-recorded by a real person (same wording, same file names).
- **Synthetic sounds:** fire and alarm sounds are generated by code. Real recordings would be more convincing.
- **No real headset testing:** only the XR Device Simulator.
- **Few automated tests:** only the fire-contact EditMode tests exist, and the timed failure sequence (fades, sounds) can only be checked by hand in Play mode. No test reports are saved.
- **Single scenario:** the fire is always at Exit A, and the route is always to Exit B. There is no randomisation or difficulty levels.
- **No scoring or saved history:** the result is shown on screen and in the Console, but not saved to a file or database.
- **Lighting is fully realtime:** there is no "baked" lighting (pre-calculated light bounce), which would look more realistic and run faster.
- **Smoke doesn't affect the trainee:** no breathing or visibility penalty, and no advice to "stay low".
- **Shared URP settings not tuned:** shadow and SSAO settings in the URP quality assets are still the template's (heavier than needed for VR). We deliberately did not change shared project settings.

---

## 11. Future Improvements (📋 planned, not built)

1. **Our real college building:** import a 3D model (e.g. made in Blender or SketchUp, or from architectural drawings). Then place the existing zones and waypoints on it. The training scripts are already independent of the building.
2. **Realism:** realistic materials (scanned textures), real furniture models, more architectural detail.
3. **Lighting:** baked global illumination and light probes, emergency lighting mode (lights dim, exit signs glow), tuned VR shadow settings.
4. **Animation:** a rigged human instructor with real walking, talking and pointing animations (the code already supports an `Animator` with a "Speed" value), plus lip sync.
5. **Fire and smoke:** flipbook fire from real footage, smoke that spreads over time, heat haze, sprinklers activating.
6. **Audio:** professionally recorded voice lines (maybe in English and Hindi), recorded fire and alarm sounds.
7. **VR performance:** test on a headset (e.g. Meta Quest), reduce shadows and SSAO costs, keep a steady 72–90 FPS.
8. **More scenarios:** random fire location, different blocked exits, night mode, crowds of other students (NPCs).
9. **Assessment:** scores, saved reports (CSV or online), a trainer dashboard.
10. **Interaction:** pressing the fire-alarm call point, feeling closed doors for heat, using the extinguisher (training on *when not to*).

---

## 12. Simple Project Summary (1–2 minutes to speak)

> "Our project is **SafeRoute VR**, a virtual reality fire-evacuation training simulator built in **Unity** with **C#**, the **XR Interaction Toolkit**, and the **Universal Render Pipeline**.
>
> The trainee starts in a classroom when a fire alarm goes off. A virtual fire-warden instructor guides them out of the building. The nearest exit, Exit A, is blocked by fire and smoke, so the trainee must use Exit B instead. They must not use the lift, and they must not go into other classrooms, the lab or the stairs.
>
> We marked every important area with invisible **zones**. A manager script checks every frame which zone the trainee's head is in. If they go somewhere dangerous, a red warning appears, the instructor stops and warns them, and the mistake is recorded. Restricted rooms also have physical barriers. Floor arrows, exit signs and a distance counter show the safe route. The instructor walks ahead, waits if the trainee falls behind, and explains each decision, with spoken voice and subtitles.
>
> At the assembly point the program shows a report: which exit was used, the time taken, and any unsafe or restricted areas the trainee went near. If the trainee walks into the fire, the attempt fails: a warm flash and a 'Fire contact — evacuation failed' message, and then the simulation restarts from the classroom so they can try again.
>
> The whole building is **generated by our own editor script**, so we can change it easily. We kept the training logic separate from the building, so next we can import our **real college building** as a 3D model. We tested it using the **XR Device Simulator**, because we don't have a headset yet. Future work includes the real building, a realistic animated instructor, recorded voice, and testing on a real headset."

---

## 13. Possible Viva Questions (with short answers)

1. **What is SafeRoute VR?**
   A VR simulator that trains people to evacuate a college building safely during a fire, using the correct exit and avoiding hazards.

2. **Why use VR instead of a video or a real drill?**
   VR is interactive (you make the decisions), safe (fire and smoke without danger), repeatable, and it records mistakes. A video is passive, and a real drill can't include real fire.

3. **Which software and languages did you use?**
   Unity 2022.3 with C#, URP for rendering, XR Interaction Toolkit 2.6.4 for VR, and OpenXR for headset support.

4. **What is the XR Device Simulator?**
   A tool from the XR Interaction Toolkit that lets you control a pretend headset and controllers with the keyboard and mouse. We use it because we don't have a headset.

5. **What is an XR Origin (XR Rig)?**
   The player object in VR. It holds the camera (your eyes) and the controllers (your hands), and it moves when you move.

6. **How does the program know where the trainee is?**
   It reads the position of the VR camera (the head) every frame and checks which zone box contains that point.

7. **What is an EvacuationZone?**
   An invisible box with a type (blocked exit, unavailable route, safe exit, assembly point or restricted), a title, an explanation and an instructor line.

8. **Why check the head position instead of using physics triggers?**
   Head checks work for every kind of movement, including the simulator moving only the head. A simple maths check (is the point inside the box?) is also reliable and cheap.

9. **How do you stop people entering restricted rooms?**
   Three layers: a visible barrier belt and NO ENTRY sign, an invisible solid collider in the doorway, and the `HeadCollisionGuard` script, which stops the head moving through walls.

10. **How do you make sure the safe route is really safe?**
    `ValidateRoute()` samples points every 25 cm along the route and checks that none is inside a dangerous or restricted zone. If one is, it logs an error.

11. **How does the instructor work?**
    It follows the route waypoints, stays about 2 m ahead, waits if you fall behind, and at certain waypoints ("cues") stops to speak, point and show instructions. When you go somewhere dangerous, it turns to you and warns you.

12. **How do the voice and subtitles stay in sync?**
    Each voice clip is stored with its exact sentence. The instructor shows the sentence and plays only the clip with that exact text, so they can't disagree. Lines queue instead of overlapping, and the same line isn't repeated within 6 seconds.

13. **Why did you generate the scene with code?**
    It's consistent and easy to change: edit a number and regenerate. It also keeps the training logic separate from the building, so the real college model can replace the placeholder later.

14. **What does the final report show?**
    Whether the assembly point was reached, which exit was used, the time taken, which unsafe areas were approached, and how many restricted entries happened (and where).

15. **What are the main limitations and future plans?**
    The building and instructor are placeholders, the voice is text-to-speech, and it hasn't been tested on a real headset. Next: import our real college building, add a realistic animated instructor and recorded voices, add more scenarios and scoring, and optimise for a headset like the Quest.

16. **(Extra) How does the fire spread?**
    Each burning area is a "fire patch" described by a few numbers: position, size, flame height, intensity, smoke density, light, sound, danger radius and ignition time. One shared builder creates every patch the same way. At runtime, `FireSpread` ignites the patches one after another from Exit A eastward and grows each one from small to full size. Each patch's danger zone only becomes active once it is burning, and everything resets when a new attempt starts.

17. **(Extra) What happens if the trainee walks into the fire, and how does the restart work?**
    A danger cylinder around the fire detects it, only while the fire is burning and only once per attempt. The attempt is marked FAILED, a warm flash and a fire sound play, and the alarm and instructor stop. After a few seconds the screen fades to dark. The player is moved back to the classroom, every system resets itself (instructor, route, panel, alarm), and attempt 2 starts. We don't reload the scene, because that could break the XR rig and the simulator.

---

*Document written from the project's actual source code, scenes, packages and Git history as of 28 October 2026.*
