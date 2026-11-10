#!/usr/bin/env python3
"""Export Draftmaster 2's Master Draft data from the surviving C# into Assets/Resources/MasterDraft/*.json.

Stdlib only. Re-run whenever the DM2 source data or the livery sprites change:

    python Tools/masterdraft_export.py            # writes every part this script knows
    python Tools/masterdraft_export.py --check    # exit 1 if a written file would change

Parts (see Docs/MasterDraft-Rework.md for the row plan):
  cars    -> Assets/Resources/MasterDraft/Cars.json, from Assets/Scripts/DriverNames.cs
  series  -> Assets/Resources/MasterDraft/Series.json, from Assets/Scripts/SeriesData.cs

Cars
----
DriverNames.cs fills fixed 101-slot arrays per carset (cup2020Names, cup2020Rarity...) with one literal
assignment per line, sometimes from another carset's function (cup20() sets cup1979Rarity[43]), so the
parser reads every `array[index] = literal;` line in the file and keys it on the array, not the function.
The array -> carset prefix map comes from the `allNames.Add("cup20", cup2020Names)` block, the carset list
from `listWinnableCarsets()`, display names from `carsetNames()`. A car's number is its array index.

A car goes in the catalogue when its carset is winnable, DM2 gives it a driver, and its livery sprite
Assets/Resources/{prefix}livery{number}.png exists (a sprite with no DM2 driver is a spare paint, not a car).
Gaps in the DM2 data are filled and reported on stdout: a missing or invalid driver type (irl24 #75 has
"Sato", the driver's name) takes the same driver's type from another carset, else "Rookie"; rarity outside
1-4 is clamped; no team -> "IND"; no manufacturer -> the carset's most common one.

Car id = "{prefix}_{number}" (cup22_26). The underscore keeps ids unambiguous whatever the prefix ends in.

Series
------
SeriesData.loadSeries() fills fixed [menu, sub] arrays with one literal per line (offlineSeries[0,0] =
"Take The Wheel";), so the parser reads every `array[i] = literal;` / `array[i,j] = literal;` line, the same way
the cars part reads DriverNames.cs. A menu is a top-level tile ("Rookies"), a series is one of its sub-series.
Menus keep DM2's order; series inside a menu keep DM2's display order (offlineCustomListOrder when the menu has
one). "Mods" (menu 10) has no sub-series in DM2 -- its series came from installed mods -- so it is not exported.

Restrictions (Core Restrictions.cs reads them): minLevel, minClass and one rule from offlineMinType --
Team (offlineMinTeam), Manufacturer (offlineMinManu), Car (offlineExactCar, a car number in any carset),
Driver (a driver name; challenges only), Type (offlineMinDriverType), Rarity (offlineMinRarity). DM2's garage
matched the Rarity rule exactly (GarageUI.meetsRestrictions: getRarity != seriesRarity), so "Born To Win" takes
4-star cars only; the separate minRarity field (>=) stays 0 for every DM2 series and is there for challenges.

Prizes: DM2's offlinePrizes code (SeriesData.ListRewards) becomes a car filter -- carsets, team, manufacturer,
driverType, minRarity/maxRarity (0 = any) -- so Core can draw from the catalogue without the switch. Two codes
fell through ListRewards' default (every winnable car) and are exported as what they plainly meant instead:
"Rookie" (Rookie Season) -> driver type Rookie like "Rookies"; "Indy" / "Indy1" (the Indy Ovals seasons) ->
the IndyCar carsets (irl23, irl24, irc00), "Indy1" capped at one star like "FRD1". "" = no prize.

Fuel: SeriesUI copied offlineFuel into SeriesFuel, but loadSeries never sets it and the spend was commented out
(TrackUI.startRace), so every DM2 series costs 0. Exported as-is so the field exists for tuning.

Tracks: a tracklist is DM2 track ids ("1,2,4"). DM2_TRACKS below maps each id to a real TrackCatalog id
(Assets/Scripts/Tracks/Core/TrackDimensions.cs); venues the catalogue doesn't have go to the nearest real track
of the same kind, and the script refuses to write if a mapped id is missing or of another kind. Each round
carries DM2's base laps (TrackData.loadTrackLaps) for SeriesRules.RaceLaps.
"""
import argparse
import collections
import json
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DRIVER_NAMES = os.path.join(ROOT, "Assets", "Scripts", "DriverNames.cs")
RESOURCES = os.path.join(ROOT, "Assets", "Resources")
OUT_DIR = os.path.join(RESOURCES, "MasterDraft")

