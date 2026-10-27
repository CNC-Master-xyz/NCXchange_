using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The frames of a Fanuc block (controllers fanuc.md 4, 9 rules 3 and 6; controller-mapping 1; language 4.2): G54 to
/// G59 and G54.1 P as ORIGIN, G53 as FRAME=MACHINE, G28 and G30 as HOME, G92 (G50 in system A) as SETPOS and RPM_MAX,
/// G52 as SHIFT with SHIFT=RESET, G68/G69 as ROTATE, G51.1/G50.1 as MIRROR, G43.4/G43.5 as TCPM, G12.1/G13.1 as POLAR,
/// G7.1 as CYLINDER, G5.1 as TOLERANCE, and the builder's codes of [transform]. The tilted plane of G68.2 is FanucTilt,
/// the chain of transforms FanucChain.
/// </summary>
internal static class FanucFrames
{
    // G54 to G59 are the datums 1 to 6 (language 4.2, ORIGIN).
    private static readonly string[] s_origins = ["G54", "G55", "G56", "G57", "G58", "G59"];

    private static readonly IdentValue s_on = new("ON");
    private static readonly IdentValue s_off = new("OFF");

    /// <summary>
    /// Reads the frame words of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadOrigin(block);

        // G53 in the block: machine coordinates for that block, also with macro variables (controllers fanuc.md 4, 9
        // rule 3); the motion writes FRAME=MACHINE.
        block.MachineFrame = block.TakeCode("G53");
        ReadHome(block);
        ReadSetpos(block);
        ReadShift(block);
        ReadRotation(block);
        ReadMirror(block);
        FanucTilt.Read(block);
        ReadTcpm(block);
        ReadTransformations(block);
        ReadTolerance(block);
    }

    /// <summary>
    /// Writes the blocks of a change of the chain (FanucChain.Append, FanucChain.TryChange): before the main block,
    /// each in a block of its own, since a RESET and the entries after it depend on their order (language 4.2) while
    /// the state words of one block do not (virtual machine 3, step 3); after it when the main block selects the datum,
    /// which empties the chain first (language 4.2, ORIGIN).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="blocks">The NCX blocks of the change, in order.</param>
    public static void Write(FanucBlock block, List<DraftBlock> blocks)
    {
        List<DraftBlock> place = block.Draft.Main.Has("ORIGIN", null) ? block.Draft.After : block.Draft.Before;
        place.AddRange(blocks);
    }

    /// <summary>
    /// Why a block stays RAW whose code acts on entries of a chain the reader does not know (FanucChain.Known).
    /// </summary>
    /// <param name="code">The code, "G69".</param>
    public static string UnknownChain(string code)
    {
        return $"{code} acts on the transforms of the caller of its subprogram, or on those a called subprogram left, "
            + "which the reader does not know (virtual machine 3.9)";
    }

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

    // G68 X Y R rotates the plane by R about the centre X Y, G69 cancels it and the tilted plane of G68.2 (controllers
    // fanuc.md 4; controller-mapping 1, ROTATE and TILT). D245 (a): ROTATE turns about the current origin (language
    // 4.2), and a G68 without X Y turns about the tool position (B-64304EN-2 p.191), so the reader writes ROTATE where
    // the centre is the origin of the plane, given or known as the tool position; otherwise, and without R (parameter
    // 5410), the block stays RAW and the control rotates natively. (b): a second 2D G68 under absolute R replaces the
    // first, ROTATE=RESET plus ROTATE; under G91 parameter 5400#0 may add R, so it stays RAW (B-64304EN-2 p.190,
    // 196-197). A G68 with I J K is the 3D conversion, kept as RAW: a second one accumulates, a third raises PS5043
    // (B-64484EN p.342-344). G68 while G68.2 is on raises PS5462 (B-64484EN p.948-950).
    private static void ReadRotation(FanucBlock block)
    {
        if (block.TakeCode("G68"))
        {
            ReadG68(block);
            return;
        }

        if (block.TakeCode("G69"))
        {
            ReadCancel(block);
        }
    }

    private static void ReadG68(FanucBlock block)
    {
        FanucChain chain = block.Fanuc.Chain;
        SourceWord? angle = block.Take("R");
        bool spatial = false;
        foreach (string address in new[] { "I", "J", "K" })
        {
            spatial |= block.Take(address) is not null;
        }

        List<FanucAxisWord> centre = FanucAxes.Unread(block);
        foreach (FanucAxisWord axis in centre)
        {
            block.MarkRead(axis.Word);
        }

        if (chain.Active("G68.2"))
        {
            block.Draft.KeepAsRaw("G68 may not stand while the tilted working plane of G68.2 is on, the control raises "
                + "PS5462 (D245)");
            return;
        }

        if (spatial)
        {
            ReadConversion(block, chain);
            return;
        }

        if (chain.Conversions > 0)
        {
            block.Draft.KeepAsRaw("a G68 without I J K while the three-dimensional coordinate conversion is on is kept "
                + "as RAW (D245)");
            return;
        }

        if (angle is null)
        {
            KeepRotationNative(block, "G68 names no angle R, and the control turns by the angle of parameter 5410 "
                + "(D245)");
            return;
        }

        if (CentreAwayFromTheOrigin(block, centre) is string reason)
        {
            KeepRotationNative(block, reason);
            return;
        }

        if (FanucMacro.ValueOf(block, angle) is not Value value)
        {
            chain.SetNative("G68", true);
            return;
        }

        var rotation = new FanucChainEntry("ROTATE", "G68", [new Word { Key = "ROTATE", Value = value }]);
        bool replaces = chain.Holds("G68") || chain.HoldsNative("G68");
        if (replaces && block.Incremental)
        {
            KeepRotationNative(block, "a second G68 under G91 adds R to the active angle or not by parameter 5400#0, "
                + "which the reader does not know (D245)");
            return;
        }

        var blocks = new List<DraftBlock>();
        if (chain.Holds("G68"))
        {
            var desired = new List<FanucChainEntry>();
            foreach (FanucChainEntry entry in chain.Entries)
            {
                desired.Add(entry.Owner == "G68" ? rotation : entry);
            }

            if (!chain.TryChange(desired, blocks))
            {
                KeepRotationNative(block, "the second G68 would unwind the chain of transforms with a RESET that the "
                    + "documents read in two ways (language 4.2)");
                return;
            }
        }
        else
        {
            chain.Append(rotation, blocks);
        }

        chain.SetNative("G68", false);
        Write(block, blocks);
        block.Fanuc.ForgetPositions();
    }

    // D245: the 3D conversion G68 with I J K stays RAW; the control holds at most two, measured one in the other, and
    // a third raises PS5043 (B-64484EN p.342-344). A 3D G68 while a 2D G68 is on stays RAW without being counted.
    private static void ReadConversion(FanucBlock block, FanucChain chain)
    {
        if (chain.Conversions >= 2)
        {
            block.Draft.KeepAsRaw("a third three-dimensional coordinate conversion G68 raises PS5043 on the control "
                + "(D245)");
            return;
        }

        if (!chain.Active("G68"))
        {
            chain.Conversions++;
        }

        block.Draft.KeepAsRaw("the three-dimensional coordinate conversion G68 with I J K is kept as RAW (D245)");
        block.Fanuc.ForgetPositions();
    }

    // A G68 the reader keeps as RAW still rotates on the control, which its G69 must cancel there (D245).
    private static void KeepRotationNative(FanucBlock block, string reason)
    {
        block.Draft.KeepAsRaw(reason);
        block.Fanuc.Chain.SetNative("G68", true);
        block.Fanuc.ForgetPositions();
    }

    // D245 (a): the centre of G68 is the X Y of its plane where the block gives it, and the tool position where it
    // does not (B-64304EN-2 p.191); ROTATE turns about the origin of the plane, so either must be 0. The reason the
    // block stays RAW, or null where the centre is the origin.
    private static string? CentreAwayFromTheOrigin(FanucBlock block, List<FanucAxisWord> centre)
    {
        string[] plane = PlaneAxes(block);
        foreach (FanucAxisWord axis in centre)
        {
            if (axis.Word.Number != 0m)
            {
                return "G68 turns about the point X Y and ROTATE about the origin (language 4.2)";
            }
        }

        foreach (string axis in plane)
        {
            bool given = centre.Exists(word => word.Axis == axis);
            if (!given && (!block.State.Positions.TryGetValue(axis, out decimal position) || position != 0m))
            {
                return $"G68 without {axis} turns about the tool position, which the reader does not know as the "
                    + "origin of the plane here (language 4.2; D245)";
            }
        }

        return null;
    }

    // The two axes of the plane of G17, G18 or G19, the plane of G17 on a mill and of G18 on a lathe without one
    // (controllers fanuc.md 3).
    private static string[] PlaneAxes(FanucBlock block)
    {
        return block.State.ActiveCode(FanucModalGroups.Plane) switch
        {
            "G17" => ["X", "Y"],
            "G18" => ["Z", "X"],
            "G19" => ["Y", "Z"],
            _ => block.IsLathe ? ["Z", "X"] : ["X", "Y"],
        };
    }

    // G69 cancels G68 and the tilted plane of G68.2, its origin with it (controllers fanuc.md 4): the chain keeps the
    // entries of the other functions, and of a SHIFT that holds a G52 shift and the origin of the G68.2 together the
    // G52 shift (FanucTilt). It also ends the 3D conversions (D245). A G69 that cancels a G68 or G68.2 the reader kept
    // as RAW stays RAW, so that the control gets it back.
    private static void ReadCancel(FanucBlock block)
    {
        FanucChain chain = block.Fanuc.Chain;
        bool native = chain.HoldsNative("G68") || chain.HoldsNative("G68.2") || chain.Conversions > 0;
        chain.Conversions = 0;
        chain.SetNative("G68", false);
        chain.SetNative("G68.2", false);
        if (native)
        {
            block.Draft.KeepAsRaw("G69 cancels a G68 or G68.2 the reader kept as RAW (D245)");
            if (chain.Holds("G68") || chain.Holds("G68.2"))
            {
                chain.Forget();
            }

            block.Fanuc.ForgetPositions();
            return;
        }

        if (!chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain("G69"));
            return;
        }

        Change(block, WithoutRotations(chain), "G69");
    }

    /// <summary>
    /// The chain without the entries of G68 and G68.2, the G52 shift of a SHIFT that held it with the origin of a
    /// G68.2 kept (FanucTilt): the chain after a G69, or under a G68.2 that replaces the active one (D245).
    /// </summary>
    /// <param name="chain">The chain of the reader.</param>
    public static List<FanucChainEntry> WithoutRotations(FanucChain chain)
    {
        var desired = new List<FanucChainEntry>();
        foreach (FanucChainEntry entry in chain.Entries)
        {
            if (entry.Owner is not ("G68" or "G68.2"))
            {
                desired.Add(entry);
            }
            else if (entry.LocalShift is IReadOnlyList<Word> local)
            {
                desired.Add(new FanucChainEntry("SHIFT", "G52", local));
            }
        }

        return desired;
    }

    // G51.1 X0 mirrors the named axes, G50.1 cancels (controllers fanuc.md 4; controller-mapping 1, MIRROR). The value
    // is the position of the mirror line and MIRROR mirrors about the origin (language 4.2), so another value stays
    // RAW.
    private static void ReadMirror(FanucBlock block)
    {
        bool mirror = block.TakeCode("G51.1");
        bool cancel = !mirror && block.TakeCode("G50.1");
        if (!mirror && !cancel)
        {
            return;
        }

        var axes = new List<string>();
        foreach (FanucAxisWord axis in FanucAxes.Unread(block))
        {
            block.MarkRead(axis.Word);
            if (mirror && axis.Word.Number != 0m)
            {
                block.Draft.KeepAsRaw("G51.1 mirrors about a line away from the origin, MIRROR about the origin");
                block.Fanuc.Chain.SetNative("G51.1", true);
                block.Fanuc.ForgetPositions();
                return;
            }

            axes.Add(axis.Axis);
        }

        FanucChain chain = block.Fanuc.Chain;
        if (cancel && chain.HoldsNative("G51.1"))
        {
            // A G50.1 that cancels a mirror the reader kept as RAW stays RAW, so that the control gets it back.
            chain.SetNative("G51.1", false);
            block.Draft.KeepAsRaw("G50.1 cancels a G51.1 the reader kept as RAW");
            if (chain.Holds("G51.1"))
            {
                chain.Forget();
            }

            block.Fanuc.ForgetPositions();
            return;
        }

        if (mirror && axes.Count > 0)
        {
            Value value = axes.Count == 1 ? new IdentValue(axes[0]) : new ListValue(axes);
            var blocks = new List<DraftBlock>();
            chain.Append(new FanucChainEntry("MIRROR", "G51.1", [new Word { Key = "MIRROR", Value = value }]), blocks);
            Write(block, blocks);
            block.Fanuc.ForgetPositions();
        }
        else if (cancel && !chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain("G50.1"));
        }
        else if (cancel && chain.Holds("G51.1"))
        {
            var desired = new List<FanucChainEntry>();
            foreach (FanucChainEntry entry in chain.Entries)
            {
                if (entry.Owner != "G51.1")
                {
                    desired.Add(entry);
                }
            }

            Change(block, desired, "G50.1");
        }
    }

    // The chain becomes the one the control holds after the code (FanucChain.TryChange); where no cut of the chain
    // means the same under both readings of SHIFT=RESET, the block stays RAW (wave-1 question #95; D5).
    private static void Change(FanucBlock block, List<FanucChainEntry> desired, string code)
    {
        var blocks = new List<DraftBlock>();
        if (!block.Fanuc.Chain.TryChange(desired, blocks))
        {
            block.Draft.KeepAsRaw(
                $"{code} would unwind the chain of transforms with a SHIFT=RESET that the documents read in two ways, "
                + "the shifts or the last one (language 4.2, virtual machine 2.1)");
            return;
        }

        Write(block, blocks);
        block.Fanuc.ForgetPositions();
    }

    // G43.4 is tool center point control, G43.5 the same with the tool vector as I J K on the lines, TCPM=ON; G49 ends
    // it (controllers fanuc.md 4; controller-mapping 1, TCPM; language 4.2); a builder's codes, G435 and G436 on the
    // Nakamura, come from [transform]. H of G43.4 is the length offset, OFFSET:LEN. Where the reader does not know
    // whether TCPM is on (FanucCallerState), G49 writes TCPM=OFF, which it is after the G49 either way.
    private static void ReadTcpm(FanucBlock block)
    {
        TransformTable? transform = block.Machine.Transform;
        bool vector = block.TakeCode("G43.5");
        bool on = vector || block.TakeCode("G43.4") || (!IsNativeTcpm(transform?.TcpmOn)
            && FanucTemplates.TryMatch(block, transform?.TcpmOn, out _));
        bool active = block.Fanuc.Tcpm || block.Fanuc.Unknowns.Tcpm;
        bool off = !on && ((active && block.HasCode("G49"))
            || (!IsNativeTcpm(transform?.TcpmOff) && FanucTemplates.TryMatch(block, transform?.TcpmOff, out _)));
        if (on)
        {
            block.Draft.AddState("TCPM", null, s_on);
            block.Fanuc.Tcpm = true;
            block.Fanuc.VectorTcpm = vector;
            block.Fanuc.Unknowns.Tcpm = false;
            if (block.Find("H") is SourceWord length && FanucAxes.Of(length, block) is null)
            {
                block.MarkRead(length);
                if (length.Number is decimal register && register == decimal.Truncate(register))
                {
                    block.Draft.AddState("OFFSET", "LEN", Integer(decimal.ToInt32(register)));
                }
                else
                {
                    block.Draft.KeepAsRaw("the H of G43.4 names no offset register");
                }
            }
        }
        else if (off)
        {
            block.Draft.AddState("TCPM", null, s_off);
            block.Fanuc.Tcpm = false;
            block.Fanuc.VectorTcpm = false;
            block.Fanuc.Unknowns.Tcpm = false;
        }
    }

    // The native codes of tool center point control are read by their number, so a [transform] template that writes
    // them is not matched a second time.
    private static bool IsNativeTcpm(string? template)
    {
        return template is not null
            && (template.StartsWith("G43.4", StringComparison.Ordinal)
                || template.StartsWith("G43.5", StringComparison.Ordinal)
                || template.StartsWith("G49", StringComparison.Ordinal));
    }

    // G12.1 and G13.1 switch polar interpolation, POLAR; G7.1 C r the cylinder surface with its radius, CYLINDER=r, and
    // G7.1 C0 off; a builder's codes, G112, G113 and G107 on the Nakamura, come from [transform] (controllers fanuc.md
    // 4; controller-mapping 1, CYLINDER and POLAR; language 4.2; D96, D102).
    private static void ReadTransformations(FanucBlock block)
    {
        TransformTable? transform = block.Machine.Transform;
        if (block.TakeCode("G12.1") || FanucTemplates.TryMatch(block, transform?.PolarOn, out _))
        {
            block.Draft.AddState("POLAR", null, s_on);
            block.Fanuc.Polar = true;
            block.Fanuc.Unknowns.Polar = false;
            block.Fanuc.ForgetPositions();
        }
        else if (block.TakeCode("G13.1") || FanucTemplates.TryMatch(block, transform?.PolarOff, out _))
        {
            block.Draft.AddState("POLAR", null, s_off);
            block.Fanuc.Polar = false;
            block.Fanuc.Unknowns.Polar = false;
            block.Fanuc.ForgetPositions();
        }

        Value? cylinder = null;
        if (FanucTemplates.TryMatch(block, transform?.CylinderOff, out _))
        {
            cylinder = s_off;
        }
        else if (FanucTemplates.TryMatch(block, transform?.CylinderOn, out TemplateValues values))
        {
            cylinder = values.TryGetNumber("r", out decimal radius) && radius != 0 ? FanucNumbers.Of(radius) : s_off;
        }
        else if (block.TakeCode("G7.1"))
        {
            SourceWord? radius = block.Take("C");
            cylinder = radius?.Number switch
            {
                null => null,
                0m => s_off,
                _ => radius.ToNcxNumber(),
            };
            if (cylinder is null)
            {
                block.Draft.KeepAsRaw("G7.1 names no radius C as a number");
                return;
            }
        }

        if (cylinder is not null)
        {
            block.Draft.AddState("CYLINDER", null, cylinder);
            block.Fanuc.ForgetPositions();
        }
    }

    // G5.1 Q1 switches AI contour control on and Q0 off; the tolerance itself sits in a parameter, so NCX TOLERANCE
    // reaches Fanuc only as the on and off switch (controllers fanuc.md 4; controller-mapping 1, TOLERANCE; D85).
    // G5.1 Q0 is TOLERANCE=OFF, and G5.1 Q1 stays RAW, since TOLERANCE takes a number or OFF (language 4.1) and the
    // [tolerance] ON of a Fanuc machine writes no {tol} (machine-config 5; controller-mapping 1, TOLERANCE: the value
    // stays RAW). The specification wins over the phase plan (implementation README).
    private static void ReadTolerance(FanucBlock block)
    {
        if (!block.TakeCode("G5.1"))
        {
            return;
        }

        if (block.Take("Q")?.Number == 0m)
        {
            block.Draft.AddState("TOLERANCE", null, s_off);
            return;
        }

        block.Draft.KeepAsRaw(
            "G5.1 Q1 switches AI contour control on with the tolerance of a parameter (controller-mapping 1, "
            + "TOLERANCE)");
    }

    // ORIGIN empties the chain of NCX, and a new frame makes the positions of the reader unknown (language 4.2,
    // virtual machine 3.4).
    private static void ClearChain(FanucBlock block)
    {
        block.Fanuc.Chain.Clear();
        block.Fanuc.ForgetPositions();
    }

    // The words of the active G52 shift, the axes that are not 0 in the order the G52 named them (D244).
    private static List<Word> LocalWords(FanucChain chain)
    {
        var words = new List<Word>();
        foreach (KeyValuePair<string, decimal> axis in chain.Local)
        {
            if (axis.Value != 0m)
            {
                words.Add(new Word { Key = axis.Key, Value = FanucNumbers.Of(axis.Value) });
            }
        }

        return words;
    }

    private static IntegerValue Integer(int number)
    {
        return new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture));
    }
}
