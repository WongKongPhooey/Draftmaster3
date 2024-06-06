using UnityEngine;
using System.Collections;

public class EnviroToggle : MonoBehaviour {

	public bool onStraights;
	public bool onCorners;
	public bool oneStraight;
	public bool onFinishLine;
	
	//V2 enviro control
	public bool hideOnStart;
	public bool advancedPlacement;
	public bool[] straights = new bool[6];
	public bool[] corners = new bool[6];
	
	Renderer[] childRenderers;
	BoxCollider[] childColliders;
	
	public bool hideOnPit;
	public bool triOvalPit;
	public bool triOvalEntryExit;
	int pitLanePadding;
	int pitLengthPadding;
	int triOvalAngleCount;
	int triOvalAngleMax;
	float triOvalBend;
	float triOvalPitZSlip;
	public int straight;
	public bool currentState;	

	void Awake(){
		currentState = Movement.onTurn;

		if(triOvalPitZSlip == 0){
			//DEFAULT Z SLIP
			triOvalPitZSlip = 0.00196f;
		}
		if(triOvalBend == 0){
			//DEFAULT BEND
			triOvalBend = 0.03f;
		}

		childRenderers = GetComponentsInChildren<Renderer>();
		childColliders = GetComponentsInChildren<BoxCollider>();
		if(((hideOnPit == true)&&(CameraRotate.straight == 1))
		  || (hideOnStart == true)){
			toggleElement(false);
		}
	}
	
	// Update is called once per frame
	void FixedUpdate() {

		//IF ENTERING OR EXITING TURN
		if((currentState != Movement.onTurn)||(Movement.pacing == true)){
			
			//If V1 enviro setup
			if(advancedPlacement == false){
				if(Movement.onTurn == true){
					if(onCorners == true){
						toggleElement(true);
					} else {
						toggleElement(false);
					}
					if(triOvalPit == true){
						if(triOvalEntryExit == true){
							pitLengthPadding = 0;
						} else {
							pitLengthPadding = pitLanePadding;
						}
						if(((CameraRotate.turn == 4)&&(CameraRotate.cornercounter > pitLengthPadding))||((CameraRotate.turn == 1)&&(CameraRotate.cornercounter > CameraRotate.turnLength[4] - pitLengthPadding))){
							toggleElement(true);
						} else {
							toggleElement(false);
						}
					}
				} else {
					if(onStraights == true){
						if((oneStraight == true)&&(CameraRotate.straight == straight)){
							toggleElement(true);
						} else {
							if(oneStraight == false){
								toggleElement(true);
							}
						}
						if((hideOnPit == true)&&(CameraRotate.straight == 1)){
							toggleElement(false);
						}
					} else {
						if((onFinishLine == true)&&(CameraRotate.straight == 1)&&(CameraRotate.straightcounter == 0)){
							toggleElement(true);
						} else {
							toggleElement(false);
						}
					}
				}
			} else {
				//V2 enviro setup
				if(Movement.onTurn == true){
					if(corners[CameraRotate.turn] == true){
						toggleElement(true);
					} else {
						toggleElement(false);
					}
				} else {
					if(straights[CameraRotate.straight] == true){
						toggleElement(true);
					} else {
						toggleElement(false);
					}
				}
			}

			//UPDATE CURRENT STATE
			currentState = Movement.onTurn;
		}
		if(triOvalPit == true){
			Renderer[] subAsset = GetComponentsInChildren<Renderer>();
			if(triOvalEntryExit == true){
				pitLengthPadding = 0;
			} else {
				pitLengthPadding = pitLanePadding;
			}
			if(((CameraRotate.turn == 4)&&(CameraRotate.cornercounter > pitLengthPadding))||((CameraRotate.turn == 1)&&(CameraRotate.cornercounter < ((CameraRotate.turnAngle[3] * CameraRotate.turnLength[3]) - pitLengthPadding)))){
				//Debug.Log("Corner Count:" + CameraRotate.cornercounter + ", Padding:" + pitLengthPadding);
				toggleElement(true);
			} else {
				toggleElement(false);
			}
			if(CameraRotate.turn == 4){
				triOvalAngleCount++;
				this.transform.Translate(-triOvalBend,0,triOvalPitZSlip);
				this.transform.Rotate(0,triOvalBend,0, Space.Self);
			} else {
				if(CameraRotate.turn == 1){
					triOvalAngleCount++;
					this.transform.Translate(triOvalBend,0,triOvalPitZSlip);
					this.transform.Rotate(0,triOvalBend,0, Space.Self);
				} else {
					if(triOvalAngleCount > -triOvalAngleMax){
						if(triOvalAngleCount > triOvalAngleMax){
							triOvalAngleMax = triOvalAngleCount;
						}
						triOvalAngleCount-=1;
						this.transform.Rotate(0,-triOvalBend,0, Space.Self);
					}
				}
			}
		}
	}

	void toggleElement(bool isShowing = false){
		foreach(Renderer rend in childRenderers){
			rend.enabled = isShowing;
		}
		foreach(BoxCollider col in childColliders){
			col.enabled = isShowing;
		}
	}
}
