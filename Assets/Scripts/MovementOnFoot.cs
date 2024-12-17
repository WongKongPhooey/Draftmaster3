using System.Collections;
using System.Collections.Generic;
using NUnit.Framework.Constraints;
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

    void FixedUpdate(){
        if(RaceManager.thePlayer != this.gameObject){
            //If the player is not the on foot character
            //We don't need to calc any movement
            return;
        }
        direction.Set(InputManager.direction.x,InputManager.direction.y);
        body.linearVelocity = direction * playerSpeed;

        Vector3 lookDir = (transform.position + new Vector3(direction.x,direction.y,0));

        float angle = Mathf.Atan2(lookDir.y - transform.position.y,lookDir.x - transform.position.x) * Mathf.Rad2Deg;

        //When the stick is released, leave the player as is, don't reset to 0
        if(angle != 0){
            Quaternion angleAxis = Quaternion.AngleAxis(angle + 90, Vector3.forward);
            //transform.rotation = Quaternion.Slerp(transform.rotation, angleAxis, Time.deltaTime * 10);
            body.MoveRotation(Quaternion.Slerp(transform.rotation, angleAxis, Time.deltaTime * 10));
        }

        animator.SetFloat(horizontal, direction.x);
        animator.SetFloat(vertical, direction.y);
    }
}