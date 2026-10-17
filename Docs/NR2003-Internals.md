# NR2003 internals: what the binary says

Findings from decompiling `NR2003.exe` (NASCAR Racing 2003 Season, Papyrus, build of 11 Oct 2003) with
Ghidra 12.1.4 through REA, plus the shipped `papy_ai.ini`, `track.ini` and `.lp` files. This replaces
guesswork in `Docs/AI-NR2003.md` ("Papyrus never published the internals") with what the code actually
does, and ends with how to bring each part into Draftmaster 3.

**Confidence key.** *Observed*: read directly from decompiled code or data. *Inferred*: the most likely
meaning of observed code, named by us (the binary has no symbols). *Unknown*: not settled. Function
names like `FUN_00530110` are Ghidra's, keyed by address, so they can be re-opened later (see
[Reproducing](#reproducing-this-work)).

## The short version

1. **The AI is not a physics car.** AI cars are kinematic: each tick they advance along the track in
   track coordinates (`dlong` metres along, `dlat` metres sideways, + is left) under a traction-circle
   limit. Only the player runs the full tyre model. *Observed* (`aiAdvanceCarOnLine` named in
   `papy_ai.ini`; `FUN_0051e2a0` integrates dlong/dlat accelerations against a circle).
2. **Lateral choice is an argmax over a 1-D "acceleration field".** Every tick the road is cut into
   ~160 lateral slots. Each slot holds the acceleration the car could achieve if it drove there. Free
   road scores by how fast the line lets it go. A slot behind another car is capped at "the
   acceleration that keeps me at drafting distance behind it", plus a draft bonus. The car picks the
   best slot it can reach, paying a cost per slot it moves and a penalty for slots it would hit within
   0.5 s. **Passing is not a decision. It falls out of that argmax.** *Observed*
   (`FUN_00533830`, `FUN_00533ea0`, `FUN_00534050`).
3. **A car alongside is a wall, not a force.** When another car overlaps alongside, its predicted
   lateral position 0.5 s ahead, plus both half-widths, becomes a hard bound on that side. Nothing
   pushes. The planner simply cannot pick slots past it. This is NR2003's famous lane holding.
   *Observed* (`FUN_00530ca0` writes `+0x1bc1` / `+0x1bb5`).
4. **Each nearby car gets a relationship with a state machine**: up to 8 slots, 7 relationship types,
   11 sub-states (several with left/right halves), and timers that give hysteresis ("give up the pass
   after 3.75 s", "switch sides only if the other is 3x better"). *Observed* (jump table at
   `0x74bdec`).
5. **Following is a gap controller, not a headway rule**: kinematic braking when far, PD on the gap
   error when close, feed-forward of the leader's acceleration, and a randomised panic brake as the
   floor. *Observed* (`FUN_0052acd0`).
6. **Draft strength scales with (v / v_top)^10.** It decays linearly to zero at 1.5 s of gap. It is
   nothing at Martinsville and decisive at Daytona, from one formula. *Observed* (`FUN_00530ca0`
   tail).
7. **The track author shapes the racing.** `race/minrace/maxrace.lp` lines are pre-processed into a
   per-point lane table: room to pass on each side, braking-zone follow padding, and v²-interpolated
   speed between lines. `track.ini` has about 30 knobs per track. *Observed* (`FUN_00526b90`).

## Units and constants

The sim runs at **36 ticks per second**. `papy_ai.ini` states `1G = 0.007562` m/tick², and
√(0.007562 / 9.81) = 1/36 s. Every tick count below converts at 36 per second.

| Value in code | Meaning | Real units |
|---|---|---|
| `0.007562` | 1 g | m/tick² |
| `5.02524` | car length | m (16.5 ft) |
| `2.51262` | half car length | m |
| `1.9035` | car width | m (6.25 ft) |
| `0.95175` | half car width | m |
| `2.85525` | lateral room needed beside a line to count as a lane | m (1.5 car widths) |
| `6.02524` | "clear" gap: car length + 1 m | m |
| `6.0` | clear-ahead threshold in the relationship predicates | m |
| `3.0` | `.lp` point spacing along the centreline | m |
| `18` ticks | lateral prediction horizon | 0.5 s |
| `54` ticks | look-ahead range per unit speed; draft fade-out | 1.5 s |
| `108` ticks | speed-plan horizon | 3 s |
| `126` / `135` ticks | pass-attempt timers | 3.5 s / 3.75 s |
| `288` ticks | time-to-contact cap ("never") | 8 s |
| `50` m / `350` m | min look-ahead / max look-behind range | m |

Speeds inside the AI are m/tick: multiply by 36 for m/s, or by 80.53 for mph. `0.248` m/tick ≈ 20 mph,
`0.062` ≈ 5 mph, `0.9306` ≈ 75 mph.

## Data the game ships

### `.lp` racing lines

Header `<u16 a, u16 b, u32 n>`, then `n` records of 6 floats, **one record per 3 m of centreline**
(Daytona: 1342 × 3 = 4026 m = 2.50 mi). *Observed* from the files. The meaning of each field comes
from how the code uses it:

| Field | Meaning | Evidence |
|---|---|---|
| 0 | **Speed ceiling** at this point, m/tick | Daytona 184 mph flat (plate); Martinsville 49 to 64 mph; on straights at most tracks it is a huge "no limit" number (Atlanta 299, Talladega 413 mph), so the engine and drag set the speed there |
| 1 | lateral slope (d dlat / d step) | small, sign follows the line's drift |
| 2 | **dlat**, metres from centre, + = left | `FUN_00526b90` subtracts these between lines to get lane width |
| 3 | 0 in every file seen | |
| 4 | arc length of the line over this 3 m step | 3.00 on straights, ~2.92 on the inside of turns |
| 5 | 0 | |

In memory the record grows to 7 floats (`0x1c` bytes) with derived data (curvature, compared against
`±0.0018905`). The files are `race.lp` (the line), `minrace.lp` / `maxrace.lp` (inner and outer
passing-lane edges), `minpanic` / `maxpanic`, and the pit lines.

**Correction to `Docs/AI-NR2003.md`:** the `.lp` speed is a *ceiling*, not a target the AI aims for
everywhere. That explains the "299 mph at Atlanta" puzzle.

### `papy_ai.ini` and `track.ini`

Every tunable is in one table in the exe at `0x74aa78`: 24-byte entries `{section*, name*, value,
default, 0, …}`, 144 parameters. *Observed*. The defaults in the exe differ from the shipped ini in
places, and the ini or `track.ini` wins. AI-relevant `[ai_track]` parameters, with what the code does
with them:

| Parameter | Exe default | Range over 26 tracks | What the code does with it |
|---|---|---|---|
| `ai_drafting_distance` | 1.4 | 1.07 to 1.55 | Follow gap = **car lengths** (× 5.025 m). Sets the desired gap in the follow controller and the cap on slots behind a car. |
| `ai_pacing_distance` | 2.0 | 2.0 | Car lengths behind while pacing. |
| `ai_bunching_distance` | 1.7 | 1.7 | Car lengths behind while bunching for the start. |
| `ai_dlongpad_scale` | 1000 | 1.5 to 1000 | Grows the follow gap in braking zones: `gap = L·(1 + (draft−1)·pad)`, with `pad = max(1, (scale·g − decel)/(scale·g))`. Small values mean a much bigger gap under heavy braking. |
| `ai_dlat_pad` | 2.0 | 0.7 to 1.3 | Lateral margin around other cars. It is multiplied by a crowding factor (below). |
| `ai_squeeze_pcnt` | 0.6 | 0 (one track 0.1) | Moves the lane when it narrows. Off almost everywhere. |
| `ai_panic_decel` | 1.8 | 1.8 to 6.0 | g. The resolver raises a "trouble" flag when the best slot needs more braking than this. |
| `ai_offset_long_lookahead` / `_lat_lookahead` | 10.8 / 10.8 | (not in track.ini) | Ticks of look-ahead used to measure lane room left/right: `room(dlong + v·10.8, dlat + vlat·10.8)`. |
| `ai_inverse_slipcurve_k` | 0.32 | 0.21 to 0.80 | Slip angle the AI needs to corner. A lower value means more slip and more drag. |
| `ai_grip_modifier` / `ai_drag_modifier` | 1.0 | 0.975 to 1.15 / 1.03 to 1.12 | Multiply the AI traction circle and drag. |
| `ai_accel_modifier` / `ai_decel_modifier` | 0.94 | 1.0 | Scale throttle and brake grip. |

`[physics]` holds the spring-dampers the kinematic car uses (exe defaults, per tick):
`dlat_accel_k1/k2 = 0.0070 / 0.09` (lateral tracking: ω ≈ 3.0 rad/s, ζ ≈ 0.54, a 2.1 s natural
period), `dlong_accel_to_sep_k1/k2 = 0.009 / 0.01`, `yaw_accel_k1/k2 = 0.02 / 0.2`,
`alt_accel_k1/k2 = 0.085 / 0.28`. `inertial_factor = 0.1`.

`[ASC]` (rubber-banding, from the ini comments; code not traced): every 180 ticks (5 s) adjust each AI
car's strength from its time gap `dt` and closing rate `ddt` to the player, with cubic terms
(`kdt3_ahead/behind`, `kddt3_fast/slow`). The step is limited to −0.04 / +0.08 strength per second and
the range clamped to 0.5 to 1.3, with a pull back toward base strength (`base_bias_low/high`).

## The per-tick AI pipeline

`FUN_0052b930` runs once per AI car per tick. *Observed*; the step names are ours:

```
brain(car):
  1. mode switch       car.mode (vtable at +0x13b3: racing / pacing / pitting / ...) swapped if requested
  2. scan neighbours   walk the position-sorted car list forward (<= 6 cars, until gap > max(50 m, v * 54 ticks))
                       and backward (<= 4 cars, within 350 m); assign each to one of 8 relationship slots
  3. relation update   8x FUN_00533540 -> state machine [type][subState] (transitions, see below)
  4. build field       FUN_00533830: per lateral slot, the acceleration achievable there; preferred-slot cost slope
  5. stamp relations   8x FUN_00530ca0: each car's predicted lateral band, time to contact, follow accel,
                       draft bonus -> FUN_00533ea0 writes them into the field; alongside cars become walls
  6. resolve           FUN_00534050: best reachable slot between the walls -> lateral target + accel limit
  7. lateral bounds    write min/max dlat (+0x1b95 / +0x1b99) for the lateral spring-damper
  8. crowding pad      n = active relations;  latPad = base + 0.5 * n^2 / 64   (more neighbours -> more room)
  9. trouble flag      +0x16b7 / +0x16b8: off-line or needs > panic decel
```

`FUN_0052b520` is a second, simpler pass (pacing / caution mode). It runs the relationships through
`FUN_00530110`, which turns each relationship into speed caps (`+0x1b75`, `+0x1b79`) and lateral bounds
using the pacing and drafting distances, then fits the car between them.

Car fields used throughout (*inferred* from use): `+0x186a` dlong (m), `+0x186e` index of the current
`.lp` point (`dlong / 3`), `+0x1886` dlat, `+0x188a` lateral velocity, `+0x1872` speed (m/tick),
`+0x1876` longitudinal accel, `+0x1ac2` path-length scale at the car's lateral position (inside of a
turn is shorter), `+0x1afa` drag / draft coefficient, `+0x18f2` car width, `+0x185e` top speed.

