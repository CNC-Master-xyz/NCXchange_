using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The arcs of Klartext (controllers heidenhain.md 2; 8 rule 4; controller-mapping 2): an ARC with CENTER as CC, the
/// pole, plus C to the end point, the canonical form; an ARC with R as CR; an ARC with the sweep ANGLE as CC plus CP
/// IPA, the incremental polar angle. DR- is CW, DR+ is CCW (language 4.3).
/// </summary>
internal static class HeidenhainArcs
{
    private const string Clockwise = "DR-";
    private const string Counterclockwise = "DR+";

    /// <summary>
    /// Writes the arc of the block: CR X+2 Y+7 R+5 DR-, CC X+50 Y+50 and C X+70 Y+50 DR+, CC X+50 Y+50 and CP
    /// IPA+737,956 IZ-5,4 DR+ (controllers heidenhain.md 2; 8 rule 4).
    /// </summary>
    public static void Write(HeidenhainBlock writing)
    {
        Word verb = writing.Take("ARC") ?? throw new InvalidOperationException("The block has no ARC.");

        // The arc runs with the compensation of the program, which only an L block switches (language 4.4;
        // controllers heidenhain.md 2).
        HeidenhainCompensation.Check(writing, "The ARC");
        bool counterclockwise = verb.Value is IdentValue { Name: "CCW" };
        string direction = counterclockwise ? Counterclockwise : Clockwise;

        // D268: the path movements of a helix can be programmed in polar coordinates only, so every ARC that carries
        // a tool-axis word is written as CC plus CP IPA, with the sweep and the tool-axis travel as the incremental
        // IZ, whatever the sweep; C and CR stay for the arcs in the plane (decisions.md; controllers heidenhain.md 2,
        // 8 rule 4).
        if (ToolAxisWord(writing) is HeidenhainAxisWord travel)
        {
            WriteHelix(writing, travel, counterclockwise, direction);
            return;
        }

        // CR when the NCX block had R (heidenhain 8 rule 4).
        if (writing.Take("R") is Word radius)
        {
            WriteRadius(writing, radius, direction);
            return;
        }

        // Otherwise the pole CC and the arc about it: C to the end point, the canonical form, or CP IPA over the
        // sweep, the form of Klartext for a sweep (heidenhain 8 rule 4; virtual machine 3.2: the sweep form for
        // controllers that have it, Heidenhain CP IPA).
        if (!WritePole(writing))
        {
            return;
        }

        if (writing.Take("ANGLE") is Word angle)
        {
            WriteSweep(writing, angle, counterclockwise, direction);
        }
        else
        {
            WriteCenter(writing, direction);
        }
    }

    // The axis word of the tool axis of the active working plane, which makes the ARC a helix (language 4.3, D84);
    // null for an arc in the plane.
    private static HeidenhainAxisWord? ToolAxisWord(HeidenhainBlock writing)
    {
        string tool = HeidenhainAxes.ToolAxis(writing.After.Frame.Workplane);
        return HeidenhainAxes.Of(writing.Block).Find(axis => axis.Axis == tool);
    }

    // CC X+50 Y+50 and CP IPA-1800 IZ+5 DR-: the helix form of Klartext, the pole, the incremental total angle and
    // the height of the helix entered incrementally (D268; controllers heidenhain.md 2). The pole is the CENTER of
    // the block, or the centre the virtual machine computed for an R form (virtual machine 3.2).
    private static void WriteHelix(HeidenhainBlock writing, HeidenhainAxisWord travel, bool counterclockwise,
        string direction)
    {
        Word? radius = writing.Take("R");
        if (!(radius is null ? WritePole(writing) : WriteComputedPole(writing)))
        {
            return;
        }

        if (SweepOf(writing) is not decimal sweep)
        {
            return;
        }

        if (HeightOf(writing, travel) is not string height)
        {
            return;
        }

        // CP takes the polar angle and the height: the end point in the plane follows from the sweep, which the
        // virtual machine computed from those very words (virtual machine 3.2), so they are written with it.
        MarkPlaneWords(writing);

        string sign = counterclockwise ? "+" : "-";
        string polarAngle = "IPA" + sign + writing.Numbers.Format("ANGLE", Math.Abs(sweep), writing.Block);
        CheckSweptEnd(writing, Math.Abs(sweep), counterclockwise, polarAngle);
        WriteArc(writing, ["CP", polarAngle, height, direction]);
    }

