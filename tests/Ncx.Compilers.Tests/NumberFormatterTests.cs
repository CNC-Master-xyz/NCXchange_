using Ncx.Compilers.Tests.Fakes;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// Numbers per [format] (machine-config 2): decimals per address with the family defaults (D366), trailing zeros, the
/// decimal separator, never rounds what fits, rounds silently what does not (D363).
/// </summary>
public sealed class NumberFormatterTests
{
    // The block a WARNING cites.
    private static readonly Block s_block = new() { Line = 7, Words = [] };

    // Phase 3, P3-03: what fits is written as it is, never rounded.
    [Fact]
    public void Format_ValueThatFits_IsWrittenAsItIs()
    {
        var diagnostics = new Diagnostics("T.ncx");
        NumberFormatter numbers = Formatter(FakeMachines.PointFormat, diagnostics);

        Assert.Equal("10.12", numbers.Format("X", 10.12m, s_block));
        Assert.Empty(diagnostics.Items);
    }

    // D363: a value with more decimals than the machine takes is rounded half away from zero on its decimal value to
    // the decimals of its address (15.0175 becomes 15.018), silently; CMP020 is retired.
    [Theory]
    [InlineData("10.1235", "10.124")]
    [InlineData("-10.1235", "-10.124")]
    [InlineData("0.0005", "0.001")]
    [InlineData("15.0175", "15.018")]
    public void Format_MoreDecimalsThanTheMachineTakes_IsRoundedSilently(string value, string written)
    {
        var diagnostics = new Diagnostics("T.ncx");
        NumberFormatter numbers = Formatter(FakeMachines.PointFormat, diagnostics);

        string text = numbers.Format("X", decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            s_block);

        Assert.Equal(written, text);
        Assert.Empty(diagnostics.Items);
    }

    // Machine-config 2: a value rounded to zero is written without a minus sign.
    [Fact]
    public void Format_NegativeValueRoundedToZero_HasNoMinusSign()
    {
        NumberFormatter numbers = Formatter(FakeMachines.PointFormat, new Diagnostics("T.ncx"));

        Assert.Equal("0", numbers.Format("X", -0.0004m, s_block));
    }

    // Machine-config 2: trailing_zeros = true pads to the decimals of the address, with the comma of the machine.
    [Fact]
    public void Format_TrailingZerosAndComma_PadsToTheDecimalsWithTheComma()
    {
        NumberFormatter numbers = Formatter(FakeMachines.CommaFormat, new Diagnostics("T.ncx"));

        Assert.Equal("10,5000", numbers.Format("X", 10.5m, s_block));
        Assert.Equal("-7,0250", numbers.Format("Y", -7.025m, s_block));
    }

    // Controllers fanuc.md 2, 10 rule 5: a controller that writes the point on every real number writes X70., and the
    // speed of S = 0 without it.
    [Fact]
    public void Format_PointOnWholeNumbers_WritesThePointWhereTheAddressTakesDecimals()
    {
        NumberFormatter numbers =
            Formatter(FakeMachines.PointFormat, new Diagnostics("T.ncx"), pointOnWholeNumbers: true);

        Assert.Equal("70.", numbers.Format("X", 70m, s_block));
        Assert.Equal("70.", numbers.Format("X", 70.000m, s_block));
        Assert.Equal("1592", numbers.FormatSpeed(1592m, s_block));
        Assert.Equal("565.", numbers.FormatFeed(565m, s_block));
    }

    // D366: no address is written with all its digits; a linear address [format] decimals leaves out takes the
    // decimals of X.
    [Fact]
    public void Format_LinearAddressWithoutDecimals_TakesTheDecimalsOfX()
    {
        NumberFormatter numbers = Formatter(FakeMachines.PointFormat, new Diagnostics("T.ncx"));

        Assert.Equal("1.235", numbers.Format("Q", 1.23456m, s_block));
        Assert.Equal("2.5", numbers.Format("R", 2.500m, s_block));
    }

    // D366: an angle address [format] decimals leaves out, ANGLE or a rotary axis, takes four decimals on Heidenhain
    // (its input resolution of 0.0001 degree) and three on Siemens and Fanuc; a written ANGLE wins.
    [Theory]
    [InlineData("heidenhain", "ANGLE", "12.3457")]
    [InlineData("heidenhain", "C", "12.3457")]
    [InlineData("siemens", "ANGLE", "12.346")]
    [InlineData("fanuc", "B", "12.346")]
    [InlineData("fanuc", "ROTATE", "12.346")]
    public void Format_AngleAddressWithoutDecimals_TakesTheFamilyDefault(string controller, string address,
        string written)
    {
        var numbers = new NumberFormatter(Machine(FakeMachines.Mill(controller,
            "[format]\ndecimal_separator = \".\"\ndecimals = { X = 2 }")), pointOnWholeNumbers: false);

        Assert.Equal(written, numbers.Format(address, 12.345678m, s_block));
    }

    // D366: a written ANGLE takes the decimals the machine file gives it.
    [Fact]
    public void Format_WrittenAngleDecimals_Win()
    {
        var numbers = new NumberFormatter(Machine(FakeMachines.Mill("heidenhain",
            "[format]\ndecimal_separator = \".\"\ndecimals = { X = 3, ANGLE = 2 }")), pointOnWholeNumbers: false);

        Assert.Equal("12.35", numbers.Format("ANGLE", 12.345678m, s_block));
    }

    // Wave-1 question #64: without decimal_separator a Heidenhain machine writes the comma of its files, the others the
    // point; a written decimal_separator wins.
    [Theory]
    [InlineData("heidenhain", null, ",")]
    [InlineData("fanuc", null, ".")]
    [InlineData("siemens", null, ".")]
    [InlineData("heidenhain", ".", ".")]
    [InlineData("fanuc", ",", ",")]
    public void DecimalSeparatorOf_ControllerAndKey_IsTheWrittenKeyElseTheControllersOwn(string controller,
        string? written, string expected)
    {
        string format = written is null
            ? "[format]\ndecimals = { X = 3 }"
            : $"[format]\ndecimal_separator = \"{written}\"\ndecimals = {{ X = 3 }}";

        MachineConfig machine = Machine(FakeMachines.Mill(controller, format));

        Assert.Equal(expected, NumberFormatter.DecimalSeparatorOf(machine));
    }

    private static NumberFormatter Formatter(string format, Diagnostics diagnostics, bool pointOnWholeNumbers = false)
    {
        return new NumberFormatter(Machine(FakeMachines.Mill(format: format)), pointOnWholeNumbers);
    }

    private static MachineConfig Machine(string toml)
    {
        var diagnostics = new Diagnostics("fake.toml");
        return MachineConfigLoader.LoadText(toml, diagnostics)
            ?? throw new InvalidOperationException(diagnostics.ToText());
    }
}
