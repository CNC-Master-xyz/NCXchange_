using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The frame rules of virtual machine 5, one test per rule with the smallest input: SETPOS, the path tolerance and ROT
/// against the kinematics of the machine (language 4.1, 4.2; D82, D85, D101).
/// </summary>
public sealed class FrameValidationTests
{
    // VM 5, language 4.1, D85: TOLERANCE:ROTARY without an active TOLERANCE is an ERROR.
    [Fact]
    public void ToleranceRotary_WithoutTolerance_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOLERANCE:ROTARY=0.05");

        RuleAssert.Only(vm, DiagnosticCodes.ToleranceWordWithoutTolerance);
    }

    // VM 5, language 4.1, D85: TOLERANCE_MODE without an active TOLERANCE is an ERROR.
    [Fact]
    public void ToleranceMode_WithoutTolerance_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOLERANCE_MODE=ROUGH");

        RuleAssert.Only(vm, DiagnosticCodes.ToleranceWordWithoutTolerance);
    }

    // VM 3 step 3: the state words of a block do not depend on each other, so the TOLERANCE of the block makes it
    // active for TOLERANCE:ROTARY and TOLERANCE_MODE (language 6, the path tolerance example).
    [Fact]
    public void ToleranceWords_WithTheToleranceOfTheirBlock_AreAccepted()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=FINISH", "TOLERANCE_MODE=ROUGH");

        vm.AssertNoDiagnostics();
    }

    // VM 5, D85: after TOLERANCE=OFF the tolerance is no longer active.
    [Fact]
    public void ToleranceMode_AfterToleranceOff_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOLERANCE=0.02", "TOLERANCE=OFF",
            "TOLERANCE_MODE=ROUGH");

        RuleAssert.Only(vm, DiagnosticCodes.ToleranceWordWithoutTolerance);
    }

    // VM 5, language 4.2: SETPOS without an axis word is an ERROR.
    [Fact]
    public void Setpos_WithoutAnAxisWord_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("SETPOS");

        RuleAssert.Only(vm, DiagnosticCodes.SetposWithoutAxis);
    }

    // VM 3.4, 5, D101: SETPOS with an axis unknown in every frame is an ERROR.
    [Fact]
    public void Setpos_AxisUnknownInEveryFrame_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("SETPOS X=0");

        RuleAssert.Only(vm, DiagnosticCodes.SetposAxisUnknown);
    }

    // VM 5, language 4.2, D82: ROT on a target without table kinematics is a WARNING; the mill-turn has no rotary axis
    // on a table.
    [Fact]
    public void Rot_OnAMachineWithoutATable_Warns()
    {
        string text = VmHarness.File("TILT B=45 ROT=COORD", "PROGRAM=END");

        RuleAssert.Only(VmHarness.Run(text, VmMachines.MillTurn()), DiagnosticCodes.RotWithoutTableKinematics);
    }

    // Language 4.2, D82: on table kinematics ROT chooses how the table reaches the plane.
    [Fact]
    public void Rot_OnAMachineWithARotaryTable_IsAccepted()
    {
        string text = VmHarness.File("TILT B=45 ROT=COORD", "PROGRAM=END");

        VmHarness.Run(text, ValidationMachines.MillWithRotaryTable()).AssertNoDiagnostics();
    }

    // D77, D103: the built-in default machine is no target, so ROT is not checked without a machine file.
    [Fact]
    public void Rot_WithoutAMachineFile_IsNotChecked()
    {
        string text = VmHarness.File("TILT B=45 ROT=COORD", "PROGRAM=END");

        VmHarness.Run(text, VmMachines.Default()).AssertNoDiagnostics();
    }

    // Language 4.2, D351: a STOCK with its six coordinate words after the first ORIGIN and before the first motion is
    // accepted; the ORIGIN of its own block counts, since the state words come before the verb (VM 3).
    [Theory]
    [InlineData("ORIGIN=1", "STOCK=BOX X=0 Y=0 Z=-20 MAX:X=100 MAX:Y=100 MAX:Z=0")]
    [InlineData("UNITS=MM", "ORIGIN=1 STOCK=CYLINDER X=-40 Y=-40 Z=-50 MAX:X=40 MAX:Y=40 MAX:Z=0")]
    [InlineData("WORKPLANE=ZX ORIGIN=1", "STOCK=CYLINDER X=-40 Y=0 Z=-40 MAX:X=40 MAX:Y=120 MAX:Z=40")]
    public void Stock_InTheHeaderWithItsSixWords_IsAccepted(string first, string second)
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(first, second, "UNITS=MM", "RAPID X=0");

        vm.AssertNoDiagnostics();
    }

    // VM 5, D351: a STOCK without all six coordinate words of its form is VM423.
    [Fact]
    public void Stock_WithoutAllSixCoordinateWords_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("ORIGIN=1", "STOCK=BOX X=0 Y=0 Z=-20 MAX:X=100 MAX:Y=100");

        Diagnostic diagnostic = RuleAssert.Only(vm, DiagnosticCodes.StockWithoutItsCorners);
        Assert.Contains("MAX:Z", diagnostic.Message);
    }

    // VM 5, D351: a side of zero or negative extent, or a cylinder whose two sides across the tool axis of the
    // WORKPLANE differ, is VM424.
    [Theory]
    [InlineData("STOCK=BOX X=0 Y=0 Z=0 MAX:X=100 MAX:Y=100 MAX:Z=0")]
    [InlineData("STOCK=BOX X=100 Y=0 Z=-20 MAX:X=0 MAX:Y=100 MAX:Z=0")]
    [InlineData("STOCK=CYLINDER X=0 Y=0 Z=-20 MAX:X=80 MAX:Y=60 MAX:Z=0")]
    public void Stock_WithoutAValidExtent_IsAnError(string stock)
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("ORIGIN=1", stock);

        RuleAssert.Only(vm, DiagnosticCodes.StockExtentInvalid);
    }

    // VM 5, D351: a second STOCK, a STOCK before the first ORIGIN and a STOCK after the first motion verb are VM425.
    [Theory]
    [InlineData("ORIGIN=1", "STOCK=BOX X=0 Y=0 Z=-20 MAX:X=100 MAX:Y=100 MAX:Z=0")]
    [InlineData("UNITS=MM", "COMMENT=\"NO ORIGIN\"")]
    [InlineData("UNITS=MM ORIGIN=1", "RAPID X=0")]
    public void Stock_SecondBeforeTheOriginOrAfterAMotion_IsAnError(string first, string second)
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute(first, second, "STOCK=BOX X=0 Y=0 Z=-20 MAX:X=100 MAX:Y=100 MAX:Z=0");

        RuleAssert.Only(vm, DiagnosticCodes.StockMisplaced);
    }

    // VM 5, D351: STOCK stands once per program; a new PROGRAM=BEGIN starts the count again.
    [Fact]
    public void Stock_OncePerProgram_IsAccepted()
    {
        string text = "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"ONE\"\nORIGIN=1\n"
            + "STOCK=BOX X=0 Y=0 Z=-20 MAX:X=100 MAX:Y=100 MAX:Z=0\nPROGRAM=END\n"
            + "PROGRAM=BEGIN NAME=\"TWO\"\nORIGIN=1\nSTOCK=BOX X=0 Y=0 Z=-20 MAX:X=100 MAX:Y=100 MAX:Z=0\n"
            + "PROGRAM=END\nFILE=END\n";

        VmHarness.Run(text, VmMachines.Default()).AssertNoDiagnostics();
    }
}
