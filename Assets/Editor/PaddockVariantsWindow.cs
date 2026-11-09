using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Draftmaster > Tracks > Paddock Variants — author and tweak a track's paddock layouts
// (Resources/Paddocks/<TrackId>.json, see PaddockSidecar) in the Editor.
//
// LOAD a variant and its markers and lot areas appear as real objects — WeekendMarker boxes, PaddockLotArea
// footprints with their packed-rig gizmos, Seat/View children — over the track package, whichever way it is
// open: Prefab Mode (Edit Selected Package), in context, or previewed in RaceScene. Move, rotate, resize the
// boxes (Edit Collider), duplicate or delete pieces, then SAVE: the layout is read back in package-local
// metres and written into that variant, marked `authored` so Tools/paddock_generate.py never overwrites it.
//
// The loaded objects are HideFlags.DontSave and sit beside the package, not inside it: saving the scene or
// the prefab can never bake them into the package (where they would win over every variant forever).
// They are cleared before Play Mode (offering to save first) and when another variant is loaded.
//
// FORCE IN PLAY makes the race scene use one variant instead of the per-visit pick, to see it in the game.
public class PaddockVariantsWindow : EditorWindow
{
    const string EditPrefix = "PaddockVariant ";

    string _trackId;
    PaddockSidecar.File _file;
    int _loaded = -1;
    string _loadedSnapshot;
    GameObject _root;
    Vector2 _scroll;
    string _report = "";
    string _newName = "";

    static string FilePath(string id) => $"Assets/Resources/{PaddockSidecar.Folder}/{id}.json";

    [MenuItem("Draftmaster/Tracks/Paddock Variants", priority = 442)]
    static void Open() => GetWindow<PaddockVariantsWindow>("Paddock Variants");

    void OnEnable()
    {
        if (string.IsNullOrEmpty(_trackId)) _trackId = TrackSelection.CurrentId;
        EditorApplication.playModeStateChanged += OnPlayMode;
        AdoptOrphan();
        Reload();
    }

    void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayMode;

    void OnDestroy()
    {
        if (_root != null && Dirty() && EditorUtility.DisplayDialog("Paddock Variants",
                $"Save your changes to {_trackId} variant {_loaded} before closing?", "Save", "Discard"))
            Save();
        Unload();
    }

