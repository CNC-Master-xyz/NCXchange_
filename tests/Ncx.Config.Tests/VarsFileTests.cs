using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The start values of the variables, &lt;name&gt;.vars.toml (machine-config 8, virtual machine 2.7, 3.6).
/// </summary>
public sealed class VarsFileTests
{
    // Machine-config 8: Q1 = 10 seeds the variable Q1 with 10.
    [Fact]
    public void VarsFile_Q1Equals10_SeedsQ1WithTen()
    {
        var diagnostics = new Diagnostics("pattern.vars.toml");

        VarsFile? vars = VarsFile.LoadText("""
            Q1 = 10
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(vars);
        Assert.Equal(new IntegerValue(10, "10"), vars.Variables["Q1"]);
    }

    // Machine-config 8: numbers and strings, as the file writes them.
    [Fact]
    public void VarsFile_DecimalAndString_SeedAsWritten()
    {
        var diagnostics = new Diagnostics("pattern.vars.toml");

        VarsFile? vars = VarsFile.LoadText("""
            Q2 = 5.5
            QS1 = "TEXT"
            """, diagnostics);

        Assert.NotNull(vars);
        Assert.Equal(new DecimalValue(5.5m, "5.5"), vars.Variables["Q2"]);
        Assert.Equal(new StringValue("TEXT"), vars.Variables["QS1"]);
        Assert.Equal(["Q2", "QS1"], vars.Variables.Keys);
    }

    // Machine-config 8: a start value is a number or a string; a table is a wrong type.
    [Fact]
    public void VarsFile_Table_ReportsWrongType()
    {
        var diagnostics = new Diagnostics("pattern.vars.toml");

        VarsFile? vars = VarsFile.LoadText("""
            Q1 = 10
            Q2 = { X = 1 }
            """, diagnostics);

        Assert.Null(vars);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(2, error.Line);
    }

    // D120: an indexed register is the name as the program writes it, without $, quoted, with the index as a plain
    // whole number; the key grammar allows a comma-separated multi-index from the start.
    [Fact]
    public void D120_IndexedKeys_SeedTheirRegisters()
    {
        var diagnostics = new Diagnostics("pattern.vars.toml");

        VarsFile? vars = VarsFile.LoadText("""
            "SYS_WEAR_Z[99]" = 0.012
            "SYS_X[3,1]" = 1
            "SYS_WEAR_X[007]" = 2
            SYS_POS_X = 5
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(vars);
        Assert.Equal(new DecimalValue(0.012m, "0.012"), vars.Variables["SYS_WEAR_Z[99]"]);
        Assert.Equal(new IntegerValue(1, "1"), vars.Variables["SYS_X[3,1]"]);
        Assert.Equal(new IntegerValue(2, "2"), vars.Variables["SYS_WEAR_X[7]"]);
        Assert.Equal(new IntegerValue(5, "5"), vars.Variables["SYS_POS_X"]);
    }

    // D120: a key that is no name as a program writes it, or an index that is no plain whole number, is an ERROR on
    // its line; only a SYS_ name takes an index (D145).
    [Theory]
    [InlineData("\"SYS_WEAR_Z[-1]\" = 1")]
    [InlineData("\"SYS_WEAR_Z[1.5]\" = 1")]
    [InlineData("\"SYS_WEAR_Z[ 1 ]\" = 1")]
    [InlineData("\"SYS_WEAR_Z[]\" = 1")]
    [InlineData("\"SYS_X[3,]\" = 1")]
    [InlineData("\"$Q1\" = 1")]
    [InlineData("\"Q1[2]\" = 1")]
    public void D120_KeyOfAnotherForm_IsAnError(string line)
    {
        var diagnostics = new Diagnostics("pattern.vars.toml");

        VarsFile? vars = VarsFile.LoadText("Q1 = 1\n" + line + "\n", diagnostics);

        Assert.Null(vars);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
        Assert.Equal(2, error.Line);
    }
}
