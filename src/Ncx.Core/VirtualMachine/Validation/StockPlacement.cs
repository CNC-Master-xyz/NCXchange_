namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// What the program that runs has done so far that decides where its STOCK may stand: once per program, after the
/// first ORIGIN and before the first motion verb (language 4.2, virtual machine 5, D351). One per run; a PROGRAM=BEGIN
/// starts it again.
/// </summary>
internal sealed class StockPlacement
{
    /// <summary>
    /// True once the program has selected a datum with ORIGIN.
    /// </summary>
    public bool OriginSeen { get; set; }

    /// <summary>
    /// True once a block of the program has had a motion verb.
    /// </summary>
    public bool MotionSeen { get; set; }

    /// <summary>
    /// True once the program has had a STOCK.
    /// </summary>
    public bool StockSeen { get; set; }

    /// <summary>
    /// Forgets the program before, at its PROGRAM=BEGIN.
    /// </summary>
    public void StartProgram()
    {
        OriginSeen = false;
        MotionSeen = false;
        StockSeen = false;
    }
}
