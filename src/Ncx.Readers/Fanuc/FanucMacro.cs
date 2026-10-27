using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// Custom macro B and the calls of subprograms (controllers fanuc.md 1, 7; controller-mapping 6; language 4.9): #n =
/// ... to VAR:Vn (D33), a [ ] expression to { }, IF GOTO to JUMP with IF, IF THEN and WHILE DO END lowered to LABEL,
/// JUMP and IF, GOTO to JUMP, JUMP=END to a block that ends the program, the NT NURSE branch commands of the builder
/// nakamura to JUMP with IF, M98 P L to CALL with TIMES, and M200 P{times}{nnnn} of the builder nakamura the same, G65
/// with its letter arguments to CALL with ARG, and the external call code of the machine, [format] external_call, to
/// the call of an external program; a machine that names none has no external call (D322). G66 and G67 stay RAW
/// (controller-mapping 9).
/// </summary>
internal static class FanucMacro
{
    // The builder whose NT NURSE macros the branch commands are (controller-mapping 6 and 8; machine-builders.md).
    private const string Nakamura = "nakamura";

    // The comparisons of G471 to G476 and of G481 to G486, in the order of their codes: equal, not equal, >=, <=, >, <
    // (controller-mapping 6, JUMP + IF).
    private static readonly string[] s_comparisons = ["EQ", "NE", "GE", "LE", "GT", "LT"];

    // The letters of a G65 call and the local variables they set by the standard table (controllers fanuc.md 7); G, L,
    // N, O and P cannot be arguments.
    private static readonly Dictionary<string, int> s_arguments = new(StringComparer.Ordinal)
    {
        ["A"] = 1,
        ["B"] = 2,
        ["C"] = 3,
        ["I"] = 4,
        ["J"] = 5,
        ["K"] = 6,
        ["D"] = 7,
        ["E"] = 8,
        ["F"] = 9,
        ["H"] = 11,
        ["M"] = 13,
        ["Q"] = 17,
        ["R"] = 18,
        ["S"] = 19,
        ["T"] = 20,
        ["U"] = 21,
        ["V"] = 22,
        ["W"] = 23,
        ["X"] = 24,
        ["Y"] = 25,
        ["Z"] = 26,
    };

    /// <summary>
    /// Reads the macro statements and the calls of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        // G66 makes a macro modal and G67 ends it; both stay RAW (controller-mapping 9).
        if (block.TakeCode("G66") || block.TakeCode("G67"))
        {
            block.Draft.KeepAsRaw("G66 and G67, the modal macro call, are kept as RAW (controller-mapping 9)");
            return;
        }

        if (ReadBranchCommand(block))
        {
            return;
        }

