using System.Globalization;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The flow of custom macro B (controllers fanuc.md 1, 7; controller-mapping 6; language 4.9): LABEL as the block
/// number N that GOTO names, JUMP as GOTO or IF [ ] GOTO, JUMP=END as the program end, the M99 loop of a main program,
/// VAR as #n = expression through [variables] map, CALL as M98 P L, or G65 P with the letter arguments of ARG. The
/// structured forms are never reconstructed (phase 3, P3-06).
/// </summary>
internal static class FanucFlow
{
    // Which Fanuc call the ARG words of a CALL say (D148): the subprogram call M98 of a CALL without an ARG, which
    // shares the caller's #1..#33 and carries no arguments, the macro call G65 of a CALL that carries an argument
    // value, whether or not a pass-through word stands next to it, since D148's evidence has it that only a macro
    // call can take arguments at all, or the pass-through ARG:Vn={$Vn} alone, which both calls write, so that the
    // NCX program does not say which of the two it is.
    private enum CallForm
    {
        Subprogram,
        Macro,
        PassThrough,
    }

    // The local variables of G65 and the letters that set them (controllers fanuc.md 7): #1 is A, #2 B, ...
    private static readonly Dictionary<int, string> s_letters = new()
    {
        [1] = "A",
        [2] = "B",
        [3] = "C",
        [4] = "I",
        [5] = "J",
        [6] = "K",
        [7] = "D",
        [8] = "E",
        [9] = "F",
        [11] = "H",
        [13] = "M",
        [17] = "Q",
        [18] = "R",
        [19] = "S",
        [20] = "T",
        [21] = "U",
        [22] = "V",
        [23] = "W",
        [24] = "X",
        [25] = "Y",
        [26] = "Z",
    };

    /// <summary>
    /// A LABEL is the block number of a line of its own, the target of GOTO (controllers fanuc.md 1: N block numbers
    /// matter as jump targets); the label of the M99 loop writes nothing (D210). A jump reaches the label with the
    /// modal codes of the block it jumps from, so the target state is unknown after it.
    /// </summary>
    public static void WriteLabel(FanucBlock write)
    {
        if (write.Block.Find("LABEL") is not Word label)
        {
            return;
        }

        write.Written(label);
        string name = label.Value.ToCanonical();
        if (name == write.Labels.LoopLabel)
        {
            return;
        }

        // The STATIC walk does not follow JUMP (virtual machine 1), so the values of NCX at the label are those of the
        // block before it, and a jump arrives with those of its own block. A modal value a block may take without
        // stating it, F, the feed mode, the plane, D, S, G96 or G97, whose value of NCX differs by path, or depends on
        // the path that led to the label or to a jump (D53), is the control's to keep (language 4.3, 4.4, 4.5, 4.11:
        // modal), written here and before each jump where the control does not hold it and a line of its own can;
        // where none can, the first block after the label that takes it without stating it is CMP309 (FanucPaths).
        bool reached = write.Labels.IsJumpTarget(name);
        Dictionary<string, bool> byPath = reached ? FanucPaths.MeetAtLabel(write, name) : [];
        if (write.Labels.NumberOf(name) is int number)
        {
            write.Writer("N" + number.ToString(CultureInfo.InvariantCulture));
        }

        // Every other modal code, G0 or G1, G90 or G91, the canned cycle, and a modal value of the same value on every
        // path, stands again at its next use (language 2 rules 2 and 3; controllers fanuc.md 10 rule 1). The M99 loop
        // returns to the start block of the program, whose lines run again.
        if (!reached)
        {
            return;
        }

        write.MakeTargetUnknown();
        foreach (KeyValuePair<string, bool> value in byPath)
        {
            if (value.Value)
            {
                write.Hold(value.Key);
            }
            else
            {
                write.Lose(value.Key);
            }
        }
    }

