using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using Random=UnityEngine.Random;

public class DriverNames : MonoBehaviour {

	public static ArrayList series = new ArrayList();
	public static ArrayList winnableSeries = new ArrayList();

	public static string[] names = new string[101];
	public static string[] teams = new string[101];
	public static string[] manufacturer = new string[101];
	public static int[] rarity = new int[101];
	public static string[] types = new string[101];

	public static Dictionary<string, string[]> allNames = new Dictionary<string, string[]>();
	public static Dictionary<string, string[]> allTeams = new Dictionary<string, string[]>();
	public static Dictionary<string, string[]> allManufacturer = new Dictionary<string, string[]>();
	public static Dictionary<string, int[]> allRarity = new Dictionary<string, int[]>();
	public static Dictionary<string, string[]> allTypes = new Dictionary<string, string[]>();
	public static Dictionary<string, string> allCarsetNames = new Dictionary<string, string>();

	public static Dictionary<string, int> numXPos = new Dictionary<string, int>();

	public static string[] allCarsets = new string[8];
	public static string[] allWinnableCarsets = new string[6];
	public static string[] allManufacturers = new string[7];

	public static string[] cup2020Names = new string[101];
	public static string[] cup2020Teams = new string[101];
	public static string[] cup2020Manufacturer = new string[101];
	public static int[] cup2020Rarity = new int[101];
	public static string[] cup2020Types = new string[101];
	
	public static string[] cup2022Names = new string[101];
	public static string[] cup2022Teams = new string[101];
	public static string[] cup2022Manufacturer = new string[101];
	public static int[] cup2022Rarity = new int[101];
	public static string[] cup2022Types = new string[101];
	
	public static string[] cup2023Names = new string[101];
	public static string[] cup2023Teams = new string[101];
	public static string[] cup2023Manufacturer = new string[101];
	public static int[] cup2023Rarity = new int[101];
	public static string[] cup2023Types = new string[101];
	
	public static string[] irl2023Names = new string[101];
	public static string[] irl2023Teams = new string[101];
	public static string[] irl2023Manufacturer = new string[101];
	public static int[] irl2023Rarity = new int[101];
	public static string[] irl2023Types = new string[101];
	
	public static string[] cup2001Names = new string[101];
	public static string[] cup2001Teams = new string[101];
	public static string[] cup2001Manufacturer = new string[101];
	public static int[] cup2001Rarity = new int[101];
	public static string[] cup2001Types = new string[101];
	
	public static string[] cup1979Names = new string[101];
	public static string[] cup1979Teams = new string[101];
	public static string[] cup1979Manufacturer = new string[101];
	public static int[] cup1979Rarity = new int[101];
	public static string[] cup1979Types = new string[101];
	
	public static string[] irc2000Names = new string[101];
	public static string[] irc2000Teams = new string[101];
	public static string[] irc2000Manufacturer = new string[101];
	public static int[] irc2000Rarity = new int[101];
	public static string[] irc2000Types = new string[101];
	
	public static string[] dmc2015Names = new string[101];
	public static string[] dmc2015Teams = new string[101];
	public static string[] dmc2015Manufacturer = new string[101];
	public static int[] dmc2015Rarity = new int[101];
	public static string[] dmc2015Types = new string[101];
	
	public static string[] legendsNames = new string[10];
	public static string[] legendsLiveries = new string[10];
	
	public static string[] cup2020AltNames = new string[20];

	// Use this for initialization
	void Awake() {
		loadData();
	}
	
	public static void loadData(){
		cup20();
		cup22();
		cup23();
		irl23();
		cup01();
		cup79();
		dmc15();
		irc00();
		listCarsets();
		listManufacturers();
		listWinnableCarsets();
		carsetNames();
		
		series.Clear();
		series.Add("cup20");
		series.Add("cup22");
		series.Add("cup23");
		series.Add("irl23");
		series.Add("cup01");
		series.Add("cup79");
		series.Add("dmc15");
		series.Add("irc00");
		
		winnableSeries.Clear();
		winnableSeries.Add("cup20");
		winnableSeries.Add("cup22");
		winnableSeries.Add("cup23");
		winnableSeries.Add("irl23");
		winnableSeries.Add("dmc15");
		winnableSeries.Add("irc00");
		
		if(allNames.ContainsKey("cup20") == false){
			allNames.Add("cup20", cup2020Names);
			allTeams.Add("cup20", cup2020Teams);
			allManufacturer.Add("cup20", cup2020Manufacturer);
			allRarity.Add("cup20", cup2020Rarity);
			allTypes.Add("cup20", cup2020Types);
			
			allNames.Add("cup22", cup2022Names);
			allTeams.Add("cup22", cup2022Teams);
			allManufacturer.Add("cup22", cup2022Manufacturer);
			allRarity.Add("cup22", cup2022Rarity);
			allTypes.Add("cup22", cup2022Types);
			
			allNames.Add("cup23", cup2023Names);
			allTeams.Add("cup23", cup2023Teams);
			allManufacturer.Add("cup23", cup2023Manufacturer);
			allRarity.Add("cup23", cup2023Rarity);
			allTypes.Add("cup23", cup2023Types);
			
			allNames.Add("irl23", irl2023Names);
			allTeams.Add("irl23", irl2023Teams);
			allManufacturer.Add("irl23", irl2023Manufacturer);
			allRarity.Add("irl23", irl2023Rarity);
			allTypes.Add("irl23", irl2023Types);
			
			allNames.Add("cup01", cup2001Names);
			allTeams.Add("cup01", cup2001Teams);
			allManufacturer.Add("cup01", cup2001Manufacturer);
			allRarity.Add("cup01", cup2001Rarity);
			allTypes.Add("cup01", cup2001Types);
			
			allNames.Add("cup79", cup1979Names);
			allTeams.Add("cup79", cup1979Teams);
			allManufacturer.Add("cup79", cup1979Manufacturer);
			allRarity.Add("cup79", cup1979Rarity);
			allTypes.Add("cup79", cup1979Types);
			
			allNames.Add("irc00", irc2000Names);
			allTeams.Add("irc00", irc2000Teams);
			allManufacturer.Add("irc00", irc2000Manufacturer);
			allRarity.Add("irc00", irc2000Rarity);
			allTypes.Add("irc00", irc2000Types);
			
			allNames.Add("dmc15", dmc2015Names);
			allTeams.Add("dmc15", dmc2015Teams);
			allManufacturer.Add("dmc15", dmc2015Manufacturer);
			allRarity.Add("dmc15", dmc2015Rarity);
			allTypes.Add("dmc15", dmc2015Types);
			
			numXPos.Add("cup20", 17);
			numXPos.Add("cup22", 10);
			numXPos.Add("cup23", 11);
			numXPos.Add("irl23", 25);
			numXPos.Add("dmc15", 17);
			numXPos.Add("irc00", 17);
		}
	}
	
	public static void listCarsets(){
		allCarsets[0] = "cup20";
		allCarsets[1] = "cup22";
		allCarsets[2] = "cup23";
		allCarsets[3] = "cup01";
		allCarsets[4] = "cup79";
		allCarsets[5] = "dmc15";
		allCarsets[6] = "irc00";
		allCarsets[7] = "irl23";
	}
	
	public static void listManufacturers(){
		allManufacturers[0] = "CHV";
		allManufacturers[1] = "FRD";
		allManufacturers[2] = "TYT";
		allManufacturers[3] = "HON";
		allManufacturers[4] = "OLD";
		allManufacturers[5] = "PNT";
		allManufacturers[6] = "DDG";
	}
	
	public static void listWinnableCarsets(){
		allWinnableCarsets[0] = "cup20";
		allWinnableCarsets[1] = "cup22";
		allWinnableCarsets[2] = "cup23";
		allWinnableCarsets[3] = "dmc15";
		allWinnableCarsets[4] = "irc00";
		allWinnableCarsets[5] = "irl23";
	}
	
	public static bool isOfficialSeries(string seriesPrefix){
		loadData();
		for(int i=0;i<allCarsets.Length;i++){
			if(seriesPrefix == allCarsets[i]){
				return true;
			}
		}
		return false;
	}
	
	public static bool isOfficialManu(string manu){
		Debug.Log(manu);
		loadData();
		for(int i=0;i<allManufacturers.Length;i++){
			if(manu == allManufacturers[i]){
				return true;
			}
		}
		return false;
	}
	
	public static string getName(string seriesPrefix, int index){
		loadData();
		string[] names = allNames[seriesPrefix];
		return names[index];
	}
	
	public static string getTeam(string seriesPrefix, int index){
		loadData();
		string[] teams = allTeams[seriesPrefix];
		return teams[index];
	}
	
	public static string getManufacturer(string seriesPrefix, int index){
		loadData();
		string[] manufacturer = allManufacturer[seriesPrefix];
		return manufacturer[index];
	}
	
