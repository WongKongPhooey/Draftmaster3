using System.Linq;
using UnityEngine;

public class ScrollingTexture : MonoBehaviour
{
    public Material material;
    private float motionOffset;
    private SpriteRenderer objectRenderer;
    string pixelsFromShaderName;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        material = GetComponent<SpriteRenderer>().material;
        motionOffset = 0;

        objectRenderer = GetComponent<SpriteRenderer>();

        if(objectRenderer.material != null){
			pixelsFromShaderName = GetNumbersFromString(objectRenderer.material.name);
			Debug.Log(pixelsFromShaderName + " pixels.");
		}
    }

    void Update()
    {
        motionOffset-= (RaceManager.motionSpeed / (float.Parse(pixelsFromShaderName) / 2f));
        if(motionOffset <= 0){
			motionOffset++;
		}
        material.SetFloat("_MotionOffset", motionOffset);
    }

    private static string GetNumbersFromString(string input)
	{
		return new string(input.Where(c => char.IsDigit(c)).ToArray());
	}
}