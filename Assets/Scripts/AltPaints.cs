using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AltPaints : MonoBehaviour {
	
	public static string[,] cup2020AltPaintNames = new string[101,5];
	public static int[,] cup2020AltPaintRarity = new int[101,5];
	
    // Start is called before the first frame update
    void Start(){
        
		cup2020AltPaintRarity[18,1] = 4;
		cup2020AltPaintRarity[27,1] = 2;
		cup2020AltPaintRarity[32,1] = 2;
		
		cup2020AltPaintNames[18,1] = "#1 Halloween";
		cup2020AltPaintNames[27,1] = "#1 Panini";
		cup2020AltPaintNames[32,1] = "#1 Scoob";
		
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
