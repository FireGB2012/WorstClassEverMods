# Meshy batch, 2026-11-14 (toy style, 21 credits a model)

Done. Gabriel approved the other 36 models for about 756 credits; 34 were made (714 credits).
`hat_baldcap` and `face_nose` were left as the Blender-script models: a plain dome and a plain
ball are already right.

- Models: `meshy\toy\<name>.glb`, 35 files, named exactly like the mod's models in
  `package\models` (`hat_cowboy.glb` is the first toy-style test, copied in).
- Pictures: `meshy\pictures\toy\<name>.png`, and three overview sheets
  `meshy\pictures\toy_models_1.png` to `_3.png`.
- Recipe: "Game-style Meshy recipe" in PROGRESS.md. Meshy runs at most 10 tasks at once on this
  plan, and a picture task must be finished before its 3D task can start.
- Credits: 1,362 at the start of the day, 582 left (all permanent; the monthly ones are used up
  until 2026-11-25).
- Meshy's download links expire on 2026-11-17. The files here are the copies to keep.

What I'd look at again if Gabriel is not happy with some: the note (reads as a thick grey
booklet), the gym teacher belly (came out as a tiny t-shirt with sleeves), the laser pointer
(chunky like a marker), the glue puddle (beige, not white), and the shotgun and sniper (more
realistic than the rest). White things look grey in the renders; check them under the game's lights.

NOT done: none of these is converted to `.cpm` or installed. `tools\convert_meshy.py` does the
conversion; each model needs its size, origin and facing set (hats sit on a head point, guns
point forward from the hand, flat things lie on the floor).

## Teacher characters, 2026-11-14 (105 credits, 477 left)

Recipe per teacher: style picture (`meshy_image_to_image`, nano-banana-2, the game's teacher
screenshot `.../apps/5175310/142910416f.../ss_142910416f....1920x1080.jpg` as reference, A-pose,
3:4) -> `meshy_image_to_3d` (smart-topology meshy-t2.5, 8000 faces, meshy-n1, pose_mode a-pose)
-> `meshy_rig` -> `meshy_animate` with action_ids [0, 613, 14] (Idle, Casual Walk in place,
Run 2) -> `meshy_download_model` with task_type animation gives the URL of one
`merged_animations.glb` holding the body and all three moves; fetch it with Invoke-WebRequest.

| Teacher | 3D task | Rig task | Moves task |
|---------|---------|----------|------------|
| gym | 01a1dc13-1081-74b1-b0e8-890575f750b4 | 01a1dc13-f5e1-73df-ad9f-422ec38c9b50 | 01a1dc15-0247-741c-b247-17a3914a5f02 |
| lunch lady | 01a1dc13-1531-7269-aa29-70c6f16b06a5 | 01a1dc14-0290-76de-a4d4-97f87340c2e6 | 01a1dc15-03d2-706c-9da8-6d597aa55cc2 |
| janitor | 01a1dc13-18ff-74f3-a0e5-1443789b9f5a | 01a1dc14-212d-70b2-ac72-cf3544e08530 | 01a1dc15-0549-76be-88a8-d272950c6139 |

Saved: `meshy\teachers\teacher_<name>.glb` (still) and `teacher_<name>_moves.glb` (with moves).

## Pool props for the swim class, 2026-11-14 (126 credits approved, 477 before)

Same game-style recipe (style picture, then smart-topology meshy-t2.5 with 5000 faces and the
meshy-n1 texture). Saved as `meshy\toy\pool_<name>.glb`.

| Prop | Picture task | 3D task |
|------|--------------|---------|
| pool_board (diving board) | 01a1dc3c-65f9-749d-b9f2-36d984170e69 | 01a1dc3c-a9f2-72fd-88bf-d459adb9eb74 |
| pool_chair (lifeguard chair) | 01a1dc3c-6802-702a-b20d-0dc7c5be4189 | 01a1dc3c-ac87-72ea-bfb3-fea8ab7c9c99 |
| pool_ladder | 01a1dc3c-69fc-7731-be06-54a0a150ebd4 | 01a1dc3c-af51-77bc-9afd-e4bf802e81f2 |
| pool_ring (life ring) | 01a1dc3c-6bfc-726c-a6a7-fc3faf43cdcb | 01a1dc3c-b248-768b-a69b-298115a61a30 |
| pool_bench | 01a1dc3c-6e18-7041-b145-e7a6e2465304 | 01a1dc3c-b551-7343-bb28-8f8da6d3d82d |
| pool_duck (duck float) | 01a1dc3c-7051-7628-8a80-9c277cd734a2 | 01a1dc3e-b446-703e-9610-cf4c1c587e6f |

All six were made (126 credits, 351 left), converted by `tools/convert_all_meshy.py` into
`package/models/pool_*.cpm` and are in version 0.9.0. Pictures: `meshy/pictures/pool/` and the
sheet `meshy/pictures/pool_models.png`. Their textures are bright and flat; the renders only
look dull because of the render's light. Meshy's download links expire on 2026-11-17.

