using System.Linq;
using UnityEngine;

public class EnvironmentObjectV2 : MonoBehaviour
{
	public Material materialInstance;
   	[SerializeField]
    private bool[] straights,corners;
    [SerializeField]
    private bool scrollable;
	[SerializeField]
	private bool startsVisible;
	public int specificStartLocation, specificEndLocation;
	
	private float centeredStartLocation, centeredEndLocation; 

	private float objectLength, objectScale;
	public float scrollSpeedOverride;
	private SpriteRenderer objectRenderer;

	Renderer[] childRenderers;
	BoxCollider[] childColliders;
	Material[] childMaterials;
	private string pixelsFromShaderName;
	private float scrollDivisor;
	
	private MaterialPropertyBlock materialOverride;

    private float playerLocation;
	private bool isScrolling;
	private bool postScroll;
    private bool isVisible;

	public bool debugObject;

	void Awake(){

		childRenderers = GetComponentsInChildren<Renderer>();
		childColliders = GetComponentsInChildren<BoxCollider>();

		objectRenderer = GetComponent<SpriteRenderer>();
		materialInstance = objectRenderer.material;
		objectLength = objectRenderer.size.x;
		objectScale = transform.localScale.x;
		
		if(objectRenderer.drawMode == SpriteDrawMode.Tiled){
			centeredStartLocation = specificStartLocation + ((objectLength * objectScale) / 2);
			centeredEndLocation = specificEndLocation - ((objectLength * objectScale) / 2);
		} else {
			//Start position defines when the middle of object.x is centered at 0;
			centeredStartLocation = specificStartLocation + (objectScale / 2);
			centeredEndLocation = specificEndLocation - (objectScale / 2);
		}

		//If material is defined, use it's name to determine the width of it. e.g. Material128 is 128px.
		if(objectRenderer.material != null){
			pixelsFromShaderName = GetNumbersFromString(objectRenderer.material.name);
		}
		float pixelSize = (pixelsFromShaderName != "") ? float.Parse(pixelsFromShaderName) : 128f;
		scrollDivisor = (scrollSpeedOverride != 0) ? scrollSpeedOverride : ((pixelSize / 512f) * 40f);

		materialOverride = new MaterialPropertyBlock();

		if((startsVisible == true)||(scrollable == false)){
			toggleVisibility(true);
		} else {
			toggleVisibility(false);
		}
		isScrolling = false;
		postScroll = false;

		//Object crosses the start/finish line whilst visible..
		if(specificStartLocation > specificEndLocation){
			toggleVisibility(true);
			toggleScrolling(true);
		}

		straights = new bool[RaceManager.totalTurns];
		corners = new bool[RaceManager.totalTurns];
	}
	
	// Update is called once per frame
	void FixedUpdate() {
		
        playerLocation = RaceManager.playerLocation;

		if(scrollable == false){
			this.transform.position = new Vector3(playerLocation - centeredStartLocation, transform.position.y, 0);
			return;
		}

		//If the object is approaching (<100m)..
		//And is invisible, and not past the centrepoint..
		//Spawn it in
		if((playerLocation > (centeredStartLocation - 100))
		&&(isVisible == false)
		&&(postScroll == false)){
			this.transform.position = new Vector3(playerLocation - centeredStartLocation, transform.position.y, 0);
			toggleVisibility(true);
			isScrolling = false;
		}

		//Once the object has been and gone..
		//Hide it again, and reset it's position
        if(specificStartLocation < specificEndLocation){

            if((playerLocation > (specificEndLocation + 100))
            && isVisible == true){
                toggleVisibility(false);
                this.transform.position = new Vector3(0, transform.position.y,0);
            }
		} else {
			/*if((playerLocation > (specificEndLocation + 100))
			&&(playerLocation > (centeredStartLocation - 100))
            && isVisible == true){
                toggleVisibility(false);
                this.transform.position = new Vector3(0, transform.position.y,0);
            }*/
		}


        if(isVisible == true){
			//The player has just passed the zero point of the scrolling object..
			//So now it should scroll using the shader.
			if((playerLocation > centeredStartLocation)
			&&(postScroll == false)){
				isScrolling = true;
			} else {
				this.transform.position = new Vector3(playerLocation - centeredStartLocation, transform.position.y,0);
			}

			if(isScrolling == true){
				this.transform.position = new Vector3(0, transform.position.y, 0);
				if(materialInstance != null){
					float rawOffset = (playerLocation - centeredStartLocation) / scrollDivisor;
					float shaderOffset = 1f - (rawOffset - Mathf.Floor(rawOffset));
					materialInstance.SetFloat("_MotionOffset", shaderOffset);
				}
			}

			//100 metres before the object leaves, the scrolling stops
			//The final 100 metres is the object sliding out of view.
			if(playerLocation > centeredEndLocation){
				if(isScrolling == true){
					isScrolling = false;
					postScroll = true;
				}
			}

			//When the object is moving out of view
			if(postScroll == true){

				this.transform.position = new Vector3(playerLocation - centeredEndLocation, transform.position.y, 0);
				
				//Once out of view, hide and reset
				if((this.transform.position.x > 200)
				||(this.transform.position.x < -200)){
					toggleVisibility(false);
					this.transform.position = new Vector3(0, transform.position.y,0);
				}
			}
        }
	}

	void toggleVisibility(bool isShowing = false){

        isVisible = isShowing;
		foreach(Renderer rend in childRenderers){
			rend.enabled = isShowing;
		}
		foreach(BoxCollider col in childColliders){
			col.enabled = isShowing;
		}
		postScroll = false;
	}

	public void resetState(){
		isScrolling = false;
		postScroll = false;
		if(scrollable == false){
			toggleVisibility(true);
		} else {
			toggleVisibility(false);
		}
		this.transform.position = new Vector3(0, transform.position.y, 0);

		if(specificStartLocation > specificEndLocation){
			toggleVisibility(true);
			isScrolling = true;
		}
	}

	void toggleScrolling(bool scrolling){

		if(scrolling == true){
			isScrolling = true;
		}
		if(scrolling == false){
			isScrolling = false;
		}
		//this.gameObject.GetComponent<SpriteRenderer>().SetPropertyBlock(materialOverride);
	}

	private static string GetNumbersFromString(string input)
	{
		string numOut = new string(input.Where(c => char.IsDigit(c)).ToArray());
		if(numOut == ""){
			return "128";
		} else {
			return numOut;
		}
	}
}