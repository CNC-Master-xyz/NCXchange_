namespace Ncx.Analytics.Runtime;

/// <summary>
/// The sense an ARC turns in, in the coordinates of its two plane axes taken in a given order: the one that turns the
/// radius of its start by the sweep onto the radius of its end (virtual machine 3.2). The runtime estimate needs it
/// for the closest approach of D290 and for the direction reversal of D286, and reads it from the geometry the MOTION
/// carries, so that the order of the axes does not matter.
/// </summary>
internal static class ArcSense
{
    private const double FullTurn = 2 * Math.PI;

    // A micro-radian: the arc ends where its sweep says, to the six decimals the virtual machine keeps (D121).
    private const double Tolerance = 1e-6;

    /// <summary>
    /// +1 when the arc turns from the first axis toward the second, -1 when the other way, 0 when its sweep is a half
    /// or a whole turn (or several), which both senses fit.
    /// </summary>
    /// <param name="start">The start point, first and second coordinate.</param>
    /// <param name="end">The end point.</param>
    /// <param name="center">The centre.</param>
    /// <param name="sweep">The sweep in radians, positive.</param>
    public static int Of(double[] start, double[] end, double[] center, double sweep)
    {
        double startAngle = Math.Atan2(start[1] - center[1], start[0] - center[0]);
        double endAngle = Math.Atan2(end[1] - center[1], end[0] - center[0]);
        bool positive = Turned(startAngle + sweep, endAngle);
        bool negative = Turned(startAngle - sweep, endAngle);
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

    private static bool Turned(double angle, double target)
    {
        double difference = Normalized(angle - target);
        return difference < Tolerance || FullTurn - difference < Tolerance;
    }
}
