using System.Collections.Generic;
using Draftmaster.Data;
using SQLite;
using UnityEditor;
using UnityEngine;

// Edit-time browser/editor for the Drivers table in draftmaster.db (Application.persistentDataPath).
// Window ▸ Draftmaster ▸ Driver Database. Opens the same SQLite file the game uses, so edits here show up in play.
public class DriverDatabaseWindow : EditorWindow
{
    const string DbFileName = "draftmaster.db";

    SQLiteConnection _db;
    string _error;
    readonly List<Driver> _drivers = new();
    int _selected = -1;
    string _search = "";
    Vector2 _listScroll, _editScroll;

    [MenuItem("Window/Draftmaster/Driver Database")]
    static void Open() => GetWindow<DriverDatabaseWindow>("Drivers");

    string DbPath => System.IO.Path.Combine(Application.persistentDataPath, DbFileName);

    void OnEnable() => Connect();
    void OnDisable() => Close_();

    void Connect()
    {
        _error = null;
        Close_();
        try
        {
            _db = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
            _db.CreateTable<Driver>();
            Reload();
        }
        catch (System.Exception e)
        {
            _error = e.Message;
            _db = null;
        }
    }

    void Close_()
    {
        if (_db != null) { _db.Close(); _db.Dispose(); _db = null; }
    }

    void Reload()
    {
        _drivers.Clear();
        if (_db == null) return;
        _drivers.AddRange(_db.Table<Driver>());
        _drivers.Sort((a, b) =>
        {
            int c = string.Compare(a.LastName, b.LastName, System.StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.FirstName, b.FirstName, System.StringComparison.OrdinalIgnoreCase);
        });
        _selected = Mathf.Clamp(_selected, -1, _drivers.Count - 1);
    }

    void OnGUI()
    {
        DrawToolbar();

        if (_error != null)
        {
            EditorGUILayout.HelpBox("DB error: " + _error, MessageType.Error);
            if (GUILayout.Button("Retry connect")) Connect();
            return;
        }
        if (_db == null) { EditorGUILayout.HelpBox("No database connection.", MessageType.Warning); return; }

        EditorGUILayout.BeginHorizontal();
        DrawList();
        DrawEditor();
        EditorGUILayout.EndHorizontal();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) Connect();
        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50))) CreateDriver();
        if (GUILayout.Button("Reseed", EditorStyles.toolbarButton, GUILayout.Width(60))) Reseed();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{_drivers.Count} drivers", EditorStyles.miniLabel);
        if (GUILayout.Button("Open folder", EditorStyles.toolbarButton, GUILayout.Width(80)))
            EditorUtility.RevealInFinder(DbPath);
        EditorGUILayout.EndHorizontal();
    }

    void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(220));
        _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        for (int i = 0; i < _drivers.Count; i++)
        {
            var d = _drivers[i];
            string label = $"{d.LastName}, {d.FirstName}";
            if (!string.IsNullOrEmpty(_search) &&
                label.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                (d.Nickname ?? "").IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            bool on = i == _selected;
            var style = on ? EditorStyles.boldLabel : EditorStyles.label;
            string num = d.CarNumber > 0 ? $"#{d.CarNumber} " : "";
            if (GUILayout.Button($"{num}{label}  ({d.CurrentAbility})", style)) _selected = i;
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawEditor()
    {
        EditorGUILayout.BeginVertical();
        if (_selected < 0 || _selected >= _drivers.Count)
        {
            EditorGUILayout.HelpBox("Select a driver from the list, or click New.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        var d = _drivers[_selected];
        _editScroll = EditorGUILayout.BeginScrollView(_editScroll);

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        d.FirstName = EditorGUILayout.TextField("First Name", d.FirstName);
        d.LastName = EditorGUILayout.TextField("Last Name", d.LastName);
        d.ShortName = EditorGUILayout.TextField("Short Name", d.ShortName);
        d.Nickname = EditorGUILayout.TextField("Nickname", d.Nickname);
        d.Age = EditorGUILayout.IntField("Age", d.Age);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ride", EditorStyles.boldLabel);
        d.CarNumber = EditorGUILayout.IntField("Car Number", d.CarNumber);
        d.TeamName = EditorGUILayout.TextField("Team", d.TeamName);
        d.Manufacturer = EditorGUILayout.TextField("Manufacturer", d.Manufacturer);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Track Aptitudes (0-20)", EditorStyles.boldLabel);
        d.ShortTracks = Stat("Short Tracks", d.ShortTracks);
        d.Speedways = Stat("Speedways", d.Speedways);
        d.Superspeedways = Stat("Superspeedways", d.Superspeedways);
        d.RoadCourses = Stat("Road Courses", d.RoadCourses);
        d.DirtCourses = Stat("Dirt Courses", d.DirtCourses);
        d.OpenWheel = Stat("Open Wheel", d.OpenWheel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Craft (0-20)", EditorStyles.boldLabel);
        d.FuelManagement = Stat("Fuel Management", d.FuelManagement);
        d.TyreManagement = Stat("Tyre Management", d.TyreManagement);
        d.Qualifying = Stat("Qualifying", d.Qualifying);
        d.Consistency = Stat("Consistency", d.Consistency);
        d.Aggression = Stat("Aggression", d.Aggression);
        d.Awareness = Stat("Awareness", d.Awareness);
        d.Adaptability = Stat("Adaptability", d.Adaptability);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Standing (0-20)", EditorStyles.boldLabel);
        d.SponsorAppeal = Stat("Sponsor Appeal", d.SponsorAppeal);
        d.FanSupport = Stat("Fan Support", d.FanSupport);
        d.Prestige = Stat("Prestige", d.Prestige);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ability (0-100)", EditorStyles.boldLabel);
        d.CurrentAbility = EditorGUILayout.IntSlider("Current", d.CurrentAbility, 0, Driver.AbilityMax);
        d.PotentialAbility = EditorGUILayout.IntSlider("Potential", d.PotentialAbility, 0, Driver.AbilityMax);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save", GUILayout.Height(28)))
        {
            _db.Update(d);
            Reload();
        }
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Delete", GUILayout.Width(80), GUILayout.Height(28)) &&
            EditorUtility.DisplayDialog("Delete driver", $"Delete {d.FirstName} {d.LastName}?", "Delete", "Cancel"))
        {
            _db.Delete(d);
            _selected = -1;
            Reload();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    static int Stat(string label, int value) => EditorGUILayout.IntSlider(label, value, 0, Driver.StatMax);

    void CreateDriver()
    {
        var d = new Driver { FirstName = "New", LastName = "Driver", Nickname = "", Age = 25 };
        _db.Insert(d);
        Reload();
        _selected = _drivers.FindIndex(x => x.Id == d.Id);
    }

    void Reseed()
    {
        if (!EditorUtility.DisplayDialog("Reseed drivers",
            "Drop ALL drivers and reinsert the DummyDrivers set? This wipes any edits.", "Reseed", "Cancel"))
            return;
        _db.DropTable<Driver>();
        _db.CreateTable<Driver>();
        _db.InsertAll(DummyDrivers.Build());
        _selected = -1;
        Reload();
    }
}
