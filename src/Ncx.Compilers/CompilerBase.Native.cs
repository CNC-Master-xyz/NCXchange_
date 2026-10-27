using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

// What compiles only to one controller (language 4.1, 4.7.1; controller-mapping 9; D5, D94): RAW with its source text,
// and a cycle written natively with its parameters.
public abstract partial class CompilerBase
{
    /// <summary>
    /// The source text of a RAW block, which the compiler writes verbatim on its own controller or builder (language
    /// 4.1, D5); null for a block without RAW.
    /// </summary>
    /// <param name="block">The block.</param>
    protected static string? RawText(Block block)
    {
        return block.Find("RAW")?.Value is StringValue text ? text.Content : null;
    }

    /// <summary>
    /// The native cycle of a CYCLE:controller=n block with its native parameters in source order, which a compiler of
    /// that family writes as the control's own cycle (language 4.7.1, D94); null for any other block.
    /// </summary>
    /// <param name="block">The block.</param>
    protected static NativeCycle? NativeCycleOf(Block block)
    {
        Word? cycle = null;
        foreach (Word word in block.Words)
        {
            if (word.Key == "CYCLE" && word.Addr is not null)
            {
                cycle = word;
                break;
            }
        }

        if (cycle?.Addr is not string controller)
        {
            return null;
        }

        // Every key the word catalog does not know is a native parameter, kept in source order after the cycle words
        // (language 4.7.1, D94).
        var parameters = new List<Word>();
        foreach (Word word in block.Words)
        {
            if (word.Definition is null)
            {
                parameters.Add(word);
            }
        }

        return new NativeCycle
        {
            Controller = controller,
            Number = CycleCatalog.NativeOf(cycle.Value),
            Parameters = parameters,
        };
    }

    // RAW compiles only to the controller or builder it names, and a CYCLE:controller=n block with its native
    // parameters only to that controller family; any other target is an ERROR at compile time, the same for both
    // (language 4.1, 4.7.1; controller-mapping 9; virtual machine 5; D5, D94). NCX writes the address in capitals, the
    // machine file its builder in small letters (language 3, machine-config 1).
    private void CheckNativeText(NcxProgram program, MachineConfig machine, Diagnostics diagnostics)
    {
        string family = Controller.ToString().ToUpperInvariant();
        string? builder = string.IsNullOrEmpty(machine.Machine.Builder)
            ? null
            : machine.Machine.Builder.ToUpperInvariant();
        foreach (Block block in program.Blocks)
        {
            foreach (Word word in block.Words)
            {
                if (word.Key == "RAW" && word.Addr is string dialect && dialect != family && dialect != builder)
                {
                    ReportNativeText(block, "RAW:" + dialect, dialect, "controller or builder", machine, diagnostics);
                }
                else if (word.Key == "CYCLE" && word.Addr is string cycleFamily && cycleFamily != family)
                {
                    ReportNativeText(block, word.ToCanonical() + " with its native parameters", cycleFamily,
                        "controller family", machine, diagnostics);
                }
            }
        }
    }

    private void ReportNativeText(Block block, string what, string named, string kind, MachineConfig machine,
        Diagnostics diagnostics)
    {
        string target = string.IsNullOrEmpty(machine.Machine.Builder)
            ? $"a {Controller} machine"
            : $"a {Controller} machine of the builder {machine.Machine.Builder}";
        diagnostics.Error(block, DiagnosticCodes.NativeTextOfAnotherController,
            $"{what} compiles only to {named}, the {kind} it names, and the machine \"{machine.Machine.Name}\" is "
            + $"{target}: an ERROR at compile time (language 4.1, 4.7.1; controller-mapping 9; D5, D94).");
    }

    // An ARC with a tool vector compiles like RAW of another controller where the controller has no vector arc: an
    // ERROR at compile time, every one reported before anything is written (D185; language 4.3, TX).
    private void CheckVectorArcs(NcxProgram program, MachineConfig machine, Diagnostics diagnostics)
    {
        if (WritesVectorArcs)
        {
            return;
        }

        foreach (Block block in program.Blocks)
        {
            if (block.Verb?.Key == "ARC" && (block.Has("TX") || block.Has("TY") || block.Has("TZ")))
            {
                diagnostics.Error(block, DiagnosticCodes.VectorArcNotWritable,
                    $"An ARC with a tool vector has no form on a {Controller} machine (\"{machine.Machine.Name}\"): "
                    + "write the arc as LINE blocks with their vectors, or as RAW of the controller (D185).");
            }
        }
    }

    // A cycle the machine marks available = false on the compiled channel, or on the whole machine, is the ERROR
    // CMP004 at its definition, and nothing is written for it: no before line and no substitute such as a bare G84
    // (machine-config 6, D361, D377).
    private static void CheckCyclesAvailable(NcxProgram program, MachineConfig machine, int channel,
        Diagnostics diagnostics)
    {
        foreach (Block block in program.Blocks)
        {
            if (block.Find("CYCLE", null)?.Value is not IdentValue name
                || machine.CycleCatalog.Find(name.Name) is not CycleEntry { Available: false } entry)
            {
                continue;
            }

            diagnostics.Error(block, DiagnosticCodes.CycleNotAvailable,
                $"The cycle {entry.Name} is not available on channel {channel} of the machine "
                + $"\"{machine.Machine.Name}\" ([[cycle]] available = false), so nothing is written for it "
                + "(machine-config 6, D361, D377).");
        }
    }
}
