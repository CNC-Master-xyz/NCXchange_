using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Readers;

/// <summary>
/// The skeleton every reader shares (architecture 7; code-guidelines 5, Template Method): tokenize the file, lay out
/// its structure, and for every source block apply the source-side state, offer it to the reader rules of the plugins
/// and read a block no rule claims with the configuration tables and the mapping of the controller family into the
/// builder (D231); diagnostics carry RDR codes. A reader of a controller family fills in the tokenizer, the structure
/// of a block and the reading of a block.
/// </summary>
public abstract class ReaderBase : IReader
{
    // The word of the optional block skip (language 4.1) and of a structuring comment (controller-mapping 1).
    private const string SkipKey = "SKIP";
    private const string SectionKey = "SECTION";

    private static readonly Dictionary<string, int> s_noModalGroups = [];

    private MachineConfig? _machine;
    private Diagnostics? _diagnostics;
    private NcxBuilder? _builder;
    private SourceState? _state;
    private TemplateSet? _templates;
    private IReadOnlyList<ISourceRule> _rules = [];
    private ReadOptions? _options;

    // The comment of the source block being read, until an NCX block takes it (controller-mapping 1).
    private string? _pendingComment;

    // The source blocks of the file structure and of the control flow, the frame, a section begin, a program end, a
    // return, a jump, a call, a repeat (language 4.13): a reader rule's look-ahead stops in front of them (D232).
    private readonly HashSet<SourceBlock> _flowBlocks = new(ReferenceEqualityComparer.Instance);

    // The look-ahead of the Read steps of the file being read (D232).
    private ReadLookAhead? _lookAhead;

    // The contour sections the structure pass made of the file being read (language 4.7.1, D65).
    private ContourLayout _contours = ContourLayout.None;

    /// <summary>
    /// The controller family whose files this reader reads (machine-config 1).
    /// </summary>
    public abstract Controller Controller { get; }

    /// <summary>
    /// The tokenizer of the controller's syntax (architecture 7).
    /// </summary>
    protected abstract ISourceTokenizer Tokenizer { get; }

    /// <summary>
    /// The modal group of every modal code of the controller family, by the code without leading zeros, "G1" is
    /// group 1 on Fanuc (controllers fanuc.md 3); empty for a family without modal groups.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, int> ModalGroupOfCode => s_noModalGroups;

    /// <summary>
    /// The machine of the file being read, while Read reads it.
    /// </summary>
    protected MachineConfig Machine => _machine ?? throw NotReading();

    /// <summary>
    /// The options of the file being read, while Read reads it: the channel of ncx convert --channel (D223).
    /// </summary>
    protected ReadOptions Options => _options ?? throw NotReading();

    /// <summary>
    /// The diagnostics of the file being read, while Read reads it (D98).
    /// </summary>
    protected Diagnostics Diagnostics => _diagnostics ?? throw NotReading();

    /// <summary>
    /// The builder of the program being read, while Read reads it (architecture 7).
    /// </summary>
    protected NcxBuilder Builder => _builder ?? throw NotReading();

    /// <summary>
    /// The source-side state of the file being read, while Read reads it (architecture 7).
    /// </summary>
    protected SourceState State => _state ?? throw NotReading();

    /// <summary>
    /// Reads one file (architecture 7): never throws on bad input, reports through the diagnostics of the program,
    /// produces a program that check can run and keeps what it cannot express as RAW (code-guidelines 4, D5).
    /// </summary>
    /// <param name="source">The file, its name and its text.</param>
    /// <param name="machine">The machine the file was written for.</param>
    /// <param name="options">The reader rules of the plugins.</param>
    public NcxProgram Read(SourceFile source, MachineConfig machine, ReadOptions options)
    {
        Start(source, machine, options);

        // The tokenizer cuts the file into blocks, the reader of the family says what each block is for the file
        // structure, and the structure pass lays out the order in which the blocks are read and the blocks of the
        // structure are written (architecture 7, language 4.13).
        List<SourceBlock> blocks = [.. Tokenizer.Tokenize(source.Text)];
        BeginStructure(blocks);
        var structures = new List<SourceStructure>(blocks.Count);
        foreach (SourceBlock block in blocks)
        {
            SourceStructure structure = block.IsTrivia ? SourceStructure.None : StructureOf(block);
            structures.Add(structure);
            if (IsFlow(structure))
            {
                _flowBlocks.Add(block);
            }
        }

        SourceBlock? current = null;
        List<ReadStep> plan = StructurePass.Plan(blocks, structures, Diagnostics, out _contours);
        _lookAhead = _rules.Count > 0 ? new ReadLookAhead(plan, _flowBlocks) : null;

        // The last step of the run a reader rule claimed (D232); the claimed blocks after the offered one are not read.
        int claimedThrough = -1;
        for (int index = 0; index < plan.Count; index++)
        {
            ReadStep step = plan[index];
            if (!ReferenceEquals(step.Source, current))
            {
                KeepPendingComment();
                current = step.Source;
                _pendingComment = string.IsNullOrEmpty(current.Comment) ? null : current.Comment;
            }

            if (step.Kind == ReadStepKind.Read && index <= claimedThrough)
            {
                // A block of a claimed run acts on the source state, and its comment stays as a comment-only line
                // after the rule's blocks (D232, D5).
                State.Apply(step.Source);
            }
            else if (step.Kind == ReadStepKind.Read)
            {
                claimedThrough = ReadSourceBlock(plan, index);
            }
            else
            {
                TakeStep(step);
            }
        }

        KeepPendingComment();
        return Builder.Build();
    }

