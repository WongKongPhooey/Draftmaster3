# Driver Fights

Paddock scraps between the player and a driver they've fallen out with on track. Started from a dialogue
option, fought with shoves, and ended by nearby NPCs pulling the two of them apart.

Rating note: this is written to sit inside a 7+ rating. Shoves only, no blood, no injury art, no weapons,
nobody is knocked out, and the crowd always breaks it up. A landed move costs "composure", and a fighter who
runs out of it simply stops swinging.

## The flow

1. Walk up to a driver on foot and press **E**.
2. If the pair's `DriverRelationships` score is at or below `RivalThreshold` (-30), they greet you with an
   argument instead of small talk (`RivalDriverNPC.rivalLines`).
3. When the argument runs out, a `DialogueChoiceUI` panel offers **Square up to them** / **Let it go**.
4. Squaring up starts a `DriverFight`: a composure bar appears over each character's head, both square up for
   a beat, then it's live.
5. **SPACE** (gamepad **X**) shoves. The rival circles and shoves back on their own timer.
6. After ~14 seconds — or as soon as either fighter is spent — nearby NPCs run over, wedge themselves between
   the two of them, and walk each driver away. The fight is over; nobody wins by knockout.
7. Fallout: the pair's relationship drops a further 12 points, and `fights.started` plus
   `fights.won` / `fights.lost` / `fights.drawn` land on the stats ledger (quest-able like any other counter).

A 20-second cooldown stops the same argument restarting the moment the crews let go.

## Who you can fight

Nothing in the scene decides this — the relationship score does, and that score comes from racing:
`VehicleCollision` reports contact, AI paybacks make it worse, clean races heal it (see
`Docs/Rivalry-and-TeamSwitch.md`).

Drivers you can walk up to are spawned by `DriverPresenceDirector` and now carry `RivalDriverNPC` instead of
a plain `NPCInteractable`. Below the rival threshold they argue and offer the fight; above it they behave
exactly as before. Drivers sat in their cars are talked to through the window and are never fightable —
they'd have nowhere to fight from.

### Identity — read this before touching relationships

`DriverRelationships` keys on the name a driver **races** under, not the name shown in dialogue:

- **The driver**: `DriverLabel.driverName`, which `GridSpawner` fills from `RosterLookup.LabelName` (roster
  ShortName, e.g. `Chastain`, `A.Dillon`). `RivalDriverNPC.driverName` must be that same string, while
  `speakerName` stays the full name for the bubble. Using the full name silently keys a second, empty
  relationship for the same person, so a feud earned on track never reaches the paddock.
- **The player**: `DriverRelationships.PlayerName` — the player car's `DriverLabel` first, the position
  tracker only as a fallback. The tracker is not reliable on its own: it holds the placeholder `"You"` until
  `TeamSwitchController` renames it once the car is labelled, so anything that read it early keyed a
  different person than the racing did. This bit both the paddock gate and the test seeder before it was
  fixed.

Turn the whole option off with `DriverPresenceDirector.allowFights`, or per driver with
`RivalDriverNPC.allowFights`.

## Testing it

`FightTestRivals` installs itself in any single-player scene with the on-foot pit flow. It waits for the
paddock to populate, picks the drivers nearest the player's spawn, and — only if the pair are still on
neutral terms — seeds scores of -72 and -46 so the fight option is reachable on a fresh save. It never
overwrites a feud earned on track, and it logs the driver's name and distance from the player.

- Turn it off: `FightTestRivals.Enabled = false`.
- Undo what it wrote: **Draftmaster > Fights > Clear Seeded Test Rivalries**.
- Nuclear option: **Draftmaster > Fights > Reset ALL Driver Relationships** (confirms first).

## Scripts

| Script | Role |
| --- | --- |
| `Assets/Scripts/Fights/FightRules.cs` | Pure decision maths: damage, whether a move connects, when to break it up, who came off better. Own asmdef (`Draftmaster.Fights`) so it is unit-testable in EditMode. |
| `Assets/Scripts/OnFoot/Fighter.cs` | One character in a fight: composure, throwing a move, taking one (stagger, knock-back, hit flash). |
| `Assets/Scripts/OnFoot/RivalFightAI.cs` | The rival's brain — holds fighting distance, circles, swings on an aggression-driven timer. |
| `Assets/Scripts/OnFoot/DriverFight.cs` | The director: starts the fight, reads the player's attack input, leashes them to the fight, calls the break-up, applies the fallout. |
| `Assets/Scripts/OnFoot/FightPeacemaker.cs` | A bystander who runs in, wedges between the pair, and marches one of them away. |
| `Assets/Scripts/OnFoot/FightHealthBar.cs` | The world-space composure bar over each fighter's head. |
| `Assets/Scripts/OnFoot/RivalDriverNPC.cs` | The dialogue: argument lines, the square-up choice, and the hand-off into `DriverFight`. |
| `Assets/Scripts/OnFoot/FightMotion.cs` | Shared move/turn/animate helpers, so Animator rigs and paper-doll rigs are driven the same way. |
| `Assets/Scripts/OnFoot/FightTestRivals.cs` | Seeds the test rivalries described above. |
| `Assets/Tests/Editor/FightRulesTests.cs` | EditMode coverage of the maths (12 tests). |

## Animation

Two different rigs have to fight:

- **Animator rigs** — the player and `PitLaneStart`'s NPCs are `TaylorEmerson` clones whose
  `PlayerOnFoot.controller` already has a `Pushing` state driving `Assets/Animation/OnFoot/Push.anim`.
  `Fighter` plays that state by name and returns to the `Movement` blend tree when the move is done. No
  animator parameter is needed (the controller has no trigger for it).
- **Paper-doll rigs** — paddock drivers are `NPCLayeredAppearance` outfits with only a walk cycle. They get a
  scripted lunge instead: the body commits forward along its facing for the length of the move. It reads as a
  shove from the top-down camera without any new art.

### Adding the hooks later

`FightMove.LeftHook` / `RightHook` are already wired end to end — damage (higher than a shove), input
(**J** / **K**, mouse buttons, or the shoulder buttons), and a shoulder-biased lunge. They are gated behind
`DriverFight.enableHooks`, which is off because there is no clip for them yet. To turn them on:

1. Author the clips and add them to `PlayerOnFoot.controller` as states (e.g. `HookLeft`, `HookRight`).
2. Give `Fighter` the state names — today it plays `pushStateName` for every move, so add the two fields and
   pick per `FightMove` in `PlayMoveVisual`.
3. Set `enableHooks = true` on `DriverFight`.

## Tuning

Everything is a field on the runtime-created components, so the quickest way to tune is to edit the defaults:

- Reach, timing and knock-back: `Fighter` (`reach`, `windupSeconds`, `recoverySeconds`, `staggerSeconds`).
- Damage and pacing: `FightRules` (`ShoveDamage`, `HookDamage`, aggression scale, `AiAttackInterval`).
- Fight length, leash, camera zoom, fallout: `DriverFight`.
- How the crews behave: `FightPeacemaker` (`runSpeed`, `wedgeSeconds`, `separationDistance`).
