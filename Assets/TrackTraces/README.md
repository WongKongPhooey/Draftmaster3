# Traced track centrelines

One JSON file per venue, each holding a circuit's centreline as it is drawn in OpenStreetMap, plus the
published lap length it was checked against.

## Why these exist

The spline system used to invent every shape. `OvalGeometry` solves one from the published lap length and a
guessed share of it spent cornering, which is fine for a plain oval and wrong for anything else — a published
figure describes a *distance*, not a *shape*. "A 1,551 ft back stretch on a 1.022 mile lap" is equally true of
a long thin oval and of a rounded triangle, and Phoenix, which is the second, was generated as the first: its
corners came out 34 m tighter than the real ones and its straights 92 m too long.

A trace does not have that problem. It says where the road actually goes.

## What is in a file

```
trackId          the id used everywhere else (Resources/Tracks/<id>.asset, the calendar, the packages)
osmName          the venue name the ring was identified against
foundBy          which query turned it up, so a hand-fixed trace can say so
publishedMiles   the lap length from TrackDimensions
tracedMetres     the length of the trace as measured, for comparison
namedShare       how much of the ring runs on way a mapper actually named
geometry         the centreline, lat/lon, first node == last node
```

## How they were made

An Overpass query for `highway=raceway` ways near the venue, then a ring is found among them:

- a circuit mapped as **one closed way** is taken as it stands;
- one mapped as **several open ways** — most of the big ones — is walked: follow the road and, at every
  junction, carry straight on. That is what a lap does; the pit road and the infield roads leave at an
  angle. Small gaps in the mapping are hopped if the road carries on straight the other side.

Every ring is then **checked against the published lap length** and refused if it is more than 12% out,
which is what stops a kart track or a drag strip in the same infield being imported as the speedway. A ring
that **crosses itself** is refused outright: that is the signature of infield service roads chained into a
loop of roughly the right size, which is exactly what Daytona offers if you let it.

Re-fetching is a manual step on purpose. These are committed so an import is reproducible and needs no
network, and so that a change to a track's shape shows up as a reviewable diff rather than appearing one day
because somebody edited a map.

## Which venues have one, and which do not

Twenty-one venues have a trace. Fifteen of those import: the rest are refused by the importer itself,
because the shape it reads does not close to within 3% of its own lap, and a reading that far out is worse
than the generated or hand-authored geometry it would replace. Refused today:
Mid-Ohio, Milwaukee, Portland, Rockingham, Sonoma. Watkins Glen is skipped on purpose — it is hand-measured
off satellite imagery and a trace does not improve on it. Atlanta's old trace (refused) ran down pit road and
through the AMS Small Oval; it was rewritten 2026-11-18 from `track_reference.py` candidate 0 (the oval itself)
and now imports. Kansas's old trace ran 34% down pit road; it was rewritten the same day from the
raceway ring and now imports.
Darlington's ring always imported, but the old importer read each end as one arc, so the egg came back as a
symmetric four-piece oval (38 m RMS off the real track); its trace was rewritten 2026-11-18 from the same OSM ring
for the curvature importer, with a mapped pit lane cut down to the way's front-stretch run (the way also wraps both
turn aprons). Re-importing it in Unity is still to do.

IRP had no trace (its generated oval was a long thin 464 x 156 m, 24.5 m RMS off the real one). One was written
2026-11-18 from `track_reference.py` candidate 0 (OSM's raceway ring, on the racing surface all round), with the
mapped pit lane cut to the way's front-stretch run (it also wraps the Turn 1 and Turn 3-4 aprons). Importing it in
Unity is still to do.

COTA had no trace (its RoadCourseLayouts sketch was 68 m RMS off the real circuit). One was written 2026-11-20 from
`track_reference.py` candidate 0 (OSM's raceway ways round the Grand Prix circuit, counter-clockwise as raced), with
the mapped Pit Lane way and the start/finish line estimated mid front straight (OSM marks none). Read headless at the
road-course window it lies 2.5 m RMS off the trace; importing it in Unity is still to do.

Lime Rock had no trace (its RoadCourseLayouts sketch was 46.7 m RMS off the real circuit). One was written
2026-11-20 from `track_reference.py` candidate 0 (OSM's raceway ways round the full circuit with the Climbing Turn,
no chicane - 1.498 mi against the 1.5 mi in TrackDimensions), raced clockwise, start/finish on the front straight and
the mapped Pit Lane way 35341695. Imported the same day: 70 pieces, closed 2.6 m -> 0, and the package re-dressed and
its line retrained; it now measures 1.4 m RMS against the real track.

Mexico City's old batch trace was refused on the 3% closure check. It was rewritten 2026-11-20 from
`track_reference.py` candidate 0 (the same OSM raceway ways round the 2015-present Grand Prix circuit through the Foro
Sol), raced clockwise, start/finish estimated on the main straight (OSM marks none) and the mapped Pit Lane ways
638504647 + 772763791. The candidate also detoured 33 m off the back straight round unnamed raceway way 1128592397, an
access loop rather than the circuit; that stretch was replaced by the straight it bypasses (lap +1.02% -> +0.05%). Read
headless at the road-course window it lies 2.1 m RMS off the trace; importing it in Unity is still to do.

Dover, Iowa, Las Vegas and Pocono had 2026-09 traces of OSM's raceway ring (one closed way each, Las Vegas a walked
chain) read by the old importer as one arc per end, which left them 4-7 m RMS off the real tracks. They were rewritten
2026-11-22 from `track_reference.py` candidate 0 (the same rings) for the curvature importer, `--start` at the point the
game's start/finish already sat on (the back stretch at Dover, Iowa and Las Vegas, the west end of the front straight at
Pocono) so each package keeps its frame. Dover, Iowa and Las Vegas carry no pit lane: their mapped pit roads are on the
real front stretch, the far side of the lap from the game's line, so pit road stays where it was as a share of the lap.
Pocono's mapped Pit Lane (way 563465040) is on the same straight as the line and is used. Imported, re-dressed,
re-exported and retrained the same day; they measure 0.6 / 0.5 / 1.0 / 3.1 m RMS.

