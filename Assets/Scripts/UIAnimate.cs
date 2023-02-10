using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIAnimate : MonoBehaviour
{
	public int animOffset = 0;
	public bool startHidden;
	public bool fadeInStart;
	Vector3 shakeStartPos;
	float shakeStrength;
	
    // Start is called before the first frame update
    void Awake(){
		if(startHidden == true){
			hide();
		}
		if(fadeInStart == true){
			scaleIn();
		}
	}

    // Update is called once per frame
    void Update()
    {
    }
	
	public void scaleIn(){
		LeanTween.scale(gameObject, new Vector3(1f,1f,1f), 0.5f).setDelay(0.01f * animOffset).setEase(LeanTweenType.easeInOutCubic);
	}
	
	public void scaleOut(){
		LeanTween.scale(gameObject, new Vector3(0f,0f,0f), 0.5f).setDelay(0.01f * animOffset).setEase(LeanTweenType.easeInOutCubic);
	}
	
	public void shakeCamX(Vector3 startPos, float strength){
		shakeStartPos = startPos;
		shakeStrength = strength;
		LeanTween.moveX(gameObject, shakeStrength, 0.1f).setOnComplete(resetCam);
	}
	public void shakeCamZ(Vector3 startPos, float strength){
		shakeStartPos = startPos;
		shakeStrength = strength;
		LeanTween.moveZ(gameObject, shakeStrength, 0.1f).setOnComplete(resetCam);
	}
	void resetCam(){
		LeanTween.move(gameObject, shakeStartPos, 0.1f);
		Debug.Log("Shake Strength: " + shakeStrength);
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
		//Debug.Log("Hide the popup");
	}
}