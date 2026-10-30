using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Expressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The block being written as Klartext and what the concerns of the Heidenhain compiler write it with: the step with
/// the state before and after it, the machine with its number format and templates, what the control has active, the
/// look-ahead of the STATIC pass, and the words of the block that a concern has written, so that no word of the program
/// is dropped without a diagnostic (language 2 rule 8; architecture 8).
/// </summary>
internal sealed class HeidenhainBlock
{
    // The optional block skip is a / at the start of the block (controllers heidenhain.md 1; controller-mapping 1,
    // SKIP).
    private const string SkipMark = "/";

    // A value of the target state that the control may not have: the next value under its key is written whatever it
    // is. A key removed instead would be taken over after the return of a subprogram as the caller left it
    // (TargetState.TakeOver; virtual machine 3.9, D99), so the unknown stands as a value.
    private const string UnknownValue = "?";

    // The function that reads a system datum into a Q parameter (D182; TNC 640 Programmierung-Klartext 892903-18,
    // connector p. 569: FN 18 reads a system datum and stores it in a Q parameter).
    private const string SystemReadKey = "FN 18: SYSREAD ";

    // The block that stops the look-ahead of the control before an FN 18 read of real-time data (D397, delegated
    // 2026-10-30).
    private const string WaitForSync = "FN 20: WAIT FOR SYNC";

    // The FN 18 groups of real-time data: the position in the active system and in the REF system (D182).
    private static readonly string[] s_realTimeIds = ["ID270 ", "ID240 "];

    private readonly HashSet<Word> _written = [];

    // The formulas of the block spilled into QL parameters, by formula (D338).
    // The FN 18 reads of SYS_ names take their QL from the same pool, under this text and their ids (D397).
    private readonly Dictionary<string, string> _spilled = new(StringComparer.Ordinal);

    /// <summary>
    /// The block with its state before and after it and its events (architecture 8).
    /// </summary>
    public required BlockStep Step { get; init; }

    /// <summary>
    /// The machine of the compile.
    /// </summary>
    public required MachineConfig Machine { get; init; }

    /// <summary>
    /// The numbers as [format] writes them, the comma of Klartext among them (machine-config 2).
    /// </summary>
    public required NumberFormatter Numbers { get; init; }

    /// <summary>
    /// What the control has active in the program or walk being written, so that a modal word is written on change.
    /// </summary>
    public required TargetState Target { get; init; }

    /// <summary>
    /// The diagnostics of the compile (D98).
    /// </summary>
    public required Diagnostics Diagnostics { get; init; }

    /// <summary>
    /// Every step of the run, for what a block needs from the blocks around it (architecture 8, D52).
    /// </summary>
    public required LookAhead LookAhead { get; init; }

    /// <summary>
    /// The templates of the machine, each parsed once (architecture 6).
    /// </summary>
    public required TemplateSet Templates { get; init; }

    /// <summary>
    /// The labels of the program being written and the jumps forward that wait for them (language 4.9;
    /// HeidenhainArrivals).
    /// </summary>
    public required HeidenhainLabelWays Labels { get; init; }

    /// <summary>
    /// The LBL numbers of the compile (D271, D272).
    /// </summary>
    public required HeidenhainLabelNumbers LabelNumbers { get; init; }

    /// <summary>
    /// The REPEAT blocks written as counter loops, with their counters (D213).
    /// </summary>
    public required HeidenhainRepeats Repeats { get; init; }

    /// <summary>
    /// The QL parameters free for the formulas of a block (D338).
    /// </summary>
    public required HeidenhainSpills Spills { get; init; }

    /// <summary>
    /// The CYLINDER sections cycle 27 writes (D394).
    /// </summary>
    public required HeidenhainCylinders Cylinders { get; init; }

    /// <summary>
    /// The radius the tool table gives a tool; null where it gives none or there is no table (D10).
    /// </summary>
    public required Func<ToolRef, decimal?> ToolRadius { get; init; }

    /// <summary>
    /// Writes one line of the block (CompilerBase.Line).
    /// </summary>
    public required Action<string> WriteLine { get; init; }

    /// <summary>
    /// Writes the tool change of the block per [tool_change] with the values the compiler sets itself and the text it
    /// appends to the command, the feed of a TOOL block (CompilerBase.WriteToolChange, D251).
    /// </summary>
    public required Action<TemplateValues, string?> WriteToolChange { get; init; }

    /// <summary>
    /// Writes the PRELOAD of the block with the preload template (CompilerBase.WritePreload).
    /// </summary>
    public required Action WritePreload { get; init; }

    /// <summary>
    /// A comment text in the charset of the machine (CompilerBase.CommentText).
    /// </summary>
    public required Func<string, string> CommentText { get; init; }

    /// <summary>
    /// The final name of a program's file, which BEGIN PGM and END PGM carry (CompilerBase.ProgramFileName, D266).
    /// </summary>
    public required Func<Section, string> ProgramName { get; init; }