The twelve venues with no trace at all — Martinsville, North Wilkesboro, Nashville, Homestead, Charlotte,
Daytona, Talladega, Indianapolis, Road America and Watkins Glen — are not a bug in the
fetch. **OSM's raceway mapping is incomplete at those venues**: Talladega has 3,345m of its 4,281m lap drawn
and Daytona 3,368m of 4,023m, with the rest simply not there. A venue with no usable trace keeps its
generated geometry, which is why the generator stays.

### Traced from the outer wall instead

Daytona's raceway lines are incomplete, but its outer SAFER wall is mapped as one closed `barrier=wall` way.
On an oval the racing surface is a fixed width inside that wall, so `Tools/trace_from_wall.py` offsets the
wall inward by half the surface width and writes the centreline here in the same format (plus
`surfaceWidthMetres` / `apronWidthMetres`). Daytona's trace (11 m surface, 3.5 m apron) was checked against
USGS NAIP imagery: the inside-edge offset lands on the painted yellow line on the straights and in the banked
turns, and the lap reads 4,011.5 m against a published 4,023 m. Talladega is the next candidate for the same
treatment.

### Traced from the inside edge (Martinsville)

Martinsville's `highway=raceway` ring does close (827 m), but checked against NAIP imagery it is centred on
the straights and balloons 10-18 m wide at both turn apexes — a 16-node sketch of each half. Its inside edge is
better mapped: two `barrier=wall` ways (448514914, 448514913) follow the pit wall and the yellow line round
both turns, and the one gap (turn 3 exit onto the back stretch) is filled by way `-1` in
`Tools/osm/Martinsville.json`, read off the yellow line's pixels in the imagery. `trace_from_wall.py --inner`
joins them and offsets 7.5 m outward (15 m surface, measured wall to pit wall on both straights). The outer
offset lands on the outside wall in both turns. The centreline is 787 m against a published 846 m; the
importer scales it up to the published lap as it does every trace. Pit road is the front-stretch run of
mapped way 402168723 (the real one wraps the whole infield; the game's pit road is one straight).

### From the track-reference report (Charlotte)

`Tools/track_reference.py Charlotte --write-trace=0 --start=apex --pit=116034339,401660631,1052107105,401660628`
(2026-11-18): OSM's raceway ring for the quad-oval itself (candidate 0, 2,362 m against a published 2,414 m - mapped
a little inside the groove, which the rescale absorbs), resampled and smoothed, starting at the front-stretch apex.
Its pit lane is four mapped ways joined north to south, 668 m. Checked without the Editor by
`TracedOvalImportTests`: closes to 3 m, lies ~1 m RMS off the trace once built, pit road fits at 820 m.

Road America, Chicago, Indianapolis, Sonoma, Portland, Talladega and Rockingham were written 2026-11-22 by
`track_reference.py --write-trace=0` (Chicago from its synthetic street-centreline way, -1), and IRP and Mexico City
imported as written. Road courses are raced clockwise (`--clockwise`; Sonoma's direction from OSM's one-way ways).
Start/finish lines are estimates mid pit lane except Indianapolis (OSM's start-finish node on the bricks) and Chicago
(Columbus Dr by Buckingham Fountain). Sonoma's and Rockingham's `pitLane` were trimmed by hand to the front-stretch
nodes of their ways, which wrap a turn apron; Chicago has no mapped pit lane. Commands and results per venue are in
`Tools/TrackReference/audit.json`.

## What a trace cannot tell you

**Banking** is not recorded in OSM at all, and **width** almost never is for raceways. Both still come from
`TrackDimensions`, along with pit speed limits and lap counts. `OsmTrackImporter` takes the plan view from
here and everything else from there.

## Licence

> Data © OpenStreetMap contributors, made available under the Open Database Licence (ODbL).
> https://www.openstreetmap.org/copyright · https://opendatacommons.org/licenses/odbl/

These files are a Derived Database and carry the ODbL with them. Track shapes generated from them in-game are
most likely a *Produced Work*, which needs attribution rather than share-alike — but that is a judgement worth
making deliberately before shipping, not after. At minimum the credits should carry the line above.
