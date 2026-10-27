using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// Writes an NCX program as iTNC 530 Klartext (controllers heidenhain.md 8; controller-mapping, the Heidenhain column;
/// architecture 8): one .h file per program between BEGIN PGM and END PGM with consecutive block numbers from 0, the
/// comma and a sign on every coordinate, L with FMAX or F, CC plus C and CR, TOOL CALL, HOME as M91 moves, CYCL DEF
/// with its Q parameters and CYCL CALL or M99, LBL sections after the M30 of every caller. Each concern of a block is a
/// file of this folder; this class writes a block through them in the order the control executes it.
/// </summary>
public sealed class HeidenhainCompiler : CompilerBase
{
    // The labels of the program being written and the jumps forward that wait for them (language 4.9).
    private readonly HeidenhainLabelWays _labels = new();

    // The target state the last block was written with. The step before the SUB=BEGIN of a walk is its CALL, or the
    // SUB=END of the walk before it where a CALL with TIMES walks the section again, so this is what the control has
    // where the walk begins (virtual machine 1, 3.9, D99).
    private TargetState? _lastTarget;

    // The LBL numbers and the counter loops of the compile, found once from every step of the run (D213, D271, D272).
    private LookAhead? _numberedFor;
    private HeidenhainLabelNumbers? _labelNumbers;
    private HeidenhainRepeats? _repeats;
    private HeidenhainSpills? _spills;

    /// <summary>
    /// Heidenhain, the controller of the machine files whose programs this compiler writes (machine-config 1).
    /// </summary>
    public override Controller Controller => Controller.Heidenhain;

    /// <summary>
    /// The extension of a Klartext file, .h (controllers heidenhain.md, introduction).
    /// </summary>
    protected override string FileExtension => ".h";

    /// <summary>
    /// D316, D388 (13): on Heidenhain each SUB that no channel program of a job calls becomes a program of its own,
    /// &lt;job name&gt;_LIB_&lt;sub&gt;.h, which another program calls with CALL PGM: the body of its LBL section
    /// between BEGIN PGM and END PGM, without M2 or M30, which a called program must not contain (D274; iTNC 530
    /// Benutzerhandbuch Klartext 340490, connector p.523, Programmier-Hinweise). The units are the machine's
    /// units_default, since a SUB no program calls runs from the default entry state (D99).
    /// </summary>
    private protected override List<CompiledFile> LibraryFiles(string library, IReadOnlyList<Section> subs)
    {
        string units = Machine.Machine.UnitsDefault == "INCH" ? "INCH" : "MM";
        var files = new List<CompiledFile>();
        foreach (Section sub in subs)
        {
            // Every SUB is written, also one whose walk wrote no lines; its program is then empty.
            IReadOnlyList<OutputLine> lines = SubLines(sub);
            Block begin = lines.Count > 0 ? lines[0].Block : SubBeginBlock(sub);
            Block end = lines.Count > 0 ? lines[^1].Block : begin;
            string name = library + "_" + sub.Name;
            OutputBuffer output = NewOutput();
            output.Line("BEGIN PGM " + name + " " + units, begin);
            for (int index = 1; index < lines.Count - 1; index++)
            {
                output.Add([LibraryCall(lines[index], library, subs)]);
            }

            output.Line("END PGM " + name + " " + units, end);
            files.Add(new CompiledFile
            {
                Name = name + FileExtension,
                Text = output.ToText(Diagnostics),
                Library = true,
            });
        }

        return files;
    }

    // Inside a library program a call of another SUB of the library is a call of that program, CALL PGM
    // <job name>_LIB_<sub>, since the LBL is in another program now (D316, D274); every other line stays.
    private static OutputLine LibraryCall(OutputLine line, string library, IReadOnlyList<Section> subs)
    {
        if (!line.Text.StartsWith("CALL LBL ", StringComparison.Ordinal)
            || line.Block.Find("CALL")?.Value is not Value target)
        {
            return line;
        }

        string called = target switch
        {
            StringValue text => text.Content,
            IdentValue ident => ident.Name,
            _ => target.ToCanonical(),
        };
        foreach (Section sub in subs)
        {
            if (sub.Name == called)
            {
                return line with { Text = "CALL PGM " + library + "_" + sub.Name };
            }
        }

        return line;
    }

    /// <summary>
    /// An ARC with a tool vector is the ERROR CMP017: LN, the only Klartext block with a tool vector, has no arc form
    /// (D185).
    /// </summary>
    protected override bool WritesVectorArcs => false;

    /// <summary>
    /// Klartext numbers its blocks without a letter in front, 0 BEGIN PGM (controllers heidenhain.md 1, 8 rule 1).
    /// </summary>
    protected override string BlockNumberPrefix => "";

    /// <summary>
    /// A comment block of Klartext, ; text (controllers heidenhain.md 1; controller-mapping 1, COMMENT).
    /// </summary>
    protected override string CommentLine(string text)
    {
        return HeidenhainProgramFrame.CommentLine(text);
    }

    /// <summary>
    /// Every line takes a block number except the continuation lines of a cycle definition, which belong to its first
    /// line (controllers heidenhain.md 1).
    /// </summary>
    protected override bool TakesBlockNumber(string line)
    {
        return !HeidenhainCycles.IsContinuation(line);
    }

    /// <summary>
    /// BEGIN PGM name MM (heidenhain 8 rule 1).
    /// </summary>
    protected override void WriteHeader(Section program, ChannelSnapshot state)
    {
        _labels.Clear();
        HeidenhainBlock writing = NewBlock();
        HeidenhainProgramFrame.WriteBegin(writing, program, state);
        HeidenhainFunctions.EnterProgram(writing);
    }

