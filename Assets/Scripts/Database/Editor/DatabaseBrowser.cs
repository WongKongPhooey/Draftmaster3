using System.Collections.Generic;
using System.IO;
using SQLite;
using UnityEditor;
using UnityEngine;

// Look inside the game's SQLite database without leaving Unity.
//
// The database is the game's memory — every driver, team, contract, entry, race and result lives in it, and
// until now the only way to see any of that was to add a Debug.Log, enter Play Mode, and read the console. A
// question as small as "did the season rollover actually write the new contracts" cost a play session.
//
// This is a reader, deliberately. It opens its OWN connection with SQLiteOpenFlags.ReadOnly, so it cannot
// corrupt a running game's state and it cannot be blamed when something goes wrong with a save; SQLite is
// happy with several readers at once, so it works while the game is playing. Nothing here writes, and the
// query box refuses anything that is not a SELECT.
//
// Rows are read through the low-level SQLite3 API rather than sqlite-net's typed Query<T>, because the whole
// point is to browse tables WITHOUT having a model class for them — including the ones sqlite-net makes for
// itself, and any table added since the last time this window was touched.
public class DatabaseBrowser : EditorWindow
{
    const int PageSize = 200;

    [MenuItem("Draftmaster/Database/Browse Database %#d", priority = 1)]
    public static void Open()
    {
        var window = GetWindow<DatabaseBrowser>("Database");
        window.minSize = new Vector2(520f, 300f);
        window.Refresh();
    }

    string _dbPath;
    readonly List<string> _tables = new();
    int _table = -1;

    string _sql = "";
    string _ranSql = "";
    string _error;

    List<string> _columns = new();
    List<string[]> _rows = new();
    int _page;
    int _rowCount;

    Vector2 _tableScroll, _gridScroll;

    void OnEnable() => Refresh();

    // ---------------------------------------------------------------- reading

    // The same file DatabaseManager opens. Read off the component when one exists so a renamed database
    // file still resolves; fall back to the default name when the editor has no scene loaded.
    static string ResolvePath()
    {
        var manager = Object.FindFirstObjectByType<DatabaseManager>(FindObjectsInactive.Include);
        string file = manager != null ? manager.databaseFileName : "draftmaster.db";
        if (string.IsNullOrEmpty(file)) file = "draftmaster.db";
        return Path.Combine(Application.persistentDataPath, file);
    }

    public void Refresh()
    {
        _dbPath = ResolvePath();
        _tables.Clear();
        _error = null;

        if (!File.Exists(_dbPath))
        {
            _error = "No database on disk yet. It is created the first time the game runs.";
            return;
        }

        foreach (var row in Read("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name", out _))
            if (row.Length > 0) _tables.Add(row[0]);

        if (_tables.Count > 0 && _table < 0) Select(0);
        else if (_table >= _tables.Count) _table = _tables.Count - 1;
    }

    void Select(int index)
    {
        _table = index;
        _page = 0;
        _sql = $"SELECT * FROM \"{_tables[index]}\"";
        Run();
    }

    void Run()
    {
        _error = null;
        _ranSql = _sql;
        _rows = Read(Paged(_sql), out _columns);
        _rowCount = CountOf(_sql);
    }

    string Paged(string sql)
    {
        // Never pull a whole results table into the editor: a season's worth of results is tens of thousands
        // of rows and drawing them all would hang the window rather than show it.
        return sql.ToLowerInvariant().Contains(" limit ") ? sql : $"{sql} LIMIT {PageSize} OFFSET {_page * PageSize}";
    }

    int CountOf(string sql)
    {
        var rows = Read($"SELECT COUNT(*) FROM ({sql})", out _);
        return rows.Count > 0 && rows[0].Length > 0 && int.TryParse(rows[0][0], out int n) ? n : rows.Count;
    }

    // Any SELECT, as strings, through the low-level API — no model class needed for the table being read.
    List<string[]> Read(string sql, out List<string> columns)
    {
        columns = new List<string>();
        var rows = new List<string[]>();
        if (string.IsNullOrWhiteSpace(sql) || !File.Exists(_dbPath)) return rows;

        SQLiteConnection connection = null;
        try
        {
            connection = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);
            var stmt = SQLite3.Prepare2(connection.Handle, sql);
            try
            {
                int columnCount = SQLite3.ColumnCount(stmt);
                for (int i = 0; i < columnCount; i++) columns.Add(SQLite3.ColumnName16(stmt, i));

                while (SQLite3.Step(stmt) == SQLite3.Result.Row)
                {
                    var row = new string[columnCount];
                    for (int i = 0; i < columnCount; i++)
                        row[i] = SQLite3.ColumnType(stmt, i) == SQLite3.ColType.Null ? "NULL" : SQLite3.ColumnString(stmt, i);
                    rows.Add(row);
                }
            }
            finally { SQLite3.Finalize(stmt); }
        }
        catch (SQLiteException e) { _error = e.Message; }
        catch (System.Exception e) { _error = e.Message; }
        finally { connection?.Close(); }