    // The end point of the arc in the plane is written as the sweep of the CP block (D268): its words are marked as
    // written, so that nothing of the program is reported as dropped (language 2 rule 8).
    private static void MarkPlaneWords(HeidenhainBlock writing)
    {
        string[] plane = HeidenhainAxes.PlaneAxes(writing.After.Frame.Workplane);
        foreach (HeidenhainAxisWord axis in HeidenhainAxes.Of(writing.Block))
        {
            if (plane.Contains(axis.Axis))
            {
                writing.MarkWritten(axis.Word);
            }
        }
    }

    // The pole of an R form, the centre the virtual machine computed for the arc, as CC in the workpiece frame of the
    // program (virtual machine 3.2, 3.4); false when it is not known there, which is reported.
    private static bool WriteComputedPole(HeidenhainBlock writing)
    {
        IReadOnlyDictionary<string, AxisPosition>? centre = null;
        foreach (VmEvent step in writing.Step.Events)
        {
            centre = step is MotionEvent { Center: not null } motion ? motion.Center : centre;
        }

        var words = new List<string>();
        foreach (string axis in HeidenhainAxes.PlaneAxes(writing.After.Frame.Workplane))
        {
            if (centre is null || !centre.TryGetValue(axis, out AxisPosition position)
                || HeidenhainSetpos.WorkpieceCoordinate(writing.Before, axis, position) is not decimal value)
            {
                writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                    $"A helix is written as CC plus CP IPA, and the centre of the arc on {axis} is not known in the "
                    + "workpiece frame, so CC has no coordinate (D268; virtual machine 3.2, 3.4).");
                return false;
            }

            string letter = HeidenhainAxes.LetterOf(writing, axis);
            decimal written = writing.Numbers.DecimalsOf(letter) is int decimals
                ? Math.Round(value, decimals, MidpointRounding.AwayFromZero)
                : value;
            words.Add(letter + HeidenhainNumbers.Signed(writing, letter, written));
        }

