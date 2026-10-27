namespace Ncx.Core.Machine;

// The built-in drilling family on the SINUMERIK 840D sl (siemens 7, controller-mapping 5); cycles/siemens.toml writes
// the same entries. A direct CYCLE8x(...) block calls once, MCALL makes the call modal: the call form is a word of its
// own and no entry is modal.
public static partial class DrillingFamily
{
    // SDIS is unsigned and added to the reference plane RFP: CLEARANCE = RFP + SDIS (siemens 7, controller-mapping 5).
    private static readonly string[] s_siemensAbsolute = ["CLEARANCE"];

    // The cycle always returns to RTP, so CYCLE_RETRACT is a rule (controller-mapping 5); so is AXIS, the plane of
    // G17..G19, where no _AXN carries it. Both follow from the params (CycleEntry.RuleWords, D157).

    // The full 840D sl signatures of SINUMERIK 840D sl NC-Programmierung 06/2019 (CNC-SW 4.92), section 3.25, as
    // cycles/siemens.toml writes them (D181; cnc-manuals connector
    // Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019): CYCLE81 p.1082, CYCLE82 p.1083,
    // CYCLE83 p.1086, CYCLE84 p.1089, CYCLE85 p.1092, CYCLE86 p.1093.
    private static readonly string[] s_cycle81 =
        ["RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "_GMODE", "_DMODE", "_AMODE"];

    private static readonly string[] s_cycle82 =
    [
        "RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "_GMODE", "_DMODE", "_AMODE", "_VARI", "S_ZA", "S_FA", "S_ZD", "S_FD",
    ];

    private static readonly string[] s_cycle83 =
    [
        "RTP", "RFP", "SDIS", "DP", "DPR", "FDEP", "FDPR", "_DAM", "DTB", "DTS", "FRF", "VARI", "_AXN", "_MDEP", "_VRT",
        "_DTD", "_DIS1", "_GMODE", "_DMODE", "_AMODE",
    ];

    private static readonly string[] s_cycle84 =
    [
        "RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "SDAC", "MPIT", "PIT", "POSS", "SST", "SST1", "_AXN", "_PITA",
        "_TECHNO", "_VARI", "_DAM", "_VRT", "_PITM", "_PTAB", "_PTABA", "_GMODE", "_DMODE", "_AMODE",
    ];

    private static readonly string[] s_cycle85 =
        ["RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "FFR", "RFF", "_GMODE", "_DMODE", "_AMODE"];

    private static readonly string[] s_cycle86 =
    [
        "RTP", "RFP", "SDIS", "DP", "DPR", "DTB", "SDIR", "RPA", "RPO", "RPAP", "POSS", "_GMODE", "_DMODE", "_AMODE",
    ];

    // D181: a position left out takes the value zero (840D sl Zyklen 01/2008 p.18), so an omitted VARI of CYCLE83 is
    // 0, chip breaking; the reader assumes it (D164).
    private static readonly Dictionary<string, string> s_cycle83Defaults = new() { ["VARI"] = "0" };

    // D181: the ISO 261 coarse pitch of the metric thread sizes MPIT of CYCLE84 and CYCLE840 can stand for, 3 for M3
    // to 48 for M48 (SW4.92 p.1089: "Gewindegröße nur für ISO metrisch").
    private static readonly Dictionary<decimal, decimal> s_isoCoarsePitch = new()
    {
        [3m] = 0.5m,
        [4m] = 0.7m,
        [5m] = 0.8m,
        [6m] = 1m,
        [7m] = 1m,
        [8m] = 1.25m,
        [10m] = 1.5m,
        [12m] = 1.75m,
        [14m] = 2m,
        [16m] = 2m,
        [18m] = 2.5m,
        [20m] = 2.5m,
        [22m] = 2.5m,
        [24m] = 3m,
        [27m] = 3m,
        [30m] = 3.5m,
        [33m] = 3.5m,
        [36m] = 4m,
        [39m] = 4m,
        [42m] = 4.5m,
        [45m] = 4.5m,
        [48m] = 5m,
    };

    /// <summary>
    /// The ISO 261 coarse pitch of a metric thread size, the MPIT of CYCLE84 and CYCLE840, whose pitch the cycle
    /// computes at run time: 1.5 for M10 (D181; SW4.92 p.1089). The sign of MPIT gives the hand and is not part of the
    /// size. Null for a size that is no ISO coarse size between M3 and M48, which the reader keeps native (D94).
    /// </summary>
    /// <param name="threadSize">The MPIT value, 10 for M10.</param>
    public static decimal? IsoCoarsePitch(decimal threadSize)
    {
        return s_isoCoarsePitch.TryGetValue(Math.Abs(threadSize), out decimal pitch) ? pitch : null;
    }

    /// <summary>
    /// Whether the _DP of a CYCLE830 is absolute: the ones digit of _AMODE (position 38) is 1 = absolute, 0 =
    /// incremental, the reverse of CYCLE81 to CYCLE86; an omitted _AMODE is 0, so its _DP is incremental and DEPTH is
    /// RFP less |_DP| counted away from the retract plane, the DPR rule of D334 (D380; SW4.92 p.1120, 1122-1123).
    /// </summary>
    /// <param name="amode">The _AMODE of the call; null where the call leaves it out.</param>
    public static bool Cycle830DepthIsAbsolute(decimal? amode)
    {
        return amode is decimal value && Math.Abs(decimal.Truncate(value)) % 10 == 1;
    }

    // DRILL is CYCLE81, DRILL_DWELL CYCLE82, PECK and CHIP_BREAK CYCLE83 with VARI 1 or 0, TAP CYCLE84, REAM CYCLE85,
    // BORE CYCLE86 (controller-mapping 5).
    private static CycleCatalog SiemensFamily()
    {
        return new CycleCatalog
        {
            Controller = Controller.Siemens,
            Entries =
            [
                SiemensEntry("DRILL", "CYCLE81", s_cycle81, SiemensModalFeedParams(), null),
                SiemensEntry("DRILL_DWELL", "CYCLE82", s_cycle82, SiemensModalFeedParams(), null),
                SiemensEntry("PECK", "CYCLE83", s_cycle83, SiemensPeckParams(), 1m)
                    with { Defaults = s_cycle83Defaults },
                SiemensEntry("CHIP_BREAK", "CYCLE83", s_cycle83, SiemensPeckParams(), 0m)
                    with { Defaults = s_cycle83Defaults },
                SiemensEntry("TAP", "CYCLE84", s_cycle84, SiemensTapParams(), null),
                SiemensEntry("REAM", "CYCLE85", s_cycle85, SiemensReamParams(), null),
                SiemensEntry("BORE", "CYCLE86", s_cycle86, SiemensModalFeedParams(), null),
            ],
        };
    }

    // One Siemens drilling entry; VARI tells PECK from CHIP_BREAK on CYCLE83 (controller-mapping 5).
    private static CycleEntry SiemensEntry(
        string name,
        string native,
        string[] signature,
        Dictionary<string, string> parameters,
        decimal? vari)
    {
        var fixedValues = new Dictionary<string, decimal>();
        if (vari is decimal value)
        {
            fixedValues["VARI"] = value;
        }

        return new CycleEntry
        {
            Name = name,
            Native = native,
            Signature = signature,
            Params = parameters,
            AbsoluteFromSurface = s_siemensAbsolute,
            Fixed = fixedValues,
            Family = Controller.Siemens,
        };
    }

    // RTP retract plane is SAFE, RFP reference plane SURFACE, SDIS the safety distance of CLEARANCE, DP the absolute
    // DEPTH, DTB the dwell (siemens 7, controller-mapping 5).
    private static Dictionary<string, string> SiemensPlanes()
    {
        return new Dictionary<string, string>
        {
            ["SAFE"] = "RTP",
            ["SURFACE"] = "RFP",
            ["CLEARANCE"] = "SDIS",
            ["DEPTH"] = "DP",
            ["CYCLE_DWELL"] = "DTB",
        };
    }

    // The cycle feed is the modal F: the move to the final depth of CYCLE81, CYCLE82 and CYCLE86 uses the feed
    // programmed before the call (840D sl Zyklen 01/2008 p.46, 49, 77; D181), and the reader turns it into CYCLE_F, an
    // address word before the call and no position of the signature (controller-mapping 5, machine-config 6).
    private static Dictionary<string, string> SiemensModalFeedParams()
    {
        Dictionary<string, string> parameters = SiemensPlanes();
        parameters["CYCLE_F"] = "F";
        return parameters;
    }

    // CYCLE83 uses the modal F as well and carries _AXN, the drilling axis, which is exactly AXIS (controller-mapping
    // 5, D59). D181: the constant PECK is FDPR, the first depth relative to RFP, with _DAM = 0 and FDEP left empty.
    // D356: for PECK and CHIP_BREAK without PECK, or with PECK = 0, the compiler writes FDPR = the whole depth in one
    // infeed, the distance from DEPTH to SURFACE, or to CLEARANCE where the block gives no SURFACE; FDEP stays empty.
    // D181 (gap closed 2026-10-27, code wave 2): the dwell at the bottom, CYCLE_DWELL, is _DTD, position 16,
    // "Verweilzeit auf Endbohrtiefe"; DTB of CYCLE83 is the dwell at every drilling depth (SW4.92 p.1086-1087), as on
    // CYCLE830 (p.1120).
    private static Dictionary<string, string> SiemensPeckParams()
    {
        Dictionary<string, string> parameters = SiemensPlanes();
        parameters["CYCLE_DWELL"] = "_DTD";
        parameters["PECK"] = "FDPR";
        parameters["AXIS"] = "_AXN";
        parameters["CYCLE_F"] = "F";
        return parameters;
    }

    // CYCLE84: PIT the pitch and _AXN the drilling axis (controller-mapping 5). D181: CYCLE84 taps with G331/G332 at
    // SST and SST1 and uses no programmed feed (840D sl Zyklen 01/2008 p.58-62), so CYCLE_F is not mapped; D362: a
    // program's CYCLE_F is derivable from PITCH (CycleEntry.DerivedFrom).
    private static Dictionary<string, string> SiemensTapParams()
    {
        Dictionary<string, string> parameters = SiemensPlanes();
        parameters["PITCH"] = "PIT";
        parameters["AXIS"] = "_AXN";
        return parameters;
    }

    // CYCLE85 has its own feed FFR (controller-mapping 5).
    private static Dictionary<string, string> SiemensReamParams()
    {
        Dictionary<string, string> parameters = SiemensPlanes();
        parameters["CYCLE_F"] = "FFR";
        return parameters;
    }
}
