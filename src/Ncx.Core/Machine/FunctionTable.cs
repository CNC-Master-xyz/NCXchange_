namespace Ncx.Core.Machine;

/// <summary>
/// The templates of one resource or function per state: a [spindle.ROLE], [spindle_mode.ROLE] or [spindle_sync]
/// table, a [coolant] channel or a [func] entry, with its channel binding (D56) and its expansion rule (machine-config
/// 5, 5a). The VM only knows the abstract word; the template is the machine's business.
/// </summary>
public sealed record FunctionTable
{
    /// <summary>
    /// The template of each state, CW = "M3", OPEN = "M68", every M and G code normalized (D105).
    /// </summary>
    public IReadOnlyDictionary<string, string> States { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// channel: the only channel the machine accepts these codes from (D56); null when left out.
    /// </summary>
    public int? Channel { get; init; }

    /// <summary>
    /// channels = "all": the codes must stand in every channel program behind a SYNC (D56); false when left out.
    /// </summary>
    public bool AllChannels { get; init; }

    /// <summary>
    /// state_channels: the states that take a binding of their own, OFF = 1 or OPEN = "all"; every other state follows
    /// channel and channels (machine-config 5, D414). Empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, StateBinding> StateChannels { get; init; } =
        new Dictionary<string, StateBinding>();

    /// <summary>
    /// pre, post, requires, restore of the table (machine-config 5a); null when it has none of the four keys.
    /// </summary>
    public ExpansionRule? Rule { get; init; }

    /// <summary>
    /// rpm_min of a spindle table: the lowest speed of the machine; the expander warns or clamps (D64); null when left
    /// out.
    /// </summary>
    public decimal? RpmMin { get; init; }

    /// <summary>
    /// rpm_max of a spindle table: the highest speed of the machine; the expander warns or clamps (D64); null when left
    /// out.
    /// </summary>
    public decimal? RpmMax { get; init; }

    /// <summary>
    /// accel_time of a spindle table: the seconds from stop to rpm_max, for the runtime estimate (D64); null when left
    /// out.
    /// </summary>
    public decimal? AccelTime { get; init; }

    /// <summary>
    /// The table as it binds one of its states (D414): a state that state_channels lists takes that binding, every
    /// other state, and a word that names none, the channel or channels of the table, unbound when it has neither.
    /// </summary>
    /// <param name="state">The state a word sets (FunctionStates.Of); null for none.</param>
    public FunctionTable ForState(string? state)
    {
        return state is not null && StateChannels.TryGetValue(state, out StateBinding? binding)
            ? this with { Channel = binding.Channel, AllChannels = binding.AllChannels }
            : this;
    }
}
