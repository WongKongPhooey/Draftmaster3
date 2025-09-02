using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public GameObject activePlayer;
    public static Vector2 direction;
    public static float playerDirection;

    public static bool autoTurn;

    public static PlayerInput playerInput;

    bool isPushing, isPunching, isThrowing;

    public static int inputSensitivity = 20;

    Animator animator;

    private void Awake()
    {
        autoTurn = false;

        playerInput = this.gameObject.GetComponent<PlayerInput>();
        animator = activePlayer.GetComponent<Animator>();

        if (activePlayer.tag == "Vehicle"){
            playerInput.SwitchCurrentActionMap("InCar");
            CameraManager.cameraCentering = true;
        } else {
            playerInput.SwitchCurrentActionMap("OnFoot");
            CameraManager.cameraCentering = false;
        }
    }

    private void FixedUpdate(){
    }

    public void pushTrigger()
    {
        animator.SetTrigger("PushTrigger");
    }

    //This took me ages to remember how this works..
    //The InputManager Player Input component creates these method names..
    //based on the name of the input in the Input Action Asset.
    public void OnMovement(InputValue value)
    {
        direction = value.Get<Vector2>();
        Debug.Log("Analog walking direction" + direction.x + ", " + direction.y);
    }

    public void OnSteering(InputValue value)
    {
        direction = value.Get<Vector2>();
        Debug.Log("Analog steering direction" + direction.x + ", " + direction.y);
    }

    public static void ChangeInputMap(string mapName){
        switch(mapName){
            case "InCar":
                playerInput.SwitchCurrentActionMap("InCar");
                CameraManager.cameraCentering = true;
                break;
            case "OnFoot":
                playerInput.SwitchCurrentActionMap("OnFoot");
                CameraManager.cameraCentering = false;
                break;
            default:
                playerInput.SwitchCurrentActionMap("InCar");
                CameraManager.cameraCentering = true;
                break;
        }
    }
}
