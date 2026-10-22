# Type scale

One set of text sizes for the whole game. Code: `Assets/Scripts/Controls/UIType.cs`
(`Draftmaster.Controls.UIType`, `TypeRole`). Tests: `Assets/Tests/Editor/TypeScaleTests.cs`.

Text is asked for by **role**, not by number. A role has one size in kit pixels — the 640x360 grid the
Iron Oval kit is drawn on — and every surface converts that to its own canvas:

| Role | Kit px | 1080p screen | Use it for |
|---|---:|---:|---|
| **Title** | 24 | 72 | the name of a whole screen |
| **Heading** | 16 | 48 | a window or section name, a place name, a big readout (cash) |
| **Body** | 16 | 48 (Pixelify: 40) | prose: blurbs, descriptions, tutorial text |
| **Dialogue** | 16 | 48 | what somebody says |
| **Data** | 16 | 48 | rows, columns, numbers, part lists, status lines |
| **Label** | 8 | 24 | buttons, small caps captions, map keys, footers — **the floor** |

Nothing is smaller than **Label**. On a 1080p screen that is 24px; on any canvas it is
`8 * referenceHeight / 360`.

## Where the numbers come from

The objective strip (`WeekendObjectiveHUD`) is the reference Josh picked as reading right on a phone
(2026-09-28): its detail line is the data face at one cell (16 kit px) and its title the display face at
two cells (16 kit px of Silkscreen). The IMGUI panels (`PixelGUI`, scale = screen height / 360) and the
640x360 Iron Oval canvases (`IronOvalUI`, whose roles now read their sizes from `UIType`) were already on
those sizes. What drifted were the canvases authored at **1920x1080** with sizes picked by eye — on a
1080-tall canvas one kit pixel is three canvas pixels, so a `fontSize` of 16 there is a third of the size
the race panels draw the same row at. The satnav's side panel was the case that showed it.

## Using it

- **1920x1080 canvas:** `UIType.CanvasPx(role, 1080f, PixelGUI.FontCell(font, 0))` — 48 for Data, 24
  for Label, snapped onto the face's cell.
- **640x360 canvas (`IronOvalUI`)**: the kit sizes as they are — `IronOvalUI.Apply` does this.
- **IMGUI (`PixelGUI`)**: `UIType.KitPx(role) * PixelGUI.Scale`, snapped to the face's cell.
- Sizes snap to the face's pixel cell (Silkscreen 8, VT323 / fixedsys 16, Pixelify Sans 20) so bitmap
  faces stay sharp, and never round down under the floor (`UIType.Snap`).
- Bigger type needs room. When a panel does not fit at these sizes, make it wider or let it scroll
  (the satnav side panel does both) rather than shrinking the type.
- Change the scale in one place — `UIType.KitPx` — and `TypeScaleTests` says what that breaks.

## Finding text that is too small

`Draftmaster > UI > Audit Text Sizes` measures every uGUI `Text` / `TextMeshProUGUI` in every prefab
(and any open, saved scene) as it would come out on a 1920x1080 screen, through its canvas's
`CanvasScaler`, and lists everything under the floor in `Docs/TextSizeAudit.md`, worst first. It reports;
it does not fix (bigger text needs its box checked). World-space text (speech bubbles, signs) and IMGUI
panels are not measured. Scenes have to be open to be audited — the tool never opens one itself.

Known exception: the satnav's node names (`TravelMap/MapPlot/Nodes/*/Label`) are authored at 8 and 16
and zoom with the board (2.4x when the map opens), so they read at roughly 19 and 38px there. They are
kept small on purpose — at full size the middle of the country turns into a wall of overlapping names.

## Buttons

Every button in the game is one size, taken from the pause menu (`RacePauseMenu`). Code:
`Assets/Scripts/Controls/UIButton.cs`. Tests: `Assets/Tests/Editor/ButtonStandardTests.cs`.

| | Kit px | 1080p screen | 1920x1080 canvas |
|---|---:|---:|---:|
| **Plate height** | 20 | 60 | 60 |
| **Caption** | 8 (Label) | 24 | 24 |

The caption is the display face (Silkscreen), regular weight, no extra tracking, centred. A plain button
is the red plate with `text`-coloured caption; a tab is the flat plate with a `textDim` caption (gold or
white when selected). On a phone the caption takes the data face (fixedsys 16) and the plate grows to hold
it with the same padding — about 33 kit px.

Where to get it, so nothing types its own number:

- **IMGUI (`PixelGUI`)**: height `PixelGUI.MenuButtonH`, drawn with `PixelGUI.Button` / `PixelGUI.Tab`.
- **640x360 canvas (`IronOvalUI`)**: `IronOvalUI.Button` / `TabButton` force the height; only the width is the caller's.
- **1920x1080 runtime panels (legacy `Text`)**: plate `IronOvalUI.ButtonHeight()`, caption `IronOvalUI.ButtonCaption(text, colour)`.
- **A button authored in a prefab or scene**: its binder calls `IronOvalUI.StandardiseButton(button)`.

`Draftmaster > UI > Audit Buttons` lists every captioned uGUI button the build reaches (build scenes +
`Resources`) in `Docs/ButtonAudit.md` with what is off. `Draftmaster > UI > Standardise Buttons In Prefabs`
fixes those prefabs in place (it touches only the buttons). Scenes are fixed through their builders.

Not buttons, and left to their own sizes: menu list rows with a cursor (title menu, SINGLE RACE rows,
OPTIONS rows, dialogue choices), on-screen interact prompts (`PixelGUI.ActionButton` / `PromptButton`),
icon and swatch buttons, a window's corner close glyph (X), the satnav's map pins, and the F-key debug
panels.
