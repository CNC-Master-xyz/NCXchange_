using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Cycles;

// D358: the [cycles] keys of D357 hold distances the control keeps, and a key is the one source the compiler writes.
public static partial class CycleCatalogLoader
{
    // The [cycles] key of the chip-break lift (D357).
    private const string ChipBreakLiftKey = "chip_break_lift";

    // The [cycles] key of the peck re-approach distance (D357).
    private const string PeckClearanceKey = "peck_clearance";

    // The Heidenhain native the chip-break lift is written to: Q256 of cycle 203 (D358; heidenhain 5).
    private const string HeidenhainLift = "Q256";

    /// <summary>
    /// Checks the cycle catalog of a machine against the [cycles] keys of D357 the machine sets (D358): CFG156 where
    /// a set key and the defaults of an entry name the same native parameter, CFG158 where a Heidenhain catalog writes
    /// Q256 and the machine sets no chip_break_lift. The caller that reads the [cycles] keys calls it once the
    /// catalog file is under the machine (machine-config 6).
    /// </summary>
    /// <param name="catalog">The cycle catalog of the machine, its catalog file and its [[cycle]] entries.</param>
    /// <param name="setKeys">The names of the [cycles] keys the machine file sets, "chip_break_lift".</param>
    /// <param name="line">The line of the [cycles] table, where the diagnostics stand.</param>
    /// <param name="diagnostics">Where the findings are reported.</param>
    public static void CheckHeldDistances(
        CycleCatalog catalog, IReadOnlyCollection<string> setKeys, int line, Diagnostics diagnostics)
    {
        foreach (CycleEntry entry in catalog.Entries)
        {
            foreach (string key in setKeys)
            {
                string? native = HeldNative(catalog.Controller, entry, key);
                if (native is not null && entry.Defaults.ContainsKey(native))
                {
                    diagnostics.Warning(line, DiagnosticCodes.CycleDefaultBesideMachineKey,
                        $"[cycles] {key} and the defaults of the cycle entry {entry.Name} both give {native} of "
                        + $"{entry.Native}; the compiler writes the machine key, its one source (D358).");
                }
            }

            if (catalog.Controller == Controller.Heidenhain && !setKeys.Contains(ChipBreakLiftKey)
                && entry.Signature.Contains(HeidenhainLift) && !entry.Defaults.ContainsKey(HeidenhainLift))
            {
                diagnostics.Error(line, DiagnosticCodes.CycleChipBreakLiftMissing,
                    $"Cycle {entry.Native} of the entry {entry.Name} writes {HeidenhainLift}, the chip-break lift the "
                    + $"control holds, and the machine sets no [cycles] {ChipBreakLiftKey}; NCX does not guess a lift "
                    + "(D164, D358).");
            }
        }
    }

    // The native parameter a [cycles] key is written to (D358): Heidenhain Q256 from chip_break_lift; Siemens _VRT
    // of CYCLE83 from chip_break_lift and _DIS1 from peck_clearance (positions 15 and 17); Fanuc none, its cycle block
    // having no such address.
    private static string? HeldNative(Controller? controller, CycleEntry entry, string key)
    {
        string? native = null;
        if (controller == Controller.Heidenhain)
        {
            if (key == ChipBreakLiftKey)
            {
                native = HeidenhainLift;
            }
        }
        else if (controller == Controller.Siemens)
        {
            if (key == ChipBreakLiftKey)
            {
                native = "_VRT";
            }
            else if (key == PeckClearanceKey)
            {
                native = "_DIS1";
            }
        }

        return native is not null && entry.Signature.Contains(native) ? native : null;
    }
}
