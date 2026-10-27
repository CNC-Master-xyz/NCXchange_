namespace Ncx.Core.Machine;

/// <summary>
/// [format] motion_code: on which motion blocks a compiler writes the code of the motion verb, G0 to G3, L and C, read
/// by every compiler (D299). A file that leaves it out takes the value of its family, the form main writes: Fanuc
/// <see cref="Arcs"/>, Siemens and Heidenhain <see cref="EveryBlock"/> (<see cref="OutputFormat.WithFamilyDefaults"/>).
/// </summary>
public enum MotionCode
{
    /// <summary>
    /// motion_code = "change": the verb only where it changes, arcs included.
    /// </summary>
    Change,

    /// <summary>
    /// motion_code = "arcs": G0 and G1 where they change, G2 and G3 on every arc block.
    /// </summary>
    Arcs,

    /// <summary>
    /// motion_code = "every_block": the verb on every motion block.
    /// </summary>
    EveryBlock,
}
