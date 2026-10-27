using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The seconds a spindle start, stop, speed change or reversal adds to the runtime estimate (D323; virtual machine 8,
/// machine-config 5): accel_time is the seconds from stop to rpm_max, and the spindle ramps linearly, an NCX estimate.
/// </summary>
internal static class SpindleRamp
{
    /// <summary>
    /// The seconds of one change of a spindle.
    /// </summary>
    /// <param name="before">The direction it turned in before the change; null while it stood.</param>
    /// <param name="after">The direction it turns in after the change; null when it stands.</param>
    /// <param name="oldRpm">The rpm before, where known; null under CSS or without an S.</param>
    /// <param name="newRpm">The rpm after, where known.</param>
    /// <param name="accelTime">accel_time of the spindle in seconds.</param>
    /// <param name="rpmMax">rpm_max of the spindle; null when the machine file gives none.</param>
    public static double Seconds(SpindleDirection? before, SpindleDirection? after, double? oldRpm, double? newRpm,
        double accelTime, double? rpmMax)
    {
        if (before is null && after is null)
        {
            return 0;
        }

        // D323: the linear ramp where rpm_max and the speeds it needs are known; else the whole accel_time per start
        // and per stop, a reversal twice, and nothing for a speed change, as before D323.
        if (before is null)
        {
            return Ramp(accelTime, rpmMax, newRpm) ?? accelTime;
        }

        if (after is null)
        {
            return Ramp(accelTime, rpmMax, oldRpm) ?? accelTime;
        }

        if (before != after)
        {
            return oldRpm is double from && newRpm is double to
                ? Ramp(accelTime, rpmMax, from + to) ?? 2 * accelTime
                : 2 * accelTime;
        }

        return oldRpm is double old && newRpm is double next ? Ramp(accelTime, rpmMax, Math.Abs(next - old)) ?? 0 : 0;
    }

    // accel_time x rpm / rpm_max; null when rpm_max or the rpm is unknown.
    private static double? Ramp(double accelTime, double? rpmMax, double? rpm)
    {
        return rpmMax is double cap && cap > 0 && rpm is double change ? accelTime * change / cap : null;
    }
}
