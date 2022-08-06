using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Events;
using TMPro;

public class ColorPicker : MonoBehaviour
{
	public GameObject carBase;
	public GameObject carVinyl;
	public GameObject activeLayer;
	TMPro.TMP_Text layerName;
	RectTransform Rect;
	Texture2D ColorTexture;
	public GameObject[] paintableLayers;
	int layerCounter;
	
    // Start is called before the first frame update
    void Start()
    {
        Rect = GetComponent<RectTransform>();
		ColorTexture = GetComponent<Image>().mainTexture as Texture2D;
		layerName = GameObject.Find("LayerName").GetComponent<TMPro.TMP_Text>();
		layerCounter = 0;
		activeLayer = paintableLayers[layerCounter];
    }

    // Update is called once per frame
    void Update()
    {
		if(RectTransformUtility.RectangleContainsScreenPoint(Rect, Input.mousePosition)){
			Vector2 delta;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, Input.mousePosition, null, out delta);
		
			float width = Rect.rect.width;
			float height = Rect.rect.height;
			
			float x = Mathf.Clamp(delta.x / width, 0f, 1f);
			float y = Mathf.Clamp(delta.y / height, 0f, 1f);
			
			int texX = Mathf.RoundToInt(x * ColorTexture.width);
			int texY = Mathf.RoundToInt(y * ColorTexture.height);
			
			Color color = ColorTexture.GetPixel(texX, texY);
			
			if(Input.GetMouseButtonDown(0)){
				activeLayer.GetComponent<Image>().color = color;
			}
		}
	}
	
	public void changeLayer(){
		layerCounter++;
		if(layerCounter > 2){
			layerCounter = 0;
		}
		activeLayer = paintableLayers[layerCounter];
		layerName.text = activeLayer.name;
	}
	
	public void savePaint(){
		foreach(GameObject layer in paintableLayers){
			PlayerPrefs.SetString("PaintBooth" + layer.name,layer.GetComponent<Image>().color.ToString());
			Debug.Log("Saved PaintBooth" + layer.name + ": " + layer.GetComponent<Image>().color.ToString());
		}
	}
}
