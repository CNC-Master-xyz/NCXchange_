using Ncx.Compilers.Fanuc;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The measurement points on Fanuc (D373 registers, D375, D376, D392): a named point writes the H and D the mapping
/// gives, never TIP_OFFSET or POINT_RADIUS; a program without point words compiles as before.
/// </summary>
public sealed class FanucToolPointTests
{
    // The registers form with the shop's step rule: point k of tool t is H and D t + (k - 1) * 100 (D373 (5)).
    private const string StepRule = """
        [tool_points]
        form = "registers"
        length_register_step = 100
        radius_register_step = 100
        """;

    // D373, D375: point 2 of tool 5 writes G43 H105 with the first move of the tool axis, and nothing for a and r.
    [Fact]
    public void PointTwo_WritesTheLengthRegisterOfTheMappingAndNoValue()
    {
        string body = Body("TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8\nRAPID X=0 Y=0\nRAPID Z=2", StepRule);

        Assert.Equal("T5 M6\nG0 G17 X0. Y0.\nG43 Z2. H105\n", body);
    }

    // D392, R1-13: an explicit point 1 writes the length register of the mapping, so the CAM's TOOL_POINT=1 gets the
    // G43 H that TOOL alone does not write.
    [Fact]
    public void PointOneNamed_WritesTheLengthRegisterOfTheMapping()
    {
        string body = Body("TOOL=5 TOOL_POINT=1\nRAPID X=0 Y=0\nRAPID Z=2", StepRule);

        Assert.Equal("T5 M6\nG0 G17 X0. Y0.\nG43 Z2. H5\n", body);
    }

    // D372, D373: a point change without a tool change takes effect with the next move of the tool axis.
    [Fact]
    public void PointChangeWithoutToolChange_WritesTheNewRegisterWithTheNextToolAxisMove()
    {
        string body = Body("TOOL=5 TOOL_POINT=1\nRAPID X=0 Y=0\nRAPID Z=2\nTOOL_POINT=2 TIP_OFFSET=2\nRAPID Z=10",
            StepRule);

        Assert.Equal("T5 M6\nG0 G17 X0. Y0.\nG43 Z2. H5\nG43 Z10. H105\n", body);
    }

    // D373, controller-mapping 3: the radius register of the point stands with G41 or G42.
    [Fact]
    public void PointTwoUnderCompensation_WritesTheRadiusRegisterOfTheMapping()
    {
        string body = Body("TOOL=5 TOOL_POINT=2 POINT_RADIUS=8\nRAPID X=0 Y=0 Z=2\nLINE X=10 F=100 COMP=LEFT",
            StepRule);

        Assert.Contains("G41", body, StringComparison.Ordinal);
        Assert.Contains("D105", body, StringComparison.Ordinal);
    }

    // D375: a program without point words compiles byte for byte as on a machine without [tool_points].
    [Fact]
    public void ProgramWithoutPointWords_CompilesAsBefore()
    {
        const string Blocks = "TOOL=5\nRAPID X=0 Y=0\nRAPID Z=2 OFFSET:LEN=5\nLINE X=10 F=100";

        Assert.Equal(Body(Blocks, ""), Body(Blocks, StepRule));
    }

