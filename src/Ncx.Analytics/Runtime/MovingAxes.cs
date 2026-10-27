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
    /// True for a motion of the polar plane through its centre, L = 0, where the C limit is not estimated and the X
    /// limits apply alone (D290).
    /// </summary>
    public bool ThroughCentre { get; init; }

    /// <summary>
    /// The moving axes of a motion: every axis whose position differs between From and To, and both plane axes of an
    /// ARC that turns about its center; in the polar plane X and C always, since the face motion turns C and moves X
    /// together (D290).
    /// </summary>
    public static MovingAxes Of(MotionEvent motion, MachineDynamics dynamics)
    {
        var limits = new AxisLimits();
        bool turnsRotary = false;
        bool throughCentre = false;
        double millimetresPerUnit = CommandedFeed.MillimetresPerUnit(motion);
        bool polar = false;
        foreach (KeyValuePair<string, AxisPosition> target in motion.To)
        {
            AxisPosition start = MotionPositions.StartOf(motion, target.Key);
            AxisDef? axis = dynamics.Axis(target.Key);
            bool inPlane = InPlane(start) && InPlane(target.Value);
            polar |= inPlane && target.Value.Frame == PositionFrame.Polar;
            if (start == target.Value && !TurnsAbout(motion, target.Key)
                && !(inPlane && target.Value.Frame == PositionFrame.Polar))
            {
                continue;
            }

            // The commanded feed is limited by the max_feed of every axis that moves, and the block accelerates with
            // the smallest acceleration of the moving axes (virtual machine 8). A rotary axis turns in degrees: the
            // runtime of rotary moves belongs to the kinematics module (virtual machine 9), so it sets no limit here,
            // except in the polar and the cylinder plane, where its word is a length like the others (virtual machine
            // 3.1, D102). D192 keeps the VM441 comparison of max_feed with the linear axes; this is the estimate.
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

            // D290: the deg/min and deg/s^2 of a rotary axis (machine-config 4) convert to the length of the plane,
            // 1 deg = pi r / 180 mm, under CYLINDER at its radius r and under POLAR at L, the closest approach of the
            // block to the centre of rotation; at L = 0 the C limit is not estimated and X limits alone. A RAPID takes
            // the rapid of the linear axes: G00 is not allowed under G12.1 or G07.1, so the rotary rapid never applies.
            double? radius = target.Value.Frame == PositionFrame.Cylinder
                ? (double?)motion.After.Frame.Cylinder * millimetresPerUnit
                : PolarApproach.Of(motion) * millimetresPerUnit;
            if (radius is not double length || length <= 0)
            {
                throughCentre = true;
                continue;
            }

            limits.Add(null, axis.MaxFeed, axis.Acceleration, length * Math.PI / 180);
        }

        // D290: in the polar plane the X limits apply as well, the X of the face being the machine X it moves.
        if (polar && !motion.To.ContainsKey(PolarLinearAxis) && dynamics.Axis(PolarLinearAxis) is AxisDef x)
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
        };
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
