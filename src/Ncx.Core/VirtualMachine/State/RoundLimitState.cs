namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The rounding allowance of the program: ROUND_LIMIT and ROUND_LIMIT:ROTARY together (language 4.1, virtual machine
/// 2.1, D363). State only, read by the compiler, which reports a written point moved by more than the allowance; it has
/// no control form. Immutable: a word that changes one of the two replaces the whole value.
/// </summary>
public sealed record RoundLimitState
{
    /// <summary>
    /// ROUND_LIMIT: the allowance for the linear axes in the active units; null for OFF, under which rounding is not
    /// reported.
    /// </summary>
    public required decimal? Value { get; init; }

    /// <summary>
    /// ROUND_LIMIT:ROTARY: the allowance for the rotary axes and the angle addresses in degrees; null when none is set.
    /// </summary>
    public required decimal? Rotary { get; init; }
}
