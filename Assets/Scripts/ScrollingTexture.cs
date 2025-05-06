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
        if(scrollSpeedOverride != 0){
			motionOffset-= (RaceManager.motionSpeed / scrollSpeedOverride);
        } else {
            //motionOffset-= (RaceManager.motionSpeed / (float.Parse(pixelsFromShaderName) / 2f)); //128/2 = 64
            //motionOffset-= RaceManager.motionSpeed / ((float.Parse(pixelsFromShaderName) / 512f) * 40f);
            motionOffset-= (RaceManager.motionSpeed / ((objectLength * 100f) / 32f));
        }
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