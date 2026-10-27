using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;
using Ncx.Core.Writing;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// Reads Fanuc and ISO programs into canonical NCX (controllers fanuc.md 9; controller-mapping, the Fanuc column of
/// every section). The structure pass lays out the file from %, O, N, GOTO, M30, M2 and M99; the header of D34 follows
/// PROGRAM=BEGIN, and a subprogram runs with the state of its callers (FanucCallerState); then every block is resolved
/// against the source-side state and read concern by concern, each in a file of its own: the macro statements and calls
/// (FanucMacro), the frames (FanucFrames, FanucTilt), the tool and spindle words (FanucToolWords), the cycles
/// (FanucCycles), the motion (FanucMotion, FanucCorners, FanucPolarCoordinates) and the M codes (FanucBuilder). What
/// NCX cannot express stays RAW with a WARNING (D5).
/// </summary>
public sealed partial class FanucReader : ReaderBase
{
    // Oxxxx has four digits, eight on the 30i (controllers fanuc.md 1).
    private const decimal MaxProgramNumber = 99999999m;

    // The modal groups the reading of a block consults: the motion of group 01, the plane, G90/G91, the feed mode, the
    // return level of a cycle and G15/G16 (controllers fanuc.md 3, 9 rule 1).
    private static readonly int[] s_consultedGroups =
    [
        FanucModalGroups.Motion, FanucModalGroups.Plane, FanucModalGroups.Distance, FanucModalGroups.FeedMode,
        FanucModalGroups.ReturnLevel, FanucModalGroups.PolarCoordinates,
    ];

    private FanucState? _fanuc;
    private TemplateSet? _templates;

    // The channel of the file being read, from the command line or the file name (D223).
    private long? _fileChannel;

    /// <summary>
    /// The Fanuc family, Fanuc and the ISO dialects (controllers fanuc.md).
    /// </summary>
    public override Controller Controller => Controller.Fanuc;

    /// <summary>
    /// The tokenizer of Fanuc and ISO programs.
    /// </summary>
    protected override ISourceTokenizer Tokenizer { get; } = new FanucTokenizer();

    /// <summary>
    /// The modal groups of the G-code system of the machine (controllers fanuc.md 3).
    /// </summary>
    protected override IReadOnlyDictionary<string, int> ModalGroupOfCode =>
        FanucModalGroups.For(Machine.Machine.GcodeSystem);

    /// <summary>
    /// The calls of the whole file, before the structure pass lays it out: which sections a block of the file enters
    /// is a fact of the file, not of the blocks walked so far, because a caller may stand after the section it calls
    /// (D221; controller-mapping 6). The counting of the calls stays with the pass (FanucCalls.Expect).
    /// </summary>
    /// <param name="blocks">Every block of the file, in file order.</param>
    protected override void BeginStructure(IReadOnlyList<SourceBlock> blocks)
    {
        FanucState fanuc = Facts();
        _fileChannel = ChannelOfFile(blocks);
        foreach (SourceBlock block in blocks)
        {
            if (block.IsTrivia)
            {
                continue;
            }

            foreach (FanucCall call in FanucMacro.CallsOf(block, Machine))
            {
                if (!call.External)
                {
                    fanuc.Calls.Call(call.Name);
                }
            }
        }
    }

