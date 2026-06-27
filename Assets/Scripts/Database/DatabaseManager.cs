using System;
using System.Collections;
using System.IO;
using Draftmaster.Data;
using SQLite;
using UnityEngine;
using UnityEngine.Networking;

public class DatabaseManager : MonoBehaviour
{
    public static DatabaseManager Instance { get; private set; }

    [Tooltip("File name of the SQLite database on disk (under Application.persistentDataPath).")]
    public string databaseFileName = "draftmaster.db";

    [Tooltip("If true, look for a pre-seeded copy of databaseFileName in StreamingAssets and copy it to persistentDataPath on first launch.")]
    public bool seedFromStreamingAssets = true;

    [Tooltip("If true, overwrite the on-disk database with the StreamingAssets copy every launch. Useful while iterating on the seed db; leave off in production.")]
    public bool forceReseedEveryLaunch = false;

    public SQLiteConnection Connection { get; private set; }
    public bool IsReady { get; private set; }
    public event Action Ready;

    string DbPath => Path.Combine(Application.persistentDataPath, databaseFileName);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        StartCoroutine(InitialiseRoutine());
    }

    IEnumerator InitialiseRoutine()
    {
        if (seedFromStreamingAssets && (forceReseedEveryLaunch || !File.Exists(DbPath)))
        {
            yield return CopySeedFromStreamingAssets();
        }

        Connection = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
        CreateTables(Connection);
        IsReady = true;
        Ready?.Invoke();
    }

    // Bump when a model's columns change in a way that needs a clean rebuild of its table.
    const int SchemaVersion = 2;

    // Register table schemas here as model classes are added. CreateTable is idempotent — safe to call every launch.
    static void CreateTables(SQLiteConnection db)
    {
        // On a schema bump, drop tables whose columns changed so they're recreated + reseeded fresh
        // (sqlite-net only ADDs missing columns; it won't drop/rename old ones, which would leave stale data).
        const string key = "DbSchemaVersion";
        if (PlayerPrefs.GetInt(key, 1) < SchemaVersion)
        {
            db.DropTable<Driver>();
            PlayerPrefs.SetInt(key, SchemaVersion);
            PlayerPrefs.Save();
        }

        db.CreateTable<Driver>();
        SeedDriversIfEmpty(db);

        // Additive tables: CreateTable is idempotent, seed only when empty — no schema-version bump needed.
        // Fully-qualified: legacy global-namespace types (e.g. GarageUI's `Series`/`Entry`) otherwise shadow the models.
        db.CreateTable<Draftmaster.Data.Series>();
        SeedSeriesIfEmpty(db);

        db.CreateTable<Draftmaster.Data.Team>();
        SeedTeamsIfEmpty(db);

        db.CreateTable<Draftmaster.Data.Sponsor>();
        SeedSponsorsIfEmpty(db);

        db.CreateTable<Draftmaster.Data.Track>();
        SeedTracksIfEmpty(db);

        // Per-team staff: seeded after Teams exist (reads them to build a roster each).
        db.CreateTable<Draftmaster.Data.Staff>();
        SeedStaffIfEmpty(db);

        // Transactional tables — schema only; populated by season generation / the race sim, not statically seeded.
        db.CreateTable<Draftmaster.Data.Entry>();
        db.CreateTable<Draftmaster.Data.Race>();
        db.CreateTable<Draftmaster.Data.Result>();
        db.CreateTable<Draftmaster.Data.Contract>();
        db.CreateTable<Draftmaster.Data.DriverContract>();
        db.CreateTable<Draftmaster.Data.Standing>();
        db.CreateTable<Draftmaster.Data.Career>();
        db.CreateTable<Draftmaster.Data.Transaction>();
    }

    static void SeedDriversIfEmpty(SQLiteConnection db)
    {
        if (db.Table<Driver>().Count() > 0) return;
        db.InsertAll(DummyDrivers.Build());
    }

    static void SeedSeriesIfEmpty(SQLiteConnection db)
    {
        if (db.Table<Draftmaster.Data.Series>().Count() > 0) return;
        db.InsertAll(DummySeries.Build());
    }

    static void SeedTeamsIfEmpty(SQLiteConnection db)
    {
        if (db.Table<Draftmaster.Data.Team>().Count() > 0) return;
        db.InsertAll(DummyTeams.Build());
    }

    static void SeedSponsorsIfEmpty(SQLiteConnection db)
    {
        if (db.Table<Draftmaster.Data.Sponsor>().Count() > 0) return;
        db.InsertAll(DummySponsors.Build());
    }

    static void SeedTracksIfEmpty(SQLiteConnection db)
    {
        if (db.Table<Draftmaster.Data.Track>().Count() > 0) return;
        db.InsertAll(DummyTracks.Build());
    }

    static void SeedStaffIfEmpty(SQLiteConnection db)
    {
        if (db.Table<Draftmaster.Data.Staff>().Count() > 0) return;
        foreach (var team in db.Table<Draftmaster.Data.Team>())
            db.InsertAll(DummyStaff.ForTeam(team));
    }

    IEnumerator CopySeedFromStreamingAssets()
    {
        string source = Path.Combine(Application.streamingAssetsPath, databaseFileName);
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var req = UnityWebRequest.Get(source))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(DbPath, req.downloadHandler.data);
            }
            else
            {
                Debug.Log($"No seed db at {source} ({req.error}) — starting with empty database.");
            }
        }
#else
        if (File.Exists(source))
        {
            File.Copy(source, DbPath, overwrite: true);
        }
        else
        {
            Debug.Log($"No seed db at {source} — starting with empty database.");
        }
        yield break;
#endif
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Connection?.Close();
            Connection?.Dispose();
            Instance = null;
        }
    }
}
