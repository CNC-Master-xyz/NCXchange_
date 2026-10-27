using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The rotations and mirrors of a Fanuc block (controllers fanuc.md 4; D245): G68 and G69 as ROTATE, G51.1 and
/// G50.1 as MIRROR, and the modes a block kept as RAW switches on or off.
/// </summary>
internal static partial class FanucFrames
{
    // G68 X Y R rotates the plane by R about the centre X Y, G69 cancels it and the tilted plane of G68.2 (controllers
    // fanuc.md 4; controller-mapping 1, ROTATE and TILT). D245 (a): ROTATE turns about the current origin (language
    // 4.2), and a G68 without X Y turns about the tool position (B-64304EN-2 p.191), so the reader writes ROTATE where
    // the centre is the origin of the plane, given or known as the tool position; otherwise, and without R (parameter
    // 5410), the block stays RAW and the control rotates natively. (b): a second 2D G68 under absolute R replaces the
    // first, ROTATE=RESET plus ROTATE; under G91 parameter 5400#0 may add R, so it stays RAW (B-64304EN-2 p.190,
    // 196-197). A G68 with I J K is the 3D conversion, kept as RAW: a second one accumulates, a third raises PS5043
    // (B-64484EN p.342-344). G68 while G68.2 is on raises PS5462 (B-64484EN p.948-950).
    private static void ReadRotation(FanucBlock block)
    {
        if (block.TakeCode("G68"))
        {
            ReadG68(block);
            return;
        }

        if (block.TakeCode("G69"))
        {
            ReadCancel(block);
        }
    }

    private static void ReadG68(FanucBlock block)
    {
        FanucChain chain = block.Fanuc.Chain;
        SourceWord? angle = block.Take("R");
        // The I J K of the 3D conversion, all read so that none is left unread (D245).
        bool spatial = block.Take("I") is not null | block.Take("J") is not null | block.Take("K") is not null;

        List<FanucAxisWord> centre = FanucAxes.Unread(block);
        foreach (FanucAxisWord axis in centre)
        {
            block.MarkRead(axis.Word);
        }

        if (chain.Active("G68.2"))
        {
            block.Draft.KeepAsRaw("G68 may not stand while the tilted working plane of G68.2 is on, the control raises "
                + "PS5462 (D245)");
            return;
        }

        if (spatial)
        {
            block.Draft.KeepAsRaw(chain.Conversions >= 2
                ? "a third three-dimensional coordinate conversion G68 raises PS5043 on the control (D245)"
                : "the three-dimensional coordinate conversion G68 with I J K is kept as RAW (D245)");
            return;
        }

        if (chain.Conversions > 0)
        {
            block.Draft.KeepAsRaw("a G68 without I J K while the three-dimensional coordinate conversion is on is kept "
                + "as RAW (D245)");
            return;
        }

        // D225, D245 (b): on a chain of callers the reader does not know, the G68 may replace a rotation of the caller,
        // which the reader cannot cut.
        if (!chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain("G68"));
            return;
        }

        if (angle is null)
        {
            block.Draft.KeepAsRaw("G68 names no angle R, and the control turns by the angle of parameter 5410 (D245)");
            return;
        }

        if (CentreAwayFromTheOrigin(block, centre) is string reason)
        {
            block.Draft.KeepAsRaw(reason);
            return;
        }

        if (FanucMacro.ValueOf(block, angle) is not Value value)
        {
            return;
        }

        var rotation = new FanucChainEntry("ROTATE", "G68", [new Word { Key = "ROTATE", Value = value }]);
        bool replaces = chain.Holds("G68") || chain.HoldsNative("G68");
        if (replaces && block.Incremental)
        {
            block.Draft.KeepAsRaw("a second G68 under G91 adds R to the active angle or not by parameter 5400#0, "
                + "which the reader does not know (D245)");
            return;
        }

        var blocks = new List<DraftBlock>();
        if (chain.Holds("G68"))
        {
            var desired = new List<FanucChainEntry>();
            foreach (FanucChainEntry entry in chain.Entries)
            {
                desired.Add(entry.Owner == "G68" ? rotation : entry);
            }

            if (!chain.TryChange(desired, blocks))
            {
                block.Draft.KeepAsRaw("the second G68 would unwind the chain of transforms with a RESET that the "
                    + "documents read in two ways (language 4.2)");
                return;
            }
        }
        else
        {
            chain.Append(rotation, blocks);
        }

