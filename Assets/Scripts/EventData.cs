using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventData : MonoBehaviour
{
    public static string[] offlineEvent = new string[10];
	public static string[] offlineEventWeek = new string[10];
	public static string[,] offlineEventChapter = new string[10,10];
	public static string[] offlineEventImage = new string[10];
	public static string[,] offlineChapterImage = new string[10,10];
	public static string[] eventDescriptions = new string[10];
	public static string[,] eventChapterDescriptions = new string[10,10];
	public static string[,] offlineMinType = new string[10,10];
	public static int[,] offlineMinLevel = new int[10,10];
	public static int[,] offlineMinClass = new int[10,10];
	public static int[,] offlineMinRarity = new int[10,10];
	public static string[,] offlineMinTeam = new string[10,10];
	public static int[,] offlineExactCar = new int[10,10];
	public static string[,] offlineMinManu = new string[10,10];
	public static string[,] offlineMinDriverType = new string[10,10];
	public static string[,] offlineTracklists = new string[10,10];
	public static string[] offlineCircuits = new string[20];
	public static string[,] offlinePrizes = new string[10,10];
	public static string[,] offlineSetPrizes = new string[10,10];
	
    // Start is called before the first frame update
    void Start(){
        
		offlineEvent[0] = "Ol' Seven-Time";
		offlineEvent[1] = "The Intimidator";
		offlineEvent[2] = "All Stars";
		//offlineEvent[2] = "Return Of The King";
		//offlineEvent[3] = "Wonder Boy";
		
		offlineEventWeek[0] = "4,8,12";
		offlineEventWeek[1] = "2,6,10";
		offlineEventWeek[2] = "1,2,3,4,5,6,7,8,9,10,11,12";
		//offlineEventWeek[2] = "5";
		//offlineEventWeek[3] = "7";
		
		//Ol' Seven-Time
		offlineEventChapter[0,0] = "Where It All Began";
		offlineChapterImage[0,0] = "SuperTriOval";
		offlineMinClass[0,0] = 2;
		offlineMinLevel[0,0] = 5;
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
		offlineMinType[1,1] = "Team";
		offlineMinTeam[1,1] = "IND";
		offlineTracklists[1,1] = "8";
		offlinePrizes[1,1] = "Earnhardt";
		offlineSetPrizes[1,1] = "15";
		eventChapterDescriptions[1,1] = "April 1, 1979. Earnhardt's first win comes at Bristol in the Southeastern 500.";
		
		offlineEventChapter[1,2] = "Sophomore Season";
		offlineChapterImage[1,2] = "TinyOval";
		offlineMinClass[1,2] = 4;
		offlineMinLevel[1,2] = 15;
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
		offlineMinType[1,3] = "TEAM";
		offlineMinTeam[1,3] = "RCR";
		offlineTracklists[1,3] = "2";
		offlinePrizes[1,3] = "AltPaint";
		offlineSetPrizes[1,3] = "cup20livery3alt3";
		eventChapterDescriptions[1,3] = "Nov 2, 1986. Earnhardt wins his 2nd championship in style with a race to spare, his first with RCR.";
		
		offlineEventChapter[1,4] = "Pass In The Grass";
		offlineChapterImage[1,4] = "Bristol";
		offlineMinClass[1,4] = 3;
		offlineMinLevel[1,4] = 25;
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
		//offlineMinSeries[2,0] = "cup22";
		offlineTracklists[2,0] = "30";
		offlinePrizes[2,0] = "1Star";
		eventChapterDescriptions[2,0] = "February 6th, 2022. The next generation starts here. Let battle commence in the Coliseum.";
		
		//Return Of The King
		//offlineEventChapter[2,0] = "Where It All Began";
		//offlineEventChapter[2,1] = "Phoenix Nights";
		//offlineEventChapter[2,2] = "Daytona!";
		//offlineEventChapter[2,3] = "Daytona! Again!";
		//offlineEventChapter[2,4] = "Eleventh Heaven";
		//offlineEventChapter[2,5] = "Serial Champion";
		
		//Wonder Boy
		offlineEventChapter[3,0] = "Where It All Began";
		offlineEventChapter[3,1] = "Phoenix Nights";
		offlineEventChapter[3,2] = "Daytona!";
		offlineEventChapter[3,3] = "Daytona! Again!";
		offlineEventChapter[3,4] = "Eleventh Heaven";
		offlineEventChapter[3,5] = "Serial Champion";
		
		
		offlineEventImage[0] = "cup20livery48";
		offlineEventImage[1] = "cup20livery3alt2";
		offlineEventImage[2] = "cup22livery45";
		offlineEventImage[3] = "cup20livery24";

		
		eventDescriptions[0] = "Relive Johnson's best moments leading to an incredible 7 championships.";
		eventDescriptions[1] = "Do it for Dale! Revisit some of Earnhardt's finest drives.";
		eventDescriptions[2] = "Touchdown in LA. The new season starts here.";
		eventDescriptions[3] = "A variety of iconic circuits that witnessed the talent of King Petty.";
		eventDescriptions[4] = "Moments in history that defined Gordon's timeless career.";
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
