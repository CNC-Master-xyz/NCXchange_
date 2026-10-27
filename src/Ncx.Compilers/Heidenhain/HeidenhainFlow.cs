using System.Globalization;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// Labels, jumps, repeats and variables in Klartext (controllers heidenhain.md 1, 6; controller-mapping 1 and 6;
/// language 4.9): LABEL=n as LBL n, JUMP with IF as FN 9 to FN 12 IF a comparison b GOTO LBL n, JUMP without IF as
/// the jump whose condition always holds, JUMP=END to an LBL before the M30, RETURN as the sub_end LBL 0 in a
/// subprogram and as JUMP=END in a program, REPEAT=n TIMES=k as CALL LBL n REP k, and VAR:Qn as the formula Qn = ...
/// of the Q parameter.
/// </summary>
internal static class HeidenhainFlow
{
    // The jump whose condition always holds, FN 9 with two equal values (controllers heidenhain.md 6).
    private const string AlwaysTrue = "FN 9: IF +0 EQU +0";

    // What the target state keeps at a label: the tool axis of the last TOOL CALL and the compensation the control has
    // on the way the text runs, which no word after the label can write again and which the jumps check
    // (HeidenhainArrivals), the compensation and the feed written last, which the label marks itself
    // (HeidenhainArrivals.EnterLabel), and the place of the cycle 7 of SETPOS in the chain of transforms, for which the
    // chain words after the label are written and which the jumps check with the chain (HeidenhainChainArrivals).
    private static readonly string[] s_keptAtLabels =
    [
        HeidenhainToolCall.PlaneKey, HeidenhainCompensation.ActiveKey, HeidenhainCompensation.WrittenKey,
        HeidenhainMotion.FeedKey, HeidenhainSetpos.PlaceKey,
    ];

    /// <summary>
    /// A label or subprogram name as a message names it: the number, 5, or the name in quotes, "DRILL_ROW".
    /// </summary>
    public static string Label(Value value)
    {
        return value switch
        {
            IntegerValue number => number.Number.ToString(CultureInfo.InvariantCulture),
            StringValue name => "\"" + name.Content + "\"",
            _ => "\"" + value.ToCanonical() + "\"",
        };
    }

    /// <summary>
    /// The LBL number of a LABEL, or of the target of a JUMP or REPEAT, in the section of the block being written
    /// (D271): its own number where it keeps it, the number the compiler gave it otherwise. LBL "NAME" is never
    /// written, since numbers run on every software level (controllers heidenhain.md 1).
    /// </summary>
    public static string LabelNumber(HeidenhainBlock writing, Value label)
    {
        return writing.LabelNumbers.LabelOf(writing.Step.Section, label) ?? Label(label);
    }

    /// <summary>
    /// Writes LABEL=n as LBL n where the block begins, the target of a jump and the start of a repeat (controller-
    /// mapping 6, LABEL).
    /// </summary>
    public static void WriteLabel(HeidenhainBlock writing)
    {
        if (writing.Take("LABEL") is not Word label)
        {
            return;
        }

        // A counter loop of a REPEAT to this label starts fresh on every arrival through the text (D213).
        foreach (string counter in writing.Repeats.ResetsBefore(writing.Block))
        {
            writing.Line(counter + " = 0");
        }

        writing.Line("LBL " + LabelNumber(writing, label.Value));

        // A JUMP or a CALL LBL REP reaches the label with what the control has active at the jump, which the STATIC
        // walk records and does not follow (virtual machine 1), and the blocks from the label on run on every way with
        // the modal state that way brings, as on every controller (language 1, 2 rule 2; 4.9: REPEAT runs the blocks
        // from the label again). The M functions, the speed and the modes, which Klartext writes where their block
        // states them, are written at their next word whatever the text before the label left (heidenhain 8 rule 2;
        // language 4.9).
        writing.MakeUnknownExcept(s_keptAtLabels);

        // R0, RL or RR and F, which Klartext writes with the motion whether or not its block states them, are written
        // after the label only where a block from the label on states them, and each jump is followed to them
        // (HeidenhainArrivals). The chain words and SETPOS after the label are written for the frame of the way the
        // text runs into it, and each jump is followed with the frame it brings to the first of them that would act
        // otherwise on its way (language 4.2, 4.9; HeidenhainChainArrivals, CMP119).
        HeidenhainArrivals.EnterLabel(writing);
    }