        writing.Line("CC " + string.Join(" ", words));
        return true;
    }

    // The sweep of the helix: the ANGLE of the block, or the sweep the virtual machine computed from the end point
    // (virtual machine 3.2, D84); null when it is not known, which is reported.
    private static decimal? SweepOf(HeidenhainBlock writing)
    {
        if (writing.Take("ANGLE") is Word angle)
        {
            if (HeidenhainNumbers.NumberOf(angle.Value) is decimal given)
            {
                return given;
            }

            HeidenhainNumbers.ReportValue(writing, angle);
            return null;
        }

        foreach (VmEvent step in writing.Step.Events)
        {
            if (step is MotionEvent { Sweep: decimal sweep })
            {
                return sweep;
            }
        }

        writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
            "A helix is written as CC plus CP IPA, and the sweep of the arc is not known, so CP has no IPA (D268; "
            + "virtual machine 3.2).");
        return null;
    }

    // The height of the helix, the travel along the tool axis, entered incrementally: the incremental word as it
    // stands, an absolute word less the position the arc starts at (D268; controllers heidenhain.md 2); null when it
    // has no Klartext form, which is reported.
    private static string? HeightOf(HeidenhainBlock writing, HeidenhainAxisWord travel)
    {
        writing.MarkWritten(travel.Word);
        string letter = HeidenhainAxes.LetterOf(writing, travel.Axis);
        if (travel.Incremental)
        {
            string? written = HeidenhainNumbers.SignedValue(writing, letter, travel.Word.Value);
            if (written is not null)
            {
                return "I" + letter + written;
            }

            HeidenhainNumbers.ReportValue(writing, travel.Word);
            return null;
        }

        if (HeidenhainNumbers.NumberOf(travel.Word.Value) is not decimal target
            || HeidenhainSetpos.WorkpieceCoordinate(writing.Before, travel.Axis) is not decimal start)
        {
            writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                $"A helix is written as CC plus CP IPA with the height entered incrementally, and {letter} of the "
                + "block or the position the arc starts at is not known, so CP has no height (D268; controllers "
                + "heidenhain.md 2).");
            return null;
        }

        decimal height = writing.Numbers.DecimalsOf(letter) is int decimals
            ? Math.Round(target - start, decimals, MidpointRounding.AwayFromZero)
            : target - start;
        return "I" + letter + HeidenhainNumbers.Signed(writing, letter, height);
    }

    // CR X+2 Y+7 R+5 DR-: a positive radius for an arc of at most 180 degrees, a negative one for more (controllers
    // heidenhain.md 2; language 4.3, R).
    private static void WriteRadius(HeidenhainBlock writing, Word radius, string direction)
    {
        string? axes = HeidenhainAxes.Write(writing, HeidenhainAxes.Of(writing.Block));
        string? value = HeidenhainNumbers.SignedValue(writing, radius.Key, radius.Value);
        if (axes is null)
        {
            return;
        }

        if (value is null)
        {
            HeidenhainNumbers.ReportValue(writing, radius);
            return;
        }

        WriteArc(writing, ["CR", axes, "R" + value, direction]);
    }

    // CC X+50 Y+50, the pole absolute; CC IX IY incremental from the current position, the start of the arc
    // (controllers heidenhain.md 2; controller-mapping 2, CENTER:X and CENTER:IX).
    private static bool WritePole(HeidenhainBlock writing)
    {
        var center = new List<HeidenhainAxisWord>();
        foreach (Word word in writing.Block.Words)
        {
            if (word.Key == "CENTER" && word.Addr is string addr)
            {
                bool incremental = addr.Length > 1 && addr.StartsWith('I');
                center.Add(new HeidenhainAxisWord(word, incremental ? addr.Substring(1) : addr, incremental));
            }
        }

        center.Sort((first, second) => first.Incremental == second.Incremental
            ? string.CompareOrdinal(first.Axis, second.Axis)
            : first.Incremental ? 1 : -1);
        if (PoleOnTheGrid(writing, center) is string onTheGrid)
        {
            writing.Line("CC " + onTheGrid);
            return true;
        }

        string? pole = HeidenhainAxes.Write(writing, center);
        if (pole is null)
        {
            return false;
        }

        writing.Line("CC " + pole);
        return true;
    }

    // D364: the pole of literal CENTER words on the output grid, CC X Y absolute and CC IX IY from the written start:
    // the one of the nine grid points around the rounded centre whose radii from the written start and to the written
    // end agree best (ArcOnTheGrid), with CMP030 where they still differ by more than [machine] arc_radius_tolerance.
    // Null where the block or the virtual machine gives no such centre: an expression, a word off the plane, a start,
    // end or centre the virtual machine does not know in the workpiece frame, or CP IPA, whose end the control
    // computes about the pole, so that no written end point is there to agree with (D365).
    private static string? PoleOnTheGrid(HeidenhainBlock writing, List<HeidenhainAxisWord> center)
    {
        if (writing.Block.Find("ANGLE") is not null || ToolAxisWord(writing) is not null)
        {
            return null;
        }

        string[] plane = HeidenhainAxes.PlaneAxes(writing.After.Frame.Workplane);
        MotionEvent? arc = null;
        foreach (VmEvent step in writing.Step.Events)
        {
            arc = step is MotionEvent { Center: not null } motion ? motion : arc;
        }

        if (arc is null || center.Count != 2 || writing.After.Frame.Polar || writing.After.Frame.Cylinder is not null
            || center.Any(word => !plane.Contains(word.Axis) || HeidenhainNumbers.NumberOf(word.Word.Value) is null)
            || center[0].Axis == center[1].Axis)
        {
            return null;
        }

        var axes = new Dictionary<string, GridAxis>(StringComparer.Ordinal);
        foreach (HeidenhainAxisWord word in center)
        {
            if (GridAxisOf(writing, arc, word.Axis, word.Incremental) is not GridAxis axis)
            {
                return null;
            }

            axes[word.Axis] = axis;
        }

        decimal tolerance = ArcOnTheGrid.ToleranceOf(writing.Machine, writing.After.Frame.Units,
            Math.Max(axes[plane[0]].CentreStep, axes[plane[1]].CentreStep));
        GridCentre centre = ArcOnTheGrid.Choose(axes[plane[0]], axes[plane[1]], tolerance);
        if (ArcOnTheGrid.Report(centre, tolerance) is string message)
        {
            writing.Warning(DiagnosticCodes.ArcRadiiDifferOnTheGrid, message);
        }

        var words = new List<string>();
        foreach (HeidenhainAxisWord word in center)
        {
            writing.MarkWritten(word.Word);
            bool first = word.Axis == plane[0];
            decimal value = first ? centre.First : centre.Second;
            decimal start = first ? centre.WrittenStartFirst : centre.WrittenStartSecond;
            string letter = HeidenhainAxes.LetterOf(writing, word.Axis);
            words.Add((word.Incremental ? "I" : "") + letter
                + HeidenhainNumbers.Signed(writing, letter, word.Incremental ? value - start : value));
        }

        return string.Join(" ", words);
    }

    // One plane axis of the arc in the workpiece frame of the program, where CC and C take their coordinates (virtual
    // machine 3.4); null where the virtual machine does not know it there.
    private static GridAxis? GridAxisOf(HeidenhainBlock writing, MotionEvent arc, string axis, bool incremental)
    {
        if (!arc.From.TryGetValue(axis, out AxisPosition from) || !arc.To.TryGetValue(axis, out AxisPosition to)
            || !arc.Center!.TryGetValue(axis, out AxisPosition centre)
            || HeidenhainSetpos.WorkpieceCoordinate(writing.Before, axis, from) is not decimal start
            || HeidenhainSetpos.WorkpieceCoordinate(writing.Before, axis, to) is not decimal end
            || HeidenhainSetpos.WorkpieceCoordinate(writing.Before, axis, centre) is not decimal middle)
        {
            return null;
        }

        string letter = HeidenhainAxes.LetterOf(writing, axis);
        decimal step = ArcOnTheGrid.StepOf(writing.Numbers.WrittenDecimalsOf(letter));
        return new GridAxis(start, end, middle, step, step, incremental);
    }

    // C X+70 Y+50 DR+ to the end point about the pole. A block without an end point in the plane is the full circle,
    // whose end is its start (virtual machine 3.2), written with the coordinates of the start. An ARC with a
    // tool-axis word never reaches here: a helix is CC plus CP IPA (D268, WriteHelix).
    private static void WriteCenter(HeidenhainBlock writing, string direction)
    {
        List<HeidenhainAxisWord> axes = HeidenhainAxes.Of(writing.Block);
        string? end = axes.Count == 0 ? StartOf(writing) : HeidenhainAxes.Write(writing, axes);
        if (end is null)
        {
            return;
        }

        WriteArc(writing, ["C", end, direction]);
    }

    // CP IPA+737,956 IZ-5,4 DR+: the incremental polar angle in the direction of the arc, with the travel of a helix
    // (controllers heidenhain.md 2; language 4.3, ANGLE; D84).
    private static void WriteSweep(HeidenhainBlock writing, Word angle, bool counterclockwise, string direction)
    {
        if (HeidenhainNumbers.NumberOf(angle.Value) is not decimal sweep)
        {
            HeidenhainNumbers.ReportValue(writing, angle);
            return;
        }

        string? travel = HeidenhainAxes.Write(writing, HeidenhainAxes.Of(writing.Block));
        if (travel is null)
        {
            return;
        }

        string sign = counterclockwise ? "+" : "-";
        string polarAngle = "IPA" + sign + writing.Numbers.Format("ANGLE", sweep, writing.Block);
        CheckSweptEnd(writing, sweep, counterclockwise, polarAngle);
        WriteArc(writing, ["CP", polarAngle, travel, direction]);
    }

    // D365: CP IPA leaves the end to the control, which turns the written start about the written pole by the written
    // angle. Where that end lies farther from the rounded end point of the block than ROUND_LIMIT allows, or than half
    // an output step without it, the compiler has used its one lever, the decimals of ANGLE (D268, D366), and reports
    // the WARNING CMP031 with the point the control reaches, the deviation and the words. Nothing is computed for a
    // pole from an expression or a start, end or centre the virtual machine does not know in the workpiece frame.
    private static void CheckSweptEnd(HeidenhainBlock writing, decimal sweep, bool counterclockwise, string polarAngle)
    {
        string[] plane = HeidenhainAxes.PlaneAxes(writing.After.Frame.Workplane);
        MotionEvent? arc = null;
        foreach (VmEvent step in writing.Step.Events)
        {
            arc = step is MotionEvent { Center: not null } motion ? motion : arc;
        }

        if (arc is null || writing.After.Frame.Polar || writing.After.Frame.Cylinder is not null)
        {
            return;
        }

        var start = new decimal[2];
        var end = new decimal[2];
        var centre = new decimal[2];
        decimal outputStep = 0m;
        for (int index = 0; index < 2; index++)
        {
            string axis = plane[index];
            Word? incremental = writing.Block.Find("CENTER", "I" + axis);
            Word? word = incremental ?? writing.Block.Find("CENTER", axis);
            if ((word is not null && HeidenhainNumbers.NumberOf(word.Value) is null)
                || GridAxisOf(writing, arc, axis, incremental is not null) is not GridAxis known)
            {
                return;
            }

            outputStep = Math.Max(outputStep, known.PointStep);
            start[index] = ArcOnTheGrid.Written(known.Start, known.PointStep);
            end[index] = ArcOnTheGrid.Written(known.End, known.PointStep);
            centre[index] = incremental is not null
                ? start[index] + ArcOnTheGrid.Written(known.Centre - start[index], known.CentreStep)
                : ArcOnTheGrid.Written(known.Centre, known.CentreStep);
        }

        int angleDecimals = writing.Numbers.WrittenDecimalsOf("ANGLE");
        decimal written = Math.Round(sweep, angleDecimals, MidpointRounding.AwayFromZero);
        PlanePoint reached = ArcOnTheGrid.SweptEnd(new PlanePoint(start[0], start[1]),
            new PlanePoint(centre[0], centre[1]), counterclockwise ? written : -written);
        if (ArcOnTheGrid.EndBeyond(reached, new PlanePoint(end[0], end[1]), outputStep,
            writing.After.Frame.RoundLimit.Value) is decimal deviation)
        {
            writing.Warning(DiagnosticCodes.ArcEndPointOffTheGrid,
                $"CP {polarAngle} turns the written start about the pole to {plane[0]}{Text(reached.First)} "
                + $"{plane[1]}{Text(reached.Second)}, {Text(deviation)} from the rounded end point "
                + $"{plane[0]}{Text(end[0])} {plane[1]}{Text(end[1])}, all to six decimals; IPA is written with the "
                + $"{angleDecimals.ToString(CultureInfo.InvariantCulture)} decimals of ANGLE ([format] decimals, "
                + "four by default), the one lever of Klartext for a helix and a sweep (D365, D268, D366).");
        }
    }

    private static string Text(decimal value)
    {
        return Math.Round(value, 6, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
    }

    // The arc with the feed where it changed (heidenhain 8 rule 2); the compensation is the one the L blocks before it
    // switched (Write).
    private static void WriteArc(HeidenhainBlock writing, List<string> words)
    {
        words.RemoveAll(word => word.Length == 0);
        if (HeidenhainMotion.Feed(writing) is string feed)
        {
            words.Add(feed);
        }

        writing.Line(string.Join(" ", words));
    }

    // The coordinates of the start of the arc in the plane, where the full circle ends, in the workpiece frame of the
    // program, where C takes them: the position before the block read through the setpos shift of a SETPOS (virtual
    // machine 3.4; HeidenhainSetpos.WorkpieceCoordinate); null when one is not known there, which is reported.
    private static string? StartOf(HeidenhainBlock writing)
    {
        var words = new List<string>();
        foreach (string axis in HeidenhainAxes.PlaneAxes(writing.After.Frame.Workplane))
        {
            if (HeidenhainSetpos.WorkpieceCoordinate(writing.Before, axis) is not decimal start)
            {
                writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                    $"The full circle ends where it starts, and the start of {axis} is not known in the workpiece "
                    + "frame, so C has no end point (virtual machine 3.2, 3.4; controllers heidenhain.md 2).");
                return null;
            }

            string letter = HeidenhainAxes.LetterOf(writing, axis);
            decimal value = writing.Numbers.DecimalsOf(letter) is int decimals
                ? Math.Round(start, decimals, MidpointRounding.AwayFromZero)
                : start;
            words.Add(letter + HeidenhainNumbers.Signed(writing, letter, value));
        }

        return string.Join(" ", words);
    }
}