    /// <summary>
    /// Says what a source block is for the structure of the file: the file frame, the begin of a program or
    /// subprogram, the program end, the return, its label and the labels it jumps to (language 4.13). The structure
    /// pass writes the words of the structure from it; ReadBlock writes the other words of the block.
    /// </summary>
    /// <summary>
    /// Called with the blocks of the file, in file order, before the first StructureOf: a family that needs a fact of
    /// the whole file to lay out a single block works it out here, so that the reading of a block does not depend on
    /// how far the structure pass has walked (D221). Does nothing by default.
    /// </summary>
    /// <param name="blocks">Every block of the file, trivia among them, in file order.</param>
    protected virtual void BeginStructure(IReadOnlyList<SourceBlock> blocks)
    {
    }

    /// <param name="block">A source block with words.</param>
    protected abstract SourceStructure StructureOf(SourceBlock block);

    /// <summary>
    /// Reads a source block into NCX blocks, each begun with BeginBlock(block), with the mapping of the controller
    /// family; the words of the file structure are the structure pass's (architecture 7).
    /// </summary>
    /// <param name="block">A source block with words that no reader rule claimed.</param>
    protected abstract void ReadBlock(SourceBlock block);

    /// <summary>
    /// The NAME of the SUB section the structure pass made of the contour a cycle block names (SourceStructure.Contour;
    /// language 4.7, CONTOUR, and 4.7.1; D65); null when it made none, and the family keeps the cycle as its source
    /// writes it.
    /// </summary>
    /// <param name="block">The cycle block.</param>
    protected string? ContourOf(SourceBlock block)
    {
        return _contours.Cycles.TryGetValue(block.Line, out string? name) ? name : null;
    }

    /// <summary>
    /// The NAME of the SUB section the structure pass made of the block range a repeat block names, a copy of the range
    /// that the repeat calls (SourceStructure.Repeat; controller-mapping 6, REPEAT + TIMES); null when it made none,
    /// and the family keeps the repeat as its source writes it.
    /// </summary>
    /// <param name="block">The repeat block.</param>
    protected string? RepeatOf(SourceBlock block)
    {
        return _contours.Repeats.TryGetValue(block.Line, out string? name) ? name : null;
    }

    /// <summary>
    /// Tells whether the structure pass moved a source block into the SUB section of a contour (language 4.7.1, D65).
    /// </summary>
    /// <param name="block">A source block.</param>
    protected bool InContourSection(SourceBlock block)
    {
        return _contours.Moved.Contains(block.Line);
    }

    /// <summary>
    /// Called when the structure pass has written the PROGRAM=BEGIN or SUB=BEGIN of a section, the SUB=BEGIN of a
    /// contour section among them, before the first block of the section is read: a family writes the header of a
    /// program here, the complete header of D34. Does nothing by default.
    /// </summary>
    /// <param name="kind">A program or a subprogram, as the structure pass decided it (language 4.13).</param>
    /// <param name="begin">The source block the section begins with: the Fanuc O line, or the first block of a program
    /// the source does not name.</param>
    protected virtual void BeginSection(SectionKind kind, SourceBlock begin)
    {
    }

    /// <summary>
    /// Begins an NCX block read from a source block: with SKIP or SKIP=n when the source block carries the block skip
    /// (language 4.1), and with the comment of the source block on the first NCX block read from it (controller-mapping
    /// 1). Add the words and End() it.
    /// </summary>
    /// <param name="block">The source block being read.</param>
    /// <returns>The builder with the block begun.</returns>
    protected NcxBuilder BeginBlock(SourceBlock block)
    {
        Builder.Begin(block.Line);
        AddSkip(block);
        TakeComment();
        return Builder;
    }

