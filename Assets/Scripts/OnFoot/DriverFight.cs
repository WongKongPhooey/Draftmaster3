using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Draftmaster.Fights;

// Runs one paddock scrap between the player and a rival driver, from the moment the dialogue option is taken
// to the moment the two of them are walked apart.
//
// Shape of a fight:
//   1. Both drivers get a Fighter (composure + moves) and a health bar over their head.
//   2. The player shoves with SPACE / gamepad X; the rival's RivalFightAI circles and shoves back.
//   3. It ends the way these things actually end — nearby NPCs run over, get between them, and walk them
//      away from each other. Never with a knockout: a fighter who runs out of composure just stops swinging.
//   4. The fallout is a further hit to the pair's relationship and a line on the career stats ledger.
//
// Age rating: shoves only, no blood, no injury art, no weapons, and the crowd always breaks it up. The hooks
// are wired (FightMove.LeftHook/RightHook) but stay off until their animations exist — see enableHooks.
//
// Created at runtime by DriverFight.Begin; there is never more than one, and it destroys itself when done.
public class DriverFight : MonoBehaviour
{
    [Header("Fight")]
    [Tooltip("Seconds before the crews step in regardless of how it's going.")]
    public float maxSeconds = 14f;
    [Tooltip("Composure at or below which a fighter is visibly spent and the crews step in early.")]
    public float exhaustedHealth = FightRules.ExhaustedHealth;
    [Tooltip("How far the player may stray from the fight before being held back (m). They can circle, not leave.")]
    public float leashRadius = 5f;
    [Tooltip("Aggression used for the player's own damage (roster stat scale, 1-20).")]
    public int playerAggression = 12;
    [Tooltip("Enable the left/right hook moves. Off until their animations exist — the shove clip is all the art there is today.")]
    public bool enableHooks = false;

    [Header("Breaking it up")]
    [Tooltip("How many bystanders come over. The first two walk a driver away each; extras just get in the way.")]
    public int peacemakerCount = 3;
    [Tooltip("How far around the fight to look for bystanders (m) before giving up and spawning a couple.")]
    public float bystanderSearchRadius = 60f;

    [Header("Fallout")]
    [Tooltip("Extra relationship damage between the two drivers once the fight is over.")]
    public float relationshipFallout = 12f;

    [Header("Camera")]
    [Tooltip("Orthographic size while fighting. Tighter than the walking zoom so both fighters and their bars fill the screen.")]
    public float fightZoom = 2.6f;

    enum State { SquareUp, Fighting, BreakingUp, Over }

    public static DriverFight Active { get; private set; }
    public static bool IsActive => Active != null && Active._state != State.Over;

    [Tooltip("Seconds after a fight before another can be started.")]
    public const float CooldownSeconds = 20f;
    static float _lastEndTime = -999f;
    public static bool OnCooldown => Time.time - _lastEndTime < CooldownSeconds;

    Fighter _player, _rival;
    FightHealthBar _playerBar, _rivalBar;
    string _playerName, _rivalName;
    State _state = State.SquareUp;
    float _elapsed;
    float _squareUpLeft = 0.6f;
    readonly List<FightPeacemaker> _peacemakers = new();
    readonly List<FightPeacemaker> _escorts = new();    // the ones actually walking a driver away
    readonly List<GameObject> _spawnedBystanders = new();
    readonly List<Behaviour> _rivalSuspended = new();   // wandering / chatter / dialogue, switched off for the fight
    bool _attacksLocked;

    PitLaneStart _pitScene;
    bool _shoveHeldPrev, _leftHeldPrev, _rightHeldPrev;

    // Start a fight. playerGo is the on-foot player, rivalGo the driver they squared up to. Returns null if
    // a fight is already running (only one at a time).
    public static DriverFight Begin(GameObject playerGo, GameObject rivalGo, string rivalName, int rivalAggression)
    {
        if (IsActive) return null;
        if (playerGo == null || rivalGo == null)
        {
            Debug.LogWarning("DriverFight.Begin: missing player or rival.");
            return null;
        }

        var go = new GameObject("DriverFight");
        var fight = go.AddComponent<DriverFight>();
        fight.Setup(playerGo, rivalGo, rivalName, rivalAggression);
        return fight;
    }

