using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// D394: a CYLINDER section, CYLINDER=r .. CYLINDER=OFF, as CYCL DEF 14 KONTUR, CYCL DEF 27 ZYLINDER-MANTEL and CYCL
/// CALL, with its contour in a subprogram LBL n .. LBL 0 after the M30 of the program. Only a section with the shape
/// the cycle executes itself is written: positioning moves above the start point, one feed motion along the radial
/// axis to a constant depth (the plunge), a chain of LINE and ARC in the cylinder plane at that depth under COMP=LEFT
/// or RIGHT with one feed, and a retract along the radial axis back to the radius the plunge started from; several
/// passes with the same contour at increasing depth set the infeed Q10. Anything else is CMP101 and writes nothing
/// (controller-mapping 1, CYLINDER; TNC 640 Zyklen 892905-18, connector p. 333-335; iTNC 530 Benutzerhandbuch-Klartext
/// 340490, connector p. 430-431).
/// </summary>
internal static class HeidenhainCylinder
{
    // The dialect of the iTNC 530, whose contour takes the machine's rotary letter and the axis parallel to it (D146).
    private const string Itnc530 = "iTNC530";

    /// <summary>
    /// Writes CYLINDER: for CYLINDER=r the cycle of its section where the section has the shape of cycle 27, and the
    /// ERROR CMP101 otherwise; CYLINDER=OFF writes nothing, the cycle having ended the section.
    /// </summary>
    public static void Write(HeidenhainBlock writing)
    {
        if (writing.Take("CYLINDER") is not Word cylinder || cylinder.Value is IdentValue)
        {
            return;
        }

        if (Match(writing, cylinder, out string? problem) is not Shape section)
        {
            writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                $"{cylinder.ToCanonical()}: Klartext machines a cylinder surface with cycle 27, which plunges, mills "
                + $"its contour and retracts itself, and {problem}; nothing is written for it (controller-mapping 1, "
                + "CYLINDER; D394).");
            return;
        }

