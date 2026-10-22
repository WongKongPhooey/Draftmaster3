using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Draftmaster.Weekend
{
    // Real NASCAR careers to the end of the 2025 season, one row per driver PER SERIES: a Cup regular who
    // also runs Xfinity races has a Cup row and a National row, and the laptop's driver sheet shows the one for
    // the championship being browsed. Starts, wins, top 10s, poles and championship years.
    //
    // Covers every real driver in the Cup roster (CupRoster2026) and the National / Truck entry lists
    // (SeriesEntryLists2025). A series a driver never started a race in has no row.
    //
    // Source: each driver's Wikipedia infobox (Total_*_Races, *_Wins, *_Top_Tens, *_Poles, Best_*_Pos for
    // Cup, Busch = Xfinity, and Truck) as it stood on 31 December 2025, so the 2026 season is not in it.
    // Generated. Seeds the DriverSeriesStats table, which is where results get added from here on.
    public static class SeriesCareers2025
    {
        public readonly struct Record
        {
            public readonly int Starts, Wins, Top10s, Poles;
            public readonly int[] TitleYears;
            public int Titles => TitleYears.Length;

            public Record(int starts, int wins, int top10s, int poles, int[] titleYears)
            {
                Starts = starts; Wins = wins; Top10s = top10s; Poles = poles;
                TitleYears = titleYears ?? Array.Empty<int>();
            }
        }

        static readonly Dictionary<(string, RacingSeries), Record> Rows = new();
        static readonly List<(string name, RacingSeries series)> Order = new();

        static void R(string name, RacingSeries series, int starts, int wins, int top10s, int poles, params int[] titles)
        {
            Rows[(Key(name), series)] = new Record(starts, wins, top10s, poles, titles);
            Order.Add((name, series));
        }

        static SeriesCareers2025()
        {
            //  driver                   series                   starts wins top10 poles titles
            R("Kyle Larson",             RacingSeries.Cup,        402,  32, 204, 22, 2021, 2025);
            R("Kyle Larson",             RacingSeries.National,   120,  17,  86,  8);
            R("Kyle Larson",             RacingSeries.Trucks,      17,   4,  13,  2);
            R("Chase Elliott",           RacingSeries.Cup,        356,  21, 190, 12, 2020);
            R("Chase Elliott",           RacingSeries.National,    87,   6,  71,  3, 2014);
            R("Chase Elliott",           RacingSeries.Trucks,      18,   3,  15,  3);
            R("William Byron",           RacingSeries.Cup,        288,  16, 120, 16);
            R("William Byron",           RacingSeries.National,    43,   5,  27,  3, 2017);
            R("William Byron",           RacingSeries.Trucks,      31,   8,  20,  3);
            R("Alex Bowman",             RacingSeries.Cup,        357,   8, 112,  7);
            R("Alex Bowman",             RacingSeries.National,    57,   1,  19,  4);
            R("Alex Bowman",             RacingSeries.Trucks,       4,   0,   1,  0);
            R("Denny Hamlin",            RacingSeries.Cup,        718,  60, 376, 48);
            R("Denny Hamlin",            RacingSeries.National,   164,  18, 101, 21);
            R("Denny Hamlin",            RacingSeries.Trucks,      16,   2,  10,  1);
            R("Chase Briscoe",           RacingSeries.Cup,        178,   5,  49,  9);
            R("Chase Briscoe",           RacingSeries.National,    87,  11,  54,  2);
            R("Chase Briscoe",           RacingSeries.Trucks,      29,   2,  18,  5);
            R("Christopher Bell",        RacingSeries.Cup,        216,  13, 107, 14);
            R("Christopher Bell",        RacingSeries.National,    81,  19,  51, 14);
            R("Christopher Bell",        RacingSeries.Trucks,      58,   7,  44,  6, 2017);
            R("Ty Gibbs",                RacingSeries.Cup,        114,   0,  33,  2);
            R("Ty Gibbs",                RacingSeries.National,    66,  12,  41,  9, 2022);
            R("Austin Cindric",          RacingSeries.Cup,        142,   3,  27,  1);
            R("Austin Cindric",          RacingSeries.National,   133,  13,  89,  8, 2020);
            R("Austin Cindric",          RacingSeries.Trucks,      29,   1,  16,  1);
            R("Ryan Blaney",             RacingSeries.Cup,        378,  17, 168, 12, 2023);
            R("Ryan Blaney",             RacingSeries.National,    70,   7,  57,  3);
            R("Ryan Blaney",             RacingSeries.Trucks,      58,   4,  40,  5);
            R("Joey Logano",             RacingSeries.Cup,        609,  37, 300, 33, 2018, 2022, 2024);
            R("Joey Logano",             RacingSeries.National,   179,  30, 145, 36);
            R("Joey Logano",             RacingSeries.Trucks,       9,   2,   6,  2);
            R("Ross Chastain",           RacingSeries.Cup,        259,   6,  70,  2);
            R("Ross Chastain",           RacingSeries.National,   213,   2,  57,  1);
            R("Ross Chastain",           RacingSeries.Trucks,     117,   5,  53,  4);
            R("Shane van Gisbergen",     RacingSeries.Cup,         47,   6,  11,  4);
            R("Shane van Gisbergen",     RacingSeries.National,    36,   4,  12,  5);
            R("Shane van Gisbergen",     RacingSeries.Trucks,       1,   0,   0,  0);
            R("Connor Zilisch",          RacingSeries.Cup,          3,   0,   0,  0);
            R("Connor Zilisch",          RacingSeries.National,    34,  11,  25,  9);
            R("Connor Zilisch",          RacingSeries.Trucks,       8,   0,   3,  2);
            R("Justin Haley",            RacingSeries.Cup,        180,   1,  18,  0);
            R("Justin Haley",            RacingSeries.National,   108,   4,  69,  0);
            R("Justin Haley",            RacingSeries.Trucks,      53,   3,  33,  1);
            R("Bubba Wallace",           RacingSeries.Cup,        291,   3,  60,  3);
            R("Bubba Wallace",           RacingSeries.National,    88,   0,  36,  2);
            R("Bubba Wallace",           RacingSeries.Trucks,      51,   6,  30,  3);
            R("Riley Herbst",            RacingSeries.Cup,         40,   0,   2,  0);
            R("Riley Herbst",            RacingSeries.National,   179,   3,  88,  2);
            R("Riley Herbst",            RacingSeries.Trucks,      11,   0,   4,  1);
            R("Tyler Reddick",           RacingSeries.Cup,        218,   8,  92, 11);
            R("Tyler Reddick",           RacingSeries.National,    99,  10,  58,  7, 2018, 2019);
            R("Tyler Reddick",           RacingSeries.Trucks,      63,   3,  40,  3);
            R("Corey Heim",              RacingSeries.Cup,          7,   0,   1,  0);
            R("Corey Heim",              RacingSeries.National,    20,   0,   6,  0);
            R("Corey Heim",              RacingSeries.Trucks,      89,  23,  68, 14, 2025);
            R("Austin Dillon",           RacingSeries.Cup,        435,   6,  85,  6);
            R("Austin Dillon",           RacingSeries.National,   156,   9, 107, 17, 2013);
            R("Austin Dillon",           RacingSeries.Trucks,      73,   7,  48, 13, 2011);
            R("Kyle Busch",              RacingSeries.Cup,        750,  63, 392, 34, 2015, 2019);
            R("Kyle Busch",              RacingSeries.National,   367, 102, 267, 70, 2009);
            R("Kyle Busch",              RacingSeries.Trucks,     180,  67, 144, 23);
            R("Austin Hill",             RacingSeries.Cup,         15,   0,   1,  0);
            R("Austin Hill",             RacingSeries.National,   146,  14,  87,  6);
            R("Austin Hill",             RacingSeries.Trucks,     122,   8,  54,  3);
            R("Brad Keselowski",         RacingSeries.Cup,        584,  36, 276, 18, 2012);
            R("Brad Keselowski",         RacingSeries.National,   257,  39, 174, 22, 2010);
            R("Brad Keselowski",         RacingSeries.Trucks,      66,   1,  15,  1);
            R("Chris Buescher",          RacingSeries.Cup,        365,   6,  86,  1);
            R("Chris Buescher",          RacingSeries.National,    74,   3,  36,  0, 2015);
            R("Chris Buescher",          RacingSeries.Trucks,       1,   0,   0,  0);
            R("Ryan Preece",             RacingSeries.Cup,        214,   0,  30,  2);
            R("Ryan Preece",             RacingSeries.National,    62,   2,  21,  1);
            R("Ryan Preece",             RacingSeries.Trucks,      12,   2,  11,  1);
            R("Daniel Suárez",           RacingSeries.Cup,        323,   2,  75,  3);
            R("Daniel Suárez",           RacingSeries.National,    89,   4,  57,  6, 2016);
            R("Daniel Suárez",           RacingSeries.Trucks,      28,   1,  16,  1);
            R("Michael McDowell",        RacingSeries.Cup,        537,   2,  50,  8);
            R("Michael McDowell",        RacingSeries.National,    95,   1,  20,  2);
            R("Michael McDowell",        RacingSeries.Trucks,       4,   0,   0,  0);
            R("Carson Hocevar",          RacingSeries.Cup,         81,   0,  15,  1);
            R("Carson Hocevar",          RacingSeries.National,     7,   0,   3,  0);
            R("Carson Hocevar",          RacingSeries.Trucks,      81,   5,  34,  1);
            R("Noah Gragson",            RacingSeries.Cup,        102,   0,  11,  0);
            R("Noah Gragson",            RacingSeries.National,   139,  13,  99,  2);
            R("Noah Gragson",            RacingSeries.Trucks,      47,   2,  30,  9);
            R("Todd Gilliland",          RacingSeries.Cup,        144,   0,  15,  0);
            R("Todd Gilliland",          RacingSeries.Trucks,      94,   3,  53,  2);
            R("Layne Riggs",             RacingSeries.National,     3,   0,   1,  0);
            R("Layne Riggs",             RacingSeries.Trucks,      54,   5,  28,  4);
            R("Zane Smith",              RacingSeries.Cup,         79,   0,  10,  1);
            R("Zane Smith",              RacingSeries.National,    12,   0,   8,  0);
            R("Zane Smith",              RacingSeries.Trucks,      97,   9,  61,  3, 2022);
            R("John Hunter Nemechek",    RacingSeries.Cup,        104,   0,  15,  0);
            R("John Hunter Nemechek",    RacingSeries.National,   111,  11,  67,  3);
            R("John Hunter Nemechek",    RacingSeries.Trucks,     149,  13,  82,  9);
            R("Erik Jones",              RacingSeries.Cup,        316,   3,  95,  2);
            R("Erik Jones",              RacingSeries.National,    79,   9,  50, 15);
            R("Erik Jones",              RacingSeries.Trucks,      42,   7,  35,  7, 2015);
            R("Jimmie Johnson",          RacingSeries.Cup,        700,  83, 375, 36, 2006, 2007, 2008, 2009, 2010, 2013, 2016);
            R("Jimmie Johnson",          RacingSeries.National,    93,   1,  24,  2);
            R("Jimmie Johnson",          RacingSeries.Trucks,       1,   0,   0,  0);
            R("Ty Dillon",               RacingSeries.Cup,        281,   0,   8,  0);
            R("Ty Dillon",               RacingSeries.National,   165,   1,  92,  4);
            R("Ty Dillon",               RacingSeries.Trucks,      76,   3,  38,  5);
            R("AJ Allmendinger",         RacingSeries.Cup,        475,   3,  86,  5);
            R("AJ Allmendinger",         RacingSeries.National,   131,  18,  86, 11);
            R("AJ Allmendinger",         RacingSeries.Trucks,      14,   0,   3,  0);
            R("Josh Berry",              RacingSeries.Cup,         84,   1,  15,  0);
            R("Josh Berry",              RacingSeries.National,    97,   5,  52,  4);
            R("Josh Berry",              RacingSeries.Trucks,      11,   0,   1,  0);
            R("Cole Custer",             RacingSeries.Cup,        153,   1,  15,  1);
            R("Cole Custer",             RacingSeries.National,   176,  15, 119, 21, 2023);
            R("Cole Custer",             RacingSeries.Trucks,      42,   2,  24,  5);
            R("Ricky Stenhouse Jr.",     RacingSeries.Cup,        463,   4,  63,  3);
            R("Ricky Stenhouse Jr.",     RacingSeries.National,   110,   8,  63,  8, 2011, 2012);
            R("Cody Ware",               RacingSeries.Cup,        140,   0,   2,  0);
            R("Cody Ware",               RacingSeries.National,    28,   0,   1,  0);
            R("Cody Ware",               RacingSeries.Trucks,       7,   0,   0,  0);
            R("Kaz Grala",               RacingSeries.Cup,         31,   0,   2,  0);
            R("Kaz Grala",               RacingSeries.National,    81,   0,  20,  0);
            R("Kaz Grala",               RacingSeries.Trucks,      51,   1,  19,  1);
            R("J. J. Yeley",             RacingSeries.Cup,        396,   0,   9,  1);
            R("J. J. Yeley",             RacingSeries.National,   393,   0,  48,  3);
            R("J. J. Yeley",             RacingSeries.Trucks,      37,   0,   2,  0);
            R("Josh Bilicki",            RacingSeries.Cup,        107,   0,   1,  0);
            R("Josh Bilicki",            RacingSeries.National,   127,   0,   4,  0);
            R("Josh Bilicki",            RacingSeries.Trucks,      13,   0,   2,  0);
            R("Anthony Alfredo",         RacingSeries.Cup,         43,   0,   2,  0);
            R("Anthony Alfredo",         RacingSeries.National,   151,   0,  23,  1);
            R("Anthony Alfredo",         RacingSeries.Trucks,      13,   0,   2,  0);
            R("Chad Finchum",            RacingSeries.Cup,         12,   0,   0,  0);
            R("Chad Finchum",            RacingSeries.National,   116,   0,   0,  0);
            R("Katherine Legge",         RacingSeries.Cup,          7,   0,   0,  0);
            R("Katherine Legge",         RacingSeries.National,    11,   0,   0,  0);
            R("Justin Allgaier",         RacingSeries.Cup,         83,   0,   2,  0);
            R("Justin Allgaier",         RacingSeries.National,   504,  28, 302, 12, 2024);
            R("Justin Allgaier",         RacingSeries.Trucks,       8,   0,   0,  0);
            R("Jesse Love",              RacingSeries.Cup,          5,   0,   0,  0);
            R("Jesse Love",              RacingSeries.National,    66,   3,  40,  9, 2025);
            R("Jesse Love",              RacingSeries.Trucks,       3,   0,   2,  0);
            R("Harrison Burton",         RacingSeries.Cup,        109,   1,   6,  0);
            R("Harrison Burton",         RacingSeries.National,   108,   4,  59,  1);
            R("Harrison Burton",         RacingSeries.Trucks,      40,   0,  18,  1);
            R("Sam Mayer",               RacingSeries.National,   149,   8,  75,  3);
            R("Sam Mayer",               RacingSeries.Trucks,      14,   1,   5,  0);
            R("Aric Almirola",           RacingSeries.Cup,        460,   3,  96,  6);
            R("Aric Almirola",           RacingSeries.National,   132,  10,  59,  5);
            R("Aric Almirola",           RacingSeries.Trucks,      78,   2,  38,  0);
            R("Sammy Smith",             RacingSeries.National,   108,   3,  52,  3);
            R("Sammy Smith",             RacingSeries.Trucks,       9,   0,   6,  0);
            R("Sheldon Creed",           RacingSeries.Cup,          1,   0,   0,  0);
            R("Sheldon Creed",           RacingSeries.National,   136,   0,  68,  4);
            R("Sheldon Creed",           RacingSeries.Trucks,      76,   8,  37,  1, 2020);
            R("Brandon Jones",           RacingSeries.National,   335,   7, 132, 14);
            R("Brandon Jones",           RacingSeries.Trucks,      56,   1,  27,  0);
            R("Christian Eckes",         RacingSeries.National,    33,   0,  15,  0);
            R("Christian Eckes",         RacingSeries.Trucks,     116,   9,  74,  9);
            R("Carson Kvapil",           RacingSeries.National,    42,   0,  18,  0);
            R("Carson Kvapil",           RacingSeries.Trucks,       2,   0,   0,  0);
            R("Taylor Gray",             RacingSeries.National,    45,   1,  17,  3);
            R("Taylor Gray",             RacingSeries.Trucks,      56,   0,  22,  0);
            R("Nick Sanchez",            RacingSeries.National,    41,   1,  13,  0);
            R("Nick Sanchez",            RacingSeries.Trucks,      47,   2,  26,  7);
            R("William Sawalich",        RacingSeries.National,    34,   0,   9,  2);
            R("William Sawalich",        RacingSeries.Trucks,      21,   0,   4,  1);
            R("Jeb Burton",              RacingSeries.Cup,         34,   0,   0,  0);
            R("Jeb Burton",              RacingSeries.National,   208,   2,  48,  0);
            R("Jeb Burton",              RacingSeries.Trucks,      57,   1,  21,  7);
            R("Brennan Poole",           RacingSeries.Cup,         43,   0,   1,  0);
            R("Brennan Poole",           RacingSeries.National,   190,   0,  44,  1);
            R("Brennan Poole",           RacingSeries.Trucks,      38,   0,   4,  0);
            R("Dean Thompson",           RacingSeries.National,    35,   0,   8,  0);
            R("Dean Thompson",           RacingSeries.Trucks,      70,   0,  11,  0);
            R("Daniel Dye",              RacingSeries.National,    45,   0,  10,  0);
            R("Daniel Dye",              RacingSeries.Trucks,      46,   0,  10,  1);
            R("Ryan Sieg",               RacingSeries.Cup,          6,   0,   0,  0);
            R("Ryan Sieg",               RacingSeries.National,   400,   0,  75,  0);
            R("Ryan Sieg",               RacingSeries.Trucks,     108,   0,   7,  0);
            R("Parker Retzlaff",         RacingSeries.Cup,          2,   0,   1,  0);
            R("Parker Retzlaff",         RacingSeries.National,   107,   0,  15,  2);
            R("Parker Retzlaff",         RacingSeries.Trucks,       1,   0,   0,  0);
            R("Jeremy Clements",         RacingSeries.National,   530,   2,  42,  0);
            R("Leland Honeyman",         RacingSeries.National,    56,   0,   4,  0);
            R("Leland Honeyman",         RacingSeries.Trucks,       1,   0,   0,  0);
            R("Josh Williams",           RacingSeries.Cup,          5,   0,   0,  0);
            R("Josh Williams",           RacingSeries.National,   249,   0,  17,  0);
            R("Josh Williams",           RacingSeries.Trucks,       2,   0,   0,  0);
            R("Mason Massey",            RacingSeries.National,    68,   0,   3,  0);
            R("Mason Massey",            RacingSeries.Trucks,      31,   0,   0,  0);
            R("Blaine Perkins",          RacingSeries.National,   108,   0,   5,  0);
            R("Blaine Perkins",          RacingSeries.Trucks,      22,   0,   0,  0);
            R("Matt DiBenedetto",        RacingSeries.Cup,        248,   0,  31,  0);
            R("Matt DiBenedetto",        RacingSeries.National,   127,   0,   6,  0);
            R("Matt DiBenedetto",        RacingSeries.Trucks,      43,   1,  19,  0);
            R("Greg Van Alst",           RacingSeries.National,    15,   0,   0,  0);
            R("Greg Van Alst",           RacingSeries.Trucks,       9,   0,   0,  0);
            R("Kyle Sieg",               RacingSeries.National,   114,   0,   4,  0);
            R("Garrett Smithley",        RacingSeries.Cup,         76,   0,   0,  0);
            R("Garrett Smithley",        RacingSeries.National,   205,   0,   6,  0);
            R("Garrett Smithley",        RacingSeries.Trucks,      10,   0,   0,  0);
            R("Joey Gase",               RacingSeries.Cup,         93,   0,   0,  0);
            R("Joey Gase",               RacingSeries.National,   304,   0,   4,  0);
            R("Joey Gase",               RacingSeries.Trucks,       6,   0,   0,  0);
            R("Ryan Ellis",              RacingSeries.Cup,          6,   0,   0,  0);
            R("Ryan Ellis",              RacingSeries.National,   164,   0,   1,  0);
            R("Ryan Ellis",              RacingSeries.Trucks,      26,   0,   0,  0);
            R("Chandler Smith",          RacingSeries.Cup,          3,   0,   0,  0);
            R("Chandler Smith",          RacingSeries.National,    69,   3,  36,  6);
            R("Chandler Smith",          RacingSeries.Trucks,      87,   7,  51,  1);
            R("Grant Enfinger",          RacingSeries.Cup,          1,   0,   0,  0);
            R("Grant Enfinger",          RacingSeries.National,     1,   0,   0,  0);
            R("Grant Enfinger",          RacingSeries.Trucks,     221,  12, 129,  6);
            R("Ty Majeski",              RacingSeries.National,    15,   0,   3,  0);
            R("Ty Majeski",              RacingSeries.Trucks,     114,   6,  66, 11, 2024);
            R("Matt Crafton",            RacingSeries.Cup,          3,   0,   0,  0);
            R("Matt Crafton",            RacingSeries.National,     4,   0,   3,  0);
            R("Matt Crafton",            RacingSeries.Trucks,     592,  15, 333, 16, 2013, 2014, 2019);
            R("Daniel Hemric",           RacingSeries.Cup,         83,   0,   7,  1);
            R("Daniel Hemric",           RacingSeries.National,   189,   1, 105,  6, 2021);
            R("Daniel Hemric",           RacingSeries.Trucks,      75,   1,  45,  0);
            R("Ben Rhodes",              RacingSeries.Cup,          1,   0,   0,  0);
            R("Ben Rhodes",              RacingSeries.National,    10,   0,   2,  0);
            R("Ben Rhodes",              RacingSeries.Trucks,     235,   7, 120,  6, 2021, 2023);
            R("Stewart Friesen",         RacingSeries.Cup,          1,   0,   0,  0);
            R("Stewart Friesen",         RacingSeries.Trucks,     200,   4,  91,  4);
            R("Gio Ruggiero",            RacingSeries.Trucks,      25,   1,  11,  2);
            R("Rajah Caruth",            RacingSeries.National,    22,   0,   0,  0);
            R("Rajah Caruth",            RacingSeries.Trucks,      75,   2,  29,  2);
            R("Tyler Ankrum",            RacingSeries.Trucks,     161,   2,  65,  1);
            R("Kaden Honeycutt",         RacingSeries.National,     2,   0,   0,  0);
            R("Kaden Honeycutt",         RacingSeries.Trucks,      59,   0,  24,  0);
            R("Jake Garcia",             RacingSeries.Trucks,      75,   0,  21,  3);
            R("Brent Crews",             RacingSeries.Trucks,      10,   0,   3,  0);
            R("Tanner Gray",             RacingSeries.Trucks,     142,   0,  33,  2);
            R("Corey Day",               RacingSeries.National,    11,   0,   2,  0);
            R("Corey Day",               RacingSeries.Trucks,      13,   0,   3,  1);
            R("Connor Mosack",           RacingSeries.National,    34,   0,   4,  0);
            R("Connor Mosack",           RacingSeries.Trucks,      36,   0,   8,  1);
            R("Andrés Pérez de Lara",    RacingSeries.National,     1,   0,   0,  0);
            R("Andrés Pérez de Lara",    RacingSeries.Trucks,      27,   0,   4,  0);
            R("Bayley Currey",           RacingSeries.Cup,         12,   0,   0,  0);
            R("Bayley Currey",           RacingSeries.National,   111,   0,   2,  0);
            R("Bayley Currey",           RacingSeries.Trucks,      83,   0,  10,  0);
            R("Dawson Sutton",           RacingSeries.Trucks,      32,   0,   3,  0);
            R("Matt Mills",              RacingSeries.Cup,          1,   0,   0,  0);
            R("Matt Mills",              RacingSeries.National,   124,   0,   1,  0);
            R("Matt Mills",              RacingSeries.Trucks,      71,   0,   5,  0);
            R("Frankie Muniz",           RacingSeries.National,     2,   0,   0,  0);
            R("Frankie Muniz",           RacingSeries.Trucks,      25,   0,   1,  0);
            R("Spencer Boyd",            RacingSeries.Cup,          3,   0,   0,  0);
            R("Spencer Boyd",            RacingSeries.National,    50,   0,   0,  0);
            R("Spencer Boyd",            RacingSeries.Trucks,     156,   1,   5,  0);
            R("Nathan Byrd",             RacingSeries.National,     3,   0,   0,  0);
            R("Nathan Byrd",             RacingSeries.Trucks,      18,   0,   0,  0);
            R("Toni Breidinger",         RacingSeries.Trucks,      29,   0,   0,  0);
            R("Josh Reaume",             RacingSeries.National,    35,   0,   0,  0);
            R("Josh Reaume",             RacingSeries.Trucks,      83,   0,   1,  0);
        }

        // A driver's career in one series, or false when they never started a race in it (or are not a real
        // driver on file — the player's own driver is deliberately not here).
        public static bool TryGet(string driverName, RacingSeries series, out Record record) =>
            Rows.TryGetValue((Key(driverName), series), out record);

        // Every row, in file order: what the DriverSeriesStats table is seeded from.
        public static IEnumerable<(string name, RacingSeries series, Record record)> All()
        {
            foreach (var (name, series) in Order) yield return (name, series, Rows[(Key(name), series)]);
        }

        // How a name is matched: lower case, letters only, accents dropped — so "Daniel Suarez" finds
        // "Daniel Suárez" and "J.J. Yeley" finds "J. J. Yeley".
        public static string Key(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(name.Length);
            foreach (char c in name.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
