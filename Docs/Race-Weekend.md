# The race weekend

A weekend is three days at one track, played as **six half-days**: Friday morning and afternoon, Saturday
morning and afternoon, Sunday morning and afternoon. Three championships share the venue — **Trucks**,
**National** and **Cup** — and the player is entered in exactly one of them. Their own practice, qualifying
and race are the only sessions they drive; the other two championships' six sessions are things to go and
watch, and everything else in the three days is what actually fills a driver's time: strategy meetings,
media, signing sessions, sponsor obligations, and on race day the mandatory drivers meeting and the walk
down the intro stage.

**You do not have to open anything.** Arriving at a track puts the day and time under the spawn card
("Watkins Glen - RV" / "FRIDAY - 9:30 AM"), books whatever is next on the sheet, and puts an objective
marker on it. Step out of the motorhome and the team liaison walks over to tell you where you are due;
follow the marker, do the thing, and the next booking is already live with its own marker before the
result card has faded. `T` travels you there if you would rather not walk.

Press **F10** to open the timetable when you want to change what you are doing — it is also on the pause
menu, on the phone (`SCHEDULE` tile) and on the race results screen. It no longer opens itself over your
first steps.

---

## 1. What the timetable looks like

The schedule screen is six half-day tabs across the top, everything booked in the selected half-day down
the left, and the highlighted booking down the right with the button that commits to it.

The clock at the top right is what makes it a schedule rather than a menu. Every booking has a **start time
and a length**. Doing something moves the clock to the end of it, and anything in that half-day that started
before the clock and was never attended is **MISSED**. Two things booked over the same hour cannot both be
done — the rail says `CLASHES WITH …` so the trade is visible before you commit.

Row markers:

| Mark | Meaning |
|---|---|
| `[ ]` | available, optional |
| `[!]` | available, an **obligation** — skipping it costs money, appeal or sponsor mood |
| `[x]` | done |
| `[-]` | missed |

Gold rows are your own sessions. `SKIP TO <next>` gives up the rest of the half-day and moves the day on;
everything left unattended in it is swept up as a no-show first.

## 2. The session schedule

This is the shape of a modern compressed stock-car weekend: the trucks run and race Friday, the second-tier
cars qualify Saturday morning and race Saturday afternoon, and the top series gets practice and qualifying
back-to-back on Saturday before racing Sunday.

| | Practice | Qualifying | Race |
|---|---|---|---|
| **Trucks** | Fri 10:00 | Fri 13:00 | **Fri 19:00** |
| **National** | Fri 15:00 | Sat 09:00 | **Sat 16:30** |
| **Cup** | Sat 10:30 | Sat 14:00 | **Sun 14:00** |

Whichever row is yours becomes drivable and the other two become `WATCH`. The two mandatory race-day beats
are placed **relative to your own race**, not to Sunday: the drivers meeting is two hours before your green
flag and driver introductions are thirty minutes before it. A truck driver's race day is Friday, and their
Sunday is two other people's races to watch.

Change which championship you are in with the `SERIES:` control in the schedule footer. It is only
available on a weekend nothing has happened in yet — once Friday morning has been spent you are in the
series you are in.

## 3. What you can do with the rest of the time

**Every obligation is a place you walk to.** Committing to something on the sheet does not run it — it
books it. The schedule closes, an objective strip appears at the top of the screen naming the place and the
distance left, and the obligation itself starts when you are stood there and press the action button on
whoever is waiting. The world stays live throughout: nothing freezes, the crowd keeps moving, and you can
walk away mid-sentence, which counts as not having done it.

| Where | What happens there |
|---|---|
| **The pit box** | The pre-weekend plan meeting with the crew chief, and where a broadcaster catches you for a media hit. |
| **Your motorhome** | Session debriefs, sat at the dinette with the engineer across the table. |
| **The drivers' room** | The drivers meeting and the press conference. A top table, and a chair for every driver entered in all three championships. |
| **The fan fence** | Signing sessions and the hauler parade. A barrier along the public edge of the paddock with the crowd behind it — you sign from the inside. |
| **The hospitality tent** | Sponsor appearances and photo shoots, under the awning in the middle of the paddock. |
| **The intro stage** | Driver introductions, on the platform at the end of pit road. |
| **A grandstand** | Watching somebody else's session, sat in the crowd with the cars going past. |

