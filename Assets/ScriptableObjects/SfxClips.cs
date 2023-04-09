using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random=UnityEngine.Random;

[CreateAssetMenu(fileName = "Data", menuName = "Sounds/Clips", order = 1)]

public class SfxClips : ScriptableObject {
    // Start is called before the first frame update
	
	public AudioClip[] skidClips, scrapeClips, impactClips, gearShiftClips, grassClips;
	
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
