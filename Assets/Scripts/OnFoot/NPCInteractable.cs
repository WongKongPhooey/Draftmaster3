using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Proximity-triggered conversational NPC. Holds inline dialogue lines. Walk up, interact to talk.
public class NPCInteractable : MonoBehaviour
{
    public static readonly List<NPCInteractable> All = new();

    [Tooltip("Display name shown in the dialogue panel.")]
    public string speakerName = "Crew Member";
    [TextArea]
    [Tooltip("One conversation line per entry. Cycles through on each interact.")]
    public string[] lines = { "Hey, good to see you in the pits." };
    [Tooltip("Player must be within this distance to start/continue talking.")]
    public float interactRange = 2.2f;
    [Tooltip("Loop back to first line when the conversation ends, or stay finished.")]
    public bool repeatable = true;
    [Tooltip("World height (m) this speaker's bubble floats above them. Raise it for a talker the size of a car, or the box sits inside the bodywork. 0 = SpeechBubble's own default.")]
    public float bubbleHeadHeight = 0f;
    [Tooltip("Turn to face the player when the conversation opens. Off for a speaker whose transform means something — a driver sat in a parked car can't swivel the car round, and its heading is read back when someone drives it.")]
    public bool turnsToFace = true;
    [Tooltip("World height (m) of the floating keycap prompt. Roughly half a head — the old text glyph read as a giant letter because nothing tied it to a real size.")]
    public float promptIconHeight = 0.3f;

    int _index;
    bool _talking;
    Transform _interactor;                              // the player currently talking to this NPC
    SpeechBubble _npcBubble, _playerBubble, _activeBubble;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); BuildFloatingPrompt(false); CleanupBubbles(); }

    public bool InRange(Vector2 playerPos) => Vector2.Distance(playerPos, transform.position) <= interactRange;

    // True while ANY speaker in the scene has a conversation open. Ambient chatter checks this so a
    // background NPC never mutters over a line the player is actually reading.
    public static bool AnyConversationActive
    {
        get
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].IsTalking) return true;
            return false;
        }
    }

    // Called by the interacting controller right before Interact() so #player lines can target the player's bubble.
    public void SetInteractor(Transform interactor) => _interactor = interactor;

    // Who this NPC is currently talking to (null when nobody has engaged). PaddockWalker reads it to
    // stand still and turn toward whoever stopped it.
    public Transform Interactor => _interactor;

    // Name shown over the driver's own bubble on "#player" lines. The position tracker holds the
    // career name; before it exists (on foot, pre-race) fall back to a neutral label.
    public static string PlayerSpeakerName
    {
        get
        {
            var rt = RacePositionTracker.Instance;
            return rt != null && !string.IsNullOrEmpty(rt.playerName) ? rt.playerName : "You";
        }
    }

    // Returns true while a conversation is ongoing (caller should keep focus).
    public virtual bool Interact()
    {
        if (lines == null || lines.Length == 0) return false;

        if (!_talking)
        {
            _talking = true;
            _index = 0;
            SpeakCurrent();
            return true;
        }

        // Mid-type: first press finishes the current line instantly instead of advancing.
        if (_activeBubble != null && _activeBubble.IsRevealing)
        {
            _activeBubble.CompleteInstantly();
            return true;
        }

        _index++;
        if (_index >= lines.Length)
        {
            EndConversation();
            return false;
        }
        SpeakCurrent();
        return true;
    }

    // Lines ending with "#player" are spoken by the player (in their own bubble); the marker is stripped.
    void SpeakCurrent()
    {
        string raw = lines[_index];
        bool playerLine = false;
        string trimmed = raw.TrimEnd();
        if (trimmed.EndsWith("#player"))
        {
            playerLine = true;
            raw = trimmed.Substring(0, trimmed.Length - "#player".Length).TrimEnd();
        }

        if (playerLine && _interactor != null)
        {
            if (_playerBubble == null) _playerBubble = SpeechBubble.Attach(_interactor);
            _npcBubble?.Hide();
            _activeBubble = _playerBubble;
        }
        else
        {
            if (_npcBubble == null)
            {
                _npcBubble = SpeechBubble.Attach(transform);
                if (bubbleHeadHeight > 0f) _npcBubble.headHeight = bubbleHeadHeight;
            }
            _playerBubble?.Hide();
            _activeBubble = _npcBubble;
        }
        // Both halves of the conversation are owned by this NPC, so the player's reply is never queued
        // behind the line it is answering.
        _activeBubble.Speak(raw, playerLine ? PlayerSpeakerName : speakerName,
                            Draftmaster.Sim.SpeechPriority.Conversation, owner: this);
    }

    public void EndConversation()
    {
        _talking = false;
        if (!repeatable) _index = Mathf.Max(0, lines.Length - 1);
        _npcBubble?.Hide();
        _playerBubble?.Hide();
        _activeBubble = null;
    }

    void CleanupBubbles()
    {
        if (_npcBubble != null) Destroy(_npcBubble.gameObject);
        if (_playerBubble != null) Destroy(_playerBubble.gameObject);
    }

    public virtual bool IsTalking => _talking;

    GameObject _prompt;
    TextMesh _promptLabel;
    public void BuildFloatingPrompt(bool show)
    {
        if (show)
        {
            if (_prompt == null)
            {
                _prompt = new GameObject("Prompt");
                _prompt.transform.SetParent(transform, false);

                // The NPC may be scaled up (e.g. 8x on the pit-crew prefab). Cancel that scale so the
                // prompt sits a fixed world distance above the head at a fixed world size — otherwise it
                // renders huge and floats off-screen above a zoomed-in on-foot camera.
                float inv = transform.lossyScale.y != 0f ? 1f / transform.lossyScale.y : 1f;
                _prompt.transform.localPosition = new Vector3(0f, 0.7f * inv, -0.1f * inv); // -z nudges it in front of the road mesh
                _prompt.transform.localScale = Vector3.one * inv; // children below render at world scale

                // Kenney pixel keycap. It already draws its own dark bezel and lit keytop, so it needs
                // neither the disc that used to sit behind the letter nor a tint — the art IS the button.
                var icon = InputPromptIcon.Create(_prompt.transform, "Icon", promptIconHeight, "Vehicles", 60);

                if (icon == null)
                {
                    // Art missing: fall back to the old text glyph so a prompt still appears.
                    var labelGo = new GameObject("Label");
                    labelGo.transform.SetParent(_prompt.transform, false);
                    labelGo.transform.localPosition = new Vector3(0f, 0f, -0.05f);
                    _promptLabel = labelGo.AddComponent<TextMesh>();
                    _promptLabel.anchor = TextAnchor.MiddleCenter;
                    _promptLabel.alignment = TextAlignment.Center;
                    _promptLabel.characterSize = 0.18f;
                    _promptLabel.fontSize = 64;
                    _promptLabel.color = new Color(1f, 1f, 0.5f, 1f);
                    var mr = labelGo.GetComponent<MeshRenderer>();
                    mr.sortingLayerName = "Vehicles";
                    mr.sortingOrder = 61;
                }
            }
            // E is THE interact key, whatever is plugged in — a wheel or an idle pad used to flip every
            // prompt to the gamepad face button while the player was still on the keyboard.
            if (_promptLabel != null) _promptLabel.text = "E";
            // Pin above the head in world space — never inherit the NPC's facing rotation.
            _prompt.transform.position = transform.position + Vector3.up * 0.7f;
            _prompt.transform.rotation = Quaternion.identity;
            _prompt.SetActive(true);
        }
        else if (_prompt != null) _prompt.SetActive(false);
    }

}
