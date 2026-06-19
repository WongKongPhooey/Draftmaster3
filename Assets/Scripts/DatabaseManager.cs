using Mono.Data.Sqlite;
using System.Data;
using System.Collections.Generic;
using UnityEngine;

// DatabaseManager is the single gateway to the local SQLite database that ships
// with the game (MyDatabase.sqlite). It owns the schema and exposes a small set
// of static helpers so that the rest of the game (StatsManager, QuestManager,
// driving systems, dialogue) can persist player stats and side-quest progress
// without each of them having to know any SQL.
//
// Two tables are managed here:
//   PlayerStats(statName TEXT PRIMARY KEY, statValue INTEGER)
//   SideQuests(questId TEXT PRIMARY KEY, status TEXT, progress INTEGER,
//              target INTEGER, rewardStat TEXT, rewardAmount INTEGER)
public class DatabaseManager : MonoBehaviour
{
    // Kept as a field so a build can point it at Application.persistentDataPath
    // if needed, while the editor/default keeps parity with the original code.
    private static string dbUri = "URI=file:MyDatabase.sqlite";

    void Start(){
        // Make sure the schema exists as soon as the manager wakes up so the
        // first stat/quest write never has to create a table mid-game.
        EnsureSchema();
    }

    // ---------------------------------------------------------------------
    // Connection / schema
    // ---------------------------------------------------------------------

    private static IDbConnection CreateAndOpenDatabase(){
        IDbConnection dbConnection = new SqliteConnection(dbUri);
        dbConnection.Open();
        return dbConnection;
    }

