# Co-op career

A second player drops into the host's career at any moment, takes over one of the drivers already entered
in the weekend, and rides the host's weekend — every session the host drives, the guest drives; every time
skip, fast travel or scene change the host makes, the guest is carried along or pulled back to them.

The host is player one and owns everything: the save, the ledger, the clock, the track selection, the AI
field. The guest persists nothing. State replicates one direction only, which is what makes this tractable
— there is never a merge, never a conflict, never a question of whose weekend it is.

---

## What was already there

`Assets/Scripts/Multiplayer/` held a **competitive** multiplayer mode, last touched 2026-08-22:

| File | Role |
| --- | --- |
| `NetworkLauncher.cs` | UGS Sessions + Relay, join code, NGO scene management, transport-failure recovery |
| `NetworkedCarBindings.cs` | Per-player car: ready-up lobby, server-assigned livery, PreGrid→Formation→Green gate |
| `NetworkedAICar.cs` | Host-authoritative AI; clients hold pure NetworkTransform puppets |
| `NetworkCarOwnerGate.cs` | Owner drives, every other peer's brains off |
| `PaceLapAssist.cs` | Formation-lap speed cap derived from world-space transforms |
| `GameSession.cs` | The `SinglePlayer` / `Multiplayer` switch |

Plus three files bound to the **legacy scrolling system** and unreachable from the live flow —
`MPMovement.cs` (1298 lines), `NetworkedVehicle.cs`, `NetworkedOnFoot.cs`. They read
`RaceManager.playerLocation` and `EnvironmentManager.circuitRotation`; their prefabs are the two at
`Assets/Prefabs/` root, not the live pair in `Assets/Prefabs/Multiplayer/`. Left in place, not deleted.

### The thing that had to change first

That mode is built as the **opposite** of co-op. Seventeen call sites branch on `GameSession`, and nearly
all of them switch the career layer *off* when a network session is up:

```
CareerPathNPCSpawner:73    "no on-foot paddock in multiplayer"
DriverMotorhomeLot:147     "MP skips the on-foot paddock entirely"
SponsorRepSpawner:63       bails
FightTestRivals:46         bails
RaceDirector:70            no race finish, no results, no NEXT WEEKEND
RaceWeekend:13,14          IsPractice / IsQualifying hardcoded false
RaceWeekend:53             SessionLive forced true
TeamSwitchController:59    enabled = false
GridSpawner:131,140        separate host-spawn path that ignores the weekend clock
FormationDirector:86,108,171
```

Co-op needs the network session running *inside* the career, not instead of it. So the mode is not a
boolean any more.

---

## The mode model

`GameSession.Mode` gains a third value, and the two questions the codebase actually asks are separated:

```csharp
enum Mode { SinglePlayer, Multiplayer, CoopCareer }

IsSinglePlayer   // literally solo, no network        — unchanged meaning
IsMultiplayer    // the old competitive lobby race    — unchanged meaning
IsCoop           // co-op career
IsNetworked      // Multiplayer || CoopCareer
CareerActive     // SinglePlayer || CoopCareer  ← the weekend/paddock/career layer is live
```

Every existing gate keeps its meaning, so the competitive mode is untouched. The career gates that used to
read `IsSinglePlayer` move to `CareerActive`, which turns the paddock, the weekend clock, the race
director and the session flow back on for co-op.

Roles come from `Coop`:

```csharp
Coop.Active     // a co-op session is up
Coop.IsHost     // player one — owns the career
Coop.IsGuest    // player two — owns nothing
Coop.Guest      // the connected guest's client id, or none
```

**The guest spawns nothing career-side.** Every content spawner gates on
`CareerActive && !Coop.IsGuest`: the paddock cast, the RV lot, the sponsor reps, the AI field, the
weekend director's bookings. The guest receives all of it from the host. This is what keeps the two
processes from each independently deciding what Friday morning looks like.

---

## Phase 1 — foundation *(implemented)*

No visible gameplay on its own, but it is the load-bearing layer and it is testable in isolation.

### `Assets/Scripts/Multiplayer/CoopSession.cs` — new

`Coop` static: role queries, guest client id, connect/disconnect events, and `Coop.Reset()` for a clean
teardown back to single player.

### `Assets/Scripts/Multiplayer/GameSession.cs` — edited

Third mode plus the `CareerActive` / `IsNetworked` / `IsCoop` predicates above.

### `Assets/Scripts/Weekend/Core/WeekendLedger.cs` — edited

The whole weekend lives in one `Book` serialised to JSON in `PlayerPrefs["weekend.ledger"]` — slot index,
clock minute, done/missed lists, the four meters, earnings, headlines. Rather than mirror twelve fields as
twelve `NetworkVariable`s, two new entry points move the lot:

