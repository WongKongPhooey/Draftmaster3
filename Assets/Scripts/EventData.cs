using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventData : MonoBehaviour
{
    public static string[] offlineEvent = new string[10];
	public static int[] offlineEventWeek = new int[10];
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
		//offlineEvent[2] = "Return Of The King";
		//offlineEvent[3] = "Wonder Boy";
		
		offlineEventWeek[0] = 6;
		offlineEventWeek[1] = 0;
		offlineEventWeek[2] = 5;
		offlineEventWeek[3] = 7;
		
		//Ol' Seven-Time
		offlineEventChapter[0,0] = "Where It All Began";
		offlineChapterImage[0,0] = "SuperTriOval";
		offlineMinClass[0,0] = 2;
		offlineMinLevel[0,0] = 5;
		offlineMinType[0,0] = "Team";
		offlineMinTeam[0,0] = "HEN";
		offlinePrizes[0,0] = "Johnson";
		offlineSetPrizes[0,0] = "10";
		eventChapterDescriptions[0,0] = "April 28, 2002. Johnson's first win came in California, beating Ku. Busch by less than 1 second.";
		
		offlineEventChapter[0,1] = "Daytona!";
		offlineChapterImage[0,1] = "SuperTriOval";
		offlineMinClass[0,1] = 3;
		offlineMinLevel[0,1] = 10;
		offlineMinType[0,1] = "Team";
		offlineMinTeam[0,1] = "HEN";
		offlinePrizes[0,1] = "Johnson";
		offlineSetPrizes[0,1] = "20";
		eventChapterDescriptions[0,1] = "Feb 19, 2006. Johnson's first Daytona 500 was won under caution, fending off Mears and Newman late on.";
		
		offlineEventChapter[0,2] = "Phoenix Nights";
		offlineChapterImage[0,2] = "Phoenix";
		offlineMinClass[0,2] = 4;
		offlineMinLevel[0,2] = 15;
		offlineMinType[0,2] = "Team";
		offlineMinTeam[0,2] = "HEN";
		offlinePrizes[0,2] = "Johnson";
		offlineSetPrizes[0,2] = "35";
		eventChapterDescriptions[0,2] = "Nov 11, 2007. Johnson wins his 10th race in one season on his way to his 2nd championship.";
		
		offlineEventChapter[0,3] = "Daytona! Again!";
		offlineChapterImage[0,3] = "cup20livery48alt1";
		offlineMinClass[0,3] = 3;
		offlineMinLevel[0,3] = 20;
		offlineMinType[0,3] = "Car";
		offlineExactCar[0,3] = 48;
		offlinePrizes[0,3] = "AltPaint";
		offlineSetPrizes[0,3] = "cup20livery48alt1";
		eventChapterDescriptions[0,3] = "July 6, 2013. Johnson becomes the first driver since Allison to win both Daytona races in 1 season.";
		
		offlineEventChapter[0,4] = "Serial Champion";
		offlineChapterImage[0,4] = "cup20livery48alt3";
		offlineMinClass[0,4] = 4;
		offlineMinLevel[0,4] = 30;
		offlineMinType[0,4] = "Car";
		offlineExactCar[0,4] = 48;
		offlinePrizes[0,4] = "AltPaint";
		offlineSetPrizes[0,4] = "cup20livery48alt3";
		eventChapterDescriptions[0,4] = "Nov 20, 2016. Johnson takes his 7th title, tying him with Earnhardt and Petty for the most titles.";
		
		offlineEventChapter[0,5] = "Eleventh Heaven";
		offlineChapterImage[0,5] = "cup20livery48alt2";
		offlineMinClass[0,5] = 5;
		offlineMinLevel[0,5] = 40;
		offlineMinType[0,5] = "Car";
		offlineExactCar[0,5] = 48;
		offlinePrizes[0,5] = "AltPaint";
		offlineSetPrizes[0,5] = "cup20livery48alt2";
		eventChapterDescriptions[0,5] = "June 4, 2017. Johnson wins in Dover for the 11th time, the 5th driver to score 10+ wins at a single track.";
		
		
		offlineTracklists[0,0] = "5";
		offlineTracklists[0,1] = "1";
		offlineTracklists[0,2] = "4";
		offlineTracklists[0,3] = "1";
		offlineTracklists[0,4] = "21";
		offlineTracklists[0,5] = "11";
		
		//The Intimidator
		offlineEventChapter[1,0] = "Where It All Began";
		offlineEventChapter[1,1] = "Phoenix Nights";
		offlineEventChapter[1,2] = "Daytona!";
		offlineEventChapter[1,3] = "Daytona! Again!";
		offlineEventChapter[1,4] = "Eleventh Heaven";
		offlineEventChapter[1,5] = "Serial Champion";
		
		//Return Of The King
		offlineEventChapter[2,0] = "Where It All Began";
		offlineEventChapter[2,1] = "Phoenix Nights";
		offlineEventChapter[2,2] = "Daytona!";
		offlineEventChapter[2,3] = "Daytona! Again!";
		offlineEventChapter[2,4] = "Eleventh Heaven";
		offlineEventChapter[2,5] = "Serial Champion";
		
		//Wonder Boy
		offlineEventChapter[3,0] = "Where It All Began";
		offlineEventChapter[3,1] = "Phoenix Nights";
		offlineEventChapter[3,2] = "Daytona!";
		offlineEventChapter[3,3] = "Daytona! Again!";
		offlineEventChapter[3,4] = "Eleventh Heaven";
		offlineEventChapter[3,5] = "Serial Champion";
		
		
		offlineEventImage[0] = "cup20livery48";
		offlineEventImage[1] = "cup20livery3";
		offlineEventImage[2] = "cup20livery43";
		offlineEventImage[3] = "cup20livery24";

		
		eventDescriptions[0] = "Relive Johnson's best moments en-route to an incredible 7 championships.";
		eventDescriptions[1] = "Do it for Dale! Revisit some of Earnhardt's finest drives.";
		eventDescriptions[2] = "A variety of iconic circuits that witnessed the talent of King Petty.";
		eventDescriptions[3] = "Moments in history that defined Gordon's timeless career.";
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
