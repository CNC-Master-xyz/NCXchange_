using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The parameters of a catalog cycle, CYCLE=RECT_POCKET LENGTH=60 (language 4.7.1, D114): the parser keeps every key
/// the word catalog does not know as a cycle parameter, and the virtual machine checks their names against the
/// machine's cycle catalog. The row of the rule is in CycleValidation.
/// </summary>
internal static class CycleParameterValidation
{
    /// <summary>
    /// Checks the parameters of the cycle a CYCLE block defined. With a machine file a parameter the entry of the
    /// cycle does not map is VM254, an ERROR; without one it is the WARNING "not checked: no machine file", once per
    /// name (D103, D114). The words of language 4.7 and the native parameters of CYCLE:controller=n are no catalog
    /// parameters and are not checked here.
    /// </summary>
    /// <param name="context">The CYCLE block.</param>
    /// <param name="cycle">The cycle the block defined, with its parameters.</param>
    public static void Check(BlockContext context, CycleState cycle)
    {
        bool catalogCycle = cycle.Controller is null && WordCatalog.IsCatalogCycleBlock(context.Block);
        if (!catalogCycle || cycle.Name is not string name)
        {
            return;
        }

        CycleEntry? entry = context.Machine.CycleCatalog.Find(name);
        foreach (Word parameter in cycle.Parameters)
        {
            if (parameter.Definition is not null)
            {
                continue;
            }

            if (context.Resources.WithoutMachineFile)
            {
                WarnNotChecked(context, name, parameter.Key);
            }
            else if (entry is null || !entry.Params.ContainsKey(parameter.Key))
            {
                string why = entry is null
                    ? $"the machine's cycle catalog has no entry {name}"
                    : $"the entry {name} of the machine's cycle catalog maps {Names(entry)}";
                context.Diagnostics.Error(context.Block, DiagnosticCodes.CycleParameterUnknown,
                    $"{parameter.Key} is no parameter of the cycle {name}: {why} (language 4.7.1, D114).");
            }
        }
    }

    // Without a machine file the cycle catalog is unknown, so the name is not checked; the WARNING of D103 comes once
    // per cycle parameter.
    private static void WarnNotChecked(BlockContext context, string cycle, string parameter)
    {
        if (!context.Resources.IsFirstNotChecked($"cycle parameter {cycle} {parameter}"))
        {
            return;
        }

        context.Diagnostics.Warning(context.Block, DiagnosticCodes.NotCheckedNoMachineFile,
            $"The built-in default machine has no cycle catalog: the parameter {parameter} of the cycle {cycle} is "
            + "not checked: no machine file (virtual machine 3.8, D103, D114).");
    }

    // The NCX words an entry maps, for the message: "LENGTH, WIDTH and DEPTH", or "no parameters".
    private static string Names(CycleEntry entry)
    {
        var names = new List<string>(entry.Params.Keys);
        return names.Count switch
        {
            0 => "no parameters",
            1 => names[0],
            _ => string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[^1],
        };
    }
}
