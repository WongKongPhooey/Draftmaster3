using System.Collections.Generic;
using System.IO;
using System.Linq;
using Draftmaster.Weekend;
using UnityEditor;
using UnityEngine;

// Where a race weekend gets laid out: six half-days down the page, the bookings in each one, and a button
// to add another.
//
// The window edits a plan file (Resources/Weekends/<Track>.<Series>.json) and nothing else. There is no
// second copy of the schedule in here — it reads the JSON, shows it, writes it back, and the game reads the
// same file. Everything the format allows is editable; everything it validates is shown in red as you type
// rather than discovered when you press Play.
//
// The other half of the job is the places. `MARKERS IN THE OPEN SCENE` lists every WeekendMarker the loaded
// track has, says whether the player can actually walk to each one, and offers to create the missing ones —
// which is the fix for a marker that lands on the fence because nobody ever said where the venue was.
public class WeekendPlanWindow : EditorWindow
{
    [MenuItem("Draftmaster/Weekend/Plan Editor %#e", priority = 0)]
    public static void Open()
    {
        var w = GetWindow<WeekendPlanWindow>("Weekend Plan");
        w.minSize = new Vector2(660f, 460f);
        w.Show();
    }

    string _track = "WatkinsGlen";
    RacingSeries _series = RacingSeries.Cup;

    WeekendPlan _plan;
    string _loadedFrom = "";
    bool _dirty;

    Vector2 _scroll;
    readonly HashSet<string> _collapsed = new();
    string[] _eventIds;
    string[] _trackIds;

    void OnEnable()
    {
        _eventIds = WeekendEventCatalog.Ids();
        _trackIds = TrackIds();
        Reload();
    }

    static string[] TrackIds()
    {
        var ids = new List<string>();
        foreach (var asset in Resources.LoadAll<TrackInfoV2>("Tracks"))
            if (asset != null) ids.Add(asset.name);
        if (ids.Count == 0) ids.Add("WatkinsGlen");
        ids.Sort(System.StringComparer.OrdinalIgnoreCase);
        return ids.ToArray();
    }

    // ------------------------------------------------------------------ file

    string Path => WeekendPlanLibrary.AssetPath(_track, _series);

    void Reload()
    {
        string path = Path;
        if (File.Exists(path))
        {
            try
            {
                _plan = JsonUtility.FromJson<WeekendPlan>(File.ReadAllText(path));
                _loadedFrom = path;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Weekend Plan: could not read {path} — {e.Message}");
                _plan = null;
                _loadedFrom = "";
            }
        }
        else
        {
            _plan = null;
            _loadedFrom = "";
        }
        _dirty = false;
    }

    void Save()
    {
        if (_plan == null) return;

        string path = Path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(_plan, prettyPrint: true));
        AssetDatabase.ImportAsset(path);

        // Without this the next Play would build from whatever was on disk when the editor started.
        WeekendPlanLibrary.ClearCache();

