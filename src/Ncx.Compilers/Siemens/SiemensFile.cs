using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// What the Siemens compiler knows of the whole NCX file while it writes one block of it (controllers siemens.md 1, 8,
/// 12 rule 1): the name of the unit of every program and subprogram, the parameters of every subprogram from the
/// arguments its calls give, the labels of every section, the label before the end of a program that a JUMP=END
/// reaches, and the variable names every unit uses, which its definition part declares (siemens.md 12 rule 7, D261).
/// </summary>
internal sealed partial class SiemensFile
{
    // The label a JUMP=END goes to, before the program end (controller-mapping 1, JUMP=END).
    private const string EndLabel = "PROGRAM_END";

    // A label of an integer, or of a name whose first two characters are not letters (controllers siemens.md 8).
    private const string LabelPrefix = "LABEL_";

    // The prefix of the NCX system variables, which no DEF declares (language 4.12; D51).
    private const string SystemPrefix = "SYS_";

    // The statements of the language of controllers siemens.md 8 and 9, which no label may be.
    private static readonly HashSet<string> s_keywords = new(StringComparer.Ordinal)
    {
        "IF", "ELSE", "ENDIF", "LOOP", "ENDLOOP", "FOR", "TO", "ENDFOR", "WHILE", "ENDWHILE", "REPEAT", "REPEATB",
        "UNTIL", "CASE", "OF", "DEFAULT", "GOTO", "GOTOF", "GOTOB", "GOTOC", "GOTOS", "PROC", "RET", "DEF", "EXTERN",
        "CALL", "MCALL", "PCALL", "EXTCALL", "DEFINE", "AS", "SAVE", "STOPRE", "MSG", "INIT", "START", "WAITE", "WAITM",
        "WAITMC", "SETM", "CLEARM", "AND", "OR", "NOT", "XOR", "DIV", "MOD",
    };

    private readonly IReadOnlyList<BlockStep> _steps;
    private readonly Dictionary<Section, string> _unitNames = [];
    private readonly Dictionary<Section, string> _endLabels = [];
    private readonly Dictionary<string, List<string>> _parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Section> _subs = new(StringComparer.Ordinal);

    // The jumps over a conditional call: the target state of the program or walk and what the control has active at
    // the jump, which the block after the call meets once the subprogram has returned (SiemensFlow).
    private readonly List<(TargetState Target, Dictionary<string, string> AtJump)> _jumpsOverCalls = [];

    // The variable names every unit uses, in the order of their first use, and how many units use each of them
    // (D261).
    private readonly Dictionary<Section, List<string>> _names = [];
    private readonly Dictionary<string, int> _unitsOfName = new(StringComparer.Ordinal);

    // The names a RAW:SIEMENS block of a unit declares itself, DEF REAL LEN the reader kept (D261).
    private readonly Dictionary<Section, HashSet<string>> _rawDeclared = [];

    // The names a VAR word of the file assigns a string to, which the definition part declares STRING (D169).
    private readonly HashSet<string> _stringNames = new(StringComparer.Ordinal);

    // The RAW PROC line of a subprogram, PROC NAME SAVE as the reader keeps it (controller-mapping 6, SUB=BEGIN): a
    // RAW PROC line that is the first block of the SUB section, as the reader declares the subprogram by a PROC at the
    // top of its unit only (SiemensUnits.Read); a PROC inside a unit stays a RAW line of its own
    // (SiemensCalls.ReadProc).
    private readonly Dictionary<Section, Block> _procRaws = [];

    // The block numbers the RAW lines of each unit carry, with the block that carries each (D417).
    private readonly Dictionary<(Section? Unit, int Number), Block> _rawNumbers = [];

    // The SUB sections whose first block the walk has met.
    private readonly HashSet<Section> _subsBegun = [];

    private SiemensFile(IReadOnlyList<BlockStep> steps)
    {
        _steps = steps;
        foreach (BlockStep step in steps)
        {
            if (step.Section is Section section && section.Kind == SectionKind.Sub && section.Name is string name)
            {
                _subs.TryAdd(name, section);
            }

            string? raw = (step.Block.Find("RAW")?.Value as StringValue)?.Content;
            if (raw is not null)
            {
                RawFrameKeys.UnionWith(SiemensFrames.KeysNamedBy(raw));
            }

            if (step.Section is Section { Kind: SectionKind.Sub } sub && !step.Block.Has("SUB")
                && _subsBegun.Add(sub) && raw is not null && SiemensProcLine.Parse(raw) is not null)
            {
                _procRaws.Add(sub, step.Block);
            }
        }

        CollectParameters();
        CollectNames();
    }

