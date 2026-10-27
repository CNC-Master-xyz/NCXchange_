namespace Ncx.Core.Machine;

/// <summary>
/// [cycles]: the cycle catalog of the controller family that the machine uses (machine-config 6).
/// </summary>
public sealed record CyclesConfig
{
    /// <summary>
    /// catalog: the catalog file, "heidenhain.toml", shipped with NCXchange as &lt;family&gt;.toml and
    /// user-extendable, a shop catalog under any name (D156); null when left out.
    /// </summary>
    public string? CatalogFile { get; init; }
}
