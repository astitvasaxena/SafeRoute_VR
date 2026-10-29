# SafeRoute VR - third-party assets

All files below were downloaded by `Tools/Assets/fetch_third_party_assets.py` and converted for Unity URP
(OpenGL normal maps; roughness converted to a MetallicSmoothness map; signs and flipbooks resized).
Licences were verified on 2026-10-30 against each source's API or licence page. The scene generator falls
back to its procedural materials and props when any of these files is missing.

| Asset | Source | Licence | Attribution | Used for | Files |
|---|---|---|---|---|---|
| painted_plaster_wall | https://polyhaven.com/a/painted_plaster_wall | CC0 1.0 | Not required | Corridor and classroom painted walls | PolyHaven/painted_plaster_wall/ (2k, 2.0 m tile) |
| oak_veneer_01 | https://polyhaven.com/a/oak_veneer_01 | CC0 1.0 | Not required | Door leaves, teacher's desk top and outdoor benches | PolyHaven/oak_veneer_01/ (1k, 1.83 m tile) |
| smooth_concrete_floor | https://polyhaven.com/a/smooth_concrete_floor | CC0 1.0 | Not required | Stairs, lift alcove, copings and roof (tinted to the scene's grey) | PolyHaven/smooth_concrete_floor/ (1k, 2.0 m tile) |
| concrete_pavers | https://polyhaven.com/a/concrete_pavers | CC0 1.0 | Not required | Outdoor path to the assembly point | PolyHaven/concrete_pavers/ (1k, 1.92 m tile) |
| SchoolDesk_01 (model) | https://polyhaven.com/a/SchoolDesk_01 | CC0 1.0 | Not required | Classroom 1 desks | PolyHaven/SchoolDesk_01/ (FBX, 1k maps) |
| SchoolChair_01 (model) | https://polyhaven.com/a/SchoolChair_01 | CC0 1.0 | Not required | Classroom 1 chairs | PolyHaven/SchoolChair_01/ (FBX, 1k maps) |
| korean_fire_extinguisher_01 (model) | https://polyhaven.com/a/korean_fire_extinguisher_01 | CC0 1.0 | Not required | Corridor fire extinguishers (Korean paper label replaced by an English label) | PolyHaven/korean_fire_extinguisher_01/ (FBX, 1k maps) |
| fire_alarm (model) | https://polyhaven.com/a/fire_alarm | CC0 1.0 | Not required | Manual call points | PolyHaven/fire_alarm/ (FBX, 1k maps) |
| Tiles110 | https://ambientcg.com/view?id=Tiles110 | CC0 1.0 | Not required | Corridor and classroom terrazzo floor | ambientCG/Tiles110/ (2K; real size 1.85 m) |
| OfficeCeiling001 | https://ambientcg.com/view?id=OfficeCeiling001 | CC0 1.0 | Not required | Suspended T-bar ceiling (tiled at 3.6 m so its 6 x 6 tiles fit the 0.6 m ceiling grid) | ambientCG/OfficeCeiling001/ (1K; real size 3.85 m) |
| Metal009 | https://ambientcg.com/view?id=Metal009 | CC0 1.0 | Not required | Brushed steel lift doors and lift metalwork | ambientCG/Metal009/ (1K; real size not published, tiled by eye) |
| ISO_Exit_-_Left.svg | https://commons.wikimedia.org/wiki/File:ISO_Exit_-_Left.svg | CC0 1.0 | Not required (author: Maxxl2 (arrow) and The Navigators (running man)) | Exit sign pointing left | Wikimedia/ISO_Exit_-_Left.png |
| ISO_Exit_-_Right.svg | https://commons.wikimedia.org/wiki/File:ISO_Exit_-_Right.svg | CC0 1.0 | Not required (author: Maxxl2 (arrow) and The Navigators (running man)) | Exit sign pointing right | Wikimedia/ISO_Exit_-_Right.png |
| ISO_Exit_-_Up.svg | https://commons.wikimedia.org/wiki/File:ISO_Exit_-_Up.svg | CC0 1.0 | Not required (author: Maxxl2 (arrow) and The Navigators (running man)) | Exit sign straight ahead / through this door | Wikimedia/ISO_Exit_-_Up.png |
| ISO_7010_F001.svg | https://commons.wikimedia.org/wiki/File:ISO_7010_F001.svg | Public domain | Not required (author: MaxxL) | Fire extinguisher sign | Wikimedia/ISO_7010_F001.png |
| ISO_7010_F005.svg | https://commons.wikimedia.org/wiki/File:ISO_7010_F005.svg | Public domain | Not required (author: MaxxL) | Fire alarm call point sign | Wikimedia/ISO_7010_F005.png |
| ISO_7010_E007.svg | https://commons.wikimedia.org/wiki/File:ISO_7010_E007.svg | Public domain | Not required (author: MaxxL) | Evacuation assembly point sign | Wikimedia/ISO_7010_E007.png |
| Flame02 flipbook | https://unity.com/blog/engine-platform/free-vfx-image-sequences-flipbooks | CC0 1.0 | Not required (Unity Technologies, Unity Labs) | Flame tongues of all fire patches | UnityLabs/Flame02/ |
| WispySmoke01 flipbook | https://unity.com/blog/engine-platform/free-vfx-image-sequences-flipbooks | CC0 1.0 | Not required (Unity Technologies, Unity Labs) | Smoke plumes and the ceiling smoke layer | UnityLabs/WispySmoke01/ |
| BurnedWood2 | https://opengameart.org/content/burned-wood-texture-seamless-texture-with-normalmap-0 | CC-BY 3.0 | **Required:** "Burned Wood Texture" by Keith333, https://opengameart.org/content/burned-wood-texture-seamless-texture-with-normalmap-0, CC-BY 3.0 | Charred wood on burnt debris and boxes | OpenGameArt/BurnedWood2/ |

## Changes made to the original files
- korean_fire_extinguisher_01: the Korean text on the inspection tag, the stencilled band and the product sticker
  was replaced by English artwork (inspection record, FIRE EXTINGUISHER, ABC dry powder / PULL-AIM-SQUEEZE-SWEEP).
- smooth_concrete_floor: colour map desaturated to grey and lifted to a 0.2 mean albedo (cast concrete).
- BurnedWood2 (CC-BY 3.0): resized from 2048 x 1169 to 1024 x 584; otherwise unchanged.
- Flame02: the 16 x 4 sheet of tall frames repacked, frame order unchanged, into an 8 x 8 sheet of square frames.
- ISO signs: *_Mask.png files are the white pictogram strokes of the official artwork as an alpha mask (border
  trimmed, padded to a multiple of 4 px); the artwork itself is not altered or mirrored.
- All roughness maps were converted to Unity MetallicSmoothness maps (R = metallic, A = 1 - roughness).

## Excluded on purpose
- ISO 7010 P020 (do not use lift in case of fire): Wikimedia lists it as public domain, but the author field is 'ISO' on a
  self-published release, so the redistribution rights are unclear. The lift keeps its own text notice.
- Poly Haven cardboard_box_01: about 17k triangles, over the VR budget for a small prop.
- Reviewed and licence-checked but not shipped (no fitting material in the scene): Poly Haven burned_ground_01; ambientCG PaintedMetal004; ambientCG Leaking001; ambientCG Leaking008; ambientCG SurfaceImperfections014; Unity Labs Flame03, WispySmoke03, DiscSmoke01.
- Sketchfab, Kenney and Unity Asset Store items: manual downloads, skipped for now.
