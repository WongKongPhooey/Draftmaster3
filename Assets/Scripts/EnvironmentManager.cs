using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{

	public static Material scrollingMaterial;
	public static Material staticMaterial;

	public static float motionOffset;

	void Awake(){

		scrollingMaterial = Resources.Load("Materials/ScrollingMaterial", typeof(Material)) as Material;
		staticMaterial = Resources.Load("Materials/StaticMaterial", typeof(Material)) as Material;
	}
	
	// Update is called once per frame
	void FixedUpdate() {
		
	}

	public static Material getScrollingShader(){
		return scrollingMaterial;
	}

	public static void setOffset(float offset){
		motionOffset = offset;
	}
}