`T` travels you to the venue if you would rather not walk. The clock cost of a booking is the hour it
takes, charged when it is done — the walk is free either way.

**The chain.** `WeekendSchedulePlan.NextWorthDoing()` is the one line of the sheet that matters at any
moment: the earliest booking that is not done, not missed and allowed by the clock. `WeekendDirector.BookNextUp`
puts it in `WeekendAppointment` on arrival, again once the venues are built, and again the moment anything
is settled — so the weekend plays as one thing after another. A booking you make yourself on F10 stands;
it is only replaced when the clock has moved past it.

| Activity | What it is | Scored on |
|---|---|---|
| **Rookie orientation** | First weekend of a career only, 09:30 on the Friday morning. The crew chief shows you the phone: the key that opens it, TASKS for what is outstanding, NOTES for who asked you for what. Optional, and missing it costs nothing but the explanation. | Team morale, and a crib sheet left in the phone's NOTES |
| **Team strategy briefing** / **race plan meeting** | The crew chief lays out the weekend and asks what you want the car to be. | Setup knowledge, team morale |
| **Practice debrief** | The same question with data behind it. Worth far more if you actually ran the practice session. | Setup knowledge |
| **Press conference** / **media availability** / **broadcast hit** | A reporter asks, three answers on the desk. | Press standing, fans, sponsor mood, rivalry |
| **Signing session** / **hauler parade** | A queue at the fence, one person at a time, each holding something. Sign it, sign it and ask their name, pose for the photo, or wave and move on. | Fan appeal, `autographs` counter |
| **Sponsor photo shoot** | The photographer wants hero or human, and the brand's rep wants the cap in every frame. | Sponsor mood, fans, crew morale |
| **Hospitality Q&A / suite meet & greet** | A guest asks something and one of the answers is the line the brand paid for. The funny one is not it. | Sponsor mood — the off-message answers buy fans and press instead |
| **Watch practice / qualifying / race** | Sit down in a grandstand and watch it. Somebody else's session, simulated and played forward on a compressed clock, with the timing tower and the broadcast calls down the right-hand side of a screen that still shows the track. `SPEED`, `SKIP` and `SEEN ENOUGH` (Esc) shorten it. | Setup knowledge (homework), team morale |
| **Drivers meeting** | Mandatory, in the drivers' room with the field sat around you. Officials read four notes; one of them will catch somebody out at this track today. Say which. | Setup knowledge, morale |
| **Driver introductions** | Mandatory, on the stage. Your name over the PA — decide what to give the crowd. | Fan appeal, sponsor mood |

## 4. What it is all worth

Five meters run across the weekend, shown on the schedule rail and on the phone.

| Meter | Range | What it does |
|---|---|---|
| **FANS** | 0–100 | `Draftmaster.Fans.FanAppeal` — the existing meter. Decides how many autograph seekers turn up along the pit wall in the race scene, and shows on the race results screen. |
| **SPONSOR** | −100…+100 | Multiplies the sponsor payout at the race: `WeekendLedger.SponsorPayoutMultiplier`, up to +30% / −40%. |
| **TEAM** | −100…+100 | `CrewSharpness01` and a share of `CarPreparation01`. |
| **PRESS** | −100…+100 | Warms or chills the room — a friendly press rewards a bold answer harder and punishes a dull one harder. |
| **SETUP** | 0–100% | Pace banked from practice, briefings, debriefs and homework: `WeekendLedger.CarPreparation01`. |

Money moves too. Appearances pay a fee just for turning up, a good session pays a bonus on top, and
**no-showing a contracted appearance takes half the fee back** and drops sponsor mood. Missing the drivers
meeting is a $5,000 fine.

