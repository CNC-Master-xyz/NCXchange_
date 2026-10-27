using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The units of a Fanuc program (D283; controller-mapping 1, UNITS): G20 or G21, G70 or G71 in G-code system C, in a
/// block of their own at the start of every program, before any coordinate-system or offset setup, whatever
/// units_default says; units_default is the reader's assumption only (D11, D34). A change of the units after the first
/// motion is the ERROR CMP313, since the control forbids the switch inside a running program.
/// </summary>
internal static class FanucUnits
{
    /// <summary>
    /// Writes the units of the program in a block of their own: the units the program has at its first motion, or at
    /// its end where it has none, so that a UNITS of the header or before the first motion is the one written here.
    /// </summary>
    /// <param name="write">The PROGRAM=BEGIN block.</param>
    public static void WriteStart(FanucBlock write)
    {
        if (FanucCodes.UnitsCode(UnitsOfTheProgram(write), write.System) is string code)
        {
            write.Write(code);
            write.Target.Set(FanucCodes.Units, code);
        }
    }

    /// <summary>
    /// A UNITS word writes nothing: before the first motion of a program its units stand in the block of the start,
    /// after it a change is the ERROR CMP313, as it is in a subprogram, which runs inside its caller (D283).
    /// </summary>
    /// <param name="write">The block.</param>
    public static void Check(FanucBlock write)
    {
        if (!write.Block.Has("UNITS"))
        {
            return;
        }

        write.Written("UNITS");
        if (FanucCodes.UnitsCode(write.After.Frame.Units, write.System) is not string code)
        {
            return;
        }

        bool changes = write.Target.ActiveOf(FanucCodes.Units) is string active && active != code;
        if (changes && (write.Step.Section?.Kind != SectionKind.Program || MovedBefore(write)))
        {
            write.Error(DiagnosticCodes.FanucUnitsChangeInProgram,
                $"UNITS={write.Block.Find("UNITS")?.Value.ToCanonical()} changes the units after the first motion, "
                + "which a Fanuc control forbids inside a running program; the units stand in a block of their own "
                + "at the start of the program (D283; controller-mapping 1, UNITS).");
        }

        // The control keeps the units of the start, so FanucMotion writes no G20 or G21 for the word.
        write.Target.Set(FanucCodes.Units, code);
    }

    // The units of the program from the PROGRAM=BEGIN block on: those of its first motion, a motion of a subprogram it
    // calls first among them, or those after its last block.
    private static Units UnitsOfTheProgram(FanucBlock write)
    {
        Units units = write.After.Frame.Units;
        bool started = false;
        foreach (BlockStep step in write.Steps)
        {
            if (ReferenceEquals(step, write.Step))
            {
                started = true;
                continue;
            }

            if (!started)
            {
                continue;
            }

            if (step.Section?.Kind == SectionKind.Program && step.Section != write.Step.Section)
            {
                break;
            }

            units = step.After.Frame.Units;
            if (step.IsMotion)
            {
                break;
            }
        }

        return units;
    }

    // True where a motion ran in the program before the block, in it or in a subprogram it called.
    private static bool MovedBefore(FanucBlock write)
    {
        bool moved = false;
        foreach (BlockStep step in write.Steps)
        {
            if (ReferenceEquals(step, write.Step))
            {
                return moved;
            }

            if (step.Section == write.Step.Section && step.Block.Has("PROGRAM", null, "BEGIN"))
            {
                moved = false;
            }

            moved |= step.IsMotion;
        }

        return moved;
    }
}
