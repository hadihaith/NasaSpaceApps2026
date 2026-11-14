# Cutscene scripts: Operation: The Great Homecoming

Owner: Member 6 (Hafiz). Status: v2, matched to the game's discoveries.

## Rules of the alien universe

- **The aliens love humans, not Earth.** They want humans safe and happy, just somewhere else.
- **Worse conditions on Earth = progress.** They celebrate drought and heat like a marketing win.
- **Better conditions on Earth = a setback.** They panic when Earth gets more comfortable.
- **Real NASA data = intelligence gathering.** It is public data, which they find thrilling.
- **The scientist is always right.** Dr. Nyxen states the science correctly. The comedy comes from the
  Commander's interpretation, never from wrong science.
- **Everything the player changes happens in a simulation.** Real Earth is never touched.

## Cast (names used in the game, `src/homecoming/Crew.tsx`)

| Script role | Name | Model |
| --- | --- | --- |
| Commander | **Commander Zorvax** | `characters/commander-zorvax/commander-zorvax.blend` |
| Scientist | **Dr. Nyxen** | `characters/dr-nyxen/dr-nyxen.blend` |
| Cadet | **Cadet B** | `characters/alien-student/alien-student.blend` |

Earlier drafts used Zorp, Zib and Bloop. Those names are retired.

## Scene list

| # | Title | Plays when (game discovery id in `src/homecoming/model.ts`) | Length | Status |
| --- | --- | --- | --- | --- |
| 1 | The Welcome Baskets | `drought` discovered | 14 s | **In Unity, needs testing** |
| 2 | Excellent News | `demand` discovered | 10 s | Script |
| 3 | The Recruitment Campaign | `heat` discovered | 10 s | Script |
| 4 | The Unfortunate Recovery | `recharge` or `recovery` discovered | 10 s | Script |
| 5 | Budget Cuts | `cool` discovered | 10 s | Script |
| 6 | The Spy Satellite | first Earth Archive investigation | 10 s | **Draft video** (`artifacts/cutscenes/spy-satellite/`) |
| 7 | The Evil Master Plan | first two-variable comparison in the Archive | 10 s | Script |
| 8 | The Cover-Up | an Archive result of "significance not established" | 10 s | Script |
| 9 | The Progress Report | 3-discovery milestone | 12–15 s | Script |
| 10 | Phase Two | final report (replaces the current `ENDING`) | 20–25 s | Script |

The discoveries `decline`, `fire`, `humidity`, `greening`, `waterlogging` and `surplus` keep their
one-line quotes from `model.ts` and have no cutscene.

## Scripts

### 1. The Welcome Baskets (`drought`)

*A red alert flashes: SOIL MOISTURE DECLINING.*

> **NYXEN:** Commander, the soil is drying out.
> **ZORVAX:** Wonderful! Prepare the human welcome baskets!
> **CADET B:** How many humans are coming?
> **ZORVAX:** None yet.
> **CADET B:** So… how many baskets?
> **ZORVAX:** ALL OF THEM!

*A mountain of baskets collapses on Cadet B.*

### 2. Excellent News (`demand`)

*Nyxen displays a graph.*

> **NYXEN:** Evaporative demand is up. The air is pulling more water from soil and plants.
> **ZORVAX:** Meaning?
> **NYXEN:** The air is thirstier.
> **ZORVAX:** Excellent. Humans hate being thirsty!
> **CADET B:** Sir, humans can drink water.

*Zorvax stares at Cadet B.*

> **ZORVAX:** Stop helping Earth.

### 3. The Recruitment Campaign (`heat`)

*A giant holographic thermometer flashes.*

> **NYXEN:** Another month hotter than this region's normal.
> **ZORVAX:** PERFECT! Launch the campaign!

*A giant advert appears: TIRED OF EARTH? WE HAVE SHADE!*

> **CADET B:** Sir, our planet has three suns.

*Long silence.*

> **ZORVAX:** …Don't mention the suns. And send the humans sunscreen. They must arrive in good condition.

Changed from "above forty degrees": the game flags heat relative to the region's normal. The sunscreen
line keeps "humans are precious".

### 4. The Unfortunate Recovery (`recharge` / `recovery`)

*A green notice appears: SOIL WATER RECOVERING.*

> **NYXEN:** Commander, the rain has refilled the soil. Plants can reach water again.
> **ZORVAX:** RECOVERING?!
> **CADET B:** The humans might stay!

