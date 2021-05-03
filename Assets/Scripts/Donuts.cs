using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Donuts : MonoBehaviour {
 
    [Range(-1.0f, 1.0f)]
    public float ForceDirection = 0.6f;
 
    public float speedMultiplier = 1;
 
    public bool worldPivote = false;
 
    private Space spacePivot = Space.Self;
 
    void Start()
    {
        if (worldPivote) spacePivot = Space.World;
    }
 
    void Update()
    {
        this.transform.Rotate(0 * speedMultiplier, ForceDirection * speedMultiplier, 0 * speedMultiplier, spacePivot);
    }
 
}
