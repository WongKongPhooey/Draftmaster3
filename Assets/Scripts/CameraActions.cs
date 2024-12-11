using System.Collections;
using System.Collections.Generic;
using PlayFab.ClientModels;
using Unity.Cinemachine;
using UnityEngine;

public class CameraActions : MonoBehaviour
{
    CinemachineCamera actionedCamera;
    public GameObject player;

    void Start(){
        actionedCamera = this.gameObject.GetComponent<CinemachineCamera>();
    }

    public void triggerAction(string actionName, GameObject target, int actionParamInt){
        Debug.Log("Camera Action Triggered");

        switch(actionName){
            case "FocusAwayForSeconds":
                StartCoroutine(FocusAwayForSeconds(target, actionParamInt));
                break;
            default:
                //Invalid type, do nothing
                Debug.Log("Camera Action Not Found - " + actionName);
                break;
        }
    }

    IEnumerator FocusAwayForSeconds(GameObject target, int timeToPan){
        Debug.Log("Camera Focus To Target");
        actionedCamera.Follow = target.transform;
        actionedCamera.Lens.OrthographicSize = 18;
        yield return new WaitForSeconds(timeToPan);

        Debug.Log("Camera Focus To Player");
        actionedCamera.Lens.OrthographicSize = 3;
        actionedCamera.Follow = player.transform;
    }
}
