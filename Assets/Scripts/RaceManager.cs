using UnityEngine;

public class RaceManager : MonoBehaviour
{
    Camera mainCam;

	public static GameObject thePlayer;
    public static TrackInfo currentTrackInfo;
    public static int[] straightLength, turnLength, turnAngle;

	public static int trackLength;
	public static int totalTurns;
	public static int currentLapLength;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start(){
        setFPS();
        setCameraZoom();
        trackInit();
    }

    // Update is called once per frame
    void Update(){

        //float frameRotation = turnAngle[turn] / (turnLength[turn] / vehicleLogic.speedMetres) * Time.deltaTime;
       //mainCam.transform.Rotate(0,0,-frameRotation);
    }

    void trackInit(){
        currentTrackInfo = Resources.Load<TrackInfo>("Tracks/Phoenix");

        totalTurns = currentTrackInfo.totalTurns;
		straightLength = new int[totalTurns];
		turnLength = new int[totalTurns];
		turnAngle = new int[totalTurns];
		for(int i=0;i<totalTurns;i++){
			Debug.Log("Straight Length " + i + ": " + currentTrackInfo.straightLengths[i]);
			straightLength[i] = currentTrackInfo.straightLengths[i];
			turnLength[i] = currentTrackInfo.turnLengths[i];
			turnAngle[i] = currentTrackInfo.turnAngles[i];
			trackLength += straightLength[i] + turnLength[i];
		}
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

    void setCameraZoom(){
        mainCam = GameObject.Find("Main Camera").GetComponent<Camera>();
		mainCam.orthographicSize = 20.0f;
    }
}