	public static int getRarity(string seriesPrefix, int index){
		loadData();
		int[] rarities = allRarity[seriesPrefix];
		return rarities[index];
	}
	
	public static string getType(string seriesPrefix, int index){
		loadData();
		string[] types = allTypes[seriesPrefix];
		return types[index];
	}
	
	public static int getFieldSize(string seriesPrefix){
		loadData();
		string[] nameSet = allNames[seriesPrefix];
		return nameSet.Length;
	}
	
	public static int getNumXPos(string seriesPrefix){
		loadData();
		return numXPos[seriesPrefix];
	}
	
	public static int getStorePrice(string seriesPrefix, int index, bool alt, bool storeDiscount){
		int carRarity = getRarity(seriesPrefix, index);
		int carPrice = 999;
		int shopDiscount = PlayerPrefs.GetInt("ShopDiscount");
		if(shopDiscount == 1){
			carRarity -= 1;
		}
		if(seriesPrefix == "cup23"){
			carRarity += 1;
		}
		if(seriesPrefix == "irl23"){
			carRarity += 1;
		}
		if(seriesPrefix == "irc00"){
			carRarity -= 1;
		}
		if(alt == true){
			carRarity += 3;
		}
		switch(carRarity){
			case -1:
				carPrice = 1;
				break;
			case 0:
				carPrice = 3;
				break;
			case 1:
				carPrice = 5;
				break;
			case 2:
				carPrice = 10;
				break;
			case 3:
				carPrice = 20;
				break;
			case 4:
				carPrice = 35;
				break;
			case 5:
				carPrice = 50;
				break;
			case 6:
				carPrice = 75;
				break;
			case 7:
				carPrice = 100;
				break;
			default:
				carPrice = 100;
				break;
		}
		return carPrice;
	}
	
	public static string getSeriesNiceName(string seriesPrefix){
		loadData();
		carsetNames();
		return allCarsetNames[seriesPrefix];
	}
	
	public static string getRandomSeries(){
		loadData();
		string randSeries = series[Mathf.FloorToInt(Random.Range(0,series.Count))].ToString();
		return randSeries;
	}
	
	public static string getRandomWinnableSeries(){
		loadData();
		string randSeries = winnableSeries[Mathf.FloorToInt(Random.Range(0,winnableSeries.Count))].ToString();
		return randSeries;
	}
	
	public static string getRandomAltPaint(string seriesPrefix,int index,bool canNull=false,int altProb=100){
		ArrayList randomAlts = new ArrayList();
		string randAlt = null;
		
		for(int i=0;i<10;i++){
			if(AltPaints.getAltPaintName(seriesPrefix,index,i) != null){
				//Debug.Log("Alt found " + seriesPrefix + "livery" + index + "alt" + i);
				randomAlts.Add("" + seriesPrefix + "livery" + index + "alt" + i);
			}
		}
		//If nulls are impossible, pick an alt
		//If nulls are possible, roll random against probability
		if((canNull == false)||
		  ((canNull == true)&&(altProb >= Random.Range(0,100)))){
			if(randomAlts.Count > 0){
				randAlt = (string)randomAlts[Random.Range(0,randomAlts.Count)];
			}
		}
		return randAlt;
	}
	
	public static void carsetNames(){
		if(allNames.ContainsKey("cup20") == false){
			allCarsetNames.Add("cup20", "Cup '20");
			allCarsetNames.Add("cup22", "Cup '22");
			allCarsetNames.Add("cup23", "Cup '23");
			allCarsetNames.Add("irl23", "IRL '23");
			allCarsetNames.Add("cup01", "Cup '01");
			allCarsetNames.Add("cup79", "Cup '79");
			allCarsetNames.Add("irc00", "IROC '00");
			allCarsetNames.Add("dmc15", "DM1 '15");
		}
	}
	