    /// <summary>
    /// The structure of a Fanuc block: % frames the file, O begins a program or a subprogram, N is a label, GOTO jumps,
    /// M30 and M2 end the program, M99 returns, M98 P, G65 P and the M200 P of the builder nakamura call, G70 to G76
    /// name a contour (controllers fanuc.md 1, 6, 7; controller-mapping 1, 5, 6, 8; language 4.7.1, 4.13); on a machine
    /// of the builder nakamura the I{n} of a G411 and of an NT NURSE branch command names a label too
    /// (controller-mapping 6 and 8), and the file name the channel (controller-mapping 7).
    /// </summary>
    /// <param name="block">A source block with words.</param>
    protected override SourceStructure StructureOf(SourceBlock block)
    {
        FanucState fanuc = Facts();
        fanuc.AddBlock(block);
        StructureRole role = StructureRole.None;
        long? number = null;
        long? channel = null;
        string? label = null;
        var labelsUsed = new List<string>();
        var calls = new List<string>();
        SourceWord? program = block.Find("P");
        SourceWord? returnsToTheBlock = null;
        foreach (SourceWord word in block.Words)
        {
            string? code = word.Address is "G" or "M" ? NativeCode.Of(word) : null;
            if (word.Address == "%")
            {
                role = StructureRole.FileFrame;
            }
            else if (word.Address == "O" && word.Number is decimal programNumber && programNumber >= 0
                && programNumber <= MaxProgramNumber)
            {
                role = StructureRole.SectionBegin;
                number = decimal.ToInt64(decimal.Truncate(programNumber));
                channel = _fileChannel;
                fanuc.Channel = channel;
                fanuc.ProgramNumbers.Add(number.Value);
                fanuc.SectionName = number.Value.ToString(CultureInfo.InvariantCulture);
                fanuc.Calls.Begins[fanuc.SectionName] = block;
                fanuc.BeginSectionLabels();
            }
            else if (word.Address == "N")
            {
                label = FanucState.LabelOf(word);
            }
            else if (word.Address == "GOTO" && word.Number is not null)
            {
                Jump(fanuc, block, FanucState.LabelOf(word), labelsUsed);
            }
            else if (code is "M30" or "M2")
            {
                role = StructureRole.ProgramEnd;
            }
            else if (code == "M99")
            {
                if (program is null)
                {
                    role = StructureRole.Return;
                }
                else
                {
                    returnsToTheBlock = program;
                }
            }
        }

        // D221: M99 Pn in a section that no block of the file calls returns to the block Nn of that same section, a
        // jump backwards, and reads as JUMP=n with LABEL=n on that block (decisions.md; controller-mapping 6). In a
        // subprogram a block of the file calls, P names a block of the caller, which no JUMP of NCX reaches, so the
        // reader keeps the block as RAW (FanucMacro.ReadCalls, D5). Which sections the file calls comes from the
        // pre-scan of BeginStructure, so that a caller standing after its section reads the same way as one before it.
        bool returnsToABlock = false;
        if (returnsToTheBlock is not null && FanucState.LabelOf(returnsToTheBlock) is string target
            && !fanuc.Calls.IsCalled(fanuc.SectionName ?? string.Empty) && fanuc.HasSectionLabel(target))
        {
            fanuc.ReturnsToTheBlock[block.Line] = target;
            Jump(fanuc, block, target, labelsUsed);

            // D211 and D221: the loop that such a block closes is an end the source writes, as a plain M99 loop is,
            // so the structure pass writes PROGRAM=END after it without the WARNING RDR010 (decisions.md;
            // controller-mapping 1, the PROGRAM=END row).
            returnsToABlock = true;
        }

        // M98 P, G65 P and the M200 P of the builder nakamura enter a subprogram of the file; the external call code
        // of the machine P calls a program from external memory (controller-mapping 6, 8; D322).
        foreach (FanucCall call in FanucMacro.CallsOf(block, Machine))
        {
            if (!call.External)
            {
                calls.Add(call.Name);
                fanuc.Calls.Expect(call.Name);
            }
        }

        AddBuilderJump(fanuc, block, labelsUsed);
        if (label is not null)
        {
            fanuc.AddLabel(label, HoldsOnlyTheEnd(block));
        }

        return new SourceStructure
        {
            Role = role,
            Number = number,
            Channel = channel,
            Label = label,
            LabelsUsed = labelsUsed,
            Calls = calls,
            Contour = FanucCycles.ContourOf(block, Machine, out _),
            ReturnsToABlock = returnsToABlock,
        };
    }

    // A GOTO or a branch command names a label; the block of the label is a jump target, and a jump to the block that
    // holds only the end of the program is JUMP=END without a label (FanucState.AddJump; controller-mapping 1,
    // JUMP=END).
    private static void Jump(FanucState fanuc, SourceBlock block, string label, List<string> labelsUsed)
    {
        fanuc.AddJump(block.Line, label, labelsUsed);
        fanuc.JumpTargets.Add(label);
    }

    // On a machine of the builder nakamura, I{n} of a G411 x1. I{n}, the jump on the part status that stays RAW, names
    // the block Nn, which keeps its LABEL so that the RAW jump finds it after a compile (controller-mapping 6 and 8;
    // D5, language 2 rule 8); I{n} of an NT NURSE branch command names it as a GOTO does (controller-mapping 6).
    private void AddBuilderJump(FanucState fanuc, SourceBlock block, List<string> labelsUsed)
    {
        if (!FanucMacro.IsBuilderNakamura(Machine) || block.Find("I") is not SourceWord target
            || FanucMacro.WholeNumber(target) is not long number)
        {
            return;
        }

        string label = number.ToString(CultureInfo.InvariantCulture);
        foreach (SourceWord word in block.Words)
        {
            string? code = word.Address == "G" ? NativeCode.Of(word) : null;
            if (code == "G411")
            {
                labelsUsed.Add(label);
                fanuc.JumpTargets.Add(label);
                return;
            }

            if (code is not null && FanucMacro.IsBranchCommand(Machine, code))
            {
                Jump(fanuc, block, label, labelsUsed);
                return;
            }
        }
    }

