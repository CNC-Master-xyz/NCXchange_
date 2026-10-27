using Ncx.Core.Machine;
using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// D292: the group 13 pair the compiler writes for UNITS against the basic system of the machine (base_units, MD10240)
/// and the G700/G710 option (extended_units, MD10260), and the feeds it converts where G70/G71 leave them in the basic
/// system (840D sl NC-Programmierung SW4.92 p.110-111, 115, 164-166).
/// </summary>
public sealed class SiemensUnitsTests
{
    private const string InchHeader = "FEED_MODE=PER_MIN COMP=OFF UNITS=INCH WORKPLANE=XY CYCLE=OFF";

    // D292: UNITS equal to the basic system is G71 or G70, the feed as written.
    [Fact]
    public void Units_EqualToTheBasicSystem_IsG71AndTheFeedAsWritten()
    {
        CompileResult result = Run(Program(MillHeader, "LINE X=10 F=500"), Mill());

        Assert.Empty(CompilerCodes(result));
        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G71", "G1 X10 F500", "M30"), TextOf(result));
    }

    // D292: UNITS other than the basic system on a machine with G700/G710 (extended_units) is G700, which puts the
    // feed in the programmed system too, so the feed stays as written.
    [Fact]
    public void Units_OtherThanTheBasicSystemWithExtendedUnits_IsG700AndTheFeedAsWritten()
    {
        CompileResult result = Run(Program(InchHeader, "LINE X=1 F=10"), Extended());

        Assert.Empty(CompilerCodes(result));
        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G700", "G1 X1 F10", "M30"), TextOf(result));
    }

    // D292: UNITS other than the basic system without G700/G710 is G70, under which the feed stays in the basic
    // system, mm/min: F10 inch/min is written F254, with the INFO CMP551 naming the conversion (SW4.92 p.165-166:
    // N40 G70 X2.75 with "F=500 mm/min" unchanged).
    [Fact]
    public void Units_OtherThanTheBasicSystemWithoutExtendedUnits_IsG70AndTheFeedInTheBasicSystem()
    {
        CompileResult result = Run(Program(InchHeader, "VAR:R1=5", "LINE X=1 F=10", "LINE X=2 F={$R1 * 2}"), Mill());

        Assert.Equal(["CMP551"], CompilerCodes(result));
        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G70", "R1=5", "G1 X1 F254", "G1 X2 F=(R1*2)*25.4", "M30"),
            TextOf(result));
    }

    // D292: on an inch machine (base_units = "inch") UNITS=MM without G710 is G71 with the feed converted into
    // inch/min, F254 mm/min written F10.
    [Fact]
    public void Units_MmOnAnInchMachineWithoutExtendedUnits_IsG71AndTheFeedInInches()
    {
        MachineConfig inch = Replaced(MillFile, "units_default = \"MM\"",
            "units_default = \"MM\"\nbase_units = \"inch\"");

        CompileResult result = Run(Program(MillHeader, "LINE X=10 F=254"), inch);

        Assert.Equal(["CMP551"], CompilerCodes(result));
        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G71", "G1 X10 F10", "M30"), TextOf(result));
    }

    // D292: a change back to the basic system within the program writes G71 and the feeds as written again.
    [Fact]
    public void Units_BackToTheBasicSystem_IsG71AndTheFeedAsWritten()
    {
        CompileResult result = Run(Program(InchHeader, "LINE X=1 F=10", "UNITS=MM", "LINE X=30 F=500"), Mill());

        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G70", "G1 X1 F254", "G71", "G1 X30 F500", "M30"),
            TextOf(result));
    }

    // D292: the feed is converted from its exact value, not from the text rounded to the decimals of F: the
    // F=0.393701 the reader writes for a source F10 under G70 comes back as F10.
    [Fact]
    public void Feed_ConvertedFromTheExactValue_RoundTripsTheSourceFeed()
    {
        CompileResult result = Run(Program(InchHeader, "LINE X=1 F=0.393701"), Mill());

        Assert.Equal(["CMP551"], CompilerCodes(result));
        Assert.Contains("G1 X1 F10", LinesOf(TextOf(result)));
    }

    // D292: a positional cycle feed is a feed too: under G70 on the metric mill FFR of CYCLE85, S_FB of CYCLE830 and
    // FFD of SLOT1 are written in mm/min, CYCLE_F=4 inch/min as 101.6 (SW4.92 p.115, p.164-166).
    [Theory]
    [InlineData("CYCLE=REAM SURFACE=0 CLEARANCE=0.1 DEPTH=-1 CYCLE_F=4", "MCALL CYCLE85(0.1,0,0.1,-1,,,101.6)")]
    [InlineData("CYCLE=DEEP_HOLE SURFACE=0 CLEARANCE=0.1 DEPTH=-1 SAFE=1 CYCLE_RETRACT=SAFE CYCLE_F=4",
        "CYCLE830(1,0,0.1,-1,,,,,,,,,,,,,,101.6,")]
    [InlineData("CYCLE=SLOT SURFACE=0 CLEARANCE=0.1 DEPTH=-1 SAFE=1 CYCLE_RETRACT=SAFE PECK=0.1 CYCLE_F=4",
        ",,,,101.6,,0.1,")]
    public void PositionalCycleFeed_UnderG70OnAMetricMill_IsInTheBasicSystem(string cycle, string expected)
    {
        CompileResult result = Run(Program(InchHeader, "RAPID X=0 Y=0 Z=1", cycle, "CYCLE_CALL X=1 Y=1",
            "CYCLE=OFF"), Mill());

        Assert.Contains(expected, TextOf(result), StringComparison.Ordinal);
        Assert.DoesNotContain(",4,", TextOf(result), StringComparison.Ordinal);
    }

    // The 840D sl mill with G700/G710 available (MD10260 = 1).
    private static MachineConfig Extended()
    {
        return Replaced(MillFile, "units_default = \"MM\"", "units_default = \"MM\"\nextended_units = true");
    }
}
