# What to do next

Written 2026-11-15, updated the same evening on the second PC. The mod is at version 0.13.0:
built, installed and self-tested there. Details are in `PROGRESS.md`, sections "Bots in the
pool and a new swimming move" and "Dance class".

## 0. Dance class (new in 0.13.0)

Done and seen in the self-test: the dance hall with the six Meshy props, lights and the game's
own party song; everyone always dances (Q, the chicken dance, shuffling slowly); the worm (J)
pays mischief while a student keeps it up; bots always dance; DoorDash on the snack table; the
teacher at the DJ booth.

Waiting for Gabriel:

- [ ] Play a dance class and say how it feels: the chicken dance, the worm, and whether the
      worm's mischief (+1 every 5 s, F6 "Worm pays" / "Worm every") is balanced.
- [ ] Ideas if he wants more: bots that do the worm now and then, the music as a choice in F6.

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

- [x] **Dance class** (idea 9): built in 0.13.0, see section 0.
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
