#!/usr/bin/env python3
"""Export Draftmaster 2's Master Draft data from the surviving C# into Assets/Resources/MasterDraft/*.json.

Stdlib only. Re-run whenever the DM2 source data or the livery sprites change:

    python Tools/masterdraft_export.py            # writes every part this script knows
    python Tools/masterdraft_export.py --check    # exit 1 if a written file would change

Parts (see Docs/MasterDraft-Rework.md for the row plan):
  cars  -> Assets/Resources/MasterDraft/Cars.json, from Assets/Scripts/DriverNames.cs

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

    ok = write(os.path.join(OUT_DIR, "Cars.json"), dump(build_cars(log)), args.check)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
