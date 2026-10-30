using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The walk back from a Fanuc cycle block to the "before" line of its catalog entry, M29 S of rigid tapping in front
/// of G84 (machine-config 6; controllers fanuc.md 9 rule 9; D256, D360, D383-D386): it marks the blocks that may hold
/// the line, or why the reader cannot tell, in the facts of the file.
/// </summary>
internal sealed class FanucBeforeLineWalk
{
    private readonly FanucState _fanuc;
    private readonly MachineConfig _machine;
    private readonly IReadOnlyDictionary<string, int> _modalGroupOfCode;

    /// <summary>
    /// The walk over the blocks of a file.
    /// </summary>
    /// <param name="fanuc">The facts of the file.</param>
    /// <param name="machine">The machine with its cycle catalog and its axes.</param>
    /// <param name="modalGroupOfCode">The modal group of every code of the machine's G-code system.</param>
    public FanucBeforeLineWalk(FanucState fanuc, MachineConfig machine,
        IReadOnlyDictionary<string, int> modalGroupOfCode)
    {
        _fanuc = fanuc;
        _machine = machine;
        _modalGroupOfCode = modalGroupOfCode;
    }

    /// <summary>
    /// Marks the blocks in front of a cycle block that may hold the "before" line of its entry (machine-config 6, D256,
    /// D360).
    /// </summary>
    /// <param name="cycleIndex">The index of the cycle block among the blocks of the file.</param>
    /// <param name="entry">The NCX name of the entry, EntryOf.</param>
    // The blocks in front of a cycle block that may hold the "before" line of its entry (machine-config 6, D256).
    // The Fanuc control forbids only an S command or an axis movement between M29 and G84, alarm PS0203, and names
    // M29 S "before a tapping command", not directly before it (B-64484EN-2 p.103, the three ways into rigid mode;
    // p.104, M29): Doosan programs M29S500P12 / M177 / G84 (MX-Series CNC-Programmierbuch p.131), and Mori programs
    // the line in the block of the positioning move in front of the cycle, "Z30.0 M329 S400;" then G84 (NTX1000SZM
    // p.281-282). So the walk goes back from the cycle block over the blocks with neither an S word nor an axis word
    // and ends at the first block with one, which is a place of the line as well, since PS0203 concerns only what
    // stands between the M29 block and G84.
    // It ends without the line at the head of a program, at a block with G80, a canned-cycle code or a group-01
    // code, which cancel the rigid tapping mode (0i-D Connection Manual B-64303EN-1 p.927, 10.13.16.8; B-64484EN-2
    // p.104, the cancel clears S to 0), and at the program end M2 or M30, whose reset cancels it as well (B-64484EN
    // p.297, p.1135; B-64484EN-2 p.103, Reset) - the block of an earlier cycle among them, whose own M29 S therefore
    // does not reach a G84 after its G80. It ends unseen, with no place of the line, where the reader does not see
    // what runs in front of a block or what a block does: a jump enters the cycle block or a block of the walk, a
    // block is skipped by the operator's switch, a block calls, returns or branches, or the walk reaches the head of a
    // section a block of the file calls. These are tested on every block of the walk before its S and axis words, so
    // that they cover the line block itself.
    // D383: a block that may move an axis without an axis word - the tool change M6 or a T word, the tool length
    // compensation G43, G44, G49 - the manual neither allows nor forbids between M29 and G84, and whether it works
    // depends on the machine; the control does not cancel the mode for it on its own. The walk ends there unseen, so
    // the cycle stays RAW with the "does not see" reason rather than reading TAP or the other machining.
    // D385: M3, M4, M5 and M19 between the M29 S line and the G84 are handled by the machine's PMC and may raise an
    // alarm or drop the rigid mode, so the walk ends unseen there too, with a reason that names the spindle code; a
    // spindle code before the line, S500 M3 / M29 S500 / G84, is unaffected.
    // A block with the entry's own native code, the G84 of an earlier cycle block of the same run, ends the walk with
    // no place of the line and no mark: whether the run was entered with the line is the reading of that cycle block
    // (FanucCycles.WithItsBeforeLine, the previous cycle of the run). It is tested before the cancel codes, of which
    // G84 is one.
    // D384: whether the line holds where its own block carries a group-01 code, "G0 Z10. M29 S500;" in front of G84,
    // the manuals do not say: they state the group-01 cancel only after tapping (B-64303EN-1 p.927), and the one
    // builder example of a move in the M29 block has no G code (Mori NTX1000SZM p.281-282). The walk ends at such a
    // block with a reason of its own (BeforeLineWithACancel), so the cycle stays RAW; a modal move in the M29 block
    // with no G code, "Z10. M29 S500;", stays a valid place for the line.
    // D386: a cycle block that carries a group-01 code is no cycle (EntryOf), so the line in front of it gets no
    // mark.
    public void Mark(int cycleIndex, string entry)
    {
        IReadOnlyList<SourceBlock> blocks = _fanuc.Blocks;
        SourceBlock cycle = blocks[cycleIndex];
        var lines = new List<int>();
        _fanuc.BeforeLinesOf[cycle.Line] = lines;
        CycleEntry? catalogEntry = _machine.CycleCatalog.Find(entry);
        string before = catalogEntry?.Before ?? "";
        if (IsJumpTarget(cycle))
        {
            DecideFromTheFlow(cycleIndex, entry, catalogEntry, lines);
            return;
        }

        string? spindle = null;
        bool lineBehindTheSpindle = false;
        int? stop = null;
        for (int index = cycleIndex - 1; index >= 0; index--)
        {
            SourceBlock block = blocks[index];
            if (block.Find("O") is SourceWord program)
            {
                if (program.Number is not decimal number)
                {
                    _fanuc.BeforeLineUnseen.Add(cycle.Line);
                    lines.Clear();
                }
                else if (_fanuc.Calls.IsCalled(
                    decimal.ToInt64(decimal.Truncate(number)).ToString(CultureInfo.InvariantCulture)))
                {
                    lines.Clear();
                    DecideFromTheFlow(cycleIndex, entry, catalogEntry, lines);
                    return;
                }

                break;
            }

            // D360: a jump into a block of the walk is decided from the flow; any other block the reader does not see
            // past ends the walk unseen.
            if (HidesWhatRunsBefore(block))
            {
                lines.Clear();
                if (!block.BlockSkip && IsJumpTarget(block) && !HidesWhatRunsBeforeApartFromTheJump(block))
                {
                    DecideFromTheFlow(cycleIndex, entry, catalogEntry, lines);
                    return;
                }

                _fanuc.BeforeLineUnseen.Add(cycle.Line);
                break;
            }

            // The entry's own native code re-issued in the run: the cycle block before it holds the mode or not by
            // its own reading, and FanucCycles.WithItsBeforeLine continues the run of a cycle entered with the line.
            if (catalogEntry is not null && HasNativeCode(block, catalogEntry.Native))
            {
                break;
            }

            if (CancelsTheMode(block) && CarriesTheCodesOf(block, before))
            {
                _fanuc.BeforeLineUnseen.Add(cycle.Line);
                _fanuc.BeforeLineWithACancel.Add(cycle.Line);
                lines.Clear();
                break;
            }

            if (CancelsTheMode(block))
            {
                stop = MovesOrSetsTheSpeed(block) ? index : null;
                break;
            }

            lines.Add(block.Line);
            lineBehindTheSpindle |= spindle is not null && CarriesTheCodesOf(block, before);
            if (MovesOrSetsTheSpeed(block))
            {
                stop = CarriesTheCodesOf(block, before) ? null : index;
                break;
            }

            spindle ??= SpindleCodeOf(block);
        }

        // D256, D359: a line further back than the S or axis block that stopped the walk is no G84 without the line:
        // the control raises PS0203 for an S command or an axis movement between M29 and G84 (B-64484EN-2 p.104,
        // p.108), so the blocks stay RAW with that reason instead of reading TAP_COMPENSATING.
        if (stop is int stopIndex && LineBeyond(stopIndex, before, catalogEntry?.Native) is int alarmLine)
        {
            _fanuc.BeforeLineUnseen.Add(cycle.Line);
            _fanuc.BeforeLineAlarm[cycle.Line] = alarmLine;
            lines.Clear();
        }

        // D385: a line further back than a spindle code reaches the G84 only past that code.
        if (lineBehindTheSpindle)
        {
            _fanuc.BeforeLineUnseen.Add(cycle.Line);
            _fanuc.BeforeLineSpindle[cycle.Line] = spindle!;
            lines.Clear();
        }

        foreach (int line in lines)
        {
            _fanuc.CycleBefore[line] = entry;
        }
    }