    // D392: an OFFSET word after a named point replaces the register of the mapping, which it writes, with CMP033.
    [Fact]
    public void OffsetWordAfterANamedPoint_WritesItsRegisterAndWarns()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("TOOL=5 TOOL_POINT=2", "RAPID X=0 Y=0", "RAPID Z=2 OFFSET:LEN=7"),
            FanucCompile.Mill(extra: StepRule));

        Assert.Contains("G43 Z2. H7\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Diagnostic warning = Assert.Single(FanucCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.OffsetReplacesPointRegister, warning.Code);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains("TOOL_POINT=2", warning.Message, StringComparison.Ordinal);
    }

    // D392: an OFFSET word of a tool whose program named no point warns nothing, as before.
    [Fact]
    public void OffsetWordWithoutANamedPoint_DoesNotWarn()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("TOOL=5", "RAPID X=0 Y=0", "RAPID Z=2 OFFSET:LEN=5"),
            FanucCompile.Mill(extra: StepRule));

        Assert.Empty(FanucCompile.CompilerDiagnostics(result));
    }

    // D373 (4), CMP341: point 2 on a managed machine without a row has no register, an ERROR.
    [Fact]
    public void PointTwoOnAManagedMachineWithoutARow_IsCmp341()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program("TOOL=5 TOOL_POINT=2"),
            FanucCompile.Mill(extra: "[tool_points]\nform = \"registers\"\nmanaged = true"));

        Assert.Equal(DiagnosticCodes.FanucToolPointNotMapped, FanucCompile.ErrorOf(result).Code);
    }

    // D373 (4): tool management maps point 1 to H99 and D99.
    [Fact]
    public void PointOneOnAManagedMachine_IsH99()
    {
        string body = Body("TOOL=5 TOOL_POINT=1\nRAPID X=0 Y=0\nRAPID Z=2",
            "[tool_points]\nform = \"registers\"\nmanaged = true");

        Assert.Contains("G43 Z2. H99", body, StringComparison.Ordinal);
    }

    // D373, CMP016: a machine without [tool_points] writes nothing for point 2 and says so once per program.
    [Fact]
    public void PointTwoWithoutToolPoints_WritesNothingWithCmp016Once()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("TOOL=5 TOOL_POINT=2", "TOOL_POINT=3", "TOOL=6 TOOL_POINT=2"), FanucCompile.Mill());

        Assert.Contains("T5 M6\nT6 M6\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Diagnostic info = Assert.Single(FanucCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.ToolPointWithoutForm, info.Code);
        Assert.Equal(Severity.Info, info.Severity);
    }

    // D375: point_comment = true writes one comment line with the point, a and r; the length register that still
    // waits for the tool axis stands before the end (language 2 rule 8).
    [Fact]
    public void PointComment_WritesOneCommentLine()
    {
        string body = Body("TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8", StepRule + "\npoint_comment = true");

        Assert.Equal("T5 M6\n(TOOL 5 POINT 2 TIP_OFFSET 2 POINT_RADIUS 8)\nG43 H105\n", body);
    }

    // D376, CMP032: a tip offset that differs from the tool table by more than one output step warns, and the output
    // stays as the program wrote it; within one step it is silent.
    [Theory]
    [InlineData("2.0005", false)]
    [InlineData("2.002", true)]
    public void TipOffsetAgainstTheToolTable_WarnsBeyondOneOutputStep(string tipOffset, bool warns)
    {
        ToolTable table = ToolTable.LoadText("""
            [[tool]]
            number = 5

            [[tool.point]]
            k = 2
            tip_offset = 2
            radius = 8
            """, new Diagnostics("t.tools.toml"))!;
        MachineConfig machine = MachineConfigLoader.LoadText(FanucCompile.Mill(extra: StepRule),
            new Diagnostics("test.toml"))!;
        NcxProgram program = Parser.Parse(
            FanucCompile.Program($"TOOL=5 TOOL_POINT=2 TIP_OFFSET={tipOffset} POINT_RADIUS=8"), "T.ncx",
            new ParserOptions());

        CompileResult result = new FanucCompiler().Compile(program, machine,
            new CompileOptions { ToolTable = table, ToolTableFile = "t.tools.toml" });

        Assert.Equal(warns, result.Diagnostics.Items.Any(
            static diagnostic => diagnostic.Code == DiagnosticCodes.ToolPointDiffersFromTable));
        Assert.Contains("T5 M6\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
    }

    private static string Body(string blocks, string extra)
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Program(blocks.Split('\n')),
            FanucCompile.Mill(extra: extra)));
        const string Start = "%\nO0001 (T)\nG0 G40\nG80 G90 G94 G98\n";
        const string End = "M30\n%\n";
        Assert.StartsWith(Start, text, StringComparison.Ordinal);
        Assert.EndsWith(End, text, StringComparison.Ordinal);
        return text.Substring(Start.Length, text.Length - Start.Length - End.Length);
    }
}