## Swim moves and lounge props, 2026-11-15 (60 credits approved: 18 + 42; 351 before)

Swim moves: `meshy_animate` on each teacher's rig task with action_ids [568, 569] (Swim Idle,
Swim Forward), 6 credits each.

| Teacher | Swim moves task |
|---------|-----------------|
| gym | 01a1de46-6a0b-728c-8eb3-507d3a00c081 |
| lunch lady | 01a1de46-6bcd-7346-8914-fe0c975bb1e8 |
| janitor | 01a1de46-6d7a-7120-9799-e836931f50cd |

Lounge props (he chose "sofa and table only"), game-style recipe:

| Prop | Picture task | 3D task |
|------|--------------|---------|
| lounge_sofa | 01a1de46-6f20-7201-a8ad-bc9936484476 | 01a1de46-b7b3-7373-8fe1-17ff5b5fe3ac |
| lounge_table | 01a1de46-7188-7313-a44c-ab2c75d5c2eb | 01a1de46-baab-7024-8720-8e352637b832 |

All made: `meshy/teachers/teacher_<name>_swim.glb` (swim moves, added to the `.cpc` files with
`convert_character.py --append`; the janitor's also gave `package/models/moves_swim.cpr`) and
`meshy/toy/lounge_sofa.glb`, `lounge_table.glb` (pictures: `meshy/pictures/lounge_models.png`).
60 credits used, 291 left by my count.

## Dance class props, 2026-11-15 (126 credits approved, 291 before)

Gabriel chose "6 props, 126 credits". Game-style recipe as for the pool props; the two style
screenshots (from the Steam store data) are
`https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/5175310/5e243e634eb16b2ef336a47f76c107de1e2f4d31/ss_5e243e634eb16b2ef336a47f76c107de1e2f4d31.1920x1080.jpg`
and `.../12f3c4378f48f9f45c56c24d019370026ec3c1c7/ss_12f3c4378f48f9f45c56c24d019370026ec3c1c7.1920x1080.jpg`.
Style pictures: `meshy/style/dance_<name>.png`.

| Prop | Picture task | 3D task |
|------|--------------|---------|
| dance_discoball | 01a1e048-cfd3-726f-9fae-2f41ff5271cb | 01a1e04a-29a3-7212-8825-43c087f29d82 |
| dance_speaker | 01a1e048-d33a-7325-806d-e6497cc12ea2 | 01a1e04a-2e41-7058-b496-16ccfc2abb0e |
| dance_djbooth | 01a1e048-d631-7179-a569-16cc962a9e51 | 01a1e04a-3170-77bd-b76c-e5e5bd2aa8e8 |
| dance_stage | 01a1e048-d82c-7578-b609-1d029fa88baa | 01a1e04a-347b-7569-a8cc-2e173a04764b |
| dance_boombox | 01a1e048-da54-7461-90b7-fc2c89ac9f08 | 01a1e04a-37b7-7315-b6df-4cf47fb57512 |
| dance_snacks | 01a1e048-dd1c-746f-93b7-4aa65ed8f17d | 01a1e04a-3b5a-7663-9378-d32575f5c878 |

## Hairstyles, 2026-11-15 (42 credits approved, 165 before)

Gabriel chose "Afro + dreads, 42 credits". Game-style recipe, made very light grey so the
wardrobe's hair colour can tint them. Style pictures: `meshy/style/hair_<name>.png`.

| Hair | Picture task | 3D task |
|------|--------------|---------|
| hair_afro | 01a1e087-f009-758d-b173-ed4fe54e5461 | 01a1e088-9da8-7679-ba23-ae4818ad5b8c |
| hair_dreads | 01a1e087-f24c-7167-b0c3-384b54122b26 | 01a1e088-a097-76b7-95be-3934b439efdd |

## Phone, elevator panel, mohawk and spiky hair, 2026-11-15 (84 credits approved, 123 before)

Gabriel chose "Phone, panel, 3 hairstyles" and picked two styles (mohawk, spiky anime hair);
the third hairstyle (21 credits) waits until he names one. Same game-style recipe; the hair is
very light grey so the wardrobe's hair colour tints it. Style pictures: meshy/style/<name>.png.

| Model | Picture task | 3D task |
|-------|--------------|---------|
| hair_mohawk | 01a1e101-8b38-7644-b076-b03ae8f36a4a | 01a1e102-677d-73c7-8f64-cea35296a5df |
| hair_spiky | 01a1e101-8d1d-72f6-b682-84baea69ff98 | 01a1e102-69fd-73fe-bc68-ce60bf750959 |
| phone | 01a1e101-8f35-7479-aea5-c89bc35249ae | 01a1e102-6cc5-70d1-9d17-e0c0c80a4bbc |
| elevator_panel | 01a1e101-90eb-72d9-9d25-d94ded736cd5 | 01a1e102-6f7b-72d5-8fb4-2d3a53d56d50 |
