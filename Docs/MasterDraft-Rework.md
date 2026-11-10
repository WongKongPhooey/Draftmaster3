# Master Draft rework — back to the Draftmaster 2 model

Master Draft (title row MASTER DRAFT → `Assets/Scenes/MasterDraft.unity`) is moving off its card format (packs, a team of
driver / car / crew chief / action cards, trades, an in-race hand — `Docs/Master-Draft.md`) to the structure Draftmaster 2
(Rolling Thunder) had: **collect cars, level them up, race them in series with entry restrictions, and take on
challenges that unlock cars you can't get any other way.** This file is the brief the overnight rows work from.

## What Draftmaster 2 had, and where it still is

The DM2 game layer was deleted on 2026-10-05 (commit `2217821d`, "Remove Draftmaster 2 scenes, prefabs and dead
scripts") but **its data survives** in `Assets/Scripts/` (Assembly-CSharp, global namespace):

| File | What it holds |
|---|---|
| `DriverNames.cs` (2.8k lines) | Every carset (series prefix such as `cup22`) and every car in it by number: driver name, team, manufacturer (`CHV` / `FRD` / `TYT`…), rarity 1–4 (stars), driver type (`Rookie`, `Strategist`, `Closer`, `Intimidator`, `Blocker`, `Dominator`, `Legend`), number placement. `allWinnableCarsets`, `getName/getRarity/getType`. |
| `GameData.cs` | Levelling numbers: `classMax(class)` gears to fill a class (0→10, 1→20, 2→35, 3→50, 4→70, 5→100, 6→150), `unlockGears`, `upgradeCost(class)` (0, 10k, 50k, 100k, 250k, 500k…). |
| `SeriesData.cs` | Series menus → sub-series (`offlineSeries[menu, sub]`, e.g. "Take The Wheel", "Find The Draft"…) with **restrictions** (`offlineMinLevel`, `offlineMinClass`, `offlineMinRarity`, `offlineMinType` = Team / Manufacturer / Car / Type / Rarity with `offlineMinTeam`, `offlineMinManu`, `offlineExactCar`, `offlineMinDriverType`), AI level, tracklist, fuel, prizes. |
| `EventData.cs` | **Challenges**: events ("Ol' Seven-Time", "The Intimidator", "The Closer"…) of chapters with the same restriction fields, the weeks they run (`offlineEventWeek`), AI level, tracklist, laps, *moments* (start at lap N, custom field order), and prizes — a named driver's car (`"Johnson"`), a category (`Rookies`, `Rarity1`–`4`, `1Star`…, team or manufacturer) via `ListRewards`, set prizes. |
| `UI/GarageUI.cs` | The DM2 garage screen (legacy uGUI): shows the gears/class model in use — PlayerPrefs `{prefix}{n}Gears`, `{prefix}{n}Class`, `{prefix}{n}Unlocked`. |

Deleted but readable with `git show 2217821d^:<path>`: `Assets/Scripts/PrizeCollection.cs` (how a prize unlocks a car or,
for a car you own, adds gears; a new car starts with 10 gears), `UI/SeriesUI.cs` + `SeriesUIFunctions.cs`
(championship rounds: PlayerPrefs `SeriesChampionship{id}{sub}Round/Length/CarChoice`, difficulty and lap options),
`UI/EventsUI.cs`, `UI/ChallengesUI.cs`, `UI/StoreUI.cs`, `AllCars.cs`.

**Read these as reference; don't bring the old code back.** Port the rules into Master Draft's own Core and the data into
JSON, the way the rest of the project has moved.

## Where it goes

- **Rules: `Assets/Scripts/MasterDraft/Core/` (asmdef `Draftmaster.MasterDraft`, `noEngineReferences: true`).** Pure C#,
  no UnityEngine. JSON is parsed through the existing `ProfileJson.Read(json, type)` hook (installed as JsonUtility in the
  game, System.Text.Json in headless tests), so Core can load its own data files without an engine reference.
- **Data: `Assets/Resources/MasterDraft/*.json`** — `Cars.json`, `Series.json`, `Challenges.json`, generated from the DM2
  files by `Tools/masterdraft_export.py` (stdlib Python, parses the C#) so it can be re-run.
- **Tests: `Assets/Tests/Editor/MasterDraft<Thing>Tests.cs`.** Anything named `MasterDraft*Tests.cs` is compiled into
  `Tools/HeadlessTests` automatically (wildcard), so keep those test files engine-free — they run under `dotnet test`
  as an overnight gate. A test that truly needs Unity goes in a file with another name.
- **Runtime/UI: `Assets/Scripts/MasterDraft/Runtime/`** — `MasterDraftUI` (runtime-built screens, like SingleRaceUI),
  `MasterDraftSession`, `MasterDraftRaceLauncher` → `RaceScene` in `GameSession.Mode.MasterDraft` (`IsOneOffRace`).

## Decisions already made (rows shouldn't re-open them)

- **A car** = carset + number, id `"{prefix}{number}"` (e.g. `cup22_26` is fine as a JSON id if the prefix ends in a digit;
  pick one scheme and keep it). Livery sprite = `Resources/{prefix}livery{number}.png`. Only carsets whose livery sprites
  exist in `Assets/Resources` go in the catalogue.
- **Levelling** = DM2's class 0–6 and gears, numbers from `GameData` (`classMax`, `upgradeCost`). Duplicate prizes
  become gears. Class gives a **small** performance step through the existing `PlayerVehicleController.cardPowerScale /
  cardGripScale` hooks — one tunable table in Core (start: +1% power and +0.5% grip per class above 0).
- **Restrictions** are one engine shared by series and challenges: min player level, min class, min rarity, and one
  "type" rule (team / manufacturer / exact car / driver type / rarity). It answers *eligible or not, and why* (for the UI).
- **Tracks**: DM2 tracklists name DM2 circuits; map each to a real catalogue id (`TrackCatalog`, e.g. `Daytona`). DM2's
  fictional ovals (Super Tri-Oval, Mute City…) map to the nearest real track of the same type; the mapping table lives in
  the export script, documented.
- **Profile**: stays `persistentDataPath/masterdraft.json` (never the career DB). Schema 1 (cards) → 2 (cars) migrates:
  nobody loses a save silently — card collections convert to a starter car plus coins, and the old file is kept as
  `masterdraft.v1.json`.
- **The card mode retires last**, after the new screens work: packs, action cards, trades and the in-race hand go out of
  the menu flow; delete code only when nothing references it.

## Josh's calls after rows 1-5 (2026-11-10)

- **"1Star" prizes are one-star cars only** (DM2 fell through to "any car"; not ported).
- **Settled:** one currency — card coins and race money share `coins`, on DM2's scale (10,000 for a win); card trade-in
  values 250 / 1,000 / 2,500 / 5,000 (`Economy`); a champion always gets a prize car; DM2's level-up rewards are not
  ported; an unreadable save is kept as `masterdraft.unreadable.json`.
- **Pending — don't change yet:** the "Earnhardt" prize (exported as `cup20_3`, A.Dillon's #3 in the catalogue) and
  challenge prizes in general are being reworked. Josh will send instructions; a row will follow.

## How the overnight rows split it

Two worktree slots run `code` rows in parallel; `unity` rows run one at a time in the main checkout. Rows with no After
between them must not edit the same file, so each row owns its files:

1. **code** — car catalogue: `Tools/masterdraft_export.py` (cars part), `Resources/MasterDraft/Cars.json`, Core `CarCatalog`
   (new file, e.g. `Core/Garage/CarCatalog.cs`), `MasterDraftCarCatalogTests.cs`.
2. **code** — levelling rules: Core `CarProgression.cs` (+ `OwnedCar`), `MasterDraftProgressionTests.cs`. Parallel with 1.
3. **code** — series: export (series part), `Series.json`, Core `Restrictions.cs` + `SeriesRules.cs` (championships),
   `MasterDraftSeriesTests.cs`. After 1, 2.
4. **code** — challenges: export (challenges part), `Challenges.json`, Core `Challenges.cs` (weeks, eligibility, prize
   resolution), `MasterDraftChallengeTests.cs`. After 3.
5. **code** — profile v2 + rewards: `MasterDraftProfile` schema 2 + migration, `RaceRewards` (money, XP, gears to the car
   raced, completion prizes), `MasterDraftProfileTests.cs`. After 2, 3, 4.
6. **unity** — Garage screen in `MasterDraftUI`. After 5.
7. **unity** — Series screen + race hookup (launcher, track, AI level, laps, livery, class performance step, results →
   rewards, championship progress). After 6.
8. **unity** — Challenges screen (+ moments where RaceScene supports them). After 7.
9. **code** — retire the card mode from the flow and rewrite `Docs/Master-Draft.md` for the new system. After 8.

Only row 9 edits `Docs/Master-Draft.md`; earlier rows note what they did in their commit messages.
