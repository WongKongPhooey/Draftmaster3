# Master Draft

> **Being reworked (2026-11-09)** to the Draftmaster 2 model — collect cars, level them, series with restrictions, challenges that unlock cars. Brief and row plan: `Docs/MasterDraft-Rework.md`. This page describes the card mode until that lands.

The collectible-card mode, reached from the title screen's **MASTER DRAFT** row (between SINGLE RACE and
MULTIPLAYER). Collect drivers, cars, crew chiefs and action cards from packs, build a team, race it, and
play action cards from a hand during the race. Inspired by FIFA Ultimate Team (squad, packs, trading), the
Pokémon TCG app (pack opening, collection) and Balatro (a hand of cards played during a run).

Master Draft is meant to be an **online** mode, unlike the career, which plays offline. v1 is
**offline-playable with online built and switched off** (`MasterDraftOnline.Enabled = false`), so the
rules can be played and debugged before anything is exposed to a server.

Status as of 2026-10-31: phases 1-5 of the first build are done — rules, runtime services, menu scene,
race integration and this doc. **Nothing past EditMode tests has been run:** the menu screens and the race
HUD have not been play-tested (see "Play-test checklist").

---

## The player's route

```
TitleScreen ─MASTER DRAFT─▶ MasterDraft.unity ─ Home / Go Race / My Team / Packs / Trades
                                │  Esc/B from Home ──▶ TitleScreen
                                └─ Go Race ▸ GO ──▶ RaceScene (GameSession.Mode.MasterDraft)
                                                       └─ results ▸ Back to Master Draft ──▶ MasterDraft.unity
```

1. **First visit**: a free **Starter Pack** waits in Packs (Home shows an "Open Starter Pack" shortcut).
   Opening it gives 12 cards and **builds a team automatically** (`TeamRules.AutoBuild`), so GO RACE works
   at once. My Team can change it.
2. **My Team**: active driver + bench (up to 5 drivers), one car, one crew chief, an action deck of 5-15
   cards. Hints show the best team driver for each track type the venues cover.
3. **Go Race**: Quick Race only. Pick a track (every built venue, like SINGLE RACE), see each team driver's
   aptitude for that track's type (the best is tagged and preselected), pick one, GO. The **Online** tab is
   visible but says "Coming soon".
4. **The race**: one race, driven start to finish, exactly like SINGLE RACE (no paddock, no on-foot, no
   career writes). Every time the player crosses the start/finish line after the green, one action card is
   drawn into the hand. At the flag the results screen shows the coins earned and **Back to Master Draft**.
5. **Packs**: Standard Packs cost coins. **Trades**: swap spare cards with offers on a board (bots while
   offline).

---

## Rules and numbers

All the rules are pure C# in `Assets/Scripts/MasterDraft/Core/` (asmdef `Draftmaster.MasterDraft`, no scene
access, seeded RNG — `DraftRng`, splitmix64, so a seed gives the same pack everywhere).

### Cards