### Distances along the track are scaled by lateral position

Every gap uses `((scale_a + scale_b)/2) · Δdlong` when the cars are within 20 m, blends linearly to the
along-the-line distance by 150 m, and subtracts a car length. Two cars side by side in a turn are
therefore compared on the arc they actually drive. *Observed* (`FUN_00530ca0`, `FUN_0052acd0`).

## Relationships: a state machine per neighbour

Each of the 8 slots (`0x5d` bytes, starting at car `+0x13d3`) holds `{other car, type, subState,
prevSubState, time-to-contact (ticks), meet dlong, lateral band [min,max], timer, flags, commitment}`.
Transitions go through `FUN_005316e0(type, sub, other)`. Sub-states come in left/right pairs: the odd
one means the other car is on my right, chosen by `FUN_00531820`. *Observed* transitions; the type
names are *inferred*:

| Type | Our name | Sub-states and transitions seen |
|---|---|---|
| 1 | **Following** (car ahead, not worth passing) | 0 far → 5/6 close on a side; becomes type 4 once it is overlapping; dropped when out of range |
| 2 | **Hunting** (car ahead I'm faster than) | 0 → 3/4 "set up the pass on a side" (side picked by lane room) → 5/6 close → 7/8 nosed alongside → type 3 when clear by 6 m. **Gives up and reverts after 135 ticks (3.75 s)** in set-up. Reverts to type 1 if no longer faster. |
| 3 | **Passed** (car now behind me) | 0, 7/8 alongside; clear when 6 m ahead |
| 4 | **Alongside / deciding** | 0..2, 7/8, 9/10. 9/10 (`FUN_00532290`, the biggest handler) scores both sides: blocked by a third car, time-to-clear (quadratic in the closing acceleration), room before the lane ends. **It only swaps sides if the new side is 3x better** (1.75x in one mode). |
| 5 | (*unknown*: likely being passed / defending) | 0 and 7/8, same shape as type 3 |
| 6 | **Drafting / pushing** | 0, 7/8, 9/10; the draft bonus is a constant here and records the previous side |
| 7 | reserved | all handlers are no-ops |

Two predicates drive most transitions:

- **"Worth passing"** (`FUN_0052a860`): my achievable speed at *its* position must beat its current
  speed by a margin. The margin is 0.248 m/tick (~20 mph), cut by half of how far I am below my own
  line speed, floor 0.062 (~5 mph). So a car that is *already* going slower than its line drops the bar
  and passes more readily. Lapped or crashed cars always qualify.
- **Contact predictor** (`FUN_0052a960`): estimated time to close the gap (blending my current speed
  with my achievable speed by distance) against lateral overlap. It predicts a hit when
  `|Δdlat| < (1 − t) · width`.

`FUN_00533560` counts **how many cars are stacked beside me on one side** (2-wide, 3-wide …) by
chaining overlapping cars, and stores it in the slot (`+0x38`). Used for pack spacing.

## The lateral acceleration field

This is the heart of it, and the most transferable idea.

### Building the field (`FUN_00533830`, *observed*)

```
slots i in [-80, +79]  (lateral, relative to the race line; slot width not yet settled)
for each slot:
    vTarget(i)   = line speed interpolated in v^2 between race.lp and min/maxrace.lp at dlong + v*108 ticks
    accel(i)     = (vTarget(i) - v) / 108 * 4          // accel to reach that speed within the 3 s horizon
    accel(i)     = min(accel(i), carAccelLimit)        // traction circle / engine
    accel(i)    += prefSlope * |i - iPreferred|        // small cost away from preferred lateral
occupant(i) = none ; timeToContact(i) = 288
iPreferred  = slot of (current dlat blended 80% toward the centre of the lateral bounds)
followGap   = (1 - k) * ai_drafting_distance * L  +  k * laneTable.brakePaddedGap     // +0x1be6
```

### Stamping each neighbour (`FUN_00530ca0` → `FUN_00533ea0`, *observed*)

```
for each relationship r:
    gap        = scaled gap (above)
    ttc        = time to contact in ticks (cap 288); meetDlong = where we would meet
    latAtMeet  = other.dlat + 18 ticks * other.latVel            // 0.5 s lateral prediction
    band       = latAtMeet +- (other.width + 1.9035) / 2
    if r is alongside on my LEFT : leftWall  = min(leftWall,  band.right)    // hard bound
    if r is alongside on my RIGHT: rightWall = max(rightWall, band.left)
    followAccel = FollowController(me, other, desiredGap = ai_drafting_distance * L)
    draft      = DraftBonus(me, other)                            // peaks at the band centre
    for slot i in band:
        a = followAccel + draft * ramp(i)                         // ramp: 0 at band edge -> 1 at centre
        if a < accel(i): accel(i) = a; occupant(i) = r; timeToContact(i) = min(ttc, timeToContact(i))
```

The effect: driving *into* the band behind a slower car caps your acceleration at what keeps you a
drafting distance behind it. The draft bonus makes dead behind it the best of those slots. The slot
beside it keeps its free-road value.

### Resolving (`FUN_00534050`, *observed*)

```
clamp search to [rightWall, leftWall]
from the current slot, walk outward left and right:
    cost += lateralMoveCost per slot              // from lateral speed limits (DAT_00bdb208/20c)
    if timeToContact(slot) < 18 ticks: cost += 0.0015124 per slot beyond the first blocked one
    choose slot if accel(slot) - cost > best      // improvement must beat noise (0.5 * 0.007562)
target dlat   = chosen slot;  accelLimit = min accel along the path to it
if best accel < speedCaps - ai_panic_decel * g: car is "in trouble" (flag)
```

Why it races well:

- **The pull-out comes from the numbers.** While the field beside the car ahead is worth more than
  following it, minus the cost of getting there, the AI moves over. When it isn't (the draft bonus
  behind is big, or the side is walled, or the time to contact is short), it stays. No
  `quicker && gap < initiateRange` rule is needed.
