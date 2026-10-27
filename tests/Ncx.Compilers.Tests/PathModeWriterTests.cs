using Ncx.Compilers.Tests.Fanuc;
using Ncx.Compilers.Tests.Heidenhain;
using Ncx.Compilers.Tests.Siemens;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// D349: PATH_MODE through the [transform] templates PATH_MODE_EXACT_STOP and PATH_MODE_CONTINUOUS, after the
/// tolerance words of its block; DEFAULT takes the machine's [dynamics] path_mode (D350); an empty or missing template
/// is the WARNING of the family, CMP361, CMP533 or CMP120, and nothing is written (machine-config 5).
/// </summary>
public sealed class PathModeWriterTests
{
    private const string FanucTables = """
        [transform]
        PATH_MODE_EXACT_STOP = "G61"
        PATH_MODE_CONTINUOUS = "G64"

        [dynamics]
        path_mode = "exact_stop"
        """;

    [Fact]
    public void Fanuc_PathMode_IsG61OrG64AfterTheTolerance()
    {
        string body = FanucCompile.Body(
            "TOLERANCE=0.01 PATH_MODE=CONTINUOUS\nPATH_MODE=EXACT_STOP\nPATH_MODE=CONTINUOUS\nPATH_MODE=DEFAULT",
            FanucTables);

        Assert.Equal("G5.1 Q1\nG64\nG61\nG64\nG61\n", body);
    }

    [Fact]
    public void Fanuc_PathModeWithoutTemplate_IsCmp361AndNotWritten()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program("PATH_MODE=EXACT_STOP"), FanucCompile.Mill());

        Assert.DoesNotContain("G61", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Diagnostic warning = Assert.Single(result.Diagnostics.Items,
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucOptionNotWritten);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains("PATH_MODE_EXACT_STOP", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Siemens_PathMode_IsG60OrG64()
    {
        Assert.Equal(SiemensCompile.Lines("G60", "G64"),
            SiemensCompile.MillBody("PATH_MODE=EXACT_STOP", "PATH_MODE=CONTINUOUS"));
    }

    [Fact]
    public void Siemens_PathModeWithEmptyTemplate_IsCmp533()
    {
        var machine = SiemensCompile.Replaced("machines/siemens-840dsl-mill.toml", "PATH_MODE_EXACT_STOP = \"G60\"",
            "PATH_MODE_EXACT_STOP = \"\"");

        CompileResult result = SiemensCompile.Run(
            SiemensCompile.Program(SiemensCompile.MillHeader, "PATH_MODE=EXACT_STOP"), machine);

        Assert.Contains(DiagnosticCodes.SiemensOptionNotWritten, SiemensCompile.Codes(result));
        Assert.DoesNotContain("G60", SiemensCompile.TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Heidenhain_PathMode_IsCmp120AndNotWritten()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("PATH_MODE=EXACT_STOP"));

        Diagnostic warning = HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainPathModeNotWritten);
        Assert.Equal(Severity.Warning, warning.Severity);
    }
}
