using System.Globalization;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// PROGRAM_BEGIN and PROGRAM_END (virtual machine 7): raised at PROGRAM=BEGIN and PROGRAM=END with the name and the
/// number of the program, and the run statistics at PROGRAM_END.
/// </summary>
public sealed record ProgramEvent : VmEvent
{
    /// <summary>
    /// PROGRAM_BEGIN or PROGRAM_END.
    /// </summary>
    public required EventPhase Phase { get; init; }

    /// <summary>
    /// program.name: the NAME of the program; empty without one (virtual machine 2.1).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// program.number: the NUMBER of the program; 0 without one (virtual machine 2.1).
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Run statistics at PROGRAM_END: the blocks executed from PROGRAM=BEGIN to PROGRAM=END, both included, the blocks
    /// of the subprograms it called among them; 0 at PROGRAM_BEGIN.
    /// </summary>
    public int Blocks { get; init; }

    /// <summary>
    /// Run statistics at PROGRAM_END: the known lengths of the MOTIONs of those blocks other than RAPID; 0 at
    /// PROGRAM_BEGIN (D235).
    /// </summary>
    public decimal FeedDistance { get; init; }

    /// <summary>
    /// Run statistics at PROGRAM_END: the known lengths of the RAPID motions of those blocks; 0 at PROGRAM_BEGIN
    /// (D235).
    /// </summary>
    public decimal RapidDistance { get; init; }

    /// <summary>
    /// Run statistics at PROGRAM_END: the MOTIONs of those blocks whose length is unknown, so that a report can call
    /// the distances a lower bound; 0 at PROGRAM_BEGIN (D235).
    /// </summary>
    public int UnknownLengthMotions { get; init; }

    /// <summary>
    /// The derived total, FeedDistance plus RapidDistance (D235).
    /// </summary>
    public decimal Distance => FeedDistance + RapidDistance;

    /// <inheritdoc/>
    public override string Kind => Phase == EventPhase.Begin ? "PROGRAM_BEGIN" : "PROGRAM_END";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        string program = $"name {EventText.Quoted(Name)}, number {Number.ToString(CultureInfo.InvariantCulture)}";
        if (Phase == EventPhase.Begin)
        {
            return program;
        }

        return program + ", " + EventText.Statistics(Blocks, FeedDistance, RapidDistance, UnknownLengthMotions);
    }
}