    void OnPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode || _root == null) return;
        if (Dirty() && EditorUtility.DisplayDialog("Paddock Variants",
                $"{_trackId} variant {_loaded} has unsaved changes. Save before Play?", "Save", "Discard"))
            Save();
        // Out of the scene before Play: a live PaddockLotArea here would be found by the lots.
        Unload();
    }

    // ------------------------------------------------------------------ data

    void Reload()
    {
        _file = PaddockSidecar.Load(_trackId);
        Repaint();
    }

    int VariantCount => _file?.variants?.Length ?? 0;

    // ------------------------------------------------------------------ GUI

    void OnGUI()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            string id = EditorGUILayout.DelayedTextField("Track", _trackId);
            if (id != _trackId) { SwitchTrack(id); return; }
            if (GUILayout.Button("Selected track", GUILayout.Width(100))) { SwitchTrack(TrackSelection.CurrentId); return; }
        }

        var frame = Frame(out string frameNote);
        EditorGUILayout.HelpBox(frameNote, frame != null ? MessageType.None : MessageType.Warning);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open package (Prefab Mode)"))
            {
                TrackSelection.Select(_trackId);
                EditorApplication.ExecuteMenuItem("Draftmaster/Tracks/Edit Selected Package (Prefab Mode)");
            }
            if (GUILayout.Button("Validate")) Validate();
            if (GUILayout.Button("Reload file")) Reload();
        }

        if (_file == null)
        {
            EditorGUILayout.HelpBox($"No {FilePath(_trackId)} yet. Run `python Tools/paddock_generate.py {_trackId}` " +
                                    "for generated layouts, or start an empty one here and add pieces.", MessageType.Info);
            if (GUILayout.Button("Start an empty sidecar")) { _file = new PaddockSidecar.File { trackId = _trackId }; AddVariant(EmptyVariant(), "authored 1"); }
            DrawReport();
            return;
        }

        int forced = SessionState.GetInt(PaddockSidecar.ForcedVariantKey, -1);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int k = 0; k < VariantCount; k++)
        {
            var v = _file.variants[k];
            using (new EditorGUILayout.HorizontalScope(k == _loaded ? EditorStyles.helpBox : GUIStyle.none))
            {
                string label = $"{k}  {(string.IsNullOrEmpty(v.name) ? (v.authored ? "authored" : "generated") : v.name)}" +
                               $"  · {v.markers?.Length ?? 0} markers, {v.lots?.Length ?? 0} lots" + (v.authored ? "  ✎" : "");
                GUILayout.Label(label, k == _loaded ? EditorStyles.boldLabel : EditorStyles.label);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(k == _loaded ? "Reload" : "Load", GUILayout.Width(60))) Load(k);
                if (GUILayout.Button("Duplicate", GUILayout.Width(70))) { AddVariant(Clone(v), (v.name ?? $"variant {k}") + " copy"); }
                bool isForced = forced == k;
                if (GUILayout.Toggle(isForced, "Force in Play", "Button", GUILayout.Width(95)) != isForced)
                    SessionState.SetInt(PaddockSidecar.ForcedVariantKey, isForced ? -1 : k);
                if (GUILayout.Button("Delete", GUILayout.Width(55)) &&
                    EditorUtility.DisplayDialog("Paddock Variants", $"Delete {_trackId} variant {k}?", "Delete", "Cancel"))
                {
                    if (_loaded == k) Unload();
                    else if (_loaded > k) _loaded--;
                    var list = new List<PaddockSidecar.Variant>(_file.variants);
                    list.RemoveAt(k);
                    _file.variants = list.ToArray();
                    if (forced >= VariantCount) SessionState.SetInt(PaddockSidecar.ForcedVariantKey, -1);
                    Write();
                    GUIUtility.ExitGUI();
                }
            }
        }
        EditorGUILayout.EndScrollView();
        if (forced >= 0) EditorGUILayout.HelpBox($"Play Mode uses variant {forced} at every track that has one. Click it again to go back to the per-visit pick.", MessageType.Info);

        EditorGUILayout.Space();
        if (_root != null)
        {
            EditorGUILayout.LabelField($"Editing variant {_loaded}", Dirty() ? "unsaved changes" : "saved", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save to variant " + _loaded)) Save();
                if (GUILayout.Button("Save as new variant")) { var v = ReadBack(); AddVariant(v, string.IsNullOrEmpty(_newName) ? "authored" : _newName); }
                if (GUILayout.Button("Select pieces")) Selection.objects = Pieces();
                if (GUILayout.Button("Unload")) Unload();
            }
            _newName = EditorGUILayout.TextField("Variant name", _newName);
            EditorGUILayout.LabelField("Add a piece at the scene view centre:");
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var venue in new[] { "SponsorSuite", "SigningFence", "MeetingRoom", "IntroStage", "Grandstand", "PitBox", "Motorhome" })
                    if (GUILayout.Button(venue, EditorStyles.miniButton)) AddMarker(venue + "_Marker");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Motorhomes lot", EditorStyles.miniButton)) AddLot("Motorhomes");
                if (GUILayout.Button("Garages lot", EditorStyles.miniButton)) AddLot("Garages");
            }
        }
        DrawReport();
    }

    void DrawReport()
    {
        if (string.IsNullOrEmpty(_report)) return;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(_report, MessageType.None);
    }

    void SwitchTrack(string id)
    {
        if (_root != null && Dirty() && EditorUtility.DisplayDialog("Paddock Variants",
                $"Save {_trackId} variant {_loaded} first?", "Save", "Discard"))
            Save();
        Unload();
        _trackId = id;
        _report = "";
        Reload();
    }

    // ------------------------------------------------------------------ frame

    // The package the layout is measured against: the one open in Prefab Mode, else one in the open scenes.
    Transform Frame(out string note)
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            var pkg = stage.prefabContentsRoot.GetComponent<TrackPackage>();
            if (pkg != null && Same(pkg.trackId, _trackId))
            {
                note = $"Package open in Prefab Mode: {stage.assetPath}." + AuthoredNote(pkg);
                return pkg.transform;
            }
        }
        foreach (var pkg in Object.FindObjectsByType<TrackPackage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Same(pkg.trackId, _trackId) && pkg.gameObject.scene.IsValid())
            {
                note = $"Package in scene '{pkg.gameObject.scene.name}': {pkg.name}." + AuthoredNote(pkg);
                return pkg.transform;
            }
        note = $"{_trackId}'s package isn't open, so there is nothing to place against. Open it (button below), or " +
               "preview it in RaceScene. You can still load a variant: it is placed as if the package were at the origin.";
        return null;
    }

    static string AuthoredNote(TrackPackage pkg)
    {
        var names = new List<string>();
        foreach (var t in pkg.GetComponentsInChildren<Transform>(true))
            if (WeekendMarker.IsMarkerName(t.name)) names.Add(t.name);
        foreach (var a in pkg.GetComponentsInChildren<PaddockLotArea>(true)) names.Add(a.kind + " lot");
        return names.Count == 0 ? "" :
            $"\nThe package itself places {string.Join(", ", names)} — in the game those win over every variant.";
    }

    static bool Same(string a, string b) => string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ load / save

    void Load(int k)
    {
        if (_root != null && k != _loaded && Dirty() && EditorUtility.DisplayDialog("Paddock Variants",
                $"Save {_trackId} variant {_loaded} first?", "Save", "Discard"))
            Save();
        Unload();
        var frame = Frame(out _);
        var v = _file.variants[k];

        _root = new GameObject($"{EditPrefix}({_trackId} v{k}) — not saved with the scene");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) SceneManager.MoveGameObjectToScene(_root, stage.scene);
        if (frame != null) { _root.transform.position = frame.position; _root.transform.rotation = frame.rotation; }
        var f = frame != null ? frame : _root.transform;
        foreach (var m in v.markers ?? System.Array.Empty<PaddockSidecar.Marker>())
            if (m != null) PaddockSidecar.BuildMarker(f, _root.transform, m);
        foreach (var l in v.lots ?? System.Array.Empty<PaddockSidecar.Lot>())
            if (l != null) PaddockSidecar.BuildLot(f, _root.transform, l);
        foreach (var t in _root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;

        _loaded = k;
        _newName = v.name ?? "";
        _loadedSnapshot = Json(ReadBack());
        Selection.activeGameObject = _root;
        SceneView.lastActiveSceneView?.FrameSelected();
        _report = $"Loaded variant {k}. Move / rotate / Edit Collider the pieces, then Save.";
    }

    PaddockSidecar.Variant ReadBack()
    {
        var frame = Frame(out _);
        return PaddockSidecar.ReadLayout(frame != null ? frame : _root.transform, _root.transform);
    }

    bool Dirty() => _root != null && Json(ReadBack()) != _loadedSnapshot;

    void Save()
    {
        if (_root == null || _loaded < 0 || _loaded >= VariantCount) return;
        var v = ReadBack();
        v.authored = true;
        v.name = string.IsNullOrEmpty(_newName) ? _file.variants[_loaded].name : _newName;
        _file.variants[_loaded] = v;
        Write();
        _loadedSnapshot = Json(v);
        _report = $"Saved variant {_loaded} ({v.markers.Length} markers, {v.lots.Length} lots), marked authored.";
    }

    void AddVariant(PaddockSidecar.Variant v, string name)
    {
        v.authored = true;
        v.name = name;
        var list = new List<PaddockSidecar.Variant>(_file.variants ?? System.Array.Empty<PaddockSidecar.Variant>()) { v };
        _file.variants = list.ToArray();
        Write();
        _report = $"Added variant {VariantCount - 1}.";
    }

    static PaddockSidecar.Variant EmptyVariant() => new PaddockSidecar.Variant
    {
        markers = System.Array.Empty<PaddockSidecar.Marker>(),
        lots = System.Array.Empty<PaddockSidecar.Lot>(),
    };

    // A deep copy by hand: a JsonUtility round trip would turn an absent seat's NaN into something else.
    static PaddockSidecar.Variant Clone(PaddockSidecar.Variant v)
    {
        PaddockSidecar.Point P(PaddockSidecar.Point p) =>
            PaddockSidecar.Has(p) ? new PaddockSidecar.Point { x = p.x, y = p.y } : null;
        var markers = new List<PaddockSidecar.Marker>();
        foreach (var m in v.markers ?? System.Array.Empty<PaddockSidecar.Marker>())
            if (m != null)
                markers.Add(new PaddockSidecar.Marker { name = m.name, x = m.x, y = m.y, rotation = m.rotation,
                    width = m.width, height = m.height, label = m.label, seat = P(m.seat), view = P(m.view) });
        var lots = new List<PaddockSidecar.Lot>();
        foreach (var l in v.lots ?? System.Array.Empty<PaddockSidecar.Lot>())
            if (l != null)
                lots.Add(new PaddockSidecar.Lot { kind = l.kind, x = l.x, y = l.y, rotation = l.rotation,
                    width = l.width, height = l.height, gap = l.gap, rowGap = l.rowGap });
        return new PaddockSidecar.Variant { markers = markers.ToArray(), lots = lots.ToArray() };
    }

    void Unload()
    {
        if (_root != null) DestroyImmediate(_root);
        _root = null;
        _loaded = -1;
    }

    // The window keeps its loaded root through a domain reload (its fields are serialized); any OTHER loaded
    // root — left by a window that was closed or crashed — would be found by the lots in Play Mode, so it goes.
    void AdoptOrphan()
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go != _root && go.transform.parent == null && go.name.StartsWith(EditPrefix) &&
                (go.hideFlags & HideFlags.DontSaveInEditor) != 0 && go.scene.IsValid())
                DestroyImmediate(go);
        if (_root == null) _loaded = -1;
    }

    GameObject[] Pieces()
    {
        var list = new List<GameObject>();
        foreach (Transform t in _root.transform) list.Add(t.gameObject);
        return list.ToArray();
    }

    void AddMarker(string name)
    {
        var at = SceneCentre();
        var go = PaddockSidecar.BuildMarker(_root.transform, _root.transform, new PaddockSidecar.Marker
        {
            name = name, x = 0, y = 0, width = 10, height = 10,
        });
        go.transform.position = at;
        if (name.StartsWith("Grandstand"))
        {
            var seat = new GameObject("Seat");
            seat.transform.SetParent(go.transform, false);
            seat.transform.localPosition = new Vector3(0f, 40f, 0f);
            go.GetComponent<WeekendMarker>().teleportTo = seat.transform;
        }
        Finish(go);
    }

    void AddLot(string kind)
    {
        var go = PaddockSidecar.BuildLot(_root.transform, _root.transform, new PaddockSidecar.Lot
        {
            kind = kind, width = kind == "Motorhomes" ? 120 : 160, height = kind == "Motorhomes" ? 40 : 30,
        });
        go.transform.position = SceneCentre();
        Finish(go);
    }

    void Finish(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
        Undo.RegisterCreatedObjectUndo(go, "Add paddock piece");
        Selection.activeGameObject = go;
    }

    static Vector3 SceneCentre()
    {
        var sv = SceneView.lastActiveSceneView;
        var p = sv != null ? sv.pivot : Vector3.zero;
        return new Vector3(p.x, p.y, 0f);
    }

    // ------------------------------------------------------------------ file

    // Written by hand rather than JsonUtility: JsonUtility would write every absent seat/view as a Point (it
    // never writes null), and the NaN it holds isn't JSON. Same shape the generator writes.
    void Write()
    {
        if (VariantCount > 0)
        {
            _file.markers = _file.variants[0].markers;
            _file.lots = _file.variants[0].lots;
        }
        var sb = new StringBuilder("{\n");
        sb.Append(" \"trackId\": ").Append(Q(_trackId)).Append(",\n");
        sb.Append(" \"markers\": ").Append(Markers(_file.markers)).Append(",\n");
        sb.Append(" \"lots\": ").Append(Lots(_file.lots)).Append(",\n");
        sb.Append(" \"variants\": [");
        for (int k = 0; k < VariantCount; k++)
        {
            var v = _file.variants[k];
            sb.Append(k == 0 ? "\n  {" : ",\n  {");
            if (!string.IsNullOrEmpty(v.name)) sb.Append("\"name\": ").Append(Q(v.name)).Append(", ");
            if (v.authored) sb.Append("\"authored\": true, ");
            sb.Append("\"markers\": ").Append(Markers(v.markers)).Append(", \"lots\": ").Append(Lots(v.lots)).Append('}');
        }
        sb.Append("\n ]\n}\n");
        string path = FilePath(_trackId);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path);
        Reload();
    }

    static string Json(PaddockSidecar.Variant v) => Markers(v.markers) + Lots(v.lots);

    static string Markers(PaddockSidecar.Marker[] ms)
    {
        var parts = new List<string>();
        foreach (var m in ms ?? System.Array.Empty<PaddockSidecar.Marker>())
        {
            if (m == null) continue;
            var sb = new StringBuilder("{");
            sb.Append("\"name\": ").Append(Q(m.name)).Append($", \"x\": {F(m.x)}, \"y\": {F(m.y)}, \"rotation\": {F(m.rotation)}, " +
                                                            $"\"width\": {F(m.width)}, \"height\": {F(m.height)}");
            if (!string.IsNullOrEmpty(m.label)) sb.Append(", \"label\": ").Append(Q(m.label));
            if (PaddockSidecar.Has(m.seat)) sb.Append($", \"seat\": {{\"x\": {F(m.seat.x)}, \"y\": {F(m.seat.y)}}}");
            if (PaddockSidecar.Has(m.view)) sb.Append($", \"view\": {{\"x\": {F(m.view.x)}, \"y\": {F(m.view.y)}}}");
            parts.Add(sb.Append('}').ToString());
        }
        return parts.Count == 0 ? "[]" : "[\n   " + string.Join(",\n   ", parts) + "]";
    }

    static string Lots(PaddockSidecar.Lot[] ls)
    {
        var parts = new List<string>();
        foreach (var l in ls ?? System.Array.Empty<PaddockSidecar.Lot>())
            if (l != null)
                parts.Add($"{{\"kind\": {Q(l.kind)}, \"x\": {F(l.x)}, \"y\": {F(l.y)}, \"rotation\": {F(l.rotation)}, " +
                          $"\"width\": {F(l.width)}, \"height\": {F(l.height)}, \"gap\": {F(l.gap)}, \"rowGap\": {F(l.rowGap)}}}");
        return parts.Count == 0 ? "[]" : "[\n   " + string.Join(",\n   ", parts) + "]";
    }

    static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // ------------------------------------------------------------------ validate

    void Validate()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("python", $"Tools/paddock_sidecar.py validate {_trackId}")
            {
                WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(60000);
            _report = output.Trim();
        }
        catch (System.Exception e)
        {
            _report = $"Couldn't run python Tools/paddock_sidecar.py: {e.Message}";
        }
    }
}
