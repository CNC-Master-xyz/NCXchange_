using Ncx.Compilers;
using Ncx.Compilers.Siemens;
using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;
using Ncx.Readers;
using Ncx.Readers.Siemens;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance;

/// <summary>
/// The frame offset and the machine position of a facing axis read by the Siemens reader and compiled back by the
/// Siemens compiler, for every combination of DIAMETER and [[axis]] frames_in_diameter on the switchable X axis of
/// millturn1.toml (D125, D240, D388; Funktionshandbuch Grundfunktionen 2012 p.901-903): the reader is the inverse of
/// the compiler, so the X of TRANS, G59 and CYCLE800 comes back as the source wrote it, SHIFT X keeps its NCX value,
/// and the factor the reader puts on $AA_IM[X] and the one the compiler puts on $SYS_MPOS_X multiply to 1.
/// </summary>
public sealed class SiemensFramesInDiameterRoundTrips
{
    /// <summary>
    /// The four combinations: the DIAMON or DIAMOF of the source, frames_in_diameter on the X axis, and the R1 line the
    /// compiler writes back for R1=$AA_IM[X], the reader's factor inside the compiler's.
    /// </summary>
    public static TheoryData<string, bool, string> Combinations()
    {
        return new TheoryData<string, bool, string>
        {
            // Frames in radii (MD20360 bits 3, 6, 7 off): under DIAMON the radius is doubled into the NCX diameter
            // and halved again; under DIAMOF both are radii.
            { "DIAMON", false, "R1=(($AA_IM[X]*2)/2)" },
            { "DIAMOF", false, "R1=$AA_IM[X]" },

            // Frames in diameters (the bits set): under DIAMON both are diameters; under DIAMOF the diameter is
            // halved into the NCX radius and doubled again.
            { "DIAMON", true, "R1=$AA_IM[X]" },
            { "DIAMOF", true, "R1=(($AA_IM[X]/2)*2)" },
        };
    }

    /// <summary>
    /// The frame instructions that carry an X frame value, TRANS, G59 and CYCLE800 with _X0, in each of the four
    /// combinations: the source line, the NCX line the reader writes for its X, and the line the compiler writes back
    /// with the X of the source. G59 replaces the X of the programmable shift and comes back as ATRANS after TRANS Z5
    /// (controllers siemens.md 4); CYCLE800 comes back with the tool carrier of millturn1.toml's TILT_ON and its
    /// reference point (D333); under DIAMON with radius frames its _X0 has no known unit and stays RAW (D125, D333).
    /// </summary>
    public static TheoryData<string, bool, string, string, string> FrameInstructions()
    {
        const string Swivel = "CYCLE800(0,\"TABLE\",0,27,10,0,0,0,45,0,0,0,0,-1,100,1)";
        const string Compiled = "CYCLE800(1,\"TC1\",0,27,10,0,0,0,45,0,0,0,0,-1,100,1)";
        var rows = new TheoryData<string, bool, string, string, string>
        {
            { "DIAMON", false, "TRANS X10 Z5", "SHIFT X=20 Z=5", "TRANS X10 Z5" },
            { "DIAMOF", false, "TRANS X10 Z5", "SHIFT X=10 Z=5", "TRANS X10 Z5" },
            { "DIAMON", true, "TRANS X10 Z5", "SHIFT X=10 Z=5", "TRANS X10 Z5" },
            { "DIAMOF", true, "TRANS X10 Z5", "SHIFT X=5 Z=5", "TRANS X10 Z5" },
            { "DIAMON", false, "TRANS Z5\nG59 X10", "SHIFT X=20", "ATRANS X10" },
            { "DIAMOF", false, "TRANS Z5\nG59 X10", "SHIFT X=10", "ATRANS X10" },
            { "DIAMON", true, "TRANS Z5\nG59 X10", "SHIFT X=10", "ATRANS X10" },
            { "DIAMOF", true, "TRANS Z5\nG59 X10", "SHIFT X=5", "ATRANS X10" },
            { "DIAMON", false, Swivel, "RAW:SIEMENS=\"CYCLE800(0,\\\"TABLE\\\",0,27,10,0,0,0,45,0,0,0,0,-1,100,1)\"",
                Swivel },
            { "DIAMOF", false, Swivel, "SHIFT X=10", Compiled },
            { "DIAMON", true, Swivel, "SHIFT X=10", Compiled },
            { "DIAMOF", true, Swivel, "SHIFT X=5", Compiled },
        };
        return rows;
    }

