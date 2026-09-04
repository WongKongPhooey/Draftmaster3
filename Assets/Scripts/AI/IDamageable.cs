using UnityEngine;
using Draftmaster.Sim;

public interface IDamageable
{
    // The real one: the body that hit us, folding our panels out of the space it is occupying. The dent
    // comes out the shape of the striker — see BodyDeform.
    //
    // `share` is how much of this one contact THIS body absorbs. Two cars both run it against each other,
    // so a full share on both folds one impact's metal twice and leaves a void between them; a wall gives
    // nothing, so a car that hits one takes all of it (BodyDeform.RigidPartner).
    void OnImpact(in BodyDeform.Striker striker, float severity, float share);

    // Same, for a striker that is the only thing giving way.
    void OnImpact(in BodyDeform.Striker striker, float severity);

    // A hit with no body behind it: an authored dent, a kerb, a stone. Struck as a small hammer press at
    // `worldPoint` folding along `worldInward`, not as a crater centred there.
    void OnImpact(Vector2 worldPoint, Vector2 worldInward, float severity);
}
