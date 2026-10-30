using System.Globalization;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// A feed or a speed that a Q parameter sets. A Q parameter may stand wherever a number stands (controllers
/// heidenhain.md 6), so F and S take it in place of their number, FQ1 and SQ2. NCX reads the parameter where its word
/// stands, before the words of its block act (virtual machine 3.6), and the control reads it where Klartext writes the
/// F or the S, which for the F of a block without a motion is the next motion, and for the RPM of the block after a
/// TOOL the TOOL CALL (heidenhain 8 rules 2 and 3); the virtual machine keeps the value UNKNOWN in STATIC mode (virtual
/// machine 1). The parameter is written only where it has the value NCX read at the word, and the control keeps the
/// value it read until the next F or S, so what the control has active is the parameter with the step of the word it
/// was read for, Q1@12: the value NCX read at that word. A formula or a parameter with a sign is spilled into a QL
/// parameter in a block of its own directly before the block, which then writes FQL499 or SQL499 (D338, D396); the
/// target state keeps the formula with the step, =Q1 * 2@12.
/// </summary>
internal static class HeidenhainParameters
{
    // Between the parameter and the step of the word it was read for in what the target state keeps: Q1@12.
    private const string Place = "@";

    // Before the Klartext formula of a feed or a speed in what the target state keeps, =Q1 * 2@12 (D396).
    private const string FormulaMark = "=";

    /// <summary>
    /// The Q parameter a feed or a speed takes in place of its number, Q1 of {$Q1}; null for any other value, a number,
    /// a formula or a parameter with a sign.
    /// </summary>
    public static string? Of(Value value)
    {
        return value is ExprValue { Tree: ExprNode tree } && HeidenhainFormula.SignedParameter(tree) is string signed
            && signed.StartsWith('+')
            ? signed.Substring(1)
            : null;
    }

