# NPCs, Paper-Doll Characters & Side Quests

How to author characters, dialogue, and quests in Draftmaster3. Everything here works in the
spline-based scenes (WatkinsGlen etc.) where the on-foot player uses `OnFootController`.

---

## 1. Paper-Doll Character System

NPC (and eventually player) appearance is built from **layered sprite sheets** — Stardew Valley
style. Each layer is a separate greyscale sprite sheet tinted at runtime, so one drawn garment
yields every colour variation for free.

### How it works

- **`NPCPartLibrary`** (asset: `Assets/Resources/NPC/NPCPartLibrary.asset`) holds the shared frame
  grid and the part categories in draw order, back to front: **Base, Bottoms, Shoes, Top, Hair, Hat**.
  Each category has a list of sprite-sheet options and a list of tint colours (the palette).
- **`NPCLayeredAppearance`** (component) builds one child SpriteRenderer per layer, slices the
  sheets at runtime, and animates all layers in lock-step via `SetFrame(i)`.
- Facing is done by **rotating the transform** (art faces -Y / down); the walk animation is a
  single row of frames, not directional.

### Sprite sheet spec

| Property | Value |
|---|---|
| Format | PNG, transparent background |
| Layout | Horizontal strip, frames left→right |
| Frame size | **8×8 px** (current grid — set in NPCPartLibrary) |
| Frame count | 7 (walk cycle) → sheet is **56×8**. Static parts (hats) may be a single 8×8 frame |
| Facing | Character faces **down** (-Y) |
| Import settings | Texture Type: Sprite, Filter: Point, Compression: None |

**Every sheet must share the same canvas, frame count, and per-frame pose** so layers line up.
Easiest workflow: one Aseprite file, one layer per part, export each layer as its own PNG.

### Greyscale rule (the Stardew trick)

Tint is a **multiply**: white pixel × red tint = exactly red; grey pixel × red tint = darker red
(your shading, automatically in the right hue).

- Parts that should be **recolourable** (skin, tops, bottoms, shoes, hair): paint in a
  **white/grey ramp** — pure white where light hits, light grey for shading.
- Parts with **one fixed look**: paint in real colours, leave tint white (multiply by white = unchanged).
- Tint covers the whole layer — a coloured logo on a greyscale shirt will shift hue with the
  fabric. Fixed-colour details need their own layer.
- The **Base** layer is the full body (skin), complete under all clothing — short sleeves/shorts
  variants would otherwise show holes. Skin tone = Base layer tint.

### Adding a new part

1. Drop the PNG anywhere under `Assets/Sprites/` (convention: `Assets/Sprites/Walking/` or a
   `Parts/` subfolder), set import settings per the table above.
2. Open `Assets/Resources/NPC/NPCPartLibrary.asset`, add the texture to the matching category's
   **Options** list.
3. Add palette colours to that category's **Tint Options** — these become one-click swatches in
   the NPC inspector and the pool random crowd NPCs draw from.

### Designing an NPC in the editor

**GameObject > 2D Object > Layered NPC (Paper Doll)** creates a ready-to-design NPC with the
library assigned and `Use Authored Outfit` on. In the `NPCLayeredAppearance` inspector:

- Per layer: **include toggle**, **style dropdown** (sheet names + Random), **colour picker**,
  **swatch row** (from the category's Tint Options).
- **Preview / Rebuild** — builds the layers live in the Scene view.
- **Randomize** — rolls a full random outfit.
- **Preview Frame** slider — scrubs the walk cycle to check layer alignment.

Preview layers are ordinary child objects; at runtime `Build()` sweeps and rebuilds them, so they
never double up. Scene-placed NPCs build themselves on Start — no spawner needed. Spawner-built
crowd NPCs (PaddockSpawner, PitCrewSpawner) keep `Use Authored Outfit` off and randomise.

---

## 2. Dialogue

Add **`NPCInteractable`** to any NPC. Registration is automatic — the player's `OnFootController`
shows the E/A prompt in range; E/Space/gamepad-A starts and advances; both characters turn to face
each other; typewriter speech bubbles handle display.

- `speakerName` — display name.
- `lines[]` — one entry per conversation beat. Suffix a line with `#player` and it's spoken from
  the **player's** bubble instead of the NPC's.
- `repeatable` — loop the conversation or stay finished.

For branching dialogue there is `InkNPCInteractable` (compiled Ink .json + DialogueHandler
canvases from the Phoenix era), but the SpeechBubble path above is the proven one in spline scenes.

---

## 3. Side Quests

All quest code lives in `Assets/Scripts/Quests/`. Quest definitions are **`QuestInfo`**
ScriptableObjects in **`Assets/Resources/Quests/`** (create via **Assets > Create > Quests > Quest**).

### Objective types

| Type | Completes when | Key fields |
|---|---|---|
| `BeatDriverInRace` | Player finishes ahead of a named driver | `driverName` (case-insensitive contains-match vs race results), `singleRaceAttempt` (fail resets quest; off = keep trying every race) |
| `FinishRacePosition` | Player finishes at or better than a position | `targetPosition` (1 = win) |
| `StatThreshold` | A career counter reaches a target | `statKey`, `statTarget`, `countFromAccept` (count from accept instead of career total) |
| `DeliverItem` | Player hands an item to the delivery-target NPC | `itemId`, `itemDisplayName` |
| `RelationshipBelow` | Player's relationship with a driver falls to a target (make an enemy) | `driverName` (**empty = any driver**), `relationshipTarget` (e.g. -60) |
| `RelationshipAbove` | Player's relationship with a driver rises to a target (make a friend) | `driverName` (**empty = any driver**), `relationshipTarget` (e.g. 20) |
| `ContactDriver` | Player trades paint with a driver hard enough | `driverName` (**empty = anyone**), `minContactSeverity` (0..1; ~0.2 bump, ~0.6 slam), `playerMustCause` (player must be the striker) |

Relationship objectives ride on the driver-relationship system (see `Docs/Rivalry-and-TeamSwitch.md`).
Because the AI field's names reshuffle every race, **leave `driverName` empty (wildcard)** unless the
quest targets a driver you know will be present.

Other fields:

- `id` — **stable save key, never change once shipped** (state persists under it).
- `prerequisiteQuestId` — quest can't be offered until that quest is Completed (chains).
- `rewardItemId` — item granted on completion (e.g. completing quest A hands over the charm that
  quest B needs).

### Quest state machine

`NotStarted → Active → ReadyToTurnIn → Completed`, persisted in PlayerPrefs (`quest.state.<id>`).
Race objectives flip to ReadyToTurnIn when the final classification lands; stat objectives when the
counter crosses the target; delivery quests skip ReadyToTurnIn and complete at the hand-over.

### Giving a quest to an NPC

Use **`QuestGiverNPC`** instead of NPCInteractable (it extends it — bubbles/prompt work the same).

- `quest` — the QuestInfo asset.
- `offersQuest` — this NPC offers it (off for an NPC that's only the delivery target).
- `isDeliveryTarget` — DeliverItem quests hand the item over here.
- `grantItemOnAccept` — item id given when the player accepts (e.g. a package to carry).
- Line sets per state: `offerLines`, `activeLines`, `turnInLines`, `completedLines`, `lockedLines`
  (shown while the prerequisite is unmet). `#player` suffix works in all of them.

The line set is chosen when the conversation opens; the state change (accept / turn-in / delivery)
commits when it ends.

**Lucky-charm chain example:**

1. NPC A: `QuestGiverNPC`, own intro quest or none, `grantItemOnAccept = lucky_charm`
   (or A's quest has `rewardItemId = lucky_charm`).
2. NPC B: `QuestGiverNPC`, `quest = Quest_LuckyCharm`, `isDeliveryTarget` on. B offers the quest;
   once the player carries the charm, B's next conversation uses the turn-in lines and consumes it.

### Career stats (for StatThreshold quests)

`PlayerStatsLedger` — persistent counters in PlayerPrefs (`stat.<key>`). Currently fed by
`RaceDirector`:

| Key | Incremented |
|---|---|
| `starts` | Every green flag in a race session |
| `races` | Every final classification with the player in it |
| `wins` / `top5s` / `top10s` | Position-based, at classification |
| `contacts.caused` / `contacts.received` | Player was the striker / victim of a logged car contact |
| `paybacks.against` | An AI declared a payback move on the player |
| `teamswitches` | Player swapped cars via the TEAM panel mid-race |

Any new counter is immediately quest-able — call `PlayerStatsLedger.Increment("your.key")` from
anywhere. Manufacturer starts (`starts.chevrolet` …) are planned but wait on the player career
having a manufacturer; the hook point is commented in RaceDirector.

### Items

`PlayerInventory` — persistent item-id list in PlayerPrefs (`inventory.items`). Plain string ids
(`lucky_charm`); duplicates allowed. `Has` / `Add` / `Remove`.

### HUD

`QuestHUD` shows tracked quests (Active / ReadyToTurnIn) top-right: title + progress line, green
when ready to turn in. It appears on first accept, survives scene loads, only draws in gameplay
scenes (race or on-foot), and revives itself on launch when a save has tracked quests.

### The mission board (no NPC required)

The race pause menu (Esc) has a **MISSIONS** panel listing every QuestInfo asset: available quests
can be accepted there, and non-delivery quests can be turned in there too. It mirrors what a
QuestGiverNPC would do, so quests are fully playable in race scenes with no walking NPCs. Quests
whose prerequisite is unmet are hidden from the board. DeliverItem still hands over at its target
NPC.

### Example quest assets (`Resources/Quests/`)

- **Quest_BeatRival** — BeatDriverInRace. `driverName` is `CHANGE_ME`; set a real driver name.
- **Quest_SponsorStarts** — StatThreshold, 30 career `starts` (sponsor gate).
- **Quest_LuckyCharm** — DeliverItem, `lucky_charm`.
- **Quest_SendMessage** — ContactDriver, wildcard, severity ≥ 0.5, player must cause. Entry point of
  the rivalry chain.
- **Quest_PublicEnemy** — RelationshipBelow -60 with anyone (prereq `send_message`).
- **Quest_DraftingPartners** — RelationshipAbove +20 with anyone (draft-bond your way there).
- **Quest_FreshSeat** — StatThreshold `teamswitches` ≥ 1 from accept (use the TEAM panel).

---

## Gotchas

- Sprite sheets are sliced at runtime (`Sprite.Create`) — no Unity sprite-editor slicing or
  Read/Write flag needed. The frame grid comes from NPCPartLibrary, so changing frame size there
  requires **all** sheets to move together (8×8 → 16×16 means doubling pixelsPerUnit to 200 to
  keep world size).
- Quest `id` doubles as the save key — renaming it orphans player progress.
- `BeatDriverInRace` name matching is contains-based against race-result names; a rival absent from
  a race consumes no attempt.
- Resetting quest/stat/inventory state during testing: delete the PlayerPrefs keys
  (`quest.state.*`, `quest.base.*`, `stat.*`, `inventory.items`).