	public static void cup20(){
		cup2020Names[0] = "Houff";
		cup2020Names[1] = "Ku. Busch";
		cup2020Names[2] = "Keselowski";
		cup2020Names[3] = "A. Dillon";
		cup2020Names[4] = "Harvick";
		cup2020Names[5] = "Larson";
		cup2020Names[6] = "Newman";
		cup2020Names[7] = "Bilicki";
		cup2020Names[8] = "Reddick";
		cup2020Names[9] = "Elliott";
		cup2020Names[10] = "Almirola";
		cup2020Names[11] = "Hamlin";
		cup2020Names[12] = "Blaney";
		cup2020Names[13] = "T. Dillon";
		cup2020Names[14] = "Bowyer";
		cup2020Names[15] = "Poole";
		cup2020Names[16] = "Haley";
		cup2020Names[17] = "Buescher";
		cup2020Names[18] = "Ky. Busch";
		cup2020Names[19] = "Truex Jr";
		cup2020Names[20] = "Jones";
		cup2020Names[21] = "Dibenedetto";
		cup2020Names[22] = "Logano";
		cup2020Names[24] = "Byron";
		cup2020Names[27] = "Gaulding";
		cup2020Names[32] = "LaJoie";
		cup2020Names[34] = "McDowell";
		cup2020Names[36] = "Ragan";
		cup2020Names[37] = "Preece";
		cup2020Names[38] = "Nemechek";
		cup2020Names[41] = "Custer";
		cup2020Names[42] = "Kenseth";
		cup2020Names[43] = "Wallace";
		cup2020Names[47] = "Stenhouse Jr.";
		cup2020Names[48] = "Johnson";
		cup2020Names[49] = "Finchum";
		cup2020Names[51] = "Gase";
		cup2020Names[52] = "Mcleod";
		cup2020Names[53] = "Davison";
		cup2020Names[54] = "Yeley";
		cup2020Names[62] = "Gaughan";
		cup2020Names[66] = "Hill";
		cup2020Names[74] = "Sorenson";
		cup2020Names[77] = "Chastain";
		cup2020Names[78] = "Smithley";
		cup2020Names[88] = "Bowman";
		cup2020Names[95] = "Bell";
		cup2020Names[96] = "Suarez";
		
		cup2020AltNames[0] = "Barrett";
		cup2020AltNames[1] = "Grala";
		cup2020AltNames[2] = "Ware";
		cup2020AltNames[3] = "Starr";
		cup2020AltNames[4] = "Allgaier";
		cup2020AltNames[5] = "Currey";
		cup2020AltNames[6] = "Hemric";
		cup2020AltNames[7] = "McMurray";
		cup2020AltNames[8] = "Mears";
		cup2020AltNames[9] = "Allmendinger";
		
		cup2020Teams[0] = "IND";
		cup2020Teams[1] = "CGR";
		cup2020Teams[2] = "PEN";
		cup2020Teams[3] = "RCR";
		cup2020Teams[4] = "SHR";
		cup2020Teams[5] = "CGR";
		cup2020Teams[6] = "RFR";
		cup2020Teams[7] = "IND";
		cup2020Teams[8] = "RCR";
		cup2020Teams[9] = "HEN";
		cup2020Teams[10] = "SHR";
		cup2020Teams[11] = "JGR";
		cup2020Teams[12] = "PEN";
		cup2020Teams[13] = "IND";
		cup2020Teams[14] = "SHR";
		cup2020Teams[15] = "IND";
		cup2020Teams[16] = "IND";
		cup2020Teams[17] = "RFR";
		cup2020Teams[18] = "JGR";
		cup2020Teams[19] = "JGR";
		cup2020Teams[20] = "JGR";
		cup2020Teams[21] = "IND";
		cup2020Teams[22] = "PEN";
		cup2020Teams[24] = "HEN";
		cup2020Teams[27] = "RWR";
		cup2020Teams[32] = "IND";
		cup2020Teams[34] = "FRM";
		cup2020Teams[36] = "FRM";
		cup2020Teams[37] = "JTG";
		cup2020Teams[38] = "FRM";
		cup2020Teams[41] = "SHR";
		cup2020Teams[42] = "CGR";
		cup2020Teams[43] = "IND";
		cup2020Teams[47] = "JTG";
		cup2020Teams[48] = "HEN";
		cup2020Teams[49] = "IND";
		cup2020Teams[51] = "RWR";
		cup2020Teams[52] = "RWR";
		cup2020Teams[53] = "RWR";
		cup2020Teams[54] = "RWR";
		cup2020Teams[62] = "IND";
		cup2020Teams[66] = "IND";
		cup2020Teams[74] = "IND";
		cup2020Teams[77] = "IND";
		cup2020Teams[78] = "IND";
		cup2020Teams[88] = "HEN";
		cup2020Teams[95] = "IND";
		cup2020Teams[96] = "IND";
		
		cup2020Manufacturer[0] = "CHV";
		cup2020Manufacturer[1] = "CHV";
		cup2020Manufacturer[2] = "FRD";
		cup2020Manufacturer[3] = "CHV";
		cup2020Manufacturer[4] = "FRD";
		cup2020Manufacturer[5] = "CHV";
		cup2020Manufacturer[6] = "FRD";
		cup2020Manufacturer[7] = "CHV";
		cup2020Manufacturer[8] = "CHV";
		cup2020Manufacturer[9] = "CHV";
		cup2020Manufacturer[10] = "FRD";
		cup2020Manufacturer[11] = "TYT";
		cup2020Manufacturer[12] = "FRD";
		cup2020Manufacturer[13] = "CHV";
		cup2020Manufacturer[14] = "FRD";
		cup2020Manufacturer[15] = "CHV";
		cup2020Manufacturer[16] = "CHV";
		cup2020Manufacturer[17] = "FRD";
		cup2020Manufacturer[18] = "TYT";
		cup2020Manufacturer[19] = "TYT";
		cup2020Manufacturer[20] = "TYT";
		cup2020Manufacturer[21] = "FRD";
		cup2020Manufacturer[22] = "FRD";
		cup2020Manufacturer[24] = "CHV";
		cup2020Manufacturer[27] = "FRD";
		cup2020Manufacturer[32] = "FRD";
		cup2020Manufacturer[34] = "FRD";
		cup2020Manufacturer[36] = "FRD";
		cup2020Manufacturer[37] = "CHV";
		cup2020Manufacturer[38] = "FRD";
		cup2020Manufacturer[41] = "FRD";
		cup2020Manufacturer[42] = "CHV";
		cup2020Manufacturer[43] = "CHV";
		cup2020Manufacturer[47] = "CHV";
		cup2020Manufacturer[48] = "CHV";
		cup2020Manufacturer[49] = "TYT";
		cup2020Manufacturer[51] = "FRD";
		cup2020Manufacturer[52] = "FRD";
		cup2020Manufacturer[53] = "FRD";
		cup2020Manufacturer[54] = "FRD";
		cup2020Manufacturer[62] = "CHV";
		cup2020Manufacturer[66] = "TYT";
		cup2020Manufacturer[74] = "CHV";
		cup2020Manufacturer[77] = "CHV";
		cup2020Manufacturer[78] = "CHV";
		cup2020Manufacturer[88] = "CHV";
		cup2020Manufacturer[95] = "TYT";
		cup2020Manufacturer[96] = "TYT";
		
		cup2020Types[0] = "Rookie";
		cup2020Types[1] = "Intimidator";
		cup2020Types[2] = "Intimidator";
		cup2020Types[3] = "Strategist";
		cup2020Types[4] = "Dominator";
		cup2020Types[5] = "Closer";
		cup2020Types[6] = "Closer";
		cup2020Types[7] = "Strategist";
		cup2020Types[8] = "Rookie";
		cup2020Types[9] = "Closer";
		cup2020Types[10] = "Strategist";
		cup2020Types[11] = "Closer";
		cup2020Types[12] = "Strategist";
		cup2020Types[13] = "Closer";
		cup2020Types[14] = "Intimidator";
		cup2020Types[15] = "Rookie";
		cup2020Types[16] = "Rookie";
		cup2020Types[17] = "Strategist";
		cup2020Types[18] = "Intimidator";
		cup2020Types[19] = "Dominator";
		cup2020Types[20] = "Closer";
		cup2020Types[21] = "Closer";
		cup2020Types[22] = "Intimidator";
		cup2020Types[24] = "Closer";
		cup2020Types[27] = "Strategist";
		cup2020Types[32] = "Strategist";
		cup2020Types[34] = "Intimidator";
		cup2020Types[36] = "Closer";
		cup2020Types[37] = "Rookie";
		cup2020Types[38] = "Rookie";
		cup2020Types[41] = "Rookie";
		cup2020Types[42] = "Strategist";
		cup2020Types[43] = "Strategist";
		cup2020Types[47] = "Intimidator";
		cup2020Types[48] = "Legend";
		cup2020Types[49] = "Rookie";
		cup2020Types[51] = "Strategist";
		cup2020Types[52] = "Closer";
		cup2020Types[53] = "Strategist";
		cup2020Types[54] = "Closer";
		cup2020Types[62] = "Intimidator";
		cup2020Types[66] = "Closer";
		cup2020Types[74] = "Strategist";
		cup2020Types[77] = "Intimidator";
		cup2020Types[78] = "Closer";
		cup2020Types[88] = "Strategist";
		cup2020Types[95] = "Rookie";
		cup2020Types[96] = "Closer";
		
		cup2020Rarity[0] = 1;
		cup2020Rarity[1] = 2;
		cup2020Rarity[2] = 3;
		cup2020Rarity[3] = 2;
		cup2020Rarity[4] = 3;
		cup2020Rarity[5] = 3;
		cup2020Rarity[6] = 2;
		cup2020Rarity[7] = 1;
		cup2020Rarity[8] = 1;
		cup2020Rarity[9] = 3;
		cup2020Rarity[10] = 2;
		cup2020Rarity[11] = 3;
		cup2020Rarity[12] = 2;
		cup2020Rarity[13] = 1;
		cup2020Rarity[14] = 2;
		cup2020Rarity[15] = 1;
		cup2020Rarity[16] = 1;
		cup2020Rarity[17] = 1;
		cup2020Rarity[18] = 3;
		cup2020Rarity[19] = 3;
		cup2020Rarity[20] = 1;
		cup2020Rarity[21] = 1;
		cup2020Rarity[22] = 3;
		cup2020Rarity[24] = 2;
		cup2020Rarity[27] = 1;
		cup2020Rarity[32] = 1;
		cup2020Rarity[34] = 1;
		cup2020Rarity[36] = 2;
		cup2020Rarity[37] = 1;
		cup2020Rarity[38] = 1;
		cup2020Rarity[41] = 1;
		cup2020Rarity[42] = 2;
		cup2020Rarity[43] = 2;
		cup2020Rarity[47] = 1;
		cup2020Rarity[48] = 3;
		cup2020Rarity[49] = 1;
		cup2020Rarity[51] = 1;
		cup2020Rarity[52] = 1;
		cup2020Rarity[53] = 1;
		cup2020Rarity[54] = 1;
		cup2020Rarity[62] = 1;
		cup2020Rarity[66] = 1;
		cup2020Rarity[74] = 1;
		cup2020Rarity[77] = 1;
		cup2020Rarity[78] = 1;
		cup2020Rarity[88] = 2;
		cup2020Rarity[95] = 1;
		cup2020Rarity[96] = 1;
		
		legendsNames[0] = "Petty";
		legendsNames[1] = "Earnhardt";
		legendsNames[2] = "Johnson";
		legendsNames[3] = "Yarborough";
		
		legendsLiveries[0] = "20livery2";
		legendsLiveries[1] = "20livery0";
		legendsLiveries[2] = "20livery1";
		legendsLiveries[3] = "76livery2";
	}
	
