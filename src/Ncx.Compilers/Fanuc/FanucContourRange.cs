namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The block numbers P and Q of the first and the last block of a contour written inline after a G70 to G73, with the
/// modes the range runs under and the keys of the target state that are unknown after a cycle that runs it (D278).
/// </summary>
/// <param name="First">The block number of the first block, P.</param>
/// <param name="Last">The block number of the last block, Q.</param>
internal sealed record FanucContourRange(int First, int Last)
{
    /// <summary>
    /// The plane, the feed mode and G90/G91 of the range by group key, written in front of every cycle line that
    /// names it where the program differs (B-64484EN-1 p.90).
    /// </summary>
    public IReadOnlyDictionary<string, string> Modes { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The keys of the target state that the blocks of the range set, F and the modes among them, which are unknown
    /// after a cycle that runs the range (B-64484EN-1 p.76, p.281).
    /// </summary>
    public IReadOnlyList<string> Forgotten { get; init; } = [];
}
