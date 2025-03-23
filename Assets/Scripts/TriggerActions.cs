using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class TriggerActions : MonoBehaviour{

    [Header("Trigger Connections")]
    public GameObject player;
    public GameObject target;

    [Header("Trigger Type (Dialogue/Camera/Cutscene)")]
    [SerializeField] private string triggerType;

    [Header("Trigger Options (Dependant On Type)")]
    public GameObject triggerCamera;
    public GameObject triggerDialogue;
    [SerializeField] private int triggerParamInt;
    [SerializeField] private string triggerParamString;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start(){
    }

    public void OnTriggerEnter2D(Collider2D other){

        //Vehicles can't trigger things, only people/props
        if(RaceManager.thePlayer.tag == "Vehicle"){
            return;
        }

        switch(triggerType){
            case "FocusCameraAway":
                triggerCamera.GetComponent<CameraActions>().triggerAction("FocusAwayForSeconds", target, triggerParamInt);
                //RaceManager.setPlayer(GameObject.Find("cup24livery20"));
                break;
            case "Dialogue":
                triggerDialogue.GetComponent<DialogueManager>().TriggerDialogue(triggerParamString);
                break;
            default:
                //Invalid type, do nothing
                break;
        }
    }

    /*public void OnTriggerExit2D(Collider2D other){

        //Vehicles can't trigger things, only people/props
        if(RaceManager.thePlayer.tag == "Vehicle"){
            return;
        }

        switch(triggerType){
            case "FocusCameraAway":
                triggerCamera.GetComponent<CameraActions>().triggerAction("FocusAwayForSeconds", target, triggerParamInt);
                //RaceManager.setPlayer(GameObject.Find("cup24livery20"));
                break;
            case "Dialogue":
                triggerDialogue.GetComponent<DialogueManager>().TriggerDialogue(triggerParamString);
                break;
            default:
                //Invalid type, do nothing
                break;
        }
    }*/

    // Update is called once per frame
    void Update()
    {
        
    }
}
