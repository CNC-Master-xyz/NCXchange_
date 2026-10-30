using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests.Cycles;

/// <summary>
/// The cycle catalog of a machine: the built-in drilling family of its controller, the catalog file of [cycles] and
/// the [[cycle]] entries of the machine file over both (machine-config 6, 5a).
/// </summary>
public sealed class MachineCycleEntriesTests
{
    // A Fanuc lathe that selects the peck behaviour with a mode function before G83 (machine-config 5a, the Doosan
    // of language 4.7.1).
    private const string PeckModeLathe = """
        [machine]
        name = "Test lathe"
        controller = "fanuc"

        [cycles]
        catalog = "fanuc-lathe.toml"

        [[cycle]]
        name = "PECK"
        native = "G83"
        pre = ["FUNC:PECK_MODE=RETRACT"]

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }
        """;

    // Machine-config 6: without [[cycle]] a machine has the built-in family of its controller.
    [Fact]
    public void MachineFile_WithoutCycleEntries_HasTheDrillingFamilyOfItsController()
    {
        MachineConfig machine = LoadMachine("""
            [machine]
            name = "Test mill"
            controller = "siemens"

            [coolant]
            STANDARD = { ON = "M8", OFF = "M9" }
            """);

        Assert.Empty(machine.CycleEntries);
        Assert.Equal(Controller.Siemens, machine.CycleCatalog.Controller);
        Assert.Equal("CYCLE81", machine.CycleCatalog.Find("DRILL")?.Native);
    }

    // P2-03: a catalog entry with a pre rule loads into an ExpansionRule; the entry of the machine file overrides the
    // family per machine and keeps the address words of G83 (machine-config 6, 5a).
    [Fact]
    public void MachineFile_CycleEntryWithPre_OverridesTheFamilyAndCarriesTheRule()
    {
        MachineConfig machine = LoadMachine(PeckModeLathe);

        CycleEntry written = Assert.Single(machine.CycleEntries);
        Assert.Equal(["name", "native", "pre"], written.WrittenKeys);
        CycleEntry? peck = machine.CycleCatalog.Find("PECK");
        Assert.NotNull(peck);
        Assert.Equal(["FUNC:PECK_MODE=RETRACT"], peck.Rule?.Pre);
        Assert.Equal("Q", peck.NativeOf("PECK"));
        Assert.True(peck.Modal);
    }

    // Machine-config 6: the catalog file of [cycles] stands between the family and the entries of the machine file;
    // its turning cycles arrive and the machine's PECK stays on top.
    [Fact]
    public void WithCatalog_ShippedFanucCatalog_StandsBetweenTheFamilyAndTheMachine()
    {
        MachineConfig machine = LoadMachine(PeckModeLathe);
        CycleCatalog catalog = LoadShippedCatalog(machine);

        MachineConfig withCatalog = CycleCatalogLoader.WithCatalog(machine, catalog);

        Assert.True(withCatalog.CycleCatalog.Find("TURN_OD")?.Modal);
        Assert.Equal(["P", "Q"], withCatalog.CycleCatalog.Find("ROUGH_TURN")?.Contour);
        Assert.Equal(["FUNC:PECK_MODE=RETRACT"], withCatalog.CycleCatalog.Find("PECK")?.Rule?.Pre);
        Assert.Equal("Q", withCatalog.CycleCatalog.Find("PECK")?.NativeOf("PECK"));
        Assert.Same(machine.CycleEntries, withCatalog.CycleEntries);
    }

    // The catalog of another controller family cannot serve a machine; that is a mistake of the caller, not of a file
    // (code-guidelines 6).
    [Fact]
    public void WithCatalog_CatalogOfAnotherFamily_Throws()
    {
        MachineConfig machine = LoadMachine(PeckModeLathe);

        Assert.Throws<InvalidOperationException>(
            () => CycleCatalogLoader.WithCatalog(machine, DrillingFamily.Catalog(Controller.Heidenhain)));
    }

