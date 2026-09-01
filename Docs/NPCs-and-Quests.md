# NPCs, Paper-Doll Characters & Side Quests

How to author characters, dialogue, and quests in Draftmaster3. Everything here works in the
spline-based race scene (`Assets/Scenes/RaceScene.unity` plus whichever track package it loads),
where the on-foot player uses `OnFootController`.

---

## 0. Placing NPCs (`PlacedNPC` + the NPC Director)

**This is how you add, move, remove and gate an NPC.** Drop an empty GameObject, add **`PlacedNPC`**,
fill it in. Nothing else needs wiring: the body is cloned from the on-foot prefab at runtime
(`NPCFactory`) and the dialogue / cutscene components are attached to it.

**Where to put the marker:**

| The NPC is… | Put the marker in | Anchor |
|---|---|---|
| specific to one track (a local promoter, a track quest giver, the RV engineer) | the track package, under `Paddock/NPCs` — `Draftmaster > Tracks > Edit Selected Package` | `Here`, or a geometry anchor if they should follow a piece of track furniture |
| part of the every-track cast (pit greeter, crew chief) | `Assets/Scenes/RaceScene.unity`, under the `NPCs` root | a geometry anchor |

Scene markers live under one empty at the origin called **`NPCs`** — created on demand by
`PlacedNPCDefaults.Root()`. (They used to be parented to `PitLaneStart`, which only marks where the pit
lane begins.)

**Where the marker lives is what decides which tracks they appear at.** A marker in a package exists at
that track and nowhere else — no appearance condition needed. `AppearanceConditions.tracks` is for the
other case: a marker in the shared scene that should only turn up at some tracks.

Already placed one in the wrong half? `Draftmaster > NPCs > Move Selected NPC Into Track Package` copies
the marker — tuned anchor, trigger ring, dialogue, conditions and all — into the selected track's package
and deletes it from the scene.

The pit greeter, the crew chief and the team liaison are installed automatically in an undressed scene
(`PlacedNPCDefaults`), because they work off geometry every track has: the pit lane, the player's car and
the motorhome door.

The motorhome door used to hold a **race engineer** who talked about how the car looked overnight and sent
the player off to the crew chief for a setup call. That beat is retired — the weekend books the driver's day
now, and the person waiting outside the RV is the liaison telling them what they are wanted at next. The
`PlacedNPC.Role.RaceEngineer` value is kept (deleting it would renumber every role serialized in a scene or
package) but nothing creates one; a hand-placed marker still using it is legacy content and can be deleted.

**Anchors** are what let one marker work at all thirty-five tracks — the position is derived from
geometry rather than typed in:

- `Here` — exactly where you put it. For anything inside a track package.
- `PitLane` — `along` metres up the pit lane from the player's spawn, `lateral` metres off it
  (negative = away from the wall).
- `ParkedCar` — relative to the player's car, measured from the *car's own* lateral offset. Turn on
  `followAnchor`: `GridSpawner` re-parks the car into its fitted pit box several frames after the
  scene opens, and a marker placed once ends up metres away.
- `RVDoor` — `along` metres straight out of the RV door, `lateral` along its flank (+ = toward the
  cab). Falls back to `PlayerSpawn` at a track whose package has no RV.
- `PlayerSpawn` — on the line the player walks from their spawn toward their car.

**Interaction** is how the player meets them:

- `TalkOnInteract` — stand there, press E. The ordinary case.
- `WalkUpCutscene` — the player freezes, the letterbox bars come in, the NPC walks over and opens the
  conversation face-to-face (`NPCWalkUpCutscene`). `waitForTrigger` puts a walk-over ring
  (`CutsceneTrigger`) at `triggerOffset`; off = it plays as the scene opens. `objectiveOnFinish` puts
  a banner up when the conversation ends.
- `OnCarEntry` — silent until the scene flow runs them. This is the crew chief's briefing, fired by
  `PitLaneStart.EnterCar`.
- `Silent` — a body, no dialogue.

**When they appear** is the full `AppearanceConditions` block (section 4 below): session, series,
track, career-stat range, quest state, inventory, career path, repeat scope, chance.

Setting `quest` turns the marker into a `QuestGiverNPC` and the four quest line sets take over the
conversation — see section 3.

### The NPC Director — `Draftmaster > NPCs > Director` (Ctrl+Shift+N)

- **Session switcher** (Practice / Qualifying / Race) plus track and series fields. These drive
  `AppearanceConditions.Preview`, so the whole editor — window, inspector and scene gizmos — answers
  "would this person be here?" for that hypothetical **without entering play mode**.
- **Table** with a ✔/· column per session, so a cast that changes across a weekend reads at a glance.
  A row that isn't appearing says which clause stopped it ("not in Race", "wins is 0, needs 5..∞").
- Click a row to select and frame the NPC; `×` deletes; **Add NPC** drops one at the scene-view pivot
  (into `Paddock/NPCs` when a package is open in Prefab Mode).