    // Creates every table the game relies on if it isn't there yet. Safe to
    // call as often as you like - it is a no-op once the tables exist.
    public static void EnsureSchema(){
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText =
                    "CREATE TABLE IF NOT EXISTS PlayerStats (" +
                    "statName TEXT PRIMARY KEY, " +
                    "statValue INTEGER NOT NULL DEFAULT 0)";
                command.ExecuteNonQuery();
            }
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText =
                    "CREATE TABLE IF NOT EXISTS SideQuests (" +
                    "questId TEXT PRIMARY KEY, " +
                    "status TEXT NOT NULL DEFAULT 'available', " +
                    "progress INTEGER NOT NULL DEFAULT 0, " +
                    "target INTEGER NOT NULL DEFAULT 1, " +
                    "rewardStat TEXT, " +
                    "rewardAmount INTEGER NOT NULL DEFAULT 0)";
                command.ExecuteNonQuery();
            }
            dbConnection.Close();
        }
    }

    // ---------------------------------------------------------------------
    // Player stats
    // ---------------------------------------------------------------------

    // Returns the stored value for a stat, or 0 if it has never been written.
    public static int GetStat(string statName){
        int value = 0;
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "SELECT statValue FROM PlayerStats WHERE statName = @name";
                AddParameter(command, "@name", statName);
                object result = command.ExecuteScalar();
                if(result != null && result != System.DBNull.Value){
                    value = System.Convert.ToInt32(result);
                }
            }
            dbConnection.Close();
        }
        return value;
    }

    // Overwrites a stat with an absolute value. INSERT OR REPLACE is safe here
    // because PlayerStats only carries the two columns we are writing.
    public static void SetStat(string statName, int value){
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText =
                    "INSERT OR REPLACE INTO PlayerStats (statName, statValue) VALUES (@name, @value)";
                AddParameter(command, "@name", statName);
                AddParameter(command, "@value", value);
                command.ExecuteNonQuery();
            }
            dbConnection.Close();
        }
    }

    // Adds (or subtracts, for a negative amount) to a stat and returns the new
    // total. Uses an UPDATE-then-INSERT pattern rather than UPSERT so it works
    // on the older SQLite builds Unity bundles on some platforms.
    public static int AddStat(string statName, int amount){
        int newValue;
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            int rowsAffected;
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "UPDATE PlayerStats SET statValue = statValue + @amount WHERE statName = @name";
                AddParameter(command, "@amount", amount);
                AddParameter(command, "@name", statName);
                rowsAffected = command.ExecuteNonQuery();
            }
            if(rowsAffected == 0){
                using(IDbCommand insert = dbConnection.CreateCommand()){
                    insert.CommandText = "INSERT INTO PlayerStats (statName, statValue) VALUES (@name, @amount)";
                    AddParameter(insert, "@name", statName);
                    AddParameter(insert, "@amount", amount);
                    insert.ExecuteNonQuery();
                }
            }
            using(IDbCommand readBack = dbConnection.CreateCommand()){
                readBack.CommandText = "SELECT statValue FROM PlayerStats WHERE statName = @name";
                AddParameter(readBack, "@name", statName);
                object result = readBack.ExecuteScalar();
                newValue = (result == null || result == System.DBNull.Value)
                    ? amount : System.Convert.ToInt32(result);
            }
            dbConnection.Close();
        }
        return newValue;
    }

    // Reads every stat into a dictionary, handy for stat screens / debugging.
    public static Dictionary<string, int> GetAllStats(){
        Dictionary<string, int> stats = new Dictionary<string, int>();
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "SELECT statName, statValue FROM PlayerStats";
                using(IDataReader reader = command.ExecuteReader()){
                    while(reader.Read()){
                        stats[reader.GetString(0)] = reader.GetInt32(1);
                    }
                }
            }
            dbConnection.Close();
        }
        return stats;
    }

    // ---------------------------------------------------------------------
    // Side-quests
    // ---------------------------------------------------------------------

    // Inserts a quest definition if it has never been seen. Existing rows are
    // left untouched so a returning player keeps their progress and status.
    public static void RegisterQuest(string questId, int target, string rewardStat, int rewardAmount){
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText =
                    "INSERT OR IGNORE INTO SideQuests " +
                    "(questId, status, progress, target, rewardStat, rewardAmount) " +
                    "VALUES (@id, 'available', 0, @target, @rewardStat, @rewardAmount)";
                AddParameter(command, "@id", questId);
                AddParameter(command, "@target", target < 1 ? 1 : target);
                AddParameter(command, "@rewardStat", rewardStat);
                AddParameter(command, "@rewardAmount", rewardAmount);
                command.ExecuteNonQuery();
            }
            dbConnection.Close();
        }
    }

    public static string GetQuestStatus(string questId){
        string status = "available";
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "SELECT status FROM SideQuests WHERE questId = @id";
                AddParameter(command, "@id", questId);
                object result = command.ExecuteScalar();
                if(result != null && result != System.DBNull.Value){
                    status = result.ToString();
                }
            }
            dbConnection.Close();
        }
        return status;
    }

    public static void SetQuestStatus(string questId, string status){
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "UPDATE SideQuests SET status = @status WHERE questId = @id";
                AddParameter(command, "@status", status);
                AddParameter(command, "@id", questId);
                command.ExecuteNonQuery();
            }
            dbConnection.Close();
        }
    }

    public static int GetQuestProgress(string questId){
        int progress = 0;
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "SELECT progress FROM SideQuests WHERE questId = @id";
                AddParameter(command, "@id", questId);
                object result = command.ExecuteScalar();
                if(result != null && result != System.DBNull.Value){
                    progress = System.Convert.ToInt32(result);
                }
            }
            dbConnection.Close();
        }
        return progress;
    }

    public static int GetQuestTarget(string questId){
        int target = 1;
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "SELECT target FROM SideQuests WHERE questId = @id";
                AddParameter(command, "@id", questId);
                object result = command.ExecuteScalar();
                if(result != null && result != System.DBNull.Value){
                    target = System.Convert.ToInt32(result);
                }
            }
            dbConnection.Close();
        }
        return target;
    }

    // Bumps a quest's progress counter and returns the new value.
    public static int AddQuestProgress(string questId, int amount){
        int newProgress = amount;
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "UPDATE SideQuests SET progress = progress + @amount WHERE questId = @id";
                AddParameter(command, "@amount", amount);
                AddParameter(command, "@id", questId);
                command.ExecuteNonQuery();
            }
            using(IDbCommand readBack = dbConnection.CreateCommand()){
                readBack.CommandText = "SELECT progress FROM SideQuests WHERE questId = @id";
                AddParameter(readBack, "@id", questId);
                object result = readBack.ExecuteScalar();
                if(result != null && result != System.DBNull.Value){
                    newProgress = System.Convert.ToInt32(result);
                }
            }
            dbConnection.Close();
        }
        return newProgress;
    }

    // Returns the reward (stat name + amount) attached to a quest.
    public static void GetQuestReward(string questId, out string rewardStat, out int rewardAmount){
        rewardStat = null;
        rewardAmount = 0;
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "SELECT rewardStat, rewardAmount FROM SideQuests WHERE questId = @id";
                AddParameter(command, "@id", questId);
                using(IDataReader reader = command.ExecuteReader()){
                    if(reader.Read()){
                        rewardStat = reader.IsDBNull(0) ? null : reader.GetString(0);
                        rewardAmount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    }
                }
            }
            dbConnection.Close();
        }
    }

    // Quest ids whose objective is matched by a given driving/dialogue event,
    // limited to those currently in the 'active' state.
    public static List<string> GetActiveQuestIds(){
        List<string> ids = new List<string>();
        using(IDbConnection dbConnection = CreateAndOpenDatabase()){
            using(IDbCommand command = dbConnection.CreateCommand()){
                command.CommandText = "SELECT questId FROM SideQuests WHERE status = 'active'";
                using(IDataReader reader = command.ExecuteReader()){
                    while(reader.Read()){
                        ids.Add(reader.GetString(0));
                    }
                }
            }
            dbConnection.Close();
        }
        return ids;
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static void AddParameter(IDbCommand command, string name, object value){
        IDbDataParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? System.DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
