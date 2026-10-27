using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The motion rules of virtual machine 5, one test per rule with the smallest input: UNITS, feed, IX, F in a RAPID
/// block, and the machine limits max_feed and limits (machine-config 4, D64, D100).
/// </summary>
public sealed class MotionValidationTests
{
    // VM 3.1, 5: motion before UNITS is an ERROR.
    [Fact]
    public void Motion_BeforeUnits_IsAnError()
    {
        RuleAssert.Only(new VmHarness(VmMachines.Default()).Execute("RAPID X=0"), DiagnosticCodes.MotionBeforeUnits);
    }

    // VM 3.1, 5: LINE without feed is an ERROR.
    [Fact]
    public void Line_WithoutFeed_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM SPINDLE=CW", "LINE X=1");

        RuleAssert.Only(vm, DiagnosticCodes.LineWithoutFeed);
    }

    // D184: an ARC is interpolated at the active feed exactly as a LINE is, so an ARC without feed is VM201 too.
    [Fact]
    public void Feed_ArcWithoutFeed_IsTheSameErrorAsALine()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE=CW", "RAPID X=0 Y=0", "ARC=CW X=20 Y=0 CENTER:X=10 CENTER:Y=0");

        Diagnostic error = RuleAssert.Only(vm, DiagnosticCodes.LineWithoutFeed);

        Assert.Equal("ARC moves at the active feed, and no F is set (virtual machine 3.2, D184).", error.Message);
    }

    // D184: CYCLE_CALL stays outside the rule, because its feed is CYCLE_F and never F (D29).
    [Fact]
    public void Feed_CycleCallWithoutFeed_IsNotTheError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE=CW", "RAPID X=0 Y=0 Z=5",
                "CYCLE=DRILL DEPTH=-10 CLEARANCE=2 CYCLE_F=100", "CYCLE_CALL X=10 Y=10");

        Assert.Equal(0, vm.Count(DiagnosticCodes.LineWithoutFeed));
    }

    // VM 3.1, 5: IX from an unknown position is an ERROR.
    [Fact]
    public void IncrementalWord_FromAnUnknownPosition_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM F=100 SPINDLE=CW", "LINE IX=1");

        RuleAssert.Only(vm, DiagnosticCodes.IncrementalFromUnknownPosition);
    }

    // VM 5: F in a RAPID block is a WARNING; RAPID moves at the rapid rate (language 4.3).
    [Fact]
    public void Feed_InARapidBlock_Warns()
    {
        string text = VmHarness.File("UNITS=MM", "RAPID X=0 F=500", "PROGRAM=END");

        Diagnostic warning = RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.FeedInRapid);

        Assert.Equal(4, warning.Line);
    }

    // D192: VM441 compares the path feed at a LINE or ARC with the max_feed of each linear axis the block moves and
    // names them: X follows 10000 mm/min, Z 8000. An F alone moves nothing and is not compared.
    [Fact]
    public void D192_FeedAboveTheMaxFeedOfTwoMovingAxes_WarnsNamingBoth()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM F=12000 SPINDLE:TOOL=CW");
        vm.AssertNoDiagnostics();

        vm.Execute("LINE X=100 Z=5");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.FeedAboveMaxFeed);
        Assert.StartsWith("F=12000 is above the max_feed of X (10000 mm/min), Z (8000 mm/min)", warning.Message,
            StringComparison.Ordinal);
        Assert.Contains("may clamp", warning.Message, StringComparison.Ordinal);
    }

    // D192: no axis is named that does not move: F=9000 is above Z's 8000 only, and a LINE of X alone passes.
    [Fact]
    public void D192_FeedAboveTheMaxFeedOfAnAxisThatDoesNotMove_IsNoWarning()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM F=9000 SPINDLE:TOOL=CW", "LINE X=100");
        vm.AssertNoDiagnostics();

        vm.Execute("LINE Z=5");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.FeedAboveMaxFeed);
        Assert.StartsWith("F=9000 is above the max_feed of Z (8000 mm/min),", warning.Message,
            StringComparison.Ordinal);
    }

    // D192: one WARNING per F word, on its first exceeding block; a new F word may warn again.
    [Fact]
    public void D192_FeedAboveTheMaxFeed_WarnsOncePerFWord()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM F=9000 SPINDLE:TOOL=CW", "LINE Z=5", "LINE Z=10", "LINE Z=15");

        Assert.Equal(1, vm.Count(DiagnosticCodes.FeedAboveMaxFeed));

        vm.Execute("LINE Z=20 F=9500");

        Assert.Equal(2, vm.Count(DiagnosticCodes.FeedAboveMaxFeed));
    }

    // Machine-config 4, D192: max_feed is in mm/min, and F under UNITS=INCH is in inches per minute: 400 in/min is
    // 10160.
    [Fact]
    public void D192_FeedInInchesAboveTheMaxFeed_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=INCH F=400 SPINDLE:TOOL=CW", "LINE X=10");

        RuleAssert.Only(vm, DiagnosticCodes.FeedAboveMaxFeed);
    }

    // D192: a feed per revolution is converted with the known rpm, 10 mm/rev at 1000 rpm is 10000 mm/min, above Z's
    // 8000; while the rpm is unknown it is skipped.
    [Theory]
    [InlineData("RPM:TOOL=1000", true)]
    [InlineData("RPM:TOOL={$Q1}", false)]
    public void D192_FeedPerRevolution_IsConvertedWithTheKnownRpm(string rpm, bool warns)
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM FEED_MODE=PER_REV F=10 SPINDLE:TOOL=CW " + rpm, "LINE Z=5");

        Assert.Equal(warns ? 1 : 0, vm.Count(DiagnosticCodes.FeedAboveMaxFeed));
    }

    // D192: CYCLE_F of a built-in drilling cycle is compared with the drilling axis, Z (8000 mm/min), once.
    [Fact]
    public void D192_CycleFeedAboveTheMaxFeedOfTheDrillingAxis_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits()).Execute(
            "UNITS=MM SPINDLE:TOOL=CW", "RAPID X=10 Y=0 Z=50",
            "CYCLE=DRILL CLEARANCE=5 DEPTH=-10 CYCLE_F=9000", "CYCLE_CALL X=10 Y=10", "CYCLE_CALL X=20 Y=10");

        Assert.Equal(1, vm.Count(DiagnosticCodes.FeedAboveMaxFeed));
    }

    // VM 5, D100: a target beyond the upper limit, compared in the MACHINE frame: X homes to 300 and a machine-frame
    // move of 10 more ends at 310.
    [Fact]
    public void Target_BeyondTheUpperLimitInTheMachineFrame_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM", "HOME X", "RAPID IX=10 FRAME=MACHINE");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.TargetBeyondLimits);

        Assert.StartsWith("X ends at 310 in the MACHINE frame, beyond its upper limit 300", warning.Message,
            StringComparison.Ordinal);
    }

    // VM 5, D193: the limits of a rotary axis without modulo = true are travel limits and are compared: B homes to 0
    // and a machine-frame move to 130 leaves the swivel range.
    [Fact]
    public void RotaryTravelLimits_SwivelTargetBeyondTheRange_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithRotaryLimits())
            .Execute("UNITS=MM", "HOME B", "RAPID B=130 FRAME=MACHINE");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.TargetBeyondLimits);

        Assert.StartsWith("B ends at 130 in the MACHINE frame, beyond its upper limit 120", warning.Message,
            StringComparison.Ordinal);
    }

    // VM 5, D193: the limits of a modulo rotary axis are its display range and are never compared: C homes to 90 and a
    // machine-frame move to 400 passes without a diagnostic.
    [Fact]
    public void RotaryTravelLimits_ModuloAxisTargetBeyondTheDisplayRange_IsNotCompared()
    {
        new VmHarness(ValidationMachines.MillTurnWithRotaryLimits())
            .Execute("UNITS=MM", "SPINDLE_MODE:MAIN=AXIS", "HOME C", "RAPID C=400 FRAME=MACHINE")
            .AssertNoDiagnostics();
    }

    // VM 5, D100: the lower limit: Z homes to 450 and moves 500 down in the machine frame to -50, below -10.
    [Fact]
    public void Target_BelowTheLowerLimitInTheMachineFrame_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM", "HOME Z", "RAPID IZ=-500 FRAME=MACHINE");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.TargetBeyondLimits);

        Assert.Contains("beyond its lower limit -10", warning.Message, StringComparison.Ordinal);
    }

    // VM 3.4, 5, D101: after SETPOS against the machine position the machine position follows the workpiece
    // coordinate, and the target is compared there: X=0 declared at 300, X=20 stands at 320 in the MACHINE frame.
    [Fact]
    public void Target_BeyondTheLimitThroughTheSetposShift_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithLimits())
            .Execute("UNITS=MM", "HOME X", "SETPOS X=0", "RAPID X=20");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.TargetBeyondLimits);

        Assert.StartsWith("X ends at 320 in the MACHINE frame", warning.Message, StringComparison.Ordinal);
    }

    // VM 5, D100: a target is not compared while the machine position is unknown: X known in the workpiece frame of an
    // ORIGIN whose datum the configuration does not give.
    [Fact]
    public void Target_WithTheMachinePositionUnknown_IsNotCompared()
    {
        new VmHarness(ValidationMachines.MillTurnWithLimits()).Execute("UNITS=MM", "RAPID X=1000")
            .AssertNoDiagnostics();
    }
}
