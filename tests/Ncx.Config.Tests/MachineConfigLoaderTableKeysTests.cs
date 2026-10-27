using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The keys the decisions of 2026-10-07 and 2026-10-27 add to the tables of machine-config 3 to 5: ON_ROTARY and the
/// held value of [tolerance] (D151, D367), the rot map and the path mode of [transform] (D269, D349), the named tool
/// change (D172), the reserved marks (D315), rapid_exact_stop (D286), [stock] (D351) and the pair of [spindle_sync]
/// (D154).
/// </summary>
public sealed class MachineConfigLoaderTableKeysTests
{
    // Two work spindles, the default one leading; the lathe the [spindle_sync] tests start from.
    private const string TwoWorkSpindles = """
        [machine]
        name = "Test lathe"
        controller = "fanuc"
        default_spindle = "S1"

        [roles]
        MAIN = "S1"
        SUB = "S2"
        TOOL = "S3"

        [[resource]]
        id = "S1"
        type = "work_spindle"

        [[resource]]
        id = "S2"
        type = "work_spindle"

        [[resource]]
        id = "S3"
        type = "tool_spindle"

        """;

    // D151, D367: ON_ROTARY is written while TOLERANCE:ROTARY is set; held_in, held_value and held_comment say where
    // the control holds a value the template cannot carry.
    [Fact]
    public void Tolerance_OnRotaryAndHeldValue_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "siemens"

