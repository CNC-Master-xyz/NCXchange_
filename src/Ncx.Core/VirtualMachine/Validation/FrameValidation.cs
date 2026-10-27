using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The frame of virtual machine 5 (language 4.1, 4.2; virtual machine 2.1, 3.4): SETPOS, the path tolerance, the
/// positioning options of a tilt and the blank of STOCK. MOVE and ROT without TILT or TILT_AXIS are the parser's (PAR016, language 5 rule 5).
/// </summary>
internal static class FrameValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Frame",
        Summary = "SETPOS, the path tolerance, the tilted plane and the blank (language 4.1, 4.2; VM 2.1, 3.4, 5). MOVE "
            + "and ROT without TILT or TILT_AXIS are PAR016 of the structure.",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.SetposAxisUnknown,
                "SETPOS with an axis unknown in every frame, except directly after a HOME of that axis that found no "
                + "reference point.", "VM 3.4, 5; D100, D101"),
            ValidationRule.Error(DiagnosticCodes.TiltAxisNotRotary,
                "A TILT_AXIS axis word on an axis the machine does not declare rotary.", "language 4.2; VM 3.4, 5; "
                + "D117"),
            ValidationRule.Error(DiagnosticCodes.ToleranceWordWithoutTolerance,
                "TOLERANCE:ROTARY or TOLERANCE_MODE without an active TOLERANCE, ROUND_LIMIT:ROTARY without an active "
                + "ROUND_LIMIT.", "language 4.1; VM 5; D85, D363"),
            ValidationRule.Error(DiagnosticCodes.SetposWithoutAxis, "SETPOS without an axis word.",
                "language 4.2; VM 5"),
            ValidationRule.Warning(DiagnosticCodes.RotWithoutTableKinematics,
                "ROT on a target without table kinematics: the machine file has no rotary axis on a table. Not checked "
                + "without a machine file.", "language 4.2; VM 5; D82"),
            ValidationRule.Error(DiagnosticCodes.StockWithoutItsCorners,
                "STOCK without all six coordinate words of its form, X Y Z and MAX:X MAX:Y MAX:Z.",
                "language 4.2; VM 5; D351"),
            ValidationRule.Error(DiagnosticCodes.StockExtentInvalid,
                "STOCK with a side of zero or negative extent, or STOCK=CYLINDER whose two sides across the tool axis "
                + "of the WORKPLANE differ, so that its footprint is not a circle.", "language 4.2; VM 5; D351"),
            ValidationRule.Error(DiagnosticCodes.StockMisplaced,
                "A second STOCK in one program, a STOCK before the first ORIGIN or one after the first motion verb.",
                "language 4.2; VM 5; D351"),
        ],
    };

    /// <summary>
    /// SETPOS declares the position of the axes it names (language 4.2); SETPOS without an axis word is an ERROR
    /// (virtual machine 5). An axis word of SETPOS names an axis: X Y Z A B C, their incremental forms, or a machine
    /// axis of the D93 form (language 5 rule 2).
    /// </summary>
    public static void CheckSetposNamesAnAxis(BlockContext context)
    {
        Block block = context.Block;
        if (block.Verb?.Key != "SETPOS")
        {
            return;
        }

        foreach (Word word in block.Words)
        {
            bool standardAxis = word.Definition is not null
                && word.Addr is null
                && WordCatalog.IsStandardAxis(word.Key);
            bool machineAxis = word.Definition is null && WordCatalog.TryMachineAxis(word.Key, out _);
            if (standardAxis || machineAxis)
            {
                return;
            }
        }

        context.Diagnostics.Error(block, DiagnosticCodes.SetposWithoutAxis,
            "SETPOS declares the position of the axes it names, SETPOS C=0, and this one names none (language 4.2, "
            + "virtual machine 5).");
    }

    /// <summary>
    /// TOLERANCE:ROTARY and TOLERANCE_MODE stand only with an active TOLERANCE (language 4.1): without one they are an
    /// ERROR of the modal state (virtual machine 5, D85). The state words of a block do not depend on each other
    /// (virtual machine 3 step 3), so the TOLERANCE of the same block makes it active and a TOLERANCE=OFF ends it.
    /// </summary>
    public static void CheckToleranceWords(BlockContext context)
    {
        CheckRoundLimitRotary(context);
        if (context.State.Frame.Tolerance.Value is not null)
        {
            return;
        }

        foreach (Word word in context.Block.Words)
        {
            bool rotaryTolerance = word.Key == "TOLERANCE" && word.Addr is not null;
            if (!rotaryTolerance && word.Key != "TOLERANCE_MODE")
            {
                continue;
            }

            context.Diagnostics.Error(context.Block, DiagnosticCodes.ToleranceWordWithoutTolerance,
                $"{word.ToCanonical()} stands only with an active TOLERANCE, and the path tolerance is OFF (language "
                + "4.1, virtual machine 5, D85).");
        }
    }

    // ROUND_LIMIT:ROTARY stands only with an active ROUND_LIMIT, the same ERROR as TOLERANCE:ROTARY without TOLERANCE
    // (language 4.1, virtual machine 5, D363); the ROUND_LIMIT of the same block makes it active.
    private static void CheckRoundLimitRotary(BlockContext context)
    {
        if (context.State.Frame.RoundLimit.Value is not null
            || context.Block.Find("ROUND_LIMIT", "ROTARY") is not Word rotary)
        {
            return;
        }

        context.Diagnostics.Error(context.Block, DiagnosticCodes.ToleranceWordWithoutTolerance,
            $"{rotary.ToCanonical()} stands only with an active ROUND_LIMIT, and the rounding allowance is OFF "
            + "(language 4.1, virtual machine 5, D363).");
    }

    /// <summary>
    /// ROT chooses, on table kinematics, whether the table turns or only the coordinate system; a target that has no
    /// such option ignores it with a WARNING (language 4.2, virtual machine 5, D82). The built-in default machine of
    /// D103 is no target, because nothing is compiled for it (D77), so without a machine file ROT is not checked.
    /// </summary>
    // TODO(question): virtual machine 5 warns for "ROT on a target without table kinematics" without saying what table
    // kinematics is in a machine file, nor whether it is checked without one; here it is a rotary [[axis]] whose owner
    // is a [[resource]] of type table (machine-config 4), and a run without a machine file does not check it, until
    // D191 is answered.
    public static void CheckRot(Block block, MachineConfig machine, Diagnostics diagnostics)
    {
        if (block.Find("ROT") is not Word rot || machine.Machine.Controller is null || HasRotaryTable(machine))
        {
            return;
        }

        diagnostics.Warning(block, DiagnosticCodes.RotWithoutTableKinematics,
            $"{rot.ToCanonical()} chooses how a rotary table reaches the plane, and the machine has no rotary axis on "
            + "a table: the option is ignored (language 4.2, virtual machine 5, D82).");
    }

    // A rotary axis of the machine owned by a table (machine-config 4: [[axis]] owner, [[resource]] type).
    private static bool HasRotaryTable(MachineConfig machine)
    {
        foreach (AxisDef axis in machine.Axes)
        {
            if (axis.Kind == AxisKind.Rotary
                && axis.Owner is string owner
                && machine.FindResource(owner)?.Type == ResourceType.Table)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// STOCK gives the blank by its bounding box, X Y Z one corner and MAX:X MAX:Y MAX:Z the opposite one, and stands
    /// once per program, after the first ORIGIN and before the first motion verb (language 4.2, virtual machine 5,
    /// D351). A coordinate from an expression, which STATIC mode does not evaluate, is not compared (virtual machine 1).
    /// Called after the verb of every block, so the ORIGIN of the STOCK block counts and its motion does not.
    /// </summary>
    public static void CheckStock(BlockContext context, StockPlacement placement)
    {
        Block block = context.Block;
        if (block.Has("PROGRAM", null, "BEGIN"))
        {
            placement.StartProgram();
        }

        if (block.Has("ORIGIN"))
        {
            placement.OriginSeen = true;
        }

        if (block.Verb is Word stock && stock.Key == "STOCK")
        {
            CheckStockPlace(context, placement);
            CheckStockBox(context, stock);
            placement.StockSeen = true;
        }

        if (MotionRules.IsMotion(block))
        {
            placement.MotionSeen = true;
        }
    }

    // VM425: once per program, after the first ORIGIN, before the first motion verb (D351).
    private static void CheckStockPlace(BlockContext context, StockPlacement placement)
    {
        string? misplaced = placement.StockSeen ? "is the second STOCK of the program"
            : !placement.OriginSeen ? "stands before the first ORIGIN"
            : placement.MotionSeen ? "stands after the first motion verb"
            : null;
        if (misplaced is not null)
        {
            context.Diagnostics.Error(context.Block, DiagnosticCodes.StockMisplaced,
                $"This STOCK {misplaced}; the blank stands once per program, after the first ORIGIN and before the "
                + "first motion verb (language 4.2, virtual machine 5, D351).");
        }
    }

    // VM423 for a missing coordinate word, VM424 for a side of no extent or a cylinder that is no circle about the
    // tool axis of the active WORKPLANE (D351).
    private static void CheckStockBox(BlockContext context, Word stock)
    {
        Block block = context.Block;
        var missing = new List<string>();
        var extent = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        foreach (string axis in new[] { "X", "Y", "Z" })
        {
            Word? corner = block.Find(axis);
            Word? opposite = block.Find("MAX", axis);
            if (corner is null)
            {
                missing.Add(axis);
            }

            if (opposite is null)
            {
                missing.Add("MAX:" + axis);
            }

            extent[axis] = NumberOf(corner) is decimal low && NumberOf(opposite) is decimal high ? high - low : null;
        }

        if (missing.Count > 0)
        {
            context.Diagnostics.Error(block, DiagnosticCodes.StockWithoutItsCorners,
                $"{stock.ToCanonical()} lacks {string.Join(", ", missing)}; the blank is given by the six coordinate "
                + "words of its bounding box, X Y Z and MAX:X MAX:Y MAX:Z (language 4.2, virtual machine 5, D351).");
            return;
        }

        foreach (KeyValuePair<string, decimal?> side in extent)
        {
            if (side.Value is decimal length && length <= 0m)
            {
                context.Diagnostics.Error(block, DiagnosticCodes.StockExtentInvalid,
                    $"{stock.ToCanonical()} has no extent along {side.Key}: MAX:{side.Key} must lie beyond {side.Key} "
                    + "(language 4.2, virtual machine 5, D351).");
                return;
            }
        }

        string[] plane = AcrossTheToolAxis(context.State.Frame.Workplane);
        string first = plane[0];
        string second = plane[1];
        if (BlockContext.IdentOf(stock) == "CYLINDER"
            && extent[first] is decimal across && extent[second] is decimal along && across != along)
        {
            context.Diagnostics.Error(block, DiagnosticCodes.StockExtentInvalid,
                $"{stock.ToCanonical()} is {across} along {first} and {along} along {second}, so its footprint across "
                + "the tool axis is no circle; the cylinder is inscribed in a box with two equal sides about it "
                + "(language 4.2, virtual machine 5, D351).");
        }
    }

    // The two axes of the working plane, across the tool axis (language 4.2, WORKPLANE).
    private static string[] AcrossTheToolAxis(Workplane workplane)
    {
        return workplane switch
        {
            Workplane.ZX => ["Z", "X"],
            Workplane.YZ => ["Y", "Z"],
            _ => ["X", "Y"],
        };
    }

    // A literal number; null for an expression, which STATIC mode does not evaluate, and for a missing word.
    private static decimal? NumberOf(Word? word)
    {
        return word?.Value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue value => value.Number,
            _ => null,
        };
    }
}
