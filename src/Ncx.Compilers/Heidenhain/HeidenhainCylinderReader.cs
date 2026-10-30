using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// One pass of a CYLINDER section that cycle 27 writes (D394): the start point on the developed surface and the radius
/// the plunge starts from, the depth it plunges to with its feed, and the contour with its feed and its side.
/// </summary>
internal sealed record CylinderPass(decimal StartAxial, decimal StartCircumference, decimal Start, decimal Depth,
    decimal PlungeFeed, decimal ContourFeed, bool Left, List<CylinderElement> Elements)
{
    /// <summary>
    /// Tells whether two passes differ in their depth alone.
    /// </summary>
    public bool SameContour(CylinderPass other)
    {
        return StartAxial == other.StartAxial && StartCircumference == other.StartCircumference
            && Start == other.Start && PlungeFeed == other.PlungeFeed && ContourFeed == other.ContourFeed
            && Left == other.Left && Elements.SequenceEqual(other.Elements);
    }
}

/// <summary>
/// An element of the contour of cycle 27: its end point on the developed surface, and for an arc its radius or its
/// centre, and its direction in the NCX cylinder plane, the cylinder axis then C (D102).
/// </summary>
internal sealed record CylinderElement(decimal Axial, decimal Circumference, decimal? Radius, CylinderPoint? Center,
    bool Ccw);

/// <summary>
/// A point on the developed surface of cycle 27: along the circumference, a length (D102), and along the axis.
/// </summary>
internal sealed record CylinderPoint(decimal Circumference, decimal Axial);

/// <summary>
/// Reads the blocks of a CYLINDER section into the passes of cycle 27 (D394): each pass is positioning RAPIDs, one
/// LINE along the radial axis alone (the plunge), a chain of LINE and ARC in the cylinder plane under COMP=LEFT or
/// RIGHT with one feed, and a RAPID or LINE along the radial axis alone back to the radius the plunge started from (the
/// retract), up to CYLINDER=OFF. Every coordinate is absolute and a number; any other word, block or form is a reason.
/// </summary>
internal sealed class HeidenhainCylinderReader
{
    private readonly HeidenhainBlock _writing;
    private readonly string _axial;
    private readonly string _radial;

    private decimal? _axialAt;
    private decimal? _circumferenceAt;
    private decimal? _radialAt;
    private int _index;

    /// <summary>
    /// A reader of the section after the CYLINDER block being written.
    /// </summary>
    /// <param name="writing">The CYLINDER block being written.</param>
    /// <param name="axial">The NCX letter of the cylinder axis (D146).</param>
    /// <param name="radial">The NCX letter of the radial axis.</param>
    public HeidenhainCylinderReader(HeidenhainBlock writing, string axial, string radial)
    {
        _writing = writing;
        _axial = axial;
        _radial = radial;
    }

    private IReadOnlyList<BlockStep> Steps => _writing.LookAhead.Steps;

    private Block Current => Steps[_index].Block;

    /// <summary>
    /// The passes from a step to the CYLINDER=OFF that ends the section; null with the reason where the section does
    /// not have the shape of cycle 27.
    /// </summary>
    /// <param name="from">The step after the CYLINDER=r block.</param>
    /// <param name="end">The step of the CYLINDER=OFF.</param>
    /// <param name="problem">Why the section does not have the shape; null where it has.</param>
    public List<CylinderPass>? Read(int from, out int end, out string? problem)
    {
        _index = from;
        end = -1;
        var passes = new List<CylinderPass>();
        while (true)
        {
            problem = Check();
            if (problem is not null)
            {
                return null;
            }

            if (Current.Has("CYLINDER"))
            {
                end = _index;
                problem = passes.Count == 0 ? "the section holds no plunge, contour and retract" : null;
                return problem is null ? passes : null;
            }

            if (ReadPass(out problem) is not CylinderPass pass)
            {
                return null;
            }

            passes.Add(pass);
        }
    }

    // Positioning RAPIDs above the start point, the plunge, the contour and the retract.
    private CylinderPass? ReadPass(out string? problem)
    {
        while ((problem = Check()) is null && Verb() == "RAPID")
        {
            Position();
            _index++;
        }

        problem = Check();
        if (problem is not null)
        {
            return null;
        }

        if (_axialAt is not decimal startAxial || _circumferenceAt is not decimal startCircumference
            || _radialAt is not decimal start)
        {
            problem = "the start point of the contour or the radius of the plunge is not known before the plunge";
            return null;
        }

        if (Verb() != "LINE" || !OnlyRadial() || Steps[_index].After.Motion.Feed is not decimal plungeFeed)
        {
            problem = "no LINE along the radial axis alone with a known feed plunges after the positioning";
            return null;
        }

        Position();
        decimal depth = _radialAt!.Value;
        _index++;
        if (ReadContour(out bool left, out decimal contourFeed, out problem) is not List<CylinderElement> elements)
        {
            return null;
        }

        problem = Check();
        if (problem is not null || Verb() is not ("RAPID" or "LINE") || !OnlyRadial())
        {
            problem ??= "no RAPID or LINE along the radial axis alone retracts after the contour";
            return null;
        }

        Position();
        if (_radialAt != start)
        {
            problem = "the retract does not return to the radius the plunge started from, where cycle 27 ends";
            return null;
        }

        _index++;
        return new CylinderPass(startAxial, startCircumference, start, depth, plungeFeed, contourFeed, left,
            elements);
    }

