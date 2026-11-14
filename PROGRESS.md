# Worst Class Ever mods: progress

Ideas list: Google Doc "worst class ever mod ideas" (38 ideas).

## How this works

- The game has official mod support. A mod is a folder in the game's `Mods` folder with a `mod.json` and a dll.
- `ClassPack\` is the mod's code. `package\mod.json` is its description.
- `.\build.ps1` compiles it into `dist\classpack`. `.\build.ps1 -Install` also copies it to
  `C:\Program Files (x86)\Steam\steamapps\common\Worst Class Ever\Mods\classpack`.
- After installing: start the game, open the mod menu, switch Class Pack on, restart the game.
- Settings (keys, timers, on/off per idea) are saved in
  `%USERPROFILE%\AppData\LocalLow\Funcore Games\Worst Class Ever\Mods\Config\classpack\config.json`.
- The game's log is `%USERPROFILE%\AppData\LocalLow\Funcore Games\Worst Class Ever\Player.log`.
  Lines from this mod start with `[Mod:classpack]`.
- Gameplay only switches on in rooms where the host has the mod too, so it can't be used as a cheat.

## First pack

| Idea | State |
|------|-------|
| 11 DoorDash | Rebuilt on 2026-11-14 the way Gabriel wants it (version 0.3.0): O pulls up a phone, the DoorDash app orders a dish, about 10 seconds later it is dropped on the floor of the teacher's office, and eating it pays 15 mischief. Not yet tried in the game. The drop spot is a best guess (see below). The first version, pressing O at a computer desk, is gone. |
| 6 (part) Teacher eats snacks from drawers for stamina | Built and installed. Not yet tried in the game. Teacher stands by a desk and presses G. |
| 38 (part) Power cut in the middle of class | Built and installed. Not yet tried in the game. Once per class, at a random moment, for 25 seconds. The darkening look is a guess until it is seen in the game. |
| 3 Bald male teacher | Moved to the models pack: his hair is part of his body model, so it can't just be hidden |

Version 0.1.0 was installed to the game's Mods folder on 2026-11-14 and switched on in the mod menu.

## Testing helpers (version 0.2.0, installed 2026-11-14, log shows it loads)

| Helper | State |
|--------|-------|
| Solo class | Built and installed. Not yet tried in the game. A lone host can ring the bell. You keep the role picked at the wardrobe; the classmates are the game's bots. There is no bot teacher. |
| Owner settings window (F6) | Built and installed. Not yet seen in the game. Class length, teachers, points to win. It writes the game's own room rules, so it works for players without the mod too. Owner only, between classes. |

The game must be closed before installing an update, because it holds the mod file open.

### The host must not message itself (fixed in 0.3.1)

In 0.3.0 every DoorDash order from the room owner was "cancelled". The log showed no error and no
delivery. Cause: the mod sent the owner's own requests over the network to itself, and in this game
such a message is never delivered. The game's own code never does that; the host calls its handlers
directly and only real clients use the wire. `Net.cs` now does the same (`SendToHost`, `SendTo`,
`SendToModded` each take a direct path when the other end is this same game). The same bug would
have stopped the owner's teacher snack and kept the owner's screen bright during a power cut.
Not yet confirmed in the game.

### Where DoorDash drops the food

The game has no marker for "the teacher's office", so `TeacherOffice.cs` works it out on the host:

1. If the point where teachers are placed before class is outside the classroom, drop it there.
2. Otherwise go 2.2 m through the office door, to the side that is not the classroom.

Each delivery writes a log line starting `[Mod:classpack] DoorDash:` with the spot and which rule
picked it. If the food lands in the wrong place, read that line first. `doorDashOfficeSide`
(1 or -1) and `doorDashOfficeDepth` in config.json move it without a rebuild.

### Things to check in the first play test

- Does "Class Pack" show in the mod menu without an error, and does the log say `[Mods] Loaded classpack`?
- DoorDash: the hint when you sit at a computer, the order message, the food landing in the drawer.
- Do the O and G keys clash with anything the game already uses? Both can be changed in config.json.
- Teacher snack: does the snack vanish for the students too, and does the stamina bar go up?
- Power cut: is the room dark enough, too dark, or not dark at all?

## Second pack (version 0.4.0, installed 2026-11-14, not yet tried in the game)

Gabriel asked for "all the rest of the mods in the doc" and agreed to do them in packs, testing
each pack before the next. He chose nerf guns for both teacher and students.

| Idea | State |
|------|-------|
| 1 Nerf guns | Built. Four guns float around the classroom at class start (pistol 15, shotgun 6, sniper 5, RPG 1). B takes or throws away, X or a wheel click shoots. A student who is hit goes on a pole with no rescue; a teacher who is hit falls over; the RPG hits everyone, the shooter too. Guns are block shapes built in code. |
| 7 Window hang spot | Built as up to three extra poles standing inside at the classroom windows. Not outside the glass: a student let go out there could be stranded outside the building. |
| 29 Prank call | Built as a second app on the phone. The teacher's phone rings 8 seconds; H answers; ignoring it costs 10 stamina. Caller gets 3 mischief, one call a minute. |
| 34 Fire alarm | Built. Red box above the nearest classroom door, B under it, once per class. 15 seconds of siren and red flashing, and a teacher who sprints slips. No water and no reaction from the classmates yet. |

### Nerf guns reworked (version 0.5.0, installed 2026-11-14, not yet tried in the game)

After trying 0.4.0 (the log showed all four parts working) Gabriel asked for way better gun
models, guns that come out of the drawers, one shot each, and low odds.

- **Models:** `tools\make_nerf_models.py` builds four toy blasters in Blender (run in the
  background) and writes them to `package\models\*.cpm`, the mod's own small text format.
  `Models.cs` loads them. Preview pictures land in `dist\previews`. To change a gun, edit the
  script and run:
  `& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python tools\make_nerf_models.py -- package\models dist\previews`
- **Where guns come from (changed again in 0.5.1, at Gabriel's request):** no roll at class
  start. Each time a student finishes a snack, there is a chance a gun turns up in their own desk
  drawer (open it, press B), and every teacher gets a warning. A teacher who eats a snack out of
  a student's drawer (G) has a smaller chance of finding one and gets it straight in hand.
- **Odds:** 8% per snack a student finishes, 5% per snack the teacher eats (`nerfChancePerSnack`,
  `nerfTeacherChancePerSnack` in config.json). Kind: pistol 45, shotgun 25, sniper 22, RPG 8.
  0.5.0 rolled 20% once per class; he lost it five classes running and asked where they spawn.
- **One shot** per gun.
- **Picking it up (made easier in 0.5.2, installed 2026-11-14, not yet tried in the game):**
  in 0.5.1 the log showed a sniper turning up in Gabriel's drawer and never being taken; he said
  it was too hard to pick up. It lay small among the snacks and only the B key took it, only with
  the drawer open. Now: B takes it any time he sits at his desk, drawer open or shut, and a line
  at the bottom of the screen says so for as long as the gun waits. With the drawer open the gun
  hovers over it, bigger and turning, and looking at it and pressing the game's own pick-up key
  takes it like a snack (`PickUpKeyPatch` on `InputManager.SendMsg` uses up that press so the
  drawer doesn't shut). The log now says when the gun is shown and which key asked for it
  (`nerf guns: showing the ...`, `nerf guns: asking for the ...`).
- **Testing switch:** the F6 owner window has "Every snack eaten gives a nerf gun".
- Only desks with a normal drawer count, so no student guns on the restroom, dormitory or
  schoolyard floors yet.

Things to check: where the guns and the alarm box show up, whether B, X and H clash with the
game's own keys, whether block shapes are drawn in colour, and whether window poles appear at all
(the log says how many windows were usable).

## Third pack: student tricks (version 0.6.0, installed 2026-11-14, not yet tried in the game)

On 2026-11-14 Gabriel confirmed the easier gun pick-up and said to make all the rest of the doc
"but not the skin part". Read as: skip idea 2, customizable characters. Still pack by pack.

All seven tricks are in `Tricks.cs` and are picked from a new Tricks app on the phone (O).
Five go in the hand: X or a wheel click uses them, B puts them away. Models come from
`tools\make_nerf_models.py` (it now builds the trick props too).

| Idea | How it plays |
|------|--------------|
| 24 Paper airplane | Throw it. Hitting a teacher from behind pays mischief; from the front the teacher is told who threw it. A miss lies on the floor for 25 seconds pointing back at the thrower. 15 second wait. |
| 27 Water balloon | Throw it at a blackboard: the board is soaked for 20 seconds and the teacher can't start writing on it (`SoakedBoardPatch` on `BlackBoardComponent.Interect`). 40 second wait. |
| 26 Super glue | Two per class. Squirted at a teacher: no chalk or eraser throws for 10 seconds (`GluedHandPatch` on `SkillComponent.GetCmdInput`). Squirted at the floor: a puddle; the first teacher to walk in is stuck for 4 seconds (the game's own taser stun, `TeacherStunComponent.TryGetProjectileStun`, so the teacher's tip says taser). |
| 25 Laser pointer | Hold X. Everybody sees the red line. A teacher facing the student gets a white screen while it is on them; mischief per second. Six seconds of battery that refills. |
| 23 Whoopee cushion | Put it on a seat or the floor. The first other person within half a metre, classmate bots included, sets it off after 2 seconds: mischief, more for a teacher. |
| 31 Pass notes | The note hops to the classmate furthest away. Mischief on arrival, more for a longer trip. A teacher within about 1.4 m grabs it and is told who wrote it. 30 second wait. |
| 30 Sleep in class | Only while seated. Pays 1, then 2, then 3 mischief every 2 seconds. Everybody sees "Z z z" over the sleeper, and the snore is heard room-wide after about 20 seconds. Any key wakes you. |

Guesses to check in the game: the sounds (snore is the balloon-blowing sound, the cushion is the
balloon-leak sound), whether the held prop shows where the hand is, whether bots count as
`IsStudent` for the note and the cushion, and the mischief amounts.
Alone with bots there is no teacher, so the teacher halves (airplane hit, laser blinding, both
glue effects, note grab) need a second player.

## Looks and new teachers (version 0.7.0, built 2026-11-14, NOT yet installed or tried)

After pack 3 Gabriel tested only the nerf guns (the log shows the easier pick-up working: two
shotguns taken with the game's pick-up key and fired) and then asked for "skins, new teachers and
all that", so idea 2 is back in.

What the game gives us (found in its code): a student's body is four sets of pieces under the
`RenderRoot` part, named `Hair1..4`, `Body1..4`, `Cloth1..4` and `Hand1..4`; the game shows one
set (`NetData.BodyMeshIndex`). Named anchor points come from `unit.GetPartRoot(EUnitPartType.X)`:
`HeadPosition`, `HandPosition`, `RenderRoot`. The player's own body is on the `PlayerSelf` layer
and only casts a shadow, so nobody sees their own outfit in the world.

| Idea | How it plays |
|------|--------------|
| 2 Customizable characters | `Looks.cs` + `Wardrobe.cs`. F7 in a room, or the Wardrobe app on the phone. Hat (8), face extra (3), back item (3), and for students: hair 1-4 or bald, face and body 1-4, clothes 1-4, and a colour each for hair, clothes and skin. A small picture in the window shows the player (its own camera on the `PlayerSelf` layer). The pick is saved in config.json (`lookA`, `lookB`), sent to the host and passed to everybody with the mod. Four Fit sliders move and size the hats (`headUp`, `headForward`, `headSize`, `bodyUp`). |
| 6 Gym teacher | Teacher type in the wardrobe. Bald cap, belly, wider body. X throws a pizza slice: the student hit gets pizza over their screen and is knocked over. 8 second wait. Eating snacks from drawers (G) was already there for every teacher. |
| 19 Lunch lady | Hairnet, apron, ladle. X throws mashed potatoes: the student hit is stuck for 4 seconds (the game's taser stun). She can't write on the blackboard; a big pot is put out near it and holding B next to it gives stamina. |
| 20 Janitor | Cap and mop. X mops: thrown banana peels, glue puddles and whoopee cushions within 3 m are removed and a wet patch stays for 20 seconds that walking students slip on. Not made slower: no clean hook for walking speed was found. |
| 3 + 22 Bald teacher with a wig | Bald cap plus a blond wig. A student behind him within 2.2 m, or within 3.6 m holding the plunger, presses B to yank it off for 20 mischief. It lands a few steps away; he can't throw chalk or the eraser until he walks over it. |

Guesses to check in the game, in this order:
1. Does the picture in the wardrobe show the player at all?
2. Do hats sit on the head? `HeadPosition` is assumed to be at the eyes. The log line
   `looks: my body root is ...` gives the anchor names and the head height.
3. Do colours work? Each character shader logs one line `looks: shader '...' is coloured through ...`
   listing its colour settings; if the wrong one was picked, change `ColorProperty` in `Looks.cs`.
4. Does mixing hair, body and clothes look right, and does the bald cap hide the teacher's hair?
5. Where the pot, the belly, the apron and the things in the hand end up.

## Packs still to do

- **Pack 4, teacher and class events:** coffee mug and hot sauce (28), teacher's pet snitch (36),
  class hamster (33), food fight (35), more random events (38), points shop at the vending
  machine (37), the principal (21).
- **The toilet idea (10).**
- **Pack 7, new floors and modes, the slow part:** pool (4), dance class (9), science lab (12),
  gym (13), art room (14), music room (15), library (16), school bus (17), teachers lounge (18),
  prop hunt mode (32). Each floor needs a room built and models made.

## Not building

- Idea 8 as written, idea 5, and the twerk emote in idea 9 (dance class gets a goofy dance instead).

## Models from Meshy (asked for on 2026-11-14)

Gabriel saw the preview pictures of the Blender-script models and said to use Meshy instead
("these look ass"). State when this was written:

- The mod can now show models with a picture wrapped around them: `.cpm` takes an `x file.png`
  line per colour group and `u v` at the end of each `v` line (`Models.cs`, `Shapes.Textured`).
- `tools\convert_meshy.py` (run with Blender in the background) turns a Meshy `.glb` into a
  `.cpm` plus a shrunk `.png`: joins the parts, thins it to about 6000 triangles, turns it to face
  forward, sizes it, and puts (0, 0, 0) where the mod expects it. Tried on
  `Downloads\Generated-Model.glb`: 237,986 triangles became 5,000 with its picture.
- Not done: no model has been made in Meshy yet. There is no Meshy connector in this session and
  the built-in browser was not signed in to www.meshy.ai. One model cost 20 credits on the page.
  All 37 models would be about 740 credits, so ask Gabriel how many to make before spending any.
- A replacement keeps the old model's file name (for example `hat_cap.cpm`), so no code changes.

### First Meshy models (2026-11-14)

Gabriel installed the Meshy MCP (tools `mcp__meshy__*`; 1,362 credits at the start) and approved 3
test models for 45 credits. Recipe used, 15 credits a model: `meshy_text_to_3d` with
`model_type: smart-topology`, `ai_model: meshy-t2.5`, `target_polycount: 5000`, `target_formats: [glb]`
(5 credits), then `meshy_text_to_3d_refine` with a short `texture_prompt` (10 credits), then
`meshy_download_model` with `save_to`. Prompts that worked start with the thing, then
"stylized cartoon game prop ... single object, no head, no mannequin".

- Saved: `meshy\hat_crown.glb`, `meshy\hat_cowboy.glb`, `meshy\back_pack.glb`, pictures in
  `meshy\pictures\`. The download links expire on 2026-11-17; the files here are the copies to keep.
- He then said "dont put in just show me them", so they were only shown as pictures. NOTHING from
  Meshy is in `package\models` or in the game yet. Wait for his word before converting or installing.
- Still to make if he likes them: 34 models, about 510 credits.

### Game-style Meshy recipe (2026-11-14, after "they look wrong, make them the same style as the game")

The first three models were too realistic. The game's look, from its Steam screenshots: chibi
characters with very big round heads, smooth rounded toy-like props, flat bright colours.
Hats will need to be much bigger than a real head: start `headSize` well above 1.

Recipe that matched the style, 21 credits a model:
1. `meshy_image_to_image`, `ai_model: nano-banana-2` (6 credits), `remove_background: true`,
   `aspect_ratio: 1:1`, with two Steam screenshots of the game as `reference_image_urls`
   (`.../apps/5175310/5e243e63.../ss_5e243e63....1920x1080.jpg` shows props in a drawer,
   `.../12f3c437.../ss_12f3c437....1920x1080.jpg` shows characters). Prompt pattern: "Draw one
   new object: a <thing>, as a 3D game prop in exactly the same art style as the objects and
   characters in these game screenshots: smooth, rounded, chunky toy-like shapes, soft plastic
   vinyl-toy look, flat bright solid colours with soft simple shading, no realistic texture, no
   fine detail. <colours>. The <thing> alone, centred, seen from the front and slightly from
   above, on a plain white background. No characters, no hands, no text."
2. `meshy_image_to_3d` with `input_task_id` = that image task, `model_type: smart-topology`,
   `ai_model: meshy-t2.5`, `target_polycount: 5000`, `texture_model: meshy-n1`,
   `target_formats: [glb]` (15 credits).

Made this way: `meshy\hat_cowboy_toy.glb` (style picture in `meshy\style\`, render in
`meshy\pictures\`). Gabriel asked for just this one first. Still nothing from Meshy in the game.

### All toy-style Meshy models made (2026-11-14)

34 more models made with the game-style recipe after Gabriel said "Yes, make the rest":
`meshy\toy\*.glb` (35 with the cowboy hat), pictures in `meshy\pictures\`. 582 credits left.
Details and the weak ones in `meshy\tasks.md`. They were shown to him as three overview sheets.
Still NOT converted and NOT installed: the game runs 0.6.0, and 0.7.0 (wardrobe and new
teachers) waits with the old Blender-script models in `package\models`.

### Meshy models in the game (version 0.7.0 installed 2026-11-14)

Gabriel said "Yes, put them in". `tools\convert_all_meshy.py` converted all 35 toy-style models
into `package\models` (`.cpm` plus a 512 px `.png` each; its MODELS table holds every model's
size, origin and facing, and three greyish white models are brightened). Facing was checked on
preview sheets in `dist\previews` (red bar = forward): guns, caps, sunglasses, backpack all right.
The bald cap and the clown nose are still the Blender-script models.

Version 0.7.0 (wardrobe, new teachers, Meshy models) is installed and the log shows
`[Mods] Loaded classpack 0.7.0` with Looks=True Teachers=True and no errors. NOTHING of it has
been seen in a room yet. Hat defaults were raised for the game's big heads (`headSize` 1.8,
`headUp` 0.05, `headForward` -0.10); these are guesses, the wardrobe's Fit sliders correct them.
Share zip: `share\ClassPack-0.7.0.zip` (12 MB).

First things to check with him: the wardrobe picture shows the player; hats sit on the head;
colours work; models are the right size in the hand (guns, tricks) and are not too dark.
