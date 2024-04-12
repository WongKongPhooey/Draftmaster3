using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

/*----------------------
01 Daytona Beach, FL
02 Atlanta, GA
03 Las Vegas, NV
04 Phoenix, AZ
05 Fontana, CA 
06 Martinsville, VA
07 Fort Worth, TX
08 Bristol, TN
09 Richmond, VA
10 Talladega, AL 
11 Dover, DE
12 Kansas City, KS
13 Charlotte, NC
14 Long Pond, PA
15 Michigan - Brooklyn, MI
16 Joliet, IL
17 Kentucky - Sparta, KY
18 New Hampshire - Loudon, NH
19 Darlington, SC
20 Indianapolis, IN
21 Homestead, FL
22 WWT Gateway - Madison, IL
23 Nashville, TN
26 North Wilkesboro, NC
30 LA Coliseum, CA

24 Rockingham, UK
25 Rockingham, NC
27 Nazareth, PN
28 Iowa, IA
29 Pikes Peak - Fountain, CO
31 Motegi, JPN
32 Lausitzring, GER
33 Milwaukee, WI
----------------------*/

public class TrackData : MonoBehaviour
{
	
	public static string[] trackNames = new string[50];
	public static string[] trackCodeNames = new string[50];
	public static int[] trackLaps = new int[50];
	public static string trackCodeName;
	public static int speedFactor;
	
    // Start is called before the first frame update
    void Start()
    {
        loadTrackNames();
		loadTrackCodeNames();
		
		if(PlayerPrefs.GetString("raceSeries") == "IndyCar"){
			speedFactor = 24;
		} else {
			speedFactor = 0;
		}
    }

	public static string getTrackCodeName(int trackId){
		loadTrackCodeNames();
		//Debug.Log("Track Codename is " + trackCodeNames[trackId]);
		return trackCodeNames[trackId];
	}
	
	public static string getTrackName(int trackId){
		loadTrackNames();
		//Debug.Log("Track Codename is " + trackCodeNames[trackId]);
		return trackNames[trackId];
	}

	public static int getTrackLaps(int trackId){
		loadTrackLaps();
		//Debug.Log("Track Codename is " + trackCodeNames[trackId]);
		return trackLaps[trackId];
	}

	public void loadTrackData(string trackCodeName){
		//Get the method information using the method info class
		MethodInfo trackLoader = this.GetType().GetMethod(trackCodeName);

		//Invoke the method
		// (null- no parameter for the method call)
		trackLoader.Invoke(this, null);
	}

	public static void loadTrackNames(){
		trackNames[1] = "Daytona Beach, FL";
		trackNames[2] = "Atlanta, GA";
		trackNames[3] = "Las Vegas, NV";
		trackNames[4] = "Phoenix, AZ";
		trackNames[5] = "Fontana, CA";
		trackNames[6] = "Martinsville, VA";
		trackNames[7] = "Fort Worth, TX";
		trackNames[8] = "Bristol, TN";
		trackNames[9] = "Richmond, VA";
		trackNames[10] = "Talladega, AL";
		trackNames[11] = "Dover, DE";
		trackNames[12] = "Kansas City, KS";
		trackNames[13] = "Charlotte, NC";
		trackNames[14] = "Long Pond, PA";
		trackNames[15] = "Brooklyn, MI";
		trackNames[16] = "Joliet, IL";
		trackNames[17] = "Sparta, KY";
		trackNames[18] = "Loudon, NH";
		trackNames[19] = "Darlington, SC";
		trackNames[20] = "Indianapolis, IN";
		trackNames[21] = "Homestead, FL";
		trackNames[22] = "Madison, IL";
		trackNames[23] = "Nashville, TN";
		trackNames[30] = "Los Angeles, CA";
		
		trackNames[24] = "Rockingham, England";
		
		trackNames[25] = "Rockingham, NC";
		trackNames[26] = "North Wilkesboro, NC";
		trackNames[27] = "Nazareth, PN";
		trackNames[28] = "Newton, IA";
		trackNames[29] = "Nashville, TN";
	}
	