    /// <summary>
    /// The variables, the jump and the call of the block, in lines after the main line.
    /// </summary>
    public static void Write(FanucBlock write)
    {
        Block block = write.Block;
        foreach (Word word in block.Words)
        {
            if (word.Key == "VAR" && word.Addr is string name)
            {
                write.Written(word);
                WriteVariable(write, name, word.Value);
            }
        }

        write.Written("IF");
        if (block.Find("JUMP") is Word jump)
        {
            write.Written(jump);
            WriteJump(write, jump);
        }

        if (block.Find("CALL") is Word call)
        {
            write.Written(call);
            write.Written("ARG");
            write.Written("TIMES");
            WriteCall(write, call);
        }

        if (block.Find("REPEAT") is Word repeat)
        {
            write.Written(repeat);
            write.Written("TIMES");
            WriteRepeat(write, repeat);
        }
    }

    /// <summary>
    /// Clears the counters of every repeat counter the program uses, at its start: a common variable keeps its value
    /// over a reset, so a run that stopped inside a repeat would leave its count (D213, D280).
    /// </summary>
    public static void ClearCounters(FanucBlock write)
    {
        IReadOnlyList<string> counters = write.Machine.Variables.RepeatCounters;
        for (int depth = 0; depth < write.Repeats.Counters && depth < counters.Count; depth++)
        {
            write.Write(counters[depth] + " = 0");
        }
    }

    // REPEAT=label TIMES=k runs the blocks from the label to the REPEAT k more times (language 4.9, D212): the counter
    // of its nesting depth counts the passes, IF GOTO returns to the label while it is not above k, and the counter is
    // 0 again once the repeat ends, so that a later arrival starts it fresh (controller-mapping 6, REPEAT + TIMES;
    // D213, D280).
    private static void WriteRepeat(FanucBlock write, Word repeat)
    {
        string label = repeat.Value.ToCanonical();
        if (write.Repeats.RangeOf(write.Step.Section, write.Block) is not FanucRepeatRange range
            || write.Labels.NumberOf(label) is not int number
            || CounterOf(write, range) is not string counter)
        {
            return;
        }

        string times = "1";
        if (write.Block.Find("TIMES") is Word count)
        {
            if (FanucExpressions.WordValue(write, "L", count.Value, 1m) is not string value)
            {
                return;
            }

            times = value;
        }

        // The label takes the modal values that differ by path from the control (FanucFlow.WriteLabel).
        FanucPaths.CheckJump(write, label);
        write.Write(counter + " = " + counter + " + 1");
        write.Write("IF [" + counter + " LE " + times + "] GOTO " + number.ToString(CultureInfo.InvariantCulture));
        write.Write(counter + " = 0");
    }

    // The counter of a range: the variable of its nesting depth in [variables] repeat_counters; CMP314 where the list
    // is missing or too short, CMP315 where the program uses the variable itself; no fallback (D280).
    private static string? CounterOf(FanucBlock write, FanucRepeatRange range)
    {
        IReadOnlyList<string> counters = write.Machine.Variables.RepeatCounters;
        string depth = (range.Depth + 1).ToString(CultureInfo.InvariantCulture);
        if (range.Depth >= counters.Count)
        {
            write.Error(DiagnosticCodes.FanucRepeatCounterMissing,
                $"REPEAT={range.Label} needs a counter variable for nesting depth {depth}, and [variables] "
                + $"repeat_counters names {counters.Count.ToString(CultureInfo.InvariantCulture)}; the compiler "
                + "guesses no variable (D280; machine-config 7).");
            return null;
        }

        string counter = counters[range.Depth];
        if (write.Repeats.Uses(counter))
        {
            write.Error(DiagnosticCodes.FanucRepeatCounterInUse,
                $"REPEAT={range.Label} counts in {counter} of [variables] repeat_counters, which the program uses "
                + "itself (D280; machine-config 7).");
            return null;
        }

        return counter;
    }

