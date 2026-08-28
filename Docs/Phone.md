# The phone

The player's phone slides up from the bottom of the screen while they're on foot. **P** opens and closes
it; **Esc** backs out one level (app → home → away).

It is not a pause. The paddock keeps moving behind it — the player just stops walking while it's up.

## What's on it

Six tiles, two by three, and all six are filled. A seventh app is kept but not reachable from the grid,
which is loud enough to notice.

| App | Shows | Reads from |
| --- | --- | --- |
| **Tasks** | What stands between the player and progress: the weekend's next step, sponsors unsigned or unpainted, fan appeal, quests waiting to be reported | `RaceWeekend`, `LapTimingManager`, `RaceDirector`, `SponsorBook`, `FanAppeal`, `QuestManager` |
| **Notes** | Who asked for what. Every accepted side quest is logged with the person's name and their own words; finished ones drop to the bottom rather than disappearing | `PhoneNotes` (PlayerPrefs), `QuestManager` for progress |
| **SoBuzz** | Fan appeal as a gauge and a follower count, plus a feed: which brands are watching, which are still out of reach and by how much, and what the crowd is saying | `FanAppeal`, `SponsorCatalog`, `SponsorTerms`, `SponsorBook`, `PlayerStatsLedger` |
| **Schedule** | The weekend timetable: where the clock is, what is still to come today, your own three sessions wherever they fall, and the five meters | `WeekendDirector`, `WeekendLedger`, `FanAppeal` |
| **Points** | The three championships. This weekend's races (who won, or when they are due), a results feed, and the tables with the player's own series first | `SeasonChampionships` |
| **DrivR** | The form guide: every driver ranked by ability, tap for their season results, craft stats, track-type stats and off-track ratings | `Drivers` and `Results` tables via `DatabaseManager` |

**SoBuzz is not flavour text.** A brand posting "we're watching" means `SponsorTerms.CanApproach` is true
for the player's current standing, so that rep will deal when the player finds them in the pit lane. A
locked brand shows exactly how much more appeal is needed. Posts are seeded on `RaceWeekend.WeekendId`, so
the feed is stable within a weekend and different at the next one.

**DrivR's stats are the ones the AI drives on** — `Qualifying` and `Consistency` set pace,
`Aggression` skews the racing line (`AIDriverBinding`) — so a driver who reads aggressive races that way.

**Where the player is told any of this.** Nothing else in the paddock mentions the phone, so the first
weekend of a career books fifteen minutes at the pit box for it: `ActivityKind.Orientation`, 09:30 Friday
morning, weekend zero only. The crew chief names the key, TASKS (what is outstanding, and what the tile's
badge counts) and NOTES (who asked for what), and finishing it leaves the same summary in NOTES as an
unread note — `WeekendDirector.LeavePhoneCribSheet`. The key in those lines is read off `PhoneUI.toggleKey`
at build time (`WeekendScripts.PhoneKeyName`), so rebinding the toggle cannot leave the one conversation
that explains the phone naming a dead key. Content: `Weekend/Core/Conversations/OrientationContent.cs`.

## Adding an app

One subclass and one line:

```csharp
public class PhoneRadioApp : PhoneApp
{
    public override string Id => "radio";
    public override string TileName => "RADIO";
    public override string TileSubtitle => "Spotter chatter";
    public override Color Accent => PixelGUI.Gold;
    public override int Badge => UnreadMessages;          // draws a red count on the tile

    public override float Draw(float x, float y, float w)
    {
        float y0 = y;
        y += Section(x, y, w, "TODAY");
        y += Row(x, y, w, "Spotter", "clear", PixelGUI.TextDim);
        y += Body(x, y, w, "…");
        return y - y0;                                    // the device scrolls on this height
    }
}
```

Then register it in `PhoneUI.BuildApps()` (or call `PhoneUI.Register(new PhoneRadioApp())` from your own
system's bootstrap). Past six it stays registered but unreachable from the grid.

`PhoneApp` gives every app the same drawing vocabulary — `Section`, `Row`, `Body`, `Meter`, `Plate`,
`Empty` — each taking a top-left corner and a width and returning the height it used, so `Draw` is a
running `y += …` and the total is what the device scrolls.

## Writing to it from elsewhere

```csharp
PhoneTasksApp.Push("engine", "Collect the engine from the truck");   // adds a line under REMINDERS
PhoneTasksApp.Resolve("engine");                                     // takes it away again

PhoneNotes.Record("id", "Title", "Marla Boyd", "What she wanted");   // a note that isn't a quest
PhoneNotes.RecordQuest(quest, speakerName);                          // QuestGiverNPC does this on accept
PhoneNotes.ResolveQuest(quest);                                      // QuestManager.Complete does this

PhoneUI.Open("drivr");                                               // open straight into an app
```

Both stores are PlayerPrefs-backed, so they survive the scene reloads between sessions.

## How it behaves

- **Arms only on foot.** It polls for an `OnFootController` every half second; in the car there's nothing
  to open.
- **Refuses to open while the player is held** by a conversation or a cutscene (`MovementLocked` already
  set by someone else), and takes that lock itself while it's up — which is also what stops an interact
  press from starting a conversation through the phone.
- **Owns Escape while open.** `RacePauseMenu` stands down (`PhoneUI.IsOpen`), so the first press puts the
  phone away rather than pausing the game.
- **Self-bootstraps** like `RacePauseMenu` and `DriverInfoPanel` — no scene wiring, no prefab.
- Drawn with the Iron Oval kit (`PixelGUI`) at `GUI.depth = -50`, so it sits over the other IMGUI panels.

## Files

`Assets/Scripts/UI/Phone/` — `PhoneUI` (device, slide, home grid, input), `PhoneApp` (base + drawing
vocabulary), `PhoneTasksApp`, `PhoneNotesApp`, `PhoneNotes` (store), `PhoneSoBuzzApp`, `PhoneDrivRApp`,
`PhoneScheduleApp`, `PhoneChampionshipApp`.
