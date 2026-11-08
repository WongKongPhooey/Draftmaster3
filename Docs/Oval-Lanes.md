# Oval lanes (oval AI rework, stages 1-3)

The oval AI is being rebuilt in three stages (road courses are not touched by any of them):

1. **Two lanes, and perfect lane following** - this page.
2. **A single-file train in each lane: smooth back-off, no accordion** - this page, [Stage 2](#stage-2-trains).
3. **Passing by switching lanes when there is a gap and a run** - this page, [Stage 3](#stage-3-passing-by-lane-change).

Stage 1 is the foundation: an AI car told to hold a lane holds it, lap after lap, without drifting, running wide
or hitting anything. Test bed: Texas Motor Speedway (`FortWorth`).

## The two lanes

`Draftmaster.Tracks.OvalLanes` (`Assets/Scripts/Tracks/Core/OvalLanes.cs`, pure, no scene) lays two fixed lines
round any oval, as lateral offsets off the track centreline, from the road's own width at every centreline
sample. Nothing is authored per track.

| | Where | Default (`OvalLanes.Default`) |
|---|---|---|
| **Inside lane** | the bottom groove: `insideEdgeMargin` up from the inside edge of the racing surface, so the inside tyres stay on the banking, clear of the apron and pit road | 1.8 m (half a 2.0 m Cup car + 0.8 m) |
| **Outside lane** | `laneSpacing` above the inside lane - a car width plus clearance - never closer than `outsideEdgeMargin` to the outside edge | 3.2 m (2.0 m car + 1.2 m between doors); 1.8 m from the edge, so 1.8 m from a generated oval's outside wall (1 m past the edge) |

- **Which side is the infield:** the lap's net turning (`OvalLanes.InsideSide`); anticlockwise = left.
- **Width changes** (Texas runs 15.5 m on the front stretch, 33 m in Turns 1-2): the width is averaged over
  ±40 m (`smoothingMetres`) first, so a step in the road becomes a drift in the lane, not a kink.
- **Narrow road:** the outside lane gives way first (squeezed toward the inside lane); a road too narrow for one
  lane puts both on the centreline.
- **Which tracks:** `OvalLanes.AppliesTo(id)` = the id is in `TrackDimensions` and its kind is not
  `RoadCourse`. An unknown id (test track, hand-built scene) never gets lanes. (Not `TrackCatalog.TypeOf`: that
  answers `Speedway` for an unknown id.)

`TrackBuilder.TryGetOvalLanes(samples, out inside, out outside)` is the one entry point; the AI, the gizmo and
the overlay all go through it, so they always agree.

### Seeing them

- **Scene view:** `TrackBuilder` draws the inside lane cyan and the outside lane magenta on an oval
  (`drawOvalLanesGizmo`, `insideLaneColor`, `outsideLaneColor`), alongside the existing racing-line gizmo.
- **In game:** `OvalLaneOverlay` (self-bootstrapping like `RacingLineDisplay`) draws the same two lines on the
  road. Pause menu (`Esc`) > **Oval lanes** toggles it; the row only appears on an oval. Persisted in
  PlayerPrefs `ShowOvalLanes`, default on.

## Following a lane

### The brain: `SplineDriver.ovalLane`

`ovalLane` (`None` / `Inside` / `Outside`, default `None`). When set on an oval, `LaneFollowing` is true and:

- The car's line **is** the lane: it replaces the trained / authored / optimised line and `lineFactor`, and it
  is not relaxed (relaxing a constant offset round a bend pulls it toward the inside).
- The bounds open to the road edges (`ClampToRoad`), so nothing clamps the car off its lane - the authored
  leftmost/rightmost band can sit inside the bottom groove.
- `tacticalLateralOffset` is ignored on the main track (`TacticalLateral`). Moving across is stage 3's job, and
  it will do it by changing the assigned lane.
- **Speed is planned off the lane.** The curvature profile is built from the line the car drives, so the corner
  speeds, braking zones and the grip governor's radius all come from the lane's own radius - an outside-lane car
  plans the outside line's speed and goes round on the outside, rather than drifting down to where the speed
  plan was made.
- Changing `ovalLane` at run time re-lays the line, curvature and speed plan on the next step. (The aim point
  jumps 3.2 m sideways when it does. A pass does not set it this way: stage 3's `StepLaneChange` blends the line
  across, see [Stage 3](#stage-3-passing-by-lane-change).)

On a road course `ovalLane` is ignored and `LaneFollowing` stays false (`OvalLaneTests.ARoadCourseIgnoresAnAssignedLane`).

### The hands: `SplineInputDriver` lane feed-forward

The existing controller (pure pursuit + yaw damping + slip catch) held the lane to a mean of 0.27 m, but the
error had a shape: through both long turns the car sat a steady **0.33 m wide**, in either lane. That is a
steady-state bias - the heading loop only steers when it has an error to steer with, and with ~6° of body slip
in a turn at the limit the error it settles on is the car running wide.

So, only while `LaneFollowing` (never on a road course), the steering adds a feed-forward toward the inside of
the bend in proportion to the lateral acceleration the lane asks for there:

    laneFfDeg = gain x (lane curvature x speed²) x (bend direction)

with the curvature read `laneFeedForwardLeadTime` (0.2 s) ahead of the car. The gain starts at
`laneFeedForwardInitial` (0.08 deg per m/s² - the bias measured at Texas was ~1.7° of wheel at 19 m/s²) and
**learns**: in a settled bend (no slide, not just hit, over 15 m/s) it grows while the car runs wide of its lane
and shrinks while it runs inside (`laneFeedForwardLearnRate`, ~2 s to settle, clamped to
`laneFeedForwardMax`). Whatever this car and track need - more slip, less grip, worn tyres - it settles on. On a
straight the curvature is zero, so the term is zero; it can't wind up across a straight the way a plain integral
would, and doesn't overshoot the exit.

## Texas geometry fix (start/finish seam)

The first run found a geometry bug, not an AI one. After the 2026-11-02 reshape, `FortWorth.asset`'s segments
summed to a lap that ended **4.36 m past** its own start along the front straight. `TrackBuilder` stitches a
closing piece from the end back to the start - here that piece ran *backwards*, a 4.4 m reverse with the heading
swinging through 180°. The brain read it as a 6 m-radius hairpin: every car planned 32 m/s (72 mph) across the
start/finish line, and the aim point flipping behind the car tripped the "spun" recovery on every lap.
(`BuiltTrackAssetTests.EveryBuiltTrackClosesBackOntoItsStartLine` allows 5 m, so it passed.)

Fix, through the MCP SerializedObject tools: the start moves 4.3622 m west (`startPosition` (0,0) ->
(-4.3622, 0)) and segment 0 (the front straight) shortens by the same, 220 -> 215.6378 m. Every point of the
road after the first 4.4 m is exactly where it was - only the start/finish line moved, 4.4 m down the front
straight - and the lap now closes exactly. The pit entry/exit distances were rebaked to the same physical
points (2014 -> 2009.64, 530 -> 525.64). Lap 2364.36 -> 2355.64 m.

Already failing before this and still failing, from the reshape itself (not this work): the trained racing line
`RacingLines/FortWorth.json` was trained on a 2414 m lap, so Texas drives its authored line
(`AILapSimTests.EveryTrackDrivesItsTrainedLine`); and the built lap is 1.464 mi against the published 1.5
(`BuiltTrackAssetTests.EveryBuiltTrackMeasuresItsPublishedLength`; it was 1.467 before the fix). Lane-following
cars don't use the trained line.

## Verification

`Assets/Tests/Editor/OvalLaneTests.cs` - EditMode, no play mode, ~1 s a car:

- Geometry: lane heights and spacing, a narrow road squeezing the outside lane, the infield side, oval-only
  gating, width smoothing.
- `AnAICarHoldsItsLaneAtTexas(Inside|Outside)`: one dynamic AI car (SplineDriver + PlayerVehicleController +
  SplineInputDriver, built as `AILapSimTests` builds one) alone at Texas, calibrated grip and pace
  (`AIPaceCalibration.ApplyFor("FortWorth")`), the field's top driver pace (1.04 x `AiPaceMultiplier`), a run-up
  lap then **6 timed laps**. Lateral error = the car centre's lateral against the lane's lateral at the car.
  Wall clearance is geometric against where the dressing stands the walls (outside 1 m past the edge, infield
  6 m), with a 1.0 m car half-width; "spin" = the controller's recovery mode; "off" = the lap timer's legal
  surface rule. Asserts mean < 0.3 m, max < 1 m, 0 walls, 0 spins, 0 offs. The log line also prints the signed
  error by 50 m of track.
- `SeamGeometry` (Explicit): the centreline, lanes, curvature and speed plan either side of the start/finish
  seam.

Results, 6 timed laps each (2026-11-03):

| | Inside lane | Outside lane |
|---|---|---|
| Lap times (s) | 33.12, 33.14, 33.12, 33.14, 33.14, 33.14 | 33.32, 33.32, 33.32, 33.34, 33.32, 33.32 |
| Lateral error, mean | **0.120 m** (0.108 in the turns) | **0.118 m** (0.104 in the turns) |
| Lateral error, max | **0.651 m** (275 m: turn-in to the Turn 1 dog-leg) | **0.636 m** (276 m, same place) |
| Closest to outside wall / infield wall | 13.1 m / 6.3 m | 10.0 m / 9.5 m |
| Wall hits / spins / offs | 0 / 0 / 0 | 0 / 0 / 0 |
| Max body slip | 7.1° | 7.0° |

How it got there (same test, inside lane; outside identical within 0.01 m):

| | Mean | Max | Spins | Lap |
|---|---|---|---|---|
| Lane mode, existing controller, seam bug | 0.298 m | 3.29 m (at the seam) | 7 (every lap, at the seam) | 37.2 s |
| Seam fixed | 0.272 m (turns 0.329, a steady 0.33 m wide) | 0.871 m | 0 | 33.2 s |
| + lane feed-forward | **0.120 m** | **0.651 m** | 0 | 33.1 s |

Road courses unchanged (every change is behind `LaneFollowing`, which is false off an oval and whenever
`ovalLane` is `None`): `AILapSimTests.AnAICarLapsWatkinsGlenCleanly` (all four pace/line cases) and
`TheBrainStaysWithTheCar` pass.

What is left in the error: the two dog-legs on the front stretch (20° in 50 m, no spiral into them) - the car
turns in 0.4 m wide and comes out 0.3 m inside - and ~0.25 m wide at the turn-in to the long turns. Shorter
pure-pursuit lookahead or a spiralled dog-leg would shave that; neither was needed for the targets.

## Stage 2: trains

A field on an oval runs as two trains, one per lane. Every car holds its lane (stage 1), follows exactly the line of
the car ahead (they share the lane) and never changes lanes. The only thing a car decides is how close it runs.

### The gap law: `Draftmaster.Sim.TrainFollow`

Pure (`Assets/Scripts/Sim/TrainFollow.cs`). A constant-time-headway follower:

    wanted gap = standstillGap + headwaySeconds x v           (centre to centre, v = this car's speed)
    speed      = min( vAhead + gapGain x (gap - wanted),       the linear term: settles the gap
                      vAhead + sqrt(2 x b x (gap - wanted)) )   the approach: closing planned on a lift
    b          = liftShare x what a lift sheds right now

| `TrainFollow.Spec.Default` | | |
|---|---|---|
| `standstillGap` | 7 m | a 4.8 m car and 2.2 m of air |
| `headwaySeconds` | 0.3 s | ~29.5 m centre to centre at Texas speeds, inside the 35 m tow |
| `gapGain` | 0.5 /s | overdamped with the speed loop: settles in ~2 s, no overshoot |
| `liftShare` | 0.8 | the approach plans on 80% of a lift's deceleration |
| `hardGap` | 6 m | closing inside 1.2 m nose to tail is an emergency |
| `emergencyDecel` | 4 m/s² | needing more than this to stop closing is an emergency |

- **No accordion.** The headway term is what damps the line. A gap error passed from car to car shrinks at every
  frequency while `headwaySeconds x (2 + gapGain x headwaySeconds) >= 2 x` the speed loop's lag: 0.645 s here,
  against ~0.4 s. (With no lag the car-to-car speed transfer is `(s + k) / ((1 + k h) s + k)`, never above 1.)
- **The draft.** `b` is the dynamic model's coast (`PlayerVehicleController.coastDecel`), with half of it gone in a
  full tow (`TowFactor`), the same way the physics applies it. A car closing in a tow sheds less on a lift, so the
  approach starts backing off earlier. The tow pulling it in is planned for, not found at the bumper.
- **Emergency** = still closing with less room than `emergencyDecel` can stop it in (a stopped or spinning car).

### Who runs it

- `OvalTrain` (`Assets/Scripts/AI/OvalTrain.cs`) finds the car ahead: the nearest car within half a lap that has
  the same `ovalLane` or is physically in the car's path (centres closer than 2 m across, one car width; the lanes are
  3.2 m apart). A free-driven human car in its path counts too. `OvalTrain.Spec` is the law for the whole field.
- `SplineDriver.trainFollowing` (default on) + `StepTrain()` run each step, but only while `LaneFollowing`, on the
  main track, under green. They publish `TrainActive`, `TrainCapMps`, `TrainEmergency`, `TrainGap` and
  `TrainWantedGap`. For a dynamic car the cap is **not** folded into `TargetMph`, because the input driver brakes
  to that. A kinematic car just takes the cap.
- `SplineInputDriver`: where the train allows less than the speed plan, throttle is the lower of the two asks and
  brake the higher. The train's ask is a lift first. The brake comes in only once the car is `trainBrakeMargin`
  (1 m/s) too quick, at `trainBrakeGain` (8% brake per m/s past it), and full braking only in an emergency. The
  speed plan's own braking for a corner is untouched. `LastTrainBinding` / `LastTrainBrake` are there for telemetry.
- `AIRacingBehaviour`: while its car is `LaneFollowing` with `trainFollowing` on and not under a yellow, its follow
  caps stand aside (`aiMaxSpeedMph` is left open). Those caps are built for passing and brake a line in steps. The
  tow boost stays, because it keeps a slower car hanging on in the draft. Everything under a yellow still applies.
  Mistakes still apply too: their wobble does nothing in a lane, but their pace dip does.

Road courses: `LaneFollowing` is false off an oval, so none of this runs. Watkins Glen's `AILapSimTests` (all four
cases), `TheBrainStaysWithTheCar` and `GrandstandRaceSimTests.TheDynamicWatchedField_TakesTurnOneApex_AtWatkinsGlen`
pass unchanged.

### Verification: `OvalLaneTests.TwoTrainsHoldTheirLanesAndGapsAtTexas`

EditMode, ~45 s. Texas, **2 lanes x 9 cars**, built like `GrandstandRaceSimTests.RunDynamic` (SplineDriver +
AIRacingBehaviour + PlayerVehicleController + SplineInputDriver, stepped by hand). Grip and pace are calibrated,
driver ratings are rolled the way GridSpawner rolls them, and mistakes are off.

- Each lane is ordered **slowest first**, so every follower is quicker than the car ahead and the whole line has to
  close up and hold.
- Rolling start 30 m apart, one lap to form up, then **20 timed laps**.
- On timed lap 10 each lane's leader lifts (6% off its pace for 2 s). The response is watched down the line for 20 s.
- Contacts are geometric: car boxes (4.8 x 2.0 m) in track space. Nothing steps Physics2D.
- Walls, spins, offs and lateral error are measured as in stage 1.

Three unit tests cover the law itself.

Results (2026-11-03):

| | |
|---|---|
| Car-to-car contacts / wall hits / spins / offs | **0 / 0 / 0 / 0** |
| Closest nose to tail (same lane) | 16.95 m |
| Closest door to door (cars overlapping, other lane) | 0.80 m |
| Lateral error vs lane, all 18 cars | mean **0.115 m**, max **0.707 m** (276 m, the Turn 1 dog-leg turn-in, same as solo) |
| Max body slip | 7.3° |
| Leader laps | inside 34.24 -> 34.50 s, outside 34.34 -> 34.60 s (the slowest drivers; tyres fade ~0.013 s a lap) |
| Train emergencies | **0** steps |
| Train brake | on 12.2% of follower time, mean 0.16 when on, over 20% for 4.1% of the time, max 0.51 |
| Any brake over 30% | followers 4.8% of the time, **leaders 8.6%** |

The last row means a follower lifts early and brakes less than the car leading it into the corner.

Gap to the car ahead by train position (centre to centre; error = gap minus the wanted gap at the car's speed):

| Position | Mean (m) | Min | Max | Error mean ± sd | Headway (s) mean ± sd | After the lift: peak error | Min gap |
|---|---|---|---|---|---|---|---|
| P1 | 25.2 | 21.7 | 29.6 | -1.87 ± 0.61 | 0.377 ± 0.011 | 3.65 | 21.7 |
| P2 | 25.6 | 22.2 | 31.4 | -1.47 ± 0.69 | 0.383 ± 0.013 | 3.16 | 22.3 |
| P3 | 25.4 | 22.4 | 29.5 | -1.71 ± 0.56 | 0.380 ± 0.011 | 3.06 | 22.4 |
| P4 | 25.5 | 22.4 | 29.4 | -1.64 ± 0.58 | 0.381 ± 0.011 | 2.82 | 22.6 |
| P5 | 25.5 | 22.3 | 29.6 | -1.56 ± 0.54 | 0.382 ± 0.011 | 2.81 | 22.9 |
| P6 | 25.7 | 23.0 | 32.4 | -1.45 ± 0.71 | 0.383 ± 0.013 | 2.48 | 23.4 |
| P7 | 25.7 | 22.8 | 32.9 | -1.42 ± 0.65 | 0.384 ± 0.012 | 2.50 | 23.5 |
| P8 | 25.6 | 23.0 | 30.0 | -1.48 ± 0.58 | 0.383 ± 0.011 | 2.52 | 23.5 |

- **No accordion.** The gap's wobble round the lap (sd 0.54-0.71 m, headway sd 0.011-0.013 s) is the same at the
  back of the train as at the front. The leader's lift **shrinks** down the line: 3.65 m of gap error behind the
  leader, 2.52 m at the eighth car. The test asserts the last car's peak is no bigger than the first's (+1 m).
- The trains run ~1.5-1.9 m inside the wanted gap (0.38 s against ~0.39 s wanted). That is the steady lag a
  time-headway follower carries behind a car that is accelerating for most of the lap, plus a follower in the tow
  creeping in until the lift catches it. It holds steady and does not grow.
- These were measured with the working tree's uncommitted AI changes present (`SplineDriver.driveToTarget` and
  others, not part of this work), as stage 1 was.

## Stage 3: passing by lane change

A car in a lane train may move across to the other lane to pass the car it is following. It does this when it is
held up, has a run on that car, and the other lane has room. The move is a smooth blend from one lane's line to the
other. Once landed, the car is an ordinary lane follower in its new lane. The car that was passed keeps its lane and
does nothing different: it never defends, and if the passer later pulls back in ahead of it, it backs off by the stage 2
train law.

### The rules: `Draftmaster.Sim.LaneChange`

Pure (`Assets/Scripts/Sim/LaneChange.cs`). A move **starts** only when all of these hold:

| Rule | | `LaneChange.Spec.Default` |
|---|---|---|
| **Held up** | the gap to the car ahead (centre to centre) is within `engageGap` of the train gap wanted | 8 m |
| **A run on it** | the larger of two figures, smoothed over `advantageSeconds`, is at least `minAdvantage`. The first is the car's own speed plan at the car ahead's position (pace, plus any tow boost) minus that car's own plan there; this is the pace edge, and it has no bias because both sides are plans. The second is the closing speed (a run off the corner, a draft) | 0.8 m/s (~1% of pace at Texas), 1 s |
| **Room ahead** in the other lane | no car closer than `max(train gap at my speed, gap to the car being passed) + aheadMargin`. That is room to pull fully alongside the car being passed, and never less than a train gap | +10 m (≈ 39 m at Texas) |
| **Room behind** in the other lane | every car behind gets its own train gap, plus `closingLookSeconds` of its closing speed if it is quicker. Nobody is cut off | 3 s |
| **Not mid-corner** | the most cornering load (curvature × speed²) on the line over the move's length ahead is under `maxCornerAccel`. The move adds up to 1.5 m/s² sideways, and the speed plan keeps only ~2 m/s² in hand at the limit. So moves start on the straights, the dog-legs and the last part of a corner exit | 12 m/s² |
| **Not just moved** | `cooldownSeconds` since the last move ended | 3 s |

"In the other lane" means any car whose `ovalLane` is that lane, any car that is **mid lane change itself** (it
counts as being in both lanes, so two cars can never swap into each other), and anything physically on that lane's
line (centres within `OvalTrain.CorridorWidth`, 2 m), including a free-driven human car.

**The move** (`LaneChange.Step`): progress goes from 0 (old line) to 1 (new line). Sideways speed is capped at
`maxLateralSpeed` (2 m/s) and sideways acceleration at `lateralAccel` (1.5 m/s²), with a braking ramp
(`rate = sqrt(2 a remaining)`) into the new line. The car speeds up sideways, holds its speed, slows, and **lands
exactly on the new line**: progress is snapped to 1 when it is within 0.01% of it, a millimetre or less. For a 3.2 m
change that takes ~2.8 s and peaks at 1.5° of heading at Texas speeds.

**Turning back** (`LaneChange.CanAbort`): every step of a move, the other lane is checked again, with the room cut to
`abortShare` (0.6) of what the start needed (ahead: 0.6 × a train gap + margin). If the room has gone, and the car can
still stop short of the middle of the road (progress plus its sideways stopping distance < 0.5), it turns back.
The old and new lanes swap, progress becomes 1 - progress and the rate is negated. The same acceleration-limited
motion then carries it back. It decelerates sideways rather than reversing at once, so there is no swerve. Past the
middle, it completes, and the new lane's train law handles whatever is there.

### Who runs it

- `SplineDriver.lanePassing` (default on) + `StepLaneChange()`. This runs first in every `FixedUpdate`, before the
  re-lay for a new `ovalLane`, so a decision and its re-lay land on the same step. It reads last step's train. It
  runs only while `LaneFollowing`, under green, on the main track, not called to the pits, not on the apron, and only
  with a car ahead in the train.
- At the start of a move, `ovalLane` becomes the **new** lane at once. Its line, its speed plan and its train take
  over at once (a car ahead in the new lane is followed from the first step). The car's line is laid as the old
  lane blended into the new one: `LaneShiftAt(d) = (old(d) - new(d)) × (1 - progress)`. This is added in `Place`
  (before the line smoothing, so the commanded lateral is continuous), in `PathPointAhead` and in the pit rejoin. Read
  ahead of the car, progress is projected forward at the move's current rate, so the steering aims at where the
  blend will be. The car being passed still counts as "in the path" (`OvalTrain`, 2 m corridor) until the mover is
  2 m across, so the mover cannot run into its back early in the move.
- `OvalPass` (`Assets/Scripts/AI/OvalPass.cs`) walks the field for the room rules. `OvalPass.Spec` is the rule set
  for the whole field.
- `SplineInputDriver`: the lane feed-forward stops learning during a lane change, because the error there is the
  blend leading the car, not the car running wide.
- Telemetry: `LaneChanging`, `LaneChangeProgress`, `LaneChangeFrom`, `LaneChangeAborting`, `LaneChangePassing` (the
  car it set out to pass), `PassAdvantage`, `LaneChangesStarted/Completed/Aborted`, `LaneLateralAt(lane, d)`,
  `LaneTargetLateralAt(d)` (the line the car is meant to be on, mid-blend included).

Road courses: `LaneFollowing` is false off an oval, so `StepLaneChange` returns at once and `LaneShiftAt` is 0.
Watkins Glen's `AILapSimTests.AnAICarLapsWatkinsGlenCleanly` (all four cases), `TheBrainStaysWithTheCar` and
`GrandstandRaceSimTests.TheDynamicWatchedField_TakesTurnOneApex_AtWatkinsGlen` pass unchanged.

Stage 2's `TwoTrainsHoldTheirLanesAndGapsAtTexas` sets `lanePassing = false`, so it still measures the trains
alone. Its numbers and the stage 1 solo-lane numbers are unchanged to the last digit.

### Verification: `Assets/Tests/Editor/OvalPassingTests.cs`

- Three unit tests of the rules: a move lands exactly on the new line inside its speed and acceleration limits; a
  turn back never passes halfway and never swerves; the room ahead and behind, and the wish to pass.
- `AMixedFieldPassesByChangingLanesAtTexas` (~45 s): **18 cars**, 9 to a lane. Driver ratings are rolled as the
  stage 2 trains roll them (pace 0.966-1.038) but started in **mixed order**, 70 m apart, with the outside lane half a
  row back. One lap to form up, then **20 timed laps** under green with passing on. Mistakes are off. Contacts,
  walls, spins and offs are measured as in stage 2. Lane error is the car centre against `LaneTargetLateralAt`: the
  lane, or the blend mid-move. A "pass" is a lane change that gets the car past the car it set out to pass within
  60 s.
- `ALaneChangeTurnsBackWhenTheGapCloses` (~5 s): a quick car (A) runs up on a slow one (B) in the inside lane and
  pulls out. As soon as it is 5% across, a kinematic car (C) is dropped 14 m behind it in the outside lane at its
  speed (inside the 0.6 × 29.5 m a move under way keeps). A must turn back.

Results (2026-11-03):

| Mixed field, 20 laps | |
|---|---|
| Lane changes started / completed / aborted | **19 / 19 / 0** |
| Passes made by a lane change (got by the car followed within 60 s) | **12** (6 didn't get by: a 1% pace edge needs ~40 s to clear 30 m, and some found the new lane's train ahead) |
| Running-order changes, any (both lanes, incl. lapping) | 153 |
| Car-to-car contacts / wall hits / spins / offs | **0 / 0 / 0 / 0** |
| Train emergencies | **0** steps |
| Closest door to door / nose to tail (same lane) | 0.82 m / 16.29 m |
| Closest car behind in the new lane as a move began | 26.3 m |
| Lane error outside lane changes | mean **0.113 m**, max **0.647 m** (275 m, the Turn 1 dog-leg turn-in, as solo) |
| Lane error during lane changes (vs the blend) | mean **0.214 m**, max **0.539 m** |
| Fastest sideways, against the lanes | 2.96 m/s during changes; 2.63 m/s with no change at all. The background is the projected car lateral moving against a lane whose offset drifts with Texas's width, so the move itself adds little |
| Max body slip | 7.5° (7.3° with trains alone) |

| Turn-back scenario | |
|---|---|
| Pulled out | 1087 m, 70.3 m/s |
| Aborted / completed | **1 / 0**, final lane Inside |
| Furthest across | **7%** of the way to the other lane |
| Back on the inside lane | 1.30 s after C appeared |
| Fastest sideways / max error vs the blend | 0.67 m/s / 0.271 m |
| Contacts / spins | **0 / 0** |

These were measured with the working tree's uncommitted AI changes present, as stages 1 and 2 were.

## Live races: `OvalLaneDirector`

`OvalLaneDirector` (`Assets/Scripts/AI/OvalLaneDirector.cs`) puts the lanes into real sessions: single races,
weekend races, practice, and watched fields. It bootstraps itself (no scene wiring) and runs before `SplineDriver`
each physics step.

- **Under green on an oval**, every AI car on the main track (every enabled `SplineDriver` in `RaceField`, except the
  safety car, cars on pit road and cars held still on the grid) is given a lane, at most 8 cars a step. Trains,
  spacing and passing by lane change then run exactly as in the sim.
- **Off the green** (pre-grid, the formation lap, a full-course caution) it takes back the lanes it gave. A car mid
  lane change keeps its lane until the move lands (~3 s). The formation brain then lines the field up two-wide and
  single file as before, and the next green hands the lanes out again.
- **Which lane:** the nearer one. Cars alongside each other (within 6 m along, 4.5 m across) are split: the one
  further toward the infield takes the inside lane and the other the outside. A car alongside one that already has
  a lane takes the other one.
- **No snap at the green.** Lane mode drops the car's tactical offsets at once. So the car is handed its lane with
  `lateralOffset` set to where it already is relative to the lane line. `AIRacingBehaviour` eases `lateralOffset` to 0
  at its sideways rate (1.6 m/s) under green. A practice stint car, which `AIRacingBehaviour` leaves alone, is eased
  by the director.
- **Not on road courses** (`TryGetOvalLanes` fails there) and **not on plate tracks** (`TrackConditions.AiPackRacing`),
  which keep their three-wide pack AI. Cars given a lane by hand (the sim tests) are never taken back.
- **A/B switch:** pause menu (`Esc`) > **Lane AI**, shown on ovals that race in lanes. Stored in PlayerPrefs
  `OvalLaneAI`, default on. Turning it off hands every lane back at once (cars mid-move finish their move first).
  **Oval lanes** next to it is just the drawing.
- EditMode tests never start it (it bootstraps at runtime), so every sim number above is unchanged.

### Verification: `OvalPassingTests.ATwoWidePackIsHandedItsLanesAtTheGreen`

EditMode, ~12 s. Texas, **16 cars, none with a lane**. They are rolled to the green as a tight two-wide pack: rows
12 m apart, each pair 3.5 m across on GridSpawner's grid stagger. The director is stepped before the cars and hands out
the lanes at the green. Then 5 laps with passing on. Contacts, walls, spins and offs count from the very first step.

Results (2026-11-03):

| | |
|---|---|
| Lanes a second after the green | `I O I O I O I O I O I O I O I O`: every row split, no car without a lane |
| Car-to-car contacts / wall hits / spins / offs | **0 / 0 / 0 / 0** |
| Train emergencies | **0** steps |
| Closest door to door | 0.86 m |
| Lane error (from the first timed lap) | mean **0.113 m**, max **0.620 m** |
| Lane changes started / completed / aborted | 3 / 3 / 0, 2 passes |

## Single file on the ideal, side by side inside / outside (2026-11-03)

Replaces "two fixed lanes" as the live rule. `SplineDriver.idealWhenSingleFile` (default on; only acts with
`lanePassing` on):

- **The lanes are the authored AI lines.** `TrackBuilder.ovalLanesFromAiLines` (default on) makes
  `TryGetOvalLanes` build the inside and outside lanes from the track's **leftmost and rightmost AI lines** (the blue
  and red racing-line gizmo lines) through `OvalLanes.BuildFromLines`: per sample the line nearer the infield is the
  inside lane (the leftmost on an anticlockwise oval), each kept `insideEdgeMargin` / `outsideEdgeMargin` off the
  edges, pushed at least `laneSpacing` (3.2 m) apart, then smoothed over ±20 m. Off = the old fixed grooves from
  the road width. The gizmo, overlay, director and brain all read it, so they agree.
- **Single file = `OvalLanes.Lane.Ideal`**: the car's normal ideal line (trained / optimised / authored), with no
  per-driver `lineFactor` spread. The director hands Ideal to every car at the green that has nobody alongside.
- **Alongside** (another car within `alongsideEnterMetres`, 6.5 m centre to centre along the track, anywhere
  across it; `OvalPass.TryFindAlongside`): a car on the ideal moves to the side lane the other car is not in. If
  both are on the ideal, the one nearer the infield goes inside. Both cars read the same pair, so they always split.
  This "split" move is the stage 3 blend, but it starts even mid-corner and is never turned back.
- **Clear again** (nobody within `alongsideClearMetres`, 9 m, so a pair drawing level doesn't flick back and forth):
  back to the ideal once the lane cooldown is over, the corner allows the move, and the ideal has the room a move
  under way keeps (`abortShare` of a train gap).
- **Passing from the ideal** pulls out to whichever side lane is further from the car being passed and has room,
  falling back to the other. The passer stays out until it is 9 m clear ahead of that car, or
  `pairPassGiveUpSeconds` (30 s) pass. The car being passed sees it come alongside and drops to the other lane.
- Hand-set lanes in tests (`SetLane` in `OvalLaneTests` / `OvalPassingTests`) turn the rule off, so the stage 1-3
  measurements above still describe fixed lanes. Measured by
  `OvalPassingTests.SingleFileRunsTheIdeal_SideBySideSplitsInsideAndOutside`: the 18-car mixed field with the rule
  on, 10 laps. It asserts 0 contacts / walls / spins / offs, that at least 90% of single-file car-steps are on the
  ideal, and that at least 90% of overlapping pair-steps are split the right way. Not yet run; not play-tested.

### Trained, anchor-pinned lanes (2026-11-03)

`Draftmaster > AI > Train Racing Lines Pinned To Anchors (Selected Track)` (the selected TrackInfoV2, or
`TrackSelection.CurrentId`) retrains from scratch with **three** lines: ideal, leftmost and rightmost. Each one is
trained for lap time from its authored line, but pinned through its own entry / apex / exit anchors: a funnel
`RacingLineTrainer.BuildPins`, +/-0.15 m at the point for the side lines and +/-0.4 m for the ideal, opening 0.03 m
per metre out to 15 m either side, and free beyond that. The side lines may use the road edge to edge, less
`SplineDriver.RoadEdgeMargin`. All three are stored in `RacingLines/<id>.json` (`leftLateral`, `rightLateral`,
`anchorPinned`), and later "Another Session" clicks keep them pinned. While stored side lines match the lap,
`TryGetOvalLanes` builds the inside and outside lanes from them unsmoothed, held 1.4 m off the edges, and still
pushed 3.2 m apart where they come closer than that.

Texas, first run (2026-11-03, 59k laps):

| Line | Lap (seed -> trained) | Off its 14 authored points: mean / worst |
|---|---|---|
| ideal | 42.50 -> 42.32 s | 0.36 / 0.42 m |
| leftmost (inside lane) | 42.21 -> 41.73 s | 0.12 / 0.21 m |
| rightmost (outside lane) | 42.91 -> 42.10 s | 0.13 / 0.21 m |

The pinned ideal is 0.5 s slower than the unpinned trained line it replaced (41.82 s), which was free to leave the
drawn points. The worst misses (2331 m, just before the line) are the 2 m storage grid's round trip.

## Wider, higher lanes and a flat-out launch (2026-11-06)

Josh's report: the field still stacked up at the start (no longer crashing), and everyone should be flat out until
they are up to speed. Two changes.

**The lanes from the AI lines sit further apart and further up the banking**, most of all through the trioval.
`OvalLanes.Spec` gained `extraSpacing` / `outwardShift` (all round: +0.4 m apart, +0.3 m up) and
`frontStretchExtraSpacing` / `frontStretchOutwardShift` (on top, on the front stretch: +0.6 m apart, +0.9 m up).
`BuildFromLines` widens the drawn gap (never less than `laneSpacing`) about its middle by the spread, moves the pair up
by the shift, then holds both off the edges as before. Plain `Build` (the fixed grooves) is unchanged.
`OvalLanes.FrontStretchWeight` finds the front stretch from the lap's own curvature: every sample through the
start/finish line whose curvature, averaged over ±120 m, is under 60% of the lap's sharpest. 1 there, easing to 0 over
80 m at each end. The long window matters: Texas's trioval is a run of short kinks as sharp as the turns at their
peak (5.6 /km against 4.4 /km), but only 100-150 m long. `TrackBuilder.BuildOvalLanes` passes the weight to both
`BuildFromLines` paths.

Texas (`OvalLaneTests.TexasLanesOpenOutAndMoveUpThroughTheTrioval`): front stretch -154 m to 366 m of the 2356 m lap.

| | lanes apart (drawn -> now) | pair's middle up the banking |
|---|---|---|
| Trioval (weight 1) | 4.78 -> 5.78 m | +0.78 m (the outside lane meets its 1.8 m edge margin in places) |
| Rest of the lap | 4.92 -> 5.32 m | +0.30 m |

**A flat-out launch** (`SplineDriver.flatOutLaunch`, default on; `Draftmaster.Sim.TrainFollow`). The stack-up was the
train law itself. The green falls with rows 8-9 m apart, and the time gap wants 7 m + 0.3 s x v, so every car lifted
for the car ahead the moment the green fell. Now:

- From the green (a start or a restart) until a car is up to speed (`launchUpToSpeedShare` 0.97 of its speed plan, or
  of the speed of a car ahead that is itself up to speed; at most `launchMaxSeconds` 30 s after the green), it is held
  only by `launchGap` (7.5 m centre to centre). That hold is the approach term alone, so it lifts only when it is
  closing too fast to stop short of 7.5 m on a lift. A car that joins more than 30 s after the green never launches.
- Once up to speed, the time gap wanted starts from the gap the car has (never under `launchHeadwayMin`, 0.1 s) and
  grows by `launchHeadwayGrowth` (0.004 s a second) back to 0.3 s, about 1.5 laps at Texas. It grows in time, not
  metres, so the law keeps its speed term (its damping). Two values were tried and dropped. Opening the gap at
  1 m/s a car compounded down the train: the eighth car in line ran 10 m/s under its plan out of turn 2. With no
  floor, a car merging onto the ideal ahead of a train squeezed the cars behind to 0.3 m nose to tail.

Measured by `OvalPassingTests.AGreenAtPaceSpeedRunsTheFieldFlatOutUpToSpeed`: Texas, 16 cars two wide, green at
60 mph with rows 9 m apart, 2 laps. The `(False)` case is Explicit and report-only: the old law.

| | old law | flat-out launch |
|---|---|---|
| Mean throttle until the first car is up to speed (4.7 s) | 0.758 | **0.994** |
| Field length (first to last) at the green / 25 s later | 63 / 203 m | 63 / **133 m** |
| Train emergency steps (2 laps) | 195 | **82** |
| Car-steps braking for the car ahead in the first 25 s | 1188 | 991 |
| Closest nose to tail | 4.06 m | 2.82 m |
| Contacts | 2 (lane changes at ~1545 m) | 0 |

The report carries a 2-second trace of every car (speed / plan, throttle / train brake, train gap, L launching, T the
train binding, C changing lanes). Not play-tested.

## Not done yet

- **Live races not yet play-tested.** The director has only been run in the sim.
- `AIRacingBehaviour`'s steering round a wrecked car is still ignored in lane mode. The train brakes for a slow or
  stopped car, and a car with a run on it may pass by changing lanes. A stopped car on an oval brings out the
  full-course caution (single player), which hands the lanes back.
- The train only runs under green. The formation lap and cautions still belong to `FormationController` /
  `AIRacingBehaviour`.
- Passing only starts under green. A yellow or a pit call mid-move lets the move finish (~3 s); it is dropped only
  once the car is on pit road.
- The car being passed never defends or reacts. A pass that stalls alongside (the new lane's train ahead, or too
  little pace edge) stays alongside; nothing makes the passer give up and drop back.
- Plate tracks: `TrackConditions.AiFlatOut` still plans the turns flat out whatever the lane; only Texas was
  measured.
