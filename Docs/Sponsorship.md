# Sponsorship

Sponsors are found at the track, signed by haggling, and only pay once their decal is on a panel of the
car. The AI field carries sponsors too, so a grid doesn't look like blank test mules.

The one rule everything hangs off: **a signed deal earns nothing until it is placed on the car.** Four
panels, more brands than panels, and every deal burning races whether or not it is painted on — that is
the whole decision the feature exists to create.

## The loop

1. **Meet a rep.** `SponsorRepSpawner` stands one or two sponsor reps in the pit lane on a race weekend.
   Who turns up is deterministic per (track, weekend), so the same faces are there all weekend and the
   next round brings different brands.
2. **Haggle.** `SponsorRepNPC` runs a normal on-foot conversation (`NPCInteractable` + `DialogueChoiceUI`).
   Accept, push for more, push hard, ask for a shorter/richer deal, or walk. They concede once, hold at
   their ceiling, and walk if pushed past it twice.
3. **Place it.** Back at the team garage, the Sponsorship Manager station opens `SponsorBoardPanel`: pick a
   deal, pick a panel. `F6` opens the same board anywhere, for testing at the track.
4. **Get paid.** `RaceDirector` banks the sponsorship money at the end of a race, alongside the prize
   purse, and reports it on the results screen. Every live deal then burns one race.

## Panels

| Slot | Pays | Where on the 64×32 livery (texture pixels, origin bottom-left) |
|---|---|---|
| Hood | 100% | `(8, 8, 16×16)` — the bonnet between the front arches |
| Tail | 70% | `(46, 12, 12×9)` — the decklid |
| Quarter left | 45% | `(44, 24, 12×7)` |
| Quarter right | 45% | `(44, 1, 12×7)` |

The cup26 art runs **nose at −X**: front bumper x0–6, hood x7–26, greenhouse x27–44 (the roof number),
decklid x45–58, rear bumper x59–63. Rects live in `Assets/Resources/Sponsors/cup26Layout.asset`
(`CarSponsorLayout`); a carset with a different body shape gets its own `Resources/Sponsors/<carset>Layout`.

**Draftmaster > Sponsors > Preview Slots On Livery** writes `Temp/SponsorSlotPreview.png` — the panels
drawn over a real livery, magnified — so rects can be nudged without entering play mode.

## Decal art

`Resources/Sponsors/Car/<key>.png`, where the key is the brand name slugged
(`"Voltage Energy"` → `voltage-energy`, see `SponsorCatalog.LogoKey`). Size them to the smallest panel
(12×7 today) and draw at the project pixel standard — decals are blitted **1:1 and never scaled**, because
resampling takes them off the 12.8 px/m grid. Oversized art is centred and clipped.

**Draftmaster > Sponsors > Generate Placeholder Decals** writes a coloured plate with the brand's initials
for every sponsor in the database. Overwrite any file with real art at the same name and nothing else
changes. **Preview Sponsored Car** bakes a car through the real runtime path to `Temp/SponsorCarPreview.png`.

## How the paint works

Cars are not sprites at runtime: each one becomes a subdivided, deformable mesh (`VehicleDamage`) whose UVs
come straight off its livery. So `SponsorLiveryBaker` composites the decals **into a copy of the livery
texture** and hands back a `Sprite`. The decal therefore dents with the panel it is painted on, needs no
second renderer or sorting order, and cannot drift as the car rotates — and the AI field gets sponsors
through the same call.

`SponsorPaintDirector` self-installs in any scene with cars: it polls briefly after load (the AI field
spawns several frames in, after the driver database opens), paints the player's car from `SponsorBook` and
every AI car from its car number, and repaints the player instantly whenever the book changes.

Liveries must be **Read/Write enabled** for this. `PixelSpriteImport` (Draftmaster > Art > Retarget World
Sprites to Pixel Standard) sets that for livery and sponsor textures only — a 64×32 paint costs 8KB, and
marking the whole 1500-sprite world library readable would be pointless.

## Money

Values are sized against the live wallet economy (`PlayerWallet`: $5,000 start, a race win pays $12,000),
not the six-figure numbers in the SQLite `Sponsors` table — those belong to the unbuilt team-budget
economy. A hood deal with a national brand lands around $5–6k a race, a small one around $2k, so a fully
sold car roughly doubles race-day income without replacing racing.

- Base per-race value: `300 + wealth² × 0.6` (`SponsorTerms.BaseValue`).
- Opening offer scales 0.75×–1.10× of that with how far the player's standing sits above the brand's
  `MinPrestige`; their ceiling is 1.15×–1.50×.
- Clause bonus (finish top 5/10/15, by brand prestige) is 60% of the per-race value, paid on races that
  meet it.

**Standing** is `FanAppeal.Value` (0–100) — the only live reputation the running game maintains. It is
compared directly against `Sponsor.MinPrestige`, so a brand the player is too small for still sends a rep,
who tells them the number they need.

## Code map

| File | What it is |
|---|---|
| `Assets/Scripts/Sponsors/` (`Draftmaster.Sponsors` asmdef) | Pure, testable: `SponsorSlot`, `SponsorDeal`, `SponsorBook`, `SponsorTerms`, `CarSponsorLayout`, `SponsorLiveryBaker` |
| `Assets/Scripts/Sponsorship/` | Game-coupled: `SponsorCatalog` (SQLite + weekend picks), `SponsorArt`, `SponsorPainter`, `SponsorPaintDirector` |
| `Assets/Scripts/OnFoot/SponsorRepNPC.cs`, `SponsorRepSpawner.cs` | The pit-lane negotiation |
| `Assets/Scripts/UI/SponsorBoardPanel.cs` | The garage board (and `F6`) |
| `Assets/Editor/SponsorArtTools.cs` | Placeholder art, layout asset, both previews |
| `Assets/Tests/Editor/SponsorTests.cs` | Payout, placement and haggling rules |

Signed deals persist in PlayerPrefs (`sponsors.book`) like the rest of the live career state
(`PlayerWallet`, `PlayerStatsLedger`, `FanAppeal`), not in the half-built SQLite career tables. The brand
catalogue itself is the seeded `Sponsors` table (`DummySponsors`).
