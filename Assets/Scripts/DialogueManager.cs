using System.Collections;
using PlayFab.MultiplayerModels;
using UnityEngine;
using TMPro;

public class DialogueManager : MonoBehaviour
{

    public GameObject actor;
    private GameObject dialogueCanvas;
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

    public void TriggerDialogue(string dialogue){
        dialogueOutput.text = "";
        dialogueText = dialogue;
        dialogueCanvas.SetActive(true);
        StartCoroutine(DialogueLine());
    }

    // Update is called once per frame
    void Update(){
        this.transform.position = actor.transform.position + new Vector3(0,0.4f,0);
        //this.transform.rotation = new Quaternion();
    
    }

    IEnumerator DialogueLine(){
        foreach(char c in dialogueText.ToCharArray()){
            dialogueOutput.text += c;
            yield return new WaitForSeconds(0.075f);
        }
        yield return new WaitForSeconds(2);
        dialogueCanvas.SetActive(false);
    }
}
