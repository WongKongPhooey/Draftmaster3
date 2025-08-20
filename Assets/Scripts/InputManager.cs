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

    private PlayerInput playerInput;
    private InputAction moveAction;

    bool isPushing, isPunching, isThrowing;

    public static int inputSensitivity = 20;

    Animator animator;

    private void Awake()
    {
        autoTurn = false;

        playerInput = this.gameObject.GetComponent<PlayerInput>();
        animator = activePlayer.GetComponent<Animator>();

        if (activePlayer.tag == "Vehicle"){
            moveAction = playerInput.actions["InCar"];
        }
    }

    private void FixedUpdate()
    {
    }

    public void pushTrigger()
    {
        animator.SetTrigger("PushTrigger");
    }

    public void OnSteering(InputValue value)
    {
        direction = value.Get<Vector2>();
        //Debug.Log("Analog direction" + direction.x + ", " + direction.y);
    }
}
