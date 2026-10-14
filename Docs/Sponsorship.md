# Sponsorship

Sponsors are found at the track, signed by haggling, and only pay once their decal is on a panel of the
car. The AI field carries sponsors too, so a grid doesn't look like blank test mules.

The one rule everything hangs off: **a signed deal earns nothing until it is placed on the car.** Four
panels, more brands than panels, and every deal burning races whether or not it is painted on — that is
the whole decision the feature exists to create.

## Team HQ paint shop

The player repaints the car and chooses its sponsors at **Team HQ**. On the travel map, stop at Team HQ
and press **GO INSIDE TEAM HQ**: that loads `TeamGarage` (`LandmarkLoader.VisitTeamHQ`), and its exit door
takes you back on the road with the map open (`LandmarkLoader.CameFromRoad` → `ExitToRoad`).

Talk to the **Paint Scheme Designer** (`PaintShopNPC`, under the `PaintShop` root in TeamGarage) to open
`PaintShopPanel`:

- **Base coat**: 16 swatches (`PaintShop.Palette`).
- **Livery**: a pattern from `Resources/PaintShop/Patterns/` (plain, roof, stripe, viper), plus its colour.
- **Sponsors**: PRIMARY (hood, 100%), SECONDARY (both rear quarters, 90%) and TRUNK (decklid, 70%),
  cycled through your active deals. One brand takes one slot.
- **Factory paint** puts back the livery exactly as drawn. Nothing changes until **SAVE**, which writes
  `PaintBook` (`paint.scheme`) and calls `SponsorBook.Arrange`.

`TeamCarDisplay` on the shop's `TeamCar` and `SponsorPaintDirector` on the race car both repaint
whenever the paint or the sponsors change.

**Repaintable liveries** are split into layers under `Resources/PaintShop/`:

| File | What it holds |
|---|---|
| `<livery>_mask.png` | Paintable body. Alpha marks what can be painted; grey sets shading (255 = full colour, darker = shaded rim). |
| `<livery>_detail.png` | Everything that isn't paint (glass, tyres, lights). Drawn on top. No number. |
| `<livery>_blank.png` | The livery as drawn with its number painted out — what FACTORY PAINT shows. |

Order: base coat × shading (or the `_blank` art for factory paint) → pattern in the accent colour → detail →
car number → sponsor decals (`LiveryCompositor`, then `SponsorLiveryBaker`). The number is never in the
layers: `PaintShop` stamps `Resources/cup20num<n>` (n read off the livery name) centred on the roof, tinted
by the scheme's NUMBER COLOUR (multiply — white digits take the colour, the dark outline stays dark). The
number sprites aren't Read/Write, so their pixels are copied through the GPU once. The base
`Resources/cup26livery89.png` still has its number drawn in for places that show the raw art (popup garages,
co-op guests); anything that goes through `PaintShop.Render` gets the stamped one. A livery without layers keeps its drawn paint; only its sponsors can change.
`cup26livery89`'s layers were split automatically from the drawn art: grey 175 is the body, the red rim
tones (140/112) became shading at 200/160, everything else is detail. Hand-drawn layers can replace them
at the same names.

`Draftmaster > Team HQ > Install Paint Shop In Open Scene` adds the designer and `TeamCarDisplay` to an
open TeamGarage scene. It only fills in what's missing and stays out of `TeamGarageBuilder`'s root.

## Starting sponsor