    void Setup(GameObject playerGo, GameObject rivalGo, string rivalName, int rivalAggression)
    {
        Active = this;

        _playerName = DriverRelationships.PlayerName;   // racing identity, so the fallout lands on the real feud
        _rivalName = string.IsNullOrEmpty(rivalName) ? rivalGo.name : rivalName;

        _player = Fighter.Attach(playerGo, _playerName, playerAggression, true);
        _rival = Fighter.Attach(rivalGo, _rivalName, rivalAggression, false);
        _player.Opponent = _rival;
        _rival.Opponent = _player;

        // Whatever the rival was doing before — wandering their row, muttering at passers-by, being talkable
        // — steps aside for the fight and comes back afterwards.
        SuspendRivalIdleBehaviour(rivalGo);
        _rival.gameObject.AddComponent<RivalFightAI>();

        _playerBar = FightHealthBar.Attach(_player);
        _rivalBar = FightHealthBar.Attach(_rival);

        // Square up: both turn to face each other before anything is thrown.
        _player.FaceOpponent();
        _rival.FaceOpponent();

        _pitScene = FindObjectOfType<PitLaneStart>();
        if (_pitScene != null) _pitScene.SetZoomTarget(fightZoom);

        ControlHints.Show("fight", "SPACE", "X", "Shove", 6f);
        if (enableHooks)
        {
            ControlHints.Show("fightleft", "J", "LB", "Left hook", 6f);
            ControlHints.Show("fightright", "K", "RB", "Right hook", 6f);
        }

        PlayerStatsLedger.Increment("fights.started");
        Debug.Log($"DriverFight: {_playerName} squares up to {_rivalName}.", this);
    }

    void Update()
    {
        if (_player == null || _rival == null) { End(); return; }

        switch (_state)
        {
            case State.SquareUp:
                _player.FaceOpponent();
                _rival.FaceOpponent();
                _squareUpLeft -= Time.deltaTime;
                if (_squareUpLeft <= 0f) _state = State.Fighting;
                break;

            case State.Fighting:
                _elapsed += Time.deltaTime;
                ReadPlayerMoves();
                Leash();
                var reason = FightRules.ShouldBreakUp(_elapsed, maxSeconds, _player.Health, _rival.Health, exhaustedHealth);
                if (reason != BreakupReason.None) StartBreakup(reason);
                break;

            case State.BreakingUp:
                // No leash here on purpose: the whole point of the break-up is that the pair END UP further
                // apart than the leash allows, and a peacemaker walking a driver out would be fighting it.
                StepBreakup();
                break;
        }
    }

    // ---------------------------------------------------------------- player input

    // Read directly from the devices, the same way DialogueChoiceUI does: the fight is a short modal beat
    // that borrows keys the on-foot map doesn't use, so it needs no new action-map entries.
    void ReadPlayerMoves()
    {
        if (_attacksLocked || _player == null) return;
        if (RacePauseMenu.IsPaused) return;

        var kb = Keyboard.current;
        var gp = Gamepad.current;
        var mouse = Mouse.current;

        bool shove = (kb != null && kb.spaceKey.isPressed) || (gp != null && gp.buttonWest.isPressed);
        bool left = enableHooks && ((kb != null && kb.jKey.isPressed) ||
                                    (mouse != null && mouse.leftButton.isPressed) ||
                                    (gp != null && gp.leftShoulder.isPressed));
        bool right = enableHooks && ((kb != null && kb.kKey.isPressed) ||
                                     (mouse != null && mouse.rightButton.isPressed) ||
                                     (gp != null && gp.rightShoulder.isPressed));

        if (shove && !_shoveHeldPrev) _player.TryThrow(FightMove.Shove);
        else if (left && !_leftHeldPrev) _player.TryThrow(FightMove.LeftHook);
        else if (right && !_rightHeldPrev) _player.TryThrow(FightMove.RightHook);

        _shoveHeldPrev = shove;
        _leftHeldPrev = left;
        _rightHeldPrev = right;
    }

