using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Config.Tests;

/// <summary>
/// The [machine] keys of the 2026-10 decisions: arc_tolerance (D139), arc_radius_tolerance (D364), dwell_increment
/// (D159), base_units and extended_units (D292).
/// </summary>
public sealed class MachineKeysTests
{
    // D139, D364, D159, D292: every key read as written.
    [Fact]
    public void MachineKeys_Written_LoadIntoTheIdentity()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "siemens"
            units_default = "MM"
            arc_tolerance = 0.02
            arc_radius_tolerance = 0.005
            dwell_increment = 0.01
            base_units = "inch"
            extended_units = true
            """);

        Assert.Equal(0.02m, machine.Machine.ArcTolerance);
        Assert.Equal(0.005m, machine.Machine.ArcRadiusTolerance);
        Assert.Equal(0.01m, machine.Machine.DwellIncrement);
        Assert.Equal("INCH", machine.Machine.BaseUnits);
        Assert.Equal("INCH", machine.Machine.ResolvedBaseUnits);
        Assert.True(machine.Machine.ExtendedUnits);
        Assert.Equal(0.02m, VmOptions.ForMachine(machine).ArcTolerance);
    }

    // D139: "absent means the D36 defaults per unit"; D159: "default 0.001"; D292: base_units defaults to
    // units_default, extended_units to false; D364: without the key one output step applies (null).
    [Fact]
    public void MachineKeys_LeftOut_TakeTheirDefaults()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "siemens"
            units_default = "INCH"
            """);

        Assert.Null(machine.Machine.ArcTolerance);
        Assert.Null(VmOptions.ForMachine(machine).ArcTolerance);
        Assert.Null(machine.Machine.ArcRadiusTolerance);
        Assert.Equal(0.001m, machine.Machine.DwellIncrement);
        Assert.Null(machine.Machine.BaseUnits);
        Assert.Equal("INCH", machine.Machine.ResolvedBaseUnits);
        Assert.False(machine.Machine.ExtendedUnits);
    }

    // A tolerance or an increment of 0 or below is no length and no time: CFG005 on its line.
    [Theory]
    [InlineData("arc_tolerance = 0")]
    [InlineData("arc_radius_tolerance = -0.01")]
    [InlineData("dwell_increment = 0")]
    public void MachineKeys_NotGreaterThanZero_IsValueNotAllowed(string line)
    {
        Diagnostic error = SingleError($"""
            [machine]
            name = "Test mill"
            controller = "fanuc"
            {line}
            """);

        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
        Assert.Equal(4, error.Line);
    }

    // D292: base_units takes "mm" or "inch" (MD10240).
    [Fact]
    public void BaseUnits_OtherWord_IsValueNotAllowed()
    {
        Diagnostic error = SingleError("""
            [machine]
            name = "Test mill"
            controller = "siemens"
            base_units = "cm"
            """);

        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
        Assert.Equal(4, error.Line);
    }

    // Loads a clean machine file, with the coolant channel every file names (D130).
    private static MachineConfig LoadClean(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(toml + Coolant, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
        return machine;
    }

    // Loads a machine file with one ERROR and returns it.
    private static Diagnostic SingleError(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        Assert.Null(MachineConfigLoader.LoadText(toml + Coolant, diagnostics));
        return Assert.Single(diagnostics.Items);
    }

    private const string Coolant = """


        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }
        """;
}
