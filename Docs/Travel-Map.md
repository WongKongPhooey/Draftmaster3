# Travel Map — the road trip between races

The breakout loop: after each race, the player drives the USA road map to the next venue, spending a
limited stop budget on detours to useful locations. Geography is **fixed** (learning what lives where is
the game); junkyard **stock rerolls weekly**. All code in `Assets/Scripts/Travel/`.

## Player flow

1. Race results → **HIT THE ROAD** opens the fullscreen map — an authored Canvas prefab
   (`Assets/Resources/UI/TravelMap.prefab`) with the `TravelMapScreen` binder on its root.
   **SKIP TRAVEL** keeps the old instant weekend loop for testing.
2. **Choose the next race**: click any circuit node. Stop budget = BFS direct route +
   `TravelGraph.DetourAllowance` (2) — enough for a small detour, per the design intent.
3. **Drive**: click an adjacent node, 1 stop each. Arriving anywhere opens the side panel. Minor
   locations are grey "?" dots until first visited — after that they show name + type (`[E]` engine
   shop / `[J]` junkyard) forever. That's the discovery/learning mechanic.
4. **Shop**: junkyards sell a 3-item salvage roll (deterministic per location+week, 45–70% of book
   price, duds *and* gems in the pool — reading the shelf is a skill); engine shops sell a fixed
   catalog at full price. **Buying installs immediately** and scraps the old part (one part per slot).
5. **Race**: at the destination, START RACE WEEKEND → `RaceWeekend.ResetWeekend()` + scene load
   (falls back to reloading the current scene when the circuit's scene isn't in the build).
6. Out of stops away from the destination → **tow** ($2,000, clamped at $0 — never a softlock).

`F9` toggles the map in any scene (dev hotkey, self-bootstrapped like QuestHUD).

## Editing the map layout

The map is a **Canvas prefab** you edit in Prefab Mode (`Assets/Resources/UI/TravelMap.prefab`):

- Each node is a `TravelNodeMarker` child under `MapPlot/Nodes` — **drag it to move the node on the
  map**. The marker's `nodeId` must match a `TravelGraph` node id; its RectTransform position is the
  node's position. Highway lines are rebuilt from marker positions at runtime, so they follow.
  For an in-editor preview, right-click the `TravelMapScreen` component → **Rebuild Edges**.
- Styling (fonts, colors, panel layout, dot/halo/label sizes) is all ordinary uGUI — edit freely.
- Menu items (`Draftmaster > Travel Map`):
  - **Build Prefab** — one-shot generator (refuses to overwrite an existing prefab).
  - **Sync Node Markers** — after adding nodes to `TravelGraph` in code, adds markers for them
    (never moves existing ones); position the new markers by hand.
  - **Force Rebuild Prefab** — regenerate from `TravelGraph` coords, **losing hand edits**.
  - **Open (Play Mode)** — opens the map without the F9 key (automation convenience).
- `TravelGraph` remains the source of truth for **topology** (edges, BFS routing, shop stock);
  `TravelGraph.pos` is only used to seed marker positions at build/sync time.

## Systems

- **`TravelGraph`** — code-defined map (DummyDrivers pattern; MCP can't grow SO arrays): 26 circuit
  nodes (id = scene name) + 13 made-up locations on the highways between them, e.g. *Pitt Brothers
  Engine Builders* (a dead-end Maine detour with the premium motors) or *Mojave Boneyard*.
  Normalized coords, x west→east, y north→south. `ShortestHops` = BFS, every edge costs 1 stop.
- **`TravelState`** — PlayerPrefs: `travel.node`, `travel.dest`, `travel.stops`, `travel.week`,
  `travel.visited` (CSV), `travel.bought.<week>.<loc>.<part>`. Week ticks when a destination is
  chosen, so junkyard shelves are stable across one leg. Movement feeds `PlayerStatsLedger`
  (`travelstops`, `locations`, `visit.<locationId>`) — locations are immediately quest-able via the
  existing StatThreshold objective, no new quest code.
- **`PartCatalog`** — code-defined parts (engines / gearboxes / tires / chassis) with coarse stat
  mods: `topSpeedAdd` (mph), `accelScale`, `gripAdd` (lateral g), `wearScale`. `junkyardOnly` parts
  (barn-find motor, tired 305) only appear in salvage rolls.
- **`PlayerCarBuild`** — installed part per slot in PlayerPrefs (`car.part.<slot>`).
  `Outfit(VehicleInfo)` returns a runtime **clone** with mods applied (topSpeed, maxLateralG,
  tireWearRate, accel curve values+tangents scaled) — shared VehicleInfo assets are never mutated.
  Hooked in `PlayerVehicleController.Start` for the human car only (`!externalInput`).
- **`PlayerWallet`** — PlayerPrefs cash (`career.cash`, starts $5,000). `RaceDirector` pays
  `PayoutForPosition` (P1 $12,000 → floor $800) on final classification and shows it on the results
  headline.

## Known limits / next steps

- Only the physics side (PlayerVehicleController) reads the outfitted clone; SplineDriver /
  EngineGearbox on the player car still reference the stock asset (only matters in AI-driven
  Broadcast mode — slightly conservative brain targets).
- Team switch moves the human into a stock car: parts belong to your car, not to you.
- Circuits without spline scenes in the build fall back to re-running the current scene; travel
  position still advances.
- Natural extensions: delivery quests keyed to `visit.<locationId>`, uninstalled-parts inventory +
  garage install UI, calendar-driven destinations instead of free choice, per-shop haggling by
  driver relationship.
