using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
	public GameObject environmentRoot;
	public static Material scrollingMaterial;
	public static Material staticMaterial;

	public static float circuitRotation;

	public static float motionOffset;

	void Awake()
	{	
		scrollingMaterial = Resources.Load("Materials/ScrollingMaterial", typeof(Material)) as Material;
		staticMaterial = Resources.Load("Materials/StaticMaterial", typeof(Material)) as Material;

		circuitRotation = 0;
	}
	
	// Update is called once per frame
	void FixedUpdate() {
         environmentRoot.transform.position = new Vector3(CameraManager.cameraOffset, environmentRoot.transform.position.y, environmentRoot.transform.position.z);
	}

	public static Material getScrollingShader(){
		return scrollingMaterial;
	}

	public static void setOffset(float offset){
		motionOffset = offset;
	}
}