    // Keep the player in the fight. They can circle and back off, but not walk away from it — walking away
    // would leave a rival swinging at nobody, and the fight is meant to end with the crews pulling them apart.
    void Leash()
    {
        if (_player == null || _rival == null) return;
        Vector3 centre = Centre();
        Vector3 to = _player.transform.position - centre;
        to.z = 0f;
        if (to.magnitude <= leashRadius) return;

        Vector3 clamped = centre + to.normalized * leashRadius;
        FightMotion.PlaceAt(_player.transform, _player.Body, clamped);
    }

    Vector3 Centre()
    {
        Vector3 a = _player.transform.position, b = _rival.transform.position;
        Vector3 c = (a + b) * 0.5f;
        c.z = a.z;
        return c;
    }

    // ---------------------------------------------------------------- breaking it up

    void StartBreakup(BreakupReason reason)
    {
        _state = State.BreakingUp;
        Debug.Log($"DriverFight: breaking it up ({reason}).", this);

        Vector3 centre = Centre();
        var bystanders = FindBystanders(centre, Mathf.Max(2, peacemakerCount));

        // Whoever turns up first takes the player, the next takes the rival; anyone else just wades in.
        for (int i = 0; i < bystanders.Count; i++)
        {
            Fighter assigned = i == 0 ? _player : (i == 1 ? _rival : null);
            Vector3 wedge = WedgePoint(centre, i);
            var pm = FightPeacemaker.Send(bystanders[i], assigned, centre, wedge);
            if (pm == null) continue;
            _peacemakers.Add(pm);
            if (assigned != null) _escorts.Add(pm);   // only these two decide when the fight is over
        }

        // Nobody available at all (an empty test scene): stop the fight rather than leaving it running.
        if (_peacemakers.Count == 0)
        {
            LockAttacks();
            End();
        }
    }

    // A spot just off the midpoint, fanned out so three bodies don't converge on the same square metre.
    Vector3 WedgePoint(Vector3 centre, int index)
    {
        Vector3 axis = _rival.transform.position - _player.transform.position;
        axis.z = 0f;
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;
        axis.Normalize();
        Vector3 side = new Vector3(-axis.y, axis.x, 0f);

        float offset = index == 0 ? -0.35f : index == 1 ? 0.35f : 0f;
        float lateral = index >= 2 ? 0.8f : 0f;
        Vector3 p = centre + axis * offset + side * lateral;
        p.z = centre.z;
        return p;
    }

    void StepBreakup()
    {
        // The moment a body is physically between them, the swinging stops. The player keeps their feet but
        // loses control of them while they're marched away.
        if (!_attacksLocked)
        {
            for (int i = 0; i < _peacemakers.Count; i++)
            {
                if (_peacemakers[i] == null || !_peacemakers[i].InPosition) continue;
                LockAttacks();
                break;
            }
        }

        if (!_attacksLocked) return;

        // Over once every peacemaker who was walking a driver away has finished doing so. Extra bodies who
        // only waded in don't count — they head home as soon as the swinging stops.
        if (_escorts.Count == 0) { End(); return; }

        for (int i = 0; i < _escorts.Count; i++)
        {
            var pm = _escorts[i];
            if (pm == null) continue;                                    // destroyed: treat as done
            if (pm.Current != FightPeacemaker.Phase.Return && pm.Current != FightPeacemaker.Phase.Done) return;
        }
        End();
    }

