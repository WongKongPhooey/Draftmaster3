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
	public static int[,] offlineMinClass = new int[10,10];
	public static int[,] offlineMinLevel = new int[10,10];
	public static string[,] offlineTracklists = new string[10,10];
	public static string[] offlineCircuits = new string[20];
	
    // Start is called before the first frame update
    void Start(){
        
		offlineEvent[0] = "Ol' Seven-Time";
		offlineEvent[1] = "The Intimidator";
		offlineEvent[2] = "Return Of The King";
		offlineEvent[3] = "Wonder Boy";
		
		offlineEventWeek[0] = 1;
		offlineEventWeek[1] = 3;
		offlineEventWeek[2] = 5;
		offlineEventWeek[3] = 7;
		
		//Ol' Seven-Time
		offlineEventChapter[0,0] = "Where It All Began";
		offlineChapterImage[0,0] = "California";
		offlineMinClass[0,0] = 0;
		offlineMinLevel[0,0] = 1;
		eventChapterDescriptions[0,0] = "April 28, 2002. Johnson's first win came in California, beating Ku. Busch by less than 1 second.";
		
		offlineEventChapter[0,1] = "Daytona!";
		offlineChapterImage[0,1] = "Daytona";
		offlineMinClass[0,1] = 1;
		offlineMinLevel[0,1] = 10;
		eventChapterDescriptions[0,1] = "Feb 19, 2006. Johnson's first Daytona 500 was won under caution, fending off Mears and Newman late on.";
		
		offlineEventChapter[0,2] = "Phoenix Nights";
		offlineChapterImage[0,2] = "Phoenix";
		offlineMinClass[0,2] = 2;
		offlineMinLevel[0,2] = 20;
		eventChapterDescriptions[0,2] = "Nov 11, 2007. Johnson wins his 10th race in one season on his way to his 2nd championship.";
		
		offlineEventChapter[0,3] = "Daytona! Again!";
		offlineChapterImage[0,3] = "Daytona";
		offlineMinClass[0,3] = 3;
		offlineMinLevel[0,3] = 30;
		eventChapterDescriptions[0,3] = "July 6, 2013. Johnson becomes the first driver since Allison to win both Daytona races in 1 season.";
		
		offlineEventChapter[0,4] = "Eleventh Heaven";
		offlineChapterImage[0,4] = "Dover";
		offlineMinClass[0,4] = 4;
		offlineMinLevel[0,4] = 40;
		eventChapterDescriptions[0,4] = "May 31, 2015. Johnson wins 10 in Dover, becoming the 5th driver ever to score 10+ wins at a single track.";
		
		offlineEventChapter[0,5] = "Serial Champion";
		offlineChapterImage[0,5] = "Miami";
		offlineMinClass[0,5] = 5;
		offlineMinLevel[0,5] = 50;
		eventChapterDescriptions[0,5] = "Nov 20, 2016. Johnson takes his 7th title, tying him with Earnhardt and Petty for the most titles.";
		
		
		offlineTracklists[0,0] = "5";
		offlineTracklists[0,1] = "4";
		offlineTracklists[0,2] = "1";
		offlineTracklists[0,3] = "1";
		offlineTracklists[0,4] = "3,6,2,4";
		offlineTracklists[0,5] = "1,2,3,4,6";
		
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
		
		
		offlineEventImage[0] = "cup20livery2";
		offlineEventImage[1] = "cup20livery9";
		offlineEventImage[2] = "cup20livery11";
		offlineEventImage[3] = "cup20livery22";

		
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