# Field suffix on the DM2 array name -> catalogue field.
FIELDS = {"Names": "driver", "Teams": "team", "Manufacturer": "manufacturer", "Rarity": "rarity", "Types": "type"}
DRIVER_TYPES = ("Rookie", "Strategist", "Closer", "Intimidator", "Blocker", "Pusher", "Dominator", "Legend")

ASSIGN = re.compile(r'^\s*([A-Za-z_][A-Za-z0-9_]*)\[(\d+)\]\s*=\s*("(?:[^"\\]|\\.)*"|-?\d+)\s*;')
ALL_ADD = re.compile(r'^\s*all(Names|Teams|Manufacturer|Rarity|Types)\.Add\("([^"]+)",\s*([A-Za-z0-9_]+)\)\s*;')
WINNABLE = re.compile(r'^\s*allWinnableCarsets\[(\d+)\]\s*=\s*"([^"]+)"\s*;')
CARSET_NAME = re.compile(r'^\s*allCarsetNames\.Add\("([^"]+)",\s*"((?:[^"\\]|\\.)*)"\)\s*;')


def strip_comment(line):
    """Drop a // comment that is not inside a string literal."""
    in_str = False
    i = 0
    while i < len(line):
        c = line[i]
        if c == "\\" and in_str:
            i += 2
            continue
        if c == '"':
            in_str = not in_str
        elif not in_str and line.startswith("//", i):
            return line[:i]
        i += 1
    return line


def parse_driver_names(path):
    with open(path, encoding="utf-8-sig") as f:
        lines = [strip_comment(l) for l in f.read().splitlines()]

    array_to = {}  # array name -> (prefix, field)
    winnable = {}
    nice = {}
    values = collections.defaultdict(dict)  # array name -> {index: value}
    for line in lines:
        m = ALL_ADD.match(line)
        if m:
            array_to[m.group(3)] = (m.group(2), FIELDS[m.group(1)])
            continue
        m = WINNABLE.match(line)
        if m:
            winnable[int(m.group(1))] = m.group(2)
            continue
        m = CARSET_NAME.match(line)
        if m:
            nice[m.group(1)] = json.loads('"' + m.group(2) + '"')
            continue
        m = ASSIGN.match(line)
        if m:
            raw = m.group(3)
            values[m.group(1)][int(m.group(2))] = json.loads(raw) if raw.startswith('"') else int(raw)

    carsets = collections.defaultdict(lambda: collections.defaultdict(dict))  # prefix -> number -> field -> value
    for array, (prefix, field) in array_to.items():
        for index, value in values.get(array, {}).items():
            carsets[prefix][index][field] = value
    return [winnable[i] for i in sorted(winnable)], nice, carsets


def livery_exists(prefix, number):
    return os.path.isfile(os.path.join(RESOURCES, "%slivery%d.png" % (prefix, number)))


