namespace Ncx.Readers.Siemens;

/// <summary>
/// The hole patterns that call the modal cycle of MCALL at every position (controllers siemens.md 7, 11 rule 5;
/// controller-mapping 5, repeats): HOLES1(SPCA, SPCO, STA1, FDIS, DBH, NUM), a row from the reference point SPCA SPCO
/// at the angle STA1, its first hole FDIS from the point and the next ones DBH apart; HOLES2(CPA, CPO, RAD, STA1, INDA,
/// NUM), a circle about CPA CPO of radius RAD from the angle STA1 in steps of INDA, the full circle divided by NUM
/// where INDA is 0; CYCLE801, a grid or a frame of N1 by N2 positions L1 and L2 apart from X0 Y0 at the base angle a0;
/// CYCLE802, up to nine free positions (D341). The reader expands them into one CYCLE_CALL per hole.
/// </summary>
internal static class SiemensPatterns
{
    // The most holes the reader expands.
    private const int MaxHoles = 9999;

    /// <summary>
    /// True where the reader can expand the pattern. The modal cycle is not part of this test, so that a look-ahead
    /// can ask the question before the cycle is read (controllers siemens.md 7; D259, D341).
    /// </summary>
    /// <param name="pattern">The pattern word with its arguments.</param>
    public static bool CanExpand(SourceWord pattern)
    {
        List<string> arguments = SiemensArguments.Split(pattern.Text);
        return pattern.Address switch
        {
            "CYCLE801" => GridPoints(arguments) is not null,
            "CYCLE802" => PositionPoints(arguments) is not null,
            _ => RowOrCirclePoints(pattern.Address, arguments) is not null,
        };
    }

    /// <summary>
    /// Reads a hole pattern into the calls of the modal cycle.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="pattern">The pattern word with its arguments.</param>
    public static void Read(SiemensBlock block, SourceWord pattern)
    {
        switch (pattern.Address)
        {
            case "CYCLE801":
                ReadGrid(block, pattern);
                return;
            case "CYCLE802":
                ReadPositions(block, pattern);
                return;
            default:
                ReadRowOrCircle(block, pattern);
                return;
        }
    }

    // HOLES1 and HOLES2, the row and the circle (controllers siemens.md 7).
    private static void ReadRowOrCircle(SiemensBlock block, SourceWord pattern)
    {
        block.MarkRead(pattern);
        SiemensCycle? cycle = block.Facts.ModalCycle;
        List<string> arguments = SiemensArguments.Split(pattern.Text);
        if (cycle is null
            || RowOrCirclePoints(pattern.Address, arguments) is not List<(decimal First, decimal Second)> points)
        {
            block.Draft.KeepAsRaw($"{pattern.Address} calls the modal cycle of MCALL at positions of numbers; the "
                + "reader expands it only so (controllers siemens.md 7)");
            return;
        }

        WriteCalls(block, cycle, points);
    }

    // The holes of HOLES1 (a row) and of HOLES2 (a circle): six numeric arguments and a whole number of holes in
    // range, null where the reader cannot expand the pattern (controllers siemens.md 7; D259).
    private static List<(decimal First, decimal Second)>? RowOrCirclePoints(string address, List<string> arguments)
    {
        if (arguments.Count < 6)
        {
            return null;
        }

        var numbers = new List<decimal>();
        int decimals = SiemensNumbers.LeastDecimals;
        foreach (string argument in arguments.GetRange(0, 6))
        {
            if (SiemensNumbers.NumberOf(SiemensNumbers.Parse(argument)) is not decimal number)
            {
                return null;
            }

            numbers.Add(number);
            decimals = Math.Max(decimals, SiemensNumbers.DecimalsOf(argument));
        }

        if (Whole(numbers[5]) is not int count || count < 1 || count > MaxHoles)
        {
            return null;
        }

        List<double> values = numbers.ConvertAll(number => (double)number);
        double step = address == "HOLES2" && values[4] == 0 ? 360.0 / count : values[4];
        var points = new List<(decimal First, decimal Second)>();
        for (int hole = 0; hole < count; hole++)
        {
            double x;
            double y;
            if (address == "HOLES1")
            {
                double distance = values[3] + (hole * step);
                x = values[0] + (distance * Math.Cos(values[2] * Math.PI / 180));
                y = values[1] + (distance * Math.Sin(values[2] * Math.PI / 180));
            }
            else
            {
                double angle = (values[3] + (hole * step)) * Math.PI / 180;
                x = values[0] + (values[2] * Math.Cos(angle));
                y = values[1] + (values[2] * Math.Sin(angle));
            }

            points.Add((SiemensNumbers.Round(x, decimals), SiemensNumbers.Round(y, decimals)));
        }

        return points;
    }

