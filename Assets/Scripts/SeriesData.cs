using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SeriesData : MonoBehaviour{
	
	public static string[] offlineMenu = new string[20];
	public static string[,] offlineSeries = new string[20,25];
	public static string[] offlineImage = new string[20];
	public static string[,] offlineSeriesImage = new string[20,25];
	public static string[] seriesDescriptions = new string[20];
	public static string[,] offlineDescriptions = new string[20,25];
	public static int[,] offlineAILevel = new int[20,25];
	public static string[,] offlineMinType = new string[20,25];
	public static int[,] offlineMinLevel = new int[20,25];
	public static int[,] offlineMinClass = new int[20,25];
	public static int[,] offlineMinRarity = new int[20,25];
	public static string[,] offlineMinTeam = new string[20,25];
	public static int[,] offlineExactCar = new int[20,25];
	public static string[,] offlineMinManu = new string[20,25];
	public static string[,] offlineMinDriverType = new string[20,25];
	public static string[,] offlineTracklists = new string[20,25];
	public static string[] offlineCustomListOrder = new string[20];
	public static int[,] offlineFuel = new int[20,25];
	public static string[,] offlinePrizes = new string[20,25];
	
    // Start is called before the first frame update
    void Start(){
		loadSeries();
	}
	
	public static void loadSeries(){
		
		//--------Top Level---------
						
		offlineMenu[0] = "Rookies";
		offlineMenu[1] = "Superspeedways";
		offlineMenu[2] = "Cookie Cutters";
		offlineMenu[3] = "Short Tracks";
		offlineMenu[4] = "Class";
		offlineMenu[5] = "Team";
		offlineMenu[6] = "Manufacturer";
		offlineMenu[7] = "Type";
		offlineMenu[8] = "Rarity";
		offlineMenu[9] = "Seasons";
		offlineMenu[10] = "Mods";
		
		offlineImage[0] = "cup24livery4";
		offlineImage[1] = "cup24livery11";
		offlineImage[2] = "cup24livery24";
		offlineImage[3] = "cup24livery22";
		offlineImage[4] = "cup24livery5";
		offlineImage[5] = "cup24livery6";
		offlineImage[6] = "cup24livery8";
		offlineImage[7] = "cup24livery47";
		offlineImage[8] = "cup24livery48";
		offlineImage[9] = "cup24livery12";
		offlineImage[10] = "cup03livery97";
		
		seriesDescriptions[0] = "The first step onto the oval racing ladder. Show 'em what you've got!";
		seriesDescriptions[1] = "Learn the challenges of high speed pack racing.";
		seriesDescriptions[2] = "They say they all look the same, but the 1.5 mile oval is one you must master.";
		seriesDescriptions[3] = "Time to get physical! These tracks are tight and congested, with little room for error.";
		seriesDescriptions[4] = "Build your favourite driver's class to unlock the toughest challenges.";
		seriesDescriptions[5] = "The weight of the team rests on your shoulders. Time to deliver!";
		seriesDescriptions[6] = "Back your favourite manufacturer and take them to the top.";
		seriesDescriptions[7] = "Some drive clean, some not so much. Play to your drivers strengths here.";
		seriesDescriptions[8] = "Proven winners wear their stars with pride. Rarer drivers win bigger rewards!";
		seriesDescriptions[9] = "The complete calendars. The ultimate challenge!";
		seriesDescriptions[10] = "Community made mod series sets!";
		
		//Rookies
		offlineSeries[0,0] = "Take The Wheel";
		offlineSeries[0,1] = "Find The Draft";
		offlineSeries[0,2] = "Hit The Front";
		offlineSeries[0,3] = "Hold The Line";
		offlineSeries[0,4] = "Take The Win";
		
		offlineSeriesImage[0,0] = "cup24livery97";
		offlineSeriesImage[0,1] = "cup24livery62";
		offlineSeriesImage[0,2] = "cup24livery77";
		offlineSeriesImage[0,3] = "cup24livery31";
		offlineSeriesImage[0,4] = "cup24livery4";
		
		offlineDescriptions[0,0] = "The first step onto the oval racing ladder. Show 'em what you've got!";
		offlineDescriptions[0,1] = "These circuits are wider, meaning more room for overtaking!.";
		offlineDescriptions[0,2] = "How about some of the more unusual-shaped ovals?";
		offlineDescriptions[0,3] = "Try out some of the oldest circuits on the calendar.";
		offlineDescriptions[0,4] = "Finish off rookies with the biggest races of the year.";
		
		offlineAILevel[0,0] = 0;
		offlineAILevel[0,1] = 0;
		offlineAILevel[0,2] = 1;
		offlineAILevel[0,3] = 1;
		offlineAILevel[0,4] = 2;
		
		offlineMinLevel[0,0] = 1;
		offlineMinLevel[0,1] = 2;
		offlineMinLevel[0,2] = 4;
		offlineMinLevel[0,3] = 7;
		offlineMinLevel[0,4] = 10;
		
		offlineMinClass[0,0] = 0;
		offlineMinClass[0,1] = 0;
		offlineMinClass[0,2] = 0;
		offlineMinClass[0,3] = 0;
		offlineMinClass[0,4] = 0;
		
		offlineTracklists[0,0] = "1,2,4";
		offlineTracklists[0,1] = "3,5,10";
		offlineTracklists[0,2] = "6,14,8";
		offlineTracklists[0,3] = "12,9,15";
		offlineTracklists[0,4] = "13,20,21";
		
		offlinePrizes[0,0] = "Rookies";
		offlinePrizes[0,1] = "Rookies";
		offlinePrizes[0,2] = "Rarity1";
		offlinePrizes[0,3] = "Rarity1";
		offlinePrizes[0,4] = "Rarity2";
		
		
		//Speedways
		offlineSeries[1,0] = "Shuffle The Pack";
		offlineSeries[1,1] = "Running Hot";
		offlineSeries[1,2] = "200MpH";
		offlineSeries[1,3] = "Aboard The Train";
		offlineSeries[1,4] = "The Big One";
		
		offlineSeriesImage[1,0] = "cup24livery47";
		offlineSeriesImage[1,1] = "cup24livery43";
		offlineSeriesImage[1,2] = "cup24livery23";
		offlineSeriesImage[1,3] = "cup24livery34";
		offlineSeriesImage[1,4] = "cup24livery11";
		
		offlineDescriptions[1,0] = "Superspeedways are a different beast. Time to learn to tame them!";
		offlineDescriptions[1,1] = "With pack racing, strategy and timing is key to success.";
		offlineDescriptions[1,2] = "Keep an eye out for teammates that can help and push you!";
		offlineDescriptions[1,3] = "Tandem draft like it's 2010 and take a friend to victory lane!";
		offlineDescriptions[1,4] = "Some drivers just know how to win at plate tracks. How about you?";
		
		offlineAILevel[1,0] = 2;
		offlineAILevel[1,1] = 4;
		offlineAILevel[1,2] = 6;
		offlineAILevel[1,3] = 8;
		offlineAILevel[1,4] = 10;

		offlineMinLevel[1,0] = 5;
		offlineMinLevel[1,1] = 10;
		offlineMinLevel[1,2] = 20;
		offlineMinLevel[1,3] = 30;
		offlineMinLevel[1,4] = 35;
		
		offlineMinClass[1,0] = 1;
		offlineMinClass[1,1] = 2;
		offlineMinClass[1,2] = 2;
		offlineMinClass[1,3] = 3;
		offlineMinClass[1,4] = 3;
		
		offlineTracklists[1,0] = "1,10,5,15";
		offlineTracklists[1,1] = "1,5,10,15";
		offlineTracklists[1,2] = "5,14,10,1";
		offlineTracklists[1,3] = "15,5,10,15";
		offlineTracklists[1,4] = "10,1,15,4";
		
		offlinePrizes[1,0] = "Rarity1";
		offlinePrizes[1,1] = "Rarity1";
		offlinePrizes[1,2] = "Rarity2";
		offlinePrizes[1,3] = "Rarity2";
		offlinePrizes[1,4] = "Rarity3";
		
		
		//Cookie Cutters
		offlineSeries[2,0] = "One Point Five";
		offlineSeries[2,1] = "Tri Oval Trouble";
		offlineSeries[2,2] = "Under The Lights";
		offlineSeries[2,3] = "Feels Familiar";
		offlineSeries[2,4] = "Victory Vacuum";
		
		offlineSeriesImage[2,0] = "cup24livery6";
		offlineSeriesImage[2,1] = "cup24livery17";
		offlineSeriesImage[2,2] = "cup24livery5";
		offlineSeriesImage[2,3] = "cup24livery24";
		offlineSeriesImage[2,4] = "cup24livery84";
				
		offlineAILevel[2,0] = 2;
		offlineAILevel[2,1] = 4;
		offlineAILevel[2,2] = 6;
		offlineAILevel[2,3] = 8;
		offlineAILevel[2,4] = 10;
				
		offlineMinLevel[2,0] = 6;
		offlineMinLevel[2,1] = 11;
		offlineMinLevel[2,2] = 21;
		offlineMinLevel[2,3] = 31;
		offlineMinLevel[2,4] = 36;
		
		offlineMinClass[2,0] = 1;
		offlineMinClass[2,1] = 2;
		offlineMinClass[2,2] = 2;
		offlineMinClass[2,3] = 3;
		offlineMinClass[2,4] = 3;
		
		offlineTracklists[2,0] = "2,3,7,13";
		offlineTracklists[2,1] = "16,17,21,23";
		offlineTracklists[2,2] = "7,2,19,12";
		offlineTracklists[2,3] = "3,16,12,23";
		offlineTracklists[2,4] = "13,19,7,21";
				
		offlinePrizes[2,0] = "Rarity1";
		offlinePrizes[2,1] = "Rarity1";
		offlinePrizes[2,2] = "Rarity2";
		offlinePrizes[2,3] = "Rarity2";
		offlinePrizes[2,4] = "Rarity3";
		
		//Short Tracks
		offlineSeries[3,0] = "Push To Pass";
		offlineSeries[3,1] = "Bump And Run";
		offlineSeries[3,2] = "Rubbin Is Racin";
		offlineSeries[3,3] = "Hot Headed";
		offlineSeries[3,4] = "Clean Air";
		
		offlineSeriesImage[3,0] = "cup24livery14";
		offlineSeriesImage[3,1] = "cup24livery20";
		offlineSeriesImage[3,2] = "cup24livery1";
		offlineSeriesImage[3,3] = "cup24livery8";
		offlineSeriesImage[3,4] = "cup24livery22";
		
		offlineAILevel[3,0] = 2;
		offlineAILevel[3,1] = 4;
		offlineAILevel[3,2] = 6;
		offlineAILevel[3,3] = 8;
		offlineAILevel[3,4] = 10;
				
		offlineMinLevel[3,0] = 7;
		offlineMinLevel[3,1] = 12;
		offlineMinLevel[3,2] = 22;
		offlineMinLevel[3,3] = 32;
		offlineMinLevel[3,4] = 37;
		
		offlineMinClass[3,0] = 1;
		offlineMinClass[3,1] = 2;
		offlineMinClass[3,2] = 2;
		offlineMinClass[3,3] = 3;
		offlineMinClass[3,4] = 3;
		
		offlineTracklists[3,0] = "4,8,6,9";
		offlineTracklists[3,1] = "18,11,6,8";
		offlineTracklists[3,2] = "9,11,4,18";
		offlineTracklists[3,3] = "6,8,18,9,30";
		offlineTracklists[3,4] = "8,6,11,4,30";
				
		offlinePrizes[3,0] = "Rarity1";
		offlinePrizes[3,1] = "Rarity1";
		offlinePrizes[3,2] = "Rarity2";
		offlinePrizes[3,3] = "Rarity2";
		offlinePrizes[3,4] = "Rarity3";
		
		//Class
		offlineSeries[4,0] = "A Fan Favourite";
		offlineSeries[4,1] = "Ol' Reliable";
		offlineSeries[4,2] = "Perfect Pair";
		offlineSeries[4,3] = "Proven Winner";
		offlineSeries[4,4] = "Unbreakable Bond";
		
		offlineSeriesImage[4,0] = "cup24livery10";
		offlineSeriesImage[4,1] = "cup24livery3";
		offlineSeriesImage[4,2] = "cup24livery7";
		offlineSeriesImage[4,3] = "cup24livery23";
		offlineSeriesImage[4,4] = "cup24livery9";
		
		offlineAILevel[4,0] = 2;
		offlineAILevel[4,1] = 4;
		offlineAILevel[4,2] = 6;
		offlineAILevel[4,3] = 8;
		offlineAILevel[4,4] = 10;
		
		offlineMinLevel[4,0] = 8;
		offlineMinLevel[4,1] = 13;
		offlineMinLevel[4,2] = 23;
		offlineMinLevel[4,3] = 33;
		offlineMinLevel[4,4] = 38;
		
		offlineMinClass[4,0] = 2;
		offlineMinClass[4,1] = 3;
		offlineMinClass[4,2] = 4;
		offlineMinClass[4,3] = 5;
		offlineMinClass[4,4] = 6;
		
		offlineTracklists[4,0] = "1,12,16,18,5";
		offlineTracklists[4,1] = "2,8,7,15,17";
		offlineTracklists[4,2] = "3,9,19,14,20";
		offlineTracklists[4,3] = "11,10,7,15,6";
		offlineTracklists[4,4] = "13,4,1,20,21";
				
		offlinePrizes[4,0] = "Rarity1";
		offlinePrizes[4,1] = "Rarity1";
		offlinePrizes[4,2] = "Rarity2";
		offlinePrizes[4,3] = "Rarity2";
		offlinePrizes[4,4] = "Rarity3";
		
		//Team
		offlineSeries[5,0] = "Lone Ranger";
		offlineSeries[5,1] = "The Tallest Spire";
		offlineSeries[5,2] = "Front Row Seats";
		offlineSeries[5,3] = "Brad Jackeroushki";
		offlineSeries[5,4] = "Childress Of The Sea";
		offlineSeries[5,5] = "Trackhouse Rock";
		offlineSeries[5,6] = "The Real Haastle";
		offlineSeries[5,7] = "Apres Penske";
		offlineSeries[5,8] = "Dibbs On Gibbs";
		offlineSeries[5,9] = "The Hendrick Experience";

		offlineSeries[5,10] = "Kauligraphy";
		offlineSeries[5,11] = "Ware On Earth";
		offlineSeries[5,12] = "Air Jordans";
		offlineSeries[5,13] = "Lasting Legacy";
		offlineSeries[5,14] = "Street Foyter";
		offlineSeries[5,15] = "Andretti Confetti";
		offlineSeries[5,16] = "Follow The Arrow";
		offlineSeries[5,17] = "Chip Off The Block";
		offlineSeries[5,18] = "Flip Of A Coyne";
		offlineSeries[5,19] = "The Rein Of Dr Drey";
		offlineSeries[5,20] = "The Carpenters";
		offlineSeries[5,21] = "Holliwood Juncyard";
		offlineSeries[5,22] = "Meyer Of Shankville";
		offlineSeries[5,23] = "Guest On Letterman";
		
		offlineCustomListOrder[5] = "0,11,18,1,14,21,2,19,22,23,10,15,13,20,3,12,16,5,6,4,8,7,17,9";

		offlineSeriesImage[5,0] = "cup24livery21";
		offlineSeriesImage[5,1] = "cup24livery77";
		offlineSeriesImage[5,2] = "cup24livery34";
		offlineSeriesImage[5,3] = "cup24livery6";
		offlineSeriesImage[5,4] = "cup24livery8";
		offlineSeriesImage[5,5] = "cup24livery1";
		offlineSeriesImage[5,6] = "cup24livery10";
		offlineSeriesImage[5,7] = "cup24livery12";
		offlineSeriesImage[5,8] = "cup24livery19";
		offlineSeriesImage[5,9] = "cup24livery24";
		offlineSeriesImage[5,10] = "cup24livery16";
		offlineSeriesImage[5,11] = "cup24livery51";
		offlineSeriesImage[5,12] = "cup24livery23";
		offlineSeriesImage[5,13] = "cup24livery43";
		offlineSeriesImage[5,14] = "irl24livery41";
		offlineSeriesImage[5,15] = "irl24livery98";
		offlineSeriesImage[5,16] = "irl24livery5";
		offlineSeriesImage[5,17] = "irl24livery9";
		offlineSeriesImage[5,18] = "irl24livery18";
		offlineSeriesImage[5,19] = "irl24livery24";
		offlineSeriesImage[5,20] = "irl24livery20";
		offlineSeriesImage[5,21] = "irl24livery77";
		offlineSeriesImage[5,22] = "irl24livery60";
		offlineSeriesImage[5,23] = "irl24livery15";

		offlineAILevel[5,0] = 1;
		offlineAILevel[5,1] = 3;
		offlineAILevel[5,2] = 5;
		offlineAILevel[5,3] = 8;
		offlineAILevel[5,4] = 11;
		offlineAILevel[5,5] = 8;
		offlineAILevel[5,6] = 8;
		offlineAILevel[5,7] = 14;
		offlineAILevel[5,8] = 11;
		offlineAILevel[5,9] = 14;
		offlineAILevel[5,10] = 5;
		offlineAILevel[5,11] = 3;
		offlineAILevel[5,12] = 8;
		offlineAILevel[5,13] = 5;
		offlineAILevel[5,14] = 3;
		offlineAILevel[5,15] = 5;
		offlineAILevel[5,16] = 8;
		offlineAILevel[5,17] = 14;
		offlineAILevel[5,18] = 3;
		offlineAILevel[5,19] = 5;
		offlineAILevel[5,20] = 5;
		offlineAILevel[5,21] = 3;
		offlineAILevel[5,22] = 5;
		offlineAILevel[5,23] = 5;
		
		offlineMinLevel[5,0] = 5;
		offlineMinLevel[5,1] = 10;
		offlineMinLevel[5,2] = 15;
		offlineMinLevel[5,3] = 20;
		offlineMinLevel[5,4] = 30;
		offlineMinLevel[5,5] = 25;
		offlineMinLevel[5,6] = 25;
		offlineMinLevel[5,7] = 35;
		offlineMinLevel[5,8] = 30;
		offlineMinLevel[5,9] = 35;
		offlineMinLevel[5,10] = 15;
		offlineMinLevel[5,11] = 10;
		offlineMinLevel[5,12] = 20;
		offlineMinLevel[5,13] = 15;
		offlineMinLevel[5,14] = 10;
		offlineMinLevel[5,15] = 15;
		offlineMinLevel[5,16] = 20;
		offlineMinLevel[5,17] = 35;
		offlineMinLevel[5,18] = 10;
		offlineMinLevel[5,19] = 15;
		offlineMinLevel[5,20] = 15;
		offlineMinLevel[5,21] = 10;
		offlineMinLevel[5,22] = 15;
		offlineMinLevel[5,23] = 15;
		
		offlineMinClass[5,0] = 1;
		offlineMinClass[5,1] = 2;
		offlineMinClass[5,2] = 2;
		offlineMinClass[5,3] = 3;
		offlineMinClass[5,4] = 3;
		offlineMinClass[5,5] = 2;
		offlineMinClass[5,6] = 2;
		offlineMinClass[5,7] = 4;
		offlineMinClass[5,8] = 3;
		offlineMinClass[5,9] = 4;
		offlineMinClass[5,10] = 2;
		offlineMinClass[5,11] = 1;
		offlineMinClass[5,12] = 3;
		offlineMinClass[5,13] = 2;
		offlineMinClass[5,14] = 2;
		offlineMinClass[5,15] = 2;
		offlineMinClass[5,16] = 3;
		offlineMinClass[5,17] = 4;
		offlineMinClass[5,18] = 2;
		offlineMinClass[5,19] = 2;
		offlineMinClass[5,20] = 2;
		offlineMinClass[5,21] = 2;
		offlineMinClass[5,22] = 2;
		offlineMinClass[5,23] = 2;
		
		offlineMinType[5,0] = "Team";
		offlineMinType[5,1] = "Team";
		offlineMinType[5,2] = "Team";
		offlineMinType[5,3] = "Team";
		offlineMinType[5,4] = "Team";
		offlineMinType[5,5] = "Team";
		offlineMinType[5,6] = "Team";
		offlineMinType[5,7] = "Team";
		offlineMinType[5,8] = "Team";
		offlineMinType[5,9] = "Team";
		offlineMinType[5,10] = "Team";
		offlineMinType[5,11] = "Team";
		offlineMinType[5,12] = "Team";
		offlineMinType[5,13] = "Team";
		offlineMinType[5,14] = "Team";
		offlineMinType[5,15] = "Team";
		offlineMinType[5,16] = "Team";
		offlineMinType[5,17] = "Team";
		offlineMinType[5,18] = "Team";
		offlineMinType[5,19] = "Team";
		offlineMinType[5,20] = "Team";
		offlineMinType[5,21] = "Team";
		offlineMinType[5,22] = "Team";
		offlineMinType[5,23] = "Team";
		
		offlineMinTeam[5,0] = "IND";
		offlineMinTeam[5,1] = "SPI";
		offlineMinTeam[5,2] = "FRM";
		offlineMinTeam[5,3] = "RFK";
		offlineMinTeam[5,4] = "RCR";
		offlineMinTeam[5,5] = "TRK";
		offlineMinTeam[5,6] = "SHR";
		offlineMinTeam[5,7] = "PEN";
		offlineMinTeam[5,8] = "JGR";
		offlineMinTeam[5,9] = "HEN";
		offlineMinTeam[5,10] = "KAU";
		offlineMinTeam[5,11] = "RWR";
		offlineMinTeam[5,12] = "23X";
		offlineMinTeam[5,13] = "LMC";
		offlineMinTeam[5,14] = "FOY";
		offlineMinTeam[5,15] = "AND";
		offlineMinTeam[5,16] = "ARR";
		offlineMinTeam[5,17] = "CGR";
		offlineMinTeam[5,18] = "DCR";
		offlineMinTeam[5,19] = "DRE";
		offlineMinTeam[5,20] = "EDC";
		offlineMinTeam[5,21] = "JUN";
		offlineMinTeam[5,22] = "MEY";
		offlineMinTeam[5,23] = "RLL";
		
		offlineTracklists[5,0] = "1,12,16,18,5";
		offlineTracklists[5,1] = "2,8,7,15,17";
		offlineTracklists[5,2] = "3,9,19,14,20";
		offlineTracklists[5,3] = "11,10,7,15,6";
		offlineTracklists[5,4] = "13,4,1,22,21";
		offlineTracklists[5,5] = "2,23,5,10,16";
		offlineTracklists[5,6] = "8,4,22,14,12";
		offlineTracklists[5,7] = "11,9,6,3,21";
		offlineTracklists[5,8] = "13,7,17,20,19";
		offlineTracklists[5,9] = "15,18,1,5,2";
		offlineTracklists[5,10] = "1,12,16,18,5";
		offlineTracklists[5,11] = "2,8,7,15,17";
		offlineTracklists[5,12] = "3,9,19,14,20";
		offlineTracklists[5,13] = "11,10,7,15,6";
		offlineTracklists[5,14] = "13,4,1,22,21";
		offlineTracklists[5,15] = "2,23,5,10,16";
		offlineTracklists[5,16] = "1,12,16,18,5";
		offlineTracklists[5,17] = "2,8,7,15,17";
		offlineTracklists[5,18] = "3,9,19,14,20";
		offlineTracklists[5,19] = "11,10,7,15,6";
		offlineTracklists[5,20] = "13,4,1,22,21";
		offlineTracklists[5,21] = "2,23,5,10,16";
		offlineTracklists[5,22] = "3,9,19,14,20";
		offlineTracklists[5,23] = "11,10,7,15,6";
			
		offlinePrizes[5,0] = "IND";
		offlinePrizes[5,1] = "SPI";
		offlinePrizes[5,2] = "FRM";
		offlinePrizes[5,3] = "RFK";
		offlinePrizes[5,4] = "RCR";
		offlinePrizes[5,5] = "TRK";
		offlinePrizes[5,6] = "SHR";
		offlinePrizes[5,7] = "PEN";
		offlinePrizes[5,8] = "JGR";
		offlinePrizes[5,9] = "HEN";
		offlinePrizes[5,10] = "KAU";
		offlinePrizes[5,11] = "RWR";
		offlinePrizes[5,12] = "23X";
		offlinePrizes[5,13] = "LMC";
		offlinePrizes[5,14] = "FOY";
		offlinePrizes[5,15] = "AND";
		offlinePrizes[5,16] = "ARR";
		offlinePrizes[5,17] = "CGR";
		offlinePrizes[5,18] = "DCR";
		offlinePrizes[5,19] = "DRE";
		offlinePrizes[5,20] = "EDC";
		offlinePrizes[5,21] = "JUN";
		offlinePrizes[5,22] = "MEY";
		offlinePrizes[5,23] = "RLL";
		
		//Manufacturer
		offlineSeries[6,0] = "Mustang Sally";
		offlineSeries[6,1] = "American Pie";
		offlineSeries[6,2] = "2JZ Swapped";
		offlineSeries[6,3] = "Thunderbird";
		offlineSeries[6,4] = "Heavy Chevy";
		offlineSeries[6,5] = "Lets Go Places";
		offlineSeries[6,6] = "VTEC Kicked In";
		offlineSeries[6,7] = "Power Of Dreams";
		
		offlineCustomListOrder[6] = "0,1,2,6,3,4,5,7";

		
		offlineSeriesImage[6,0] = "cup24livery41";
		offlineSeriesImage[6,1] = "cup24livery62";
		offlineSeriesImage[6,2] = "cup24livery43";
		offlineSeriesImage[6,3] = "cup24livery22";
		offlineSeriesImage[6,4] = "cup24livery5";
		offlineSeriesImage[6,5] = "cup24livery11";
		offlineSeriesImage[6,6] = "irl24livery30";
		offlineSeriesImage[6,7] = "irl24livery10";
		
		offlineAILevel[6,0] = 7;
		offlineAILevel[6,1] = 7;
		offlineAILevel[6,2] = 7;
		offlineAILevel[6,3] = 12;
		offlineAILevel[6,4] = 12;
		offlineAILevel[6,5] = 12;
		offlineAILevel[6,6] = 7;
		offlineAILevel[6,7] = 12;
		
		offlineMinLevel[6,0] = 10;
		offlineMinLevel[6,1] = 10;
		offlineMinLevel[6,2] = 10;
		offlineMinLevel[6,3] = 25;
		offlineMinLevel[6,4] = 25;
		offlineMinLevel[6,5] = 25;
		offlineMinLevel[6,6] = 10;
		offlineMinLevel[6,7] = 25;
		
		offlineMinClass[6,0] = 3;
		offlineMinClass[6,1] = 3;
		offlineMinClass[6,2] = 3;
		offlineMinClass[6,3] = 5;
		offlineMinClass[6,4] = 5;
		offlineMinClass[6,5] = 5;
		offlineMinClass[6,6] = 3;
		offlineMinClass[6,7] = 5;
		
		offlineMinType[6,0] = "Manufacturer";
		offlineMinType[6,1] = "Manufacturer";
		offlineMinType[6,2] = "Manufacturer";
		offlineMinType[6,3] = "Manufacturer";
		offlineMinType[6,4] = "Manufacturer";
		offlineMinType[6,5] = "Manufacturer";
		offlineMinType[6,6] = "Manufacturer";
		offlineMinType[6,7] = "Manufacturer";
		
		offlineMinManu[6,0] = "FRD";
		offlineMinManu[6,1] = "CHV";
		offlineMinManu[6,2] = "TYT";
		offlineMinManu[6,3] = "FRD";
		offlineMinManu[6,4] = "CHV";
		offlineMinManu[6,5] = "TYT";
		offlineMinManu[6,6] = "HON";
		offlineMinManu[6,7] = "HON";
		
		offlineTracklists[6,0] = "1,12,16,18,5";
		offlineTracklists[6,1] = "2,8,7,15,17";
		offlineTracklists[6,2] = "3,9,19,14,22";
		offlineTracklists[6,3] = "11,10,23,15,6";
		offlineTracklists[6,4] = "13,4,1,20,21";
		offlineTracklists[6,5] = "2,15,5,10,16";
		offlineTracklists[6,6] = "3,9,19,14,22";
		offlineTracklists[6,7] = "11,10,23,15,6";
		
		offlinePrizes[6,0] = "FRD1";
		offlinePrizes[6,1] = "CHV1";
		offlinePrizes[6,2] = "TYT1";
		offlinePrizes[6,3] = "FRD";
		offlinePrizes[6,4] = "CHV";
		offlinePrizes[6,5] = "TYT";
		offlinePrizes[6,6] = "HON1";
		offlinePrizes[6,7] = "HON";
		
		//Type
		offlineSeries[7,0] = "Rookie Season";
		offlineSeries[7,1] = "Strategy Calls";
		offlineSeries[7,2] = "Closing In";
		offlineSeries[7,3] = "Intimidating";
		offlineSeries[7,4] = "Downright Dominate";
		offlineSeries[7,5] = "Legend Of The Sport";
		
		offlineSeriesImage[7,0] = "cup24livery77";
		offlineSeriesImage[7,1] = "cup24livery60";
		offlineSeriesImage[7,2] = "cup24livery17";
		offlineSeriesImage[7,3] = "cup24livery10";
		offlineSeriesImage[7,4] = "cup24livery5";
		offlineSeriesImage[7,5] = "cup24livery84";
		
		offlineAILevel[7,0] = 2;
		offlineAILevel[7,1] = 4;
		offlineAILevel[7,2] = 6;
		offlineAILevel[7,3] = 8;
		offlineAILevel[7,4] = 10;
		offlineAILevel[7,5] = 12;
		
		offlineMinLevel[7,0] = 20;
		offlineMinLevel[7,1] = 22;
		offlineMinLevel[7,2] = 24;
		offlineMinLevel[7,3] = 26;
		offlineMinLevel[7,4] = 28;
		offlineMinLevel[7,5] = 30;
		
		offlineMinClass[7,0] = 1;
		offlineMinClass[7,1] = 2;
		offlineMinClass[7,2] = 2;
		offlineMinClass[7,3] = 2;
		offlineMinClass[7,4] = 2;
		offlineMinClass[7,5] = 3;
		
		offlineMinType[7,0] = "Type";
		offlineMinType[7,1] = "Type";
		offlineMinType[7,2] = "Type";
		offlineMinType[7,3] = "Type";
		offlineMinType[7,4] = "Type";
		offlineMinType[7,5] = "Type";
		
		offlineMinDriverType[7,0] = "Rookie";
		offlineMinDriverType[7,1] = "Strategist";
		offlineMinDriverType[7,2] = "Closer";
		offlineMinDriverType[7,3] = "Intimidator";
		offlineMinDriverType[7,4] = "Dominator";
		offlineMinDriverType[7,5] = "Legend";
		
		offlineTracklists[7,0] = "1,12,16,18,5";
		offlineTracklists[7,1] = "2,8,7,15,17";
		offlineTracklists[7,2] = "3,9,19,14,20";
		offlineTracklists[7,3] = "11,10,23,15,6";
		offlineTracklists[7,4] = "13,4,1,22,21";
		offlineTracklists[7,5] = "2,15,5,10,16";
		
		offlinePrizes[7,0] = "Rookie";
		offlinePrizes[7,1] = "Strategist";
		offlinePrizes[7,2] = "Closer";
		offlinePrizes[7,3] = "Intimidator";
		offlinePrizes[7,4] = "Dominator";
		offlinePrizes[7,5] = "Legend";
		
		//Rarity
		offlineSeries[8,0] = "Driver For Hire";
		offlineSeries[8,1] = "Hot Property";
		offlineSeries[8,2] = "Proven Talent";
		offlineSeries[8,3] = "Born To Win";
		
		offlineSeriesImage[8,0] = "cup24livery2";
		offlineSeriesImage[8,1] = "cup24livery45";
		offlineSeriesImage[8,2] = "cup24livery6";
		offlineSeriesImage[8,3] = "cup24livery22";
		
		offlineAILevel[8,0] = 4;
		offlineAILevel[8,1] = 8;
		offlineAILevel[8,2] = 12;
		offlineAILevel[8,3] = 15;
		
		offlineMinLevel[8,0] = 10;
		offlineMinLevel[8,1] = 25;
		offlineMinLevel[8,2] = 35;
		offlineMinLevel[8,3] = 40;
		
		offlineMinClass[8,0] = 1;
		offlineMinClass[8,1] = 3;
		offlineMinClass[8,2] = 4;
		offlineMinClass[8,3] = 5;
		
		offlineMinType[8,0] = "Rarity";
		offlineMinType[8,1] = "Rarity";
		offlineMinType[8,2] = "Rarity";
		offlineMinType[8,3] = "Rarity";
		
		offlineMinRarity[8,0] = 1;
		offlineMinRarity[8,1] = 2;
		offlineMinRarity[8,2] = 3;
		offlineMinRarity[8,3] = 4;
		
		offlineTracklists[8,0] = "1,12,16,18,5";
		offlineTracklists[8,1] = "2,8,7,15,17";
		offlineTracklists[8,2] = "3,9,19,14,20";
		offlineTracklists[8,3] = "6,15,21,7,11";
		
		offlinePrizes[8,0] = "Rarity1";
		offlinePrizes[8,1] = "Rarity2";
		offlinePrizes[8,2] = "Rarity3";
		offlinePrizes[8,3] = "Rarity3Plus";
		
		//Seasons
		offlineSeries[9,0] = "Half Season 1";
		offlineSeries[9,1] = "Half Season 2";
		offlineSeries[9,2] = "2020 Cup Season";
		offlineSeries[9,3] = "2022 Cup Season";
		offlineSeries[9,4] = "2023 Cup Season";
		offlineSeries[9,5] = "2024 Cup (Easy)";
		offlineSeries[9,6] = "2024 Cup (Hard)";
		offlineSeries[9,7] = "2024 Cup (Extra Hard)";
		
		offlineSeries[9,8] = "2024 Indy Ovals";
		offlineSeries[9,9] = "2003 Indy Ovals";
		
		offlineCustomListOrder[9] = "0,1,8,5,2,3,4,6,9,7";

		
		offlineSeriesImage[9,0] = "cup20livery22";
		offlineSeriesImage[9,1] = "cup20livery2";
		offlineSeriesImage[9,2] = "cup20livery9";
		offlineSeriesImage[9,3] = "cup22livery5";
		offlineSeriesImage[9,4] = "cup23livery22";
		offlineSeriesImage[9,5] = "cup24livery84";
		offlineSeriesImage[9,6] = "cup24livery8";
		offlineSeriesImage[9,7] = "cup24livery12";
		offlineSeriesImage[9,8] = "irl24livery10";
		offlineSeriesImage[9,9] = "irl24livery9";
		
		offlineAILevel[9,0] = 5;
		offlineAILevel[9,1] = 5;
		offlineAILevel[9,2] = 7;
		offlineAILevel[9,3] = 9;
		offlineAILevel[9,4] = 12;
		offlineAILevel[9,5] = 5;
		offlineAILevel[9,6] = 10;
		offlineAILevel[9,7] = 15;
		offlineAILevel[9,8] = 8;
		offlineAILevel[9,9] = 12;
		
		offlineMinLevel[9,0] = 5;
		offlineMinLevel[9,1] = 10;
		offlineMinLevel[9,2] = 20;
		offlineMinLevel[9,3] = 20;
		offlineMinLevel[9,4] = 20;
		offlineMinLevel[9,5] = 25;
		offlineMinLevel[9,6] = 28;
		offlineMinLevel[9,7] = 35;
		offlineMinLevel[9,8] = 15;
		offlineMinLevel[9,9] = 30;
		
		offlineMinClass[9,0] = 1;
		offlineMinClass[9,1] = 1;
		offlineMinClass[9,2] = 1;
		offlineMinClass[9,3] = 1;
		offlineMinClass[9,4] = 1;
		offlineMinClass[9,5] = 1;
		offlineMinClass[9,6] = 1;
		offlineMinClass[9,7] = 1;
		offlineMinClass[9,8] = 1;
		offlineMinClass[9,9] = 1;
		
		
		offlineTracklists[9,0] = "1,2,3,4,5,6,7,8,9,10,11";
		offlineTracklists[9,1] = "12,13,14,15,16,17,18,19,20,21";
		offlineTracklists[9,2] = "1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21";
		offlineTracklists[9,3] = "30,1,5,3,4,2,9,6,8,10,11,19,12,7,13,22,23,2,18,14,20,15,9,1,19,12,8,7,10,3,21,6,4";
		offlineTracklists[9,4] = "30,1,5,3,4,2,9,8,6,10,11,12,19,26,13,22,23,2,18,14,9,20,1,19,12,8,7,10,3,21,6,4";
		offlineTracklists[9,5] = "30,1,2,3,4,8,9,6,7,10,11,12,19,26,13,22,28,18,23,14,20,9,15,1,19,2,8,12,10,3,21,6,4";
		offlineTracklists[9,6] = "30,1,2,3,4,8,9,6,7,10,11,12,19,26,13,22,28,18,23,14,20,9,15,1,19,2,8,12,10,3,21,6,4";
		offlineTracklists[9,7] = "30,1,2,3,4,8,9,6,7,10,11,12,19,26,13,22,28,18,23,14,20,9,15,1,19,2,8,12,10,3,21,6,4";
		offlineTracklists[9,8] = "20,28,28,22,33,23";
		offlineTracklists[9,9] = "21,4,31,20,7,29,9,12,23,15,22,17,27,16,5,7";
		
		offlinePrizes[9,0] = "";
		offlinePrizes[9,1] = "";
		offlinePrizes[9,2] = "cup20";
		offlinePrizes[9,3] = "cup22";
		offlinePrizes[9,4] = "cup23";
		offlinePrizes[9,5] = "Rarity1";
		offlinePrizes[9,6] = "Rarity2";
		offlinePrizes[9,7] = "Rarity3";
		offlinePrizes[9,8] = "Indy1";
		offlinePrizes[9,9] = "Indy";
	}

	public static List<string> ListRewards(string category){
		List<string> validDriver = new List<string>();
		switch(category){
			
			//Team Rewards
			case "cup20":
			case "cup22":
			case "cup23":
			case "cup24":
			case "irl23":
			case "irl24":
			case "dmc15":
			case "irc00":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					if(seriesPrefix == category){
						for(int j=0;j<=99;j++){
							if(DriverNames.getName(seriesPrefix,j) != null){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log(category + " Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rookies":
				//for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getType(seriesPrefix,j) == "Rookie"){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("Rookie Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity1":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 1){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("1* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity2":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 2){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("2* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity3":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 3){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("3* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity3Plus":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) >= 3){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("4* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity4":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 4){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("4* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			
			//Manufacturer Rewards
			case "CHV":
			case "FRD":
			case "TYT":
			case "HON":
			case "DDG":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getManufacturer(seriesPrefix,j) == category){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log(category + " Added: #" + i);
							}
						}
					}
				}
			break;
			
			//Manufacturer Rewards (Capped at 1*)
			case "CHV1":
			case "FRD1":
			case "TYT1":
			case "HON1":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getManufacturer(seriesPrefix,j) == category.Substring(0,3)){
								if(DriverNames.getRarity(seriesPrefix,j) == 1){
									validDriver.Add("" + seriesPrefix + j + "");
								}
							}
						}
					}
				}
			break;
			
			//Team Rewards
			case "IND":
			case "SPI":
			case "RWR":
			case "FRM":
			case "RFR":
			case "RFK":
			case "TRK":
			case "RCR":
			case "CGR":
			case "SHR":
			case "PEN":
			case "JGR":
			case "HEN":
			case "KAU":
			case "23X":
			case "LMC":
			case "FOY":
			case "AND":
			case "ARR":
			case "DCR":
			case "DRE":
			case "EDC":
			case "JUN":
			case "MEY":
			case "RLL":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getTeam(seriesPrefix,j) == category){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log(category + " Added: #" + i);
							}
						}
					}
				}
			break;
			
			//Driver Type Rewards
			case "Strategist":
			case "Closer":
			case "Intimidator":
			case "Pusher":
			case "Blocker":
			case "Dominator":
			case "Legend":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getType(seriesPrefix,j) == category){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log(category + " Added: #" + i);
							}
						}
					}
				}
			break;
			default:
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							validDriver.Add("" + seriesPrefix + j + "");
							//Debug.Log("Added: #" + i);
						}
					}
				}
			break;
		}
		return validDriver;
	}

    // Update is called once per frame
    void Update(){
    }
	
	public static string classAbbr(int carClass){
		string classLetter;
		switch(carClass){
			case 1:
				classLetter = "R";
				break;
		    case 2:
				classLetter = "D";
				break;
			case 3:
				classLetter = "C";
				break;
			case 4:
				classLetter = "B";
				break;
			case 5:
				classLetter = "A";
				break;
			case 6:
				classLetter = "S";
				break;
		    default:
				classLetter = "R";
				break;
		}
		return classLetter;
	}
}
