using Ncx.Core.Machine;

namespace Ncx.Readers.Siemens;

// The rules of the sl cycle forms the reader converts beyond the params of the catalog entry (controllers siemens.md
// 7, 11 rule 5): the values of NUM and _AMODE that select the entries of SLOT1, SLOT2 and CYCLE830 (D379, D380), the
// depth mode of those cycles, and the calls that stay native because NCX has no word for what they set (D181, D379;
// D94).
internal static partial class SiemensCycles
{
    /// <summary>
    /// The native values the reader matches against the fixed values of the entries: NUM empty or 0 is NUM = 1 on
    /// SLOT1 and SLOT2, the one slot of the sl form (D379; SW4.92 p.1053, 1055); an _AMODE of 0, 1 or empty is the
    /// fixed 1 of CYCLE830, whose ones digit gives only the depth mode the reader converts (D380; p.1123), and an
    /// _AMODE of 0, 1, 2 or empty the fixed 2 of SLOT1, ones digit 0 compatibility, 1 incremental, 2 absolute
    /// (p.1054). SLOT2 has the same digit (p.1057) and fixes no _AMODE, so ModeAccepted lets it through.
    /// </summary>
    /// <param name="native">The native cycle, "SLOT1".</param>
    /// <param name="numbers">The native values the call gives, by native name.</param>
    private static Dictionary<string, decimal> Selecting(string native, Dictionary<string, decimal> numbers)
    {
        var selecting = new Dictionary<string, decimal>(numbers, StringComparer.Ordinal);
        if (native is "SLOT1" or "SLOT2" && (!numbers.TryGetValue("NUM", out decimal count) || count == 0))
        {
            selecting["NUM"] = 1;
        }

        decimal? amode = numbers.TryGetValue("_AMODE", out decimal given) ? given : null;
        if (native == "CYCLE830" && amode is null or 0 or 1)
        {
            selecting["_AMODE"] = 1;
        }
        else if (native == "SLOT1" && amode is null or 0 or 1 or 2)
        {
            selecting["_AMODE"] = 2;
        }

        return selecting;
    }

    /// <summary>
    /// Whether the depth _DP of the call is incremental, counted from RFP away from the retract plane as DPR is (D334):
    /// on CYCLE830 the ones digit of _AMODE 0 or an omitted _AMODE (D380; DrillingFamily.Cycle830DepthIsAbsolute), on
    /// SLOT1 the ones digit 1, "Z1 (ink)" (SW4.92 p.1054).
    /// </summary>
    /// <param name="native">The native cycle.</param>
    /// <param name="numbers">The native values the call gives, by native name.</param>
    private static bool DepthIsIncremental(string native, Dictionary<string, decimal> numbers)
    {
        decimal? amode = numbers.TryGetValue("_AMODE", out decimal given) ? given : null;
        return native switch
        {
            "CYCLE830" => !DrillingFamily.Cycle830DepthIsAbsolute(amode),
            "SLOT1" or "SLOT2" => amode == 1,
            _ => false,
        };
    }

    // A mode integer the reader reads itself although the entry does not fix it: the _AMODE ones digit 0, 1 or 2 of
    // SLOT2, "Endtiefe Z1" compatibility, incremental or absolute (SW4.92 p.1057), as for SLOT1.
    private static bool ModeAccepted(string native, string mode, decimal? value)
    {
        return native == "SLOT2" && mode == "_AMODE" && value is 0 or 1 or 2;
    }

