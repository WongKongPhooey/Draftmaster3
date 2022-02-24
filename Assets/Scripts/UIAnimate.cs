using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIAnimate : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        LeanTween.scale(gameObject, new Vector3(1f,1f,1f), 0.5f).setDelay(.5f).setEase(LeanTweenType.easeInOutCubic);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}