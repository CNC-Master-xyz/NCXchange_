using Ncx.Core.Machine;

namespace Ncx.Config.Templates;

// One state of a function table or one [workpiece] selection with its parsed template, in the order of
// machine-config 5 and of the file (architecture 7, D155).
internal sealed record FunctionStateTemplate
{
    // The word of the table with its address, "SPINDLE:MAIN", "WORKPIECE".
    public required string Word { get; init; }

    // The key of the state, "CW"; the role of a [workpiece] selection.
    public required string State { get; init; }

    // The table of the state; null for a [workpiece] selection.
    public FunctionTable? Table { get; init; }

    // The template text of the state as the record keeps it (D105).
    public required string Text { get; init; }
}
