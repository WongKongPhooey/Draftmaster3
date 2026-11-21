using System.Collections.Generic;
using System.Globalization;

namespace Draftmaster.Tracks
{
    // The rules behind the SINGLE RACE screens (Track › Series › Car select), kept apart from the uGUI so they
    // can be tested without the engine (Tools/HeadlessTests). Built to the design handoff in
    // Designs/draftmaster3_race_flow_unity_handoff (README.md + data.json): what a track tile is tagged, where
    // a venue is, what a series tile says about its field, how the cursor walks a grid of tiles, and the whole
    // number the canvas is scaled by.
    public static class RaceFlow
    {
        // ------------------------------------------------------------------ tracks

        // The header's filter chips. ROAD includes the street circuits.
        public enum TrackFilter { All, Road, Oval, Dirt }

        // The tag in the top-left of a track tile.
        public enum TrackTag { Road, Street, Oval, Dirt }

        public static readonly TrackFilter[] Filters = { TrackFilter.All, TrackFilter.Road, TrackFilter.Oval, TrackFilter.Dirt };

        // The venues built to race - package authored, AI tuned and play-tested. Every other track shows greyed out
        // on the SINGLE RACE grid and can't be picked (Josh, 2026-11-21). Martinsville is the next to join.
        static readonly HashSet<string> AuthoredTracks = new HashSet<string> { "WatkinsGlen", "Daytona", "FortWorth" };
        public static bool IsAuthored(string trackId) => trackId != null && AuthoredTracks.Contains(trackId);
        public const string NotBuiltNote = "COMING SOON";

        // Temporary circuits laid out on public roads. Everything else that turns both ways is a road course.
        static readonly HashSet<string> StreetCircuits = new HashSet<string> { "Chicago", "SanDiego" };

        public static TrackTag Tag(string trackId, bool roadCourse, bool dirt)
        {
            if (dirt) return TrackTag.Dirt;
            if (!roadCourse) return TrackTag.Oval;
            return trackId != null && StreetCircuits.Contains(trackId) ? TrackTag.Street : TrackTag.Road;
        }

        public static string TagLabel(TrackTag tag) => tag switch
        {
            TrackTag.Street => "STREET",
            TrackTag.Oval => "OVAL",
            TrackTag.Dirt => "DIRT",
            _ => "ROAD",
        };

        public static string FilterLabel(TrackFilter filter) => filter switch
        {
            TrackFilter.Road => "ROAD",
            TrackFilter.Oval => "OVAL",
            TrackFilter.Dirt => "DIRT",
            _ => "ALL",
        };

        public static bool Passes(TrackFilter filter, TrackTag tag) => filter switch
        {
            TrackFilter.Road => tag == TrackTag.Road || tag == TrackTag.Street,
            TrackFilter.Oval => tag == TrackTag.Oval,
            TrackFilter.Dirt => tag == TrackTag.Dirt,
            _ => true,
        };

        // Q / E (or the shoulders) step through the chips, wrapping.
        public static TrackFilter StepFilter(TrackFilter filter, int by)
        {
            int n = Filters.Length;
            int i = (((int)filter + by) % n + n) % n;
            return Filters[i];
        }

        // "2.45 MI" — the published lap length, as many decimals as it was published with (up to three).
        public static string Miles(float miles) =>
            miles.ToString("0.0##", CultureInfo.InvariantCulture) + " MI";

        // Where each calendar venue is, the way a broadcast captions it: town, then state.
        static readonly Dictionary<string, string> Places = new Dictionary<string, string>
        {
            { "Daytona", "Daytona Beach, FL" },
            { "Talladega", "Talladega, AL" },
            { "Indianapolis", "Speedway, IN" },
            { "Charlotte", "Concord, NC" },
            { "Atlanta", "Hampton, GA" },
            { "LasVegas", "Las Vegas, NV" },
            { "Kansas", "Kansas City, KS" },
            { "Miami", "Homestead, FL" },
            { "FortWorth", "Fort Worth, TX" },
            { "Michigan", "Brooklyn, MI" },
            { "Darlington", "Darlington, SC" },
            { "Dover", "Dover, DE" },
            { "Nashville", "Lebanon, TN" },
            { "Gateway", "Madison, IL" },
            { "LongPond", "Long Pond, PA" },
            { "Bristol", "Bristol, TN" },
            { "Martinsville", "Ridgeway, VA" },
            { "Richmond", "Richmond, VA" },
            { "Phoenix", "Avondale, AZ" },
            { "NewHampshire", "Loudon, NH" },
            { "Iowa", "Newton, IA" },
            { "NorthWilkesboro", "North Wilkesboro, NC" },
            { "Rockingham", "Rockingham, NC" },
            { "IRP", "Brownsburg, IN" },
            { "Milwaukee", "West Allis, WI" },
            { "BowmanGray", "Winston-Salem, NC" },
            { "WatkinsGlen", "Watkins Glen, NY" },
            { "Sonoma", "Sonoma, CA" },
            { "COTA", "Austin, TX" },
            { "CharlotteRoval", "Concord, NC" },
            { "MidOhio", "Lexington, OH" },
            { "Portland", "Portland, OR" },
            { "RoadAmerica", "Elkhart Lake, WI" },
            { "LimeRock", "Lakeville, CT" },
            { "IndyRoad", "Speedway, IN" },
            { "Chicago", "Chicago, IL" },
            { "SanDiego", "San Diego, CA" },
            { "MexicoCity", "Mexico City, MX" },
        };