```csharp
public static string ExportJson();          // host: the whole book
public static void   ImportJson(string);    // guest: overwrite wholesale, fire Changed
```

`ImportJson` bypasses `Save()`'s PlayerPrefs write on the guest — the guest must not leave a foreign
career in its own prefs — and raises `Changed` so an open schedule screen redraws.

### `Assets/Scripts/Multiplayer/CareerMirror.cs` — new

A plain `MonoBehaviour` on the NetworkManager, host-authoritative, one per session. Built on NGO **named
messages** rather than a `NetworkBehaviour`: there is no transform to replicate, no ownership to hand around
and no scene presence, so a `NetworkObject` would have bought nothing and cost a prefab asset plus
registration on both peers. Publishes host career state to the guest:

| Replicated | Source of truth on host |
| --- | --- |
| Ledger book (JSON) | `WeekendLedger.ExportJson()` |
| Weekend id | `RaceWeekend.WeekendId` |
| Session kind | `RaceWeekend.Current` |
| `SessionLive` | `RaceWeekend.SessionLive` |
| Track id | `TrackSelection.CurrentId` |
| Race phase | `RaceStart.Current` |

Pushed on change (host polls its own statics; they are prefs-backed and have no universal change event) and
pushed in full to each client on connect, so a mid-weekend join lands correctly. The guest applies to its
local statics and never writes back.

### `Assets/Scripts/Multiplayer/CoopScene.cs` — new

Single scene-load router:

```csharp
CoopScene.Load(name);   // NGO SceneManager when hosting co-op, plain SceneManager otherwise
```

This is the whole of "the guest follows a time skip". Every career scene change already funnels through a
handful of `SceneManager.LoadScene` calls, and NGO's scene management drags the guest along:

- `Weekend/WeekendDirector.cs:446` — session routing (practice → qualifying → race, half-day advance)
- `Travel/TravelMapScreen.cs:426` — fast travel between venues
- `Travel/LandmarkLoader.cs:28,37` — landmark in/out
- `Garage/GarageScreenLoader.cs:43,65` — garage sheet in/out

### `Assets/Scripts/Multiplayer/NetworkLauncher.cs` — edited

`HostCoop()` / `JoinCoop(code)` beside the existing `HostGame()` / `JoinGame()`. The co-op path sets
`Mode.CoopCareer`, attaches `CareerMirror` + `CoopBodies` to the NetworkManager, and — the important
difference — **does not load a lobby
scene and does not wait for a ready-up**. The host stays exactly where it is; the guest is pulled into
whatever scene the host is standing in.

---

## Phase 2 — the guest's body *(implemented)*

### `Assets/Scripts/Multiplayer/CoopBodies.cs` — new

Each peer sends its own on-foot pose at 15Hz and holds one puppet per other peer, built from the same
prefab the scene already spawns the player from (`PitLaneStart.onFootPrefab`). Received poses are
interpolated; anything past `snapDistance` snaps instead, so a recall or a teleport is not animated as a
sprint across the paddock. The puppet is tinted so the two players are told apart at a glance.

Named messages again, and here the deciding reason is scene loads: the career reloads the scene constantly
(practice → qualifying → race, travel, the garage sheet), and a spawned `NetworkObject` surviving those is
far more machinery than a walking avatar justifies. A puppet is a plain scene object — it dies with the
scene and is rebuilt from the next pose that arrives, which makes a scene change self-healing rather than
something to coordinate. The legacy `NetworkedOnFoot.cs` was not reusable either way: it drives the
scrolling system.

### `Assets/Scripts/OnFoot/OnFootController.cs` — edited

New `RemotePuppet` flag. A puppet stays in `All` (so the crowd and anything scanning for people sees it)
but is skipped by `Current`, reads no input and runs no interactions. That matters more than it looks:
`Current` is asked several times a frame by the objective marker, the phone, the crowd director and the
fan spawner, and every one of them means *me* — handing any of them the other player's body would point
this player's HUD at somebody else.

### Career gates flipped

`CareerPathNPCSpawner`, `DriverMotorhomeLot`, `FightTestRivals`, `SponsorRepSpawner` and `RaceDirector`
move from `IsSinglePlayer` to `CareerActive && !Coop.IsGuest` — the paddock and the race director run in
co-op, but only the host stands them up. `PitLaneStart` needed no change: it already gated on
`IsMultiplayer`, which co-op is deliberately not.

### Recall

