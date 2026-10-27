using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Readers;

/// <summary>
/// Chooses among the states that write one native code those the reader writes: it drops the states its source state
/// rules out, writes every remaining state that does not already hold, and falls back to the first state in file order
/// (architecture 7, D155).
/// </summary>
public static class FunctionChoice
{
    private const string Off = "OFF";

    /// <summary>
    /// The states to write for a native code, the [workpiece] selection first (D40, D155): M9 on the Nakamura gives
    /// COOLANT=OFF for each channel that was on, M34 on the Doosan WORKPIECE=MAIN and SPINDLE_MODE:MAIN=SPINDLE when
    /// the C axis was on.
    /// </summary>
    /// <param name="matches">Every state that writes the code, as TemplateSet.FindFunctionsByCode answers.</param>
    /// <param name="context">What the source state says about the code.</param>
    /// <param name="line">The line of the source block, for the WARNING.</param>
    /// <param name="nativeCode">The native code as the source writes it, for the WARNING.</param>
    /// <param name="diagnostics">Where RDR020 goes.</param>
    /// <returns>The states to write, in file order; empty when no state writes the code.</returns>
    public static IReadOnlyList<FunctionMatch> Choose(
        IReadOnlyList<FunctionMatch> matches, FunctionContext context, int line, string nativeCode,
        Diagnostics diagnostics)
    {
        // The reader drops the states its source state rules out: a table bound to another channel (D56), a spindle
        // the source does not address, an OFF of a resource that is not on; and it writes every remaining state that
        // does not already hold (D155).
        var remaining = new List<FunctionMatch>();
        foreach (FunctionMatch match in matches)
        {
            if (!RuledOut(match, context) && !Holds(match, context))
            {
                remaining.Add(match);
            }
        }

        // Nothing remains: the first state in file order (D155).
        if (remaining.Count == 0)
        {
            return matches.Count == 0 ? [] : [matches[0]];
        }

        // Two resources of one word would be switched on, M3 of MAIN and TOOL on the Mori Seiki: the first state in
        // file order, with a WARNING that names the others (D155).
        var others = new List<string>();
        for (int index = 1; index < remaining.Count; index++)
        {
            if (SwitchesOnAnother(remaining[0], remaining[index]))
            {
                others.Add(remaining[index].Key);
            }
        }

        if (others.Count == 0)
        {
            return remaining;
        }

        diagnostics.Warning(line, DiagnosticCodes.FunctionCodeOfSeveralResources,
            $"{nativeCode} switches on more than one resource of the machine; the reader writes {remaining[0].Key} and "
            + $"not {string.Join(", ", others)} (machine-config 5, D155).");
        return [remaining[0]];
    }

    // A state the source state rules out (D155): bound to another channel (D56), a spindle word of a role the source
    // does not address, the OFF of a resource that is not on.
    private static bool RuledOut(FunctionMatch match, FunctionContext context)
    {
        if (match.Table is { AllChannels: false, Channel: int bound } && context.Channel is int channel
            && bound != channel)
        {
            return true;
        }

        bool spindleWord = match.Family is "SPINDLE" or "SPINDLE_MODE";
        if (spindleWord && context.AddressedSpindles is { } addressed && match.Address is string role
            && !addressed.Contains(role))
        {
            return true;
        }

        return match.State == Off && context.Holding is { } holding
            && (!holding.TryGetValue(match.Word, out string? state) || state == Off);
    }

    // A state that already holds is not written again (D155).
    private static bool Holds(FunctionMatch match, FunctionContext context)
    {
        return match.State != Off && context.Holding?.TryGetValue(match.Word, out string? state) == true
            && state == match.State;
    }

    // Two states that switch on two resources of one word, SPINDLE:MAIN=CW and SPINDLE:TOOL=CW (D155).
    private static bool SwitchesOnAnother(FunctionMatch first, FunctionMatch other)
    {
        return first.Table is not null && other.Table is not null && first.Family == other.Family
            && first.Address != other.Address && first.State != Off && other.State != Off;
    }
}
