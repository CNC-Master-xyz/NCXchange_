using Ncx.Compilers.Tests.Fanuc;
using Ncx.Compilers.Tests.Heidenhain;
using Ncx.Compilers.Tests.Siemens;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// CMP023 of the compiler framework (D363; language 4.1, ROUND_LIMIT; machine-config 2): rounding to the decimals of an
/// address is silent, and the compiler reports the point the written file produces where it lies farther from the
/// programmed point than ROUND_LIMIT allows. The Fanuc test mill writes X, Y and Z with three decimals.
/// </summary>
public sealed class RoundLimitTests
{
    // D363: without ROUND_LIMIT nothing is reported, and the value is rounded half away from zero on its decimal value.
    [Fact]
    public void WithoutRoundLimit_RoundingIsSilent()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("RAPID X=15.0175 Y=0.0004"), FanucCompile.Mill());

        Assert.Contains("X15.018", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Assert.Empty(Reports(result));
    }

    // D363: the deviation over the linear axes of the block, here sqrt(0.0004² + 0.0004²) = 0.000566, against the
    // allowance: reported above it with the block, the deviation and the address, silent at or below it.
    [Theory]
    [InlineData("0.0005", 1)]
    [InlineData("0.0006", 0)]
    public void RoundLimit_DeviationOverTheLinearAxes_IsReportedAboveTheAllowance(string limit, int count)
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("ROUND_LIMIT=" + limit, "RAPID X=10.0004 Y=20.0004"), FanucCompile.Mill());

        List<Diagnostic> found = Reports(result);
        Assert.Equal(count, found.Count);
        if (count == 1)
        {
            Assert.Equal(Severity.Warning, found[0].Severity);
            Assert.Equal(5, found[0].Line);
            Assert.Contains("0.000566", found[0].Message, StringComparison.Ordinal);
            Assert.Contains("X", found[0].Message, StringComparison.Ordinal);
        }
    }

    // D363: ROUND_LIMIT=OFF stops the report again.
    [Fact]
    public void RoundLimitOff_StopsTheReport()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("ROUND_LIMIT=0.0001", "ROUND_LIMIT=OFF", "RAPID X=10.0004"), FanucCompile.Mill());

        Assert.Empty(Reports(result));
    }

    private static List<Diagnostic> Reports(CompileResult result)
    {
        return result.Diagnostics.Items.Where(item => item.Code == DiagnosticCodes.RoundingBeyondTheLimit).ToList();
    }

    // D363: ROUND_LIMIT and ROUND_LIMIT:ROTARY are state only, without a control form: every family compiles them
    // without an ERROR and writes nothing for them.
    [Fact]
    public void RoundLimit_CompilesWithoutAnErrorOnFanuc()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program("ROUND_LIMIT=0.001 ROUND_LIMIT:ROTARY=0.01",
            "RAPID X=10", "ROUND_LIMIT=OFF"), FanucCompile.Mill());

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.DoesNotContain("ROUND", FanucCompile.TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void RoundLimit_CompilesWithoutAnErrorOnSiemens()
    {
        CompileResult result = SiemensCompile.Run(SiemensCompile.Program(SiemensCompile.MillHeader,
            "ROUND_LIMIT=0.001 ROUND_LIMIT:ROTARY=0.01", "RAPID X=10", "ROUND_LIMIT=OFF"), SiemensCompile.Mill());

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.DoesNotContain("ROUND", SiemensCompile.TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void RoundLimit_CompilesWithoutAnErrorOnHeidenhain()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ROUND_LIMIT=0.001 ROUND_LIMIT:ROTARY=0.01", "RAPID X=10", "ROUND_LIMIT=OFF"));

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.DoesNotContain("ROUND", HeidenhainCompile.Body(result), StringComparison.Ordinal);
    }

    // D363: the written point follows the file block by block: five LINE IX=0.0004 are each written IX0, so the written
    // X stays while the programmed X moves 0.0004 a block; above ROUND_LIMIT=0.001 from the third block on.
    [Fact]
    public void RoundLimit_DriftOfRepeatedIncrements_IsReported()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program("ROUND_LIMIT=0.001", "LINE X=0 F=100",
            "LINE IX=0.0004", "LINE IX=0.0004", "LINE IX=0.0004", "LINE IX=0.0004", "LINE IX=0.0004"),
            FanucCompile.Mill());

        Assert.Equal([8, 9, 10], Reports(result).Select(item => item.Line ?? 0));
    }

    // D363: ROTATE is an angle word, measured against ROUND_LIMIT:ROTARY with the three decimals of a Fanuc angle.
    [Theory]
    [InlineData("0.0001", 1)]
    [InlineData("0.001", 0)]
    public void RoundLimitRotary_Rotate_IsMeasuredAgainstTheRotaryAllowance(string limit, int count)
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program(
            "ROUND_LIMIT=1 ROUND_LIMIT:ROTARY=" + limit, "ROTATE=10.0004", "ROTATE=RESET"), FanucCompile.Mill());

        Assert.Equal(count, Reports(result).Count);
    }
}
