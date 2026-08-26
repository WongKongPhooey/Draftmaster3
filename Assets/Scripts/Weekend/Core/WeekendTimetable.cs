using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // The printed schedule for one race weekend at one track, built from the shape a real modern stock-car
    // weekend has: the trucks run and race on Friday, the second-tier cars qualify Saturday morning and race
    // Saturday afternoon, and the top series gets a compressed practice-then-qualifying block on Saturday
    // before racing Sunday afternoon. Everything else in the three days is the obligations that fill a
    // driver's time around their own two hours in the car - the pre-weekend strategy meeting, hauler parade,
    // media availability, signing sessions, sponsor hospitality, and on race day the mandatory drivers
    // meeting two hours before green followed by driver intros on stage.
    //
    // Whichever series the player is entered in, THEIR sessions become drivable (Practice/Qualifying/Race)
    // and the other two championships' sessions become things to go and watch. The mandatory race-day beats
    // are placed relative to the player's own race, so a truck driver's Friday evening carries the drivers
    // meeting and the intros while Cup's Sunday is somebody else's schedule to watch.
    //
    // Pure and deterministic: same (playerSeries, weekendId) in, same timetable out, every rebuild - which
    // matters because the weekend reloads the race scene between sessions and rebuilds this every time.
    public class WeekendTimetable
    {
        public RacingSeries playerSeries;
        public int weekendId;
        public string trackName = "";

        readonly List<WeekendActivity> _activities = new();
        public IReadOnlyList<WeekendActivity> Activities => _activities;

        // ---------------------------------------------------------------- session schedule

        // When each championship's own sessions run. Minutes from midnight on the slot's day.
        public struct SessionTime
        {
            public WeekendSlot slot;
            public int startMinute;
            public int minutes;
            public SessionTime(WeekendSlot s, int start, int mins) { slot = s; startMinute = start; minutes = mins; }
        }

        public static SessionTime PracticeTime(RacingSeries s) => s switch
        {
            RacingSeries.Trucks => new SessionTime(WeekendSlot.FridayAM, 10 * 60, 75),
            RacingSeries.National => new SessionTime(WeekendSlot.FridayPM, 15 * 60, 75),
            _ => new SessionTime(WeekendSlot.SaturdayAM, 10 * 60 + 30, 60),
        };

        public static SessionTime QualifyingTime(RacingSeries s) => s switch
        {
            RacingSeries.Trucks => new SessionTime(WeekendSlot.FridayPM, 13 * 60, 60),
            RacingSeries.National => new SessionTime(WeekendSlot.SaturdayAM, 9 * 60, 60),
            _ => new SessionTime(WeekendSlot.SaturdayPM, 14 * 60, 60),
        };

        public static SessionTime RaceTime(RacingSeries s) => s switch
        {
            RacingSeries.Trucks => new SessionTime(WeekendSlot.FridayPM, 19 * 60, SeriesCatalog.RaceMinutes(RacingSeries.Trucks)),
            RacingSeries.National => new SessionTime(WeekendSlot.SaturdayPM, 16 * 60 + 30, SeriesCatalog.RaceMinutes(RacingSeries.National)),
            _ => new SessionTime(WeekendSlot.SundayPM, 14 * 60, SeriesCatalog.RaceMinutes(RacingSeries.Cup)),
        };

        // ---------------------------------------------------------------- build

        public static WeekendTimetable Build(RacingSeries playerSeries, int weekendId, string trackName = "")
        {
            var t = new WeekendTimetable { playerSeries = playerSeries, weekendId = weekendId, trackName = trackName ?? "" };
            var rng = WeekendRandom.For(weekendId, (int)playerSeries, 7717);
            t.BuildSessions();
            t.BuildTeamMeetings();
            t.BuildObligations(ref rng);
            t.BuildRaceDayCeremony();
            t._activities.Sort(Chronological);
            return t;
        }

        static int Chronological(WeekendActivity a, WeekendActivity b)
        {
            int bySlot = a.slot.CompareTo(b.slot);
            if (bySlot != 0) return bySlot;
            int byTime = a.startMinute.CompareTo(b.startMinute);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.id, b.id);
        }

        // Every championship's practice, qualifying and race. The player's own become drivable; the other
        // two become somewhere to be a spectator.
        void BuildSessions()
        {
            foreach (var s in SeriesCatalog.All)
            {
                bool mine = s == playerSeries;
                string code = SeriesCatalog.ShortCode(s);
                string nick = SeriesCatalog.Nickname(s);

                Add(PracticeTime(s), mine ? ActivityKind.Practice : ActivityKind.SpectatePractice, s,
                    code + " PRACTICE",
                    mine ? "Run the R&D list: long runs, tyre falloff, and a mock qualifying lap at the end."
                         : "Stand on the wall and watch what the " + nick + " field is doing with the track.",
                    mine ? "Your pit box" : "Pit wall");

                Add(QualifyingTime(s), mine ? ActivityKind.Qualifying : ActivityKind.SpectateQualifying, s,
                    code + " QUALIFYING",
                    mine ? "One lap that decides where you start. Miss it and you go to the back."
                         : "Watch the " + nick + " grid get set.",
                    mine ? "Your pit box" : "Timing stand");

                Add(RaceTime(s), mine ? ActivityKind.Race : ActivityKind.SpectateRace, s,
                    code + " RACE",
                    mine ? SeriesCatalog.RaceLaps(s) + " laps. This is what the other two days were for."
                         : "The " + nick + " race. Watching the leaders here is free homework.",
                    mine ? "Your pit box" : "Grandstand");
            }
        }

        // Behind closed doors with the crew chief. The pre-weekend meeting sets the plan; the debrief after
        // your own practice is where the R&D run turns into a setup change.
        void BuildTeamMeetings()
        {
            var briefing = Add(new SessionTime(WeekendSlot.FridayAM, 8 * 60, 60), ActivityKind.TeamBriefing, playerSeries,
                "TEAM STRATEGY BRIEFING",
                "The crew chief walks the weekend: what the sim says, what the tyre does here, who to race and who to leave alone.",
                "Hauler lounge");
            briefing.mandatory = true;
            briefing.skipReason = "The crew chief set the weekend's plan without you in the room.";

            // Straight after your own practice ends, while the run sheet is still warm - unless the session ran
            // so late in the half-day that the debrief would not fit, in which case it rolls over lunch into
            // the afternoon, which is exactly what happens at a real track.
            var p = PracticeTime(playerSeries);
            const int debriefMinutes = 45;
            var debrief = FitAfter(p.slot, p.startMinute + p.minutes + 15, debriefMinutes);
            Add(debrief, ActivityKind.Debrief, playerSeries,
                "PRACTICE DEBRIEF",
                "Engineers, tyre data, and your own read on the car. Pick what to change before qualifying.",
                "Engineering truck");

            // Race morning: the last plan of the weekend, agreed over coffee.
            var race = RaceTime(playerSeries);
            var raceMorning = WeekendSlots.MorningOf(race.slot);
            int planStart = raceMorning == race.slot
                ? System.Math.Max(WeekendSlots.OpensAt(raceMorning), race.startMinute - 300)
                : 11 * 60 + 15;
            if (planStart + 30 <= WeekendSlots.ClosesAt(raceMorning))
            {
                Add(new SessionTime(raceMorning, planStart, 30), ActivityKind.TeamBriefing, playerSeries,
                    "RACE PLAN MEETING",
                    "Fuel windows, pit calls, and what the spotter says about the first ten laps.",
                    "Engineering truck");
            }
        }

        // Media, fans and sponsors. Deliberately booked over the top of sessions in places - the sponsor
        // suite meet-and-greet really is at the same hour as somebody's race, and choosing is the point.
        void BuildObligations(ref WeekendRandom rng)
        {
            // ---- Friday morning: gates open, haulers roll in ----
            Fan(WeekendSlot.FridayAM, 9 * 60, 30, ActivityKind.HaulerParade, "HAULER PARADE",
                "Walk the transporters in past the fence with the crew. Cheap goodwill, and the fans remember it.",
                "Main gate", 0);
            Sponsor(WeekendSlot.FridayAM, 9 * 60 + 45, 60, ActivityKind.PhotoShoot, "SPONSOR PHOTO SHOOT",
                "Stills for the season's marketing: firesuit on, same three poses, an hour of standing still.",
                "Sponsor hospitality", 1200, "The photographer flew in for that hour and shot an empty backdrop.");

            // ---- Friday afternoon ----
            Sponsor(WeekendSlot.FridayPM, 13 * 60 + 15, 45, ActivityKind.SponsorDuty, "PIT-STOP CHALLENGE",
                "Sponsor guests against the clock on a wheel gun, and you are the one keeping score.",
                "Display pit stall", 900, "The guest list turned up to a pit stall with nobody in it.");
            Media(WeekendSlot.FridayPM, 14 * 60 + 15, 45, ActivityKind.PressConference, "MEDIA AVAILABILITY",
                "The written press get their twenty minutes. Whatever you say here is the quote of the weekend.",
                "Media centre");
            Fan(WeekendSlot.FridayPM, 16 * 60 + 30, 60, ActivityKind.Autographs, "SIGNING SESSION",
                "A queue down the midway with hero cards and sharpies. Sign as many as the hour allows.",
                "Souvenir midway", 750);
            Sponsor(WeekendSlot.FridayPM, 17 * 60 + 45, 45, ActivityKind.SponsorDuty, "HOSPITALITY Q&A",
                "Forty guests, a microphone, and the brand's talking points to get through.",
                "Sponsor suite", 1500, "Forty guests ate dinner without the driver they were promised.");

            // ---- Saturday morning ----
            Team(WeekendSlot.SaturdayAM, 8 * 60 + 15, 30, ActivityKind.Debrief, "OVERNIGHT DEBRIEF",
                "What the engineers found in Friday's data while you slept.", "Engineering truck");
            Sponsor(WeekendSlot.SaturdayAM, 9 * 60 + 15, 45, ActivityKind.PhotoShoot, "DEALER GROUP PHOTOS",
                "Two hundred handshake photos with the regional dealer network. Smile on every one.",
                "Sponsor hospitality", 1100, "The dealer network got a cardboard cut-out.");
            Fan(WeekendSlot.SaturdayAM, 11 * 60, 60, ActivityKind.Autographs, "FAN ZONE STAGE",
                "On stage in front of the fan zone, taking questions and signing whatever gets handed up.",
                "Fan zone stage", 900);

            // ---- Saturday afternoon ----
            Media(WeekendSlot.SaturdayPM, 13 * 60, 45, ActivityKind.PressConference, "PRESS CONFERENCE",
                "Top table, three microphones, and the story they already decided to write.", "Media centre");
            Fan(WeekendSlot.SaturdayPM, 15 * 60 + 30, 45, ActivityKind.Autographs, "GARAGE WALK",
                "Fans with garage passes, walking the stalls. Less structured, more of them.", "Garage stalls", 600);
            Sponsor(WeekendSlot.SaturdayPM, 17 * 60, 60, ActivityKind.SponsorDuty, "SUITE MEET & GREET",
                "The people who pay for the hood want an hour of your evening, and the race is on the TV behind them.",
                "Sponsor suite", 2000, "The suite watched the race without the driver whose name is on the door.");

            // ---- Sunday morning: race-morning appearances ----
            Sponsor(WeekendSlot.SundayAM, 9 * 60, 45, ActivityKind.SponsorDuty, "RACE MORNING APPEARANCE",
                "Coffee, a stage, and the brand's guests before the crowd fills the place up.",
                "Sponsor hospitality", 1400, "The brand's guests got an empty stage on race morning.");
            Fan(WeekendSlot.SundayAM, 10 * 60, 60, ActivityKind.Autographs, "PIT ROAD WALK",
                "Pit passes are the hottest ticket of the weekend and they all want a signature.", "Pit road", 1000);
            Media(WeekendSlot.SundayAM, 11 * 60 + 30, 20, ActivityKind.MediaHit, "BROADCAST HIT",
                "Ninety seconds live on the pre-race show. One question, no second take.", "TV compound");

            // ---- Sunday afternoon ----
            Sponsor(WeekendSlot.SundayPM, 12 * 60, 60, ActivityKind.SponsorDuty, "HOSPITALITY BRUNCH",
                "The last obligation before the biggest race of the weekend starts without you in it.",
                "Sponsor suite", 1600, "Brunch went ahead with an empty chair at the head table.");
            Media(WeekendSlot.SundayPM, 13 * 60 + 30, 20, ActivityKind.MediaHit, "GRID WALK INTERVIEW",
                "Handed a microphone on the grid with the cars firing up behind you.", "Starting grid");

            AddFeature(ref rng);
        }

        // One rotating headline obligation per weekend, so the sheet is not the same six columns every round.
        void AddFeature(ref WeekendRandom rng)
        {
            switch (rng.Range(0, 5))
            {
                case 0:
                    Media(WeekendSlot.FridayAM, 11 * 60, 45, ActivityKind.PressConference, "SEASON MEDIA DAY",
                        "Every outlet at the track in one room, and a season's worth of narrative to set.", "Media centre");
                    break;
                case 1:
                    Fan(WeekendSlot.SaturdayAM, 8 * 60 + 30, 45, ActivityKind.HaulerParade, "CHARITY FUN RUN",
                        "A lap of the infield with two hundred fans in branded t-shirts before it gets hot.", "Infield road", 0);
                    break;
                case 2:
                    Sponsor(WeekendSlot.SaturdayPM, 12 * 60 + 15, 40, ActivityKind.PhotoShoot, "NEW LIVERY REVEAL",
                        "Pull the sheet off the car for the cameras and say the sponsor's name four times.",
                        "Display stage", 2500, "The car got unveiled by a marketing manager instead.");
                    break;
                case 3:
                    Fan(WeekendSlot.SundayAM, 8 * 60 + 15, 40, ActivityKind.Autographs, "JUNIOR FAN CLUB",
                        "Forty kids, forty die-casts, and forty parents filming it.", "Fan zone stage", 500);
                    break;
                default:
                    Media(WeekendSlot.FridayPM, 12 * 60 + 15, 30, ActivityKind.MediaHit, "PODCAST TAPING",
                        "An hour of questions cut down to twenty minutes people will actually quote.", "Media centre");
                    break;
            }
        }

        // The two things the sport requires of whoever is racing that day: the mandatory drivers meeting two
        // hours before the green flag, and intros on stage half an hour before it.
        void BuildRaceDayCeremony()
        {
            var race = RaceTime(playerSeries);

            int meetingStart = race.startMinute - 120;
            var meetingSlot = meetingStart < 12 * 60 ? WeekendSlots.MorningOf(race.slot) : race.slot;
            if (meetingStart >= WeekendSlots.OpensAt(meetingSlot))
            {
                var meeting = Add(new SessionTime(meetingSlot, meetingStart, 30), ActivityKind.DriversMeeting, playerSeries,
                    "DRIVERS MEETING",
                    "Mandatory. Officials read the rules for this track, then the room empties.",
                    "Drivers meeting room");
                meeting.mandatory = true;
                meeting.skipMoneyPenalty = 5000;
                meeting.skipReason = "Missing the drivers meeting is a fine and a start-at-the-rear penalty.";
            }

            int introStart = race.startMinute - 30;
            var introSlot = introStart < 12 * 60 ? WeekendSlots.MorningOf(race.slot) : race.slot;
            var intros = Add(new SessionTime(introSlot, introStart, 25), ActivityKind.DriverIntros, playerSeries,
                "DRIVER INTRODUCTIONS",
                "Your name over the PA and a walk down the stage in front of the grandstand.",
                "Front stretch stage");
            intros.mandatory = true;
            intros.skipAppealPenalty = 4f;
            intros.skipReason = "The crowd heard your name and nobody walked out.";
        }

        // ---------------------------------------------------------------- helpers

        // Place something of `minutes` length at `preferredStart` in `slot`. If it would run past the close
        // of that half-day, push it to the opening of the next one; if there is no next one, back it up
        // against the close.
        static SessionTime FitAfter(WeekendSlot slot, int preferredStart, int minutes)
        {
            int start = Mathf.Max(preferredStart, WeekendSlots.OpensAt(slot));
            if (start + minutes <= WeekendSlots.ClosesAt(slot)) return new SessionTime(slot, start, minutes);

            int next = (int)slot + 1;
            if (next < WeekendSlots.Count)
            {
                var nextSlot = (WeekendSlot)next;
                return new SessionTime(nextSlot, WeekendSlots.OpensAt(nextSlot), minutes);
            }
            return new SessionTime(slot, WeekendSlots.ClosesAt(slot) - minutes, minutes);
        }

        WeekendActivity Add(SessionTime when, ActivityKind kind, RacingSeries series,
                            string title, string subtitle, string location)
        {
            var a = new WeekendActivity
            {
                slot = when.slot,
                startMinute = when.startMinute,
                minutes = when.minutes,
                kind = kind,
                series = series,
                title = title,
                subtitle = subtitle,
                // Where it happens is decided by the kind, not by whoever wrote the booking: the player has
                // to walk to it, and a sheet that says "Media centre" while the conversation is waiting at
                // the pit box is a sheet that lies. On-track sessions keep the passed-in label — those are
                // the ones that are not walked to.
                location = WeekendVenues.For(kind) == WeekendVenue.None
                    ? location
                    : WeekendVenues.ShortLabel(WeekendVenues.For(kind)),
            };
            a.id = MakeId(a);
            _activities.Add(a);
            return a;
        }

        WeekendActivity Media(WeekendSlot slot, int start, int mins, ActivityKind kind,
                              string title, string subtitle, string location)
        {
            var a = Add(new SessionTime(slot, start, mins), kind, playerSeries, title, subtitle, location);
            a.skipReason = "The press wrote the story without your side of it.";
            return a;
        }

        WeekendActivity Fan(WeekendSlot slot, int start, int mins, ActivityKind kind,
                            string title, string subtitle, string location, int fee)
        {
            var a = Add(new SessionTime(slot, start, mins), kind, playerSeries, title, subtitle, location);
            a.appearanceFee = fee;
            a.skipAppealPenalty = kind == ActivityKind.Autographs ? 2f : 1f;
            a.skipReason = "The queue waited and then went home.";
            return a;
        }

        // Contracted appearances. The money was spent on your behalf before you got to the track, so not
        // turning up is not free - it comes back out of the deal.
        WeekendActivity Sponsor(WeekendSlot slot, int start, int mins, ActivityKind kind,
                                string title, string subtitle, string location, int fee, string skipReason)
        {
            var a = Add(new SessionTime(slot, start, mins), kind, playerSeries, title, subtitle, location);
            a.appearanceFee = fee;
            a.mandatory = true;
            a.skipMoneyPenalty = fee / 2;
            a.skipReason = skipReason;
            return a;
        }

        WeekendActivity Team(WeekendSlot slot, int start, int mins, ActivityKind kind,
                             string title, string subtitle, string location)
            => Add(new SessionTime(slot, start, mins), kind, playerSeries, title, subtitle, location);

        // Stable across rebuilds: the same weekend always produces the same ids, so the ledger's record of
        // what has been done survives the scene reloads the weekend does between sessions.
        static string MakeId(WeekendActivity a) =>
            ((int)a.slot) + "." + a.startMinute + "." + ((int)a.kind);

        // ---------------------------------------------------------------- queries

        public List<WeekendActivity> InSlot(WeekendSlot slot)
        {
            var list = new List<WeekendActivity>();
            foreach (var a in _activities) if (a.slot == slot) list.Add(a);
            return list;
        }

        public WeekendActivity ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var a in _activities) if (a.id == id) return a;
            return null;
        }

        // The player's own drivable session of a given kind (Practice / Qualifying / Race).
        public WeekendActivity PlayerSession(ActivityKind kind)
        {
            foreach (var a in _activities) if (a.kind == kind && a.IsOnTrack) return a;
            return null;
        }

        // Everything on the sheet the given booking would collide with.
        public List<WeekendActivity> ClashesFor(WeekendActivity a)
        {
            var list = new List<WeekendActivity>();
            if (a == null) return list;
            foreach (var other in _activities)
                if (!ReferenceEquals(other, a) && a.ClashesWith(other)) list.Add(other);
            return list;
        }
    }
}
