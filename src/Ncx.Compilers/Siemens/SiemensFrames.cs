using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The frames of a SINUMERIK block (controllers siemens.md 3, 4, 6, 12 rule 4; controller-mapping 1; machine-config
/// 3, 4, 5; language 4.2; D31): G54 to G57 and G505 to G599 for ORIGIN; the chain in program order, TRANS, ROT RPL=
/// and MIRROR for its first entry, ATRANS, AROT RPL= and AMIRROR for the rest, CYCLE800 from the [transform] template
/// with {dir} from MOVE; DIAMON and DIAMOF from [diameter]; TRAORI, TRACYL and TRANSMIT from [transform]; CYCLE832
/// from [tolerance]; G74 and G75 from [home]; PRESETON from [setpos]; RETRACT from [retract].
/// </summary>
internal static partial class SiemensFrames
{
    // The target keys of the settable frame and of the diameter programming (controllers siemens.md 2, groups 8, 29).
    private const string DatumGroup = "G54";
    private const string DiameterGroup = "DIAMON";

    // The target keys of the programmable frame (group 3) and of the swivel of CYCLE800, which a RAW line may change
    // outside the chain of the virtual machine (controllers siemens.md 2, 4): each stands in the target state only
    // while it is unknown, from such a RAW line to the block that ends it again (AfterRaw, WriteChain).
    private const string ProgrammableFrame = "TRANS";
    private const string Swivel = "CYCLE800";

    // The target key of the plane, group 6, as SiemensMotion keeps it (controllers siemens.md 2).
    private const string PlaneGroup = "G17";

    // The words of the transform chain (language 4.2).
    private static readonly string[] s_chainWords = ["SHIFT", "ROTATE", "MIRROR", "TILT", "TILT_AXIS", "MOVE", "ROT"];

    // The values of {dir} per MOVE, which never come from [transform] move: _DIR alone decides whether the rotary
    // axes move at all, -1 onto the smaller rotary-axis value and 0 "Schwenken nein", only computing the swivel
    // frame; MOVE positions them too, while the tool tip is tracked by the tens digit of _ST (machine-config 5, move;
    // controller-mapping 1, TILT and MOVE; D262).
    private static readonly Dictionary<TiltMove, string> s_directions = new()
    {
        [TiltMove.Turn] = "-1",
        [TiltMove.Move] = "-1",
        [TiltMove.Stay] = "0",
    };

    /// <summary>
    /// What the block did to the chain, in the order of D31 (controllers siemens.md 4, 12 rule 4): where it removed a
    /// shift, rotation or mirror, the programmable frame is written again from its first entry, TRANS alone where none
    /// stays; a removed tilt is CYCLE800() and the tilts that stay are written again; then every appended entry in
    /// program order, the first of the chain replacing, the others additive, each in a block of its own.
    /// </summary>
    public static void WriteChain(SiemensBlock write)
    {
        Block block = write.Block;
        foreach (string key in s_chainWords)
        {
            write.Written(key);
        }

        if (block.Verb?.Key is "SHIFT" or "TILT" or "TILT_AXIS")
        {
            foreach (SiemensAxisWord axis in SiemensAxes.Of(block))
            {
                write.Written(axis.Word);
            }
        }

        // ROT is not available on Siemens and is ignored with a WARNING (controller-mapping 1, MOVE and ROT; language
        // 4.2, ROT).
        if (block.Has("ROT"))
        {
            write.Warning(DiagnosticCodes.SiemensOptionNotWritten,
                "ROT has no SINUMERIK form and is not written (controller-mapping 1, MOVE and ROT; language 4.2).");
        }

        // After a RAW line that may have changed the programmable frame or the swivel (AfterRaw), a block that ends
        // them in NCX ends them on the control too, although the chain of the virtual machine holds nothing of what the
        // RAW text did: ORIGIN starts an empty chain and a RESET removes the entries of its kind (language 4.2); TRANS
        // alone ends the programmable frame and CYCLE800() the swivel (controllers siemens.md 4). What stays of the
        // chain is written again after it, so that the control has the chain of the virtual machine from here on.
        // D421: the entries of the swivel that the reader writes again after a datum, which the control still holds,
        // are written neither at the datum nor again.
        if (write.File.IsHeldSwivel(write.Step))
        {
            return;
        }

        // D430: while the programmable frame is unknown after a RAW line, a block that appends to it writes it in its
        // replacing form, the chain from its first entry with TRANS, ROT or MIRROR, as a block that ends it does.
        ChainChange change = ChainWriter.Between(write.Before.Frame, write.After.Frame);
        bool endsProgrammable = write.IsMadeUnknown(ProgrammableFrame)
            && (EndsProgrammableFrame(block) || AppendsProgrammable(change));
        bool endsSwivel = write.IsMadeUnknown(Swivel) && EndsSwivel(write);
        if (change.IsEmpty && !endsProgrammable && !endsSwivel)
        {
            return;
        }

        int kept = write.After.Frame.Chain.Count - change.Appended.Count;
        if (block.Has("ORIGIN") && HeldSwivel(write) is { Count: > 0 } held)
        {
            change = change with { Removed = [.. change.Removed.Where(entry => !held.Contains(entry))] };
        }

        WriteRemoved(write, change, kept, endsProgrammable, endsSwivel);
        if (endsProgrammable)
        {
            write.Target.Forget(ProgrammableFrame);
        }

        if (endsSwivel)
        {
            write.Target.Forget(Swivel);
        }

        IReadOnlyList<TransformEntry> chain = write.After.Frame.Chain;
        for (int index = kept; index < chain.Count; index++)
        {
            WriteEntry(write, chain, index, StartsProgrammableFrame(chain, index), appended: true);
        }
    }

    // D421: the swivel of CYCLE800 stands on the control after a datum of group 8, a component of the total frame of
    // its own (D333, D340 (3)), and the reader writes it again after the ORIGIN, the TILT with MOVE=STAY. Where the
    // blocks right after the ORIGIN append exactly the entries of the chain before it up to its last tilt, the tilts
    // with MOVE=STAY, the control still holds that swivel: those entries, the shifts its reference point carries among
    // them, are held, and neither the datum ends them nor those blocks write them again. Empty where they differ.
    private static HashSet<TransformEntry> HeldSwivel(SiemensBlock write)
    {
        var held = new HashSet<TransformEntry>(ReferenceEqualityComparer.Instance);
        IReadOnlyList<TransformEntry> before = write.Before.Frame.Chain;
        int lastTilt = -1;
        for (int index = 0; index < before.Count; index++)
        {
            lastTilt = IsTilt(before[index]) ? index : lastTilt;
        }

        if (lastTilt < 0 || write.After.Frame.Chain.Count > 0)
        {
            return held;
        }

        var steps = new List<BlockStep>();
        foreach (BlockStep step in write.File.StepsAfter(write.Step))
        {
            IReadOnlyList<TransformEntry> chain = step.After.Frame.Chain;
            bool chainOnly = step.Block.Verb?.Key is "SHIFT" or "TILT" or "TILT_AXIS";
            if (!chainOnly || chain.Count != steps.Count + 1 || chain.Count > lastTilt + 1
                || !SameSwivelEntry(before[chain.Count - 1], chain[^1]))
            {
                return held;
            }

            steps.Add(step);
            if (chain.Count == lastTilt + 1)
            {
                write.File.HoldSwivel(steps);
                for (int index = 0; index <= lastTilt; index++)
                {
                    held.Add(before[index]);
                }

                return held;
            }
        }

        return held;
    }

    // The same entry of a swivel written again after a datum: the same kind and values, a tilt with MOVE=STAY (D421).
    private static bool SameSwivelEntry(TransformEntry standing, TransformEntry again)
    {
        if (IsTilt(again) && again.Move != TiltMove.Stay)
        {
            return false;
        }

        return standing.Kind == again.Kind && standing.Rot == again.Rot && standing.Angle == again.Angle
            && standing.Workplane == again.Workplane && standing.Mirrored.SequenceEqual(again.Mirrored)
            && SameValues(standing.Shift, again.Shift) && SameValues(standing.Angles, again.Angles);
    }

    private static bool SameValues(
        IReadOnlyDictionary<string, decimal?> first, IReadOnlyDictionary<string, decimal?> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, decimal?> value in first)
        {
            if (!second.TryGetValue(value.Key, out decimal? other) || other != value.Value)
            {
                return false;
            }
        }

        return true;
    }

    // D333: CYCLE800 writes system frames, and a swivel to a new plane (_ST ones digit 0) deletes the programmable
    // frames (840D sl/828D Bedienhandbuch Fräsen 2013, connector p.481, 483). A shift before a tilt is therefore
    // written into the _X0/_Y0/_Z0 of the next CYCLE800, the reference point before the rotation (Arbeitsvorbereitung
    // 2013, connector p.712-713), and not again as TRANS: true for a shift a later tilt of the chain carries.
    private static bool IsFoldedIntoTilt(IReadOnlyList<TransformEntry> chain, int index)
    {
        return IsFoldedIntoTilt(chain, index, chain.Count);
    }

    // The same within the first count entries of the chain, the part a RESET keeps.
    private static bool IsFoldedIntoTilt(IReadOnlyList<TransformEntry> chain, int index, int count)
    {
        if (chain[index].Kind != TransformKind.Shift)
        {
            return false;
        }

        for (int later = index + 1; later < count; later++)
        {
            if (IsTilt(chain[later]))
            {
                return true;
            }
        }

        return false;
    }

    // True where no programmable entry stands on the control before this one: every entry before it back to the last
    // tilt, whose new swivel deleted the programmable frame (D333), is a shift folded into a tilt, so TRANS, ROT or
    // MIRROR is written rather than ATRANS, AROT or AMIRROR.
    private static bool StartsProgrammableFrame(IReadOnlyList<TransformEntry> chain, int index)
    {
        for (int before = index - 1; before >= 0; before--)
        {
            if (IsTilt(chain[before]))
            {
                return true;
            }

            if (!IsFoldedIntoTilt(chain, before))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTilt(TransformEntry entry)
    {
        return entry.Kind is TransformKind.Tilt or TransformKind.TiltAxis;
    }

    /// <summary>
    /// ORIGIN=n as the settable frame G54 to G57, G505 to G599, G500 for 0, where the control has another active
    /// (controllers siemens.md 4; controller-mapping 1, ORIGIN); the chain it clears is WriteChain's.
    /// </summary>
    public static void WriteOrigin(SiemensBlock write)
    {
        if (write.Block.Find("ORIGIN") is not Word origin)
        {
            return;
        }

        write.Written(origin);
        if (origin.Value is not IntegerValue number || DatumCode(number.Number) is not string code)
        {
            write.Error(DiagnosticCodes.SiemensOriginNotWritable,
                $"{origin.ToCanonical()} names no settable frame of the control, G54 to G57 for 1 to 4 and G505 to "
                + "G599 for 5 to 99 (controllers siemens.md 4; controller-mapping 1, ORIGIN).");
            return;
        }

        SiemensMotion.Change(write, DatumGroup, SiemensLine.DatumRank, code);
    }

    /// <summary>
    /// The code of the settable frame of ORIGIN=n: G54 to G57 for 1 to 4, G505 to G599 for 5 to 99, G500 for 0
    /// (controllers siemens.md 4; controller-mapping 1, ORIGIN); null beyond.
    /// </summary>
    public static string? DatumCode(long origin)
    {
        return origin switch
        {
            0 => "G500",
            >= 1 and <= 4 => "G" + (53 + origin).ToString(CultureInfo.InvariantCulture),
            >= 5 and <= 99 => "G5" + origin.ToString("00", CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    /// <summary>
    /// DIAMETER through [diameter], DIAMON and DIAMOF, where the X axis is switchable; elsewhere the programming of the
    /// axis decides and nothing is written (language 4.2; machine-config 4; controllers siemens.md 2, group 29; D60).
    /// </summary>
    public static void WriteDiameter(SiemensBlock write)
    {
        if (write.Block.Find("DIAMETER") is not Word diameter)
        {
            return;
        }

        write.Written(diameter);
        if (write.Machine.ResolveAxis("X")?.Programming != Programming.Switchable)
        {
            return;
        }

        bool on = diameter.Value.ToCanonical() == "ON";
        string? template = on ? write.Machine.Diameter?.On : write.Machine.Diameter?.Off;
        if (write.Render(template, "[diameter] (machine-config 4, D60)", new TemplateValues()) is string text
            && text.Length > 0 && write.Target.Changes(DiameterGroup, text))
        {
            write.Main.Code(SiemensLine.KeywordRank, text);
        }
    }

    /// <summary>
    /// The transformations and the tolerance in blocks of their own: TCPM, POLAR, CYLINDER, TOLERANCE, the rotary
    /// options (machine-config 5; controller-mapping 1; controllers siemens.md 3, 6).
    /// </summary>
    public static void WriteTransformations(SiemensBlock write)
    {
        TransformTable? transform = write.Machine.Transform;
        OnOff(write, "TCPM", transform?.TcpmOn, transform?.TcpmOff);
        OnOff(write, "POLAR", transform?.PolarOn, transform?.PolarOff);
        WriteCylinder(write, transform);
        WriteTolerance(write);
        WritePathMode(write, transform);
        WriteRotaryPath(write, transform);
        WriteRotaryFeed(write, transform);
    }

    /// <summary>
    /// HOME, SETPOS and RETRACT, the verbs of the frame that move or declare, each in a block of its own (language 4.2,
    /// 4.3; controllers siemens.md 3).
    /// </summary>
    public static void WriteVerbs(SiemensBlock write)
    {
        switch (write.Block.Verb?.Key)
        {
            case "HOME":
                WriteHome(write);
                break;
            case "SETPOS":
                WriteSetpos(write);
                break;
            case "RETRACT":
                WriteRetract(write);
                break;
        }
    }

    /// <summary>
    /// After a RAW line: a RAW text that names an instruction of the programmable frame (TRANS, ATRANS, ROT, AROT,
    /// ROTS, AROTS, CROTS, SCALE, ASCALE, MIRROR, AMIRROR, G58, G59) or of the swivel (CYCLE800, TOFRAME, TOROT, PAROT,
    /// and ROTS, AROTS, CROTS, which NCX reads as TILT), or names a frame variable ($P_PFRAME, $P_ACTFRAME, $P_BFRAME),
    /// may have changed it on the control, where the chain of the virtual machine does not hold it (controllers
    /// siemens.md 4; controller-mapping 1, TILT; language 4.1, D5): it is unknown until a block ends it (WriteChain).
    /// The settable frame is the datum, which every RAW line leaves unknown already.
    /// </summary>
    // D420 as D430 revises it: a RAW subprogram or builder-cycle call (the STAMA DREH(...), NEWCOORD; machine-builders
    // 2) may change the frame as well, and the control keeps whatever frame the call left (NC Programming p.500-502),
    // so the frame is unknown after it (KeysNamedBy) and the compiler writes no frame of its own: the next frame word
    // of the program goes out in its replacing form (WriteChain). A RAW line that is MSG, STOPRE, a comment, G4 or
    // another frame-neutral instruction leaves the frame known (controllers sample-corpus.md: MSG before every
    // operation; SiemensRawCall).
    public static void AfterRaw(SiemensBlock write, string raw)
    {
        foreach (string key in KeysNamedBy(raw))
        {
            write.MakeUnknown(key);
        }
    }

    /// <summary>
    /// At the start of a subprogram and at a label, where the control arrives from a caller or a jump that the STATIC
    /// walk does not follow and every value of the target state is unknown (virtual machine 1, 3.9; D99): the
    /// programmable frame or the swivel that a RAW line of the file may change is unknown too, since the RAW line may
    /// stand in a caller or after the label, on the way of a jump back to it (controllers siemens.md 4, 8).
    /// </summary>
    public static void ForgetRawFrames(SiemensBlock write)
    {
        foreach (string key in write.File.RawFrameKeys)
        {
            write.MakeUnknown(key);
        }
    }

    /// <summary>
    /// The keys of the target state that a RAW text may change, the programmable frame and the swivel, by the
    /// instructions and the frame variables it names, or both where it calls a subprogram or a builder cycle (AfterRaw,
    /// D420).
    /// </summary>
    /// <param name="raw">The text of the RAW block.</param>
    public static List<string> KeysNamedBy(string raw)
    {
        // The words of the text, without its strings and its comments, whose words are no instructions.
        string code = StringOrComment().Replace(raw, " ");
        bool everyFrame = FrameVariable().IsMatch(code) || SiemensRawCall.IsCall(raw);
        var keys = new List<string>();
        if (everyFrame || ProgrammableInstruction().IsMatch(code))
        {
            keys.Add(ProgrammableFrame);
        }

        if (everyFrame || SwivelInstruction().IsMatch(code))
        {
            keys.Add(Swivel);
        }

        return keys;
    }

    /// <summary>
    /// True where a RAW text names an instruction of the programmable frame or of the swivel, or a frame variable, as
    /// KeysNamedBy finds them; such a line is no call (SiemensRawCall, D430).
    /// </summary>
    /// <param name="raw">The text of the RAW block.</param>
    public static bool NamesFrame(string raw)
    {
        string code = StringOrComment().Replace(raw, " ");
        return FrameVariable().IsMatch(code) || ProgrammableInstruction().IsMatch(code)
            || SwivelInstruction().IsMatch(code);
    }

    // A removed shift, rotation or mirror: the programmable frame again from its first entry, TRANS alone where none
    // stays and no appended entry replaces it; a removed tilt: CYCLE800() and the tilts that stay again (controllers
    // siemens.md 4: every replacing instruction deletes all earlier programmable frame instructions; language 4.2).
    // The programmable frame or the swivel that a RAW line may have changed is ended the same way (WriteChain).
    private static void WriteRemoved(SiemensBlock write, ChainChange change, int kept, bool endsProgrammable,
        bool endsSwivel)
    {
        bool programmable = endsProgrammable;
        bool tilt = endsSwivel;
        foreach (TransformEntry entry in change.Removed)
        {
            programmable |= entry.Kind is TransformKind.Shift or TransformKind.Rotate or TransformKind.Mirror;
            tilt |= entry.Kind is TransformKind.Tilt or TransformKind.TiltAxis;
        }

        IReadOnlyList<TransformEntry> chain = write.After.Frame.Chain;

        // D333: CYCLE800() clears the swivel and its _X0/_Y0/_Z0 with it, so a removed tilt writes the programmable
        // frame that stays again, the shifts it carried among it.
        bool tiltRemoved = false;
        foreach (TransformEntry entry in change.Removed)
        {
            tiltRemoved |= IsTilt(entry);
        }

        for (int index = 0; index < kept && tiltRemoved; index++)
        {
            programmable |= !IsTilt(chain[index]);
        }

        if (tilt)
        {
            TransformTable? transform = write.Machine.Transform;
            if (write.Render(transform?.TiltOff, "[transform] TILT_OFF (machine-config 5)", new TemplateValues())
                is string off)
            {
                write.Write(off);
            }
        }

        bool rewritesProgrammable = false;
        bool startsFrame = true;
        for (int index = 0; index < kept; index++)
        {
            TransformEntry entry = chain[index];
            bool isTilt = IsTilt(entry);

            // A shift a kept tilt carries in its _X0/_Y0/_Z0 is not written as TRANS (D333).
            if (!isTilt && IsFoldedIntoTilt(chain, index, kept))
            {
                continue;
            }

            if ((isTilt && tilt) || (!isTilt && programmable))
            {
                WriteEntry(write, chain, index, first: !isTilt && startsFrame, appended: false,
                    restating: endsProgrammable || endsSwivel);
                rewritesProgrammable |= !isTilt;
                startsFrame = isTilt;
            }
            else if (isTilt)
            {
                startsFrame = true;
            }
        }

        // TRANS alone clears the programmable frame where no entry of it stays and the first appended one does not
        // replace it (controllers siemens.md 4).
        bool appendedReplaces = kept == 0 && chain.Count > 0 && chain[0].Kind is not (TransformKind.Tilt
            or TransformKind.TiltAxis);
        if (programmable && !rewritesProgrammable && !appendedReplaces)
        {
            write.Write("TRANS");
        }
    }

    // One entry of the chain: TRANS, ROT RPL= or MIRROR for the first of the programmable frame, ATRANS, AROT RPL= or
    // AMIRROR for the rest, CYCLE800 for a tilt (controllers siemens.md 4, 12 rule 4; controller-mapping 1, SHIFT,
    // ROTATE, MIRROR, TILT).
    private static void WriteEntry(SiemensBlock write, IReadOnlyList<TransformEntry> chain, int index, bool first,
        bool appended, bool restating = false)
    {
        TransformEntry entry = chain[index];
        switch (entry.Kind)
        {
            case TransformKind.Shift:
                WriteShift(write, entry, first, appended, restating);
                break;
            case TransformKind.Rotate:
                WriteRotation(write, entry, first, appended, restating);
                break;
            case TransformKind.Mirror:
                WriteMirror(write, entry, first);
                break;
            case TransformKind.Tilt:
                WriteTilt(write, chain, index, write.Machine.Transform?.TiltOn, "[transform] TILT_ON", appended,
                    restating);
                break;
            case TransformKind.TiltAxis:
                // TILT_AXIS needs the kinematics module where the machine has no TILT_AXIS_ON (D82).
                string? template = write.Machine.Transform?.TiltAxisOn;
                if (string.IsNullOrEmpty(template))
                {
                    write.Error(DiagnosticCodes.SiemensTiltNotWritable,
                        "TILT_AXIS has no TILT_AXIS_ON template in [transform] of the machine, and without the "
                        + "kinematics module the target cannot write it (language 4.2; machine-config 5; D82).");
                    return;
                }

                WriteTilt(write, chain, index, template, "[transform] TILT_AXIS_ON", appended, restating);
                break;
        }
    }

    // TRANS X Y Z or ATRANS X Y Z, the values of the SHIFT block as written where it appends the entry, else of the
    // entry (controllers siemens.md 4). The entry holds what the virtual machine stores: under DIAMETER=ON its X is the
    // radius of the diameter the SHIFT wrote (D125). A frame of the control on a facing axis is a radius unless the
    // machine keeps frames in diameters ([[axis]] frames_in_diameter, MD20360 bit 3; D125, D388), so the frame
    // factors of SiemensAxes apply, not those of a motion word.
    private static void WriteShift(SiemensBlock write, TransformEntry entry, bool first, bool appended,
        bool restating)
    {
        var words = new List<string>();
        bool fromBlock = appended && write.Block.Verb?.Key == "SHIFT";
        foreach (KeyValuePair<string, decimal?> axis in Ordered(entry.Shift))
        {
            string letter = SiemensAxes.LetterOf(write, axis.Key);
            Word? word = fromBlock ? write.Block.Find(axis.Key) : null;
            Value? value = word?.Value
                ?? (axis.Value is decimal known ? new DecimalValue(known, Invariant(known)) : null);
            string? text = word is not null
                ? SiemensAxes.FrameValueOf(write, axis.Key, word.Value, letter)
                : axis.Value is decimal stored
                    ? write.Format(SiemensAxes.DecimalsAddress(write, letter),
                        stored * SiemensAxes.FrameStateFactor(write, axis.Key))
                    : null;
            if (text is null)
            {
                ChainNotWritable(write, restating,
                    $"The shift of {axis.Key} is not known, so {(first ? "TRANS" : "ATRANS")} cannot be written "
                    + "(virtual machine 1; language 4.2).");
                return;
            }

            words.Add(SiemensAxes.Address(letter, text, "", value is ExprValue));
        }

        write.Write((first ? "TRANS " : "ATRANS ") + string.Join(" ", words));
    }

    // ROT RPL= or AROT RPL=, the rotation about the axis perpendicular to the plane (controllers siemens.md 4;
    // controller-mapping 1, ROTATE); in another plane than the one of the entry, about the tool axis of its plane.
    private static void WriteRotation(SiemensBlock write, TransformEntry entry, bool first, bool appended,
        bool restating)
    {
        Word? word = appended ? write.Block.Find("ROTATE") : null;
        string? angle = word is not null && word.Value is not IdentValue
            ? SiemensExpressions.ValueText(write, word.Value, "ROTATE")
            : entry.Angle is decimal known ? write.Format("ROTATE", known) : null;
        if (angle is null)
        {
            ChainNotWritable(write, restating,
                "The angle of the rotation is not known, so AROT cannot be written (virtual machine 1; language 4.2).");
            return;
        }

        string about = entry.Workplane == write.After.Frame.Workplane
            ? "RPL"
            : entry.Workplane switch
            {
                Workplane.ZX => "Y",
                Workplane.YZ => "X",
                _ => "Z",
            };
        write.Write((first ? "ROT " : "AROT ") + about + "=" + angle);
    }

    // MIRROR X0 or AMIRROR X0, the value meaningless (controllers siemens.md 4).
    private static void WriteMirror(SiemensBlock write, TransformEntry entry, bool first)
    {
        var words = new List<string>();
        foreach (string axis in entry.Mirrored)
        {
            words.Add(SiemensAxes.Address(SiemensAxes.LetterOf(write, axis), "0", "", expression: false));
        }

        write.Write((first ? "MIRROR " : "AMIRROR ") + string.Join(" ", words));
    }

    // CYCLE800 from the template with the angles {a} {b} {c} of the tilt and the origin {x} {y} {z} 0 since the
    // chain carries the shifts. D262: _ST and _DIR are independent parameters of the cycle, so the template writes
    // {move} in the place of _ST and {dir} in the place of _DIR; {move} comes from [transform] move, which holds the
    // _ST of each MOVE option with its tens digit, and {dir} from MOVE alone and never from that table. A template
    // without {dir} cannot write STAY or MOVE, because its constant _DIR turns the rotary axes (machine-config 5,
    // TILT_ON and move; controller-mapping 1, TILT and MOVE; controllers siemens.md 4, 12 rule 4; decisions.md D262).
    private static void WriteTilt(SiemensBlock write, IReadOnlyList<TransformEntry> chain, int index,
        string? template, string what, bool appended, bool restating)
    {
        TransformEntry entry = chain[index];
        var values = new TemplateValues();
        if (!SetReferencePoint(write, chain, index, template, what, values, restating))
        {
            return;
        }

        bool fromBlock = appended && write.Block.Verb?.Key is "TILT" or "TILT_AXIS";
        foreach (string angle in new[] { "A", "B", "C" })
        {
            Word? word = fromBlock ? write.Block.Find(angle) : null;
            string? angleText = word is not null ? SiemensExpressions.ValueText(write, word.Value, angle)
                : entry.Angles.TryGetValue(angle, out decimal? known) && known is decimal value
                    ? write.Format(angle, value)
                    : entry.Angles.ContainsKey(angle) ? null : "0";
            if (angleText is not null)
            {
                values.Set(angle.ToLowerInvariant(), angleText);
            }
        }

        string option = entry.Move.ToString().ToUpperInvariant();
        TransformTable? transform = write.Machine.Transform;
        if (transform?.Move.TryGetValue(option, out string? move) == true)
        {
            values.Set("move", move);
        }

        values.Set("dir", s_directions[entry.Move]);
        if (entry.Move != TiltMove.Turn && !write.HasPlaceholder(template, "dir"))
        {
            write.Error(DiagnosticCodes.SiemensTiltNotWritable,
                $"MOVE={option} needs {{dir}} in the place of _DIR of the template, and {what} of the machine writes "
                + "_DIR as a constant, which turns the rotary axes (machine-config 5; controller-mapping 1, MOVE; "
                + "D262).");
            return;
        }

        if (write.Render(template, what + " (machine-config 5)", values) is string text)
        {
            write.Write(text);
        }
    }

    // D333: {x} {y} {z}, the _X0/_Y0/_Z0 of CYCLE800, the sum of the shifts between the last tilt before this one and
    // it, which the new swivel would delete as TRANS (Bedienhandbuch Fräsen 2013, connector p.481, 483;
    // Arbeitsvorbereitung 2013, connector p.712-713: "Bezugspunkt vor der Drehung"). A frame value of X is a radius
    // unless the machine keeps frames in diameters (D125, D388). A ROTATE, a MIRROR or another tilt before it is the
    // ERROR CMP535 until a rule for additive swivels is drafted; a shift the template has no placeholder for is
    // CMP532. False where the tilt cannot be written.
    private static bool SetReferencePoint(SiemensBlock write, IReadOnlyList<TransformEntry> chain, int index,
        string? template, string what, TemplateValues values, bool restating)
    {
        var sums = new Dictionary<string, decimal> { ["X"] = 0m, ["Y"] = 0m, ["Z"] = 0m };
        for (int before = index - 1; before >= 0; before--)
        {
            TransformEntry entry = chain[before];

            // A second swivel to a new plane deletes the earlier swivel with the programmed frames (Bedienhandbuch
            // Fräsen 2013, connector p.483), so a tilt on a tilt would lose the first one silently (D333).
            if (IsTilt(entry))
            {
                write.Error(DiagnosticCodes.SiemensFrameBeforeTilt,
                    "Another tilt stands before this tilt in the chain, and CYCLE800 with a new swivel plane deletes "
                    + "the earlier swivel and the programmed frames; no rule for an additive swivel is drafted "
                    + "(Bedienhandbuch Fräsen 2013, p.483; D333).");
                return false;
            }

            if (entry.Kind != TransformKind.Shift)
            {
                write.Error(DiagnosticCodes.SiemensFrameBeforeTilt,
                    $"A {entry.Kind.ToString().ToUpperInvariant()} stands before the tilt in the chain, and CYCLE800 "
                    + "with a new swivel plane deletes the programmable frame; no rule for an additive swivel is "
                    + "drafted (Bedienhandbuch Fräsen 2013, p.481, 483; D333).");
                return false;
            }

            foreach (KeyValuePair<string, decimal?> axis in entry.Shift)
            {
                if (!sums.ContainsKey(axis.Key))
                {
                    write.Error(DiagnosticCodes.SiemensTiltNotWritable,
                        $"The shift of {axis.Key} before the tilt has no place in CYCLE800, whose reference point "
                        + "_X0 _Y0 _Z0 takes X, Y and Z only (Arbeitsvorbereitung 2013, p.712-713; D333).");
                    return false;
                }

                if (axis.Value is not decimal known)
                {
                    ChainNotWritable(write, restating,
                        $"The shift of {axis.Key} before the tilt is not known, so the reference point of CYCLE800 "
                        + "cannot be written (virtual machine 1; language 4.2; D333).");
                    return false;
                }

                sums[axis.Key] += known;
            }
        }

        // MD20360 bit 14 may make the values of the cycle masks diameters, which is not verified; the radius is
        // written and the X of the reference point is a WARNING under DIAMETER=ON (D125, D333).
        if (sums["X"] != 0m && write.After.Frame.Diameter)
        {
            write.Warning(DiagnosticCodes.SiemensTiltShiftUnderDiameter,
                "The shift of X before the tilt is written into _X0 of CYCLE800 as a frame value under DIAMETER=ON; "
                + "whether the cycle takes it as a radius or, with MD20360 bit 14, as a diameter is not verified "
                + "(Funktionshandbuch Grundfunktionen 2012, p.901-903; D125, D333).");
        }

        foreach (KeyValuePair<string, decimal> sum in sums)
        {
            string placeholder = sum.Key.ToLowerInvariant();
            if (sum.Value != 0m && !write.HasPlaceholder(template, placeholder))
            {
                write.Error(DiagnosticCodes.SiemensTiltNotWritable,
                    $"A shift of {sum.Key} stands before the tilt, and {what} of the machine writes _{sum.Key}0 as a "
                    + $"constant: the template needs {{{placeholder}}} there, since a new swivel deletes TRANS "
                    + "(machine-config 5; D333).");
                return false;
            }

            string letter = SiemensAxes.LetterOf(write, sum.Key);
            values.Set(placeholder, sum.Value == 0m
                ? "0"
                : write.Format(SiemensAxes.DecimalsAddress(write, letter),
                    sum.Value * SiemensAxes.FrameStateFactor(write, sum.Key)));
        }

        return true;
    }

    // D146: TRACYL works in the plane of the developed surface, G19 with Y around the circumference and Z along the
    // cylinder (840D sl/828D Arbeitsvorbereitung 2013, connector p.352: N40 TRACYL(40), N50 G19); TRAFOOF returns
    // to the plane of the program, which the WORKPLANE group writes again.
    private static void WriteCylinderPlane(SiemensBlock write, bool off)
    {
        string plane = !off
            ? "G19"
            : write.After.Frame.Workplane switch
            {
                Workplane.ZX => "G18",
                Workplane.YZ => "G19",
                _ => "G17",
            };
        write.Write(plane);
        write.Target.Set(PlaneGroup, plane);
    }

    /// <summary>
    /// The words a block under CYLINDER=n cannot carry on TRACYL (D146): a Y word, which TRACYL takes for the
    /// circumference, and a rotary word other than the rotary axis of the workpiece holder, which is no length on this
    /// cylinder. Each is the ERROR CMP536. An ARC is written in G19 with the other motion code (SiemensArcs).
    /// </summary>
    public static void ReportCylinder(SiemensBlock write)
    {
        if (write.After.Frame.Cylinder is null)
        {
            return;
        }

        foreach (SiemensAxisWord axis in SiemensAxes.Of(write.Block))
        {
            if (axis.Axis == "Y" || (SiemensAxes.IsRotary(write, axis.Axis)
                && !SiemensAxes.IsCylinderLength(write, axis.Axis)))
            {
                write.Error(DiagnosticCodes.SiemensCylinderNotWritable,
                    $"{axis.Word.ToCanonical()} under CYLINDER: TRACYL takes Y for the circumference, the length of "
                    + "the C word on the rotary axis of the workpiece holder, so another rotary word or a Y word "
                    + "has no place (Arbeitsvorbereitung 2013, p.352-353; D146).");
            }
        }
    }

    // A transformation on and off by its template (machine-config 5; controller-mapping 1, TCPM, POLAR).
    private static void OnOff(SiemensBlock write, string key, string? on, string? off)
    {
        if (write.Block.Find(key) is not Word word)
        {
            return;
        }

        write.Written(word);
        bool switchesOn = word.Value.ToCanonical() == "ON";
        string state = switchesOn ? "ON" : "OFF";
        if (write.Render(switchesOn ? on : off, $"[transform] {key}_{state} (machine-config 5)", new TemplateValues())
            is string text)
        {
            write.Write(text);
        }
    }

    // CYLINDER=r switches on with the reference radius {r} and the working diameter {d} that TRACYL takes, OFF switches
    // off (language 4.2; machine-config 5; D96).
    private static void WriteCylinder(SiemensBlock write, TransformTable? transform)
    {
        if (write.Block.Find("CYLINDER") is not Word cylinder)
        {
            return;
        }

        write.Written(cylinder);
        var values = new TemplateValues();
        bool off = cylinder.Value.ToCanonical() == "OFF";
        if (!off && SiemensExpressions.ValueText(write, cylinder.Value, "CYLINDER") is string radius)
        {
            values.Set("r", radius);
            decimal? number = cylinder.Value is ExprValue
                ? null
                : decimal.Parse(cylinder.Value.ToCanonical(), CultureInfo.InvariantCulture);
            values.Set("d", number is decimal known ? write.Format("CYLINDER", known * 2m) : "(" + radius + ")*2");
        }

        string what = off
            ? "[transform] CYLINDER_OFF (machine-config 5)"
            : "[transform] CYLINDER_ON (machine-config 5)";
        if (write.Render(off ? transform?.CylinderOff : transform?.CylinderOn, what, values) is string text)
        {
            write.Write(text);
            WriteCylinderPlane(write, off);
        }
    }

    // TOLERANCE through [tolerance], CYCLE832(tol, mode, otol), OFF as CYCLE832(0,0,1); TOLERANCE:ROTARY and
    // TOLERANCE_MODE write it again while it is on (machine-config 5; controller-mapping 1, TOLERANCE; D85).
    // ON_ROTARY is written instead of ON while TOLERANCE:ROTARY is set; a machine without it writes ON (D151). The
    // report of a value the template cannot carry, CMP015, is the compiler framework's (D367).
    private static void WriteTolerance(SiemensBlock write)
    {
        Block block = write.Block;
        bool changes = block.Has("TOLERANCE") || block.Has("TOLERANCE_MODE");
        if (!changes)
        {
            return;
        }

        write.Written("TOLERANCE");
        write.Written("TOLERANCE_MODE");
        ToleranceTable? table = write.Machine.Tolerance;
        ToleranceState state = write.After.Frame.Tolerance;
        if (block.Has("TOLERANCE", null, "OFF"))
        {
            if (write.Render(table?.Off, "[tolerance] OFF (machine-config 5, D85)", new TemplateValues())
                is string off)
            {
                write.Write(off);
            }

            return;
        }

        if (state.Value is null && block.Find("TOLERANCE", null)?.Value is not ExprValue)
        {
            return;
        }

        var values = new TemplateValues();
        SetTolerance(write, values, "tol", block.Find("TOLERANCE", null), state.Value);
        SetTolerance(write, values, "rotary", block.Find("TOLERANCE", "ROTARY"), state.Rotary);
        string mode = state.Mode == ToleranceMode.Rough ? "ROUGH" : "FINISH";
        if (table is not null && table.Mode.TryGetValue(mode, out string? modeValue))
        {
            values.Set("mode", modeValue);
        }

        if (write.Render(ToleranceTemplate.On(table, state), ToleranceTemplate.What(table, state), values)
            is string on)
        {
            string? comment = ToleranceTemplate.Comment(table, state);
            write.Write(comment is null ? on : on + " ; " + write.CommentText(comment));
        }
    }

    // PATH_MODE after the tolerance words, G60 or G64 through [transform] (D349); an empty or missing template is the
    // WARNING CMP533.
    private static void WritePathMode(SiemensBlock write, TransformTable? transform)
    {
        if (write.Block.Find("PATH_MODE") is not Word word)
        {
            return;
        }

        write.Written(word);
        string? template = PathModeTemplate.Of(transform, write.After.Frame);
        if (string.IsNullOrEmpty(template))
        {
            write.Warning(DiagnosticCodes.SiemensOptionNotWritten,
                PathModeTemplate.NotWritten(word.ToCanonical(), write.After.Frame));
            return;
        }

        write.Write(template);
    }

    private static void SetTolerance(SiemensBlock write, TemplateValues values, string name, Word? word,
        decimal? state)
    {
        if (word is not null && SiemensExpressions.ValueText(write, word.Value, "TOLERANCE") is string text)
        {
            values.Set(name, text);
        }
        else if (state is decimal known)
        {
            values.Set(name, write.Format("TOLERANCE", known));
        }
    }

    // ROTARY_PATH=SHORTEST writes DC() on the rotary axes (SiemensAxes), and a template where the machine gives one
    // (machine-config 5, ROTARY_PATH_SHORTEST; D86).
    private static void WriteRotaryPath(SiemensBlock write, TransformTable? transform)
    {
        if (write.Block.Find("ROTARY_PATH") is not Word path)
        {
            return;
        }

        write.Written(path);
        string? template = path.Value.ToCanonical() == "SHORTEST"
            ? transform?.RotaryPathShortest
            : transform?.RotaryPathFull;
        if (!string.IsNullOrEmpty(template))
        {
            write.Write(template);
        }
    }

    // ROTARY_FEED through [transform], FGREF[{axis}] per rotary axis of the machine; without a template it is not
    // written, with a WARNING (machine-config 5, ROTARY_FEED_MM_MIN; D86).
    private static void WriteRotaryFeed(SiemensBlock write, TransformTable? transform)
    {
        if (write.Block.Find("ROTARY_FEED") is not Word feed)
        {
            return;
        }

        write.Written(feed);
        string? template = feed.Value.ToCanonical() == "MM_MIN"
            ? transform?.RotaryFeedMmMin
            : transform?.RotaryFeedDegMin;
        if (string.IsNullOrEmpty(template))
        {
            write.Warning(DiagnosticCodes.SiemensOptionNotWritten,
                $"{feed.ToCanonical()} has no template in [transform] of the machine and is not written "
                + "(machine-config 5; controller-mapping 1, ROTARY_FEED; D86).");
            return;
        }

        foreach (AxisDef axis in write.Machine.Axes)
        {
            if (axis.Kind != AxisKind.Rotary)
            {
                continue;
            }

            var values = new TemplateValues();
            values.Set("axis", axis.Letter ?? axis.NcxName);
            if (write.Render(template, "[transform] ROTARY_FEED (machine-config 5)", values) is string text)
            {
                write.Write(text);
            }
        }
    }

    // HOME through [home] (machine-config 3; controllers siemens.md 3; controller-mapping 1, HOME): G74 with the
    // machine axis names of the axes and 0, X1=0 Z1=0, for the reference point; the point template, G75 X0 Z0 FP=2,
    // for POINT=2 and further.
    private static void WriteHome(SiemensBlock write)
    {
        write.Written("HOME");
        write.Written("POINT");
        int point = write.Block.Find("POINT")?.Value is IntegerValue number ? (int)number.Number : 1;
        var axes = new List<string>();
        foreach (SiemensAxisWord axis in SiemensAxes.Of(write.Block))
        {
            write.Written(axis.Word);
            AxisDef? definition = SiemensAxes.Resolve(write, axis.Axis);
            string name = point > 1
                ? definition?.Letter ?? axis.Axis
                : definition?.Id ?? axis.Axis;
            axes.Add(SiemensAxes.Address(name, "0", "", expression: false));
        }

        var values = new TemplateValues();
        values.Set("axes", string.Join(" ", axes));
        values.Set("point", point);
        string? template = point > 1 ? write.Machine.Home?.Point : write.Machine.Home?.Template;
        string what = point > 1 ? "[home] point (machine-config 3, D100)" : "[home] template (machine-config 3, D100)";
        if (write.Render(template, what, values) is string text)
        {
            write.Write(text);
        }
    }

    // SETPOS through [setpos], one call per axis: PRESETON(X,10) (language 4.2, SETPOS; controller-mapping 1; D55).
    private static void WriteSetpos(SiemensBlock write)
    {
        write.Written("SETPOS");
        foreach (SiemensAxisWord axis in SiemensAxes.Of(write.Block))
        {
            write.Written(axis.Word);
            string letter = SiemensAxes.LetterOf(write, axis.Axis);
            if (SiemensAxes.ValueOf(write, axis.Axis, axis.Word.Value, letter) is not string value)
            {
                continue;
            }

            var values = new TemplateValues();
            values.Set("axis", letter);
            values.Set("value", value);
            values.Set("axes", SiemensAxes.Address(letter, value, "", axis.Word.Value is ExprValue));
            if (write.Render(write.Machine.Setpos?.Template, "[setpos] template (machine-config 3, D55)", values)
                is string text)
            {
                write.Write(text);
            }
        }
    }

    // RETRACT through [retract]: bare to the axis limit, with a value by that distance; an empty template is a
    // computed move of the kinematics module, without which the block is an ERROR (controller-mapping 1, RETRACT;
    // D83).
    private static void WriteRetract(SiemensBlock write)
    {
        Word retract = write.Block.Find("RETRACT")!;
        write.Written(retract);
        bool bare = retract.Value is NoValue;
        string? template = bare ? write.Machine.Retract?.Max : write.Machine.Retract?.By;
        if (string.IsNullOrEmpty(template))
        {
            write.Error(DiagnosticCodes.SiemensRetractNotWritable,
                "RETRACT has no template in [retract] of the machine, and without the kinematics module the retract "
                + "cannot be computed (controller-mapping 1, RETRACT; D83).");
            return;
        }

        var values = new TemplateValues();
        if (!bare && SiemensExpressions.ValueText(write, retract.Value, "RETRACT") is string distance)
        {
            values.Set("distance", distance);
        }

        if (write.Render(template, "[retract] (machine-config 5, D83)", values) is string text)
        {
            write.Write(text);
        }
    }

    // The axes of a shift in the order a block writes them (language 5 rule 6).
    private static List<KeyValuePair<string, decimal?>> Ordered(IReadOnlyDictionary<string, decimal?> shift)
    {
        string[] order = ["X", "Y", "Z", "A", "B", "C"];
        var ordered = new List<KeyValuePair<string, decimal?>>();
        foreach (string axis in order)
        {
            if (shift.TryGetValue(axis, out decimal? value))
            {
                ordered.Add(new KeyValuePair<string, decimal?>(axis, value));
            }
        }

        foreach (KeyValuePair<string, decimal?> axis in shift)
        {
            if (!order.Contains(axis.Key))
            {
                ordered.Add(axis);
            }
        }

        return ordered;
    }

    private static string Invariant(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // An entry of the chain that cannot be written: the ERROR CMP531, and the WARNING CMP538 where the compiler
    // restates a chain the control may no longer hold after a RAW line, which the program did not ask it to write
    // (D430).
    private static void ChainNotWritable(SiemensBlock write, bool restating, string message)
    {
        if (restating)
        {
            write.Warning(DiagnosticCodes.SiemensChainNotRestated, message);
            return;
        }

        write.Error(DiagnosticCodes.SiemensChainNotWritable, message);
    }

    // True where a block appends a shift, a rotation or a mirror to the chain (D430).
    private static bool AppendsProgrammable(ChainChange change)
    {
        foreach (TransformEntry entry in change.Appended)
        {
            if (!IsTilt(entry))
            {
                return true;
            }
        }

        return false;
    }

    // The words that end the programmable frame in NCX: ORIGIN starts an empty chain, SHIFT=RESET and ROTATE=RESET
    // remove their kind (language 4.2), MIRROR=OFF the mirror (D124, the reading of the virtual machine).
    private static bool EndsProgrammableFrame(Block block)
    {
        return block.Has("ORIGIN")
            || block.Has("SHIFT", null, "RESET")
            || block.Has("ROTATE", null, "RESET")
            || block.Has("MIRROR", null, "OFF");
    }

    // The words that end the swivel in NCX: TILT=RESET and TILT_AXIS=RESET (language 4.2, D82), and ORIGIN, which
    // starts an empty chain, where [transform] of the machine gives the swivel an end, TILT_OFF (machine-config 5): a
    // machine without it has no swivel the compiler could end, and the RESET words report the missing template.
    private static bool EndsSwivel(SiemensBlock write)
    {
        Block block = write.Block;
        return block.Has("TILT", null, "RESET")
            || block.Has("TILT_AXIS", null, "RESET")
            || (block.Has("ORIGIN") && write.Machine.Transform?.TiltOff is not null);
    }

    // A string in double quotes, or a comment from ; to the end of its line (controllers siemens.md 1).
    [GeneratedRegex("\"[^\"\\n]*\"|;[^\\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex StringOrComment();

    // An instruction of the programmable frame, group 3 (controllers siemens.md 4), as a word of its own; names are
    // case-insensitive (siemens 1).
    [GeneratedRegex("(?<![A-Za-z0-9_$])(A?TRANS|A?ROTS?|CROTS|A?SCALE|A?MIRROR|G5[89])(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ProgrammableInstruction();

    // An instruction that swivels the plane or aligns the frame with the tool, and the spatial rotations that NCX
    // reads as TILT (controllers siemens.md 4; controller-mapping 1, TILT).
    [GeneratedRegex("(?<![A-Za-z0-9_$])(CYCLE800|TOFRAME[A-Z]*|TOROT[A-Z]*|PAROT[A-Z]*|A?ROTS|CROTS)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SwivelInstruction();

    // A frame variable, $P_PFRAME, $P_BFRAME, $P_ACTFRAME and the others whose name ends in FRAME (controllers
    // siemens.md 4); $P_UIFR is the settable frame, the datum.
    [GeneratedRegex("(?<![A-Za-z0-9_])\\$P_[A-Z0-9_]*FRAME(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FrameVariable();
}
