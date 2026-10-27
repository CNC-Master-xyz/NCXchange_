namespace Ncx.Compilers;

/// <summary>
/// The centre <see cref="ArcOnTheGrid"/> picked on the output grid (D364), in the units the virtual machine keeps.
/// </summary>
/// <param name="First">The centre on the first plane axis.</param>
/// <param name="Second">The centre on the second plane axis.</param>
/// <param name="WrittenStartFirst">The written start on the first plane axis, which an incremental centre counts
/// from.</param>
/// <param name="WrittenStartSecond">The written start on the second plane axis.</param>
/// <param name="RadiusDifference">How much the radius from the written start and the radius to the written end still
/// differ about it.</param>
internal sealed record GridCentre(decimal First, decimal Second, decimal WrittenStartFirst, decimal WrittenStartSecond,
    decimal RadiusDifference);
