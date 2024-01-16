using UnityEngine;
using System.Collections;

public class DriverPoints : MonoBehaviour {
	
	public static int[] pointsTotal = new int[100];
	
	// Use this for initialization
	void Start () {		
	}

	//PlayerPrefs.DeleteKey("SeriesChampionship" + seriesIndex + "Points" + i);

	public static void resetPoints(string seriesIndex, string seriesPrefix) {
		if(ModData.isModSeries(seriesPrefix) == true){
			for(int i = 0; i < 100;i++){
				if(ModData.getName(seriesPrefix, i) == null){
					if(PlayerPrefs.HasKey("SeriesChampionship" + seriesIndex + "Points"  + i)){
						PlayerPrefs.DeleteKey("SeriesChampionship" + seriesIndex + "Points"  + i);
					}
				} else {
					PlayerPrefs.SetInt("SeriesChampionship" + seriesIndex + "Points"  + i,0);
					pointsTotal[i] = 0;
				}
			}
		} else {
			for(int i = 0; i < 100;i++){
				if(DriverNames.getName(seriesPrefix, i) == null){
					if(PlayerPrefs.HasKey("SeriesChampionship" + seriesIndex + "Points"  + i)){
						PlayerPrefs.DeleteKey("SeriesChampionship" + seriesIndex + "Points"  + i);
					}
				} else {
					PlayerPrefs.SetInt("SeriesChampionship" + seriesIndex + "Points"  + i,0);
					pointsTotal[i] = 0;
				}
			}
		}
	}
}
