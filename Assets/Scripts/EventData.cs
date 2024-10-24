﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventData : MonoBehaviour
{
    public static string[] offlineEvent = new string[10];
	public static string[] offlineEventType = new string[10];
	public static string[] offlineEventWeek = new string[10];
	public static string[,] offlineEventChapter = new string[10,15];
	public static string[] offlineEventImage = new string[10];
	public static string[,] offlineChapterImage = new string[10,15];
	public static string[] eventDescriptions = new string[10];
	public static string[,] eventChapterDescriptions = new string[10,15];
	public static int[,] offlineAILevel = new int[10,15];
	public static string[,] offlineSeries = new string[10,15];
	public static string[,] offlineMinType = new string[10,15];
	public static int[,] offlineMinLevel = new int[10,15];
	public static int[,] offlineMinClass = new int[10,15];
	public static int[,] offlineMinRarity = new int[10,15];
	public static string[,] offlineMinTeam = new string[10,15];
	public static string[,] offlineExactSeries = new string[10,15];
	public static int[,] offlineExactCar = new int[10,15];

	public static string[,] offlineExactDriver = new string[10,15];
	public static string[,] offlineMinManu = new string[10,15];
	public static string[,] offlineMinDriverType = new string[10,15];
	public static string[,] offlineTracklists = new string[10,15];
	public static string[] offlineCircuits = new string[20];
	public static string[,] offlinePrizes = new string[10,15];
	public static string[,] offlineSetPrizes = new string[10,15];
	
	public static string[,] offlineCustomCar = new string[10,15];
	public static string[,] offlineCustomField = new string[10,15];
	public static string[,] offlineCustomFieldOrder = new string[10,15];
	public static int[,] offlineRaceLaps = new int[10,15];
	public static int[,] offlineStartingLap = new int[10,15];
	public static int[,] offlineEndingLap = new int[10,15];
	public static string[,] offlineModifier = new string[10,15];
	public static string[,] offlineMoment = new string[10,15];
	
    // Start is called before the first frame update
    void Start(){   
		loadEvents();
	}
	
	public static void loadEvents() {
		offlineEvent[0] = "Ol' Seven-Time";
		offlineEvent[1] = "The Intimidator";
		offlineEvent[3] = "The Closer";
		offlineEvent[4] = "Modern Era";
		offlineEvent[5] = "Classics";
		
		switch(PlayerPrefs.GetString("SpecialEvent")){
			case "Cup22Final4":
				offlineEvent[9] = "The Final 4";
				eventDescriptions[9] = "Play out the race of each possible champion to win their alt paint!";
				break;
			default:
				break;
		}
		
		offlineEventType[0] = "Progression";
		offlineEventType[1] = "Progression";
		//offlineEventType[2] = "Replay";
		offlineEventType[3] = "Progression";
		offlineEventType[4] = "Replay";
		offlineEventType[5] = "Replay";
		offlineEventType[9] = "Replay";
		
		offlineEventWeek[0] = "4,8,12";
		offlineEventWeek[1] = "2,6,10";
		//offlineEventWeek[2] = "1,2,3,4,5,6,7,8,9,10,11,12";
		offlineEventWeek[3] = "1,5,9";
		offlineEventWeek[4] = "1,2,3,4,5,6,7,8,9,10,11,12";
		offlineEventWeek[5] = "1,2,3,4,5,6,7,8,9,10,11,12";
		offlineEventWeek[9] = "1,2,3,4,5,6,7,8,9,10,11,12";
		
		//Ol' Seven-Time
		offlineEventChapter[0,0] = "Where It All Began";
		offlineChapterImage[0,0] = "SuperTriOval";
		offlineMinClass[0,0] = 2;
		offlineMinLevel[0,0] = 5;
		offlineAILevel[0,0] = 8;
		offlineMinType[0,0] = "Team";
		offlineMinTeam[0,0] = "HEN";
		offlineTracklists[0,0] = "5";
		offlinePrizes[0,0] = "Johnson";
		offlineSetPrizes[0,0] = "10";
		eventChapterDescriptions[0,0] = "April 28, 2002. Johnson's first win came in California, beating Ku. Busch by less than 1 second.";
		
		offlineEventChapter[0,1] = "Daytona!";
		offlineChapterImage[0,1] = "SuperTriOval";
		offlineMinClass[0,1] = 3;
		offlineMinLevel[0,1] = 10;
		offlineAILevel[0,1] = 9;
		offlineMinType[0,1] = "Team";
		offlineMinTeam[0,1] = "HEN";
		offlineTracklists[0,1] = "1";
		offlinePrizes[0,1] = "Johnson";
		offlineSetPrizes[0,1] = "20";
		eventChapterDescriptions[0,1] = "Feb 19, 2006. Johnson's first Daytona 500 was won under caution, fending off Mears and Newman late on.";
		
		offlineEventChapter[0,2] = "First Of Many";
		offlineChapterImage[0,2] = "BigOval";
		offlineMinClass[0,2] = 4;
		offlineMinLevel[0,2] = 15;
		offlineAILevel[0,2] = 10;
		offlineMinType[0,2] = "Team";
		offlineMinTeam[0,2] = "HEN";
		offlineTracklists[0,2] = "21";
		offlinePrizes[0,2] = "Johnson";
		offlineSetPrizes[0,2] = "35";
		eventChapterDescriptions[0,2] = "Nov 19, 2006. Johnson earned his first championship in the final race of the season at Homestead";
		
		offlineEventChapter[0,3] = "Phoenix Nights";
		offlineChapterImage[0,3] = "cup20livery48alt1";
		offlineMinClass[0,3] = 3;
		offlineMinLevel[0,3] = 20;
		offlineAILevel[0,3] = 11;
		offlineMinType[0,3] = "Car";
		offlineExactCar[0,3] = 48;
		offlineTracklists[0,3] = "4";
		offlinePrizes[0,3] = "AltPaint";
		offlineSetPrizes[0,3] = "cup20livery48alt1";
		eventChapterDescriptions[0,3] = "Nov 11, 2007. Johnson wins his 10th race in one season on his way to his 2nd championship.";
		
		offlineEventChapter[0,4] = "Half Century";
		offlineChapterImage[0,4] = "Bristol";
		offlineMinClass[0,4] = 3;
		offlineMinLevel[0,4] = 25;
		offlineAILevel[0,4] = 12;
		offlineMinType[0,4] = "Car";
		offlineExactCar[0,4] = 48;
		offlinePrizes[0,4] = "Johnson";
		offlineTracklists[0,4] = "8";
		offlineSetPrizes[0,4] = "50";
		eventChapterDescriptions[0,4] = "Nov 11, 2007. Johnson wins his 10th race in one season on his way to his 2nd championship.";
		
		offlineEventChapter[0,5] = "Daytona! Again!";
		offlineChapterImage[0,5] = "cup20livery48alt3";
		offlineMinClass[0,5] = 4;
		offlineMinLevel[0,5] = 30;
		offlineAILevel[0,5] = 13;
		offlineMinType[0,5] = "Car";
		offlineExactCar[0,5] = 48;
		offlineTracklists[0,5] = "1";
		offlinePrizes[0,5] = "AltPaint";
		offlineSetPrizes[0,5] = "cup20livery48alt3";
		eventChapterDescriptions[0,5] = "July 6, 2013. Johnson becomes the first driver since Allison to win both Daytona races in 1 season.";
		
		offlineEventChapter[0,6] = "Serial Champion";
		offlineChapterImage[0,6] = "BigOval";
		offlineMinClass[0,6] = 4;
		offlineMinLevel[0,6] = 35;
		offlineAILevel[0,6] = 14;
		offlineMinType[0,6] = "Car";
		offlineExactCar[0,6] = 48;
		offlinePrizes[0,6] = "Johnson";
		offlineTracklists[0,6] = "21";
		offlineSetPrizes[0,6] = "70";
		eventChapterDescriptions[0,6] = "Nov 20, 2016. Johnson takes his 7th title, tying him with Earnhardt and Petty for the most titles.";
		
		offlineEventChapter[0,7] = "Eleventh Heaven";
		offlineChapterImage[0,7] = "cup20livery48alt2";
		offlineMinClass[0,7] = 5;
		offlineMinLevel[0,7] = 40;
		offlineAILevel[0,7] = 15;
		offlineMinType[0,7] = "Car";
		offlineExactCar[0,7] = 48;
		offlineTracklists[0,7] = "11";
		offlinePrizes[0,7] = "AltPaint";
		offlineSetPrizes[0,7] = "cup20livery48alt2";
		eventChapterDescriptions[0,7] = "June 4, 2017. Johnson wins in Dover for the 11th time, his 83rd and last Cup series win.";
		
		//The Intimidator
		offlineEventChapter[1,0] = "The Winston Way";
		offlineChapterImage[1,0] = "TriOval";
		offlineMinClass[1,0] = 2;
		offlineMinLevel[1,0] = 5;
		offlineAILevel[1,0] = 8;
		offlineMinType[1,0] = "Team";
		offlineMinTeam[1,0] = "IND";
		offlineTracklists[1,0] = "13";
		offlinePrizes[1,0] = "Earnhardt";
		offlineSetPrizes[1,0] = "10";
		eventChapterDescriptions[1,0] = "May 25, 1975. Earnhardt makes his debut at the 1975 World 600, in Charlotte NC.";
		
		offlineEventChapter[1,1] = "South Southeast";
		offlineChapterImage[1,1] = "Bristol";
		offlineMinClass[1,1] = 3;
		offlineMinLevel[1,1] = 10;
		offlineAILevel[1,1] = 9;
		offlineMinType[1,1] = "Team";
		offlineMinTeam[1,1] = "IND";
		offlineTracklists[1,1] = "8";
		offlinePrizes[1,1] = "Earnhardt";
		offlineSetPrizes[1,1] = "15";
		eventChapterDescriptions[1,1] = "April 1, 1979. Earnhardt's first win comes at Bristol in the Southeastern 500.";
		
		offlineEventChapter[1,2] = "Sophomore Season";
		offlineChapterImage[1,2] = "LongOval";
		offlineMinClass[1,2] = 4;
		offlineMinLevel[1,2] = 15;
		offlineAILevel[1,2] = 10;
		offlineMinType[1,2] = "Team";
		offlineMinTeam[1,2] = "IND";
		offlineTracklists[1,2] = "6";
		offlinePrizes[1,2] = "Earnhardt";
		offlineSetPrizes[1,2] = "20";
		eventChapterDescriptions[1,2] = "September 28, 1980. Earnhardt gets his 4th of 5 wins, and later his first Cup series in only his 2nd season.";
		
		offlineEventChapter[1,3] = "RCR Enterprise";
		offlineChapterImage[1,3] = "cup20livery3alt3";
		offlineMinClass[1,3] = 3;
		offlineMinLevel[1,3] = 20;
		offlineAILevel[1,3] = 11;
		offlineMinType[1,3] = "Team";
		offlineMinTeam[1,3] = "RCR";
		offlineTracklists[1,3] = "2";
		offlinePrizes[1,3] = "AltPaint";
		offlineSetPrizes[1,3] = "cup20livery3alt3";
		eventChapterDescriptions[1,3] = "Nov 2, 1986. Earnhardt wins his 2nd championship in style with a race to spare, his first with RCR.";
		
		offlineEventChapter[1,4] = "Pass In The Grass";
		offlineChapterImage[1,4] = "TriOval";
		offlineMinClass[1,4] = 3;
		offlineMinLevel[1,4] = 25;
		offlineAILevel[1,4] = 12;
		offlineMinType[1,4] = "Car";
		offlineExactCar[1,4] = 3;
		offlinePrizes[1,4] = "Earnhardt";
		offlineTracklists[1,4] = "13";
		offlineSetPrizes[1,4] = "25";
		eventChapterDescriptions[1,4] = "May 17, 1987. The Intimidator makes the iconic 'Pass In The Grass' and goes on to win the All-Star race.";
		
		offlineEventChapter[1,5] = "It Hurt So Good";
		offlineChapterImage[1,5] = "cup20livery3alt4";
		offlineMinClass[1,5] = 4;
		offlineMinLevel[1,5] = 30;
		offlineAILevel[1,5] = 13;
		offlineMinType[1,5] = "Car";
		offlineExactCar[1,5] = 3;
		offlineTracklists[1,5] = "20";
		offlinePrizes[1,5] = "AltPaint";
		offlineSetPrizes[1,5] = "cup20livery3alt4";
		eventChapterDescriptions[1,5] = "Aug 3, 1996. 1 week after breaking his collarbone, sternum, and shoulder, Earnhardt starts the Brickyard 500.";
		
		offlineEventChapter[1,6] = "20th Time Lucky";
		offlineChapterImage[1,6] = "BigOval";
		offlineMinClass[1,6] = 5;
		offlineMinLevel[1,6] = 35;
		offlineAILevel[1,6] = 14;
		offlineMinType[1,6] = "Car";
		offlineExactCar[1,6] = 3;
		offlinePrizes[1,6] = "Earnhardt";
		offlineTracklists[1,6] = "1";
		offlineSetPrizes[1,6] = "30";
		eventChapterDescriptions[1,6] = " Feb 15, 1998. Earnhardt finally wins the Daytona 500 in his 20th attempt.";
		
		offlineEventChapter[1,7] = "Talladega Tenth";
		offlineChapterImage[1,7] = "cup20livery3alt2";
		offlineMinClass[1,7] = 5;
		offlineMinLevel[1,7] = 40;
		offlineAILevel[1,7] = 15;
		offlineMinType[1,7] = "Car";
		offlineExactCar[1,7] = 3;
		offlineTracklists[1,7] = "10";
		offlinePrizes[1,7] = "AltPaint";
		offlineSetPrizes[1,7] = "cup20livery3alt2";
		eventChapterDescriptions[1,7] = "Oct 15, 2000. Earnhardt passes 17 cars in the last 6 laps to take his final ever win, and his 10th at Talladega.";
		
		//All Stars
		/*offlineEventChapter[2,0] = "Gladiators";
		offlineChapterImage[2,0] = "TinyOval";
		offlineMinClass[2,0] = 1;
		offlineMinLevel[2,0] = 1;
		offlineAILevel[2,0] = 12;
		offlineSeries[2,0] = "cup22";
		offlineCustomField[2,0] = "cup22AllStar";
		offlineTracklists[2,0] = "30";
		offlinePrizes[2,0] = "1Star";
		eventChapterDescriptions[2,0] = "February 6th, 2022. The next generation starts here. Let battle commence in the Coliseum.";
		*/
		
		//The Closer (Harvick)
		offlineEventChapter[3,0] = "Big Boots To Fill";
		offlineChapterImage[3,0] = "cup01livery29";
		offlineMinClass[3,0] = 1;
		offlineMinLevel[3,0] = 1;
		offlineAILevel[3,0] = 12;
		offlineSeries[3,0] = "cup01";
		offlineCustomCar[3,0] = "cup01livery29";
		offlineCustomField[3,0] = "cup01MomentsAtlanta";
		offlineRaceLaps[3,0] = 326;
		offlineStartingLap[3,0] = 324;
		offlineMoment[3,0] = "Atlanta01";
		offlineTracklists[3,0] = "2";
		offlinePrizes[3,0] = "Harvick20";
		offlineSetPrizes[3,0] = "30";
		offlineCustomFieldOrder[3,0] = "x,player,x,x,x,x,24";
		eventChapterDescriptions[3,0] = "Mar 11, 2001. Harvick picks up a dramatic first win in only his 3rd start, going toe to toe with Gordon in Atlanta.";
		
		offlineEventChapter[3,1] = "The Yard Of Bricks";
		offlineChapterImage[3,1] = "Indianapolis";
		offlineMinClass[3,1] = 2;
		offlineMinLevel[3,1] = 3;
		offlineMinType[3,1] = "Team";
		offlineMinTeam[3,1] = "RCR";
		offlineAILevel[3,1] = 4;
		offlineRaceLaps[3,1] = 161;
		offlineStartingLap[3,1] = 151;
		offlineTracklists[3,1] = "20";
		offlinePrizes[3,1] = "Harvick20";
		offlineSetPrizes[3,1] = "35";
		eventChapterDescriptions[3,1] = "Aug 3, 2003. Harvick's first crown jewel victory, winning the Brickyard 400 for RCR.";
		
		offlineEventChapter[3,2] = "The Great American";
		offlineChapterImage[3,2] = "SuperTriOval";
		offlineMinClass[3,2] = 3;
		offlineMinLevel[3,2] = 5;
		offlineMinType[3,2] = "Driver";
		offlineExactDriver[3,2] = "Harvick";
		offlineAILevel[3,2] = 7;
		offlineRaceLaps[3,2] = 201;
		offlineStartingLap[3,2] = 191;
		offlineTracklists[3,2] = "1";
		offlinePrizes[3,2] = "Harvick23";
		offlineSetPrizes[3,2] = "35";
		eventChapterDescriptions[3,2] = "Feb 18, 2007. As the field wrecked behind them, Harvick edged out Martin to win the 500 by a nose.";
		
		offlineEventChapter[3,3] = "The Tandem Tango";
		offlineChapterImage[3,3] = "Talladega";
		offlineMinClass[3,3] = 3;
		offlineMinLevel[3,3] = 10;
		offlineMinType[3,3] = "Driver";
		offlineExactDriver[3,3] = "Harvick";
		offlineAILevel[3,3] = 10;
		offlineRaceLaps[3,3] = 201;
		offlineStartingLap[3,3] = 191;
		offlineTracklists[3,3] = "10";
		offlinePrizes[3,3] = "Harvick23";
		offlineSetPrizes[3,3] = "40";
		eventChapterDescriptions[3,3] = "Apr 25, 2010. The most lead changes in history and the longest Talladega race ever belonged to Harvick.";

		offlineEventChapter[3,4] = "Heartbreaker";
		offlineChapterImage[3,4] = "cup23livery4alt1";
		offlineMinClass[3,4] = 3;
		offlineMinLevel[3,4] = 15;
		offlineSeries[3,4] = "cup23";
		offlineCustomCar[3,4] = "cup23livery4";
		offlineAILevel[3,4] = 11;
		offlineRaceLaps[3,4] = 401;
		offlineStartingLap[3,4] = 386;
		offlineTracklists[3,4] = "13";
		offlinePrizes[3,4] = "AltPaint";
		offlineSetPrizes[3,4] = "cup23livery4alt1";
		eventChapterDescriptions[3,4] = "May 29, 2011. With 1 turn to go, Harvick swept past an out of gas Earnhardt Jr to win the Coke 600.";

		offlineEventChapter[3,5] = "Clash Of Titans";
		offlineChapterImage[3,5] = "SuperTriOval";
		offlineMinClass[3,5] = 3;
		offlineMinLevel[3,5] = 20;
		offlineMinType[3,5] = "Driver";
		offlineExactDriver[3,5] = "Harvick";
		offlineAILevel[3,5] = 12;
		offlineRaceLaps[3,5] = 201;
		offlineStartingLap[3,5] = 192;
		offlineTracklists[3,5] = "5";
		offlinePrizes[3,5] = "Harvick23";
		offlineSetPrizes[3,5] = "40";
		eventChapterDescriptions[3,5] = "Mar 27, 2011. A brave pass around the outside of Johnson on the final lap sealed the win at Auto Club.";


		offlineEventChapter[3,6] = "Win And In";
		offlineChapterImage[3,6] = "Phoenix";
		offlineMinClass[3,6] = 4;
		offlineMinLevel[3,6] = 25;
		offlineMinType[3,6] = "Driver";
		offlineExactDriver[3,6] = "Harvick";
		offlineAILevel[3,6] = 14;
		offlineRaceLaps[3,6] = 313;
		offlineStartingLap[3,6] = 292;
		offlineTracklists[3,6] = "4";
		offlinePrizes[3,6] = "Harvick22";
		offlineSetPrizes[3,6] = "35";
		eventChapterDescriptions[3,6] = "Nov 9, 2014. In the first elimination chase season, Harvick booked a championship 4 place at Phoenix.";

		offlineEventChapter[3,7] = "Chase For The Cup";
		offlineChapterImage[3,7] = "cup23livery4alt2";
		offlineMinClass[3,7] = 4;
		offlineMinLevel[3,7] = 30;
		offlineMinType[3,7] = "Driver";
		offlineExactDriver[3,7] = "Harvick";
		offlineAILevel[3,7] = 15;
		offlineRaceLaps[3,7] = 268;
		offlineStartingLap[3,7] = 265;
		offlineTracklists[3,7] = "21";
		offlinePrizes[3,7] = "AltPaint";
		offlineSetPrizes[3,7] = "cup23livery4alt2";
		eventChapterDescriptions[3,7] = "Nov 16, 2014. Harvick soaked up the pressure of a late caution to win the championship in Miami.";


		offlineEventChapter[3,8] = "Superman";
		offlineChapterImage[3,8] = "dm2selivery4";
		offlineMinClass[3,8] = 4;
		offlineMinLevel[3,8] = 35;
		offlineAILevel[3,8] = 15;
		offlineSeries[3,8] = "dm2se";
		offlineCustomCar[3,8] = "dm2selivery4";
		offlineCustomFieldOrder[3,8] = "x,player,x,x,x,x,x,x,19";
		offlineCustomField[3,8] = "dm2sePhoenix16";
		offlineRaceLaps[3,8] = 313;
		offlineStartingLap[3,8] = 311;
		offlineMoment[3,8] = "Phoenix16";
		offlineTracklists[3,8] = "4";
		offlinePrizes[3,8] = "Harvick22";
		offlineSetPrizes[3,8] = "40";
		eventChapterDescriptions[3,8] = "Mar 13, 2016. A tense late restart on old tyres saw Harvick defend from a charging Edwards in Phoenix.";

		offlineEventChapter[3,9] = "Cloud Nine";
		offlineChapterImage[3,9] = "TinyOval";
		offlineMinClass[3,9] = 4;
		offlineMinLevel[3,9] = 40;
		offlineMinType[3,9] = "Driver";
		offlineExactDriver[3,9] = "Harvick";
		offlineAILevel[3,9] = 15;
		offlineRaceLaps[3,9] = 501;
		offlineStartingLap[3,9] = 420;
		offlineTracklists[3,9] = "8";
		offlinePrizes[3,9] = "Harvick22";
		offlineSetPrizes[3,9] = "40";
		eventChapterDescriptions[3,9] = "Sep 19, 2020. Harvick took his 9th win of a season where the playoff format denied him a 2nd title.";


		offlineEventChapter[3,10] = "Vintage Harvick";
		offlineChapterImage[3,10] = "cup22livery4alt1";
		offlineMinClass[3,10] = 3;
		offlineMinLevel[3,10] = 45;
		offlineSeries[3,10] = "cup22";
		offlineCustomCar[3,10] = "cup22livery4";
		offlineAILevel[3,10] = 15;
		offlineRaceLaps[3,10] = 401;
		offlineStartingLap[3,10] = 256;
		offlineTracklists[3,10] = "9";
		offlinePrizes[3,10] = "AltPaint";
		offlineSetPrizes[3,10] = "cup22livery4alt1";
		eventChapterDescriptions[3,10] = "Harvick grabbed his final ever race victory at Richmond in 2022 in typically dominant style.";


		//Modern Era
		offlineEventChapter[4,1] = "Chastain's Wallride";
		offlineChapterImage[4,1] = "cup22livery1alt2";
		offlineMinClass[4,1] = 1;
		offlineMinLevel[4,1] = 1;
		offlineAILevel[4,1] = 10;
		offlineSeries[4,1] = "cup22";
		offlineCustomCar[4,1] = "cup22livery1";
		offlineCustomField[4,1] = "cup22Chastain";
		offlineStartingLap[4,1] = 27;
		offlineModifier[4,1] = "wallride";
		offlineRaceLaps[4,1] = 501;
		offlineStartingLap[4,1] = 500;
		offlineMoment[4,1] = "ChastainWallride";
		offlineTracklists[4,1] = "6";
		offlinePrizes[4,1] = "1Star";
		offlineCustomFieldOrder[4,1] = "20,x,x,x,x,x,x,x,5,x,x,x,x,x,2,x,x,x,x,x,6,x,x,x,11,x,x,x,22,x,24,x,x,x,45,x,x,x,14,x,player,x,9,x,x,x,21";
		eventChapterDescriptions[4,1] = "October 30th 2022, Chastain goes full speed along the wall to qualify for the playoffs final.";
		
		offlineEventChapter[4,2] = "Wide Open Wheels";
		offlineChapterImage[4,2] = "irl23livery2";
		offlineMinClass[4,2] = 1;
		offlineMinLevel[4,2] = 1;
		offlineAILevel[4,2] = 10;
		offlineSeries[4,2] = "irl23";
		offlineCustomCar[4,2] = "irl23livery2";
		offlineStartingLap[4,2] = 14;
		offlineMoment[4,2] = "WideOpenWheels";
		offlineTracklists[4,2] = "7";
		offlinePrizes[4,2] = "1Star";
		offlineCustomFieldOrder[4,2] = "player,x,5,x,x,x,10,x,18";
		eventChapterDescriptions[4,2] = "April 2nd 2023, Newgarden wins out after a thrilling 4 car battle for the win in Texas.";
		
		offlineEventChapter[4,3] = "A Photo For Three";
		offlineChapterImage[4,3] = "cup24livery99";
		offlineMinClass[4,3] = 1;
		offlineMinLevel[4,3] = 1;
		offlineAILevel[4,3] = 1;
		offlineSeries[4,3] = "cup24";
		offlineCustomCar[4,3] = "cup24livery99";
		offlineRaceLaps[4,3] = 261;
		offlineStartingLap[4,3] = 259;
		offlineMoment[4,3] = "PhotoForThree";
		offlineTracklists[4,3] = "2";
		offlinePrizes[4,3] = "AltPaint";
		offlineSetPrizes[4,3] = "cup24livery99alt2";
		offlineCustomFieldOrder[4,3] = "12,x,8,x,player,x,x,2,23,x,47,1,x,34,x,54,19,17,x,7,41,x,x,31,21,15,x,24,9";
		eventChapterDescriptions[4,3] = "February 25th 2024, an incredible 3-wide finish at Atlanta sees Suarez win by 0.003s.";
		
		/*offlineEventChapter[4,2] = "Austin As Moses";
		offlineChapterImage[4,2] = "cup22livery3";
		offlineMinClass[4,2] = 1;
		offlineMinLevel[4,2] = 1;
		offlineAILevel[4,2] = 12;
		offlineSeries[4,2] = "cup22";
		offlineCustomCar[4,2] = "cup22livery3";
		offlineModifier[4,2] = "suddenshower";
		offlineMoment[4,2] = "DaytonaRain22";
		offlineTracklists[4,2] = "1";
		offlinePrizes[4,2] = "1Star";
		eventChapterDescriptions[4,2] = "August 28th 2022, everyone hits the rain and spins, except A.Dillon, who moves from 16th to 1st.";*/
		
		if(PlayerPrefs.GetString("MomentName") != ""){
			//There's a Live Moment to add
			offlineEventChapter[4,9] = PlayerPrefs.GetString("MomentName");
			offlineChapterImage[4,9] = PlayerPrefs.GetString("MomentImage");
			offlineMinClass[4,9] = 1;
			offlineMinLevel[4,9] = 1;
			//Debug.Log("Live Moment AI Level: " + PlayerPrefs.GetString("MomentAILevel"));
			offlineAILevel[4,9] = int.Parse(PlayerPrefs.GetString("MomentAILevel"));
			offlineSeries[4,9] = PlayerPrefs.GetString("MomentSeries");
			offlineCustomCar[4,9] = PlayerPrefs.GetString("MomentCar");
			offlineStartingLap[4,9] = int.Parse(PlayerPrefs.GetString("MomentLap"));
			offlineModifier[4,9] = "delicate";
			offlineMoment[4,9] = "LiveMoment";
			offlineTracklists[4,9] = PlayerPrefs.GetString("MomentTrack");
			offlinePrizes[4,9] = "1Star";
			eventChapterDescriptions[4,9] = PlayerPrefs.GetString("MomentDescription");
		} else {
			Debug.Log("No Live Moment Currently");
		}
		
		//Classics
		offlineEventChapter[5,0] = "Daytona '79";
		offlineChapterImage[5,0] = "cup79livery11";
		offlineMinClass[5,0] = 1;
		offlineMinLevel[5,0] = 1;
		offlineAILevel[5,0] = 5;
		offlineSeries[5,0] = "cup79";
		offlineCustomCar[5,0] = "cup79livery11";
		offlineCustomField[5,0] = "cup79MomentsDaytona";
		offlineModifier[5,0] = "delicate";
		offlineRaceLaps[5,0] = 201;
		offlineStartingLap[5,0] = 200;
		offlineMoment[5,0] = "Daytona79";
		offlineTracklists[5,0] = "1";
		offlinePrizes[5,0] = "1Star";
		offlineCustomFieldOrder[5,0] = "1,x,x,x,player,x,x,x,x,x,x,x,x,x,x,x,x,x,x,x,x,x,43";
		eventChapterDescriptions[5,0] = "February 18th, 1979. Allison and Yarborough collide, gifting Petty the win.";
		
		offlineEventChapter[5,1] = "Craven Got Him";
		offlineChapterImage[5,1] = "cup03livery32";
		offlineMinClass[5,1] = 1;
		offlineMinLevel[5,1] = 1;
		offlineAILevel[5,1] = 10;
		offlineSeries[5,1] = "cup03";
		offlineCustomCar[5,1] = "cup03livery32";
		offlineCustomField[5,1] = "MomentsDarlington03";
		offlineModifier[5,1] = "delicate";
		offlineRaceLaps[5,1] = 293;
		offlineStartingLap[5,1] = 292;
		offlineMoment[5,1] = "Darlington03";
		offlineTracklists[5,1] = "19";
		offlinePrizes[5,1] = "1Star";
		offlineCustomFieldOrder[5,1] = "97,x,x,x,player";
		eventChapterDescriptions[5,1] = "March 16th, 2003. Craven beats and bangs with Busch across the line, winning by 0.002s.";
		
		offlineEventImage[0] = "cup20livery48";
		offlineEventImage[1] = "cup20livery3alt2";
		offlineEventImage[2] = "cup22livery45";
		offlineEventImage[3] = "cup22livery4";
		offlineEventImage[4] = "cup22livery1alt2";
		offlineEventImage[5] = "cup79livery1";
		offlineEventImage[9] = "cup22livery1alt1";
		
		eventDescriptions[0] = "Relive Johnson's best moments leading to an incredible 7 championships.";
		eventDescriptions[1] = "Do it for Dale! Revisit some of Earnhardt's finest drives.";
		eventDescriptions[2] = "Touchdown in LA. The new season starts here.";
		eventDescriptions[3] = "Play out the biggest races in Harvick's career.";
		eventDescriptions[4] = "Memorable races from recent years.";
		eventDescriptions[5] = "Reliving famous wrecks and familiar fueds.";
		
	}

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public static List<string> ListRewards(string category){
		List<string> validDriver = new List<string>();
		switch(category){
			case "Rookies":
				//for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
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
					for(int j=0;j<99;j++){
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
					for(int j=0;j<99;j++){
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
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 3){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("3* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity4":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
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
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
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
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getManufacturer(seriesPrefix,j) == category){
								if(DriverNames.getRarity(seriesPrefix,j) == 1){
									validDriver.Add("" + seriesPrefix + j + "");
									//Debug.Log(category + " Added: #" + i);
								}
							}
						}
					}
				}
			break;
			
			//Team Rewards
			case "IND":
			case "RWR":
			case "FRM":
			case "RFR":
			case "RCR":
			case "CGR":
			case "SHR":
			case "PEN":
			case "JGR":
			case "HEN":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
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
			case "Blocker":
			case "Dominator":
			case "Legend":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getType(seriesPrefix,j) == category){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log(category + " Added: #" + i);
							}
						}
					}
				}
			break;
			
			//Event Specific
			case "Johnson":
				validDriver.Add("cup2048");
			break;
			case "Earnhardt":
				validDriver.Add("cup203");
			break;
			case "Harvick20":
				validDriver.Add("cup204");
			break;
			case "Harvick22":
				validDriver.Add("cup224");
			break;
			case "Harvick23":
				validDriver.Add("cup234");
			break;
			
			default:
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
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
}
