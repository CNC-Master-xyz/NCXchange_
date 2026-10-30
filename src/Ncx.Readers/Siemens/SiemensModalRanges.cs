namespace Ncx.Readers.Siemens;

/// <summary>
/// The modal ranges of one unit that the reader keeps RAW (D259, D422), found once per unit by one walk over its
/// blocks: the MCALL blocks that arm a call kept RAW, those among them whose range holds a pattern the reader cannot
/// expand, the blocks inside such a range and the bare MCALL that ends it.
/// </summary>
internal sealed class SiemensModalRanges
{
    /// <summary>
    /// The MCALL blocks whose range holds a pattern the reader cannot expand (D259).
    /// </summary>
    public HashSet<SourceBlock> PatternArming { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The blocks after an MCALL kept RAW and before the next MCALL, or the end of the unit (D422).
    /// </summary>
    public HashSet<SourceBlock> Inside { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The bare MCALL blocks that end a range kept RAW (D259, D422).
    /// </summary>
    public HashSet<SourceBlock> Ending { get; } = new(ReferenceEqualityComparer.Instance);
}
