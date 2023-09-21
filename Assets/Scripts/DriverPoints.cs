using UnityEngine;
using System.Collections;

public class DriverPoints : MonoBehaviour {
	
	public static int[] pointsTotal = new int[100];
	
	// Use this for initialization
	void Start () {		
	}

	public static void resetPoints(string seriesPrefix) {
		if(ModData.isModSeries(seriesPrefix) == true){
			for(int i = 0; i < 100;i++){
				if(ModData.getName(seriesPrefix, i) == null){
					if(PlayerPrefs.HasKey("ChampionshipPoints"  + i)){
						PlayerPrefs.DeleteKey("ChampionshipPoints"  + i);
					}
				} else {
					PlayerPrefs.SetInt("ChampionshipPoints"  + i,0);
					pointsTotal[i] = 0;
				}
			}
		} else {
			for(int i = 0; i < 100;i++){
				if(DriverNames.getName(seriesPrefix, i) == null){
					if(PlayerPrefs.HasKey("ChampionshipPoints"  + i)){
						PlayerPrefs.DeleteKey("ChampionshipPoints"  + i);
					}
				} else {
					PlayerPrefs.SetInt("ChampionshipPoints"  + i,0);
					pointsTotal[i] = 0;
				}
			}
		}
	}
}