    // D125, D388: the X of TRANS, G59 and CYCLE800 is read with the frame factor of the axis and written back with
    // its inverse, so the source value comes back, and the compiled program reads into the same SHIFT X values. The
    // read-back of CYCLE800 also holds the RETRACT and the chain the compiler writes around the swivel (D333), so the
    // SHIFT X values are compared, not the whole body.
    [Theory]
    [MemberData(nameof(FrameInstructions))]
    public void FrameX_ReadAndCompiledBack_IsTheSourceValue(string diameter, bool framesInDiameter, string frame,
        string shift, string compiledLine)
    {
        MachineConfig machine = MillTurn(framesInDiameter);
        string source = diameter + "\n" + frame + "\nG0 X40 Z5\n";

        string ncx = Read(source, machine);
        string compiled = Compile(ncx, machine);

        Assert.Contains(shift, Lines(ncx));
        Assert.Contains(compiledLine, Lines(compiled));
        Assert.Equal(ShiftXValues(ncx), ShiftXValues(Read(compiled, machine)));
    }

    // D240, D388: $AA_IM[X] is read into $SYS_MPOS_X with the actual-value factor of the axis and $SYS_MPOS_X written
    // back with its inverse, so the value of R1 is the value the source assigns.
    [Theory]
    [MemberData(nameof(Combinations))]
    public void AaImX_ReadAndCompiledBack_KeepsItsValue(string diameter, bool framesInDiameter,
        string machinePosition)
    {
        MachineConfig machine = MillTurn(framesInDiameter);
        string source = diameter + "\nR1=$AA_IM[X]\n";

        string compiled = Compile(Read(source, machine), machine);

        Assert.Contains(machinePosition, Lines(compiled));
    }

    // A source of the main program TEST read by the Siemens reader and written as canonical NCX, without an ERROR.
    private static string Read(string body, MachineConfig machine)
    {
        string text = body.StartsWith("%_N_", StringComparison.Ordinal) ? body : "%_N_TEST_MPF\n" + body + "M30\n";
        NcxProgram read = new SiemensReader().Read(new SourceFile("TEST.mpf", text), machine, new ReadOptions());
        Assert.False(read.Diagnostics.HasErrors, read.Diagnostics.ToText());
        return NcxWriter.Write(read);
    }

    // An NCX program compiled by the Siemens compiler into one file, without an ERROR.
    private static string Compile(string ncx, MachineConfig machine)
    {
        NcxProgram program = Parser.Parse(ncx, "TEST.ncx", new ParserOptions());
        CompileResult result = ((ICompiler)new SiemensCompiler()).Compile(program, machine, new CompileOptions());
        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        return Assert.Single(result.Files).Text;
    }

    // The lines of a text without block numbers.
    private static List<string> Lines(string text)
    {
        var lines = new List<string>();
        foreach (string line in text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'))
        {
            int blank = line.IndexOf(' ', StringComparison.Ordinal);
            bool numbered = line.Length > 1 && line[0] == 'N' && char.IsAsciiDigit(line[1]) && blank > 0;
            lines.Add(numbered ? line.Substring(blank + 1) : line);
        }

        return lines;
    }

    // The distinct X values of the SHIFT lines of an NCX text, in the order they first stand.
    private static List<string> ShiftXValues(string ncx)
    {
        var values = new List<string>();
        foreach (string line in Lines(ncx))
        {
            if (!line.StartsWith("SHIFT X=", StringComparison.Ordinal))
            {
                continue;
            }

            string value = line.Substring("SHIFT X=".Length).Split(' ')[0];
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    // millturn1.toml with or without frames_in_diameter on its switchable X axis, $AA_IM[X] mapped to SYS_MPOS_X, and
    // the Siemens cycle catalog its [cycles] names (machine-config 4, 6, 7).
    private static MachineConfig MillTurn(bool framesInDiameter)
    {
        string text = File.ReadAllText(Path.Combine(Fixture.RepositoryRoot(), "machines", "millturn1.toml"));
        string key = "programming = \"switchable\"";
        Assert.Contains(key, text, StringComparison.Ordinal);
        text = text.Replace(key, key + (framesInDiameter ? "\nframes_in_diameter = true" : ""),
            StringComparison.Ordinal) + "\n[system_variables]\nSYS_MPOS_X = \"$AA_IM[X]\"\n";
        var diagnostics = new Diagnostics("millturn1.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(text, diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "siemens.toml"), Controller.Siemens, diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }
}
