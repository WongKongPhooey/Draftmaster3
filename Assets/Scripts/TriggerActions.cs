using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class TriggerActions : MonoBehaviour{

    GameObject player;
    private BoxCollider2D playerCollider;

    [Header("Trigger Connections (Optional)")]
    public GameObject altTrigger;
    public GameObject target;

    [Header("Trigger Type (Dialogue/Camera/Cutscene)")]
    [SerializeField] private string triggerType;
    [SerializeField] private TextAsset inkJSON;
    [SerializeField] private int triggerParamInt;

    [Header("Camera Trigger")]
    public GameObject triggerCamera;

    [Header("Dialogue Trigger")]
    public GameObject triggerDialogue;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start(){
        player = RaceManager.getPlayer();
        playerCollider = player.GetComponent<BoxCollider2D>();
    }

    public void OnTriggerEnter2D(Collider2D other){

        player = RaceManager.getPlayer(); 
        playerCollider = player.GetComponent<BoxCollider2D>();

        if(other != playerCollider){
            return;
        }

        //Only the player can trigger actions
        if(player.tag == "Vehicle"){
            return;
        }

        switch(triggerType){
            case "FocusCameraAway":
                triggerCamera.GetComponent<CameraActions>().triggerAction("FocusAwayForSeconds", target, triggerParamInt);
                //RaceManager.setPlayer(GameObject.Find("cup24livery20"));
                break;
            case "Dialogue":
                //target.transform.LookAt(player.transform.position);
                triggerDialogue.GetComponent<DialogueHandler>().TriggerDialogue(inkJSON);
                break;
            default:
                //Invalid type, do nothing
                break;
        }
    }

    public void OnTriggerExit2D(Collider2D other){
        if(other != playerCollider){
            return;
        }

        //Only the player can trigger actions
        if(player.tag == "Vehicle"){
            return;
        }

        switch(triggerType){
            case "FocusCameraAway":
                triggerCamera.GetComponent<CameraActions>().triggerExitAction("ReturnFocusToPlayer", target, triggerParamInt);
                break;
            default:
                //Invalid type, do nothing
                break;
        }
    }

    // Update is called once per frame
    void Update()
    {
    }
}
