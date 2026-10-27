using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The datums of a Fanuc block (controllers fanuc.md 4; D244): G54 to G59 as ORIGIN, G28 and G30 as HOME, G92
/// (G50 in system A) as SETPOS and RPM_MAX, G52 as SHIFT, and where the modes of the control forbid them.
/// </summary>
internal static partial class FanucFrames
{
    // G54 to G59 are the datums 1 to 6, G54.1 Pn the datum 6 + n (language 4.2, ORIGIN; controller-mapping 1). ORIGIN
    // starts an empty chain (language 4.2).
    // D244 (a): a G52 sets the local system in all of G54 to G59, so after the ORIGIN the reader writes the active G52
    // SHIFT again (B-64484EN p.216-217). Where the callers of a subprogram leave that shift unknown, the block stays
    // RAW (D225). (c): no G54 to G59 under G68, G51.1, a 3D G68 or G68.2 (Prohibition).
    private static void ReadOrigin(FanucBlock block)
    {
        int? origin = null;
        string code = "";
        for (int index = 0; index < s_origins.Length; index++)
        {
            if (block.TakeCode(s_origins[index]))
            {
                origin = index + 1;
                code = s_origins[index];
            }
        }

        if (block.TakeCode("G54.1"))
        {
            SourceWord? extra = block.Take("P");
            if (extra?.Number is not decimal number || number < 1 || number != decimal.Truncate(number))
            {
                block.Draft.KeepAsRaw("G54.1 names no additional datum P");
                return;
            }

            origin = 6 + decimal.ToInt32(number);
            code = "G54.1";
        }

        if (origin is not int datum)
        {
            return;
        }

        FanucChain chain = block.Fanuc.Chain;
        if (Prohibition(block, code, true) is string prohibited)
        {
            block.Draft.KeepAsRaw(prohibited);
            return;
        }

        if (!chain.Known)
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason($"the G52 shift that {code} keeps"));
            return;
        }

        block.Draft.AddState("ORIGIN", null, Integer(datum));
        ClearChain(block);
        List<Word> local = LocalWords(chain);
        if (local.Count > 0)
        {
            var blocks = new List<DraftBlock>();
            chain.Append(new FanucChainEntry("SHIFT", "G52", local), blocks);
            Write(block, blocks);
        }
    }

    /// <summary>
    /// Why a coordinate-system code stays RAW under a mode of the control, or null where it may stand (D244 (c), (d)):
    /// under the 2D rotation G68 and the mirror G51.1 neither G27 to G30 nor G52 to G59 and G92 may be given, PS0412 on
    /// the 0i-D (B-64304EN-2 p.191; B-64484EN p.368); under the 3D conversion G68 with I J K not G52 to G59 and G92
    /// (B-64484EN p.344-345); under G68.2 NCX keeps G52 to G59 and G92 native, since whether the control allows them
    /// depends on its edition and, for G54 to G59, on parameter 1205#6 (B-64484EN p.890-891, 948-949).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="code">The code, "G54".</param>
    /// <param name="changesCoordinates">True for G52 to G59 and G92, false for G28 and G30.</param>
    public static string? Prohibition(FanucBlock block, string code, bool changesCoordinates)
    {
        FanucChain chain = block.Fanuc.Chain;
        if (chain.Active("G68") || chain.Active("G51.1"))
        {
            return $"{code} may not stand while the coordinate rotation G68 or the mirror image G51.1 is on, the "
                + "control raises an alarm (PS0412 on the 0i-D; D244)";
        }

        if (!changesCoordinates)
        {
            return null;
        }

        if (chain.Conversions > 0)
        {
            return $"{code} may not stand in the three-dimensional coordinate conversion G68 with I J K, the control "
                + "raises an alarm (D244)";
        }

        return chain.Active("G68.2")
            ? $"NCX keeps {code} under the tilted working plane of G68.2 native: whether the control allows it depends "
                + "on its edition and on parameter 1205#6 (D244)"
            : null;
    }

    // G28 returns the named axes to the reference point, G30 P to the P-th one, POINT=P, the second by default; the
    // axes are unknown in the workpiece frame afterwards (controllers fanuc.md 4, 9 rule 3; controller-mapping 1, HOME;
    // virtual machine 3.4).
    // D243: the words of G28 and G30 are the intermediate point, reached at rapid before the return; only an
    // incremental 0 means no intermediate move, and an absolute word, 0 included, writes a RAPID to it first. An
    // address with no incremental twin is always absolute, so G28 B0 of system A is RAPID B=0 then HOME B
    // (decisions.md).
    private static void ReadHome(FanucBlock block)
    {
        bool reference = block.TakeCode("G28");
        bool further = !reference && block.TakeCode("G30");
        if (!reference && !further)
        {
            return;
        }

        SourceWord? point = further ? block.Take("P") : null;
        List<FanucAxisWord> axes = FanucAxes.Unread(block);
        if (axes.Count == 0)
        {
            return;
        }

        // D244 (d): no G28 or G30 under the 2D G68 or G51.1; under the 3D G68 and G68.2 they are HOME.
        if (Prohibition(block, reference ? "G28" : "G30", false) is string prohibited)
        {
            block.Draft.KeepAsRaw(prohibited);
            return;
        }

        var intermediate = new DraftBlock().WithVerb("RAPID");
        DraftBlock home = block.Draft.Main.WithVerb("HOME");
        foreach (FanucAxisWord axis in axes)
        {
            block.MarkRead(axis.Word);
            if (FanucMacro.ValueOf(block, axis.Word) is not Value value)
            {
                return;
            }

            // Whether an absolute address is absolute or incremental is G90 or G91, which the reader may not know
            // here (FanucCallerState).
            if (!axis.Incremental && block.Fanuc.Unknowns.Groups.Contains(FanucModalGroups.Distance))
            {
                block.Draft.KeepAsRaw(FanucUnknowns.Reason("G90 or G91"));
                return;
            }

            if (!axis.Incremental || FanucNumbers.NumberOf(value) != 0m)
            {
                intermediate.Add(axis.Key, value);
            }

            home.Add(axis.Axis, NoValue.Instance);
            block.State.ForgetPosition(axis.Axis);
        }

        if (intermediate.Words.Count > 0)
        {
            block.Draft.Before.Add(intermediate);
        }

        if (further)
        {
            decimal number = point?.Number ?? 2m;
            home.Add("POINT", Integer(decimal.ToInt32(decimal.Truncate(number))));
        }
    }

    // G92 X Z (mills, systems B and C) and G50 X Z (system A) declare the current position, SETPOS; with S they set
    // the speed limit, RPM_MAX (controllers fanuc.md 4; controller-mapping 1 SETPOS, 4 RPM_MAX; D55). On a mill G50
    // cancels the scaling, which NCX does not write. SETPOS carries absolute words only (language 4.2), so the reader
    // folds an incremental word, the G50 U(u) W(w) of system A and a G92 under G91, into the coordinate it declares:
    // the current tool position becomes (x+u, z+w), and where that position is not known the block stays RAW (D117).
    private static void ReadSetpos(FanucBlock block)
    {
        string code = block.System == GcodeSystem.A ? "G50" : "G92";
        if (!block.TakeCode(code))
        {
            return;
        }

        SourceWord? limit = block.Take("S");
        if (limit is not null && block.Fanuc.Unknowns.Spindle)
        {
            // The limit belongs to the spindle selected last, which the reader may not know here (FanucCallerState).
            block.Draft.KeepAsRaw(FanucUnknowns.Reason("the spindle selected last, whose speed S limits"));
            return;
        }

        if (limit is not null && FanucMacro.ValueOf(block, limit) is Value speed)
        {
            string? role = block.State.LastSpindle;
            block.Draft.AddState("RPM_MAX", FanucFunctions.SpindleAddress(block, role), speed);
        }

        List<FanucAxisWord> axes = FanucAxes.Unread(block);
        if (axes.Count == 0)
        {
            return;
        }

        FanucChain chain = block.Fanuc.Chain;
        if (Prohibition(block, code, true) is string prohibited)
        {
            block.Draft.KeepAsRaw(prohibited);
            return;
        }

        if (!chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain(code));
            return;
        }

        DraftBlock setpos = block.Draft.Main.WithVerb("SETPOS");
        foreach (FanucAxisWord word in axes)
        {
            block.MarkRead(word.Word);
            if (FanucMacro.ValueOf(block, word.Word) is not Value value)
            {
                return;
            }

            FanucAxisWord axis = word;
            if (axis.Incremental)
            {
                if (FanucNumbers.NumberOf(value) is not decimal distance
                    || !block.State.Positions.TryGetValue(axis.Axis, out decimal position))
                {
                    block.Draft.KeepAsRaw($"an incremental word of {code} declares the current position plus its "
                        + $"value and the position of {axis.Axis} is not known here");
                    return;
                }

                value = FanucNumbers.Of(position + distance);
                axis = axis with { Incremental = false };
            }
            else if (block.Fanuc.Unknowns.Groups.Contains(FanucModalGroups.Distance))
            {
                // Whether the word is the coordinate or the increment the reader folds depends on G90 or G91, which
                // the reader may not know here (FanucCallerState).
                block.Draft.KeepAsRaw(FanucUnknowns.Reason("G90 or G91"));
                return;
            }

            setpos.Add(axis.Axis, value);
            FanucMotion.Move(block, axis, value);
        }

        // D244 (d): a G92 cancels the G52 shift of the axes it names and keeps the others (B-64484EN p.216-217,
        // caution 4): SHIFT=RESET before the SETPOS, the G52 SHIFT of the other axes after it.
        foreach (FanucAxisWord axis in axes)
        {
            chain.Local.Remove(axis.Axis);
        }

        if (chain.Holds("G52"))
        {
            var reset = new List<DraftBlock>();
            chain.TryChange([], reset);
            block.Draft.Before.AddRange(reset);
            List<Word> local = LocalWords(chain);
            if (local.Count > 0)
            {
                var again = new List<DraftBlock>();
                chain.Append(new FanucChainEntry("SHIFT", "G52", local), again);
                block.Draft.After.AddRange(again);
            }
        }
    }

    // G52 replaces the earlier G52, so the reader writes SHIFT=RESET before the new SHIFT when one is active; G52 X0 Y0
    // Z0 cancels (controllers fanuc.md 4, 9 rule 6; controller-mapping 1, SHIFT; language 4.2; D31). The axes the new
    // G52 does not name are 0. The control holds the new shift also where the reader keeps the block as RAW.
    // D244 (b): in a G52 block U and W are the lathe spelling of the absolute local origin, measured from the active
    // G54 to G59 zero like X and Z and not added to the active G52, and G52 U0 W0 cancels like X0 Z0 (Nakamura 31i-A
    // p.51-52; D117). (c): under G68, G51.1, a 3D G68 or G68.2 the G52 stays RAW (Prohibition), so the chain holds at
    // most the SHIFT of the earlier G52 here.
    // D125: on a lathe the work shift X is a diameter, as every X word under DIAMETER=ON, SHIFT X included (language
    // 4.2), so the reader writes the value as the source gives it.
    private static void ReadShift(FanucBlock block)
    {
        if (!block.TakeCode("G52"))
        {
            return;
        }

        if (Prohibition(block, "G52", true) is string prohibited)
        {
            block.Draft.KeepAsRaw(prohibited);
            return;
        }

        FanucChain chain = block.Fanuc.Chain;
        var shift = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var words = new List<Word>();
        foreach (FanucAxisWord axis in FanucAxes.Unread(block))
        {
            block.MarkRead(axis.Word);
            if (axis.Word.Number is not decimal number || axis.Word.Expression is not null)
            {
                block.Draft.KeepAsRaw("G52 with a variable is kept as RAW");
                return;
            }

            shift[axis.Axis] = number;
            words.Add(new Word
            {
                Key = axis.Axis,
                Value = axis.Word.ToNcxNumber()!,
            });
        }

        chain.Local.Clear();
        bool shifts = false;
        foreach (KeyValuePair<string, decimal> axis in shift)
        {
            chain.Local[axis.Key] = axis.Value;
            shifts |= axis.Value != 0;
        }

        block.Fanuc.ForgetPositions();
        if (!chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain("G52"));
            return;
        }

        var blocks = new List<DraftBlock>();
        chain.TryChange(shifts ? [new FanucChainEntry("SHIFT", "G52", words)] : [], blocks);
        Write(block, blocks);
    }
}
