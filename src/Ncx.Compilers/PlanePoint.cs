namespace Ncx.Compilers;

/// <summary>
/// A point of the working plane, its coordinates on the first and the second plane axis (virtual machine 3.2), as
/// <see cref="ArcOnTheGrid"/> computes the end the control reaches (D365).
/// </summary>
/// <param name="First">The coordinate on the first plane axis.</param>
/// <param name="Second">The coordinate on the second plane axis.</param>
internal sealed record PlanePoint(decimal First, decimal Second);
