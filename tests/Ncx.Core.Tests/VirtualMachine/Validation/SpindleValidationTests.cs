using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The spindle rules of virtual machine 5, one test per rule with the smallest input: the spindle modes of 3.8 rule 4,
/// the spindle of the tool holder before a LINE, and the speed limits of the machine (machine-config 5, D64).
/// </summary>
public sealed class SpindleValidationTests
{
    // VM 3.8 rule 4, 5: SPINDLE on a spindle in AXIS mode is an ERROR.
    [Fact]
    public void Spindle_InAxisMode_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE_MODE:MAIN=AXIS", "SPINDLE:MAIN=CW");

        RuleAssert.Only(vm, DiagnosticCodes.SpindleInAxisMode);
    }

    // VM 3.8 rule 4, 5: C= on a spindle in SPINDLE mode is an ERROR.
    [Fact]
    public void AxisWord_OnASpindleInSpindleMode_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "RAPID C=0");

        RuleAssert.Only(vm, DiagnosticCodes.AxisOfSpindleInSpindleMode);
    }

    // D131: the rule covers every word that positions the axis or declares its position, so the bare C of HOME and a
    // TILT_AXIS that moves the rotary axes are ERRORs in SPINDLE mode too.
    [Theory]
    [InlineData("HOME C")]
    [InlineData("TILT_AXIS C=180 MOVE=TURN")]
    [InlineData("TILT_AXIS C=180 MOVE=MOVE")]
    public void SpindleAxisWords_WordThatPositionsTheAxisInSpindleMode_IsAnError(string block)
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", block);

        Assert.Equal(1, vm.Count(DiagnosticCodes.AxisOfSpindleInSpindleMode));
    }

    // D131: a SHIFT appends a chain entry and moves nothing, and a TILT_AXIS with MOVE=STAY leaves the axes standing;
    // neither is covered.
    [Theory]
    [InlineData("SHIFT C=5")]
    [InlineData("TILT_AXIS C=180 MOVE=STAY")]
    [InlineData("TILT_AXIS C=180")]
    public void SpindleAxisWords_WordThatMovesNothingInSpindleMode_IsNotAnError(string block)
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", block);

        Assert.Equal(0, vm.Count(DiagnosticCodes.AxisOfSpindleInSpindleMode));
    }

    // VM 5: spindle OFF before a LINE is a WARNING; on the default machine the tool holder carries the spindle TOOL.
    [Fact]
    public void SpindleOff_BeforeALine_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM F=100", "LINE X=1");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.SpindleOffBeforeLine);

        Assert.Equal(
            "LINE cuts while spindle TOOL, the spindle of the current tool holder, is OFF (virtual machine 5).",
            warning.Message);
    }

    // VM 5: a holder without a spindle of its own checks the default spindle: the second turret of the mill-turn has
    // none, and the default spindle MAIN is OFF while the tool spindle runs.
    [Fact]
    public void SpindleOff_HolderWithoutASpindle_ChecksTheDefaultSpindle()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM F=100 SPINDLE:TOOL=CW", "TOOL:TURRET2=5", "LINE X=1");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.SpindleOffBeforeLine);

        Assert.Equal(
            "LINE cuts while the default spindle MAIN is OFF; the current tool holder TURRET2 has no spindle of its "
            + "own (virtual machine 5).",
            warning.Message);
    }

    // D129: the WARNING fires only when neither the spindle of the current tool holder nor the spindle of the
    // current workpiece holder runs: turning with a stationary tool against the running work spindle MAIN is a normal
    // cut, and with MAIN stopped as well the WARNING stands.
    [Fact]
    public void D129_SpindleOffWhileTheWorkSpindleRuns_IsNoWarning()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM F=100 SPINDLE:MAIN=CW RPM:MAIN=1500", "TOOL=5", "LINE X=40", "LINE Z=-60");

        Assert.DoesNotContain(DiagnosticCodes.SpindleOffBeforeLine, vm.Codes());

        vm.Execute("SPINDLE:MAIN=OFF", "LINE X=50");

        Assert.Contains(DiagnosticCodes.SpindleOffBeforeLine, vm.Codes());
    }

    // D194: an ARC cuts as a LINE does, so the rule covers it too (decisions.md; virtual machine 5).
    [Fact]
    public void SpindleOff_BeforeAnArc_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM F=100", "RAPID X=0 Y=0", "ARC=CW X=10 Y=0 R=5");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.SpindleOffBeforeLine);

        Assert.Equal(
            "ARC cuts while spindle TOOL, the spindle of the current tool holder, is OFF (virtual machine 5).",
            warning.Message);
    }

    // D194: a CYCLE_CALL of the built-in drilling family feeds to DEPTH at CYCLE_F, and each Fanuc canned-cycle
    // section says the spindle must be set turning before the cycle, so the rule covers it (decisions.md; fanuc 6).
    [Fact]
    public void SpindleOff_BeforeADrillingCycleCall_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM", "RAPID X=0 Y=0 Z=50", "CYCLE=DRILL CLEARANCE=5 DEPTH=-10 CYCLE_F=100",
                "CYCLE_CALL X=10");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.SpindleOffBeforeLine);

        Assert.Equal(
            "CYCLE_CALL cuts while spindle TOOL, the spindle of the current tool holder, is OFF (virtual machine 5).",
            warning.Message);
    }

    // D194: TAP is rigid tapping (controller-mapping 5), where the cycle positions and drives the spindle itself, so
    // the spindle state before the call does not decide the cut and the rule passes.
    [Fact]
    public void SpindleOff_BeforeATapCycleCall_IsNoWarning()
    {
        new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM", "RAPID X=0 Y=0 Z=50", "CYCLE=TAP CLEARANCE=5 DEPTH=-10 PITCH=1.5", "CYCLE_CALL X=10")
            .AssertNoDiagnostics();
    }

    // Language 5 rule 3: the state words of a motion block take effect before its motion, so the spindle started in the
    // LINE block runs when it cuts.
    [Fact]
    public void SpindleOff_StartedInTheLineBlock_IsOn()
    {
        new VmHarness(VmMachines.Default()).Execute("UNITS=MM F=100", "LINE X=1 SPINDLE=CW").AssertNoDiagnostics();
    }

    // VM 5, D64: RPM above the spindle's rpm_max is a WARNING.
    [Fact]
    public void Rpm_AboveRpmMax_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithSpindleLimits()).Execute("RPM:MAIN=8000");

        Diagnostic warning = RuleAssert.Only(vm, DiagnosticCodes.RpmAboveMax);

        Assert.StartsWith("RPM:MAIN=8000 is above the rpm_max 6000 of the spindle MAIN", warning.Message,
            StringComparison.Ordinal);
    }

    // VM 5, D64: RPM below the spindle's rpm_min is a WARNING.
    [Fact]
    public void Rpm_BelowRpmMin_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithSpindleLimits()).Execute("RPM:MAIN=10");

        RuleAssert.Only(vm, DiagnosticCodes.RpmBelowMin);
    }

    // VM 3.8 rule 2: RPM without a role address is the speed of the default spindle, MAIN on the mill-turn.
    [Fact]
    public void Rpm_WithoutRoleAboveRpmMaxOfTheDefaultSpindle_Warns()
    {
        VmHarness vm = new VmHarness(ValidationMachines.MillTurnWithSpindleLimits()).Execute("RPM=8000");

        RuleAssert.Only(vm, DiagnosticCodes.RpmAboveMax);
    }

    // Language 4.11: RPM is ignored while CSS is on, so it is not compared.
    [Fact]
    public void Rpm_UnderCss_IsNotCompared()
    {
        new VmHarness(ValidationMachines.MillTurnWithSpindleLimits()).Execute("CSS:MAIN=ON RPM:MAIN=8000")
            .AssertNoDiagnostics();
    }
}
