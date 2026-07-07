using System.Collections.Generic;
using UnityEngine;

// The car-part catalog: everything buyable at travel-map locations. Code-defined (DummyDrivers pattern)
// rather than ScriptableObject assets — one part per entry, applied to the player's VehicleInfo clone by
// PlayerCarBuild.Outfit. One part installed per slot; buying installs immediately (the old part is scrapped).
//
// Stat model: additive top speed (mph), multiplicative acceleration scale, additive lateral grip (g),
// multiplicative tyre-wear scale. Deliberately coarse — parts should read at a glance in a shop list.
public enum PartSlot { Engine, Gearbox, Tires, Chassis }

public class PartDef
{
    public string id;
    public string name;
    public string blurb;
    public PartSlot slot;
    public int price;
    public float topSpeedAdd;    // mph
    public float accelScale = 1f;
    public float gripAdd;        // lateral g
    public float wearScale = 1f;
    public bool junkyardOnly;    // never in fixed shop stock; only turns up in salvage rolls

    // One-line effect summary for shop rows, e.g. "+6 mph top speed, +10% accel".
    public string EffectSummary()
    {
        var bits = new List<string>();
        if (topSpeedAdd != 0f) bits.Add($"{(topSpeedAdd > 0 ? "+" : "")}{topSpeedAdd:0} mph top speed");
        if (!Mathf.Approximately(accelScale, 1f)) bits.Add($"{(accelScale > 1f ? "+" : "")}{(accelScale - 1f) * 100f:0}% accel");
        if (gripAdd != 0f) bits.Add($"{(gripAdd > 0 ? "+" : "")}{gripAdd:0.00} g grip");
        if (!Mathf.Approximately(wearScale, 1f)) bits.Add($"{(wearScale > 1f ? "+" : "")}{(wearScale - 1f) * 100f:0}% tyre wear");
        return bits.Count > 0 ? string.Join(", ", bits) : "no measurable effect";
    }
}

public static class PartCatalog
{
    static List<PartDef> _all;
    static Dictionary<string, PartDef> _byId;

    public static IReadOnlyList<PartDef> All { get { EnsureBuilt(); return _all; } }

    public static PartDef Get(string id)
    {
        EnsureBuilt();
        return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var p) ? p : null;
    }

    static void EnsureBuilt()
    {
        if (_all != null) return;
        _all = new List<PartDef>
        {
            // --- Engines ---
            new PartDef { id = "engine_358", name = "Fresh 358 Smallblock", slot = PartSlot.Engine, price = 6500,
                topSpeedAdd = 3f, accelScale = 1.05f,
                blurb = "A tight, honest rebuild. Nothing exotic, everything right." },
            new PartDef { id = "engine_r7", name = "R7 Race Engine", slot = PartSlot.Engine, price = 14000,
                topSpeedAdd = 6f, accelScale = 1.10f,
                blurb = "Dyno-sheet included. The sheet is showing off." },
            new PartDef { id = "engine_hemi", name = "Experimental Hemi", slot = PartSlot.Engine, price = 27000,
                topSpeedAdd = 9f, accelScale = 1.15f,
                blurb = "Built in small numbers for reasons nobody will explain." },
            new PartDef { id = "engine_bootleg", name = "Bootlegger's Big Block", slot = PartSlot.Engine, price = 9000,
                topSpeedAdd = 7f, accelScale = 1.08f,
                blurb = "Outran the law before it ever saw a racetrack." },
            new PartDef { id = "engine_junk", name = "Tired 305 Smallblock", slot = PartSlot.Engine, price = 1200,
                topSpeedAdd = -2f, accelScale = 0.96f, junkyardOnly = true,
                blurb = "Turns over. That's the whole sales pitch." },
            new PartDef { id = "engine_barnfind", name = "Barn-Find Superspeedway Motor", slot = PartSlot.Engine, price = 4800,
                topSpeedAdd = 8f, accelScale = 1.02f, junkyardOnly = true,
                blurb = "Thirty years under a tarp. Somebody built this one to run flat out." },

            // --- Gearboxes ---
            new PartDef { id = "gearbox_close", name = "Close-Ratio 5-Speed", slot = PartSlot.Gearbox, price = 5200,
                accelScale = 1.08f,
                blurb = "Keeps the engine on the boil out of every corner." },
            new PartDef { id = "gearbox_tall", name = "Tall Final Drive", slot = PartSlot.Gearbox, price = 3200,
                topSpeedAdd = 4f, accelScale = 0.97f,
                blurb = "Lazy off the corner, endless down the straight. Superspeedway thinking." },

            // --- Tires ---
            new PartDef { id = "tires_soft", name = "Soft Compound Set", slot = PartSlot.Tires, price = 2400,
                gripAdd = 0.08f, wearScale = 1.5f,
                blurb = "Blistering pace while they last. They will not last." },
            new PartDef { id = "tires_hard", name = "Endurance Compound Set", slot = PartSlot.Tires, price = 1900,
                gripAdd = 0.02f, wearScale = 0.55f,
                blurb = "Still working at the end of a long run when everyone else is sliding." },

            // --- Chassis ---
            new PartDef { id = "chassis_light", name = "Lightweight Chassis Kit", slot = PartSlot.Chassis, price = 9500,
                accelScale = 1.06f, gripAdd = 0.03f,
                blurb = "Every bracket drilled, every panel thinner than the rulebook likes." },
            new PartDef { id = "aero_kit", name = "Slick Aero Kit", slot = PartSlot.Chassis, price = 4600,
                topSpeedAdd = 2f, gripAdd = 0.01f,
                blurb = "Massaged fenders and a lip nobody measured too carefully." },
            new PartDef { id = "chassis_junk", name = "Bent-but-True Frame Rails", slot = PartSlot.Chassis, price = 900,
                gripAdd = 0.01f, junkyardOnly = true,
                blurb = "Straighter than they look. Mostly." },
        };
        _byId = new Dictionary<string, PartDef>();
        foreach (var p in _all) _byId[p.id] = p;
    }

    // Junkyard salvage roll: deterministic per (location, week) so stock is stable across the whole leg —
    // learnable within a visit, rerolled between races. Duds and gems both in the pool on purpose: reading
    // a junkyard shelf is a skill.
    public static List<(PartDef part, int price)> JunkyardStock(string locationId, int week, int count = 3)
    {
        EnsureBuilt();
        var rng = new System.Random((locationId ?? "yard").GetHashCode() * 486187739 + week * 7919);
        var pool = new List<PartDef>(_all); // everything can wash up in a junkyard, including the good stuff
        var stock = new List<(PartDef, int)>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var pick = pool[rng.Next(pool.Count)];
            pool.Remove(pick);
            float discount = 0.45f + (float)rng.NextDouble() * 0.25f; // 45-70% of book price
            int price = Mathf.Max(150, Mathf.RoundToInt(pick.price * discount / 50f) * 50);
            stock.Add((pick, price));
        }
        return stock;
    }
}
