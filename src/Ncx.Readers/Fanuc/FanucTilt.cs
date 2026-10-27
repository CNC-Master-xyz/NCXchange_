using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The tilted working plane of G68.2 (controllers fanuc.md 4; controller-mapping 1, TILT, MOVE; language 4.2; D82):
/// G68.2 X Y Z I J K with Euler angles (P0) or roll, pitch and yaw (P1) is a SHIFT to its origin and a TILT with the
/// spatial angles A B C, entries of the chain (FanucChain); a G53.1 in the next block positions the rotary axes,
/// MOVE=TURN; other P forms, and G68.1, stay RAW.
/// </summary>
internal static class FanucTilt
{
    // Computed angles keep the decimals of the source angles, at least these (FanucNumbers.Round).
    private const int LeastDecimals = 3;

    // Below this the cosine of B is 0, and A and C turn about the same axis.
    private const double GimbalLimit = 1e-9;

    /// <summary>
    /// Reads G68.2, G53.1 and G68.1 of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        // D246: G68.1 is the lathe form of the G68 coordinate rotation, a rotation about the axis direction I J K
        // through the centre Xp Yp Zp, and names no rotary axis position, so it is no source form of TILT_AXIS and
        // stays RAW in 1.0 (fanuc 4; controller-mapping 1; decisions.md, D82 clarified).
        if (block.TakeCode("G68.1"))
        {
            block.Draft.KeepAsRaw("G68.1, the 3D coordinate conversion, is kept as RAW");
            return;
        }

        // G53.1 after G68.2 positions the rotary axes to the plane: MOVE=TURN on the TILT (controller-mapping 1).
        if (block.FindCode("G53.1") is SourceWord turn)
        {
            block.MarkRead(turn);
            if (!block.Fanuc.TurnsOfTilts.Contains(block.Line))
            {
                block.Draft.KeepAsRaw(
                    "G53.1 positions the rotary axes to the G68.2 of the block before it, and there is none");
                return;
            }
        }

        if (block.TakeCode("G68.2"))
        {
            ReadTilt(block);
            return;
        }

