namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The block numbers P and Q of the first and the last block of a contour written inline after a G70 to G73 (D278).
/// </summary>
/// <param name="First">The block number of the first block, P.</param>
/// <param name="Last">The block number of the last block, Q.</param>
internal sealed record FanucContourRange(int First, int Last);
