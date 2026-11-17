# Handoff: Draftmaster 3 — race flow (Track › Series › Car select, Race HUD)

## Overview
Four screens to rebuild in Unity (uGUI + TextMeshPro), each in PC landscape and mobile landscape, plus a mobile portrait Race HUD:

| Screen | Reference (1x / @2x) | Canvas |
|---|---|---|
| Track Select | `reference/track_select_{pc,mobile}.png` | PC 640×360 · mobile 844×390 |
| Series Select | `reference/series_select_{pc,mobile}.png` | same |
| Car Select | `reference/car_select_{pc,mobile}.png` | same |
| Race HUD | `reference/race_hud_{pc,mobile}.png` | same |
| Race HUD, portrait | `reference/race_hud_portrait.png` | 390×844 (phone only, always touch controls) |

Flow: Track › **NEXT** › Series › **NEXT** › Car › **RACE** › HUD. **BACK** (ESC) goes back one screen.
`reference/Draftmaster 3 Screens.dc.html` (with `support.js`) is the live design; it's a reference, not code to port. The CRT scanline overlay is left out of the renders (do it as a post effect).

## Units and scaling
- All numbers are **design px at 1x**, measured from the **top-left** of the screen. The references are at 1x; the `@2x` copies are for checking detail.
- Canvas Scaler: Constant Pixel Size with a **whole-number** Scale Factor only. Never scale pixel art by fractions.
- Sprite import: Sprite (2D and UI), Filter **Point**, Compression **None**, Mip Maps **off**, Mesh **Full Rect**. On Image, use Set Native Size, Preserve Aspect on, Raycast Target off on decoration. Import the 1x files; `@2x` is only for previews.

## Fonts
- **monogram Bold** (`fonts/monogram-extended-bold.ttf`): the in-game type at **16 px** for titles, tile names, rows, body, values and buttons. TMP: Sampling Point Size 16, Render Mode RASTER_HINTED, atlas filter Point.
- **Silkscreen** Regular at **8 px**, letter spacing +1 to +2 px: tags, chips, small labels, key hints. (Google Fonts, OFL.)
- **Pixelify Sans** Bold: big numerals only (P3 at 44 px, series codes at 30 px, speedometer readout at 14 px on a 100-unit dial).
- Keycaps (ENTER, ESC…): Silkscreen Bold 8 px, ink on cream `#F4EAD7`, padding 1×4, with a 2 px `#8B93AD` drop below.

## Colours
| Token | Hex | Use |
|---|---|---|
| ink | `#05060A` | outlines, hard shadows |
| bg | `#0D0F16` | screen background |
| bar | `#1B2030` | header bar |
| panel | `#182136` | tile fill |
| panel-sel | `#243A5E` | selected tile fill |
| line | `#2C3145` | idle tile border, dividers |
| cream | `#F4EAD7` | primary text, frames |
| sand | `#B9AE9A` | secondary text, idle track line |
| slate | `#8B93AD` | tertiary text, hints |
| gold | `#E8B13C` | selection, cursor ▶, headers |
| red | `#C4452F` | confirm buttons, start marker, needle, redline |
| red-text | `#E0604A` | car numbers, alerts |

## Shared select-screen shell (Track / Series / Car)
- **Header bar**: full width, 30 px tall, `bar` fill, 3 px ink bottom border, padding 0 6 0 10, gap 8. It holds the title ("SELECT TRACK" etc., cream), then context text in slate (count, or the Watkins Glen › Premier Cup breadcrumb, ellipsized), then a right-aligned BACK button (22 px tall, `#2C3145` fill, 2 px ink outline, ESC keycap on PC).
- **Grid**: fills the space between the bars; 8 px top/bottom padding; centred columns; scrolls vertically, scrollbar hidden.
- **Footer**: 28 px tall on PC, 34 px on mobile; 2 px `bar` top border; padding 0 10. Left: key hints on PC, a "TAP A …" hint on mobile. Right: position counter (e.g. `9 / 17`, slate), then the confirm button (red fill, 2 px ink outline, 3 px hard shadow, 20 px tall on PC / 26 px on mobile, label in cream, ENTER keycap on PC).
- **Tile states**: idle = `panel` fill + 3 px `line` outline; selected = `panel-sel` fill + 3 px gold outline + a 4 px ink hard shadow to the bottom-right, a blinking gold ▶ before the name (0.9 s, stepped on/off), and gold/cream accents. On PC hover, the fill goes to `#1B2840`.

| | PC | Mobile |
|---|---|---|
| Track / Series tiles | 4 cols × 140 px square, gap 10 | 5 cols × 150 px, gap 10 |
| Car tiles | 6 cols × 92 px square, gap 8 | 7 cols × 100 px, gap 8 |

### Track tile (padding 6, gap 4, vertical)
1. Art box (flex-fill): `#0F1420` with an 8 px grid of `#182136` 1 px lines, 2 px ink outline.
   - Layout sprite `sprites/tracks/<id>_{idle,selected}.png` (120×70 at PC size), inset 14 px top and 4 px on the other sides, centred. Red start/finish square baked in.
   - Type tag top-left (3,3): Silkscreen 8, slate (gold when selected), e.g. ROAD / OVAL / STREET / DIRT.
   - Length chip top-right (3,2): ink fill, padding 2×3, Silkscreen 8, e.g. `2.45 MI`.
2. Name: monogram 16, one line with ellipsis; sand (cream when selected).
3. Place: "Watkins Glen, NY", monogram 16, slate.

