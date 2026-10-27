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
/// the block, the deviation and the address. An angle word, a rotary axis, ROTATE or ANGLE, is measured on its own
/// against ROUND_LIMIT:ROTARY. Without ROUND_LIMIT nothing is reported. One check per compile: it keeps the written
/// position of every linear axis from block to block, an absolute word setting the rounded value and an incremental
/// word adding its rounded increment, so the drift of repeated increments is caught.
/// </summary>
internal sealed partial class RoundLimitCheck
{
    // The NCX angle words that are no axis words (D366): the turn of ROTATE and the sweep of ANGLE.
    private static readonly string[] s_angleWords = ["ROTATE", "ANGLE"];

    private readonly MachineConfig _machine;
    private readonly NumberFormatter _numbers;
    private readonly Diagnostics _diagnostics;

    // The position of each linear axis as the written file has it, in the radius values the virtual machine keeps.
    private readonly Dictionary<string, decimal> _written = new(StringComparer.Ordinal);

    /// <summary>
    /// The check of one compile.
    /// </summary>
    /// <param name="machine">The machine, whose axes say which word is linear and which rotary.</param>
    /// <param name="numbers">The formatter, whose decimals the written file has.</param>
    /// <param name="diagnostics">Where the WARNING goes.</param>
    public RoundLimitCheck(MachineConfig machine, NumberFormatter numbers, Diagnostics diagnostics)
    {
        _machine = machine;
        _numbers = numbers;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Follows the written position through the step and reports its block where the rounding moves the point beyond
    /// the allowance of the program.
    /// </summary>
    /// <param name="step">The step being written.</param>
    public void Report(BlockStep step)
    {
        FrameSnapshot frame = step.After.Frame;
        bool moves = step.Block.Verb?.Key is "RAPID" or "LINE" or "ARC" && !frame.Polar && frame.Cylinder is null;
        var named = new List<Word>();
        if (moves)
        {
            foreach (Word word in step.Block.Words)
            {
                if (word.Addr is null && AxisForm().IsMatch(word.Key) && Linear(step, AxisOf(word)) is not null)
                {
                    named.Add(word);
                }
            }
        }

        decimal squares = 0m;
        Word? largest = null;
        decimal largestDeviation = -1m;
        foreach (Word word in named)
        {
            string axis = AxisOf(word);
            if (After(step, axis) is not decimal reached)
            {
                _written.Remove(axis);
                continue;
            }

            decimal factor = Factor(step, axis);
            int decimals = _numbers.WrittenDecimalsOf(Linear(step, axis)!);
            decimal? written = null;
            if (NumberOf(word.Value) is decimal value)
            {
                decimal rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero) / factor;
                bool incremental = word.Key.Length > 1 && word.Key[0] == 'I';
                decimal? from = _written.TryGetValue(axis, out decimal held) ? held : Before(step, axis);
                written = incremental ? from + rounded : rounded;
            }

            // An expression is written as it stands, and the control computes it exactly.
            _written[axis] = written ?? reached;
            decimal deviation = Math.Abs(_written[axis] - reached);
            squares += deviation * deviation;
            if (deviation > largestDeviation)
            {
                largestDeviation = deviation;
                largest = word;
            }
        }

        // Every axis the block moves without a word of its own, or that a frame or a cycle moves, is written where the
        // virtual machine has it.
        Resync(step, named);

        RoundLimitState limit = frame.RoundLimit;
        if (limit.Value is not decimal linearLimit)
        {
            return;
        }

        decimal distance = (decimal)Math.Sqrt((double)squares);
        if (largest is not null && distance > linearLimit)
        {
            Warn(step, largest, distance, linearLimit, frame.Units == Units.Inch ? "inch" : "mm", "ROUND_LIMIT");
        }

        if (limit.Rotary is decimal rotaryLimit)
        {
            ReportAngles(step, rotaryLimit);
        }
    }