    /// <summary>
    /// Why a call that matches an entry stays native, or null where the reader converts it: a CYCLE83 that sets FDEP
    /// or a _DAM other than 0 has no constant PECK (D181); a CYCLE840 with ENC = 1 takes its pitch from the feed,
    /// which TAP_COMPENSATING does not carry where the entry does not fix it (D181; SW4.92 p.1129); a SLOT1 or SLOT2
    /// whose reserved RAD or INDA is set, or a direct call whose reference point _CPA/_CPO is not the position reached,
    /// is no single slot at the call position (D379; SW4.92 p.1053; Bedienhandbuch Fräsen 840D sl/828D 2013, connector
    /// p.361-363, the input window "Längsnut (SLOT1)", whose reference point counts for a single position).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="entry">The entry the call matches.</param>
    /// <param name="arguments">The arguments of the call in signature order.</param>
    /// <param name="modal">True for MCALL, whose positions come from the blocks that follow.</param>
    private static string? NativeOnly(SiemensBlock block, CycleEntry entry, List<string> arguments, bool modal)
    {
        string native = entry.Native;
        if (native == "CYCLE83" && (GivenAt(entry, arguments, "FDEP") || NotZero(entry, arguments, "_DAM")))
        {
            return "CYCLE83 sets the first depth FDEP or a degression _DAM, and NCX has only the constant PECK, FDPR "
                + "with _DAM = 0 (D181)";
        }

        // D181: MPIT and PIT are millimetres only with _PITA 0 (MPIT and PIT) or 1 (PIT); TPI, inch and module stay
        // native (SW4.92 p.1129-1130 for CYCLE840, p.1089-1091 for CYCLE84).
        decimal? pita = NumberAt(entry, arguments, "_PITA");
        if (PositionOf(entry, "_PITA") >= 0 && pita is not (null or 0)
            && (pita != 1 || GivenAt(entry, arguments, "MPIT")))
        {
            return $"{native} gives the pitch with _PITA = {pita}, in a unit or a form PITCH does not carry (D181)";
        }

        if (native == "CYCLE840" && !entry.Fixed.ContainsKey("ENC") && NumberAt(entry, arguments, "ENC") == 1)
        {
            return "CYCLE840 with ENC = 1 takes its pitch from the programmed feed, which TAP_COMPENSATING does not "
                + "carry (D181)";
        }

        if (native is not ("SLOT1" or "SLOT2"))
        {
            return null;
        }

        if (native == "SLOT1" && (NotZero(entry, arguments, "RAD") || NotZero(entry, arguments, "INDA")))
        {
            return "SLOT1 sets RAD or INDA, reserved in the sl form, so it is the older pattern of slots (D379)";
        }

        if (modal)
        {
            return null;
        }

        var plane = SiemensPlane.Of(block.Facts.WorkingPlane);
        if (!AtPosition(block, entry, arguments, "_CPA", plane.First)
            || !AtPosition(block, entry, arguments, "_CPO", plane.Second))
        {
            return $"{native} has its reference point _CPA/_CPO away from the position reached, and SLOT and CIRC_SLOT "
                + "stand at the call position (D379)";
        }

        return null;
    }

    // True where the reference point of a direct call is the position of the axis the reader knows.
    private static bool AtPosition(SiemensBlock block, CycleEntry entry, List<string> arguments, string native,
        string axis)
    {
        return NumberAt(entry, arguments, native) is decimal value
            && block.State.Positions.TryGetValue(axis, out decimal position) && position == value;
    }

    private static bool GivenAt(CycleEntry entry, List<string> arguments, string native)
    {
        int index = PositionOf(entry, native);
        return index >= 0 && index < arguments.Count && arguments[index].Length > 0;
    }

    private static bool NotZero(CycleEntry entry, List<string> arguments, string native)
    {
        return GivenAt(entry, arguments, native) && NumberAt(entry, arguments, native) != 0;
    }

    private static decimal? NumberAt(CycleEntry entry, List<string> arguments, string native)
    {
        int index = PositionOf(entry, native);
        return index >= 0 && index < arguments.Count
            ? SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[index]))
            : null;
    }

    private static int PositionOf(CycleEntry entry, string native)
    {
        for (int index = 0; index < entry.Signature.Count; index++)
        {
            if (entry.Signature[index] == native)
            {
                return index;
            }
        }

        return -1;
    }
}
