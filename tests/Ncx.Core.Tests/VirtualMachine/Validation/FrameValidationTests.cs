using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The frame rules of virtual machine 5, one test per rule with the smallest input: SETPOS, the path tolerance and the
/// rounding allowance, ROT against the machine and with TILT_AXIS, and STOCK (language 4.1, 4.2; D82, D85, D101,
/// D191, D351, D363).
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

    // VM 5, D191: ROT on a machine file without any rotary axis is a WARNING, since no free rotary axis can arise.
    [Fact]
    public void Rot_OnAMachineWithoutARotaryAxis_Warns()
    {
        string text = VmHarness.File("TILT B=45 ROT=COORD", "PROGRAM=END");

        RuleAssert.Only(VmHarness.Run(text, ValidationMachines.ThreeAxisMill()),
            DiagnosticCodes.RotWithoutTableKinematics);
    }

    // D191: whether ROT takes effect does not depend on the owner of the rotary axis, table, work spindle or head, so
    // the mill-turn with its C axes on the work spindles and the table mill are both accepted.
    [Fact]
    public void Rot_OnAMachineWithARotaryAxisOfAnyOwner_IsAccepted()
    {
        string text = VmHarness.File("TILT B=45 ROT=COORD", "PROGRAM=END");

        VmHarness.Run(text, VmMachines.MillTurn()).AssertNoDiagnostics();
        VmHarness.Run(text, ValidationMachines.MillWithRotaryTable()).AssertNoDiagnostics();
    }

    // VM 5, D191: ROT written with TILT_AXIS is a WARNING on any target, with or without a machine file, because the
    // control ignores the transformation type there (PLANE AXIAL).
    [Fact]
    public void Rot_WithTiltAxis_Warns()
    {
        string text = VmHarness.File("TILT_AXIS B=45 ROT=TABLE", "PROGRAM=END");

        RuleAssert.Only(VmHarness.Run(text, ValidationMachines.MillWithRotaryTable()), DiagnosticCodes.RotWithTiltAxis);
        RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()), DiagnosticCodes.RotWithTiltAxis);
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

    // VM 5, D351: VM423 names the missing corner words X Y Z first, then the MAX words.
    [Fact]
    public void Stock_WithoutCornerWords_NamesTheCornerFirstThenTheMaxWords()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("ORIGIN=1", "STOCK=BOX X=0 Z=-20 MAX:X=100 MAX:Z=0");

        Diagnostic diagnostic = RuleAssert.Only(vm, DiagnosticCodes.StockWithoutItsCorners);
        Assert.Contains("lacks Y, MAX:Y;", diagnostic.Message);
    }

    // Language 4.2, D351: the X Y Z of STOCK are the corners of the blank, not machine axes, so a machine without a Y
    // axis checks a STOCK clean.
    [Fact(Skip = "needs W1-C's STOCK skip in ResolveAxisWords")]
    public void Stock_OnAMachineWithoutAYAxis_ChecksClean()
    {
        string text = VmHarness.File("ORIGIN=1", "STOCK=BOX X=0 Y=0 Z=-20 MAX:X=100 MAX:Y=100 MAX:Z=0",
            "PROGRAM=END");

        VmHarness.Run(text, ValidationMachines.MillWithoutY()).AssertNoDiagnostics();
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

    // VM 5, language 4.1, D363: ROUND_LIMIT:ROTARY without an active ROUND_LIMIT is the same ERROR as
    // TOLERANCE:ROTARY without TOLERANCE.
    [Fact]
    public void RoundLimitRotary_WithoutRoundLimit_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("ROUND_LIMIT:ROTARY=0.01");

        Diagnostic diagnostic = RuleAssert.Only(vm, DiagnosticCodes.ToleranceWordWithoutTolerance);
        Assert.Contains("ROUND_LIMIT", diagnostic.Message);
    }

    // VM 3 step 3, D363: the ROUND_LIMIT of the block makes it active; after ROUND_LIMIT=OFF it is not.
    [Fact]
    public void RoundLimitRotary_WithTheRoundLimitOfItsBlock_IsAccepted_AfterOffIsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("ROUND_LIMIT=0.001 ROUND_LIMIT:ROTARY=0.01", "ROUND_LIMIT:ROTARY=0.02");
        vm.AssertNoDiagnostics();

        vm.Execute("ROUND_LIMIT=OFF", "ROUND_LIMIT:ROTARY=0.02");
        RuleAssert.Only(vm, DiagnosticCodes.ToleranceWordWithoutTolerance);
    }
}