    // The chain of LINE and ARC in the cylinder plane: COMP=LEFT or RIGHT on its first block and on none after it, one
    // feed for all of it.
    private List<CylinderElement>? ReadContour(out bool left, out decimal feed, out string? problem)
    {
        left = false;
        feed = 0;
        var elements = new List<CylinderElement>();
        while ((problem = Check()) is null && Verb() is "LINE" or "ARC" && !Names(_radial))
        {
            Block block = Current;
            Word? comp = block.Find("COMP");
            if (elements.Count == 0)
            {
                if (comp?.Value is not IdentValue { Name: "LEFT" or "RIGHT" } side)
                {
                    problem = "the contour does not start with COMP=LEFT or RIGHT, which cycle 27 needs, since it "
                        + "offsets the contour itself";
                    return null;
                }

                left = side.Name == "LEFT";
            }
            else if (comp is not null)
            {
                problem = "the compensation changes within the contour";
                return null;
            }

            if (Steps[_index].After.Motion.Feed is not decimal blockFeed || (elements.Count > 0 && blockFeed != feed))
            {
                problem = "the contour has more than one feed or one that is not a number";
                return null;
            }

            feed = blockFeed;
            if (Element(block, out problem) is not CylinderElement element)
            {
                return null;
            }

            elements.Add(element);
            _index++;
        }

        if (problem is null && elements.Count == 0)
        {
            problem = "no contour of LINE and ARC in the cylinder plane follows the plunge";
        }

        return problem is null ? elements : null;
    }

    // A LINE or an ARC of the contour, its end point absolute; an ARC with R or with CENTER of both axes.
    private CylinderElement? Element(Block block, out string? problem)
    {
        problem = null;
        Position();
        decimal axial = _axialAt!.Value;
        decimal circumference = _circumferenceAt!.Value;
        if (block.Verb?.Key == "LINE")
        {
            return new CylinderElement(axial, circumference, null, null, false);
        }

        bool ccw = block.Verb?.Value is IdentValue { Name: "CCW" };
        if (block.Find("R") is Word radius && HeidenhainNumbers.NumberOf(radius.Value) is decimal r)
        {
            return new CylinderElement(axial, circumference, r, null, ccw);
        }

        if (block.Find("CENTER", _axial) is Word centerAxial && block.Find("CENTER", "C") is Word centerCircumference
            && HeidenhainNumbers.NumberOf(centerAxial.Value) is decimal ca
            && HeidenhainNumbers.NumberOf(centerCircumference.Value) is decimal cc)
        {
            return new CylinderElement(axial, circumference, null, new CylinderPoint(cc, ca), ccw);
        }

        problem = "an arc of the contour has neither R nor the CENTER of both axes of the cylinder plane";
        return null;
    }

    // The words a block of the section may hold: its verb, the three axes as absolute numbers, F and COMP, and for an
    // ARC its R and CENTER; nothing else, no block skip, and no other section.
    private string? Check()
    {
        if (_index >= Steps.Count || Steps[_index].Section != Steps[_index - 1].Section)
        {
            return "the section has no CYLINDER=OFF in the same program";
        }

        Block block = Current;
        if (block.Has("CYLINDER"))
        {
            return block.Words.Count == 1 && block.Find("CYLINDER")?.Value is IdentValue
                ? null
                : "the CYLINDER=OFF block holds other words";
        }

        foreach (Word word in block.Words)
        {
            if (ReferenceEquals(word, block.Verb) && word.Key is "RAPID" or "LINE" or "ARC")
            {
                continue;
            }

            bool allowed = word.Key switch
            {
                "F" or "COMP" => word.Addr is null,
                "R" => block.Verb?.Key == "ARC" && word.Addr is null,
                "CENTER" => block.Verb?.Key == "ARC" && (word.Addr == _axial || word.Addr == "C"),
                _ => word.Addr is null && (word.Key == _axial || word.Key == "C" || word.Key == _radial)
                    && HeidenhainNumbers.NumberOf(word.Value) is not null,
            };
            if (!allowed)
            {
                return $"the section holds {word.ToCanonical()}, which the shape of the cycle does not take";
            }
        }

        if (block.Verb is null)
        {
            return "the section holds a block without a motion";
        }

        return block.Find("COMP")?.Value is IdentValue { Name: "OFF" } && block.Verb.Key == "ARC"
            ? "an arc of the section switches the compensation off"
            : null;
    }

    private string? Verb()
    {
        return Current.Verb?.Key;
    }

    private bool Names(string axis)
    {
        return Current.Find(axis) is not null;
    }

    // A block along the radial axis alone: the radial word and none of the plane.
    private bool OnlyRadial()
    {
        return Names(_radial) && !Names(_axial) && !Names("C");
    }

    // The position after the block from its words (all absolute numbers, Check).
    private void Position()
    {
        Block block = Current;
        _axialAt = Number(block, _axial) ?? _axialAt;
        _circumferenceAt = Number(block, "C") ?? _circumferenceAt;
        _radialAt = Number(block, _radial) ?? _radialAt;
    }

    private static decimal? Number(Block block, string key)
    {
        return block.Find(key) is Word word ? HeidenhainNumbers.NumberOf(word.Value) : null;
    }
}
