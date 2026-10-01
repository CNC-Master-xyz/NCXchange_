using System.Diagnostics;
using System.Text.RegularExpressions;
using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.Writing;
using Ncx.Readers;
using Ncx.Readers.Heidenhain;
using Ncx.Tests.Fixtures;
using ReaderCodes = Ncx.Readers.DiagnosticCodes;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The Heidenhain reader over the example sources end to end, through the library: the reader with
/// machines/heidenhain-itnc530.toml loaded by its path, then the canonical writer (phase 3, P3-05; ncx convert joins
/// with P3-02 part two). 2.5D_FRAESEN.h reads into the blocks of examples/2.5D_FRAESEN.ncx with the WARNING about the
/// missing M30, BOHREN.h into Expected/BOHREN.ncx of the Fanuc source except the blocks the two sources write
/// otherwise, 3D_FRAESEN.h in well under a second, and under NCX_CORPUS every Heidenhain file of the corpus without a
/// crash.
/// </summary>
[Collection(ChainTiming.Name)]
public sealed partial class HeidenhainReaderTests
{
    // The blocks of examples/2.5D_FRAESEN.ncx that the Heidenhain reading writes otherwise, in their order; a reading
    // of several lines inserts blocks after the one it replaces.
    // TODO(question): the example gives the program NUMBER=1, which only the Fanuc O0001 carries (Klartext has no
    // program number, controllers heidenhain.md 1), and keeps BLK FORM as a comment line, while heidenhain 7 rule 9
    // keeps BLK FORM as RAW with a WARNING; the reader follows heidenhain 7, and the test compares every other block of
    // the example as the phase plan says, until D217 is answered.
    internal static readonly KeyValuePair<string, string>[] s_heidenhainReading =
    [
        new("PROGRAM=BEGIN NAME=\"2.5D FRAESEN\" NUMBER=1", "PROGRAM=BEGIN NAME=\"2.5D FRAESEN\""),
        new("ORIGIN=1", "ORIGIN=1\nRAW:HEIDENHAIN=\"2 BLK FORM 0.1 Z X0 Y0 Z-20\"\n"
            + "RAW:HEIDENHAIN=\"3 BLK FORM 0.2 X100 Y100 Z0\""),
    ];

    // The blocks of Expected/BOHREN.ncx, read from the Fanuc source, that the Heidenhain source reads into otherwise,
    // in their order, each as the first block it replaces, the block before which the replaced range ends (null for one
    // block), and the Heidenhain reading. Every comment line of the expected file, a comment of the Fanuc source
    // (D92), is a * - block of the Klartext source, SECTION (controller-mapping 1). The differences: the program number
    // of O0001 and BLK FORM (as for 2.5D_FRAESEN); the offsets with the TOOL CALL and the speed in it (D7, heidenhain 7
    // rule 2); the R0 of the first positioning; the plunge feed Q206=565,487 against F565 and the dwell Q211=0 that
    // G81 does not write; the fourth operation, whose Q213=3 is the mixed form of cycle 203 that D163 reads natively
    // and whose Q208=MAX has no NCX value, so the Klartext blocks stay RAW where the Fanuc source writes single moves
    // (wave-2 question 94); M29 S500 of the Fanuc tapping; M91 moves against G28 (2.5D_FRAESEN note 4). Q204=5
    // beside Q200=5 is no difference any more: the second clearance acts only above the first, so both sources give
    // CYCLE_RETRACT=CLEARANCE (D226), and cycle 203 with Q213=24 is the CHIP_BREAK of the Fanuc G73 (D163).
    private static readonly BohrenDifference[] s_bohrenReading =
    [
        new("PROGRAM=BEGIN NAME=\"BOHREN\" NUMBER=1", null, "PROGRAM=BEGIN NAME=\"BOHREN\""),
        new("ORIGIN=1", null, "ORIGIN=1\nRAW:HEIDENHAIN=\"2 BLK FORM 0.1 Z X0 Y0 Z-25\"\n"
            + "RAW:HEIDENHAIN=\"3 BLK FORM 0.2 X100 Y20 Z0\""),
        new("TOOL=1", null, "TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1 RPM=10000"),
        new("SPINDLE=CW RPM=10000", null, "SPINDLE=CW"),
        new("RAPID X=10 Y=10", null, "RAPID X=10 Y=10 COMP=OFF"),
        new("RAPID Z=5 OFFSET:LEN=1", null, "RAPID Z=5"),
        new(Cycle("DRILL", "", "CYCLE_F=565"), null, Cycle("DRILL", "", "CYCLE_F=565.487 CYCLE_DWELL=0")),
        new(Cycle("CHIP_BREAK", "PECK=1.2 ", "CYCLE_F=565"), null,
            Cycle("CHIP_BREAK", "PECK=1.2 ", "CYCLE_F=565.487 CYCLE_DWELL=0")),
        new(Cycle("PECK", "PECK=1.2 ", "CYCLE_F=565"), null,
            Cycle("PECK", "PECK=1.2 ", "CYCLE_F=565.487 CYCLE_DWELL=0")),
        new("LINE Z=3.8 F=565", "HOME Z", RawMixedCycle203() + "\nRAPID Z=5"),
        new("HOME Z", null, "RAPID Z=0 FRAME=MACHINE"),
        new("HOME X Y", null, "RAPID X=0 Y=0 FRAME=MACHINE"),
        new("TOOL=2", null, "TOOL=2 OFFSET:LEN=2 OFFSET:RAD=2 RPM=500"),
        new("SPINDLE=CW RPM=500", null, "SPINDLE=CW"),
        new("RAPID Z=5 OFFSET:LEN=2", null, "RAPID Z=5"),
        new("RPM=500 FUNC:RIGID_TAP=ON", null, ""),
        new("CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=750 CYCLE_DWELL=0 PITCH=1.5",
            null, "CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE PITCH=1.5"),
        new("HOME Z", null, "RAPID Z=0 FRAME=MACHINE"),
        new("HOME X Y", null, "RAPID X=0 Y=0 FRAME=MACHINE"),
        new("TOOL=3", null, "TOOL=3 OFFSET:LEN=3 OFFSET:RAD=3 RPM=8000"),
        new("SPINDLE=CW RPM=8000", null, "SPINDLE=CW"),
        new("RAPID Z=5 OFFSET:LEN=3", null, "RAPID Z=5"),
        new("CYCLE=REAM SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=420", null,
            "CYCLE=REAM SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=420 CYCLE_DWELL=0"),
        new("HOME Z", null, "RAPID Z=0 FRAME=MACHINE"),
        new("HOME X Y", null, "RAPID X=0 Y=0 FRAME=MACHINE"),
    ];

