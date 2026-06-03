using UnityEngine;

public interface IDamageable
{
    // worldPoint: contact point. worldInward: unit direction into the body. severity: 0..1.
    void OnImpact(Vector2 worldPoint, Vector2 worldInward, float severity);
}