	public static void cup22(){
		cup2022Names[1] = "Chastain";
		cup2022Names[2] = "Cindric";
		cup2022Names[3] = "A. Dillon";
		cup2022Names[4] = "Harvick";
		cup2022Names[5] = "Larson";
		cup2022Names[6] = "Keselowski";
		cup2022Names[7] = "LaJoie";
		cup2022Names[8] = "Reddick";
		cup2022Names[9] = "Elliott";
		cup2022Names[10] = "Almirola";
		cup2022Names[11] = "Hamlin";
		cup2022Names[12] = "Blaney";
		cup2022Names[14] = "Briscoe";
		cup2022Names[15] = "Preece";
		cup2022Names[16] = "Allmendinger";
		cup2022Names[17] = "Buescher";
		cup2022Names[18] = "Ky. Busch";
		cup2022Names[19] = "Truex Jr";
		cup2022Names[20] = "Bell";
		cup2022Names[21] = "Burton";
		cup2022Names[22] = "Logano";
		cup2022Names[23] = "Wallace";
		cup2022Names[24] = "Byron";
		cup2022Names[26] = "Kvyat";
		cup2022Names[27] = "Villeneuve";
		cup2022Names[31] = "Haley";
		cup2022Names[34] = "McDowell";
		cup2022Names[38] = "Gilliland";
		cup2022Names[41] = "Custer";
		cup2022Names[42] = "T. Dillon";
		cup2022Names[43] = "Jones";
		cup2022Names[44] = "Biffle";
		cup2022Names[45] = "Ku. Busch";
		cup2022Names[47] = "Stenhouse Jr.";
		cup2022Names[48] = "Bowman";
		cup2022Names[50] = "Grala";
		cup2022Names[51] = "Ware";
		cup2022Names[52] = "Bilicki";
		cup2022Names[53] = "Smithley";
		cup2022Names[55] = "Yeley";
		cup2022Names[62] = "Gragson";
		cup2022Names[66] = "Hill";
		cup2022Names[77] = "Cassill";
		cup2022Names[78] = "McLeod";
		cup2022Names[91] = "Raikkonen";
		cup2022Names[99] = "Suarez";
		
		cup2022Teams[1] = "TRK";
		cup2022Teams[2] = "PEN";
		cup2022Teams[3] = "RCR";
		cup2022Teams[4] = "SHR";
		cup2022Teams[5] = "HEN";
		cup2022Teams[6] = "RFK";
		cup2022Teams[7] = "SPI";
		cup2022Teams[8] = "RCR";
		cup2022Teams[9] = "HEN";
		cup2022Teams[10] = "SHR";
		cup2022Teams[11] = "JGR";
		cup2022Teams[12] = "PEN";
		cup2022Teams[14] = "SHR";
		cup2022Teams[15] = "RWR";
		cup2022Teams[16] = "KAU";
		cup2022Teams[17] = "RFK";
		cup2022Teams[18] = "JGR";
		cup2022Teams[19] = "JGR";
		cup2022Teams[20] = "JGR";
		cup2022Teams[21] = "IND";
		cup2022Teams[22] = "PEN";
		cup2022Teams[23] = "23X";
		cup2022Teams[24] = "HEN";
		cup2022Teams[26] = "IND";
		cup2022Teams[27] = "IND";
		cup2022Teams[31] = "KAU";
		cup2022Teams[34] = "FRM";
		cup2022Teams[38] = "FRM";
		cup2022Teams[41] = "SHR";
		cup2022Teams[42] = "GMS";
		cup2022Teams[43] = "GMS";
		cup2022Teams[44] = "IND";
		cup2022Teams[45] = "23X";
		cup2022Teams[47] = "IND";
		cup2022Teams[48] = "HEN";
		cup2022Teams[50] = "IND";
		cup2022Teams[51] = "RWR";
		cup2022Teams[52] = "RWR";
		cup2022Teams[53] = "RWR";
		cup2022Teams[55] = "MBM";
		cup2022Teams[62] = "IND";
		cup2022Teams[66] = "MBM";
		cup2022Teams[77] = "SPI";
		cup2022Teams[78] = "IND";
		cup2022Teams[91] = "TRK";
		cup2022Teams[99] = "TRK";
		
		cup2022Manufacturer[1] = "CHV";
		cup2022Manufacturer[2] = "FRD";
		cup2022Manufacturer[3] = "CHV";
		cup2022Manufacturer[4] = "FRD";
		cup2022Manufacturer[5] = "CHV";
		cup2022Manufacturer[6] = "FRD";
		cup2022Manufacturer[7] = "CHV";
		cup2022Manufacturer[8] = "CHV";
		cup2022Manufacturer[9] = "CHV";
		cup2022Manufacturer[10] = "FRD";
		cup2022Manufacturer[11] = "TYT";
		cup2022Manufacturer[12] = "FRD";
		cup2022Manufacturer[14] = "FRD";
		cup2022Manufacturer[15] = "FRD";
		cup2022Manufacturer[16] = "CHV";
		cup2022Manufacturer[17] = "FRD";
		cup2022Manufacturer[18] = "TYT";
		cup2022Manufacturer[19] = "TYT";
		cup2022Manufacturer[20] = "TYT";
		cup2022Manufacturer[21] = "FRD";
		cup2022Manufacturer[22] = "FRD";
		cup2022Manufacturer[23] = "TYT";
		cup2022Manufacturer[24] = "CHV";
		cup2022Manufacturer[26] = "TYT";
		cup2022Manufacturer[27] = "FRD";
		cup2022Manufacturer[31] = "CHV";
		cup2022Manufacturer[34] = "FRD";
		cup2022Manufacturer[38] = "FRD";
		cup2022Manufacturer[41] = "FRD";
		cup2022Manufacturer[42] = "CHV";
		cup2022Manufacturer[43] = "CHV";
		cup2022Manufacturer[44] = "CHV";
		cup2022Manufacturer[45] = "TYT";
		cup2022Manufacturer[47] = "CHV";
		cup2022Manufacturer[48] = "CHV";
		cup2022Manufacturer[50] = "CHV";
		cup2022Manufacturer[51] = "FRD";
		cup2022Manufacturer[52] = "FRD";
		cup2022Manufacturer[53] = "FRD";
		cup2022Manufacturer[55] = "FRD";
		cup2022Manufacturer[62] = "CHV";
		cup2022Manufacturer[66] = "TYT";
		cup2022Manufacturer[77] = "CHV";
		cup2022Manufacturer[78] = "FRD";
		cup2022Manufacturer[91] = "CHV";
		cup2022Manufacturer[99] = "CHV";
		
		cup2022Types[1] = "Intimidator";
		cup2022Types[2] = "Rookie";
		cup2022Types[3] = "Strategist";
		cup2022Types[4] = "Dominator";
		cup2022Types[5] = "Dominator";
		cup2022Types[6] = "Intimidator";
		cup2022Types[7] = "Strategist";
		cup2022Types[8] = "Closer";
		cup2022Types[9] = "Closer";
		cup2022Types[10] = "Strategist";
		cup2022Types[11] = "Closer";
		cup2022Types[12] = "Strategist";
		cup2022Types[13] = "Closer";
		cup2022Types[14] = "Intimidator";
		cup2022Types[15] = "Blocker";
		cup2022Types[16] = "Rookie";
		cup2022Types[17] = "Blocker";
		cup2022Types[18] = "Intimidator";
		cup2022Types[19] = "Dominator";
		cup2022Types[20] = "Closer";
		cup2022Types[21] = "Rookie";
		cup2022Types[22] = "Intimidator";
		cup2022Types[23] = "Strategist";
		cup2022Types[24] = "Closer";
		cup2022Types[26] = "Rookie";
		cup2022Types[27] = "Intimidator";
		cup2022Types[31] = "Strategist";
		cup2022Types[34] = "Blocker";
		cup2022Types[38] = "Rookie";
		cup2022Types[41] = "Closer";
		cup2022Types[42] = "Blocker";
		cup2022Types[43] = "Strategist";
		cup2022Types[44] = "Dominator";
		cup2022Types[45] = "Intimidator";
		cup2022Types[47] = "Intimidator";
		cup2022Types[48] = "Closer";
		cup2022Types[50] = "Rookie";
		cup2022Types[51] = "Blocker";
		cup2022Types[52] = "Closer";
		cup2022Types[53] = "Closer";
		cup2022Types[55] = "Blocker";
		cup2022Types[62] = "Intimidator";
		cup2022Types[66] = "Closer";
		cup2022Types[77] = "Strategist";
		cup2022Types[78] = "Blocker";
		cup2022Types[91] = "Rookie";
		cup2022Types[99] = "Closer";
		
		cup2022Rarity[1] = 2;
		cup2022Rarity[2] = 2;
		cup2022Rarity[3] = 2;
		cup2022Rarity[4] = 4;
		cup2022Rarity[5] = 4;
		cup2022Rarity[6] = 3;
		cup2022Rarity[7] = 1;
		cup2022Rarity[8] = 2;
		cup2022Rarity[9] = 3;
		cup2022Rarity[10] = 2;
		cup2022Rarity[11] = 3;
		cup2022Rarity[12] = 3;
		cup2022Rarity[14] = 2;
		cup2022Rarity[15] = 1;
		cup2022Rarity[16] = 2;
		cup2022Rarity[17] = 1;
		cup2022Rarity[18] = 4;
		cup2022Rarity[19] = 3;
		cup2022Rarity[20] = 2;
		cup2022Rarity[21] = 1;
		cup2022Rarity[22] = 3;
		cup2022Rarity[23] = 2;
		cup2022Rarity[24] = 3;
		cup2022Rarity[26] = 1;
		cup2022Rarity[27] = 1;
		cup2022Rarity[31] = 1;
		cup2022Rarity[34] = 2;
		cup2022Rarity[38] = 1;
		cup2022Rarity[41] = 2;
		cup2022Rarity[42] = 1;
		cup2022Rarity[43] = 2;
		cup2022Rarity[44] = 2;
		cup2022Rarity[45] = 3;
		cup2022Rarity[47] = 2;
		cup2022Rarity[48] = 3;
		cup2022Rarity[50] = 1;
		cup2022Rarity[51] = 1;
		cup2022Rarity[52] = 1;
		cup2022Rarity[53] = 1;
		cup2022Rarity[55] = 1;
		cup2022Rarity[62] = 1;
		cup2022Rarity[66] = 1;
		cup2022Rarity[77] = 1;
		cup2022Rarity[78] = 1;
		cup2022Rarity[91] = 1;
		cup2022Rarity[99] = 2;
	}
	
