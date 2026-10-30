using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

// The output files (machine-config 2, D48; language 4.13, D99): the programs and subprograms of the NCX file laid out
// per program_layout, and the warning block of D10 at the head of a program whose {kind} took the default.
public abstract partial class CompilerBase
{
    // What the warning block of D10 names when the machine file names no tool table.
    private const string NoToolTable = "NONE, THE MACHINE FILE NAMES NO TOOL_TABLE";

    // The final file names of the programs under file_per_program, once per compile (D266).
    private Dictionary<Section, string>? _programFileNames;

    // The output files of the compile, per program_layout (machine-config 2, D48).
    // A machine file that leaves program_layout out takes the default of its controller family, file_per_program for
    // Heidenhain and one_file else (row W3-3; heidenhain 1, 8 rule 6; language 4.13; ProgramLayouts).
    private List<CompiledFile> Files()
    {
        NcxProgram program = _program ?? throw NotCompiling();
        string stem = Path.GetFileNameWithoutExtension(program.FileName);
        List<CompiledFile> files =
            ProgramLayouts.Of(Machine) == ProgramLayout.FilePerProgram ? FilePerProgram() : OneFile(stem);
        if (Options.Job?.Library is string library && UncalledSubs() is { Count: > 0 } uncalled)
        {
            files.AddRange(LibraryFiles(library, uncalled));
        }

        return files;
    }

    // The SUB sections of the file that no program of this compile calls, in file order (D99, D264).
    private List<Section> UncalledSubs()
    {
        NcxProgram source = _program ?? throw NotCompiling();
        var uncalled = new List<Section>();
        foreach (Section sub in source.Subs)
        {
            if (!_programs.Exists(program => program.CalledSubs.Contains(sub)))
            {
                uncalled.Add(sub);
            }
        }

        return uncalled;
    }

    /// <summary>
    /// D316: the library file of a job, which holds every SUB that no channel program calls, once, named &lt;job
    /// name&gt;_LIB with the family's extension (D388 (13)): the frame of the file around the subprograms as the family
    /// writes them, on Fanuc one O program per SUB as under file_per_program (ready for //CNC_MEM/USER/LIBRARY), on
    /// Siemens one SPF unit per SUB. A family whose subprograms are no programs of their own overrides it.
    /// </summary>
    /// <param name="library">The name of the library file without extension.</param>
    /// <param name="subs">The subprograms no channel program calls, in file order.</param>
    private protected virtual List<CompiledFile> LibraryFiles(string library, IReadOnlyList<Section> subs)
    {
        OutputBuffer output = NewOutput();
        output.Add(_fileBegin);
        WriteSubprograms(output, subs);
        output.Add(_fileEnd);
        return [new CompiledFile { Name = library + FileExtension, Text = output.ToText(Diagnostics), Library = true }];
    }

    /// <summary>
    /// The lines of a subprogram as its first walk wrote them (D99); empty for one no walk wrote or one written inline
    /// (D278).
    /// </summary>
    private protected IReadOnlyList<OutputLine> SubLines(Section sub)
    {
        return _subs.TryGetValue(sub, out SubText? text) && !IsInlined(sub) ? text.Lines : [];
    }

    /// <summary>
    /// The SUB=BEGIN block of a subprogram of the file being compiled.
    /// </summary>
    private protected Block SubBeginBlock(Section sub)
    {
        NcxProgram source = _program ?? throw NotCompiling();
        return source.Blocks[sub.FirstBlock];
    }

    // program_layout = "one_file": every program and subprogram of the NCX file in one output file (machine-config 2,
    // D48), the programs in file order, each SUB once, after the first program that calls it (language 4.13, D99).
    // D264: every SUB is written at least once; under one_file a SUB that no program calls follows the last program,
    // before FILE=END.
    private List<CompiledFile> OneFile(string stem)
    {
        NcxProgram source = _program ?? throw NotCompiling();
        OutputBuffer output = NewOutput();
        output.Add(_fileBegin);
        var placed = new List<Section>();
        foreach (ProgramText program in _programs)
        {
            WriteProgram(output, program);
            var subs = new List<Section>();
            foreach (Section sub in source.Subs)
            {
                if (program.CalledSubs.Contains(sub) && !placed.Contains(sub))
                {
                    subs.Add(sub);
                    placed.Add(sub);
                }
            }

            WriteSubprograms(output, subs);
            output.Add(program.Footer);
        }

        // D316: in a channel program of a job an uncalled SUB goes to the library file of the job instead.
        var uncalled = new List<Section>();
        foreach (Section sub in source.Subs)
        {
            if (!placed.Contains(sub) && Options.Job?.Library is null)
            {
                uncalled.Add(sub);
            }
        }

        WriteSubprograms(output, uncalled);
        output.Add(_fileEnd);
        return [new CompiledFile { Name = stem + FileExtension, Text = output.ToText(Diagnostics) }];
    }

