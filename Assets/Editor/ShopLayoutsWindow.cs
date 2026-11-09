using System.Collections.Generic;
using System.IO;
using Draftmaster.Tracks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Draftmaster > Travel Map > Shop Layouts — author and tweak the floor plans of the satnav's roadside stops
// (Resources/Travel/Shops/<Kind>.json, or <locationId>.json for one place of its own; see RoadsideShopBuilder).
//
// LOAD a variant and RoadsideShopBuilder builds it in the open scene (best: Assets/Scenes/Landmark.unity, the
// button opens it) exactly as the game would — shells, engines, wrecks, people, clutter — minus the doors and
// shop counters that would load scenes. Move and rotate the bays, people, props, counter and clerk; resize a
// prop with its scale. Add or remove pieces with the buttons (or delete the GameObject), flip a bay between
// finished and mid-build, then SAVE: the room is read back into that variant and marked `authored`, which
// Tools/shop_layouts.py never overwrites. Validate runs the same rules the game's tests do.
//
// The built objects are HideFlags.DontSave, so they never end up in the scene file; they're cleared before
// Play Mode (offering to save first). FORCE IN PLAY makes every stop of that kind use one variant.
public class ShopLayoutsWindow : EditorWindow
{
    const string EditPrefix = "ShopLayout ";
    const string MatPath = "Assets/Art/Garage/GarageUnlit.mat";

    string _fileName = "Fabricator";
    ShopKind _kind = ShopKind.Fabricator;
    ShopLayoutFile _file;
    int _loaded = -1;
    string _snapshot;
    GameObject _root;
    Vector2 _scroll;
    string _report = "";
    string _propKind = "bench";

    static string PathOf(string name) => $"Assets/Resources/{ShopLayouts.Folder}/{name}.json";

    [MenuItem("Draftmaster/Travel Map/Shop Layouts")]
    static void Open() => GetWindow<ShopLayoutsWindow>("Shop Layouts");

