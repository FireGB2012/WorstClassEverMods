# Class Pack: a mod for Worst Class Ever

A gameplay mod for the Steam game *Worst Class Ever* (teacher against students). It adds a
swim class in a pool hall, a phone with DoorDash, prank calls and student tricks, nerf guns,
a wardrobe, three new teachers and more. `PROGRESS.md` has the full story: what each version
added, what was tested and what is still open.

## Setting up on another PC

You need:

1. **The game**, installed through Steam. The build expects it at
   `C:\Program Files (x86)\Steam\steamapps\common\Worst Class Ever`. If your Steam library is
   somewhere else, change the `$game` line near the top of `build.ps1`.
2. **The .NET SDK** (version 6 or newer). The mod is compiled with the SDK's own C# compiler
   straight against the game's dlls; no packages are downloaded.

   ```
   winget install Microsoft.DotNet.SDK.8
   ```

Then, in this folder, with the game closed:

```
.\build.ps1 -Install
```

That builds the mod and copies it into the game's `Mods\classpack` folder. Start the game,
open the mod menu, switch **Class Pack** on and restart the game once. (A new mod is off until
you switch it on, and every PC has to do this once.)

Other build commands: `.\build.ps1` builds only (into `dist\classpack`), and
`.\build.ps1 -Share` makes a zip in `share\` to give to friends.

Only needed when you make or change 3D models:

- **Blender 5.2** at `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`
- **Python 3** with Pillow (`pip install pillow`)
- **Meshy** (the Meshy connector in Claude), for new models. The models already made are in
  `meshy\` and, converted, in `package\models\`.

## What is where

| Folder | What |
|--------|------|
| `ClassPack\` | The mod's code (C#) |
| `package\` | What ships with the dll: `mod.json` and `models\` |
| `tools\` | Scripts that make and convert models (run with Blender or Python) |
| `meshy\` | The original models from Meshy, their pictures, and `tasks.md` with how each was made |
| `share-notes\` | The install note that goes into the zip for friends |
| `dist\`, `share\` | Build output, not kept in git |

## Things that are per PC and not in this repository

- **Your settings** (keys, which class is next, the wardrobe outfit):
  `%USERPROFILE%\AppData\LocalLow\Funcore Games\Worst Class Ever\Mods\Config\classpack\config.json`.
  Copy that file over if you want the same settings; otherwise the mod makes a fresh one.
- **The game's log**, where the mod writes what it does (lines start with `[Mod:classpack]`):
  `%USERPROFILE%\AppData\LocalLow\Funcore Games\Worst Class Ever\Player.log`
- **The game's own code**, which the mod was written against. It is not in this repository
  because it belongs to the game. To read it again, decompile
  `Worst Class Ever_Data\Managed\Assembly-CSharp.dll` from the game's folder:

  ```
  dotnet tool install ilspycmd --version 8.0.0.7345 --tool-path .\ilspy
  .\ilspy\ilspycmd.exe -p -o .\game-src "C:\Program Files (x86)\Steam\steamapps\common\Worst Class Ever\Worst Class Ever_Data\Managed\Assembly-CSharp.dll"
  ```

  Keep `ilspy\` and `game-src\` out of git. The game's data tables can be read with
  `tools\unbundle.py` (see the top of that file).

## Working on it with Claude

Open this folder in Claude Code. `CLAUDE.md` tells Claude how the project works and how to
work on it, and `PROGRESS.md` holds everything that has been built and found out so far.
