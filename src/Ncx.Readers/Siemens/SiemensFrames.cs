using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The frames of a SINUMERIK block (controllers siemens.md 3, 4, 11 rule 4; controller-mapping 1; language 4.2; D31):
/// G54 to G57 and G505 to G599 as ORIGIN 1 to 99, G500 as ORIGIN=0; TRANS, ROT, MIRROR and SCALE cut the chain at its
/// first entry and then append, ATRANS, AROT, AMIRROR append; ROT RPL= and a rotation about the tool axis as ROTATE, a
/// rotation about another axis as TILT; G58 and G59 set the absolute and the additive part of the shift; G74 as HOME,
/// G75 FP= as HOME POINT=; PRESETON as SETPOS. The tilt of CYCLE800 and ROTS is SiemensTilt, G53, G153 and SUPA the
/// FRAME=MACHINE of SiemensMotion.
/// </summary>
internal static class SiemensFrames
{
    // The programmable frame instructions: the replacing ones delete the whole programmable frame before they act
    // (controllers siemens.md 4).
    private static readonly string[] s_instructions = ["TRANS", "ATRANS", "ROT", "AROT", "MIRROR", "AMIRROR", "SCALE",
        "ASCALE"];

    /// <summary>
    /// Reads the frame words of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadOrigin(block);
        ReadInstruction(block);
        ReadPartShift(block);
        ReadHome(block);
        ReadPreset(block);
    }

    // G54 to G57 are the datums 1 to 4, G505 to G599 5 and up, G500 cancels them (controller-mapping 1, ORIGIN); ORIGIN
    // starts an empty chain (language 4.2). The settable frame and the programmable frame are separate components of
    // the total frame on the control, so a datum of group 8 leaves TRANS, ROT, MIRROR and SCALE standing: the reader
    // writes the entries of the programmable frame again after the ORIGIN that empties the chain (D340).
    // TODO(question): D340 (3) settles the programmable frame of group 3 alone, and the swivel of CYCLE800 belongs to
    // none of its instructions; whether a datum of group 8 leaves that swivel standing is the open D333. A tilt of
    // CYCLE800 in the chain is therefore not written again, as it was not before D340, so a datum writes no second
    // swivel motion into the converted program.
    private static void ReadOrigin(SiemensBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            string? code = SiemensBlock.CodeOf(word);
            if (code is null || SiemensGroups.GroupOf(code) != SiemensGroups.Datum)
            {
                continue;
            }

            block.MarkRead(word);
            int number = int.Parse(code.AsSpan(1), CultureInfo.InvariantCulture);
            int datum = number switch
            {
                500 => 0,
                >= 505 => number - 500,
                _ => number - 53,
            };
            SiemensChain chain = block.Facts.Chain;
            var programmable = new List<SiemensChainEntry>();
            if (chain.Known)
            {
                foreach (SiemensChainEntry entry in chain.Entries)
                {
                    if (entry.Instruction != "CYCLE800")
                    {
                        programmable.Add(entry);
                    }
                }
            }

            bool known = chain.Known;
            block.Draft.AddState("ORIGIN", null, new IntegerValue(datum, datum.ToString(CultureInfo.InvariantCulture)));
            chain.Clear();
            var again = new List<SiemensDraftBlock>();
            foreach (SiemensChainEntry entry in programmable)
            {
                chain.Append(entry with { Block = entry.Block.Copy() }, again);
            }

            if (!known)
            {
                chain.Forget();
            }

            block.Draft.After.AddRange(again);
            block.Facts.Unknown.Remove(SiemensFacts.Frames);
            block.Siemens.ForgetPositions();
        }
    }

    // TRANS X Y Z, ROT, MIRROR X0, SCALE: the replacing instructions delete every earlier programmable frame
    // instruction, so the reader cuts the chain at its first entry and appends the new word; ATRANS, AROT, AMIRROR
    // build on the current frame and are appended as written (controllers siemens.md 4; controller-mapping 1, SHIFT,
    // ROTATE, MIRROR; language 4.2, D31).
    private static void ReadInstruction(SiemensBlock block)
    {
        SourceWord? instruction = null;
        foreach (SourceWord word in block.Unread())
        {
            if (word.Text.Length == 0 && Array.IndexOf(s_instructions, word.Address) >= 0)
            {
                instruction = word;
                break;
            }
        }

        if (instruction is null)
        {
            return;
        }

        block.MarkRead(instruction);
        string name = instruction.Address;
        if (name == "ASCALE")
        {
            block.MarkAllRead();
            block.Draft.KeepAsRaw("ASCALE scales the frame, which NCX has no word for (controllers siemens.md 4)");
            return;
        }

        List<SourceWord> axes = FrameWords(block);
        SiemensChain chain = block.Facts.Chain;
        var blocks = new List<SiemensDraftBlock>();
        bool replacing = name is "TRANS" or "ROT" or "MIRROR" or "SCALE";
        if (replacing && !chain.TryCut(blocks))
        {
            block.MarkAllRead();
            block.Draft.KeepAsRaw($"{name} deletes the programmable frame, and an entry of it stands before a tilt "
                + "of CYCLE800, or the chain is not known here (controllers siemens.md 4; virtual machine 3.9; D333)");
            return;
        }

        SiemensChainEntry? entry = name switch
        {
            "TRANS" or "ATRANS" => Shift(block, name, axes),
            "ROT" or "AROT" => Rotation(block, name, axes),
            "MIRROR" or "AMIRROR" => Mirror(block, name, axes),
            _ => Scale(block, instruction, axes),
        };
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (entry is not null)
        {
            chain.Append(entry, blocks);
        }

        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // TRANS X10 Y20 is SHIFT X=10 Y=20, an axis left out is 0 (language 4.2); TRANS alone clears the programmable
    // frame and appends nothing.
    private static SiemensChainEntry? Shift(SiemensBlock block, string name, List<SourceWord> axes)
    {
        if (axes.Count == 0)
        {
            return null;
        }

        var shift = new SiemensDraftBlock().WithVerb("SHIFT");
        foreach (SourceWord word in axes)
        {
            if (Value(block, word) is not Value value)
            {
                return null;
            }

            string axis = SiemensAxes.AxisOf(block, word)!;
            shift.Add(axis, ShiftValue(block, axis, value));
        }

        return new SiemensChainEntry("SHIFT", name, shift);
    }

    // The value of a shift in NCX. A Siemens facing axis keeps its frame offsets as radii, MD20360 bit 3 off
    // (Funktionshandbuch Grundfunktionen 2012 p.901-903, cnc-manuals), while NCX reads SHIFT X under DIAMETER=ON as a
    // diameter, so the reader doubles the X of a shift under DIAMON and DIAM90; where [[axis]] frames_in_diameter
    // says the control keeps them as diameters, X is taken as written (D125, D388).
    private static Value ShiftValue(SiemensBlock block, string axis, Value value)
    {
        return axis == "X" && XInRadius(block) ? SiemensMotion.Doubled(block, value) : value;
    }

    /// <summary>
    /// True where the control keeps the frame offsets and the machine position of X as radii while NCX reads X as a
    /// diameter: under DIAMON or DIAM90, on an X axis whose programming is diameter or switchable and which does not
    /// set frames_in_diameter (D125, D240, D388).
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static bool XInRadius(SiemensBlock block)
    {
        return block.Facts.DiameterMode is "DIAMON" or "DIAM90" && block.Machine.ResolveAxis("X") is
        {
            Programming: Programming.Diameter or Programming.Switchable,
            FramesInDiameter: false,
        };
    }

    // ROT RPL=30 and ROT Z30 under G17 turn about the tool axis, ROTATE=30; ROT X180 turns about another axis, the
    // spatial angle of TILT about it; ROT with several axes is a rotation in Euler order that the reader keeps as RAW
    // (controller-mapping 1, ROTATE; language 4.2).
    private static SiemensChainEntry? Rotation(SiemensBlock block, string name, List<SourceWord> axes)
    {
        SourceWord? plane = block.Take("RPL");
        var tool = SiemensPlane.Of(block.Facts.WorkingPlane).Tool;
        if (plane is null && axes.Count == 0)
        {
            return null;
        }

        if ((plane is not null && axes.Count > 0) || axes.Count > 1)
        {
            block.Draft.KeepAsRaw($"{name} turns about several axes in Euler order, which the reader keeps as RAW "
                + "(controllers siemens.md 4)");
            return null;
        }

        SourceWord angleWord = plane ?? axes[0];
        if (Value(block, angleWord) is not Value angle)
        {
            return null;
        }

        string axis = plane is null ? SiemensAxes.AxisOf(block, angleWord)! : tool;
        if (axis == tool)
        {
            return new SiemensChainEntry("ROTATE", name, new SiemensDraftBlock().Add("ROTATE", angle));
        }

        string spatial = axis switch
        {
            "X" => "A",
            "Y" => "B",
            _ => "C",
        };
        var tilt = new SiemensDraftBlock().WithVerb("TILT");
        foreach (string key in new[] { "A", "B", "C" })
        {
            tilt.Add(key, key == spatial ? angle : new IntegerValue(0, "0"));
        }

        return new SiemensChainEntry("TILT", name, tilt);
    }

    // MIRROR X0 mirrors the named axes, the value is meaningless; MIRROR alone clears (controller-mapping 1, MIRROR).
    private static SiemensChainEntry? Mirror(SiemensBlock block, string name, List<SourceWord> axes)
    {
        if (axes.Count == 0)
        {
            return null;
        }

        var names = new List<string>();
        foreach (SourceWord word in axes)
        {
            block.MarkRead(word);
            names.Add(SiemensAxes.AxisOf(block, word)!);
        }

        Value value = names.Count == 1 ? new IdentValue(names[0]) : new ListValue(names);
        return new SiemensChainEntry("MIRROR", name, new SiemensDraftBlock().Add("MIRROR", value));
    }

    // SCALE with factors has no NCX word; after the cut of the chain the factors stay a RAW word of the block, SCALE
    // alone only clears (controllers siemens.md 4).
    private static SiemensChainEntry? Scale(SiemensBlock block, SourceWord instruction, List<SourceWord> axes)
    {
        if (axes.Count == 0)
        {
            return null;
        }

        block.Draft.RawWords.Add(block.SpanOf(instruction));
        foreach (SourceWord word in axes)
        {
            block.KeepAsRawWord(word);
        }

        // The scale stays in the frame of the control, where only the RAW word says so, and the next instruction that
        // deletes the frame deletes it too: the reader no longer knows the chain (D5).
        block.Facts.MakeUnknown(SiemensFacts.Frames);
        return null;
    }

    // G58 X.. replaces the absolute part of the programmable shift per axis, G59 X.. the additive part (controllers
    // siemens.md 4; controller-mapping 1, ORIGIN): the reader replaces the shift of TRANS or of ATRANS where it is the
    // last entry of the chain, or appends one to an empty chain, and G59 appends one after the shift of TRANS.
    private static void ReadPartShift(SiemensBlock block)
    {
        bool absolute = block.TakeCode("G58");
        bool additive = !absolute && block.TakeCode("G59");
        if (!absolute && !additive)
        {
            return;
        }

        SiemensChain chain = block.Facts.Chain;
        SiemensChainEntry? old = chain.LastOf(absolute ? "TRANS" : "ATRANS");
        var shift = new SiemensDraftBlock().WithVerb("SHIFT");
        if (old is not null)
        {
            shift.Words.AddRange(old.Block.Words);
        }

        foreach (SourceWord word in FrameWords(block))
        {
            if (Value(block, word) is not Value value)
            {
                return;
            }

            string axis = SiemensAxes.AxisOf(block, word)!;
            shift.Remove(axis);
            shift.Add(axis, ShiftValue(block, axis, value));
        }

        // A G59 without an additive shift before it appends one where the shift of TRANS or G58 is the last entry.
        var blocks = new List<SiemensDraftBlock>();
        bool fits = old is not null || chain.Entries.Count == 0
            || (additive && chain.Entries[^1] is { Kind: "SHIFT", Instruction: "TRANS" });
        if (!fits || !chain.TryReplace(old, new SiemensChainEntry("SHIFT", absolute ? "TRANS" : "ATRANS", shift),
            blocks))
        {
            block.MarkAllRead();
            block.Draft.KeepAsRaw((absolute ? "G58" : "G59") + " sets a part of a shift that is not the last entry of "
                + "the chain of transforms, or one the reader does not know (language 4.2, D31)");
            return;
        }

        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // G74 X1=0 Y1=0 Z1=0 returns the axes to the reference point by their machine names, HOME X Y Z, the counterpart
    // of Fanuc G28; G75 X0 Z0 FP=2 to FP=4 approaches the fixed point of MD30600 $MA_FIX_POINT_POS, HOME X Z POINT=2,
    // the counterpart of Fanuc G30 P2 to P4 (D260; controllers siemens.md 3; controller-mapping 1, HOME). G75 FP=1,
    // and G75 without FP, which the control reads as FP=1, stay RAW with the WARNING RDR509: POINT=1 is the reference
    // point G74 approaches, so NCX has no POINT for fixed point 1 (840D sl/828D Programmierhandbuch Grundlagen 2013
    // p.377-378, cnc-manuals; D260).
    private static void ReadHome(SiemensBlock block)
    {
        bool reference = block.TakeCode("G74");
        bool fixedPoint = !reference && block.TakeCode("G75");
        if (!reference && !fixedPoint)
        {
            return;
        }

        SiemensDraftBlock home = block.Draft.Main.WithVerb("HOME");
        foreach (SourceWord word in block.Unread())
        {
            string? axis = word.Text.Length > 0 && !word.Text.StartsWith('(')
                ? SiemensAxes.MachineAxisOf(block.Machine, word.Address)
                : null;
            if (axis is null)
            {
                continue;
            }

            block.MarkRead(word);
            home.Add(axis, NoValue.Instance);
            block.State.ForgetPosition(axis);
        }

        if (fixedPoint)
        {
            SourceWord? point = block.Take("FP");
            int? number = point is null ? 1 : SiemensNumbers.WholeNumber(point.Text);
            if (number is 0 or 1)
            {
                block.Draft.ReadingWarnings.Add(new SiemensWarning(DiagnosticCodes.SiemensFixedPoint1HasNoPoint,
                    "G75 approaches fixed point 1, which NCX has no POINT for, since POINT=1 is the reference point "
                    + "that G74 approaches; the block is kept as RAW (D260, D5)."));
                block.Draft.KeepAsRaw("G75 approaches fixed point 1, which NCX has no POINT for (D260)");
                return;
            }

            if (number is not int fixedNumber || fixedNumber is < 2 or > 4)
            {
                block.Draft.KeepAsRaw("FP of G75 names no fixed point 1 to 4 (controllers siemens.md 3)");
                return;
            }

            home.Add("POINT", new IntegerValue(fixedNumber, fixedNumber.ToString(CultureInfo.InvariantCulture)));
        }

        block.Facts.Tangent = null;
    }

    // PRESETON(C, 0) declares the current position of an axis, SETPOS C=0; PRESETONS keeps the reference status and
    // reads the same (controller-mapping 1, SETPOS; D55).
    private static void ReadPreset(SiemensBlock block)
    {
        SourceWord? preset = block.Take("PRESETON") ?? block.Take("PRESETONS");
        if (preset is null)
        {
            return;
        }

        List<string> arguments = SiemensArguments.Split(preset.Text);
        if (arguments.Count == 0 || arguments.Count % 2 != 0)
        {
            block.Draft.KeepAsRaw($"{preset.Address} gives no pairs of axis and position (controllers siemens.md 3)");
            return;
        }

        SiemensDraftBlock setpos = block.Draft.Main.WithVerb("SETPOS");
        for (int index = 0; index < arguments.Count; index += 2)
        {
            string? axis = SiemensAxes.MachineAxisOf(block.Machine, arguments[index].Trim().ToUpperInvariant());
            Value? value = SiemensExpression.ValueOf(block, arguments[index + 1], out string? problem);
            if (axis is null || value is null)
            {
                block.Draft.KeepAsRaw($"{preset.Address} names an axis or a position NCX cannot write: {problem}");
                return;
            }

            setpos.Add(axis, value);
            SiemensMotion.Move(block, axis, value, incremental: false);
        }
    }

    // The axis words of a frame block, read; a value of AC() or IC() is none a frame takes.
    private static List<SourceWord> FrameWords(SiemensBlock block)
    {
        var words = new List<SourceWord>();
        foreach (SourceWord word in block.Unread())
        {
            if (word.Text.Length > 0 && !word.Text.StartsWith('(') && SiemensAxes.AxisOf(block, word) is not null)
            {
                block.MarkRead(word);
                words.Add(word);
            }
        }

        return words;
    }

    private static Value? Value(SiemensBlock block, SourceWord word)
    {
        block.MarkRead(word);
        Value? value = SiemensMotion.Inner(word.Text, out _) is null
            ? SiemensExpression.ValueOf(block, word.Text, out string? problem)
            : null;
        if (value is null)
        {
            block.Draft.KeepAsRaw($"{word.Address}={word.Text} is no value a frame takes (language 4.2)");
        }

        return value;
    }
}
