using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

// CMP015 is the framework's, not a family's (D367): the [tolerance] template a compiler writes may lack a placeholder
// for a value the program set, and the control then keeps the value it holds.
public abstract partial class CompilerBase
{
    private const decimal MillimetresPerInch = 25.4m;

    // The values reported in the current program, "TOLERANCE:ROTARY=0.5" (D367: once per distinct value per program).
    private readonly HashSet<string> _toleranceReported = new(StringComparer.Ordinal);

    // CMP015 for each value of the tolerance the written template cannot carry: {tol} for TOLERANCE, {rotary} for
    // TOLERANCE:ROTARY (also the rotary tolerance a machine without ON_ROTARY drops, D151), {mode} for
    // TOLERANCE_MODE=ROUGH, FINISH being the start value the control takes without it. Never for TOLERANCE=OFF.
    private void CheckTolerance(BlockStep step)
    {
        Block block = step.Block;
        if (block.Has("PROGRAM", null, "BEGIN"))
        {
            _toleranceReported.Clear();
        }

        if (!block.Has("TOLERANCE") && !block.Has("TOLERANCE_MODE"))
        {
            return;
        }

        ToleranceTable? table = Machine.Tolerance;
        ToleranceState state = step.After.Frame.Tolerance;
        if (state.Value is not decimal value || ToleranceTemplate.On(table, state) is not string template)
        {
            return;
        }

        bool inch = step.After.Frame.Units == Units.Inch;
        if (!TemplateText.Has(template, "tol"))
        {
            decimal millimetres = inch ? value * MillimetresPerInch : value;
            bool held = table?.HeldValue is decimal heldValue && heldValue <= millimetres;
            ReportTolerance(block, table, "TOLERANCE=" + Text(value), Text(value) + (inch ? " in" : " mm"), "tol",
                held);
        }

        if (state.Rotary is decimal rotary && !TemplateText.Has(template, "rotary"))
        {
            ReportTolerance(block, table, "TOLERANCE:ROTARY=" + Text(rotary), Text(rotary) + " deg", "rotary", false);
        }

        if (state.Mode == ToleranceMode.Rough && !TemplateText.Has(template, "mode"))
        {
            ReportTolerance(block, table, "TOLERANCE_MODE=ROUGH", "ROUGH", "mode", false);
        }
    }

    private void ReportTolerance(Block block, ToleranceTable? table, string word, string value, string placeholder,
        bool held)
    {
        if (!_toleranceReported.Add(word))
        {
            return;
        }

        // held_in names where the control holds the path tolerance, so only {tol} quotes it (D367).
        string where = placeholder == "tol" && table?.HeldIn is string heldIn
            ? $"the control holds it in {heldIn}"
            : "the control keeps the value it holds";
        string message = $"{word.Split('=')[0]} {value} is not written: the [tolerance] template has no "
            + $"{{{placeholder}}}, and {where} (machine-config 5, D151, D367).";
        if (held)
        {
            Diagnostics.Info(block, DiagnosticCodes.ToleranceValueNotWritten, message);
        }
        else
        {
            Diagnostics.Warning(block, DiagnosticCodes.ToleranceValueNotWritten, message);
        }
    }

    private static string Text(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