*Zorvax slams the emergency button.*

> **ZORVAX:** CANCEL THE WELCOME CAKE!

*A cake slides silently into a trash chute.*

Changed from "soggier soil… improved": very wet soil (waterlogging) is not an improvement, so this
scene belongs to recharge and recovery only.

### 5. Budget Cuts (`cool`)

*Nyxen studies the model results.*

> **NYXEN:** Cooler than usual. Growth is slowing down.
> **ZORVAX:** So is Earth getting worse or better?
> **NYXEN:** That measurement alone doesn't tell us.
> **ZORVAX:** So we can't celebrate OR panic?
> **NYXEN:** Correct.
> **ZORVAX:** I hate science.

*Nyxen silently hands him another graph.*

The game has no "lower evaporative demand" discovery, so the joke moves to `cool`. The lesson stays: one
measurement does not show that the whole environment changed.

### 6. The Spy Satellite (first Archive investigation)

*A satellite view of Earth appears.*

> **ZORVAX:** Finally! Intelligence from the enemy planet!
> **NYXEN:** It's publicly available NASA data.
> **ZORVAX:** So we've infiltrated NASA?
> **NYXEN:** No. We opened their website.

*Zorvax slowly puts on sunglasses.*

> **ZORVAX:** Outstanding espionage.

### 7. The Evil Master Plan (two-variable comparison)

*Two graphs appear side by side.*

> **ZORVAX:** Look! They move together! We can control BOTH!
> **NYXEN:** Correlation doesn't establish causation.
> **ZORVAX:** But they move together!
> **NYXEN:** That doesn't mean one controls the other.

*Zorvax turns to Cadet B.*

> **ZORVAX:** Cancel the double-control machine.
> **CADET B:** We already ordered twelve.

### 8. The Cover-Up ("significance not established")

*A report flashes: TREND NOT ESTABLISHED.*

> **NYXEN:** Commander, the evidence doesn't support our theory.
> **ZORVAX:** Delete the evidence.
> **NYXEN:** No.
> **ZORVAX:** Delete the theory?
> **NYXEN:** Revise it.

*Zorvax looks horrified.*

> **ZORVAX:** That's even MORE paperwork!

### 9. The Progress Report (3 discoveries)

*Zorvax addresses the Galactic Council (heard off-screen, so no new character model is needed).*

> **ZORVAX:** Distinguished members, Earth is becoming increasingly uncomfortable in our simulations!
> **COUNCIL (off-screen):** Excellent. How many humans have relocated?
> **ZORVAX:** Zero.
> **COUNCIL:** How many have agreed to relocate?
> **ZORVAX:** Also zero.

*Awkward silence.*

> **ZORVAX:** But our graphs are BEAUTIFUL!

*Nyxen quietly applauds.*

### 10. Phase Two (final report, replaces the current ending)

*The crew stands before a giant hologram of Earth.*

> **ZORVAX:** We've tried drought. Heat. Everything.
> **NYXEN:** In simulations, Commander.
> **ZORVAX:** And the humans still won't come!
> **CADET B:** Maybe they really love their planet.

*Zorvax looks at Earth for a long moment.*

> **ZORVAX:** …Then we'll just have to make OUR planet even better.
> **CADET B:** So we cancel the operation?
> **ZORVAX:** Absolutely not.

*He slams the red button. A giant banner unrolls: FREE WIFI. UNLIMITED SNACKS. HUMANS WELCOME.*

> **ZORVAX:** Operation Great Homecoming… PHASE TWO!

*The crew cheers.* On screen: **BETTER TOGETHER. LOVE THY HUMAN.**

## Production notes

- **Cutscenes are built in the Unity project** (`unity/Homecoming`), with Ibrahim's rigs and
  animations in Hadi's set. No AI video. See "Discovery cutscenes" in `unity/README.md`.
- Character references come from renders of Ibrahim's `.blend` files, not the 2D concept sheets, so
  the videos match the in-game 3D models.
- Videos are generated with Higgsfield (keyframes with GPT Image 2.6, animation with Kling 3.0),
  about 20–25 credits per 10-second scene.
- For the game, export each scene at 720p and keep it to about 2–3 MB. Add subtitles from this file;
  AI voices may not say the lines word for word.
- Optional stretch idea: a fictional "Galactic Relocation Forecast" meter, clearly labelled as a
  game score and not a prediction. Needs Members 1 and 2.
