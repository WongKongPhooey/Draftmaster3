using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace IronOval.Cards.EditorTools
{
    /// Builds uGUI prefabs (Image + TextMeshPro) from the layout files exported from the Iron Oval design canvas.
    /// Menu: Tools ▸ Iron Oval ▸ Build Screens
    public static class IronOvalCardsBuilder
    {
        /// Sprite PPU. Must equal the Canvas "Reference Pixels Per Unit" so 1 sprite pixel = 1 UI unit.
        /// The UI kit handoff uses 1; if your canvas uses Unity's default of 100, change both to 100.
        public const float PixelsPerUnit = 1f;

        const string Root = "Assets/IronOval/Cards/";
        const string LayoutDir = Root + "Layout/";
        const string PrefabDir = Root + "Prefabs/";
        const string FontDir = Root + "Fonts/";
        const string FontAssetDir = Root + "Fonts/Generated/";

        [Serializable] class Node
        {
            public int id, parent;
            public string name, kind, color, sprite, text, font, align, anim, role;
            public float x, y, w, h, fontSize, letterSpacing;
            public float aMinX, aMaxX, aMinY, aMaxY, oMinX, oMaxX, oMinY, oMaxY;
            public int borderPx;
            public bool isButton, mask, scrollY, bold, wrap, ellipsis;
        }
        [Serializable] class LayoutFile { public string screen, platform; public int width, height; public Node[] nodes; }

        static readonly Dictionary<string, TMP_FontAsset> fontCache = new Dictionary<string, TMP_FontAsset>();
        static TMP_FontAsset fallback;

        [MenuItem("Tools/Iron Oval/Build Screens")]
        public static void BuildAll()
        {
            fontCache.Clear(); fallback = null;
            // re-import sprites so the pixel-art import settings apply even if they were imported before the importer compiled
            AssetDatabase.ImportAsset(Root + "Sprites", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(FontAssetDir);
            var built = new List<GameObject>();
            try
            {
                var files = Directory.GetFiles(LayoutDir, "*.json");
                Array.Sort(files);
                var variants = new Dictionary<string, Dictionary<string, GameObject>>();
                var order = new List<string>();
                for (int i = 0; i < files.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Iron Oval", "Building " + Path.GetFileName(files[i]), i / (float)files.Length);
                    var layout = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(files[i]));
                    var prefab = BuildScreen(layout);
                    if (!variants.ContainsKey(layout.screen)) { variants[layout.screen] = new Dictionary<string, GameObject>(); order.Add(layout.screen); }
                    variants[layout.screen][layout.platform] = prefab;
                }
                foreach (var screen in ScreenOrder(order))
                    built.Add(BuildCombined(screen, variants[screen]));
                BuildPreviewCanvas(built);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Iron Oval: built {built.Count} screens (Mobile + PC variants) into {PrefabDir}");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static readonly string[] PreferredOrder = { "Title", "DriveScout", "Map", "Liaison", "Choice", "DaySummary", "RaceHUD", "Results", "Collection", "CardDetail", "PackSealed", "PackOpened" };
        static List<string> ScreenOrder(List<string> found)
        {
            var list = new List<string>();
            foreach (var s in PreferredOrder) if (found.Contains(s)) list.Add(s);
            foreach (var s in found) if (!list.Contains(s)) list.Add(s);
            return list;
        }

        static GameObject BuildScreen(LayoutFile layout)
        {
            var gos = new GameObject[layout.nodes.Length];
            foreach (var n in layout.nodes)
            {
                var go = new GameObject(n.id == 0 ? layout.screen + "_" + layout.platform : Clean(n.name), typeof(RectTransform));
                gos[n.id] = go;
                if (n.parent >= 0) go.transform.SetParent(gos[n.parent].transform, false);
                var rt = (RectTransform)go.transform;
                // anchors solved from renders at several sizes: elements stretch / pin exactly as the design's flex layout does
                rt.pivot = (n.anim == "spin" || n.anim == "pop") ? new Vector2(0.5f, 0.5f) : new Vector2(0, 1);
                rt.anchorMin = new Vector2(n.aMinX, n.aMinY);
                rt.anchorMax = new Vector2(n.aMaxX, n.aMaxY);
                rt.offsetMin = new Vector2(n.oMinX, n.oMinY);
                rt.offsetMax = new Vector2(n.oMaxX, n.oMaxY);

                if (!string.IsNullOrEmpty(n.text)) { AddText(go, n); }
                else { AddGraphic(go, n); }

                if (n.isButton)
                {
                    var img = go.GetComponent<Image>();
                    if (img == null) { img = go.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0); }
                    img.raycastTarget = true;
                    var b = go.AddComponent<Button>();
                    b.targetGraphic = img;
                    b.transition = Selectable.Transition.None; // pixel UI: swap sprites or offset content instead of tinting
                }
                if (n.mask) go.AddComponent<RectMask2D>();
                if (!string.IsNullOrEmpty(n.anim)) AddAnim(go, n);
            }
            // ScrollRects need their children in place first
            foreach (var n in layout.nodes)
            {
                if (!n.scrollY) continue;
                var go = gos[n.id];
                RectTransform content = null;
                foreach (Transform c in go.transform) { content = (RectTransform)c; break; }
                if (content == null) continue;
                if (go.GetComponent<Image>() == null) { var hit = go.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); }
                var sr = go.AddComponent<ScrollRect>();
                sr.content = content; sr.horizontal = false; sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.viewport = (RectTransform)go.transform;
            }

            var root = gos[0];
            if (layout.platform == "Mobile") root.AddComponent<IronOvalSafeArea>();
            Directory.CreateDirectory(PrefabDir + "Variants/");
            var prefabPath = PrefabDir + "Variants/" + layout.screen + "_" + layout.platform + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // Screen prefab = full-bleed background + Mobile and PC variants + IronOvalPlatformSwitch
        static GameObject BuildCombined(string screen, Dictionary<string, GameObject> variants)
        {
            var root = new GameObject(screen, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var bg = root.GetComponent<Image>(); bg.color = Hex("#12141cff"); bg.raycastTarget = false;
            var sw = root.AddComponent<IronOvalPlatformSwitch>();
            foreach (var kv in variants)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(kv.Value, root.transform);
                var irt = (RectTransform)inst.transform;
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = irt.offsetMax = Vector2.zero;
                if (kv.Key == "Mobile") sw.mobile = inst; else sw.pc = inst;
            }
            sw.Apply();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + screen + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void AddGraphic(GameObject go, Node n)
        {
            if (n.kind == "none" || string.IsNullOrEmpty(n.kind)) return;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = Hex(n.color);
            if (n.kind == "color") return;

            var sprite = LoadSprite(n.sprite);
            if (sprite == null) { Debug.LogWarning($"Iron Oval: missing sprite '{n.sprite}' for {n.name}"); return; }
            img.sprite = sprite;
            switch (n.kind)
            {
                case "frame": // CSS outline -> 1px 9-slice drawn borderPx thick
                    img.type = Image.Type.Sliced; img.fillCenter = false;
                    img.pixelsPerUnitMultiplier = 1f / Mathf.Max(1, n.borderPx);
                    break;
                case "stripes": case "scanline":
                    img.type = Image.Type.Tiled; img.pixelsPerUnitMultiplier = 1f;
                    break;
                default: // sprite, rays, vignette, fade
                    img.type = Image.Type.Simple; img.preserveAspect = false;
                    break;
            }
        }

        static void AddText(GameObject go, Node n)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = n.text;
            t.font = FontFor(n.font, n.bold, Mathf.RoundToInt(n.fontSize));
            t.fontSize = n.fontSize;
            t.color = Hex(n.color);
            t.characterSpacing = n.fontSize > 0 ? n.letterSpacing / n.fontSize * 100f : 0f;
            t.alignment = n.align == "center" ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            t.raycastTarget = false;
            t.richText = false;
            t.overflowMode = n.ellipsis ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
#if UNITY_2023_2_OR_NEWER
            t.textWrappingMode = n.wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
#else
            t.enableWordWrapping = n.wrap;
#endif
            t.margin = Vector4.zero;
            t.extraPadding = false;

            // pixel fonts: size by whole screen-pixel steps so they stay crisp at any integer UI scale
            if (n.font == "MonogramBold" || (n.font == "Silkscreen" && n.fontSize <= 8))
            {
                var px = go.AddComponent<IronOvalPixelText>();
                px.nativePx = n.font == "MonogramBold" ? 16 : 8;
                px.role = RoleFor(n.role);
            }
        }

        static IronOvalPixelText.Role RoleFor(string r)
        {
            switch (r)
            {
                case "row": return IronOvalPixelText.Role.Row;
                case "data": return IronOvalPixelText.Role.Data;
                case "name": case "namelg": return IronOvalPixelText.Role.Name;
                case "head": return IronOvalPixelText.Role.Header;
                case "title": return IronOvalPixelText.Role.Title;
                case "menu": return IronOvalPixelText.Role.Menu;
                case "btn": return IronOvalPixelText.Role.Button;
                case "label": return IronOvalPixelText.Role.Label;
                default: return IronOvalPixelText.Role.Body;
            }
        }

        static void AddAnim(GameObject go, Node n)
        {
            var a = go.AddComponent<IronOvalUIAnim>();
            switch (n.anim)
            {
                case "blink": a.mode = IronOvalUIAnim.Mode.Blink; a.period = 1f; break;
                case "bob": a.mode = IronOvalUIAnim.Mode.Bob; a.period = 1.2f; break;
                case "rise": a.mode = IronOvalUIAnim.Mode.Rise; a.period = 0.5f; break;
                case "spin": a.mode = IronOvalUIAnim.Mode.Spin; a.period = 16f; break;
                case "pop": a.mode = IronOvalUIAnim.Mode.Pop; a.period = 0.45f; break;
                default: UnityEngine.Object.DestroyImmediate(a); break;
            }
        }

        // ---------- fonts: one raster-hinted, point-filtered TMP asset per family/weight/size ----------
        static TMP_FontAsset FontFor(string family, bool bold, int size)
        {
            string file;
            if (family == "MonogramBold") { file = "monogram-extended-bold"; bold = false; size = 16; }  // atlas at its native 16px grid; IronOvalPixelText scales by whole steps
            else if (family == "Fixedsys") { file = "FSEX302"; bold = false; }
            else if (family == "Silkscreen" || family == "PixelifySans") file = family + (bold ? "-Bold" : "-Regular");
            else file = "DejaVuSans";
            string key = file + "_" + size + "px";
            if (fontCache.TryGetValue(key, out var cached)) return cached;
            var fa = LoadOrCreateFont(file, size, key);
            if (file != "DejaVuSans")
            {
                if (fallback == null) fallback = LoadOrCreateFont("DejaVuSans", 16, "DejaVuSans_16px");
                if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!fa.fallbackFontAssetTable.Contains(fallback)) { fa.fallbackFontAssetTable.Add(fallback); EditorUtility.SetDirty(fa); }
            }
            fontCache[key] = fa;
            return fa;
        }

        static TMP_FontAsset LoadOrCreateFont(string file, int size, string key)
        {
            string assetPath = FontAssetDir + key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + file + ".ttf");
            if (font == null) throw new Exception("Iron Oval: font not found: " + FontDir + file + ".ttf");
            var fa = TMP_FontAsset.CreateFontAsset(font, Mathf.Max(6, size), 1, GlyphRenderMode.RASTER_HINT, 256, 256, AtlasPopulationMode.Dynamic, true);
            fa.name = key;
            AssetDatabase.CreateAsset(fa, assetPath);
            var tex = fa.atlasTextures[0];
            tex.name = key + " Atlas";
            tex.filterMode = FilterMode.Point;
            AssetDatabase.AddObjectToAsset(tex, fa);
            fa.material.name = key + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            return fa;
        }

        // ---------- preview canvas with all screens, Collection active ----------
        static void BuildPreviewCanvas(List<GameObject> screens)
        {
            var canvasGo = new GameObject("IronOvalCards_Preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = PixelsPerUnit;
            canvasGo.AddComponent<IronOvalPixelScaler>();

            bool first = true;
            foreach (var prefab in screens)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
                var rt = (RectTransform)inst.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                inst.SetActive(first); first = false;
            }
            PrefabUtility.SaveAsPrefabAsset(canvasGo, PrefabDir + "IronOvalCards_Preview.prefab");
            UnityEngine.Object.DestroyImmediate(canvasGo);
        }

        // ---------- helpers ----------
        static Sprite LoadSprite(string name)
        {
            foreach (var dir in new[] { "Sprites/UI/", "Sprites/Cars/", "Sprites/Art/" })
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(Root + dir + name + ".png");
                if (s != null) return s;
            }
            // anywhere else in the project, e.g. the Draftmaster 3 logo sprites (digit3_1x, bolt_top_1x, bolt_bottom_1x) from the logo handoff
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Sprite"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }

        static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }

        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "node";
            s = s.Replace('/', '-').Replace('\n', ' ');
            return s.Length > 40 ? s.Substring(0, 40) : s;
        }
    }
}
