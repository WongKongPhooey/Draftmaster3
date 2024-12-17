using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    private static CinemachineCamera actionedCamera;

	private static GameObject thePlayer;
    private static TrackInfo currentTrackInfo;
    private static int[] straightLength, turnLength, turnAngle;

	private static int trackLength;
	private static int totalTurns;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start(){
        setFPS();
    }

    public static void setRotation(float angle){
        actionedCamera.Lens.Dutch = -angle;
    }

    public static void setPlayer(GameObject playerVehicle){
        thePlayer = playerVehicle;
		actionedCamera = GameObject.Find("FollowCamera").GetComponent<CinemachineCamera>();
        actionedCamera.Lens.OrthographicSize = 18f;
		actionedCamera.Follow = thePlayer.transform;
	}

    void setFPS(){
        int fpsCap = PlayerPrefs.GetInt("FPSLimit");
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