	public static void cup23(){
		cup2023Names[1] = "Chastain";
		cup2023Names[2] = "Cindric";
		cup2023Names[3] = "A.Dillon";
		cup2023Names[4] = "Harvick";
		cup2023Names[5] = "Larson";
		cup2023Names[6] = "Keselowski";
		cup2023Names[7] = "LaJoie";
		cup2023Names[8] = "Busch";
		cup2023Names[9] = "Elliott";
		cup2023Names[10] = "Almirola";
		cup2023Names[11] = "Hamlin";
		cup2023Names[12] = "Blaney";
		cup2023Names[13] = "C.Smith";
		cup2023Names[14] = "Briscoe";
		cup2023Names[15] = "Yeley";
		cup2023Names[16] = "Allmendinger";
		cup2023Names[17] = "Buescher";
		cup2023Names[19] = "Truex Jr";
		cup2023Names[20] = "Bell";
		cup2023Names[21] = "Burton";
		cup2023Names[22] = "Logano";
		cup2023Names[23] = "Wallace";
		cup2023Names[24] = "Byron";
		cup2023Names[31] = "Haley";
		cup2023Names[33] = "Kostecki";
		cup2023Names[34] = "McDowell";
		cup2023Names[36] = "Z.Smith";
		cup2023Names[38] = "Gilliland";
		cup2023Names[41] = "Preece";
		cup2023Names[42] = "Gragson";
		cup2023Names[43] = "Jones";
		cup2023Names[45] = "Reddick";
		cup2023Names[47] = "Stenhouse Jr.";
		cup2023Names[48] = "Bowman";
		cup2023Names[50] = "Daly";
		cup2023Names[51] = "Crafton";
		cup2023Names[54] = "Gibbs";
		cup2023Names[62] = "Hill";
		cup2023Names[67] = "Pastrana";
		cup2023Names[77] = "T.Dillon";
		cup2023Names[78] = "McLeod";
		cup2023Names[84] = "Johnson";
		cup2023Names[91] = "Raikkonen";
		cup2023Names[99] = "Suarez";
		
		cup2023Teams[1] = "TRK";
		cup2023Teams[2] = "PEN";
		cup2023Teams[3] = "RCR";
		cup2023Teams[4] = "SHR";
		cup2023Teams[5] = "HEN";
		cup2023Teams[6] = "RFK";
		cup2023Teams[7] = "SPI";
		cup2023Teams[8] = "RCR";
		cup2023Teams[9] = "HEN";
		cup2023Teams[10] = "SHR";
		cup2023Teams[11] = "JGR";
		cup2023Teams[12] = "PEN";
		cup2023Teams[13] = "KAU";
		cup2023Teams[14] = "SHR";
		cup2023Teams[15] = "RWR";
		cup2023Teams[16] = "KAU";
		cup2023Teams[17] = "RFK";
		cup2023Teams[19] = "JGR";
		cup2023Teams[20] = "JGR";
		cup2023Teams[21] = "IND";
		cup2023Teams[22] = "PEN";
		cup2023Teams[23] = "23X";
		cup2023Teams[24] = "HEN";
		cup2023Teams[31] = "KAU";
		cup2023Teams[33] = "RCR";
		cup2023Teams[34] = "FRM";
		cup2023Teams[36] = "FRM";
		cup2023Teams[38] = "FRM";
		cup2023Teams[41] = "SHR";
		cup2023Teams[42] = "LMC";
		cup2023Teams[43] = "LMC";
		cup2023Teams[45] = "23X";
		cup2023Teams[47] = "IND";
		cup2023Teams[48] = "HEN";
		cup2023Teams[50] = "IND";
		cup2023Teams[51] = "IND";
		cup2023Teams[54] = "JGR";
		cup2023Teams[62] = "IND";
		cup2023Teams[67] = "23X";
		cup2023Teams[77] = "SPI";
		cup2023Teams[78] = "IND";
		cup2023Teams[84] = "IND";
		cup2023Teams[91] = "TRK";
		cup2023Teams[99] = "TRK";
		
		cup2023Manufacturer[1] = "CHV";
		cup2023Manufacturer[2] = "FRD";
		cup2023Manufacturer[3] = "CHV";
		cup2023Manufacturer[4] = "FRD";
		cup2023Manufacturer[5] = "CHV";
		cup2023Manufacturer[6] = "FRD";
		cup2023Manufacturer[7] = "CHV";
		cup2023Manufacturer[8] = "CHV";
		cup2023Manufacturer[9] = "CHV";
		cup2023Manufacturer[10] = "FRD";
		cup2023Manufacturer[11] = "TYT";
		cup2023Manufacturer[12] = "FRD";
		cup2023Manufacturer[13] = "CHV";
		cup2023Manufacturer[14] = "FRD";
		cup2023Manufacturer[15] = "FRD";
		cup2023Manufacturer[16] = "CHV";
		cup2023Manufacturer[17] = "FRD";
		cup2023Manufacturer[19] = "TYT";
		cup2023Manufacturer[20] = "TYT";
		cup2023Manufacturer[21] = "FRD";
		cup2023Manufacturer[22] = "FRD";
		cup2023Manufacturer[23] = "TYT";
		cup2023Manufacturer[24] = "CHV";
		cup2023Manufacturer[31] = "CHV";
		cup2023Manufacturer[33] = "CHV";
		cup2023Manufacturer[34] = "FRD";
		cup2023Manufacturer[36] = "FRD";
		cup2023Manufacturer[38] = "FRD";
		cup2023Manufacturer[41] = "FRD";
		cup2023Manufacturer[42] = "CHV";
		cup2023Manufacturer[43] = "CHV";
		cup2023Manufacturer[45] = "TYT";
		cup2023Manufacturer[47] = "CHV";
		cup2023Manufacturer[48] = "CHV";
		cup2023Manufacturer[50] = "CHV";
		cup2023Manufacturer[51] = "FRD";
		cup2023Manufacturer[54] = "TYT";
		cup2023Manufacturer[62] = "CHV";
		cup2023Manufacturer[67] = "TYT";
		cup2023Manufacturer[77] = "CHV";
		cup2023Manufacturer[78] = "CHV";
		cup2023Manufacturer[84] = "CHV";
		cup2023Manufacturer[91] = "CHV";
		cup2023Manufacturer[99] = "CHV";
		
		cup2023Types[1] = "Intimidator";
		cup2023Types[2] = "Strategist";
		cup2023Types[3] = "Strategist";
		cup2023Types[4] = "Dominator";
		cup2023Types[5] = "Dominator";
		cup2023Types[6] = "Intimidator";
		cup2023Types[7] = "Strategist";
		cup2023Types[8] = "Intimidator";
		cup2023Types[9] = "Closer";
		cup2023Types[10] = "Strategist";
		cup2023Types[11] = "Closer";
		cup2023Types[12] = "Strategist";
		cup2023Types[13] = "Rookie";
		cup2023Types[14] = "Intimidator";
		cup2023Types[15] = "Blocker";
		cup2023Types[16] = "Strategist";
		cup2023Types[17] = "Blocker";
		cup2023Types[19] = "Dominator";
		cup2023Types[20] = "Closer";
		cup2023Types[21] = "Strategist";
		cup2023Types[22] = "Intimidator";
		cup2023Types[23] = "Strategist";
		cup2023Types[24] = "Closer";
		cup2023Types[31] = "Strategist";
		cup2023Types[33] = "Rookie";
		cup2023Types[34] = "Strategist";
		cup2023Types[36] = "Rookie";
		cup2023Types[38] = "Blocker";
		cup2023Types[41] = "Closer";
		cup2023Types[42] = "Intimidator";
		cup2023Types[43] = "Closer";
		cup2023Types[45] = "Closer";
		cup2023Types[47] = "Intimidator";
		cup2023Types[48] = "Closer";
		cup2023Types[50] = "Rookie";
		cup2023Types[51] = "Blocker";
		cup2023Types[54] = "Rookie";
		cup2023Types[62] = "Rookie";
		cup2023Types[67] = "Rookie";
		cup2023Types[77] = "Blocker";
		cup2023Types[78] = "Blocker";
		cup2023Types[84] = "Legend";
		cup2023Types[91] = "Closer";
		cup2023Types[99] = "Closer";
		
		cup2023Rarity[1] = 3;
		cup2023Rarity[2] = 2;
		cup2023Rarity[3] = 2;
		cup2023Rarity[4] = 4;
		cup2023Rarity[5] = 4;
		cup2023Rarity[6] = 3;
		cup2023Rarity[7] = 1;
		cup2023Rarity[8] = 4;
		cup2023Rarity[9] = 3;
		cup2023Rarity[10] = 2;
		cup2023Rarity[11] = 4;
		cup2023Rarity[12] = 3;
		cup2023Rarity[13] = 1;
		cup2023Rarity[14] = 2;
		cup2023Rarity[15] = 1;
		cup2023Rarity[16] = 2;
		cup2023Rarity[17] = 2;
		cup2023Rarity[19] = 4;
		cup2023Rarity[20] = 3;
		cup2023Rarity[21] = 1;
		cup2023Rarity[22] = 4;
		cup2023Rarity[23] = 2;
		cup2023Rarity[24] = 3;
		cup2023Rarity[31] = 1;
		cup2023Rarity[33] = 1;
		cup2023Rarity[34] = 2;
		cup2023Rarity[36] = 1;
		cup2023Rarity[38] = 1;
		cup2023Rarity[41] = 1;
		cup2023Rarity[42] = 1;
		cup2023Rarity[43] = 2;
		cup2023Rarity[45] = 2;
		cup2023Rarity[47] = 2;
		cup2023Rarity[48] = 3;
		cup2023Rarity[50] = 1;
		cup2023Rarity[51] = 1;
		cup2023Rarity[54] = 1;
		cup2023Rarity[62] = 1;
		cup2023Rarity[67] = 1;
		cup2023Rarity[77] = 1;
		cup2023Rarity[78] = 1;
		cup2023Rarity[84] = 4;
		cup2023Rarity[91] = 1;
		cup2023Rarity[99] = 2;
	}
	
