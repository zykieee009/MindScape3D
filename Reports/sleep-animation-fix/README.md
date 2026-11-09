# Bed animation repair

Updated `Blender Files/Dormitory_Sleep_Scenarios.blend` through Blender MCP.
Play frames 1–539 at 30 FPS to review the complete sequence. The player uses
three consecutive NLA strips; Maya loops the matching sleeping pose.

| Clip | Local frames | Behavior |
| --- | --- | --- |
| Walk_To_Bed_And_Sleep | 1–240 | Approach with alternating foot contacts, turn, sit, lift feet, swing onto mattress, recline, close eyes |
| Sleeping_Idle | 1–121 | Four-second breathing loop with closed eyes |
| Wake_Up_And_Get_Out_Of_Bed | 1–180 | Open eyes, sit up, swing feet out, plant soles, lean forward, stand |

The revised FBX and GLB libraries are in `Assets/Characters/Animations/`.
`Sleeping_Animations` contains exactly these three clips, including eyelid
morph animation. `Dormitory_Sleep_Scenarios` contains the complete two-character
scene and its composed timeline. Existing asset `.meta` files were preserved.

The clips use bed-local motion: Blender +Y points toward the pillow, and entry
is from the -X side. Root movement is baked on the root bone; preview placement
is on the armature object. Both preview characters use uniform scale 0.78 to
fit the existing mattresses. The single bed was turned to align its headboard
with the pillow. Added blanket blocks are folded at the foot of each bed.

The eye closure is a reversible `Sleep_Eyes_Closed` shape key. No rig or
constraint installation is needed to play the exported motion. Unity runtime
controllers and episode scripts were not changed. An importer/player should
loop only `Sleeping_Idle` and play the entry/exit clips once.

## Review and checks

- `sleep-sequence-preview.mp4`: complete 18-second preview, sampled at 10 FPS.
- `contact-sheet.png`: fourteen reviewed poses from approach to standing.
- `validation.json`: evaluated bone seam checks, sole contact measurements,
  and exported clip/morph verification.
- Entry → sleep, sleep-loop endpoints, and sleep → wake match exactly in
  Blender and the GLB animation data.
- Sampled planted soles are within 0.00001 m of the floor. Sleeping soles are
  about 0.0023 m above the quilt surface.
- FBX eyelid channels were checked per take: open → closed, closed → closed,
  and closed → open.

Original exports are in `backups/`. The pre-repair Blender snapshot is
`Blender Files/Backups/Dormitory_Sleep_Before_Fix_20261021_180500.blend`.
Original actions are also retained in the Blender file with
`__BeforeContactFix` suffixes; unrelated imported NLA tracks remain muted.

To reproduce in the existing scene, run `scripts/blender/fix_sleep_sequences.py`
inside Blender with `__name__ == '__main__'`, then run
`scripts/blender/export_sleep_sequences.py`. The latter isolates the requested
clips and pairs FBX morph channels with their matching takes.