    /// <summary>
    /// D213: a JUMP that leaves a repeat ends it, so it clears the counter of every range it leaves, under the
    /// condition of the jump where it has one, before it jumps; a RETURN leaves every range around it, as JUMP=END
    /// does, so that the next call of the subprogram counts its passes afresh.
    /// </summary>
    /// <param name="write">The JUMP or RETURN block.</param>
    /// <param name="target">The label of the JUMP; "END" for JUMP=END and RETURN.</param>
    /// <param name="condition">The Fanuc condition of the jump; null for an unconditional one.</param>
    public static void ClearLeftRepeats(FanucBlock write, string target, string? condition)
    {
        int? line = target == "END" ? null : LineOf(write, target);
        IReadOnlyList<string> counters = write.Machine.Variables.RepeatCounters;
        foreach (FanucRepeatRange range in write.Repeats.LeftBy(write.Step.Section, write.Block.Line, line))
        {
            if (range.Depth < counters.Count)
            {
                string clear = counters[range.Depth] + " = 0";
                write.Write(condition is null ? clear : "IF " + condition + " THEN " + clear);
            }
        }
    }

    // The line of the LABEL block of a label in the section of the block.
    private static int? LineOf(FanucBlock write, string label)
    {
        foreach (BlockStep step in write.Steps)
        {
            if (step.Section == write.Step.Section && step.Block.Find("LABEL")?.Value.ToCanonical() == label)
            {
                return step.Block.Line;
            }
        }

        return null;
    }

    // #n = e assigns (controllers fanuc.md 7): a V name is #n (D33), a Heidenhain name the variable of [variables] map
    // (machine-config 7).
    private static void WriteVariable(FanucBlock write, string name, Value value)
    {
        string? variable = FanucExpressions.Variable(write, name, null);
        string? text = value switch
        {
            IntegerValue or DecimalValue => value.ToCanonical(),
            ExprValue { Tree: not null } expression => FanucExpressions.Expression(write, expression.Tree),
            _ => null,
        };
        if (text is null && value is not ExprValue)
        {
            write.Error(DiagnosticCodes.FanucExpressionNotWritable,
                $"VAR:{name}={value.ToCanonical()}: custom macro B holds numbers only (controllers fanuc.md 7).");
        }

        if (variable is not null && text is not null)
        {
            write.Write(variable + " = " + text);
        }
    }

