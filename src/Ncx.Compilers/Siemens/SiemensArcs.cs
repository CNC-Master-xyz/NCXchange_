using System.Globalization;
using Ncx.Core.Geometry;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The arcs of a SINUMERIK block (controllers siemens.md 3, 12 rule 2; controller-mapping 2, CENTER, R and ANGLE;
/// language 4.3; D84): the end point, I=AC() for an absolute center and I J K for an incremental one, CR= for the R
/// form, AR= for a sweep below one turn, and TURN= with the end point for a sweep beyond, or on an older 840D the sweep
/// split into full turns and a rest.
/// </summary>
internal static class SiemensArcs
{
    // A full turn in degrees.
    private const decimal FullTurn = 360m;

    // The dialect of the older 840D, which takes no TURN= (controllers siemens.md 12 rule 2; machine-config 1).
    private const string Older840D = "840D";

    /// <summary>
    /// True on a side whose datum runs Z the other way, for an arc or a compensation in a plane that holds Z: with Z
    /// negated the arc turns the other way round and the compensation lies on the other side (machine-config 5,
    /// SUB_frame; D57).
    /// </summary>
    // D298: the holder frame is the main spindle's frame mirrored in Z, not a right-handed frame, so the compiler
    // negates Z and, in the planes that hold Z, turns G2 into G3 and G41 into G42, exactly as a control does under
    // its own Z mirror; nothing else changes (decisions.md D298; machine-config 5, SUB_frame).
    public static bool Mirrored(SiemensBlock write)
    {
        // Under TRACYL the plane is G19, which holds Z, and NCX's Z is the Z of the transformation only on a cylinder
        // about Z (D146).
        if (write.After.Frame.Cylinder is not null)
        {
            return SiemensAxes.IsZNegated(write) && write.After.Frame.CylinderAxis == "Z";
        }

        return SiemensAxes.IsZNegated(write) && write.After.Frame.Workplane != Workplane.XY;
    }

    /// <summary>
    /// True under CYLINDER=n, where an arc of NCX is written with the other motion code (D146).
    /// </summary>
    // D146: the cylinder plane of NCX turns ARC=CCW from the cylinder axis toward C (virtual machine 3.2;
    // Plane.CylinderAbout). TRACYL works in G19 (Arbeitsvorbereitung 2013, connector p.352: N50 G19), the plane of the
    // 2nd and 3rd geometry axes, whose G3 turns from Y toward Z (Programmierhandbuch Grundlagen 2013, connector p.154:
    // "G19: Arbeitsebene Y/Z, Zustellrichtung X, Ebenenanwahl 2. - 3. Geometrieachse"), and Y is the circumference,
    // the C word, and Z the cylinder axis (Arbeitsvorbereitung 2013, p.352-353). The same arc in the same coordinates
    // turns from C toward the cylinder axis on the control, so ARC=CCW is G2 and ARC=CW is G3.
    public static bool TurnedByTheCylinderPlane(SiemensBlock write)
    {
        return write.After.Frame.Cylinder is not null;
    }

    /// <summary>
    /// Writes the words of an ARC block after its motion code.
    /// </summary>
    public static void Write(SiemensBlock write, List<SiemensAxisWord> axes)
    {
        Block block = write.Block;
        if (block.Find("ANGLE") is Word angle)
        {
            write.Written(angle);
            WriteSweep(write, axes, angle);
            return;
        }

        SiemensMotion.WriteAxes(write, axes);
        WriteCenter(write);
        if (block.Find("R") is Word radius)
        {
            // CR= for the R form, negative for the arc over 180 degrees as in NCX (controllers siemens.md 3; language
            // 4.3, R).
            write.Written(radius);
            if (SiemensExpressions.ValueText(write, radius.Value, "R") is string value)
            {
                write.Main.Word(SiemensLine.ArcRank, "CR=" + value);
            }
        }
    }

    // A sweep below one turn is AR= with the center; a sweep of a turn or more the end point with TURN= and the full
    // turns before it, or on an older 840D full turns and a rest (controllers siemens.md 3, 12 rule 2; D84).
    private static void WriteSweep(SiemensBlock write, List<SiemensAxisWord> axes, Word angle)
    {
        if (angle.Value is not (IntegerValue or DecimalValue))
        {
            write.Error(DiagnosticCodes.SiemensArcNotWritable,
                $"{angle.ToCanonical()}: a sweep from an expression gives neither AR= nor TURN= a value the compiler "
                + "knows (language 4.3, ANGLE; D84).");
            write.WrittenAll();
            return;
        }

        decimal sweep = decimal.Parse(angle.Value.ToCanonical(), CultureInfo.InvariantCulture);
        if (sweep < FullTurn && ArcOf(write.Step) is MotionEvent swept && SweptEndBeyond(write, swept, sweep))
        {
            // D365: AR= with the centre leaves the end to the control, which would miss the rounded end point by
            // more than ROUND_LIMIT or half an output step; the lever of Siemens is the end-point form with the centre.
            foreach (SiemensAxisWord axis in axes)
            {
                write.Written(axis.Word);
            }

            AddPoint(write, swept.To, swept, withToolAxis: true);
            AddAbsoluteCenter(write, swept);
            write.Written("CENTER");
            return;
        }

        if (sweep < FullTurn)
        {
            SiemensMotion.WriteAxes(write, axes);
            WriteCenter(write);
            write.Main.Word(SiemensLine.ArcRank, "AR=" + write.Format("ANGLE", sweep));
            return;
        }

        if (write.After.Frame.Cylinder is not null)
        {
            // D146: TURN= takes the end point and the center from the events of the virtual machine, which keeps them
            // in the cylinder plane; that form is not written under TRACYL.
            write.Error(DiagnosticCodes.SiemensCylinderNotWritable,
                $"{angle.ToCanonical()} under CYLINDER: a sweep of a turn or more needs TURN= with the end point of "
                + "the developed surface, which this compiler does not write under TRACYL; split the sweep below one "
                + "turn (controllers siemens.md 3; D84, D146).");
            write.WrittenAll();
            return;
        }

        MotionEvent? arc = ArcOf(write.Step);
        if (arc?.Center is null || !Known(arc.From) || !Known(arc.To) || !Known(arc.Center))
        {
            write.Error(DiagnosticCodes.SiemensArcNotWritable,
                $"{angle.ToCanonical()}: the virtual machine does not know the start, the center or the end of the "
                + "arc, which TURN= needs with the end point (controllers siemens.md 3; D84).");
            write.WrittenAll();
            return;
        }

        int turns = (int)Math.Ceiling(sweep / FullTurn) - 1;
        if (string.Equals(write.Machine.Machine.Dialect, Older840D, StringComparison.OrdinalIgnoreCase) && turns > 0)
        {
            WriteTurns(write, axes, arc, sweep, turns);
            return;
        }

        foreach (SiemensAxisWord axis in axes)
        {
            write.Written(axis.Word);
        }

        AddPoint(write, arc.To, arc, withToolAxis: true);
        AddAbsoluteCenter(write, arc);
        write.Written("CENTER");
        if (turns > 0)
        {
            write.Main.Word(SiemensLine.ArcRank, "TURN=" + turns.ToString(CultureInfo.InvariantCulture));
        }
    }

    // An older 840D takes no TURN=: every full turn is an arc back to its start, the tool axis advanced by its share of
    // the sweep, the first in the main line of the block and the others after it, then the rest to the end point
    // (controllers siemens.md 12 rule 2).
    private static void WriteTurns(SiemensBlock write, List<SiemensAxisWord> axes, MotionEvent arc, decimal sweep,
        int turns)
    {
        foreach (SiemensAxisWord axis in axes)
        {
            write.Written(axis.Word);
        }

        write.Written("CENTER");
        string toolAxis = Plane(write).Tool;
        decimal? toolStart = arc.From.TryGetValue(toolAxis, out AxisPosition from) && from.Known ? from.Value : null;
        decimal? toolEnd = arc.To.TryGetValue(toolAxis, out AxisPosition to) && to.Known ? to.Value : null;
        bool helix = toolStart is decimal first && toolEnd is decimal last && first != last;
        for (int turn = 1; turn <= turns; turn++)
        {
            var point = new Dictionary<string, AxisPosition>(arc.From);
            if (helix)
            {
                decimal travel = (toolEnd!.Value - toolStart!.Value) * FullTurn * turn / sweep;
                point[toolAxis] = new AxisPosition(toolStart.Value + travel, from.Frame, Known: true);
            }

            var words = new List<string>(PointWords(write, point, helix));
            words.AddRange(AbsoluteCenterWords(write, arc));
            if (turn == 1)
            {
                foreach (string word in words)
                {
                    write.Main.Word(SiemensLine.ArcRank, word);
                }
            }
            else
            {
                write.Following.Add(string.Join(" ", words));
            }
        }

        var rest = new List<string>(PointWords(write, arc.To, helix));
        rest.AddRange(AbsoluteCenterWords(write, arc));
        write.Following.Add(string.Join(" ", rest));
    }

    // The center words as the block gives them: CENTER:X absolute as I=AC(), CENTER:IX incremental as I (controllers
    // siemens.md 3, 12 rule 2; controller-mapping 2, CENTER).
    private static void WriteCenter(SiemensBlock write)
    {
        if (WriteCenterOnTheGrid(write))
        {
            return;
        }

        foreach (Word word in write.Block.Words)
        {
            if (word.Key != "CENTER" || word.Addr is not string address)
            {
                continue;
            }

            bool incremental = address.Length > 1 && address[0] == 'I';
            string axis = incremental ? address.Substring(1) : address;
            // D146: under TRACYL the center takes the letter of its axis on the developed surface, J for C (Y) and K
            // for the cylinder axis (Z).
            string written = write.After.Frame.Cylinder is not null ? SiemensAxes.LetterOf(write, axis) : axis;
            if (CenterLetter(written) is not string letter)
            {
                continue;
            }

            write.Written(word);
            decimal factor = incremental
                ? (axis == "Z" && SiemensAxes.IsZNegated(write) ? -1m : 1m)
                : SiemensAxes.WordFactor(write, axis);
            string? value = Scaled(write, word.Value, SiemensAxes.DecimalsAddress(write, written), factor);
            if (value is not null)
            {
                write.Main.Word(SiemensLine.ArcRank,
                    incremental ? SiemensAxes.Address(letter, value, "", word.Value is ExprValue)
                        : SiemensAxes.Address(letter, value, "AC", expression: false));
            }
        }
    }

    // D364: the center of literal CENTER words on the output grid, I=AC() on the grid of the plane addresses and I on
    // the grid of the offset from the written start: the one of the nine grid points around the rounded center whose
    // radii from the written start and to the written end agree best (ArcOnTheGrid), with CMP030 where they still
    // differ by more than [machine] arc_radius_tolerance. False where the block or the virtual machine gives no such
    // center: an expression, a center under TRACYL, a start, end or center the virtual machine does not know, or AR=
    // with the center, whose end the control computes, so that no written end point is there to agree with (D365).
    private static bool WriteCenterOnTheGrid(SiemensBlock write)
    {
        (string First, string Second, string Tool) plane = Plane(write);
        MotionEvent? arc = ArcOf(write.Step);
        if (write.After.Frame.Cylinder is not null || arc?.Center is null || write.Block.Find("ANGLE") is not null)
        {
            return false;
        }

        var words = new Dictionary<string, Word>(StringComparer.Ordinal);
        foreach (Word word in write.Block.Words)
        {
            if (word.Key != "CENTER" || word.Addr is not string address)
            {
                continue;
            }

            string axis = address.Length > 1 && address[0] == 'I' ? address.Substring(1) : address;
            if ((axis != plane.First && axis != plane.Second) || word.Value is not (IntegerValue or DecimalValue))
            {
                return false;
            }

            words[axis] = word;
        }

        if (!words.TryGetValue(plane.First, out Word? firstWord)
            || !words.TryGetValue(plane.Second, out Word? secondWord)
            || CentreOnTheGrid(write, arc, plane.First, plane.Second, words) is not GridCentre centre)
        {
            return false;
        }

        WriteGridWord(write, plane.First, firstWord, centre.First, centre.WrittenStartFirst);
        WriteGridWord(write, plane.Second, secondWord, centre.Second, centre.WrittenStartSecond);
        return true;
    }

    // The centre on the grid of its written form, CMP030 reported; null where the virtual machine does not know the
    // start, the end or the centre in the plane.
    private static GridCentre? CentreOnTheGrid(SiemensBlock write, MotionEvent arc, string first, string second,
        Dictionary<string, Word>? words)
    {
        if (arc.Center is null || !KnownIn(arc.Center, first, second) || !KnownIn(arc.From, first, second)
            || !KnownIn(arc.To, first, second))
        {
            return null;
        }

        GridAxis firstAxis = GridAxisOf(write, arc, first, words?[first]);
        GridAxis secondAxis = GridAxisOf(write, arc, second, words?[second]);
        decimal tolerance = ArcOnTheGrid.ToleranceOf(write.Machine, write.After.Frame.Units,
            Math.Max(firstAxis.CentreStep, secondAxis.CentreStep));
        GridCentre centre = ArcOnTheGrid.Choose(firstAxis, secondAxis, tolerance);
        if (ArcOnTheGrid.Report(centre, tolerance) is string message)
        {
            write.Warning(DiagnosticCodes.ArcRadiiDifferOnTheGrid, message);
        }

        return centre;
    }

    // One plane axis: the point written with its state factor (a diameter, D60), an absolute centre the same way, an
    // incremental one in radius values; null word for the absolute I=AC() of TURN=.
    private static GridAxis GridAxisOf(SiemensBlock write, MotionEvent arc, string axis, Word? word)
    {
        bool incremental = word?.Addr is string address && address.Length > 1 && address[0] == 'I';
        string letter = SiemensAxes.LetterOf(write, axis);
        decimal step = ArcOnTheGrid.StepOf(write.Numbers.WrittenDecimalsOf(SiemensAxes.DecimalsAddress(write, letter)));
        decimal factor = Math.Abs(SiemensAxes.StateFactor(write, axis));
        return new GridAxis(arc.From[axis].Value, arc.To[axis].Value, arc.Center![axis].Value, step / factor,
            incremental ? step : step / factor, incremental);
    }

    // I=AC() of the absolute centre in the state factor, I of the incremental one from the written start.
    private static void WriteGridWord(SiemensBlock write, string axis, Word word, decimal centre, decimal start)
    {
        write.Written(word);
        string letter = CenterLetter(axis)!;
        string address = SiemensAxes.DecimalsAddress(write, SiemensAxes.LetterOf(write, axis));
        bool incremental = word.Addr!.Length > 1 && word.Addr[0] == 'I';
        if (incremental)
        {
            decimal sign = axis == "Z" && SiemensAxes.IsZNegated(write) ? -1m : 1m;
            write.Main.Word(SiemensLine.ArcRank,
                SiemensAxes.Address(letter, write.Format(address, (centre - start) * sign), "", false));
            return;
        }

        write.Main.Word(SiemensLine.ArcRank, SiemensAxes.Address(letter,
            write.Format(address, centre * SiemensAxes.StateFactor(write, axis)), "AC", expression: false));
    }

    private static bool KnownIn(IReadOnlyDictionary<string, AxisPosition> points, string first, string second)
    {
        return points.TryGetValue(first, out AxisPosition one) && one.Known
            && points.TryGetValue(second, out AxisPosition two) && two.Known;
    }

    // D365: true where AR= with the centre of the block, as written, turns the written start to a point that lies
    // farther from the rounded end point than ROUND_LIMIT allows, or than half an output step without it. False where
    // the check has nothing to compute: a centre from an expression, the cylinder plane, or a start, end or centre the
    // virtual machine does not know.
    private static bool SweptEndBeyond(SiemensBlock write, MotionEvent arc, decimal sweep)
    {
        (string First, string Second, string Tool) plane = Plane(write);
        if (write.After.Frame.Cylinder is not null || arc.Center is null || !KnownIn(arc.Center, plane.First,
            plane.Second) || !KnownIn(arc.From, plane.First, plane.Second) || !KnownIn(arc.To, plane.First,
            plane.Second))
        {
            return false;
        }

        var centre = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var start = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var end = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal step = 0m;
        foreach (string axis in new[] { plane.First, plane.Second })
        {
            bool incremental = write.Block.Find("CENTER", "I" + axis) is not null;
            Word? word = write.Block.Find("CENTER", incremental ? "I" + axis : axis);
            if (word?.Value is not (IntegerValue or DecimalValue))
            {
                return false;
            }

            decimal pointStep = ArcOnTheGrid.StepOf(write.Numbers.WrittenDecimalsOf(
                SiemensAxes.DecimalsAddress(write, SiemensAxes.LetterOf(write, axis))));
            decimal factor = Math.Abs(SiemensAxes.StateFactor(write, axis));
            step = Math.Max(step, pointStep / factor);
            start[axis] = ArcOnTheGrid.Written(arc.From[axis].Value, pointStep / factor);
            end[axis] = ArcOnTheGrid.Written(arc.To[axis].Value, pointStep / factor);
            centre[axis] = incremental
                ? start[axis] + ArcOnTheGrid.Written(arc.Center[axis].Value - start[axis], pointStep)
                : ArcOnTheGrid.Written(arc.Center[axis].Value, pointStep / factor);
        }

        decimal written = Math.Round(sweep, write.Numbers.WrittenDecimalsOf("ANGLE"), MidpointRounding.AwayFromZero);
        decimal signed = arc.Direction == ArcDirection.Counterclockwise ? written : -written;
        PlanePoint reached = ArcOnTheGrid.SweptEnd(new PlanePoint(start[plane.First], start[plane.Second]),
            new PlanePoint(centre[plane.First], centre[plane.Second]), signed);
        return ArcOnTheGrid.EndBeyond(reached, new PlanePoint(end[plane.First], end[plane.Second]), step,
            write.After.Frame.RoundLimit.Value) is not null;
    }

    // The value of a center word with its factor.
    private static string? Scaled(SiemensBlock write, Value value, string address, decimal factor)
    {
        return value switch
        {
            IntegerValue integer => factor == 1m ? write.Format(address, integer.Number)
                : write.Format(address, integer.Number * factor),
            DecimalValue number => factor == 1m ? write.Format(address, number.Number)
                : write.Format(address, number.Number * factor),
            ExprValue expression when SiemensExpressions.Text(write, expression) is string text => factor switch
            {
                1m => text,
                -1m => "-(" + text + ")",
                2m => "(" + text + ")*2",
                _ => "(" + text + ")/2",
            },
            _ => null,
        };
    }

    // I, J, K are the centers on X, Y, Z (controllers siemens.md 3).
    private static string? CenterLetter(string axis)
    {
        return axis switch
        {
            "X" => "I",
            "Y" => "J",
            "Z" => "K",
            _ => null,
        };
    }

    // The end point of the plane axes, and of the tool axis where the arc is a helix, from the state of the virtual
    // machine.
    private static void AddPoint(SiemensBlock write, IReadOnlyDictionary<string, AxisPosition> point, MotionEvent arc,
        bool withToolAxis)
    {
        bool helix = withToolAxis && arc.From.TryGetValue(Plane(write).Tool, out AxisPosition from)
            && point.TryGetValue(Plane(write).Tool, out AxisPosition to) && from.Known && to.Known
            && from.Value != to.Value;
        foreach (string word in PointWords(write, point, helix))
        {
            write.Main.Word(SiemensLine.AxisRank, word);
        }
    }

    private static List<string> PointWords(SiemensBlock write, IReadOnlyDictionary<string, AxisPosition> point,
        bool withToolAxis)
    {
        (string First, string Second, string Tool) plane = Plane(write);
        var names = new List<string> { plane.First, plane.Second };
        if (withToolAxis)
        {
            names.Add(plane.Tool);
        }

        var words = new List<string>();
        foreach (string name in names)
        {
            if (!point.TryGetValue(name, out AxisPosition position) || !position.Known)
            {
                continue;
            }

            string letter = SiemensAxes.LetterOf(write, name);
            string value = write.Format(SiemensAxes.DecimalsAddress(write, letter),
                position.Value * SiemensAxes.StateFactor(write, name));
            words.Add(SiemensAxes.Address(letter, value, "", expression: false));
        }

        return words;
    }

    private static void AddAbsoluteCenter(SiemensBlock write, MotionEvent arc)
    {
        foreach (string word in AbsoluteCenterWords(write, arc))
        {
            write.Main.Word(SiemensLine.ArcRank, word);
        }
    }

    // The center of the virtual machine as I=AC() J=AC() (controllers siemens.md 3).
    private static List<string> AbsoluteCenterWords(SiemensBlock write, MotionEvent arc)
    {
        (string First, string Second, string Tool) plane = Plane(write);
        var words = new List<string>();
        if (CentreOnTheGrid(write, arc, plane.First, plane.Second, words: null) is GridCentre centre)
        {
            words.Add(SiemensAxes.Address(CenterLetter(plane.First)!, write.Format(SiemensAxes.DecimalsAddress(
                write, plane.First), centre.First * SiemensAxes.StateFactor(write, plane.First)), "AC", false));
            words.Add(SiemensAxes.Address(CenterLetter(plane.Second)!, write.Format(SiemensAxes.DecimalsAddress(
                write, plane.Second), centre.Second * SiemensAxes.StateFactor(write, plane.Second)), "AC", false));
            return words;
        }

        foreach (string name in new[] { plane.First, plane.Second })
        {
            if (arc.Center is null || !arc.Center.TryGetValue(name, out AxisPosition center) || !center.Known
                || CenterLetter(name) is not string letter)
            {
                continue;
            }

            string value = write.Format(SiemensAxes.DecimalsAddress(write, name),
                center.Value * SiemensAxes.StateFactor(write, name));
            words.Add(SiemensAxes.Address(letter, value, "AC", expression: false));
        }

        return words;
    }

    // The plane axes and the tool axis of the working plane (language 4.2, WORKPLANE).
    private static (string First, string Second, string Tool) Plane(SiemensBlock write)
    {
        return write.After.Frame.Workplane switch
        {
            Workplane.ZX => ("Z", "X", "Y"),
            Workplane.YZ => ("Y", "Z", "X"),
            _ => ("X", "Y", "Z"),
        };
    }

    private static MotionEvent? ArcOf(BlockStep step)
    {
        foreach (VmEvent vmEvent in step.Events)
        {
            if (vmEvent is MotionEvent motion && motion.Verb == Verb.Arc)
            {
                return motion;
            }
        }

        return null;
    }

    private static bool Known(IReadOnlyDictionary<string, AxisPosition> positions)
    {
        foreach (AxisPosition position in positions.Values)
        {
            if (!position.Known)
            {
                return false;
            }
        }

        return positions.Count > 0;
    }
}
