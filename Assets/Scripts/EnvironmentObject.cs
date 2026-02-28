using UnityEditor.Rendering;
using UnityEngine;

public class EnvironmentObject : MonoBehaviour
{

   	[SerializeField]
    private bool[] straights,corners;
    [SerializeField]
    private bool scrolling;
	[SerializeField]
	private bool startsVisible;
	public int specificStartLocation, specificEndLocation;
	
	private float centeredStartLocation, centeredEndLocation; 

	private float objectLength;
	private SpriteRenderer objectRenderer;

	Renderer[] childRenderers;
	BoxCollider[] childColliders;
	Material[] childMaterials;

	private static Material scrollingMaterial;
	private static Material staticMaterial;

	private MaterialPropertyBlock materialOverride;

    private float playerLocation;

	public static float locationOffset;
	private bool scrollActive;
	private bool scrollEnded;
    private bool isVisible;

	public bool debugObject;

	void Awake(){

		playerLocation = RaceManager.playerLocation + CameraManager.cameraOffset;

		childRenderers = GetComponentsInChildren<Renderer>();
		childColliders = GetComponentsInChildren<BoxCollider>();

		objectRenderer = this.gameObject.GetComponent<SpriteRenderer>();
		objectLength = this.gameObject.transform.localScale.x;
		
		if(objectRenderer.drawMode == SpriteDrawMode.Tiled){
			centeredStartLocation = specificStartLocation + ((objectLength * objectRenderer.size.x) / 2);
			centeredEndLocation = specificEndLocation - ((objectLength * objectRenderer.size.x) / 2);
		} else {
			//Start position defines when the middle of object.x is centered at 0;
			centeredStartLocation = specificStartLocation + (objectLength / 2);
			centeredEndLocation = specificEndLocation - (objectLength / 2);
		}

		materialOverride = new MaterialPropertyBlock();

		if(startsVisible == true){
			toggleVisibility(true);
		} else {
			toggleVisibility(false);
		}
		scrollActive = false;
		scrollEnded = false;

		//Object must be visible at the start/finish line
		if((specificStartLocation > specificEndLocation)&&(specificEndLocation != 0)){

			#if UNITY_EDITOR
			if(debugObject == true){
				Debug.Log("Object: " + this.gameObject.name + " - Starts Visible: " + playerLocation + " - Centered Start Location: " + centeredStartLocation + " - Centered End Location: " + specificEndLocation);
			}
			#endif

			scrollActive = true;
			toggleVisibility(true);
			toggleScrollMotion(true);
		}

		straights = new bool[RaceManager.totalTurns];
		corners = new bool[RaceManager.totalTurns];
	}
	
	// Update is called once per frame
	void FixedUpdate() {
		
		#if UNITY_EDITOR
		if(debugObject == true){
			//Debug.Log("Object: " + this.gameObject.name + " - Player Location: " + playerLocation + " - Centered Start Location: " + centeredStartLocation + " - Camera Offset:" + CameraManager.cameraOffset);
		}
		#endif

		//Nothing uses motion when the environment is fixed at 0
		if (RaceManager.thePlayer.tag == "PlayerOnFoot"){
			return;
		}

        playerLocation = RaceManager.playerLocation + CameraManager.cameraOffset;

		//If the object is approaching (<100m)..
		//And is invisible, and not past the centrepoint..
		//Spawn it in
		if ((playerLocation > (centeredStartLocation - 100))
		&& (isVisible == false)
		&& (scrollEnded == false))
		{
			this.transform.position = new Vector3(centeredStartLocation - 100, transform.position.y, 0);
			scrollEnded = false;
			toggleVisibility(true);
			toggleScrollMotion(false);
		}

		//Once the object has been and gone..
		//Hide it again, and reset it's position
        if(specificStartLocation < specificEndLocation){

            if((playerLocation > (specificEndLocation + 100))
            && isVisible == true){
                toggleVisibility(false);
				toggleScrollMotion(false);
                this.transform.position = new Vector3(0, transform.position.y,0);
            }
		} else {
			//For cases where the object appears before the start/finish, and disappears after it
			if((playerLocation > (specificEndLocation + 100))
			&&(playerLocation < specificStartLocation)
			&&(scrolling == true)
			&&(scrollEnded == true)
			&&(isVisible == true)){
				toggleVisibility(false);
				toggleScrollMotion(false);
				this.transform.position = new Vector3(centeredEndLocation - 100, transform.position.y,0);
			}
		}

        if(isVisible == true){
			if(scrolling == true){
				//The player has just passed the zero point of the scrolling object..
				//So now it should scroll using the shader.
				if((playerLocation > centeredStartLocation)
				&&(scrollEnded == false)){
					if(scrollActive == false){
						scrollActive = true;
						toggleScrollMotion(true);
					}
				}
				//100 metres before the object leaves, the scrolling stops
				//The final 100 metres is the object sliding out of view.
				if(playerLocation > centeredEndLocation){
					if(scrollActive == true){
						scrollActive = false;
						scrollEnded = true;
						toggleScrollMotion(false);
					}
				}
				if(scrollActive == false){
					if(scrollEnded == true){
						this.transform.position = new Vector3(playerLocation - centeredEndLocation, transform.position.y,0);

						//Once out of view, hide and reset
						/*if((this.transform.position.x > 200)
						||(this.transform.position.x < -200)){
							isVisible = false;
							this.transform.position = new Vector3(0, transform.position.y,0);
						}*/
					} else {
						this.transform.position = new Vector3(playerLocation - centeredStartLocation, transform.position.y,0);
					}
				}
			} else {
				this.transform.position = new Vector3(playerLocation - centeredStartLocation, transform.position.y,0);
			}
        }
	}

	void toggleVisibility(bool isShowing = false){

		#if UNITY_EDITOR
		if(debugObject == true){
			//Debug.Log("Object: " + this.gameObject.name + " - Toggle Visibility: " + isShowing + " - Location: " + playerLocation);
		}
		#endif

        isVisible = isShowing;
		foreach(Renderer rend in childRenderers){
			rend.enabled = isShowing;
		}
		foreach(BoxCollider col in childColliders){
			col.enabled = isShowing;
		}
		scrollEnded = false;
	}

	void toggleScrollMotion(bool scrollMotion){

		#if UNITY_EDITOR
		if(debugObject == true){
			//Debug.Log("Object: " + this.gameObject.name + " - Toggle Scroll: " + scrollMotion + " - Location: " + playerLocation);
		}
		#endif

		if(scrollMotion == true){
			materialOverride.Clear();
			// Apply the MaterialPropertyBlock to the GameObject
            this.gameObject.GetComponent<SpriteRenderer>().SetPropertyBlock(materialOverride);
		}
		if(scrollMotion == false){
			materialOverride.SetFloat("_MotionOffset", RaceManager.motionOffset);
			// Apply the MaterialPropertyBlock to the GameObject
            this.gameObject.GetComponent<SpriteRenderer>().SetPropertyBlock(materialOverride);
		}
	}
}