- **It is continuous.** Nothing flips between "following" and "passing". The target moves one slot at
  a time and the lateral spring-damper smooths it.
- **3-wide is natural.** Each car alongside is a wall. The middle car of three has two walls and only
  slots between them.
- **It is cheap.** About 160 floats, 8 stamps and one scan per car per tick.

## Following: the gap controller (`FUN_0052acd0`, *observed*)

Returns a longitudinal acceleration for holding `desiredGap` behind `other`.
`e = scaledGap − desiredGap` and `vc = v_me − v_other` (the closing speed):

```
if e > 10 m:     a = -vc^2 / (2 e)                     // brake so speeds match exactly as the gap closes
                 (if not closing: free; if a would exceed the cap -> no constraint)
elif e >= 0:     a = blend(leaderAccel feed-forward) + Kp * g * e - Kd * vc
else (too close) a = Kff * other.accel + (Kp * w * g * e - w * Kd * vc),   w = 1 + depth^2
floor: if a < -3 g  ->  a = -5 g + rand * 1 g          // panic brake, randomised
```

| Variant | Desired gap | Kp | Kd | Kff | Used for |
|---|---|---|---|---|---|
| normal (`0052ac80`) | `ai_drafting_distance × L` | 0.10 | 0.25 | 1.0 | following in traffic |
| pack (`0052b290`) | car's own gap × 1.1 | 0.143 | 0.333 | 1.0 | drafting lines; in one race mode the gap tightens with the number of cars alongside (`n × 0.125`) |

