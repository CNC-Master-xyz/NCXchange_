using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// D388 (R1-13): a Fanuc tool change applies no length offset of its own, so the first tool-axis motion after a TOOL
/// without OFFSET:LEN, OFFSET or the register of a named point is a WARNING, once per tool change and naming the tool;
/// no G43 H is written by default, and [tool_change] length_offset_by_macro = true silences it.
/// </summary>
public sealed class FanucLengthOffsetWarningTests
{
    // D388: TOOL=1 then a Z move without a length offset warns once, at the Z move, and writes no G43.
    [Fact]
    public void ToolAxisMotionWithoutLengthOffset_WarnsOnceNamingTheTool()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("TOOL=1", "RAPID X=0 Y=0", "RAPID Z=2", "RAPID Z=50"), FanucCompile.Mill());

        string text = FanucCompile.TextOf(result);
        Assert.DoesNotContain("G43", text, StringComparison.Ordinal);
        Diagnostic warning = Assert.Single(FanucCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.FanucToolWithoutLengthOffset, warning.Code);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(6, warning.Line);
        Assert.Contains("tool 1", warning.Message, StringComparison.Ordinal);
    }

    // D388: once per tool change: two changes without an offset warn twice.
    [Fact]
    public void TwoToolChangesWithoutLengthOffset_WarnTwice()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("TOOL=1", "RAPID Z=2", "TOOL=2", "RAPID Z=2"), FanucCompile.Mill());

        Assert.Equal(2, FanucCompile.CompilerDiagnostics(result).Count);
    }

    // D388: OFFSET:LEN, a combined OFFSET or the register of a named point is a length offset, and nothing warns.
    [Theory]
    [InlineData("TOOL=1 OFFSET:LEN=1")]
    [InlineData("TOOL=1 OFFSET=1")]
    public void ToolWithALengthOffset_DoesNotWarn(string tool)
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program(tool, "RAPID Z=2"), FanucCompile.Mill());

        Assert.DoesNotContain(DiagnosticCodes.FanucToolWithoutLengthOffset, Codes(result));
    }

    // D388, D392: a named point whose mapping gives H is a length offset.
    [Fact]
    public void ToolWithANamedPoint_DoesNotWarn()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program("TOOL=1 TOOL_POINT=1", "RAPID Z=2"),
            FanucCompile.Mill(extra: "[tool_points]\nform = \"registers\"\nlength_register_step = 100"));

        Assert.Empty(FanucCompile.CompilerDiagnostics(result));
    }

    // D388: length_offset_by_macro = true says the builder's change macro applies the length.
    [Fact]
    public void LengthOffsetByMacro_DoesNotWarn()
    {
        string machine = FanucCompile.Mill().Replace("unload = \"T0 M6\"",
            "unload = \"T0 M6\"\nlength_offset_by_macro = true", StringComparison.Ordinal);

        CompileResult result = FanucCompile.Run(FanucCompile.Program("TOOL=1", "RAPID Z=2"), machine);

        Assert.Empty(FanucCompile.CompilerDiagnostics(result));
    }

    // D388: an empty spindle has no length to apply.
    [Fact]
    public void EmptySpindle_DoesNotWarn()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program("TOOL=0", "RAPID Z=2"), FanucCompile.Mill());

        Assert.Empty(FanucCompile.CompilerDiagnostics(result));
    }

    private static List<string> Codes(CompileResult result)
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in result.Diagnostics.Items)
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }
}
