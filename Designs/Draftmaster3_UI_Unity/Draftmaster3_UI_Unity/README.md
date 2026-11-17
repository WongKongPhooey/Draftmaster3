# Draftmaster 3 — UI screens for Unity (Mobile + PC)

This package holds twelve screens from the design canvas, each in a Mobile and a PC variant, for **uGUI + TextMesh Pro**:

- **In-game:** Title, DriveScout, iDroid Map, Liaison dialogue, Choice dialogue, Day summary, Race HUD, Results
- **Car Cards mode:** Collection, Card Detail, Pack (sealed), Pack (opened)

The look follows the Iron Oval UI kit: cream window frames, navy panels, gold headers, red reserved for alarms and the main confirm button, point filtering, integer scaling.

## Type standard

| Font | Size | Used for |
|---|---|---|
| **monogram Bold** (`monogram-extended-bold.ttf`) | 16px, plus pixel steps | Everything you read: menu items, window titles, section headers, buttons, body text, data rows, dialogue |
| Silkscreen | 8px, letter-spaced | Small tags, hints, badges, keycaps |
| Pixelify Sans Bold | Large | Display numerals only (position, speed, level) and the logo wordmark |

monogram Bold is a custom pixel bold made from datagoblin's monogram (CC0). Each letter is one pixel heavier, and 1px gaps such as the inside of m, w and # stay open.

## Import

1. Copy `Assets/IronOval` into your project's `Assets/`. Requires Unity 2021.3+ with TextMesh Pro, or Unity 6. If Unity asks for TMP Essentials, import them.
2. Run **Tools ▸ Iron Oval ▸ Build Screens**.
3. The prefabs appear in `Assets/IronOval/Cards/Prefabs/`:
   - `Title.prefab`, `DriveScout.prefab` and so on: one per screen. Each has a full-bleed background, a Mobile child, a PC child and `IronOvalPlatformSwitch`.
   - `Variants/<Screen>_Mobile.prefab` and `Variants/<Screen>_PC.prefab`: the individual layouts.
   - `IronOvalCards_Preview.prefab`: an overlay Canvas with all twelve screens, Title active. Drop it into a scene with an EventSystem.

Re-running the menu item rebuilds everything from `Layout/*.json`. Customise through prefab variants rather than editing the generated prefabs.

## Crisp text at in-between sizes: `IronOvalPixelText`

A pixel font is sharp when every font pixel covers a whole number of screen pixels. Every monogram and 8px Silkscreen text gets `IronOvalPixelText`, which sizes text in **steps** instead of fixed point sizes:

```
fontSize = nativePx × (scale + step) ÷ scale        (monogram nativePx = 16)
```

- **Step 0** is the native size, exactly as designed.
- **Step +1** is one more screen pixel per font pixel. For monogram that's 21⅓px at 1080p (3×) and 20px at 1440p (4×), always perfectly sharp.

Each text has a **role** (Body, Row, Data, Name, Header, Title, Menu, Button, Label). Defaults live in `IronOvalPixelText.DefaultStep()`: **Menu is +1** (the title menu), and everything else is 0. Change one line there to resize a whole role across the game. To change a single text, untick *Use Role Default* and set its *Step*.

Layouts were designed at step 0. A text you bump up grows from its anchor, so check that neighbours still clear it. Headers and menus usually have the room; dense rows such as the DriveScout stats don't.

**This relies on:**
- an integer canvas scale (`IronOvalPixelScaler`)
- TMP atlases sampled at the font's native size with RASTER_HINT and Point filtering (the builder does this)
- no TMP Auto Size, and no fractional scale on parent objects

## Platforms and screen sizes

| | Mobile | PC |
|---|---|---|
| Reference size | 844×390 | 640×360 (exactly 3× at 1080p, 4× at 1440p, 6× at 4K) |
| Input | On-screen drift, boost and d-pad; tall tab bar | Key prompts (Shift, Space, Esc, Enter, E, W/S), Q/E tab hints, slimmer tab bar |
| Safe area | `IronOvalSafeArea` keeps content clear of the notch; the background stays full-bleed | — |

- **Screens stretch to fit.** Anchors were measured by rendering the design at several sizes, so panels stretch, pin or divide space exactly as the design does. As a check, every screen was rebuilt from these anchors at an ultrawide width and compared with a real render at that width; the only differences were text anti-aliasing and the map's route lines.
- **Scaling.** `IronOvalPixelScaler` sets the canvas scale to floor(screenHeight ÷ 360), clamped 1–6.
- **Choosing the platform.** `IronOvalPlatformSwitch` picks Mobile when `Application.isMobilePlatform` is true. Override it per screen (`force`) or globally with `IronOvalPlatformSwitch.SetGlobal(...)`. In the editor it shows PC unless forced.

## What's in the folder

| Path | Contents |
|---|---|
| `Layout/*.json` | Anchors, offsets, colours, sprites, text and text role for every element, one file per screen and platform |
| `Sprites/Cars/` | Car liveries, 64×32 |
| `Sprites/Art/` | Title hero art and the #89 car (both cut from your screenshots), plus the race minimap |
| `Sprites/UI/` | Generated pixel tiles: stripes and hatches, scanline, pack rays, vignette, title fade, 1px outline frame |
| `Fonts/` | monogram Bold (CC0), Silkscreen and Pixelify Sans (SIL OFL), DejaVu Sans as a fallback for ◈ ▣. Licences included. |
| `Fonts/Generated/` | Created by the builder: TMP atlases, one per font and native size |
| `Data/cards_data.json` | Rarities, parts and costs, all 16 cards with stats and traits, and the pack contents |
| `Runtime/` | Pixel scaler, pixel text, platform switch, safe area, and stepped animations (blink, bob, rays spin, card pop, cards rise) |
| `Reference/` (outside Assets) | The design rendered at 1× and 3× for every screen and platform |

## Things to know

- **Logo.** The Title screen uses your logo-handoff sprites (`digit3_1x`, `bolt_top_1x`, `bolt_bottom_1x`). The builder finds them anywhere in your project; if they're missing, it warns and leaves those images empty.
- **Map lines.** On the iDroid map, the nodes, labels and legend are exported. The route lines are an empty `map lines` rect to fill from your track data.
- **Pixels Per Unit.** Sprites import at PPU 1 and the preview canvas uses Reference PPU 1. If your canvas uses 100, change `IronOvalCardsBuilder.PixelsPerUnit` to 100 and rebuild.
- **Static snapshots.** Each prefab shows one state with sample data, and buttons have no listeners yet. For the collection, turn one card into its own prefab and fill the grid from `cards_data.json`.
- **Hover and focus on PC.** These are in the design but not baked in. Add a `Selectable` transition or a frame-sprite swap, following the kit's gold-frame focus rule.
- **Livery branding.** Some liveries carry real sponsor marks. Replace them before release.
