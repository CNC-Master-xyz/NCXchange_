namespace Ncx.Compilers;

/// <summary>
/// A section written inline in the block that names it, the contour of a Fanuc G70 to G73 (D278): its lines, written
/// from the target state the caller gave (CompilerBase.InlineSection), and the keys of the target state they changed,
/// which are unknown after them.
/// </summary>
public sealed class InlinedSection
{
    /// <summary>
    /// The lines of the blocks of the section, without its SUB=BEGIN and SUB=END, each without block number.
    /// </summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>
    /// The keys of the target state that the lines set.
    /// </summary>
    public required IReadOnlyList<string> WrittenKeys { get; init; }

    /// <summary>
    /// The blocks of the section between SUB=BEGIN and SUB=END, in walk order.
    /// </summary>
    public required IReadOnlyList<BlockStep> Steps { get; init; }
}
