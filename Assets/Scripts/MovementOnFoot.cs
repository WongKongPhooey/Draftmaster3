using System.Collections;
using System.Collections.Generic;
using NUnit.Framework.Constraints;
using UnityEngine;

public class MovementOnFoot : MonoBehaviour {

    private Vector2 direction;
    private Vector2 lastKnownPos;
    [SerializeField] private float playerSpeed = 2.0f;
    private Rigidbody2D body;
    private Animator animator;
    public Material motionShader;

    private const string horizontal = "Horizontal";
    private const string vertical = "Vertical";
    private Vector2 lookAtDir;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        lastKnownPos = this.gameObject.transform.position;
    }

    void FixedUpdate(){
        if(RaceManager.thePlayer != this.gameObject){
            this.transform.position = new Vector2(RaceManager.playerLocation + EnvironmentManager.cameraOffset - lastKnownPos.x, lastKnownPos.y);
            return;
        }
        lastKnownPos = this.gameObject.transform.position;
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

    public void setAsPlayer(){
        Debug.Log("Last Known Pos: " + lastKnownPos.x);
		RaceManager.setPlayer(this.gameObject, 3f);
        this.transform.position = new Vector2(lastKnownPos.x, lastKnownPos.y);
        RaceManager.playerLocation = lastKnownPos.x;
        motionShader.SetFloat("_MotionOffset", 0f);
        CameraManager.setRotation(0f);
	}
}