    // A block that holds only its number and the M30 or M2 that ends the program, without a block skip: a jump to it
    // ends the program (controller-mapping 1, JUMP=END).
    private static bool HoldsOnlyTheEnd(SourceBlock block)
    {
        if (block.BlockSkip)
        {
            return false;
        }

        int ends = 0;
        foreach (SourceWord word in block.Words)
        {
            string? code = word.Address == "M" ? NativeCode.Of(word) : null;
            if (code is "M30" or "M2")
            {
                ends++;
            }
            else if (word.Address != "N")
            {
                return false;
            }
        }

        return ends == 1;
    }

    // The channel of the file (D223; controller-mapping 7, CHANNEL; language 4.14): ncx convert --channel n, else the
    // suffix .P-n of the file name on any Fanuc machine, as Nakamura names the files of one job by path, O1000 and
    // O1000.P-2 (controllers fanuc.md 1); .P-1 is channel 1. Without either the reader writes no CHANNEL: channel 1
    // (language 4.1), unless a job binds the file (D15). A channel the machine file does not have is an ERROR, RDR107,
    // on the first O line; a machine file without channels has channel 1 (language 4.1).
    private long? ChannelOfFile(IReadOnlyList<SourceBlock> blocks)
    {
        long? channel = Options.Channel;
        if (channel is null)
        {
            Match path = PathSuffix().Match(Path.GetFileName(Diagnostics.File));
            channel = path.Success
                ? long.Parse(path.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture)
                : null;
        }

        IReadOnlyList<int> channels = Machine.Machine.Channels.Count == 0 ? [1] : Machine.Machine.Channels;
        if (channel is long number && !channels.Contains((int)number))
        {
            SourceBlock? first = blocks.FirstOrDefault(block => block.Find("O") is not null);
            Diagnostics.Error(first?.Line ?? 1, DiagnosticCodes.FanucChannelNotOfTheMachine,
                $"The file runs on channel {number}, which the machine file does not have (channels "
                + $"{string.Join(", ", channels)}); CHANNEL={number} is written as the file names it (D223; "
                + "controller-mapping 7).");
        }

        return channel;
    }

