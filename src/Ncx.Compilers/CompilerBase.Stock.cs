using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

// STOCK, the blank of the control's graphic (D351; language 4.2; machine-config 5): the framework decides what is
// written and reports CMP012 to CMP014, the families write the template with their own numbers.
public abstract partial class CompilerBase
{
    private static readonly string[] s_axes = ["X", "Y", "Z"];

    // The STOCK blocks of programs that also hold a RAW blank of the target, which is written alone (CMP014).
    private readonly HashSet<Block> _stockBesideRawBlank = [];

    /// <summary>
    /// What the family writes for the STOCK block of the step; null for another block, and for a STOCK that writes
    /// nothing: a form without a template (INFO CMP012), a stock the form cannot express (INFO CMP013), or a program
    /// with a RAW blank of the target (CMP014, reported before the run).
    /// </summary>
    /// <param name="step">The step being written.</param>
    protected StockForm? StockOf(BlockStep step)
    {
        Block block = step.Block;
        if (block.Verb is not Word verb || verb.Key != "STOCK" || _stockBesideRawBlank.Contains(block))
        {
            return null;
        }

        bool cylinder = verb.Value.ToCanonical() == "CYLINDER";
        string form = cylinder ? "CYLINDER" : "BOX";
        string? template = cylinder ? Machine.Stock?.Cylinder : Machine.Stock?.Box;
        if (string.IsNullOrEmpty(template))
        {
            Diagnostics.Info(block, DiagnosticCodes.StockFormWithoutTemplate,
                $"STOCK={form} is not written: the machine file has no [stock] {form}, and the blank feeds only the "
                + "control's graphic (machine-config 5, D351).");
            return null;
        }

        var low = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var high = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var numbers = new List<StockNumber>();
        foreach (string axis in s_axes)
        {
            if (NumberOf(block.Find(axis, null)) is not decimal first
                || NumberOf(block.Find("MAX", axis)) is not decimal second)
            {
                ReportInexpressible(block, form, "its corners are not all numbers");
                return null;
            }

            numbers.Add(Number(axis.ToLowerInvariant(), axis, first));
            numbers.Add(Number("max_" + axis.ToLowerInvariant(), axis, second));
            low[axis] = Math.Min(first, second);
            high[axis] = Math.Max(first, second);
        }

        Workplane plane = step.After.Frame.Workplane;
        string toolAxis = plane switch
        {
            Workplane.ZX => "Y",
            Workplane.YZ => "X",
            _ => "Z",
        };
        string[] planeAxes = s_axes.Where(axis => axis != toolAxis).ToArray();
        decimal radius = (high[planeAxes[0]] - low[planeAxes[0]]) / 2m;
        numbers.Add(Number("radius", planeAxes[0], radius));
        numbers.Add(Number("diameter", planeAxes[0], radius * 2m));
        numbers.Add(Number("length", toolAxis, high[toolAxis] - low[toolAxis]));
        numbers.Add(Number("dist", toolAxis, high[toolAxis]));
        string? reason = toolAxis != "Z" && !TemplateText.Has(template, "axis")
            ? $"the [stock] {form} template has no {{axis}} and the tool axis is {toolAxis}"
            : cylinder ? CylinderInexpressible(template, planeAxes, low, high) : null;
        if (reason is not null)
        {
            ReportInexpressible(block, form, reason);
            return null;
        }

        return new StockForm
        {
            Template = template,
            What = $"[stock] {form} (machine-config 5, D351)",
            Axis = toolAxis,
            Numbers = numbers,
        };
    }

    // CMP014: a program that holds a STOCK and a RAW blank of the target, a RAW of this controller or builder whose
    // text begins as a [stock] template of the machine does (BLK FORM, WORKPIECE), writes the RAW text alone, so that
    // the control does not get two blanks (D351).
    private void CheckStockBesideRawBlank(NcxProgram program, MachineConfig machine, Diagnostics diagnostics)
    {
        _stockBesideRawBlank.Clear();
        var heads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? template in (string?[])[machine.Stock?.Box, machine.Stock?.Cylinder])
        {
            if (!string.IsNullOrEmpty(template) && Head(template) is { Length: > 0 } head)
            {
                heads.Add(head);
            }
        }

        if (heads.Count == 0)
        {
            return;
        }

        string family = Controller.ToString().ToUpperInvariant();
        string? builder = string.IsNullOrEmpty(machine.Machine.Builder)
            ? null
            : machine.Machine.Builder.ToUpperInvariant();
        foreach (Section section in program.Programs)
        {
            Block? stock = null;
            bool rawBlank = false;
            for (int index = section.FirstBlock; index <= section.LastBlock; index++)
            {
                Block block = program.Blocks[index];
                stock ??= block.Verb?.Key == "STOCK" ? block : null;
                rawBlank |= block.Find("RAW") is Word { Value: StringValue raw } word
                    && (word.Addr is null || word.Addr == family || word.Addr == builder)
                    && heads.Contains(Head(raw.Content));
            }

            if (stock is not null && rawBlank)
            {
                _stockBesideRawBlank.Add(stock);
                diagnostics.Warning(stock, DiagnosticCodes.StockBesideRawBlank,
                    "The program holds a RAW blank of the control beside STOCK; the RAW text is written alone, and "
                    + "nothing for STOCK (D351).");
            }
        }
    }

    // The first word of a template or a RAW text, up to a space, a bracket or a placeholder: BLK, WORKPIECE.
    private static string Head(string text)
    {
        string trimmed = text.TrimStart();
        int end = trimmed.IndexOfAny([' ', '(', '{', '\n']);
        return end < 0 ? trimmed : trimmed[..end];
    }

    // A template without {axis}, BOX or CYLINDER, is written about Z and needs WORKPLANE=XY (StockOf). A cylinder
    // template without a coordinate of the plane axes is centred on the tool axis through the datum
    // (siemens-840dsl-mill.toml).
    private static string? CylinderInexpressible(string template, string[] planeAxes,
        Dictionary<string, decimal> low, Dictionary<string, decimal> high)
    {
        bool placed = planeAxes.Any(axis => TemplateText.Has(template, axis.ToLowerInvariant())
            || TemplateText.Has(template, "max_" + axis.ToLowerInvariant()));
        bool centred = planeAxes.All(axis => low[axis] + high[axis] == 0m);
        return placed || centred
            ? null
            : "the [stock] CYLINDER template has no centre, and the cylinder is not centred on the tool axis through "
                + "the datum";
    }

    private void ReportInexpressible(Block block, string form, string reason)
    {
        Diagnostics.Info(block, DiagnosticCodes.StockNotExpressible,
            $"STOCK={form} is not written: {reason}, and the blank feeds only the control's graphic (machine-config "
            + "5, D351).");
    }

    private StockNumber Number(string name, string address, decimal value)
    {
        decimal rounded = Numbers.DecimalsOf(address) is int decimals
            ? Math.Round(value, decimals, MidpointRounding.AwayFromZero)
            : value;
        return new StockNumber { Name = name, Address = address, Value = rounded };
    }

    private static decimal? NumberOf(Word? word)
    {
        return word?.Value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue number => number.Number,
            _ => null,
        };
    }
}
