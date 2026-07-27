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

    int _index;
    bool _talking;
    Transform _interactor;                              // the player currently talking to this NPC
    SpeechBubble _npcBubble, _playerBubble, _activeBubble;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); BuildFloatingPrompt(false); CleanupBubbles(); }

    public bool InRange(Vector2 playerPos) => Vector2.Distance(playerPos, transform.position) <= interactRange;

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
        _activeBubble.Speak(raw, playerLine ? PlayerSpeakerName : speakerName);
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

                // Dark disc background so the glyph reads as a button.
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(_prompt.transform, false);
                iconGo.transform.localScale = Vector3.one * 0.55f;
                var sr = iconGo.AddComponent<SpriteRenderer>();
                sr.sprite = SpeechBubbleSprite();
                sr.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
                sr.sortingLayerName = "Vehicles";
                sr.sortingOrder = 60;
                // Scene uses the 3D URP renderer — default Sprite-Lit gets no Light2D and renders black. Force unlit.
                Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh != null) sr.sharedMaterial = new Material(sh);

                // Button glyph (TextMesh renders reliably under the 3D renderer; same approach as the car-enter prompt).
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
            // Reflect the active input device each frame the prompt is shown.
            if (_promptLabel != null) _promptLabel.text = Gamepad.current != null ? "A" : "E";
            // Pin above the head in world space — never inherit the NPC's facing rotation.
            _prompt.transform.position = transform.position + Vector3.up * 0.7f;
            _prompt.transform.rotation = Quaternion.identity;
            _prompt.SetActive(true);
        }
        else if (_prompt != null) _prompt.SetActive(false);
    }

    static Sprite _bubble;
    static Sprite SpeechBubbleSprite()
    {
        if (_bubble != null) return _bubble;
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                px[y * s + x] = d < s * 0.45f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        _bubble = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return _bubble;
    }
}
