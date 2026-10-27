namespace Ncx.Readers;

/// <summary>
/// How a reader reads one file (code-guidelines 5, Options): passed explicitly, so a test builds the options it needs.
/// </summary>
public sealed record ReadOptions
{
    /// <summary>
    /// The reader rules of the plugins, asked in this order for what the configuration tables of the machine leave
    /// undecided (D40, D66, D106); empty by default.
    /// </summary>
    public IReadOnlyList<ISourceRule> Rules { get; init; } = [];

    /// <summary>
    /// The channel the programs of the file run on, from ncx convert --channel n: the reader writes CHANNEL=n on every
    /// PROGRAM=BEGIN, and it wins over a channel the file name gives (D223; controller-mapping 7; architecture 10);
    /// null when the command line names none.
    /// </summary>
    public int? Channel { get; init; }
}
