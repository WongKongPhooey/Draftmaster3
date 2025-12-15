using System.Collections;
using PlayFab.MultiplayerModels;
using UnityEngine;
using TMPro;
using Ink.Runtime;

public class DialogueManager : MonoBehaviour
{

    public GameObject actor;
    private GameObject dialogueCanvas;

    private Story currentDialogue;

    private TMPro.TMP_Text dialogueOutput;
    public string dialogueText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dialogueCanvas = this.transform.gameObject;
        dialogueOutput = dialogueCanvas.transform.GetChild(1).transform.GetChild(0).gameObject.GetComponent<TMPro.TMP_Text>();
        dialogueCanvas.SetActive(false);
        dialogueOutput.text = "";
    }

    public void TriggerDialogue(TextAsset inkJSON){
        
        //A dialogue is already active
        if(dialogueCanvas.activeSelf == false){
            Debug.Log(dialogueCanvas + " - is now active");
            currentDialogue = new Story(inkJSON.text);
        }

        dialogueOutput.text = "";
        if(currentDialogue.canContinue){
            dialogueText = currentDialogue.Continue();
            dialogueCanvas.SetActive(true);
            StartCoroutine(DialogueLine());
        } else {
            endDialogue();
        }
    }

    // Update is called once per frame
    void Update(){
        this.transform.position = actor.transform.position + new Vector3(-0.4f,-0.3f,0);
        this.transform.eulerAngles = new Vector3(0,0,0);
    }

    IEnumerator DialogueLine(){
        foreach(char c in dialogueText.ToCharArray()){
            dialogueOutput.text += c;
            yield return new WaitForSeconds(0.1f);
        }
        //yield return new WaitForSeconds(5);
        //dialogueCanvas.SetActive(false);
    }

    void endDialogue(){
        dialogueCanvas.SetActive(false);
    }
}
