using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementOnFoot : MonoBehaviour {

    private Vector2 direction;
    [SerializeField] private float playerSpeed = 2.0f;
    private Rigidbody2D body;
    private Animator animator;

    private const string horizontal = "Horizontal";
    private const string vertical = "Vertical";
    private Vector2 lookAtDir;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update(){
        direction.Set(InputManager.direction.x,InputManager.direction.y);
        body.linearVelocity = direction * playerSpeed;

        Vector3 facing = new Vector3(direction.x,direction.y,0);

        Debug.DrawLine(transform.position, transform.position + facing, Color.red); 

        animator.SetFloat(horizontal, direction.x);
        animator.SetFloat(vertical, direction.y);
    }
}
