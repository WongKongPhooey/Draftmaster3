using System.Collections;
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
	public static string[,] offlineMinManu = new string[10,10];
	public static string[,] offlineTracklists = new string[10,10];
	public static int[,] offlineFuel = new int[10,10];
	public static string[,] offlinePrizes = new string[10,10];
	
    // Start is called before the first frame update
    void Start(){
        
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
		
		offlineAILevel[0,0] = -5;
		offlineAILevel[0,0] = -5;
		offlineAILevel[0,0] = -4;
		offlineAILevel[0,0] = -4;
		offlineAILevel[0,0] = -3;
		
		offlineMinType[0,0] = "Level";
		offlineMinType[0,1] = "Level";
		offlineMinType[0,2] = "Level";
		offlineMinType[0,3] = "Level";
		offlineMinType[0,4] = "Level";
		
		offlineMinLevel[0,0] = 1;
		offlineMinLevel[0,1] = 2;
		offlineMinLevel[0,2] = 5;
		offlineMinLevel[0,3] = 7;
		offlineMinLevel[0,4] = 10;
		
		offlineTracklists[0,0] = "1,2,4";
		offlineTracklists[0,1] = "3,5,10";
		offlineTracklists[0,2] = "6,14,8";
		offlineTracklists[0,3] = "12,9,15";
		offlineTracklists[0,4] = "13,20,21";
		
		offlineFuel[0,0] = 4;
		offlineFuel[0,1] = 4;
		offlineFuel[0,2] = 4;
		offlineFuel[0,3] = 4;
		offlineFuel[0,4] = 4;
		
		offlinePrizes[0,0] = "Rookies";
		offlinePrizes[0,1] = "Rookies";
		offlinePrizes[0,2] = "Rookies";
		offlinePrizes[0,3] = "Rookies";
		offlinePrizes[0,4] = "Rookies";
		
		
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
		
		offlineAILevel[1,0] = -2;
		offlineAILevel[1,0] = 0;
		offlineAILevel[1,0] = 2;
		offlineAILevel[1,0] = 3;
		offlineAILevel[1,0] = 5;
				
		offlineMinType[1,0] = "Level";
		offlineMinType[1,1] = "Level";
		offlineMinType[1,2] = "Level";
		offlineMinType[1,3] = "Level";
		offlineMinType[1,4] = "Level";
		
		offlineMinLevel[1,0] = 5;
		offlineMinLevel[1,1] = 10;
		offlineMinLevel[1,2] = 20;
		offlineMinLevel[1,3] = 30;
		offlineMinLevel[1,4] = 40;
		
		offlineTracklists[1,0] = "1,10,5,15";
		offlineTracklists[1,1] = "1,5,10,15";
		offlineTracklists[1,2] = "5,15,10,1";
		offlineTracklists[1,3] = "15,5,10,15";
		offlineTracklists[1,4] = "10,1,15,5";
		
		offlineFuel[0,0] = 5;
		offlineFuel[0,1] = 6;
		offlineFuel[0,2] = 7;
		offlineFuel[0,3] = 8;
		offlineFuel[0,4] = 9;
		
		offlinePrizes[1,0] = "Rookies,Plate";
		offlinePrizes[1,1] = "Rookies,Plate";
		offlinePrizes[1,2] = "Rookies,Plate";
		offlinePrizes[1,3] = "Plate";
		offlinePrizes[1,4] = "Plate";
		
		
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
				
		offlineAILevel[2,0] = -2;
		offlineAILevel[2,0] = 0;
		offlineAILevel[2,0] = 2;
		offlineAILevel[2,0] = 3;
		offlineAILevel[2,0] = 5;
						
		offlineMinType[2,0] = "Level";
		offlineMinType[2,1] = "Level";
		offlineMinType[2,2] = "Level";
		offlineMinType[2,3] = "Level";
		offlineMinType[2,4] = "Level";
				
		offlineMinLevel[2,0] = 7;
		offlineMinLevel[2,1] = 12;
		offlineMinLevel[2,2] = 22;
		offlineMinLevel[2,3] = 32;
		offlineMinLevel[2,4] = 42;
		
		offlineTracklists[2,0] = "2,3,7,13";
		offlineTracklists[2,1] = "16,17,21,13";
		offlineTracklists[2,2] = "7,2,19,12";
		offlineTracklists[2,3] = "3,16,12,2";
		offlineTracklists[2,4] = "13,19,7,21";
		
		offlineFuel[0,0] = 5;
		offlineFuel[0,1] = 6;
		offlineFuel[0,2] = 7;
		offlineFuel[0,3] = 8;
		offlineFuel[0,4] = 9;
		
		offlinePrizes[2,0] = "Rookies";
		offlinePrizes[2,1] = "Rookies";
		offlinePrizes[2,2] = "Rookies,Plate";
		offlinePrizes[2,3] = "Rookies,Plate";
		offlinePrizes[2,4] = "All";
		
		//Short Tracks
		offlineSeries[3,0] = "Push To Pass";
		offlineSeries[3,1] = "Bump And Run";
		offlineSeries[3,2] = "Rubbin Is Racin";
		offlineSeries[3,3] = "Hot Headed";
		offlineSeries[3,4] = "Clean Air";
		
		offlineSeriesImage[3,0] = "cup20livery12";
		offlineSeriesImage[3,1] = "cup20livery9";
		offlineSeriesImage[3,2] = "cup20livery42";
		offlineSeriesImage[3,3] = "cup20livery1";
		offlineSeriesImage[3,4] = "cup20livery18";
		
		offlineDailyPlays[3,0] = 10;
		offlineDailyPlays[3,1] = 10;
		offlineDailyPlays[3,2] = 10;
		offlineDailyPlays[3,3] = 10;
		offlineDailyPlays[3,4] = 10;
		
		offlineAILevel[3,0] = -2;
		offlineAILevel[3,0] = 0;
		offlineAILevel[3,0] = 2;
		offlineAILevel[3,0] = 3;
		offlineAILevel[3,0] = 5;
						
		offlineMinType[3,0] = "Level";
		offlineMinType[3,1] = "Level";
		offlineMinType[3,2] = "Level";
		offlineMinType[3,3] = "Level";
		offlineMinType[3,4] = "Level";
				
		offlineMinLevel[3,0] = 7;
		offlineMinLevel[3,1] = 12;
		offlineMinLevel[3,2] = 22;
		offlineMinLevel[3,3] = 32;
		offlineMinLevel[3,4] = 42;
		
		offlineTracklists[3,0] = "4,8,6,9";
		offlineTracklists[3,1] = "18,11,6,8";
		offlineTracklists[3,2] = "9,11,4,18";
		offlineTracklists[3,3] = "6,8,18,9";
		offlineTracklists[3,4] = "8,6,11,4";
		
		//Class
		offlineSeries[4,0] = "A Fan Favourite";
		offlineSeries[4,1] = "Ol' Reliable";
		offlineSeries[4,2] = "Perfect Pair";
		offlineSeries[4,3] = "Proven Winner";
		offlineSeries[4,4] = "Unbreakable Bond";
		
		offlineSeriesImage[4,0] = "cup20livery21";
		offlineSeriesImage[4,1] = "cup20livery3";
		offlineSeriesImage[4,2] = "cup20livery47";
		offlineSeriesImage[4,3] = "cup20livery10";
		offlineSeriesImage[4,4] = "cup20livery34";
		
		//Team
		offlineSeries[5,0] = "Lone Ranger";
		offlineSeries[5,1] = "Childress Of The Sea";
		offlineSeries[5,2] = "Dibbs On Gibbs";
		offlineSeries[5,3] = "Apres Penske";
		offlineSeries[5,4] = "Show Me The Fenway";
		offlineSeries[5,5] = "Front Row Seats";
		offlineSeries[5,6] = "The Real Haastle";
		offlineSeries[5,7] = "Chip Off The Old Block";
		offlineSeries[5,8] = "The Hendrick Experience";
		
		offlineSeriesImage[5,0] = "cup20livery7";
		offlineSeriesImage[5,1] = "cup20livery8";
		offlineSeriesImage[5,2] = "cup20livery20";
		offlineSeriesImage[5,3] = "cup20livery12";
		offlineSeriesImage[5,4] = "cup20livery17";
		offlineSeriesImage[5,5] = "cup20livery1";
		offlineSeriesImage[5,6] = "cup20livery24";
		
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
		offlineSeriesImage[6,3] = "cup20livery22";
		offlineSeriesImage[6,4] = "cup20livery43";
		offlineSeriesImage[6,5] = "cup20livery11";
		
		//Type
		offlineSeries[7,0] = "Rookie Season";
		offlineSeries[7,1] = "Strategy Calls";
		offlineSeries[7,2] = "Closing In";
		offlineSeries[7,3] = "Intimidating";
		offlineSeries[7,4] = "Gentleman Driver";
		offlineSeries[7,5] = "Legend Of The Sport";
		
		offlineSeriesImage[7,0] = "cup20livery14";
		offlineSeriesImage[7,1] = "cup20livery88";
		offlineSeriesImage[7,2] = "cup20livery96";
		offlineSeriesImage[7,3] = "cup20livery22";
		offlineSeriesImage[7,4] = "cup20livery43";
		offlineSeriesImage[7,5] = "cup20livery11";
		
		//Type
		offlineSeries[8,0] = "Driver For Hire";
		offlineSeries[8,1] = "Hot Property";
		offlineSeries[8,2] = "Proven Talent";
		
		offlineSeriesImage[8,0] = "cup20livery21";
		offlineSeriesImage[8,1] = "cup20livery1";
		offlineSeriesImage[8,2] = "cup20livery48";
		
		//Seasons
		offlineSeries[9,0] = "Half Season 1";
		offlineSeries[9,1] = "Half Season 2";
		offlineSeries[9,2] = "2019 Calendar Season";
		
		offlineImage[0] = "cup20livery41";
		offlineImage[1] = "cup20livery11";
		offlineImage[2] = "cup20livery4";
		offlineImage[3] = "cup20livery1";
		offlineImage[4] = "cup20livery2";
		offlineImage[5] = "cup20livery22";
		offlineImage[6] = "cup20livery43";
		offlineImage[7] = "cup20livery12";
		offlineImage[8] = "cup20livery18";
		offlineImage[9] = "cup20livery9";
		
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
		
		offlineDescriptions[9,0] = "Season Half 1";
		offlineDescriptions[9,1] = "Season Half 2";	
		offlineDescriptions[9,2] = "Full 2019 Season";
		
		offlineMinType[4,0] = "Class";
		offlineMinType[4,1] = "Class";
		offlineMinType[4,2] = "Class";
		offlineMinType[4,3] = "Class";
		offlineMinType[4,4] = "Class";
		
		offlineMinType[5,0] = "Team";
		offlineMinType[5,1] = "Team";
		offlineMinType[5,2] = "Team";
		offlineMinType[5,3] = "Team";
		offlineMinType[5,4] = "Team";
		offlineMinType[5,5] = "Team";
		offlineMinType[5,6] = "Team";
		
		offlineMinType[6,0] = "Manufacturer";
		offlineMinType[6,1] = "Manufacturer";
		offlineMinType[6,2] = "Manufacturer";
		offlineMinType[6,3] = "Manufacturer";
		offlineMinType[6,4] = "Manufacturer";
		offlineMinType[6,5] = "Manufacturer";
		
		offlineMinType[9,0] = "Level";
		offlineMinType[9,1] = "Level";
		offlineMinType[9,2] = "Level";
		
		offlineMinLevel[3,0] = 10;
		offlineMinLevel[3,1] = 15;
		offlineMinLevel[3,2] = 25;
		offlineMinLevel[3,3] = 35;
		offlineMinLevel[3,4] = 45;
		
		offlineMinLevel[9,0] = 10;
		offlineMinLevel[9,1] = 20;
		offlineMinLevel[9,2] = 30;
		
		offlineMinClass[4,0] = 2;
		offlineMinClass[4,1] = 3;
		offlineMinClass[4,2] = 4;
		offlineMinClass[4,3] = 5;
		offlineMinClass[4,4] = 6;
		
		offlineMinTeam[5,0] = "IND";
		offlineMinTeam[5,1] = "RCR";
		offlineMinTeam[5,2] = "JGR";
		offlineMinTeam[5,3] = "PEN";
		offlineMinTeam[5,4] = "RFR";
		offlineMinTeam[5,5] = "CGR";
		offlineMinTeam[5,6] = "HEN";
		
		offlineMinManu[6,0] = "CHV";
		offlineMinManu[6,1] = "FRD";
		offlineMinManu[6,2] = "TYT";
		offlineMinManu[6,3] = "FRD";
		offlineMinManu[6,4] = "CHV";
		offlineMinManu[6,5] = "TYT";
		
		offlineTracklists[4,0] = "1,12,16,18,5";
		offlineTracklists[4,1] = "2,8,7,15,17";
		offlineTracklists[4,2] = "3,9,19,14,20";
		offlineTracklists[4,3] = "11,10,7,15,6";
		offlineTracklists[4,4] = "13,4,1,20,21";
		
		offlineTracklists[9,0] = "1,2,3,4,5,6,7,8";
		offlineTracklists[9,1] = "1,2,3,4,5,6,7,8";
		offlineTracklists[9,2] = "1,2,3,4,5,6,7,8";
		
		//Team
		offlinePrizes[5,0] = "IND";
		offlinePrizes[5,1] = "RCR";
		offlinePrizes[5,2] = "JGR";
		offlinePrizes[5,3] = "PEN";
		offlinePrizes[5,4] = "RFR";
		offlinePrizes[5,5] = "FRM";
		offlinePrizes[5,6] = "SHR";
		offlinePrizes[5,7] = "CGR";
		offlinePrizes[5,8] = "HEN";
		
		//Manufacturer
		offlinePrizes[6,0] = "FRD";
		offlinePrizes[6,1] = "CHV";
		offlinePrizes[6,2] = "TYT";
		offlinePrizes[6,3] = "FRD";
		offlinePrizes[6,4] = "CHV";
		offlinePrizes[6,5] = "TYT";
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
