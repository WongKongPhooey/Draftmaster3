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
            //Apply any quest/stat side effects carried by this line's tags
            ProcessTags(currentTags);
            if(IsPlayerLine(currentTags)){
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

    // Returns true when the current line should be voiced by the player rather
    // than this NPC (the Ink line carries a #player tag).
    bool IsPlayerLine(List<string> tags){
        if(tags == null){
            return false;
        }
        foreach(string tag in tags){
            if(tag == "player"){
                return true;
            }
        }
        return false;
    }

    // Inspects the tags attached to an Ink line and triggers any side-quest or
    // stat actions they request. This is what lets an NPC's dialogue hand out a
    // side-quest or grant stats. Supported tag forms (everything after the # in
    // an Ink line):
    //   quest_start:QuestId            - offer/begin a side-quest
    //   quest_complete:QuestId         - mark a side-quest finished
    //   quest_progress:QuestId:amount  - push a side-quest's progress along
    //   stat:StatName:amount           - grant stat points there and then
    void ProcessTags(List<string> tags){
        if(tags == null){
            return;
        }

        foreach(string tag in tags){
            string[] parts = tag.Split(':');
            switch(parts[0]){
                case "quest_start":
                    if(parts.Length >= 2){
                        QuestManager.StartQuest(parts[1]);
                    }
                    break;
                case "quest_complete":
                    if(parts.Length >= 2){
                        QuestManager.CompleteQuest(parts[1]);
                    }
                    break;
                case "quest_progress":
                    if(parts.Length >= 3){
                        int amount;
                        if(int.TryParse(parts[2], out amount)){
                            QuestManager.AdvanceQuest(parts[1], amount);
                        }
                    }
                    break;
                case "stat":
                    if(parts.Length >= 3){
                        int amount;
                        if(int.TryParse(parts[2], out amount)){
                            StatsManager.GainStat(parts[1], amount);
                        }
                    }
                    break;
                default:
                    //Not a quest/stat tag (e.g. the #player routing tag)
                    break;
            }
        }
    }
}