Nothing here is free. Backing your crew in public buys morale and prints nothing. Taking a swing at the
driver who wrecked you is the clip of the weekend, costs the sponsor's afternoon, and moves that driver's
`DriverRelationships` entry — which the AI reads for payback in the race.

## 5. The championships you are not driving in

Three series share every venue and the player is entered in one. The other two run their whole weekend
whether the player watches from a grandstand, spends the hour signing hats, or never leaves the motorhome —
and those results are now kept. `SeasonChampionships` is the season: the list of rounds the player has
turned up to, and three points tables folded back out of it.

**Almost nothing about a result is stored.** `SeriesSimulator` is deterministic from `(series, round)`, so
a list of round numbers is a complete record of three championships — which is also why the race the player
sat and watched in the stand is, by construction, the race in the standings. The one fact that cannot be
recomputed is written down: the player's own finishing position on the rounds they actually drove.

| The result of | Comes from |
|---|---|
| The two series the player is not in | Simulated in full — grid, classification, retirements, cautions |
| The player's own series, on a round they drove | The same simulated field with the player cut into it at the position they finished; everybody they beat drops a place, and the grid shuffles the same way behind their qualifying position |
| The player's own series, on a round they skipped | Simulated like the other two; the player scores nothing |

The field is the simulator's roster rather than the cars that were actually on track, because the race
scene's AI is a shuffled handful of database drivers who change from round to round, and a championship
table needs the same thirty-odd names every week to be worth reading.

**Points** are the modern stock-car scale (`ChampionshipPoints`): 40 for a win, 35 for second, then down a
point at a time to one for 36th, plus a point for pole. Ties break on wins, then best finish, then poles.

**You do not know Sunday's result on Friday.** Everything the player can read goes through
`SeasonChampionships.HasRun(series, round)`, which puts that series' race slot against the weekend ledger's
clock — so the standings fill in across the three days in the order the races actually run, and a result
that lands while the player is under the hospitality awning is news when they next look at their phone.

Where it surfaces:

- **The phone, `POINTS` tile** — this weekend's three races (the winner, or when it is due), a results feed,
  and the three tables with the player's own championship first. The tile badges results that have come in
  since they last looked; their own race is not news.
- **The schedule rail** — a race row reads `WON BY #21 Junior Kemp` once it has been run, over the points
  leader of the championship it counts towards.
- `GrandstandSpectate.LastWinner(series)` answers for every race that has run, not only the ones watched.

A round goes on the calendar in `WeekendDirector` the first time the weekend's timetable is built, and the
player's own result is recorded by `RaceDirector` when the race classifies — but only for a race the
weekend routed there, so a one-off started from track select is nobody's round.
`SeasonChampionships.StartNewSeason()` rolls all three tables over; nothing calls it yet, which is where a
season-end flow hooks in. Covered by `Assets/Tests/Editor/SeasonChampionshipTests.cs` (23 tests).

## 6. How it hangs together in code