    /// <summary>
    /// The keys of the target state of the programmable frame and the swivel that a RAW line of the file may change
    /// (SiemensFrames.AfterRaw, controllers siemens.md 4).
    /// </summary>
    public HashSet<string> RawFrameKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The file of a run: every step of the STATIC walk (architecture 8).
    /// </summary>
    public static SiemensFile Of(IReadOnlyList<BlockStep> steps)
    {
        return new SiemensFile(steps);
    }

    /// <summary>
    /// Records the block number a RAW line of a unit carries; false where another block of the unit carries it
    /// already, which leaves a GOTO to it no one block (controllers siemens.md 1, 8; D417).
    /// </summary>
    /// <param name="unit">The program or subprogram of the line.</param>
    /// <param name="number">The N of the line.</param>
    /// <param name="block">The block of the line; a further walk of the same block is no second line.</param>
    public bool TakeBlockNumber(Section? unit, int number, Block block)
    {
        if (_rawNumbers.TryGetValue((unit, number), out Block? taken))
        {
            return ReferenceEquals(taken, block);
        }

        _rawNumbers[(unit, number)] = block;
        return true;
    }

    /// <summary>
    /// The subprogram of the file a CALL names by its NAME; null for an external program (language 4.9, CALL).
    /// </summary>
    public Section? SubNamed(Value call)
    {
        if (call is StringValue)
        {
            return null;
        }

        return _subs.GetValueOrDefault(call.ToCanonical());
    }

    /// <summary>
    /// The name of the unit of a program or subprogram: %_N_NAME_MPF, PROC NAME (controllers siemens.md 1, 8, 12 rule
    /// 1): the NAME of the section in capitals; a subprogram named by a number is L and the number, the form of L100;
    /// a character a SINUMERIK name does not hold is an underscore, with a WARNING once per section; a name another
    /// section of the same kind has takes _2 behind it, with a WARNING.
    /// </summary>
    /// <param name="section">The program or subprogram.</param>
    /// <param name="fileStem">The name of the NCX file, for a program without NAME and NUMBER.</param>
    /// <param name="write">The block being written, where the WARNING stands.</param>
    public string UnitNameOf(Section section, string fileStem, SiemensBlock write)
    {
        if (_unitNames.TryGetValue(section, out string? known))
        {
            return known;
        }

        // A subprogram named by a number is called as L100, where the leading zeros count (controllers siemens.md 8).
        string given = section.Name
            ?? section.Number?.ToString(CultureInfo.InvariantCulture)
            ?? fileStem;
        string written = Written(given);
        string name = section.Kind == SectionKind.Sub && IsDigits(given) ? "L" + given : written;
        if (!string.Equals(written, given, StringComparison.OrdinalIgnoreCase))
        {
            write.Warning(DiagnosticCodes.SiemensNameChanged,
                $"The {(section.Kind == SectionKind.Sub ? "subprogram" : "program")} {given} is written as {name}: a "
                + "SINUMERIK name holds letters, digits and underscores (controllers siemens.md 1).");
        }

        // One file per unit and one unit per name (controllers siemens.md 1, 12 rule 1): a name another program or
        // subprogram of the file already has takes _2 behind it, as the framework names the files of two programs of
        // one name, so that no unit replaces another (language 2 rule 8).
        string unique = name;
        for (int count = 2; IsTaken(section.Kind, unique); count++)
        {
            unique = name + "_" + count.ToString(CultureInfo.InvariantCulture);
        }

        if (unique != name)
        {
            write.Warning(DiagnosticCodes.SiemensUnitNameTaken,
                $"The {(section.Kind == SectionKind.Sub ? "subprogram" : "program")} {given} is written as {unique}: "
                + $"another {(section.Kind == SectionKind.Sub ? "subprogram" : "program")} of the file is the unit "
                + $"{name}, and a SINUMERIK file holds one unit per name (controllers siemens.md 1, 12 rule 1).");
        }

        _unitNames.Add(section, unique);
        return unique;
    }

    /// <summary>
    /// The parameters of the PROC of a subprogram, the names of the ARG words its calls give, in the order they first
    /// stand in the file (controllers siemens.md 8; controller-mapping 6, CALL + ARG).
    /// </summary>
    public IReadOnlyList<string> ParametersOf(Section sub)
    {
        return sub.Name is string name && _parameters.TryGetValue(name, out List<string>? parameters)
            ? parameters
            : [];
    }

