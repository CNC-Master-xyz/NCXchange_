namespace Ncx.Readers.Fanuc;

/// <summary>
/// G16 polar coordinates: the first axis of the plane carries the radius and the second the angle, about the origin of
/// the workpiece; the reader converts them to Cartesian (controllers fanuc.md 4). G15 ends them.
/// </summary>
internal static class FanucPolarCoordinates
{
    /// <summary>
    /// Writes the Cartesian point of the radius and angle words of a motion block under G16; the other axis words stay
    /// for the motion.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="axes">The unread axis words of the block.</param>
    /// <param name="main">The main block with its verb.</param>
    /// <returns>False when the block is kept as RAW.</returns>
    public static bool Read(FanucBlock block, List<FanucAxisWord> axes, DraftBlock main)
    {
        FanucMotion.PlaneAxes(block, out string first, out string second, out _, out _, out _);
        FanucAxisWord? radius = null;
        FanucAxisWord? angle = null;
        foreach (FanucAxisWord axis in axes)
        {
            if (axis.Axis == first)
            {
                radius = axis;
            }
            else if (axis.Axis == second)
            {
                angle = axis;
            }
        }

        if (radius is null && angle is null)
        {
            return true;
        }

        // TODO: G16 under G91, where the current position is the centre and the words are incremental, is not converted
        // yet; such a block stays RAW.
        if (radius?.Incremental == true || angle?.Incremental == true
            || (radius is not null && radius.Word.Number is null) || (angle is not null && angle.Word.Number is null))
        {
            block.Draft.KeepAsRaw("G16 polar coordinates with incremental words or variables are not converted");
            return false;
        }

        decimal radiusValue = radius?.Word.Number ?? block.Fanuc.PolarRadius;
        decimal angleValue = angle?.Word.Number ?? block.Fanuc.PolarAngle;
        foreach (FanucAxisWord? axis in new[] { radius, angle })
        {
            if (axis is not null)
            {
                block.MarkRead(axis.Word);
            }
        }

        // The Cartesian point keeps 6 decimals, whatever the decimals of the radius and the angle (D403).
        double radians = (double)angleValue * Math.PI / 180.0;
        decimal firstValue = ComputedNumbers.Round((double)radiusValue * Math.Cos(radians));
        decimal secondValue = ComputedNumbers.Round((double)radiusValue * Math.Sin(radians));
        main.Add(first, FanucNumbers.Of(firstValue)).Add(second, FanucNumbers.Of(secondValue));
        block.State.SetPosition(first, firstValue);
        block.State.SetPosition(second, secondValue);
        block.Fanuc.PolarRadius = radiusValue;
        block.Fanuc.PolarAngle = angleValue;
        return true;
    }
}
