using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{

	public bool[] straights = new bool[6];
	public bool[] corners = new bool[6];
    public static TrackInfo currentTrackInfo;
    Renderer[] childRenderers;
	BoxCollider[] childColliders;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start(){
        currentTrackInfo = Resources.Load<TrackInfo>("Tracks/Phoenix");

        straights = new bool[currentTrackInfo.totalTurns];
	    corners = new bool[currentTrackInfo.totalTurns];
    }

    // Update is called once per frame
    void Update(){
        
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

    void toggleElement(bool isShowing = false){
		foreach(Renderer rend in childRenderers){
			rend.enabled = isShowing;
		}
		foreach(BoxCollider col in childColliders){
			col.enabled = isShowing;
		}
	}
}
