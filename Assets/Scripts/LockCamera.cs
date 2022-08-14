using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockCamera : MonoBehaviour
{
	GameObject player;
	float camHeight;
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.Find("Player");
		camHeight = this.transform.position.y;
    }

    // Update is called once per frame
    void Update()
    {
        if((Movement.isWrecking == true)||(Movement.wreckOver == true)){
			this.transform.position = new Vector3(player.transform.position.x,camHeight,player.transform.position.z);
		}
    }
}
