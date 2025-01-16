using UnityEngine;

public class EnvironmentObject : MonoBehaviour
{
   	[SerializeField]
    private bool[] straights,corners;
    [SerializeField]
    private bool scrolling;
	[SerializeField]
	private bool startsVisible;
	public int specificStartLocation;
	public int specificEndLocation;
	
	Renderer[] childRenderers;
	BoxCollider[] childColliders;
	Material[] childMaterials;

	private static Material scrollingMaterial;
	private static Material staticMaterial;

    private float playerLocation;
	private bool scrollActive;
	private bool scrollEnded;
    private bool isVisible;

	void Awake(){

		childRenderers = GetComponentsInChildren<Renderer>();
		childColliders = GetComponentsInChildren<BoxCollider>();

		if(startsVisible == true){
			toggleVisibility(true);
		} else {
			toggleVisibility(false);
		}
		scrollActive = false;
		scrollEnded = false;

		if(specificStartLocation > specificEndLocation){
			toggleVisibility(true);
		}

		straights = new bool[RaceManager.totalTurns];
		corners = new bool[RaceManager.totalTurns];
	}
	
	// Update is called once per frame
	void FixedUpdate() {
		
        playerLocation = RaceManager.playerLocation;

        if(specificStartLocation != 0){
            if((playerLocation > (specificStartLocation - 100))
            && isVisible == false){
                this.transform.position = new Vector3(specificStartLocation - 100, transform.position.y,0);
				scrollEnded = false;
                toggleVisibility(true);
				toggleScrollMotion(false);
            }

            if((playerLocation > (specificEndLocation + 100))
            && isVisible == false){
                toggleVisibility(false);
				toggleScrollMotion(false);
                this.transform.position = new Vector3(specificEndLocation - 100, transform.position.y,0);
            }
        }

        if(isVisible == true){
			if(scrolling == true){
				//The player has just passed the zero point of the scrolling object..
				//So now it should scroll using the shader.
				if(playerLocation > specificStartLocation){
					if(scrollActive != true){
						scrollActive = true;
						scrollEnded = false;
						toggleScrollMotion(true);
					}
				}
				//100 metres before the object leaves, the scrolling stops
				//The final 100 metres is the object sliding out of view.
				if(playerLocation > (specificEndLocation - 100)){
					if(scrollActive != false){
						scrollActive = false;
						toggleScrollMotion(false);
						scrollEnded = true;
					}
				}
				if(scrollActive == false){
					if(scrollEnded == true){
						this.transform.position = new Vector3((playerLocation + 100) - specificEndLocation, transform.position.y,0);
					} else {
						this.transform.position = new Vector3(playerLocation - specificStartLocation, transform.position.y,0);
					}
				}
			} else {
				this.transform.position = new Vector3(playerLocation - specificStartLocation, transform.position.y,0);
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
	}

	void toggleScrollMotion(bool scrollMotion){
		if(scrollMotion == true){
			if(this.gameObject.GetComponent<SpriteRenderer>().material == staticMaterial){
				this.gameObject.GetComponent<SpriteRenderer>().material = scrollingMaterial;
			}
		}
		if(scrollMotion == false){
			scrollingMaterial = this.gameObject.GetComponent<SpriteRenderer>().material;
			this.gameObject.GetComponent<SpriteRenderer>().material = EnvironmentManager.staticMaterial;
		}
	}
}
