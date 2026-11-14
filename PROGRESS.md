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
| 11 DoorDash on the computers | Built, compiles. Not yet tried in the game. |
| 6 (part) Teacher eats snacks from drawers for stamina | Not built |
| 38 (part) Power cut in the middle of class | Not built |
| 3 Bald male teacher | Moved to the models pack: his hair is part of his body model, so it can't just be hidden |

## Later

- Nerf guns (idea 1) that send whoever is hit up to a pole, with no rescue.
- Window as another place to hang caught students (idea 7).
- Everything that needs new 3D models: new floors, new teachers, new prank items.

## Not building

- Idea 8 as written, idea 5, and the twerk emote in idea 9 (dance class gets a goofy dance instead).