The gap grows in braking zones through the lane table's padded gap (`ai_dlongpad_scale`). That is the
single biggest reason NR2003 packs don't stack up under braking.

## Drafting (`FUN_00530ca0` tail, *observed*)

For a car directly ahead (relationship types 4 and 6):

```
r     = (36 / v_top) * v_other                 // speed as a fraction of top speed
dv    = v_other - v_me,  clamped: if dv > 0.124 then dv = 0.248 - dv
draft = -2 * dragCoeff_me * r^10 * (1 + 4 r^2 dv) * (1 - gap / (r^3 * v_other * 54 ticks))
```

- **`r^10`**: at 50% of top speed the draft is 0.1% of full, at 80% it is 11%, at 95% it is 60%.
  It needs no per-track draft switch.
- **It fades linearly to nothing at `1.5 s × r³` of gap**, measured in time, so faster tracks have longer
  tails, and near top speed the tail is the full 1.5 s.
- Type 6 (pushing) gets a constant −0.015 m/tick² instead. Being pushed is a flat bonus, not
  distance-shaped.

## Lane room from the lines (`FUN_00526b90`, *observed*)

At load, for each 3 m point, from `race`, `maxrace` and `minrace`:

- lane width = `max.dlat − min.dlat` (floor 1 m), and where the race line sits within it;
- speed at any lateral position = **v² interpolation** between the race line and the edge line;
- **room-to-pass flags**: left if `max.dlat > race.dlat + 2.85525`, right if
  `min.dlat < race.dlat − 2.85525`. For every point, the index of the next point where each side's room
  ends. The side-by-side handler uses this to ask "will I clear before the lane closes?";