    // The line of a block before the stop of the walk that carries the codes of the before line with nothing between
    // them that ends the mode it switches on (G80, a canned-cycle code, M2, M30, the entry's own native) or hides what
    // runs; null for none.
    private int? LineBeyond(int stopIndex, string before, string? native)
    {
        IReadOnlyList<SourceBlock> blocks = _fanuc.Blocks;
        for (int index = stopIndex - 1; index >= 0; index--)
        {
            SourceBlock block = blocks[index];
            if (block.Find("O") is not null || HidesWhatRunsBefore(block)
                || (native is not null && HasNativeCode(block, native)))
            {
                return null;
            }

            if (CarriesTheCodesOf(block, before))
            {
                return block.Line;
            }

            if (CancelsTheMode(block) && !HasGroupOneMotion(block))
            {
                return null;
            }
        }

        return null;
    }

    private static bool HasGroupOneMotion(SourceBlock block)
    {
        return HasCode(block, "G0") || HasCode(block, "G1") || HasCode(block, "G2") || HasCode(block, "G3");
    }

    // D360: a cycle block whose before line the reader cannot see directly, because a jump enters the cycle block or
    // a block in front of it, or because the cycle block begins a section a block of the file calls, is decided from
    // the flow, as the control does: the mode is in force when the line stands directly before every block that
    // reaches the cycle block - before each CALL of the section the block begins, before every jump to the label a
    // jump enters, and before the block in front of it where the flow also runs through. The cycle then reads as
    // the entry names it (CYCLE=TAP), and the line is not written again as an M function. Where one path reaches the
    // block without the line and another with it, the blocks stay RAW with the reason; where no path has the line,
    // the cycle is the one without it, as for a block whose walk the reader sees (D359). A path back to the cycle
    // block itself, the jump back of a loop over the run, carries the mode the run has and decides nothing. No new
    // word, key or code, and nothing leans on the open D195.
    private void DecideFromTheFlow(int cycleIndex, string entry, CycleEntry? catalogEntry, List<int> lines)
    {
        int cycleLine = _fanuc.Blocks[cycleIndex].Line;
        var paths = new FanucLinePaths
        {
            CycleIndex = cycleIndex,
            Before = catalogEntry?.Before ?? "",
            Native = catalogEntry?.Native,
        };
        IntoBlock(cycleIndex, paths);
        foreach (int line in paths.Lines)
        {
            // The line must be read before the cycle block, so a line behind it in the file is not seen.
            paths.Unseen |= line > cycleLine;
        }

        if (paths.Unseen || (!paths.InForce && !paths.Without))
        {
            _fanuc.BeforeLineUnseen.Add(cycleLine);
            return;
        }

        if (paths.InForce && paths.Without)
        {
            _fanuc.BeforeLineUnseen.Add(cycleLine);
            _fanuc.BeforeLineNotOnEveryPath.Add(cycleLine);
            return;
        }

        if (paths.InForce)
        {
            lines.AddRange(paths.Lines);
            foreach (int line in paths.Lines)
            {
                _fanuc.CycleBefore[line] = entry;
            }
        }
    }

