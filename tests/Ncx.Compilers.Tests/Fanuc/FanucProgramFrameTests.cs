using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The O number of a program without NUMBER (D257), the units block at the start of each program (D283) and the block
/// numbers that may stand once per O program (D277).
/// </summary>
public sealed class FanucProgramFrameTests
{
    // D257: without NUMBER the compiler takes the lowest number from 1 that the output file does not use, with the
    // WARNING CMP310 that names it; the name stays in parentheses.
    [Fact]
    public void D257_ProgramWithoutNumber_TakesTheLowestFreeNumberWithCmp310()
    {
        string program = FanucCompile.Program("RAPID X=1 Y=1").Replace(" NUMBER=1", "", StringComparison.Ordinal);

        CompileResult result = FanucCompile.Run(program, FanucCompile.Mill());

        Assert.StartsWith("%\nO0001 (T)\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Diagnostic warning = Assert.Single(FanucCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.FanucProgramNumberGenerated, warning.Code);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains("O0001", warning.Message, StringComparison.Ordinal);
    }

    // D257: a number the file uses, here the O number of a SUB it calls, is not taken.
    [Fact]
    public void D257_NumberOfASubOfTheFile_IsNotTaken()
    {
        string program = FanucCompile.Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\"",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF", "CALL=1", "PROGRAM=END",
            "SUB=BEGIN NAME=\"1\"", "RAPID X=1 Y=1", "SUB=END", "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Mill()));

        Assert.StartsWith("%\nO0002 (T)\n", text, StringComparison.Ordinal);
        Assert.Contains("\nO0001\n", text, StringComparison.Ordinal);
    }

    // D257: without NUMBER a NAME of digits is the O number, and no WARNING names it.
    [Fact]
    public void D257_NameOfDigits_IsTheONumber()
    {
        string program = FanucCompile.Program("RAPID X=1 Y=1").Replace("NAME=\"T\" NUMBER=1", "NAME=\"1234\"",
            StringComparison.Ordinal);

        CompileResult result = FanucCompile.Run(program, FanucCompile.Mill());

        Assert.StartsWith("%\nO1234 (1234)\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Assert.Empty(FanucCompile.CompilerDiagnostics(result));
    }

    // D283: the units stand in a block of their own at the start of the program, whatever units_default says.
    [Fact]
    public void D283_InchProgram_WritesG20InABlockOfItsOwn()
    {
        string program = FanucCompile.Program("RAPID X=1 Y=1").Replace("UNITS=MM", "UNITS=INCH",
            StringComparison.Ordinal);

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Mill()));

        Assert.StartsWith("%\nO0001 (T)\nG20\nG0 G40\n", text, StringComparison.Ordinal);
        Assert.Single(text.Split('\n'), line => line.Contains("G20", StringComparison.Ordinal));
    }

    // D283: a UNITS before the first motion is the units of the start block, and nothing is written where it stands.
    [Fact]
    public void D283_UnitsBeforeTheFirstMotion_StandInTheStartBlock()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("UNITS=INCH", "RAPID X=1 Y=1"), FanucCompile.Mill()));

        Assert.StartsWith("%\nO0001 (T)\nG20\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("G21", text, StringComparison.Ordinal);
    }

    // D283: a change of the units after the first motion is the ERROR CMP313.
    [Fact]
    public void D283_UnitsChangeAfterTheFirstMotion_IsTheErrorCmp313()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("RAPID X=1 Y=1", "UNITS=INCH", "RAPID X=2"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucUnitsChangeInProgram, error.Code);
    }

    // D277: no N may stand twice in one O program; a RAW line with the number of a label is the ERROR CMP318.
    [Fact]
    public void D277_RawLineWithTheNumberOfALabel_IsTheErrorCmp318()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("RAPID X=1 Y=1", "LABEL=10", "RAPID X=2", "RAW:FANUC=\"N10 G4 X1.\""),
            FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucBlockNumberTwice, error.Code);
    }

    // D277: block_numbers skips every label number, so a numbered line never takes the N of a label.
    [Fact]
    public void D277_BlockNumbers_SkipTheNumberOfALabel()
    {
        string machine = FanucCompile.Mill().Replace("block_numbers = { enabled = false }",
            "block_numbers = { enabled = true, start = 10, step = 10 }", StringComparison.Ordinal);

        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("RAPID X=1 Y=1", "LABEL=50", "RAPID X=2", "JUMP=50"), machine));

        Assert.Single(text.Split('\n'), line => line.StartsWith("N50", StringComparison.Ordinal));
    }
}
