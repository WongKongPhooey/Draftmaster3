using UnityEngine;

public class DialogueManager : MonoBehaviour
{

    public GameObject actor;

    public string dialogueText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dialogueText = "I want to go get a closer look..";
    }

    public static void triggerDialogue(GameObject actor){

    }

    // Update is called once per frame
    void Update(){
        this.transform.position = actor.transform.position + new Vector3(0,0.4f,0);
    }
}
