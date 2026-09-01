# Driver Relationships, Payback & Team Switching

Two race-scene systems added 2026-07-07, both single-player, both live in `Assets/Scenes/RaceScene.unity`.

## Driver relationships (NASCAR Thunder 2004 style)

`Assets/Scripts/AI/DriverRelationships.cs` — static store. Every driver **pair** carries a score
from **-100 (hated) to +100 (ally)**, persisted in PlayerPrefs (`rel.<a|b>`, name-pair keyed,
order/case-insensitive; index under `rel.index`). Identity = the car's `DriverLabel.driverName`;
the player is `RacePositionTracker.playerName` ("You").

### Score movement

| Event | Change |
|---|---|
| Car contact (closing speed ≥ `VehicleCollision.damageMinSpeed`, green flag only) | −3 to −17, scaled by severity; one log per pair per 1.5 s |
| Drafting the same partner cleanly (`AIRacingBehaviour.draftBondSeconds`, default 8 s) | +1 per interval |
| Race classification (`RaceDirector`) | every stored pair drifts 4 points toward 0 |

Blame: the striker is the car whose contact point lies ahead along its direction of travel
(`VehicleCollision` reports `frontHit`). Safety-car contacts are ignored.

### Standings & thresholds

`StandingOf(value)`: **Ally ≥ +40**, Neutral, **Rival ≤ -30**, **Furious ≤ -60** (`PaybackThreshold`).

### Payback (deliberate wrecks)

`AIRacingBehaviour` (Rivalry/Payback header): every `paybackScanInterval` each AI scans ±`paybackRange`
(22 m) for anyone (AI **or the player**) whose relationship is ≤ -60. Launch chance per scan =
`paybackBaseChance × (0.5 + aggression) × depth-below-threshold`, with a per-rival
`paybackCooldownSeconds` (45 s). While active (2.2 s): steers INTO the rival's lateral, drops all
follow caps, +6 mph boost, side-by-side repulsion vs the target disabled. The resulting contact
drops the relationship further — feuds escalate but the cooldown keeps it episodic.

### HUD

`Assets/Scripts/UI/RivalryFeed.cs` — self-bootstraps on first logged contact (race scenes only).
Top-center toasts: player contacts, standing changes ("X and Y are now RIVALS", "X is FURIOUS
with Y!"), payback declarations. **F4** toggles a standings panel of the player's relationships.

### Quests / stats hooks

`DriverRelationships` feeds `QuestManager.OnRelationshipChanged` / `OnPlayerContact` and the ledger
keys `contacts.caused`, `contacts.received`, `paybacks.against` (see NPCs-and-Quests.md).

## Team switching

`Assets/Scripts/TeamSwitchController.cs` — scene object in `RaceScene`. Bottom-left **TEAM** panel:
one button per team car (`#number NAME  Pposition`), click to jump into that car mid-race
(green-flag onwards; disabled in practice/qualifying; blocked while the target is on the pit lane).

- **Teams**: `GridSpawner` (Teams header) stamps `DriverLabel.teamId/teamName` in spawn order — the
  first `playerTeammates` (default 2) AI join team 0, the player's team; the rest pair into rival
  teams of `teamSize`. Only team-0 cars with a `PlayerVehicleController` (dynamic AI) are offered.
- **Hand-over out**: departing car re-engages its `SplineDriver` from the current pose
  (`EngageFromCurrentPose`, no teleport), gets `SplineInputDriver` + `AIRacingBehaviour`
  (added if missing — the original PlayerCar gains them on first switch), PVC flips to
  `externalInput`, wheelspin off, damage-impairs-handling off, `PitStopController` re-enabled.
- **Take-over in**: AI brains disabled (`SplineInputDriver`, `AIRacingBehaviour`,
  `PitStopController`), spline disabled (out of RaceField), PVC re-enabled so it registers as a
  RaceObstacle (the field brakes for it), `SeedPose` at the current pose — no lurch.
- **Identity**: driver **names swap with the human** (you are always "You" in standings/results);
  car number + livery stay with the chassis. `RacePositionTracker.SetLocalPlayer` moves the player
  flag; `DriveModeController.RetargetPlayerCar` keeps the V broadcast toggle pointed at the right
  car; `PlayerTelemetryHUD` retargets.
- Each switch increments the `teamswitches` stat (quest-able).

### Known gaps / polish backlog

- Engine audio doesn't swap profiles between cars (the old car keeps the louder player mix).
- `PlayerPitService` / pit-box markers still track the original player car's reserved box.
- Player gets no draft speed bonus (PVC has no drafting model), so player draft-bonding only
  accrues when an AI is the one tucked in behind the player.
- Relationships are name-keyed: AI names reshuffle between races, so a feud persists with the NAME,
  which may attach to a different liveried car next weekend (Thunder was the same).