        chain.SetNative("G68", false);
        Write(block, blocks);
        block.Fanuc.ForgetPositions();
    }

    // D245 (a): the centre of G68 is the X Y of its plane where the block gives it, and the tool position where it
    // does not (B-64304EN-2 p.191); ROTATE turns about the origin of the plane, so either must be 0. The reason the
    // block stays RAW, or null where the centre is the origin.
    private static string? CentreAwayFromTheOrigin(FanucBlock block, List<FanucAxisWord> centre)
    {
        string[] plane = PlaneAxes(block);
        foreach (FanucAxisWord axis in centre)
        {
            if (axis.Word.Number != 0m)
            {
                return "G68 turns about the point X Y and ROTATE about the origin (language 4.2)";
            }
        }

        foreach (string axis in plane)
        {
            bool given = centre.Exists(word => word.Axis == axis);
            if (!given && (!block.State.Positions.TryGetValue(axis, out decimal position) || position != 0m))
            {
                return $"G68 without {axis} turns about the tool position, which the reader does not know as the "
                    + "origin of the plane here (language 4.2; D245)";
            }
        }

        return null;
    }

    // The two axes of the plane of G17, G18 or G19, the plane of G17 on a mill and of G18 on a lathe without one
    // (controllers fanuc.md 3).
    private static string[] PlaneAxes(FanucBlock block)
    {
        return block.State.ActiveCode(FanucModalGroups.Plane) switch
        {
            "G17" => ["X", "Y"],
            "G18" => ["Z", "X"],
            "G19" => ["Y", "Z"],
            _ => block.IsLathe ? ["Z", "X"] : ["X", "Y"],
        };
    }

    // G69 cancels G68 and the tilted plane of G68.2, its origin with it (controllers fanuc.md 4): the chain keeps the
    // entries of the other functions, and of a SHIFT that holds a G52 shift and the origin of the G68.2 together the
    // G52 shift (FanucTilt). It also ends the 3D conversions (D245). A G69 that cancels a G68 or G68.2 the reader kept
    // as RAW stays RAW, so that the control gets it back, and so does a G69 on a chain the reader does not know;
    // KeepRawModes writes the end of the entries the chain holds beside it.
    private static void ReadCancel(FanucBlock block)
    {
        FanucChain chain = block.Fanuc.Chain;
        if (chain.HoldsNative("G68") || chain.HoldsNative("G68.2") || chain.Conversions > 0)
        {
            block.Draft.KeepAsRaw("G69 cancels a G68 or G68.2 the reader kept as RAW (D245)");
            return;
        }

        if (!chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain("G69"));
            return;
        }

        Change(block, WithoutRotations(chain), "G69");
    }

    /// <summary>
    /// The modes of the control a block kept as RAW switches on or off (D244, D245): the chain goes back to where it
    /// stood before the block, a G69 or G50.1 ends the entries it cancels, with their RESET or OFF written beside the
    /// RAW block, and ends the modes kept as RAW; a G68, G68.2 or G68.3 that replaces an entry of the chain ends it
    /// the same way; a G68, G68.2, G68.3, G68.4 or G51.1 switches its mode on, which its cancel then gives back to the
    /// control. A code the control rejects with an alarm, a cross form (PS5462) or a third 3D G68 (PS5043), switches
    /// nothing.
    /// </summary>
    /// <param name="block">The block kept as RAW.</param>
    public static void KeepRawModes(FanucBlock block)
    {
        bool rotation = HasCode(block, "G68");
        bool tilt = HasCode(block, "G68.2") || HasCode(block, "G68.3");
        bool additive = HasCode(block, "G68.4");
        bool mirror = HasCode(block, "G51.1");
        bool cancel = HasCode(block, "G69");
        bool mirrorCancel = HasCode(block, "G50.1");
        if (!(rotation || tilt || additive || mirror || cancel || mirrorCancel))
        {
            return;
        }

        FanucChain chain = block.Fanuc.Chain;
        if (block.ChainBefore is FanucChain before)
        {
            chain.CopyFrom(before);
        }

        if (cancel)
        {
            EndBesideRaw(block, "G68", "G68.2");
            chain.SetNative("G68", false);
            chain.SetNative("G68.2", false);
            chain.Conversions = 0;
        }

        if (mirrorCancel)
        {
            EndBesideRaw(block, "G51.1");
            chain.SetNative("G51.1", false);
        }

        bool rotated = chain.Active("G68") || chain.Conversions > 0;
        if (rotation && !chain.Active("G68.2"))
        {
            bool spatial = block.Source.Words.Any(word => word.Address is "I" or "J" or "K");
            if (spatial && chain.Conversions < 2 && !chain.Active("G68"))
            {
                chain.Conversions++;
            }
            else if (!spatial && chain.Conversions == 0)
            {
                EndBesideRaw(block, "G68");
                chain.SetNative("G68", true);
            }
        }

        if ((tilt || additive) && !rotated)
        {
            if (tilt)
            {
                EndBesideRaw(block, "G68.2");
            }

            chain.SetNative("G68.2", true);
        }

        if (mirror)
        {
            chain.SetNative("G51.1", true);
        }

        block.Fanuc.ForgetPositions();
    }

    // The entries of these functions end where the chain holds them: their RESET or OFF beside the RAW block where the
    // reader knows the chain and the cut means one thing, dropped otherwise, the chain then unknown where it was known
    // (language 4.2; D225).
    private static void EndBesideRaw(FanucBlock block, params string[] owners)
    {
        FanucChain chain = block.Fanuc.Chain;
        if (!chain.Entries.Any(entry => owners.Contains(entry.Owner)))
        {
            return;
        }

        if (!chain.Known)
        {
            chain.Drop(owners);
            return;
        }

        var desired = new List<FanucChainEntry>();
        foreach (FanucChainEntry entry in chain.Entries)
        {
            if (!owners.Contains(entry.Owner))
            {
                desired.Add(entry);
            }
            else if (entry.LocalShift is IReadOnlyList<Word> local)
            {
                desired.Add(new FanucChainEntry("SHIFT", "G52", local));
            }
        }

        if (!chain.TryChange(desired, block.Draft.BesideRaw))
        {
            chain.Forget();
        }
    }

    // Whether the source block holds this G code, read or not.
    private static bool HasCode(FanucBlock block, string code)
    {
        return block.Source.Words.Any(word => word.Address == "G" && NativeCode.Of(word) == code);
    }

    /// <summary>
    /// The chain without the entries of G68 and G68.2, the G52 shift of a SHIFT that held it with the origin of a
    /// G68.2 kept (FanucTilt): the chain after a G69, or under a G68.2 that replaces the active one (D245).
    /// </summary>
    /// <param name="chain">The chain of the reader.</param>
    public static List<FanucChainEntry> WithoutRotations(FanucChain chain)
    {
        var desired = new List<FanucChainEntry>();
        foreach (FanucChainEntry entry in chain.Entries)
        {
            if (entry.Owner is not ("G68" or "G68.2"))
            {
                desired.Add(entry);
            }
            else if (entry.LocalShift is IReadOnlyList<Word> local)
            {
                desired.Add(new FanucChainEntry("SHIFT", "G52", local));
            }
        }

        return desired;
    }

    // G51.1 X0 mirrors the named axes, G50.1 cancels (controllers fanuc.md 4; controller-mapping 1, MIRROR). The value
    // is the position of the mirror line and MIRROR mirrors about the origin (language 4.2), so another value stays
    // RAW.
    private static void ReadMirror(FanucBlock block)
    {
        bool mirror = block.TakeCode("G51.1");
        bool cancel = !mirror && block.TakeCode("G50.1");
        if (!mirror && !cancel)
        {
            return;
        }

        var axes = new List<string>();
        foreach (FanucAxisWord axis in FanucAxes.Unread(block))
        {
            block.MarkRead(axis.Word);
            if (mirror && axis.Word.Number != 0m)
            {
                block.Draft.KeepAsRaw("G51.1 mirrors about a line away from the origin, MIRROR about the origin");
                return;
            }

            axes.Add(axis.Axis);
        }

        FanucChain chain = block.Fanuc.Chain;
        if (cancel && chain.HoldsNative("G51.1"))
        {
            // A G50.1 that cancels a mirror the reader kept as RAW stays RAW, so that the control gets it back; the
            // MIRROR=OFF of the mirror the chain holds stands beside it (KeepRawModes).
            block.Draft.KeepAsRaw("G50.1 cancels a G51.1 the reader kept as RAW");
            return;
        }

        if (mirror && axes.Count > 0)
        {
            Value value = axes.Count == 1 ? new IdentValue(axes[0]) : new ListValue(axes);
            var blocks = new List<DraftBlock>();
            chain.Append(new FanucChainEntry("MIRROR", "G51.1", [new Word { Key = "MIRROR", Value = value }]), blocks);
            Write(block, blocks);
            block.Fanuc.ForgetPositions();
        }
        else if (cancel && !chain.Known)
        {
            block.Draft.KeepAsRaw(UnknownChain("G50.1"));
        }
        else if (cancel && chain.Holds("G51.1"))
        {
            var desired = new List<FanucChainEntry>();
            foreach (FanucChainEntry entry in chain.Entries)
            {
                if (entry.Owner != "G51.1")
                {
                    desired.Add(entry);
                }
            }

            Change(block, desired, "G50.1");
        }
    }
}
