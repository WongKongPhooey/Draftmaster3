using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FuelUI : MonoBehaviour
{
	int fuel;
    // Start is called before the first frame update
    void Start()
    {
        fuel = PlayerPrefs.GetInt("GameFuel");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}