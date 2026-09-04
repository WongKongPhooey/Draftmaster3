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

Twenty-one venues have a trace. Thirteen of those import: the rest are refused by the importer itself,
because the shape it reads does not close to within 3% of its own lap, and a reading that far out is worse
than the generated or hand-authored geometry it would replace. Refused today: Atlanta, Kansas, Mexico City,
Mid-Ohio, Milwaukee, Portland, Rockingham, Sonoma. Watkins Glen is skipped on purpose — it is hand-measured
off satellite imagery and a trace does not improve on it.

The twelve venues with no trace at all — Martinsville, North Wilkesboro, Nashville, Homestead, Charlotte,
Daytona, Talladega, Indianapolis, COTA, Road America, Lime Rock and Watkins Glen — are not a bug in the
fetch. **OSM's raceway mapping is incomplete at those venues**: Talladega has 3,345m of its 4,281m lap drawn
and Daytona 3,368m of 4,023m, with the rest simply not there. A venue with no usable trace keeps its
generated geometry, which is why the generator stays.

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