Filters in the header: ALL / ROAD / OVAL / DIRT (ROAD includes STREET). 20 px tall chips; the active one is a gold fill with ink text.

### Series tile (same frame)
1. Art box: `sprites/series/<id>_bg.png` (128×88 at PC). The stripes are 135° in the series colour, 6 px bands at alpha 0x66/0x33 (0x2E/0x18 when there's no field), with a 4 px solid colour bar at the bottom.
   - Code (CUP, TRK…): Pixelify Sans Bold 30 px, centred, +6 px down, cream (slate when there's no field), 3 px ink hard shadow.
   - Kind tag top-left on an ink 80% chip; field chip top-right, "48 CARS" or "NO FIELD".
2. Name (monogram 16), 3. "48 entered" / "No field entered" (sand / slate).

### Car tile (padding 5, gap 3)
1. Art box: asphalt (2 px `#2B2B30` / 1 px `#303036` horizontal stripes), 2 px ink outline, car sprite at **native 64×32**, centred. The player's car gets a gold "YOU" chip at the top-right (2,2).
2. **Last name only**, monogram 16 (e.g. "van Gisbergen").
3. `#62` in red-text + team name in slate, one line with ellipsis.

## Race HUD — landscape
Background, from top to bottom:
- **Grandstand** (top 0 to 34% − 3 px): roof bar 9 px (`bar` + `#3A4152` 2 px posts every 48 px, 2 px ink bottom border), then crowd rows anchored to the bottom (tile `sprites/hud/crowd_tile_148x240.png` horizontally, bottom-aligned, crop the top), then the catch fence (10 px: 1 px slate posts every 5 px, 2 px sand top rail, 3 px `#5A5E6C` base).
- A left **vignette** over the stand (34% wide, ink 60% → 0) keeps P3 / LAP readable.
- **Kerbs** 4 px (8 px cream / 8 px red), **track** 31% tall asphalt with 2 px cream lines inset 3 px, another kerb.
- **Paddock** strip (48 px) right under the bottom kerb: `sprites/hud/paddock_strip_860x48.png`, left-aligned, cropped on the right. Grass elsewhere: 45° stripes `#2F6B2A`/`#2B6427`, 4 px each.

Overlays (PC / mobile where they differ):
- P3 (Pixelify 44, cream, 3 px ink shadow) + "/44" at (14,10). LAP 1/5 (Silkscreen 8, gold) at (16,60).
- Alert banner, top centre at y 12: `#2A1210` 93% fill, 2 px red-text outline, blinking title + monogram body.
- Minimap: top-right (12,12), 116×66, 3 px cream outline. Tire/Draft panel below at top 88.
- Lap times bar: bottom centre, 10 px up.
- **Speedometer** 104×104: bottom-right (12,10) on PC; on mobile, right 162 px / bottom 12 so it clears the DRIFT/BOOST buttons. 3 px ink drop shadow.
- AUTO toggle: right 12, bottom 124 on PC / 96 on mobile.
- PC: key hints bottom-left. Mobile: 72 px D-pad at bottom-left; DRIFT 56 px and BOOST 68 px buttons at bottom-right.

## Race HUD — portrait (390×844)
| Element | Rect / position |
|---|---|
| Status bar | 0,0 · 390×82, `bg`, 3 px ink bottom. P3 at (14,10), LAP at (16,62), minimap at right 10 / top 8 |
| Alert banner | centred, top 96 |
| Grandstand | top 82 → 318 (same build; crowd fills about 26 rows) |
| Kerb / track / kerb | 318 (4) · 322 (150) · 472 (4) |
| Paddock strip | top 476, 48 tall |
| Lap times | left/right 12, top 538, items spread evenly (label above value) |
| Tire/Draft panel | (12, 600), 116 wide |
| AUTO toggle | (12, 680) |
| Speedometer | 128×128, right 16, top 594 |
| D-pad | 96×96, left 18, bottom 28 |
| DRIFT / BOOST | 64 / 84 px squares, right 18, bottom 28, gap 10 |

## Speedometer
Build it from two sprites so the needle can rotate:
- `speedo_face_{104,128}.png`: the bezel, white face, ticks, numerals, the 160–200 redline and the "MPH" label. The live readout is **not** baked in.
- `speedo_needle_{104,128}.png`: the same square canvas with the needle pointing straight up and the hub at the exact centre. Set the pivot to (0.5, 0.5) and stack it on top of the face.
- Rotation: `rotationZ = 135 − 270 × mph / 200` (0 mph points bottom-left, 200 bottom-right). Snap the angle to whole degrees, or step the needle, to keep the pixel look.
- Readout: TMP Pixelify Bold, ink `#12141C`, centred at x 50%, y 74% of the dial; size 14 × dial/100 (about 15 px on the 104 dial, 18 px on the 128).

## Data
`data.json` holds the tracks (id, name, place, state, miles, type, sprite paths, the source SVG path in a 100×64 box), the series (code, kind, field count, colour), the cars (number, driver, last name, team, sprite, player flag) and the speedometer constants.

## Notes
- **Animation**: the ▶ cursor and alert title blink at 0.9 s, stepped. Only move things in whole-pixel steps.
- **Placeholders**: the crowd and paddock are random filler; swap in real art when it exists.
- **Accessibility**: every tile is a real button. In Unity, wire the grid with explicit Navigation (W/A/S/D, arrows, D-pad) and scroll the selected tile into view.
