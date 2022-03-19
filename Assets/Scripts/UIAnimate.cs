using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIAnimate : MonoBehaviour
{
	public int animOffset = 0;
	public bool startHidden;
	
    // Start is called before the first frame update
    void Start(){
		if(startHidden == true){
			hide();
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public void scaleIn(){
		LeanTween.scale(gameObject, new Vector3(1f,1f,1f), 0.5f).setDelay(0.05f * animOffset).setEase(LeanTweenType.easeInOutCubic);
	}
	
	public void scaleOut(){
		LeanTween.scale(gameObject, new Vector3(0f,0f,0f), 0.5f).setDelay(0.05f * animOffset).setEase(LeanTweenType.easeInOutCubic);
	}
	
	public void show(){
		LeanTween.scale(gameObject, new Vector3(1f,1f,1f), 0f);
	}
	
	public void hide(){
		LeanTween.scale(gameObject, new Vector3(0f,0f,0f), 0f);
	}
}