	public static void irl23(){

		irl2023Names[2] = "Newgarden";
		irl2023Names[3] = "Mclaughlin";
		irl2023Names[5] = "O Ward";
		irl2023Names[6] = "Rosenqvist";
		irl2023Names[7] = "Rossi";
		irl2023Names[8] = "Ericsson";
		irl2023Names[9] = "Dixon";
		irl2023Names[10] = "Palou";
		irl2023Names[11] = "Sato";
		irl2023Names[12] = "Power";
		irl2023Names[14] = "Ferrucci";
		irl2023Names[15] = "Rahal";
		irl2023Names[16] = "Castroneves";
		irl2023Names[18] = "Malukas";
		irl2023Names[20] = "Daly";
		irl2023Names[21] = "VeeKay";
		irl2023Names[23] = "Hunter-Reay";
		irl2023Names[24] = "Wilson";
		irl2023Names[26] = "Herta";
		irl2023Names[27] = "Kirkwood";
		irl2023Names[28] = "Grosjean";
		irl2023Names[29] = "Defrancesco";
		irl2023Names[30] = "Harvey";
		irl2023Names[33] = "Carpenter";
		irl2023Names[44] = "Legge";
		irl2023Names[45] = "Lundgaard";
		irl2023Names[50] = "Enerson";
		irl2023Names[51] = "Robb";
		irl2023Names[55] = "Pedersen";
		irl2023Names[60] = "Pagenaud";
		irl2023Names[66] = "Kanaan";
		irl2023Names[77] = "Ilott";
		irl2023Names[78] = "Canapino";
		irl2023Names[98] = "Andretti";
		
		irl2023Teams[2] = "PEN";
		irl2023Teams[3] = "PEN";
		irl2023Teams[5] = "ARR";
		irl2023Teams[6] = "ARR";
		irl2023Teams[7] = "ARR";
		irl2023Teams[8] = "CGR";
		irl2023Teams[9] = "CGR";
		irl2023Teams[10] = "CGR";
		irl2023Teams[11] = "CGR";
		irl2023Teams[12] = "PEN";
		irl2023Teams[14] = "FOY";
		irl2023Teams[15] = "RLL";
		irl2023Teams[16] = "MEY";
		irl2023Teams[18] = "DCR";
		irl2023Teams[20] = "EDC";
		irl2023Teams[21] = "EDC";
		irl2023Teams[23] = "DRE";
		irl2023Teams[24] = "DRE";
		irl2023Teams[26] = "AND";
		irl2023Teams[27] = "AND";
		irl2023Teams[28] = "AND";
		irl2023Teams[29] = "AND";
		irl2023Teams[30] = "RLL";
		irl2023Teams[33] = "EDC";
		irl2023Teams[44] = "RLL";
		irl2023Teams[45] = "RLL";
		irl2023Teams[50] = "IND";
		irl2023Teams[51] = "DCR";
		irl2023Teams[55] = "FOY";
		irl2023Teams[60] = "MEY";
		irl2023Teams[66] = "ARR";
		irl2023Teams[77] = "JUN";
		irl2023Teams[78] = "JUN";
		irl2023Teams[98] = "AND";
		
		irl2023Manufacturer[2] = "CHV";
		irl2023Manufacturer[3] = "CHV";
		irl2023Manufacturer[5] = "CHV";
		irl2023Manufacturer[6] = "CHV";
		irl2023Manufacturer[7] = "CHV";
		irl2023Manufacturer[8] = "HON";
		irl2023Manufacturer[9] = "HON";
		irl2023Manufacturer[10] = "HON";
		irl2023Manufacturer[11] = "HON";
		irl2023Manufacturer[12] = "CHV";
		irl2023Manufacturer[14] = "CHV";
		irl2023Manufacturer[15] = "HON";
		irl2023Manufacturer[16] = "HON";
		irl2023Manufacturer[18] = "HON";
		irl2023Manufacturer[20] = "CHV";
		irl2023Manufacturer[21] = "CHV";
		irl2023Manufacturer[23] = "CHV";
		irl2023Manufacturer[24] = "CHV";
		irl2023Manufacturer[26] = "HON";
		irl2023Manufacturer[27] = "HON";
		irl2023Manufacturer[28] = "HON";
		irl2023Manufacturer[29] = "HON";
		irl2023Manufacturer[30] = "HON";
		irl2023Manufacturer[33] = "CHV";
		irl2023Manufacturer[44] = "HON";
		irl2023Manufacturer[45] = "HON";
		irl2023Manufacturer[50] = "CHV";
		irl2023Manufacturer[51] = "HON";
		irl2023Manufacturer[55] = "CHV";
		irl2023Manufacturer[60] = "HON";
		irl2023Manufacturer[66] = "CHV";
		irl2023Manufacturer[77] = "CHV";
		irl2023Manufacturer[78] = "CHV";
		irl2023Manufacturer[98] = "HON";
		
		irl2023Types[2] = "CHV";
		irl2023Types[3] = "CHV";
		irl2023Types[5] = "CHV";
		irl2023Types[6] = "CHV";
		irl2023Types[7] = "CHV";
		irl2023Types[8] = "HON";
		irl2023Types[9] = "Legend";
		irl2023Types[10] = "HON";
		irl2023Types[11] = "HON";
		irl2023Types[12] = "CHV";
		irl2023Types[14] = "CHV";
		irl2023Types[15] = "HON";
		irl2023Types[16] = "Legend";
		irl2023Types[18] = "HON";
		irl2023Types[20] = "CHV";
		irl2023Types[21] = "CHV";
		irl2023Types[23] = "CHV";
		irl2023Types[24] = "CHV";
		irl2023Types[26] = "HON";
		irl2023Types[27] = "HON";
		irl2023Types[28] = "HON";
		irl2023Types[29] = "HON";
		irl2023Types[30] = "HON";
		irl2023Types[33] = "CHV";
		irl2023Types[44] = "HON";
		irl2023Types[45] = "HON";
		irl2023Types[50] = "CHV";
		irl2023Types[51] = "HON";
		irl2023Types[55] = "CHV";
		irl2023Types[60] = "HON";
		irl2023Types[66] = "CHV";
		irl2023Types[77] = "Blocker";
		irl2023Types[78] = "CHV";
		irl2023Types[98] = "HON";

		irl2023Rarity[2] = 3;
		irl2023Rarity[3] = 2;
		irl2023Rarity[5] = 2;
		irl2023Rarity[6] = 2;
		irl2023Rarity[7] = 2;
		irl2023Rarity[8] = 2;
		irl2023Rarity[9] = 4;
		irl2023Rarity[10] = 3;
		irl2023Rarity[11] = 3;
		irl2023Rarity[12] = 3;
		irl2023Rarity[14] = 2;
		irl2023Rarity[15] = 2;
		irl2023Rarity[16] = 4;
		irl2023Rarity[18] = 1;
		irl2023Rarity[20] = 2;
		irl2023Rarity[21] = 2;
		irl2023Rarity[23] = 3;
		irl2023Rarity[24] = 1;
		irl2023Rarity[26] = 2;
		irl2023Rarity[27] = 2;
		irl2023Rarity[28] = 2;
		irl2023Rarity[29] = 1;
		irl2023Rarity[30] = 1;
		irl2023Rarity[33] = 2;
		irl2023Rarity[44] = 1;
		irl2023Rarity[45] = 1;
		irl2023Rarity[50] = 1;
		irl2023Rarity[51] = 1;
		irl2023Rarity[55] = 1;
		irl2023Rarity[60] = 3;
		irl2023Rarity[66] = 3;
		irl2023Rarity[77] = 1;
		irl2023Rarity[78] = 1;
		irl2023Rarity[98] = 2;
	}
	
