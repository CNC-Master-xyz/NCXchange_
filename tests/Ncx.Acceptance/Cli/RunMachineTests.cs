using System.Globalization;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// The machine of a run: --machine, else the machine that ncx.toml of the working directory names, else the built-in
/// default machine of D103; the catalog file that [cycles] catalog names beneath the [[cycle]] entries of the machine
/// (architecture 10; machine-config 6, 10; implementation 12, P2-04).
/// </summary>
public sealed class RunMachineTests : IDisposable
{
    // A catalog file with an entry whose key machine-config 6 does not know, the WARNING CFG002 on line 4, so that the
    // test sees that the catalog was loaded (P2-01, P2-03).
    private const string MarkedCatalog = """
        [[cycle]]
        name = "DRILL"
        native = "G81"
        {0} = true
        """;

    private readonly ProjectHarness _project = new();

    public void Dispose()
    {
        _project.Dispose();
    }

    // Architecture 10: ncx.toml names the machine file that --machine defaults to; the turret the clutch mill does not
    // have is then an ERROR against it, not the D103 WARNING (VM 3.8 rule 1).
    [Fact]
    public void NcxToml_Machine_IsTheMachineOfARunWithoutMachineOption()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "machine = \"mill\"\n");
        _project.WriteInWorkingDirectory("machines/mill.toml", TestMachines.ClutchMill);

        int exitCode = _project.Check(TurretProgram(), null);

        Assert.Equal(1, exitCode);
        Assert.Contains("(4): ERROR ", _project.Error, StringComparison.Ordinal);
    }

    // D103, architecture 10: without --machine and without a machine in ncx.toml the run uses the built-in default
    // machine, and the turret is the WARNING "not checked: no machine file".
    [Fact]
    public void NcxToml_WithoutMachine_RunsAgainstTheDefaultMachine()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "out = \"nc\"\n");

        int exitCode = _project.Check(TurretProgram(), null);

        Assert.True(exitCode == 0, _project.Error);
        Assert.Contains("(4): WARNING VM003: ", _project.Error, StringComparison.Ordinal);
    }

    // Architecture 10: --machine wins over the machine that ncx.toml names, which is then never looked for.
    [Fact]
    public void MachineOption_WinsOverTheMachineOfNcxToml()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "machine = \"nowhere\"\n");
        _project.WriteInWorkingDirectory("machines/mill.toml", TestMachines.ClutchMill);
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "mill");

        Assert.True(exitCode == 0, _project.Error);
        Assert.Equal("", _project.Error);
    }

    // D97, D98: a machine of ncx.toml that cannot be found is exit code 2, reported on the line of the machine key.
    [Fact]
    public void NcxToml_MachineThatCannotBeFound_ExitsTwoOnTheLineOfTheKey()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "out = \"nc\"\nmachine = \"nowhere\"\n");

        int exitCode = _project.Check(TurretProgram(), null);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx.toml(2): ERROR CLI200: The machine nowhere of ncx.toml names no file at that path",
            _project.Error, StringComparison.Ordinal);
    }

    // P2-01, D97: an ncx.toml with an ERROR stops the run before it starts, with exit code 1.
    [Fact]
    public void NcxToml_WithAnError_ExitsOneBeforeTheRun()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "machine = 5\n");

        int exitCode = _project.Check(TurretProgram(), null);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("ncx.toml(1): ERROR CFG003: ", _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("VM003", _project.Error, StringComparison.Ordinal);
    }

    // D97: an ncx.toml whose bytes are no UTF-8 text is an input that cannot be read, exit code 2.
    [Fact]
    public void NcxToml_ThatIsNoUtf8_ExitsTwoWithCli201()
    {
        _project.WriteBytesInWorkingDirectory("ncx.toml", [0x6D, 0xFF, 0xFE, 0x0A]);

        int exitCode = _project.Check(TurretProgram(), null);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx.toml(1): ERROR CLI201: ", _project.Error, StringComparison.Ordinal);
    }

    // P2-01: an unknown key of ncx.toml is a WARNING on its line, and the run goes on.
    [Fact]
    public void NcxToml_UnknownKey_IsAWarningAndTheRunGoesOn()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "colour = \"red\"\n");
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, null);

        Assert.True(exitCode == 0, _project.Error);
        Assert.StartsWith("ncx.toml(1): WARNING CFG002: Unknown key colour in ncx.toml", _project.Error,
            StringComparison.Ordinal);
    }

    // Machine-config 6, 10: the catalog file that [cycles] catalog names is found in cycles/ and loaded beneath the
    // [[cycle]] entries of the machine; its mistakes are diagnostics of the catalog on their lines.
    [Fact]
    public void CycleCatalog_OfTheMachine_IsLoadedFromTheCycleFolder()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithCatalog("shop.toml"));
        _project.WriteInWorkingDirectory("cycles/shop.toml", Catalog("found_in_work"));
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "mill");

        Assert.True(exitCode == 0, _project.Error);
        Assert.StartsWith(Path.Combine("cycles", "shop.toml") + "(4): WARNING CFG002: Unknown key found_in_work",
            _project.Error, StringComparison.Ordinal);
    }

    // Architecture 10: ncx.toml names the cycle folder, which is searched first; the tool's own folder, which holds
    // the shipped catalogs, comes last.
    [Fact]
    public void CycleCatalog_CycleFolderOfNcxToml_IsSearchedFirst()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "cycles = \"shop-cycles\"\n");
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithCatalog("shop.toml"));
        _project.WriteInWorkingDirectory("shop-cycles/shop.toml", Catalog("found_in_shop"));
        _project.WriteInWorkingDirectory("cycles/shop.toml", Catalog("found_in_work"));
        _project.WriteInToolFolder("cycles/shop.toml", Catalog("found_in_tool"));
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "mill");

        Assert.True(exitCode == 0, _project.Error);
        Assert.Contains("found_in_shop", _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("found_in_work", _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("found_in_tool", _project.Error, StringComparison.Ordinal);
    }

    // Machine-config 6, 10, D97: a catalog that no cycle folder holds is an input that cannot be read, exit code 2.
    [Fact]
    public void CycleCatalog_InNoCycleFolder_ExitsTwoWithCli202()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithCatalog("nowhere.toml"));
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "mill");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("nowhere.toml(1): ERROR CLI202: The cycle catalog nowhere.toml that [cycles] catalog of "
            + Path.Combine("machines", "mill.toml") + " names is in none of the cycle folders cycles, ",
            _project.Error, StringComparison.Ordinal);
        Assert.Contains(" and no built-in catalog of that name (", _project.Error, StringComparison.Ordinal);
    }

    // D207, D371: a shipped catalog that none of the cycle folders holds is the copy built into ncx, the fourth and
    // last place of the lookup.
    [Fact]
    public void CycleCatalog_ShippedCatalogInNoCycleFolder_IsTheBuiltInCopy()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithCatalog("fanuc.toml"));
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "mill");

        Assert.True(exitCode == 0, _project.Error);
    }

    // D207, D371: a shop's own catalog of a shipped name wins over the built-in copy.
    [Fact]
    public void CycleCatalog_ShopFileOfAShippedName_WinsOverTheBuiltInCopy()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithCatalog("fanuc.toml"));
        _project.WriteInWorkingDirectory("cycles/fanuc.toml", "[[cycle]\n");
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "mill");

        Assert.Equal(1, exitCode);
        Assert.StartsWith(Path.Combine("cycles", "fanuc.toml") + "(1): ERROR CFG001: ", _project.Error,
            StringComparison.Ordinal);
    }

    // Machine-config 6, 10: the catalog is a file of a cycle folder and never a file of the working directory itself,
    // so a machine file that carries the name of its catalog is not loaded as its own catalog.
    [Fact]
    public void CycleCatalog_FileOfItsNameInTheWorkingDirectory_DoesNotShadowTheCycleFolder()
    {
        _project.WriteInWorkingDirectory("shop.toml", MillWithCatalog("shop.toml"));
        _project.WriteInWorkingDirectory("cycles/shop.toml", Catalog("found_in_work"));
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "shop.toml");

        Assert.True(exitCode == 0, _project.Error);
        Assert.StartsWith(Path.Combine("cycles", "shop.toml") + "(4): WARNING CFG002: Unknown key found_in_work",
            _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("[machine]", _project.Error, StringComparison.Ordinal);
    }

    // Machine-config 6, 10: a catalog value with a folder in it is looked for below each cycle folder in turn, not from
    // the working directory as a path of --machine is.
    [Fact]
    public void CycleCatalog_ValueWithAFolder_IsLookedForBelowTheCycleFolders()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithCatalog("shop/drill.toml"));
        _project.WriteInWorkingDirectory("shop/drill.toml", Catalog("found_from_work"));
        _project.WriteInWorkingDirectory("cycles/shop/drill.toml", Catalog("found_in_work"));
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "mill");

        Assert.True(exitCode == 0, _project.Error);
        Assert.StartsWith(
            Path.Combine("cycles", "shop", "drill.toml") + "(4): WARNING CFG002: Unknown key found_in_work",
            _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("found_from_work", _project.Error, StringComparison.Ordinal);
    }

    // Machine-config 6, 10, D97: a full path names a file outside the cycle folders, which is none of their catalogs:
    // exit code 2 with CLI202, although the file exists.
    [Fact]
    public void CycleCatalog_FullPathOutsideTheCycleFolders_ExitsTwoWithCli202()
    {
        string elsewhere = _project.WriteInWorkingDirectory("elsewhere/shop.toml", Catalog("found_elsewhere"));
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithCatalog(elsewhere));
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "mill");

        Assert.Equal(2, exitCode);
        Assert.StartsWith(elsewhere + "(1): ERROR CLI202: ", _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("found_elsewhere", _project.Error, StringComparison.Ordinal);
    }

    // P2-01, D97: a catalog that is not TOML is an ERROR of the catalog, which stops the run with exit code 1.
    [Fact]
    public void CycleCatalog_ThatIsNotToml_ExitsOne()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", MillWithCatalog("shop.toml"));
        _project.WriteInWorkingDirectory("cycles/shop.toml", "[[cycle]\n");
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "mill");

        Assert.Equal(1, exitCode);
        Assert.StartsWith(Path.Combine("cycles", "shop.toml") + "(1): ERROR CFG001: ", _project.Error,
            StringComparison.Ordinal);
    }

    // A tool of a turret the clutch mill does not have, on line 4 (VM 3.8 rule 1, D103).
    private string TurretProgram()
    {
        return _project.WriteInWorkingDirectory("turret.ncx", CliHarness.OneProgram("UNITS=MM", "TOOL:TURRET1=1"));
    }

    // The clutch mill with the catalog file of its controller family named in [cycles] (machine-config 6), as a TOML
    // literal string, so that a full path of any system is written as it is.
    private static string MillWithCatalog(string catalog)
    {
        return TestMachines.ClutchMill + "\n\n[cycles]\ncatalog = '" + catalog + "'\n";
    }

    // A catalog file of the Fanuc family whose DRILL entry carries the key given.
    private static string Catalog(string mark)
    {
        return MarkedCatalog.Replace("{0}", mark, StringComparison.Ordinal) + "\n";
    }

    // D358: a Heidenhain machine whose catalog writes Q256 and that sets no [cycles] chip_break_lift is the ERROR
    // CFG158 on the line of [cycles], with the shipped catalog and with the built-in drilling family alike.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HeidenhainMachine_WithoutChipBreakLift_IsCfg158(bool withCatalog)
    {
        string machine = ShippedMachine("heidenhain-itnc530.toml").Replace("chip_break_lift = 0.6", "#",
            StringComparison.Ordinal);
        if (!withCatalog)
        {
            machine = machine.Replace("catalog = \"heidenhain.toml\"", "#", StringComparison.Ordinal);
        }

        _project.WriteInWorkingDirectory("machines/tnc.toml", machine);
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "tnc");

        Assert.Equal(1, exitCode);
        Assert.Contains("tnc.toml(" + LineOf(machine, "[cycles]") + "): ERROR CFG158: ", _project.Error,
            StringComparison.Ordinal);
    }

    // D358: the shipped Heidenhain machine sets chip_break_lift and loads clean.
    [Fact]
    public void ShippedHeidenhainMachine_LoadsClean()
    {
        _project.WriteInWorkingDirectory("machines/tnc.toml", ShippedMachine("heidenhain-itnc530.toml"));
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        int exitCode = _project.Check(program, "tnc");

        Assert.True(exitCode == 0, _project.Error);
        Assert.DoesNotContain("CFG15", _project.Error, StringComparison.Ordinal);
    }

    // D358: on Siemens chip_break_lift is _VRT and peck_clearance _DIS1 of CYCLE83; a [[cycle]] entry whose defaults
    // give the same parameter beside the key is the WARNING CFG156.
    [Theory]
    [InlineData("chip_break_lift = 1.0", "CHIP_BREAK", "_VRT")]
    [InlineData("peck_clearance = 1.0", "PECK", "_DIS1")]
    public void SiemensMachine_KeyBesideADefaultOfItsParameter_IsCfg156(string key, string entry, string native)
    {
        string machine = ShippedMachine("siemens-840dsl-mill.toml").Replace(
            "chip_break_lift = 1.0", key, StringComparison.Ordinal);
        machine += $$"""

            [[cycle]]
            name = "{{entry}}"
            native = "CYCLE83"
            signature = [
                "RTP", "RFP", "SDIS", "DP", "DPR", "FDEP", "FDPR", "_DAM", "DTB", "DTS", "FRF", "VARI", "_AXN", "_MDEP",
                "_VRT", "_DTD", "_DIS1",
            ]
            params = { SAFE = "RTP", SURFACE = "RFP", CLEARANCE = "SDIS", DEPTH = "DP", PECK = "FDPR" }
            defaults = { {{native}} = 2 }
            """;
        _project.WriteInWorkingDirectory("machines/sinumerik.toml", machine);
        string program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));

        _project.Check(program, "sinumerik");

        Assert.Contains("WARNING CFG156: ", _project.Error, StringComparison.Ordinal);
        Assert.Contains(native, _project.Error, StringComparison.Ordinal);
    }

    // A machine file of machines/ of the repository, as shipped.
    private static string ShippedMachine(string name)
    {
        return File.ReadAllText(Path.Combine(Fixture.RepositoryRoot(), "machines", name));
    }

    // The 1-based line of the first line of a text that starts with a prefix.
    private static string LineOf(string text, string prefix)
    {
        string[] lines = text.ReplaceLineEndings("\n").Split('\n');
        int index = Array.FindIndex(lines, line => line.StartsWith(prefix, StringComparison.Ordinal));
        return (index + 1).ToString(CultureInfo.InvariantCulture);
    }
}