    /// <summary>
    /// Keeps a source block that NCX cannot express as RAW:controller, or RAW:builder when it holds a code of the
    /// machine's [raw] table, with its text verbatim and a WARNING (D5, controller-mapping 9).
    /// </summary>
    /// <param name="block">The source block being read.</param>
    /// <param name="reason">Why the block cannot be read, in the words of the machine.</param>
    protected void EmitRaw(SourceBlock block, string reason)
    {
        // The RAW text holds the comment of the block, verbatim (language 4.1).
        _pendingComment = null;
        RawEmitter.Emit(Builder, block, RawEmitter.DialectOf(block, Machine, Controller), reason, Diagnostics);
    }

    /// <summary>
    /// Writes a structuring comment as SECTION="title", Heidenhain * - title (controller-mapping 1).
    /// </summary>
    /// <param name="block">The source block being read.</param>
    /// <param name="title">The text of the structuring comment.</param>
    protected void EmitSection(SourceBlock block, string title)
    {
        BeginBlock(block).Word(SectionKey, null, new StringValue(title)).End();
    }

    /// <summary>
    /// Maps a native code to the state of the function table of the machine that writes it, codes compared by number:
    /// "M08" is "COOLANT:STANDARD=ON" where the table writes M8 (machine-config 5, D105).
    /// </summary>
    /// <param name="nativeCode">The native text of the function, "M88", "M03 P11".</param>
    /// <returns>The state, the word of its table with the state as its value; null when no table names the code.
    /// </returns>
    protected string? FindFunction(string nativeCode)
    {
        // The state names the word of its table, COOLANT:STANDARD=ON; the word of the language, COOLANT=ON for the
        // default channel, is the family's to write (wave-1 question #62, D154), and of a code several states write the
        // first is named; FindFunctions chooses among them with the source state (D155).
        return (_templates ?? throw NotReading()).FindFunctionByCode(nativeCode);
    }

    /// <summary>
    /// The states of the machine to write for a native code: every state that writes it, less those the source state
    /// rules out and those that already hold, or the first in file order, with the [workpiece] selection first
    /// (machine-config 5, architecture 7, D155).
    /// </summary>
    /// <param name="block">The source block being read.</param>
    /// <param name="nativeCode">The native text of the function, "M9", "M03 P11".</param>
    /// <param name="context">What the source state of the family says about the code.</param>
    /// <returns>The states to write; empty when no state of the machine writes the code.</returns>
    protected IReadOnlyList<FunctionMatch> FindFunctions(SourceBlock block, string nativeCode, FunctionContext context)
    {
        IReadOnlyList<FunctionMatch> matches = (_templates ?? throw NotReading()).FindFunctionsByCode(nativeCode);
        return FunctionChoice.Choose(matches, context, block.Line, nativeCode, Diagnostics);
    }

    /// <summary>
    /// Tells whether a word is a builder code of the machine's [raw] table, compared by number (machine-config 5,
    /// D105).
    /// </summary>
    /// <param name="word">The source word.</param>
    protected bool IsBuilderCode(SourceWord word)
    {
        return RawEmitter.IsBuilderCode(word, Machine.Raw);
    }

    private void Start(SourceFile source, MachineConfig machine, ReadOptions options)
    {
        _machine = machine;
        _diagnostics = new Diagnostics(source.Name);
        _builder = new NcxBuilder(_diagnostics);
        _state = new SourceState(machine.Machine.GcodeSystem, ModalGroupOfCode);
        _rules = options.Rules;
        _options = options;
        _pendingComment = null;
        _contours = ContourLayout.None;
        _flowBlocks.Clear();
        _lookAhead = null;

        // The templates of the machine, which the tables are matched through (architecture 6). A template the machine
        // file cannot use is an ERROR of the machine file; the machine record keeps neither its file nor the lines of
        // its templates (wave-1 question #61), so the machine's name stands for the file.
        var machineDiagnostics = new Diagnostics(machine.Machine.Name);
        _templates = new TemplateSet(machine, machineDiagnostics);
        foreach (Diagnostic diagnostic in machineDiagnostics.Items)
        {
            _diagnostics.Add(diagnostic);
        }
    }

    private void TakeStep(ReadStep step)
    {
        switch (step.Kind)
        {
            case ReadStepKind.Trivia:
                // A blank or comment-only source line is trivia, kept in its place (D92; controller-mapping 1).
                Builder.Trivia(_pendingComment is null ? "" : "; " + _pendingComment);
                _pendingComment = null;
                break;
            default:
                WriteStructure(step);
                break;
        }
    }

