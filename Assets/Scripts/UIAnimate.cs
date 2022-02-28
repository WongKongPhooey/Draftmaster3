using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIAnimate : MonoBehaviour
{
	public int animOffset = 0;
	
    // Start is called before the first frame update
    void Start()
    {
    
	}

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public void scaleIn(){
		LeanTween.scale(gameObject, new Vector3(1f,1f,1f), 0.5f).setDelay(0.5f + (0.05f * animOffset)).setEase(LeanTweenType.easeInOutCubic);
	}
}