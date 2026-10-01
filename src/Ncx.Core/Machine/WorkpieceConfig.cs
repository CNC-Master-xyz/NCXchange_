namespace Ncx.Core.Machine;

/// <summary>
/// [workpiece]: the native selection of the holder the turret works on, per role, and how that side is programmed
/// (machine-config 5, D57).
/// </summary>
public sealed record WorkpieceConfig
{
    /// <summary>
    /// The selection template per holder role, MAIN = "M428", SUB = "M427": the selection code alone, never the
    /// datum, which is ORIGIN (D222). A role the machine gives no template writes nothing.
    /// </summary>
    public IReadOnlyDictionary<string, string> Templates { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// ROLE_frame per holder role, SUB_frame = "datum": whether the compiler negates Z against a datum or writes the
    /// mirror cycle (D57).
    /// </summary>
    public IReadOnlyDictionary<string, WorkpieceFrame> Frames { get; init; } =
        new Dictionary<string, WorkpieceFrame>();

    /// <summary>
    /// ROLE_mirror per holder role, SUB_mirror = { ON = "G360", OFF = "G361" }: the mirror cycle of that side.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionTable> Mirrors { get; init; } =
        new Dictionary<string, FunctionTable>();
}