    // The paths into a block: the block in front of it where the flow runs through, and every jump to its label.
    private void IntoBlock(int index, FanucLinePaths paths)
    {
        IReadOnlyList<SourceBlock> blocks = _fanuc.Blocks;
        SourceBlock target = blocks[index];
        if (index > 0 && !EndsTheFlow(blocks[index - 1]))
        {
            Back(index - 1, false, paths);
        }
        else if (index == 0)
        {
            paths.Without = true;
        }

        if (!IsJumpTarget(target))
        {
            return;
        }

        string label = FanucState.LabelOf(target.Find("N")!);
        bool found = false;
        foreach (int jump in JumpsInTheSectionOf(index, label))
        {
            found = true;
            Back(jump, true, paths);
        }

        // A label that some other branch enters, M99 P or a builder's branch command, is not followed.
        paths.Unseen |= !found;
    }

    // One path, walked back from a block that runs before the one the path enters, the block included; the block of a
    // jump or a call being followed is passed over for its branch.
    private void Back(int start, bool branch, FanucLinePaths paths)
    {
        IReadOnlyList<SourceBlock> blocks = _fanuc.Blocks;
        bool spindle = false;
        for (int index = start; index >= 0; index--)
        {
            if (index == paths.CycleIndex || !paths.Visited.Add(index))
            {
                return;
            }

            SourceBlock block = blocks[index];
            if (block.Find("O") is SourceWord program)
            {
                Callers(program, paths);
                return;
            }

            bool followed = branch && index == start;
            if (block.BlockSkip || (!followed && HidesWhatRunsBeforeApartFromTheJump(block)))
            {
                paths.Unseen = true;
                return;
            }

            if ((paths.Native is not null && HasNativeCode(block, paths.Native))
                || (CancelsTheMode(block) && CarriesTheCodesOf(block, paths.Before)))
            {
                paths.Unseen = true;
                return;
            }

            if (CancelsTheMode(block) || (!CarriesTheCodesOf(block, paths.Before) && MovesOrSetsTheSpeed(block)))
            {
                // A line beyond an S or axis block is alarm PS0203 on this path, which the reader keeps RAW.
                bool alarm = MovesOrSetsTheSpeed(block) && LineBeyond(index, paths.Before, paths.Native) is not null;
                paths.Unseen |= alarm;
                paths.Without |= !alarm;
                return;
            }

            if (CarriesTheCodesOf(block, paths.Before))
            {
                paths.Lines.Add(block.Line);
                paths.InForce |= !spindle;
                paths.Unseen |= spindle;
                return;
            }

            spindle |= SpindleCodeOf(block) is not null;
            if (IsJumpTarget(block))
            {
                IntoBlock(index, paths);
                return;
            }
        }

        paths.Without = true;
    }

