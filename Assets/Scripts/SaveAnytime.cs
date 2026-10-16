using Draftmaster.Weekend;

// SAVE GAME on the pause menu: keep the career as it stands, right now, wherever the player is.
//
// Everything the career owns is already in PlayerPrefs (see CareerSave), so a save here is three things:
// write down which objective marker is up (SavedObjective), flush the lot to disk, and date it so the title
// screen's CONTINUE row says when. CONTINUE then calls RestoreMarker, which puts that marker back if the
// game came up without one.
//
// The player's spot in the paddock is not kept — CONTINUE wakes them in the motorhome as it always has —
// and nor is a conversation half-had. Obligations are a short walk and a chat; the booking is what matters.
public static class SaveAnytime
{
    // A career to save, and it is this player's own: a co-op guest's save point is the host's to make.
    public static bool CanSave => GameSession.CareerActive && !Coop.IsGuest;

    // Save now. Returns what the objective was, for the menu to confirm ("" when nothing was booked).
    public static string Save()
    {
        if (!CanSave) return "";

        var pending = WeekendObjectiveHUD.RoadCalled ? null : WeekendAppointment.Pending;
        SavedObjective.Write(RaceWeekend.WeekendId, pending != null ? pending.id : "");
        CareerSave.Stamp();   // flushes PlayerPrefs

        if (pending != null) return pending.title;
        return WeekendLedger.WeekendOver || WeekendObjectiveHUD.RoadCalled ? "HIT THE ROAD" : "";
    }

    // CONTINUE: put the saved marker back if nothing is booked. Guarded so the timetable is only built when
    // the ledger on file is the weekend and championship it would build — the title screen must not reset a
    // weekend by asking about it.
    public static bool RestoreMarker()
    {
        if (!CanSave) return false;

        int weekend = RaceWeekend.WeekendId;
        string savedId = SavedObjective.Id;
        if (string.IsNullOrEmpty(savedId) || SavedObjective.WeekendId != weekend) return false;
        if (WeekendLedger.WeekendId != weekend || WeekendLedger.Series != SeriesCatalog.PlayerSeries) return false;
        if (WeekendLedger.WeekendOver || WeekendObjectiveHUD.RoadCalled) return false;

        var activity = WeekendDirector.Timetable?.ById(savedId);
        bool doable = activity != null && WeekendLedger.CanDo(activity, out _);
        if (!SavedObjective.ShouldRestore(SavedObjective.WeekendId, savedId, weekend,
                                          WeekendAppointment.PendingId, doable))
            return false;

        // A save with a booking in it was made after the day was handed over; don't wait for it again.
        WeekendDirector.MarkBriefed();
        WeekendAppointment.Make(activity);
        return true;
    }
}