```
Assets/Scripts/Weekend/
  Core/                            ← Draftmaster.Weekend asmdef: pure, testable, no scenes
    RacingSeries.cs                Trucks / National / Cup + SeriesCatalog (names, field sizes, purses)
    WeekendSlot.cs                 six half-days, their clock windows, time formatting
    WeekendActivity.cs             one booking: kind, series, start time, length, obligation + penalty
    WeekendTimetable.cs            builds the whole sheet from (playerSeries, weekendId) — deterministic
    WeekendLedger.cs               what has been done/missed, the clock cursor, the five meters (PlayerPrefs)
    WeekendOutcome.cs              what one completed activity did
    SeriesSimulator.cs             the other two championships' sessions: results + broadcast timeline
    SeriesWeekendResult.cs         one championship's round, classified and priced - the player cut in
    ChampionshipPoints.cs          what a finishing position is worth
    SeasonChampionships.cs         the season: rounds run, three points tables, what has been read
    PressConferenceContent.cs      the question bank and what each tone trades away
    WeekendRandom.cs               seeded xorshift so every rebuild is identical
    WeekendVenue.cs                which place each kind of booking happens in, and what it is called
    WeekendConversation.cs         beats, answers and what an answer is worth — the shape of an obligation
    Conversations/                 TeamMeetingContent, CeremonyContent, SponsorContent, SigningContent
  WeekendDirector.cs               owns the timetable, books activities, settles outcomes  (Assembly-CSharp)
  WeekendScheduleUI.cs             the sheet (F10)
  WeekendAppointment.cs            the booking you have said yes to and not turned up for yet
  WeekendObjectiveHUD.cs           where you are due, how far, and T to travel there
  WeekendResultCard.cs             what that hour did (a corner card in the world; modal only after a session)
  WeekendModal.cs                  one owner of Time.timeScale across stacked panels
  Venues/
    WeekendVenueAnchor.cs          a place in the paddock, and how close counts as being there
    WeekendVenueSites.cs           builds them all into whatever track is loaded
    WeekendVenueHost.cs            the person you talk to — plays a conversation in speech bubbles
    WeekendScripts.cs              which conversation a booking is, plus the runtime facts it needs
    GrandstandSeat.cs              sit down and watch
    PaddockProps.cs / PaddockPerson.cs   flat blocked-out props, and people to stand in them
  Activities/GrandstandSpectate.cs the simulated session, played beside the live world
Assets/Scripts/UI/Phone/PhoneScheduleApp.cs      read-only glance at today
Assets/Scripts/UI/Phone/PhoneChampionshipApp.cs  the three championships and what has come in
```

The split is deliberate. `Core/` holds every rule and is covered by
`Assets/Tests/Editor/WeekendTimetableTests.cs` and `WeekendActivityContentTests.cs` (26 tests). The runtime
layer is the only thing that knows about scenes, `PlayerWallet`, `PlayerStatsLedger` and
`DriverRelationships` — it installs those as callbacks on the ledger at boot (`WeekendLedger.MoneyHook` and
friends), because the core assembly cannot reference Assembly-CSharp.

### Where a booking happens

```
schedule → Begin(activity) → WeekendAppointment.Make(activity)     (not on-track)
                             objective strip: "Head to the drivers' room — 86 m"  [T] travels there
        → player walks up to the WeekendVenueHost at that venue and presses E
        → WeekendScripts.For(activity) → a WeekendConversation, played through SpeechBubble +
          DialogueChoiceUI, answer by answer
        → WeekendDirector.Finish(activity, outcome, inWorld: true) → corner card → ledger
```

`WeekendVenueSites` builds the places out of what the track already has: the pit box marker, the player's
own motorhome from `DriverMotorhomeLot`, the paddock rectangle (`PaddockSpawner.TryGetArea`) for the room,
the fence, the tent and the stage, and every generated `Grandstand` gets a seat. A track package can author
a `WeekendVenueAnchor` of its own and the builder will leave that venue alone — same rule as the rest of
the track dressing.

### Crossing scene loads

Your own sessions are not panels — they hand off to the race scene:

```
schedule → Begin(activity) → RaceWeekend.Current = Practice/Qualifying/Race
                             WeekendDirector.PendingRouteId = activity.id
                             load RaceScene
        ← PracticeDirector "END SESSION"  (or RaceDirector classifying the race)
          → WeekendDirector.FinishRoutedSession(outcome) → result card → schedule
```

`PendingRouteId` is in PlayerPrefs, not a static, because the weekend reloads the scene between practice,
qualifying and the race. Qualifying still captures `RaceWeekend.GridOrder` exactly as before; skipping it
means starting the race from the back.

`WeekendModal` exists because `Destroy()` is deferred to the end of the frame: with per-panel timescale
save/restore, the incoming panel's `Awake` runs before the outgoing panel's `OnDestroy` and the game starts
running underneath an open menu. One depth counter, saved once in and restored once out.

## 7. Adding to it

