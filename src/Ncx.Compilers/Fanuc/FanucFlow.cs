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
    // shares the caller's #1..#33 and carries no arguments, the macro call G65, whose letters carry argument values,
    // the pass-through ARG:Vn={$Vn} that both calls write, so that the NCX program does not say which it is, or both
    // a pass-through and an argument value at once, which no Fanuc call writes.
    private enum CallForm
    {
        Subprogram,
        Macro,
        PassThrough,
        Both,
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
            // TODO(question): controller-mapping 6 writes REPEAT with TIMES as "counter variable and IF GOTO
            // (compiler)" and names no variable for the counter, which may clash with a variable of the program; the
            // block is an ERROR until that is answered (D213). D212 settles only what a missing TIMES means, the
            // count 1.
            write.Written(repeat);
            write.Written("TIMES");
            write.Error(DiagnosticCodes.FanucRepeatNotWritten,
                "REPEAT needs a counter variable and IF GOTO on Fanuc, and no document names the variable that holds "
                + "the count (controller-mapping 6, REPEAT + TIMES).");
        }
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

        if (target == "END")
        {
            if (!conditional)
            {
                FanucProgramFrame.WriteEnd(write, "");
            }
            else if (write.Labels.EndLabel is int end)
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
    // are the pass-through ARG:Vn={$Vn} is, the NCX program does not say: the block is written as the subprogram call
    // and reported with CMP311 (FormOf, row 196).
    // TODO(question): D215: the file name of CALL="name" is open; a name O followed by digits is called by its number,
    // any other external name is an ERROR, until D215 is answered.
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
        if (form == CallForm.Both)
        {
            write.Error(DiagnosticCodes.FanucCallArgumentsOfNoOneCall,
                $"CALL={call.Value.ToCanonical()} carries both the pass-through of a caller's local "
                + $"({PassThroughWords(write.Block)}) and an argument value, which is neither Fanuc call: M98 shares "
                + "the caller's #1 to #33 and takes no arguments at all, and under G65 the pass-through would set the "
                + "locals of a new local-variable level, the difference D148 turns on (controllers fanuc.md 7; "
                + "controller-mapping 6, CALL and ARG).");
            return;
        }

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
    // Macro where every ARG carries an argument value, the letters of G65, which raises the local-variable level;
    // PassThrough where every ARG is the pass-through of the callee's own local, ARG:Vn={$Vn} with n in 1..33, which
    // is what the reader writes for the locals an M98 subprogram reads from its caller, since a subprogram call opens
    // no local-variable level and shares the caller's #1..#33 - but which a G65 Pnnnn A#1 of the same locals reads as
    // too (controllers fanuc.md 7; controller-mapping 6, CALL and ARG). A CALL without an ARG is a Subprogram.
    // TODO(question): no word of the NCX program says which of the two Fanuc calls a PassThrough CALL is: the two
    // sources convert to the same NCX text (G65 P0100 A#1 passes the caller's #1 to the callee's #1 too), so no
    // reading of the block compiles both back. D148 settles the reader's side alone, the ARG:Vn={$Vn} of an M98, so
    // the compiler writes the M98 that shape came from and reports every such block with the WARNING CMP311, which
    // names the G65 reading it does not write and that virtual machine 3.6 gives a CALL; the mixed block, which
    // neither call writes, stays the ERROR CMP310 (row 196 of ../../../docs/implementation/03-open-questions.md).
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
            ? (passThrough ? CallForm.Both : CallForm.Macro)
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

    // The pass-through words of a block, for the messages of CMP310 and CMP311.
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
