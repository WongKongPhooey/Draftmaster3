using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public GameObject activePlayer;
    public static Vector2 direction;

    private PlayerInput playerInput;
    private InputAction moveAction;

    bool isPushing, isPunching, isThrowing;

    public static int inputSensitivity = 16;

    Animator animator;

    private void Awake()
    {
        playerInput = activePlayer.GetComponent<PlayerInput>();
        animator = activePlayer.GetComponent<Animator>();

        moveAction = playerInput.actions["Movement"];
    }

    private void Update()
    {
    }

    public void pushTrigger()
    { 
        animator.SetTrigger("PushTrigger");
    }
}
