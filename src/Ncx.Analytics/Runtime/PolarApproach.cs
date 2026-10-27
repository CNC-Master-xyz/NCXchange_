using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// L of D290: the closest approach of the tool centre to the centre of rotation along a block of the polar plane, the
/// face plane of X and of C as a length, whose centre is X=0 C=0 (virtual machine 3.1, D102). The C limit of the
/// runtime estimate converts at L.
/// </summary>
internal static class PolarApproach
{
    private const string First = "X";
    private const string Second = "C";

    /// <summary>
    /// L in the units of the program: for a LINE the perpendicular distance of the segment from the centre, or the
    /// nearer end when the foot lies outside it; for an ARC the distance of its circle from the centre where the arc
    /// passes the point nearest to it, or the nearer end; null when X or C is not known at both ends.
    /// </summary>
    public static double? Of(MotionEvent motion)
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
            return nearerEnd;
        }

        // The foot of the perpendicular from the centre, as a fraction of the segment from its start.
        double foot = -((start[0] * dx) + (start[1] * dy)) / squared;
        if (foot <= 0 || foot >= 1)
        {
            return nearerEnd;
        }

        return Length(start[0] + (foot * dx), start[1] + (foot * dy));
    }

    // The point of the arc's circle nearest the centre of rotation lies on the ray from the arc centre toward it; the
    // arc passes it when its angle from the start, in the arc's direction, is within the sweep. The direction is the
    // one that turns the start radius by the sweep onto the end radius; a sweep that a half or a whole turn makes
    // ambiguous gives null, and the nearer end is taken.
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
        int sense = ArcSense.Of(start, end, center, sweep);
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
