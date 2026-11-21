using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // One of the player's builds (the Garage's Builds tab, Josh 2026-11-21): a driver, a car and a crew chief card
    // from the collection and a car number, kept for a kind of track — a Short Tracks build, a Road Courses build,
    // a Dirt build. Builds are alternatives, so one card can sit in several of them. Slots hold CardInstance uids;
    // an empty string is an empty slot.
    [Serializable]
    public class DraftBuild
    {
        public TrackKind track;
        public string driver = "";
        public string car = "";
        public string crewChief = "";
        // 0-99, or BuildRules.DriversNumber (-1): run the driver card's own number. The Garage's Numbers tab is
        // still to come, so any number can be picked.
        public int number = BuildRules.DriversNumber;

        public bool Uses(string uid) =>
            !string.IsNullOrEmpty(uid) && (uid == driver || uid == car || uid == crewChief);

        // Drops a uid from every slot (a card leaving the collection in a trade).
        public void Remove(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            if (driver == uid) driver = "";
            if (car == uid) car = "";
            if (crewChief == uid) crewChief = "";
        }

        public string Slot(CardType type) => type switch
        {
            CardType.Driver => driver,
            CardType.Car => car,
            _ => crewChief,
        };

        public void SetSlot(CardType type, string uid)
        {
            uid ??= "";
            switch (type)
            {
                case CardType.Driver: driver = uid; break;
                case CardType.Car: car = uid; break;
                default: crewChief = uid; break;
            }
        }
    }

    // The builds' rules. Engine-free (Core).
    public static class BuildRules
    {
        public const int MaxBuilds = 5;
        public const int DriversNumber = -1;
        public const int MaxNumber = 99;

        public static readonly CardType[] SlotTypes = { CardType.Driver, CardType.Car, CardType.CrewChief };

        // What a build for this kind of track is called.
        public static string TrackName(TrackKind kind) => kind switch
        {
            TrackKind.ShortTrack => "Short Tracks",
            TrackKind.Speedway => "Speedways",
            TrackKind.Superspeedway => "Superspeedways",
            TrackKind.RoadCourse => "Road Courses",
            _ => "Dirt",
        };

        // The build's name: its track kind, numbered when an earlier build has the same one ("Road Courses 2").
        public static string Name(IList<DraftBuild> builds, int index)
        {
            if (builds == null || index < 0 || index >= builds.Count || builds[index] == null) return "";
            var kind = builds[index].track;
            int same = 1;
            for (int i = 0; i < index; i++) if (builds[i] != null && builds[i].track == kind) same++;
            return same > 1 ? $"{TrackName(kind)} {same}" : TrackName(kind);
        }

        // Up to MaxBuilds builds. A missing one is added for the first track kind no build covers (Short Tracks,
        // Speedways, Superspeedways, Road Courses, Dirt — one each on a new profile) and filled from the
        // collection (AutoFill). Existing builds are never refilled, so a slot the player emptied stays empty.
        public static void Ensure(MasterDraftProfile p)
        {
            if (p == null) return;
            p.builds ??= new List<DraftBuild>();
            p.builds.RemoveAll(b => b == null);
            if (p.builds.Count > MaxBuilds) p.builds.RemoveRange(MaxBuilds, p.builds.Count - MaxBuilds);
            while (p.builds.Count < MaxBuilds)
            {
                var build = new DraftBuild { track = NextTrack(p.builds) };
                AutoFill(build, p);
                p.builds.Add(build);
            }
        }

        static TrackKind NextTrack(List<DraftBuild> builds)
        {
            foreach (var kind in CardText.AllTrackKinds)
                if (!builds.Exists(b => b.track == kind)) return kind;
            return CardText.AllTrackKinds[builds.Count % CardText.AllTrackKinds.Length];
        }

        // Back inside the rules after a load: slots hold owned cards of the right type, the number is in range, the
        // track kind is one there is.
        public static void Sanitise(MasterDraftProfile p)
        {
            if (p == null) return;
            p.builds ??= new List<DraftBuild>();
            p.builds.RemoveAll(b => b == null);
            if (p.builds.Count > MaxBuilds) p.builds.RemoveRange(MaxBuilds, p.builds.Count - MaxBuilds);
            foreach (var b in p.builds)
            {
                if (!Enum.IsDefined(typeof(TrackKind), b.track)) b.track = TrackKind.ShortTrack;
                foreach (var type in SlotTypes)
                    if (!Holds(p, b.Slot(type), type)) b.SetSlot(type, "");
                if (b.number < DriversNumber || b.number > MaxNumber) b.number = DriversNumber;
            }
        }

        static bool Holds(MasterDraftProfile p, string uid, CardType type)
        {
            if (string.IsNullOrEmpty(uid)) return false;
            var card = p.Find(uid);
            return card?.def != null && card.def.type == type;
        }

        public static bool IsEmpty(DraftBuild b) =>
            b == null || (string.IsNullOrEmpty(b.driver) && string.IsNullOrEmpty(b.car) && string.IsNullOrEmpty(b.crewChief));

        // The build's cards (a slot is null while it is empty or its card has left the collection).
        public static CardBuild Resolve(DraftBuild b, MasterDraftProfile p)
        {
            var cards = new CardBuild();
            if (b == null || p == null) return cards;
            cards.driver = Card(p, b.driver, CardType.Driver);
            cards.car = Card(p, b.car, CardType.Car);
            cards.crewChief = Card(p, b.crewChief, CardType.CrewChief);
            return cards;
        }

        static CardInstance Card(MasterDraftProfile p, string uid, CardType type)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            var card = p.Find(uid);
            return card?.def != null && card.def.type == type ? card : null;
        }

        // The number the build runs: its own pick, else its driver card's number; -1 with neither.
        public static int Number(DraftBuild b, MasterDraftProfile p)
        {
            if (b == null) return -1;
            if (b.number >= 0) return b.number;
            var driver = p == null ? null : Card(p, b.driver, CardType.Driver);
            return driver != null && driver.def.carNumber >= 0 && driver.def.carNumber <= MaxNumber ? driver.def.carNumber : -1;
        }

        // One up or down from the number the build runs now, wrapping 0-99. Picking a number stops it following
        // the driver card.
        public static int StepNumber(DraftBuild b, MasterDraftProfile p, int by)
        {
            if (b == null) return -1;
            int now = Number(b, p);
            if (now < 0) now = by > 0 ? -1 : 0;   // nothing yet: + starts at 0, - at 99
            int n = MaxNumber + 1;
            b.number = ((now + by) % n + n) % n;
            return b.number;
        }

        // The cards that can go in a slot, best first. Drivers by their aptitude for the build's track kind, then
        // OVR; cars and crew chiefs by OVR. Ties keep the collection's order (uid).
        public static List<CardInstance> Choices(DraftBuild b, MasterDraftProfile p, CardType type)
        {
            var list = new List<CardInstance>();
            if (p?.cards == null) return list;
            foreach (var c in p.cards) if (c?.def != null && c.def.type == type) list.Add(c);
            var kind = b?.track ?? TrackKind.ShortTrack;
            list.Sort((x, y) =>
            {
                if (type == CardType.Driver)
                {
                    int a = y.def.Aptitude(kind).CompareTo(x.def.Aptitude(kind));
                    if (a != 0) return a;
                }
                int o = y.def.overall.CompareTo(x.def.overall);
                return o != 0 ? o : string.CompareOrdinal(x.uid, y.uid);
            });
            return list;
        }

        // Put an owned card of the slot's type in the slot. False (and no change) for anything else.
        public static bool Assign(DraftBuild b, MasterDraftProfile p, CardType type, string uid)
        {
            if (b == null || p == null || !Holds(p, uid, type)) return false;
            b.SetSlot(type, uid);
            return true;
        }

        // The best of the collection for the build's track kind: the driver with the best aptitude for it (then
        // OVR), the best car and crew chief by OVR. The number goes back to following the driver card.
        public static void AutoFill(DraftBuild b, MasterDraftProfile p)
        {
            if (b == null || p == null) return;
            foreach (var type in SlotTypes)
            {
                var choices = Choices(b, p, type);
                b.SetSlot(type, choices.Count > 0 ? choices[0].uid : "");
            }
            b.number = DriversNumber;
        }

        // Fill every build that has no cards at all (a new player's builds, once the Starter Pack lands).
        public static void FillEmpty(MasterDraftProfile p)
        {
            if (p?.builds == null) return;
            foreach (var b in p.builds) if (IsEmpty(b)) AutoFill(b, p);
        }

        public static void Clear(DraftBuild b)
        {
            if (b == null) return;
            b.driver = b.car = b.crewChief = "";
            b.number = DriversNumber;
        }

        // The next track kind round the five (the build's Track button).
        public static TrackKind NextTrack(TrackKind kind)
        {
            var all = CardText.AllTrackKinds;
            int i = Array.IndexOf(all, kind);
            return all[(i + 1 + all.Length) % all.Length];
        }
    }
}
