using UnityEngine;

// The player's cash, PlayerPrefs-backed like the rest of the career state (PlayerStatsLedger,
// PlayerInventory). Earned from race payouts (RaceDirector), spent at travel-map locations.
public static class PlayerWallet
{
    const string Key = "career.cash";
    public const int StartingCash = 5000;

    public static int Cash
    {
        get => PlayerPrefs.GetInt(Key, StartingCash);
        private set { PlayerPrefs.SetInt(Key, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static void Add(int amount) { if (amount != 0) Cash = Cash + amount; }

    public static bool TrySpend(int amount)
    {
        if (amount < 0 || Cash < amount) return false;
        Cash = Cash - amount;
        return true;
    }

    public static string Format(int amount) => "$" + amount.ToString("N0");
    public static string CashText => Format(Cash);

    // Prize money by finishing position. P1 $12,000 sliding to a back-of-field minimum — enough that a
    // good weekend buys a mid engine after a few races, a bad one still keeps fuel in the tank.
    public static int PayoutForPosition(int position) =>
        position <= 0 ? 0 : Mathf.Max(800, 12000 - (position - 1) * 1100);
}
