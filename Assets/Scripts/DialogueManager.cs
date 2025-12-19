using System.Collections;
using System.Collections.Generic;
using PlayFab.MultiplayerModels;
using UnityEngine;
using TMPro;
using Ink.Runtime;

public class DialogueManager : MonoBehaviour
{
    public static GameObject playerDialogueCanvas;
    public static TMPro.TMP_Text playerDialogueOutput;

    void Awake(){
        playerDialogueCanvas = GameObject.Find("PlayerOnFoot/DialogueCanvas");
        playerDialogueOutput = GameObject.Find("PlayerOnFoot/DialogueCanvas/DialogueBox/DialogueText").GetComponent<TMPro.TMP_Text>();
    }

    public static GameObject getPlayerDialogueCanvas(){

        if(playerDialogueCanvas == null){
            playerDialogueCanvas = GameObject.Find("PlayerOnFoot/DialogueCanvas");
        }
        return playerDialogueCanvas;
    }

    public static TMPro.TMP_Text getPlayerDialogueOutput(){
        
        if(playerDialogueOutput == null){
            playerDialogueOutput = GameObject.Find("PlayerOnFoot/DialogueCanvas/DialogueBox/DialogueText").GetComponent<TMPro.TMP_Text>();
        }
        return playerDialogueOutput;
    }
}