    // M6: 2.5D_FRAESEN.h with machines/heidenhain-itnc530.toml reads into the blocks of examples/2.5D_FRAESEN.ncx,
    // comments and trivia stripped on both sides, with the WARNING about the missing M30 (controller-mapping 1,
    // PROGRAM=END) and the WARNINGs of the two RAW blocks.
    [Fact]
    public void Convert_25DFraesen_ReadsIntoTheBlocksOfTheExample()
    {
        NcxProgram example = Parser.Parse(
            Fixture.ReadText("2.5D_FRAESEN.ncx"), "2.5D_FRAESEN.ncx", new ParserOptions());
        NcxProgram read = Read("2.5D_FRAESEN.h");

        var expected = new List<string>();
        int difference = 0;
        foreach (string block in BlocksOf(example))
        {
            bool differs = difference < s_heidenhainReading.Length && s_heidenhainReading[difference].Key == block;
            expected.Add(differs ? s_heidenhainReading[difference++].Value : block);
        }

        Assert.Equal(s_heidenhainReading.Length, difference);
        Assert.Equal(string.Join('\n', expected), string.Join('\n', BlocksOf(read)));
        Assert.Equal([ReaderCodes.ProgramEndMissing, ReaderCodes.KeptAsRaw, ReaderCodes.KeptAsRaw], CodesOf(read));
    }

    // M6: BOHREN.h reads into the blocks of Expected/BOHREN.ncx, frozen from the Fanuc source, except the blocks the
    // two sources write otherwise (s_bohrenReading); D226 took the SAFE blocks out of that list, so the two readers
    // now agree on CYCLE_RETRACT (phase 3, P3-05).
    [Fact]
    public void Convert_Bohren_ReadsIntoTheExpectedFileExceptTheListedBlocks()
    {
        string frozen = File.ReadAllText(
            Path.Combine(Fixture.RepositoryRoot(), "tests", "Ncx.Acceptance", "Expected", "BOHREN.ncx"));
        NcxProgram read = Read("BOHREN.h");

        Assert.Equal(string.Join('\n', BohrenReading(frozen)), string.Join('\n', BlocksOf(read)));
    }

