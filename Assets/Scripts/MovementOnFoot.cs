using System.Collections;
using System.Collections.Generic;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.InputSystem;

public class MovementOnFoot : MonoBehaviour {

    private Vector2 direction;
    private Vector2 lastKnownPos;
    [SerializeField] private float playerSpeed = 2.0f;
    [SerializeField] private float runMultiplier = 2.0f;
    [SerializeField] private float startLocation = 0f;
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

    private void Start()
    {
        var track = RaceManager.currentTrackInfo;
        if (track != null) {
            transform.position = new Vector2(transform.position.x, track.infieldScenePositionY);
        } else {
            transform.position = new Vector2(startLocation, lastKnownPos.y);
        }
        lastKnownPos = transform.position;
    }

    void FixedUpdate(){
        if(RaceManager.thePlayer != this.gameObject){
            //If the NPC isn't the active player, have it become an faux environment object
            this.transform.position = new Vector2(RaceManager.playerLocation + CameraManager.cameraOffset - lastKnownPos.x, lastKnownPos.y);
            animator.speed = 1f; //don't leave sprint playback rate behind when control switches away
            return;
        }
        lastKnownPos = this.gameObject.transform.position;
        //Debug.Log("Playable NPC Location: " + lastKnownPos);
        //Read the device directly. The legacy InputManager.direction bus freezes at its last value on release
        //(PlayerInput is set to Invoke Unity Events, which doesn't deliver a clean (0,0)) — that caused the ice-slide.
        direction = ReadMoveInput();
        bool running = IsRunHeld() && direction != Vector2.zero;
        body.linearVelocity = direction * playerSpeed * (running ? runMultiplier : 1f);
        //Same walk animation, played faster while running
        animator.speed = running ? runMultiplier : 1f;
        //Debug.Log("Applying direction: x" + direction.x + ", y" + direction.y);

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

    //Sprint modifier: Left Shift on keyboard, L1/left shoulder on gamepad.
    bool IsRunHeld(){
        var gp = Gamepad.current;
        if(gp != null && gp.leftShoulder.isPressed) return true;
        var kb = Keyboard.current;
        if(kb != null && kb.leftShiftKey.isPressed) return true;
        return false;
    }

    //Direct device read with a deadzone so releasing the stick stops the player instantly (no coasting).
    Vector2 ReadMoveInput(){
        Vector2 m = Vector2.zero;
        var gp = Gamepad.current;
        if(gp != null){
            m = gp.leftStick.ReadValue(); // .ReadValue() applies the stick deadzone
            if(m.magnitude < 0.15f) m = Vector2.zero;
        }
        var kb = Keyboard.current;
        if(kb != null){
            if(kb.aKey.isPressed || kb.leftArrowKey.isPressed) m.x = -1f;
            if(kb.dKey.isPressed || kb.rightArrowKey.isPressed) m.x = 1f;
            if(kb.wKey.isPressed || kb.upArrowKey.isPressed) m.y = 1f;
            if(kb.sKey.isPressed || kb.downArrowKey.isPressed) m.y = -1f;
        }
        return Vector2.ClampMagnitude(m, 1f);
    }

    public void setAsPlayer(){
        this.transform.position = new Vector2(lastKnownPos.x, lastKnownPos.y);
        //Debug.Log("Last Known Pos: " + lastKnownPos.x);
		RaceManager.setPlayer(this.gameObject, 6f);
		InputManager.ChangeInputMap("OnFoot");
	}
}