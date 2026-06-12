using System.Collections.Generic;
using UnityEngine;

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

    int _index;
    bool _talking;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); BuildFloatingPrompt(false); }

    public bool InRange(Vector2 playerPos) => Vector2.Distance(playerPos, transform.position) <= interactRange;

    // Returns true while a conversation is ongoing (caller should keep focus).
    public bool Interact()
    {
        if (lines == null || lines.Length == 0) return false;

        if (!_talking)
        {
            _talking = true;
            _index = 0;
            DialogueUI.Instance?.Show(speakerName, lines[_index]);
            return true;
        }

        _index++;
        if (_index >= lines.Length)
        {
            EndConversation();
            return false;
        }
        DialogueUI.Instance?.Show(speakerName, lines[_index]);
        return true;
    }

    public void EndConversation()
    {
        _talking = false;
        if (!repeatable) _index = Mathf.Max(0, lines.Length - 1);
        DialogueUI.Instance?.Hide();
    }

    public bool IsTalking => _talking;

    GameObject _prompt;
    public void BuildFloatingPrompt(bool show)
    {
        if (show)
        {
            if (_prompt == null)
            {
                _prompt = new GameObject("Prompt");
                _prompt.transform.SetParent(transform, false);
                _prompt.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                var sr = _prompt.AddComponent<SpriteRenderer>();
                sr.sprite = SpeechBubbleSprite();
                sr.sortingOrder = 50;
                sr.color = new Color(1f, 1f, 0.4f, 0.95f);
                _prompt.transform.localScale = Vector3.one * 0.5f;
            }
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
