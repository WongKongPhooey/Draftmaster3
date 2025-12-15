using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.UI;

public class CameraManager : MonoBehaviour
{
    private static CinemachineCamera actionedCamera;
	private static Transform followCamera;
	public GameObject centeredCamera;

	private static GameObject thePlayer;
    private static TrackInfo currentTrackInfo;
    private static int[] straightLength, turnLength, turnAngle;

	private static int trackLength;
	private static int totalTurns;

	public static bool cameraCentering;
	public static float cameraOffset;

	private RectTransform topBar, bottomBar;

    void Start(){
        setFPS();
		cameraCentering = true;
		cameraOffset = 0;

		createLetterbox();
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

	void createLetterbox(){
		followCamera = GameObject.Find("FollowCamera").GetComponent<Transform>();
		GameObject tBar = new GameObject("topBar", typeof(Image));
		tBar.transform.SetParent(followCamera, false);
		tBar.GetComponent<Image>().color = Color.black;
		topBar = tBar.GetComponent<RectTransform>();
		topBar.anchorMin = new Vector2(0,1);
		topBar.anchorMax = new Vector2(1,1);
		topBar.sizeDelta = new Vector2(0,300);
	}
}
