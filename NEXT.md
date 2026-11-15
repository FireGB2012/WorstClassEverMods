# What to do next

Written 2026-11-15. The mod is at version 0.11.0 (a test build). Details and causes are in
`PROGRESS.md`, section "What the first two self-test runs showed".

## 1. Swim class: fix these first

- [ ] **Bots don't go into the water.** They can't walk any slope, so the ramp is a dead end
      for them. Give them their own way down at the ramp and send about half of them into
      the pool. (`Swim.cs`, `BringBots`)
- [ ] **The swimming move looks bad.** Replace it with a clear front crawl (big arm circles,
      kicking legs, head up) and treading water, on the game's real bones. The real hips bone
      is `gupen`. (`SwimPose.cs`)
- [ ] **Check the new lighting.** The hall was washed out by the sun; the colours are toned
      down in the code but nobody has looked at a picture of it yet.
- [ ] **Check DoorDash in the lounge.** The food should land on the coffee table. The test
      class ended before a picture was taken.
- [ ] **Run the swim class as a teacher**, once for each teacher type, and look at the pictures.

## 2. How to check any of it

Run the hands-free test (the game opens by itself for about two minutes, then closes):

```
.\tools\selftest.ps1
.\tools\selftest.ps1 -Role teacher -Kind 1
```

Then look at the sheets in `dist\autotest\student\` or `dist\autotest\teacher1\`.

## 3. After the swim class

- [ ] **Dance class** (idea 9): a hall with a dance floor; two students close together who both
      dance earn mischief; a goofy dance, not a twerk emote.
- [ ] **Pack 4, events:** coffee mug, snitch, hamster, food fight, random events, points shop,
      principal.
- [ ] **Toilet idea** (idea 10).
- [ ] **Other floors and modes** (ideas 12 to 18, prop hunt).

## 4. Still waiting for a first look from Gabriel

- [ ] The seven student tricks on the phone.
- [ ] Hat and glasses fit in the wardrobe.
- [ ] The gym teacher, lunch lady and janitor in a normal class (they were rebuilt at the right
      size on 2026-11-15).
- [ ] The lunch lady's pot on the teacher's stand.

## On another PC

`README.md` has the setup. Then `git pull`, and `.\build.ps1 -Install` with the game closed.
