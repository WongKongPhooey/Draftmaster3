using Unity.Netcode;
using UnityEngine;

// Put on the networked player vehicle prefab. Only the OWNER drives their car (local input + physics); on
// every other client that car is a remote puppet positioned by NetworkTransform, so its local controller is
// switched off to avoid two brains fighting over the transform.
public class NetworkCarOwnerGate : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        bool owner = IsOwner;

        // The local driving brain runs only for the owner.
        var pvc = GetComponent<PlayerVehicleController>();
        if (pvc != null) pvc.enabled = owner;

#if UNITY_EDITOR
        // Multiplayer Play Mode local testing: keyboard for the main editor, gamepad for each virtual player, so
        // both players can drive at once without swapping window focus. Real builds keep Auto (both devices).
        if (owner && pvc != null && GameSession.IsMultiplayer)
            pvc.controlScheme = Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor
                ? PlayerVehicleController.ControlScheme.Keyboard
                : PlayerVehicleController.ControlScheme.Gamepad;
#endif

        // Spline/AI brains have no place on a player-driven car.
        var spline = GetComponent<SplineDriver>();
        if (spline != null) spline.enabled = false;
        var input = GetComponent<SplineInputDriver>();
        if (input != null) input.enabled = false;

        // Remote puppets shouldn't run their own collision depenetration (NetworkTransform owns their pose).
        var col = GetComponent<VehicleCollision>();
        if (col != null) col.enabled = owner;

        // Follow the local player's own car with the camera.
        if (owner)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null) follow.target = transform;
            }
        }
    }
}