    // GOTO n to the block Nn, IF [c] GOTO n under the condition (controllers fanuc.md 7; controller-mapping 6);
    // JUMP=END ends the program, as a GOTO to the block with M30 does (controller-mapping 1, JUMP=END; D88); the jump
    // to the label after the header is the M99 of a main program, which jumps to its first block (controller-mapping
    // 6; D210, recommendation).
    private static void WriteJump(FanucBlock write, Word jump)
    {
        string target = jump.Value.ToCanonical();
        string? condition = write.Block.Find("IF")?.Value is ExprValue { Tree: not null } test
            ? FanucExpressions.Condition(write, test.Tree)
            : null;
        bool conditional = write.Block.Has("IF");
        if (conditional && condition is null)
        {
            return;
        }

        // A conditional JUMP=END in a subprogram ends the whole program, which no GOTO of the subprogram reaches: it is
        // CMP302 below and writes nothing, the clearing included.
        bool endInSub = target == "END" && conditional && write.Step.Section?.Kind != SectionKind.Program;
        if (!endInSub)
        {
            ClearLeftRepeats(write, target, condition);
        }

        if (target == "END")
        {
            if (!conditional)
            {
                FanucProgramFrame.WriteEnd(write, "");
            }
            else if (!endInSub && write.Labels.EndLabel is int end)
            {
                write.Write("IF " + condition + " GOTO " + end.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                write.Error(DiagnosticCodes.FanucConditionalEndInSub,
                    "A conditional JUMP=END in a subprogram has no Fanuc form: a GOTO stays in its program "
                    + "(controllers fanuc.md 7; language 4.9, JUMP).");
            }

            return;
        }

        if (!conditional && target == write.Labels.LoopLabel)
        {
            write.Write("M99");
            return;
        }

        if (write.Labels.NumberOf(target) is not int number)
        {
            return;
        }

        // The label takes the modal values that differ by path from the control (FanucFlow.WriteLabel).
        if (write.Labels.IsJumpTarget(target))
        {
            FanucPaths.CheckJump(write, target);
        }

        string jumpText = "GOTO " + number.ToString(CultureInfo.InvariantCulture);
        write.Write(conditional ? "IF " + condition + " " + jumpText : jumpText);
    }

    // M98 P L calls a subprogram L times, G65 P with the letter arguments a macro (controllers fanuc.md 1, 7;
    // controller-mapping 6, CALL and TIMES, CALL and ARG); a subprogram is called by its O number, an external program
    // by the number of its file name O9010 (language 4.9). A call of a program outside the file writes the code of
    // [format] external_call, M98 where the machine names none (D322). Which of the two calls a CALL whose ARG words
    // are the pass-through ARG:Vn={$Vn} alone is, the NCX program does not say: the block is written as the
    // subprogram call and reported with CMP311 (FormOf, row 196). A CALL that carries an argument value is the macro
    // call, a pass-through word next to it or not (D148: only a macro call can take arguments).
    // D215: the name of CALL="name" is the file name without directory and extension; a Fanuc program is named by its
    // O number only (controllers fanuc.md 1), so a name O followed by digits, or digits alone, is called by its number
    // and any other external name has no Fanuc form, an ERROR.
    private static void WriteCall(FanucBlock write, Word call)
    {
        if (write.Block.Has("IF"))
        {
            write.Error(DiagnosticCodes.FanucConditionalCall,
                "IF with CALL has no Fanuc form: IF [ ] THEN assigns and IF [ ] GOTO jumps (controllers fanuc.md 7).");
            return;
        }

        string name = call.Value is StringValue external ? external.Content : call.Value.ToCanonical();
        string digits = name.Length > 1 && name[0] == 'O' ? name.Substring(1) : name;
        if (digits.Length == 0 || digits.Length > 8 || !digits.All(char.IsAsciiDigit))
        {
            write.Error(DiagnosticCodes.FanucCallOfNoProgramNumber,
                $"CALL={call.Value.ToCanonical()} names no Fanuc program number, which M98 P and G65 P call "
                + "(controllers fanuc.md 1, 7).");
            return;
        }

        CallForm form = FormOf(write.Block);
        string program = digits.TrimStart('0').PadLeft(4, '0');
        var words = new List<string>();
        bool macro = form == CallForm.Macro;
        bool outside = call.Value is StringValue;
        string code = outside && !macro ? write.Machine.Format?.ExternalCall ?? "M98" : "M98";
        words.Add(macro ? "G65" : code);
        words.Add("P" + program);
        if (write.Block.Find("TIMES") is Word times
            && FanucExpressions.WordValue(write, "L", times.Value, 1m) is string count)
        {
            words.Add("L" + count);
        }

        if (macro && !AddArguments(write, words))
        {
            return;
        }

        string block = string.Join(" ", words);
        if (form == CallForm.PassThrough)
        {
            write.Warning(DiagnosticCodes.FanucCallFormOfTheSharedLocals,
                $"CALL={call.Value.ToCanonical()}, whose ARG words are the pass-through of a caller's locals "
                + $"({PassThroughWords(write.Block)}), is written as the subprogram call {block}, where those words "
                + "carry nothing: M98 opens no local-variable level, so the subprogram reads the caller's #1 to #33 "
                + "itself, which is the shape D148 gives the reader. The same NCX text is what a G65 macro call of "
                + "those locals reads as, and the G65 level is the one virtual machine 3.6 gives a CALL, whose "
                + "callee's V1 to V33 the return restores; under M98 a write of #1 to #33 in the subprogram reaches "
                + "the caller instead. Which of the two Fanuc calls this block is, no document says (row 196 of "
                + "docs/implementation/03-open-questions.md).");
        }

        write.Write(block);
    }

    // Which Fanuc call the ARG words of a CALL say (D148), told apart by what they say and not by their presence:
    // Macro where any ARG carries an argument value, the letters of G65, which raises the local-variable level - a
    // block that carries an argument value is the macro call whether or not a pass-through word stands next to it,
    // since D148's evidence has it that only a macro call can take arguments at all, and controller-mapping 6 maps a
    // CALL with ARG to G65 Pnnnn A1 B2; PassThrough where every ARG is the pass-through of the callee's own local,
    // ARG:Vn={$Vn} with n in 1..33, which is what the reader writes for the locals an M98 subprogram reads from its
    // caller, since a subprogram call opens no local-variable level and shares the caller's #1..#33 - but which a G65
    // Pnnnn A#1 of the same locals reads as too (controllers fanuc.md 7; controller-mapping 6, CALL and ARG). A CALL
    // without an ARG is a Subprogram.
    // TODO(question): no word of the NCX program says which of the two Fanuc calls a PassThrough CALL is: the two
    // sources convert to the same NCX text (G65 P0100 A#1 passes the caller's #1 to the callee's #1 too), so no
    // reading of the block compiles both back. D148 settles the reader's side alone, the ARG:Vn={$Vn} of an M98, so
    // the compiler writes the M98 that shape came from and reports every such block with the WARNING CMP311, which
    // names the G65 reading it does not write and that virtual machine 3.6 gives a CALL (row 196 of
    // ../../../docs/implementation/03-open-questions.md).
    private static CallForm FormOf(Block block)
    {
        bool passThrough = false;
        bool values = false;
        foreach (Word argument in block.Words)
        {
            if (argument.Key != "ARG")
            {
                continue;
            }

            if (IsPassThrough(argument))
            {
                passThrough = true;
            }
            else
            {
                values = true;
            }
        }

        return values
            ? CallForm.Macro
            : (passThrough ? CallForm.PassThrough : CallForm.Subprogram);
    }

    // ARG:Vn={$Vn}, the local n of the callee taking the local n of the caller, for one of the shared locals #1..#33
    // (D148). An expression whose tree the parser has not built is compared as the text the writer emits.
    private static bool IsPassThrough(Word argument)
    {
        if (argument.Addr is not string name
            || !(name.Length > 1 && name[0] == 'V'
                && int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int local))
            || local < 1 || local > 33
            || argument.Value is not ExprValue expression)
        {
            return false;
        }

        return expression.Tree is ExprNode tree
            ? tree is VariableNode { Index: null } variable && variable.Name == name
            : expression.Text.Trim() == "$" + name;
    }

