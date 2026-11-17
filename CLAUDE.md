# Class Pack, a mod for Worst Class Ever

Read `PROGRESS.md` before doing anything: it has the state of every idea, every version, what
was tested, what is still unchecked, and the facts found about the game. `meshy\tasks.md` has
how each 3D model was made. `README.md` has the setup for a new PC.

This file carries over what the assistant's own notes held on the first PC, so work can go on
from another one.

## The project in short

- The game: *Worst Class Ever* (Steam app 5175310, Unity 2022.3, Mono, Mirror networking). It
  has official mod support: a folder in `Mods` with a `mod.json` and a dll, an entry class
  implementing `IMod.OnLoad`, and Harmony for patching.
- The mod: "Class Pack" (id `classpack`). Code in `ClassPack\`, built by `.\build.ps1`.
  `.\build.ps1 -Install` needs the game closed, because the game locks the dll.
- The ideas come from the owner's Google Doc "worst class ever mod ideas" (38 ideas). They are
  built in packs, and he tests each pack before the next.
- Not built, as said to him: idea 8 (the stereotype student), idea 5, and the twerk emote in
  idea 9 (the dance class gets a goofy dance instead).
- After a start, check the game's log for `[Mods] Loaded classpack <version>`. Wait until the
  log file's time has changed, or you read the previous session's log.
- The host must never send a network message to itself: it is not delivered and nothing is
  logged. `Net.cs` routes host-to-self calls directly. When a feature silently does nothing
  for the room owner, look there first.
- Bump the version in `package\mod.json` with a plain text edit. PowerShell's
  `Set-Content -Encoding utf8` adds a byte-order mark that breaks the file.
- The GitHub repository is public (since 2026-11-17). Nothing private goes in it. Commits use
  the no-reply address `214180419+FireGB2012@users.noreply.github.com` (set it with
  `git config user.email` in the repository on each PC); his Gmail was taken out of the history.
- A release: `.\build.ps1 -Share` makes `share\ClassPack-<version>.zip` (the mod folder and
  `share-notes\HOW TO INSTALL.txt`, which must describe the version); then a GitHub release
  tagged `v<version>` with that zip. The first one is v0.15.0.
- In Git Bash, a heredoc holding an odd number of single quotes fails. Write the script to a
  file instead.

## How the owner wants to work

- Ask a yes/no question before each real change: closing or starting the game, installing,
  spending Meshy credits (say how many), pushing. Looking around needs no question.
- Describe a plan in plain gameplay terms. When an idea of his is short, say how it will play
  before building it. When he answers a question in his own words instead of picking an
  option, those words are the instruction.
- Put the full answer in the last message, after all tool calls, as short bullets. No wide
  tables.
- Build alone in the session: no workflows or subagents unless he asks. Commit each part and
  keep `PROGRESS.md` up to date, so a cut-off session loses nothing.
- Aim for a fast first look and a small scope, then improve from his screenshots.

## What he expects from the result

- Props are real 3D models made in Meshy, in the game's own toy style (the recipe is in
  `PROGRESS.md`, "Game-style Meshy recipe"). Shapes built in code read as not good enough.
  Show him pictures of new models before they go into the game.
- Things he picks up work with the game's own pick-up key and are big and obvious.
- A new feature gets an on-screen explanation the first time (the how-to cards in `Tips.cs`).
- A rare event he can't see reads as broken: show the trigger and give a way to force it.
- The mod's windows follow the look of the game's menus (`Skin.cs`).
- The game is first person. A player never sees their own body, so anything about looks has
  to be judged on other players, bots or the wardrobe's preview.

## Reading the game's code

The game's code is not in this repository. Decompile it on the PC you work on (see
`README.md`) into a folder that stays out of git, and search it there.
