using System;
using System.Threading.Tasks;
using Draftmaster.MasterDraft;
using UnityEngine;

// The Master Draft player for as long as the game is running: the backend, the card catalogue and the
// loaded profile, plus the hand-off to and from a race. Created on first use, survives scene loads, so the
// menu scene, the race scene and the menu again all see the same profile.
public class MasterDraftSession : MonoBehaviour
{
    public static MasterDraftSession Instance { get; private set; }

    public IMasterDraftBackend Backend { get; private set; }
    public CardCatalog Catalog { get; private set; }
    public MasterDraftProfile Profile => Backend?.Profile;

    public bool Ready { get; private set; }
    public bool Loading { get; private set; }
    public string Status { get; private set; } = "";

    // ---- race hand-off (set by Go Race, read by MasterDraftRaceHost, cleared on return) ----
    [Serializable]
    public class PendingRace
    {
        public string trackId;
        public string driverUid;
    }

    [Serializable]
    public class RaceOutcome
    {
        public string trackId;
        public int position;      // 1-based; 0 = did not finish
        public int fieldSize;
        public int cardsPlayed;
        public int coinsAwarded;
    }

    public PendingRace Pending { get; set; }
    public RaceOutcome LastRace { get; set; }

    // True between GO and the return to the Master Draft menu.
    public bool InRace => Pending != null;

    // The session, made if it doesn't exist yet. Call InitialiseAsync (or Ensure().Begin()) before reading
    // Profile.
    public static MasterDraftSession Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("MasterDraftSession");
        DontDestroyOnLoad(go);
        return go.AddComponent<MasterDraftSession>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    Task _init;

    // Builds the catalogue, picks the backend for the online switch, signs in and loads the profile. Safe to
    // call repeatedly — every caller awaits the same work.
    public Task InitialiseAsync()
    {
        if (Ready) return Task.CompletedTask;
        return _init ??= Initialise();
    }

    async Task Initialise()
    {
        Loading = true;
        Status = "Loading…";
        try
        {
            Catalog = MasterDraftSources.BuildCatalog();
            Backend = MasterDraftOnline.Enabled
                ? new UgsMasterDraftBackend(Catalog)
                : new LocalMasterDraftBackend(Catalog);

            if (!await Backend.SignIn())
            {
                // Online sign-in failed: fall back to the device file rather than locking the player out.
                Debug.LogWarning("[MasterDraft] Sign-in failed — playing offline.");
                Backend = new LocalMasterDraftBackend(Catalog);
                await Backend.SignIn();
            }

            await Backend.LoadProfile();
            Ready = Profile != null;
            Status = Ready ? "" : "Couldn't load your collection.";
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Status = "Couldn't load your collection.";
            _init = null;   // let the next caller try again
        }
        finally
        {
            Loading = false;
        }
    }
}
