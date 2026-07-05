using System.Collections.Generic;
using UnityEngine;

// Tiny persistent pouch for quest items ("lucky_charm", "spare_gauge"...). Stored as a CSV of ids in
// PlayerPrefs. Duplicates allowed — Remove takes one instance.
public static class PlayerInventory
{
    const string Key = "inventory.items";

    public static List<string> Items
    {
        get
        {
            var raw = PlayerPrefs.GetString(Key, "");
            var list = new List<string>();
            foreach (var s in raw.Split(','))
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
            return list;
        }
    }

    public static bool Has(string id) => !string.IsNullOrEmpty(id) && Items.Contains(id);

    public static void Add(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var items = Items;
        items.Add(id);
        Save(items);
    }

    public static bool Remove(string id)
    {
        var items = Items;
        if (!items.Remove(id)) return false;
        Save(items);
        return true;
    }

    static void Save(List<string> items)
    {
        PlayerPrefs.SetString(Key, string.Join(",", items));
        PlayerPrefs.Save();
    }
}