    /// <summary>
    /// Writes VAR:Qn=value as the formula of the Q parameter, Q1 = Q1 + 20 (controllers heidenhain.md 6;
    /// controller-mapping 6, VAR).
    /// </summary>
    public static void WriteVariables(HeidenhainBlock writing)
    {
        foreach (Word variable in writing.TakeAll("VAR"))
        {
            if (variable.Addr is not string name || !HeidenhainFormula.IsParameter(name))
            {
                writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                    $"{variable.ToCanonical()}: Klartext names its variables Q, QL and QR, and the machine "
                    + "configuration maps no other name to them (controllers heidenhain.md 6; language 4.9).");
                continue;
            }

            if (FormulaOf(writing, variable) is string formula)
            {
                writing.Line(name + " = " + formula);
            }
        }
    }

    /// <summary>
    /// Writes the flow words at the end of the block: REPEAT as CALL LBL n REP k, RETURN as the sub_end of the machine
    /// in a subprogram and as JUMP=END in a program, JUMP as FN 9 to FN 12.
    /// </summary>
    public static void WriteJump(HeidenhainBlock writing)
    {
        WriteRepeat(writing);

        // RETURN first: it pops the call, or ends a program (virtual machine 3.6), so a JUMP of the same block (RETURN
        // plus JUMP, D221) is written after it, where neither the virtual machine nor the control reaches it.
        var labels = new List<string>();
        if (ReturnLabel(writing) is string end)
        {
            labels.Add(end);
        }

        // JUMP=END continues at the end of the program: FN 9 to FN 12 to an LBL that ends with M30 (controller-mapping
        // 1, JUMP=END), the label the end of the program writes before its M30 (HeidenhainProgramFrame.WriteEnd). A
        // jump to a label brings what the control has at the jump to the blocks after the label (HeidenhainArrivals);
        // one after a return is never reached.
        if (writing.Take("JUMP") is Word jump)
        {
            bool toTheEnd = jump.Value is IdentValue { Name: "END" };
            string? target = toTheEnd ? EndLabel(writing) : LabelNumber(writing, jump.Value);
            if (target is not null)
            {
                labels.Add(target);
            }

            if (!toTheEnd && !writing.Block.Has("RETURN"))
            {
                HeidenhainArrivals.Check(writing, jump);
            }
        }

        if (labels.Count == 0)
        {
            return;
        }

        string? test = writing.Take("IF") is Word condition ? Condition(writing, condition) : AlwaysTrue;
        if (test is null)
        {
            return;
        }

        foreach (string label in labels)
        {
            writing.Line(test + " GOTO LBL " + label);
        }
    }

    /// <summary>
    /// The formula of the value of a VAR or ARG word: a number as written, an expression as the Klartext formula; null
    /// when it has none, which is reported.
    /// </summary>
    public static string? FormulaOf(HeidenhainBlock writing, Word word)
    {
        string? problem = "a string has no formula of numbers";
        string? formula = word.Value switch
        {
            IntegerValue or DecimalValue => word.Value.ToCanonical().Replace(".", writing.Numbers.DecimalSeparator,
                StringComparison.Ordinal),
            ExprValue { Tree: ExprNode tree } => HeidenhainFormula.Of(tree, writing.Numbers.DecimalSeparator,
                out problem),
            _ => null,
        };
        if (formula is null)
        {
            writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
                $"{word.ToCanonical()}: {problem}, so the Q parameter has no Klartext formula (controllers "
                + "heidenhain.md 6).");
        }

        return formula;
    }

    /// <summary>
    /// The number of the LBL before the M30 of the program whose file the block is written into, which JUMP=END and a
    /// RETURN of the program jump to (controller-mapping 1, JUMP=END), numbered with the labels (D271); null where the
    /// program has none.
    /// </summary>
    public static string? EndLabel(HeidenhainBlock writing)
    {
        return HeidenhainProgramFrame.ProgramOf(writing) is Section program
            ? writing.LabelNumbers.EndOf(program)
            : null;
    }

    // FN 9: IF +Q1 EQU +Q3, FN 10 unequal, FN 11 greater, FN 12 less (controllers heidenhain.md 6), with two values,
    // each a number or a Q parameter with its sign; <=, >=, AND, OR and NOT have no FN function.
    // D336: the comparison words are EQU, NE, GT and LT, each with the sign on both values (decisions.md; controllers
    // heidenhain.md 6).
    private static string? Condition(HeidenhainBlock writing, Word condition)
    {
        string separator = writing.Numbers.DecimalSeparator;
        if (condition.Value is ExprValue { Tree: ExprNode tree }
            && HeidenhainFormula.Unwrapped(tree) is BinaryNode comparison)
        {
            string? function = comparison.Operator switch
            {
                BinaryOperator.Equal => "FN 9: IF {0} EQU {1}",
                BinaryOperator.NotEqual => "FN 10: IF {0} NE {1}",
                BinaryOperator.Greater => "FN 11: IF {0} GT {1}",
                BinaryOperator.Less => "FN 12: IF {0} LT {1}",
                _ => null,
            };
            // D338: an operand that is a formula is spilled into a QL parameter before the jump.
            string? left = function is null ? null : HeidenhainFormula.Operand(comparison.Left, separator)
                ?? HeidenhainNumbers.Spilled(writing, comparison.Left);
            string? right = function is null ? null : HeidenhainFormula.Operand(comparison.Right, separator)
                ?? HeidenhainNumbers.Spilled(writing, comparison.Right);
            if (function is not null && left is not null && right is not null)
            {
                return string.Format(CultureInfo.InvariantCulture, function, left, right);
            }
        }

        writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
            $"{condition.ToCanonical()}: FN 9 to FN 12 compare two values, numbers or Q parameters, for equal, "
            + "unequal, greater or less (controllers heidenhain.md 6); the condition has no Klartext form.");
        return null;
    }

    // CALL LBL n REP k repeats the part between LBL n and the call k more times (controllers heidenhain.md 1;
    // controller-mapping 6, REPEAT).
    // D212 settles the default alone: TIMES is optional on REPEAT and defaults to 1, so a REPEAT without it runs
    // the blocks once more (decisions.md; rationale.md D212).
    // TODO(question): which text a compiler writes for a REPEAT without TIMES is not in that answer, which names no
    // compiler form, so REP 1 here, P=1 or no P on Siemens waits on the maintainer; REP 1 stays until then, and a
    // Klartext source cannot produce the form (heidenhain.md 7 rule 3), so no round trip depends on it.
    // D213: where a JUMP leaves the part, the repeat is a counter loop in a register of [format] counter_registers:
    // the counter is set to 0 before the label (WriteLabel), counted here, and the jump back taken while it has not
    // passed k, then set to 0 again, so that a later arrival starts the repeat fresh with k passes, as NCX does; the
    // TNC keeps the REP count of CALL LBL REP as block state that only GOTO 0 or a new program selection resets
    // (decisions.md D213, D388 (12)). A repeat without a register is the ERROR CMP130 of HeidenhainRepeats.
    private static void WriteRepeat(HeidenhainBlock writing)
    {
        if (writing.Take("REPEAT") is not Word repeat)
        {
            return;
        }

        long count = 1;
        if (writing.Take("TIMES") is Word times)
        {
            if (times.Value is not IntegerValue number)
            {
                HeidenhainNumbers.ReportValue(writing, times);
                return;
            }

            count = number.Number;
        }

        string label = LabelNumber(writing, repeat.Value);
        string text = count.ToString(CultureInfo.InvariantCulture);
        if (writing.Repeats.CounterOf(writing.Block) is string counter)
        {
            string passes = (count + 1).ToString(CultureInfo.InvariantCulture);
            writing.Line(counter + " = " + counter + " + 1");
            writing.Line("FN 12: IF +" + counter + " LT +" + passes + " GOTO LBL " + label);
            writing.Line(counter + " = 0");
        }
        else if (!writing.Repeats.IsRefused(writing.Block))
        {
            writing.Line("CALL LBL " + label + " REP " + text);
        }

        // The repeat brings what the control has at the call to the blocks after the label (HeidenhainArrivals).
        HeidenhainArrivals.Check(writing, repeat);
    }

    // RETURN returns to the caller before SUB=END is reached (language 4.9, 4.13). In a program, which no caller
    // entered, RETURN is treated as JUMP=END (language 4.9; virtual machine 3.6): the label of the LBL before the M30
    // is returned, for WriteJump to write the jump to it with the IF of the block.
    // D272: in a subprogram a RETURN before the end of the section is a jump to an LBL m directly before the closing
    // LBL 0 of the section (HeidenhainProgramFrame.WriteEnd), FN 9: IF +0 EQU +0 GOTO LBL m without IF and FN 9 to
    // FN 12 with the condition under IF; a RETURN directly before SUB=END writes nothing. Each section keeps exactly
    // one LBL 0 (decisions.md D272; machine-config 2, sub_end).
    private static string? ReturnLabel(HeidenhainBlock writing)
    {
        if (writing.Take("RETURN") is null)
        {
            return null;
        }

        if (writing.Step.Section is Section { Kind: SectionKind.Program })
        {
            return EndLabel(writing);
        }

        if (writing.Step.Section is not Section sub || HeidenhainLabelNumbers.EndsTheSection(writing.LookAhead,
            writing.Step))
        {
            return null;
        }

        return writing.LabelNumbers.ReturnOf(sub);
    }
}
