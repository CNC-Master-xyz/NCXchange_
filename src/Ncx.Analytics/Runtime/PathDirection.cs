using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The direction of travel at the ends of a motion, over the axes of its path (the linear axes, and a rotary axis in
/// the polar or cylinder plane, where its word is a length: virtual machine 3.1, D102, D353), so that the runtime
/// estimate tells a direction reversal, one of the rest points of D286.
/// </summary>
internal static class PathDirection
{
    // The cosine below which two directions are opposite: within about a twelfth of a degree of a half turn.
    // Gap closed 2026-10-27 (delegated): D286 names "a direction reversal" without a tolerance.
    private const double Opposite = -0.999999;

    /// <summary>
    /// True when the motion after a corner leaves it in the direction opposite to the one the motion before it
    /// arrived in; false where it does not, and where a direction is not known (an axis unknown at an end, an arc of
    /// a half or whole turn whose sense its geometry leaves open).
    /// </summary>
    public static bool Reverses(MotionEvent before, MotionEvent after, MachineDynamics dynamics)
    {
        if (At(before, dynamics, atEnd: true) is not Dictionary<string, double> arriving
            || At(after, dynamics, atEnd: false) is not Dictionary<string, double> leaving)
        {
            return false;
        }

        double dot = 0;
        foreach (KeyValuePair<string, double> axis in arriving)
        {
            dot += leaving.TryGetValue(axis.Key, out double other) ? axis.Value * other : 0;
        }

        double norms = Norm(arriving) * Norm(leaving);
        return norms > 0 && dot / norms < Opposite;
    }

    // The direction at the start or the end of a motion by axis; null when it is not known.
    private static Dictionary<string, double>? At(MotionEvent motion, MachineDynamics dynamics, bool atEnd)
    {
        var direction = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, AxisPosition> target in motion.To)
        {
            AxisPosition start = MotionPositions.StartOf(motion, target.Key);
            if (start == target.Value)
            {
                continue;
            }

            bool length = dynamics.Axis(target.Key)?.Kind != AxisKind.Rotary
                || (MotionPositions.InTransformedPlane(start) && MotionPositions.InTransformedPlane(target.Value));
            if (!length)
            {
                continue;
            }

            if (!start.Known || !target.Value.Known || start.Frame != target.Value.Frame)
            {
                return null;
            }

            direction[target.Key] = (double)(target.Value.Value - start.Value);
        }

        return motion.Verb == Verb.Arc ? ArcDirection(motion, direction, atEnd) : direction;
    }

    // An ARC: the tangent of its circle in its plane at that end, as long as the arc in the plane, and the travel of
    // every axis it moves outside the plane, the helix (D353).
    private static Dictionary<string, double>? ArcDirection(MotionEvent motion, Dictionary<string, double> travel,
        bool atEnd)
    {
        if (motion.Center is not { Count: 2 } center || motion.Sweep is not decimal sweepDegrees)
        {
            return null;
        }

        var axes = new List<string>(center.Keys);
        double[]? centre = Point(center, axes);
        double[]? start = Point(StartPositions(motion, axes), axes);
        double[]? end = Point(motion.To, axes);
        if (centre is null || start is null || end is null)
        {
            return null;
        }

        double sweep = (double)sweepDegrees * Math.PI / 180;
        int sense = ArcSense.Of(start, end, centre, sweep);
        if (sense == 0)
        {
            return null;
        }

        double[] point = atEnd ? end : start;
        double radialFirst = point[0] - centre[0];
        double radialSecond = point[1] - centre[1];
        double radius = Math.Sqrt((radialFirst * radialFirst) + (radialSecond * radialSecond));
        if (radius == 0)
        {
            return null;
        }

        // Turning the radius a quarter turn in the arc's sense gives the tangent, as long as the radius; times the
        // sweep it is as long as the arc in the plane.
        travel[axes[0]] = -sense * radialSecond * sweep;
        travel[axes[1]] = sense * radialFirst * sweep;
        return travel;
    }

    private static Dictionary<string, AxisPosition> StartPositions(MotionEvent motion, List<string> axes)
    {
        var positions = new Dictionary<string, AxisPosition>(StringComparer.Ordinal);
        foreach (string axis in axes)
        {
            positions[axis] = MotionPositions.StartOf(motion, axis);
        }

        return positions;
    }

    private static double[]? Point(IReadOnlyDictionary<string, AxisPosition> positions, List<string> axes)
    {
        if (!positions.TryGetValue(axes[0], out AxisPosition first) || !first.Known
            || !positions.TryGetValue(axes[1], out AxisPosition second) || !second.Known)
        {
            return null;
        }

        return [(double)first.Value, (double)second.Value];
    }

    private static double Norm(Dictionary<string, double> direction)
    {
        double squares = 0;
        foreach (double component in direction.Values)
        {
            squares += component * component;
        }

        return Math.Sqrt(squares);
    }
}
