using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The rounding allowance of virtual machine 2.1: ROUND_LIMIT in the active units, ROUND_LIMIT:ROTARY in degrees, OFF
/// at the start, state only, read by the compiler (language 4.1, D363).
/// </summary>
public sealed class RoundLimitTests
{
    // D363: no allowance at the start, so the compiler reports nothing for rounding.
    [Fact]
    public void StartValue_IsOff()
    {
        var vm = new VmHarness(VmMachines.Default());

        Assert.Equal(new RoundLimitState { Value = null, Rotary = null }, vm.State.Frame.RoundLimit);
    }

    // D363: ROUND_LIMIT and ROUND_LIMIT:ROTARY set the allowance; OFF clears both.
    [Fact]
    public void RoundLimit_SetsTheAllowance_OffClearsBoth()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("ROUND_LIMIT=0.001 ROUND_LIMIT:ROTARY=0.01");
        Assert.Equal(new RoundLimitState { Value = 0.001m, Rotary = 0.01m }, vm.State.Frame.RoundLimit);
        Assert.Equal(0.001m, vm.Vm.Snapshot().Frame.RoundLimit.Value);

        vm.Execute("ROUND_LIMIT=OFF");
        Assert.Equal(new RoundLimitState { Value = null, Rotary = null }, vm.State.Frame.RoundLimit);
        vm.AssertNoDiagnostics();
    }

    // VM 4: PROGRAM=END sets the allowance OFF, as it does the tolerance.
    [Fact]
    public void ProgramEnd_SetsTheAllowanceOff()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("ROUND_LIMIT=0.001", "PROGRAM=END");

        Assert.Null(vm.State.Frame.RoundLimit.Value);
    }
}
