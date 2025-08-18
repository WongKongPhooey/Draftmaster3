using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
	public GameObject centeredCamera;
	public GameObject environmentRoot;
	public static Material scrollingMaterial;
	public static Material staticMaterial;

	public static float cameraOffset;
	public static float motionOffset;

	void Awake()
	{

		scrollingMaterial = Resources.Load("Materials/ScrollingMaterial", typeof(Material)) as Material;
		staticMaterial = Resources.Load("Materials/StaticMaterial", typeof(Material)) as Material;

		cameraOffset = 0;
	}
	
	// Update is called once per frame
	void FixedUpdate() {
		if (centeredCamera != null)
        {
			cameraOffset = centeredCamera.transform.position.x;
            environmentRoot.transform.position = new Vector3(cameraOffset, environmentRoot.transform.position.y, environmentRoot.transform.position.z);
        }
	}

	public static Material getScrollingShader(){
		return scrollingMaterial;
	}

	public static void setOffset(float offset){
		motionOffset = offset;
	}
}