    // CYCLE801(_SPCA, _SPCO, _STA, _DIS1, _DIS2, _NUM1, _NUM2, _VARI, _UMODE, _ANG1, _ANG2, _HIDE, _NSP, _DMODE): the
    // grid or frame of N1 columns and N2 rows, L1 and L2 apart, from the reference point X0 Y0 at the base angle a0 to
    // the first axis; _VARI ones digit 0 is the grid and 1 the frame, which works the border positions only (D341).
    private static void ReadGrid(SiemensBlock block, SourceWord pattern)
    {
        block.MarkRead(pattern);
        SiemensCycle? cycle = block.Facts.ModalCycle;
        List<string> arguments = SiemensArguments.Split(pattern.Text);
        if (cycle is null || GridPoints(arguments) is not List<(decimal First, decimal Second)> points)
        {
            block.Draft.KeepAsRaw($"{pattern.Address} works a grid or a frame of positions of numbers in the working "
                + "plane, without shear angles, without suppressed positions and without a mode; the reader expands "
                + "it only so (controllers siemens.md 7; D341)");
            return;
        }

        WriteCalls(block, cycle, points);
    }

    // The positions of a CYCLE801 grid or frame, null where the reader cannot expand the call. The shear angles _ANG1
    // and _ANG2 and the suppressed positions of _HIDE are kept RAW: D341 names them without a geometry, and _HIDE
    // counts the positions in an order no page gives. _UMODE, _NSP and _DMODE, whose ones digit carries the plane, are
    // kept RAW where they are given and not 0, as the drilling cycles keep RAW every call that sets a mode integer
    // (SiemensCycles.ModeSet): the expansion works in the modal working plane.
    private static List<(decimal First, decimal Second)>? GridPoints(List<string> arguments)
    {
        decimal?[] values = Numbers(arguments, 0, 8);
        int decimals = Decimals(arguments, 0, 5);
        int? columns = Whole(values[5]);
        int? rows = Whole(values[6]);
        int variant = Whole(values[7]) ?? 0;
        if (AnyMissing(values, 0, 5) || columns is not int across || rows is not int down
            || across < 1 || down < 1 || (long)across * down > MaxHoles || variant % 10 > 1
            || !IsEmpty(arguments, 9) || !IsEmpty(arguments, 10) || !IsEmpty(arguments, 11)
            || !NoMode(arguments, 8) || !NoMode(arguments, 12) || !NoMode(arguments, 13))
        {
            return null;
        }

        double angle = (double)values[2]!.Value * Math.PI / 180;
        var points = new List<(decimal First, decimal Second)>();
        for (int row = 0; row < down; row++)
        {
            for (int column = 0; column < across; column++)
            {
                if (variant % 10 == 1 && column > 0 && column < across - 1 && row > 0 && row < down - 1)
                {
                    continue;
                }

                double along = column * (double)values[3]!.Value;
                double across2 = row * (double)values[4]!.Value;
                double x = (double)values[0]!.Value + (along * Math.Cos(angle)) - (across2 * Math.Sin(angle));
                double y = (double)values[1]!.Value + (along * Math.Sin(angle)) + (across2 * Math.Cos(angle));
                points.Add((SiemensNumbers.Round(x, decimals), SiemensNumbers.Round(y, decimals)));
            }
        }

        return points;
    }

    // CYCLE802(_XA, _YA, X0, Y0 ... X8, Y8, _VARI, _UMODE, _DMODE, S_ABA, S_AB0 ... S_AB8): up to nine free positions.
    private static void ReadPositions(SiemensBlock block, SourceWord pattern)
    {
        block.MarkRead(pattern);
        SiemensCycle? cycle = block.Facts.ModalCycle;
        List<string> arguments = SiemensArguments.Split(pattern.Text);
        if (cycle is null || PositionPoints(arguments) is not List<(decimal First, decimal Second)> points)
        {
            block.Draft.KeepAsRaw($"{pattern.Address} works up to nine absolute positions of numbers, one after "
                + "another, in the working plane and without rotary axis positions; the reader expands it only so "
                + "(controllers siemens.md 7; D341)");
            return;
        }

        WriteCalls(block, cycle, points);
    }