        ReadCalls(block);
        ReadFlow(block);
        ReadLoops(block);
    }

    /// <summary>
    /// Tells whether the machine is of the builder nakamura, whose NT NURSE macros the branch commands and the G411
    /// jumps are (controller-mapping 6 and 8).
    /// </summary>
    /// <param name="machine">The machine.</param>
    public static bool IsBuilderNakamura(MachineConfig machine)
    {
        return string.Equals(machine.Machine.Builder, Nakamura, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The code that calls a program outside the file on this machine, [format] external_call, which the control takes
    /// from parameter 6030 (controllers fanuc.md 1; controller-mapping 6; D322); null where the machine names none,
    /// and then the reader reads no external call at all. D322: a machine that uses M198 for the call names it, and a
    /// machine whose wait range covers the code must not, so the wait mark keeps the code there.
    /// </summary>
    /// <param name="machine">The machine being read for.</param>
    public static string? ExternalCallCode(MachineConfig machine)
    {
        string? code = machine.Format?.ExternalCall;
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        return IsWaitMark(machine, code) ? null : code;
    }

    // A code the mark range of [sync] covers is a wait mark of the machine, M198 of a Nakamura, never the external
    // call (controllers fanuc.md 8; machine-config 5; D322).
    private static bool IsWaitMark(MachineConfig machine, string code)
    {
        return machine.Sync?.MarkRange is MarkRange range
            && code.Length > 1
            && (code[0] == 'M' || code[0] == 'm')
            && int.TryParse(code.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            && number >= range.First && number <= range.Last;
    }

    /// <summary>
    /// Tells whether a G code is an NT NURSE branch command of the machine, G480, G471 to G476 or G481 to G486 of the
    /// builder nakamura, which the reader reads as JUMP with IF and does not keep as RAW (controller-mapping 6, 9).
    /// </summary>
    /// <param name="machine">The machine.</param>
    /// <param name="code">The code without leading zeros, "G471".</param>
    public static bool IsBranchCommand(MachineConfig machine, string code)
    {
        return IsBuilderNakamura(machine) && (code == "G480" || ComparisonOf(code) is not null);
    }

    /// <summary>
    /// The whole number a word carries as written, the label 20 of I20. or the variable 1 of Q1.; null for a word with
    /// an expression, a fraction or a sign.
    /// </summary>
    /// <param name="word">The word.</param>
    public static long? WholeNumber(SourceWord word)
    {
        return word.Expression is null && word.Number is decimal number && number >= 0
            && number == decimal.Truncate(number)
            ? decimal.ToInt64(number)
            : null;
    }

    /// <summary>
    /// The NCX value of a source word: the number as written, or the expression of a #n variable or a [ ] expression;
    /// null, with the block kept as RAW, when NCX cannot express it.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="word">The word, X50.4, B-#501 or B-[#502+#11099].</param>
    public static Value? ValueOf(FanucBlock block, SourceWord word)
    {
        if (word.Expression is null)
        {
            Value? number = word.ToNcxNumber();
            if (number is null)
            {
                block.Draft.KeepAsRaw($"{word.Address}{word.Text} carries no number");
            }

            return number;
        }

        return ExpressionOf(block, word.Expression);
    }

    /// <summary>
    /// The number of the program an M98 P, a G65 P, an external call P or an M200 P of the builder nakamura calls:
    /// the digits of P, and in the older form of M98 and in M200, P with more than four digits and no L, the last four
    /// (controllers fanuc.md 1 and 7, controller-mapping 6); null when P is no program number.
    /// </summary>
    /// <param name="program">The P word.</param>
    /// <param name="olderForm">True for an M98 or an M200 without L, whose P may carry the count in front of the
    /// program number; G65 P and an external call P are the program number as written.</param>
    public static long? CalledProgram(SourceWord program, bool olderForm)
    {
        string digits = Digits(program);
        if (digits.Length == 0)
        {
            return null;
        }

        // D249: in the 30i program format M98 P with more than four digits and no L carries the count in front of the
        // last four digits, P51002 = O1002 five times; M98 P L names the program in P and the count in L. The same
        // holds for M200 of the builder nakamura; G65 and M198 have no older form (fanuc 1 and 7, controller-mapping
        // 6). The Series 15 program format (0001#1), where P51002 calls O1002 once, is not supported (fanuc 1).
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long whole))
        {
            return null;
        }

        return olderForm && digits.Length > 4 ? whole % 10000 : whole;
    }

    /// <summary>
    /// The programs a block calls: M98 P, G65 P, the external call code of the machine P, and on a machine of the
    /// builder nakamura M200 P, the same as M98 for NCX (controllers fanuc.md 1, 7; controller-mapping 6, CALL and
    /// TIMES; controller-mapping 8); none for a P that names no program.
    /// </summary>
    /// <param name="block">A source block.</param>
    /// <param name="machine">The machine with its builder.</param>
    public static List<FanucCall> CallsOf(SourceBlock block, MachineConfig machine)
    {
        var calls = new List<FanucCall>();
        SourceWord? program = block.Find("P");
        if (program is null)
        {
            return calls;
        }

        SourceWord? times = block.Find("L");
        string? external = ExternalCallCode(machine);
        foreach (SourceWord word in block.Words)
        {
            string? code = word.Address is "G" or "M" ? NativeCode.Of(word) : null;
            bool older = code == "M98" || (code == "M200" && IsBuilderNakamura(machine));
            if ((older || code == "G65" || (external is not null && code == external))
                && CalledProgram(program, older && times is null) is long called)
            {
                calls.Add(new FanucCall(called, Repeats(program, times, older), code == external, code == "G65"));
            }
        }

        return calls;
    }

    // A call runs its program more than once, or a number of times the reader does not know: an L other than 1, and in
    // the older form of M98 and in M200 the digits in front of the program number (controller-mapping 6).
    private static bool Repeats(SourceWord program, SourceWord? times, bool older)
    {
        if (times is not null)
        {
            return times.Expression is not null || times.Number != 1m;
        }

        string digits = Digits(program);
        return older && digits.Length > 4
            && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long whole)
            && whole / 10000 > 1;
    }

    // M98 P L calls a subprogram L times, the external call code of the machine P an external program, G65 P L a
    // macro with letter arguments (controllers fanuc.md 1, 7; controller-mapping 6; D322); Nakamura M200
    // P{times}{nnnn} calls a program from the memory card, the same as M98 for NCX (controller-mapping 6, CALL and
    // TIMES; controller-mapping 8).
    private static void ReadCalls(FanucBlock block)
    {
        SourceWord? m98 = block.FindCode("M98");
        SourceWord? external = ExternalCallCode(block.Machine) is string code ? block.FindCode(code) : null;
        SourceWord? g65 = block.FindCode("G65");
        SourceWord? m200 = IsBuilderNakamura(block.Machine) ? block.FindCode("M200") : null;
        if (m98 is null && external is null && g65 is null && m200 is null)
        {
            // D221: M99 Pn in a section that no block of the file calls returns to the block Nn of that section, a
            // JUMP=n to the LABEL=n the structure pass writes there; in a subprogram a block of the file calls, P
            // names a block of the caller, which no JUMP of NCX reaches, and the block stays RAW (decisions.md;
            // controller-mapping 6, the paragraph after the table; D5).
            if (block.HasCode("M99") && block.Find("P") is SourceWord returnTo)
            {
                if (block.Fanuc.ReturnsToTheBlock.TryGetValue(block.Source.Line, out string? target))
                {
                    block.MarkRead(block.FindCode("M99")!);
                    block.MarkRead(returnTo);
                    block.Draft.Main.Add("JUMP", JumpTo(block,
                        long.Parse(target, NumberStyles.None, CultureInfo.InvariantCulture)));
                }
                else
                {
                    block.Draft.KeepAsRaw("M99 P returns to a block of the caller, which no JUMP of NCX reaches");
                }
            }

            return;
        }

        foreach (SourceWord? word in new[] { m98, external, g65, m200 })
        {
            if (word is not null)
            {
                block.MarkRead(word);
            }
        }

        SourceWord? program = block.Take("P");
        SourceWord? times = block.Take("L");
        bool olderForm = (m98 is not null || m200 is not null) && times is null;
        long? number = program is null ? null : CalledProgram(program, olderForm);
        if (number is not long called)
        {
            block.Draft.KeepAsRaw("the call names no program number P");
            return;
        }

        DraftBlock main = block.Draft.Main;
        main.Add("CALL", external is not null ? External(called) : Target(block, called));
        if (times is not null)
        {
            if (ValueOf(block, times) is Value count)
            {
                main.Add("TIMES", count);
            }
        }
        else if (Digits(program!).Length > 4 && (m98 is not null || m200 is not null))
        {
            // The older form M98 P51002 calls program 1002 five times, M200 P20100 program 0100 twice
            // (controller-mapping 6).
            long count = long.Parse(Digits(program!), NumberStyles.None, CultureInfo.InvariantCulture) / 10000;
            if (count > 1)
            {
                main.Add("TIMES", new IntegerValue(count, count.ToString(CultureInfo.InvariantCulture)));
            }
        }

        if (g65 is not null)
        {
            ReadArguments(block);
            return;
        }

        if (external is null)
        {
            ReadSharedLocals(block, called);
        }
    }

    // D148: a subprogram call opens no local-variable level, so an M98 subprogram (and the M200 of the builder
    // nakamura) shares the caller's #1..#33 for reading and for writing, while a G65 macro call raises the level and
    // starts the callee's vacant apart from its letter arguments (fanuc 7; controllers fanuc.md 7). A CALL of NCX is
    // the macro call: the callee's V1..V33 start unassigned apart from the ARG values and the caller's are restored
    // at the return (virtual machine 3.6, 4). So the locals the subprogram reads from the caller stand on the CALL as
    // ARG:Vn={$Vn}, and a local it writes back for the caller does not reach the caller through a CALL at all, which
    // the reader reports (decisions.md, D148; RDR106). D148 settles those two alone: a local the subprogram assigns
    // itself before it reads it is no local it reads from the caller, and one it leaves behind that no block of the
    // caller reads is none it writes back for the caller, so neither puts a word or a diagnostic on the CALL.
    // TODO(question): an external program, the call code of [format] external_call and a P the file holds no O line
    // for, has blocks the reader does not see, so it cannot tell which of #1..#33 that program reads or writes; D148
    // settles the subprogram of the file and says nothing of one outside it, and the reader passes no ARG and reports
    // nothing there, as main does (wave-3 question #194).
    private static void ReadSharedLocals(FanucBlock block, long called)
    {
        var read = new SortedSet<int>();
        var written = new SortedSet<int>();
        SharedLocalsOf(block, called, read, written);
        foreach (int local in read)
        {
            string name = "V" + local.ToString(CultureInfo.InvariantCulture);
            if (FanucExpression.Parse("$" + name, block, out _) is ExprValue value)
            {
                block.Draft.Main.Add("ARG", name, value);
            }
        }

        SortedSet<int> back = WrittenBackLocals(block, written);
        if (back.Count == 0)
        {
            return;
        }

        var locals = new List<string>();
        foreach (int local in back)
        {
            locals.Add("#" + local.ToString(CultureInfo.InvariantCulture));
        }

        block.Draft.Warnings.Add((DiagnosticCodes.FanucSharedLocalWritten,
            $"The subprogram {called.ToString(CultureInfo.InvariantCulture)} writes "
            + $"{string.Join(", ", locals)}, which it shares with its caller because a subprogram call opens no "
            + "local-variable level; the CALL of NCX restores the caller's V1..V33 at the return, so the write does "
            + "not reach the caller (D148, virtual machine 3.6)."));
    }

    // The locals #1..#33 that a called program of the file, and every program it calls at the same level, reads from
    // the caller, and the ones they write (D148). A G65 of the subprogram opens a level of its own and is not
    // followed, an external program has blocks the reader does not see, and a program that calls itself is walked
    // once. The walk follows the blocks in the order of the file and the calls where they stand, so that a local the
    // subprogram assigns before it reads it is not counted as read from the caller.
    private static void SharedLocalsOf(FanucBlock block, long called, SortedSet<int> read, SortedSet<int> written)
    {
        WalkLevel(block, called, read, written, [], []);
    }

    // The blocks of one program of the file and of the programs it calls at the same local-variable level, in the
    // order they run in, adding what they read from the caller to read and what they write to written; assigned
    // carries the locals the walk has written already and walked the programs it has walked (D148).
    private static void WalkLevel(FanucBlock block, long program, SortedSet<int> read, SortedSet<int> written,
        HashSet<int> assigned, HashSet<long> walked)
    {
        if (!walked.Add(program)
            || !block.Fanuc.Calls.Begins.TryGetValue(
                program.ToString(CultureInfo.InvariantCulture), out SourceBlock? begin))
        {
            return;
        }

        foreach (SourceBlock body in block.Fanuc.SectionOf(begin))
        {
            LocalsOf(body, read, written, assigned);
            foreach (FanucCall call in CallsOf(body, block.Machine))
            {
                if (!call.External && !call.Macro)
                {
                    WalkLevel(block, call.Program, read, written, assigned, walked);
                }
            }
        }
    }

    // Which of the locals an M98 subprogram writes it writes back for the caller (D148): the blocks of the caller
    // after the CALL, and the subprograms they call at the same level, read it without assigning it themselves
    // first, so the value they expect is the one the subprogram left. The CALL of NCX restores the caller's V1..V33
    // at the return instead (virtual machine 3.6), which is the loss the reader reports; a local the subprogram only
    // used as its own scratch reaches no block that reads it and is none.
    private static SortedSet<int> WrittenBackLocals(FanucBlock block, SortedSet<int> written)
    {
        if (written.Count == 0)
        {
            return written;
        }

        var read = new SortedSet<int>();
        var ignored = new SortedSet<int>();
        var assigned = new HashSet<int>();
        var walked = new HashSet<long>();
        bool after = false;
        foreach (SourceBlock body in block.Fanuc.SectionOf(block.Source))
        {
            if (!after)
            {
                after = body.Line == block.Line;
                continue;
            }

            LocalsOf(body, read, ignored, assigned);
            foreach (FanucCall call in CallsOf(body, block.Machine))
            {
                if (!call.External && !call.Macro)
                {
                    WalkLevel(block, call.Program, read, ignored, assigned, walked);
                }
            }
        }

        read.IntersectWith(written);
        return read;
    }

    // The locals #1..#33 one block reads from the state it began with and the ones it writes, word by word in the
    // order of the block: #n = e writes #n, and every other #n of the block, in the value of a word, in an
    // expression and in the index of an indirect variable, is a read (D148). A read counts as one from the caller
    // only where no write of the walk assigned it before, so that #1=0 before WHILE[#1 LT 3] is the subprogram's own
    // counter and not the caller's local; a block reads before it writes, as #1=#1+1 does. An indirect assignment,
    // #[#1+1]=5, names no local the reader can tell, so it assigns none here and a read after it still counts as the
    // caller's, which is what D148 says of a local the subprogram reads.
    private static void LocalsOf(SourceBlock block, SortedSet<int> read, SortedSet<int> written,
        HashSet<int> assigned)
    {
        foreach (SourceWord word in block.Words)
        {
            int? target = word.Address == "#" && word.Expression is not null
                && long.TryParse(word.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long number)
                && IsLocal(number)
                    ? (int)number
                    : null;
            if (target is null)
            {
                AddLocals(word.Text, read, assigned);
            }

            AddLocals(word.Expression, read, assigned);
            if (target is int local)
            {
                written.Add(local);
                assigned.Add(local);
            }
        }
    }

    // Every #n with n in 1..33 of a text, the value of a word or an expression as the control writes it, that no
    // write of the walk assigned before (D148).
    private static void AddLocals(string? text, SortedSet<int> locals, HashSet<int> assigned)
    {
        if (text is null)
        {
            return;
        }

        for (int index = text.IndexOf('#', StringComparison.Ordinal); index >= 0;
            index = text.IndexOf('#', index + 1))
        {
            int end = index + 1;
            while (end < text.Length && char.IsAsciiDigit(text[end]))
            {
                end++;
            }

            if (end > index + 1
                && long.TryParse(text.AsSpan(index + 1, end - index - 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out long local) && IsLocal(local)
                && !assigned.Contains((int)local))
            {
                locals.Add((int)local);
            }
        }
    }

    // #1..#33 are the local variables of one call level, which NCX names V1..V33 (fanuc 7; language 4.9, D33).
    private static bool IsLocal(long variable)
    {
        return variable is >= 1 and <= 33;
    }

    // The letters of G65 are the arguments; the callee reads A as #1, which NCX names V1 (language 4.9, controllers
    // fanuc.md 7).
    private static void ReadArguments(FanucBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "N")
            {
                continue;
            }

            if (!s_arguments.TryGetValue(word.Address, out int local))
            {
                block.Draft.KeepAsRaw($"{word.Address} is no argument of G65 (controllers fanuc.md 7)");
                return;
            }

            block.MarkRead(word);
            if (ValueOf(block, word) is Value value)
            {
                block.Draft.Main.Add("ARG", "V" + local.ToString(CultureInfo.InvariantCulture), value);
            }
        }
    }

    // A subprogram of the file is called by its NAME, the number of its O line; any other program is an external one,
    // called by its file name, O and the four digits of the program (language 4.9, CALL; controllers fanuc.md 1).
    private static Value Target(FanucBlock block, long program)
    {
        return block.Fanuc.ProgramNumbers.Contains(program)
            ? new IntegerValue(program, program.ToString(CultureInfo.InvariantCulture))
            : External(program);
    }

    private static StringValue External(long program)
    {
        return new StringValue("O" + program.ToString("0000", CultureInfo.InvariantCulture));
    }

    // #n = ... assigns, GOTO n jumps, IF [ ] GOTO n jumps under the condition, IF [ ] THEN #n = ... assigns under it
    // (controllers fanuc.md 7, controller-mapping 6).
    private static void ReadFlow(FanucBlock block)
    {
        SourceWord? condition = block.Take("IF");
        SourceWord? then = block.Take("THEN");
        SourceWord? jump = block.Take("GOTO");
        SourceWord? assignment = block.Take("#");
        if (condition is not null && then is not null)
        {
            ReadIfThen(block, condition, assignment);
            return;
        }

        if (then is not null || (condition is not null && jump is null))
        {
            block.Draft.KeepAsRaw("IF needs GOTO or THEN (controllers fanuc.md 7)");
            return;
        }

        if (jump is not null)
        {
            ReadJump(block, jump, condition);
        }

        if (assignment is not null)
        {
            Assign(block, assignment, block.Draft.Main);
        }
    }

    // GOTO n is JUMP=n to the block Nn, which the structure pass labels; IF [ ] GOTO n adds the condition (language
    // 4.9, controller-mapping 6).
    private static void ReadJump(FanucBlock block, SourceWord jump, SourceWord? condition)
    {
        if (jump.Number is not decimal target || target != decimal.Truncate(target) || target < 0)
        {
            block.Draft.KeepAsRaw("a GOTO to a computed block has no NCX label (language 4.9, JUMP)");
            return;
        }

        block.Draft.Main.Add("JUMP", JumpTo(block, decimal.ToInt64(target)));
        if (condition is not null && ConditionOf(block, condition, negated: false) is Value test)
        {
            block.Draft.Main.Add("IF", test);
        }
    }

    // JUMP=n to the block Nn, and JUMP=END where Nn holds only the M30 or M2 that ends the program: JUMP=END continues
    // at PROGRAM=END (controller-mapping 1, JUMP=END; language 4.9). FanucReader.StructureOf decides which while it
    // lays out the labels, and writes no LABEL for such a jump.
    private static Value JumpTo(FanucBlock block, long label)
    {
        return block.Fanuc.JumpsToTheEnd.Contains(block.Line)
            ? new IdentValue("END")
            : new IntegerValue(label, label.ToString(CultureInfo.InvariantCulture));
    }

    // The NT NURSE branch commands of the builder nakamura are JUMP with IF, not RAW (controller-mapping 6, JUMP + IF;
    // controller-mapping 9): G480 I{n} jumps to the block Nn, G471 to G476 D Q I{n} jump when D compares with Q, G481
    // to G486 Q R I{n} when the variable #q compares with the variable #r, the six codes in the order equal, not equal,
    // >=, <=, >, <. Every other word of the block would be an argument of the macro that NCX does not know, and keeps
    // the block as RAW (D5).
    private static bool ReadBranchCommand(FanucBlock block)
    {
        SourceWord? command = null;
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "G" && NativeCode.Of(word) is string code && IsBranchCommand(block.Machine, code))
            {
                command = word;
                break;
            }
        }

        if (command is null)
        {
            return false;
        }

        block.MarkRead(command);
        string branch = NativeCode.Of(command)!;
        if (block.Take("I") is not SourceWord target || WholeNumber(target) is not long label)
        {
            block.Draft.KeepAsRaw($"{branch} names no block I{{n}} to jump to (controller-mapping 6)");
            return true;
        }

        string? condition = null;
        if (ComparisonOf(branch) is string comparison)
        {
            condition = branch.StartsWith("G47", StringComparison.Ordinal)
                ? Compared(block, "D", "Q", comparison, variables: false)
                : Compared(block, "Q", "R", comparison, variables: true);
            if (condition is null)
            {
                block.Draft.KeepAsRaw($"{branch} needs the two values it compares (controller-mapping 6)");
                return true;
            }
        }

        List<SourceWord> unread = block.Unread();
        if (unread.Count > 0)
        {
            block.Draft.KeepAsRaw($"{unread[0].Address}{unread[0].Text} is no argument of the branch command {branch}");
            return true;
        }

        block.Draft.Main.Add("JUMP", JumpTo(block, label));
        if (condition is not null && ConditionOf(block, condition, negated: false) is Value test)
        {
            block.Draft.Main.Add("IF", test);
        }

        return true;
    }

    // The comparison of G471 to G476 and of G481 to G486 in custom macro B, EQ to LT; null for another code.
    private static string? ComparisonOf(string code)
    {
        for (int index = 0; index < s_comparisons.Length; index++)
        {
            string number = (index + 1).ToString(CultureInfo.InvariantCulture);
            if (code == "G47" + number || code == "G48" + number)
            {
                return s_comparisons[index];
            }
        }

        return null;
    }

    // The condition of a branch command in the syntax of custom macro B, so that the variables map as in every other
    // condition (language 4.12, D51): [D EQ Q] with the values as written, [#q EQ #r] with the variables that Q and R
    // name by number; null when a word is missing or does not name a variable.
    private static string? Compared(FanucBlock block, string left, string right, string comparison, bool variables)
    {
        SourceWord? first = block.Take(left);
        SourceWord? second = block.Take(right);
        if (first is null || second is null)
        {
            return null;
        }

        if (!variables)
        {
            return "[" + (first.Expression ?? first.Text) + " " + comparison + " " + (second.Expression ?? second.Text)
                + "]";
        }

        return WholeNumber(first) is long q && WholeNumber(second) is long r
            ? string.Create(CultureInfo.InvariantCulture, $"[#{q} {comparison} #{r}]")
            : null;
    }

    // IF [c] THEN #n = e is lowered to a jump over the assignment when c does not hold (controller-mapping 6, the
    // Fanuc structured forms lowered to LABEL, JUMP and IF; phase 3, P3-02).
    private static void ReadIfThen(FanucBlock block, SourceWord condition, SourceWord? assignment)
    {
        if (assignment is null)
        {
            block.Draft.KeepAsRaw("IF THEN needs an assignment #n = ... (controllers fanuc.md 7)");
            return;
        }

        string skip = "IF_" + block.Line.ToString(CultureInfo.InvariantCulture);
        if (ConditionOf(block, condition, negated: true) is not Value test)
        {
            return;
        }

        block.Draft.Main.Add("JUMP", new IdentValue(skip)).Add("IF", test);
        var assigned = new DraftBlock();
        Assign(block, assignment, assigned);
        block.Draft.After.Add(assigned);
        block.Draft.After.Add(new DraftBlock().Add("LABEL", new IdentValue(skip)));
    }

    // #n = e: local, common and permanent variables are Vn (D33); a system variable is the control's and a program
    // does not assign it (language 4.12, virtual machine 5).
    private static void Assign(FanucBlock block, SourceWord assignment, DraftBlock target)
    {
        if (assignment.Expression is null)
        {
            block.Draft.KeepAsRaw($"#{assignment.Text} without = is no statement of custom macro B");
            return;
        }

        if (!long.TryParse(assignment.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long variable))
        {
            block.Draft.KeepAsRaw("the indirect variable #[...] has no NCX name (language 4.9)");
            return;
        }

        if (variable is 0 or >= 1000)
        {
            block.Draft.KeepAsRaw(
                $"#{assignment.Text} is a variable of the control, which the program does not assign (language 4.12)");
            return;
        }

        Value? number = new SourceWord { Address = "", Text = assignment.Expression }.ToNcxNumber();
        Value? value = number ?? ExpressionOf(block, assignment.Expression);
        if (value is not null)
        {
            target.Add("VAR", "V" + variable.ToString(CultureInfo.InvariantCulture), value);
        }
    }

    // WHILE [c] DOm ... ENDm, and DOm ... ENDm without a condition, are lowered to a label at the head, a jump out when
    // c does not hold, and a jump back at the end (controller-mapping 6, structured loops; controllers fanuc.md 7).
    private static void ReadLoops(FanucBlock block)
    {
        SourceWord? loop = block.Take("WHILE");
        SourceWord? head = block.Take("DO");
        SourceWord? end = block.Take("END");
        if (loop is not null && head is null)
        {
            block.Draft.KeepAsRaw("WHILE needs DO (controllers fanuc.md 7)");
            return;
        }

        if (head is not null)
        {
            string label = (loop is null ? "DO_" : "WHILE_") + block.Line.ToString(CultureInfo.InvariantCulture);
            block.Draft.Before.Add(new DraftBlock().Add("LABEL", new IdentValue(label)));
            if (loop is not null && ConditionOf(block, loop, negated: true) is Value test)
            {
                block.Draft.Before.Add(new DraftBlock().Add("JUMP", new IdentValue(label + "_END")).Add("IF", test));
            }

            block.Fanuc.Loops.Add(new FanucLoop(LoopNumber(head), label));
            block.Fanuc.ForgetPositions();
            block.Fanuc.ForgetWritten();
            FanucJoins.AtLoopHead(block.Fanuc, block.Source, LoopNumber(head), block.Machine);
        }

        if (end is not null)
        {
            ReadLoopEnd(block, end);
        }
    }

    // ENDm closes the innermost open loop of number m.
    private static void ReadLoopEnd(FanucBlock block, SourceWord end)
    {
        int number = LoopNumber(end);
        List<FanucLoop> loops = block.Fanuc.Loops;
        for (int index = loops.Count - 1; index >= 0; index--)
        {
            if (loops[index].Number != number)
            {
                continue;
            }

            string head = loops[index].Head;
            loops.RemoveRange(index, loops.Count - index);
            block.Draft.After.Add(new DraftBlock().Add("JUMP", new IdentValue(head)));
            block.Draft.After.Add(new DraftBlock().Add("LABEL", new IdentValue(head + "_END")));
            block.Fanuc.ForgetPositions();
            block.Fanuc.ForgetWritten();
            return;
        }

        block.Diagnostics.Error(block.Line, DiagnosticCodes.FanucLoopEndWithoutDo,
            $"END{end.Text} closes no WHILE ... DO{end.Text} of its program; the block is kept as RAW (controllers "
            + "fanuc.md 7).");
        block.Draft.KeepAsRaw($"END{end.Text} without DO{end.Text}");
    }

    private static int LoopNumber(SourceWord word)
    {
        return word.Number is decimal number ? decimal.ToInt32(decimal.Truncate(number)) : 0;
    }

    // The condition of IF or WHILE, [#503 EQ 0], without the brackets that the statement puts around it, as an NCX
    // expression; negated with NOT for the jump out of a loop or over an assignment.
    private static ExprValue? ConditionOf(FanucBlock block, SourceWord word, bool negated)
    {
        return ConditionOf(block, word.Expression ?? "", negated);
    }

    private static ExprValue? ConditionOf(FanucBlock block, string condition, bool negated)
    {
        if (condition.StartsWith('[') && condition.EndsWith(']'))
        {
            condition = condition.Substring(1, condition.Length - 2);
        }

        string? text = FanucExpression.ToText(condition, block, out string? problem);
        if (text is null)
        {
            block.Draft.KeepAsRaw(problem!);
            return null;
        }

        ExprValue? value = FanucExpression.Parse(negated ? "NOT (" + text + ")" : text, block, out problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw(problem!);
        }

        return value;
    }

    private static ExprValue? ExpressionOf(FanucBlock block, string fanuc)
    {
        string? text = FanucExpression.ToText(fanuc, block, out string? problem);
        ExprValue? value = text is null ? null : FanucExpression.Parse(text, block, out problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw(problem!);
        }

        return value;
    }

    // The digits of a program number as written, P0100 as 0100, without a sign or a trailing dot; empty when the word
    // is no whole number.
    private static string Digits(SourceWord word)
    {
        string text = word.Text.TrimEnd('.');
        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return "";
            }
        }

        return text;
    }
}
