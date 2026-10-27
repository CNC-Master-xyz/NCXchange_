using Ncx.Core.Machine;

namespace Ncx.Compilers;

/// <summary>
/// The rule of [format] motion_code that every compiler reads (D299, D282 (a); machine-config 2): "change" writes the
/// code of the motion verb only where it changes, arcs included; "arcs" writes G0 and G1 where they change and G2 and
/// G3 on every arc block; "every_block" writes the code on every motion block.
/// </summary>
internal static class MotionCodeRule
{
    /// <summary>
    /// True when the block writes its motion code.
    /// </summary>
    /// <param name="format">The [format] of the machine, with its family defaults (OutputFormat.WithFamilyDefaults).
    /// </param>
    /// <param name="arc">True for an ARC.</param>
    /// <param name="active">The code the control has active; null while it is unknown.</param>
    /// <param name="code">The code of the block: G0, G1, G2, G3.</param>
    public static bool Writes(OutputFormat? format, bool arc, string? active, string code)
    {
        return format?.MotionCode switch
        {
            MotionCode.Change => active != code,
            MotionCode.EveryBlock => true,
            _ => arc || active != code,
        };
    }
}