    /// <summary>
    /// The variable names a program or subprogram uses, in the order of their first use: the addresses of its VAR
    /// words and the variables its expressions read, without the system variables (D261).
    /// </summary>
    /// <param name="unit">The program or subprogram.</param>
    public IReadOnlyList<string> NamesUsedBy(Section unit)
    {
        return _names.TryGetValue(unit, out List<string>? names) ? names : [];
    }

    /// <summary>
    /// True where a VAR word of the file assigns the name a string, so that its DEF is STRING, the one type the control
    /// compares with == and &lt;&gt; as text (D169).
    /// </summary>
    /// <param name="name">The NCX variable name.</param>
    public bool HoldsString(string name)
    {
        return _stringNames.Contains(name);
    }

    /// <summary>
    /// True where more than one unit of the file uses the name: a DEF is local to its unit, so the value does not
    /// carry over and the compiler may not declare the name twice (D261).
    /// </summary>
    /// <param name="name">The NCX variable name.</param>
    public bool IsUsedBySeveralUnits(string name)
    {
        return _unitsOfName.GetValueOrDefault(name) > 1;
    }

    /// <summary>
    /// True where a RAW:SIEMENS block of the unit declares the name itself, a DEF the reader kept (D261).
    /// </summary>
    /// <param name="unit">The program or subprogram.</param>
    /// <param name="name">The NCX variable name.</param>
    public bool IsDeclaredByRaw(Section unit, string name)
    {
        return _rawDeclared.TryGetValue(unit, out HashSet<string>? declared) && declared.Contains(name);
    }

    /// <summary>
    /// The subprograms with parameters that the blocks of a program or subprogram call, which EXTERN announces in it
    /// (controllers siemens.md 8).
    /// </summary>
    public List<Section> CalledWithParameters(Section unit)
    {
        var called = new List<Section>();
        foreach (BlockStep step in _steps)
        {
            if (step.Section != unit || step.Block.Find("CALL")?.Value is not Value target)
            {
                continue;
            }

            if (SubNamed(target) is Section sub && ParametersOf(sub).Count > 0 && !called.Contains(sub))
            {
                called.Add(sub);
            }
        }

        return called;
    }

    /// <summary>
    /// The SINUMERIK label of an NCX label, NAME: at the block start (controllers siemens.md 8): an identifier whose
    /// first two characters are letters or underscores as it is, any other label, and a word of the language of siemens
    /// 8 and 9 such as LOOP, with LABEL_ in front.
    /// </summary>
    public static string LabelOf(Value label)
    {
        string text = label.ToCanonical();
        bool letters = text.Length >= 2 && IsLabelStart(text[0]) && IsLabelStart(text[1]);
        return label is IdentValue && letters && !s_keywords.Contains(text) ? text : LabelPrefix + text;
    }

    /// <summary>
    /// The label before the end of a program that JUMP=END goes to: PROGRAM_END, or PROGRAM_END_2 where the program has
    /// a label of that name (controller-mapping 1, JUMP=END).
    /// </summary>
    public string EndLabelOf(Section program)
    {
        if (_endLabels.TryGetValue(program, out string? known))
        {
            return known;
        }

        var labels = new List<string>();
        foreach (BlockStep step in _steps)
        {
            if (step.Section == program && step.Block.Find("LABEL")?.Value is Value label)
            {
                labels.Add(LabelOf(label));
            }
        }

        string name = EndLabel;
        for (int count = 2; labels.Contains(name); count++)
        {
            name = EndLabel + "_" + count.ToString(CultureInfo.InvariantCulture);
        }

        _endLabels.Add(program, name);
        return name;
    }