    /// <summary>
    /// END PGM name MM after the M30 and the subprograms behind it (heidenhain 8 rules 1 and 6), with the units of
    /// BEGIN PGM, not those of the state at its end (controllers heidenhain.md 1).
    /// </summary>
    protected override void WriteFooter(Section program, ChannelSnapshot state)
    {
        HeidenhainProgramFrame.WriteEndOfProgram(NewBlock(), program);
    }

    /// <summary>
    /// Writes one block as Klartext: its structure and label, then its state words, each in a block of its own before
    /// the motion, then the motion, then what follows it; a word no concern writes is an ERROR (CMP101).
    /// </summary>
    protected override void WriteBlock(Block block, ChannelSnapshot before, ChannelSnapshot after)
    {
        HeidenhainBlock writing = NewBlock();

        // The structure of the file and the comments first, and a label where the block begins, the target of a jump
        // (language 4.9, 4.13).
        HeidenhainProgramFrame.WriteStart(writing);
        HeidenhainFlow.WriteLabel(writing);

        // The walk of a subprogram runs with the caller's state (virtual machine 3.9, D99), the radius compensation the
        // caller left the control included.
        HeidenhainCompensation.EnterWalk(writing, _lastTarget);

        // The state words of a block act before its motion (language 5 rule 3): the frame, the tool, the modes and the
        // functions, the variables and the cycle definition, each in a Klartext block of its own.
        HeidenhainChain.Write(writing);
        HeidenhainToolCall.Write(writing);
        HeidenhainFrames.WriteModes(writing);
        HeidenhainProgramFrame.WriteStock(writing);
        HeidenhainFunctions.Write(writing);
        HeidenhainFlow.WriteVariables(writing);
        HeidenhainCycles.WriteDefinition(writing);

        // The motion of the block (language 5 rule 1).
        WriteMotion(writing);

        // After the motion: the dwell and the stop, the calls and the jumps, the end of the program or subprogram
        // (language 4.1, 4.9, 4.13).
        HeidenhainCycles.WriteDwell(writing);
        HeidenhainFunctions.WriteStop(writing);
        HeidenhainSubprograms.WriteCall(writing);
        HeidenhainFlow.WriteJump(writing);
        HeidenhainProgramFrame.WriteEnd(writing);
        HeidenhainMotion.KeepPending(writing);
        HeidenhainCompensation.KeepPending(writing);
        ReportUnwritten(writing);
        _lastTarget = Target;
    }

    // The verb of the block: RAPID and LINE as L or LN, ARC as CR, CC plus C or CC plus CP, HOME as the M91 move,
    // RETRACT as M140, CYCLE_CALL as CYCL CALL or M99; SHIFT, TILT and TILT_AXIS are written with the chain, and so is
    // SETPOS, folded into cycle 7 (machine-config 3, HeidenhainSetpos).
    private static void WriteMotion(HeidenhainBlock writing)
    {
        switch (writing.Block.Verb?.Key)
        {
            case "RAPID":
                writing.Take("RAPID");
                HeidenhainMotion.WriteStraight(writing, rapid: true, call: null);
                break;
            case "LINE":
                writing.Take("LINE");
                HeidenhainMotion.WriteStraight(writing, rapid: false, call: null);
                break;
            case "ARC":
                HeidenhainArcs.Write(writing);
                break;
            case "HOME":
                HeidenhainFrames.WriteHome(writing);
                break;
            case "RETRACT":
                HeidenhainFrames.WriteRetract(writing);
                break;
            case "CYCLE_CALL":
                HeidenhainCycles.WriteCall(writing);
                break;
        }
    }

    // Nothing of the program is dropped silently (language 2 rule 8): a word no concern wrote is an ERROR.
    private static void ReportUnwritten(HeidenhainBlock writing)
    {
        List<Word> unwritten = writing.Unwritten();
        if (unwritten.Count == 0)
        {
            return;
        }

        writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
            $"The Heidenhain compiler writes no Klartext for {HeidenhainBlock.WordsText(unwritten)}, and nothing is "
            + "written for it (controllers heidenhain.md 8; language 2 rule 8).");
    }

    // The block being written with what its concerns write it with.
    private HeidenhainBlock NewBlock()
    {
        // The labels are numbered once per compile, where every step of the run is known (D271).
        if (!ReferenceEquals(_numberedFor, LookAhead))
        {
            _numberedFor = LookAhead;
            _labelNumbers = HeidenhainLabelNumbers.Of(LookAhead, Machine.Format?.LabelMax ?? 999, Diagnostics);
            _repeats = HeidenhainRepeats.Of(LookAhead, CounterRange.Of(Machine.Format), Diagnostics);
            _spills = HeidenhainSpills.Of(LookAhead, CounterRange.Of(Machine.Format));
        }

        return new HeidenhainBlock
        {
            Step = Step,
            Machine = Machine,
            Numbers = Numbers,
            Target = Target,
            Diagnostics = Diagnostics,
            LookAhead = LookAhead,
            Templates = Templates,
            Labels = _labels,
            LabelNumbers = _labelNumbers!,
            Repeats = _repeats!,
            Spills = _spills!,
            WriteLine = Line,
            WriteToolChange = (values, suffix) => WriteToolChange(values, suffix),
            WritePreload = () => WritePreload(),
            CommentText = CommentText,
            ProgramName = ProgramFileName,
            PointRegisters = PointRegistersOf(Step.After),
            Stock = StockOf(Step),
        };
    }
}
