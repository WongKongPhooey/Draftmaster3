using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AltPaints : MonoBehaviour {
	
	public static string[,] cup2020AltPaintNames = new string[101,10];
	public static int[,] cup2020AltPaintRarity = new int[101,10];
	
    // Start is called before the first frame update
    void Start(){
		loadAlts();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public static void loadAlts(){
		cup2020AltPaintNames[2,1] = "#1 Patriot";
		cup2020AltPaintNames[3,1] = "#1 Halloween";
		cup2020AltPaintNames[3,2] = "#2 Icon";
		cup2020AltPaintNames[3,3] = "#3 Wrangled";
		cup2020AltPaintNames[3,4] = "#4 All Star";
		cup2020AltPaintNames[7,1] = "#1 Insure";
		cup2020AltPaintNames[9,1] = "#1 Hoot";
		cup2020AltPaintNames[12,1] = "#1 Kobe";
		cup2020AltPaintNames[13,1] = "#1 Military";
		cup2020AltPaintNames[17,1] = "#1 Sunny";
		cup2020AltPaintNames[18,1] = "#1 Halloween";
		cup2020AltPaintNames[19,1] = "#1 USO";
		cup2020AltPaintNames[22,1] = "#1 Tribute";
		cup2020AltPaintNames[27,1] = "#1 Cup";
		cup2020AltPaintNames[32,1] = "#1 Halloween";
		cup2020AltPaintNames[42,1] = "#1 Halloween";
		cup2020AltPaintNames[43,1] = "#1 BLM";
		cup2020AltPaintNames[48,1] = "#1 Champ";
		cup2020AltPaintNames[48,2] = "#2 Cobalt";
		cup2020AltPaintNames[48,3] = "#3 All Star";
		cup2020AltPaintNames[51,1] = "#1 Warhawk";
		cup2020AltPaintNames[88,1] = "#1 Cup";
		cup2020AltPaintNames[88,2] = "#2 Halloween";
	}
}