    // The paths into the head of a section: each CALL of it in the file; a section no block calls is the program,
    // whose head nothing runs before.
    private void Callers(SourceWord program, FanucLinePaths paths)
    {
        if (program.Number is not decimal number)
        {
            paths.Unseen = true;
            return;
        }

        long called = decimal.ToInt64(decimal.Truncate(number));
        bool found = false;
        IReadOnlyList<SourceBlock> blocks = _fanuc.Blocks;
        for (int index = 0; index < blocks.Count; index++)
        {
            foreach (FanucCall call in FanucMacro.CallsOf(blocks[index], _machine))
            {
                if (call.Program != called || call.External)
                {
                    continue;
                }

                found = true;
                if (call.Repeats)
                {
                    // A repeated call enters the section again from its own end.
                    paths.Unseen = true;
                    return;
                }

                Back(index, true, paths);
            }
        }

        paths.Without |= !found;
    }

    // The blocks of the section of a block that jump to a label with GOTO.
    private List<int> JumpsInTheSectionOf(int index, string label)
    {
        IReadOnlyList<SourceBlock> blocks = _fanuc.Blocks;
        int first = index;
        while (first > 0 && blocks[first].Find("O") is null)
        {
            first--;
        }

        var jumps = new List<int>();
        for (int other = first + 1; other < blocks.Count && blocks[other].Find("O") is null; other++)
        {
            if (blocks[other].Find("GOTO") is SourceWord jump && jump.Number is not null
                && FanucState.LabelOf(jump) == label)
            {
                jumps.Add(other);
            }
        }

        return jumps;
    }

