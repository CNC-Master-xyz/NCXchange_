using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The C limit of a block of the polar plane (D290): the face plane of X and of C as a length, whose centre of
/// rotation is X=0 C=0 (virtual machine 3.1, D102). The angular rate a path speed v needs at a point at the distance r
/// from the centre is v p / r^2, p being the distance of the line from the centre; it is highest where r is smallest,
/// so the C limit converts at the length r_min^2 / p per radian.
/// </summary>
// Gap closed 2026-10-28 (delegated): D290's nearer-end rule refined to the exact bound v <= Cmax r^2 / p, with r the
// smallest distance of the segment from the centre; a purely radial line (p = 0) needs no C rate and is not clamped by
// C. An ARC keeps D290's L, the closest approach of the arc to the centre.
internal static class PolarApproach
{
    private const string First = "X";
    private const string Second = "C";

    /// <summary>
    /// The length, in the units of the program, that one radian of C moves the tool along the block where C turns
    /// fastest: r_min^2 / p for a LINE, L for an ARC. 0 for a block through the centre, where the C limit is not
    /// estimated; positive infinity for a radial line that does not pass the centre, which C does not limit; null when
    /// X or C is not known at both ends.
    /// </summary>
    public static double? LengthPerRadian(MotionEvent motion)
    {
        if (Point(MotionPositions.StartOf(motion, First), MotionPositions.StartOf(motion, Second)) is not double[] start
            || Point(At(motion.To, First), At(motion.To, Second)) is not double[] end)
        {
            return null;
        }

        double nearerEnd = Math.Min(Length(start[0], start[1]), Length(end[0], end[1]));
        if (motion.Verb == Verb.Arc && motion.Center is not null && motion.Sweep is decimal sweep)
        {
            return ArcApproach(motion, start, end, (double)sweep) ?? nearerEnd;
        }

        double dx = end[0] - start[0];
        double dy = end[1] - start[1];
        double squared = (dx * dx) + (dy * dy);
        if (squared == 0)
        {
            return double.PositiveInfinity;
        }

        // The foot of the perpendicular from the centre, as a fraction of the segment from its start; the nearest
        // point of the segment is the foot, or the nearer end when the foot lies outside it.
        double foot = -((start[0] * dx) + (start[1] * dy)) / squared;
        double nearest = foot <= 0 || foot >= 1
            ? nearerEnd
            : Length(start[0] + (foot * dx), start[1] + (foot * dy));
        if (nearest == 0)
        {
            return 0;
        }

        double perpendicular = Math.Abs((start[0] * dy) - (start[1] * dx)) / Math.Sqrt(squared);
        return perpendicular == 0 ? double.PositiveInfinity : nearest * nearest / perpendicular;
    }

    // The point of the arc's circle nearest the centre of rotation lies on the ray from the arc centre toward it; the
    // arc passes it when its angle from the start, in the arc's direction, is within the sweep; else the nearer end.
    private static double? ArcApproach(MotionEvent motion, double[] start, double[] end, double sweepDegrees)
    {
        if (Point(At(motion.Center!, First), At(motion.Center!, Second)) is not double[] center)
        {
            return null;
        }

        double radius = Length(start[0] - center[0], start[1] - center[1]);
        double distance = Length(center[0], center[1]);
        if (distance == 0)
        {
            return radius;
        }

        double sweep = sweepDegrees * Math.PI / 180;
        int sense = ArcSense.Of(motion, [First, Second], start, end, center, sweep);
        if (sense == 0)
        {
            return null;
        }

        double startAngle = Math.Atan2(start[1] - center[1], start[0] - center[0]);
        double nearest = Math.Atan2(-center[1], -center[0]);
        double along = ArcSense.Normalized(sense * (nearest - startAngle));
        return along <= sweep ? Math.Abs(distance - radius) : null;
    }

    private static AxisPosition At(IReadOnlyDictionary<string, AxisPosition> positions, string axis)
    {
        return positions.TryGetValue(axis, out AxisPosition position) ? position : AxisPosition.Unknown;
    }

    // A point of the polar plane, null unless both coordinates are known in the polar frame.
    private static double[]? Point(AxisPosition x, AxisPosition c)
    {
        return x.Known && c.Known && x.Frame == PositionFrame.Polar && c.Frame == PositionFrame.Polar
            ? [(double)x.Value, (double)c.Value]
            : null;
    }

    private static double Length(double x, double y)
    {
        return Math.Sqrt((x * x) + (y * y));
    }
}