    // The positions of a CYCLE802, null where the reader cannot expand the call. _XA and _YA are nine-digit codes, 1
    // absolute and 2 incremental per position, and the first programmed position is always absolute; an incremental
    // code, a _VARI other than 0 (the spindle clamping and rotary-axis combinations of JobShop) and a rotary-axis
    // position are kept RAW, since D341 gives no rule that reads them (D341). _UMODE and _DMODE, whose ones digit
    // carries the plane, are kept RAW the same way as those of CYCLE801. An unneeded position may be left out, and the
    // manual leaves out the last ones: a position that follows a left-out one is a hole the reader cannot place, so
    // such a call stays RAW rather than losing it.
    private static List<(decimal First, decimal Second)>? PositionPoints(List<string> arguments)
    {
        var points = new List<(decimal First, decimal Second)>();
        bool broken = false;
        bool left = false;
        for (int position = 0; position < 9; position++)
        {
            int index = 2 + (position * 2);
            if (IsEmpty(arguments, index) && IsEmpty(arguments, index + 1))
            {
                left = true;
                continue;
            }

            decimal?[] pair = Numbers(arguments, index, 2);
            if (left || pair[0] is not decimal first || pair[1] is not decimal second)
            {
                broken = true;
                break;
            }

            points.Add((first, second));
        }

        bool absolute = Code(arguments, 0) && Code(arguments, 1);
        bool rotary = false;
        for (int index = 23; index < 33; index++)
        {
            rotary |= !IsEmpty(arguments, index);
        }

        int variant = Whole(Numbers(arguments, 20, 1)[0]) ?? 0;
        if (points.Count == 0 || broken || !absolute || rotary || variant != 0
            || !NoMode(arguments, 21) || !NoMode(arguments, 22))
        {
            return null;
        }

        return points;
    }

    // One CYCLE_CALL per position of the pattern, in the order the parameters give them.
    // TODO(question): the pattern cycles work out the order of the positions themselves from the position reached
    // before the call (D341, the closing note of its answer; HOLES1 decides from the actual position whether it works
    // the row from the first or the last hole), and no document says which order NCX writes; the calls stand in the
    // order the parameters give, as they do for HOLES1 and HOLES2.
    private static void WriteCalls(SiemensBlock block, SiemensCycle cycle,
        List<(decimal First, decimal Second)> points)
    {
        var plane = SiemensPlane.Of(block.Facts.WorkingPlane);
        for (int index = 0; index < points.Count; index++)
        {
            SiemensDraftBlock call = index == 0 ? block.Draft.Main : new SiemensDraftBlock();
            call.WithVerb("CYCLE_CALL").Add(plane.First, SiemensNumbers.Of(points[index].First))
                .Add(plane.Second, SiemensNumbers.Of(points[index].Second));
            if (index > 0)
            {
                block.Draft.After.Add(call);
            }

            block.State.SetPosition(plane.First, points[index].First);
            block.State.SetPosition(plane.Second, points[index].Second);
        }

        if (cycle.ReturnPlane is decimal returnPlane)
        {
            block.State.SetPosition(cycle.Axis, returnPlane);
        }
    }

    // The values of a run of positions as numbers; null where a position is empty, missing or no number.
    private static decimal?[] Numbers(List<string> arguments, int from, int count)
    {
        var values = new decimal?[count];
        for (int index = 0; index < count; index++)
        {
            values[index] = from + index < arguments.Count && arguments[from + index].Length > 0
                ? SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[from + index]))
                : null;
        }

        return values;
    }

    // The most decimals a run of positions is written with, at least the three of a computed value (D18, language 2).
    private static int Decimals(List<string> arguments, int from, int to)
    {
        int decimals = SiemensNumbers.LeastDecimals;
        for (int index = from; index <= to && index < arguments.Count; index++)
        {
            decimals = Math.Max(decimals, SiemensNumbers.DecimalsOf(arguments[index]));
        }

        return decimals;
    }

    // True where a value of the run is missing, empty or no number.
    private static bool AnyMissing(decimal?[] values, int from, int to)
    {
        for (int index = from; index <= to; index++)
        {
            if (values[index] is null)
            {
                return true;
            }
        }

        return false;
    }

    // True where the position is empty or missing, so the cycle takes its default (controllers siemens.md 7).
    private static bool IsEmpty(List<string> arguments, int index)
    {
        return index >= arguments.Count || arguments[index].Length == 0;
    }

    // True where a mode position asks for nothing the expansion does not do: it is left out or 0. _DMODE carries the
    // plane in its ones digit and _UMODE and _NSP are reserved, and the reader works in the modal working plane, so a
    // mode that is given and not 0 keeps the call RAW, as the drilling cycles keep RAW every call that sets a mode
    // integer (controllers siemens.md 7; D341).
    private static bool NoMode(List<string> arguments, int index)
    {
        return IsEmpty(arguments, index) || Numbers(arguments, index, 1)[0] == 0m;
    }

    // True where the nine-digit code of _XA or _YA says every position is absolute, or is not given at all.
    private static bool Code(List<string> arguments, int index)
    {
        if (IsEmpty(arguments, index))
        {
            return true;
        }

        return SiemensNumbers.WholeNumber(arguments[index]) == 111111111;
    }

    private static int? Whole(decimal? value)
    {
        return value is decimal number && number == decimal.Truncate(number) && Math.Abs(number) < int.MaxValue
            ? (int)number
            : null;
    }
}