    /// <summary>
    /// The index of the named measurement point of the holder after the block, from [tool_points]; null where the
    /// program named no point since the TOOL or the mapping gives none (D373, D393; CompilerBase.PointRegistersOf).
    /// </summary>
    public ToolPointRegisters? PointRegisters { get; init; }

    /// <summary>
    /// What the block writes for STOCK, the [stock] template and its numbers; null for another block and for a
    /// STOCK that writes nothing (D351; CompilerBase.StockOf).
    /// </summary>
    public StockForm? Stock { get; init; }

    /// <summary>
    /// The block.
    /// </summary>
    public Block Block => Step.Block;

    /// <summary>
    /// The state of the channel before the block.
    /// </summary>
    public ChannelSnapshot Before => Step.Before;

    /// <summary>
    /// The state of the channel after the block.
    /// </summary>
    public ChannelSnapshot After => Step.After;

    /// <summary>
    /// Writes a line of the block, or several where the text holds line breaks. Every line of a block with SKIP starts
    /// with the block skip, except the continuation lines of a cycle definition, which belong to its first line
    /// (controllers heidenhain.md 1).
    /// </summary>
    /// <param name="text">The Klartext without block number.</param>
    public void Line(string text)
    {
        foreach (string line in text.Split('\n'))
        {
            bool skipped = Block.Skip && !HeidenhainCycles.IsContinuation(line);
            WriteLine(skipped ? SkipMark + line : line);
        }
    }

    /// <summary>
    /// The word of the block with this key and without an address, marked as written; null when the block has none.
    /// </summary>
    /// <param name="key">The key: "FEED_MODE".</param>
    public Word? Take(string key)
    {
        return Take(key, null);
    }

    /// <summary>
    /// The word of the block with this key and address, marked as written; null when the block has none.
    /// </summary>
    /// <param name="key">The key: "TOLERANCE".</param>
    /// <param name="addr">The address: "ROTARY"; null for the word without one.</param>
    public Word? Take(string key, string? addr)
    {
        Word? word = Block.Find(key, addr);
        if (word is not null)
        {
            _written.Add(word);
        }

        return word;
    }

    /// <summary>
    /// Every word of the block with this key, whatever its address, marked as written.
    /// </summary>
    /// <param name="key">The key: "OFFSET".</param>
    public List<Word> TakeAll(string key)
    {
        var words = new List<Word>();
        foreach (Word word in Block.Words)
        {
            if (word.Key == key)
            {
                _written.Add(word);
                words.Add(word);
            }
        }

        return words;
    }

    /// <summary>
    /// Marks a word of the block as written.
    /// </summary>
    public void MarkWritten(Word word)
    {
        _written.Add(word);
    }

    /// <summary>
    /// The words of the block that no concern has written, in the order of the block.
    /// </summary>
    public List<Word> Unwritten()
    {
        var words = new List<Word>();
        foreach (Word word in Block.Words)
        {
            if (!_written.Contains(word))
            {
                words.Add(word);
            }
        }

        return words;
    }

    /// <summary>
    /// Tells whether the target state knows what the control has under the key: a value written, not unknown.
    /// </summary>
    /// <param name="key">The key: "COMP".</param>
    public bool Knows(string key)
    {
        return Target.ActiveOf(key) is string value && value != UnknownValue;
    }

    /// <summary>
    /// Makes the value under the key unknown, so that the next value under it is written whatever it is.
    /// </summary>
    /// <param name="key">The key: "F".</param>
    public void MakeUnknown(string key)
    {
        Target.Set(key, UnknownValue);
    }

    /// <summary>
    /// Makes every value of the target state unknown except those under the keys given, so that the next value under
    /// each is written whatever it is.
    /// </summary>
    /// <param name="kept">The keys whose values stay as they are.</param>
    public void MakeUnknownExcept(IReadOnlyCollection<string> kept)
    {
        var keys = new List<string>(Target.Active.Keys);
        foreach (string key in keys)
        {
            if (!kept.Contains(key))
            {
                MakeUnknown(key);
            }
        }
    }

    /// <summary>
    /// Writes a template of the machine for the block: rendered with the values, each of its lines a line of the block
    /// (machine-config introduction, 3). A template the machine file does not give is an ERROR, as in the framework
    /// (CMP010); an empty template writes nothing.
    /// </summary>
    /// <param name="text">The template text as the record of the machine keeps it; null when the file has none.</param>
    /// <param name="what">The template as a message names it: "[spindle.TOOL] CW (machine-config 5)".</param>
    /// <param name="values">The values of its placeholders.</param>
    /// <param name="collapseSpaces">True where a placeholder may be empty, {rot} of a ROT without effect: the runs of
    /// spaces it leaves become one, and a space at the end of a line goes (D269).</param>
    /// <returns>True when it was written.</returns>
    public bool Template(string? text, string what, TemplateValues values, bool collapseSpaces = false)
    {
        // A word whose template the machine does not give cannot be written (machine-config introduction).
        if (text is null)
        {
            Error(DiagnosticCodes.TemplateMissing,
                $"The machine \"{Machine.Machine.Name}\" has no {what}, which {WordsText(Block.Words)} needs, so "
                + "nothing is written for it.");
            return false;
        }

        Template template = Templates.For(text) ?? new Template(text, 1, new Diagnostics(Machine.Machine.Name));
        string? rendered = template.Render(values, Block, Diagnostics);
        if (rendered is null)
        {
            return false;
        }

        if (collapseSpaces)
        {
            rendered = string.Join("\n", rendered.Split('\n').Select(CollapseSpaces));
        }

        if (rendered.Length > 0)
        {
            Line(rendered);
        }

        return true;
    }