            [tolerance]
            ON = "CYCLE832({tol},{mode},1)"
            ON_ROTARY = "CYCLE832({tol},1{mode},{rotary})"
            OFF = "CYCLE832(0,0,1)"
            held_in = "parameter 19581"
            held_value = 0.005
            held_comment = true
            """);

        ToleranceTable? tolerance = machine.Tolerance;
        Assert.NotNull(tolerance);
        Assert.Equal("CYCLE832({tol},{mode},1)", tolerance.On);
        Assert.Equal("CYCLE832({tol},1{mode},{rotary})", tolerance.OnRotary);
        Assert.Equal("parameter 19581", tolerance.HeldIn);
        Assert.Equal(0.005m, tolerance.HeldValue);
        Assert.True(tolerance.HeldComment);
    }

    // D367 with D357: a held value is zero or greater; a negative one is the ERROR of a value not allowed.
    [Fact]
    public void Tolerance_NegativeHeldValue_IsValueNotAllowed()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [tolerance]
            ON = "G5.1 Q1"
            held_value = -0.01
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
        Assert.Equal(7, error.Line);
    }

    // D269: the rot map gives {rot} its words per ROT option; D349: the two path mode templates, empty where the
    // control has no modal switch.
    [Fact]
    public void Transform_RotMapAndPathMode_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "heidenhain"

            [transform]
            TILT_ON = "PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move} {rot}"
            move = { TURN = "TURN FMAX", STAY = "STAY" }
            rot = { TABLE = "TABLE ROT", COORD = "COORD ROT" }
            PATH_MODE_EXACT_STOP = ""
            PATH_MODE_CONTINUOUS = ""
            """);

        TransformTable? transform = machine.Transform;
        Assert.NotNull(transform);
        Assert.Equal("TABLE ROT", transform.Rot["TABLE"]);
        Assert.Equal("COORD ROT", transform.Rot["COORD"]);
        Assert.Equal("", transform.PathModeExactStop);
        Assert.Equal("", transform.PathModeContinuous);
    }

    // D269: the rot map takes the two ROT options only; another key is a WARNING that names the nearest one.
    [Fact]
    public void Transform_RotMapWithAnotherOption_WarnsOfTheUnknownKey()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "heidenhain"

            [transform]
            rot = { TABLE = "TABLE ROT", CORD = "COORD ROT" }
            """);

        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.UnknownKey, warning.Code);
        Assert.Contains("COORD", warning.Message, StringComparison.Ordinal);
    }

    // D349: the Fanuc path mode templates.
    [Fact]
    public void Transform_FanucPathMode_LoadsG61AndG64()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [transform]
            PATH_MODE_EXACT_STOP = "G61"
            PATH_MODE_CONTINUOUS = "G64"
            """);

        Assert.Equal("G61", machine.Transform?.PathModeExactStop);
        Assert.Equal("G64", machine.Transform?.PathModeContinuous);
    }

    // D172: the named tool change and preload stand beside the numeric ones.
    [Fact]
    public void ToolChange_NamedTemplates_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "siemens"

            [tool_change]
            change = "T{tool} M6"
            change_named = "T=\"{name}\" M6"
            preload = "T{tool}"
            preload_named = "T=\"{name}\""
            tool_name_allowed = true
            """);

        Assert.Equal("T{tool} M6", machine.ToolChange?.Change);
        Assert.Equal("T=\"{name}\" M6", machine.ToolChange?.ChangeNamed);
        Assert.Equal("T=\"{name}\"", machine.ToolChange?.PreloadNamed);
    }

    // D315: the marks the job compiler never picks; empty when left out.
    [Fact]
    public void Sync_ReservedMarks_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "siemens"

            [sync]
            wait = "WAITM({mark},{channels})"
            mark_range = [0, 99]
            reserved_marks = [0, 1, 10, 15, 20, 25, 80, 90, 95]
            """);

        Assert.Equal([0, 1, 10, 15, 20, 25, 80, 90, 95], machine.Sync?.ReservedMarks);
    }

    // D286: rapid_exact_stop overrides the family default; left out it is unknown.
    [Theory]
    [InlineData("rapid_exact_stop = true", true)]
    [InlineData("rapid_exact_stop = false", false)]
    [InlineData("", null)]
    public void Dynamics_RapidExactStop_LoadsTheOverride(string line, bool? expected)
    {
        MachineConfig machine = LoadClean($"""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [dynamics]
            path_mode = "continuous"
            {line}
            """);

        Assert.Equal(expected, machine.Dynamics?.RapidExactStop);
    }

    // D351: [stock] holds one template per value form, several lines joined by \n as in [retract]; another key is a
    // WARNING.
    [Fact]
    public void Stock_BoxAndCylinder_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "heidenhain"

            [stock]
            BOX = "BLK FORM 0.1 {axis} X{x} Y{y} Z{z}\nBLK FORM 0.2 X{max_x} Y{max_y} Z{max_z}"
            CYLINDER = "BLK FORM CYLINDER {axis} R{radius} L{length} DIST{dist}"
            """);

        Assert.Equal("BLK FORM 0.1 {axis} X{x} Y{y} Z{z}\nBLK FORM 0.2 X{max_x} Y{max_y} Z{max_z}", machine.Stock?.Box);
        Assert.Equal("BLK FORM CYLINDER {axis} R{radius} L{length} DIST{dist}", machine.Stock?.Cylinder);
    }

    // D351: a file without [stock] has none, and an unknown key of [stock] is a WARNING.
    [Fact]
    public void Stock_UnknownKey_Warns()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "siemens"

            [stock]
            BOXX = "WORKPIECE()"
            """);

        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.UnknownKey, warning.Code);
        Assert.Contains("BOX", warning.Message, StringComparison.Ordinal);
    }

    // D154: [spindle_sync] ON couples the default spindle, the leader, with the other work spindle:
    // SPINDLE_SYNC=MAIN,SUB without a pair key.
    [Fact]
    public void SpindleSync_TwoWorkSpindlesWithoutPair_CouplesTheDefaultWithTheOther()
    {
        MachineConfig machine = LoadClean(TwoWorkSpindles + """
            [spindle_sync]
            ON = "M96"
            OFF = "M97"
            """);

        Assert.Empty(machine.SpindleSyncPair);
        Assert.Equal(["MAIN", "SUB"], machine.SpindleSyncRoles());
    }

    // D154: the optional pair names the two roles, the leader first, and wins over the default pair.
    [Fact]
    public void SpindleSync_Pair_LoadsAndWins()
    {
        MachineConfig machine = LoadClean(TwoWorkSpindles + """
            [spindle_sync]
            ON = "L725(41,1,0)"
            pair = ["MAIN", "TOOL"]
            """);

        Assert.Equal(["MAIN", "TOOL"], machine.SpindleSyncPair);
        Assert.Equal(["MAIN", "TOOL"], machine.SpindleSyncRoles());
    }

    // D154: a pair names two different roles of [roles]; anything else is the ERROR of a value not allowed.
    [Theory]
    [InlineData("pair = [\"MAIN\"]")]
    [InlineData("pair = [\"MAIN\", \"MAIN\"]")]
    [InlineData("pair = [\"MAIN\", \"COUNTER\"]")]
    public void SpindleSync_BadPair_IsValueNotAllowed(string line)
    {
        Diagnostics diagnostics = Report(TwoWorkSpindles + $"""
            [spindle_sync]
            ON = "M96"
            {line}
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
    }

    // D154: the pair is required with more than two work spindles, or when the default spindle is not a work spindle.
    [Theory]
    [InlineData("S3", "work_spindle")]
    [InlineData("S3", "tool_spindle")]
    public void SpindleSync_PairNeededButMissing_IsMissingKey(string defaultSpindle, string thirdType)
    {
        Diagnostics diagnostics = Report($"""
            [machine]
            name = "Test lathe"
            controller = "fanuc"
            default_spindle = "{defaultSpindle}"

            [roles]
            MAIN = "S1"
            SUB = "S2"
            THIRD = "S3"

            [[resource]]
            id = "S1"
            type = "work_spindle"

            [[resource]]
            id = "S2"
            type = "work_spindle"

            [[resource]]
            id = "S3"
            type = "{thirdType}"

            [spindle_sync]
            ON = "M96"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.MissingKey, error.Code);
        Assert.Contains("pair", error.Message, StringComparison.Ordinal);
    }

    // Loads a machine file that is expected to report something and returns what it reported.
    private static Diagnostics Report(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfigLoader.LoadText(toml, diagnostics);
        return diagnostics;
    }

    // Loads a machine file that is expected to be clean; a failure prints the diagnostics.
    private static MachineConfig LoadClean(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
        return machine;
    }
}
