namespace Ncx.Analytics.Runtime;

/// <summary>
/// The smallest rapid, max_feed and acceleration of the axes a motion moves, found so far, in mm/min and mm/s^2
/// (virtual machine 8): the limits MovingAxes collects, a rotary axis converted to the length of its plane (D290).
/// </summary>
internal sealed class AxisLimits
{
    /// <summary>
    /// The smallest rapid in mm/min; null while no axis gave one.
    /// </summary>
    public double? Rapid { get; private set; }

    /// <summary>
    /// The smallest max_feed in mm/min; null while no axis gave one.
    /// </summary>
    public double? MaxFeed { get; private set; }

    /// <summary>
    /// The smallest acceleration in mm/s^2; null while no axis gave one.
    /// </summary>
    public double? Acceleration { get; private set; }

    /// <summary>
    /// The values of one more axis; an axis without a value sets no limit.
    /// </summary>
    /// <param name="rapid">Its rapid as the machine file gives it; null for none, or one that does not apply.</param>
    /// <param name="maxFeed">Its max_feed.</param>
    /// <param name="acceleration">Its acceleration.</param>
    /// <param name="factor">What turns the unit of the file into mm: 1 for a linear axis, pi r / 180 for the deg of a
    /// rotary axis at the radius r (D290).</param>
    public void Add(decimal? rapid, decimal? maxFeed, decimal? acceleration, double factor)
    {
        Rapid = Smaller(Rapid, rapid, factor);
        MaxFeed = Smaller(MaxFeed, maxFeed, factor);
        Acceleration = Smaller(Acceleration, acceleration, factor);
    }

    private static double? Smaller(double? found, decimal? value, double factor)
    {
        if (value is not decimal given)
        {
            return found;
        }

        double limit = (double)given * factor;
        return found is double smallest ? Math.Min(smallest, limit) : limit;
    }
}