    /// <summary>
    /// The QL parameter that holds a formula of the block, set in a block of its own written now, before the line that
    /// reads it: QL499 = Q1 + 5 (D338); the same formula twice in a block takes one parameter. Null when no QL is free.
    /// </summary>
    /// <param name="formula">The Klartext formula.</param>
    public string? Spill(string formula)
    {
        if (_spilled.TryGetValue(formula, out string? held))
        {
            return held;
        }

        if (Spills.Free(_spilled.Count) is not string register)
        {
            return null;
        }

        Line(register + " = " + formula);
        _spilled.Add(formula, register);
        return register;
    }

    /// <summary>
    /// The text that stands for a SYS_ name in a formula before it is written, $SYS_POS_X, where [system_variables] of
    /// the machine gives it an FN 18 read; null where it gives none (machine-config 7; D182, D397).
    /// </summary>
    public string? SystemReadable(VariableNode variable)
    {
        return SystemIds(variable, render: false) is null ? null : variable.ToCanonical();
    }

    /// <summary>
    /// The QL parameter a SYS_ name is read into, set by FN 18: SYSREAD QL499 = ID270 NR1 IDX1 in a block of its own
    /// written now, before the line that reads it; the highest free QL of the D338 pool, one per distinct name in the
    /// block (D182, D397). Null where [system_variables] gives the name no FN 18 read or no QL is free.
    /// </summary>
    public string? SystemRead(VariableNode variable)
    {
        if (SystemIds(variable, render: true) is not string ids)
        {
            return null;
        }

        string key = SystemReadKey + ids;
        if (_spilled.TryGetValue(key, out string? held))
        {
            return held;
        }

        if (Spills.Free(_spilled.Count) is not string register)
        {
            return null;
        }

        // A position read needs the real time: FN 20: WAIT FOR SYNC stops the look-ahead until the program has
        // reached the block (iTNC 530 Programmierung-Klartext 340490-05, connector p. 313-314, the example of ID270).
        if (s_realTimeIds.Any(id => ids.StartsWith(id, StringComparison.Ordinal)))
        {
            Line(WaitForSync);
        }

        Line(SystemReadKey + register + " = " + ids);
        _spilled.Add(key, register);
        return register;
    }

    /// <summary>
    /// Reports an ERROR on the block (D98).
    /// </summary>
    public void Error(string code, string message)
    {
        Diagnostics.Error(Block, code, message);
    }

    /// <summary>
    /// Reports a WARNING on the block (D98).
    /// </summary>
    public void Warning(string code, string message)
    {
        Diagnostics.Warning(Block, code, message);
    }

    /// <summary>
    /// Words as canonical NCX writes them, for a message.
    /// </summary>
    public static string WordsText(IEnumerable<Word> words)
    {
        var texts = new List<string>();
        foreach (Word word in words)
        {
            texts.Add(word.ToCanonical());
        }

        return string.Join(" ", texts);
    }

    // The ids of the FN 18 read of a SYS_ name, ID270 NR1 IDX1, from [system_variables] with {index} the number of
    // its index; when not rendered, any text for a name the machine maps with a number or no index; null for a name
    // the machine does not map, or maps to nothing, and for an index that is not a number (machine-config 7; D182).
    private string? SystemIds(VariableNode variable, bool render)
    {
        string? template = Machine.SystemVariables.Find(variable.Name);
        if (string.IsNullOrEmpty(template))
        {
            return null;
        }

        var values = new TemplateValues();
        if (variable.Index is NumberNode index
            && decimal.TryParse(index.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
        {
            values.Set("index", number);
        }
        else if (variable.Index is not null)
        {
            return null;
        }

        if (!render)
        {
            return template;
        }

        Template parsed = Templates.For(template) ?? new Template(template, 1, new Diagnostics(Machine.Machine.Name));
        return parsed.Render(values, Block, Diagnostics);
    }

    // The line with each run of spaces as one space and none at its end.
    private static string CollapseSpaces(string line)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string leading = line.Length - line.TrimStart(' ').Length > 0 ? " " : "";
        return leading + string.Join(' ', parts);
    }
}