`CareerMirror` watches the **host's own body for a position jump** (>`jumpThreshold` in one frame) rather
than hooking every place the career moves the player — a time skip, a weekend marker, a cutscene, an
obligation settled on the spot. One rule that cannot be forgotten at a new call site later. It also recalls
once after a scene load settles, since a scene change carries the guest but drops them at that scene's own
spawn. `Coop.RecallGuest()` leaves them alone when they are already within radius.

### Interaction ownership — host talks, guest walks

`OnFootController.Update`, `PhoneUI.Update` and `WeekendDirector`'s `F10` handler all return early on a
guest. Two peers each opening their own dialogue with the same NPC is a conversation that disagrees with
itself, and a booking moves the clock for both. The guest walks, stands in on conversations and watches.
Two-way interaction is a later pass.

## Phase 3 — the networked career field and possession *(implemented)*

### 3a — the guest sees the host's real career field

`GridSpawner`'s **career** path (not the old `SpawnNetworkedField`) branches on `Coop.Active && IsServer`:
it instantiates `networkedAiPrefab` instead of `carPrefab` and `NetworkObject.Spawn()`s each car at the end
of the loop. Everything else is untouched — the weekend clock still decides there is a session, the roster
still decides who is in it, qualifying still fixes the order, teams and pit boxes are unchanged. A co-op
guest returns early from `Start` and spawns nothing.

No new prefab had to be authored: `Assets/Prefabs/Multiplayer/NetworkedAICar.prefab` already carries every
component the career loop would otherwise `AddComponent`, and the loop is written defensively
(`GetComponent ?? AddComponent`) so it runs against it unchanged. Two details make it work:

- The prefab ships its brains **disabled**, and `Spawn()` is the last thing in the loop — so
  `NetworkedAICar.OnNetworkSpawn` re-enables them on the server after the loop has finished configuring
  them. The ordering is load-bearing.
- Spawned NetworkObjects are **not parented** to the `AIField` transform. NGO re-roots them on the client,
  so a networked field stays at the scene root.

`NetworkedAICar` gained a replicated `VehicleInfoName`. A ScriptableObject cannot travel over the wire, but
its name can, and they all live in `Resources/Vehicles` — so a guest's copy loads the same accel/decel
curves rather than being handed a car with none the moment it possesses one.

`NetworkLauncher` registers the field prefab on both peers (`RegisterPrefab`) and both it and `GridSpawner`
auto-wire their prefab references in `OnValidate`, so nothing has to be dragged in by hand.

### `Assets/Scripts/Multiplayer/CoopNetworkTransform.cs` — new

`NetworkTransform` with `OnIsServerAuthoritative() => false`. Authority is fixed per component *type*, not
per instance, so it cannot be flipped on one car at runtime — but owner authority covers both halves with
nothing to switch. A server-spawned NetworkObject is owned by the server, so while the host's AI drives,
owner- and server-authoritative are the same thing; the moment ownership moves, the guest's pose becomes
the real one. Swapped onto the AI prefab (position X/Y, rotation Z, interpolated).

### 3b — possession

`Assets/Scripts/Multiplayer/CoopPossession.cs`. Automatic, host-driven: whenever
`Coop.GuestPresent && RaceWeekend.SessionLive`, the host picks a **random** car from the field that is
still server-owned, takes the AI off it, and `ChangeOwnership`s it to the guest. When the session settles,
ownership returns and the spline brain re-engages at the car's current pose — no teleport either way.

The guest is never given a car of its own. The field is the weekend's field, and adding one would be an
entry nobody qualified; taking one over means the guest inherits that driver's identity, livery,
championship entry and relationships for free, because they *are* that driver for the session.

The handover itself is `TeamSwitchController`'s single-player mechanic split across two machines —
`HandToHuman` / `HandBackToAI` on the host, `TakeControl` / `SilenceBrains` on the guest.

Signalling is `NetworkedAICar.OnGainedOwnership` / `OnLostOwnership`, **not** a broadcast message. NGO
raises those on each peer's own copy of the object, which is the one moment that copy is certain to exist
locally; a message announcing the handover can outrun the spawn and land on nothing.

`PitLaneStart` also blocks a guest from getting into the scene's player car — that car is the host's entry,
and the guest's copy of it is scene content, not a second entry.

## Phase 4 — polish

Guest name plates, a join/leave toast, clean drop-out (possessed car reverts to AI at its current pose),
host migration explicitly **not** supported — the guest returns to the title screen if the host leaves.

---

## Order of work

1. ~~**Phase 1** — mode model, ledger export/import, `CareerMirror`, `CoopScene`, launcher entry points.~~ done
2. ~~**Phase 2** — guest avatar, paddock gates, recall.~~ done
3. ~~**Phase 3** — networked career field, possession.~~ done
4. **Phase 4** — polish.

Flip the seventeen `GameSession` gates one at a time, paddock last.
