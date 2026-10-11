using Draftmaster.Sponsors;
using UnityEngine;

// The team car on the Team HQ shop floor, wearing the player's real paint job: scheme from the Paint Scheme
// Designer, sponsors from the book. Repaints the moment either changes, so saving in the booth shows on the
// car behind it straight away.
//
// Sits on the TeamCar placeholder (a scaled white quad): the livery is drawn at its own 12.8 px/m size, so
// the transform scale goes back to 1 and the collider is resized to the paint.
[RequireComponent(typeof(SpriteRenderer))]
public class TeamCarDisplay : MonoBehaviour
{
    SpriteRenderer _sr;

    void Awake() => _sr = GetComponent<SpriteRenderer>();

    void OnEnable()
    {
        PaintBook.Changed += Repaint;
        SponsorBook.Changed += Repaint;
    }

    void OnDisable()
    {
        PaintBook.Changed -= Repaint;
        SponsorBook.Changed -= Repaint;
    }

    void Start() => Repaint();

    void Repaint()
    {
        if (_sr == null) return;
        var sprite = PaintShop.RenderPlayerCar();
        if (sprite == null) return;

        _sr.sprite = sprite;
        _sr.color = Color.white;
        transform.localScale = Vector3.one;
        var box = GetComponent<BoxCollider2D>();
        if (box != null) box.size = sprite.bounds.size;
    }
}
