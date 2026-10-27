using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The frame of virtual machine 5 (language 4.1, 4.2; virtual machine 2.1, 3.4): SETPOS, the path tolerance, the
/// positioning options of a tilt and the blank of STOCK. MOVE and ROT without TILT or TILT_AXIS are the parser's
/// (PAR016, language 5 rule 5).
/// </summary>
internal static class FrameValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Frame",
        Summary = "SETPOS, the path tolerance, the tilted plane and the blank (language 4.1, 4.2; VM 2.1, 3.4, 5). "
            + "MOVE and ROT without TILT or TILT_AXIS are PAR016 of the structure.",
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
                "ROT on a machine file without any rotary axis, whatever the owner: no free rotary axis can arise. Not "
                + "checked without a machine file.", "language 4.2; VM 5; D82, D191"),
            ValidationRule.Error(DiagnosticCodes.StockWithoutItsCorners,
                "STOCK without all six coordinate words of its form, X Y Z and MAX:X MAX:Y MAX:Z.",
                "language 4.2; VM 5; D351"),
            ValidationRule.Error(DiagnosticCodes.StockExtentInvalid,
                "STOCK with a side of zero or negative extent, or STOCK=CYLINDER whose two sides across the tool axis "
                + "of the WORKPLANE differ, so that its footprint is not a circle.", "language 4.2; VM 5; D351"),
            ValidationRule.Error(DiagnosticCodes.StockMisplaced,
                "A second STOCK in one program, a STOCK before the first ORIGIN or one after the first motion verb.",
                "language 4.2; VM 5; D351"),
            ValidationRule.Warning(DiagnosticCodes.RotWithTiltAxis,
                "ROT written with TILT_AXIS, on any target and without a machine file too: the control ignores the "
                + "transformation type there.", "language 4.2; VM 5; D191"),
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
    /// ROT chooses whether the table turns or only the coordinate system, and takes effect only through a free rotary
    /// axis, one parallel to the tool axis in the swivel situation, whatever owns it (D191). With a machine file, a
    /// machine without any rotary [[axis]] can never have one, which is the WARNING VM422; whether a given tilt frees
    /// an axis depends on the spatial angles and the kinematics and is the kinematics module's (D24). Without a machine
    /// file the machine side is not checked: the built-in default machine of D103 is no target (D77). ROT written with
    /// TILT_AXIS is the WARNING VM426 on any target, with or without a machine file, because the control ignores the
    /// transformation type there, and then VM422 is not raised besides it. The family side, Fanuc and Siemens without
    /// ROT, stays with the compilers (controller-mapping 1).
    /// Heidenhain_TNC640_Programmierung-Klartext_892903-18_NC-SW-34059x-10_DE_2019, connector p. 435-436 (a free
    /// rotary axis decides, table or head does not matter; no effect with PLANE AXIAL).
    /// </summary>
    public static void CheckRot(Block block, MachineConfig machine, Diagnostics diagnostics)
    {
        if (block.Find("ROT") is not Word rot)
        {
            return;
        }

        if (block.Verb?.Key == "TILT_AXIS")
        {
            diagnostics.Warning(block, DiagnosticCodes.RotWithTiltAxis,
                $"{rot.ToCanonical()} with TILT_AXIS is ignored: the transformation type has no effect on a plane "
                + "given by the rotary axis positions (language 4.2, virtual machine 5, D191).");
            return;
        }

        if (machine.Machine.Controller is null || HasRotaryAxis(machine))
        {
            return;
        }

        diagnostics.Warning(block, DiagnosticCodes.RotWithoutTableKinematics,
            $"{rot.ToCanonical()} chooses how a free rotary axis reaches the plane, and the machine has no rotary axis "
            + "at all: the option is ignored (language 4.2, virtual machine 5, D191).");
    }

    // A rotary axis of the machine, whatever its owner: table, work spindle or head (machine-config 4, D191).
    private static bool HasRotaryAxis(MachineConfig machine)
    {
        foreach (AxisDef axis in machine.Axes)
        {
            if (axis.Kind == AxisKind.Rotary)
            {
                return true;
            }
        }

        return false;
    }
}
