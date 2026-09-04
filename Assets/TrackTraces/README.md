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
osmWayId         the OSM way it came from, so it can be looked up or re-fetched
osmName          the name the mapper gave it — how the right ring was identified
publishedMiles   the lap length from TrackDimensions
tracedMetres     the length of the trace as measured, for comparison
geometry         the centreline, lat/lon, closed (first node == last node)
```

## How they were made

An Overpass query for `highway=raceway` ways near the venue, then, of the closed rings it returns, the one a
mapper actually named — a venue usually has several similar rings (the racing surface, edges traced
separately, an infield road course) and length alone cannot tell them apart. Whatever is chosen is then
**checked against the published lap length** and refused if it is more than 12% out, which is what stops a
kart track or a drag strip in the same infield being imported as the speedway.

Re-fetching is a manual step on purpose. These are committed so an import is reproducible and needs no
network, and so that a change to a track's shape shows up as a reviewable diff rather than appearing one day
because somebody edited a map.

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
