using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GarageTileFunctions : MonoBehaviour
{
	public string carSeriesPrefix;
	public int carNum;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public void openCarInfo(){
		PlayerPrefs.SetInt("CarFocus",carNum);
		PlayerPrefs.SetString("SeriesFocus",carSeriesPrefix);
		SceneManager.LoadScene("Levels/SingleCar");
	}
}
