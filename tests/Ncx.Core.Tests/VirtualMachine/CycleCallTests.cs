using System.Globalization;
using System.Text;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// CYCLE_CALL (language 4.7, virtual machine 3.3, 5): the sequence of the built-in drilling family along the drilling
/// axis, the position afterwards, the individual motions under ExpandCycles (D37), the lathe cycle with AXIS=X under
/// DIAMETER=ON (D59, D60), the native cycle (D94), and the cycle rules with their suppression (3.9, D99).
/// </summary>
public sealed class CycleCallTests
{
    // Language 6, VM 3.3, D37: the drilling example of the language. Under ExpandCycles each call is four motions, the
    // rapid to the hole, the rapid to CLEARANCE, the feed to DEPTH at CYCLE_F and the retract to CLEARANCE; afterwards
    // the plane axes stand at the call point and Z at the retract plane.
    [Fact]
    public void CycleCall_DrillingExampleOfTheLanguage_GivesTheFourPositionsAndTheRetractPlane()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CYCLE_F=565",
            "CYCLE_CALL X=10 Y=10");

        Assert.Equal(
            ["RAPID X=10 Y=10", "RAPID Z=5", "LINE Z=-21.732 F=565", "RAPID Z=5"],
            Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(10m), vm.Position("X"));
        Assert.Equal(Workpiece(10m), vm.Position("Y"));
        Assert.Equal(Workpiece(5m), vm.Position("Z"));

        vm.Execute("CYCLE_CALL X=30");

        Assert.Equal(
            ["RAPID X=30", "RAPID Z=5", "LINE Z=-21.732 F=565", "RAPID Z=5"],
            Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(30m), vm.Position("X"));
        Assert.Equal(Workpiece(10m), vm.Position("Y"));
        Assert.Equal(Workpiece(5m), vm.Position("Z"));

        vm.Execute("CYCLE=OFF");

        vm.AssertNoDiagnostics();
        Assert.False(vm.State.Cycle.Active);
    }

    // VM 3.3, D37: without ExpandCycles the call raises no individual motions; the position afterwards is the same.
    [Fact]
    public void CycleCall_WithoutExpandCycles_GivesNoMotionsAndTheSamePosition()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "UNITS=MM SPINDLE=CW RPM=1000",
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_F=565",
            "CYCLE_CALL X=10 Y=10");

        vm.AssertNoDiagnostics();
        Assert.Empty(vm.Vm.LastCycleMotions);
        Assert.Equal(Workpiece(10m), vm.Position("X"));
        Assert.Equal(Workpiece(5m), vm.Position("Z"));
    }

    // VM 3.3, language 4.7: CYCLE_RETRACT=SAFE retracts to SAFE.
    [Fact]
    public void CycleCall_CycleRetractSafe_RetractsToTheSafePlane()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL CLEARANCE=5 DEPTH=-10 SAFE=50 CYCLE_RETRACT=SAFE CYCLE_F=100",
            "CYCLE_CALL X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal("RAPID Z=50", Describe(vm.Vm.LastCycleMotions)[^1]);
        Assert.Equal(Workpiece(50m), vm.Position("Z"));
    }

    // VM 3.3, language 4.7: CYCLE_RETRACT=SAFE without SAFE gives no retract plane, and Z is unknown afterwards.
    [Fact]
    public void CycleCall_CycleRetractSafeWithoutSafe_LeavesTheDrillingAxisUnknown()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL CLEARANCE=5 DEPTH=-10 CYCLE_RETRACT=SAFE CYCLE_F=100",
            "CYCLE_CALL X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 3.3, 5: CYCLE_CALL without a cycle is an ERROR.
    [Fact]
    public void CycleCall_WithoutACycle_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM SPINDLE=CW RPM=1000", "CYCLE_CALL X=10");

        Assert.Equal([DiagnosticCodes.CycleCallWithoutCycle], vm.Codes());
    }

    // VM 3.3, 5: a built-in drilling cycle needs DEPTH and CLEARANCE.
    [Theory]
    [InlineData("CYCLE=DRILL CLEARANCE=5 CYCLE_F=100")]
    [InlineData("CYCLE=DRILL DEPTH=-10 CYCLE_F=100")]
    public void CycleCall_BuiltInCycleWithoutDepthOrClearance_IsAnError(string cycle)
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE=CW RPM=1000", cycle, "CYCLE_CALL X=10");

        Assert.Equal([DiagnosticCodes.CycleCallWithoutDepthOrClearance], vm.Codes());
    }

    // VM 3.3, D94: for a CYCLE:controller=n cycle the VM does not know the sequence: the call positions the plane
    // axes, the drilling axis is unknown afterwards, ExpandCycles raises no motions, and there is no DEPTH or
    // CLEARANCE check.
    [Fact]
    public void CycleCall_NativeCycle_PositionsThePlaneAxesOnly()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50", "CYCLE:HEIDENHAIN=200 Q200=2 Q201=-15 Q206=300", "CYCLE_CALL X=10 Y=10");

        vm.AssertNoDiagnostics();
        Assert.Empty(vm.Vm.LastCycleMotions);
        Assert.Equal(Workpiece(10m), vm.Position("X"));
        Assert.Equal(Workpiece(10m), vm.Position("Y"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // D187: a Fanuc turning cycle brings the tool back to its start point and has no drilling axis, so after its
    // call no axis has moved and none becomes unknown; the words of the call are the end point of one pass
    // (decisions.md; virtual machine 3.3; fanuc 6).
    [Fact]
    public void CycleCall_FanucTurningCycle_LeavesEveryAxisWhereItStood()
    {
        VmHarness vm = Turning().Execute(
            "WORKPLANE=ZX", "RAPID X=70 Y=0 Z=2", "CYCLE=TURN_OD CYCLE_F=0.3", "CYCLE_CALL X=60 Z=-70");

        vm.AssertNoDiagnostics();
        Assert.Empty(vm.Vm.LastCycleMotions);
        Assert.Equal(Workpiece(70m), vm.Position("X"));
        Assert.Equal(Workpiece(0m), vm.Position("Y"));
        Assert.Equal(Workpiece(2m), vm.Position("Z"));
    }

    // D411: G71 and G72 (type I and II), G74 and G75 leave the tool at the cycle start point too (0i-FPlus B-64700EN
    // p.290, parameter DTP; B-64694EN-1 p.77; 0i-D B-64304EN-1-01 p.88), so ROUGH_TURN, ROUGH_FACE, FACE_GROOVE and
    // GROOVE leave every axis where it stood, as TURN_OD does.
    [Theory]
    [InlineData("ROUGH_TURN")]
    [InlineData("ROUGH_FACE")]
    [InlineData("FACE_GROOVE")]
    [InlineData("GROOVE")]
    public void CycleCall_FanucCycleOfG71ToG75_LeavesEveryAxisAtTheStartPoint(string name)
    {
        VmHarness vm = Turning().Execute(
            "WORKPLANE=ZX", "RAPID X=70 Y=0 Z=2", "CYCLE=" + name, "CYCLE_CALL X=60 Z=-70");

        Assert.Equal(Workpiece(70m), vm.Position("X"));
        Assert.Equal(Workpiece(0m), vm.Position("Y"));
        Assert.Equal(Workpiece(2m), vm.Position("Z"));
    }

    // VM 3.3, D59, D60: a side drilling cycle on a lathe with AXIS=X under DIAMETER=ON, as the cross holes of
    // POLAR_FACE: the X values of the cycle are diameters and halved, C positions the hole, and X ends at the clearance
    // radius 27.
    [Fact]
    public void CycleCall_AxisXOnALatheUnderDiameterOn_HalvesTheXValuesAndPositionsWithC()
    {
        VmHarness vm = Expanding().Execute(
            "WORKPLANE=ZX DIAMETER=ON SPINDLE_MODE:MAIN=AXIS",
            "RAPID X=60 Z=-20 C=0",
            "CYCLE=DRILL AXIS=X SURFACE=50 CLEARANCE=54 DEPTH=30 CYCLE_RETRACT=CLEARANCE CYCLE_F=0.05",
            "CYCLE_CALL C=120");

        vm.AssertNoDiagnostics();
        Assert.Equal(
            ["RAPID C=120", "RAPID X=27", "LINE X=15 F=0.05", "RAPID X=27"],
            Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(27m), vm.Position("X"));
        Assert.Equal(Workpiece(-20m), vm.Position("Z"));
        Assert.Equal(Workpiece(120m), vm.Position("C"));
    }

    // VM 5, D59: AXIS naming an axis that is not a linear axis of the machine is an ERROR.
    [Fact]
    public void CycleCall_AxisNamingARotaryAxis_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE=CW RPM=1000", "CYCLE=DRILL AXIS=C CLEARANCE=5 DEPTH=-10", "CYCLE_CALL X=10");

        Assert.Equal([DiagnosticCodes.CycleAxisNotLinear], vm.Codes());
    }

    // VM 3.3, language 4.7, D190, D355: PECK without SURFACE counts its steps from CLEARANCE and returns to CLEARANCE
    // between them; the default machine has no family, so the re-entry ends at the depth reached (D357).
    [Fact]
    public void CycleCall_PeckCycle_FeedsInPecksWithAFullRetract()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=PECK CLEARANCE=2 DEPTH=-10 PECK=5 CYCLE_F=100", "CYCLE_CALL X=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(
            [
                "RAPID X=0", "RAPID Z=2", "LINE Z=-3 F=100", "RAPID Z=2", "RAPID Z=-3", "LINE Z=-8 F=100", "RAPID Z=2",
                "RAPID Z=-8", "LINE Z=-10 F=100", "RAPID Z=2",
            ],
            Describe(vm.Vm.LastCycleMotions));
    }

    // D355, D357: the steps of PECK count from SURFACE, and the re-entry rapids to peck_clearance above the depth
    // reached (Fanuc parameter 5115).
    [Fact]
    public void CycleCall_PeckFromSurface_StepsFromSurfaceAndReentersAtPeckClearance()
    {
        VmHarness vm = Expanding(Controller.Fanuc, new CyclesConfig { PeckClearance = 1m }).Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=PECK SURFACE=0 CLEARANCE=2 DEPTH=-10 PECK=4 CYCLE_F=100", "CYCLE_CALL");

        vm.AssertNoDiagnostics();
        Assert.Equal(
            [
                "RAPID Z=2", "LINE Z=-4 F=100", "RAPID Z=2", "RAPID Z=-3", "LINE Z=-8 F=100", "RAPID Z=2",
                "RAPID Z=-7", "LINE Z=-10 F=100", "RAPID Z=2",
            ],
            Describe(vm.Vm.LastCycleMotions));
    }

    // D190: a known PECK above 0 splits the feed on any cycle of the family, DRILL among them.
    [Fact]
    public void CycleCall_DrillWithPeck_FeedsInPecks()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-6 PECK=4 CYCLE_F=100", "CYCLE_CALL");

        Assert.Equal(
            ["RAPID Z=2", "LINE Z=-4 F=100", "RAPID Z=2", "RAPID Z=-4", "LINE Z=-6 F=100", "RAPID Z=2"],
            Describe(vm.Vm.LastCycleMotions));
    }

    // VM 3.3, language 4.7, D190, D357: CHIP_BREAK feeds in pecks without leaving the hole; with no family and no key
    // the lift is 0 and no lift motion is raised.
    [Fact]
    public void CycleCall_ChipBreakCycle_FeedsInPecksWithoutLeavingTheHole()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=CHIP_BREAK CLEARANCE=2 DEPTH=-10 PECK=5 CYCLE_F=100", "CYCLE_CALL X=0");

        Assert.Equal(
            ["RAPID X=0", "RAPID Z=2", "LINE Z=-3 F=100", "LINE Z=-8 F=100", "LINE Z=-10 F=100", "RAPID Z=2"],
            Describe(vm.Vm.LastCycleMotions));
    }

    // D190, D357: CHIP_BREAK lifts by chip_break_lift after each step and feeds on.
    [Fact]
    public void CycleCall_ChipBreakWithLift_LiftsAndFeedsOn()
    {
        VmHarness vm = Expanding(Controller.Fanuc, new CyclesConfig { ChipBreakLift = 0.5m }).Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=CHIP_BREAK SURFACE=0 CLEARANCE=2 DEPTH=-10 PECK=4 CYCLE_F=100", "CYCLE_CALL");

        Assert.Equal(
            [
                "RAPID Z=2", "LINE Z=-4 F=100", "RAPID Z=-3.5", "LINE Z=-8 F=100", "RAPID Z=-7.5", "LINE Z=-10 F=100",
                "RAPID Z=2",
            ],
            Describe(vm.Vm.LastCycleMotions));
    }

    // D357: without the key, Siemens lifts by the manual's 1 mm of _VRT.
    [Fact]
    public void CycleCall_ChipBreakOnSiemensWithoutKey_LiftsOneMillimetre()
    {
        VmHarness vm = Expanding(Controller.Siemens, new CyclesConfig()).Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=CHIP_BREAK SURFACE=0 CLEARANCE=2 DEPTH=-6 PECK=4 CYCLE_F=100", "CYCLE_CALL");

        Assert.Equal(
            ["RAPID Z=2", "LINE Z=-4 F=100", "RAPID Z=-3", "LINE Z=-6 F=100", "RAPID Z=2"],
            Describe(vm.Vm.LastCycleMotions));
    }

    // D190, D357: a CHIP_BREAK lift and a PECK re-entry larger than the gap left end at CLEARANCE.
    [Fact]
    public void CycleCall_LiftAndReentryLargerThanTheGap_StopAtClearance()
    {
        VmHarness chip = Expanding(Controller.Fanuc, new CyclesConfig { ChipBreakLift = 5m }).Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=CHIP_BREAK SURFACE=0 CLEARANCE=2 DEPTH=-3 PECK=1 CYCLE_F=100", "CYCLE_CALL");
        VmHarness peck = Expanding(Controller.Fanuc, new CyclesConfig { PeckClearance = 10m }).Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=PECK SURFACE=0 CLEARANCE=2 DEPTH=-2 PECK=1 CYCLE_F=100", "CYCLE_CALL");

        Assert.Equal(
            [
                "RAPID Z=2", "LINE Z=-1 F=100", "RAPID Z=2", "LINE Z=-2 F=100", "RAPID Z=2", "LINE Z=-3 F=100",
                "RAPID Z=2",
            ],
            Describe(chip.Vm.LastCycleMotions));
        Assert.Equal(
            ["RAPID Z=2", "LINE Z=-1 F=100", "RAPID Z=2", "RAPID Z=2", "LINE Z=-2 F=100", "RAPID Z=2"],
            Describe(peck.Vm.LastCycleMotions));
    }

    // D357: on Siemens without peck_clearance the re-entry is the control's automatic _DIS1: 0.6 mm up to a drilling
    // depth of 30 mm, otherwise the depth / 50, at most 7 mm.
    [Theory]
    [InlineData("-20", "10", "-9.4")]
    [InlineData("-40", "20", "-19.2")]
    [InlineData("-400", "200", "-193")]
    public void CycleCall_PeckOnSiemensWithoutKey_ReentersAtTheAutomaticDistance(
        string depth, string peck, string reentry)
    {
        VmHarness vm = Expanding(Controller.Siemens, new CyclesConfig()).Execute(
            "RAPID X=0 Y=0 Z=10", $"CYCLE=PECK SURFACE=0 CLEARANCE=2 DEPTH={depth} PECK={peck} CYCLE_F=100",
            "CYCLE_CALL");

        Assert.Equal(
            [
                "RAPID Z=2", $"LINE Z=-{peck} F=100", "RAPID Z=2", $"RAPID Z={reentry}", $"LINE Z={depth} F=100",
                "RAPID Z=2",
            ],
            Describe(vm.Vm.LastCycleMotions));
    }

    // D357: the keys are millimetres whatever the units; under UNITS=INCH the lift is converted.
    [Fact]
    public void CycleCall_ChipBreakLiftUnderInch_IsConvertedFromMillimetres()
    {
        VmHarness vm = new VmHarness(Machine(Controller.Fanuc, new CyclesConfig { ChipBreakLift = 2.54m }),
            new VmOptions { ExpandCycles = true }).Execute("UNITS=INCH SPINDLE=CW RPM=1000");
        vm.Execute("RAPID X=0 Y=0 Z=1", "CYCLE=CHIP_BREAK SURFACE=0 CLEARANCE=0.1 DEPTH=-1 PECK=0.5 CYCLE_F=10",
            "CYCLE_CALL");

        Assert.Equal(
            ["RAPID Z=0.1", "LINE Z=-0.5 F=10", "RAPID Z=-0.4", "LINE Z=-1 F=10", "RAPID Z=0.1"],
            Describe(vm.Vm.LastCycleMotions));
    }

    // D357: on Heidenhain the re-entry is the cycle's Q200, the gap between SURFACE and CLEARANCE.
    [Fact]
    public void CycleCall_PeckOnHeidenhain_ReentersAtTheSafetyClearance()
    {
        VmHarness vm = Expanding(Controller.Heidenhain, new CyclesConfig()).Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=PECK SURFACE=0 CLEARANCE=2 DEPTH=-6 PECK=4 CYCLE_F=100", "CYCLE_CALL");

        Assert.Equal(
            ["RAPID Z=2", "LINE Z=-4 F=100", "RAPID Z=2", "RAPID Z=-2", "LINE Z=-6 F=100", "RAPID Z=2"],
            Describe(vm.Vm.LastCycleMotions));
    }

    // VM 3.3, language 4.7, D37: TAP backs out at CYCLE_F behind the spindle reversal; a call without axis words
    // executes at the current position.
    [Fact]
    public void CycleCall_TapWithoutAxisWords_FeedsOutAtTheCurrentPosition()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=5 Y=5 Z=10", "CYCLE=TAP CLEARANCE=2 DEPTH=-10 PITCH=1.5 CYCLE_F=150", "CYCLE_CALL");

        vm.AssertNoDiagnostics();
        Assert.Equal(["RAPID Z=2", "LINE Z=-10 F=150", "LINE Z=2 F=150"], Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(5m), vm.Position("X"));
    }

    // D190 change 2: under CYCLE_RETRACT=SAFE, TAP feeds out only to CLEARANCE and rapids the rest to SAFE, on every
    // family.
    [Theory]
    [InlineData(Controller.Fanuc)]
    [InlineData(Controller.Siemens)]
    public void CycleCall_TapUnderSafe_FeedsToClearanceAndRapidsToSafe(Controller controller)
    {
        VmHarness vm = Expanding(controller, new CyclesConfig { ReamFeedOut = ReamFeedOut.Safe }).Execute(
            "RAPID X=5 Y=5 Z=60", "CYCLE=TAP CLEARANCE=2 DEPTH=-10 SAFE=50 CYCLE_RETRACT=SAFE PITCH=1.5 CYCLE_F=150",
            "CYCLE_CALL");

        Assert.Equal(
            ["RAPID Z=2", "LINE Z=-10 F=150", "LINE Z=2 F=150", "RAPID Z=50"], Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(50m), vm.Position("Z"));
    }

    // D190 change 1: REAM feeds out at CYCLE_F; under CYCLE_RETRACT=CLEARANCE it stops at CLEARANCE.
    [Fact]
    public void CycleCall_ReamUnderClearance_FeedsOutToClearance()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=REAM CLEARANCE=2 DEPTH=-10 CYCLE_F=80", "CYCLE_CALL");

        Assert.Equal(["RAPID Z=2", "LINE Z=-10 F=80", "LINE Z=2 F=80"], Describe(vm.Vm.LastCycleMotions));
    }

    // D378: under CYCLE_RETRACT=SAFE, REAM feeds to the plane of ream_feed_out and rapids the rest; absent, the family
    // default applies, CLEARANCE on Fanuc and Heidenhain, SAFE on Siemens.
    [Theory]
    [InlineData(Controller.Fanuc, null, "LINE Z=2 F=80", "RAPID Z=50")]
    [InlineData(Controller.Heidenhain, null, "LINE Z=2 F=80", "RAPID Z=50")]
    [InlineData(Controller.Siemens, null, "LINE Z=50 F=80", null)]
    [InlineData(Controller.Fanuc, ReamFeedOut.Safe, "LINE Z=50 F=80", null)]
    [InlineData(Controller.Siemens, ReamFeedOut.Clearance, "LINE Z=2 F=80", "RAPID Z=50")]
    public void CycleCall_ReamUnderSafe_FeedsToTheReamFeedOutPlane(
        Controller controller, ReamFeedOut? feedOut, string feedOutMotion, string? rapid)
    {
        VmHarness vm = Expanding(controller, new CyclesConfig { ReamFeedOut = feedOut }).Execute(
            "RAPID X=0 Y=0 Z=60", "CYCLE=REAM CLEARANCE=2 DEPTH=-10 SAFE=50 CYCLE_RETRACT=SAFE CYCLE_F=80",
            "CYCLE_CALL");

        var expected = new List<string> { "RAPID Z=2", "LINE Z=-10 F=80", feedOutMotion };
        if (rapid is not null)
        {
            expected.Add(rapid);
        }

        Assert.Equal(expected, Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(50m), vm.Position("Z"));
    }

    // D190: BORE retracts at rapid, after the spindle stop, which moves nothing.
    [Fact]
    public void CycleCall_Bore_RetractsAtRapid()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=10", "CYCLE=BORE CLEARANCE=2 DEPTH=-10 CYCLE_F=80", "CYCLE_CALL");

        Assert.Equal(["RAPID Z=2", "LINE Z=-10 F=80", "RAPID Z=2"], Describe(vm.Vm.LastCycleMotions));
    }

    // D188: a CLEARANCE from an expression in STATIC mode counts toward the WARNING "unresolved expression" and is no
    // ERROR; the motions to it and the drilling axis afterwards are unknown.
    [Fact]
    public void CycleCall_ClearanceFromAnExpression_CountsAsUnresolvedAndLeavesTheAxisUnknown()
    {
        string text = VmHarness.File(
            "UNITS=MM SPINDLE=CW RPM=1000", "RAPID X=0 Y=0 Z=50", "CYCLE=DRILL CLEARANCE={$Q1} DEPTH=-10 CYCLE_F=100",
            "CYCLE_CALL X=10", "PROGRAM=END");

        Assert.Equal([DiagnosticCodes.UnresolvedExpressions], VmHarness.Run(text, VmMachines.Default()).Codes());

        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50", "CYCLE=DRILL CLEARANCE={$Q1} DEPTH=-10 CYCLE_F=100", "CYCLE_CALL X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(["RAPID X=10", "RAPID Z=?", "LINE Z=-10 F=100", "RAPID Z=?"], Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(AxisPosition.Unknown, vm.Position("Z"));
    }

    // VM 1, 3.3: a DEPTH from an expression is not evaluated in STATIC mode; the depth is unknown, the call is no
    // ERROR, and Z still ends at the known clearance plane.
    [Fact]
    public void CycleCall_DepthFromAnExpression_IsNoErrorAndEndsAtTheClearancePlane()
    {
        VmHarness vm = Expanding().Execute(
            "RAPID X=0 Y=0 Z=50", "CYCLE=DRILL CLEARANCE=5 DEPTH={$Q1} CYCLE_F=100", "CYCLE_CALL X=10");

        vm.AssertNoDiagnostics();
        Assert.Equal(["RAPID X=10", "RAPID Z=5", "LINE Z=? F=100", "RAPID Z=5"], Describe(vm.Vm.LastCycleMotions));
        Assert.Equal(Workpiece(5m), vm.Position("Z"));
    }

    // VM 3.9, 5, D99: inside a subprogram that no program calls the cycle rules are suppressed.
    [Fact]
    public void UncalledSub_CycleCallWithoutACycle_IsSuppressed()
    {
        string text = VmHarness.File("PROGRAM=END", "SUB=BEGIN NAME=10", "CYCLE_CALL X=1", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        vm.AssertNoDiagnostics();
        Assert.False(vm.Result!.Stopped);
    }

    // D114: with a machine file the names of a catalog cycle's parameters are checked against the machine's cycle
    // catalog; one the entry does not map is VM254, an ERROR, and the words of language 4.7 are no catalog parameters.
    [Fact]
    public void D114_CatalogCycleParameterTheEntryDoesNotMap_IsVm254()
    {
        VmHarness vm = Pocketing(VmMachines.MillTurn()).Execute(
            "CYCLE=RECT_POCKET LENGTH=60 WIDTH=40 DEPTH=-10 CORNER=5");

        Assert.Equal([DiagnosticCodes.CycleParameterUnknown], vm.Codes());
        Assert.Contains("CORNER", Assert.Single(vm.Messages(DiagnosticCodes.CycleParameterUnknown)),
            StringComparison.Ordinal);
    }

    // D114: parameters the entry maps are accepted.
    [Fact]
    public void D114_CatalogCycleParametersTheEntryMaps_AreAccepted()
    {
        VmHarness vm = Pocketing(VmMachines.MillTurn()).Execute("CYCLE=RECT_POCKET LENGTH=60 WIDTH={$Q1}");

        vm.AssertNoDiagnostics();
        Assert.Equal(["LENGTH", "WIDTH"], vm.State.Cycle.Parameters.Select(word => word.Key));
    }

    // D114, D103: without a machine file the parameters are not checked; the WARNING "not checked: no machine file"
    // comes once per name.
    [Fact]
    public void D114_CatalogCycleWithoutAMachineFile_IsTheD103WarningOncePerName()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "CYCLE=RECT_POCKET LENGTH=60 WIDTH=40",
            "CYCLE=RECT_POCKET LENGTH=70");

        Assert.Equal(
            [DiagnosticCodes.NotCheckedNoMachineFile, DiagnosticCodes.NotCheckedNoMachineFile], vm.Codes());
        Assert.Contains("LENGTH", vm.Messages(DiagnosticCodes.NotCheckedNoMachineFile)[0], StringComparison.Ordinal);
    }

    // A machine file whose cycle catalog maps RECT_POCKET with LENGTH and WIDTH (language 4.7.1, machine-config 6).
    private static VmHarness Pocketing(MachineConfig machine)
    {
        return new VmHarness(machine with
        {
            CycleCatalog = new CycleCatalog
            {
                Controller = machine.Machine.Controller,
                Entries =
                [
                    new CycleEntry
                    {
                        Name = "RECT_POCKET",
                        Native = "CYCLE_POCKET",
                        Params = new Dictionary<string, string>
                        {
                            ["LENGTH"] = "_LENG",
                            ["WIDTH"] = "_WID",
                            ["DEPTH"] = "_DP",
                        },
                    },
                ],
            },
        });
    }

    // The default machine of D103 with ExpandCycles and the units set.
    // A machine whose cycle catalog is the Fanuc one with the simple turning cycle TURN_OD on G90 and the one-shot
    // cycles G71, G72, G74 and G75, which all return to their start point (D187, D411).
    private static VmHarness Turning()
    {
        MachineConfig machine = VmMachines.Default() with
        {
            CycleCatalog = new CycleCatalog
            {
                Controller = Controller.Fanuc,
                Entries =
                [
                    new CycleEntry { Name = "TURN_OD", Native = "G90", Modal = true },
                    new CycleEntry { Name = "ROUGH_TURN", Native = "G71" },
                    new CycleEntry { Name = "ROUGH_FACE", Native = "G72" },
                    new CycleEntry { Name = "FACE_GROOVE", Native = "G74" },
                    new CycleEntry { Name = "GROOVE", Native = "G75" },
                ],
            },
        };
        return new VmHarness(machine, new VmOptions { ExpandCycles = true }).Execute("UNITS=MM SPINDLE=CW RPM=1000");
    }

    private static VmHarness Expanding()
    {
        return new VmHarness(VmMachines.Default(), new VmOptions { ExpandCycles = true })
            .Execute("UNITS=MM SPINDLE=CW RPM=1000");
    }

    // The default machine as a machine of one family with its [cycles] keys (D357, D378).
    private static VmHarness Expanding(Controller controller, CyclesConfig cycles)
    {
        return new VmHarness(Machine(controller, cycles), new VmOptions { ExpandCycles = true })
            .Execute("UNITS=MM SPINDLE=CW RPM=1000");
    }

    private static MachineConfig Machine(Controller controller, CyclesConfig cycles)
    {
        MachineConfig machine = VmMachines.Default();
        return machine with { Machine = machine.Machine with { Controller = controller }, Cycles = cycles };
    }

    private static AxisPosition Workpiece(decimal value)
    {
        return new AxisPosition(value, PositionFrame.Workpiece, Known: true);
    }

    // Each motion as an NCX-like line, the verb and the axes in name order with their stored values, ? for unknown.
    private static List<string> Describe(IReadOnlyList<CycleMotion> motions)
    {
        var lines = new List<string>();
        foreach (CycleMotion motion in motions)
        {
            var line = new StringBuilder(motion.Verb == Verb.Rapid ? "RAPID" : "LINE");
            var axes = new List<string>(motion.To.Keys);
            axes.Sort(StringComparer.Ordinal);
            foreach (string axis in axes)
            {
                AxisPosition position = motion.To[axis];
                string value = position.Known ? position.Value.ToString(CultureInfo.InvariantCulture) : "?";
                line.Append(' ').Append(axis).Append('=').Append(value);
            }

            if (motion.Feed is decimal feed)
            {
                line.Append(" F=").Append(feed.ToString(CultureInfo.InvariantCulture));
            }

            lines.Add(line.ToString());
        }

        return lines;
    }
}
