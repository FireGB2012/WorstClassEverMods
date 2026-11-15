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

### Fit fix and how-to cards (version 0.7.1, built 2026-11-14, NOT installed yet)

Gabriel's first look at 0.7.0 in the wardrobe: hat at eye level and too big, sunglasses huge and
at the mouth, backpack down by the legs and far behind ("nothing is right position and size").
Cause, from the log line `looks: my body root is role00, head anchor neck at height 0.76`: the
game's `HeadPosition` part is the NECK. A student is about 1.1 m tall, the middle of the head is
about 0.17 m above the neck and the head is about 0.16 m in radius (measured off his screenshot).

- New fit defaults: `headUp` 0.17, `headForward` 0, `headSize` 1.45, `bodyOut` 0.11 (how far the
  back and belly are from the body's middle). `fitVersion` in config.json makes old saved fit
  numbers get replaced once. Back items sit 0.13 below the neck, belly items 0.24 below.
- Hats now turn and tilt with the head: the head bone (first bone under the neck with "head" in
  its name, else the neck) has its up and forward axes worked out while the head is upright.
- Body items, the teachers' gear and the pot were converted smaller; glasses and moustache lower.
- Held guns and tricks sit just under the neck in front (they were placed 1.1 m up, above a
  student's head); long guns are shrunk in the hand (`HeldScale`).
- What you see of your own outfit (his request): a student does not see their own head items,
  body items stay visible; a teacher sees none of their own. Done with shadow-only rendering,
  because the game's own camera does draw the `PlayerSelf` layer.
- How-to cards (`Tips.cs`, his request "make it like first time using them so you understand"):
  the first time in a class as a student, as a plain teacher, or as each new teacher type, a card
  with the keys shows for 25 seconds. F1 hides it and brings it back. Seen cards are kept in
  `seenTips`. The wardrobe also explains each teacher type and how to become the teacher.
- The teacher's body size is still unknown; the log now prints the anchors for a teacher body too.

Version 0.7.1 was installed on 2026-11-14 after Gabriel said "Yes, close and restart". The log
shows `[Mods] Loaded classpack 0.7.1` with no errors, and config.json now holds the new fit
numbers (`fitVersion` 2). Not yet seen in a room: the new hat, glasses and backpack placement,
the how-to cards, and the teacher's own outfit. Share zip: `share\ClassPack-0.7.1.zip`.

### Second refit and several items at once (version 0.7.2, installed 2026-11-14)

Gabriel's second screenshot (front view, cap + sunglasses): "still meh". The cap was sunk into
the hair with only its peak showing, and the eyes poked out over the top of the glasses.
Measured off that picture: with the hair the head is about 0.19 m in radius and 0.36 m wide,
eyes about 0.205 m above the neck, mouth about 0.10 m.

- Fit defaults now `headUp` 0.19, `headForward` 0.01, `headSize` 1.7 (`fitVersion` 3), so a hat's
  rim lands just above the eyes and its dome covers the hair.
- Glasses raised to the eyes and pushed 2 cm further out so the eyes are behind the lenses;
  moustache raised to sit between eyes and mouth.
- His request "have more on the head": Face and Back are now sets (one bit per item in
  `Look.Face` / `Look.Back`), with toggle buttons in the wardrobe. Hat is still one pick.

The log shows `[Mods] Loaded classpack 0.7.2` and the new fit numbers in config.json. Not yet
seen by him. Share zip: `share\ClassPack-0.7.2.zip`.

### Game-style windows and teacher drawings (version 0.7.3, installed 2026-11-14)

Gabriel asked for the new teachers as their own Meshy models and for all the mod's windows to
"use the game's UI and look perfect".

- Windows: the game's menus are uGUI with TextMeshPro; a player screenshot of its results window
  showed the look: cream paper panels with a brown line and round corners, white cells, green
  round buttons with white text, an orange title with a dark outline, white outlined text on
  the playing screen. `Skin.cs` now holds that look (round-cornered boxes painted in code, the
  OS font Arial Rounded MT Bold) and the phone, wardrobe, owner window, hint line and how-to
  cards all take their styles from it. Still IMGUI, not the game's real widgets. Not yet seen
  by him: check fonts, spacing and the sliders in a screenshot.
- Teachers: he approved about 100 credits for three full characters. Drawings are made (18
  credits) and were shown to him: `meshy\style\teacher_gym.png`, `teacher_lunch.png`,
  `teacher_janitor.png` (picture tasks 01a1dc0d-dcbf-76d4-8301-8a71456db809,
  01a1dc0d-dfa0-70ba-aded-3673c642e337, 01a1dc0d-e1b1-7708-8707-de97e752f516, expiring
  2026-11-17). NOT done: the 3D models, skeletons and moves, and the code that would show a
  moving character in place of the game's teacher. That code does not exist yet: the mod can
  only show still models. The game's own teacher is a tall thin adult, about twice a student.

### New teachers as moving characters (version 0.8.0, installed 2026-11-14)

Gabriel approved the drawings ("Yes, build them"). The gym teacher, lunch lady and janitor are
now whole Meshy characters with a skeleton and idle, walk and run moves (105 credits, 477 left;
task ids and recipe in `meshy\tasks.md`).

- `tools\convert_character.py` turns a rigged, animated `.glb` into a `.cpc` character: body,
  picture, bones and every bone's place in every frame at about 15 frames a second, in
  whole-body space, 1 m tall, feet on the ground, walking on the spot. The maths was checked
  against Blender's own skinning (difference 0.0000 m on 400 points in a running pose).
- `Characters.cs` loads a `.cpc`, builds a skinned body with bones side by side under one root
  and plays the moves itself, easing between them.
- `Looks.cs`: a teacher whose type is gym teacher, lunch lady or janitor gets that body. The
  game's own teacher is hidden (`forceRenderingOff`) and keeps running underneath; the new body
  follows its position and facing, is sized from the tallest the neck point has been plus
  0.28 m, and plays idle, walk or run from how fast it moves. The single pieces (belly, apron,
  cap, hats) are not used on these three any more. The wig teacher is still the game's teacher.
- Known limits: no moves for throwing, writing on the board or carrying a student, so those
  moments show the idle or walk move; what the game's teacher holds or carries floats where the
  hidden body's hand is.

The log shows the mod loading. NOT yet seen in a class: the characters themselves, their size
and whether they face the right way. A `character '...' loaded` log line appears the first
time one is shown.

### Pot on the stand, easier lunch lady (version 0.8.1, installed 2026-11-14, not yet run)

Gabriel: "put the lady's pot on top of the thing and also easy to use her part". My reading of
"the thing": the teacher's stand at the front of the class, the one the teaching plan lies on.

- Pot: the host looks for the unit with `TeachingPlanComponent` nearest the lunch lady and puts
  the pot on that same surface next to the plan (a ray down at eight spots around it, keeping
  the first that lands at the plan's height), at half size. With no such unit it falls back to
  the floor by the blackboard. The log line `teachers: the pot is at ...` says which.
- Stirring needs no key any more: standing within 2.6 m of the pot fills stamina.
- The gym teacher, lunch lady and janitor now use the game's own second throw key (the eraser
  key, `eCmdType.UseItem2`) for their power; X and the wheel click still work. The hint line
  shows the game's name for that key.
- Fix found in the log of his 0.8.0 session: `my teacher body root is missing, head anchor neck
  at height 1.51, hand HandPos at height 1.34`. The game's teacher has no `RenderRoot` part, so
  in 0.8.0 the game's teacher was NOT hidden under the new character. Now the teacher's whole
  model is used as its body root and only its own skinned parts are hidden.

Installed with the game closed. He sent a message instead of answering whether to start the
game, so it was not started and the load of 0.8.1 is not checked in the log.

## Swim class and dance class (asked for 2026-11-14, plan only, nothing built)

Gabriel: "now make the swim class and the dance class" (ideas 4 and 9). What the game's code
says about floors, checked that day:

- Every floor is in one scene (`Assets/Pack/Scenes/101_ClassroomScene.unity`; TbScene has no
  per-floor scenes). A "floor" is an area plus an index (`ClassroomManager.currentFloor`,
  `ClassroomItem.floorIndex`). So a new room can be built in code in an empty spot of that scene.
- Moving a player: the pattern of `ClassroomManager.TeleportClassPlayer`: fill a
  `UnitTeleportPositionMessageResponse_S2C` (unitUID, position, rotation, floor), call
  `RoomManager.GetUnitMessageHandler().OnUnitTeleportPositionMessageResponse(msg)` on the host
  and `BroadcastToAllExceptHost(msg)`.
- Emotes: TbExpression has eight (AnimParam DH1-DH4, SH1-SH2, POS1-POS2), played by
  `StudentExpressionComponent`. They are the only extra moves the game's students have, so a
  "dance" has to be made of them; new moves can't be put on the game's own characters.
- Limits to tell him: the classmate bots only know their own classroom and desks, so a room the
  mod builds has real players only; the game's characters have no swimming moves; a real new
  floor in the elevator would need the game's level tools, so the class type is picked in the
  F6 owner window instead.

Plan put to him (waiting for his yes): build one class at a time, swim class first.
Swim: pool hall built by the mod, players sent there at the bell; mischief for jumping off the
diving board, splashing a teacher at the pool's edge and staying under water; slow wading in
the water; poles at the poolside; a lifeguard chair where the teacher gets stamina back.
Dance: hall with a dance floor; two students close together who both dance (the game's emotes)
earn mischief every second; a goofy dance, no twerk emote. About six Meshy props per class,
roughly 130 credits each (477 left).

## Swim class (version 0.9.0, installed 2026-11-15, NOT yet seen in the game)

Gabriel said "Yes, build it", approved 126 Meshy credits for six pool props (351 left) and then
"Yes, finish it". Everything is written, compiles and is installed. Nobody has stood in the hall
yet: how it is lit, whether the floor holds and whether the props sit right are all unchecked.

How it plays:
- The room owner picks "Swim class" under "Next class" in the F6 window (saved as `nextClass`).
  One second into the next bell's Prepare phase the host picks an empty spot (300 m to the side
  and 100 m above the classroom, checked with a box query), tells every modded player
  (`CpKind.Pool`), stands four of the game's own poles along the pool and 0.6 s later teleports
  every modded player in with the game's own teleport message (floor 0, so floor visibility is
  left alone). Players without the mod stay in the classroom; the host gets a notice.
- Students: walk off the diving board into the water (+12, pays every 15 s), press B in the
  water to splash (+15 when a teacher is within 3.8 m, 6 s wait; the teacher's screen is covered
  in water for 3 s), crawl in the pool to dive under (+2 a second, 8 s of air, 3 s to catch
  breath). Wading is at 60 % speed for everyone (`InputComponent.speedFac`).
- Teacher: catches as usual. Standing within 2.4 m of the lifeguard chair fills stamina.
- When the class is over (BeforeClass) the game sends everyone back itself, the host removes
  the poles and the hall is destroyed 1.5 s later. Every client also removes it by itself after
  4 s of BeforeClass, and puts the walking speed back.
- First-time cards "swim" and "swimteacher"; a hint line; an air bar and a blue screen under water.
- The F6 window also has a "Pool lamps" slider (`poolLight`, 0.2 to 3, each player's own).

Code: `ClassPack\Rooms.cs` (tiled boxes with a picture made in code, water sheet, lamps, props,
unseen blocks; solid things go on the game's "Ground" layer) and `ClassPack\Swim.cs` (all of
the above; the hall's measurements are constants at the top, in the hall's own frame: x along
the pool, z across, y = 0 at the deck). Net.cs has `Pool = 70`, `SplashAsk = 71`, `Splash = 72`
and `Net.IsModded`.

Props (`package\models\pool_*.cpm`, real metres, converted with no turn so Meshy's front is
the game's -Z): board (origin under the plank's tip, plank top 1.00 m, block from 2.08 m,
ladder on the -Z side), chair (1.9 m, faces -Z), ladder (long legs on -Z, origin at the pool's
edge), ring (0.8 m), bench (1.9 m), duck (origin at its waterline). Sizes were checked
by reading the .cpm files; the preview pictures in `dist\previews` are useless for big props.

Things I could not check without the game, in the order I'd suspect them:
1. Light. The hall has six point lamps and a faint glow of its own on every surface (the glow
   only works if the game's build kept that kind of material). The log line "swim: hall built."
   prints the game's ambient light and whether extra lights are on.
2. The floor. Solid boxes are on the "Ground" layer, which the game's ground probe names first.
3. Getting onto the diving board: an unseen 45 degree slope over the ladder (max walkable is 65).
4. Water: see-through is set by material numbers; if the game's build lacks that, it is solid blue.
5. Whether crawling under water really hides the head (water is 0.70 m deep, students 1.1 m tall).
6. The game checks line of sight on "Ground" for a catch, so a teacher standing back from the
   edge can't grab a student down in the pool through the deck's corner. That suits the idea.

Facts found for this: unit heights travel as centimetres in a short (keep within about 300 m),
max walkable slope 65 degrees, max step 0.325 m, there is no jump, crawl is `eCmdType.Crawl`
and `StudentCrawlComponent.IsCrawling` is synced, the bots only count as scenery
(`AreAllStudentsPunished` looks at players), so the teacher can still win by catching everyone.

Dance class: not started. He tests the swim class first.

## Swim class, second round (version 0.10.0, installed 2026-11-15, the log shows it loads; NOT yet seen in a class)

His first swim class worked (log: hall built at (300, 100, 0), 4 poles, he was sent to the pool
as the only student; ambient light is flat 0.5 grey and extra lights are on per pixel, max 8).
He then wrote: "no bots are spawning, add a real swim animation, add a teachers' lounge for
DoorDash and make DoorDash 5 mischief". Asked about credits and bots he chose: swim moves yes
(18 credits), lounge props "sofa and table only" (42 credits), bots "use the bots from floor 1".
291 credits left by my count.

What 0.10.0 adds (all compiled, none of it seen in the game):
- DoorDash pays 5 mischief. `doorDashPoints` default is 5 and `rulesVersion` 1 rewrites a saved 15 once.
- Teachers' lounge: a doorway in the deep end wall (z = -6) leads to a 6 by 6 m room with a wooden
  floor, a sofa and a coffee table (`lounge_sofa`, `lounge_table`). While a swim class runs,
  `Swim.TryLoungeSpot` makes DoorDash drop the food onto that table instead of the school's office.
- Bots: the game's own bots come to the pool. The game already has roaming bots for its
  playground floor: a `PlaygroundNavigationArea` over a `NavMeshSurface` whose area is named
  "PlaygroundArea", set on a bot with `StudentNPCComponent.SetPlaygroundArea`. `Swim.BringBots`
  (host, every 0.3 s) builds such a surface from the hall's solid boxes the first time a bot
  with a `NavMeshAgent` exists (the lounge, the board's plank and its ladder slope carry a
  `NavMeshModifier` that keeps them out), then warps each new bot onto the deck and sets the
  area. The game moves and syncs them from there and removes them itself at the end of class.
  Setting `poolBots` switches it off.
- Real swimming: Meshy's "Swim Forward" (569) and "Swim Idle" (568) moves.
  - The three Meshy teachers got them as clips `swim` and `swimidle` in their `.cpc`
    (`convert_character.py --append`); `Looks.PlaceBody` plays them in the water and floats
    the body at the surface.
  - The game's own students, bots and teacher get the same moves through `SwimPose.cs`: the
    moves are also stored joint by joint in `package\models\moves_swim.cpr`
    (`convert_character.py --moves`), and in LateUpdate each swimmer's bones are turned by
    "the joint's turn since rest in the move" x "a fixed turn lining the game's bone up with
    the move's resting bone" x "how the bone was turned when the character stood upright".
    The game's bone names (found in the prefab data): spine1, spine2, neck, head, head_end,
    shoulder/upper_arm/forearm/hand .L/.R, thigh/shin/foot/toe .L/.R. The hips are then put at
    the water's surface by moving the top of the skeleton.
  - A player's own body is left alone (the game is seen through its eyes), so alone you only
    see the move on the bots.
- BUG FOUND AND FIXED: Blender's glTF import adds a helper ball ("Icosphere", 2 m across) that
  `convert_character.py` counted as part of the body. All three teacher characters were 63 %
  of their height and floated 37 % up since 0.8.0 (nobody saw it: a teacher can't see their
  own body). The converter now takes skinned meshes only and all three were rebuilt.

Log lines to read after his next test: `swim: walkable area for bots ready`, `swim: first bot
brought to the pool`, `swim move: loaded`, `swim move: first character set up` (names the top
of the skeleton and what the hips and thighs hang under), and any `error in the swimming move`.

## Seeing yourself swim, and the hands-free self-test (version 0.11.0, 2026-11-15)

He asked: "make the swim class 100% and the swim anim so the teacher and yourself can see".
He had not played a class with 0.10.0 (the log only showed it loading).

- `SelfView.cs`: while the local player swims, the picture is taken from 3 m behind and a
  little above their head, and their own body is drawn (Looks treats it like the wardrobe's
  preview) with the swimming move on it. The game's camera is not touched: the camera object is
  moved in `RenderPipelineManager.beginCameraRendering` and put back in `endCameraRendering`,
  so everything that aims along the camera still works from the eyes. Setting `swimSelfView`,
  also a toggle in the F6 window. How the game hides your own body: `Unit.SetPlayerLayer` puts
  Hair/Body/Cloth on layer PlayerSelf and sets them to shadows only; the hands stay visible.
- `AutoTest.cs` + `tools\selftest.ps1`: the way to SEE the mod without a player. The script
  builds and installs, leaves a one-shot `autotest.txt` in the settings folder and starts the
  game; the mod then hosts an offline room (`GameRoot.Inst.NetworkType = ENetworkType.Local`,
  `NetManager.StartHost()`, `SceneManager.ChangeScene(2001)`, the same calls the menu's Create
  button makes), rings the bell (`ClassroomManager.ClassPrepare(0)`), and during the swim
  class takes pictures from fixed spots with its own camera, pictures of the player's own
  screen, moves the player into the pool, under water, onto the board and into the lounge,
  orders DoorDash, writes `report.txt` and quits the game. Results: `dist\autotest\<role>\`
  with `sheet_N.jpg` overviews. He said yes to running it a few times on 2026-11-15; ask again
  on another day, since the game window takes over his screen for about two minutes a run.

### What the first two self-test runs showed (2026-11-15), and what is NOT fixed yet

He watched a run and said: "make the bots also swim" and "the anim looks like ass". Both are
true in the pictures (`dist\autotest\student\`, not kept in git).

Works, seen in pictures: the hall, pool, water, diving board (standing on it and diving off
pays), ladders, chair, benches, rings, ducks, the four poles, the doorway and the lounge with
sofa and table, 20 bots brought to the deck and walking around, the view from behind while
swimming, the under-water view with the air bar, under-water and dive mischief.

Still wrong, to do next, in this order:
1. Bots never enter the water. The game's walker for bots has max slope 0 (radius 0.18,
   height 1.00, step 0.30), so the ramp is not walkable and the pool floor (114 m2 of walkable
   area) is an island: a path from the deck to the pool is only "partial". Plan: a wide
   `NavMeshLink` from the top of the ramp to its foot (the bot code crosses links by walking
   straight to the far end, `StudentNPCComponent.MoveAcrossLink`), and a Harmony postfix on
   `PlaygroundNavigationArea.TryGetDestination` that sends about half the picks into the pool.
2. The swimming move on the game's own characters looks bad. Causes found: the real hips bone
   is `gupen` (spine1 hangs under it, the thighs hang under it too), and I had turned spine1
   as the hips, so pelvis and legs stayed upright; and Meshy's "Swim Forward" is a small
   breaststroke glide that does not read on big-headed characters (`tools\show_moves.py`
   renders the original). Plan: replace the copied move in `SwimPose.cs` with a front crawl
   and treading water made in code on the real bones (big arm circles about the shoulder
   line, kicking legs, head up), hips placed just under the surface. The skeleton is known
   now: `AutoTest.DumpSkeleton` writes `skeleton_student.txt` / `skeleton_bot.txt` (bones
   gupen, spine1, spine2, neck, head, shoulder/upper_arm/forearm .L/.R, thigh/shin .L/.R;
   foot.L/R hang under Root, not under the shins; the whole skeleton is scaled by 80).
3. Too bright: the school's sun shines into the hall (the mod's ceiling casts no shadow) on top
   of the flat grey ambient light, so pale tiles clip to white. Colours and glow were toned
   down in the code after the second run; that change is NOT yet seen in a picture.
4. DoorDash in the test: the order was taken but the class ended (he won at 20 of 10 mischief
   in the solo test) before the food was photographed. Unchecked.
5. Not run yet: the test as a teacher (`-Role teacher -Kind 0..4`).

Most likely to be wrong, in my order of doubt: the swimming pose on the game's skeleton (blind:
left/right, the hips' height, hats on swimmers), bots that were already seated when the class
turned into a swim class, bots walking through the poles (they are not in the walkable area's
picture), the sofa's facing.

## Bots in the pool and a new swimming move (version 0.12.0, 2026-11-15, second PC)

Set up on a second PC the same day: the game is in `D:\SteamLibrary\steamapps\common` (the
build now looks in both Steam libraries), .NET 8 SDK 8.0.428. ILSpy was not installed: the
game's code was read with a small PowerShell script that loads `Assembly-CSharp.dll` for
reflection only and prints a method's IL with its calls and fields named (kept outside the
repository).

Gabriel picked "bots into the pool" first and then wrote: "after that make the other stuff and
open the game yourself and check, I won't be here for a while". So everything below was built
and checked with the hands-free self-test while he was away. Nothing is pushed yet.

What the game's bot code does (read from the IL):
- `PlaygroundNavigationArea.TryGetDestination(agent, path, out destination)`: up to 12 tries of
  a random point weighted by triangle area, `NavMesh.SamplePosition` within 0.4 m, inside the
  surface's bounds, at least 2 m away, and `agent.CalculatePath` must give `PathComplete`. The
  path is left in `path`, and `StudentNPCComponent.UpdatePlayground` then calls `agent.SetPath`.
- On a link (`agent.isOnOffMeshLink`) `StudentNPCComponent.MoveAcrossLink` walks the bot in a
  straight line to the link's far end at walking speed, then `CompleteOffMeshLink`.

Bots into the pool (done, seen in the self-test):
- `Swim.AddRampLink`: a `NavMeshLink` over the ramp, 7.8 m wide, from the deck at (-8.5, 0) to
  the pool floor at (-4.5, -0.85), both ways, area "PlaygroundArea". The report now says "way
  from the deck into the pool: PathComplete".
- `PoolBotDestinations` (Harmony postfix on `TryGetDestination`) calls `Swim.SteerBot`, which
  moves 60 % of the bots' picks on the deck to a spot on the pool floor and works out the
  game's path to it (if none is found, the way to the game's own pick is put back).
- With 35 % only 2 to 7 of 20 bots were in the water; with 60 % it is 9 to 11 of 20.

New swimming move (done, seen) - `SwimPose.cs` rewritten:
- Why the old one looked bad, from the skeleton files: spine1 sits at the same place as gupen
  (so turning it as "the hips" left the pelvis and legs upright), and the hands
  (`shouzhang2.L/R`, with the fingers under them) and the feet (`foot.L/R`) hang under `Root`,
  not under the forearms and shins. The game's animations place them with IK, so when the arms
  and legs were turned, the hands and feet stayed where the walk had left them.
- Now: each bone is aimed at a direction in the swimmer's own frame (forward, right, up),
  turned the shortest way from a fixed resting direction (spine up, arms straight out to the
  side, legs down). The moves never point a bone straight against its resting direction, so an
  arm can circle without flipping. Every bone of this skeleton points along its own up axis.
  Hands and feet are then put at the wrist and ankle (their offset from the forearm or shin is
  noted when the rig is set up). The hips are held at 0.17 torso lengths under the surface in
  the crawl and 0.92 when treading water.
- Crawl: body flat, head up, big alternating arm circles (straight pull under the body, bent
  elbow over the water), flutter kick, toes pointed. Treading water: upright, hands sculling,
  legs pedalling. Speed above 0.3 m/s means the crawl.
- Seen in the pictures (`dist\autotest\student\` 10b, 11, 16 to 18, and teacher0 16 to 18):
  bots, the student player and the game's own teacher crawl flat with the head up. A bot that
  stops in the pool treads water upright; the water is so clear that this reads more like
  standing in the pool.
- The three new teachers (own Meshy bodies) still play their Meshy clips; `SwimPose.SurfaceFor`
  still reads `moves_swim.cpr` for them.

Also checked: the light (overview pictures show the tiles and colours, no longer washed out);
DoorDash in the lounge (the self-test now orders before the dive, which used to win the solo
class first; `24_lounge_food` shows the hamburger on the coffee table).

Self-test changes: the DoorDash order and lounge pictures come before the dive, a
`10b_bots_in_pool` picture and "bots in the water" counts were added, and the "bot on the deck"
picture skips bots by the walls (the camera ended up inside the wall).

Teacher runs (`-Role teacher -Kind 0..4`, all five went through with no errors): the game's
own teacher and the wig teacher crawl with the new move (the wig stays on the head). The gym
teacher, lunch lady and janitor have their own Meshy bodies and clips: their stroke is a flat
breaststroke glide, which reads fine. The first runs only showed them treading water, because
`SwimPose.ForceMoving` did not reach their bodies; `Looks.PlaceBody` now honours it too.

### Peeing in the pool (asked for 2026-11-15, same version 0.12.0)

Gabriel: "make so the way to get mischief is to piss in the pool". Then, after the first
version (+10 every 10 s, which won a solo class with one pee): "it works, it's OP now, just make
the peeing balanced".

How it plays now:
- A student in the pool's water holds P (`peeKey`) for 3 seconds: a yellow bar "PEEING..." fills
  and they can hardly move meanwhile (speed x0.25), so a teacher close by can catch them at it.
  Letting go early cancels it.
- Then a yellow cloud spreads in the water around them (about 5 m across after 3 s, fades away
  after 16 s, seen by everyone, with a leaking sound) and they get +4 mischief (`peePoints`).
  Double (+8) when a teacher is in the water inside the cloud; that teacher is told who did it.
- The next pee is possible after 30 seconds (`peeWaitSeconds`). The host's numbers count; the
  host tells the student's game the wait, so its countdown matches.
- Diving off the board, splashing the teacher and staying under water still happen, but no
  longer pay mischief unless the room owner switches on "Diving, splashing and staying under
  water pay mischief too" (`poolOldMischief`) in the F6 window. The F6 window also has "Pee
  mischief" (1 to 10) and "Pee wait" (10 to 60 s) sliders, host only. Keep labels in that
  window short: a row wider than the window spills out on the right (a scroll view was tried
  and made it worse: it widened everything and broke the wrapped notes).
- New how-to cards `swimpee` and `swimteacher2` (new names, so everyone sees them once).
- Code: `Swim.OnPeeAsk` (host), the hold in the local tick, `AddCloud`/`TickCloud` (two
  see-through sheets made with `Rooms.Water` and a soft yellow picture made in code), Net kinds
  `PeeAsk = 73` and `Pee = 74`.
- Seen in the self-test: the cloud spreading around the student, from above and through their
  eyes (`27_pee_cloud`, `28_eyes_pee`); mischief 0 before, 4 after; a second pee right away
  pays nothing; diving under and the dive off the board pay nothing; the class no longer ends
  early in the solo test. The self-test now pees right after the class starts (before
  DoorDash, whose food also pays mischief) and takes a picture of the F6 window
  (`29_owner_window`).

## Dance class (version 0.13.0, 2026-11-15, built and self-tested on the second PC)

Gabriel: "make dance class, ask me questions if needed". The answers that count in the end:
- First he said "2 people dance together at all times, you need to twerk dance to get
  mischief". The twerk was declined again (the students are kids; same call as on the first
  PC). A pairs version was built first (two dancers next to each other earn mischief), then he
  said "that's not how I wanted it: you always have to emote, but you get mischief for the
  naughty emote". Asked which naughty dance instead of the twerk, he picked the worm, and
  "normal dance on Q, naughty on J".
- Props: 6 from Meshy, 126 credits (approved, made, 165 left): disco ball, speaker, DJ booth,
  stage, boombox, snack table. `package/models/dance_*.cpm` (stage about 0.6 m high, its steps
  at its front; the disco ball hangs from (0, 0, 0)). Pictures: `meshy/pictures/dance_models.png`
  (shown to him before they went in). Tasks in `meshy/tasks.md`.
- Music: the game's own party songs. `AudioManager.PlayBGM(AudioEvents.bgm_Hajimi1 / 2)`,
  `StopBGM(FMOD.Studio.STOP_MODE)`; the field `currentBGMPath` puts the old music back after.

How it plays (`Dance.cs`, the moves in `SwimPose.cs`):
- The owner picks "Dance class" under "Next class" in F6 (`nextClass` 2). At the bell a dance
  hall (24 x 18 m) is built far away and every modded player is teleported in, like the pool.
  A light-up 8 x 8 m dance floor whose tiles change colour on the beat, four coloured party
  lights circling over it, the disco ball turning, neon strips, the stage at the far end with
  the DJ booth and the speakers, the snack table and boombox, benches, four poles.
- Students always dance. The class dance (Q, `danceKey`) is a silly chicken dance (elbows
  flapping, head pecking, knees bouncing, a sway) and lets them shuffle at 0.6 speed. The worm
  (J, `wormKey`) puts them face down on the floor with a wave rolling from the feet to the head;
  they creep at 0.3 speed, so the teacher can catch them, and the host pays `dancePoints` (1)
  every `danceSeconds` (5) while they keep it up, in class only. Q gets them back up. The view
  from behind (SelfView) shows only during the worm. F6: "Worm pays" and "Worm every".
- The teacher doesn't have to dance (Q dances for fun), catches as usual, and the DJ booth
  fills stamina. Bots always do the class dance and mostly hang around on the dance floor
  (the shared `TryGetDestination` postfix moves 70 % of their picks there).
- DoorDash food lands on the snack table; DoorDash's messages now name the right spot in every
  class (`DoorDash.WhereFood`). How-to cards `dance2` and `danceteacher2`. Hats follow dancers.
- Net kinds: DanceHall 75, DanceAsk 76, Dancing 77 (a = character, i = 0 none, 1 class dance,
  2 worm). Settings `rulesVersion` 2 moves a saved J dance key (from a test build) to Q.
- Self-test: `.\tools\selftest.ps1 -Class dance` (and `-Role teacher`), results in
  `dist\autotest\dance_<role>`.

Seen in the self-test so far (pairs version and then the worm version): the hall, props, lights
and music; 20 bots arrive and dance; the student is rooted or slowed as set and back to normal
after; DoorDash on the snack table; the teacher arrives on the stage and stands at the booth.

Worm version, self-test `dist\autotest\dance_student` (run 3): 21 dancers (20 bots and the
student) doing the class dance at once; the student shuffles at 0.6 speed; the worm creeps at
0.3, shows from behind, and paid 0 -> 3 mischief in about 13 s; Q put him back on his feet at
0.6. Pictures 17 to 19: face down on the dance floor, chest up, arms pushing. The card and the
hint line show the keys. Not yet seen: the worm by a teacher-caught student, and it played by
Gabriel himself.

## Looks pack (to be 0.14.0, not pushed yet): state on 2026-11-15

Done and committed:
- Face sliders (eyes, pupils, head), afro and dreads from Meshy, bots copy a player, wardrobe tabs.
- Looks test run 2: head size, dreads and the bots dressed like players look right. Eyes and pupils came out as giant balls.
- Reworked (builds; test run 3 was started, its pictures are not checked yet): eye size grows the eyes and lids across the face (little in depth) and moves them apart past 1.3 times; pupil size moves the eye points on the face picture (one shared copy of the face mesh per pupil size); every size starts from what the game animation gave the bone that frame, so blinks still play.
- Afro: the converter has a --cut option; the afro is cut off below the mouth so it no longer wraps round the chin.
- Test: each face picture hides every other character, and the bot in it stops dancing (SwimPose.TestStill).

Still to do:
- Check run 3: dist/autotest/dance_student/12_face_*.jpg, 07_wardrobe_tab_1.png, 11_bot_dance_*.jpg (bots copy pupils 0.6) and the log line "looks: eyes in ..." (pupil middles). Change the slider ranges if needed.
- Idea 4 of the pack, the nerf faint ragdoll, as approved: a student ragdolls 3 s and then goes on a pole with no rescue; the teacher faints 5 s and gets up; a bot faints 4 s. Not started. Plan: host tell Faint = 78 (a = uid, f = seconds, v = hit direction); stun with StudentStunComponent.TryGetProjectileStun for students and bots, Poles.Slip for the teacher; a physics ragdoll built on the spot from the character skeleton (SwimPose mode 4); the fainted player sees themselves from behind (SelfView) with a "You fainted!" line; NerfGuns.Hit rewired; a how-to card in Tips.cs.
- Bump package/mod.json to 0.14.0 (plain text edit), notes, ask before pushing.
- Run 3 result: eye size now works (big round googly eyes, moved apart, pupils show, no bulging balls). The pupil slider does nothing yet: the log says "Body1 has no picture places for its points" for Body1 to Body4, so mesh.uv came back empty or the wrong length although mesh.boneWeights reads fine. Next try: mesh.GetUVs(0, list), and log the counts of uv, vertices and boneWeights. Front of each eye bone is about its +Y axis; the lids hang under head.

### Nerf faint and version 0.14.0 (2026-11-15, later the same day, NOT self-tested yet)

- Pushed everything up to the run 3 notes (Gabriel said yes).
- Pupils: the eye picture is now read with mesh.GetUVs into four-number places (the plain
  mesh.uv read came back empty). If that still fails, the log line says how many points, places
  and weights the face mesh has, and the eyeballs grow across inside the lids instead.
- Nerf darts knock people out (ClassPack/Faint.cs, new tell CpKind.Faint = 78). The host picks
  the time (student player 3 s then a no-rescue pole, teacher 5 s with the banana slip, bot 4 s)
  and tells everybody. Every game stuns a fainted student the game way and builds a physics rag
  doll from the skeleton: capsules for pelvis, chest, arms and legs, a ball for the head,
  CharacterJoints between them, on the Ignore Raycast layer so the game aiming looks through it.
  Each frame after the animation the bones follow the capsules; at the end the body eases back
  in 0.5 s, or stops at once when the character is moved more than 3 m (put on a pole). A
  fainted player is held still (speedFac 0), sees themselves from behind (SelfView) and gets a
  big "You fainted!" with a countdown; how-to card "faint" the first time. Darts fly through
  dolls. The RPG still only hits players.
- Self-test: the dance test now ends by knocking out a bot and the player (pictures 31 to 35,
  report lines "knocked out ...", "fainted", "after the faint").
- Version 0.14.0 in package/mod.json. Not run yet: the game was open on the PC when it was
  built, and it is never closed without asking.

### Teacher bug hunt (same evening, 0.14.0, NOT run in the game yet: Gabriel was playing)

Gabriel, playing with four friends: "the other teachers rlly buggin", "remove the caught UI 0/1
at the top", "I can't throw pizza and potato and pot doesn't work", "go crazy on bug fix". His
game's log of that session had no mod errors; it showed the janitor's mop working three times,
Marcel's pizza thrown twice (both missed), the lunch lady's pot put "on the floor by the
blackboard", and no mashed potatoes at all.

Facts found in the current game's code (decompiled again into %TEMP%\claude_wce_full; the
C:\wce\src copy is from an older version):
- The game drains a teacher's stamina all through class (SkillComponent, rate from
  StudentNumBalanceConfig by class size). Writing on the blackboard sets
  SkillComponent.PauseAutoEnergyConsume and refills at the board's rate. The mod's pot, chair
  and booth only added 5 a second on top of the drain, so they could do nothing at all.
- TeacherStunComponent has IsStunned, a private `slipping` flag and `remaining`; a slip or stun
  makes the host release a carried student. CatchComponent.catchedUnitUID is who is carried.
- The "Caught 0/1" line is UIBattleMainView.caughtText, written in OnStudentGetCaught.
- A player gets a new unit id when they become the teacher; the mod already sends the look again.

Fixed:
- Stamina (Teachers.TickStamina, one place): at the lunch lady's pot, the lifeguard chair and the
  DJ booth the game's drain stops and stamina fills at least stationStaminaPerSecond (10) or the
  blackboard's own rate if faster. Anywhere in the swim and dance halls only hallDrainShare (half)
  of the drain applies. The drain is always let go again when leaving or when class ends.
- Pizza and mashed potatoes: a student near the aim line counts as hit (about 0.35 m off close
  by, 1 m far away, nothing solid in between); the food flies 1.7 times bigger; the thrower is
  told "hit X" or "missed"; the host logs why a throw was refused and the thrower's own game logs
  why the key did nothing (lines "teachers: ..."). The real reason the potatoes never flew is
  still unknown: the code path looks right, so the next log will say.
- The pot's message says where it really is; the lunch lady's hint line says how far away it is.
  No pot in the swim and dance classes (it stood in the empty classroom).
- The whole-body teachers (Looks.PlaceBody): placed late in the frame (no lag), the body is kept
  when the game refreshes it (no restart, no size jump), the standing height is measured only in
  the first two seconds, walk and run don't flick (room between the switch points) and play as
  fast as the teacher moves (the Meshy walk carries 0.27 body heights a second, the run 1.8), the
  body tips over flat while knocked out or slipping, wobbles while dizzy, holds both arms out
  (bones 9 to 11 and 13 to 15, found from the rest pose) while carrying a student, and sways on
  the beat when dancing.
- Hats, held tricks and nerf guns are placed late in the frame too (they shook while walking).
  Each late step has its own error guard.
- The wig lands where the teacher can walk to it, and goes back on by itself after 30 s.
- The "Caught 0/1" count is hidden (setting hideCaughtCount).
- A fainted teacher gets the game's own slip held for the whole 5 s (drops a carried student).
- Self-test: as gym teacher, lunch lady or janitor it now notes the stamina at the chair or booth
  and uses the power at the nearest bot (report lines "teacher power ...", pictures 19b and 24).

## Pack 0.15.0 (Gabriel's list of ten, 2026-11-15 late evening; built, NOT installed or tested)

His list: stairs at the pool; fat or thin bodies; elevator buttons for the new levels; new
hairstyles; weapons ragdoll you for some seconds instead of the pole, the teacher can grab you, and
the teacher can be shot; make everything better; teachers adjustable too but not so big they can't
see; the body resets each new game (keep it) and customise only in the lobby; a phone item from the
drawers (7.4%) held in the hand, raised when used, gone after one use, wardrobe on O in the break,
the teacher hears a writing sound; hang students out of the windows with only the fingers showing.
His answers: "Phone, panel, 3 hairstyles" (picked mohawk and spiky, so 84 credits, 39 left), the
elevator for anyone, and on the reset "it does it we want it removed".

- Meshy (recipe as before; tasks in meshy/tasks.md): hair_mohawk, hair_spiky, phone,
  elevator_panel. tools/convert_meshy.py got --head (a 0.11 m ball in the preview) and --view.
- Knockout (Faint.cs): no pole; students and bots 4 s, the teacher 5 s; the doll falls nearly on
  the spot and ends the moment the student is caught. The game lets a teacher catch a stunned
  student (it only checks that the teacher is not stunned).
- Phones.cs (new): host keeps who has a phone; DoorDash (CpOrder), prank calls and tricks
  (TrickHold, NoteAsk, NapAsk) ask Phones.HostUse before going ahead, which uses the phone up.
  Sounds: sfx_StudentPhone_sfx when raised, sfx_WriteTest_sfx every 1.1 s while up.
  New messages PhoneAsk 79 and PhoneTell 80. The F6 test toggle gives a gun and a phone per snack.
- Wardrobe: key O (rulesVersion 3 moves a saved F7 to O), never in class; the phone's wardrobe
  app is gone. The phone screen only opens in class with a phone in the hand.
- Looks.KeepGamePicks saves the game's first face, body, hair and clothes as the player's own;
  StudentParts no longer lets floors 6 and 8 put on their set outfit.
- Window hanging (WindowPoles.Watch, SwimPose mode 4): every game finds poles within 1.1 m of a
  window (WindowComponent.Config.rendererList is the glass); a student bound to one is drawn just
  outside the wall, facing in, hands over the glass's lower edge; the pole is hidden meanwhile.
- Swim climbing: ladders at (3.5, 0, +-4.5) from the water, the board's steps from the deck; the
  pick-up key or B asks the host (ClimbAsk 81), which teleports. The hidden slope up the board is
  30 degrees now (was 45, probably too steep to walk).
- Elevator.cs (new): every game puts elevator_panel on the blackboard's wall beside the board,
  buttons blue pool, pink dance hall, yellow classroom, TextMesh labels; anyone presses one out of
  class (ElevatorAsk 82); the host sets Settings.nextClass and tells everybody (Elevator 83).
- Body slider: Look.BodyWidth in lookC bits 18 to 22; the body and clothes meshes are copied with
  the torso points (weights on gupen, spine1, spine2, pelvis) pushed out from the spine1 axis,
  together with the pupils in one shared copy per mesh and sizes. Teachers: head at most 1.25,
  eyes 1.4; the Meshy teachers scale bones 0 to 4 across and 5 to 7 for the head.
- How-to cards: "student15" and "swimpee2" (new ids so they show once more).
- Version 0.15.0.
