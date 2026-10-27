using Ncx.Core.Writing;

namespace Ncx.Readers;

/// <summary>
/// A reader rule of a plugin: decides what a machine-specific M code or a run of source blocks means on this machine
/// and writes the NCX words for it, the workpiece transfer behind a builder M code that runs a hidden subprogram, or
/// the fold of M5, M51, M3 S1500 back into COOLANT:THROUGH=ON (architecture 9; virtual machine 7; D40, D66). It lives
/// with its caller, the readers, together with the types its method takes (D106). Every source block goes to the rules
/// first, in the order the plugins are loaded (D317); a block no rule claims is read by the configuration tables and
/// the words of the controller family (D231).
/// </summary>
public interface ISourceRule
{
    /// <summary>
    /// Offers one source block to the rule (D232). A rule that claims it may claim the blocks that follow it too, which
    /// it sees in block.Following; it writes the NCX blocks for the whole run with the builder, Begin(block.Line)...
    /// End(), before it returns the number of source blocks it claimed, the offered one among them. The reader then
    /// reads nothing more of the claimed blocks: it applies them to the source state and keeps their comments as
    /// comment-only lines after the rule's blocks (D5). The reader offers each block it reads, in the order it reads
    /// them, except the blocks of a run already claimed, and calls nothing after the last one, so a claim is complete
    /// when Read returns. The look-ahead ends in front of a block of the file structure or of the control flow (a
    /// jump, a return, a call), so that a run never takes control flow (gap closed 2026-10-28, delegated). A rule that
    /// claims blocks writes NCX for them: a claim with nothing written is a fault of the plugin (D5).
    /// </summary>
    /// <param name="block">The source block as the tokenizer read it, with the following blocks of its section.</param>
    /// <param name="state">The source-side state after the modal codes of the block, read only.</param>
    /// <param name="builder">The builder of the program being read.</param>
    /// <returns>The number of source blocks claimed, from the offered block on; 0 declines the block.</returns>
    int Read(SourceBlock block, SourceState state, NcxBuilder builder);
}
