using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    public bool[] straights;
	public bool[] corners;
    [SerializeField]
    private bool scrolling;
	public int specificLocation;
	
	Renderer[] childRenderers;
	BoxCollider[] childColliders;

	static Shader scrollingShader;
	static Shader staticShader;

    private float playerLocation;

    private bool isVisible;

	void Awake(){

		childRenderers = GetComponentsInChildren<Renderer>();
		childColliders = GetComponentsInChildren<BoxCollider>();

        isVisible = false;
        toggleVisibility(false);

		straights = new bool[RaceManager.totalTurns];
		corners = new bool[RaceManager.totalTurns];

		scrollingShader = CameraManager.scrollShader;
		staticShader = CameraManager.staticShader;
	}
	
	// Update is called once per frame
	void FixedUpdate() {
		
        playerLocation = RaceManager.playerLocation;

        if(specificLocation != 0){
            if((playerLocation > (specificLocation - 100))
            && isVisible == false){
                this.transform.position = new Vector3(specificLocation - 100, transform.position.y,0);
                toggleVisibility(true);
            }

            if((playerLocation > (specificLocation + 100))
            && isVisible == false){
                toggleVisibility(false);
                this.transform.position = new Vector3(specificLocation - 100, transform.position.y,0);
            }
        }

        if(isVisible == true){
            this.transform.position = new Vector3(playerLocation - specificLocation, transform.position.y,0);
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
}
