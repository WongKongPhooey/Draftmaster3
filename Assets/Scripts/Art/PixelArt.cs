using UnityEngine;

// Project-wide pixel-art standard.
//
// The car is the point of truth. A carset livery is 64x32 source pixels and is imported at
// 12.8 pixels-per-unit, so it renders as a 5.0m x 2.5m car -- exactly 12.8 texture pixels per
// world metre. Every other textured surface in the world (asphalt, grass, kerbs, buildings,
// props, NPCs) must resolve to that same density or its pixels read as a different size than
// the car's and the scene looks like a collage of different games.
//
// Two rules follow from that:
//   1. A world sprite's import PPU must be   PixelsPerMetre / (metres the sprite should cover per source pixel)
//      -- in practice: PPU = PixelsPerMetre when the sprite is placed at transform scale 1.
//   2. A tiled surface material's texture tiling must be TilingForWorldSize(), so that one
//      texture pixel covers MetresPerPixel metres no matter how big the mesh is.
public static class PixelArt
{
    // Texture pixels per world metre. Derived from the car: 64 px / 5.0 m.
    public const float PixelsPerMetre = 12.8f;

    // World metres covered by one texture pixel. 7.8125 cm.
    public const float MetresPerPixel = 1f / PixelsPerMetre;

    // The reference car, for anyone who needs to sanity-check the standard.
    public const int CarSpritePixelsLong = 64;
    public const int CarSpritePixelsWide = 32;
    public const float CarLengthMetres = CarSpritePixelsLong / PixelsPerMetre;   // 5.0
    public const float CarWidthMetres = CarSpritePixelsWide / PixelsPerMetre;    // 2.5

    // Metres spanned by one full repeat of a texture of the given pixel dimension.
    public static float TileSpanMetres(int texturePixels) => texturePixels * MetresPerPixel;

    // Material tiling for a mesh whose UVs run 0..1 across a span of `metres`, so the texture
    // lands at the standard density. Use per-axis with the texture's own width/height.
    public static float TilingForSpan(float metres, int texturePixels)
    {
        if (texturePixels <= 0) return 1f;
        return Mathf.Max(0.0001f, metres * PixelsPerMetre / texturePixels);
    }

    public static Vector2 TilingForSpan(Vector2 metres, Texture texture)
    {
        if (texture == null) return Vector2.one;
        return new Vector2(TilingForSpan(metres.x, texture.width),
                           TilingForSpan(metres.y, texture.height));
    }

    // Material tiling for a mesh whose UVs are authored directly in world metres (the pattern the
    // track/environment builders use after standardisation). One texture pixel then covers
    // MetresPerPixel metres regardless of mesh size.
    public static Vector2 TilingForMetreUvs(Texture texture)
    {
        if (texture == null) return Vector2.one;
        return new Vector2(PixelsPerMetre / texture.width, PixelsPerMetre / texture.height);
    }

    // ---- Mesh authoring -------------------------------------------------------------------------
    //
    // The builders author UVs in world metres multiplied by this scale, which bakes the standard
    // density into the mesh itself. The material's own tiling then stays at (1,1) and the density is
    // correct no matter how wide or long the generated mesh turns out to be -- which is the whole
    // problem with UVs that run 0..1 across a mesh whose width varies (a 12m road and a 100m runoff
    // sharing one material end up 8x apart).
    //
    // Returns (1,1) for an untextured material so flat-colour meshes are unaffected.
    public static Vector2 UvScale(Texture texture)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0) return Vector2.one;
        return new Vector2(PixelsPerMetre / texture.width, PixelsPerMetre / texture.height);
    }

    public static Vector2 UvScale(Material material) => UvScale(MainTextureOf(material));

    public static Texture MainTextureOf(Material m)
    {
        if (m == null) return null;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return m.GetTexture("_BaseMap");
        if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) return m.GetTexture("_MainTex");
        return null;
    }

    // Snaps a world position to the pixel grid. Keeps sprites from shimmering between texels when
    // they move slowly, and keeps parallel edges from beating against each other.
    public static float SnapToPixelGrid(float metres) => Mathf.Round(metres * PixelsPerMetre) * MetresPerPixel;

    public static Vector2 SnapToPixelGrid(Vector2 m) => new Vector2(SnapToPixelGrid(m.x), SnapToPixelGrid(m.y));

    public static Vector3 SnapToPixelGrid(Vector3 m) =>
        new Vector3(SnapToPixelGrid(m.x), SnapToPixelGrid(m.y), m.z);
}
