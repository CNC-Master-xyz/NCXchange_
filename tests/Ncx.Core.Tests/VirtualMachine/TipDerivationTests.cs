using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Tests.VirtualMachine.Events;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// What the virtual machine derives from the measurement point (D374): the position store keeps the programmed point
/// (D369), the tip is that point less TIP_OFFSET along the tool axis of the active WORKPLANE, reported in MOTION and as
/// TIP:axis in the trace; POINT_RADIUS is reported beside COMP with the wear of the tool table; under TILT, TILT_AXIS
/// or TCPM=ON no tip is derived and VM484 warns once per program.
/// </summary>
public sealed class TipDerivationTests
{
    private const string PointTwo = "TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8";

    // D374: under WORKPLANE=XY the tool axis is Z, and the tip lies a below the programmed point; the position store
    // keeps the programmed Z (D369).
    [Fact]
    public void Motion_WorkplaneXY_TipIsTheProgrammedPointLessTheTipOffsetInZ()
    {
        var harness = new VmHarness(VmMachines.Default());
        var listener = new FakeListener();
        harness.Vm.Subscribe(listener);

        harness.Execute("UNITS=MM", PointTwo, "RAPID X=10 Y=20 Z=50");

        MotionEvent motion = Assert.Single(listener.Of<MotionEvent>());
        Assert.NotNull(motion.Tip);
        Assert.Equal(48m, motion.Tip["Z"].Value);
        Assert.Equal(10m, motion.Tip["X"].Value);
        Assert.Equal(20m, motion.Tip["Y"].Value);
        Assert.Equal(50m, harness.State.Motion.Position["Z"].Value);
        Assert.Equal(2, motion.ToolPoint);
        Assert.Equal(8m, motion.PointRadius);
    }

    // D374: under WORKPLANE=ZX the tool axis is Y.
    [Fact]
    public void Motion_WorkplaneZX_TipIsShiftedInY()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM WORKPLANE=ZX", PointTwo, "RAPID X=10 Y=20 Z=50");

        MotionEvent motion = Assert.Single(listener.Of<MotionEvent>());
        Assert.Equal(18m, motion.Tip?["Y"].Value);
        Assert.Equal(50m, motion.Tip?["Z"].Value);
    }

    // D374: point 1 at the tip has the tip at the programmed point.
    [Fact]
    public void Motion_PointAtTheTip_TipIsTheProgrammedPoint()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "TOOL=5", "RAPID Z=50");

        MotionEvent motion = Assert.Single(listener.Of<MotionEvent>());
        Assert.Equal(50m, motion.Tip?["Z"].Value);
        Assert.Equal(1, motion.ToolPoint);
        Assert.Equal(0m, motion.PointRadius);
    }

    // D374: under TCPM=ON the tool axis is no machine axis: no tip, and VM484 once per program.
    [Fact]
    public void Motion_UnderTcpm_DerivesNoTipAndWarnsOnce()
    {
        var harness = new VmHarness(VmMachines.Default());
        var listener = new FakeListener();
        harness.Vm.Subscribe(listener);

        harness.Execute("UNITS=MM F=100", PointTwo, "TCPM=ON", "LINE Z=50", "LINE Z=40");

        Assert.All(listener.Of<MotionEvent>(), static motion => Assert.Null(motion.Tip));
        Assert.Single(harness.Codes(), static code => code == DiagnosticCodes.TipOffsetUnderTilt);
    }

    // D374: under a TILT the same.
    [Fact]
    public void Motion_UnderTilt_WarnsVm484()
    {
        VmHarness harness = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM", PointTwo, "TILT B=45", "RAPID Z=50");

        Assert.Contains(DiagnosticCodes.TipOffsetUnderTilt, harness.Codes());
    }

    // D374: with the tip at the programmed point there is nothing to derive, so a tilt warns nothing.
    [Fact]
    public void Motion_UnderTiltWithoutTipOffset_DoesNotWarn()
    {
        VmHarness harness = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM", "TOOL=5", "TILT B=45", "RAPID Z=50");

        Assert.DoesNotContain(DiagnosticCodes.TipOffsetUnderTilt, harness.Codes());
    }

    // D374, D376: the wear of the tool table is added to the radius the VM reports, and to nothing else.
    [Fact]
    public void Motion_WearOfTheToolTable_IsAddedToTheReportedRadius()
    {
        var wear = new ToolPointData { K = 2, TipOffset = 2m, Radius = 8m, Wear = 0.02m };
        VmOptions options = VmOptions.ForMachine(VmMachines.Default()) with
        {
            MeasuredPoint = (tool, point) => tool == new ToolRef(5) && point == 2 ? wear : null,
        };

        FakeListener listener = EventRuns.Execute(VmMachines.Default(), options, "UNITS=MM", PointTwo, "RAPID Z=50");

        MotionEvent motion = Assert.Single(listener.Of<MotionEvent>());
        Assert.Equal(8.02m, motion.PointRadius);
        Assert.Equal(48m, motion.Tip?["Z"].Value);
    }

    // D374: the trace names the tip of the tool axis as TIP:Z where it differs from the programmed point.
    [Fact]
    public void StateChange_TipOffset_TraceShowsTheTip()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", PointTwo, "RAPID Z=50");

        Assert.Contains("STATE_CHANGE(3): TIP:Z ? -> 48", listener.LinesOf("STATE_CHANGE"));
    }

    // D374: without a tip offset the trace has no TIP row, so every trace written before the words stays as it was.
    [Fact]
    public void StateChange_WithoutTipOffset_TraceHasNoTipRow()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", "TOOL=5", "RAPID Z=50");

        Assert.DoesNotContain(listener.LinesOf("STATE_CHANGE"), static line => line.Contains("TIP:"));
    }

    // D374: the payload text names the point and the reported radius beside COMP.
    [Fact]
    public void Motion_PayloadText_NamesThePointAndTheRadiusBesideComp()
    {
        FakeListener listener = EventRuns.Execute("UNITS=MM", PointTwo, "RAPID Z=50");

        string line = Assert.Single(listener.LinesOf("MOTION"));
        Assert.Contains("comp OFF, point 2 radius 8, tip Z=48", line, StringComparison.Ordinal);
    }
}
