using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Events;

[Serializable]
public class ColorEvent : UnityEvent<Color>{
	
}

public class ColorPicker : MonoBehaviour
{
	public ColorEvent onColorPreview;
	public ColorEvent onColorSelect;
	public GameObject carBase;
	RectTransform Rect;
	Texture2D ColorTexture;
	
    // Start is called before the first frame update
    void Start()
    {
        Rect = GetComponent<RectTransform>();
		ColorTexture = GetComponent<Image>().mainTexture as Texture2D;
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
			//Debug.Log(color);
			
			onColorPreview?.Invoke(color);
			
			if(Input.GetMouseButtonDown(0)){
				onColorSelect?.Invoke(color);
			}
			
			carBase.GetComponent<Renderer>().material.color = color;
		}
	}
}