	public static void loadTrackCodeNames(){
		trackCodeNames[1] = "Daytona";
		trackCodeNames[2] = "Atlanta";
		trackCodeNames[3] = "LasVegas";
		trackCodeNames[4] = "Phoenix";
		trackCodeNames[5] = "Fontana";
		trackCodeNames[6] = "Martinsville";
		trackCodeNames[7] = "FortWorth";
		trackCodeNames[8] = "Bristol";
		trackCodeNames[9] = "Richmond";
		trackCodeNames[10] = "Talladega";
		trackCodeNames[11] = "Dover";
		trackCodeNames[12] = "Kansas";
		trackCodeNames[13] = "Charlotte";
		trackCodeNames[14] = "LongPond";
		trackCodeNames[15] = "Michigan";
		trackCodeNames[16] = "Joliet";
		trackCodeNames[17] = "Kentucky";
		trackCodeNames[18] = "NewHampshire";
		trackCodeNames[19] = "Darlington";
		trackCodeNames[20] = "Indianapolis";
		trackCodeNames[21] = "Miami";
		trackCodeNames[22] = "Madison";
		trackCodeNames[23] = "Nashville";
		trackCodeNames[30] = "LosAngeles";
		
		trackCodeNames[24] = "RockinghamUK";
		
		trackCodeNames[25] = "Rockingham";
		trackCodeNames[26] = "NorthWilkesboro";
		trackCodeNames[27] = "Nazareth";
		trackCodeNames[28] = "Iowa";
		trackCodeNames[29] = "Nashville";
	}
	
	public static string getTrackImage(string trackId){
		string trackImageName = "";
		switch(trackId){
			case "1":
				trackImageName = "SuperTriOval";
				break;
			case "2":
				trackImageName = "AngledTriOval";
				break;
			case "3":
				trackImageName = "TriOval";
				break;
			case "4":
				trackImageName = "Phoenix";
				break;
			case "5":
				trackImageName = "SuperTriOval";
				break;
			case "6":
				trackImageName = "LongOval";
				break;
			case "7":
				trackImageName = "AngledTriOval";
				break;
			case "8":
				trackImageName = "TinyOval";
				break;
			case "9":
				trackImageName = "TriOval";
				break;
			case "10":
				trackImageName = "Talladega";
				break;
			case "11":
				trackImageName = "SmallOval";
				break;
			case "12":
				trackImageName = "TriOval";
				break;
			case "13":
				trackImageName = "AngledTriOval";
				break;
			case "14":
				trackImageName = "LongPond";
				break;
			case "15":
				trackImageName = "SuperTriOval";
				break;
			case "16":
				trackImageName = "TriOval";
				break;
			case "17":
				trackImageName = "TriOval";
				break;
			case "18":
				trackImageName = "LongOval";
				break;
			case "19":
				trackImageName = "Darlington";
				break;
			case "20":
				trackImageName = "Indianapolis";
				break;
			case "21":
				trackImageName = "BigOval";
				break;
			case "22":
				trackImageName = "Madison";
				break;
			case "23":
				trackImageName = "TriOval";
				break;
			case "26":
				trackImageName = "SmallOval";
				break;
			case "28":
				trackImageName = "TriOval";
				break;
			case "30":
				trackImageName = "TinyOval";
				break;
			default:
				break;
		}
		return trackImageName;
	}
	
