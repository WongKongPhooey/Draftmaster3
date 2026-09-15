# Legacy scrolling system (Phoenix scene)

Simulates racing through a **visual illusion of movement** — the player car stays near screen centre
while environment objects scroll past via shader texture offsets and transform positioning. There is
no 3D track geometry.

1. **VehicleLogic** updates each car's speed, `locationOnTrack` and turn logic every FixedUpdate.
2. The player vehicle publishes speed to **RaceManager** (`playerSpeedMetres`, `motionOffset`,
   `playerLocation`).
3. **EnvironmentObjectV2** reads `RaceManager.playerLocation` for visibility, positioning and shader
   scroll offset (`_MotionOffset`).
4. **EnvironmentManager** shifts the environment root on X via `RaceManager.playerXShift` to keep the
   player centred.
5. **CameraManager** follows via Cinemachine and applies Dutch rotation for banking through turns;
   also owns zoom and the FPS cap.

Environment objects have a lifecycle: invisible → slide in (100m before start) → shader scrolling →
slide out (100m after end) → invisible, bounded by `specificStartLocation` / `specificEndLocation`.
Scrollable objects derive `_MotionOffset` deterministically from `RaceManager.playerLocation /
scrollDivisor`, the divisor computed from the material name ("ScrollingMaterial512" = 512px) as
`(pixelSize / 512f) * 40f`, or from a per-object `scrollSpeedOverride`. Non-scrollable objects are
positioned purely by transform offset from `playerLocation`, have no visibility lifecycle, and are
always visible.

Other scripts: **InputManager** (action maps InCar/OnFoot, direction vector), **MovementOnFoot**
(Rigidbody2D on-foot movement). VehicleLogic also handles drafting and wrecking.

**Player switching.** `setAsPlayer()` on VehicleLogic or MovementOnFoot switches between vehicles and
an on-foot character mid-race. `RaceManager.setPlayer()` is the central switch point — it updates
`playerLocation` immediately, resets all EnvironmentObjectV2 states, updates the camera and sets the
Cinemachine follow target. Input maps switch separately via `InputManager.ChangeInputMap`
("InCar" vs "OnFoot").

**Vehicle & track data.** TrackInfo and VehicleInfo ScriptableObjects in `Assets/ScriptableObjects/`,
instances in `Resources/Tracks/` and `Resources/Vehicles/`. TrackInfo defines turn positions, lengths,
angles, steering angles, racing lines (lowest/ideal/highest), pit lane curves and speed profiles.
VehicleInfo defines accel/decel AnimationCurves and top speed. VehicleLogic calculates dynamic racing
lines per turn from a random high/mid/low selection, with speed curves derived from the chosen line
and track banking.

Many scripts in `Assets/Scripts/` are from a previous iteration (`Movement.cs`, `CameraRotate.cs`,
`EnviroMovement.cs`, `EnvironmentObject.cs`). They may still be referenced by old scene objects or
hold static fields read elsewhere — check references before removing.

---

This is the previous iteration. The active system is spline-based — see `CLAUDE.md`
and `Docs/Tracks.md`. This file exists so the detail stays available without being loaded
into every session.
