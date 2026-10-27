using Ncx.Core.Geometry;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The sense an ARC turns in, in the coordinates of its two plane axes taken in a given order (virtual machine 3.2):
/// from its CW or CCW where its plane is known, ARC=CCW turning from the first plane axis toward the second (XY: X to
/// Y, ZX: Z to X, YZ: Y to Z, POLAR: X to C, CYLINDER: the cylinder axis to C); else the sense that turns the radius of
/// its start by the sweep onto the radius of its end. The runtime estimate needs it for the closest approach of D290
/// and for the direction reversal of D286.
/// </summary>
internal static class ArcSense
{
    private const double FullTurn = 2 * Math.PI;

    // The six decimals of a coordinate of the virtual machine (D121), as an angle at the radius of the arc.
    private const double PositionTolerance = 1e-6;

    /// <summary>
    /// +1 when the arc turns from the first axis of the order toward the second, -1 when the other way, 0 when neither
    /// its direction nor its geometry tells (a half or whole turn of an arc whose plane is not known).
    /// </summary>
    /// <param name="motion">The ARC.</param>
    /// <param name="axes">The two plane axes in the order of the coordinates.</param>
    /// <param name="start">The start point, first and second coordinate.</param>
    /// <param name="end">The end point.</param>
    /// <param name="center">The centre.</param>
    /// <param name="sweep">The sweep in radians, positive.</param>
    public static int Of(MotionEvent motion, IReadOnlyList<string> axes, double[] start, double[] end,
        double[] center, double sweep)
    {
        if (Declared(motion, axes) is int declared)
        {
            return declared;
        }

        double radius = Math.Sqrt(((start[0] - center[0]) * (start[0] - center[0]))
            + ((start[1] - center[1]) * (start[1] - center[1])));
        double tolerance = radius > 0 ? PositionTolerance / radius : PositionTolerance;
        double startAngle = Math.Atan2(start[1] - center[1], start[0] - center[0]);
        double endAngle = Math.Atan2(end[1] - center[1], end[0] - center[0]);
        bool positive = Turned(startAngle + sweep, endAngle, tolerance);
        bool negative = Turned(startAngle - sweep, endAngle, tolerance);
        if (positive == negative)
        {
            return 0;
        }

        return positive ? 1 : -1;
    }

    /// <summary>
    /// An angle as a turn from 0 up to, not including, a full turn.
    /// </summary>
    public static double Normalized(double angle)
    {
        return ((angle % FullTurn) + FullTurn) % FullTurn;
    }

    // The sense the block's CW or CCW gives in the order of the axes; null where the plane is not one of the order.
    private static int? Declared(MotionEvent motion, IReadOnlyList<string> axes)
    {
        if (motion.Direction is not ArcDirection direction)
        {
            return null;
        }

        FrameSnapshot frame = motion.After.Frame;
        string[] plane = frame switch
        {
            { Polar: true } => ["X", "C"],
            { Cylinder: not null } => [frame.CylinderAxis, "C"],
            { Workplane: Workplane.ZX } => ["Z", "X"],
            { Workplane: Workplane.YZ } => ["Y", "Z"],
            _ => ["X", "Y"],
        };
        int counterClockwise = direction == ArcDirection.Counterclockwise ? 1 : -1;
        if (axes[0] == plane[0] && axes[1] == plane[1])
        {
            return counterClockwise;
        }

        return axes[0] == plane[1] && axes[1] == plane[0] ? -counterClockwise : null;
    }

    private static bool Turned(double angle, double target, double tolerance)
    {
        double difference = Normalized(angle - target);
        return difference < tolerance || FullTurn - difference < tolerance;
    }
}
