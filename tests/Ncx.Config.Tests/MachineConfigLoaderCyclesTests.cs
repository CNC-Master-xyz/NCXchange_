using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The keys of [cycles] beside the catalog (machine-config 6): surface_clearance (D158), chip_break_lift and
/// peck_clearance (D357), ream_feed_out (D378), contour_type_ii and peck_rigid_tapping (D388 item 11); and the
/// repeat_counters of [variables] (machine-config 7, D280).
/// </summary>
public sealed class MachineConfigLoaderCyclesTests
{
    // D158, D357, D378, D388 item 11: every key loads with its value and without a warning.
    [Fact]
    public void Cycles_EveryKey_LoadsWithoutWarning()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [cycles]
            catalog = "fanuc.toml"
            surface_clearance = 2
            chip_break_lift = 0.5
            peck_clearance = 1.0
            ream_feed_out = "SAFE"
            contour_type_ii = true
            peck_rigid_tapping = true
            """);

        CyclesConfig cycles = Assert.IsType<CyclesConfig>(machine.Cycles);
        Assert.Equal(2m, cycles.SurfaceClearance);
        Assert.Equal(0.5m, cycles.ChipBreakLift);
        Assert.Equal(1.0m, cycles.PeckClearance);
        Assert.Equal(ReamFeedOut.Safe, cycles.ReamFeedOut);
        Assert.True(cycles.ContourTypeII);
        Assert.True(cycles.PeckRigidTapping);
    }

    // D357, D378, D388 item 11: a key left out is null or false, so the family's behaviour applies.
    [Fact]
    public void Cycles_KeysLeftOut_AreNullOrFalse()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "siemens"

            [cycles]
            catalog = "siemens.toml"
            """);

        CyclesConfig cycles = Assert.IsType<CyclesConfig>(machine.Cycles);
        Assert.Null(cycles.SurfaceClearance);
        Assert.Null(cycles.ChipBreakLift);
        Assert.Null(cycles.PeckClearance);
        Assert.Null(cycles.ReamFeedOut);
        Assert.False(cycles.ContourTypeII);
        Assert.False(cycles.PeckRigidTapping);
    }

    // D357: a negative distance is the ERROR CFG005 on its line, and the key is dropped.
    [Theory]
    [InlineData("chip_break_lift")]
    [InlineData("peck_clearance")]
    [InlineData("surface_clearance")]
    public void Cycles_NegativeDistance_IsCfg005OnItsLine(string key)
    {
        Diagnostics diagnostics = Report($"""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [cycles]
            {key} = -1
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
        Assert.Equal(6, error.Line);
        Assert.Contains(key, error.Message, StringComparison.Ordinal);
    }

    // D378: ream_feed_out other than "CLEARANCE" or "SAFE" is the ERROR CFG005.
    [Fact]
    public void Cycles_ReamFeedOutOtherValue_IsCfg005()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [cycles]
            ream_feed_out = "DEPTH"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
        Assert.Equal(6, error.Line);
    }

    // D280: [variables] repeat_counters is the list of counter variables, one per REPEAT nesting depth.
    [Fact]
    public void Variables_RepeatCounters_LoadInOrder()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [variables]
            repeat_counters = ["#149", "#148"]
            """);

        Assert.Equal(["#149", "#148"], machine.Variables.RepeatCounters);
    }

    private static Diagnostics Report(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfigLoader.LoadText(toml, diagnostics);
        return diagnostics;
    }

    private static MachineConfig LoadClean(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
        return machine;
    }
}
