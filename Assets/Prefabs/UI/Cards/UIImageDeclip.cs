using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIImageDeclip : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        this.GetComponent<RectTransform>().localPosition += new Vector3(0f,0f,-1f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