- **braking-zone padding** (`ai_dlongpad_scale`) → padded follow gap per point;
- `ai_squeeze_pcnt` moves the lane when it narrows.

## The AI car itself (`FUN_0051e2a0`, *observed*)

- **Traction circle** of radius `circle × grip`. Throttle available =
  `circle·√(1 − (a_lat/circle)²)`. **Braking available = `circle·(1 − (a_lat/circle)²)`**, a
  parabola rather than a circle, so braking while cornering is limited much more than throttle. Over
  the circle, both components are projected back onto it, plus slip drag when fast.
- Engine accel = power curve × tyre-condition and suspension-condition scaling,
  minus `(drag + engine) × inertial_factor`. Wheelspin cuts grip down to `wheelspin_min_adj_coeff`.
- **Mistakes**: a flag arms a random 9 to 35 tick window (0.25 to 1 s) where braking is only 75%. This
  is the consistency mechanism: occasional long stops, not random steering. Mechanical failures follow
  `mechanical_failure_*` in the ini.
- Yaw is a spring-damper toward a target slip angle (`ai_inverse_slipcurve_k`) for the look of the car,
  with dropoff above `yaw_accel_control_angle`.

## Ratings (from `papy_ai.ini [driver]`, mapping not traced in code)

Strength feeds three car properties with spreads: traction circle (×1.0, spread 8), aero drag (×0.3,
spread 4), engine power (×0.2, spread 4). Track-type ratings add up to 3% traction circle; qualifying
adds 2% traction, 3% drag, 1.5% power. Consistency (0.95 base) drives the deviations of
**commitment** (0.03), **dlong lookahead** (0.4), **dlat lookahead** (0.75) and **left offset** (0.08).
A low-consistency driver looks ahead a varying distance, so it sometimes reacts late. That is where its
mistakes come from.

