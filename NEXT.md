# What to do next

Written 2026-11-15, updated the same evening on the second PC. The mod is at version 0.12.0:
built, installed and self-tested there; committed but NOT pushed yet. Details are in
`PROGRESS.md`, section "Bots in the pool and a new swimming move".

## 1. Swim class

Done and seen in the self-test pictures:

- [x] **Bots go into the water** over a link at the ramp; about half of them swim.
- [x] **A real front crawl and treading water** on the game's own characters (students, bots,
      the game's teacher). The three new teachers keep their own Meshy stroke.
- [x] **The lighting** is no longer washed out.
- [x] **DoorDash in the lounge**: the food lands on the coffee table.
- [x] **The swim class as each of the five teacher types**: every run went through.
- [x] **Peeing in the pool** is the swim class's mischief (hold P in the water, +4, double next
      to the teacher, 30 s wait). Diving, splashing and diving under no longer pay unless the
      owner switches them on in F6.

Waiting for Gabriel:

- [ ] Play a real swim class and say how it feels: the swimming (bots, yourself from behind,
      the teacher) and whether peeing is balanced now. The numbers are sliders in the F6
      window ("Pee mischief", "Pee wait").
- [ ] A bot that stops in the pool treads water upright; with the very clear water that reads
      like standing in the pool. If it bothers him: less clear water, or bots that stop less
      in the pool.

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
The second PC has the game in `D:\SteamLibrary`; the build finds it there by itself.
