using System.Collections;
using System.Collections.Generic;
using PlayFab.MultiplayerModels;
using UnityEngine;
using TMPro;
using Ink.Runtime;

public class PlayerDialogue : MonoBehaviour
{
    private GameObject dialogueCanvas;

    private TMPro.TMP_Text dialogueOutput;

    private bool isOutputting;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dialogueCanvas = this.transform.gameObject;
        dialogueOutput = dialogueCanvas.transform.GetChild(1).transform.GetChild(0).gameObject.GetComponent<TMPro.TMP_Text>();
        dialogueOutput.text = "";
        isOutputting = false;
    }

    public void receiveDialogue(string dialogueLine) {
        Debug.Log("Line received for player: " + dialogueLine);
        StartCoroutine(PlayerDialogueLine(dialogueLine));
    }

    IEnumerator PlayerDialogueLine(string dialogueLine){
        foreach(char c in dialogueLine.ToCharArray()){
            dialogueOutput.text += c;
            yield return new WaitForSeconds(0.06f);
        }
    }

    // Update is called once per frame
    void Update(){
        this.transform.position = this.transform.parent.position + new Vector3(-0.4f,-0.3f,0);
        this.transform.eulerAngles = new Vector3(0,0,0);
    }
}
