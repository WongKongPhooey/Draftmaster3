using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureScaling : MonoBehaviour
{
	Renderer rend;
    // Start is called before the first frame update
    void Start()
    {
        rend.material.SetTextureScale("_MainTex", new Vector2(1, 1));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
