using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The distances and the feed-out plane of the control's own drilling cycles that an expanded CYCLE_CALL takes from
/// the [cycles] keys of the machine file, or from the family's behaviour where the file leaves a key out
/// (machine-config 6, virtual machine 3.3; D190, D357, D378). Each distance is in the units of the program.
/// </summary>
internal sealed record CycleDistances
{
    private const decimal MillimetresPerInch = 25.4m;

    // D357: the manual's default lift of CYCLE83 chip breaking, _VRT = 0 meaning 1 mm (840D sl
    // Programmierhandbuch NC-Programmierung SW4.92 2019, connector p.1087).
    private const decimal SiemensChipBreakLift = 1m;

    /// <summary>
    /// The distance CHIP_BREAK lifts after each step before it feeds on.
    /// </summary>
    public required decimal ChipBreakLift { get; init; }

    /// <summary>
    /// The distance above the depth reached at which the rapid re-entry of PECK ends.
    /// </summary>
    public required decimal PeckClearance { get; init; }

    /// <summary>
    /// Whether REAM under CYCLE_RETRACT=SAFE feeds the whole way to SAFE (ream_feed_out = "SAFE") rather than to
    /// CLEARANCE.
    /// </summary>
    public required bool ReamFeedsOutToSafe { get; init; }

    /// <summary>
    /// The distances of the cycle being called on the machine of the run.
    /// </summary>
    /// <param name="context">The CYCLE_CALL block.</param>
    /// <param name="clearance">CLEARANCE along the drilling axis.</param>
    /// <param name="surface">SURFACE along the drilling axis; unknown when not written.</param>
    /// <param name="start">The plane the steps of PECK count from, SURFACE or CLEARANCE (D355).</param>
    /// <param name="depth">DEPTH along the drilling axis.</param>
    public static CycleDistances Of(BlockContext context, AxisPosition clearance, AxisPosition surface,
        AxisPosition start, AxisPosition depth)
    {
        Controller? family = context.Machine.Machine.Controller;
        CyclesConfig cycles = context.Machine.Cycles ?? new CyclesConfig();
        bool inch = context.State.Frame.Units == Units.Inch;
        return new CycleDistances
        {
            ChipBreakLift = InProgramUnits(cycles.ChipBreakLift, inch) ?? DefaultChipBreakLift(family, inch),
            PeckClearance = InProgramUnits(cycles.PeckClearance, inch)
                ?? DefaultPeckClearance(family, clearance, surface, start, depth, inch),
            ReamFeedsOutToSafe = (cycles.ReamFeedOut ?? DefaultReamFeedOut(family)) == ReamFeedOut.Safe,
        };
    }

    // D357: the keys are decimals in millimetres whatever units_default is.
    private static decimal? InProgramUnits(decimal? millimetres, bool inch)
    {
        return inch && millimetres is decimal value ? value / MillimetresPerInch : millimetres;
    }

    // D357: on Siemens the manual's 1 mm of _VRT; on Fanuc 0 (the runtime report names the key it defaulted).
    // Gap closed 2026-10-27 (delegated): Heidenhain without the key, whose lift is the cycle's Q256 that NCX does not
    // carry, and a run without a machine family take 0 as Fanuc does.
    private static decimal DefaultChipBreakLift(Controller? family, bool inch)
    {
        return family == Controller.Siemens ? InProgramUnits(SiemensChipBreakLift, inch) ?? 0m : 0m;
    }

    // D357: on Heidenhain the re-entry is the cycle's Q200, the safety clearance between SURFACE and CLEARANCE
    // (iTNC 530 Zyklen 670388-12 p.79: back at FMAX to the safety clearance above the previous infeed depth), 0
    // without SURFACE; on Fanuc 0, and so in a run without a machine family. Gap closed 2026-10-27 (delegated):
    // Siemens peck_clearance absent -> the control's automatic _DIS1 (SiemensAutomaticDistance).
    private static decimal DefaultPeckClearance(Controller? family, AxisPosition clearance, AxisPosition surface,
        AxisPosition start, AxisPosition depth, bool inch)
    {
        if (family == Controller.Heidenhain && clearance.Known && surface.Known)
        {
            return Math.Abs(clearance.Value - surface.Value);
        }

        if (family == Controller.Siemens && start.Known && depth.Known)
        {
            return SiemensAutomaticDistance(Math.Abs(depth.Value - start.Value), inch);
        }

        return 0m;
    }

    // _DIS1 = 0 lets the cycle compute the advance distance itself (840D sl PGH SW4.92, connector p.1088): 0.6 mm for
    // a drilling depth up to 30 mm, otherwise the depth / 50, at most 7 mm (840D sl/828D Programmierhandbuch
    // ISO-Fräsen 2012, connector p.93-94; ISO-Drehen 2012, connector p.104). In the units of the program.
    private static decimal SiemensAutomaticDistance(decimal drillingDepth, bool inch)
    {
        decimal millimetres = inch ? drillingDepth * MillimetresPerInch : drillingDepth;
        decimal distance = millimetres <= 30m ? 0.6m : Math.Min(millimetres / 50m, 7m);
        return inch ? distance / MillimetresPerInch : distance;
    }

    // D378: CLEARANCE on Fanuc and Heidenhain (G85 back to point R at feed, 30i-B B-64484EN-2 p.56-57, p.76; cycle
    // 201 to Q200 at F, iTNC 530 Zyklen 670388-12 p.81), SAFE on Siemens (CYCLE85 retracts at FR to the retract
    // plane, 840D sl Bedienhandbuch Fräsen 2013 p.308). Gap closed 2026-10-27 (delegated): a run without a machine
    // family takes CLEARANCE, the default of two of the three families.
    private static ReamFeedOut DefaultReamFeedOut(Controller? family)
    {
        return family == Controller.Siemens ? ReamFeedOut.Safe : ReamFeedOut.Clearance;
    }
}