    // The path suffix of a file name, .P-2 of O1000.P-2, n = 1..10, with an extension after it or none (D223).
    [GeneratedRegex(@"\.P-(10|[1-9])(?:\.[A-Za-z0-9]+)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PathSuffix();

    /// <summary>
    /// A block leaves an M code undecided that neither the controller nor a table of the machine names (D40, D66).
    /// </summary>
    /// <param name="block">A source block with words.</param>
    protected override bool LeavesUndecided(SourceBlock block)
    {
        FanucBlock reading = Reading(block);
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "M" && !FanucBuilder.Names(reading, word))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A section starts from what its source says: a program from the initial state of the control with the header of
    /// D34, a subprogram from the state of its callers (virtual machine 3.9).
    /// </summary>
    /// <param name="kind">A program or a subprogram.</param>
    /// <param name="begin">The O line, the first block of a contour section, or the first block of a program without an
    /// O line.</param>
    protected override void BeginSection(SectionKind kind, SourceBlock begin)
    {
        FanucState fanuc = PreparedFacts();
        CloseLoops(fanuc, begin);
        fanuc.ForgetPositions();
        fanuc.ForgetWritten();
        if (kind != SectionKind.Program)
        {
            // A subprogram runs with the state of its caller at the CALL (virtual machine 3.9), a contour section with
            // the state at its cycle, whose contour follows it in the source (controllers fanuc.md 6; language 4.7.1).
            // D225: a subprogram starts from the facts every call agrees on, fact by fact, TIMES passes included
            // (FanucCallerState); a fact is unknown when the calls disagree, when a call is not reached yet or when
            // nothing calls the subprogram, and a block that depends on an unknown fact stays RAW with its WARNING
            // (D5), a bare M6 a bare TOOL. After the call the caller continues with the facts the subprogram may change
            // marked unknown.
            fanuc.Calls.EntryOf(SectionName(fanuc, begin), ConsultedGroups()).Restore(State, fanuc);
            Derive(fanuc);
            return;
        }

        StartProgram(fanuc);

        // Writers emit a complete header; configuration defaults apply to source readers only (D34).
        DraftBlock header = FanucHeader.Compose(fanuc, begin, Machine);
        Builder.Begin(begin.Line);
        foreach (Word word in header.Words)
        {
            Builder.Word(word.Key, word.Addr, word.Value);
        }

        Builder.End();

        // M99 in a program loops back to the label after the header (controller-mapping 6): the blocks after the
        // label run again with the state the loop left, so no modal word is taken as written.
        if (Loops(fanuc, begin))
        {
            fanuc.ForgetWritten();
        }
    }

    // A program starts from the initial state of the control, which its header writes (D34; virtual machine 2): no
    // modal code set, no cycle, an empty chain, no spindle selected, nothing preloaded, no F, every fact known.
    private void StartProgram(FanucState fanuc)
    {
        State.SetModalGroups(new Dictionary<int, string>());
        State.LastSpindle = null;
        State.Preloaded = null;
        fanuc.Unknowns.Clear();
        fanuc.Chain.Reset();
        fanuc.Cycle = null;
        fanuc.CssOn = false;
        fanuc.Rpm.Clear();
        fanuc.Tcpm = false;
        fanuc.VectorTcpm = false;
        fanuc.Polar = false;
        fanuc.ControlFeed = null;
        fanuc.WrittenFeed = null;
        Derive(fanuc);
    }

    // The name of a subprogram section: the name of the contour section whose first block it is, or the number of its
    // O line, "100" of O0100 (controller-mapping 6; language 4.7.1); null for a section the reader cannot name.
    private static string? SectionName(FanucState fanuc, SourceBlock begin)
    {
        if (fanuc.Calls.ContourBegins.TryGetValue(begin.Line, out string? contour))
        {
            return contour;
        }

        return begin.Find("O")?.Number is decimal number
            ? decimal.ToInt64(decimal.Truncate(number)).ToString(CultureInfo.InvariantCulture)
            : null;
    }

    // The consulted groups the G-code system of the machine has (controllers fanuc.md 3).
    private List<int> ConsultedGroups()
    {
        var present = new HashSet<int>(ModalGroupOfCode.Values);
        var groups = new List<int>();
        foreach (int group in s_consultedGroups)
        {
            if (present.Contains(group))
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    /// <summary>
    /// Reads a Fanuc block concern by concern and writes the NCX blocks it reads into, or keeps it as RAW (controllers
    /// fanuc.md 9).
    /// </summary>
    /// <param name="block">A source block with words that no reader rule claimed.</param>
    protected override void ReadBlock(SourceBlock block)
    {
        FanucState fanuc = PreparedFacts();
        FanucBlock reading = Reading(block);
        ReadStructureWords(reading);
        KnowGroupsOf(fanuc, block);

        // A label a jump enters is reached with a state the blocks before it do not tell (virtual machine 3.6).
        if (block.Find("N") is SourceWord number && fanuc.JumpTargets.Contains(FanucState.LabelOf(number)))
        {
            fanuc.ForgetPositions();
            fanuc.ForgetWritten();
        }

        string? reason = fanuc.ContourLines.Contains(block.Line)
            ? "a block of the contour of a multiple repetitive cycle is kept as RAW with its cycle"
            : FanucBuilder.ReasonToKeepAsRaw(reading);
        if (reason is not null)
        {
            reading.Draft.KeepAsRaw(reason);
        }
        else
        {
            // A reader never throws on bad input (code-guidelines 4): a number beyond the range the reader computes
            // with, a decimal of more than 28 digits, keeps its block as RAW.
            try
            {
                FanucMacro.Read(reading);
                FanucFrames.Read(reading);
                FanucCycles.ReadBeforeLine(reading);
                FanucToolWords.Read(reading);
                FanucCycles.Read(reading);
                FanucMotion.Read(reading);
                FanucBuilder.Read(reading);
                KeepUnreadAsRaw(reading);
            }
            catch (OverflowException)
            {
                reading.Draft.KeepAsRaw("a number of the block is beyond the range the reader computes with");
            }
        }

        // A "before" line in a block that ends RAW reaches the control as the RAW text, so it does not count for the
        // cycle: a compiler writes the line in front of every TAP run, and a TAP after it would give the line twice
        // (D256, D5). The cycle block then stays RAW with a reason that names the block (FanucCycles).
        if (reading.Draft.IsRaw && fanuc.CycleBeforeRead.Remove(block.Line))
        {
            fanuc.BeforeLineRaw.Add(block.Line);
        }

        Write(reading);
        EnterCalls(fanuc, reading);
        Derive(fanuc);

        // The shift of an expanded corner holds for the one block after the corner (FanucCorners.FromCornerEnd).
        if (fanuc.CornerLine == block.Line)
        {
            fanuc.CornerLine = null;
            fanuc.CornerShift.Clear();
        }
    }

    // The words of the file structure are the structure pass's: %, O, N, M30, M2 and M99 (language 4.13).
    private static void ReadStructureWords(FanucBlock reading)
    {
        foreach (SourceWord word in reading.Source.Words)
        {
            string? code = word.Address == "M" ? NativeCode.Of(word) : null;
            bool returns = code == "M99" && reading.Source.Find("P") is null;
            if (word.Address is "%" or "O" or "N" || code is "M30" or "M2" || returns)
            {
                reading.MarkRead(word);
            }
        }
    }

    // A block that sets a code of a modal group makes the group known (controllers fanuc.md 3; FanucUnknowns).
    private void KnowGroupsOf(FanucState fanuc, SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (NativeCode.Of(word) is string code && ModalGroupOfCode.TryGetValue(code, out int group))
            {
                fanuc.Unknowns.Groups.Remove(group);
            }
        }
    }

    // A word no concern reads is none the reader maps; the block is kept as RAW, nothing is dropped (D5).
    private static void KeepUnreadAsRaw(FanucBlock reading)
    {
        List<SourceWord> unread = reading.Unread();
        if (unread.Count > 0)
        {
            SourceWord word = unread[0];
            reading.Draft.KeepAsRaw($"{word.Address}{word.Text} has no NCX word the Fanuc reader maps here");
        }
    }

    private void Write(FanucBlock reading)
    {
        if (reading.Draft.RawReason is string reason)
        {
            // The modes of the control a RAW block switches on or off, and the entries of the chain it ends, which
            // stand in front of the RAW block (D244, D245).
            FanucFrames.KeepRawModes(reading);
            foreach (DraftBlock draft in reading.Draft.BesideRaw)
            {
                WriteDraft(reading, draft);
            }

            EmitRaw(reading.Source, reason);

            // The control runs the RAW block and the reader does not know what it changed; an F in it reaches the
            // control but no F word of NCX.
            reading.Fanuc.ForgetPositions();
            reading.Fanuc.ForgetWritten();
            reading.Fanuc.WrittenFeed = null;
            return;
        }

        // The WARNINGs that say what the written blocks hold (FanucDraft.Warnings).
        foreach ((string code, string message) in reading.Draft.Warnings)
        {
            Diagnostics.Warning(reading.Line, code, message);
        }

        foreach (DraftBlock draft in reading.Draft.InOrder())
        {
            WriteDraft(reading, draft);
        }
    }

    private void WriteDraft(FanucBlock reading, DraftBlock draft)
    {
        if (draft.IsEmpty)
        {
            return;
        }

        NcxBuilder builder = BeginBlock(reading.Source);
        if (draft.Verb is not null)
        {
            builder.Verb(draft.Verb, draft.VerbValue);
        }

        foreach (Word word in draft.Words)
        {
            builder.Word(word.Key, word.Addr, word.Value);
        }

        builder.End();
    }

    // A CALL enters its subprogram with the state of the caller (virtual machine 3.9), which the reader keeps for the
    // subprogram's section, and a cycle with a contour enters the contour's section so (language 4.7.1). The caller
    // continues with the state the subprogram left: the reader reads the subprogram after its caller, so what the
    // subprogram may change is unknown to it after the call (FanucChanges); a CALL with TIMES runs each pass from the
    // state the pass before left. Where the tool stands after a call is unknown, also after an external program, which
    // is not followed (virtual machine 3.9).
    private void EnterCalls(FanucState fanuc, FanucBlock reading)
    {
        bool written = !reading.Draft.IsRaw;
        if (written && fanuc.Contours.TryGetValue(reading.Line, out string? contour))
        {
            fanuc.Calls.Record(contour, FanucCallerState.Of(State, fanuc));
        }

        foreach (FanucCall call in FanucMacro.CallsOf(reading.Source, Machine))
        {
            fanuc.ForgetPositions();
            if (call.External || !fanuc.ProgramNumbers.Contains(call.Program))
            {
                continue;
            }

            FanucChanges changes = ChangesOf(call.Name);
            if (written)
            {
                FanucCallerState caller = FanucCallerState.Of(State, fanuc);
                fanuc.Calls.Record(call.Name, call.Repeats ? caller.After(changes) : caller);
            }

            changes.ApplyTo(State, fanuc);
        }
    }

    // What a subprogram of the file may change (FanucChanges), worked out once per subprogram.
    private FanucChanges ChangesOf(string name)
    {
        FanucState fanuc = Facts();
        if (fanuc.Calls.TryGetChanges(name, out FanucChanges? known))
        {
            return known;
        }

        FanucChanges changes = ChangesOf(name, new HashSet<string>(StringComparer.Ordinal));
        fanuc.Calls.KeepChanges(name, changes);
        return changes;
    }

    // The changes of the blocks of a subprogram and of the subprograms it calls, each subprogram once.
    private FanucChanges ChangesOf(string name, HashSet<string> visited)
    {
        FanucState fanuc = Facts();
        var changes = new FanucChanges();
        if (!visited.Add(name) || !fanuc.Calls.Begins.TryGetValue(name, out SourceBlock? begin))
        {
            return changes;
        }

        foreach (SourceBlock block in fanuc.SectionOf(begin))
        {
            AddChanges(changes, Reading(block));
            foreach (FanucCall call in FanucMacro.CallsOf(block, Machine))
            {
                if (!call.External && fanuc.ProgramNumbers.Contains(call.Program))
                {
                    changes.Add(ChangesOf(call.Name, visited));
                }
            }
        }

        return changes;
    }

    // What a block of a subprogram may change: the modal groups of its codes; the cycle, which a cycle code starts and
    // G80, G98, G99 and a code of group 01 end (FanucCycles); the chain of transforms (G52, G54 to G59, G54.1, G68,
    // G68.2, G69, G51.1, G50.1); the spindle (S, G96, G97, a spindle M code of the machine's tables); the tool (T, M6);
    // the F; polar interpolation and TCPM (G12.1, G13.1, G43.4, G43.5, G49 and the builder's codes of [transform]).
    private void AddChanges(FanucChanges changes, FanucBlock reading)
    {
        GcodeSystem? system = Machine.Machine.GcodeSystem;
        foreach (SourceWord word in reading.Source.Words)
        {
            string? code = word.Address is "G" or "M" ? NativeCode.Of(word) : null;
            int group = 0;
            if (code is not null && ModalGroupOfCode.TryGetValue(code, out group))
            {
                changes.Groups.Add(group);
            }

            bool starts = code is not null && FanucCycles.StartsACycle(code, system);
            changes.StartsCycle |= starts;
            changes.EndsCycle |= !starts
                && (code == "G80" || group is FanucModalGroups.Motion or FanucModalGroups.ReturnLevel);
            changes.Chain |= code is "G52" or "G54" or "G55" or "G56" or "G57" or "G58" or "G59" or "G54.1" or "G68"
                or "G68.2" or "G69" or "G51.1" or "G50.1";
            changes.Tool |= word.Address == "T" || code == "M6";
            changes.Feed |= word.Address == "F";
            changes.Spindle |= word.Address == "S" || code is "G96" or "G97"
                || (word.Address == "M" && FanucFunctions.Of(reading, word, out _)?.SpindleRole is not null);
            changes.Polar |= code is "G12.1" or "G13.1";
            changes.Tcpm |= code is "G43.4" or "G43.5" or "G49";
        }

        TransformTable? transform = Machine.Transform;
        changes.Polar |= FanucTemplates.TryMatch(reading, transform?.PolarOn, out _)
            || FanucTemplates.TryMatch(reading, transform?.PolarOff, out _);
        changes.Tcpm |= FanucTemplates.TryMatch(reading, transform?.TcpmOn, out _)
            || FanucTemplates.TryMatch(reading, transform?.TcpmOff, out _);
    }

    // The facts of the source-side state that a reader rule reads: absolute or incremental, the plane, the feed mode,
    // the active cycle (architecture 7); a group without a code gives none.
    private void Derive(FanucState fanuc)
    {
        SourceState state = State;
        state.Incremental = state.ActiveCode(FanucModalGroups.Distance) == "G91";
        state.Workplane = state.ActiveCode(FanucModalGroups.Plane) switch
        {
            "G17" => Workplane.XY,
            "G18" => Workplane.ZX,
            "G19" => Workplane.YZ,
            _ => null,
        };
        string? feedMode = state.ActiveCode(FanucModalGroups.FeedMode);
        bool systemA = Machine.Machine.GcodeSystem == GcodeSystem.A;
        if (feedMode == (systemA ? "G98" : "G94"))
        {
            state.FeedMode = FeedMode.PerMin;
        }
        else if (feedMode == (systemA ? "G99" : "G95"))
        {
            state.FeedMode = FeedMode.PerRev;
        }
        else
        {
            state.FeedMode = null;
        }

        state.ActiveCycle = fanuc.Cycle?.Code;
    }

    // A DO that no END closed before its section ends leaves a loop without its jump back.
    private void CloseLoops(FanucState fanuc, SourceBlock begin)
    {
        foreach (FanucLoop loop in fanuc.Loops)
        {
            Diagnostics.Warning(begin.Line, DiagnosticCodes.FanucLoopNotClosed,
                $"DO{loop.Number.ToString(CultureInfo.InvariantCulture)} at {loop.Head} is not closed by an END before "
                + "the next program; the loop has no jump back (controllers fanuc.md 7).");
        }

        fanuc.Loops.Clear();
    }

    // True when the program holds an M99 without P, the loop of controller-mapping 6.
    private static bool Loops(FanucState fanuc, SourceBlock begin)
    {
        for (int index = fanuc.IndexOf(begin) + 1; index >= 1 && index < fanuc.Blocks.Count; index++)
        {
            SourceBlock block = fanuc.Blocks[index];
            if (block.Find("O") is not null)
            {
                return false;
            }

            foreach (SourceWord word in block.Words)
            {
                if (word.Address == "M" && NativeCode.Of(word) == "M99" && block.Find("P") is null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private FanucBlock Reading(SourceBlock block)
    {
        FanucState fanuc = Facts();
        return new FanucBlock(block, State, fanuc, Machine, _templates!, Diagnostics);
    }

    // The facts of the file being read; a new read begins with a new source-side state, and with it new facts and the
    // templates of its machine.
    private FanucState Facts()
    {
        if (_fanuc is null || !ReferenceEquals(_fanuc.Source, State))
        {
            _fanuc = new FanucState(State);
            _templates = new TemplateSet(Machine, new Diagnostics(Machine.Machine.Name));
        }

        return _fanuc;
    }

    // The facts that need the whole file, once every block is known and the structure pass has laid it out: a G53.1
    // after a G68.2, the contour sections of G70 to G76 with the first block of each and the cycles that enter it, and
    // the contour blocks that stay RAW with their cycle.
    private FanucState PreparedFacts()
    {
        FanucState fanuc = Facts();
        if (fanuc.Prepared)
        {
            return fanuc;
        }

        fanuc.Prepared = true;
        IReadOnlyList<SourceBlock> blocks = fanuc.Blocks;
        for (int index = 0; index < blocks.Count; index++)
        {
            SourceBlock block = blocks[index];
            if (HasCode(block, "G68.2") && index + 1 < blocks.Count && HasCode(blocks[index + 1], "G53.1"))
            {
                fanuc.TiltsTurned.Add(block.Line);
                fanuc.TurnsOfTilts.Add(blocks[index + 1].Line);
            }

            // The "before" line of a catalog entry stands in front of the entry's cycle block and belongs to the
            // cycle, Fanuc M29 S before a G84 (machine-config 6, D256).
            if (BeforeOf(block) is string entry)
            {
                MarkBeforeLines(fanuc, index, entry);
            }

            // The contour of a G70 to G76 is the SUB section the structure pass made of it (language 4.7.1, D65).
            if (ContourOf(block) is string contour)
            {
                fanuc.Contours[block.Line] = contour;
                fanuc.Calls.Expect(contour);
                MarkContourBegin(fanuc, block, contour);
            }
            else
            {
                MarkContour(fanuc, block);
            }
        }

        return fanuc;
    }

    // The first block of the contour section of a cycle, the block of its first label that the structure pass moved
    // into the section (language 4.7.1, D65).
    private void MarkContourBegin(FanucState fanuc, SourceBlock cycle, string contour)
    {
        if (FanucCycles.ContourOf(cycle, Machine, out _) is not SourceContour range)
        {
            return;
        }

        foreach (SourceBlock candidate in fanuc.SectionOf(cycle))
        {
            if (InContourSection(candidate) && candidate.Find("N") is SourceWord label
                && FanucState.LabelOf(label) == range.First)
            {
                fanuc.Calls.ContourBegins[candidate.Line] = contour;
                return;
            }
        }
    }

    // The blocks from the label P to the label Q of a G70 to G76 of systems A and B are its contour (controllers
    // fanuc.md 6). Where the structure pass made no SUB section of it, they stay RAW with their RAW cycle, which reads
    // them as the control does (D5): the first range of the cycle's program, whose labels are its own, among the blocks
    // that stand in no contour section.
    private void MarkContour(FanucState fanuc, SourceBlock block)
    {
        if (Machine.Machine.GcodeSystem is not (GcodeSystem.A or GcodeSystem.B)
            || block.Find("P") is not SourceWord first || block.Find("Q") is not SourceWord last)
        {
            return;
        }

        bool repetitive = false;
        foreach (string code in new[] { "G70", "G71", "G72", "G73", "G74", "G75", "G76" })
        {
            repetitive |= HasCode(block, code);
        }

        if (!repetitive)
        {
            return;
        }

        bool inside = false;
        foreach (SourceBlock candidate in fanuc.SectionOf(block))
        {
            if (InContourSection(candidate))
            {
                continue;
            }

            string? label = candidate.Find("N") is SourceWord number ? FanucState.LabelOf(number) : null;
            inside |= label == FanucState.LabelOf(first);
            if (inside)
            {
                fanuc.ContourLines.Add(candidate.Line);
            }

            if (inside && label == FanucState.LabelOf(last))
            {
                return;
            }
        }
    }

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
    // TODO(question): a block that may move an axis without an axis word - the tool change M6 or a T word, the tool
    // length compensation G43, G44, G49 - the manual neither allows nor forbids between M29 and G84; the walk ends
    // there unseen, so the cycle stays RAW with that reason rather than reading TAP or the other machining (manual
    // audit 2026-10-26, D256). Row 3 of "Manual audit 2026-10-26" in docs/implementation/03-open-questions.md.
    // A block with the entry's own native code, the G84 of an earlier cycle block of the same run, ends the walk with
    // no place of the line and no mark: whether the run was entered with the line is the reading of that cycle block
    // (FanucCycles.WithItsBeforeLine, the previous cycle of the run). It is tested before the cancel codes, of which
    // G84 is one.
    // TODO(question): whether the line holds where its own block carries a group-01 code, "G0 Z10. M29 S500;" in
    // front of G84, the manuals do not say; the cancel of 10.13.16.8 is stated for after the tapping. The walk ends
    // at such a block with a reason of its own (BeforeLineWithACancel), so the cycle stays RAW rather than reading
    // TAP or the other machining. Row 4 of "Manual audit 2026-10-26" in docs/implementation/03-open-questions.md.
    private void MarkBeforeLines(FanucState fanuc, int cycleIndex, string entry)
    {
        IReadOnlyList<SourceBlock> blocks = fanuc.Blocks;
        SourceBlock cycle = blocks[cycleIndex];
        var lines = new List<int>();
        fanuc.BeforeLinesOf[cycle.Line] = lines;
        if (IsJumpTarget(fanuc, cycle))
        {
            fanuc.BeforeLineUnseen.Add(cycle.Line);
            return;
        }

        CycleEntry? catalogEntry = Machine.CycleCatalog.Find(entry);
        string before = catalogEntry?.Before ?? "";
        for (int index = cycleIndex - 1; index >= 0; index--)
        {
            SourceBlock block = blocks[index];
            if (block.Find("O") is SourceWord program)
            {
                if (program.Number is not decimal number || fanuc.Calls.IsCalled(
                    decimal.ToInt64(decimal.Truncate(number)).ToString(CultureInfo.InvariantCulture)))
                {
                    fanuc.BeforeLineUnseen.Add(cycle.Line);
                    lines.Clear();
                }

                break;
            }

            if (HidesWhatRunsBefore(fanuc, block))
            {
                fanuc.BeforeLineUnseen.Add(cycle.Line);
                lines.Clear();
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
                fanuc.BeforeLineUnseen.Add(cycle.Line);
                fanuc.BeforeLineWithACancel.Add(cycle.Line);
                lines.Clear();
                break;
            }

            if (CancelsTheMode(block))
            {
                break;
            }

            lines.Add(block.Line);
            if (MovesOrSetsTheSpeed(block))
            {
                break;
            }
        }

        foreach (int line in lines)
        {
            fanuc.CycleBefore[line] = entry;
        }
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
                && (code == "G80" || (ModalGroupOfCode.TryGetValue(code, out int group)
                    && group is FanucModalGroups.Motion or FanucModalGroups.Cycle)))
            {
                return true;
            }
        }

        return false;
    }

    // Whether a block carries every code of a "before" line, the M29 of "M29 S{rpm}", so that it may be that line.
    private static bool CarriesTheCodesOf(SourceBlock block, string before)
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

    private static bool IsJumpTarget(FanucState fanuc, SourceBlock block)
    {
        return block.Find("N") is SourceWord label && fanuc.JumpTargets.Contains(FanucState.LabelOf(label));
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

        foreach (AxisDef axis in Machine.Axes)
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
            || (letter == "H" && Machine.Machine.GcodeSystem == GcodeSystem.A);
    }

    // Whether the reader cannot see past a block on the walk back from a cycle block: it is skipped by the
    // operator's switch, a jump enters it, it calls or returns or branches, or it may move an axis without an axis
    // word (see MarkBeforeLines).
    private bool HidesWhatRunsBefore(FanucState fanuc, SourceBlock block)
    {
        if (block.BlockSkip || IsJumpTarget(fanuc, block))
        {
            return true;
        }

        if (FanucMacro.CallsOf(block, Machine).Count > 0)
        {
            return true;
        }

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

    // The NCX name of the catalog entry whose cycle this block starts and that carries a "before" line; null for
    // every other block (machine-config 6, D256).
    private string? BeforeOf(SourceBlock block)
    {
        foreach (CycleEntry entry in Machine.CycleCatalog.Entries)
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
