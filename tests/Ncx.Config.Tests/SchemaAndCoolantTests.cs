using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The schema version ncx_schema of a machine file and a cycle catalog (D348), and the coolant channel STANDARD that
/// every machine file must name (D130).
/// </summary>
public sealed class SchemaAndCoolantTests
{
    private const string Mill = """
        [machine]
        name = "Test mill"
        controller = "fanuc"

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }
        """;

    // D348: "A machine file and a cycle catalog may carry ncx_schema = 1 as their first key ... The key joins the
    // known root keys of both loaders in the same commit, so it is never a CFG002."
    [Fact]
    public void MachineFile_NcxSchemaOne_LoadsWithoutWarning()
    {
        var diagnostics = new Diagnostics("mill.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText("ncx_schema = 1\n\n" + Mill, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
    }

    // D348: "a version this build does not read is the ERROR CFG009, naming the file and the version."
    [Fact]
    public void MachineFile_NcxSchemaTwo_IsCfg009NamingFileAndVersion()
    {
        var diagnostics = new Diagnostics("mill.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText("ncx_schema = 2\n\n" + Mill, diagnostics);

        Assert.Null(machine);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.UnsupportedSchema, error.Code);
        Assert.Equal(1, error.Line);
        Assert.Contains("mill.toml", error.Message, StringComparison.Ordinal);
        Assert.Contains("ncx_schema = 2", error.Message, StringComparison.Ordinal);
    }

    // D348: "A file without the key is version 1."
    [Fact]
    public void MachineFile_WithoutNcxSchema_IsVersionOne()
    {
        var diagnostics = new Diagnostics("mill.toml");
        Assert.NotNull(MachineConfigLoader.LoadText(Mill, diagnostics));
        Assert.Equal("", diagnostics.ToText());
    }

    // D348: the same key and the same ERROR in a cycle catalog.
    [Fact]
    public void CycleCatalog_NcxSchema_OneLoadsAndTwoIsCfg009()
    {
        const string Entry = """

            [[cycle]]
            name = "DRILL"
            native = "G81"
            """;

        var clean = new Diagnostics("fanuc.toml");
        Assert.NotNull(CycleCatalogLoader.LoadText("ncx_schema = 1\n" + Entry, Controller.Fanuc, clean));
        Assert.Equal("", clean.ToText());

        var wrong = new Diagnostics("fanuc.toml");
        Assert.Null(CycleCatalogLoader.LoadText("ncx_schema = 3\n" + Entry, Controller.Fanuc, wrong));
        Diagnostic error = Assert.Single(wrong.Items);
        Assert.Equal(DiagnosticCodes.UnsupportedSchema, error.Code);
        Assert.Contains("fanuc.toml", error.Message, StringComparison.Ordinal);
        Assert.Contains("ncx_schema = 3", error.Message, StringComparison.Ordinal);
    }

    // D130: "the machine-file loader requires [coolant] STANDARD (ERROR when missing) so the compiler always has a
    // template for it." A file without [coolant] at all.
    [Fact]
    public void MachineFile_WithoutCoolantTable_IsMissingKeyError()
    {
        var diagnostics = new Diagnostics("mill.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText("""
            [machine]
            name = "Test mill"
            controller = "fanuc"
            """, diagnostics);

        Assert.Null(machine);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.MissingKey, error.Code);
        Assert.Contains("STANDARD", error.Message, StringComparison.Ordinal);
    }

    // D130: a [coolant] table that names other channels only; the ERROR stands on the line of [coolant].
    [Fact]
    public void MachineFile_CoolantWithoutStandard_IsMissingKeyErrorOnTheTable()
    {
        var diagnostics = new Diagnostics("mill.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [coolant]
            MIST = { ON = "M7", OFF = "M9" }
            """, diagnostics);

        Assert.Null(machine);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.MissingKey, error.Code);
        Assert.Equal(5, error.Line);
    }
}