**A new activity kind:** add to `ActivityKind` and the right `ActivityKinds.Is*` group (that alone gives it
a tag and a colour on the sheet), give it a venue in `WeekendVenues.For`, book it in
`WeekendTimetable.BuildObligations`, and add a case to `WeekendScripts.For` returning a
`WeekendConversation`. The content goes in `Core/Conversations/` — beats of speaker, line and answers,
where each answer carries what it is worth. Nothing else needs writing: the venue's host plays it, and the
ledger takes the outcome. `WeekendVenueTests` will fail the build if a kind has no venue or a beat has
fewer than two answers.

**A new press question:** one entry in `PressConferenceContent.Pool`. Situational questions go in the
`if` blocks at the top so the shuffle is choosing between relevant questions. Answers are tagged with a
`PressTone` and the scoring is per-tone, so a new question needs no new scoring code.

**A rotating weekend feature:** `WeekendTimetable.AddFeature` picks one headline obligation per weekend off
a seeded roll — add a case.

**Retuning what things are worth:** all of it is in `WeekendLedger.Apply` and the `Settle()` on each runner.
Nothing else touches the meters.

## 8. Gotchas

- **The timetable must stay deterministic.** It is rebuilt from `(playerSeries, weekendId)` on every scene
  load, and the ledger records completion against `WeekendActivity.id`, which is built from
  `slot.startMinute.kind`. Move a booking's start time and the ledger loses track of it mid-weekend.
- **Everything must fit its half-day.** `EveryBooking_FitsInsideItsHalfDay` asserts this; use
  `FitAfter(...)` for anything placed relative to a session, which rolls it into the next half-day rather
  than letting it overrun.
- **09:30–09:45 on the first Friday belongs to the rookie orientation.** It is the only quarter of an hour
  on that morning nothing else wants — the hauler parade ends at 09:30 and the sponsor photo shoot starts at
  09:45 — so the tutorial costs a new player nothing to take. Book anything into that window and finishing
  one marks the other missed; `WeekendOrientationTests.ItTakesTheGapNothingElseWants` fails if you do.
- **Car numbers are partitioned mod 3** across the three championships (Cup `1+3i`, National `2+3i`, Trucks
  `3+3i`) so no number is entered twice at the same venue. Change the numbering and the collision test
  catches it.
- **Obligations do not freeze the world any more.** They are conversations in the paddock: the crowd keeps
  moving and the player can walk off mid-sentence, which leaves the booking unattended rather than done.
  Anything that must stop the world (the schedule, the post-session result card) goes through
  `WeekendModal`, and `WeekendDirector.OnSceneLoaded` resets it on every load — a panel that survives a
  scene change would otherwise carry `Time.timeScale = 0` into the next scene.
- **A venue with no host can never be attended.** `WeekendVenueSites.StaffTheVenues` stands somebody at
  each one; `WeekendVenuePresenceTests` (PlayMode) fails if any venue is missing a host or an anchor.
- The **signing queue and the drivers meeting's live note are seeded off the booking id**, so walking away
  and coming back cannot re-roll a better hour out of them.

---

## 8. Authoring a weekend (the plan files)

A weekend does not have to be the generated one. Drop a JSON file at

```
Assets/Resources/Weekends/<Track>.<Series>.json      e.g. WatkinsGlen.Cup.json
Assets/Resources/Weekends/<Track>.json               all three series at that circuit
```

and **that file is the weekend** — six half-days, whatever you put in them, empty ones included. The
procedural builder in `WeekendTimetable` does not run for a track+series that has a plan. A track without
one still generates its schedule as before, so the calendar stays playable while one round is authored.

Open **Draftmaster > Weekend > Plan Editor** (`Ctrl+Shift+E`): pick the track and series, add bookings to
half-days, and save. Problems are listed in red as you type — a booking before the half-day opens, one that
runs past its close, an event id that does not exist.

### The file

