using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// [machine] arc_tolerance is a length in millimetres, "like rapid and max_feed, converted for an INCH program" (D139).
/// </summary>
public sealed class ArcToleranceUnitsTests
{
    // D139: 0.1 mm is 0.0039 in, so a radius difference of 0.005 in is beyond it in an INCH program.
    [Fact]
    public void Arc_MillimetreToleranceInAnInchProgram_IsConvertedToInches()
    {
        VmHarness vm = new VmHarness(VmMachines.Default(), new VmOptions { ArcTolerance = 0.1m })
            .Execute("UNITS=INCH F=10 SPINDLE=CW RPM=1000", "RAPID X=0 Y=10.005",
                "ARC=CW X=10 Y=0 CENTER:X=0 CENTER:Y=0");

        Assert.Equal([DiagnosticCodes.ArcInconsistentCenter], vm.Codes());
    }

    // D139: 0.254 mm is 0.01 in, which takes the same arc.
    [Fact]
    public void Arc_MillimetreToleranceInAnInchProgram_TakesWhatTheInchValueTakes()
    {
        VmHarness vm = new VmHarness(VmMachines.Default(), new VmOptions { ArcTolerance = 0.254m })
            .Execute("UNITS=INCH F=10 SPINDLE=CW RPM=1000", "RAPID X=0 Y=10.005",
                "ARC=CW X=10 Y=0 CENTER:X=0 CENTER:Y=0");

        vm.AssertNoDiagnostics();
    }
}
