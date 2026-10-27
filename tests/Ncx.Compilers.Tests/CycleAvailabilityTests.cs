using Ncx.Compilers.Tests.Fanuc;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// D361, D377: a [[cycle]] table with available = false on the channel compiled, or without channel on the whole
/// machine, makes the cycle the ERROR CMP004, and nothing is written for it; the table of another channel does not
/// apply (machine-config 6).
/// </summary>
public sealed class CycleAvailabilityTests
{
    private static readonly string[] s_blocks =
    [
        "TOOL=1", "SPINDLE=CW RPM=1000", "RAPID X=10 Y=10", "RAPID Z=5",
        "CYCLE=DRILL CLEARANCE=5 DEPTH=-20 CYCLE_F=565", "CYCLE_CALL", "CYCLE=OFF",
    ];

    [Fact]
    public void CycleNotAvailableOnTheMachine_IsCmp004()
    {
        CompileResult result = Compile("[[cycle]]\nname = \"DRILL\"\nnative = \"G81\"\navailable = false");

        Diagnostic error = FanucCompile.ErrorOf(result);
        Assert.Equal(DiagnosticCodes.CycleNotAvailable, error.Code);
        Assert.Contains("DRILL", error.Message, StringComparison.Ordinal);
        Assert.Contains("channel 1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CycleNotAvailableOnAnotherChannel_IsWritten()
    {
        CompileResult result = Compile("[[cycle]]\nname = \"DRILL\"\nnative = \"G81\"\nchannel = 2\navailable = false");

        Assert.DoesNotContain(result.Diagnostics.Items, diagnostic => diagnostic.Severity == Severity.Error);
        Assert.Contains("G81", FanucCompile.TextOf(result), StringComparison.Ordinal);
    }

    private static CompileResult Compile(string table)
    {
        return FanucCompile.Run(FanucCompile.Program(s_blocks), FanucCompile.Mill(extra: table));
    }
}
