# Pixel art standard

The game's art now has one rule behind it. This is the authoring guide for keeping it.

## The number

**12.8 texture pixels per world metre.** One pixel is 7.8125 cm.

It comes from the car, which is the point of truth: a carset livery is **64 × 32** source pixels, imported
at **12.8 pixels-per-unit**, so it renders as a **5.0 m × 2.5 m** car.

It is defined once, in `Assets/Scripts/Art/PixelArt.cs`:

```csharp
PixelArt.PixelsPerMetre   // 12.8
PixelArt.MetresPerPixel   // 0.078125
```

This was not invented — it is the standard the original art was already drawn for. The library lands on
round metre values at 12.8 and nowhere else:

| texture | source px | at 12.8 px/m |
|---|---|---|
| `asphalt-128-128` | 128 × 128 | 10.00 × 10.00 m |
| `grass-thick` | 32 × 32 | 2.50 × 2.50 m |
| `kerb` | 16 × 32 | 1.25 × 2.50 m |
| `catch-fence` | 128 × 32 | 10.00 × 2.50 m |
| `garage` | 128 × 96 | 10.00 × 7.50 m |
| `crowd-phoenix` | 128 × 256 | 10.00 × 20.00 m |
| a livery | 64 × 32 | 5.00 × 2.50 m |

What drifted was the **import setting**, not the art. 1551 sprites had come in at Unity's default 100
px/unit and only the cup26 carset at 12.8 — so a cup20 car would have spawned 0.64 m long beside a 5 m one.

## Two UV conventions, both anchored to metres

The old generators wrote UVs that ran **0..1 across the mesh's width**. That guarantees drift: a 12 m road
and a 100 m runoff sharing one material resolved to 8 px/m and 101 px/m. Everything now bakes the density
into the UVs (`PixelArt.UvScale`), and the material's own tiling stays at **(1,1)**.

**World-anchored** — asphalt, grass, gravel, concrete, any isotropic surface.
UV = the vertex's world position × `UvScale`. The road, pit lane, runoff and grass therefore share one
continuous texel grid and cannot disagree, whatever size the mesh is.

**Ribbon** — kerbs, painted lines, barriers, any directional texture.
UV = (across the strip, along it), both in metres × `UvScale`. The stripe pattern follows the track.

> On a bend, a ribbon's density legitimately varies with radius: kerb stripes are radial, so the outer
> stripe really is longer than the inner one. The audit calls that *curve spread*, not a defect. Giving
> each rail its own arc length removes the spread but shears the stripes into a fan — don't.

## Tools

All under the **Draftmaster ▸ Art** menu.

| menu item | what it does |
|---|---|
| **Audit Pixel Scale** | Measures every renderer in the open scene, writes `Docs/PixelScaleAudit.md`. Read the `ratio` columns — `1.00` is correct. |
| **Apply Pixel Standard to Open Scene** | Rebuilds the generators, then retiles materials: (1,1) for generated meshes, computed tiling for plain 0..1-UV quads. Warns if one material is used by both. |
| **Rebuild Track Generators** | Re-runs TrackBuilder / TrackEnvironmentBuilder / ExtraTrackSpline / Grandstand. |
| **Retarget World Sprites to Pixel Standard** | Sets world sprites to 12.8 px/unit, point filter, no mips, no compression. UI folders are excluded by name. |
| **Report Surface Material Usage** | Lists which scenes and prefabs share a surface material — check before retiling a shared one. |

## Adding new art

1. Draw it at 12.8 px/m: a 5 m object is 64 px wide. Prefer sizes that divide cleanly (16, 32, 64, 128).
2. Import as Sprite, **Point** filter, **no** mipmaps, **uncompressed**, PPU **12.8**.
   `Retarget World Sprites` does this for anything in the world folders.
3. Place it at transform scale **1**. If something looks wrong at scale 1, the source size is wrong —
   fix the drawing, don't compensate with scale. (A `×3` scale override on the marshal posts is exactly
   how they ended up at 1.88 × 7.5 m.)
4. Run **Audit Pixel Scale** and check the new row reads `1.00`.

### Why scale compensation is banned

This is the failure mode that bit hardest. `TaylorEmerson.prefab` carried a root scale of **8** to turn an
8 px sprite imported at 100 px/unit into a 0.64 m character. Correct the import to 12.8 and that 8 stops
compensating and starts multiplying — the on-foot player became **5 m tall**. The same trap sat in
`NetworkedPlayerOnFoot`, `StaticCar` and `NetworkedPlayerCar`, whose sprites rendered at 40 × 20 m while
their colliders were correctly 5.12 × 2.56 m. Those prefabs had been internally inconsistent for a while,
masked only because `GridSpawner` overrides car scale to 1 on spawn.

The paper-doll NPCs were unaffected, because `NPCLayeredAppearance` builds its sprites via `Sprite.Create`
using `NPCPartLibrary.pixelsPerUnit` and never reads the PNG's import PPU. That is exactly why the player
looked broken while every NPC standing next to them looked right.