	public static void loadTrackLaps(){
		trackLaps[1] = 4;
		trackLaps[2] = 5;
		trackLaps[3] = 6;
		trackLaps[4] = 7;
		trackLaps[5] = 4;
		trackLaps[6] = 9;
		trackLaps[7] = 6;
		trackLaps[8] = 10;
		trackLaps[9] = 9;
		trackLaps[10] = 4;
		trackLaps[11] = 8;
		trackLaps[12] = 5;
		trackLaps[13] = 6;
		trackLaps[14] = 4;
		trackLaps[15] = 4;
		trackLaps[16] = 6;
		trackLaps[17] = 6;
		trackLaps[18] = 6;
		trackLaps[19] = 6;
		trackLaps[20] = 4;
		trackLaps[21] = 6;
		trackLaps[22] = 7;
		trackLaps[23] = 6;
		trackLaps[26] = 8;
		trackLaps[30] = 10;
		
		trackLaps[24] = 6;
		
		trackLaps[25] = 7;
		trackLaps[27] = 6;
		trackLaps[28] = 9;
		trackLaps[29] = 6;
	}
	
	public static void Talladega(){
		trackCodeName = "Talladega";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",200);
		PlayerPrefs.SetInt("StraightLength2",500);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",150);
		PlayerPrefs.SetInt("TurnLength2",150);
		PlayerPrefs.SetInt("TurnLength3",60);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",8);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",50);
		PlayerPrefs.SetInt("SpeedOffset",2 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",3);
		PlayerPrefs.SetString("TrackType","Plate");
	}

