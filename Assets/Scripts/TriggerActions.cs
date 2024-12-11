using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class TriggerActions : MonoBehaviour{

    public GameObject player;
    public GameObject target;
    public GameObject triggerCamera;

    [SerializeField] private string triggerType;
    [SerializeField] private int triggerParamInt;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void OnTriggerEnter2D(Collider2D other){
        Debug.Log("Trigger Entered");

        switch(triggerType){
            case "FocusCameraAway":
                triggerCamera.GetComponent<CameraActions>().triggerAction("FocusAwayForSeconds", target, triggerParamInt);
                break;
            case "Dialogue":
                DialogueManager.triggerDialogue(player);
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