    /// <summary>
    /// The blocks the Heidenhain reading of BOHREN.h gives, comments and trivia left out: the blocks of
    /// Expected/BOHREN.ncx, frozen from the Fanuc source, with the blocks the two sources write otherwise replaced by
    /// the Heidenhain reading (s_bohrenReading), and every comment line of the file, a comment of the Fanuc source
    /// (D92), as the SECTION of the * - block of the Klartext source (controller-mapping 1).
    /// </summary>
    /// <param name="frozen">The text of Expected/BOHREN.ncx.</param>
    internal static List<string> BohrenReading(string frozen)
    {
        var expected = new List<string>();
        int difference = 0;
        bool skipping = false;
        foreach (string line in frozen.ReplaceLineEndings("\n").Split('\n'))
        {
            BohrenDifference? next = difference < s_bohrenReading.Length ? s_bohrenReading[difference] : null;
            if (skipping && line != next!.Until)
            {
                continue;
            }

            if (skipping)
            {
                skipping = false;
                difference++;
                next = difference < s_bohrenReading.Length ? s_bohrenReading[difference] : null;
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("; ", StringComparison.Ordinal))
            {
                expected.Add($"SECTION=\"{line[2..]}\"");
            }
            else if (next is not null && next.First == line)
            {
                expected.Add(next.Heidenhain);
                skipping = next.Until is not null;
                difference += skipping ? 0 : 1;
            }
            else
            {
                expected.Add(line);
            }
        }

        Assert.Equal(s_bohrenReading.Length, difference);
        expected.RemoveAll(block => block.Length == 0);
        return [.. string.Join('\n', expected).Split('\n')];
    }