When removing a compensating scale, the children and colliders were sized against it too — multiply
`BoxCollider2D.size`, child `localScale` and child `localPosition` by the old factor to preserve world size.

**Draftmaster ▸ Art ▸ Report Sprite Scale Compensation** lists every prefab whose sprite is sized by
transform scale, showing what it would measure at scale 1. Anything there that already resolves to a
sensible metre size at scale 1 is compensation waiting to break. Solid-colour `WhiteSquare` quads stretched
into boxes (the RV interior) are the legitimate exception — there is no pixel grid to preserve.

## Known exceptions

- **`asphalt-dark-1024-1024`** is a 1024 px photographic texture. At the standard it tiles every 80 m and
  reads as fine noise rather than drawn pixels. It is *on standard* but it is not *pixel art* — replacing
  it with a hand-drawn 32 or 64 px tile is the single biggest remaining upgrade to the track's look.
- **People** are 8 px tall (`PitCrewSpawner.OnFootPersonHeight` = 8 / 12.8 = 0.625 m). That is correct
  scale for a figure seen from above next to a 5 m car.
- **World-space dialogue** (`SpeechBubble`) is deliberately below world pixel scale. It floats above a
  0.625 m character, so text at 12.8 px/m would be 1.25 m per line. It is a UI element sitting in the
  scene, not art painted onto it.
- **`TrackReferenceImage`** is a photographic aerial used to trace the track. It is not game art.

## UI

The UI is a separate grid, authored on a **640 × 360** canvas — ×3 to 1080p, ×6 to 4K. Integer scaling is
the point: a UI pixel then covers a whole number of screen pixels, so the interface stays as crisp as the
world instead of shimmering. See `Docs/PixelUIKit.md` for what the kit contains.

Direction: **JRPG window furniture carrying racing content** — deep blue plate, gold rule, selection
cursor, name-plated dialogue — which suits a game whose demo is paddock conversations, travel between
races and side quests rather than lap times.

- `PixelUITheme` (`Assets/Resources/UI/PixelUITheme.asset`) holds the palette, frames, icons and fonts.
  Restyling the game is a change to this asset, not a sweep through a dozen scripts.
- `PixelUI` builds themed Canvas UI; `PixelGUI` provides the same look for the IMGUI panels.
- Body/data text is **fixedsys**. Getting a pixel font to render correctly needs four things to agree, and
  missing any one of them makes it look like a broken font rather than a broken setting:
  1. Atlas rasterised `RASTER_HINTED` — hard edges with stems snapped to the pixel grid.
  2. **`atlasPadding = 1`**, never 0. Zero packs glyphs edge to edge in the atlas, so every letter samples a
     sliver of its neighbour and comes out with a ghosted double outline.
  3. Material shader **`TextMeshPro/Bitmap`**. `TMP_FontAsset.CreateFontAsset` always builds its material
     with the *distance-field* shader regardless of render mode; that shader interprets a bitmap atlas's
     coverage as a distance field, which fringes every glyph.
  4. Atlas `filterMode = Point` — and re-asserted at runtime, because TMP rebuilds a dynamic atlas when it
     meets an unrasterised glyph and the new texture comes back bilinear.
  5. The plain-`Font` copy used by IMGUI panels imported as **Hinted Raster** at its native size.

  Render mode and padding are baked into the atlas, so changing either means deleting and rebuilding the
  font asset — repairing the material is not enough.

  **Draftmaster ▸ Art ▸ Dump Pixel Font Atlas** exports the glyph sheet to a PNG and writes
  `Docs/PixelFontAtlas.txt`. A healthy atlas has 0% intermediate-alpha pixels — if the sheet is crisp but
  the text on screen is not, the fault is the shader, the padding or the filtering, not the font.

  **Draftmaster ▸ Art ▸ Preview Dialogue Bubble** builds a throwaway world-space dialogue box in the open
  scene, with its own tight orthographic camera, so the font can be screenshotted and judged at real
  magnification without entering play mode. `Clear Dialogue Bubble Preview` removes it. Use this before
  changing anything about how text renders — it turns a guess into a look.

### Seeing the UI

Screenshots of UI are possible, with caveats worth knowing:

- `manage_camera screenshot` with **no** camera argument uses the ScreenCapture API and **does** include
  Screen Space - Overlay canvases, but only in play mode.
- `capture_source: "scene_view"` captures the Scene View viewport and works in **edit** mode.
- Passing a specific `camera` renders through it and excludes Overlay UI. Pass the camera's **instance ID**
  rather than its name if the object is hidden (`HideFlags.DontSave`), because name lookup will not find it.
- Headings use **mania**, the racing display face. Note that `mania SDF.asset` is a UI-Toolkit FontAsset
  that TextMeshPro cannot use — the TMP build is `mania SDF 1.asset`.

Existing authored canvases still use a 1920 × 1080 reference and were deliberately left alone: converting
them would rescale every hand-tuned layout by 3×. New pixel UI should use `PixelUI.CreateCanvas`.