    void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go != _root && go.transform.parent == null && go.name.StartsWith(EditPrefix) &&
                (go.hideFlags & HideFlags.DontSaveInEditor) != 0 && go.scene.IsValid())
                DestroyImmediate(go);
        if (_root == null) _loaded = -1;
        Reload();
    }

    void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayMode;

    void OnDestroy()
    {
        if (Dirty() && EditorUtility.DisplayDialog("Shop Layouts", $"Save {_fileName} variant {_loaded} first?", "Save", "Discard"))
            Save();
        Unload();
    }

    void OnPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode || _root == null) return;
        if (Dirty() && EditorUtility.DisplayDialog("Shop Layouts", $"{_fileName} variant {_loaded} has unsaved changes. Save before Play?", "Save", "Discard"))
            Save();
        Unload();
    }

    void Reload()
    {
        _file = RoadsideShopBuilder.LoadFile(_fileName);
        if (_file != null && System.Enum.TryParse(_file.kind, out ShopKind k)) _kind = k;
        Repaint();
    }

    int Count => _file?.variants?.Length ?? 0;

    // ------------------------------------------------------------------ GUI

    void OnGUI()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (var k in new[] { "Fabricator", "EngineBuilder", "Junkyard" })
                if (GUILayout.Toggle(_fileName == k, k, "Button") && _fileName != k) { Switch(k); return; }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            string f = EditorGUILayout.DelayedTextField("File (or a location id)", _fileName);
            if (f != _fileName) { Switch(f); return; }
            _kind = (ShopKind)EditorGUILayout.EnumPopup(_kind, GUILayout.Width(110));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Landmark scene") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(RoadsideShopSceneBuilder.ScenePath);
            if (GUILayout.Button("Validate")) Validate();
            if (GUILayout.Button("Reload file")) Reload();
        }

        if (_file == null)
        {
            EditorGUILayout.HelpBox($"No {PathOf(_fileName)}. Kinds come from `python Tools/shop_layouts.py generate`; " +
                                    "a location's own file starts as a copy of its kind.", MessageType.Info);
            if (GUILayout.Button($"Start {_fileName}.json from the {_kind} layouts"))
            {
                var src = RoadsideShopBuilder.LoadFile(_kind.ToString());
                _file = new ShopLayoutFile { kind = _kind.ToString(), variants = src?.variants ?? new[] { RoadsideShopBuilder.Fallback(_kind) } };
                Write();
            }
            Report();
            return;
        }

        int forced = SessionState.GetInt(RoadsideShopBuilder.ForcedVariantKey, -1);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(260));
        for (int k = 0; k < Count; k++)
        {
            var v = _file.variants[k];
            using (new EditorGUILayout.HorizontalScope(k == _loaded ? EditorStyles.helpBox : GUIStyle.none))
            {
                GUILayout.Label($"{k}  {v.name}  · {v.width}x{v.height} m, {v.bays.Length} bays, {v.workers.Length} people" + (v.authored ? "  ✎" : ""),
                                k == _loaded ? EditorStyles.boldLabel : EditorStyles.label);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(k == _loaded ? "Reload" : "Load", GUILayout.Width(60))) Load(k);
                if (GUILayout.Button("Duplicate", GUILayout.Width(70)))
                {
                    var copy = JsonUtility.FromJson<ShopLayout>(JsonUtility.ToJson(v));
                    copy.name = (v.name ?? "") + " copy";
                    copy.authored = true;
                    Append(copy);
                }
                bool isForced = forced == k;
                if (GUILayout.Toggle(isForced, "Force in Play", "Button", GUILayout.Width(95)) != isForced)
                    SessionState.SetInt(RoadsideShopBuilder.ForcedVariantKey, isForced ? -1 : k);
                if (GUILayout.Button("Delete", GUILayout.Width(55)) &&
                    EditorUtility.DisplayDialog("Shop Layouts", $"Delete {_fileName} variant {k}?", "Delete", "Cancel"))
                {
                    if (_loaded == k) Unload(); else if (_loaded > k) _loaded--;
                    var list = new List<ShopLayout>(_file.variants);
                    list.RemoveAt(k);
                    _file.variants = list.ToArray();
                    Write();
                    GUIUtility.ExitGUI();
                }
            }
        }
        EditorGUILayout.EndScrollView();
        if (forced >= 0) EditorGUILayout.HelpBox($"Play Mode uses variant {forced} for every stop. Click it again to go back to the per-visit pick.", MessageType.Info);

        if (_root != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Editing variant {_loaded}", Dirty() ? "unsaved changes" : "saved", EditorStyles.boldLabel);
            var v = _file.variants[_loaded];
            v.name = EditorGUILayout.TextField("Name", v.name);
            using (new EditorGUILayout.HorizontalScope())
            {
                float w = EditorGUILayout.FloatField("Room width", v.width);
                float h = EditorGUILayout.FloatField("height", v.height);
                if (!Mathf.Approximately(w, v.width) || !Mathf.Approximately(h, v.height))
                {
                    var cur = ReadBack();
                    cur.width = Mathf.Max(10f, w);
                    cur.height = Mathf.Max(8f, h);
                    Rebuild(cur);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save to variant " + _loaded)) Save();
                if (GUILayout.Button("Save as new variant")) { var cur = ReadBack(); cur.name = v.name + " (new)"; Append(cur); }
                if (GUILayout.Button("Unload")) Unload();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add bay")) Add(l => l.bays = Append(l.bays, new ShopBay { x = Centre().x, y = Centre().y }));
                if (GUILayout.Button("Add person")) Add(l => l.workers = Append(l.workers, new ShopSpot { x = Centre().x, y = Centre().y }));
                _propKind = EditorGUILayout.TextField(_propKind, GUILayout.Width(70));
                if (GUILayout.Button("Add prop")) Add(l => l.props = Append(l.props, new ShopProp { kind = _propKind, x = Centre().x, y = Centre().y, width = 1.6f, height = 0.9f }));
            }
            var bay = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<ShopPiece>() : null;
            if (bay != null && bay.role == ShopPiece.Role.Bay &&
                GUILayout.Button(bay.state == "working" ? "Selected bay: mark finished" : "Selected bay: mark mid-build (put someone at it)"))
            {
                bay.state = bay.state == "working" ? "finished" : "working";
                Rebuild(ReadBack());
            }
        }
        Report();
    }

    void Report()
    {
        if (string.IsNullOrEmpty(_report)) return;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(_report, MessageType.None);
    }

    void Switch(string name)
    {
        if (Dirty() && EditorUtility.DisplayDialog("Shop Layouts", $"Save {_fileName} variant {_loaded} first?", "Save", "Discard"))
            Save();
        Unload();
        _fileName = name;
        if (System.Enum.TryParse(name, out ShopKind k)) _kind = k;
        _report = "";
        Reload();
    }

    // ------------------------------------------------------------------ load / save

    void Load(int k)
    {
        if (k != _loaded && Dirty() && EditorUtility.DisplayDialog("Shop Layouts", $"Save {_fileName} variant {_loaded} first?", "Save", "Discard"))
            Save();
        _loaded = k;
        Rebuild(_file.variants[k]);
        _snapshot = JsonUtility.ToJson(ReadBack());
        Selection.activeGameObject = _root;
        SceneView.lastActiveSceneView?.FrameSelected();
        _report = $"Loaded {_fileName} variant {k}. Move the pieces, then Save.";
    }

    void Rebuild(ShopLayout layout)
    {
        if (_root != null) DestroyImmediate(_root);
        _root = new GameObject($"{EditPrefix}({_fileName} v{_loaded}) — not saved with the scene");
        _root.hideFlags = HideFlags.DontSave;
        RoadsideShopBuilder.Build(layout, _root.transform, new RoadsideShopBuilder.Context
        {
            kind = _kind, locationId = _fileName, week = 1, editMode = true,
            material = AssetDatabase.LoadAssetAtPath<Material>(MatPath),
        });
        foreach (var t in _root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
        _file.variants[_loaded] = layout;
    }

    ShopLayout ReadBack()
    {
        var shop = _root.transform.Find(RoadsideShopBuilder.RootName);
        return RoadsideShopBuilder.Read(shop != null ? shop : _root.transform, _file.variants[_loaded]);
    }

    bool Dirty() => _root != null && _loaded >= 0 && JsonUtility.ToJson(ReadBack()) != _snapshot;

    void Save()
    {
        if (_root == null) return;
        var l = ReadBack();
        l.name = _file.variants[_loaded].name;
        l.authored = true;
        _file.variants[_loaded] = l;
        Write();
        _snapshot = JsonUtility.ToJson(l);
        var problems = ShopLayouts.Problems(l, _kind);
        _report = problems.Count == 0
            ? $"Saved variant {_loaded}, marked authored. No problems."
            : $"Saved variant {_loaded}, marked authored — but:\n- " + string.Join("\n- ", problems);
    }

    void Add(System.Action<ShopLayout> change)
    {
        var l = ReadBack();
        change(l);
        Rebuild(l);
    }

    void Append(ShopLayout l)
    {
        var list = new List<ShopLayout>(_file.variants) { l };
        _file.variants = list.ToArray();
        Write();
        _report = $"Added variant {Count - 1}.";
    }

    static T[] Append<T>(T[] a, T item)
    {
        var list = new List<T>(a ?? System.Array.Empty<T>()) { item };
        return list.ToArray();
    }

    static Vector2 Centre()
    {
        var sv = SceneView.lastActiveSceneView;
        var p = sv != null ? sv.pivot : Vector3.zero;
        return new Vector2(Mathf.Round(p.x * 10f) / 10f, Mathf.Round(p.y * 10f) / 10f);
    }

    void Unload()
    {
        if (_root != null) DestroyImmediate(_root);
        _root = null;
        _loaded = -1;
        Reload();   // the in-memory variant follows the room while it's loaded; discarding goes back to the file
    }

    void Write()
    {
        _file.kind = _kind.ToString();
        string path = PathOf(_fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(_file, true) + "\n");
        AssetDatabase.ImportAsset(path);
        var keepLoaded = _loaded;
        var current = _file;
        Reload();
        if (_file == null) _file = current;
        _loaded = keepLoaded;
    }

    void Validate()
    {
        var lines = new List<string>();
        for (int k = 0; k < Count; k++)
            foreach (var p in ShopLayouts.Problems(_file.variants[k], _kind))
                lines.Add($"variant {k}: {p}");
        _report = lines.Count == 0 ? $"{_fileName}: all {Count} variants are fine." : string.Join("\n", lines);
    }
}
