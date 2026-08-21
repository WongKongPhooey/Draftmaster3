using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Crowd
{
    // Shared store of the sprite frames sliced out of a paper-doll sheet.
    //
    // Every layered NPC used to slice its own copy of every sheet it wore. There are only fourteen part
    // sheets in the whole library and seven frames on each, but a paddock of twenty NPCs in six layers
    // was creating up to 840 Sprite objects to show 98 distinct pictures — all of the cost of building
    // and holding them, none of the variety. The tint that makes one NPC differ from another lives on
    // the SpriteRenderer, not on the Sprite, so the frames themselves are safe to share outright.
    //
    // Keyed on the sheet plus the grid it is cut with, so two libraries with different frame sizes
    // (or pivots, or PPU) don't collide.
    public static class NPCSpriteCache
    {
        readonly struct Key : System.IEquatable<Key>
        {
            readonly int _texture;
            readonly int _frameW, _frameH;
            readonly float _pivotX, _pivotY, _ppu;

            public Key(Texture2D sheet, int frameW, int frameH, Vector2 pivot, float ppu)
            {
                _texture = sheet != null ? sheet.GetInstanceID() : 0;
                _frameW = frameW; _frameH = frameH;
                _pivotX = pivot.x; _pivotY = pivot.y; _ppu = ppu;
            }

            public bool Equals(Key o) =>
                _texture == o._texture && _frameW == o._frameW && _frameH == o._frameH &&
                _pivotX == o._pivotX && _pivotY == o._pivotY && _ppu == o._ppu;

            public override bool Equals(object o) => o is Key k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = _texture;
                    h = h * 397 ^ _frameW;
                    h = h * 397 ^ _frameH;
                    h = h * 397 ^ _pivotX.GetHashCode();
                    h = h * 397 ^ _pivotY.GetHashCode();
                    h = h * 397 ^ _ppu.GetHashCode();
                    return h;
                }
            }
        }

        static readonly Dictionary<Key, Sprite[]> _cache = new();

        // Distinct sheet/grid combinations currently held. Read by the tests.
        public static int SheetCount => _cache.Count;

        // Total Sprite objects held across every entry.
        public static int SpriteCount
        {
            get
            {
                int n = 0;
                foreach (var kv in _cache) n += kv.Value.Length;
                return n;
            }
        }

        // Drops every cached frame and destroys it. The cache owns these outright — they are created
        // here, marked DontSave and referenced by nothing in the project — so nobody else will ever free
        // them. Anything still displaying one will lose its sprite, so only call this when no crowd is
        // on screen: at the start of a play session (below) or from a test.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            foreach (var kv in _cache)
                foreach (var s in kv.Value)
                {
                    if (s == null) continue;
                    if (Application.isPlaying) Object.Destroy(s);
                    else Object.DestroyImmediate(s);
                }
            _cache.Clear();
        }

        // The frames of `sheet` cut left-to-right on a frameW x frameH grid. The same array instance is
        // handed back on every later call for the same sheet and grid, so callers must treat it as
        // read-only — copy it if you need to shuffle or trim.
        public static Sprite[] Slice(Texture2D sheet, int frameW, int frameH, Vector2 pivot, float ppu)
        {
            if (sheet == null) return System.Array.Empty<Sprite>();

            var key = new Key(sheet, frameW, frameH, pivot, ppu);
            if (_cache.TryGetValue(key, out var cached) && IsIntact(cached)) return cached;

            var built = Build(sheet, frameW, frameH, pivot, ppu);
            _cache[key] = built;
            return built;
        }

        // A cached entry is only usable if every Sprite in it is still alive. Sprite.Create makes an
        // object nothing in the project references, so an asset unload between scenes can take them
        // even though the managed array survives — re-slice rather than hand back a null frame.
        static bool IsIntact(Sprite[] frames)
        {
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] == null) return false;
            return true;
        }

        static Sprite[] Build(Texture2D sheet, int frameW, int frameH, Vector2 pivot, float ppu)
        {
            int fw = Mathf.Max(1, frameW);
            int fh = Mathf.Max(1, frameH);
            int count = Mathf.Max(1, sheet.width / fw);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = Sprite.Create(sheet, new Rect(i * fw, 0, fw, fh), pivot, ppu, 0,
                                          SpriteMeshType.FullRect);
                frames[i].name = $"{sheet.name}_{i}";
                // Never saved to disk, and hiding them keeps the shared frames out of the project
                // window's "unsaved objects" churn.
                frames[i].hideFlags = HideFlags.HideAndDontSave;
            }
            return frames;
        }
    }
}
