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
- Styling (fonts, colors, panel layout, dot/halo/label sizes) is all ordinary uGUI — edit freely,
  but note that **Restyle (Iron Oval) overwrites it**: that pass is the source of truth for the look.
  The canvas is authored at 1920x1080 (scale factor 1 at 1080p), so every type size is a whole
  multiple of its face's pixel cell — Silkscreen 8/16/24, VT323 16/32, Pixelify Sans 20 — and every
  9-slice runs at 2x via `pixelsPerUnitMultiplier = 0.5`. Off-ladder sizes resample into mush.
- Menu items (`Draftmaster > Travel Map`):
  - **Build Prefab** — one-shot generator (refuses to overwrite an existing prefab).
  - **Sync Node Markers** — after adding nodes to `TravelGraph` in code, adds markers for them
    (never moves existing ones); position the new markers by hand.
  - **Force Rebuild Prefab** — regenerate from `TravelGraph` coords, **losing hand edits**.
  - **Restyle (Iron Oval)** — re-skins the prefab in place from the pixel kit (frames, palette, the
    three pixel faces, header icons, per-node marker sizes, the key, the two pins) and re-bakes the
    highways. **Node positions are not touched** apart from the Team Factory's, so a hand-dragged
    layout survives it. Idempotent — run it whenever the kit or the marker rules change. It also
    repairs a side panel missing its `WalkButton`.
  - **Preview PNG** — renders the map to `Assets/Screenshots/travelmap_preview.png` with a sample
    week dressed into it. The canvas is Screen Space - Overlay, which never shows up in an ordinary
    editor screenshot, so this is how you look at a restyle without entering Play Mode.
  - **Open (Play Mode)** — opens the map without the F9 key (automation convenience).
- `TravelGraph` remains the source of truth for **topology** (edges, BFS routing, shop stock);
  `TravelGraph.pos` is only used to seed marker positions at build/sync time — except the Team
  Factory, whose marker `Restyle` re-seats from the graph every run.

### How the board reads

The map is a blue field (solid `MapField` navy with the kit's dither tiled over it — an `Image` tint
multiplies, so the bare dither could only ever be darker than the backdrop) inside a cream frame:

- **Dots are flat coloured squares**, sized by importance: racetrack 16px gold, parts shop 12px blue,
  salvage yard 12px rust, grey while a minor location is still a "?". Names are drawn in their dot's
  colour. **Do not put a kit icon on a map dot**: the icons are 16px art with their own colours baked
  in, so a saturated tint at that size is a smudge (a tyre reads as a hole). Icons are used where
  there is room — the factory badge, the two pins, the header.
- **The Team Factory** is a 32px pale wrench on a deep-teal plate inside its own gold frame, which
  stays lit whether or not the state ring is (your shop is never shut).
- **Two pins hop about** instead of a badge per node: `herePin` (the map pin, where you stand) and
  `destPin` (the chequered flag, this week's race), both children of `MapPlot/Nodes` so
  `TravelMapScreen` can park one on a marker by copying its `localPosition`. Neither is a raycast
  target, so they never eat the click on the dot underneath.
- **Roads read in three states** (`TravelMapScreen.TintEdges`): gold and 5px for a road out of where
  you are standing, teal for the factory's four slip roads, steel blue 3px for the rest of the
  country. Lit roads are pushed to the front of the sibling list or the fifty that cross them win.
- **The key** sits at plot-local `(910, -8)` — the largest node-free rectangle on the board, measured
  against every dot and label. Its chips are the dots themselves at map size, so it is a sample of
  the board rather than a second set of symbols.
- `Preview PNG` drives the real `TintEdges` / pin code via `TravelMapScreen.PreviewState`, so the PNG
  shows the live rules rather than a hand-made approximation.

## Systems

- **`TravelGraph`** — code-defined map (DummyDrivers pattern; MCP can't grow SO arrays): 54 circuit
  nodes (id = scene name) + 20 made-up locations on the highways between them, e.g. *Pitt Brothers
  Engine Builders* (a dead-end Maine detour with the premium motors) or *Mojave Boneyard*, plus the
  **Team Factory**. Normalized coords, x west→east, y north→south. `ShortestHops` = BFS, every edge
  costs 1 stop.
- **The Team Factory** (`team_factory`) — your own shop, in the middle of the map at normalized
  `(0.463, 0.574)`. That is not a lattice cell on purpose: the central cells are all taken
  (Indianapolis sits on the dead centre of the board), so the spot is the most central one that still
  has ~95px of clear air round it at the authored 1470x950, labels included. Unlike the shops and
  yards it is not mounted on one highway: `FactoryHub` gives it its own slip roads to the four
  nearest circuits (Indianapolis, Salem, DuQuoin, Nashville Superspeedway), so it is one stop off a
  route through the middle of the country rather than a dead end, and those four roads are drawn in
  its own teal. On the map it is the only teal thing on the board — a wrench on a gold-edged plate,
  twice the size of a circuit — and it is never a grey "?": it is yours, so you always know where it
  is. Move it in code and `Restyle` re-seats the marker (the one node whose position the graph owns
  rather than the prefab). **Collect, don't buy:** `PartCatalog.FactoryStock(week)` is the bench — the shop finishes one
  `factoryOnly` part every `FactoryWeeksPerPart` (2) weeks, in catalogue order (engine, gearbox,
  tyres, chassis), each one better than what the same slot costs on the road. They are free and the
  COLLECT button installs them like any other part. Taken parts are gone for good
  (`TravelState.WasCollected`, keyed by part id, *not* by week like the junkyard shelves), so parts
  pile up on the rack until somebody drives out there — which is the point of the node.
- **`TravelState`** — PlayerPrefs: `travel.node`, `travel.dest`, `travel.stops`, `travel.week`,
  `travel.visited` (CSV), `travel.bought.<week>.<loc>.<part>`, `travel.collected.<part>`. Week ticks when a destination is
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
