# Oval lanes (oval AI rework, stage 1)

The oval AI is being rebuilt in three stages (road courses are not touched by any of them):

1. **Two lanes, and perfect lane following** - this page.
2. A single-file train in each lane: smooth back-off, no accordion.
3. Passing by switching lanes when there is a gap and a run.

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
  jumps 3.2 m sideways when it does; stage 3 needs a proper lane-change blend.)

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

## Not done in stage 1

- **Nothing assigns lanes in a real session yet.** `ovalLane` defaults to `None`, so races, practice and the
  formation lap behave exactly as before. Turning it on for a field now would be wrong: lane mode ignores
  `tacticalLateralOffset`, which is how `AIRacingBehaviour` passes and avoids and how `FormationController`
  lines cars up two-wide. Stage 2 (trains) assigns the lanes and owns following distance; stage 3 owns moving
  between them.
- Lane change at run time is an instant re-lay (see above), not a manoeuvre.
- Plate tracks: `TrackConditions.AiFlatOut` still plans the turns flat out whatever the lane; only Texas was
  measured.
