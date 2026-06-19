# NPC Dialogue, Side-Quests & Stats

This system lets NPCs talk to the player, hand out side-quests, and lets the
player grow RPG-style stats both from driving and from finishing those quests.
Everything the player earns is stored in the local SQLite database that ships
with the game (`MyDatabase.sqlite`), so progress survives between sessions.

## Scripts

| Script | Responsibility |
| --- | --- |
| `DatabaseManager.cs` | The only place that touches SQL. Owns the schema and exposes static read/write helpers for stats and quests. |
| `StatsManager.cs` | Gameplay-facing stat API with canonical stat keys. Persists through `DatabaseManager`. Raises `OnStatChanged`. |
| `SideQuest.cs` | Plain data model describing a single side-quest. |
| `QuestManager.cs` | Holds the quest catalogue and drives quests `available -> active -> completed`, paying out stat rewards on completion. |
| `DrivingStats.cs` | Tracks distance / top speed / drafting during a race and banks them as stats when the race ends. Also reports the race to `QuestManager`. |
| `DialogueHandler.cs` | (extended) Parses Ink line tags to start/advance/complete quests and grant stats. |

## Database schema

```
PlayerStats(statName TEXT PRIMARY KEY, statValue INTEGER)
SideQuests(questId TEXT PRIMARY KEY, status TEXT, progress INTEGER,
           target INTEGER, rewardStat TEXT, rewardAmount INTEGER)
```

Tables are created on demand by `DatabaseManager.EnsureSchema()` (called from
`DatabaseManager.Start()` and `DrivingStats.Start()`), so no manual migration is
needed.

## Stats

Canonical keys live on `StatsManager`:

- `Endurance` – earned per kilometre driven
- `Pace` – earned for hitting high speeds
- `Drafting` – earned for time spent in the slipstream
- `Charisma` – earned by talking to NPCs / finishing their quests
- `Reputation` – earned by completing quests

## Authoring quests in Ink dialogue

Quest and stat actions are driven by **tags** on Ink lines (anything after `#`).
The `DialogueHandler` parses these every line, so an NPC can give a quest or pay
out stats simply by tagging a line:

```ink
Go run a full race in my colours. #quest_start:RickLemondeLogoLap
Here's a little something for stopping by. #stat:Charisma:1
```

Supported tags:

| Tag | Effect |
| --- | --- |
| `#player` | Existing tag – routes the line to the player's speech bubble. |
| `#quest_start:QuestId` | Offers/begins a side-quest. |
| `#quest_complete:QuestId` | Marks a side-quest finished (pays its reward). |
| `#quest_progress:QuestId:amount` | Advances a quest's counter. |
| `#stat:StatName:amount` | Grants stat points immediately. |

## Quest catalogue

Quests are defined in code in `QuestManager.BuildCatalogue()`. Each quest names
an `objectiveEvent` that gameplay reports against:

- `talk` – completes the instant it starts (pure conversation reward)
- `drive_distance` – needs `target` metres driven
- `race_finish` – needs `target` races finished

`DrivingStats` reports `drive_distance` and `race_finish` at the end of each
race, so a driving quest such as **Cause a Caution** (cover 8 km) progresses
automatically as the player races.

## Wiring in the Unity editor

1. Keep a `DatabaseManager` component in the bootstrap/menu scene (it ensures the
   schema exists).
2. Add a `DrivingStats` component to the race scene (e.g. on the race manager or
   the player car). It reads the `Movement` statics, so nothing else needs
   hooking up.
3. NPC dialogue canvases already carry `DialogueHandler`; the new tag handling is
   automatic once the `.ink` files are (re)compiled by the Ink importer.
