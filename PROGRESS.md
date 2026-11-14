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

## Later

- Nerf guns (idea 1) that send whoever is hit up to a pole, with no rescue.
- Window as another place to hang caught students (idea 7).
- Everything that needs new 3D models: new floors, new teachers, new prank items.

## Not building

- Idea 8 as written, idea 5, and the twerk emote in idea 9 (dance class gets a goofy dance instead).
