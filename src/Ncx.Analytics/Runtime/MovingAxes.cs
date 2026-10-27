using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The axes a motion moves and the limits they set (virtual machine 8): the smallest rapid, the smallest max_feed and
/// the smallest acceleration of the axes that move, in mm/min and mm/s^2, and whether a rotary axis turned. A rotary
/// axis counts only in the polar and the cylinder plane, its deg/min and deg/s^2 converted to the length of the plane
/// (D290).
/// </summary>
internal sealed record MovingAxes
{
    private const string PolarLinearAxis = "X";
    private const string PolarRotaryAxis = "C";

    /// <summary>
    /// The smallest rapid of the moving linear axes that give one, in mm/min; null when none does.
    /// </summary>
    public double? Rapid { get; init; }

    /// <summary>
    /// The smallest max_feed of the moving axes that give one, in mm/min; null when none does.
    /// </summary>
    public double? MaxFeed { get; init; }

    /// <summary>
    /// The smallest acceleration of the moving axes that give one, in mm/s^2; null when none does.
    /// </summary>
    public double? Acceleration { get; init; }

    /// <summary>
    /// True when a rotary axis turned in the motion outside the polar and the cylinder plane.
    /// </summary>
    public bool TurnsRotary { get; init; }

    /// <summary>
    /// True for a motion of the polar plane through its centre, where the C limit is not estimated and the X limits
    /// apply alone (D290).
    /// </summary>
    public bool ThroughCentre { get; init; }

    /// <summary>
    /// True for a motion of C in the cylinder plane while the radius of CYLINDER is unknown or 0, where the C limit is
    /// not estimated (D290).
    /// </summary>
    public bool CylinderRadiusUnknown { get; init; }

    /// <summary>
    /// The moving axes of a motion: every axis whose position differs between From and To, and both plane axes of an
    /// ARC that turns about its center; in the polar plane, where X or C moves, both X and C, since a face motion
    /// turns C and moves the machine X together (D290).
    /// </summary>
    public static MovingAxes Of(MotionEvent motion, MachineDynamics dynamics)
    {
        var limits = new AxisLimits();
        bool turnsRotary = false;
        bool throughCentre = false;
        bool cylinderRadiusUnknown = false;
        bool polarMoves = MovesInPolarPlane(motion);
        double millimetresPerUnit = CommandedFeed.MillimetresPerUnit(motion);
        foreach (KeyValuePair<string, AxisPosition> target in motion.To)
        {
            AxisPosition start = MotionPositions.StartOf(motion, target.Key);
            bool inPlane = InPlane(start) && InPlane(target.Value);
            bool polarAxis = polarMoves && target.Key is PolarLinearAxis or PolarRotaryAxis;
            if (start == target.Value && !TurnsAbout(motion, target.Key) && !polarAxis)
            {
                continue;
            }

            // The commanded feed is limited by the max_feed of every axis that moves, and the block accelerates with
            // the smallest acceleration of the moving axes (virtual machine 8). A rotary axis turns in degrees: the
            // runtime of rotary moves belongs to the kinematics module (virtual machine 9), so it sets no limit here,
            // except in the polar and the cylinder plane, where its word is a length like the others (virtual machine
            // 3.1, D102). D192 keeps the VM441 comparison of max_feed with the linear axes; this is the estimate.
            AxisDef? axis = dynamics.Axis(target.Key);
            if (axis?.Kind != AxisKind.Rotary)
            {
                limits.Add(axis?.Rapid, axis?.MaxFeed, axis?.Acceleration, 1);
                continue;
            }

            if (!inPlane)
            {
                turnsRotary = true;
                continue;
            }

            // D290: the deg/min and deg/s^2 of a rotary axis (machine-config 4) convert to the length of the plane.
            // Under CYLINDER=r at r, 1 deg = pi r / 180 mm, its rapid included, so that a RAPID of C alone has a rate.
            // Under POLAR at the length one radian of C moves the tool where C turns fastest (PolarApproach); a RAPID
            // takes the rapid of the linear axes there, G00 not being allowed under G12.1.
            if (target.Value.Frame == PositionFrame.Cylinder)
            {
                if (motion.After.Frame.Cylinder is not decimal r || r <= 0)
                {
                    cylinderRadiusUnknown = true;
                    continue;
                }

                double atRadius = (double)r * millimetresPerUnit * Math.PI / 180;
                limits.Add(axis.Rapid, axis.MaxFeed, axis.Acceleration, atRadius);
                continue;
            }

            switch (PolarApproach.LengthPerRadian(motion))
            {
                case 0:
                    throughCentre = true;
                    break;
                case double length when !double.IsPositiveInfinity(length):
                    limits.Add(null, axis.MaxFeed, axis.Acceleration, length * millimetresPerUnit * Math.PI / 180);
                    break;
            }
        }

        // D290: in the polar plane the X limits apply as well, the X of the face being the machine X it moves.
        if (polarMoves && !motion.To.ContainsKey(PolarLinearAxis) && dynamics.Axis(PolarLinearAxis) is AxisDef x)
        {
            limits.Add(x.Rapid, x.MaxFeed, x.Acceleration, 1);
        }

        return new MovingAxes
        {
            Rapid = limits.Rapid,
            MaxFeed = limits.MaxFeed,
            Acceleration = limits.Acceleration,
            TurnsRotary = turnsRotary,
            ThroughCentre = throughCentre,
            CylinderRadiusUnknown = cylinderRadiusUnknown,
        };
    }

    // True when the motion moves in the polar plane: X or C of the polar frame changes, or an ARC turns about them.
    private static bool MovesInPolarPlane(MotionEvent motion)
    {
        foreach (string axis in new[] { PolarLinearAxis, PolarRotaryAxis })
        {
            if (!motion.To.TryGetValue(axis, out AxisPosition end) || end.Frame != PositionFrame.Polar)
            {
                continue;
            }

            if (MotionPositions.StartOf(motion, axis) != end || TurnsAbout(motion, axis))
            {
                return true;
            }
        }

        return false;
    }

    // An ARC that resolved turns about its center on both axes of its plane, also where one of them ends on the
    // coordinate it started from: on both for a full circle, start = end, and on one for a half circle (virtual machine
    // 3.2). Its center names the two plane axes (virtual machine 7); the arc has moved when its sweep is not 0.
    private static bool TurnsAbout(MotionEvent motion, string axis)
    {
        return motion.Verb == Verb.Arc
            && motion.Sweep is decimal sweep && sweep != 0
            && motion.Center is not null && motion.Center.ContainsKey(axis);
    }

    private static bool InPlane(AxisPosition position)
    {
        return position.Frame is PositionFrame.Polar or PositionFrame.Cylinder;
    }
}
