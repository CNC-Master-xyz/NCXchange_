using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The motion of a Fanuc block (controllers fanuc.md 3, 4, 9 rule 1; controller-mapping 2; language 4.3): G0 to G3 with
/// the verb on every block, G90/G91 and the incremental addresses per word, I J K as the centre and R, the helix, F,
/// the tool vector of G43.5, and the modal words of the plane, the units, the feed mode and the compensation where the
/// source changes them; G4 as DWELL. Chamfers and roundings are FanucCorners, G16 polar coordinates
/// FanucPolarCoordinates.
/// </summary>
internal static class FanucMotion
{
    /// <summary>
    /// Reads the modal words, the dwell, the feed and the motion of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadModalWords(block);
        ReadDwell(block);
        ReadFeed(block);
        if (!block.Draft.IsRaw)
        {
            ReadMotion(block);
        }
    }

    /// <summary>
    /// Follows a word on the source-side positions: an absolute number sets the axis, an incremental one moves a known
    /// axis, an expression makes it unknown (architecture 7).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="axis">The axis word.</param>
    /// <param name="value">Its NCX value.</param>
    public static void Move(FanucBlock block, FanucAxisWord axis, Value value)
    {
        decimal? number = FanucNumbers.NumberOf(value);
        if (number is not decimal distance)
        {
            block.State.ForgetPosition(axis.Axis);
        }
        else if (!axis.Incremental)
        {
            block.State.SetPosition(axis.Axis, distance);
        }
        else if (block.State.Positions.TryGetValue(axis.Axis, out decimal position))
        {
            block.State.SetPosition(axis.Axis, position + distance);
        }
    }

    /// <summary>
    /// The two axes of the working plane and their centre words: X Y with I J under G17, Z X with K I under G18, Y Z
    /// with J K under G19, X C with I J under polar interpolation (language 4.2; controller-mapping 2; D102).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="first">The first axis of the plane.</param>
    /// <param name="second">The second axis of the plane.</param>
    /// <param name="firstCentre">The centre word of the first axis.</param>
    /// <param name="secondCentre">The centre word of the second axis.</param>
    /// <param name="outside">The centre word of the axis outside the plane.</param>
    public static void PlaneAxes(FanucBlock block, out string first, out string second, out string firstCentre,
        out string secondCentre, out string outside)
    {
        string? plane = block.Fanuc.Polar ? "POLAR" : block.State.ActiveCode(FanucModalGroups.Plane);
        first = "X";
        second = "Y";
        firstCentre = "I";
        secondCentre = "J";
        outside = "K";
        if (plane == "POLAR")
        {
            second = "C";
        }
        else if (plane == "G18")
        {
            first = "Z";
            second = "X";
            firstCentre = "K";
            secondCentre = "I";
            outside = "J";
        }
        else if (plane == "G19")
        {
            first = "Y";
            second = "Z";
            firstCentre = "J";
            secondCentre = "K";
            outside = "I";
        }
    }

    /// <summary>
    /// True when the plane holds an X axis programmed in diameters, whose I is a radius value (D60): the reader does
    /// not add a radius to a diameter.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="first">The first axis of the plane.</param>
    /// <param name="second">The second axis of the plane.</param>
    public static bool DiameterInPlane(FanucBlock block, string first, string second)
    {
        return (first == "X" || second == "X") && block.Machine.ResolveAxis("X")?.Programming == Programming.Diameter;
    }

    // The modal words the source changes: WORKPLANE from G17 to G19, UNITS from G20/G21 (G70/G71 in system C),
    // FEED_MODE from G94/G95 (G98/G99 in system A), COMP from G40 to G42, PATH_MODE from G61 (EXACT_STOP) and G64
    // (CONTINUOUS) of group 13, whose G62 and G63 stay RAW (D349); G0 to G3, G90/G91 and G15/G16 are read into
    // the verb and the words of the block (controllers fanuc.md 3, 9 rule 1; controller-mapping 1, 2).
    private static void ReadModalWords(FanucBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            string? code = word.Address == "G" ? NativeCode.Of(word) : null;
            if (code is "G0" or "G1" or "G2" or "G3" or "G15" or "G16"
                || (code is "G90" or "G91" && block.System != GcodeSystem.A))
            {
                block.MarkRead(word);
                continue;
            }

            string? key = ModalWord(code, block.System, out string value);
            if (key is null)
            {
                continue;
            }

            block.MarkRead(word);
            if (block.Fanuc.TakeChange(key, value))
            {
                block.Draft.Main.Add(key, new IdentValue(value));
            }
        }
    }

    // The modal word of a code of groups 02, 05, 06, 07 and 13 and its value; null for another code.
    private static string? ModalWord(string? code, GcodeSystem? system, out string value)
    {
        bool systemA = system == GcodeSystem.A;
        bool systemC = system == GcodeSystem.C;
        value = code switch
        {
            "G17" => "XY",
            "G18" => "ZX",
            "G19" => "YZ",
            "G20" when !systemC => "INCH",
            "G21" when !systemC => "MM",
            "G70" when systemC => "INCH",
            "G71" when systemC => "MM",
            "G94" when !systemA => "PER_MIN",
            "G95" when !systemA => "PER_REV",
            "G98" when systemA => "PER_MIN",
            "G99" when systemA => "PER_REV",
            "G40" => "OFF",
            "G41" => "LEFT",
            "G42" => "RIGHT",
            "G61" => "EXACT_STOP",
            "G64" => "CONTINUOUS",
            _ => "",
        };

        return code switch
        {
            "G17" or "G18" or "G19" => "WORKPLANE",
            "G40" or "G41" or "G42" => "COMP",
            "G61" or "G64" => "PATH_MODE",
            _ when value is "INCH" or "MM" => "UNITS",
            _ when value is "PER_MIN" or "PER_REV" => "FEED_MODE",
            _ => null,
        };
    }

    // G4 P dwells P milliseconds, G4 X and G4 U seconds (controllers fanuc.md 4; controller-mapping 1, DWELL).
    // TODO(question): controller-mapping 1 gives the unit of P "ms or s per TOML" and no key of the machine
    // configuration carries it (wave-1 question #14); P is taken in milliseconds, as fanuc 4 writes it.
    private static void ReadDwell(FanucBlock block)
    {
        if (!block.TakeCode("G4"))
        {
            return;
        }

        SourceWord? milliseconds = block.Take("P");
        SourceWord? seconds = milliseconds is null ? block.Take("X") ?? block.Take("U") : null;
        if (milliseconds?.Number is decimal duration && milliseconds.Expression is null)
        {
            block.Draft.Main.Add("DWELL", FanucNumbers.Of(duration / 1000m));
        }
        else if (seconds?.Expression is null && seconds?.ToNcxNumber() is Value time)
        {
            block.Draft.Main.Add("DWELL", time);
        }
        else
        {
            block.Draft.KeepAsRaw("G4 carries no dwell time P, X or U as a number");
        }
    }

    // F is the feed in the active feed mode, modal (language 4.3); the control keeps it for the blocks after it.
    private static void ReadFeed(FanucBlock block)
    {
        SourceWord? feed = block.Take("F");
        if (feed is null || FanucMacro.ValueOf(block, feed) is not Value value)
        {
            return;
        }

        block.Draft.Main.Add("F", value);
        block.Fanuc.ControlFeed = value;
        block.Fanuc.WrittenFeed = value;
    }

    // A block with axis words moves with the verb of the active code of group 01, which the reader writes on every
    // motion block, and the axis words as written, X= absolute and IX= incremental (controllers fanuc.md 9 rule 1,
    // controller-mapping 2).
    private static void ReadMotion(FanucBlock block)
    {
        // A G53 block moves at rapid traverse whatever group 01 holds and leaves that code as it was (fanuc 4, 9 rule
        // 3; controller-mapping 1, FRAME=MACHINE; D242).
        string? motion = block.MachineFrame ? "G0" : block.State.ActiveCode(FanucModalGroups.Motion);
        bool arc = motion is "G2" or "G3";

        // D185: under G43.5 the I J K of G0, G1, G2 and G3 are the tool vector at the end point (controller-mapping 2).
        bool vector = block.Fanuc.VectorTcpm && motion is null or "G0" or "G1" or "G2" or "G3";
        List<FanucAxisWord> axes = FanucAxes.Unread(block);
        bool centreOnly = (arc || vector) && (block.Find("I") ?? block.Find("J") ?? block.Find("K")) is not null;
        if (axes.Count == 0 && !centreOnly)
        {
            return;
        }

        // A subprogram runs with the state of its caller, and a caller continues with the state its subprogram left
        // (virtual machine 3.9): where the reader does not know a fact the block moves with, the block stays RAW
        // (FanucCallerState; D5).
        if (UnknownFact(block, axes, arc) is string fact)
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason(fact));
            return;
        }

        string? verb = VerbOf(motion, out Value verbValue);
        if (verb is null)
        {
            block.Draft.KeepAsRaw($"{motion} has no NCX motion word (controller-mapping 2)");
            return;
        }

        DraftBlock main = block.Draft.Main.WithVerb(verb, verbValue);
        if (block.MachineFrame)
        {
            main.Add("FRAME", new IdentValue("MACHINE"));
        }

        var start = new Dictionary<string, decimal>(block.State.Positions, StringComparer.Ordinal);
        if (block.State.ActiveCode(FanucModalGroups.PolarCoordinates) == "G16")
        {
            if (!FanucPolarCoordinates.Read(block, axes, main))
            {
                return;
            }

            axes = FanucAxes.Unread(block);
        }

        bool incremental = block.Incremental;
        foreach (FanucAxisWord axis in axes)
        {
            block.MarkRead(axis.Word);
            if (FanucMacro.ValueOf(block, axis.Word) is not Value read)
            {
                return;
            }

            // The line after an expanded corner starts at the end of the corner (D58).
            Value value = FanucCorners.FromCornerEnd(block, axis, read);
            main.Add(axis.Key, value);
            incremental |= axis.Incremental;
            Move(block, axis, value);
            if (block.MachineFrame)
            {
                // After a machine-frame move the axis is unknown in the workpiece frame (virtual machine 3.4, D35).
                block.State.ForgetPosition(axis.Axis);
            }
        }

        if (arc && vector)
        {
            ReadVectorArc(block, main);
        }
        else if (arc)
        {
            ReadArc(block, main, start, incremental);
            if (!block.Draft.IsRaw)
            {
                ScaleHelixFeed(block, main, start);
            }
        }
        else if (vector)
        {
            ReadToolVector(block, main);
        }

        if (!block.Draft.IsRaw)
        {
            AddModalFeed(block, main, verb);
            FanucCorners.Read(block, main, start);
        }
    }

    // The fact of the modal state a motion block moves with that the reader does not know: the code of group 01, G90 or
    // G91 for an absolute address, G15 or G16, tool center point control for I J K on a line, and for an arc or a
    // corner the plane and polar interpolation.
    private static string? UnknownFact(FanucBlock block, List<FanucAxisWord> axes, bool arc)
    {
        FanucUnknowns unknown = block.Fanuc.Unknowns;
        bool planar = arc || block.Find(",C") is not null || block.Find(",R") is not null;
        if (!block.MachineFrame && unknown.Groups.Contains(FanucModalGroups.Motion))
        {
            return "the code of group 01, G0 to G3";
        }

        if (unknown.Groups.Contains(FanucModalGroups.Distance) && axes.Exists(axis => !axis.Incremental))
        {
            return "G90 or G91";
        }

        if (unknown.Groups.Contains(FanucModalGroups.PolarCoordinates))
        {
            return "G15 or G16";
        }

        if (!arc && unknown.Tcpm && (block.Find("I") ?? block.Find("J") ?? block.Find("K")) is not null)
        {
            return "tool center point control, G43.4 or G43.5";
        }

        if (planar && unknown.Groups.Contains(FanucModalGroups.Plane))
        {
            return "the plane, G17 to G19";
        }

        return planar && unknown.Polar ? "polar interpolation, G12.1 or G13.1" : null;
    }

    // G0 is RAPID, G1 LINE, G2 ARC=CW, G3 ARC=CCW (controller-mapping 2).
    // D242: group 01 comes up and resets to G0 unless bit 0 of parameter 3402 selects G1, so a block with axis words
    // before any G0 to G3 moves at RAPID (fanuc 3, 9 rule 1; decisions.md).
    private static string? VerbOf(string? motion, out Value value)
    {
        value = motion switch
        {
            "G2" => new IdentValue("CW"),
            "G3" => new IdentValue("CCW"),
            _ => NoValue.Instance,
        };

        return motion switch
        {
            null or "G0" => "RAPID",
            "G1" => "LINE",
            "G2" or "G3" => "ARC",
            _ => null,
        };
    }

    // The centre of an arc is I J K, incremental from the start point in the plane of G17 to G19, or its radius R
    // (controllers fanuc.md 4). The reader computes the absolute centre from I J K and the start point where it knows
    // the start point, and keeps I J K as CENTER:IX where it does not (controller-mapping 2, the rows CENTER:X and
    // CENTER:IX; language 6 and examples/2.5D_FRAESEN.ncx: G3 X70. Y50. I-.534 J-19.993 is CENTER:X=50 CENTER:Y=50).
    // D208: the reader computes the absolute centre under G90 from a known start point, and keeps CENTER:IX under
    // G91, from an unknown start point and on a diameter X axis (D60) (decisions.md; controller-mapping 2). The
    // number after I, J and K is always incremental on the control, whatever G90 or G91 says, so the absolute centre
    // is the reader's own exact sum and CENTER:IX is the lossless fallback.
    private static void ReadArc(FanucBlock block, DraftBlock main, Dictionary<string, decimal> start,
        bool incremental)
    {
        PlaneAxes(block, out string first, out string second, out string firstCentre, out string secondCentre,
            out string outside);
        SourceWord? radius = block.Take("R");
        SourceWord? firstWord = block.Take(firstCentre);
        SourceWord? secondWord = block.Take(secondCentre);
        if (block.Find(outside) is not null)
        {
            block.Draft.KeepAsRaw($"{outside} is no centre word of the working plane");
            return;
        }

        // D186: with both, R takes precedence on the control and I J K are ignored (controllers fanuc.md 4), so the
        // reader writes the radius form with the WARNING RDR109; NCX keeps VM223, an ARC with CENTER and R is an ERROR.
        if (radius is not null)
        {
            if (firstWord is not null || secondWord is not null)
            {
                block.Diagnostics.Warning(block.Line, DiagnosticCodes.FanucArcRadiusOverCentre,
                    "The arc carries its radius R and its centre I J K; the control takes R and ignores I J K, and so "
                    + "does the reader (D186; controllers fanuc.md 4).");
            }

            if (FanucMacro.ValueOf(block, radius) is Value value)
            {
                main.Add("R", value);
            }

            return;
        }

        if (firstWord is null && secondWord is null)
        {
            block.Draft.KeepAsRaw("an arc needs its centre I J K or its radius R");
            return;
        }

        // D241: a centre word of the plane that is left out is 0, "I0, J0 and K0 can be omitted" (fanuc 4,
        // controller-mapping 2; decisions.md).
        Value zero = new IntegerValue(0, "0");
        Value? firstValue = firstWord is null ? zero : FanucMacro.ValueOf(block, firstWord);
        Value? secondValue = secondWord is null ? zero : FanucMacro.ValueOf(block, secondWord);
        if (firstValue is null || secondValue is null)
        {
            return;
        }

        decimal? firstOffset = FanucNumbers.NumberOf(firstValue);
        decimal? secondOffset = FanucNumbers.NumberOf(secondValue);
        bool knownStart = start.TryGetValue(first, out decimal firstStart)
            & start.TryGetValue(second, out decimal secondStart);
        bool absolute = !incremental && knownStart && firstOffset is not null && secondOffset is not null
            && !block.Fanuc.Polar && !DiameterInPlane(block, first, second);
        if (absolute)
        {
            main.Add("CENTER", first, FanucNumbers.Of(firstStart + firstOffset!.Value));
            main.Add("CENTER", second, FanucNumbers.Of(secondStart + secondOffset!.Value));
            return;
        }

        main.Add("CENTER", "I" + first, firstValue);
        main.Add("CENTER", "I" + second, secondValue);
    }

    // Under G43.5 the I J K of a line are the tool vector, TX TY TZ, all three together (controller-mapping 2, D81).
    private static void ReadToolVector(FanucBlock block, DraftBlock main)
    {
        SourceWord? toolX = block.Take("I");
        SourceWord? toolY = block.Take("J");
        SourceWord? toolZ = block.Take("K");
        if (toolX is null && toolY is null && toolZ is null)
        {
            return;
        }

        if (toolX is null || toolY is null || toolZ is null)
        {
            block.Draft.KeepAsRaw("the tool vector of G43.5 needs I, J and K (D81)");
            return;
        }

        Value? x = FanucMacro.ValueOf(block, toolX);
        Value? y = FanucMacro.ValueOf(block, toolY);
        Value? z = FanucMacro.ValueOf(block, toolZ);
        if (x is not null && y is not null && z is not null)
        {
            main.Add("TX", x).Add("TY", y).Add("TZ", z);
        }
    }

    // D354: under [format] helix_feed = "arc", the Fanuc default (parameter 1403#5 = 0), the control measures the F of
    // an arc that moves an axis outside its plane along the arc in the plane, while the F of NCX is the feed along the
    // whole path; the reader scales the F the helix moves with, written or modal, by the path length over the plane
    // length, so that a compile returns the source F within the rounding, and the next LINE or ARC writes the
    // control's feed again (AddModalFeed). Where the reader does not know the geometry or the F, the F stays as the
    // source gives it with the WARNING RDR110 (machine-config 2; controllers fanuc.md 4).
    private static void ScaleHelixFeed(FanucBlock block, DraftBlock main, Dictionary<string, decimal> start)
    {
        PlaneAxes(block, out string first, out string second, out _, out _, out _);
        double rise = 0;
        bool helix = false;
        bool known = true;
        foreach (Word word in main.Words)
        {
            // An axis word is X= or the incremental IX= (controller-mapping 2).
            string axis = block.Machine.ResolveAxis(word.Key) is null && word.Key.StartsWith('I') ? word.Key[1..]
                : word.Key;
            if (word.Addr is not null || axis == first || axis == second
                || block.Machine.ResolveAxis(axis) is not { Kind: AxisKind.Linear })
            {
                continue;
            }

            helix = true;
            if (start.TryGetValue(axis, out decimal from)
                && block.State.Positions.TryGetValue(axis, out decimal to))
            {
                rise += (double)((to - from) * (to - from));
            }
            else
            {
                known = false;
            }
        }

        HelixFeed measured = block.Machine.Format?.HelixFeed ?? HelixFeed.Arc;
        if (!helix || measured != HelixFeed.Arc)
        {
            return;
        }

        Value? feed = main.Words.Find(word => word.Key == "F")?.Value ?? block.Fanuc.ControlFeed;
        double? plane = known ? PlaneLength(block, main, start, first, second) : null;
        if (plane is not double length || length <= 0 || FanucNumbers.NumberOf(feed) is not decimal source)
        {
            block.Diagnostics.Warning(block.Line, DiagnosticCodes.FanucHelixFeedNotScaled,
                "The helix moves with the F along the arc in the plane (helix_feed = \"arc\", parameter 1403#5), and "
                + "the reader does not know its geometry or its F, so the F stays as the source gives it (D354).");
            return;
        }

        double path = Math.Sqrt((length * length) + rise);
        Value scaled = FanucNumbers.Of(FanucNumbers.Round((double)source * path / length, 3));
        main.Remove("F");
        main.Add("F", scaled);
        block.Fanuc.WrittenFeed = scaled;
    }

    // The length of the arc in the plane: the radius times the swept angle, from the centre or from R, for a start
    // and an end point the reader knows; null where it does not, under polar interpolation and on a diameter axis.
    private static double? PlaneLength(FanucBlock block, DraftBlock main, Dictionary<string, decimal> start,
        string first, string second)
    {
        if (block.Fanuc.Polar || DiameterInPlane(block, first, second)
            || !start.TryGetValue(first, out decimal startFirst) || !start.TryGetValue(second, out decimal startSecond)
            || !block.State.Positions.TryGetValue(first, out decimal endFirst)
            || !block.State.Positions.TryGetValue(second, out decimal endSecond))
        {
            return null;
        }

        double x0 = (double)startFirst;
        double y0 = (double)startSecond;
        double x1 = (double)endFirst;
        double y1 = (double)endSecond;
        bool clockwise = main.VerbValue is IdentValue { Name: "CW" };
        if (FanucNumbers.NumberOf(main.Words.Find(word => word.Key == "R")?.Value) is decimal signed)
        {
            double radius = Math.Abs((double)signed);
            double chord = Math.Sqrt(((x1 - x0) * (x1 - x0)) + ((y1 - y0) * (y1 - y0)));
            double half = Math.Asin(Math.Min(1.0, chord / (2 * radius)));
            double swept = signed < 0 ? (2 * Math.PI) - (2 * half) : 2 * half;
            return radius * swept;
        }

        double? cx = CentreOf(main, first, x0);
        double? cy = CentreOf(main, second, y0);
        if (cx is not double centreX || cy is not double centreY)
        {
            return null;
        }

        double r = Math.Sqrt(((x0 - centreX) * (x0 - centreX)) + ((y0 - centreY) * (y0 - centreY)));
        double from = Math.Atan2(y0 - centreY, x0 - centreX);
        double to = Math.Atan2(y1 - centreY, x1 - centreX);
        double sweep = clockwise ? from - to : to - from;
        while (sweep <= 1e-12)
        {
            sweep += 2 * Math.PI;
        }

        return r * sweep;
    }

    // The absolute centre coordinate of an axis: CENTER:X as written, or CENTER:IX added to the start.
    private static double? CentreOf(DraftBlock main, string axis, double start)
    {
        foreach (Word word in main.Words)
        {
            if (word.Key == "CENTER" && word.Addr == axis && FanucNumbers.NumberOf(word.Value) is decimal absolute)
            {
                return (double)absolute;
            }

            if (word.Key == "CENTER" && word.Addr == "I" + axis
                && FanucNumbers.NumberOf(word.Value) is decimal offset)
            {
                return start + (double)offset;
            }
        }

        return null;
    }

    // D185: under G43.5 the I J K of G2 and G3 are the tool vector, documented for arcs of TCP type 2, so the arc
    // takes its radius form ARC R with TX TY TZ; without R it has no centre NCX can write and stays RAW
    // (controller-mapping 2; language 4.3).
    private static void ReadVectorArc(FanucBlock block, DraftBlock main)
    {
        SourceWord? radius = block.Take("R");
        if (radius is null)
        {
            block.Draft.KeepAsRaw("under G43.5 the I J K of an arc are its tool vector, and the arc names no radius R "
                + "(D185)");
            return;
        }

        if (FanucMacro.ValueOf(block, radius) is Value value)
        {
            main.Add("R", value);
            ReadToolVector(block, main);
        }
    }

    // A Fanuc F stays for the blocks after it, the F of a cycle block too, while CYCLE_F never touches the F of NCX
    // (controllers fanuc.md 2, 6; D29): a feed motion after such a block gets the F of the control, so that it moves
    // as the source does (language 2 rule 4, explicit where controllers are implicit).
    // D218: the F of a G8x block is the control's modal feed and stays in force after G80; the reader writes it on the
    // first later feed motion whose NCX F differs, never in the CYCLE block (D29; controller-mapping 5, CYCLE_F).
    private static void AddModalFeed(FanucBlock block, DraftBlock main, string verb)
    {
        Value? feed = block.Fanuc.ControlFeed;
        if (verb == "RAPID" || main.Has("F", null) || feed is null)
        {
            return;
        }

        if (block.Fanuc.WrittenFeed is Value written && written.ToCanonical() == feed.ToCanonical())
        {
            return;
        }

        main.Add("F", feed);
        block.Fanuc.WrittenFeed = feed;
    }
}
