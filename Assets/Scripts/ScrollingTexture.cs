using System.Linq;
using UnityEngine;

public class ScrollingTexture : MonoBehaviour
{
    public Material material;
    public float scrollSpeedOverride;
    private float objectLength;
    private float motionOffset;
    private SpriteRenderer objectRenderer;
    string pixelsFromShaderName;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        objectRenderer = GetComponent<SpriteRenderer>();
        material = objectRenderer.material;
        objectLength = objectRenderer.size.x;
        motionOffset = 0;

        if(objectRenderer.material != null){
			pixelsFromShaderName = GetNumbersFromString(objectRenderer.material.name);
			Debug.Log(pixelsFromShaderName + " pixels.");
		}
    }

    void Update()
    {
        material.SetFloat("_MotionOffset", RaceManager.motionOffset);
    }

    private static string GetNumbersFromString(string input)
	{
		return new string(input.Where(c => char.IsDigit(c)).ToArray());
	}
}