def build_cars(log):
    winnable, nice, carsets = parse_driver_names(DRIVER_NAMES)
    if not winnable:
        raise SystemExit("No winnable carsets parsed from %s" % DRIVER_NAMES)
    # Same driver's type in another carset, for filling a carset's missing or invalid one.
    type_by_driver = {}
    for data in carsets.values():
        for d in data.values():
            if d.get("driver") and d.get("type") in DRIVER_TYPES:
                type_by_driver.setdefault(d["driver"], d["type"])
    cars = []
    carset_rows = []
    for prefix in winnable:
        data = carsets.get(prefix, {})
        manus = collections.Counter(d["manufacturer"] for d in data.values() if d.get("manufacturer"))
        default_manu = manus.most_common(1)[0][0] if manus else "CHV"
        count = 0
        for number in range(0, 101):
            if not livery_exists(prefix, number):
                continue
            d = data.get(number, {})
            driver = d.get("driver")
            if not driver:
                log("  %s_%d skipped: livery sprite but no DM2 driver" % (prefix, number))
                continue
            fills = []
            team = d.get("team")
            if not team:
                team = "IND"
                fills.append("team")
            manu = d.get("manufacturer")
            if not manu:
                manu = default_manu
                fills.append("manufacturer")
            rarity = d.get("rarity", 0)
            if not 1 <= rarity <= 4:
                fills.append("rarity(%d)" % rarity)
                rarity = min(max(rarity, 1), 4)
            dtype = d.get("type")
            if dtype not in DRIVER_TYPES:
                fills.append("type(%s)" % dtype)
                dtype = type_by_driver.get(driver, "Rookie")
            if fills:
                log("  %s_%d filled: %s" % (prefix, number, ", ".join(fills)))
            cars.append({
                "id": "%s_%d" % (prefix, number),
                "carset": prefix,
                "number": number,
                "driver": driver,
                "team": team,
                "manufacturer": manu,
                "rarity": rarity,
                "type": dtype,
                "livery": "%slivery%d" % (prefix, number),
            })
            count += 1
        carset_rows.append({"id": prefix, "name": nice.get(prefix, prefix), "cars": count})
        log("%s: %d cars" % (prefix, count))
    return {"version": 1, "source": "Tools/masterdraft_export.py from Assets/Scripts/DriverNames.cs",
            "carsets": carset_rows, "cars": cars}


# --- Series ------------------------------------------------------------------------------------------------

SERIES_DATA = os.path.join(ROOT, "Assets", "Scripts", "SeriesData.cs")
TRACK_DIMENSIONS = os.path.join(ROOT, "Assets", "Scripts", "Tracks", "Core", "TrackDimensions.cs")

