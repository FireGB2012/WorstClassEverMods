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
- **Testing switch:** the F6 owner window has "Every snack eaten gives a nerf gun".
- Only desks with a normal drawer count, so no student guns on the restroom, dormitory or
  schoolyard floors yet.

Things to check: where the guns and the alarm box show up, whether B, X and H clash with the
game's own keys, whether block shapes are drawn in colour, and whether window poles appear at all
(the log says how many windows were usable).

## Packs still to do

- **Pack 3, student tricks:** sleep in class (30), pass notes (31), laser pointer (25), whoopee
  cushion (23), paper airplane (24), water balloon (27), super glue (26).
- **Pack 4, teacher and class events:** coffee mug and hot sauce (28), teacher's pet snitch (36),
  class hamster (33), food fight (35), more random events (38), points shop at the vending
  machine (37), the principal (21).
- **Pack 5, looks:** customizable characters (2), bald teacher and wig (3, 22), the toilet idea (10).
- **Pack 6, new teachers:** gym teacher (6), lunch lady (19), janitor (20). They reuse the current
  teacher's body with a new look and new powers; a brand new body needs a 3D model.
- **Pack 7, new floors and modes, the slow part:** pool (4), dance class (9), science lab (12),
  gym (13), art room (14), music room (15), library (16), school bus (17), teachers lounge (18),
  prop hunt mode (32). Each floor needs a room built and models made.

## Not building

- Idea 8 as written, idea 5, and the twerk emote in idea 9 (dance class gets a goofy dance instead).
