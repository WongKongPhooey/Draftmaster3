using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(MovementOnFoot))]
public class NetworkedOnFoot : NetworkBehaviour
{
	private MovementOnFoot movement;
	private Animator animator;
	private Rigidbody2D body;

	private readonly NetworkVariable<Vector2> syncedPosition = new(
		writePerm: NetworkVariableWritePermission.Owner);
	private readonly NetworkVariable<float> syncedRotation = new(
		writePerm: NetworkVariableWritePermission.Owner);
	private readonly NetworkVariable<Vector2> syncedInput = new(
		writePerm: NetworkVariableWritePermission.Owner);

	void Awake()
	{
		movement = GetComponent<MovementOnFoot>();
		animator = GetComponent<Animator>();
		body = GetComponent<Rigidbody2D>();
	}

	public override void OnNetworkSpawn()
	{
		if (IsOwner)
		{
			movement.setAsPlayer();
		}
		else
		{
			// Remote instance: don't run local input/movement; network drives transform directly
			movement.enabled = false;
			if (body != null) body.bodyType = RigidbodyType2D.Kinematic;
		}
	}

	void FixedUpdate()
	{
		if (!IsSpawned) return;

		if (IsOwner)
		{
			syncedPosition.Value = transform.position;
			syncedRotation.Value = transform.eulerAngles.z;
			syncedInput.Value = InputManager.direction;
		}
		else
		{
			transform.position = syncedPosition.Value;
			transform.rotation = Quaternion.Euler(0, 0, syncedRotation.Value);
			if (animator != null)
			{
				animator.SetFloat("Horizontal", syncedInput.Value.x);
				animator.SetFloat("Vertical", syncedInput.Value.y);
			}
		}
	}
}
