using System.Collections.Generic;

namespace Draftmaster.Data
{
    // Seed roster for the Drivers table. The field is the real 2026 NASCAR Cup Series entry list —
    // edit it in CupRoster2026, which keys every driver to the car number of their livery sprite.
    public static class DummyDrivers
    {
        public static List<Driver> Build() => CupRoster2026.BuildDrivers();
    }
}
