using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // How much of the race weekend a career plays out. Chosen on the title screen's OPTIONS, kept as a setting
    // (CareerReset leaves it alone), and read by WeekendDirector every time it builds the weekend's sheet.
    //
    //   Full         everything: media, fans, sponsors, the officials, somebody else's sessions to watch.
    //   Minimal      your own practice, qualifying and race, plus the team meetings that change the car —
    //                the strategy briefing that sets the weekend's plan and setup, the debrief after a run,
    //                and the first weekend's orientation that teaches the phone.
    //   DrivingOnly  your own practice, qualifying and race, nothing else on the sheet.
    //
    // It trims the sheet, not the paddock: the walk to the car, the people about and the phone are all still
    // there. What goes is every booking the player would otherwise be sent to, be paid for or be fined for
    // missing — and so every objective marker that would point at one.
    public enum CareerMode
    {
        Full = 0,
        Minimal = 1,
        DrivingOnly = 2,
    }

    public static class CareerModes
    {
        public const string PrefKey = "career.mode";

        public static readonly IReadOnlyList<CareerMode> All =
            new[] { CareerMode.Full, CareerMode.Minimal, CareerMode.DrivingOnly };

        public static CareerMode Current
        {
            get
            {
                int v = FramePrefs.GetInt(PrefKey, (int)CareerMode.Full);
                return v >= (int)CareerMode.Full && v <= (int)CareerMode.DrivingOnly ? (CareerMode)v : CareerMode.Full;
            }
            set
            {
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                FramePrefs.Invalidate();
            }
        }

        // Does a weekend played in `mode` keep a booking of this kind on the sheet?
        public static bool Keeps(CareerMode mode, ActivityKind kind)
        {
            if (ActivityKinds.IsOnTrack(kind)) return true;   // your own sessions are the one thing every mode has
            switch (mode)
            {
                case CareerMode.Full:
                    return true;
                case CareerMode.Minimal:
                    return kind == ActivityKind.TeamBriefing || kind == ActivityKind.Debrief ||
                           kind == ActivityKind.Orientation;
                default:
                    return false;
            }
        }

        public static CareerMode Next(CareerMode mode) => All[((int)mode + 1) % All.Count];
        public static CareerMode Previous(CareerMode mode) => All[((int)mode + All.Count - 1) % All.Count];

        public static string Label(CareerMode mode) => mode switch
        {
            CareerMode.Minimal => "MINIMAL",
            CareerMode.DrivingOnly => "DRIVING ONLY",
            _ => "FULL",
        };

        // One line for the options screen, saying what the weekend will have in it.
        public static string Describe(CareerMode mode) => mode switch
        {
            CareerMode.Minimal => "YOUR SESSIONS, PLUS THE TEAM MEETINGS THAT SHAPE THE CAR.",
            CareerMode.DrivingOnly => "PRACTICE, QUALIFYING AND THE RACE. NOTHING ELSE ON THE SHEET.",
            _ => "THE WHOLE WEEKEND: MEDIA, FANS, SPONSORS, OFFICIALS AND THE TEAM.",
        };
    }
}
