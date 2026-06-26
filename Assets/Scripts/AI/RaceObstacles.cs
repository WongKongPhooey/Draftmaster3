using System.Collections.Generic;

// Human-driven cars the AI must brake/steer for. The free-driven player runs PlayerVehicleController with its
// SplineDriver disabled, so it never enters RaceField — the racing brain is otherwise blind to it and ploughs
// straight into a parked / spun player. PlayerVehicleController registers itself here (only when it is the
// active human car, i.e. no enabled SplineDriver), projecting its position onto the track centerline so the AI
// can treat it as a slow/stopped car ahead.
public static class RaceObstacles
{
    static readonly List<PlayerVehicleController> _list = new();
    public static IReadOnlyList<PlayerVehicleController> All => _list;

    public static void Register(PlayerVehicleController p)
    {
        if (p == null) return;
        if (!_list.Contains(p)) _list.Add(p);
    }

    public static void Unregister(PlayerVehicleController p)
    {
        if (p == null) return;
        _list.Remove(p);
    }
}
