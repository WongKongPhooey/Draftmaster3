namespace Draftmaster.Sim
{
    // Which car the crew chief's camera is on, stepped through with the prev/next keys.
    //
    // The cars and the chief form one ring: the chief standing at the pit wall, then the field in
    // running order, then back round to the chief. Stepping past the last car comes home rather than stopping,
    // so the same two keys that go looking for a car always find the way back without a third one to learn.
    //
    // `Self` is the chief. A watched car that has dropped out of the running order (retired, despawned) is
    // treated as the chief too, so the next press starts from the top of the order instead of doing nothing.
    public static class PitWallWatch
    {
        public const int Self = -1;

        // One press. `current` is the watched car's index in the running order (or Self), `count` the number of
        // cars in it, `direction` +1 for next and -1 for previous.
        public static int Step(int current, int count, int direction)
        {
            if (count <= 0 || direction == 0) return Self;
            if (current < 0 || current >= count) current = Self;

            // Slot 0 is the chief, slots 1..count are the cars.
            int slots = count + 1;
            int slot = current + 1;
            slot = ((slot + (direction > 0 ? 1 : -1)) % slots + slots) % slots;
            return slot - 1;
        }
    }
}
