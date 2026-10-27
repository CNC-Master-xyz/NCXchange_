using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The subprograms of Klartext (controllers heidenhain.md 1; 8 rule 6; controller-mapping 1 and 6; language 4.9, 4.13):
/// a SUB section as LBL n ... LBL 0, which the framework places after the M30 of every program that calls it
/// (program_layout = "file_per_program", D48, D99); CALL=n as CALL LBL n, CALL="name" of another file as CALL PGM
/// name with the ARG values set in Q parameters before it.
/// </summary>
internal static class HeidenhainSubprograms
{
    // D274: every SUB is an LBL section after the M30 of each caller; the option of separate .h files with CALL PGM is
    // struck from heidenhain 8 rule 6 for 1.0 (decisions.md D274).

    /// <summary>
    /// Writes SUB=BEGIN NAME=n as LBL n (heidenhain 8 rule 6).
    /// </summary>
    public static void WriteBegin(HeidenhainBlock writing)
    {
        writing.Take("SUB");
        if (writing.Take("NAME") is Word name)
        {
            writing.Line("LBL " + (writing.LabelNumbers.SubOf(name.Value) ?? HeidenhainFlow.Label(name.Value)));
        }
    }

    /// <summary>
    /// Writes CALL: the ARG values first, then CALL LBL n for a subprogram of the file or CALL PGM name for another
    /// file, as often as TIMES says, since Klartext has no count on CALL LBL (controller-mapping 6, CALL and TIMES;
    /// virtual machine 3.9: a CALL with TIMES=n runs n times in sequence).
    /// </summary>
    public static void WriteCall(HeidenhainBlock writing)
    {
        // A CALL under IF has no Klartext form and stays unwritten, which is reported (CMP101).
        if (writing.Block.Find("CALL") is not Word call || writing.Block.Has("IF"))
        {
            return;
        }

        writing.MarkWritten(call);
        if (!WriteArguments(writing))
        {
            return;
        }

        int passes = 1;
        if (writing.Take("TIMES") is Word times)
        {
            if (times.Value is not IntegerValue count)
            {
                HeidenhainNumbers.ReportValue(writing, times);
                return;
            }

            passes = (int)count.Number;
        }

        CheckReadsAfter(writing);
        string line = CallLine(writing, call.Value);
        for (int pass = 0; pass < passes; pass++)
        {
            writing.Line(line);
        }
    }

    // CALL LBL n for a subprogram of the file, CALL PGM name for an external program (language 4.9, CALL).
    private static string CallLine(HeidenhainBlock writing, Value target)
    {
        bool external = target is StringValue name && !writing.After.Flow.Subs.ContainsKey(name.Content);
        return external
            ? "CALL PGM " + ((StringValue)target).Content
            : "CALL LBL " + (writing.LabelNumbers.SubOf(target) ?? HeidenhainFlow.Label(target));
    }

    // CALL PGM name with Q parameters set before the call (controller-mapping 6, CALL + ARG): ARG:Q1=5 as Q1 = 5.
    private static bool WriteArguments(HeidenhainBlock writing)
    {
        foreach (Word argument in writing.TakeAll("ARG"))
        {
            if (argument.Addr is not string name || !HeidenhainFormula.IsParameter(name))
            {
                writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                    $"{argument.ToCanonical()} names no Q parameter; Klartext hands a called program its values in Q "
                    + "parameters set before the call (controller-mapping 6, CALL + ARG).");
                return false;
            }

            if (HeidenhainFlow.FormulaOf(writing, argument) is not string value)
            {
                return false;
            }

            writing.Line(name + " = " + value);
        }

        return true;
    }

    // D149: every name an ARG of a CALL assigns is a local of the call in NCX, so the caller reads its own value again
    // after the call; Klartext sets the Q parameter before the call, and Q parameters survive CALL PGM and CALL LBL on
    // the control (D274). A block of the caller's section after the call that reads such a name before assigning it
    // is the ERROR CMP125 on the CALL (decisions.md D149; language 4.9).
    private static void CheckReadsAfter(HeidenhainBlock writing)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        Section? section = writing.Step.Section;
        foreach (Word argument in writing.Block.Words)
        {
            if (argument.Key != "ARG" || argument.Addr is not string name)
            {
                continue;
            }

            for (int index = writing.Step.Index + 1; index < steps.Count; index++)
            {
                Block block = steps[index].Block;
                if (steps[index].Section != section)
                {
                    continue;
                }

                if (HeidenhainReads.NamesOf(block).Contains(name))
                {
                    writing.Error(DiagnosticCodes.HeidenhainArgumentReadAfterCall,
                        $"{argument.ToCanonical()}: the block on line {block.Line} reads {name} after the call, where "
                        + $"NCX gives the caller its own value again, while Klartext sets {name} before the call and "
                        + "the control keeps the value the call leaves (language 4.9; D149).");
                    break;
                }

                bool assigns = block.Find("VAR", name) is not null || block.Find("ARG", name) is not null;
                if (assigns || block.Has("PROGRAM", null, "END") || block.Has("SUB", null, "END"))
                {
                    break;
                }
            }
        }
    }
}