| Type | Built from | Stats on the card |
| --- | --- | --- |
| Driver | `Draftmaster.Data.Driver` rows (DatabaseManager; `DummyDrivers.Build()` when the DB isn't up). Retired drivers excluded | OVR = `CurrentAbility`; the five aptitudes ShortTracks / Speedways / Superspeedways / RoadCourses / DirtCourses (0-20); Qualifying, Consistency, Aggression, Awareness, TyreManagement |
| Car | 3 makes × 4 trims, generated (`CardFactory.Cars`) | Power, Handling, Draft, Reliability (0-100). Chevrolet leans power, Ford draft, Toyota handling. Trims Club / Pro / Elite / Champion = Common…Legendary |
| Crew Chief | `Staff` rows with `StaffRole.CrewChief`; 16 generated names when there are none | PitSpeed, Strategy, Setup (0-100), spread ±6 around the staff rating, fixed per name |
| Action | `ActionCatalog` | Effect, rules text, duration, magnitude |

Every card has a definition id (`DRV-12`, `CAR-FRD-2`, `CC-mike-hutchins`, `ACT-PushToPass`) and every
copy a per-profile uid (`c17`). A copy stores a **snapshot** of its definition, so a collection doesn't
change when the career database ages its drivers.

**Rarity from OVR** (all types): Legendary ≥ 90, Epic ≥ 82, Rare ≥ 72, else Common
(`CardFactory.RarityFor`). Cars use their trim's rarity.

**Card art hook**: drop a sprite at `Resources/MasterDraft/Cards/<artKey>` (`driver_<id>`,
`car_<MAKE>_<trim>`, `chief_<slug>`, `action_<Effect>`) and `MasterDraftCardView` draws it on the card.
Until then cards are procedural: rarity-coloured frame, OVR, type, name, two stat lines.

### Action cards

| Card | Rarity | Rules | Playable |
| --- | --- | --- | --- |
| Push To Pass | Common | +6% engine power for 8 s | **Yes** |
| Fuel Saver | Common | 40% less fuel burned for 60 s | **Yes** |
| Spotter's Eye | Common | Spotter calls every car alongside for a lap | No — needs a spotter-callout system |
| Slipstream | Rare | Draft strength ×2 for 12 s | **Yes** |
| Fresh Rubber | Rare | Restore half your tyre wear | **Yes** |
| Quick Repair | Epic | Fix 40% of bodywork damage | **Yes** |
| Perfect Pit Stop | Epic | Next pit stop serviced in half the time | **Yes** |
| Rocket Restart | Legendary | +15% power for 5 s at the next green | No — needs restart detection |

`ActionSpec.Implemented` is the gate: unplayable cards stay in the catalogue (visible as "Coming soon") but
**never drop from packs**, can't go in a deck, and `RaceHand.Play` refuses them. To finish one, implement
it in `MasterDraftRaceHost.Apply` (or `ActiveEffects` for a timed boost) and flip the flag.

### Packs

| Pack | Price | Cards | Guarantees |
| --- | --- | --- | --- |
| Starter | Free, **once per profile** | 12 | 1 Rare-or-nearest driver, 1 Common driver, 1 car, 1 crew chief, 5 action cards (distinct where possible — a legal deck on its own), 3 wildcards |
| Standard | 500 coins | 6 | Last slot Rare or better |

Ordinary slots: rarity weights Common 60 / Rare 28 / Epic 10 / Legendary 2; type weights Driver 30 / Car 15
/ Crew Chief 15 / Action 40. Feature slot: 0 / 75 / 20 / 5. Guarantees are built by filling the guaranteed
slots first — there is no re-roll loop. A pool with nothing at the wanted rarity falls back to the nearest
rarity. The seed for a profile's Nth pack is `Hash(playerId) ^ N × 0x5DEECE66D`, so a pack can be re-derived
and audited. Cards are revealed commons first, best card last.

**The pack opening screen** (`MasterDraftUI.PackOpening.cs`, to the design handoff's `PackSealed_PC`): a
full-screen layer — "Open Packs" header bar, the dark stage with diagonal stripes, the sealed pack floating over
its shadow (stepped 4 px bob) and a blinking "Click or press Enter". Confirm or a tap tears it open; the cards are
dealt face down showing a playing-card back (procedural: cream border, red lattice, the 3 medallion) and turn
over one at a time, left to right; an Epic or Legendary pull holds a beat and gets a call-out. Confirm, R or Esc
skips to the opened pack (the cards are already saved). The opened layout is a placeholder until the design
settles. EditMode coverage: `PackOpeningScreenTests`.

### Team

Active driver + bench ≤ 4 (5 drivers), exactly 1 car, 1 crew chief, action deck 5-15 with at most 2 copies of
one action, all owned, none used twice, no unplayable actions (`TeamRules.Validate` returns readable
reasons). **Team OVR** = driver 50% + car 25% + chief 25%.

### In the race

* **RaceHand**: deck shuffled at the green, **opening hand 1** (`RaceHand.OpeningHand`), **hand cap 5**,
  one draw per crossing; a full hand skips the draw (the card stays in the deck); played cards go to a
  discard pile that is shuffled back in when the deck runs out.
* **Cards are not consumed** by playing them — the deck is the same next race (Balatro-style). This is a
  tunable: to make cards consumable, remove the played uids from `profile.cards` in
  `MasterDraftRaceHost.PayOutAtTheFlag` (through the backend, so it is server-side when online).
* **Car card**: power, grip and draft each move by at most ±3% (stat 75 = stock, ±25 = full swing) —
  `PlayerVehicleController.cardPowerScale / cardGripScale / cardDraftScale`, all 1 outside Master Draft.
* **Crew chief**: pit-stop service time ±10% (PitSpeed), fuel burn ±5% (Strategy), tyre wear ±5% (Setup).
  Stat 50 = stock.
* **Driver card**: who the player races as — car number (and so livery, via `PlayerCarLivery`) and name.
  Its stats do not change the human's car; they choose which driver suits the track.
* Timed cards played again while running restart their clock; they don't stack.

### Economy

| | Coins |
| --- | --- |
| New profile | 500 |
| Standard Pack | 500 |
| Win | 500, −25 per place, minimum 50 (DNF = 50) |
| Per action card played | +10, paid for at most 10 cards |

### Trades

A trade offer gives one card and wants any card of a type at a minimum rarity. Offline, the board holds 6
generated **bot offers** (refilled when empty) so the flow works; listing your own card works too but
nobody answers until online. Cards in the team or already listed can't be traded.

---

## Online

`MasterDraftOnline.Enabled` (static readonly, `false`) is the one switch. While it is off, Home shows an
**OFFLINE / DEV** badge, the session uses `LocalMasterDraftBackend`, the Online race tab is "Coming soon"
and the trade board is bots.

Everything goes through `IMasterDraftBackend` (sign in, load/save profile, claim starter, buy and open a
pack, award a race, list/propose/accept/cancel trades) — intents in, results out, so the server can own the
outcome.

* **`LocalMasterDraftBackend`** — `Application.persistentDataPath/masterdraft.json`, written atomically
  after every change. **Separate from the career's `draftmaster.db`**; Master Draft never touches the career
  schema.
* **`UgsMasterDraftBackend`** — skeleton. Signs in for real (anonymous Unity Authentication; Core and
  Authentication come with `com.unity.services.multiplayer`, as `NetworkLauncher` uses them). Cloud Save,
  Economy and Cloud Code are **not installed**; their calls are TODO stubs inside `#if MASTERDRAFT_UGS` and,
  without that define, every call falls through to the local file and local rules.

### Before online launch

1. **Server-authoritative packs and trades.** Pack opening, starter claims, race payouts and trade swaps
   must run in **Cloud Code** using the same rules as `ProfileOps` / `PackGenerator` / `TradeRules` (port or
   share them), with the server choosing the seed. Today the client rolls its own packs — a modified
   client can print cards.
2. **UGS project setup** (dashboard, by hand): link the Unity project to a UGS project and environment;
   enable Authentication, Cloud Save, Cloud Code (and Economy if coins move there — a `COINS` currency);
   install `com.unity.services.cloudsave` and `com.unity.services.cloudcode` (check versions against the
   6000.3 pin); add `MASTERDRAFT_UGS` to the scripting define symbols; replace each TODO in
   `UgsMasterDraftBackend`.
3. **Cloud Save access classes**: cards and coins writable only by the server; the client writes only its
   team layout and display name.
4. **Anti-cheat**: race results are self-reported — cap and rate-limit payouts server-side; online races
   should report results from the host/server, not the player. Validate team legality server-side.
5. **Online racing** via the existing `NetworkLauncher` lobby (Netcode for GameObjects + Multiplayer
   Services), with each player's team applied by its owner and the hand kept per player.
6. **Real trade board**: other players' listings, escrow of listed cards, atomic swap between two players'
   data.

---

## File map

| Path | What |
| --- | --- |
| `Assets/Scripts/MasterDraft/Core/` (asmdef `Draftmaster.MasterDraft`) | Pure rules |
| `  CardModel.cs` | `CardType`, `Rarity`, `TrackKind` (mirrors `Draftmaster.Data.TrackType`), `ActionEffect`, `CardDefinition`, `CardInstance` |
| `  CardCatalog.cs` | `DriverSource` / `CrewChiefSource` DTOs, `CardCatalog` pools, `CardFactory` |
| `  ActionCatalog.cs` | Action specs + `Implemented` gate |
| `  PackGenerator.cs` | Starter / Standard packs |
| `  TeamRules.cs` | `MasterDraftTeam`, validation, rating, best driver, auto-build |
| `  RaceHand.cs` | The in-race hand |
| `  RaceEffects.cs` | `TeamEffects` (car / chief trims), `ActiveEffects` (timed cards, next-stop flag) |
| `  MasterDraftProfile.cs` | Profile + JSON (through `ProfileJson`, installed by the runtime), `Economy`, `DraftResult`, `ProfileOps` transactions |
| `  TradeRules.cs` | `TradeOffer`, bot offers, accept / propose / cancel |
| `  DraftRng.cs` | Seeded RNG + stable string hash |
| `Assets/Scripts/MasterDraft/Runtime/` (Assembly-CSharp) | Runtime |
| `  IMasterDraftBackend.cs`, `LocalMasterDraftBackend.cs`, `UgsMasterDraftBackend.cs`, `MasterDraftOnline.cs` | Backends + switch |
| `  MasterDraftSession.cs` | DontDestroyOnLoad singleton: backend, catalogue, profile, race hand-off |
| `  MasterDraftSources.cs` | DB rows → card sources |
| `  MasterDraftUI.cs`, `MasterDraftCardView.cs` | The menu scene's screens; procedural cards |
| `  MasterDraftUI.PackOpening.cs` | The pack opening screen (sealed pack, deal, flip) |
| `  MasterDraftRaceLauncher.cs` | GO → RaceScene |
| `  MasterDraftRaceHost.cs` | In-race hand, effects, HUD strip, payout |
| `Assets/Editor/MasterDraftSceneBuilder.cs` | Scene builder + profile menu items |
| `Assets/Scenes/MasterDraft.unity` | Camera + `MasterDraftUI`; nothing hand-authored |
| `  MasterDraftJsonInstaller.cs` | Installs JsonUtility into `ProfileJson` (Core is engine-free) |
| `Assets/Tests/Editor/MasterDraftRulesTests.cs` | Rules tests — also run headless by `Tools/HeadlessTests` (`dotnet test`, no Unity); `TitleScreenWiringTests.MasterDraftRowOpensTheMasterDraftMenu` |

**Core has no engine references** (`noEngineReferences: true`), so its rules compile and test with plain
`dotnet test Tools/HeadlessTests` — which is what lets overnight `code`-lane rows work on the rules in
parallel without the Editor (Docs/Overnight-Lanes.md). Keep it that way: an engine service the rules need
goes through a hook the runtime installs, the way the profile's JSON does.

Touched outside the mode: `GameSession` (`Mode.MasterDraft` appended, `IsMasterDraft`, `IsOneOffRace`;
`OnFootAllowed` = not a one-off race), `PlayerCarLivery` + `GridSpawner` (ask `IsOneOffRace`),
`PlayerVehicleController` (three `card*Scale` fields, 1 by default), `RaceDirector` (`PlayerFinishPosition`,
`ClassifiedCount`, Master Draft results branch), `PadBindings` (Play card / Next card), `TitleScreenRowEditor`
(row wired to LoadScene `MasterDraft`), `TitleScreen.unity` (that row), build settings (scene added at 7).

## Menu items

| Item | Does |
| --- | --- |
| `Draftmaster > Master Draft > Build Master Draft Scene` | (Re)writes `Assets/Scenes/MasterDraft.unity` and adds it to the build. Safe to re-run — the scene holds nothing hand-authored |
| `Draftmaster > Master Draft > Reveal Offline Profile File` | Shows `masterdraft.json` |
| `Draftmaster > Master Draft > Delete Offline Profile` | Wipes it — the next visit starts with a fresh Starter Pack |
| `Draftmaster > UI > Add MASTER DRAFT Row To Title Screen` | Wires the title row in place (LoadScene `MasterDraft`). Never rebuild the title screen for this |

## Controls

**Menu**: arrows / WASD / d-pad move to the nearest item; Enter / Space / E / A select; Esc / Backspace / B
back (overlay → screen → Home → title); Tab / Shift+Tab, 1-5 or LB/RB change screen; R / Y = Reveal All
while opening a pack, Auto Pick on My Team. Every item is tappable.

**Race**: `1`-`5` play a card; pad **View** steps the highlight, **A** plays it; on a phone, tap the card.
The strip sits bottom-centre with the deck count, running effects and the last draw above it. Hidden while
paused and once the results are up.

## Play-test checklist (not done yet)

* Menu: first visit → Starter → reveal → My Team edits → Go Race track/driver → GO, on keyboard, pad and phone.
* Race: a card is drawn at each crossing (not on the formation lap); each playable card does what it says;
  Perfect Pit Stop halves the next stop; the strip doesn't collide with the touch pedals or other HUD.
* Flag: coins paid once; Back to Master Draft shows the Race Result; the career's track/driver are
  unchanged on returning to CAREER.

## Open questions

* Should cards be consumable, or decks capped by "energy" per race, to give packs ongoing value?
* Driver card stats beyond identity — should they scale the human's car (it would make the card choice matter
  more but blur skill)? Today they only guide which driver to pick.
* Duplicates: quick-sell for coins? Upgrade by combining?
* Should a race's opening hand or the hand cap scale with race length?
* Series: Quick Race uses whatever series the career has selected; should Master Draft pick its own (Cup)?