    // program_layout = "file_per_program": one output file per program, the subprograms copied after the M30 of every
    // program that calls them (machine-config 2, D48; language 4.13, D99). D264: a SUB that no program calls is
    // written once, into the first program's file, after that program's subprograms and its M30, before its footer.
    // In a channel program of a job D316 applies instead: the job compiler reports such a SUB, and the library file of
    // the job holds it (LibraryFiles), since one channel cannot reach another channel's file.
    private List<CompiledFile> FilePerProgram()
    {
        NcxProgram source = _program ?? throw NotCompiling();
        var files = new List<CompiledFile>();
        var called = new List<Section>();
        foreach (ProgramText program in _programs)
        {
            foreach (Section sub in source.Subs)
            {
                if (program.CalledSubs.Contains(sub) && !called.Contains(sub))
                {
                    called.Add(sub);
                }
            }
        }

        var uncalled = new List<Section>();
        if (Options.Job?.Library is null)
        {
            foreach (Section sub in source.Subs)
            {
                if (!called.Contains(sub))
                {
                    uncalled.Add(sub);
                }
            }
        }

        foreach (ProgramText program in _programs)
        {
            OutputBuffer output = NewOutput();
            output.Add(_fileBegin);
            WriteProgram(output, program);
            var subs = new List<Section>();
            foreach (Section sub in source.Subs)
            {
                if (program.CalledSubs.Contains(sub))
                {
                    subs.Add(sub);
                }
            }

            WriteSubprograms(output, subs);
            if (ReferenceEquals(program, _programs[0]))
            {
                WriteSubprograms(output, uncalled);
            }

            output.Add(program.Footer);
            output.Add(_fileEnd);
            string name = ProgramFileName(program.Program) + FileExtension;
            files.Add(new CompiledFile { Name = name, Text = output.ToText(Diagnostics) });
        }

        return files;
    }

    // The lines of a program, with the warning block of D10 at its head when a {kind} took the default.
    private void WriteProgram(OutputBuffer output, ProgramText program)
    {
        output.Add(program.Lines.GetRange(0, program.HeadEnd));
        var tools = new List<ToolRef>(program.ToolsWithoutData);
        if (ReferenceEquals(program, _programs[0]))
        {
            foreach (ToolRef tool in _toolsWithoutDataOutsidePrograms)
            {
                if (!tools.Contains(tool))
                {
                    tools.Add(tool);
                }
            }
        }

        if (tools.Count > 0)
        {
            output.Add(ToolDataWarning(program, tools));
        }

        output.Add(program.Lines.GetRange(program.HeadEnd, program.Lines.Count - program.HeadEnd));
    }

    // Each SUB section once, as its first walk wrote it (D99), in the order of the file.
    private void WriteSubprograms(OutputBuffer output, IReadOnlyList<Section> subs)
    {
        foreach (Section sub in subs)
        {
            if (_subs.TryGetValue(sub, out SubText? text) && !IsInlined(sub))
            {
                output.Add(text.Lines);
            }
        }
    }

    // D10: without the tool table, or for a tool it does not describe, {kind} takes the default and the compiler
    // writes a warning block at the head of the program that names the expected file and the tools it lacks, so that
    // nobody runs the program by accident. Its lines pass BLOCK_WRITE as lines of PROGRAM=BEGIN.
    // D265: the block is comment lines of the controller that name D10, the expected file and the tools without data
    // with the kind written; it stands only in a program where a template with {kind} took the default, after its
    // header, and it stops nothing (decisions.md; machine-config 1).
    private List<OutputLine> ToolDataWarning(ProgramText program, List<ToolRef> tools)
    {
        Block block = program.Begin.Block;
        var toolNames = new List<string>();
        foreach (ToolRef tool in tools)
        {
            toolNames.Add(tool.ToString());
        }

        string[] texts =
        [
            "WARNING: TOOL DATA MISSING, D10",
            "EXPECTED TOOL TABLE: " + (Options.ToolTableFile ?? NoToolTable),
            $"TOOLS WITHOUT DATA: {string.Join(", ", toolNames)} - KIND {DefaultKind} WRITTEN, CHECK BEFORE RUNNING",
        ];
        var lines = new List<string>();
        foreach (string text in texts)
        {
            string written = CommentCharset.Transliterate(text, Machine.Format?.CommentCharset, block, Diagnostics);
            lines.Add(CommentLine(written));
        }

        return OutputLinesOf(RaiseBlockWrite(program.Begin, lines), block);
    }

    private protected OutputBuffer NewOutput()
    {
        return new OutputBuffer(Machine.Format, BlockNumberPrefix, TakesBlockNumber, OwnBlockNumber);
    }

    /// <summary>
    /// The final name of a program's output file under program_layout = "file_per_program", without extension, which
    /// BEGIN PGM and END PGM carry as well (D266): the NAME of the program, the NCX file stem for a program without
    /// NAME; names are compared case-insensitively, and a taken name gets _2, _3 in file order with the WARNING CMP005.
    /// Under one_file the NAME or the stem as they are.
    /// </summary>
    /// <param name="program">A program section of the file being compiled.</param>
    protected string ProgramFileName(Section program)
    {
        NcxProgram source = _program ?? throw NotCompiling();
        string stem = Path.GetFileNameWithoutExtension(source.FileName);
        if (ProgramLayouts.Of(Machine) != ProgramLayout.FilePerProgram)
        {
            return program.Name ?? stem;
        }

        _programFileNames ??= ProgramFileNames(source, stem);
        return _programFileNames.TryGetValue(program, out string? name) ? name : program.Name ?? stem;
    }

    // The final file names of the programs in file order, each taken name with _2, _3 and the WARNING CMP005 (D266).
    private Dictionary<Section, string> ProgramFileNames(NcxProgram source, string stem)
    {
        var names = new Dictionary<Section, string>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Section program in source.Programs)
        {
            string name = program.Name ?? stem;
            string candidate = name;
            for (int suffix = 2; taken.Contains(candidate); suffix++)
            {
                candidate = name + "_" + suffix.ToString(CultureInfo.InvariantCulture);
            }

            if (candidate != name)
            {
                Diagnostics.Warning(source.Blocks[program.FirstBlock], DiagnosticCodes.ProgramNameTaken,
                    $"The program name {name} is taken already by an earlier program of the file (names are compared "
                    + $"case-insensitively), so its file is named {candidate} (machine-config 2, D266).");
            }

            taken.Add(candidate);
            names.Add(program, candidate);
        }

        return names;
    }
}
