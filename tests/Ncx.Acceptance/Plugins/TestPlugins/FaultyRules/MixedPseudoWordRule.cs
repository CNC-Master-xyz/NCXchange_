namespace FaultyRules;

/// <summary>
/// A program rewriter that inserts a block mixing the pseudo-word @SAVE with another word, @SAVE=SPINDLE:MAIN
/// SPINDLE:MAIN=OFF, before every block with a SECTION word; a pseudo-word stands in a block of its own (D167).
/// </summary>
public sealed class MixedPseudoWordRule : IProgramRewriter
{
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        if (!block.Has("SECTION"))
        {
            return RewriteResult.Unchanged;
        }

        return RewriteResult.Surround(before: ["@SAVE=SPINDLE:MAIN SPINDLE:MAIN=OFF"], after: [],
            reason: "a pseudo-word beside a word");
    }
}
