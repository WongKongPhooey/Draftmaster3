using System.Collections;
using System.Collections.Generic;
using PlayFab.MultiplayerModels;
using UnityEngine;
using TMPro;
using Ink.Runtime;

public class DialogueHandler : MonoBehaviour
{
    public GameObject actor;
    private bool isPlayer;
    private GameObject dialogueCanvas;
    private Canvas dialogueCanvasRenderer;

    private Story currentDialogue;
    private List<string> currentTags;

    private TMPro.TMP_Text dialogueOutput;
    public string dialogueText;

    public bool isOutputting;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dialogueCanvas = this.transform.gameObject;
        dialogueCanvasRenderer = dialogueCanvas.GetComponent<Canvas>();
        dialogueOutput = dialogueCanvas.transform.GetChild(1).transform.GetChild(0).gameObject.GetComponent<TMPro.TMP_Text>();
        dialogueOutput.text = "";
        isOutputting = false;

        dialogueCanvasRenderer.enabled = false;
    }

    public void AdvanceDialogue(TextAsset inkJSON){
        
        //Debug.Log("Advancing Dialogue: " + isOutputting);

        //Can't advance while it's typing out
        if(isOutputting == true){
            return;
        }

        dialogueOutput.text = "";
        if(currentDialogue.canContinue){
            dialogueText = currentDialogue.Continue();
            //Debug.Log("Next line: " + dialogueText);
            currentTags = currentDialogue.currentTags;
            if((currentTags.Count > 0)&&(currentTags[0] == "player")){
                isOutputting = false;
                GameObject playerDialogue = DialogueManager.getPlayerDialogueCanvas();
                playerDialogue.GetComponent<PlayerDialogue>().receiveDialogue(dialogueText, dialogueCanvas, inkJSON);
                dialogueCanvasRenderer.enabled = false;
            } else {
                //Debug.Log("Read out the dialogue");
                dialogueCanvasRenderer.enabled = true;
                isOutputting = true;
                StartCoroutine(DialogueLine());
            }
        } else {
            endDialogue();
        }
    }

    public void TriggerDialogue(TextAsset inkJSON){

        //Initialise a new dialogue
        if(dialogueCanvasRenderer.enabled == false){
            dialogueCanvasRenderer.enabled = true;
            Debug.Log(dialogueCanvas + " - is now active");
            currentDialogue = new Story(inkJSON.text);

            AdvanceDialogue(inkJSON);
        }
    }

    // Update is called once per frame
    void Update(){
        this.transform.position = actor.transform.position + new Vector3(-0.4f,-0.3f,0);
        this.transform.eulerAngles = new Vector3(0,0,0);
    }

    IEnumerator DialogueLine(bool isPlayer = false){
        foreach(char c in dialogueText.ToCharArray()){
            dialogueOutput.text += c;
            yield return new WaitForSeconds(0.06f);
        }
        isOutputting = false;
    }

    void endDialogue(){
        dialogueCanvas.SetActive(false);
    }
}
