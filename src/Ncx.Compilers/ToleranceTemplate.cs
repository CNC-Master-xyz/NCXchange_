using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

/// <summary>
/// The [tolerance] template every compiler writes for a TOLERANCE that is on (machine-config 5): ON_ROTARY while
/// TOLERANCE:ROTARY is set and the machine has it, ON otherwise (D151); and the optional comment beside the switch
/// that held_comment asks for (D367).
/// </summary>
public static class ToleranceTemplate
{
    /// <summary>
    /// True when the compiler writes ON_ROTARY rather than ON.
    /// </summary>
    /// <param name="table">The [tolerance] table; null when the machine has none.</param>
    /// <param name="state">The tolerance after the block.</param>
    public static bool IsRotary(ToleranceTable? table, ToleranceState state)
    {
        return state.Rotary is not null && table?.OnRotary is not null;
    }

    /// <summary>
    /// The template for a TOLERANCE that is on; null when the table has none.
    /// </summary>
    public static string? On(ToleranceTable? table, ToleranceState state)
    {
        return IsRotary(table, state) ? table?.OnRotary : table?.On;
    }

    /// <summary>
    /// The name of the template for messages: "[tolerance] ON_ROTARY (machine-config 5, D151)".
    /// </summary>
    public static string What(ToleranceTable? table, ToleranceState state)
    {
        return IsRotary(table, state)
            ? "[tolerance] ON_ROTARY (machine-config 5, D151)"
            : "[tolerance] ON (machine-config 5, D85)";
    }

    /// <summary>
    /// The text of the comment beside the switch under held_comment = true, "TOL 0.005 parameter 19581", without the
    /// comment marks of the family; null without held_comment, for TOLERANCE=OFF, or when the template carries {tol}
    /// (D367).
    /// </summary>
    public static string? Comment(ToleranceTable? table, ToleranceState state)
    {
        if (table is not { HeldComment: true } || state.Value is not decimal value
            || On(table, state) is not string template || TemplateText.Has(template, "tol"))
        {
            return null;
        }

        string text = "TOL " + value.ToString(CultureInfo.InvariantCulture);
        return table.HeldIn is string heldIn ? text + " " + heldIn : text;
    }
}