## What this means for Draftmaster 3

Mapped to the open problems in `Docs/AI-NR2003.md` and to the code in `Assets/Scripts/AI/`.
Recommended order (item 1 alone was measured and loses; see its note, and do it as part of item 2):

### 1. Replace side repulsion with walls (fixes the side-by-side contacts)

`AIRacingBehaviour` builds `desiredTactical` and then adds `repulse` (`sidewaysMaxPush`,
`contactPush`, lines ~690 to 714). The 2026-10-13 notes show every extra sideways shove in a 24-car
start turning into rear-ends behind it. NR2003 never pushes. A car alongside clamps the lateral bounds.

```csharp
// Per AI car, per FixedUpdate, before choosing desiredTactical.
// Cars "alongside" = longitudinal overlap within half length + margin.
float leftWall = laneMaxLat, rightWall = laneMinLat;            // from SplineDriver.GetLateralBounds
foreach (var o in Neighbours)
{
    if (Mathf.Abs(o.ScaledGap) > HalfLen * 2f + 1f) continue;   // not alongside
    float oLat = o.Lateral + o.LateralVelocity * 0.5f;          // NR2003: 18 ticks = 0.5 s prediction
    float half = (o.Width + MyWidth) * 0.5f + latPad;
    if (oLat > myLat) leftWall  = Mathf.Min(leftWall,  oLat - half);
    else              rightWall = Mathf.Max(rightWall, oLat + half);
}
desiredLat = Mathf.Clamp(desiredLat, rightWall, leftWall);       // a wall, never a force
```

`latPad` should grow with crowding: `basePad + 0.5f * n * n / 64f` for `n` cars tracked nearby.