    // D363: every angle word against ROUND_LIMIT:ROTARY: a rotary axis word, ROTATE and ANGLE.
    private void ReportAngles(BlockStep step, decimal limit)
    {
        foreach (Word word in step.Block.Words)
        {
            string? address = null;
            if (word.Addr is null && s_angleWords.Contains(word.Key))
            {
                address = word.Key;
            }
            else if (word.Addr is null && AxisForm().IsMatch(word.Key) && Rotary(step, AxisOf(word)) is string letter)
            {
                address = letter;
            }

            if (address is null || NumberOf(word.Value) is not decimal value)
            {
                continue;
            }

            decimal deviation = Math.Abs(value - Math.Round(value, _numbers.WrittenDecimalsOf(address),
                MidpointRounding.AwayFromZero));
            if (deviation > limit)
            {
                Warn(step, word, deviation, limit, "deg", "ROUND_LIMIT:ROTARY");
            }
        }
    }

    private void Resync(BlockStep step, List<Word> named)
    {
        var words = new HashSet<string>(named.Select(AxisOf), StringComparer.Ordinal);
        foreach (KeyValuePair<string, AxisPosition> position in step.After.Motion.Position)
        {
            if (words.Contains(position.Key))
            {
                continue;
            }

            bool same = step.Before.Motion.Position.TryGetValue(position.Key, out AxisPosition before)
                && before == position.Value;
            if (same && _written.ContainsKey(position.Key))
            {
                continue;
            }

            if (position.Value is { Known: true, Frame: PositionFrame.Workpiece })
            {
                _written[position.Key] = position.Value.Value;
            }
            else
            {
                _written.Remove(position.Key);
            }
        }
    }

    // The address of [format] decimals of a linear axis, its letter or the letter without the extension; null for an
    // axis that is not linear.
    private string? Linear(BlockStep step, string axis)
    {
        AxisDef? resolved = Resolve(step, axis);
        string letter = resolved?.Letter ?? axis;
        string address = _numbers.DecimalsOf(letter) is not null ? letter : letter.Substring(0, 1);
        bool linear = resolved is null ? _numbers.IsLinear(letter) : resolved.Kind != AxisKind.Rotary;
        return linear ? address : null;
    }

    private string? Rotary(BlockStep step, string axis)
    {
        AxisDef? resolved = Resolve(step, axis);
        string letter = resolved?.Letter ?? axis;
        bool rotary = resolved is null ? _numbers.IsAngle(letter) : resolved.Kind == AxisKind.Rotary;
        return rotary && step.After.Frame.Cylinder is null && !step.After.Frame.Polar
            ? (_numbers.DecimalsOf(letter) is not null ? letter : letter.Substring(0, 1))
            : null;
    }

    private AxisDef? Resolve(BlockStep step, string axis)
    {
        return step.After.Frame.WorkpieceHolder is string holder
            ? _machine.ResolveAxis(axis, holder)
            : _machine.ResolveAxis(axis);
    }

    // Under DIAMETER=ON an X word is a diameter, which the virtual machine halves (D60): the deviation is compared as a
    // radius.
    private static decimal Factor(BlockStep step, string axis)
    {
        return axis == "X" && step.After.Frame.Diameter ? 2m : 1m;
    }

    private static decimal? After(BlockStep step, string axis)
    {
        return step.After.Motion.Position.TryGetValue(axis, out AxisPosition position)
            && position is { Known: true, Frame: PositionFrame.Workpiece } ? position.Value : null;
    }

    private static decimal? Before(BlockStep step, string axis)
    {
        return step.Before.Motion.Position.TryGetValue(axis, out AxisPosition position)
            && position is { Known: true, Frame: PositionFrame.Workpiece } ? position.Value : null;
    }

    private static string AxisOf(Word word)
    {
        return word.Key.Length > 1 && word.Key[0] == 'I' ? word.Key.Substring(1) : word.Key;
    }

    private void Warn(BlockStep step, Word word, decimal deviation, decimal limit, string unit, string key)
    {
        _diagnostics.Warning(step.Block, DiagnosticCodes.RoundingBeyondTheLimit,
            $"The written point lies {Text(Math.Round(deviation, 6, MidpointRounding.AwayFromZero))} {unit} from the "
            + $"programmed point, most of it on {word.ToCanonical()}, rounded to the decimals of its address; "
            + $"{key}={Text(limit)} allows less (D363; machine-config 2).");
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