        public static bool HasPlace(string trackId) => trackId != null && Places.ContainsKey(trackId);

        // The venue's town, else the catalogue's country, else nothing.
        public static string Place(string trackId, string fallback = null)
        {
            if (trackId != null && Places.TryGetValue(trackId, out var place)) return place;
            return string.IsNullOrWhiteSpace(fallback) ? "" : fallback.Trim();
        }

        // ------------------------------------------------------------------ series

        // Colour of each championship's stripes and bar (data.json), by short code.
        static readonly Dictionary<string, uint> SeriesColours = new Dictionary<string, uint>
        {
            { "CUP", 0xc4452f }, { "CHL", 0x5a9ad6 }, { "TRK", 0xe08a3c }, { "NSS", 0x6fa85a },
            { "IOW", 0xa06fd0 }, { "OWL", 0x5cc8b0 }, { "ODS", 0xb07a4a },
        };

        // Discipline as Draftmaster.Data.Discipline numbers it: StockCar 0, OpenWheel 1, Dirt 2, Truck 3,
        // ShortTrack 4, SportsCar 5. A code the design never named takes its discipline's colour.
        public static uint SeriesColour(string shortCode, int discipline)
        {
            if (shortCode != null && SeriesColours.TryGetValue(shortCode.Trim().ToUpperInvariant(), out var c)) return c;
            return discipline switch
            {
                1 => 0xa06fd0,
                2 => 0xb07a4a,
                3 => 0xe08a3c,
                4 => 0x6fa85a,
                5 => 0x5a9ad6,
                _ => 0xc4452f,
            };
        }

        public static string SeriesKind(int discipline) => discipline switch
        {
            1 => "OPEN WHEEL",
            2 => "DIRT",
            3 => "TRUCK",
            5 => "SPORTS CAR",
            _ => "STOCK",
        };

        public static string FieldChip(int entered) => entered > 0 ? entered + " CARS" : "NO FIELD";
        public static string FieldLine(int entered) => entered > 0 ? entered + " entered" : "No field entered";

        // ------------------------------------------------------------------ cars

        // A car tile names the driver by surname only ("van Gisbergen"). Falls back to the whole label.
        public static string Surname(string first, string last, string label)
        {
            if (!string.IsNullOrWhiteSpace(last)) return last.Trim();
            if (!string.IsNullOrWhiteSpace(label))
            {
                string l = label.Trim();
                // "#62 Anthony Alfredo" -> drop the number.
                if (l.StartsWith("#"))
                {
                    int space = l.IndexOf(' ');
                    l = space > 0 ? l.Substring(space + 1).Trim() : l;
                }
                int at = l.IndexOf(' ');
                return at > 0 ? l.Substring(at + 1).Trim() : l;
            }
            return string.IsNullOrWhiteSpace(first) ? "" : first.Trim();
        }

        // ------------------------------------------------------------------ the grid

        // "9 / 17" under the confirm button.
        public static string Position(int index, int count) =>
            count <= 0 ? "0 / 0" : $"{System.Math.Max(0, System.Math.Min(index, count - 1)) + 1} / {count}";

        // How many tiles fit across `width`, never fewer than one or more than `max` (the design's count).
        public static int Columns(float width, float tile, float gap, int max)
        {
            if (tile <= 0f) return 1;
            int fit = (int)System.Math.Floor((width + gap) / (tile + gap));
            if (max > 0 && fit > max) fit = max;
            return fit < 1 ? 1 : fit;
        }

        // The cursor's next tile. Left/right step along the list (crossing row ends); up/down move a row.
        // Down from a row with nothing straight below lands on the last tile, if it is in a lower row;
        // a move that would leave the grid stays put.
        public static int Move(int index, int count, int columns, int dx, int dy)
        {
            if (count <= 0) return 0;
            if (columns < 1) columns = 1;
            index = System.Math.Max(0, System.Math.Min(index, count - 1));

            if (dx != 0)
            {
                int next = index + System.Math.Sign(dx);
                return next < 0 || next >= count ? index : next;
            }

            if (dy > 0)
            {
                int next = index + columns;
                if (next < count) return next;
                int lastRow = (count - 1) / columns;
                return index / columns < lastRow ? count - 1 : index;
            }

            if (dy < 0)
            {
                int next = index - columns;
                return next >= 0 ? next : index;
            }

            return index;
        }

        // ------------------------------------------------------------------ scale

        // Constant Pixel Size with a whole-number factor only (the handoff's rule): the largest factor that
        // still leaves the screen's design footprint — PC 640x360, a phone 800x360 (the 844x390 design less the
        // safe-area slack) — on the canvas.
        public static int ScaleFactor(int screenWidth, int screenHeight, bool handheld)
        {
            int minW = handheld ? 800 : 640, minH = 360;
            int s = System.Math.Min(screenWidth / minW, screenHeight / minH);
            return s < 1 ? 1 : s;
        }
    }
}
