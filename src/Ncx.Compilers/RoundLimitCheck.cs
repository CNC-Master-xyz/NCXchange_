using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

/// <summary>
/// The rounding report of D363 (language 4.1, ROUND_LIMIT; machine-config 2): the compiler rounds every number to the
/// decimals of its address silently and measures, over the linear axes of the block, the distance between the
/// programmed point and the point the written file produces; above ROUND_LIMIT it reports the WARNING CMP023, naming
/// the block, the deviation and the address. An angle address is measured on its own against ROUND_LIMIT:ROTARY.
/// Without ROUND_LIMIT nothing is reported.
/// </summary>
internal static partial class RoundLimitCheck
{
    /// <summary>
    /// Reports the block of a step where its rounding moves the point beyond the allowance of the program.
    /// </summary>
    /// <param name="step">The step being written.</param>
    /// <param name="machine">The machine, whose axes say which word is linear and which rotary.</param>
    /// <param name="numbers">The formatter, whose decimals the written file has.</param>
    /// <param name="diagnostics">Where the WARNING goes.</param>
    public static void Report(BlockStep step, MachineConfig machine, NumberFormatter numbers, Diagnostics diagnostics)
    {
        RoundLimitState limit = step.After.Frame.RoundLimit;
        if (limit.Value is not decimal linearLimit)
        {
            return;
        }

        decimal squares = 0m;
        decimal largest = -1m;
        string largestAddress = "";
        foreach (Word word in step.Block.Words)
        {
            if (word.Addr is not null || !AxisForm().IsMatch(word.Key) || NumberOf(word.Value) is not decimal value)
            {
                continue;
            }

            string axis = word.Key.Length > 1 && word.Key[0] == 'I' ? word.Key.Substring(1) : word.Key;
            AxisDef? resolved = step.After.Frame.WorkpieceHolder is string holder
                ? machine.ResolveAxis(axis, holder)
                : machine.ResolveAxis(axis);
            string letter = resolved?.Letter ?? axis;
            string address = numbers.DecimalsOf(letter) is not null ? letter : letter.Substring(0, 1);
            int decimals = numbers.WrittenDecimalsOf(address);
            decimal deviation = Math.Abs(value - Math.Round(value, decimals, MidpointRounding.AwayFromZero));
            // A rotary word is a length on the circumference under CYLINDER and in the polar plane (D102).
            bool rotary = (resolved?.Kind == AxisKind.Rotary || (resolved is null && numbers.IsAngle(letter)))
                && step.After.Frame.Cylinder is null && !step.After.Frame.Polar;
            if (rotary)
            {
                if (limit.Rotary is decimal rotaryLimit && deviation > rotaryLimit)
                {
                    Warn(step, diagnostics, word, deviation, rotaryLimit, "deg", "ROUND_LIMIT:ROTARY");
                }

                continue;
            }

            squares += deviation * deviation;
            if (deviation > largest)
            {
                largest = deviation;
                largestAddress = word.Key;
            }
        }

        decimal distance = (decimal)Math.Sqrt((double)squares);
        if (largest >= 0m && distance > linearLimit)
        {
            Word named = step.Block.Find(largestAddress) ?? step.Block.Words[0];
            Warn(step, diagnostics, named, distance, linearLimit, Unit(step), "ROUND_LIMIT");
        }
    }

    private static void Warn(BlockStep step, Diagnostics diagnostics, Word word, decimal deviation, decimal limit,
        string unit, string key)
    {
        diagnostics.Warning(step.Block, DiagnosticCodes.RoundingBeyondTheLimit,
            $"The written point lies {Text(Math.Round(deviation, 6, MidpointRounding.AwayFromZero))} {unit} from the "
            + $"programmed point, most of it on {word.ToCanonical()}, rounded to the decimals of its address; "
            + $"{key}={Text(limit)} allows less (D363; machine-config 2).");
    }

    private static string Unit(BlockStep step)
    {
        return step.After.Frame.Units == Units.Inch ? "inch" : "mm";
    }

    private static decimal? NumberOf(Value value)
    {
        return value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue number => number.Number,
            _ => null,
        };
    }

    private static string Text(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // An axis word: a standard axis, or a machine axis of the form of D93, with I in front for the incremental form.
    [GeneratedRegex("^I?[XYZABCUVW][0-9]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex AxisForm();
}