# DM2 track id -> real catalogue id. DM2's TrackData.cs (deleted in 2217821d; `git show
# 2217821d^:Assets/Scripts/TrackData.cs`) named each id's venue and base laps; those are copied here since the
# file is gone. kind = the catalogue's TrackKind the DM2 venue would have, which the mapped track must share.
# (dm2 id, DM2 code name, DM2 display name, base laps, catalogue id, kind, why)
DM2_TRACKS = [
    (1, "Daytona", "Daytona Beach, FL", 4, "Daytona", "Superspeedway", ""),
    (2, "Atlanta", "Atlanta, GA", 5, "Atlanta", "Speedway", ""),
    (3, "LasVegas", "Las Vegas, NV", 5, "LasVegas", "Speedway", ""),
    (4, "Phoenix", "Phoenix, AZ", 6, "Phoenix", "ShortTrack", ""),
    (5, "Fontana", "Fontana, CA", 4, "Michigan", "Speedway",
     "Auto Club Speedway isn't in the catalogue; Michigan is its 2-mile D-oval twin (DM2 drew both as SuperTriOval)"),
    (6, "Martinsville", "Martinsville, VA", 10, "Martinsville", "ShortTrack", ""),
    (7, "FortWorth", "Fort Worth, TX", 5, "FortWorth", "Speedway", ""),
    (8, "Bristol", "Bristol, TN", 10, "Bristol", "ShortTrack", ""),
    (9, "Richmond", "Richmond, VA", 8, "Richmond", "ShortTrack", ""),
    (10, "Talladega", "Talladega, AL", 4, "Talladega", "Superspeedway", ""),
    (11, "Dover", "Dover, DE", 8, "Dover", "Speedway", ""),
    (12, "Kansas", "Kansas City, KS", 5, "Kansas", "Speedway", ""),
    (13, "Charlotte", "Charlotte, NC", 8, "Charlotte", "Speedway", ""),
    (14, "LongPond", "Long Pond, PA", 3, "LongPond", "Speedway", ""),
    (15, "Michigan", "Brooklyn, MI", 4, "Michigan", "Speedway", ""),
    (16, "Joliet", "Joliet, IL", 6, "Kansas", "Speedway",
     "Chicagoland isn't in the catalogue; Kansas is the same 1.5-mile tri-oval design, built the same year"),
    (17, "Kentucky", "Sparta, KY", 6, "LasVegas", "Speedway",
     "Kentucky isn't in the catalogue; Las Vegas is the nearest 1.5-mile tri-oval not already beside it in DM2's lists"),
    (18, "NewHampshire", "Loudon, NH", 6, "NewHampshire", "ShortTrack", ""),
    (19, "Darlington", "Darlington, SC", 7, "Darlington", "Speedway", ""),
    (20, "Indianapolis", "Indianapolis, IN", 3, "Indianapolis", "Superspeedway", ""),
    (21, "Miami", "Homestead, FL", 5, "Miami", "Speedway", ""),
    (22, "Madison", "Madison, IL", 5, "Gateway", "Speedway", "World Wide Technology Raceway is the catalogue's Gateway"),
    (23, "Nashville", "Nashville, TN", 6, "Nashville", "Speedway", ""),
    (24, "RockinghamUK", "Rockingham, England", 6, "LasVegas", "Speedway",
     "Rockingham Motor Speedway (UK) isn't in the catalogue; a 1.5-mile oval like Las Vegas"),
    (25, "Rockingham", "Rockingham, NC", 7, "Rockingham", "ShortTrack", ""),
    (26, "NorthWilkesboro", "North Wilkesboro, NC", 8, "NorthWilkesboro", "ShortTrack", ""),
    (27, "Nazareth", "Nazareth, PN", 8, "Milwaukee", "ShortTrack",
     "Nazareth isn't in the catalogue; Milwaukee is the nearest flat 1-mile oval"),
    (28, "Iowa", "Newton, IA", 7, "Iowa", "ShortTrack", ""),
    (29, "Fountain", "Fountain, CO", 6, "Phoenix", "ShortTrack",
     "Pikes Peak isn't in the catalogue; Phoenix is the nearest flat 1-mile desert oval"),
    (30, "LosAngeles", "Los Angeles, CA", 10, "BowmanGray", "ShortTrack",
     "the LA Coliseum quarter-mile isn't in the catalogue; Bowman Gray is the quarter-mile stadium bullring"),
    (31, "Motegi", "Motegi, JPN", 6, "Gateway", "Speedway",
     "Twin Ring Motegi isn't in the catalogue; DM2 drew it with Gateway's egg-shaped image"),
    (33, "Milwaukee", "Milwaukee, WI", 6, "Milwaukee", "ShortTrack", ""),
]

# DM2 prize code -> car filter. Codes not listed here are resolved by kind in prize_filter().
INDY_CARSETS = ["irl23", "irl24", "irc00"]
TEAM_PRIZES = ("IND", "SPI", "RWR", "FRM", "RFR", "RFK", "TRK", "RCR", "CGR", "SHR", "PEN", "JGR", "HEN", "KAU", "23X",
               "LMC", "FOY", "AND", "ARR", "DCR", "DRE", "EDC", "JUN", "MEY", "RLL")
MANU_PRIZES = ("CHV", "FRD", "TYT", "HON", "DDG")
CARSET_PRIZES = ("cup20", "cup22", "cup23", "cup24", "irl23", "irl24", "dmc15", "irc00")
RULE_KINDS = ("Team", "Manufacturer", "Car", "Driver", "Type", "Rarity")