        WriteCycle(writing, section);
    }

    /// <summary>
    /// Tells whether a block belongs to a CYLINDER section that cycle 27 writes, after its CYLINDER=r block; such a
    /// block writes nothing of its own.
    /// </summary>
    public static bool Covers(HeidenhainBlock writing)
    {
        return writing.Cylinders.Covered.Contains(writing.Block);
    }

    /// <summary>
    /// Writes the contour subprograms of the CYLINDER sections of a program, LBL n .. LBL 0, after its M30.
    /// </summary>
    public static void WriteContours(HeidenhainBlock writing, Section program)
    {
        if (writing.Cylinders.Contours.TryGetValue(program, out List<string>? lines))
        {
            foreach (string line in lines)
            {
                writing.Line(line);
            }
        }
    }

    // The cycle in place of the CYLINDER=r block: the contour of cycle 14, the parameters of cycle 27 and the call.
    // The control then has a feed and a compensation the program does not state, and the next L writes them.
    private static void WriteCycle(HeidenhainBlock writing, Shape section)
    {
        string number = writing.LabelNumbers.CylinderOf(writing.Block)
            ?? throw new InvalidOperationException("The CYLINDER block has no label.");
        CylinderPass first = section.Passes[0];
        CylinderPass last = section.Passes[^1];
        decimal depth = last.Depth - section.Radius;
        decimal step = section.Passes.Count == 1 ? -depth : section.Radius - first.Depth;
        var values = new List<string>
        {
            Q(writing, 1, depth),
            Q(writing, 3, 0),
            Q(writing, 6, first.Start - section.Radius),
            Q(writing, 10, step),
            Q(writing, 11, first.PlungeFeed),
            Q(writing, 12, first.ContourFeed),
            Q(writing, 16, section.Radius),
            Q(writing, 17, 1),
        };
        writing.Line("CYCL DEF 14.0 KONTUR\nCYCL DEF 14.1 KONTURLABEL " + number);
        writing.Line(HeidenhainCycles.Continued("CYCL DEF 27 ZYLINDER-MANTEL", values));
        writing.Line("CYCL CALL");
        writing.MakeUnknown(HeidenhainMotion.FeedKey);
        writing.MakeUnknown(HeidenhainCompensation.WrittenKey);
        writing.Cylinders.Add(writing.Step.Section!, Contour(writing, section, number));
    }

    // Q1=-5: a Q parameter of cycle 27 with its sign, as the other cycles write theirs (HeidenhainCycles).
    private static string Q(HeidenhainBlock writing, int number, decimal value)
    {
        string address = "Q" + number.ToString(CultureInfo.InvariantCulture);
        return address + "=" + HeidenhainNumbers.Signed(writing, address, value);
    }

    // LBL n, the start point with both coordinates and RL or RR, the elements as L, CR or CC plus C, LBL 0: on the TNC
    // 640 X along the circumference and Y along the axis, on the iTNC 530 the rotary letter and the letter of the
    // axis parallel to it (D146); the C word is a length, so Q17 = 1 (D102).
    private static List<string> Contour(HeidenhainBlock writing, Shape section, string number)
    {
        CylinderPass pass = section.Passes[0];
        var lines = new List<string>
        {
            "LBL " + number,
            "L " + Point(writing, section, pass.StartCircumference, pass.StartAxial) + " "
                + (pass.Left ? "RL" : "RR"),
        };
        foreach (CylinderElement element in pass.Elements)
        {
            string end = Point(writing, section, element.Circumference, element.Axial);
            string direction = element.Ccw != section.Flipped ? "DR+" : "DR-";
            if (element.Radius is decimal radius)
            {
                lines.Add("CR " + end + " R" + HeidenhainNumbers.Signed(writing, "R", radius) + " " + direction);
            }
            else if (element.Center is (decimal centerCircumference, decimal centerAxial))
            {
                lines.Add("CC " + Point(writing, section, centerCircumference, centerAxial));
                lines.Add("C " + end + " " + direction);
            }
            else
            {
                lines.Add("L " + end);
            }
        }

        lines.Add("LBL 0");
        return lines;
    }

    private static string Point(HeidenhainBlock writing, Shape section, decimal circumference, decimal axial)
    {
        return section.CircumferenceLetter + HeidenhainNumbers.Signed(writing, section.CircumferenceLetter,
            circumference) + " " + section.AxialLetter + HeidenhainNumbers.Signed(writing, section.AxialLetter, axial);
    }

    // The section from the CYLINDER=r block to its CYLINDER=OFF when it has the shape of cycle 27; null with the
    // reason otherwise. The covered blocks are recorded where it has.
    private static Shape? Match(HeidenhainBlock writing, Word cylinder, out string? problem)
    {
        problem = null;
        BlockStep start = writing.Step;
        if (HeidenhainNumbers.NumberOf(cylinder.Value) is not decimal radius || radius <= 0)
        {
            problem = "its radius is no positive number";
            return null;
        }

        if (start.Section is not { Kind: SectionKind.Program } || writing.Block.Words.Count != 1)
        {
            problem = "the CYLINDER block stands in a subprogram or holds other words";
            return null;
        }

        string axial = start.After.Frame.CylinderAxis;
        string radial = axial == "Z" ? "X" : "Z";
        string toolAxis = HeidenhainAxes.ToolAxis(start.After.Frame.Workplane);
        if (toolAxis != radial)
        {
            problem = $"the tool axis {toolAxis} is not the radial axis {radial} of the cylinder about {axial}";
            return null;
        }

        if (Letters(writing, axial, out string circumferenceLetter, out string axialLetter, out bool flipped)
            is string lettersProblem)
        {
            problem = lettersProblem;
            return null;
        }

        var reader = new HeidenhainCylinderReader(writing, axial, radial);
        List<CylinderPass>? passes = reader.Read(start.Index + 1, out int end, out problem);
        if (passes is null || (problem = Compare(passes, radius) ?? ToolRadius(writing, passes[0].Start - radius))
            is not null)
        {
            return null;
        }

        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int index = start.Index + 1; index <= end; index++)
        {
            writing.Cylinders.Covered.Add(steps[index].Block);
        }

        return new Shape(radius, passes, circumferenceLetter, axialLetter, flipped);
    }

    // The letters of the contour: on the TNC 640 X for the circumference and Y along the axis, on the iTNC 530 the
    // rotary letter and the letter of the parallel axis (D146). Klartext turns DR+ from the first axis of the plane to
    // the second, NCX from the cylinder axis to C (D102), so the direction flips where the circumference comes first.
    private static string? Letters(HeidenhainBlock writing, string axial, out string circumference,
        out string axialLetter, out bool flipped)
    {
        circumference = "X";
        axialLetter = "Y";
        flipped = true;
        if (!string.Equals(writing.Machine.Machine.Dialect, Itnc530, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (RotaryOf(writing) is not AxisDef rotary || rotary.Letter is not string letter)
        {
            return "the workpiece holder has no rotary axis with a letter";
        }

        circumference = letter;
        axialLetter = HeidenhainAxes.LetterOf(writing, axial);
        string[] plane = HeidenhainAxes.PlaneAxes(writing.After.Frame.Workplane);
        flipped = plane[0] != axial;
        return null;
    }

    // The rotary axis of the workpiece holder: the axis its resource names, or one whose owner is the holder (D146).
    private static AxisDef? RotaryOf(HeidenhainBlock writing)
    {
        MachineConfig machine = writing.Machine;
        if (writing.After.Frame.WorkpieceHolder is not string holder)
        {
            return null;
        }

        if (machine.FindResource(holder)?.Axis is string id && machine.FindAxis(id) is { Kind: AxisKind.Rotary } named)
        {
            return named;
        }

        return machine.Axes.FirstOrDefault(axis => axis.Kind == AxisKind.Rotary && axis.Owner == holder);
    }

    // The passes of cycle 27: the same start, contour, compensation and feeds, and depths that step in by one infeed
    // Q10 from the surface to the depth Q1, the last one at most one infeed (TNC 640 Zyklen 892905-18 p. 333).
    private static string? Compare(List<CylinderPass> passes, decimal radius)
    {
        CylinderPass first = passes[0];
        decimal step = radius - first.Depth;
        if (step <= 0)
        {
            return "the plunge does not go below the cylinder radius";
        }

        for (int index = 1; index < passes.Count; index++)
        {
            CylinderPass pass = passes[index];
            if (!pass.SameContour(first))
            {
                return "its passes differ in more than the depth";
            }

            decimal full = radius - ((index + 1) * step);
            bool lastPass = index == passes.Count - 1;
            if (lastPass ? pass.Depth < full || pass.Depth >= passes[index - 1].Depth : pass.Depth != full)
            {
                return "its depths do not step in by one infeed";
            }
        }

        return null;
    }

    // Q6, the set-up clearance, must be greater than the tool radius (TNC 640 Zyklen 892905-18 p. 334): CMP101 where
    // the tool table says it is not, the WARNING CMP131 where it gives no radius (D394).
    private static string? ToolRadius(HeidenhainBlock writing, decimal clearance)
    {
        if (clearance <= 0)
        {
            return "the plunge starts on or below the cylinder surface";
        }

        ToolRef? tool = writing.After.LastHolder is string holder
            && writing.After.Holders.TryGetValue(holder, out HolderSnapshot? state)
            ? state.SpindleTool
            : null;
        decimal? toolRadius = tool is ToolRef held ? writing.ToolRadius(held) : null;
        if (toolRadius is decimal known)
        {
            return clearance > known
                ? null
                : $"the plunge starts {clearance.ToString(CultureInfo.InvariantCulture)} above the surface, not more "
                    + $"than the tool radius {known.ToString(CultureInfo.InvariantCulture)}, which the set-up "
                    + "clearance Q6 must exceed";
        }

        writing.Warning(DiagnosticCodes.HeidenhainCylinderClearanceUnchecked,
            $"Cycle 27 takes the set-up clearance Q6={clearance.ToString(CultureInfo.InvariantCulture)} from where the "
            + "plunge starts, which must be greater than the tool radius, and the tool table gives no radius for the "
            + "tool, so it is not checked (D394).");
        return null;
    }

    // A CYLINDER section of the shape of cycle 27 with the letters of its contour.
    private sealed record Shape(decimal Radius, List<CylinderPass> Passes, string CircumferenceLetter,
        string AxialLetter, bool Flipped);
}
