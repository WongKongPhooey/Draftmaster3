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
    public static GameObject activePlayer;
    private static string playerName;

    void Awake(){
    }

    public static void setPlayerCanvas(GameObject thePlayer){
        activePlayer = thePlayer;
        playerName = activePlayer.name;
        playerDialogueCanvas = GameObject.Find(playerName + "/DialogueCanvas");
        //playerDialogueOutput = GameObject.Find(playerName + "/DialogueCanvas/DialogueBox/DialogueText").GetComponent<TMPro.TMP_Text>();
    }

    public static GameObject getPlayerDialogueCanvas(){

        if(playerDialogueCanvas == null){
            playerDialogueCanvas = GameObject.Find(playerName + "/DialogueCanvas");
        }
        return playerDialogueCanvas;
    }

    public static TMPro.TMP_Text getPlayerDialogueOutput(){
        
        if(playerDialogueOutput == null){
            playerDialogueOutput = GameObject.Find(playerName + "/DialogueCanvas/DialogueBox/DialogueText").GetComponent<TMPro.TMP_Text>();
        }
        return playerDialogueOutput;
    }
}
