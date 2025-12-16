using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.UI;

public class CameraManager : MonoBehaviour
{
    private static CinemachineCamera actionedCamera;
	//public GameObject centeredCamera;

	private static GameObject thePlayer;
    private static TrackInfo currentTrackInfo;
    private static int[] straightLength, turnLength, turnAngle;

	private static int trackLength;
	private static int totalTurns;

	public static bool cameraCentering;
	public static float cameraOffset;

	public GameObject letterboxTop, letterboxBottom;
	private RectTransform topBar, bottomBar;

    void Start(){
        setFPS();
		cameraCentering = true;
		cameraOffset = 0;

		topBar = letterboxTop.GetComponent<RectTransform>();
		bottomBar = letterboxBottom.GetComponent<RectTransform>();
    }

	// Update is called once per frame
	void FixedUpdate() {
		/*if((centeredCamera != null)&&(cameraCentering == true)){
			cameraOffset = centeredCamera.transform.position.x;
        }*/
	}

    public static void setRotation(float angle){
        actionedCamera.Lens.Dutch = -angle;
    }

    public static void setPlayer(GameObject playerVehicle, float zoom = 18f){
        thePlayer = playerVehicle;
		actionedCamera = GameObject.Find("FollowCamera").GetComponent<CinemachineCamera>();
        actionedCamera.Lens.OrthographicSize = zoom;
		actionedCamera.Follow = thePlayer.transform;
	}

	public static void toggleLetterbox(){

	}

    void setFPS(){
        //int fpsCap = PlayerPrefs.GetInt("FPSLimit");
		int fpsCap=3;
		switch(fpsCap){
			case 1:
				Application.targetFrameRate = 30;
				break;
			case 2:
				Application.targetFrameRate = 60;
				break;
			case 3:
				Application.targetFrameRate = 120;
				#if UNITY_EDITOR
				Application.targetFrameRate = -1;
				#endif
				break;
			default:
				Application.targetFrameRate = 60;
				break;
		}
    }
}
