using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameVersion : MonoBehaviour
{
	
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<TextMesh>().text = "v" + Application.version;
    }
}
