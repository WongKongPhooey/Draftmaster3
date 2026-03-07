using System.Collections;
using System.Collections.Generic;
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
                //if(RaceManager.thePlayer.tag == "Vehicle"){
                    return;
                //}

                StartCoroutine(FocusAwayForSeconds(target, actionParamInt));
                break;
            default:
                //Invalid type, do nothing
                Debug.Log("Camera Action Not Found - " + actionName);
                break;
        }
    }

    public void triggerExitAction(string actionName, GameObject target, int actionParamInt){
        Debug.Log("Exit Trigger Action");

        switch(actionName){
            case "ReturnFocusToPlayer":
                StartCoroutine(ReturnFocusToPlayer(target, actionParamInt));
                break;
            default:
                //Invalid type, do nothing
                Debug.Log("Camera Action Not Found - " + actionName);
                break;
        }
    }

    IEnumerator FocusAwayForSeconds(GameObject target, int newZoom){
        Debug.Log("Camera Focus To Target");
        actionedCamera.Follow = target.transform;
        actionedCamera.Lens.OrthographicSize = newZoom;
        yield return new WaitForSeconds(0);
    }

    IEnumerator ReturnFocusToPlayer(GameObject target, int newZoom){
        Debug.Log("Camera Focus To Player");
        yield return new WaitForSeconds(1f); 
        actionedCamera.Lens.OrthographicSize = CameraManager.playerZoom;
        actionedCamera.Follow = player.transform;
    }
}
