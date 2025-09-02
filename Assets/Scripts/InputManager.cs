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
        } else {
            playerInput.SwitchCurrentActionMap("OnFoot");
        }
    }

    private void FixedUpdate()
    {
    }

    public void pushTrigger()
    {
        animator.SetTrigger("PushTrigger");
    }

    public void OnWalking(InputValue value)
    {
        direction = value.Get<Vector2>();
        //Debug.Log("Analog direction" + direction.x + ", " + direction.y);
    }

    public void OnSteering(InputValue value)
    {
        direction = value.Get<Vector2>();
        //Debug.Log("Analog direction" + direction.x + ", " + direction.y);
    }
}
