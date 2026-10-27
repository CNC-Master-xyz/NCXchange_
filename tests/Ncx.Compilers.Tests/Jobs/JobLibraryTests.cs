using Ncx.Compilers.Heidenhain;
using Ncx.Compilers.Siemens;
using Ncx.Compilers.Tests.Heidenhain;
using Ncx.Compilers.Tests.Siemens;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Compilers.Tests.Jobs;

/// <summary>
/// The library file of a job (D316, D388 (13)) on Heidenhain and Siemens, a job of one channel without one (language
/// 4.13), and the renamed program of a renamed channel file (D310 as D266).
/// </summary>
public sealed class JobLibraryTests
{
    // D316: on Heidenhain each uncalled SUB is a program of its own; a call of another library SUB inside it is
    // CALL PGM of that program, since its LBL stands in another program now.
    [Fact]
    public void Heidenhain_LibrarySubCallsAnother_WritesCallPgm()
    {
        CompileResult result = Run(new HeidenhainCompiler(), HeidenhainCompile.Mill(),
            File("UNITS=MM"), File("UNITS=MM", "SUB=BEGIN NAME=20", "RAPID X=2", "CALL=30",
                "SUB=END", "SUB=BEGIN NAME=30", "RAPID X=3", "SUB=END"));

        CompiledFile library = Assert.Single(result.Files, file => file.Name == "PART_LIB_20.h");
        Assert.Contains("CALL PGM PART_LIB_30", library.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("CALL LBL", library.Text, StringComparison.Ordinal);
        Assert.Contains(result.Files, file => file.Name == "PART_LIB_30.h");
    }

    // D316: on Siemens each uncalled SUB is an SPF of its own, named by its unit.
    [Fact]
    public void Siemens_UncalledSub_IsAnSpfOfItsOwn()
    {
        CompileResult result = Run(new SiemensCompiler(), Dmg(),
            File("UNITS=MM", "SYNC=50"), File("UNITS=MM", "SYNC=50", "SUB=BEGIN NAME=20", "DWELL=1", "SUB=END"));

        CompiledFile library = Assert.Single(result.Files,
            file => file.Name.EndsWith(".spf", StringComparison.Ordinal));
        Assert.Equal("L20.spf", library.Name);
        Assert.StartsWith("%_N_L20_SPF", library.Text, StringComparison.Ordinal);
    }

    // D310 as D266: two DMG channel programs of one NAME are written as T_C1.mpf and T_C2.mpf, and the unit header of
    // each names its file.
    [Fact]
    public void Siemens_ChannelFilesOfOneName_RenameTheirUnits()
    {
        CompileResult result = Run(new SiemensCompiler(), Dmg(), File("UNITS=MM", "SYNC=50"),
            File("UNITS=MM", "SYNC=50"));

        Assert.Equal(["T_C1.mpf", "T_C2.mpf"], result.Files.Select(file => file.Name).ToList());
        Assert.StartsWith("%_N_T_C1_MPF", result.Files[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("%_N_T_C2_MPF", result.Files[1].Text, StringComparison.Ordinal);
    }

    // Language 4.13, D264: a job of one channel has no library file; its uncalled SUB stands in its own file without
    // CMP720, as a compile of one file writes it.
    [Fact]
    public void OneChannelJob_UncalledSub_StandsInTheChannelFile()
    {
        CompileResult result = JobCompile.Run(JobCompile.Machine(),
            JobCompile.Program(1000, "DWELL=1").Replace("FILE=END", "SUB=BEGIN NAME=200\nDWELL=2\nSUB=END\nFILE=END",
                StringComparison.Ordinal));

        JobCompile.TextsOf(result);
        CompiledFile file = Assert.Single(result.Files);
        Assert.Contains("O0200", file.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(DiagnosticCodes.SubprogramOfNoChannel, JobCompile.CodesOf(result));
    }

    // A SUB the walks enter twice, once called by an uncalled SUB and once as uncalled itself (D99), reports its
    // missing program number once (CMP301).
    [Fact]
    public void Fanuc_SubWithoutProgramNumberWalkedTwice_ReportsCmp301Once()
    {
        string file = string.Join('\n',
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1000",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "PROGRAM=END",
            "SUB=BEGIN NAME=\"S20\"",
            "CALL=\"S30\"",
            "SUB=END",
            "SUB=BEGIN NAME=\"S30\"",
            "DWELL=1",
            "SUB=END",
            "FILE=END") + "\n";

        CompileResult result = JobCompile.Run(JobCompile.Machine(), file);

        Assert.Single(result.Diagnostics.Items, diagnostic => diagnostic.Line == 8
            && diagnostic.Code == DiagnosticCodes.FanucSubNameNotAProgramNumber);
    }

    // D316: two files of a job whose uncalled SUBs share an O number put it into _LIB and _LIB_2: the WARNING CMP722.
    [Fact]
    public void Fanuc_TwoLibrariesWithOneONumber_WarnCmp722()
    {
        string withSub = JobCompile.Program(1000, "SYNC=110").Replace("FILE=END",
            "SUB=BEGIN NAME=200\nDWELL=2\nSUB=END\nFILE=END", StringComparison.Ordinal);

        CompileResult result = JobCompile.Run(JobCompile.Machine(), withSub, withSub);

        Assert.Contains(result.Files, file => file.Name == "JOB_LIB.nc");
        Assert.Contains(result.Files, file => file.Name == "JOB_LIB_2.nc");
        Diagnostic warning = Assert.Single(result.Diagnostics.Items,
            diagnostic => diagnostic.Code == DiagnosticCodes.LibraryProgramTwice);
        Assert.Contains("O0200", warning.Message, StringComparison.Ordinal);
    }

    // The DMG CTX of machines/, channel_files = "program_name".
    private static MachineConfig Dmg()
    {
        return SiemensCompile.Variant("machines/dmg-ctx-840d.toml", text => text);
    }

    // A file of one program named T with these blocks between its header and PROGRAM=END; the blocks from SUB=BEGIN
    // on follow PROGRAM=END.
    private static string File(params string[] blocks)
    {
        var lines = new List<string> { "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\" NUMBER=1000" };
        int sub = Array.FindIndex(blocks, block => block.StartsWith("SUB=", StringComparison.Ordinal));
        lines.AddRange(sub < 0 ? blocks : blocks[..sub]);
        lines.Add("PROGRAM=END");
        if (sub >= 0)
        {
            lines.AddRange(blocks[sub..]);
        }

        lines.Add("FILE=END");
        return string.Join('\n', lines) + "\n";
    }

    // A job PART whose channel n runs the n-th file, compiled with the compiler of the family.
    private static CompileResult Run(ICompiler compiler, MachineConfig machine, params string[] files)
    {
        var channels = new List<ChannelProgram>();
        var parsed = new Dictionary<string, NcxProgram>();
        for (int index = 0; index < files.Length; index++)
        {
            string name = "C" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ncx";
            channels.Add(new ChannelProgram { Id = index + 1, File = name });
            parsed[name] = Parser.Parse(files[index], name, new ParserOptions());
        }

        var job = new JobManifest { Name = "PART", Machine = "test.toml", Channels = channels };
        CompileResult result = new JobCompiler(compiler).Compile("PART.ncxjob.toml", job, parsed, machine,
            new CompileOptions());
        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        return result;
    }
}