	public static void cup01(){

		cup2001Names[24] = "Gordon";
		cup2001Names[29] = "Harvick";
		
		cup2001Teams[24] = "HEN";
		cup2001Teams[29] = "RCR";
		
		cup2001Manufacturer[24] = "CHV";
		cup2001Manufacturer[29] = "CHV";
		
		cup2001Types[24] = "Closer";
		cup2001Types[29] = "Rookie";

		cup2001Rarity[24] = 4;
		cup2001Rarity[29] = 2;
	}
	
	public static void cup79(){

		cup1979Names[1] = "Allison";
		cup1979Names[11] = "Yarborough";
		cup1979Names[43] = "Petty";
		
		cup1979Teams[1] = "ELL";
		cup1979Teams[11] = "JJA";
		cup1979Teams[43] = "PET";
		
		cup1979Manufacturer[1] = "OLD";
		cup1979Manufacturer[11] = "OLD";
		cup1979Manufacturer[43] = "CHV";
		
		cup1979Types[1] = "Intimidator";
		cup1979Types[11] = "Legend";
		cup1979Types[43] = "Legend";

		cup1979Rarity[1] = 3;
		cup1979Rarity[11] = 4;
		cup1979Rarity[43] = 4;
	}
	
	public static void irc00(){

		irc2000Names[1] = "Earnhardt";
		irc2000Names[2] = "Stewart";
		irc2000Names[3] = "Earnhardt Jr";
		irc2000Names[4] = "Cheever Jr";
		irc2000Names[5] = "Labonte";
		irc2000Names[6] = "Gordon";
		irc2000Names[7] = "Ray";
		irc2000Names[8] = "Dismore";
		irc2000Names[9] = "Martin";
		irc2000Names[10] = "Jarrett";
		irc2000Names[11] = "Burton";
		irc2000Names[12] = "Wallace";
		
		irc2000Teams[1] = "ROC";
		irc2000Teams[2] = "ROC";
		irc2000Teams[3] = "ROC";
		irc2000Teams[4] = "ROC";
		irc2000Teams[5] = "ROC";
		irc2000Teams[6] = "ROC";
		irc2000Teams[7] = "ROC";
		irc2000Teams[8] = "ROC";
		irc2000Teams[9] = "ROC";
		irc2000Teams[10] = "ROC";
		irc2000Teams[11] = "ROC";
		irc2000Teams[12] = "ROC";
		
		irc2000Manufacturer[1] = "PNT";
		irc2000Manufacturer[2] = "PNT";
		irc2000Manufacturer[3] = "PNT";
		irc2000Manufacturer[4] = "PNT";
		irc2000Manufacturer[5] = "PNT";
		irc2000Manufacturer[6] = "PNT";
		irc2000Manufacturer[7] = "PNT";
		irc2000Manufacturer[8] = "PNT";
		irc2000Manufacturer[9] = "PNT";
		irc2000Manufacturer[10] = "PNT";
		irc2000Manufacturer[11] = "PNT";
		irc2000Manufacturer[12] = "PNT";
		
		irc2000Types[1] = "Legend";
		irc2000Types[2] = "Legend";
		irc2000Types[3] = "Legend";
		irc2000Types[4] = "Legend";
		irc2000Types[5] = "Legend";
		irc2000Types[6] = "Legend";
		irc2000Types[7] = "Legend";
		irc2000Types[8] = "Legend";
		irc2000Types[9] = "Legend";
		irc2000Types[10] = "Legend";
		irc2000Types[11] = "Legend";
		irc2000Types[12] = "Legend";

		irc2000Rarity[1] = 3;
		irc2000Rarity[2] = 3;
		irc2000Rarity[3] = 3;
		irc2000Rarity[4] = 3;
		irc2000Rarity[5] = 3;
		irc2000Rarity[6] = 3;
		irc2000Rarity[7] = 3;
		irc2000Rarity[8] = 3;
		irc2000Rarity[9] = 3;
		irc2000Rarity[10] = 3;
		irc2000Rarity[11] = 3;
		irc2000Rarity[12] = 3;
	}
	