A new career starts with **Aces & Spades** already signed onto the hood of Ace Emerson's #89
(`StarterSponsor`, 36 races at the brand's base value). It is handed out the first time the brand catalogue
loads (`SponsorCatalog.All`), once per career: `SponsorBook.GiveStarter` keeps a `sponsors.starterGiven` flag,
which `CareerReset` and `SponsorBook.ClearAll` both clear. It only takes the hood if the hood is free. Brands
on the player's book never appear on AI cars or as pit-lane reps.

`DummySponsors` is seeded additively by name, like drivers, so an existing database picks up a newly
added brand on the next launch. `&` slugs to `and` (`Aces & Spades` → `aces-and-spades`).

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

| Slot | Pays | Art | Rect on the 64×32 livery (texture px, origin bottom-left) | Turn |
|---|---|---|---|---|
| Hood (PRIMARY) | 100% | `hood.png` | `(5, 4, 14×24)` — bonnet between the front arches | none |
| Quarters (SECONDARY) | 90% | `small.png` | both quarter rects below, one brand | — |
| Tail (TRUNK) | 70% | `small.png` | `(58, 8, 4×16)` — decklid strip behind the rear window | clockwise |
| ↳ Quarter left (+Y, far side) | 45% alone | `small.png` | `(48, 26, 16×4)` | half |
| ↳ Quarter right (−Y, near side) | 45% alone | `small.png` | `(48, 2, 16×4)` | none |

The three slots sold are Hood, Quarters and Tail (`SponsorSlots.All`). The two single quarters are only
the panels that Quarters paints; a deal placed on just one of them before Quarters existed still renders,
pays and occupies the pair.

The cup26 art runs **nose at −X**: hood x5–19, windscreen x20–28, roof number x30–45, rear window x46–57,
decklid strip x58–62. Rects and turns live in `Assets/Resources/Sponsors/cup26Layout.asset`
(`CarSponsorLayout`); a carset with a different body shape gets its own `Resources/Sponsors/<carset>Layout`.

**The car is the mask.** The baker only paints where the livery is opaque and never changes its alpha, so a
rect can run off the bodywork (or the texture) on purpose — a quarter-panel logo wraps off the rear corner
and the silhouette crops it. Each panel must still be mostly on the car (`EveryPanelLandsOnTheBodywork`).

**Turns** are as seen in an image viewer. One `small.png` (16×4) serves the trunk and both quarters: the
trunk turns it a quarter to read across the decklid, the far-side quarter turns it a half, so it crops
differently on each side.

**Draftmaster > Sponsors > Preview Slots On Livery** writes `Temp/SponsorSlotPreview.png` — the panels
drawn over a real livery, magnified — so rects can be nudged without entering play mode.

## Sponsor art

Three images per brand, keyed by the brand name slugged (`"Voltage Energy"` → `voltage-energy`, see
`SponsorKeys.LogoKey`):

| File | Used for | Size |
|---|---|---|
| `Resources/Sponsors/Full/<key>.png` | boards, billboards, sponsor + team UI (`SponsorArt.LoadFull`) | any |
| `Resources/Sponsors/Hood/<key>.png` | the hood | 14×24 (the hood rect) |
| `Resources/Sponsors/Small/<key>.png` | trunk and both rear quarters | 16×4 |

`Resources/Sponsors/Car/<key>.png` is the original one-decal-per-brand folder, still read as a fallback.
Decals are blitted **1:1 and never scaled** (resampling takes them off the 12.8 px/m grid); oversized art is
centred and clipped. `SponsorDecalImport` imports anything in `Hood/`, `Small/` or `Car/` Read/Write,
point-filtered and uncompressed, so dropped-in art works without touching import settings.

**Draftmaster > Sponsors > Generate Placeholder Decals** fills in a hood and small plate for every database
sponsor that has no art yet — it never overwrites an existing file. **Preview Sponsored Car** bakes
`cup26livery89` through the real runtime path to `Temp/SponsorCarPreview.png`; select any of a brand's
images in the Project first to see that brand on every panel.

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
| `Assets/Scripts/Sponsors/` (`Draftmaster.Sponsors` asmdef) | Pure, testable: `SponsorSlot`, `SponsorDeal`, `SponsorBook`, `SponsorTerms`, `CarSponsorLayout`, `DecalTurn`, `SponsorLiveryBaker` |
| `Assets/Scripts/Sponsorship/` | Game-coupled: `SponsorCatalog` (SQLite + weekend picks), `SponsorArt`, `SponsorPainter`, `SponsorPaintDirector` |
| `Assets/Scripts/OnFoot/SponsorRepNPC.cs`, `SponsorRepSpawner.cs` | The pit-lane negotiation |
| `Assets/Scripts/UI/SponsorBoardPanel.cs` | The garage board (and `F6`) |
| `Assets/Editor/SponsorArtTools.cs` | Placeholder art, layout asset, both previews |
| `Assets/Editor/SponsorDecalImport.cs` | Import settings for decal art |
| `Assets/Tests/Editor/SponsorTests.cs` | Payout, placement and haggling rules |

Signed deals persist in PlayerPrefs (`sponsors.book`) like the rest of the live career state
(`PlayerWallet`, `PlayerStatsLedger`, `FanAppeal`), not in the half-built SQLite career tables. The brand
catalogue itself is the seeded `Sponsors` table (`DummySponsors`).
