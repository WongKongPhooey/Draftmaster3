using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class CameraActions : MonoBehaviour
{
    public CinemachineCamera followCamera;
    public GameObject player;
    public GameObject target;

    void OnTriggerEnter2D(Collider2D other){
        Debug.Log("Cutscene Triggered");
        StartCoroutine(FocusAwayForSeconds(5));
    }

    IEnumerator FocusAwayForSeconds(int timeToPan){
        followCamera.Follow = target.transform;
        followCamera.Lens.OrthographicSize = 18;
        yield return new WaitForSeconds(timeToPan);

        followCamera.Lens.OrthographicSize = 3;
        followCamera.Follow = player.transform;
    }
}