	public static void dmc15(){

		dmc2015Names[0] = "Routermann";
		dmc2015Names[1] = "D.Allman";
		dmc2015Names[2] = "Rust";
		dmc2015Names[3] = "Earnst";
		dmc2015Names[4] = "Sterling";
		dmc2015Names[5] = "T.Laborde";
		dmc2015Names[6] = "Markin";
		dmc2015Names[7] = "Kuwalski";
		dmc2015Names[8] = "Earnst.Jr";
		dmc2015Names[9] = "Orsonville";
		dmc2015Names[10] = "Ruddy";
		dmc2015Names[11] = "D.Walnutz";
		dmc2015Names[12] = "B.Allman";
		dmc2015Names[13] = "R.Gourd";
		dmc2015Names[14] = "Fonte";
		dmc2015Names[15] = "M.Walnutz";
		dmc2015Names[16] = "Biffman";
		dmc2015Names[17] = "Kenstone";
		dmc2015Names[18] = "Ky.Bush";
		dmc2015Names[19] = "Truman.Jr";
		dmc2015Names[20] = "Toney";
		dmc2015Names[21] = "Peartree";
		dmc2015Names[22] = "Fireball";
		dmc2015Names[23] = "Hollace";
		dmc2015Names[24] = "S.Gourd";
		dmc2015Names[25] = "Rich";
		dmc2015Names[27] = "Junior";
		dmc2015Names[28] = "Daveyson";
		dmc2015Names[29] = "Harvey";
		dmc2015Names[30] = "Wong";
		dmc2015Names[33] = "Giant";
		dmc2015Names[37] = "L.Schrader";
		dmc2015Names[41] = "L.Petterson";
		dmc2015Names[43] = "R.Petterson";
		dmc2015Names[44] = "Laborde";
		dmc2015Names[46] = "Speed";
		dmc2015Names[48] = "Seventine";
		dmc2015Names[50] = "Brodine";
		dmc2015Names[51] = "Ku.Bush";
		dmc2015Names[52] = "Smutt";
		dmc2015Names[55] = "Minilund";
		dmc2015Names[66] = "Dick";
		dmc2015Names[67] = "Buddy";
		dmc2015Names[71] = "Circus";
		dmc2015Names[77] = "Hornet.Jr";
		dmc2015Names[80] = "Kenny";
		dmc2015Names[88] = "Jarrold";
		dmc2015Names[89] = "Butcher";
		dmc2015Names[91] = "Shepherd";
		dmc2015Names[92] = "Herbie";
		dmc2015Names[94] = "Elio";
		dmc2015Names[97] = "Amiss";
		dmc2015Names[99] = "Eddison";
		
		dmc2015Teams[0] = "DM1";
		dmc2015Teams[1] = "DM1";
		dmc2015Teams[2] = "DM1";
		dmc2015Teams[3] = "DM1";
		dmc2015Teams[4] = "DM1";
		dmc2015Teams[5] = "DM1";
		dmc2015Teams[6] = "DM1";
		dmc2015Teams[7] = "DM1";
		dmc2015Teams[8] = "DM1";
		dmc2015Teams[9] = "DM1";
		dmc2015Teams[10] = "DM1";
		dmc2015Teams[11] = "DM1";
		dmc2015Teams[12] = "DM1";
		dmc2015Teams[13] = "DM1";
		dmc2015Teams[14] = "DM1";
		dmc2015Teams[15] = "DM1";
		dmc2015Teams[16] = "DM1";
		dmc2015Teams[17] = "DM1";
		dmc2015Teams[18] = "DM1";
		dmc2015Teams[19] = "DM1";
		dmc2015Teams[20] = "DM1";
		dmc2015Teams[21] = "DM1";
		dmc2015Teams[22] = "DM1";
		dmc2015Teams[23] = "DM1";
		dmc2015Teams[24] = "DM1";
		dmc2015Teams[25] = "DM1";
		dmc2015Teams[27] = "DM1";
		dmc2015Teams[28] = "DM1";
		dmc2015Teams[29] = "DM1";
		dmc2015Teams[30] = "DM1";
		dmc2015Teams[33] = "DM1";
		dmc2015Teams[37] = "DM1";
		dmc2015Teams[41] = "DM1";
		dmc2015Teams[43] = "DM1";
		dmc2015Teams[44] = "DM1";
		dmc2015Teams[46] = "DM1";
		dmc2015Teams[48] = "DM1";
		dmc2015Teams[50] = "DM1";
		dmc2015Teams[51] = "DM1";
		dmc2015Teams[52] = "DM1";
		dmc2015Teams[55] = "DM1";
		dmc2015Teams[66] = "DM1";
		dmc2015Teams[67] = "DM1";
		dmc2015Teams[71] = "DM1";
		dmc2015Teams[77] = "DM1";
		dmc2015Teams[80] = "DM1";
		dmc2015Teams[88] = "DM1";
		dmc2015Teams[89] = "DM1";
		dmc2015Teams[91] = "DM1";
		dmc2015Teams[92] = "DM1";
		dmc2015Teams[94] = "DM1";
		dmc2015Teams[97] = "DM1";
		dmc2015Teams[99] = "DM1";
		
		dmc2015Manufacturer[0] = "CHV";
		dmc2015Manufacturer[1] = "FRD";
		dmc2015Manufacturer[2] = "FRD";
		dmc2015Manufacturer[3] = "CHV";
		dmc2015Manufacturer[4] = "CHV";
		dmc2015Manufacturer[5] = "CHV";
		dmc2015Manufacturer[6] = "FRD";
		dmc2015Manufacturer[7] = "FRD";
		dmc2015Manufacturer[8] = "CHV";
		dmc2015Manufacturer[9] = "FRD";
		dmc2015Manufacturer[10] = "FRD";
		dmc2015Manufacturer[11] = "FRD";
		dmc2015Manufacturer[12] = "FRD";
		dmc2015Manufacturer[13] = "FRD";
		dmc2015Manufacturer[14] = "CHV";
		dmc2015Manufacturer[15] = "FRD";
		dmc2015Manufacturer[16] = "FRD";
		dmc2015Manufacturer[17] = "FRD";
		dmc2015Manufacturer[18] = "TYT";
		dmc2015Manufacturer[19] = "TYT";
		dmc2015Manufacturer[20] = "CHV";
		dmc2015Manufacturer[21] = "FRD";
		dmc2015Manufacturer[22] = "FRD";
		dmc2015Manufacturer[23] = "FRD";
		dmc2015Manufacturer[24] = "CHV";
		dmc2015Manufacturer[25] = "CHV";
		dmc2015Manufacturer[27] = "PNT";
		dmc2015Manufacturer[28] = "FRD";
		dmc2015Manufacturer[29] = "CHV";
		dmc2015Manufacturer[30] = "PNT";
		dmc2015Manufacturer[33] = "FRD";
		dmc2015Manufacturer[37] = "FRD";
		dmc2015Manufacturer[41] = "DDG";
		dmc2015Manufacturer[43] = "DDG";
		dmc2015Manufacturer[44] = "CHV";
		dmc2015Manufacturer[46] = "FRD";
		dmc2015Manufacturer[48] = "CHV";
		dmc2015Manufacturer[50] = "CHV";
		dmc2015Manufacturer[51] = "FRD";
		dmc2015Manufacturer[52] = "FRD";
		dmc2015Manufacturer[55] = "FRD";
		dmc2015Manufacturer[66] = "FRD";
		dmc2015Manufacturer[67] = "DDG";
		dmc2015Manufacturer[71] = "FRD";
		dmc2015Manufacturer[77] = "FRD";
		dmc2015Manufacturer[80] = "FRD";
		dmc2015Manufacturer[88] = "FRD";
		dmc2015Manufacturer[89] = "FRD";
		dmc2015Manufacturer[91] = "FRD";
		dmc2015Manufacturer[92] = "FRD";
		dmc2015Manufacturer[94] = "FRD";
		dmc2015Manufacturer[97] = "TYT";
		dmc2015Manufacturer[99] = "FRD";
		
		dmc2015Types[0] = "Legend";
		dmc2015Types[1] = "Legend";
		dmc2015Types[2] = "Legend";
		dmc2015Types[3] = "Legend";
		dmc2015Types[4] = "Legend";
		dmc2015Types[5] = "Legend";
		dmc2015Types[6] = "Legend";
		dmc2015Types[7] = "Legend";
		dmc2015Types[8] = "Legend";
		dmc2015Types[9] = "Legend";
		dmc2015Types[10] = "Legend";
		dmc2015Types[11] = "Legend";
		dmc2015Types[12] = "Legend";
		dmc2015Types[13] = "Legend";
		dmc2015Types[14] = "Legend";
		dmc2015Types[15] = "Legend";
		dmc2015Types[16] = "Legend";
		dmc2015Types[17] = "Legend";
		dmc2015Types[18] = "Legend";
		dmc2015Types[19] = "Legend";
		dmc2015Types[20] = "Legend";
		dmc2015Types[21] = "Legend";
		dmc2015Types[22] = "Legend";
		dmc2015Types[23] = "Legend";
		dmc2015Types[24] = "Legend";
		dmc2015Types[25] = "Legend";
		dmc2015Types[27] = "Legend";
		dmc2015Types[28] = "Legend";
		dmc2015Types[29] = "Legend";
		dmc2015Types[30] = "Legend";
		dmc2015Types[33] = "Legend";
		dmc2015Types[37] = "Legend";
		dmc2015Types[41] = "Legend";
		dmc2015Types[43] = "Legend";
		dmc2015Types[44] = "Legend";
		dmc2015Types[46] = "Legend";
		dmc2015Types[48] = "Legend";
		dmc2015Types[50] = "Legend";
		dmc2015Types[51] = "Legend";
		dmc2015Types[52] = "Legend";
		dmc2015Types[55] = "Legend";
		dmc2015Types[66] = "Legend";
		dmc2015Types[67] = "Legend";
		dmc2015Types[71] = "Legend";
		dmc2015Types[77] = "Legend";
		dmc2015Types[80] = "Legend";
		dmc2015Types[88] = "Legend";
		dmc2015Types[89] = "Legend";
		dmc2015Types[91] = "Legend";
		dmc2015Types[92] = "Legend";
		dmc2015Types[94] = "Legend";
		dmc2015Types[97] = "Legend";
		dmc2015Types[99] = "Legend";

		dmc2015Rarity[0] = 1;
		dmc2015Rarity[1] = 1;
		dmc2015Rarity[2] = 1;
		dmc2015Rarity[3] = 3;
		dmc2015Rarity[4] = 1;
		dmc2015Rarity[5] = 1;
		dmc2015Rarity[6] = 2;
		dmc2015Rarity[7] = 1;
		dmc2015Rarity[8] = 2;
		dmc2015Rarity[9] = 3;
		dmc2015Rarity[10] = 1;
		dmc2015Rarity[11] = 1;
		dmc2015Rarity[12] = 1;
		dmc2015Rarity[13] = 1;
		dmc2015Rarity[14] = 1;
		dmc2015Rarity[15] = 2;
		dmc2015Rarity[16] = 1;
		dmc2015Rarity[17] = 1;
		dmc2015Rarity[18] = 1;
		dmc2015Rarity[19] = 1;
		dmc2015Rarity[20] = 1;
		dmc2015Rarity[21] = 1;
		dmc2015Rarity[22] = 3;
		dmc2015Rarity[23] = 1;
		dmc2015Rarity[24] = 1;
		dmc2015Rarity[25] = 1;
		dmc2015Rarity[27] = 2;
		dmc2015Rarity[28] = 1;
		dmc2015Rarity[29] = 1;
		dmc2015Rarity[30] = 1;
		dmc2015Rarity[33] = 1;
		dmc2015Rarity[37] = 1;
		dmc2015Rarity[41] = 1;
		dmc2015Rarity[43] = 3;
		dmc2015Rarity[44] = 1;
		dmc2015Rarity[46] = 1;
		dmc2015Rarity[48] = 3;
		dmc2015Rarity[50] = 1;
		dmc2015Rarity[51] = 1;
		dmc2015Rarity[52] = 1;
		dmc2015Rarity[55] = 2;
		dmc2015Rarity[66] = 1;
		dmc2015Rarity[67] = 1;
		dmc2015Rarity[71] = 1;
		dmc2015Rarity[77] = 1;
		dmc2015Rarity[80] = 1;
		dmc2015Rarity[88] = 1;
		dmc2015Rarity[89] = 1;
		dmc2015Rarity[91] = 1;
		dmc2015Rarity[92] = 1;
		dmc2015Rarity[94] = 1;
		dmc2015Rarity[97] = 1;
		dmc2015Rarity[99] = 1;
	}
	
	public static string shortenedType(string type){
		switch(type){
			case "Dominator":
				type="Domin.";
				break;
			case "Intimidator":
				type="Intim.";
				break;
			case "Strategist":
				type="Strat.";
				break;
			case "Closer":
				type="Close.";
				break;
			case "Blocker":
				type="Block.";
				break;
			case "Legend":
				type="Legen.";
				break;
			case "Rookie":
				type="Rook.";
				break;
			default:
				type="";
				break;
		}
	return type;
	}
}