```json
{
  "track": "WatkinsGlen",
  "series": "Cup",
  "slots": [
    { "slot": "FridayAM", "events": [
        { "event": "sponsor_event-photoshoot", "start": "09:45", "area": "photoshoot" },
        { "event": "watch-qualifying", "start": "10:00", "series": "Trucks" }
    ]},
    { "slot": "FridayPM", "events": [] }
  ]
}
```

`event` and `start` are the only required fields. Everything else — title, subtitle, length, appearance fee,
penalties — falls back to `WeekendEventCatalog`, so a booking is one line unless it is doing something
unusual. **Draftmaster > Weekend > List Event Ids** prints the vocabulary; the groups are
`session-*` (you drive), `watch-*` (needs `"series"`), `team-*`, `official-*`, `media-*`, `fan_event-*`,
`sponsor_event-*`, `rest`.

Optional per-booking overrides: `minutes`, `title`, `subtitle`, `fee`, `skipMoney`, `skipAppeal`,
`skipReason`, `mandatory` (0 default / 1 force / 2 force off), `requires` (an event id that must have
happened first), and `area`.

### Markers: where a booking actually happens

Venues used to be worked out from the pit lane at runtime, which is how a grandstand marker ended up on the
fence line at the edge of pit road. A track now says where its places are, by having objects there.

**Make a GameObject in the track package and name it `PitBox_Marker`.** That is now the pit box: the
objective arrow points at it, and the booking starts when the player is inside it. Recognised names —

`PitBox_Marker` · `Motorhome_Marker` · `DriversRoom_Marker` · `SigningFence_Marker` ·
`SponsorSuite_Marker` · `IntroStage_Marker` · `Grandstand_Marker`

Matching ignores case, spaces and underscores, so `Pitbox_Marker` and `pit_box_marker` are the same request.
Several aliases work too (`Hospitality_Marker`, `Signing_Marker`, `Stage_Marker`, `PhotoShoot_Marker`).

**The size is the perimeter.** Give the object a Collider2D and its shape *is* the arrival test — a box the
shape of the pit stall means standing anywhere in the stall counts. Any collider works, including a rotated
box or a polygon. With no collider it falls back to a Renderer's bounds, and with neither to
`fallbackRange` as a plain radius.

**Overriding per booking.** The `Marker Location` field on a booking is the name of the object to use:

```json
{ "event": "sponsor_event-photoshoot", "start": "09:45", "markerLocation": "Podium_Marker" }
```

Left blank it falls back to the venue's default name, which the Plan Editor shows under the field along with
whether an object of that name is actually in the open scene. A marker whose name matches no venue —
`Podium_Marker` — is still a marker; it just has to be asked for like this.

### Places you cannot walk to

The grandstands at a road course are across the track, behind a boundary the player is clamped inside. So a
marker can split *where you go* from *where you end up*: give it a **`teleportTo`**, or simply a child object
called **`Seat`** (also `Destination`, `Inside`, `Teleport`), and the marker becomes a gate.

Put the marker at the paddock exit where the player can reach it, put the child in the grandstand seat. Walk
into the marker, press the action button, and a wipe puts you there. `WeekendMarkerGate` is added
automatically — nothing to wire.

A marker with a teleport is **exempt** from the reachability rule, because being at the edge is the point.
One *without* a teleport that sits outside the boundary is still reported as a fault.

Authored markers are never moved by the boundary rule either: where you put it is where it stays, and a bad
one is reported rather than quietly dragged inside.

### Checks

- `Draftmaster > Weekend > Validate All Plans` — every shipped file, with line-level problems.
- `Draftmaster > Weekend > Check Markers In Open Scene` — markers outside the boundary with no teleport to
  excuse them, duplicate names, markers with no size, and every venue still being guessed at runtime.
- `WeekendPlanTests` (EditMode) fails the build on a bad plan file, an event id that does not resolve, any
  `ActivityKind` the catalogue cannot express, and any venue whose default marker name does not resolve back
  to it.
- `WeekendVenuePresenceTests` (PlayMode) fails if any marker lands outside the walkable paddock.