- **Install Default Pit Cast** creates the greeter / race engineer / crew chief as real, editable
  markers. They're defined once, in `PlacedNPCDefaults` — a scene that has never had this run gets the
  identical three built at runtime (`PitLaneStart.installDefaultCast`), and the runtime install stays
  out of the way once markers exist.

In the scene view every placed NPC draws as a person-shaped gizmo **at their resolved position**,
green if they appear in the previewed session and grey-red if they don't, with the stand point,
trigger ring, trigger radius, interact range, stop distance and facing all on drag handles.

**Geometry anchors only resolve when a track is loaded in the open scene.** The race scene is
deliberately roadless, so run `Draftmaster > Tracks > Preview Selected Package In Scene` (and
`Clear Package Previews From Scene` when done) or author inside the package's own Prefab Mode stage.

### The random crowd — per-track dialogue pools

The Director lists *placed* NPCs. The rest of the cast is spawned procedurally (`PaddockSpawner` talkers,
`AutographFanSpawner` fans, ambient barks from `NPCAmbientChatter`), and those have no marker to select —
but **what they say is authorable per track**.

- **`DialoguePool`** (ScriptableObject, `Assets/Resources/Dialogue/`) holds ambient barks (by
  `ChatterArea` × `ChatterMood`), multi-line conversations (by `ConversationKind`: PaddockCrew,
  AutographFan, DriverFlavour) and a pool of speaker names.
- A pool with an **empty `trackId` is global**; one naming a track applies only there, **on top of** the
  global pool and the compiled-in tables. `replaceBuiltIn` drops the compiled tables instead of adding
  to them.
- **`DialogueLibrary`** resolves them at runtime — global pool, then this track's — caching per track and
  falling back to the code tables so a spawner is never left mute. It also installs
  `AmbientChatter.Provider`, which keeps `AmbientChatter` itself pure and unit-testable (no Resources, no
  track lookup) while still letting authored lines win.

Menus, all under **`Draftmaster > NPCs`**:

| Menu | Does |
|---|---|
| Dialogue Pool (Global) | create/select `Resources/Dialogue/Default.asset` |
| Dialogue Pool (Selected Track) | create/select the pool for `TrackSelection.CurrentId` |
| Seed Global Dialogue Pool From Built-Ins | copy every compiled-in table into the global pool and switch it to Replace — the house style becomes editable rows instead of something buried in a .cs file |

The NPC Director's footer shows both pools with their line counts and Create/Edit buttons.

**Not yet pooled:** `DriverPresenceDirector`'s rival-driver flavour lines (they're keyed on driver
personality rather than on the track). `ConversationKind.DriverFlavour` is reserved for wiring them up.

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

### Current parts — all placeholder

`Assets/Sprites/Walking/Parts/` holds 13 **placeholder** sheets, all 56×8 (7 frames), painted in the
white/grey ramp and meant to be redrawn. They were cut out of `walk_base.png`'s own silhouette, so
every pixel of every part lands on a pixel the body actually covers.

The body reads top-down, and each layer owns a band of it so nothing above it hides it completely:

| Layer | Band of the body | Sheets |
|---|---|---|
| Base | the whole silhouette | `walk_base` (unchanged) |
| Bottoms | hips row + feet row | `bottoms_trousers`, `bottoms_shorts` (bare ankles) |
| Shoes | the toe pixel at each end of the feet row | `shoes_low`, `shoes_boots` |
| Top | shoulders row + arms row | `top_tee` (arm tips stay skin), `top_vest` (bare arms), `top_jacket` (sleeves to the hands) |
| Hair | the head block | `hair_short`, `hair_long`, `hair_buzz` |
| Hat | the head block | `hat_cap`, `hat_beanie`, `hat_helmet` |

The head block sits in the same place on all seven frames, so hair and hats repeat one pose across the
strip. Colour variation is all tint: 5 skin tones, 8 shirt, 6 trouser, 5 shoe, 6 hair, 6 hat.

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

### Player choices mid-conversation

**`DialogueChoiceUI`** is a modal "pick one line" panel for a conversation that needs an answer:

```csharp
DialogueChoiceUI.Open(this, "So what do you want to be?", options, i => Picked(i));
```

W/S (stick, d-pad, or mouse) moves the selection, E/Space/Enter/gamepad-A answers. It's IMGUI, like
`SpawnIntroUI` and the debug panels — no prefab, no canvas wiring. The pick is dispatched from `Update`,
never from `OnGUI`, so the callback can safely open the next speech bubble.

Rules for the calling NPC (see `CareerPathNPC` for the working example):

- Override `IsTalking` to include "a choice of mine is open", or `OnFootController` unlocks the player and
  they walk off mid-question.
- Swallow interact presses for a frame or two after the answer lands — the key that answered the choice is
  the same key that advances dialogue, and the controller reads it independently.
- Build a branching conversation by swapping `lines` and calling `base.Interact()` again per beat; the base
  class restarts at index 0 whenever it isn't already talking.