        _loadedFrom = path;
        _dirty = false;
        Debug.Log($"Weekend Plan: saved {path} ({_plan.EventCount} booking(s)).");
    }

    // ------------------------------------------------------------------ gui

    void OnGUI()
    {
        DrawHeader();

        if (_plan == null)
        {
            DrawNoPlan();
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawProblems();
        foreach (var slot in WeekendSlots.All) DrawSlot(slot);
        EditorGUILayout.Space(8f);
        DrawAreas();
        EditorGUILayout.EndScrollView();
    }

    void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int trackIndex = Mathf.Max(0, System.Array.IndexOf(_trackIds, _track));
        int picked = EditorGUILayout.Popup(trackIndex, _trackIds, EditorStyles.toolbarPopup, GUILayout.Width(180f));
        if (picked != trackIndex) { _track = _trackIds[picked]; Reload(); }

        var series = (RacingSeries)EditorGUILayout.EnumPopup(_series, EditorStyles.toolbarPopup, GUILayout.Width(100f));
        if (series != _series) { _series = series; Reload(); }

        GUILayout.FlexibleSpace();

        if (_plan != null)
        {
            GUI.enabled = _dirty;
            if (GUILayout.Button(_dirty ? "SAVE *" : "SAVE", EditorStyles.toolbarButton, GUILayout.Width(70f))) Save();
            GUI.enabled = true;

            if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(60f))) Reload();
            if (GUILayout.Button("Reveal", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                EditorUtility.RevealInFinder(_loadedFrom);
        }

        EditorGUILayout.EndHorizontal();

        if (_plan != null)
        {
            EditorGUILayout.LabelField(_loadedFrom, EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            _plan.notes = EditorGUILayout.TextField("Notes", _plan.notes);
            if (EditorGUI.EndChangeCheck()) _dirty = true;
        }
    }

    void DrawNoPlan()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.HelpBox(
            $"{_track} / {_series} has no plan file, so this round builds from the generated schedule in " +
            "WeekendTimetable.\n\nCreate one to take it over: the file wins outright, including its empty " +
            "half-days.", MessageType.Info);

        EditorGUILayout.Space(6f);
        if (GUILayout.Button("New Empty Plan (six blank half-days)", GUILayout.Height(28f)))
        {
            _plan = WeekendPlan.Empty(_track, _series);
            _dirty = true;
        }

        if (GUILayout.Button("New Plan From The Generated Schedule", GUILayout.Height(28f)))
        {
            _plan = WeekendPlanExport.FromTimetable(_track, _series);
            _dirty = true;
        }

        EditorGUILayout.LabelField(
            "The second one writes out what this round already plays as, so you edit rather than start blank.",
            EditorStyles.wordWrappedMiniLabel);
    }

    void DrawProblems()
    {
        var problems = _plan.Problems();
        if (problems.Count == 0) return;

        EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Error);
    }

    void DrawSlot(WeekendSlot slot)
    {
        var planSlot = _plan.EnsureSlot(slot);
        planSlot.events ??= new List<WeekendPlanEvent>();

        string key = slot.ToString();
        bool open = !_collapsed.Contains(key);

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        bool nowOpen = EditorGUILayout.Foldout(open,
            $"{WeekendSlots.Label(slot)}   ({WeekendSlots.Clock(WeekendSlots.OpensAt(slot))}–" +
            $"{WeekendSlots.Clock(WeekendSlots.ClosesAt(slot))})   {planSlot.events.Count} booked", true,
            EditorStyles.foldoutHeader);

        if (nowOpen != open)
        {
            if (nowOpen) _collapsed.Remove(key); else _collapsed.Add(key);
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Booking", GUILayout.Width(80f)))
        {
            planSlot.events.Add(new WeekendPlanEvent
            {
                @event = "sponsor_event-photoshoot",
                start = WeekendSlots.Clock(WeekendSlots.OpensAt(slot) + 60),
            });
            _dirty = true;
        }
        EditorGUILayout.EndHorizontal();

        if (!nowOpen) return;

        if (planSlot.events.Count == 0)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("empty — nothing on, the player has the half-day to themselves",
                                       EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
            return;
        }

        var ordered = planSlot.events.OrderBy(e => WeekendPlan.ParseClock(e?.start)).ToList();
        foreach (var e in ordered) DrawEvent(planSlot, e, slot);
    }

    void DrawEvent(WeekendPlanSlot planSlot, WeekendPlanEvent e, WeekendSlot slot)
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();

        int index = Mathf.Max(0, System.Array.IndexOf(_eventIds, e.@event));
        index = EditorGUILayout.Popup(index, _eventIds);
        e.@event = _eventIds[index];

        e.start = EditorGUILayout.TextField(e.start, GUILayout.Width(60f));

        WeekendEventCatalog.TryGet(e.@event, out var type);
        int minutes = e.minutes > 0 ? e.minutes : type.minutes;
        EditorGUILayout.LabelField($"{minutes}m", GUILayout.Width(40f));

        if (GUILayout.Button("×", GUILayout.Width(22f)))
        {
            planSlot.events.Remove(e);
            _dirty = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
            return;
        }
        EditorGUILayout.EndHorizontal();

        // The one line that says what this booking is, straight off the catalogue unless overridden.
        EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(e.subtitle) ? type.subtitle : e.subtitle,
                                   EditorStyles.wordWrappedMiniLabel);

        if (type.needsSeries)
            e.series = EditorGUILayout.TextField("Whose session", e.series);

        DrawMarkerLocation(e, WeekendVenues.For(type.kind));
        e.minutes = EditorGUILayout.IntField("Minutes (0 = default)", e.minutes);
        e.title = EditorGUILayout.TextField("Title override", e.title);

        if (EditorGUI.EndChangeCheck()) _dirty = true;

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    // The object in the track package this booking sends the player to. Left blank it falls back to the
    // venue's own marker, and the field shows which name that resolves to — so the default is visible rather
    // than implied, and overriding it is a matter of typing a different object's name.
    void DrawMarkerLocation(WeekendPlanEvent e, WeekendVenue venue)
    {
        string fallback = WeekendMarkerNames.DefaultNameFor(venue);
        bool overridden = !string.IsNullOrWhiteSpace(e.markerLocation);
        string wanted = overridden ? e.markerLocation : fallback;

        EditorGUILayout.BeginHorizontal();
        e.markerLocation = EditorGUILayout.TextField("Marker Location", e.markerLocation);

        // Whether that object is actually in the open scene. A name that resolves to nothing is a booking
        // whose marker falls back to a generated venue, which is the thing this whole feature exists to stop.
        var found = FindMarkerNamed(wanted);
        var colour = GUI.color;
        GUI.color = found != null ? new Color(0.6f, 0.9f, 0.6f) : new Color(1f, 0.7f, 0.4f);
        EditorGUILayout.LabelField(found != null ? "in scene" : "not found", GUILayout.Width(70f));
        GUI.color = colour;

        GUI.enabled = found != null;
        if (GUILayout.Button("Select", GUILayout.Width(55f))) Selection.activeGameObject = found.gameObject;
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        if (!overridden)
            EditorGUILayout.LabelField($"default: {fallback}", EditorStyles.miniLabel);
        else if (found != null && found.HasTeleport)
            EditorGUILayout.LabelField("walk to the marker, then teleported to " + found.teleportTo.name,
                                       EditorStyles.miniLabel);
        EditorGUI.indentLevel--;
    }

    static WeekendMarker FindMarkerNamed(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string want = Simplify(name);
        foreach (var marker in Object.FindObjectsByType<WeekendMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (marker != null && Simplify(marker.name) == want) return marker;
        return null;
    }

    static string Simplify(string text) => WeekendMarkerNames.Simplify(text);

    // ------------------------------------------------------------------ markers

    void DrawAreas()
    {
        EditorGUILayout.LabelField("MARKERS IN THE OPEN SCENE", EditorStyles.boldLabel);

        var markers = Object.FindObjectsByType<WeekendMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (markers.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "No markers in the open scene. Without one, every venue is worked out from the pit lane at " +
                "runtime — which is what puts a marker on the fence line.\n\n" +
                "Add one by naming a GameObject 'PitBox_Marker' (or Hospitality_Marker, Signing_Marker, " +
                "Stage_Marker, DriversRoom_Marker, Grandstand_Marker), or with the button below. Give it a " +
                "collider and its shape becomes the perimeter the booking starts inside.",
                MessageType.Warning);
        }

        foreach (var marker in markers.OrderBy(m => m.name))
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.LabelField(marker.name, GUILayout.Width(180f));
            EditorGUILayout.LabelField(marker.venue == WeekendVenue.None ? "(by name only)" : marker.venue.ToString(),
                                       GUILayout.Width(110f));

            bool reachable = marker.IsReachable(out float outsideBy);
            var colour = GUI.color;
            GUI.color = reachable ? new Color(0.6f, 0.9f, 0.6f) : new Color(1f, 0.55f, 0.5f);
            EditorGUILayout.LabelField(reachable ? "reachable" : $"OUTSIDE by {outsideBy:0.0} m",
                                       GUILayout.Width(110f));
            GUI.color = colour;

            if (marker.HasTeleport) EditorGUILayout.LabelField("→ teleports", GUILayout.Width(70f));

            if (GUILayout.Button("Select", GUILayout.Width(55f))) Selection.activeGameObject = marker.gameObject;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Create Marker At Scene-View Centre"))
            WeekendMarkerMenu.CreateMarker(null);
    }
}
