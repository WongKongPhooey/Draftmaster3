using UnityEngine;
using System.Collections;

public class FontScale : MonoBehaviour {

	public static int fontScale;

	// Use this for initialization
	void Start () {
		if(Screen.width >= 2400){
			fontScale = 1;
		} else {
			if(Screen.width >= 1024){
				fontScale = 2;
			} else {
				if(Screen.width >= 512){
					fontScale = 4;
				} else {
					fontScale = 8;
				}
			}
		}
	}
}