    /// <summary>
    /// The step whose word set a variable that the virtual machine keeps UNKNOWN at a step: that step or the last one
    /// before it in walk order with such a word, while the variable stays unknown, in the program of the step; null
    /// where none sets it with a word, or a @RESTORE brought an earlier value back (virtual machine 3.10).
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="index">The step at which the variable is unknown.</param>
    /// <param name="stateKey">The state key of the variable: F, RPM:S1.</param>
    /// <param name="wordOf">The word of a block that sets the variable; null for a block without one.</param>
    public static int? SourceOf(HeidenhainBlock writing, int index, string stateKey, Func<Block, Word?> wordOf)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int at = index; at >= 0 && steps[at].After.Unknown.Contains(stateKey); at--)
        {
            Block block = steps[at].Block;
            if (wordOf(block) is not null)
            {
                return at;
            }

            if (block.Has("@RESTORE") || block.Has("PROGRAM", null, "BEGIN"))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// What the control has active once an F or S read the parameter where it had the value NCX read at the word of a
    /// step: Q1@12, the parameter with the step of that word.
    /// </summary>
    /// <param name="parameter">The parameter: Q1.</param>
    /// <param name="source">The step of the word that set the feed or the speed with the parameter.</param>
    public static string Active(string parameter, int source)
    {
        return parameter + Place + source.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The value of an F or RPM word as what the target state keeps names it with the step of its word: the Q parameter
    /// of {$Q1}, Q1, or the Klartext formula of any other expression after a mark, =Q1 * 2, which F and S write as the
    /// QL parameter it is spilled into (D396); null where Klartext has no formula for it.
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="word">The F or RPM word.</param>
    /// <param name="problem">Why Klartext has no formula for it; null when it has.</param>
    public static string? KeyOf(HeidenhainBlock writing, Word word, out string? problem)
    {
        problem = null;
        if (Of(word.Value) is string parameter)
        {
            return parameter;
        }

        problem = "the value has no formula of numbers";
        return word.Value is ExprValue { Tree: ExprNode tree }
            && HeidenhainFormula.KeyOf(writing, tree, out problem) is string formula
            ? FormulaMark + formula
            : null;
    }

    /// <summary>
    /// The value of an F or RPM word as KeyOf names it; null where Klartext has no formula for it, which is reported
    /// (CMP102).
    /// </summary>
    public static string? KeyOf(HeidenhainBlock writing, Word word)
    {
        string? key = KeyOf(writing, word, out string? problem);
        if (key is null)
        {
            ReportValue(writing, word, problem);
        }

        return key;
    }

    /// <summary>
    /// Tells whether a value as KeyOf names it is a formula, which F and S write as the QL parameter it is spilled
    /// into.
    /// </summary>
    public static bool IsFormula(string key)
    {
        return key.StartsWith(FormulaMark, StringComparison.Ordinal);
    }

    /// <summary>
    /// What F or S writes for the value of a word after the letter: the Q parameter, Q1, or the QL parameter a formula
    /// or a parameter with a sign is spilled into, set in a block of its own written now directly before the block,
    /// QL499 = Q1 * 2 then FQL499 (D338, D396); null where no QL is free, which is reported (CMP102).
    /// </summary>
    public static string? Written(HeidenhainBlock writing, Word word)
    {
        if (Of(word.Value) is string parameter)
        {
            return parameter;
        }

        string? register = word.Value is ExprValue { Tree: ExprNode tree }
            ? HeidenhainNumbers.Spilled(writing, tree)
            : null;
        if (register is null)
        {
            ReportValue(writing, word, "no QL parameter is free to hold its formula (D338, D396)");
            return null;
        }

        return register.TrimStart('+');
    }

    /// <summary>
    /// The variables the value of a word reads: Q1 and Q2 of {$Q1 * $Q2}, SYS_POS_X of {$SYS_POS_X}.
    /// </summary>
    public static IReadOnlySet<string> ReadsOf(Word word)
    {
        return word.Value is ExprValue { Tree: ExprNode tree }
            ? HeidenhainReads.NamesOf(tree)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Tells whether the control has active the feed or speed NCX has from the word of a step with the parameter or
    /// formula: the last F or S read it for that same word, or for an earlier word while the variables it reads kept
    /// their values from that word to this one.
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="active">What the target state keeps under the F or the speed of the spindle.</param>
    /// <param name="key">The parameter or formula of the word as KeyOf names it: Q1, =Q1 * 2.</param>
    /// <param name="reads">The variables the value of the word reads (ReadsOf).</param>
    /// <param name="source">The step of the word that set the feed or the speed NCX has.</param>
    public static bool Holds(HeidenhainBlock writing, string? active, string key, IReadOnlySet<string> reads,
        int source)
    {
        int place = active?.LastIndexOf(Place, StringComparison.Ordinal) ?? -1;
        if (place < 0 || active!.Substring(0, place) != key
            || !int.TryParse(active.AsSpan(place + 1), NumberStyles.None, CultureInfo.InvariantCulture,
                out int read))
        {
            return false;
        }

        // The control keeps the value it read until the next F or S (controllers heidenhain.md 2, 4), and it read the
        // parameter where it had the value NCX read at the word it was read for (Keeps). NCX reads the parameter where
        // its word stands, before the words of its block act (virtual machine 3.6), so an assignment after the word
        // of the feed NCX has changes neither that feed nor the control's.
        return read == source || KeepsValue(writing, reads, read, false, source, false, labels: true);
    }

    /// <summary>
    /// Tells whether the variables a feed or speed reads have, where the block being written reads them, the values
    /// NCX read at its word; reports it where they may not (CMP117).
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="word">The word that sets the feed or the speed with the parameter or formula.</param>
    /// <param name="reads">The variables its value reads (ReadsOf).</param>
    /// <param name="source">The step of the word: the step being written, an earlier one, or the block after a TOOL
    /// whose RPM the TOOL CALL takes.</param>
    /// <param name="afterVariables">True where the F or S stands after the VAR lines of its block.</param>
    public static bool Keeps(HeidenhainBlock writing, Word word, IReadOnlySet<string> reads, int source,
        bool afterVariables)
    {
        // The TOOL CALL reads the RPM of the block after the TOOL before that block, and before the VAR lines of its
        // own block, which NCX runs first. A jump to a label on that block does not pass the TOOL CALL, and the label
        // writes the speed again there (HeidenhainFlow.WriteLabel).
        int index = writing.Step.Index;
        bool keeps = source <= index
            ? KeepsValue(writing, reads, source, false, index, afterVariables, labels: true)
            : KeepsValue(writing, reads, index, afterVariables, source, false, labels: false);
        if (!keeps)
        {
            string names = string.Join(", ", reads.Order(StringComparer.Ordinal));
            writing.Error(DiagnosticCodes.HeidenhainParameterReadElsewhere,
                $"{word.ToCanonical()} on line {writing.LookAhead.Steps[source].Block.Line}: Klartext writes it here "
                + $"with the Q parameter of {names} in place of the number (controllers heidenhain.md 6; D396), and "
                + $"{names} may have another value here than where NCX reads it, since an assignment of it, a label "
                + "that a jump reaches, a motion before a system datum, RAW or a call of another file stands between; "
                + "nothing is written for it (virtual machine 3.6; language 4.9).");
        }

        return keeps;
    }

    /// <summary>
    /// Reports a feed or a speed whose value has no Klartext form (CMP102).
    /// </summary>
    // D396 extends D338 to F and S: a formula or a parameter with a sign is spilled into the highest free QL in a block
    // of its own directly before the block, which writes FQL499 or SQL499 (Written); CMP102 stays where Klartext has
    // no formula for the value or no QL is free. A literal negative F or S is an error of the virtual machine.
    public static void ReportValue(HeidenhainBlock writing, Word word, string? problem)
    {
        writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
            $"{word.ToCanonical()}: F and S take a number or a Q parameter in place of it, F500 or FQ1, and a formula "
            + $"through a QL parameter (controllers heidenhain.md 2, 4, 6; D338, D396), and {problem}; nothing is "
            + "written for it.");
    }

    // The parameter has one value from a point of one step to a point of the same or a later step in walk order: no
    // VAR or ARG of it between the two points, where the lines of a block that assign a Q parameter stand before its
    // motion and after its TOOL CALL (HeidenhainCompiler.WriteBlock), no RAW or call of another file, which may assign
    // any parameter, and, where labels count, no label after the first point, which a jump reaches with the value its
    // own way left (language 4.9).
    // A SYS_ name has the value FN 18 reads where it is read: it keeps it within the step alone, since a motion may
    // change it (D397).
    private static bool KeepsValue(HeidenhainBlock writing, IReadOnlySet<string> reads, int from,
        bool fromAfterVariables, int to, bool toAfterVariables, bool labels)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        if (from > to || (from != to && reads.Any(HeidenhainFormula.IsSystem)))
        {
            return false;
        }

        for (int at = from; at <= to; at++)
        {
            Block block = steps[at].Block;
            if (block.Has("RAW") || CallsAnotherFile(steps[at], block) || (labels && at > from && block.Has("LABEL")))
            {
                return false;
            }

            bool afterFirst = at > from || !fromAfterVariables;
            bool beforeSecond = at < to || toAfterVariables;
            if (afterFirst && beforeSecond && Assigns(block, reads))
            {
                return false;
            }
        }

        return true;
    }

    // VAR:Q1 assigns the parameter, and so does ARG:Q1, which Klartext sets before the call (controller-mapping 6, CALL
    // + ARG).
    private static bool Assigns(Block block, IReadOnlySet<string> reads)
    {
        foreach (Word word in block.Words)
        {
            if (word.Key is "VAR" or "ARG" && word.Addr is string address && reads.Contains(address))
            {
                return true;
            }
        }

        return false;
    }

    // CALL="name" of a program outside the file, CALL PGM name (language 4.9, CALL; HeidenhainSubprograms).
    private static bool CallsAnotherFile(BlockStep step, Block block)
    {
        return block.Find("CALL")?.Value is StringValue name && !step.After.Flow.Subs.ContainsKey(name.Content);
    }
}