ASSIGN_2D = re.compile(r'^\s*([A-Za-z_][A-Za-z0-9_]*)\[(\d+)(?:\s*,\s*(\d+))?\]\s*=\s*("(?:[^"\\]|\\.)*"|-?\d+)\s*;')


def parse_series_data(path):
    """{array name: {(i,) or (i, j): value}} for every literal assignment in loadSeries()."""
    with open(path, encoding="utf-8-sig") as f:
        lines = [strip_comment(l) for l in f.read().splitlines()]
    values = collections.defaultdict(dict)
    for line in lines:
        m = ASSIGN_2D.match(line)
        if not m:
            continue
        raw = m.group(4)
        key = (int(m.group(2)),) if m.group(3) is None else (int(m.group(2)), int(m.group(3)))
        values[m.group(1)][key] = json.loads(raw) if raw.startswith('"') else int(raw)
    return values


def catalogue_kinds(path):
    """{track id: TrackKind name} from TrackDimensions' Oval(...)/Road(...) rows."""
    with open(path, encoding="utf-8-sig") as f:
        text = f.read()
    kinds = {}
    for m in re.finditer(r'\bOval\(\s*"([^"]+)"\s*,\s*"[^"]*"\s*,\s*TrackKind\.(\w+)', text):
        kinds[m.group(1)] = m.group(2)
    for m in re.finditer(r'\bRoad\(\s*"([^"]+)"', text):
        kinds[m.group(1)] = "RoadCourse"
    return kinds


def slug(text):
    return re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_")


def prize_filter(code):
    """DM2 offlinePrizes code -> {code, carsets, team, manufacturer, driverType, minRarity, maxRarity}."""
    f = {"code": code, "carsets": [], "team": "", "manufacturer": "", "driverType": "", "minRarity": 0, "maxRarity": 0}
    if code == "":
        return f
    m = re.fullmatch(r"Rarity([1-4])(Plus)?", code)
    if m:
        f["minRarity"] = int(m.group(1))
        f["maxRarity"] = 0 if m.group(2) else int(m.group(1))
    elif code in ("Rookies", "Rookie"):
        f["driverType"] = "Rookie"
    elif code in DRIVER_TYPES:
        f["driverType"] = code
    elif code in CARSET_PRIZES:
        f["carsets"] = [code]
    elif code in TEAM_PRIZES:
        f["team"] = code
    elif code in MANU_PRIZES:
        f["manufacturer"] = code
    elif len(code) == 4 and code[:3] in MANU_PRIZES and code[3] == "1":
        f["manufacturer"] = code[:3]
        f["minRarity"] = f["maxRarity"] = 1
    elif code in ("Indy", "Indy1"):
        f["carsets"] = list(INDY_CARSETS)
        if code == "Indy1":
            f["minRarity"] = f["maxRarity"] = 1
    else:
        raise SystemExit("Unknown DM2 prize code %r: add it to prize_filter()" % code)
    return f


def prize_pool(cars, f):
    def ok(c):
        return ((not f["carsets"] or c["carset"] in f["carsets"])
                and (not f["team"] or c["team"] == f["team"])
                and (not f["manufacturer"] or c["manufacturer"] == f["manufacturer"])
                and (not f["driverType"] or c["type"] == f["driverType"])
                and (not f["minRarity"] or c["rarity"] >= f["minRarity"])
                and (not f["maxRarity"] or c["rarity"] <= f["maxRarity"]))
    return [c for c in cars if ok(c)]


def restriction_matches(cars, r):
    rule, value = r["rule"], r["value"]
    def ok(c):
        if c["rarity"] < r["minRarity"]:
            return False
        if rule == "Team":
            return c["team"] == value
        if rule == "Manufacturer":
            return c["manufacturer"] == value
        if rule == "Car":
            return str(c["number"]) == value
        if rule == "Driver":
            return c["driver"] == value
        if rule == "Type":
            return c["type"] == value
        if rule == "Rarity":
            return str(c["rarity"]) == value
        return True
    return [c for c in cars if ok(c)]


