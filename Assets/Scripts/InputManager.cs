using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static Vector2 direction;

    private PlayerInput playerInput;
    private InputAction moveAction;

    private void Awake(){
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Movement"];
    }

    private void Update(){
        direction = moveAction.ReadValue<Vector2>();
    }
}