    // P2-01: a [[cycle]] of a machine file with a wrong type is an ERROR on its line, and the machine does not load.
    [Fact]
    public void MachineFile_CycleEntryWithWrongType_ReportsItsLineAndGivesNoMachine()
    {
        var diagnostics = new Diagnostics("test.toml");

        MachineConfig? machine = MachineConfigLoader.LoadText("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [[cycle]]
            name = "PECK"
            native = "G83"
            modal = "yes"

            [coolant]
            STANDARD = { ON = "M8", OFF = "M9" }
            """, diagnostics);

        Assert.Null(machine);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(8, error.Line);
    }

    // Machine-config 6: the three mills of machines/ name the shipped catalog of their family, which loads for the
    // machine's controller and serves it.
    [Theory]
    [InlineData("fanuc-mill-30i.toml", "G81")]
    [InlineData("heidenhain-itnc530.toml", "200")]
    [InlineData("siemens-840dsl-mill.toml", "CYCLE81")]
    public void MillMachine_NamedCatalog_LoadsForItsController(string machineFile, string drillNative)
    {
        var machineDiagnostics = new Diagnostics("machines/" + machineFile);
        MachineConfig? machine = MachineConfigLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "machines", machineFile), machineDiagnostics);
        Assert.NotNull(machine);

        CycleCatalog catalog = LoadShippedCatalog(machine);
        MachineConfig withCatalog = CycleCatalogLoader.WithCatalog(machine, catalog);

        Assert.Equal("DRILL", withCatalog.CycleCatalog.FindNative(drillNative)?.Name);
    }

    // D104: millturn1.toml names the Siemens catalog, which loads for it.
    [Fact]
    public void Millturn1_NamedCatalog_LoadsForItsController()
    {
        var diagnostics = new Diagnostics("docs/spec/examples/machines/millturn1.toml");
        MachineConfig? machine =
            MachineConfigLoader.LoadText(Fixture.ReadText("machines/millturn1.toml"), diagnostics);
        Assert.NotNull(machine);

        MachineConfig withCatalog = CycleCatalogLoader.WithCatalog(machine, LoadShippedCatalog(machine));

        Assert.Equal("_AXN", withCatalog.CycleCatalog.Find("PECK")?.Axis);
    }

    // D361, D377: a [[cycle]] table with channel applies to that channel only, the channel-less table being the
    // fallback; available = false marks a cycle the channel cannot run.
    [Fact]
    public void MachineFile_CycleEntryPerChannel_AppliesToItsChannelOnly()
    {
        MachineConfig machine = LoadMachine("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"
            channels = [1, 2]

            [[cycle]]
            name = "TAP"
            native = "G84"
            before = "M329 S{rpm}"

            [[cycle]]
            name = "TAP"
            native = "G84"
            channel = 2
            available = false

            [coolant]
            STANDARD = { ON = "M8", OFF = "M9" }
            """);

        CycleCatalog channel1 = machine.CycleCatalog.ForChannel(1);
        CycleCatalog channel2 = machine.CycleCatalog.ForChannel(2);

        Assert.Equal("M329 S{rpm}", channel1.Find("TAP")?.Before);
        Assert.True(channel1.Find("TAP")?.Available);
        Assert.False(channel2.Find("TAP")?.Available);
        Assert.Equal("M329 S{rpm}", channel2.Find("TAP")?.Before);
        Assert.True(machine.CycleCatalog.Find("TAP")?.Available);
    }

    // D377: channel describes one machine; in a catalog file it is the WARNING CFG157 and ignored. D410: available =
    // false in a catalog file withdraws an inherited entry, and is no WARNING.
    [Fact]
    public void CatalogFile_ChannelWarnsAndIsIgnored_AvailableWithdrawsTheEntry()
    {
        var diagnostics = new Diagnostics("cycles/test.toml");

        CycleCatalog? catalog = CycleCatalogLoader.LoadText("""
            [[cycle]]
            name = "TAP"
            native = "G84"
            channel = 2
            available = false
            """, Controller.Fanuc, diagnostics);

        Assert.NotNull(catalog);
        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.CycleMachineKeyInCatalog, warning.Code);
        Assert.False(catalog.Find("TAP")?.Available);
        Assert.Null(catalog.FindNative("G84"));
        Assert.Empty(catalog.ChannelEntries);
    }

    // D409: first_block names params of the entry only; another word is CFG160.
    [Fact]
    public void CatalogFile_FirstBlockOfAWordThatIsNoParam_IsCfg160()
    {
        var diagnostics = new Diagnostics("cycles/test.toml");

        CycleCatalogLoader.LoadText("""
            [[cycle]]
            name = "GROOVE"
            native = "G75"
            params = { STEP_X = "P" }
            first_block = ["RETURN_AMOUNT"]
            """, Controller.Fanuc, diagnostics);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal((Severity.Error, "CFG160"), (error.Severity, error.Code));
    }

    // D361: the Mori Seiki taps rigidly with M329, the before line of its TAP entries on G84 and G88; the Nakamura
    // keeps the catalog's M29 on both paths (D377).
    [Theory]
    [InlineData("mori-ntx1000-mapps.toml", "M329 S{rpm}")]
    [InlineData("nakamura-ntjx.toml", "M29 S{rpm}")]
    public void LatheMachine_TapBeforeLine_IsTheBuildersCode(string machineFile, string before)
    {
        var machineDiagnostics = new Diagnostics("machines/" + machineFile);
        MachineConfig? machine = MachineConfigLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "machines", machineFile), machineDiagnostics);
        Assert.NotNull(machine);

        MachineConfig withCatalog = CycleCatalogLoader.WithCatalog(machine, LoadShippedCatalog(machine));

        Assert.Equal(before, withCatalog.CycleCatalog.ForChannel(1).Find("TAP", "Z")?.Before);
        Assert.Equal(before, withCatalog.CycleCatalog.ForChannel(1).Find("TAP", "X")?.Before);
        Assert.Null(machine.Functions.GetValueOrDefault("SYNC_TAP"));
    }

    // Loads a machine file that is expected to be clean; a failure prints the diagnostics.
    private static MachineConfig LoadMachine(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
        return machine;
    }

    // Loads the shipped catalog that [cycles] catalog of the machine names, from cycles/ of the repository, for the
    // controller of the machine (machine-config 6, 10).
    private static CycleCatalog LoadShippedCatalog(MachineConfig machine)
    {
        string? catalogFile = machine.Cycles?.CatalogFile;
        Controller? controller = machine.Machine.Controller;
        Assert.NotNull(catalogFile);
        Assert.NotNull(controller);

        var diagnostics = new Diagnostics("cycles/" + catalogFile);
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", catalogFile), controller.Value, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(catalog);
        return catalog;
    }
}