def restriction(values, key):
    """offlineMin* at key -> {minLevel, minClass, minRarity, rule, value}."""
    def get(name, default):
        return values.get(name, {}).get(key, default)
    rule = get("offlineMinType", "")
    if rule and rule not in RULE_KINDS:
        raise SystemExit("Unknown DM2 restriction type %r at %s" % (rule, key))
    value = {"Team": lambda: get("offlineMinTeam", ""),
             "Manufacturer": lambda: get("offlineMinManu", ""),
             "Car": lambda: str(get("offlineExactCar", "")),
             "Type": lambda: get("offlineMinDriverType", ""),
             "Rarity": lambda: str(get("offlineMinRarity", "")),
             }.get(rule, lambda: "")()
    if rule and value in ("", None):
        raise SystemExit("DM2 restriction %s at %s has no value" % (rule, key))
    return {"minLevel": get("offlineMinLevel", 0), "minClass": get("offlineMinClass", 0), "minRarity": 0,
            "rule": rule, "value": value}


def build_series(log, cars):
    values = parse_series_data(SERIES_DATA)
    kinds = catalogue_kinds(TRACK_DIMENSIONS)
    tracks = {}
    for dm2_id, code, name, laps, track, kind, why in DM2_TRACKS:
        if track not in kinds:
            raise SystemExit("DM2 track %d (%s) maps to %r, which TrackDimensions doesn't have" % (dm2_id, code, track))
        if kinds[track] != kind:
            raise SystemExit("DM2 track %d (%s) is a %s but %s is a %s" % (dm2_id, code, kind, track, kinds[track]))
        tracks[dm2_id] = {"dm2Id": dm2_id, "dm2Name": name, "track": track, "kind": kind, "baseLaps": laps, "note": why}

    menus_raw = values["offlineMenu"]
    menus = []
    for (m,) in sorted(menus_raw):
        subs = sorted(j for (i, j) in values["offlineSeries"] if i == m)
        if not subs:
            log("menu %d %s: no sub-series in DM2, skipped" % (m, menus_raw[(m,)]))
            continue
        order = values.get("offlineCustomListOrder", {}).get((m,))
        if order:
            order = [int(x) for x in order.split(",")]
            if sorted(order) != subs:
                raise SystemExit("offlineCustomListOrder[%d] doesn't list every sub-series" % m)
        else:
            order = subs
        menu_name = menus_raw[(m,)]
        menu = {"id": slug(menu_name), "name": menu_name,
                "description": values.get("seriesDescriptions", {}).get((m,), ""),
                "image": values.get("offlineImage", {}).get((m,), ""), "series": []}
        for s in order:
            key = (m, s)
            name = values["offlineSeries"][key]
            raw_list = values["offlineTracklists"][key]
            rounds = []
            for t in raw_list.split(","):
                dm2_id = int(t)
                if dm2_id not in tracks:
                    raise SystemExit("%s: DM2 track %d has no entry in DM2_TRACKS" % (name, dm2_id))
                tr = tracks[dm2_id]
                rounds.append({"track": tr["track"], "dm2Track": dm2_id, "baseLaps": tr["baseLaps"]})
            r = restriction(values, key)
            prize = prize_filter(values.get("offlinePrizes", {}).get(key, ""))
            eligible = restriction_matches(cars, r)
            pool = prize_pool(cars, prize) if prize["code"] else []
            note = "" if eligible else "  NO ELIGIBLE CARS"
            if prize["code"] and not pool:
                note += "  EMPTY PRIZE POOL"
            log("  %-26s L%-2d C%d %-12s %-10s ai %-2d %2d rounds  %3d eligible  prize %-11s %3d cars%s" % (
                name, r["minLevel"], r["minClass"], r["rule"] or "-", r["value"] or "", values["offlineAILevel"].get(key, 0),
                len(rounds), len(eligible), prize["code"] or "-", len(pool), note))
            menu["series"].append({
                "id": slug(menu_name) + "_" + slug(name),
                "menu": menu["id"],
                "dm2Index": [m, s],
                "name": name,
                "description": values.get("offlineDescriptions", {}).get(key, ""),
                "image": values.get("offlineSeriesImage", {}).get(key, ""),
                "aiLevel": values["offlineAILevel"].get(key, 0),
                "fuel": values.get("offlineFuel", {}).get(key, 0),
                "restriction": r,
                "prize": prize,
                "rounds": rounds,
            })
        log("%s: %d series" % (menu_name, len(menu["series"])))
        menus.append(menu)
    ids = [s["id"] for mn in menus for s in mn["series"]]
    dupes = [i for i, n in collections.Counter(ids).items() if n > 1]
    if dupes:
        raise SystemExit("Duplicate series ids: %s" % ", ".join(dupes))
    return {"version": 1, "source": "Tools/masterdraft_export.py from Assets/Scripts/SeriesData.cs",
            "tracks": [tracks[k] for k in sorted(tracks)], "menus": menus}