    // 3D_FRAESEN.h, 830 blocks of short moves, converts in well under a second (phase 3, P3-05).
    [Fact]
    public void Convert_3DFraesen_TakesWellUnderASecond()
    {
        Read("3D_FRAESEN.h");

        var watch = Stopwatch.StartNew();
        NcxProgram read = Read("3D_FRAESEN.h");
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 500, $"3D_FRAESEN took {watch.ElapsedMilliseconds} ms.");
        Assert.Equal([ReaderCodes.ProgramEndMissing, ReaderCodes.KeptAsRaw, ReaderCodes.KeptAsRaw], CodesOf(read));
    }

    // The output of the reader is a program that ncx format gives back as it is and that check runs without an ERROR
    // (code-guidelines 4; D91).
    [Theory]
    [InlineData("2.5D_FRAESEN.h")]
    [InlineData("BOHREN.h")]
    [InlineData("3D_FRAESEN.h")]
    public void Convert_HeidenhainSources_FormatToThemselvesAndCheckWithoutError(string source)
    {
        MachineConfig mill = Mill();
        string text = NcxWriter.Write(Read(source));
        NcxProgram parsed = Parser.Parse(text, source, new ParserOptions());
        var check = new Diagnostics(source);
        new VirtualMachine(mill, VmOptions.ForMachine(mill), check).Run(parsed);

        Assert.True(parsed.Diagnostics.Items.Count == 0, parsed.Diagnostics.ToText());
        Assert.Equal(text, NcxWriter.Write(parsed));
        Assert.False(check.HasErrors, check.ToText());
    }

    // Under NCX_CORPUS every Heidenhain file of the corpus converts without a crash, and PLANE AXIAL, CP IPA, LN and
    // M140 read into their words, TILT_AXIS, ANGLE, the tool vector and RETRACT, in every file that has them (phase 3,
    // P3-05; controllers sample-corpus 3: a crash is a bug).
    [CorpusFact]
    public void Convert_EveryHeidenhainFileOfTheCorpus_NeverCrashesAndReadsTheWordsOfD81ToD84()
    {
        string corpus = Environment.GetEnvironmentVariable(CorpusFactAttribute.Variable)!;
        MachineConfig mill = Mill();
        var problems = new List<string>();
        int files = 0;
        foreach (string path in Directory.EnumerateFiles(corpus, "*.h", SearchOption.AllDirectories))
        {
            files++;
            string source = File.ReadAllText(path);
            try
            {
                string text = NcxWriter.Write(new HeidenhainReader().Read(
                    new SourceFile(Path.GetFileName(path), source), mill, new ReadOptions()));
                problems.AddRange(MissingWords(path, source, text));
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException
                or FormatException or IndexOutOfRangeException or OverflowException or NullReferenceException
                or KeyNotFoundException)
            {
                // The test lists every file that crashes, not only the first.
                problems.Add($"{path}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} problems in {files} files:\n" + string.Join('\n', problems));
    }

    // A construct of the source whose word the output of the file never holds.
    private static List<string> MissingWords(string path, string source, string text)
    {
        var missing = new List<string>();
        (Regex Construct, string Word)[] pairs =
        [
            (PlaneAxial(), "TILT_AXIS "), (CpIpa(), " ANGLE="), (Ln(), " TX="), (M140(), "RETRACT"),
        ];
        foreach ((Regex construct, string word) in pairs)
        {
            if (construct.IsMatch(source) && !text.Contains(word, StringComparison.Ordinal))
            {
                missing.Add($"{path}: {construct} reads into no {word.Trim()}");
            }
        }

        return missing;
    }

    private static NcxProgram Read(string source)
    {
        return new HeidenhainReader().Read(new SourceFile(source, Fixture.ReadText("sources/" + source)), Mill(),
            new ReadOptions());
    }

    // The iTNC 530 of the Heidenhain sources, loaded by its path as ncx convert --machine heidenhain-itnc530 will load
    // it, with the Heidenhain cycle catalog its [cycles] names (machine-config 6).
    private static MachineConfig Mill()
    {
        const string relativePath = "machines/heidenhain-itnc530.toml";
        var diagnostics = new Diagnostics(relativePath);
        MachineConfig? machine = MachineConfigLoader.Load(Path.Combine(Fixture.RepositoryRoot(), relativePath),
            diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", "heidenhain.toml"), Controller.Heidenhain, diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }

    // The blocks of a program in canonical form without their comments; the trivia are no blocks.
    private static List<string> BlocksOf(NcxProgram program)
    {
        var blocks = new List<string>();
        foreach (Block block in program.Blocks)
        {
            blocks.Add(NcxWriter.WriteBlock(block with { Comment = null }));
        }

        return blocks;
    }

    private static List<string> CodesOf(NcxProgram program)
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in program.Diagnostics.Items)
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }

    // A drilling cycle of BOHREN with the words both sources share; Q204=5 beside Q200=5 does not act, so every
    // cycle of the file retracts to CLEARANCE (D226).
    private static string Cycle(string name, string peck, string tail)
    {
        return $"CYCLE={name} SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE {peck}{tail}";
    }

    // The fourth operation of BOHREN.h: a cycle 203 whose Q213=3 lies between 0 and the number of infeeds, the mixed
    // form that has no NCX cycle and is read natively as CYCLE:HEIDENHAIN=203 (D163). It does not get there, because a
    // native parameter takes a number or an expression (language 4.7.1, D94) and Q208=MAX is a literal of the control,
    // so the definition and the five calls that follow it stay RAW (heidenhain 7 rule 9). The blocks below are that
    // residue, not the decided reading: it waits for D115, which asks what a parameter may carry, and D164, which
    // writes Q208=MAX back (03-open-questions, wave 2 question 94; the marker in HeidenhainCycles.Native). Where every
    // Q parameter has an NCX value the native form is written, which the reader test
    // CyclDef203_WithQ213BetweenZeroAndTheInfeeds_IsWrittenNatively pins.
    private static string RawMixedCycle203()
    {
        string[] lines =
        [
            "38 CYCL DEF 203 UNIVERSALBOHREN ~",
            "    Q200=5 ;SICHERHEITSABSTAND ~",
            "    Q201=-21,732 ;TIEFE ~",
            "    Q206=565,487 ;VORSCHUBTYP FUR TAUCHEN ~",
            "    Q202=1,2 ;TIEFENZUSTELLUNG ~",
            "    Q210=0 ;VERWEILZEIT AN HOCHSTEM PUNKT ~",
            "    Q203=0 ;OBERFLACHENKOORDINATE ~",
            "    Q204=5 ;2. SICHERHEITSABSTAND ~",
            "    Q212=0 ;DEKREMENT ~",
            "    Q213=3 ;BRUCHANZAHL ~",
            "    Q205=1,2 ;MIN. TIEFENZUSTELLUNG ~",
            "    Q211=0 ;VERWEILZEIT AN TIEFSTEM PUNKT ~",
            "    Q208=MAX ;RUCKZUGSVORSCHUBTYP ~",
            "    Q256=0,6 ;ABSTAND FUR SPANBRECHEN",
            "39 CYCL CALL",
            "40 L X30 FMAX M99",
            "41 L X50 FMAX M99",
            "42 L X70 FMAX M99",
            "43 L X90 FMAX M99",
        ];
        var blocks = new List<string>();
        foreach (string line in lines)
        {
            blocks.Add($"RAW:HEIDENHAIN=\"{line}\"");
        }

        return string.Join('\n', blocks);
    }

    [GeneratedRegex(@"PLANE\s+AXIAL", RegexOptions.CultureInvariant)]
    private static partial Regex PlaneAxial();

    [GeneratedRegex(@"\bCP\b[^;\r\n]*\bIPA[+-]?[0-9]*[.,]?[0-9]*", RegexOptions.CultureInvariant)]
    private static partial Regex CpIpa();

    [GeneratedRegex(@"^\s*[0-9]+\s+LN\s[^;\r\n]*\bTX", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex Ln();

    [GeneratedRegex(@"\bM140\b", RegexOptions.CultureInvariant)]
    private static partial Regex M140();

    // One difference of the Heidenhain reading of BOHREN: the first expected block it replaces, the expected block
    // before which the replaced range ends (null for one block), and the Heidenhain blocks, one per line.
    private sealed record BohrenDifference(string First, string? Until, string Heidenhain);
}
