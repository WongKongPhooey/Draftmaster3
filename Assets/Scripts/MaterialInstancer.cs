using UnityEngine;

public class MaterialInstancer : MonoBehaviour
{
    public Material material;
    private float sizeMulti;
    private float motionOffset;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        material = GetComponent<SpriteRenderer>().material;
        sizeMulti = GetComponent<SpriteRenderer>().size.x;
        motionOffset = 0;
    }

    void Update()
    {
        motionOffset-= (RaceManager.motionSpeed / (sizeMulti * 16));
        if(motionOffset <= 0){
			motionOffset++;
		}
        material.SetFloat("_MotionOffset", motionOffset);
    }
}