        // D245: G68.3, the plane by the tool axis direction, and G68.4, the additive form, stay RAW; they tilt the
        // plane of the control, so G54 to G59 under them stay RAW and their G69 too (FanucFrames.KeepRawModes;
        // B-64484EN-2 p.27-28).
        if (block.TakeCode("G68.3") || block.TakeCode("G68.4"))
        {
            block.Draft.KeepAsRaw("G68.3 and G68.4, the tilted working plane by the tool axis and its additive form, "
                + "are kept as RAW (D245)");
        }
    }

    // D245 (b): a second G68.2 replaces the first, its plane defined in the workpiece system (B-64484EN p.916-918):
    // SHIFT=RESET, or TILT=RESET where the old one had no origin, then the new SHIFT and TILT (FanucChain.TryChange);
    // the additive form is G68.4, which stays RAW. A G68.2 while the 2D or 3D G68 is on raises PS5462 (B-64484EN
    // p.948-950) and stays RAW. On a chain of callers the reader does not know it stays RAW too, since it may replace
    // a plane of the caller (D225). A G68.2 the reader keeps as RAW still tilts the plane on the control, which
    // FanucFrames.KeepRawModes records.
    private static void ReadTilt(FanucBlock block)
    {
        FanucChain chain = block.Fanuc.Chain;
        if (chain.Active("G68") || chain.Conversions > 0)
        {
            block.Draft.KeepAsRaw("G68.2 may not stand while the coordinate rotation G68 is on, the control raises "
                + "PS5462 (D245)");
            return;
        }

        if (!chain.Known)
        {
            block.Draft.KeepAsRaw(FanucFrames.UnknownChain("G68.2"));
            return;
        }

        SourceWord? convention = block.Take("P");
        SourceWord? first = block.Take("I");
        SourceWord? second = block.Take("J");
        SourceWord? third = block.Take("K");
        if (first?.Number is not decimal i || second?.Number is not decimal j || third?.Number is not decimal k)
        {
            block.Draft.KeepAsRaw("G68.2 needs its angles I, J and K as numbers");
            return;
        }

        int decimals = LeastDecimals;
        foreach (SourceWord word in new[] { first, second, third })
        {
            decimals = Math.Max(decimals, FanucNumbers.DecimalsOf(word));
        }

        decimal a;
        decimal b;
        decimal c;
        switch (convention?.Number ?? 0m)
        {
            case 0m:
                SpatialAngles((double)i, (double)j, (double)k, out double spatialA, out double spatialB,
                    out double spatialC);
                a = FanucNumbers.Round(spatialA, decimals);
                b = FanucNumbers.Round(spatialB, decimals);
                c = FanucNumbers.Round(spatialC, decimals);
                break;
            case 1m:
                // Roll, pitch and yaw turn about X, Y and Z of the frame in that order, the spatial angles of TILT.
                a = i;
                b = j;
                c = k;
                break;
            default:
                block.Draft.KeepAsRaw($"G68.2 P{convention!.Text} is kept as RAW (controller-mapping 1, TILT)");
                return;
        }

        // The origin X Y Z of the plane is a SHIFT before the TILT, in the frame where G68.2 stands (language 4.2,
        // D31).
        var origin = new List<Word>();
        foreach (FanucAxisWord axis in FanucAxes.Unread(block))
        {
            block.MarkRead(axis.Word);
            if (FanucMacro.ValueOf(block, axis.Word) is not Value value)
            {
                return;
            }

            if (FanucNumbers.NumberOf(value) != 0m)
            {
                origin.Add(new Word { Key = axis.Axis, Value = value });
            }
        }

        var tilt = new FanucChainEntry("TILT", "G68.2",
        [
            new Word { Key = "A", Value = FanucNumbers.Of(a) },
            new Word { Key = "B", Value = FanucNumbers.Of(b) },
            new Word { Key = "C", Value = FanucNumbers.Of(c) },
        ]);
        // The chain without the active G68.2, the one the new G68.2 replaces, and the new plane after it.
        var blocks = new List<DraftBlock>();
        List<FanucChainEntry> desired = FanucFrames.WithoutRotations(chain);
        if (origin.Count == 0)
        {
            desired.Add(tilt);
        }
        else if (LocalShiftAtTheEnd(desired, origin) is FanucChainEntry local)
        {
            // The origin and the G52 shift directly in front of it are two shifts of one frame: they stand added in
            // one SHIFT, so that the chain holds one shift and its RESET means the same under both readings of
            // language 4.2 (wave-1 question #95); the G69 writes the G52 shift again (FanucFrames).
            desired[^1] = new FanucChainEntry("SHIFT", "G68.2", Sum(local.Words, origin), local.Words);
            desired.Add(tilt);
        }
        else
        {
            desired.Add(new FanucChainEntry("SHIFT", "G68.2", origin));
            desired.Add(tilt);
        }

        if (!chain.TryChange(desired, blocks))
        {
            block.Draft.KeepAsRaw("the G68.2 would unwind the chain of transforms with a RESET that the documents "
                + "read in two ways (language 4.2)");
            return;
        }

        chain.SetNative("G68.2", false);
        if (block.Fanuc.TiltsTurned.Contains(block.Line))
        {
            blocks[^1].Add("MOVE", new IdentValue("TURN"));
        }

        FanucFrames.Write(block, blocks);
        block.Fanuc.ForgetPositions();
    }

    // The SHIFT of a G52 at the end of a chain that holds no other shift, where every word of the origin is a number;
    // null otherwise.
    private static FanucChainEntry? LocalShiftAtTheEnd(List<FanucChainEntry> chain, List<Word> origin)
    {
        if (chain.Count == 0 || chain[^1] is not { Kind: "SHIFT", Owner: "G52" } last)
        {
            return null;
        }

        foreach (FanucChainEntry entry in chain)
        {
            if (entry.Kind == "SHIFT" && !ReferenceEquals(entry, last))
            {
                return null;
            }
        }

        foreach (Word word in origin)
        {
            if (FanucNumbers.NumberOf(word.Value) is null)
            {
                return null;
            }
        }

        return last;
    }

    // Two shifts of one frame added axis by axis, the axes in the order they come.
    private static List<Word> Sum(IReadOnlyList<Word> first, List<Word> second)
    {
        var axes = new List<string>();
        var sums = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (Word word in new List<Word>(first).Concat(second))
        {
            if (!sums.ContainsKey(word.Key))
            {
                axes.Add(word.Key);
            }

            sums[word.Key] = sums.GetValueOrDefault(word.Key) + (FanucNumbers.NumberOf(word.Value) ?? 0m);
        }

        var sum = new List<Word>(axes.Count);
        foreach (string axis in axes)
        {
            sum.Add(new Word { Key = axis, Value = FanucNumbers.Of(sums[axis]) });
        }

        return sum;
    }

    // The Euler angles of G68.2 P0 turn about Z by I, about the new X by J and about the new Z by K; the spatial angles
    // of TILT turn about X by A, then about Y by B, then about Z by C of the frame where the word stands (language
    // 4.2), so R = Rz(I) Rx(J) Rz(K) = Rz(C) Ry(B) Rx(A).
    private static void SpatialAngles(double i, double j, double k, out double a, out double b, out double c)
    {
        double sinI = Math.Sin(Radians(i));
        double cosI = Math.Cos(Radians(i));
        double sinJ = Math.Sin(Radians(j));
        double cosJ = Math.Cos(Radians(j));
        double sinK = Math.Sin(Radians(k));
        double cosK = Math.Cos(Radians(k));
        double r00 = (cosI * cosK) - (sinI * cosJ * sinK);
        double r01 = -(cosI * sinK) - (sinI * cosJ * cosK);
        double r10 = (sinI * cosK) + (cosI * cosJ * sinK);
        double r11 = -(sinI * sinK) + (cosI * cosJ * cosK);
        double r20 = sinJ * sinK;
        double r21 = sinJ * cosK;
        double r22 = cosJ;
        b = Math.Asin(Math.Clamp(-r20, -1.0, 1.0));
        if (Math.Abs(Math.Cos(b)) > GimbalLimit)
        {
            a = Math.Atan2(r21, r22);
            c = Math.Atan2(r10, r00);
        }
        else
        {
            a = 0;
            c = Math.Atan2(-r01, r11);
        }

        a = Degrees(a);
        b = Degrees(b);
        c = Degrees(c);
    }

    private static double Radians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static double Degrees(double radians)
    {
        return radians * 180.0 / Math.PI;
    }
}
