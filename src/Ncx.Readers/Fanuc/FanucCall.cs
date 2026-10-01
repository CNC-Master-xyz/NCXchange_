using System.Globalization;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// A call of a program in a block (controllers fanuc.md 1, 7; controller-mapping 6): M98 P, G65 P and, on a machine of
/// the builder nakamura, M200 P call a program of the file or an external one, the external call code of the machine
/// P an external program (D322).
/// </summary>
/// <param name="Program">The number of the called program, 100 of M98 P0100.</param>
/// <param name="Repeats">True when the call runs the program more than once, or a number of times the reader does not
/// know.</param>
/// <param name="External">True for the external call code of the machine, the call of a program from external
/// memory.</param>
/// <param name="Macro">True for a G65 macro call, which opens a local-variable level of its own, so that the callee's
/// #1..#33 are vacant apart from the letter arguments; a subprogram call opens none and shares the caller's (fanuc 7,
/// D148).</param>
internal sealed record FanucCall(long Program, bool Repeats, bool External, bool Macro)
{
    /// <summary>
    /// The name of the section the call enters, the number without leading zeros (controller-mapping 6).
    /// </summary>
    public string Name => Program.ToString(CultureInfo.InvariantCulture);
}