	public static void Joliet(){
		trackCodeName = "Joliet";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",24 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}
	
	public static void LasVegas(){
		trackCodeName = "LasVegas";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",18 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	public static void Bristol(){
		trackCodeName = "Bristol";
		PlayerPrefs.SetInt("RaceLaps",10);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",100);
		PlayerPrefs.SetInt("StraightLength2",100);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",50);
		PlayerPrefs.SetInt("SpeedOffset",65 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	public static void Richmond(){
		trackCodeName = "Richmond";
		PlayerPrefs.SetInt("RaceLaps",9);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",150);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",15);
		PlayerPrefs.SetInt("TurnLength2",165);
		PlayerPrefs.SetInt("TurnLength3",165);
		PlayerPrefs.SetInt("TurnLength4",15);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",58 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	public static void NewHampshire(){
		trackCodeName = "NewHampshire";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",250);
		PlayerPrefs.SetInt("StraightLength2",250);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",100);
		PlayerPrefs.SetInt("SpeedOffset",43 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Short");
	}

	public static void Indianapolis(){
		trackCodeName = "Indianapolis";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",400);
		PlayerPrefs.SetInt("StraightLength2",100);
		PlayerPrefs.SetInt("StraightLength3",400);
		PlayerPrefs.SetInt("StraightLength4",100);
		PlayerPrefs.SetInt("TurnLength1",90);
		PlayerPrefs.SetInt("TurnLength2",90);
		PlayerPrefs.SetInt("TurnLength3",90);
		PlayerPrefs.SetInt("TurnLength4",90);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",2);
		PlayerPrefs.SetInt("StartLine",250);
		PlayerPrefs.SetInt("SpeedOffset",0 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Large");
	}

	public static void Atlanta(){
		trackCodeName = "Atlanta";
		PlayerPrefs.SetInt("RaceLaps",5);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",150);
		PlayerPrefs.SetInt("StraightLength2",50);
		PlayerPrefs.SetInt("StraightLength3",350);
		PlayerPrefs.SetInt("StraightLength4",50);
		PlayerPrefs.SetInt("TurnLength1",20);
		PlayerPrefs.SetInt("TurnLength2",160);
		PlayerPrefs.SetInt("TurnLength3",160);
		PlayerPrefs.SetInt("TurnLength4",20);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",2);
		PlayerPrefs.SetInt("StartLine",40);
		PlayerPrefs.SetInt("SpeedOffset",16 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}
	
	public static void Phoenix(){
		trackCodeName = "Phoenix";
		PlayerPrefs.SetInt("RaceLaps",8);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",50);
		PlayerPrefs.SetInt("StraightLength2",100);
		PlayerPrefs.SetInt("StraightLength3",150);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",50);
		PlayerPrefs.SetInt("TurnLength2",170);
		PlayerPrefs.SetInt("TurnLength3",140);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",20);
		PlayerPrefs.SetInt("SpeedOffset",55 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",3);
		PlayerPrefs.SetString("TrackType","Short");
	}

	public static void Motegi(){
		trackCodeName = "Motegi";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",300);
		PlayerPrefs.SetInt("StraightLength2",300);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",200);
		PlayerPrefs.SetInt("TurnLength2",160);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",200);
		PlayerPrefs.SetInt("SpeedOffset",25 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Alt");
	}

	public static void LongPond(){
		trackCodeName = "LongPond";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",350);
		PlayerPrefs.SetInt("StraightLength2",250);
		PlayerPrefs.SetInt("StraightLength3",300);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",110);
		PlayerPrefs.SetInt("TurnLength2",100);
		PlayerPrefs.SetInt("TurnLength3",150);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",200);
		PlayerPrefs.SetInt("SpeedOffset",8 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",3);
		PlayerPrefs.SetString("TrackType","Large");
	}

	public static void Fontana(){
		trackCodeName = "Fontana";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",250);
		PlayerPrefs.SetInt("StraightLength4",20);
		PlayerPrefs.SetInt("TurnLength1",30);
		PlayerPrefs.SetInt("TurnLength2",150);
		PlayerPrefs.SetInt("TurnLength3",150);
		PlayerPrefs.SetInt("TurnLength4",30);
		PlayerPrefs.SetInt("TurnAngle1",8);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",8);
		PlayerPrefs.SetInt("StartLine",10);
		PlayerPrefs.SetInt("SpeedOffset",3 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Large");
	}
	
	public static void Michigan(){
		trackCodeName = "Michigan";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",250);
		PlayerPrefs.SetInt("StraightLength4",20);
		PlayerPrefs.SetInt("TurnLength1",10);
		PlayerPrefs.SetInt("TurnLength2",170);
		PlayerPrefs.SetInt("TurnLength3",170);
		PlayerPrefs.SetInt("TurnLength4",10);
		PlayerPrefs.SetInt("TurnAngle1",16);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",16);
		PlayerPrefs.SetInt("StartLine",10);
		PlayerPrefs.SetInt("SpeedOffset",3 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Large");
	}

	public static void Charlotte(){
		trackCodeName = "Charlotte";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",100);
		PlayerPrefs.SetInt("StraightLength2",50);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",50);
		PlayerPrefs.SetInt("TurnLength1",25);
		PlayerPrefs.SetInt("TurnLength2",155);
		PlayerPrefs.SetInt("TurnLength3",155);
		PlayerPrefs.SetInt("TurnLength4",25);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",40);
		PlayerPrefs.SetInt("SpeedOffset",19 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	public static void FortWorth(){
		trackCodeName = "FortWorth";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",80);
		PlayerPrefs.SetInt("StraightLength2",75);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",75);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",40);
		PlayerPrefs.SetInt("SpeedOffset",22 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	public static void Darlington(){
		trackCodeName = "Darlington";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",250);
		PlayerPrefs.SetInt("StraightLength2",0);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",250);
		PlayerPrefs.SetInt("TurnLength1",45);
		PlayerPrefs.SetInt("TurnLength2",110);
		PlayerPrefs.SetInt("TurnLength3",45);
		PlayerPrefs.SetInt("TurnLength4",160);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",2);
		PlayerPrefs.SetInt("StartLine",150);
		PlayerPrefs.SetInt("SpeedOffset",22 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Alt");
	}

	public static void Miami(){
		trackCodeName = "Miami";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",300);
		PlayerPrefs.SetInt("StraightLength2",300);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",150);
		PlayerPrefs.SetInt("SpeedOffset",19 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	public static void Dover(){
		trackCodeName = "Dover";
		PlayerPrefs.SetInt("RaceLaps",8);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",150);
		PlayerPrefs.SetInt("StraightLength2",150);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",75);
		PlayerPrefs.SetInt("SpeedOffset",47 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	public static void Kansas(){
		trackCodeName = "Kansas";
		PlayerPrefs.SetInt("RaceLaps",5);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",50);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",50);
		PlayerPrefs.SetInt("TurnLength1",15);
		PlayerPrefs.SetInt("TurnLength2",165);
		PlayerPrefs.SetInt("TurnLength3",165);
		PlayerPrefs.SetInt("TurnLength4",15);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",4);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",2);
		PlayerPrefs.SetInt("SpeedOffset",17 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Large");
	}
	
	public static void Kentucky(){
		trackCodeName = "Kentucky";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",31 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	public static void Daytona(){
		trackCodeName = "Daytona";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",2);
		PlayerPrefs.SetInt("StraightLength2",200);
		PlayerPrefs.SetInt("StraightLength3",500);
		PlayerPrefs.SetInt("StraightLength4",200);
		PlayerPrefs.SetInt("TurnLength1",30);
		PlayerPrefs.SetInt("TurnLength2",150);
		PlayerPrefs.SetInt("TurnLength3",150);
		PlayerPrefs.SetInt("TurnLength4",30);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",4);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",1);
		PlayerPrefs.SetInt("SpeedOffset",8 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Plate");
	}

	public static void Martinsville(){
		trackCodeName = "Martinsville";
		PlayerPrefs.SetInt("RaceLaps",9);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",150);
		PlayerPrefs.SetInt("StraightLength2",150);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",50);
		PlayerPrefs.SetInt("SpeedOffset",75 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	public static void LosAngeles(){
		trackCodeName = "LosAngeles";
		PlayerPrefs.SetInt("RaceLaps",10);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",20);
		PlayerPrefs.SetInt("StraightLength2",0);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",5);
		PlayerPrefs.SetInt("TurnLength2",170);
		PlayerPrefs.SetInt("TurnLength3",10);
		PlayerPrefs.SetInt("TurnLength4",170);
		PlayerPrefs.SetInt("TurnLength5",5);
		PlayerPrefs.SetInt("TurnAngle1",8);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",8);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("TurnAngle5",8);
		PlayerPrefs.SetInt("StartLine",10);
		PlayerPrefs.SetInt("SpeedOffset",85 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",5);
		PlayerPrefs.SetString("TrackType","Short");
	}

	public static void Madison(){
		trackCodeName = "Madison";
		PlayerPrefs.SetInt("RaceLaps",7);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",250);
		PlayerPrefs.SetInt("StraightLength2",250);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",170);
		PlayerPrefs.SetInt("TurnLength2",190);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",150);
		PlayerPrefs.SetInt("SpeedOffset",36 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Mid");
	}
	
	public static void Nashville(){
		trackCodeName = "Nashville";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",1);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",1);
		PlayerPrefs.SetInt("SpeedOffset",17 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	public static void Iowa(){
		trackCodeName = "Iowa";
		PlayerPrefs.SetInt("RaceLaps",9);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",150);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",25);
		PlayerPrefs.SetInt("TurnLength2",155);
		PlayerPrefs.SetInt("TurnLength3",155);
		PlayerPrefs.SetInt("TurnLength4",25);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",1);
		PlayerPrefs.SetInt("SpeedOffset",51 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	public static void NorthWilkesboro(){
		trackCodeName = "NorthWilkesboro";
		PlayerPrefs.SetInt("RaceLaps",8);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",150);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",1);
		PlayerPrefs.SetInt("StraightLength4",150);
		PlayerPrefs.SetInt("TurnLength1",5);
		PlayerPrefs.SetInt("TurnLength2",165);
		PlayerPrefs.SetInt("TurnLength3",10);
		PlayerPrefs.SetInt("TurnLength4",180);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",4);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",75);
		PlayerPrefs.SetInt("SpeedOffset",73 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Short");
	}

	public static void TestTrack(){
		trackCodeName = "TestTrack";
		PlayerPrefs.SetInt("RaceLaps",1 * PlayerPrefs.GetInt("RaceLapsMultiplier"));
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",50);
		PlayerPrefs.SetInt("StraightLength2",50);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",25);
		PlayerPrefs.SetInt("SpeedOffset",77 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
