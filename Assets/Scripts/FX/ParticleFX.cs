using UnityEngine;

// Shared bits for the runtime particle effects (tyre spray, impact sparks/debris). Everything here is
// generated in code so the effects need no art assets or prefab wiring — drop the component on a car and
// it works. One material and one texture are shared by every emitter in the scene.
public static class ParticleFX
{
    static Material _material;
    static Texture2D _dot;

    // Unlit, alpha-blended, tinted per particle. Same shader family the tyre trails use.
    public static Material DefaultMaterial()
    {
        if (_material != null) return _material;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _material = new Material(sh) { name = "ParticleFX (auto)", hideFlags = HideFlags.DontSave };
        var tex = DotTexture();
        if (_material.HasProperty("_MainTex")) _material.SetTexture("_MainTex", tex);
        if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", tex);
        return _material;
    }

    // A soft round blob. Square particles read as pixels/artefacts at this camera distance; a round
    // one reads as a clod of dirt or a spark.
    public static Texture2D DotTexture()
    {
        if (_dot != null) return _dot;
        const int size = 32;
        _dot = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ParticleFX Dot",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f, r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                // Solid core with a short feathered edge — a hard dot with no aliasing, not a soft glow.
                float a = Mathf.Clamp01((1f - d) / 0.35f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        _dot.SetPixels32(px);
        _dot.Apply();
        return _dot;
    }
}