        return rows;
    }

    // ---------------------------------------------------------------- drawing

    void OnGUI()
    {
        DrawToolbar();

        if (!string.IsNullOrEmpty(_error))
            EditorGUILayout.HelpBox(_error, MessageType.Warning);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTableList();
            using (new EditorGUILayout.VerticalScope())
            {
                DrawQueryBox();
                DrawGrid();
            }
        }
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f))) Refresh();
            if (GUILayout.Button("Reveal file", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                if (File.Exists(_dbPath)) EditorUtility.RevealInFinder(_dbPath);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(_dbPath ?? "", EditorStyles.miniLabel);
        }
    }

    void DrawTableList()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(_tableScroll, GUILayout.Width(170f)))
        {
            _tableScroll = scroll.scrollPosition;
            for (int i = 0; i < _tables.Count; i++)
            {
                bool on = i == _table;
                if (GUILayout.Toggle(on, _tables[i], EditorStyles.miniButton) != on) Select(i);
            }
            if (_tables.Count == 0) GUILayout.Label("no tables", EditorStyles.miniLabel);
        }
    }

    void DrawQueryBox()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            _sql = EditorGUILayout.TextField(_sql);
            if (GUILayout.Button("Run", GUILayout.Width(50f)))
            {
                // Read-only on purpose: this window opens the database a running game may be using, and a
                // stray UPDATE typed into a debugging tool is a bug report nobody can reproduce.
                string head = (_sql ?? "").TrimStart();
                if (head.StartsWith("select", System.StringComparison.OrdinalIgnoreCase) ||
                    head.StartsWith("with", System.StringComparison.OrdinalIgnoreCase))
                    Run();
                else
                    _error = "This window only runs SELECT. It opens the database read-only so it can never " +
                             "damage a save or a running game.";
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(_rowCount / (float)PageSize));
            using (new EditorGUI.DisabledScope(_page <= 0))
                if (GUILayout.Button("<", GUILayout.Width(26f))) { _page--; Rerun(); }
            using (new EditorGUI.DisabledScope(_page >= pages - 1))
                if (GUILayout.Button(">", GUILayout.Width(26f))) { _page++; Rerun(); }

            GUILayout.Label($"{_rowCount} row(s) — page {_page + 1}/{pages}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (_rows.Count > 0 && GUILayout.Button("Copy as TSV", EditorStyles.miniButton, GUILayout.Width(90f)))
                EditorGUIUtility.systemCopyBuffer = Tsv();
        }
    }

    void Rerun()
    {
        _sql = _ranSql;
        Run();
    }

    void DrawGrid()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(_gridScroll))
        {
            _gridScroll = scroll.scrollPosition;

            if (_columns.Count > 0)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    foreach (var c in _columns)
                        GUILayout.Label(c, EditorStyles.miniBoldLabel, GUILayout.Width(Width(c)));
            }

            foreach (var row in _rows)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < row.Length && i < _columns.Count; i++)
                        EditorGUILayout.SelectableLabel(row[i], EditorStyles.miniLabel,
                                                        GUILayout.Width(Width(_columns[i])), GUILayout.Height(16f));
                }
            }

            if (_rows.Count == 0 && string.IsNullOrEmpty(_error))
                GUILayout.Label("no rows", EditorStyles.miniLabel);
        }
    }

    // Column width from the widest thing actually in it, so an Id column does not get the same room as a
    // driver name. Capped, or one long string pushes everything else off the window.
    float Width(string column)
    {
        int index = _columns.IndexOf(column);
        int widest = column.Length;
        if (index >= 0)
            foreach (var row in _rows)
                if (index < row.Length && row[index] != null) widest = Mathf.Max(widest, row[index].Length);
        return Mathf.Clamp(widest * 7f + 12f, 44f, 260f);
    }

    string Tsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join("\t", _columns));
        foreach (var row in _rows) sb.AppendLine(string.Join("\t", row));
        return sb.ToString();
    }
}