    /// <summary>
    /// True when the RAW PROC line of the subprogram declares the attribute SAVE, PROC NAME(...) SAVE as the reader
    /// keeps it (controller-mapping 6, SUB=BEGIN), whose plain end is M17 rather than RET (D381; 840D sl
    /// NC-Programmierung SW4.92, p.511, p.523). The compiler's own PROC line never declares SAVE
    /// (SiemensFlow.ProcLine).
    /// </summary>
    public bool DeclaresSave(Section sub)
    {
        return ProcRawOf(sub) is Block block && block.Find("RAW")?.Value is StringValue raw
            && SiemensProcLine.Parse(raw.Content) is ProcLineParts parts
            && parts.Attributes.Contains("SAVE", StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The block of the RAW PROC line of the subprogram, null for none (controller-mapping 6, SUB=BEGIN).
    /// </summary>
    public Block? ProcRawOf(Section sub)
    {
        return _procRaws.GetValueOrDefault(sub);
    }

    /// <summary>
    /// True for the block of the RAW PROC line of a subprogram, which the head of its unit writes or reports
    /// (SiemensFlow.ProcLine), and which is not written again where it stands.
    /// </summary>
    public bool IsProcRaw(Block block)
    {
        return _procRaws.ContainsValue(block);
    }

    /// <summary>
    /// True when a JUMP=END, or a RETURN outside a subprogram, stands in the program or in a subprogram the program
    /// calls, so that its end carries the label: a JUMP=END in a subprogram is written RET("label",,$P_STACK), which
    /// needs the label in the program it returns to (controller-mapping 1, JUMP=END; language 4.9, RETURN; D344).
    /// </summary>
    public bool JumpsToEnd(Section program)
    {
        foreach (Section section in Reaches(program))
        {
            foreach (BlockStep step in _steps)
            {
                if (step.Section == section
                    && (step.Block.Has("JUMP", null, "END") || step.Block.Has("RETURN")))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The label that a JUMP=END of a subprogram returns to: the end label of the programs that call it, directly or
    /// through other subprograms, where they all give the same one; null where no program of the file reaches the
    /// subprogram or two of them name different labels (D344).
    /// </summary>
    /// <param name="sub">The subprogram the JUMP=END stands in.</param>
    public string? EndLabelReachedFrom(Section sub)
    {
        string? label = null;
        foreach (BlockStep step in _steps)
        {
            if (step.Section is not Section program || program.Kind == SectionKind.Sub
                || !Reaches(program).Contains(sub))
            {
                continue;
            }

            string name = EndLabelOf(program);
            if (label is not null && label != name)
            {
                return null;
            }

            label = name;
        }

        return label;
    }

    // The section itself and every subprogram it reaches through the calls of the file (language 4.9; D99).
    private HashSet<Section> Reaches(Section section)
    {
        var reached = new HashSet<Section> { section };
        for (bool grown = true; grown;)
        {
            grown = false;
            foreach (BlockStep step in _steps)
            {
                if (step.Section is not Section from || !reached.Contains(from)
                    || step.Block.Find("CALL")?.Value is not Value call
                    || SubNamed(call) is not Section sub)
                {
                    continue;
                }

                grown |= reached.Add(sub);
            }
        }

        return reached;
    }

    /// <summary>
    /// The block of a label in a section, the target of a JUMP or REPEAT; null when the section has none.
    /// </summary>
    public Block? LabelBlock(Section section, string label)
    {
        foreach (BlockStep step in _steps)
        {
            if (step.Section == section && step.Block.Find("LABEL")?.Value.ToCanonical() == label)
            {
                return step.Block;
            }
        }

        return null;
    }

    /// <summary>
    /// Records what the control has active at the jump over a conditional call, IF NOT (cond) GOTOF past the call
    /// (controller-mapping 6, structured loops).
    /// </summary>
    public void JumpOverCall(TargetState target)
    {
        _jumpsOverCalls.Add((target, new Dictionary<string, string>(target.Active, StringComparer.Ordinal)));
    }

    /// <summary>
    /// The first block written into a target state after a conditional call, once the subprogram has returned: the
    /// control arrives at the label after the call from the call, with what its walk wrote (virtual machine 3.9, D99),
    /// or from the jump over it, with what was active at the jump, so every value the call changed is unknown.
    /// </summary>
    public void MeetJumpsOverCalls(SiemensBlock write)
    {
        for (int index = _jumpsOverCalls.Count - 1; index >= 0; index--)
        {
            (TargetState target, Dictionary<string, string> atJump) = _jumpsOverCalls[index];
            if (!ReferenceEquals(target, write.Target))
            {
                continue;
            }

            _jumpsOverCalls.RemoveAt(index);
            foreach (string key in target.Active.Keys.ToList())
            {
                if (!atJump.TryGetValue(key, out string? value) || target.ActiveOf(key) != value)
                {
                    write.MakeUnknown(key);
                }
            }
        }
    }

    // The names of the ARG words of every CALL of a subprogram of the file, in file order (language 4.9, ARG).
    private void CollectParameters()
    {
        foreach (BlockStep step in _steps)
        {
            if (step.Block.Find("CALL")?.Value is not Value target || SubNamed(target)?.Name is not string name)
            {
                continue;
            }

            if (!_parameters.TryGetValue(name, out List<string>? parameters))
            {
                parameters = [];
                _parameters.Add(name, parameters);
            }

            foreach (Word word in step.Block.Words)
            {
                if (word.Key == "ARG" && word.Addr is string argument && !parameters.Contains(argument))
                {
                    parameters.Add(argument);
                }
            }
        }
    }

    // The variable names of every unit, in the order of their first use, and the names a RAW:SIEMENS DEF of a unit
    // declares itself: what the definition part of the unit declares with DEF REAL and what it must leave alone
    // (controllers siemens.md 12 rule 7; D261).
    private void CollectNames()
    {
        foreach (BlockStep step in _steps)
        {
            if (step.Section is not Section unit)
            {
                continue;
            }

            if (step.Block.Find("RAW")?.Value is StringValue raw)
            {
                foreach (string declared in NamesDefinedBy(raw.Content))
                {
                    if (!_rawDeclared.TryGetValue(unit, out HashSet<string>? names))
                    {
                        names = new HashSet<string>(StringComparer.Ordinal);
                        _rawDeclared.Add(unit, names);
                    }

                    names.Add(declared);
                }
            }

            foreach (Word word in step.Block.Words)
            {
                if (word.Key == "VAR" && word.Addr is string assigned)
                {
                    AddName(unit, assigned);
                    if (word.Value is StringValue)
                    {
                        _stringNames.Add(assigned);
                    }
                }

                if (word.Value is ExprValue { Tree: ExprNode tree })
                {
                    AddRead(unit, tree);
                }
            }
        }
    }

    // The variables an expression reads, the system variables of [system_variables] excluded: they are no user
    // variables and no DEF declares them (language 4.12; D51, D261).
    private void AddRead(Section unit, ExprNode node)
    {
        switch (node)
        {
            case VariableNode variable:
                if (!variable.Name.StartsWith(SystemPrefix, StringComparison.Ordinal))
                {
                    AddName(unit, variable.Name);
                }

                if (variable.Index is ExprNode index)
                {
                    AddRead(unit, index);
                }

                break;
            case ParenthesesNode parentheses:
                AddRead(unit, parentheses.Inner);
                break;
            case UnaryNode unary:
                AddRead(unit, unary.Operand);
                break;
            case BinaryNode binary:
                AddRead(unit, binary.Left);
                AddRead(unit, binary.Right);
                break;
            case CallNode call:
                foreach (ExprNode argument in call.Arguments)
                {
                    AddRead(unit, argument);
                }

                break;
            default:
                break;
        }
    }

    private void AddName(Section unit, string name)
    {
        if (!_names.TryGetValue(unit, out List<string>? names))
        {
            names = [];
            _names.Add(unit, names);
        }

        if (names.Contains(name))
        {
            return;
        }

        names.Add(name);
        _unitsOfName[name] = _unitsOfName.GetValueOrDefault(name) + 1;
    }

    // The names a DEF of a RAW:SIEMENS line declares: "DEF REAL LEN, WIDTH=3" declares LEN and WIDTH (controllers
    // siemens.md 8; D261).
    private static List<string> NamesDefinedBy(string raw)
    {
        var names = new List<string>();
        foreach (Match definition in DefLine().Matches(raw))
        {
            foreach (string part in definition.Groups[1].Value.Split(','))
            {
                string name = part.Split('=')[0].Trim().ToUpperInvariant();
                int bracket = name.IndexOf('[', StringComparison.Ordinal);
                if (bracket > 0)
                {
                    name = name.Substring(0, bracket);
                }

                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    // Letters, digits and underscores in capitals (controllers siemens.md 1).
    private static string Written(string name)
    {
        var text = new StringBuilder();
        foreach (char character in name.ToUpperInvariant())
        {
            text.Append(char.IsAsciiLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return text.ToString();
    }

    // A unit name that a section of the same kind already has; names are case-insensitive (controllers siemens.md 1),
    // and every name is written in capitals.
    private bool IsTaken(SectionKind kind, string name)
    {
        foreach (KeyValuePair<Section, string> unit in _unitNames)
        {
            if (unit.Key.Kind == kind && unit.Value == name)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLabelStart(char character)
    {
        return char.IsAsciiLetter(character) || character == '_';
    }

    // A DEF of a RAW line: the type, then the names it declares up to the end of the line.
    [GeneratedRegex(@"\bDEF\s+(?:INT|REAL|BOOL|CHAR|AXIS|FRAME|STRING\s*\[[^\]]*\])\s+([^\r\n;]*)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DefLine();
}
