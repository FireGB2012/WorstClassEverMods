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
