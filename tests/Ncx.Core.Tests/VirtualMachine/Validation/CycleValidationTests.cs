using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The cycle rules of virtual machine 5, one test per rule with the smallest input (VM 3.3; D59, D94).
/// </summary>
public sealed class CycleValidationTests
{
    // VM 3.3, 5: CYCLE_CALL without cycle is an ERROR.
    [Fact]
    public void CycleCall_WithoutACycle_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM SPINDLE=CW RPM=1000", "CYCLE_CALL");

        RuleAssert.Only(vm, DiagnosticCodes.CycleCallWithoutCycle);
    }

    // VM 3.3, 5: CYCLE_CALL of a built-in drilling cycle without DEPTH is an ERROR.
    [Fact]
    public void CycleCall_BuiltInDrillingCycleWithoutDepth_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE=CW RPM=1000", "CYCLE=DRILL CLEARANCE=2", "CYCLE_CALL");

        RuleAssert.Only(vm, DiagnosticCodes.CycleCallWithoutDepthOrClearance);
    }

    // VM 3.3, 5, D59: AXIS naming an axis that is not a linear axis of the machine is an ERROR.
    [Fact]
    public void Axis_NotALinearAxis_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE=CW RPM=1000", "CYCLE=DRILL AXIS=C CLEARANCE=2 DEPTH=-5", "CYCLE_CALL");

        RuleAssert.Only(vm, DiagnosticCodes.CycleAxisNotLinear);
    }

    // VM 3.3, 5, D189: a word on the drilling axis in the call block of the built-in family is an ERROR; the cycle
    // moves that axis itself, and a pre-positioning is a RAPID of its own.
    [Fact]
    public void CycleCall_WithAWordOnTheDrillingAxis_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "UNITS=MM SPINDLE=CW RPM=1000",
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_F=100",
            "CYCLE_CALL X=10 Y=10 Z=20");

        RuleAssert.Only(vm, DiagnosticCodes.CycleCallOnTheDrillingAxis);
    }

    // VM 3.3, D189: the plane words of the call block stay what they are, so a call without a drilling-axis word is
    // no ERROR.
    [Fact]
    public void CycleCall_WithPlaneWordsOnly_IsNoError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "UNITS=MM SPINDLE=CW RPM=1000",
            "RAPID X=0 Y=0 Z=50",
            "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_F=100",
            "CYCLE_CALL X=10 Y=10");

        vm.AssertNoDiagnostics();
    }
}
