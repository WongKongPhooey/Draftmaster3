using UnityEngine;
using UnityEngine.EventSystems;

// Shifts a button's content 2px down while held. The sheet asks for the pressed state to come from the
// offset rather than a second sprite, so there is only ever one drawing of the button to maintain.
//
// IN ITS OWN FILE ON PURPOSE. A MonoBehaviour whose file is not named after it gets no MonoScript of its
// own: Unity will let one into a scene and then write it out as a record with no field data behind it.
// The editor shrugs that off; a player build, reading the scene with no type tree to fall back on, walks
// off the end of the record and calls the whole file corrupt. That is what crashed the first two Windows
// builds. See IronOvalBlink.cs, which was the other half of it.
public class IronOvalPressOffset : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform content;
    public float depth = 2f;
    Vector2 _rest;
    bool _held;

    void Awake() { if (content != null) _rest = content.anchoredPosition; }
    public void OnPointerDown(PointerEventData _) => Set(true);
    public void OnPointerUp(PointerEventData _) => Set(false);
    void OnDisable() => Set(false);

    void Set(bool held)
    {
        if (content == null || _held == held) return;
        _held = held;
        content.anchoredPosition = held ? _rest + new Vector2(0f, -depth) : _rest;
    }
}