    // Whether the flow does not run on from a block into the next: an unconditional GOTO, M99, M30 or M2.
    private static bool EndsTheFlow(SourceBlock block)
    {
        if (block.Find("GOTO") is not null && block.Find("IF") is null)
        {
            return true;
        }

        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "M" && NativeCode.Of(word) is "M99" or "M30" or "M2")
            {
                return true;
            }
        }

        return false;
    }

    // HidesWhatRunsBefore without the jump into the block, which the flow decides (D360).
    private bool HidesWhatRunsBeforeApartFromTheJump(SourceBlock block)
    {
        if (block.BlockSkip || FanucMacro.CallsOf(block, _machine).Count > 0)
        {
            return true;
        }

        return HidesByItsWords(block);
    }

    // Whether a block carries G80, a code of the canned cycles, group 09, or a code of group 01, which cancel the
    // rigid tapping mode (0i-D Connection Manual B-64303EN-1 p.927, 10.13.16.8; controllers fanuc.md 3), or the
    // program end M2 or M30, which ends in the reset state (B-64484EN p.297, p.1135) - and a reset cancels the mode
    // (B-64484EN-2 p.103, Reset) - or returns to the start of the program, so that no G84 after it runs in the mode.
    private bool CancelsTheMode(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "M" && NativeCode.Of(word) is "M2" or "M30")
            {
                return true;
            }

            if (word.Address == "G" && NativeCode.Of(word) is string code
                && (code == "G80" || (_modalGroupOfCode.TryGetValue(code, out int group)
                    && group is FanucModalGroups.Motion or FanucModalGroups.Cycle)))
            {
                return true;
            }
        }

        return false;
    }

    // The spindle code of a block, M3, M4, M5 or M19, which D385 names between the M29 S line and the G84.
    private static string? SpindleCodeOf(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "M" && NativeCode.Of(word) is "M3" or "M4" or "M5" or "M19")
            {
                return NativeCode.Of(word);
            }
        }

        return null;
    }

    // Whether a block carries every code of a "before" line, the M29 of "M29 S{rpm}", so that it may be that line.
    /// <summary>
    /// Whether a block carries every G and M code of a "before" line, M29 of "M29 S{rpm}"; false for a line without
    /// one (machine-config 6, D256).
    /// </summary>
    /// <param name="block">The source block.</param>
    /// <param name="before">The before line of the catalog entry.</param>
    public static bool CarriesTheCodesOf(SourceBlock block, string before)
    {
        bool any = false;
        foreach (string token in before.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Contains('{', StringComparison.Ordinal) || token[0] is not ('G' or 'M'))
            {
                continue;
            }

            any = true;
            if (!HasNativeCode(block, token))
            {
                return false;
            }
        }

        return any;
    }

    private static bool HasNativeCode(SourceBlock block, string code)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address is "G" or "M" && NativeCode.Of(word) == code)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsJumpTarget(SourceBlock block)
    {
        return block.Find("N") is SourceWord label && _fanuc.JumpTargets.Contains(FanucState.LabelOf(label));
    }

    // Whether a block carries an S word or an axis word, what PS0203 forbids between M29 and G84 (B-64484EN-2 p.104).
    private bool MovesOrSetsTheSpeed(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "S" || IsAxisAddress(word.Address))
            {
                return true;
            }
        }

        return false;
    }

    // The letters of an axis word, as FanucAxes reads them: the [[axis]] table with its incremental letters, the
    // standard axes X Y Z A B C, the machine axes U V W, and H of system A, each with an extended number (D30, D93).
    private bool IsAxisAddress(string address)
    {
        if (address.Length == 0 || !char.IsAsciiLetterUpper(address[0]))
        {
            return false;
        }

        foreach (AxisDef axis in _machine.Axes)
        {
            if ((axis.Letter ?? axis.NcxName) == address || axis.IncrementalLetter == address)
            {
                return true;
            }
        }

        string letter = address.Substring(0, 1);
        if (address.Length > 1 && !address.Substring(1).All(char.IsAsciiDigit))
        {
            return false;
        }

        return "XYZABCUVW".Contains(letter, StringComparison.Ordinal)
            || (letter == "H" && _machine.Machine.GcodeSystem == GcodeSystem.A);
    }

    // Whether the reader cannot see past a block on the walk back from a cycle block: it is skipped by the
    // operator's switch, a jump enters it, it calls or returns or branches, or it may move an axis without an axis
    // word (see MarkBeforeLines).
    private bool HidesWhatRunsBefore(SourceBlock block)
    {
        return block.BlockSkip || IsJumpTarget(block) || FanucMacro.CallsOf(block, _machine).Count > 0
            || HidesByItsWords(block);
    }

    // The words that hide what runs before a block: a branch, a return, a call code, a tool change or a length
    // offset (D383).
    private static bool HidesByItsWords(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address is "GOTO" or "IF" or "WHILE" or "DO" or "END" or "T")
            {
                return true;
            }

            if (word.Address is "G" or "M" && NativeCode.Of(word) is "M98" or "M99" or "M198" or "M6" or "G65" or "G66"
                or "G66.1" or "G43" or "G44" or "G49")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The NCX name of the catalog entry whose cycle this block starts and that carries a "before" line; null for
    /// every other block, and for a cycle block with a group-01 code, which is no cycle (machine-config 6, D256, D386).
    /// </summary>
    /// <param name="block">A source block.</param>
    public string? EntryOf(SourceBlock block)
    {
        // D386: a cycle block with a group-01 code is no cycle, and the line in front of it is no part of one.
        if (HasCode(block, "G0") || HasCode(block, "G1") || HasCode(block, "G2") || HasCode(block, "G3"))
        {
            return null;
        }

        foreach (CycleEntry entry in _machine.CycleCatalog.Entries)
        {
            if (!string.IsNullOrEmpty(entry.Before) && HasCode(block, entry.Native))
            {
                return entry.Name;
            }
        }

        return null;
    }

    private static bool HasCode(SourceBlock block, string code)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "G" && NativeCode.Of(word) == code)
            {
                return true;
            }
        }

        return false;
    }
}
