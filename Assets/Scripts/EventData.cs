using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventData : MonoBehaviour
{
    public static string[] offlineEvent = new string[10];
	public static string[] offlineEventType = new string[10];
	public static string[] offlineEventWeek = new string[10];
	public static string[,] offlineEventChapter = new string[10,10];
	public static string[] offlineEventImage = new string[10];
	public static string[,] offlineChapterImage = new string[10,10];
	public static string[] eventDescriptions = new string[10];
	public static string[,] eventChapterDescriptions = new string[10,10];
	public static int[,] offlineAILevel = new int[10,10];
	public static string[,] offlineSeries = new string[10,10];
	public static string[,] offlineMinType = new string[10,10];
	public static int[,] offlineMinLevel = new int[10,10];
	public static int[,] offlineMinClass = new int[10,10];
	public static int[,] offlineMinRarity = new int[10,10];
	public static string[,] offlineMinTeam = new string[10,10];
	public static string[,] offlineExactSeries = new string[10,10];
	public static int[,] offlineExactCar = new int[10,10];
	public static string[,] offlineMinManu = new string[10,10];
	public static string[,] offlineMinDriverType = new string[10,10];
	public static string[,] offlineTracklists = new string[10,10];
	public static string[] offlineCircuits = new string[20];
	public static string[,] offlinePrizes = new string[10,10];
	public static string[,] offlineSetPrizes = new string[10,10];
	
	public static string[,] offlineCustomCar = new string[10,10];
	public static string[,] offlineCustomField = new string[10,10];
	public static string[,] offlineCustomFieldOrder = new string[10,10];
	public static int[,] offlineStartingLap = new int[10,10];
	public static string[,] offlineModifier = new string[10,10];
	public static string[,] offlineMoment = new string[10,10];
	
    // Start is called before the first frame update
    void Start(){   
		loadEvents();
	}
	
	public static void loadEvents() {
		offlineEvent[0] = "Ol' Seven-Time";
		offlineEvent[1] = "The Intimidator";
		offlineEvent[2] = "All Stars";
		offlineEvent[3] = "The Closer";
		offlineEvent[4] = "Moments";
		
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
		offlineEventType[2] = "Replay";
		offlineEventType[3] = "Progression";
		offlineEventType[4] = "Replay";
		offlineEventType[9] = "Replay";
		
		offlineEventWeek[0] = "4,8,12";
		offlineEventWeek[1] = "2,6,10";
		offlineEventWeek[2] = "1,2,3,4,5,6,7,8,9,10,11,12";
		offlineEventWeek[3] = "1,5,9";
		offlineEventWeek[4] = "1,2,3,4,5,6,7,8,9,10,11,12";
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
		offlineEventChapter[2,0] = "Gladiators";
		offlineChapterImage[2,0] = "TinyOval";
		offlineMinClass[2,0] = 1;
		offlineMinLevel[2,0] = 1;
		offlineAILevel[2,0] = 12;
		offlineSeries[2,0] = "cup22";
		offlineCustomField[2,0] = "cup22AllStar";
		offlineTracklists[2,0] = "30";
		offlinePrizes[2,0] = "1Star";
		eventChapterDescriptions[2,0] = "February 6th, 2022. The next generation starts here. Let battle commence in the Coliseum.";
		
		//The Closer (Harvick)
		offlineEventChapter[3,0] = "Big Boots To Fill";
		offlineChapterImage[3,0] = "cup01livery29";
		offlineMinClass[3,0] = 2;
		offlineMinLevel[3,0] = 10;
		offlineAILevel[3,0] = 5;
		offlineMinType[3,0] = "Type";
		offlineMinDriverType[3,0] = "Rookie";
		offlineTracklists[3,0] = "2";
		offlinePrizes[3,0] = "Harvick";
		offlineSetPrizes[3,0] = "15";
		eventChapterDescriptions[3,0] = "Harvick steps into the RCR #29, following Earnhardt's untimely death.";
		
		offlineEventChapter[3,1] = "Rookie Of The Year";
		offlineChapterImage[3,1] = "AngledTriOval";
		offlineMinClass[3,1] = 2;
		offlineMinLevel[3,1] = 15;
		offlineAILevel[3,1] = 7;
		offlineMinType[3,1] = "Rarity";
		offlineMinRarity[3,1] = 2;
		offlineSeries[3,1] = "cup01";
		offlineCustomField[3,1] = "cup01HarvickGordon";
		offlineTracklists[3,1] = "3";
		offlinePrizes[3,1] = "Harvick";
		offlineSetPrizes[3,1] = "15";
		eventChapterDescriptions[3,1] = "Harvick picks up a dramatic first win in only his 3rd start, going toe to toe with Gordon in Atlanta.";
		
		/*offlineEventChapter[1,2] = "Sophomore Season";
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
		eventChapterDescriptions[1,5] = "Aug 3, 1996. A week after breaking his collarbone, sternum, and shoulder at Talladega, Earnhardt starts the Brickyard 500.";
		
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
		*/
		
		//Moments
		offlineEventChapter[4,0] = "Daytona '79";
		offlineChapterImage[4,0] = "cup79livery11";
		offlineMinClass[4,0] = 1;
		offlineMinLevel[4,0] = 1;
		offlineAILevel[4,0] = 10;
		offlineSeries[4,0] = "cup79";
		offlineCustomCar[4,0] = "cup79livery11";
		offlineCustomField[4,0] = "cup79MomentsDaytona";
		offlineStartingLap[4,0] = 12;
		offlineModifier[4,0] = "delicate";
		offlineMoment[4,0] = "Daytona79";
		offlineTracklists[4,0] = "1";
		offlinePrizes[4,0] = "Rookies";
		offlineCustomFieldOrder[4,0] = "1,x,x,x,player,x,x,x,x,x,x,x,x,x,43";
		eventChapterDescriptions[4,0] = "February 18th, 1979. Allison and Yarborough collide, gifting Petty the win.";
		
		offlineEventChapter[4,1] = "Chastain's Wallride";
		offlineChapterImage[4,1] = "cup22livery1alt2";
		offlineMinClass[4,1] = 1;
		offlineMinLevel[4,1] = 1;
		offlineAILevel[4,1] = 10;
		offlineSeries[4,1] = "cup22";
		offlineCustomCar[4,1] = "cup22livery1";
		offlineModifier[4,1] = "wallride";
		offlineMoment[4,1] = "ChastainWallride";
		offlineTracklists[4,1] = "6";
		offlinePrizes[4,1] = "Rookies";
		eventChapterDescriptions[4,1] = "October 30th 2022, Chastain goes full speed along the wall to qualify for the playoffs final.";
		
		offlineEventChapter[4,2] = "Austin As Moses";
		offlineChapterImage[4,2] = "cup22livery3";
		offlineMinClass[4,2] = 1;
		offlineMinLevel[4,2] = 1;
		offlineAILevel[4,2] = 12;
		offlineSeries[4,2] = "cup22";
		offlineCustomCar[4,2] = "cup22livery3";
		offlineModifier[4,2] = "suddenshower";
		offlineMoment[4,2] = "DaytonaRain22";
		offlineTracklists[4,2] = "1";
		offlinePrizes[4,2] = "Rookies";
		eventChapterDescriptions[4,2] = "August 28th 2022, everyone hits the rain and spins, except A.Dillon, who moves from 16th to 1st.";
		
		
		//Special Event - Final 4
		offlineEventChapter[9,0] = "Unlock Logano";
		offlineChapterImage[9,0] = "cup22livery22";
		offlineMinClass[9,0] = 1;
		offlineMinLevel[9,0] = 1;
		offlineAILevel[9,0] = 6;
		offlineExactSeries[9,0] = "cup22";
		offlineMinType[9,0] = "Manufacturer";
		offlineMinManu[9,0] = "FRD";
		offlinePrizes[9,0] = "Logano";
		offlineTracklists[9,0] = "3";
		offlineSetPrizes[9,0] = "65";
		eventChapterDescriptions[9,0] = "Logano's win in Las Vegas guaranteed his place in the final 4. Replicate it with any FRD car.";
		
		offlineEventChapter[9,1] = "Unlock Elliott";
		offlineChapterImage[9,1] = "cup22livery9";
		offlineMinClass[9,1] = 1;
		offlineMinLevel[9,1] = 1;
		offlineAILevel[9,1] = 6;
		offlineExactSeries[9,1] = "cup22";
		offlineMinType[9,1] = "Manufacturer";
		offlineMinManu[9,1] = "CHV";
		offlinePrizes[9,1] = "Elliott";
		offlineTracklists[9,1] = "23";
		offlineSetPrizes[9,1] = "65";
		eventChapterDescriptions[9,1] = "Elliott won the regular points season, including this win in Nashville. Copy him in a CHV car.";
		
		offlineEventChapter[9,2] = "Unlock Bell";
		offlineChapterImage[9,2] = "cup22livery20";
		offlineMinClass[9,2] = 1;
		offlineMinLevel[9,2] = 1;
		offlineAILevel[9,2] = 6;
		offlineExactSeries[9,2] = "cup22";
		offlineMinType[9,2] = "Manufacturer";
		offlineMinManu[9,2] = "TYT";
		offlinePrizes[9,2] = "Bell";
		offlineTracklists[9,2] = "18";
		offlineSetPrizes[9,2] = "65";
		eventChapterDescriptions[9,2] = "Bell's win at New Hampshire saw him reach the playoffs. Do the same in any TYT car.";
		
		offlineEventChapter[9,3] = "Unlock Chastain";
		offlineChapterImage[9,3] = "cup22livery1";
		offlineMinClass[9,3] = 1;
		offlineMinLevel[9,3] = 1;
		offlineAILevel[9,3] = 6;
		offlineExactSeries[9,3] = "cup22";
		offlineMinType[9,3] = "Team";
		offlineMinTeam[9,3] = "TRK";
		offlinePrizes[9,3] = "Chastain";
		offlineTracklists[9,3] = "10";
		offlineSetPrizes[9,3] = "65";
		eventChapterDescriptions[9,3] = "Chastain got his first plate track win at Talladega. Get a TRK team car and win yourself.";
		
		
		offlineEventChapter[9,4] = "Joy For Joey";
		offlineChapterImage[9,4] = "cup22livery22alt1";
		offlineMinClass[9,4] = 1;
		offlineMinLevel[9,4] = 1;
		offlineAILevel[9,4] = 12;
		offlineMinType[9,4] = "Car";
		offlineExactSeries[9,4] = "cup22";
		offlineExactCar[9,4] = 22;
		offlineTracklists[9,4] = "4";
		offlinePrizes[9,4] = "AltPaint";
		offlineSetPrizes[9,4] = "cup22livery22alt1";
		eventChapterDescriptions[9,4] = "Logano can win a 2nd Cup series championship, following his success in 2018. Requires Cup '22 Logano to enter.";
		
		offlineEventChapter[9,5] = "Chase For The Cup";
		offlineChapterImage[9,5] = "cup22livery9alt1";
		offlineMinClass[9,5] = 1;
		offlineMinLevel[9,5] = 1;
		offlineAILevel[9,5] = 12;
		offlineMinType[9,5] = "Car";
		offlineExactSeries[9,5] = "cup22";
		offlineExactCar[9,5] = 9;
		offlineTracklists[9,5] = "4";
		offlinePrizes[9,5] = "AltPaint";
		offlineSetPrizes[9,5] = "cup22livery9alt1";
		eventChapterDescriptions[9,5] = "He's won it before, in the same car, with the same team, at the same track. Requires Cup '22 Elliott to enter.";
		
		offlineEventChapter[9,6] = "Victory Bell";
		offlineChapterImage[9,6] = "cup22livery20alt1";
		offlineMinClass[9,6] = 1;
		offlineMinLevel[9,6] = 1;
		offlineAILevel[9,6] = 12;
		offlineMinType[9,6] = "Car";
		offlineExactSeries[9,6] = "cup22";
		offlineExactCar[9,6] = 20;
		offlineTracklists[9,6] = "4";
		offlinePrizes[9,6] = "AltPaint";
		offlineSetPrizes[9,6] = "cup22livery20alt1";
		eventChapterDescriptions[9,6] = "All of Bell's 3 wins have been clutched when he had to progress. Make it 4. Requires Cup '22 Bell to enter.";
		
		offlineEventChapter[9,7] = "Hail Melon";
		offlineChapterImage[9,7] = "cup22livery1alt2";
		offlineMinClass[9,7] = 1;
		offlineMinLevel[9,7] = 1;
		offlineAILevel[9,7] = 12;
		offlineMinType[9,7] = "Car";
		offlineExactSeries[9,7] = "cup22";
		offlineExactCar[9,7] = 1;
		offlineTracklists[9,7] = "4";
		offlinePrizes[9,7] = "AltPaint";
		offlineSetPrizes[9,7] = "cup22livery1alt2";
		eventChapterDescriptions[9,7] = "The melon man put it all on the line to progress. He'll do it one more time. Requires Cup '22 Chastain.";
		
		
		offlineEventImage[0] = "cup20livery48";
		offlineEventImage[1] = "cup20livery3alt2";
		offlineEventImage[2] = "cup22livery45";
		offlineEventImage[3] = "cup22livery4";
		offlineEventImage[4] = "cup79livery1";
		offlineEventImage[9] = "cup22livery1alt1";
		
		eventDescriptions[0] = "Relive Johnson's best moments leading to an incredible 7 championships.";
		eventDescriptions[1] = "Do it for Dale! Revisit some of Earnhardt's finest drives.";
		eventDescriptions[2] = "Touchdown in LA. The new season starts here.";
		eventDescriptions[3] = "Play out the biggest races in Harvick's career to date.";
		eventDescriptions[4] = "Reliving wrecks and famous fueds. Wreck 'em and check 'em!";
		
	}

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public static List<string> ListRewards(string category){
		List<string> validDriver = new List<string>();
		switch(category){
			case "Rookies":
				//for(int i=0;i<DriverNames.allCarsets.Length;i++){
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
			case "Harvick":
				validDriver.Add("cup224");
			break;
			case "Logano":
				validDriver.Add("cup2222");
			break;
			case "Elliott":
				validDriver.Add("cup229");
			break;
			case "Bell":
				validDriver.Add("cup2220");
			break;
			case "Chastain":
				validDriver.Add("cup221");
			break;
			
			default:
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
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