    void SuspendRivalIdleBehaviour(GameObject rivalGo)
    {
        void Stash(Behaviour b)
        {
            if (b == null || !b.enabled) return;
            b.enabled = false;                       // NPCInteractable's OnDisable also clears its E prompt
            _rivalSuspended.Add(b);
        }

        Stash(rivalGo.GetComponent<PaddockWalker>());
        Stash(rivalGo.GetComponent<NPCAmbientChatter>());
        Stash(rivalGo.GetComponent<NPCInteractable>());   // covers RivalDriverNPC, which derives from it
    }

    void RestoreRivalIdleBehaviour()
    {
        foreach (var b in _rivalSuspended) if (b != null) b.enabled = true;
        _rivalSuspended.Clear();
    }

    void LockAttacks()
    {
        if (_attacksLocked) return;
        _attacksLocked = true;
        if (_player != null)
        {
            _player.AttacksLocked = true;
            if (_player.Controller != null) _player.Controller.MovementLocked = true;
        }
        if (_rival != null) _rival.AttacksLocked = true;
        ControlHints.Hide("fight");
        ControlHints.Hide("fightleft");
        ControlHints.Hide("fightright");
    }

    // Bystanders who could break this up: people, near the fight, who aren't in it. Cars carrying an
    // NPCInteractable (a driver sat in their box) and scenery interactables are filtered out by requiring
    // an actual character rig — a paper-doll outfit or an animated sprite.
    List<GameObject> FindBystanders(Vector3 centre, int wanted)
    {
        var found = new List<GameObject>();
        var seen = new HashSet<GameObject>();
        var candidates = new List<(GameObject go, float dist)>();

        void Consider(Component c)
        {
            if (c == null) return;
            var go = c.gameObject;
            if (go == null || !go.activeInHierarchy) return;
            if (!seen.Add(go)) return;
            if (_player != null && go == _player.gameObject) return;
            if (_rival != null && go == _rival.gameObject) return;
            if (go.GetComponent<Fighter>() != null) return;
            if (!IsCharacter(go)) return;

            float d = Vector3.Distance(go.transform.position, centre);
            if (d > bystanderSearchRadius) return;
            candidates.Add((go, d));
        }

        for (int i = 0; i < NPCInteractable.All.Count; i++) Consider(NPCInteractable.All[i]);
        foreach (var w in FindObjectsOfType<PaddockWalker>()) Consider(w);
        foreach (var f in FindObjectsOfType<AutographFan>()) Consider(f);

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
        for (int i = 0; i < candidates.Count && found.Count < wanted; i++) found.Add(candidates[i].go);

        // An empty corner of the paddock: send a couple of crew over rather than letting the fight run on.
        while (found.Count < 2)
        {
            var spawned = SpawnBystander(centre, found.Count);
            if (spawned == null) break;
            _spawnedBystanders.Add(spawned);
            found.Add(spawned);
        }
        return found;
    }

