using Ncx.Core.Machine;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The path-control mode of virtual machine 2.1: PATH_MODE=EXACT_STOP, CONTINUOUS or DEFAULT, whose start value is the
/// machine's [dynamics] path_mode, exact stop without one, and which PROGRAM=END puts back (language 4.1; D349, D350).
/// </summary>
public sealed class PathModeTests
{
    // D350: a [dynamics] without path_mode, and a machine without [dynamics], start at exact stop.
    [Fact]
    public void StartValue_WithoutPathModeInTheMachine_IsExactStop()
    {
        var vm = new VmHarness(VmMachines.Default());

        Assert.Equal(PathMode.ExactStop, vm.State.Frame.PathMode);
    }

    // D350: [dynamics] path_mode is the start value of the state.
    [Fact]
    public void StartValue_IsTheMachinesPathMode()
    {
        var vm = new VmHarness(Continuous());

        Assert.Equal(PathMode.Continuous, vm.State.Frame.PathMode);
    }

    // D349: PATH_MODE sets the mode; DEFAULT puts the machine's own mode back.
    [Fact]
    public void PathMode_SetsTheMode_DefaultPutsTheStartValueBack()
    {
        VmHarness vm = new VmHarness(Continuous()).Execute("PATH_MODE=EXACT_STOP");
        Assert.Equal(PathMode.ExactStop, vm.State.Frame.PathMode);

        vm.Execute("PATH_MODE=DEFAULT");
        Assert.Equal(PathMode.Continuous, vm.State.Frame.PathMode);

        vm.Execute("PATH_MODE=EXACT_STOP", "PATH_MODE=CONTINUOUS");
        Assert.Equal(PathMode.Continuous, vm.State.Frame.PathMode);
        vm.AssertNoDiagnostics();
    }

    // D349, VM 4: PROGRAM=END puts the start value back.
    [Fact]
    public void ProgramEnd_PutsTheStartValueBack()
    {
        VmHarness vm = new VmHarness(Continuous()).Execute("PATH_MODE=EXACT_STOP", "PROGRAM=END");

        Assert.Equal(PathMode.Continuous, vm.State.Frame.PathMode);
    }

    // VM 2.1: the snapshot carries the mode, so the runtime estimate reads the state (D350).
    [Fact]
    public void Snapshot_CarriesTheMode()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PATH_MODE=CONTINUOUS");

        Assert.Equal(PathMode.Continuous, vm.Vm.Snapshot().Frame.PathMode);
    }

    private static MachineConfig Continuous()
    {
        return VmMachines.Default() with { Dynamics = new DynamicsConfig { PathMode = PathMode.Continuous } };
    }
}