**Tried on its own 2026-10-17, and it is a dead end.** Walls clamped onto the existing `desiredTactical`, in
place of the soft push (`sideWalls`, since reverted; only `SplineDriver.LateralRate` was kept). Pack sim,
push → walls:

| Sim (rosters) | Contacts | HARD | Passes |
|---|---|---|---|
| WG start (5) | 4.8 → 5.8 | 1.4 → 4.4 | - |
| WG racing (4) | 7.5 → 12.8 | 4.3 → 7.3 | 4.3 → 3.8 |
| Daytona (3) | 245 → 1106 | 28 → 300 | 832 → 1545 |

At WG, no prediction (0 s) and a 1.7 m gap were no better (HARD 5.4 to 7.0), though 1.7 m doubled passes to
8.0. Walls only work inside NR2003's field planner. There, the slots a wall leaves are also capped by the
follow acceleration, so a walled car slots in behind instead of being clamped to a spot between lanes,
diagonally into the cars ahead and behind. Our lane snapping and `PackLanes` choose positions the wall then
truncates. **Build item 2 first and put the walls inside it.**

### 2. The lateral acceleration field (replaces initiateRange / quicker / commit timers)

New pure class in `Draftmaster.Sim` (testable without Unity):

```csharp
public sealed class LateralField
{
    public const int Slots = 41;              // e.g. 0.25 m slots across a 10 m lane
    readonly float[] accel = new float[Slots];
    readonly float[] ttc   = new float[Slots];

    public void Build(Func<float, float> lineSpeedAtLat, float v, float horizon, float accelCap,
                      int preferred, float prefSlope)
    {
        for (int i = 0; i < Slots; i++)
        {
            float vt = lineSpeedAtLat(SlotLat(i));                  // v^2-interpolated between lines
            accel[i] = Mathf.Min((vt - v) / horizon, accelCap) - prefSlope * Mathf.Abs(i - preferred);
            ttc[i] = float.MaxValue;
        }
    }

    public void Stamp(float latMin, float latMax, float followAccel, float draftBonus, float timeToContact)
    {
        int a = SlotOf(latMin), b = SlotOf(latMax), mid = (a + b) / 2;
        for (int i = a; i <= b; i++)
        {
            float ramp = 1f - Mathf.Abs(i - mid) / (float)Mathf.Max(1, (b - a) / 2);
            float v = followAccel + draftBonus * ramp;
            if (v < accel[i]) accel[i] = v;
            ttc[i] = Mathf.Min(ttc[i], timeToContact);
        }
    }

    public int Resolve(int current, int rightWall, int leftWall, float moveCost, float blockedCost)
    {
        int best = current; float bestScore = accel[current];
        for (int dir = -1; dir <= 1; dir += 2)
        {
            float cost = 0f; bool blocked = false;
            for (int i = current + dir; i >= rightWall && i <= leftWall; i += dir)
            {
                cost += moveCost;
                if (ttc[i] < 0.5f) { if (blocked) cost += blockedCost; blocked = true; }
                if (accel[i] - cost > bestScore + 0.5f * 9.81f / 36f) { bestScore = accel[i] - cost; best = i; }
            }
        }
        return best;
    }
}
```

Wire it into `AIRacingBehaviour` as the source of `desiredTactical` when `useLanes` is on, keeping the
existing lane snapping. `followAccel` comes from item 3, `draftBonus` from item 4, and the
"line speed at a lateral" from `SplineDriver`'s speed profile with the min/max lines.
**PackRaceSimTests** (`DaytonaBaseline`, `WatkinsGlenStart`, `WatkinsGlenRacing`) are the gate: passes
should hold or rise while hard contacts fall.

### 3. Follow with a gap controller, padded in braking zones

Replace the `followHeadwaySeconds × speed` cap with NR2003's controller: kinematic `−vc² / (2e)` beyond
10 m of gap error, PD (Kp ≈ 0.10 g/m, Kd ≈ 0.25 per tick → ~9 /s) plus leader-accel feed-forward
inside it, and a `TrackTuning.draftFollowScale` in **car lengths** (`ai_drafting_distance`, 1.07 to
1.55). Grow the gap with braking (`gap = L·(1 + (draft − 1)·pad)`): this is the missing piece behind
"pace-lap and braking-zone pile-ups" in `formation-and-pit-merge-gotchas`.