    // Reads the source block of a Read step and returns the index of the last step it took: the step itself, or the
    // last step of the run a reader rule claimed (D232).
    private int ReadSourceBlock(List<ReadStep> plan, int index)
    {
        // The modal codes of a block act for the block itself, so it is read with them (controllers fanuc.md 1, 3).
        SourceBlock block = plan[index].Source;
        State.Apply(block);

        // Every block goes to the reader rules first, in the order the plugins are loaded; a block no rule claims is
        // read as without a plugin, with the tables and then the words of the family (D231, D317). The order of the
        // built-in mechanisms, the [workpiece] table before the chuck function rule, is the family's (D40).
        if (_lookAhead is not null)
        {
            int claimed = ClaimedByRule(block, _lookAhead.Following(index));
            if (claimed > 0)
            {
                return _lookAhead.LastStep(index, claimed);
            }
        }

        ReadBlock(block);
        return index;
    }

    // A block of the file structure or of the control flow, which ends a reader rule's look-ahead in front of it: a
    // role in the structure (O, M30, M99, the frame), a jump, a call, a repeat, a contour or a lifted subprogram, so
    // that a rule never swallows control flow (D232; gap closed 2026-10-28, delegated).
    private static bool IsFlow(SourceStructure structure)
    {
        return structure.Role != StructureRole.None || structure.LabelsUsed.Count > 0 || structure.Calls.Count > 0
            || structure.Repeat is not null || structure.Contour is not null || structure.LiftsSub is not null
            || structure.LiftsSubEnd || structure.ReturnsToABlock;
    }

    // The first rule that claims the block reads it, and the run it claims (D231, D232); 0 when no rule claims it. A
    // rule shown n following blocks claims at most n + 1: a larger count takes the blocks it was shown and no more,
    // so that the structure after them is still written (gap closed 2026-10-27, delegated).
    private int ClaimedByRule(SourceBlock block, IReadOnlyList<SourceBlock> following)
    {
        SourceBlock offered = block with { Following = following };
        foreach (ISourceRule rule in _rules)
        {
            int claimed = rule.Read(offered, State, Builder);
            if (claimed > 0)
            {
                return Math.Min(claimed, following.Count + 1);
            }
        }

        return 0;
    }

    private void WriteStructure(ReadStep step)
    {
        Builder.Begin(step.Line);
        if (step.CarriesSkip)
        {
            AddSkip(step.Source);
        }

        foreach (Word word in step.Words)
        {
            Builder.Word(word.Key, word.Addr, word.Value);
        }

        if (step.UsesComment)
        {
            _pendingComment = null;
        }
        else if (step.TakesComment)
        {
            TakeComment();
        }

        Builder.End();

        // The first word of a section's begin is PROGRAM=BEGIN or SUB=BEGIN (language 4.13); the family writes what
        // follows it before the first block of the section is read.
        if (step.Words.Count > 0 && step.Words[0].Value is IdentValue { Name: "BEGIN" })
        {
            if (step.Words[0].Key == "PROGRAM")
            {
                BeginSection(SectionKind.Program, step.Source);
            }
            else if (step.Words[0].Key == "SUB")
            {
                BeginSection(SectionKind.Sub, step.Source);
            }
        }
    }

    // The optional block skip, / or /n in front of the block, is SKIP or SKIP=n (language 4.1, controller-mapping 1).
    private void AddSkip(SourceBlock block)
    {
        if (!block.BlockSkip)
        {
            return;
        }

        Value skipSwitch = block.SkipSwitch is int number
            ? new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture))
            : NoValue.Instance;
        Builder.Word(SkipKey, null, skipSwitch);
    }

    // A source comment on a block becomes the comment of the first NCX block read from it (controller-mapping 1).
    private void TakeComment()
    {
        if (_pendingComment is not null)
        {
            Builder.Comment("; " + _pendingComment);
            _pendingComment = null;
        }
    }

    // Comments are kept (D5): the comment of a source block that no NCX block took, because the block wrote none
    // (a Fanuc G90 alone) or a reader rule claimed it, stays as a comment-only line after its blocks (D92).
    private void KeepPendingComment()
    {
        if (_pendingComment is not null)
        {
            Builder.Trivia("; " + _pendingComment);
            _pendingComment = null;
        }
    }

    private static InvalidOperationException NotReading()
    {
        return new InvalidOperationException("A reader has a file, a machine and a builder only while Read reads.");
    }
}
