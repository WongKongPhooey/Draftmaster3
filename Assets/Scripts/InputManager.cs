using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static Vector2 direction;

    private PlayerInput playerInput;
    private InputAction moveAction;

    bool isPushing, isPunching, isThrowing;

    public static int inputSensitivity = 16;

    Animator animator;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();
        
        moveAction = playerInput.actions["Movement"];
    }

    private void Update()
    {
        direction = moveAction.ReadValue<Vector2>();

        if(Input.GetButtonDown("Push")){
            animator.SetTrigger("PushTrigger");
        }
    }
}
