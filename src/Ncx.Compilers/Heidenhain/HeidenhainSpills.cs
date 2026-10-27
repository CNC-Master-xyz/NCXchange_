using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The QL parameters the Heidenhain compiler spills formulas into (D338): heidenhain 6 lets a Q parameter stand
/// wherever a number stands, so a formula of a block is set in a QL parameter in a block of its own directly before
/// it, QL499 = Q1 + 5, then L X+QL499. The compiler takes the highest QL numbers that no word of the file names and
/// that [format] counter_registers does not hold, QL499 down to QL0, the local parameters of a program (iTNC 530
/// Klartext 340490-05, connector p. 282-283); each block starts again at the top, since its spills are read by the
/// block itself.
/// </summary>
internal sealed partial class HeidenhainSpills
{
    // The highest QL parameter of the iTNC 530 (Klartext 340490-05 p. 282-283: QL0 to QL499).
    private const int Highest = 499;

    private readonly List<string> _free = [];

    /// <summary>
    /// The free QL parameters of the compile, highest first.
    /// </summary>
    public static HeidenhainSpills Of(LookAhead lookAhead, CounterRange? counters)
    {
        HashSet<string> named = NamedParameters(lookAhead);
        var spills = new HeidenhainSpills();
        for (int number = Highest; number >= 0; number--)
        {
            string name = "QL" + number.ToString(CultureInfo.InvariantCulture);
            bool counter = counters is { Prefix: "QL" } && number >= counters.First && number <= counters.Last;
            if (!named.Contains(name) && !counter)
            {
                spills._free.Add(name);
            }
        }

        return spills;
    }

    /// <summary>
    /// The names of the run's words: the variables their values read, their addresses (VAR:QL5) and the QL parameters
    /// of RAW text.
    /// </summary>
    public static HashSet<string> NamedParameters(LookAhead lookAhead)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (BlockStep step in lookAhead.Steps)
        {
            named.UnionWith(HeidenhainReads.NamesOf(step.Block));
            foreach (Word word in step.Block.Words)
            {
                if (word.Addr is string address)
                {
                    named.Add(address);
                }

                if (word.Key == "RAW" && word.Value is StringValue raw)
                {
                    foreach (Match match in Parameter().Matches(raw.Content))
                    {
                        named.Add(match.Value);
                    }
                }
            }
        }

        return named;
    }

    /// <summary>
    /// The n-th free QL parameter from the top, 0 the highest; null when no more is free.
    /// </summary>
    public string? Free(int index)
    {
        return index < _free.Count ? _free[index] : null;
    }

    [GeneratedRegex(@"Q[LR]?[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex Parameter();
}