### 4. Draft as `(v / vTop)^10`, faded by time

`DraftAero` can take NR2003's shape directly: strength ∝ `(v/vTop)^10`, linear fade to zero at 1.5 s of
gap (`gap / (v · 1.5)`), and a flat bonus when pushed. This lets one draft model serve every track type
without `TrackTuning.draftScale` doing the work.

### 5. A relationship per neighbour, with timers

Keep a small fixed array (8) of `{other, type, sub, timer}` per AI car, updated by the transitions in
the table above. The lessons that carry over directly:

- a pass set-up **expires after ~3.75 s** if it hasn't got alongside;
- once alongside, **switch sides only for a 3x better option**;
- "worth passing" = my achievable speed at *its* spot beats its speed by ~20 mph, less when it is
  already slow;
- "clear" means 6 m (a car length plus 1 m) and nothing more.

This is the fix for "a car pulling out of line while being pushed": the pusher has a type-6
relationship with the leader. When the leader's field changes, the pusher's stamp moves with it in the
same tick, because both read the same predicted lateral band.

### 6. Mistakes as brake fade, not noise

Swap random per-second mistake rolls for NR2003's model: a short window (0.25 to 1 s) of 75% braking,
armed at a rate set by Consistency, plus Consistency-scaled variation in look-ahead distance. This keeps
the car on its line (no random swerves into neighbours) and still produces late-braking contact and
spins.

### 7. Smaller things worth taking

- Brake limit in a turn as `(1 − (lat/grip)²)` (parabola) and throttle as the circle.
- Compare cars on the arc they actually drive (`scale × Δdistance`) when within 20 m.
- Look ahead `max(50 m, 1.5 s × speed)` forward and 350 m back, at most 6 cars ahead and 4 behind.
- ASC-style rubber-banding, if ever wanted, is bounded (±0.04 to 0.08 strength per second, 0.5 to 1.3)
  and leans back to the driver's base.

## Unknowns

- Slot width of the lateral field (`FUN_005337e0` truncates with a scale we didn't trace). Default
  bounds are −80 to +79.
- Relationship type 5's role; types 1/3 sub-states 1 and 2.
- Exact feed-forward gains: some args to `FUN_0052acd0` sit in registers Ghidra lost. The table above
  uses the constants passed at the call sites.
- ASC code path; the strength → traction/drag/power mapping code (taken from the ini instead).
- The player's full physics model (not needed for AI work).

## Reproducing this work

Everything lives in `C:\Users\Josh\Documents\REA\nr2003\` (outside this repo; the game files are
abandonware and must not be committed here):

| Path | What |
|---|---|
| `nocd/NR2003.exe` | the analysed binary (SHA-256 `abe08ca8…6a2f6`) |
| `game/` | extracted game files (`papy_ai.ini`, `tracks/*/track.ini`, `*.lp`) |
| `proj/` | saved Ghidra project with analysis |
| `decomp/ai/`, `decomp/ai_ann/` | decompiled C for `0x500000` to `0x540000`; `_ann` substitutes constants and parameter names |
| `ghidra_scripts/DumpRange.java`, `DecompAt.java` | headless dump / decompile-at-address scripts |
| `tools/decomp_at.sh <addr>…` | decompile more functions into `decomp/ai_ann` |
| `tools/annotate.py`, `lp.py`, `isx.py`, `tracks.py` | constant annotator, `.lp` reader, installer extractor, track.ini table |

Key addresses: per-tick brain `0x52b930`, pacing pass `0x52b520`, relationship state machine
dispatch `0x533540` (table `0x74bdec`), field build `0x533830`, stamp `0x530ca0` / `0x533ea0`, resolve
`0x534050`, follow controller `0x52acd0`, lane table `0x526b90`, AI car accel `0x51e2a0`, parameter
table `0x74aa78`.