    // A character is something with a body: a paper-doll outfit, or a sprite rig with an animator. Keeps
    // cars, satnavs and other talkable props out of the peacemaker pool.
    static bool IsCharacter(GameObject go)
    {
        if (go.GetComponent<NPCLayeredAppearance>() != null) return true;
        var anim = go.GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null) return true;
        return false;
    }

    // Fallback body, built the same way DriverPresenceDirector builds a paddock driver so it matches the
    // crowd. Walks in from off to one side and is cleaned up when the fight ends.
    GameObject SpawnBystander(Vector3 centre, int index)
    {
        var library = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");
        var go = new GameObject($"FightBystander_{index}");
        float angle = 40f + index * 90f;
        Vector3 offset = Quaternion.Euler(0f, 0f, angle) * Vector3.right * 10f;
        go.transform.position = new Vector3(centre.x + offset.x, centre.y + offset.y, centre.z);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var mat = sh != null ? new Material(sh) : null;

        var layered = go.AddComponent<NPCLayeredAppearance>();
        layered.library = library;
        layered.layerMaterial = mat;
        layered.sortingLayerName = "Vehicles";
        layered.baseSortingOrder = 10;

        if (library != null && layered.Build(index * 977 + 3))
        {
            float frameWorldH = Mathf.Max(0.01f, library.frameHeight / Mathf.Max(1f, library.pixelsPerUnit));
            go.transform.localScale = Vector3.one * (PitCrewSpawner.OnFootPersonHeight / frameWorldH);
        }
        else
        {
            Destroy(layered);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BlobSprite(new Color(0.85f, 0.8f, 0.35f), PitCrewSpawner.OnFootPersonHeight);
            if (mat != null) sr.sharedMaterial = mat;
            sr.sortingLayerName = "Vehicles";
            sr.sortingOrder = 10;
        }
        return go;
    }

    static Sprite BlobSprite(Color color, float metres)
    {
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = Vector2.Distance(new Vector2(x, y), c) < s * 0.45f ? (Color32)color : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s / Mathf.Max(0.05f, metres));
    }

    // ---------------------------------------------------------------- ending

    void End()
    {
        if (_state == State.Over) return;
        _state = State.Over;
        _lastEndTime = Time.time;

        int outcome = _player != null && _rival != null ? FightRules.Winner(_player.Health, _rival.Health) : 0;
        ApplyFallout(outcome);
        SayPartingLine(outcome);

        if (_playerBar != null) _playerBar.Detach();
        if (_rivalBar != null) _rivalBar.Detach();
        RestoreRivalIdleBehaviour();

        if (_player != null)
        {
            if (_player.Controller != null) _player.Controller.MovementLocked = false;
            Destroy(_player);
        }
        if (_rival != null)
        {
            var ai = _rival.GetComponent<RivalFightAI>();
            if (ai != null) Destroy(ai);
            Destroy(_rival);
        }

        foreach (var extra in _spawnedBystanders)
            if (extra != null) Destroy(extra, 8f);   // let them walk back out of shot first

        if (_pitScene != null) _pitScene.SetZoomTarget(_pitScene.OnFootZoom);
        ControlHints.Hide("fight");
        ControlHints.Hide("fightleft");
        ControlHints.Hide("fightright");

        if (Active == this) Active = null;
        Destroy(gameObject, 0.1f);
    }

    // A fight makes a bad relationship worse whoever came out on top, and lands on the career ledger so
    // quests and the driver dossier can read it back.
    void ApplyFallout(int outcome)
    {
        if (!string.IsNullOrEmpty(_playerName) && !string.IsNullOrEmpty(_rivalName))
            DriverRelationships.Modify(_playerName, _rivalName, -Mathf.Abs(relationshipFallout));

        if (outcome > 0) PlayerStatsLedger.Increment("fights.won");
        else if (outcome < 0) PlayerStatsLedger.Increment("fights.lost");
        else PlayerStatsLedger.Increment("fights.drawn");

        string result = outcome > 0 ? "came out on top" : outcome < 0 ? "came off worse" : "ended it even";
        Debug.Log($"DriverFight: {_playerName} {result} against {_rivalName}. " +
                  $"Composure {_player?.Health:0} v {_rival?.Health:0}.", this);
    }

    // The rival gets the last word as they're marched off — a bubble rather than a full conversation, so it
    // doesn't hand the player back into dialogue while an NPC is still walking them away.
    void SayPartingLine(int outcome)
    {
        if (_rival == null) return;
        string line = outcome > 0
            ? "Yeah — walk away. See you Sunday."
            : outcome < 0 ? "Now we're square. Try that on the track." : "This isn't finished.";
        var bubble = SpeechBubble.Attach(_rival.transform);
        bubble.Speak(line, _rivalName);
        Destroy(bubble.gameObject, 3.5f);
    }

    // Scene change or a forced teardown mid-fight: never leave a driver frozen out of their own routine.
    void OnDestroy()
    {
        RestoreRivalIdleBehaviour();
        if (_player != null && _player.Controller != null) _player.Controller.MovementLocked = false;
        if (Active == this) Active = null;
    }
}
