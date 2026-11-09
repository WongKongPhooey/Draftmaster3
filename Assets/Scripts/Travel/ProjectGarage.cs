using System.Collections.Generic;
using UnityEngine;

// Wrecks bought out of junkyards, waiting to be rebuilt: the way into the other ladders (Rookie Stocks, Dirt
// Stocks, Single-Make Sports Cars, Sprint Cars, Indy Lights — Draftmaster.Tracks.AltSeriesCatalog). Stored in
// PlayerPrefs like the rest of the road trip (TravelState). Only ownership for now: rebuilding (cost, time,
// who does it) and racing the result are the next step, and read from here.
public static class ProjectGarage
{
    const string Key = "travel.projectcars";

    [System.Serializable]
    public class ProjectCar
    {
        public string seriesId;
        public int damagePercent;
        public int repairCost;
        public int pricePaid;
        public string boughtAt;   // junkyard location id
        public int boughtWeek;
        public int repairedPercent;   // 0 until rebuilding exists
    }

    [System.Serializable]
    class Store { public List<ProjectCar> cars = new(); }

    static Store Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return new Store();
        try { return JsonUtility.FromJson<Store>(json) ?? new Store(); }
        catch (System.Exception) { return new Store(); }
    }

    static void Save(Store s)
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
        PlayerPrefs.Save();
    }

    public static IReadOnlyList<ProjectCar> Cars => Load().cars;
    public static int Count => Load().cars.Count;

    public static void Add(Draftmaster.Tracks.Wreck w, string yardId, int week)
    {
        var s = Load();
        s.cars.Add(new ProjectCar
        {
            seriesId = w.series.id, damagePercent = w.damagePercent, repairCost = w.repairCost,
            pricePaid = w.price, boughtAt = yardId, boughtWeek = week,
        });
        Save(s);
    }

    public static void Clear() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
}