def dump_series(obj):
    # Menus and track rows at the top level, one series per line inside each menu.
    out = ["{"]
    out.append('  "version": %d,' % obj["version"])
    out.append('  "source": %s,' % json.dumps(obj["source"]))
    out.append('  "tracks": [')
    for i, row in enumerate(obj["tracks"]):
        out.append("    " + json.dumps(row, ensure_ascii=False) + ("," if i < len(obj["tracks"]) - 1 else ""))
    out.append("  ],")
    out.append('  "menus": [')
    for m_i, menu in enumerate(obj["menus"]):
        head = {k: v for k, v in menu.items() if k != "series"}
        out.append("    {")
        for k, v in head.items():
            out.append('      "%s": %s,' % (k, json.dumps(v, ensure_ascii=False)))
        out.append('      "series": [')
        for i, s in enumerate(menu["series"]):
            out.append("        " + json.dumps(s, ensure_ascii=False) + ("," if i < len(menu["series"]) - 1 else ""))
        out.append("      ]")
        out.append("    }" + ("," if m_i < len(obj["menus"]) - 1 else ""))
    out.append("  ]")
    out.append("}")
    return "\n".join(out) + "\n"


def dump(obj):
    # One car per line: diffable without being 4k lines long.
    out = ["{"]
    keys = list(obj.keys())
    for k_i, key in enumerate(keys):
        value = obj[key]
        comma = "," if k_i < len(keys) - 1 else ""
        if isinstance(value, list):
            out.append('  "%s": [' % key)
            for i, row in enumerate(value):
                out.append("    " + json.dumps(row, ensure_ascii=False) + ("," if i < len(value) - 1 else ""))
            out.append("  ]" + comma)
        else:
            out.append('  "%s": %s%s' % (key, json.dumps(value, ensure_ascii=False), comma))
    out.append("}")
    return "\n".join(out) + "\n"


def write(path, text, check):
    old = None
    if os.path.isfile(path):
        with open(path, encoding="utf-8") as f:
            old = f.read()
    if old == text:
        print("unchanged %s" % os.path.relpath(path, ROOT))
        return True
    if check:
        print("STALE %s" % os.path.relpath(path, ROOT))
        return False
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote %s" % os.path.relpath(path, ROOT))
    return True


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true", help="don't write; exit 1 if any output is out of date")
    ap.add_argument("--quiet", action="store_true", help="only report files")
    args = ap.parse_args()
    log = (lambda s: None) if args.quiet else print

    cars = build_cars(log)
    ok = write(os.path.join(OUT_DIR, "Cars.json"), dump(cars), args.check)
    log("Series")
    ok = write(os.path.join(OUT_DIR, "Series.json"), dump_series(build_series(log, cars["cars"])), args.check) and ok
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
