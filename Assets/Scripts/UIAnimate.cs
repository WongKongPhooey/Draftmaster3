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
	
	public void setCardDown(){
		LeanTween.rotateAround(gameObject, Vector3.up, 180, 0f);
	}
	
	public void flipCardXStart(){
		LeanTween.rotateAround(gameObject, Vector3.up, 90, 0.5f).setDelay(0.1f * animOffset).setEase(LeanTweenType.easeInOutCubic);
	}
	
	public void flipCardBacking(){
		LeanTween.scale(gameObject, new Vector3(0f,0f,0f), 0f).setDelay((0.1f * animOffset) + 0.5f);
	}
	
	public void flipCardXEnd(){
		LeanTween.rotateAround(gameObject, Vector3.up, 90, 0.5f).setEase(LeanTweenType.easeInOutCubic);
	}
	
	public void show(){
		LeanTween.scale(gameObject, new Vector3(1f,1f,1f), 0f);
	}
	
	public void hide(){
		LeanTween.scale(gameObject, new Vector3(0f,0f,0f), 0f);
	}
}