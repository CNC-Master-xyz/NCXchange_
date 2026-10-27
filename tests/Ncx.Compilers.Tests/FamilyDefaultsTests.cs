using Ncx.Compilers.Tests.Fakes;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// D263: a [format] key the machine file leaves out takes its family value before any line is written
/// (OutputFormat.WithFamilyDefaults; machine-config 2).
/// </summary>
public sealed class FamilyDefaultsTests
{
    // [format] without block_numbers, max_line_length and comment_charset.
    private const string BareFormat = """
        [format]
        decimal_separator = "."
        decimals = { X = 3, Y = 3, Z = 3, F = 1, S = 0 }
        program_end = "M30"
        sub_end = "M99"
        program_layout = "one_file"
        """;

    // D263: comment_charset defaults to "ASCII" on every family, so the umlauts are transliterated.
    [Fact]
    public void Compile_WithoutCommentCharset_TransliteratesUnderAscii()
    {
        string program = FakeCompile.Program("COMMENT=\"Änderung\"");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill(format: BareFormat)));

        Assert.Contains("(Aenderung)", text, StringComparison.Ordinal);
    }

    // D263: Heidenhain numbers its blocks from 0 in steps of 1 when the file leaves block_numbers out.
    [Fact]
    public void Compile_HeidenhainWithoutBlockNumbers_NumbersFromZeroInStepsOfOne()
    {
        string program = FakeCompile.Program("RAPID X=1");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill("heidenhain", BareFormat),
            Controller.Heidenhain));

        Assert.Contains("N0 G0 X1.\nN1 M30\n", text, StringComparison.Ordinal);
    }

    // D263: Siemens takes max_line_length 512, so a longer line is the WARNING CMP021.
    [Fact]
    public void Compile_SiemensWithoutMaxLineLength_LineOver512IsTheWarningCmp021()
    {
        string program = FakeCompile.Program("COMMENT=\"" + new string('A', 520) + "\"");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill("siemens", BareFormat), Controller.Siemens);

        Diagnostic warning = Assert.Single(FakeCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.LineLongerThanTheMachineTakes, warning.Code);
    }
}
