using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Touch / mouse drag on the Card Collection grid (MasterDraftUI.Collection). Reports the drag in canvas (kit) px,
// up positive, and when it ends; the screen does the scrolling. The cards are Buttons without drag handlers, so a
// drag that starts on a card bubbles up to here, and the event system cancels that card's click once it does.
public class MasterDraftDragScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Action<float> onDrag;
    public Action onEnd;

    Canvas _canvas;

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        onDrag?.Invoke(eventData.delta.y / scale);
    }

    public void OnEndDrag(PointerEventData eventData) => onEnd?.Invoke();
}
