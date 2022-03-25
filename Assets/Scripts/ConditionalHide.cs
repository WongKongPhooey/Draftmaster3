using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConditionalHide : MonoBehaviour
{
	public string param;
    // Start is called before the first frame update
    void Start()
    {
       if(PlayerPrefs.GetString(param) == ""){
		   this.gameObject.SetActive(false);
	   } 
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
