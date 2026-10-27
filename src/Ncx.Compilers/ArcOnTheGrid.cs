using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

/// <summary>
/// The centre of an arc on the output grid (D364; machine-config 1, 4; language 4.3): the virtual machine keeps the
/// resolved centre at its own precision (six decimals, D121), the compiler writes the rounded end point and picks the
/// centre among the nine points of the output grid of the centre's written form one step around the rounded centre,
/// the one that makes the radius from the written start and the radius to the written end agree best, and among equals
/// the one nearest the exact centre. A written centre may so move inside its rounding step. The remaining difference
/// is the WARNING CMP030 where it exceeds [machine] arc_radius_tolerance, or one output step of the centre's address
/// without the key.
/// </summary>
internal static class ArcOnTheGrid
{
    // Millimetres per inch, for arc_radius_tolerance in an INCH program (machine-config 1).
    private const decimal MillimetresPerInch = 25.4m;

    // Two radius differences closer than this count as equal, and the nearer centre wins.
    private const double Equal = 1e-12;

    /// <summary>
    /// Picks the centre on the grid.
    /// </summary>
    /// <param name="first">The first plane axis.</param>
    /// <param name="second">The second plane axis.</param>
    public static GridCentre Choose(GridAxis first, GridAxis second)
    {
        decimal startFirst = OnGrid(first.Start, first.PointStep);
        decimal startSecond = OnGrid(second.Start, second.PointStep);
        decimal endFirst = OnGrid(first.End, first.PointStep);
        decimal endSecond = OnGrid(second.End, second.PointStep);
        decimal anchorFirst = first.Incremental ? startFirst : 0m;
        decimal anchorSecond = second.Incremental ? startSecond : 0m;
        decimal roundedFirst = anchorFirst + OnGrid(first.Centre - anchorFirst, first.CentreStep);
        decimal roundedSecond = anchorSecond + OnGrid(second.Centre - anchorSecond, second.CentreStep);

        decimal bestFirst = roundedFirst;
        decimal bestSecond = roundedSecond;
        double bestDifference = double.MaxValue;
        double bestDistance = double.MaxValue;
        for (int stepFirst = -1; stepFirst <= 1; stepFirst++)
        {
            for (int stepSecond = -1; stepSecond <= 1; stepSecond++)
            {
                decimal centreFirst = roundedFirst + (stepFirst * first.CentreStep);
                decimal centreSecond = roundedSecond + (stepSecond * second.CentreStep);
                double difference = Math.Abs(Distance(centreFirst - startFirst, centreSecond - startSecond)
                    - Distance(centreFirst - endFirst, centreSecond - endSecond));
                double distance = Distance(centreFirst - first.Centre, centreSecond - second.Centre);
                bool better = difference < bestDifference - Equal
                    || (Math.Abs(difference - bestDifference) <= Equal && distance < bestDistance);
                if (better)
                {
                    bestFirst = centreFirst;
                    bestSecond = centreSecond;
                    bestDifference = difference;
                    bestDistance = distance;
                }
            }
        }

        return new GridCentre(bestFirst, bestSecond, startFirst, startSecond, (decimal)bestDifference);
    }

    /// <summary>
    /// The difference of the two radii the compiler accepts: [machine] arc_radius_tolerance, a length in millimetres
    /// converted for an INCH program, or one output step of the centre's address without the key (D364).
    /// </summary>
    /// <param name="machine">The machine.</param>
    /// <param name="units">The units of the program where the arc stands.</param>
    /// <param name="centreStep">The output step of the centre's address.</param>
    public static decimal ToleranceOf(MachineConfig machine, Units units, decimal centreStep)
    {
        if (machine.Machine.ArcRadiusTolerance is decimal millimetres)
        {
            return units == Units.Inch ? millimetres / MillimetresPerInch : millimetres;
        }

        return centreStep;
    }

    /// <summary>
    /// The text of CMP030 for a centre whose radii still differ by more than the tolerance; null where they agree well
    /// enough.
    /// </summary>
    /// <param name="centre">The centre picked.</param>
    /// <param name="tolerance">The tolerance of <see cref="ToleranceOf"/>.</param>
    public static string? Report(GridCentre centre, decimal tolerance)
    {
        if (centre.RadiusDifference <= tolerance)
        {
            return null;
        }

        return "On the output grid the radius from the written start and the radius to the written end of the arc "
            + $"differ by {Text(Math.Round(centre.RadiusDifference, 6, MidpointRounding.AwayFromZero))} about the "
            + $"best centre the grid offers, more than {Text(tolerance)} ([machine] arc_radius_tolerance, or one "
            + "output step of the centre without it); the control may reject the arc or move its end (D364).";
    }

    /// <summary>
    /// D365: the end the control computes for a form that writes the sweep and not the end point, Heidenhain CP IPA and
    /// Siemens AR= with the centre: the written start turned about the written centre by the written sweep,
    /// counterclockwise for a positive sweep (from the first plane axis toward the second, virtual machine 3.2).
    /// </summary>
    /// <param name="start">The written start.</param>
    /// <param name="centre">The written centre.</param>
    /// <param name="sweep">The written sweep in degrees, negative for a clockwise arc.</param>
    public static PlanePoint SweptEnd(PlanePoint start, PlanePoint centre, decimal sweep)
    {
        double angle = (double)sweep * Math.PI / 180d;
        double first = (double)(start.First - centre.First);
        double second = (double)(start.Second - centre.Second);
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        return new PlanePoint(centre.First + (decimal)((first * cos) - (second * sin)),
            centre.Second + (decimal)((first * sin) + (second * cos)));
    }

    /// <summary>
    /// D365: how far the end the control reaches lies from the rounded end point of the block, where that is more than
    /// ROUND_LIMIT allows, or, without ROUND_LIMIT, more than half an output step on one of the plane axes, so that it
    /// would not be written as the same point; null where it lies within.
    /// </summary>
    /// <param name="reached">The end the control computes.</param>
    /// <param name="written">The rounded end point of the block.</param>
    /// <param name="step">The output step of the plane addresses.</param>
    /// <param name="roundLimit">ROUND_LIMIT of the program; null for OFF.</param>
    public static decimal? EndBeyond(PlanePoint reached, PlanePoint written, decimal step, decimal? roundLimit)
    {
        decimal first = Math.Abs(reached.First - written.First);
        decimal second = Math.Abs(reached.Second - written.Second);
        decimal distance = (decimal)Distance(first, second);
        bool beyond = roundLimit is decimal limit ? distance > limit : Math.Max(first, second) > step / 2m;
        return beyond ? distance : null;
    }

    /// <summary>
    /// A value of the virtual machine as the written file has it, rounded half away from zero to a multiple of the
    /// step.
    /// </summary>
    public static decimal Written(decimal value, decimal step)
    {
        return OnGrid(value, step);
    }

    /// <summary>
    /// The output step of a number of decimals, 0.001 for three.
    /// </summary>
    public static decimal StepOf(int decimals)
    {
        decimal step = 1m;
        for (int place = 0; place < decimals; place++)
        {
            step /= 10m;
        }

        return step;
    }

    // A value rounded half away from zero to a multiple of the step.
    private static decimal OnGrid(decimal value, decimal step)
    {
        return Math.Round(value / step, 0, MidpointRounding.AwayFromZero) * step;
    }

    private static double Distance(decimal first, decimal second)
    {
        double one = (double)first;
        double two = (double)second;
        return Math.Sqrt((one * one) + (two * two));
    }

    private static string Text(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
