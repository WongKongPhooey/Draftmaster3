﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SeriesData : MonoBehaviour{
	
	public static string[] offlineMenu = new string[10];
	public static string[,] offlineSeries = new string[10,10];
	public static string[] offlineImage = new string[10];
	public static string[,] offlineSeriesImage = new string[10,10];
	public static string[] seriesDescriptions = new string[10];
	public static string[,] offlineDescriptions = new string[10,10];
	public static int[,] offlineDailyPlays = new int[10,10];
	public static int[,] offlineAILevel = new int[10,10];
	public static string[,] offlineMinType = new string[10,10];
	public static int[,] offlineMinLevel = new int[10,10];
	public static int[,] offlineMinClass = new int[10,10];
	public static int[,] offlineMinRarity = new int[10,10];
	public static string[,] offlineMinTeam = new string[10,10];
	public static int[,] offlineExactCar = new int[10,10];
	public static string[,] offlineMinManu = new string[10,10];
	public static string[,] offlineMinDriverType = new string[10,10];
	public static string[,] offlineTracklists = new string[10,10];
	public static int[,] offlineFuel = new int[10,10];
	public static string[,] offlinePrizes = new string[10,10];
	
    // Start is called before the first frame update
    void Start(){
		setData();
	}
	
	public static void setData(){
		offlineMenu[0] = "Rookies";
		offlineMenu[1] = "Super Speedways";
		offlineMenu[2] = "Cookie Cutters";
		offlineMenu[3] = "Short Tracks";
		offlineMenu[4] = "Class";
		offlineMenu[5] = "Team";
		offlineMenu[6] = "Manufacturer";
		offlineMenu[7] = "Type";
		offlineMenu[8] = "Rarity";
		offlineMenu[9] = "Seasons";
		
		//Rookies
		offlineSeries[0,0] = "Take The Wheel";
		offlineSeries[0,1] = "Find The Draft";
		offlineSeries[0,2] = "Hit The Front";
		offlineSeries[0,3] = "Hold The Line";
		offlineSeries[0,4] = "Take The Win";
		
		offlineSeriesImage[0,0] = "cup20livery0";
		offlineSeriesImage[0,1] = "cup20livery15";
		offlineSeriesImage[0,2] = "cup20livery95";
		offlineSeriesImage[0,3] = "cup20livery8";
		offlineSeriesImage[0,4] = "cup20livery41";
		
		offlineDescriptions[0,0] = "The first step onto the oval racing ladder. Show 'em what you've got!";
		offlineDescriptions[0,1] = "These circuits are wider, meaning more room for overtaking!.";
		offlineDescriptions[0,2] = "How about some of the more unusual-shaped ovals?";
		offlineDescriptions[0,3] = "Try out some of the oldest circuits on the calendar.";
		offlineDescriptions[0,4] = "Finish off rookies with the biggest races of the year.";
		
		offlineDailyPlays[0,0] = 20;
		offlineDailyPlays[0,1] = 20;
		offlineDailyPlays[0,2] = 20;
		offlineDailyPlays[0,3] = 20;
		offlineDailyPlays[0,4] = 20;
		
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
		
		offlineFuel[0,0] = 4;
		offlineFuel[0,1] = 4;
		offlineFuel[0,2] = 4;
		offlineFuel[0,3] = 5;
		offlineFuel[0,4] = 5;
		
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
		
		offlineSeriesImage[1,0] = "cup20livery47";
		offlineSeriesImage[1,1] = "cup20livery22";
		offlineSeriesImage[1,2] = "cup20livery11";
		offlineSeriesImage[1,3] = "cup20livery48";
		offlineSeriesImage[1,4] = "cup20livery2";
		
		offlineDescriptions[1,0] = "Superspeedways are a different beast. Time to learn to tame them!";
		offlineDescriptions[1,1] = "With pack racing, strategy and timing is key to success.";
		offlineDescriptions[1,2] = "Keep an eye out for teammates that can help and push you!";
		offlineDescriptions[1,3] = "Tandem draft like it's 2010 and take a friend to victory lane!";
		offlineDescriptions[1,4] = "Some drivers just know how to win at plate tracks. How about you?";
		
		offlineDailyPlays[1,0] = 10;
		offlineDailyPlays[1,1] = 10;
		offlineDailyPlays[1,2] = 10;
		offlineDailyPlays[1,3] = 10;
		offlineDailyPlays[1,4] = 10;
		
		offlineAILevel[1,0] = 2;
		offlineAILevel[1,1] = 4;
		offlineAILevel[1,2] = 6;
		offlineAILevel[1,3] = 8;
		offlineAILevel[1,4] = 10;

		offlineMinLevel[1,0] = 5;
		offlineMinLevel[1,1] = 10;
		offlineMinLevel[1,2] = 20;
		offlineMinLevel[1,3] = 30;
		offlineMinLevel[1,4] = 40;
		
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
		
		offlineFuel[1,0] = 5;
		offlineFuel[1,1] = 6;
		offlineFuel[1,2] = 7;
		offlineFuel[1,3] = 8;
		offlineFuel[1,4] = 9;
		
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
		
		offlineSeriesImage[2,0] = "cup20livery14";
		offlineSeriesImage[2,1] = "cup20livery19";
		offlineSeriesImage[2,2] = "cup20livery6";
		offlineSeriesImage[2,3] = "cup20livery4";
		offlineSeriesImage[2,4] = "cup20livery48";
		
		offlineDailyPlays[2,0] = 10;
		offlineDailyPlays[2,1] = 10;
		offlineDailyPlays[2,2] = 10;
		offlineDailyPlays[2,3] = 10;
		offlineDailyPlays[2,4] = 10;
				
		offlineAILevel[2,0] = 2;
		offlineAILevel[2,1] = 4;
		offlineAILevel[2,2] = 6;
		offlineAILevel[2,3] = 8;
		offlineAILevel[2,4] = 10;
				
		offlineMinLevel[2,0] = 6;
		offlineMinLevel[2,1] = 11;
		offlineMinLevel[2,2] = 21;
		offlineMinLevel[2,3] = 31;
		offlineMinLevel[2,4] = 41;
		
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
		
		offlineFuel[2,0] = 5;
		offlineFuel[2,1] = 6;
		offlineFuel[2,2] = 7;
		offlineFuel[2,3] = 8;
		offlineFuel[2,4] = 9;
				
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
		
		offlineSeriesImage[3,0] = "cup20livery21";
		offlineSeriesImage[3,1] = "cup20livery10";
		offlineSeriesImage[3,2] = "cup20livery14";
		offlineSeriesImage[3,3] = "cup20livery18";
		offlineSeriesImage[3,4] = "cup20livery19";
		
		offlineDailyPlays[3,0] = 10;
		offlineDailyPlays[3,1] = 10;
		offlineDailyPlays[3,2] = 10;
		offlineDailyPlays[3,3] = 10;
		offlineDailyPlays[3,4] = 10;
		
		offlineAILevel[3,0] = 2;
		offlineAILevel[3,1] = 4;
		offlineAILevel[3,2] = 6;
		offlineAILevel[3,3] = 8;
		offlineAILevel[3,4] = 10;
				
		offlineMinLevel[3,0] = 7;
		offlineMinLevel[3,1] = 12;
		offlineMinLevel[3,2] = 22;
		offlineMinLevel[3,3] = 32;
		offlineMinLevel[3,4] = 42;
		
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
		
		offlineFuel[3,0] = 5;
		offlineFuel[3,1] = 6;
		offlineFuel[3,2] = 7;
		offlineFuel[3,3] = 8;
		offlineFuel[3,4] = 9;
				
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
		
		offlineSeriesImage[4,0] = "cup20livery95";
		offlineSeriesImage[4,1] = "cup20livery22";
		offlineSeriesImage[4,2] = "cup20livery11";
		offlineSeriesImage[4,3] = "cup20livery3";
		offlineSeriesImage[4,4] = "cup20livery4";
		
		offlineDailyPlays[4,0] = 10;
		offlineDailyPlays[4,1] = 10;
		offlineDailyPlays[4,2] = 10;
		offlineDailyPlays[4,3] = 10;
		offlineDailyPlays[4,4] = 10;
		
		offlineAILevel[4,0] = 2;
		offlineAILevel[4,1] = 4;
		offlineAILevel[4,2] = 6;
		offlineAILevel[4,3] = 8;
		offlineAILevel[4,4] = 10;
		
		offlineMinLevel[4,0] = 8;
		offlineMinLevel[4,1] = 13;
		offlineMinLevel[4,2] = 23;
		offlineMinLevel[4,3] = 33;
		offlineMinLevel[4,4] = 43;
		
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
		
		offlineFuel[4,0] = 5;
		offlineFuel[4,1] = 6;
		offlineFuel[4,2] = 7;
		offlineFuel[4,3] = 8;
		offlineFuel[4,4] = 9;
				
		offlinePrizes[4,0] = "Rarity1";
		offlinePrizes[4,1] = "Rarity1";
		offlinePrizes[4,2] = "Rarity2";
		offlinePrizes[4,3] = "Rarity2";
		offlinePrizes[4,4] = "Rarity3";
		
		//Team
		offlineSeries[5,0] = "Lone Ranger";
		offlineSeries[5,1] = "Ware In The World";
		offlineSeries[5,2] = "Front Row Seats";
		offlineSeries[5,3] = "Show Me The Fenway";
		offlineSeries[5,4] = "Childress Of The Sea";
		offlineSeries[5,5] = "Chip Off The Old Block";
		offlineSeries[5,6] = "The Real Haastle";
		offlineSeries[5,7] = "Apres Penske";
		offlineSeries[5,8] = "Dibbs On Gibbs";
		offlineSeries[5,9] = "The Hendrick Experience";
		
		offlineSeriesImage[5,0] = "cup20livery77";
		offlineSeriesImage[5,1] = "cup20livery52";
		offlineSeriesImage[5,2] = "cup20livery38";
		offlineSeriesImage[5,3] = "cup20livery6";
		offlineSeriesImage[5,4] = "cup20livery3";
		offlineSeriesImage[5,5] = "cup20livery42";
		offlineSeriesImage[5,6] = "cup20livery10";
		offlineSeriesImage[5,7] = "cup20livery12";
		offlineSeriesImage[5,8] = "cup20livery20";
		offlineSeriesImage[5,9] = "cup20livery24";
		
		offlineDailyPlays[5,0] = 3;
		offlineDailyPlays[5,1] = 3;
		offlineDailyPlays[5,2] = 3;
		offlineDailyPlays[5,3] = 3;
		offlineDailyPlays[5,4] = 3;
		offlineDailyPlays[5,5] = 3;
		offlineDailyPlays[5,6] = 3;
		offlineDailyPlays[5,7] = 3;
		offlineDailyPlays[5,8] = 3;
		offlineDailyPlays[5,9] = 3;

		offlineAILevel[5,0] = 2;
		offlineAILevel[5,1] = 5;
		offlineAILevel[5,2] = 5;
		offlineAILevel[5,3] = 5;
		offlineAILevel[5,4] = 5;
		offlineAILevel[5,5] = 8;
		offlineAILevel[5,6] = 8;
		offlineAILevel[5,7] = 10;
		offlineAILevel[5,8] = 10;
		offlineAILevel[5,9] = 10;
		
		offlineMinLevel[5,0] = 10;
		offlineMinLevel[5,1] = 13;
		offlineMinLevel[5,2] = 16;
		offlineMinLevel[5,3] = 19;
		offlineMinLevel[5,4] = 22;
		offlineMinLevel[5,5] = 25;
		offlineMinLevel[5,6] = 28;
		offlineMinLevel[5,7] = 31;
		offlineMinLevel[5,8] = 34;
		offlineMinLevel[5,9] = 37;
		
		offlineMinClass[5,0] = 2;
		offlineMinClass[5,1] = 2;
		offlineMinClass[5,2] = 2;
		offlineMinClass[5,3] = 3;
		offlineMinClass[5,4] = 3;
		offlineMinClass[5,5] = 3;
		offlineMinClass[5,6] = 3;
		offlineMinClass[5,7] = 4;
		offlineMinClass[5,8] = 4;
		offlineMinClass[5,9] = 4;
		
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
		
		offlineMinTeam[5,0] = "IND";
		offlineMinTeam[5,1] = "RWR";
		offlineMinTeam[5,2] = "FRM";
		offlineMinTeam[5,3] = "RFR";
		offlineMinTeam[5,4] = "RCR";
		offlineMinTeam[5,5] = "CGR";
		offlineMinTeam[5,6] = "SHR";
		offlineMinTeam[5,7] = "PEN";
		offlineMinTeam[5,8] = "JGR";
		offlineMinTeam[5,9] = "HEN";
		
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
		
		offlineFuel[5,0] = 6;
		offlineFuel[5,1] = 7;
		offlineFuel[5,2] = 7;
		offlineFuel[5,3] = 8;
		offlineFuel[5,4] = 8;
		offlineFuel[5,5] = 8;
		offlineFuel[5,6] = 8;
		offlineFuel[5,7] = 9;
		offlineFuel[5,8] = 9;
		offlineFuel[5,9] = 9;
				
		offlinePrizes[5,0] = "IND";
		offlinePrizes[5,1] = "RWR";
		offlinePrizes[5,2] = "FRM";
		offlinePrizes[5,3] = "RFR";
		offlinePrizes[5,4] = "RCR";
		offlinePrizes[5,5] = "CGR";
		offlinePrizes[5,6] = "SHR";
		offlinePrizes[5,7] = "PEN";
		offlinePrizes[5,8] = "JGR";
		offlinePrizes[5,9] = "HEN";
		
		//Manufacturer
		offlineSeries[6,0] = "Mustang Sally";
		offlineSeries[6,1] = "American Pie";
		offlineSeries[6,2] = "Turning Japanese";
		offlineSeries[6,3] = "Thunderbird";
		offlineSeries[6,4] = "Heavy Chevy";
		offlineSeries[6,5] = "Big In Japan";
		
		offlineSeriesImage[6,0] = "cup20livery14";
		offlineSeriesImage[6,1] = "cup20livery88";
		offlineSeriesImage[6,2] = "cup20livery96";
		offlineSeriesImage[6,3] = "cup22livery22";
		offlineSeriesImage[6,4] = "cup22livery5";
		offlineSeriesImage[6,5] = "cup22livery11";
		
		offlineDailyPlays[6,0] = 3;
		offlineDailyPlays[6,1] = 3;
		offlineDailyPlays[6,2] = 3;
		offlineDailyPlays[6,3] = 3;
		offlineDailyPlays[6,4] = 3;
		offlineDailyPlays[6,5] = 3;
		
		offlineAILevel[6,0] = 5;
		offlineAILevel[6,1] = 5;
		offlineAILevel[6,2] = 5;
		offlineAILevel[6,3] = 10;
		offlineAILevel[6,4] = 10;
		offlineAILevel[6,5] = 10;
		
		offlineMinLevel[6,0] = 15;
		offlineMinLevel[6,1] = 15;
		offlineMinLevel[6,2] = 15;
		offlineMinLevel[6,3] = 30;
		offlineMinLevel[6,4] = 30;
		offlineMinLevel[6,5] = 30;
		
		offlineMinClass[6,0] = 3;
		offlineMinClass[6,1] = 3;
		offlineMinClass[6,2] = 3;
		offlineMinClass[6,3] = 5;
		offlineMinClass[6,4] = 5;
		offlineMinClass[6,5] = 5;
		
		offlineMinType[6,0] = "Manufacturer";
		offlineMinType[6,1] = "Manufacturer";
		offlineMinType[6,2] = "Manufacturer";
		offlineMinType[6,3] = "Manufacturer";
		offlineMinType[6,4] = "Manufacturer";
		offlineMinType[6,5] = "Manufacturer";
		
		offlineMinManu[6,0] = "FRD";
		offlineMinManu[6,1] = "CHV";
		offlineMinManu[6,2] = "TYT";
		offlineMinManu[6,3] = "FRD";
		offlineMinManu[6,4] = "CHV";
		offlineMinManu[6,5] = "TYT";
		
		offlineTracklists[6,0] = "1,12,16,18,5";
		offlineTracklists[6,1] = "2,8,7,15,17";
		offlineTracklists[6,2] = "3,9,19,14,22";
		offlineTracklists[6,3] = "11,10,23,15,6";
		offlineTracklists[6,4] = "13,4,1,20,21";
		offlineTracklists[6,5] = "2,15,5,10,16";
		
		offlineFuel[6,0] = 7;
		offlineFuel[6,1] = 7;
		offlineFuel[6,2] = 7;
		offlineFuel[6,3] = 9;
		offlineFuel[6,4] = 9;
		offlineFuel[6,5] = 9;
		
		offlinePrizes[6,0] = "FRD1";
		offlinePrizes[6,1] = "CHV1";
		offlinePrizes[6,2] = "TYT1";
		offlinePrizes[6,3] = "FRD";
		offlinePrizes[6,4] = "CHV";
		offlinePrizes[6,5] = "TYT";
		
		//Type
		offlineSeries[7,0] = "Rookie Season";
		offlineSeries[7,1] = "Strategy Calls";
		offlineSeries[7,2] = "Closing In";
		offlineSeries[7,3] = "Intimidating";
		offlineSeries[7,4] = "Downright Dominate";
		offlineSeries[7,5] = "Legend Of The Sport";
		
		offlineSeriesImage[7,0] = "cup20livery14";
		offlineSeriesImage[7,1] = "cup20livery88";
		offlineSeriesImage[7,2] = "cup20livery96";
		offlineSeriesImage[7,3] = "cup20livery22";
		offlineSeriesImage[7,4] = "cup20livery43";
		offlineSeriesImage[7,5] = "cup20livery11";
		
		offlineDailyPlays[7,0] = 3;
		offlineDailyPlays[7,1] = 3;
		offlineDailyPlays[7,2] = 3;
		offlineDailyPlays[7,3] = 3;
		offlineDailyPlays[7,4] = 3;
		
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
		
		offlineFuel[7,0] = 6;
		offlineFuel[7,1] = 7;
		offlineFuel[7,2] = 7;
		offlineFuel[7,3] = 7;
		offlineFuel[7,4] = 7;
		offlineFuel[7,5] = 10;
		
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
		
		offlineSeriesImage[8,0] = "cup20livery21";
		offlineSeriesImage[8,1] = "cup20livery1";
		offlineSeriesImage[8,2] = "cup20livery48";
		
		offlineDailyPlays[8,0] = 3;
		offlineDailyPlays[8,1] = 3;
		offlineDailyPlays[8,2] = 3;
		
		offlineAILevel[8,0] = 4;
		offlineAILevel[8,1] = 8;
		offlineAILevel[8,2] = 12;
		
		offlineMinLevel[8,0] = 10;
		offlineMinLevel[8,1] = 25;
		offlineMinLevel[8,2] = 40;
		
		offlineMinClass[8,0] = 1;
		offlineMinClass[8,1] = 2;
		offlineMinClass[8,2] = 3;
		
		offlineMinType[8,0] = "Rarity";
		offlineMinType[8,1] = "Rarity";
		offlineMinType[8,2] = "Rarity";
		
		offlineMinRarity[8,0] = 1;
		offlineMinRarity[8,1] = 2;
		offlineMinRarity[8,2] = 3;
		
		offlineTracklists[8,0] = "1,12,16,18,5";
		offlineTracklists[8,1] = "2,8,7,15,17";
		offlineTracklists[8,2] = "3,9,19,14,20";
		
		offlineFuel[8,0] = 6;
		offlineFuel[8,1] = 7;
		offlineFuel[8,2] = 7;
		
		offlinePrizes[8,0] = "Rarity1";
		offlinePrizes[8,1] = "Rarity2";
		offlinePrizes[8,2] = "Rarity3";
		
		//Seasons
		offlineSeries[9,0] = "Half Season 1";
		offlineSeries[9,1] = "Half Season 2";
		offlineSeries[9,2] = "2020 Calendar Season";
		offlineSeries[9,3] = "2022 Calendar Season";
		offlineSeries[9,4] = "Test Season";
		
		offlineSeriesImage[9,0] = "cup20livery22";
		offlineSeriesImage[9,1] = "cup20livery2";
		offlineSeriesImage[9,2] = "cup20livery9";
		offlineSeriesImage[9,3] = "cup22livery5";
		offlineSeriesImage[9,4] = "cup22livery1alt1";
		
		offlineDailyPlays[9,0] = 10;
		offlineDailyPlays[9,1] = 10;
		offlineDailyPlays[9,2] = 20;
		offlineDailyPlays[9,3] = 20;
		offlineDailyPlays[9,4] = 20;
		
		offlineAILevel[9,0] = 5;
		offlineAILevel[9,1] = 8;
		offlineAILevel[9,2] = 11;
		offlineAILevel[9,3] = 14;
		offlineAILevel[9,4] = 1;
		
		offlineMinLevel[9,0] = 10;
		offlineMinLevel[9,1] = 20;
		offlineMinLevel[9,2] = 30;
		offlineMinLevel[9,3] = 35;
		offlineMinLevel[9,4] = 1;
		
		offlineMinClass[9,0] = 1;
		offlineMinClass[9,1] = 1;
		offlineMinClass[9,2] = 2;
		offlineMinClass[9,3] = 2;
		offlineMinClass[9,4] = 1;
		
		offlineTracklists[9,0] = "1,2,3,4,5,6,7,8,9,10,11";
		offlineTracklists[9,1] = "12,13,14,15,16,17,18,19,20,21";
		offlineTracklists[9,2] = "1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21";
		offlineTracklists[9,3] = "30,1,5,3,4,2,9,6,8,10,11,19,12,7,13,22,23,2,18,14,20,15,9,1,19,12,8,7,10,3,21,6,4";
		offlineTracklists[9,4] = "1,2,19";
			
		offlineFuel[9,0] = 6;
		offlineFuel[9,1] = 8;
		offlineFuel[9,2] = 10;
		offlineFuel[9,3] = 10;
		offlineFuel[9,4] = 1;
		
		offlinePrizes[9,0] = "";
		offlinePrizes[9,1] = "";
		offlinePrizes[9,2] = "";
		offlinePrizes[9,3] = "";
		offlinePrizes[9,4] = "";
		
		
		//--------Top Level---------
		
		offlineImage[0] = "cup20livery41";
		offlineImage[1] = "cup20livery11";
		offlineImage[2] = "cup22livery4";
		offlineImage[3] = "cup20livery1";
		offlineImage[4] = "cup20livery2";
		offlineImage[5] = "cup22livery22";
		offlineImage[6] = "cup22livery23";
		offlineImage[7] = "cup20livery12";
		offlineImage[8] = "cup20livery18";
		offlineImage[9] = "cup22livery5";
		
		seriesDescriptions[0] = "The first step onto the oval racing ladder. Show 'em what you've got!";
		seriesDescriptions[1] = "Learn the challenges of high speed pack racing.";
		seriesDescriptions[2] = "They say they all look the same, but the 1.5 mile oval is one you must master.";
		seriesDescriptions[3] = "Time to get physical! These tracks are tight and congested, with little room for error.";
		seriesDescriptions[4] = "Build your favourite driver's class to unlock the toughest challenges.";
		seriesDescriptions[5] = "The weight of the team rests on your shoulders. Time to deliver!";
		seriesDescriptions[6] = "Back your favourite manufacturer and take them to the top.";
		seriesDescriptions[7] = "Some drive clean, some not so much. Play to your drivers strengths here.";
		seriesDescriptions[8] = "Proven winners wear their stars with pride Rarer drivers win bigger rewards!";
		seriesDescriptions[9] = "The complete calendars. The ultimate challenge!";
	}

    // Update is called once per frame
    void Update(){
    }
	
	public static int getMaxPlays(int i,int j){
		setData();
		return offlineDailyPlays[i,j];
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
