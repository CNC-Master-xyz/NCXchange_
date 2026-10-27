namespace Ncx.Compilers.Tests.Siemens;

using static SiemensCompile;

/// <summary>
/// D366 applies the family default to linear and angle addresses only: a value that is no address, a variable, an
/// ARG, a TOLERANCE or a dwell, keeps its digits as the program writes them (machine-config 2).
/// </summary>
public sealed class SiemensDigitsTests
{
    // D366: an R parameter keeps its digits.
    [Fact]
    public void Variable_KeepsItsDigits()
    {
        Assert.Equal(Lines("R1=0.12345"), MillTurnBody("VAR:R1=0.12345"));
    }

    // D366: TOLERANCE 0.0005 is CYCLE832(0.0005, ...), not rounded to the decimals of X.
    [Fact]
    public void Tolerance_KeepsItsDigits()
    {
        Assert.StartsWith("CYCLE832(0.0005,", MillTurnBody("TOLERANCE=0.0005"), StringComparison.Ordinal);
    }

    // D366: the seconds of G4 F keep their digits.
    [Fact]
    public void Dwell_KeepsItsDigits()
    {
        Assert.Equal(Lines("G1 X10 F100", "G4 F1.23456", "G1 X20"),
            MillBody("LINE X=10 F=100", "DWELL=1.23456", "LINE X=20"));
    }

    // D366: an ARG of a CALL keeps its digits.
    [Fact]
    public void Arg_KeepsItsDigits()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            CALL=POCK ARG:DEPTH=5.12345
            PROGRAM=END
            SUB=BEGIN NAME=POCK
            LINE Z={-$DEPTH} F=100
            SUB=END
            FILE=END
            """;

        Assert.Contains("POCK(5.12345)", TextOf(Run(program, Mill())), StringComparison.Ordinal);
    }
}
