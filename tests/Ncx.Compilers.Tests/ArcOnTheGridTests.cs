using Ncx.Compilers.Tests.Fanuc;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// The centre of an arc on the output grid (D364): among the nine grid points one step around the rounded centre the
/// one whose radii from the written start and to the written end agree best, among equals the nearest; CMP030 above
/// the tolerance.
/// </summary>
public sealed class ArcOnTheGridTests
{
    private const decimal Step = 0.001m;

    // D364: rounding the centre 18.098696 3.111792 to 18.099 3.112 leaves the radii 0.000876 apart; 18.098 3.112 one
    // step beside it brings them within 0.00005.
    [Fact]
    public void Choose_NeighbourOfTheRoundedCentre_WhenItsRadiiAgreeBetter()
    {
        GridCentre centre = ArcOnTheGrid.Choose(
            new GridAxis(17.48289m, 13.195465m, 18.098696m, Step, Step, Incremental: false),
            new GridAxis(8.206899m, 1.59599m, 3.111792m, Step, Step, Incremental: false));

        Assert.Equal(18.098m, centre.First);
        Assert.Equal(3.112m, centre.Second);
        Assert.True(centre.RadiusDifference < 0.0001m);
        Assert.Null(ArcOnTheGrid.Report(centre, Step));
    }

    // D364: an incremental centre lies on the grid of the offset from the written start, here 0.0004 off the grid of
    // the absolute coordinates.
    [Fact]
    public void Choose_IncrementalCentre_LiesOnTheGridOfTheWrittenStart()
    {
        GridCentre centre = ArcOnTheGrid.Choose(
            new GridAxis(0.0004m, 10.0004m, 5.0004m, Step, Step, Incremental: true),
            new GridAxis(0m, 0m, 0m, Step, Step, Incremental: true));

        Assert.Equal(0m, centre.WrittenStartFirst);
        Assert.Equal(5m, centre.First - centre.WrittenStartFirst);
        Assert.Equal(0m, centre.RadiusDifference);
    }

    // D364: radii that no grid point brings together within the tolerance are the WARNING CMP030.
    [Fact]
    public void Report_DifferenceAboveTheTolerance_IsTheText()
    {
        GridCentre centre = ArcOnTheGrid.Choose(
            new GridAxis(0m, 10m, 5m, Step, 0.1m, Incremental: false),
            new GridAxis(0m, 0.04m, 3m, Step, 0.1m, Incremental: false));

        Assert.NotNull(ArcOnTheGrid.Report(centre, Step));
    }

    // D364: the centre of N310 of 2.5D_FRAESEN on the grid leaves its radii 0.000104 apart: below one output step
    // without the key, the WARNING CMP030 above an arc_radius_tolerance of 0.00001 mm.
    [Theory]
    [InlineData("", 0)]
    [InlineData("arc_radius_tolerance = 0.00001", 1)]
    public void Cmp030_RadiiAboveTheTolerance_IsAWarning(string key, int count)
    {
        string machine = FanucCompile.Mill().Replace("default_workpiece = \"TABLE1\"",
            "default_workpiece = \"TABLE1\"\n" + key, StringComparison.Ordinal);

        CompileResult result = FanucCompile.Run(FanucCompile.Program("RAPID X=50.534 Y=69.993",
            "ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50 F=100"), machine);

        List<Diagnostic> found = result.Diagnostics.Items
            .Where(item => item.Code == DiagnosticCodes.ArcRadiiDifferOnTheGrid).ToList();
        Assert.Equal(count, found.Count);
        Assert.All(found, item => Assert.Equal(Severity.Warning, item.Severity));
    }
}
