namespace Ncx.Core.Machine;

// The built-in drilling family on a Fanuc machining center (fanuc 6, controller-mapping 5); cycles/fanuc.toml writes
// the same entries. A lathe drills with G83/G84/G85 on the face and G87/G88/G89 on the circumference and overrides
// these entries in its machine file (controller-mapping 5, D59).
public static partial class DrillingFamily
{
    // The words that rules of the Fanuc reader and compiler carry, not an address, follow from the params of each
    // entry (CycleEntry.RuleWords, D157): SURFACE, SAFE, CYCLE_RETRACT and AXIS, and PITCH on G84 (controller-mapping
    // 5).

    // The rigid tapping line of G84 stands directly before the cycle block and belongs to the cycle; a machine file
    // overrides it with its builder's code and sets it to "" where a parameter makes G84 rigid without one
    // (machine-config 6; fanuc 6; D256).
    private const string FanucTapBefore = "M29 S{rpm}";

    // D177: on a machining centre only G82 and G84 (and G89, REAM with a dwell) carry the dwell P; G73, G81, G83, G85
    // and G86 have none (Fanuc B-64484EN-2 p. 56, 61-84). A DRILL with a dwell is written as G82, a REAM with one as
    // G89, which reads back as REAM with CYCLE_DWELL: the P of REAM is the P of its G89.
    private const string FanucReamDwell = "G89";

    // The cycle block defines and calls at its own position, every following block with a position calls again, G80
    // cancels: every entry is modal (fanuc 6, 9.5).
    private static CycleCatalog FanucFamily()
    {
        return new CycleCatalog
        {
            Controller = Controller.Fanuc,
            Entries =
            [
                FanucEntry("DRILL", "G81", peck: false, dwell: false),       // G81 G99 Z-21.732 R5. F565
                FanucEntry("DRILL_DWELL", "G82", peck: false, dwell: true),  // G82 adds a dwell P
                FanucEntry("PECK", "G83", peck: true, dwell: false),         // G83 pecks with Q, full retract
                FanucEntry("CHIP_BREAK", "G73", peck: true, dwell: false),   // G73 chip-breaks
                FanucEntry("TAP", "G84", peck: false, dwell: true) with { Before = FanucTapBefore },
                FanucEntry("REAM", "G85", peck: false, dwell: true) with { DwellNative = FanucReamDwell },
                FanucEntry("BORE", "G86", peck: false, dwell: false),        // G86 bores
            ],
        };
    }

    // Z and R of the cycle block are the absolute DEPTH and CLEARANCE, Q the peck of G83 and G73, F the cycle's own
    // feed and P its dwell where the code has one, which never touch the modal F of the program (fanuc 6,
    // controller-mapping 5, D29, D177).
    private static CycleEntry FanucEntry(string name, string native, bool peck, bool dwell)
    {
        var parameters = new Dictionary<string, string>
        {
            ["DEPTH"] = "Z",
            ["CLEARANCE"] = "R",
        };
        if (peck)
        {
            parameters["PECK"] = "Q";
        }

        if (dwell)
        {
            parameters["CYCLE_DWELL"] = "P";
        }

        parameters["CYCLE_F"] = "F";
        return new CycleEntry
        {
            Name = name,
            Native = native,
            Modal = true,
            Params = parameters,
            Family = Controller.Fanuc,
        };
    }
}