- The panel cancels itself if its owner is destroyed or disabled, so poll it (`DialogueChoiceUI.IsOpen &&
  DialogueChoiceUI.Owner == this`) and end the conversation if it vanished.

---

## 2a. The career-path opening choice

**`CareerPathNPC`** is the paddock old-hand who opens a career. He asks "do you like racing?" (flavour),
then "what do you want to be when you grow up?" — and that second answer is the career's premise. Four
answers, one per `CareerPath.Path`:

| Answer | Path | Leads with |
|---|---|---|
| "I want to be on a championship winning pit crew" | `PitCrew` | +8 `career.pitcraft` |
| "I want to be a championship winning driver" | `Driver` | +9 `career.driving` |
| "I want to own my own race team" | `TeamOwner` | +8 `career.business` |
| "I want to scout the world's best young drivers" | `Scout` | +9 `career.scouting` |

**`CareerPath`** (`Assets/Scripts/Progression/`, own assembly `Draftmaster.Progression` so the maths is
EditMode-testable) persists the answer in PlayerPrefs (`career.path`) and pays out the starting stats
**once** (`career.path.applied`):

- Every path spends the same `StartingStatBudget` (17) across the five career attributes —
  `career.driving`, `career.pitcraft`, `career.engineering`, `career.business`, `career.scouting` — so the
  choice is a shape, not a power level. They're written as ordinary `PlayerStatsLedger` counters, so a
  `StatThreshold` quest or an `AppearanceConditions.statKey` can read them with no extra plumbing.
- It also nudges `FanAppeal` (`StartingFanAppealBonus`): the kid who wanted to drive starts with a name.

**Gating future opportunities on the answer** is what `AppearanceConditions.careerPaths` is for — leave it
empty for "any path", or list the paths a beat is meant for:

```csharp
public AppearanceConditions appearance = new AppearanceConditions
{
    careerPaths = new[] { CareerPath.Path.TeamOwner, CareerPath.Path.Scout },
};
```

**`CareerPathNPCSpawner`** self-installs him into the on-foot paddock flow (single player + `PitLaneStart`
+ a spline `TrackBuilder`, same gate as `DriverMotorhomeLot`), stood out from the player's RV door on the
opposite side to the race engineer's walk-up beat and clamped inside the `PaddockBoundary`. In the demo
he's simply there, out of context, until career mode owns this moment; once the choice is made he stays and
switches to small talk (`stayAfterChoosing`).

The answer is once per save, so testing it needs **Draftmaster > NPCs > Clear Career Path Choice**, which
un-answers the question and refunds the stats it paid out.

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

## 4. NPCs across thirty-five tracks

Every round runs in one scene (`Assets/Scenes/RaceScene.unity`) with the track loaded as a package — see
`Docs/Tracks.md`. That splits NPCs into two kinds, and most of them are already the first kind:

**Spawned from geometry — nothing to do per track.** `PaddockSpawner`, `PitCrewSpawner`,
`AutographFanSpawner`, `CareerPathNPCSpawner`, `DriverMotorhomeLot` and `DriverPresenceDirector` all live in
the shared scene and place people off the pit lane spline. The pit greeter, race engineer and crew chief are
`PlacedNPC` markers on geometry anchors (section 0) — same effect, but you can see and edit them. `AutographFanSpawner` and `CareerPathNPCSpawner` even self-install
(`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`), and the package is instantiated before that runs, so
they find the road. A new track gets the whole paddock cast for free.

**Placed by hand — put them in the package.** Anything belonging to *this* track (a local promoter, a
track-specific quest giver, an NPC stood by a particular gate) is a `PlacedNPC` in the package prefab under
`Paddock/NPCs`, a root the dressing factory creates for the purpose. Open the package in Prefab Mode
(`Draftmaster > Tracks > Edit Selected Package`), drop the NPC in, and it travels with the track — loaded
when that round loads, absent otherwise, with no scene to keep in sync.

Same for spawn markers: `PlayerSpawnPoint` markers are per-track furniture, so they live in the package too.
The generated paddock ships two — `SpawnPoint_RV` (inside the RV prefab, the one `PitLaneStart.forcedSpawnName`
looks for) and a weight-0.5 `SpawnPoint_Paddock` fallback.

### Gating a beat to particular tracks

`AppearanceConditions` has a **`tracks`** list (`Daytona`, `Martinsville`, …) — empty means any track. Use it
rather than `scenes`, which now says only which *scene* you're in and is the same string for every round.
Old conditions that named a track in `scenes` still work: the check accepts a scene name **or** a track id.

`Repeat.OncePerTrack` is keyed on the track id for the same reason. Before the split it keyed on the scene
name, which in a shared scene would have quietly meant "once, ever".

Both read `AppearanceConditions.CurrentTrackId` — the loaded `TrackPackage`'s id, falling back to
`TrackSelection.CurrentId`.

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
