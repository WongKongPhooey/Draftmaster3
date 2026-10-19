namespace Draftmaster.Sim
{
    // When a stopped player car is offered the tow back to the pits, lifted out of StrandedTow so it can be
    // pinned in EditMode.
    //
    // A race keeps the damage gate: a race has a grid it holds the field on and caution queues that stop it,
    // and an undamaged car sat still in either is waiting, not stranded. Practice and qualifying have neither —
    // a car stopped on the circuit there is stuck, beached or pointing the wrong way, and the only thing the
    // tow costs is session time the driver is choosing to spend. The damage gate used to apply there as well,
    // and one moderate hit does not reach it (VehicleDamage accrues 0.18 for a full-severity impact), so a car
    // that spun into the wall and stopped was often left with no way back.
    public static class StrandedTowRule
    {
        // `stoppedFor` is how long (s) the car has been below the stopped speed, off the pit surface.
        // `undamagedWait` is longer than `damagedWait` so a spin the driver can drive out of does not flash the
        // prompt up.
        public static bool Offer(float stoppedFor, float damage, bool practiceLike, bool requireDamage,
                                 float damageAtLeast, float damagedWait, float undamagedWait)
        {
            if (stoppedFor < 0f) return false;
            bool wrecked = damage >= damageAtLeast;
            if (wrecked || !requireDamage) return stoppedFor >= damagedWait;
            return practiceLike && stoppedFor >= undamagedWait;
        }
    }
}
