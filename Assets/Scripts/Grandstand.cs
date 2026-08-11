using UnityEngine;

// Trackside crowd grandstand: a flat quad tiled with a repeating crowd texture, sized in metres.
// [ExecuteAlways] like TrackBuilder — drop in the scene, assign the texture, set length/depth, and
// rotate the GameObject to run parallel with the track. UVs repeat at the texture's native pixel
// density (metresPerRepeat horizontally, vertical derived from the aspect) so the crowd blocks stay
// square no matter the stand's size. Uses the sprite-unlit shader (cull off) so it renders under
// this project's 3D URP renderer from either winding.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Grandstand : MonoBehaviour
{
    [Tooltip("Tileable crowd texture (e.g. Textures/Props/crowd-phoenix). Import wrap mode must be Repeat.")]
    public Texture2D crowdTexture;
    [Tooltip("Length of the stand (m), along local +X.")]
    public float length = 120f;
    [Tooltip("Depth of the stand (m), along local +Y.")]
    public float depth = 12f;
    [Tooltip("Leave on to size the crowd at the project pixel standard (PixelArt.PixelsPerMetre), so a crowd " +
             "pixel is the same size as a car pixel. Turn off only for a deliberate forced-perspective stand.")]
    public bool usePixelStandard = true;
    [Tooltip("World metres covered by ONE horizontal repeat of the texture. Ignored when usePixelStandard is on, " +
             "where it is derived as textureWidth / PixelArt.PixelsPerMetre.")]
    public float metresPerRepeat = 12f;
    [Tooltip("Flip the texture vertically — for stands on the far side of the track, so the rows face it.")]
    public bool flipFacing;
    [Tooltip("Above the grass (0) but below track furniture like marker boards (3).")]
    public int sortingOrder = 2;

    Mesh _mesh;
    Material _mat;

    void OnEnable() { Build(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled) Build();
        };
    }
#endif

    public void Build()
    {
        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();

        float hx = length * 0.5f, hy = depth * 0.5f;

        // One repeat spans however many metres the texture's width covers at the project standard, so the
        // crowd's pixels match the car's. At 12.8 px/m a 128px crowd strip repeats every 10m.
        float repeat = metresPerRepeat;
        if (usePixelStandard && crowdTexture != null)
            repeat = crowdTexture.width / PixelArt.PixelsPerMetre;

        float uRep = length / Mathf.Max(0.1f, repeat);
        float vRep = 1f;
        if (crowdTexture != null)
        {
            float pixelsPerMetre = crowdTexture.width / Mathf.Max(0.1f, repeat);
            vRep = depth * pixelsPerMetre / Mathf.Max(1, crowdTexture.height);
        }
        float v0 = flipFacing ? vRep : 0f;
        float v1 = flipFacing ? 0f : vRep;

        _mesh = new Mesh { name = "Grandstand" };
        _mesh.vertices = new[]
        {
            new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f),
            new Vector3(hx, hy, 0f), new Vector3(-hx, hy, 0f)
        };
        _mesh.uv = new[]
        {
            new Vector2(0f, v0), new Vector2(uRep, v0),
            new Vector2(uRep, v1), new Vector2(0f, v1)
        };
        _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        _mesh.RecalculateBounds();
        mf.sharedMesh = _mesh;

        if (_mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            _mat = new Material(sh) { name = "Grandstand" };
        }
        _mat.mainTexture = crowdTexture;
        mr.sharedMaterial = _mat;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }
}
