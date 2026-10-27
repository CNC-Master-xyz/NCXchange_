using Ncx.Compilers.Tests.Fanuc;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// CMP015 of the compiler framework (D367, D151; machine-config 5): a value of the tolerance that the written
/// [tolerance] template has no placeholder for, once per distinct value per program, and the held_comment beside the
/// switch. The Fanuc test mill writes ON = "G5.1 Q1", which carries no value.
/// </summary>
public sealed class ToleranceReportTests
{
    private const string Table = "[tolerance]\nON = \"G5.1 Q1\"\nOFF = \"G5.1 Q0\"\n";

    // D367: once per distinct value per program, never for OFF; a WARNING without held_value.
    [Fact]
    public void ToleranceWithoutPlaceholder_OncePerDistinctValue_IsAWarning()
    {
        List<Diagnostic> found = Reports(FanucCompile.Mill(),
            "TOLERANCE=0.01", "TOLERANCE=OFF", "TOLERANCE=0.01", "TOLERANCE=0.02");

        Assert.Equal(2, found.Count);
        Assert.All(found, diagnostic => Assert.Equal(Severity.Warning, diagnostic.Severity));
        Assert.Contains("TOLERANCE 0.01 mm", found[0].Message, StringComparison.Ordinal);
        Assert.Contains("TOLERANCE 0.02 mm", found[1].Message, StringComparison.Ordinal);
    }

    // D367: a held_value at least as tight as the program's TOLERANCE makes it an INFO naming held_in; a looser one
    // stays a WARNING.
    [Fact]
    public void ToleranceWithoutPlaceholder_HeldValueAtLeastAsTight_IsAnInfo()
    {
        string machine = WithTolerance("held_in = \"parameter 19581\"\nheld_value = 0.005\n");

        List<Diagnostic> found = Reports(machine, "TOLERANCE=0.01", "TOLERANCE=0.002");

        Assert.Equal([Severity.Info, Severity.Warning], found.Select(diagnostic => diagnostic.Severity));
        Assert.Contains("parameter 19581", found[0].Message, StringComparison.Ordinal);
    }

    // D151: TOLERANCE:ROTARY on a machine without ON_ROTARY writes ON and reports the rotary tolerance (CMP015).
    [Fact]
    public void RotaryTolerance_WithoutOnRotary_WritesOnAndIsReported()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("TOLERANCE=0.01 TOLERANCE:ROTARY=0.5"), FanucCompile.Mill());

        Assert.Contains("G5.1 Q1\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Assert.Contains(Codes(result),
            message => message.Contains("TOLERANCE:ROTARY 0.5 deg", StringComparison.Ordinal));
    }

    // D151: while TOLERANCE:ROTARY is set the machine's ON_ROTARY is written, which carries {rotary}.
    [Fact]
    public void RotaryTolerance_WithOnRotary_WritesOnRotary()
    {
        string machine = WithTolerance("ON_ROTARY = \"G5.1 Q1 R{rotary}\"\n");

        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("TOLERANCE=0.01 TOLERANCE:ROTARY=0.5"), machine);

        Assert.Contains("G5.1 Q1 R0.5\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Assert.DoesNotContain(Codes(result), message => message.Contains("ROTARY", StringComparison.Ordinal));
    }

    // D367: held_comment writes the program's value and held_in beside the switch.
    [Fact]
    public void HeldComment_WritesTheValueBesideTheSwitch()
    {
        string machine = WithTolerance("held_in = \"P19581\"\nheld_comment = true\n");

        CompileResult result = FanucCompile.Run(FanucCompile.Program("TOLERANCE=0.005", "TOLERANCE=OFF"), machine);

        Assert.Contains("G5.1 Q1 (TOL 0.005 P19581)\nG5.1 Q0\n", FanucCompile.TextOf(result),
            StringComparison.Ordinal);
    }

    private static string WithTolerance(string keys)
    {
        string mill = FanucCompile.Mill();
        Assert.Contains(Table, mill, StringComparison.Ordinal);
        return mill.Replace(Table, Table + keys, StringComparison.Ordinal);
    }

    private static List<Diagnostic> Reports(string machine, params string[] blocks)
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program(blocks), machine);
        return result.Diagnostics.Items.Where(diagnostic => diagnostic.Code == DiagnosticCodes.ToleranceValueNotWritten)
            .ToList();
    }

    private static List<string> Codes(CompileResult result)
    {
        return result.Diagnostics.Items.Where(diagnostic => diagnostic.Code == DiagnosticCodes.ToleranceValueNotWritten)
            .Select(diagnostic => diagnostic.Message).ToList();
    }
}
