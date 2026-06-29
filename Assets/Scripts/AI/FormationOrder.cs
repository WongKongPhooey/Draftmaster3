using System.Collections.Generic;

// A car taking part in the formation lap, ordered by grid (qualifying) position. Both the kinematic spline cars
// (AI + safety car) and the free-driven player implement this, so the field forms up in qualifying order with the
// player's grid slot reserved — each car stations behind the member directly ahead of it in the order, instead of
// just the nearest car ahead (which let cars slot into the player's place and made the running order feel wrong).
public interface IFormationMember
{
    int GridPosition { get; }    // qualifying index (0 = pole). The safety car uses FormationOrder.SafetyCarGrid.
    float TrackDistance { get; } // along-track distance (m), comparable across members on the same track
    float TrackLateral { get; }  // signed lateral offset from centerline (m)
    float SpeedMph { get; }
    bool FormationActive { get; } // registered, on-track, and currently valid to follow
}

public static class FormationOrder
{
    // Sentinel grid for the safety car: below every real qualifying position so the pole car follows it.
    public const int SafetyCarGrid = -1000;

    static readonly List<IFormationMember> _members = new();
    public static IReadOnlyList<IFormationMember> Members => _members;

    public static void Register(IFormationMember m)
    {
        if (m == null) return;
        if (!_members.Contains(m)) _members.Add(m);
    }

    public static void Unregister(IFormationMember m)
    {
        if (m == null) return;
        _members.Remove(m);
    }

    // The member directly ahead of `grid` in the order — the one with the greatest grid position still below it.
    // Null when nothing is ahead (e.g. the safety car itself, or before others have registered).
    public static IFormationMember MemberAhead(int grid)
    {
        IFormationMember best = null;
        int bestGrid = int.MinValue;
        for (int i = 0; i < _members.Count; i++)
        {
            var m = _members[i];
            if (m == null || !m.FormationActive) continue;
            int g = m.GridPosition;
            if (g < grid && g > bestGrid) { bestGrid = g; best = m; }
        }
        return best;
    }

    static int Parity(int g) => ((g % 2) + 2) % 2;

    // The member one ROW ahead of `grid` in a two-wide formation: the nearest member ahead in the SAME column
    // (same grid parity). Even-parity cars chain down to the safety car (its sentinel grid is even); odd-parity
    // front-row car has no same-column car ahead, so it also falls back to the safety car. Used to pack the
    // field into tight rows of two for the close-up before the green.
    public static IFormationMember RowAhead(int grid)
    {
        int parity = Parity(grid);
        IFormationMember best = null;
        int bestGrid = int.MinValue;
        IFormationMember safety = null;
        for (int i = 0; i < _members.Count; i++)
        {
            var m = _members[i];
            if (m == null || !m.FormationActive) continue;
            int g = m.GridPosition;
            if (g == SafetyCarGrid) safety = m;
            if (g < grid && Parity(g) == parity && g > bestGrid) { bestGrid = g; best = m; }
        }
        return best ?? safety;
    }
}
