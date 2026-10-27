using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Plugins;

/// <summary>
/// Hands the blocks and comment lines a reader rule wrote for a claimed source block to the builder of the program
/// being read, as if the rule had written them there (architecture 7; D92).
/// </summary>
internal static class ClaimedBlocks
{
    /// <summary>
    /// Copies what a rule wrote into a builder of its own into the builder of the program, in its order.
    /// </summary>
    /// <param name="claimed">What the rule's own builder built.</param>
    /// <param name="builder">The builder of the program being read.</param>
    public static void CopyInto(NcxProgram claimed, NcxBuilder builder)
    {
        // A comment line records the index of the block it stands before (NcxBuilder.Trivia, D142), and is copied
        // there: before the block of that index, the lines after the last block after it (D92).
        int nextTrivia = 0;
        for (int index = 0; index < claimed.Blocks.Count; index++)
        {
            nextTrivia = CopyTrivia(claimed, index, nextTrivia, builder);
            Block block = claimed.Blocks[index];
            builder.Begin(block.Line);
            foreach (Word word in block.Words)
            {
                builder.Word(word.Key, word.Addr, word.Value);
            }

            if (block.Comment is string comment)
            {
                builder.Comment(comment);
            }

            builder.End();
        }

        CopyTrivia(claimed, claimed.Blocks.Count, nextTrivia, builder);
    }

    // The comment lines that stand before the block of an index, from the first not copied yet; the index of the
    // next one.
    private static int CopyTrivia(NcxProgram claimed, int blockIndex, int nextTrivia, NcxBuilder builder)
    {
        while (nextTrivia < claimed.Trivia.Count && claimed.Trivia[nextTrivia].BlockIndex <= blockIndex)
        {
            builder.Trivia(claimed.Trivia[nextTrivia].Text);
            nextTrivia++;
        }

        return nextTrivia;
    }
}
