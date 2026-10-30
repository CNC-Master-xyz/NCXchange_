namespace Ncx.Core.Machine;

/// <summary>
/// The channel binding of one state of a function table, a value of its state_channels (machine-config 5, D414): the
/// one channel the machine accepts the state's code from, or every channel program behind a SYNC.
/// </summary>
public sealed record StateBinding
{
    /// <summary>
    /// The only channel the machine accepts the state's code from; null for "all".
    /// </summary>
    public int? Channel { get; init; }

    /// <summary>
    /// "all": the state's code must stand in every channel program behind a SYNC (D56).
    /// </summary>
    public bool AllChannels { get; init; }
}
