using System.Collections;
using Draftmaster.Sponsors;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gets sponsor decals onto every car in a race scene, without any scene wiring.
//
// The field doesn't exist at load: GridSpawner waits on the driver database and then builds 40-odd cars a
// few frames in, and the player's own car is parked from the moment the scene opens. So this polls for a
// short while after load, paints anything new it finds, then goes quiet. Repainting the player's car when
// the book changes is instant (event-driven) — that's what makes a decal appear the moment it's placed on
// the garage board.
//
// Self-installing on the same terms as the other paddock features: single player, in a scene that actually
// has cars in it.
public class SponsorPaintDirector : MonoBehaviour
{
    [Tooltip("How long after a scene load to keep looking for newly spawned cars.")]
    public float watchSeconds = 25f;
    [Tooltip("Seconds between sweeps while watching.")]
    public float pollInterval = 0.75f;
    [Tooltip("Paint the AI field as well as the player's car.")]
    public bool paintAiField = true;

    static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        TryInstall();
        if (_hooked) return;
        SceneManager.sceneLoaded += (_, __) => TryInstall();
        _hooked = true;
    }

    static void TryInstall()
    {
        if (FindObjectOfType<SponsorPaintDirector>() != null) return;
        if (FindObjectOfType<PlayerVehicleController>(true) == null) return;   // no cars here (menus, garage)
        SponsorPainter.Forget();                                               // fresh scene, fresh paintwork
        new GameObject("SponsorPaintDirector").AddComponent<SponsorPaintDirector>();
    }

    void OnEnable() => SponsorBook.Changed += RepaintPlayer;
    void OnDisable() => SponsorBook.Changed -= RepaintPlayer;

    void Start() => StartCoroutine(Watch());

    IEnumerator Watch()
    {
        float deadline = Time.time + watchSeconds;
        while (Time.time < deadline)
        {
            Sweep();
            yield return new WaitForSeconds(pollInterval);
        }
        Sweep();
    }

    void Sweep()
    {
        var player = CarIdentity.FindPlayerCar();
        if (player != null) SponsorPainter.PaintPlayer(player);

        if (!paintAiField) return;
        var cars = FindObjectsByType<PlayerVehicleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cars.Length; i++)
        {
            var go = cars[i].gameObject;
            if (go == player) continue;
            SponsorPainter.PaintAi(go, CarIdentity.NumberOf(go));
        }
    }

    void RepaintPlayer()
    {
        var player = CarIdentity.FindPlayerCar();
        if (player != null) SponsorPainter.PaintPlayer(player);
    }
}
