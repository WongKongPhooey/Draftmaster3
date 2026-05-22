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

    // Register table schemas here as model classes are added. CreateTable is idempotent — safe to call every launch.
    static void CreateTables(SQLiteConnection db)
    {
        db.CreateTable<Driver>();
        SeedDriversIfEmpty(db);
    }

    static void SeedDriversIfEmpty(SQLiteConnection db)
    {
        if (db.Table<Driver>().Count() > 0) return;
        db.InsertAll(DummyDrivers.Build());
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