    // The pass-through words of a block, for the message of CMP311.
    private static string PassThroughWords(Block block)
    {
        var words = new List<string>();
        foreach (Word argument in block.Words)
        {
            if (argument.Key == "ARG" && IsPassThrough(argument))
            {
                words.Add("ARG:" + argument.Addr + "=" + argument.Value.ToCanonical());
            }
        }

        return string.Join(" ", words);
    }

    // ARG:Vn is the letter that sets #n in the standard table (controllers fanuc.md 7; language 4.9).
    private static bool AddArguments(FanucBlock write, List<string> words)
    {
        foreach (Word argument in write.Block.Words)
        {
            if (argument.Key != "ARG")
            {
                continue;
            }

            string? name = argument.Addr;
            int local = 0;
            bool isLocal = name is not null && name.Length > 1 && name[0] == 'V'
                && int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out local);
            if (!isLocal || !s_letters.TryGetValue(local, out string? letter))
            {
                write.Error(DiagnosticCodes.FanucArgumentWithoutLetter,
                    $"ARG:{name} has no letter of G65: the letters set #1 to #26 by the standard table (controllers "
                    + "fanuc.md 7).");
                return false;
            }

            if (FanucExpressions.WordValue(write, "X", argument.Value, 1m) is string value)
            {
                words.Add(letter + value);
            }
        }

        return true;
    }
}
