using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The swivelled plane of CYCLE800 and ROTS (controllers siemens.md 4, 11 rule 4; controller-mapping 1, TILT,
/// TILT_AXIS, MOVE, RETRACT; language 4.2, 4.3; D82, D83): CYCLE800(_FR, _TC, _ST, _MODE, _X0, _Y0, _Z0, _A, _B, _C,
/// _X1, _Y1, _Z1, _DIR, _FR_I, _DMODE) is TILT with the angles converted from the order _MODE gives into the X, Y, Z
/// order of TILT (D335), or TILT_AXIS where _MODE gives the rotary axes directly,
/// with MOVE from _DIR and _ST and a RETRACT block in front of it from _FR; CYCLE800() is TILT=RESET; ROTS and AROTS
/// with two spatial angles are TILT. The reader keeps the form the source used (D82).
/// </summary>
internal static class SiemensTilt
{
    // Below this cosine of the second rotation the first and the third cannot be told apart, and the first is 0.
    private const double GimbalLimit = 1e-9;

    // The places of the parameters of CYCLE800 (controllers siemens.md 4).
    private const int Retract = 0;
    private const int Carrier = 1;
    private const int Swivel = 2;
    private const int Mode = 3;
    private const int BeforeRotation = 4;
    private const int FirstAngle = 7;
    private const int SecondAngle = 8;
    private const int AfterRotation = 10;
    private const int Direction = 13;
    private const int RetractDistance = 14;

    /// <summary>
    /// Reads CYCLE800, ROTS and AROTS.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (block.Take("CYCLE800") is SourceWord cycle)
        {
            List<string> arguments = SiemensArguments.Split(cycle.Text);
            if (arguments.Count == 0)
            {
                Reset(block);
            }
            else
            {
                Swivel800(block, arguments);
            }

            return;
        }

        SourceWord? rots = block.Take("ROTS") ?? block.Take("AROTS");
        if (rots is not null)
        {
            ReadRots(block, rots);
        }
    }

    // CYCLE800() cancels the swivel: the RESET of the tilts of CYCLE800 the chain holds, TILT=RESET where it holds no
    // tilt at all; a tilt of ROTS stays, which CYCLE800() does not cancel (controller-mapping 1, TILT).
    private static void Reset(SiemensBlock block)
    {
        SiemensChain chain = block.Facts.Chain;
        var blocks = new List<SiemensDraftBlock>();
        if (chain.Tilt() is null && chain.Known)
        {
            blocks.Add(SiemensChain.ResetOf("TILT"));
        }
        else if (!chain.TryRemoveSwivel(blocks))
        {
            block.Draft.KeepAsRaw("CYCLE800() would reset a tilt that is not the last entry of the chain of "
                + "transforms, "
                + "or one the reader does not know (language 4.2, D31)");
            return;
        }

        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // CYCLE800 with its parameters: the angles of _MODE into TILT or TILT_AXIS, MOVE from _DIR and _ST, the RETRACT of
    // _FR in front of it; a new swivel replaces the one of an earlier CYCLE800, an additive one (_ST ones digit 1)
    // builds on it (controllers siemens.md 4; controller-mapping 1, TILT).
    private static void Swivel800(SiemensBlock block, List<string> arguments)
    {
        int? retract = Whole(arguments, Retract);
        int? swivel = Whole(arguments, Swivel);
        int? mode = Whole(arguments, Mode);
        int? direction = Whole(arguments, Direction);
        var angles = new decimal?[3];
        for (int index = 0; index < 3; index++)
        {
            angles[index] = Number(arguments, FirstAngle + index);
        }

        if (retract is not int fr || swivel is not int st || mode is not int modeBits || direction is not int dir
            || angles[0] is null || angles[1] is null || angles[2] is null || SiemensArguments.StringOf(
                arguments.Count > Carrier ? arguments[Carrier] : "") == "0")
        {
            block.Draft.KeepAsRaw("CYCLE800 gives _FR, _ST, _MODE, the angles and _DIR as numbers, and a tool carrier "
                + "other than \"0\", which deselects it (controllers siemens.md 4)");
            return;
        }

        SiemensDraftBlock? before = ReferenceShift(arguments, BeforeRotation);
        SiemensDraftBlock? after = ReferenceShift(arguments, AfterRotation);
        if (before is null || after is null || (st / 100) % 10 != 0)
        {
            block.Draft.KeepAsRaw("CYCLE800 with a reference point that is no number, or with the tool aligned instead "
                + "of the plane, has no NCX word (controllers siemens.md 4)");
            return;
        }

        // Under DIAMON whether the cycle takes _X0 and _X1 as radii or diameters depends on MD20360 bit 14, which no
        // machine key carries, so a non-zero X of a reference point keeps the block RAW (D125, D333).
        if (SiemensFrames.XInRadius(block) && (before.Has("X", null) || after.Has("X", null)))
        {
            block.Draft.KeepAsRaw("CYCLE800 with an X reference point under DIAMON, whose unit, radius or diameter, "
                + "depends on MD20360 bit 14 (D125, D333)");
            return;
        }

        SiemensDraftBlock? entry = Entry(block, modeBits, angles);
        if (entry is null)
        {
            return;
        }

        bool tracks = (st / 10) % 10 == 1;
        entry.Add("MOVE", new IdentValue(tracks ? "MOVE" : dir == 0 ? "STAY" : "TURN"));
        SiemensDraftBlock? retractBlock = RetractOf(block, fr, arguments);
        if (block.Draft.IsRaw)
        {
            return;
        }

        var blocks = new List<SiemensDraftBlock>();
        if (retractBlock is not null)
        {
            blocks.Add(retractBlock);
        }

        // D333: a new swivel (_ST ones digit 0) resets the earlier tilts and every programmable entry before its TILT,
        // an additive one (1) appends; _X0, _Y0, _Z0 are a SHIFT before the TILT and _X1, _Y1, _Z1 one after it, all
        // entries of CYCLE800, a system frame that TRANS and the other replacing instructions leave standing.
        SiemensChain chain = block.Facts.Chain;
        if (st % 10 != 1 && !chain.TryClear(blocks))
        {
            block.Draft.KeepAsRaw("the new swivel of CYCLE800 resets a chain of transforms the reader does not know "
                + "here (language 4.2, D31; virtual machine 3.9)");
            return;
        }

        SiemensDraftBlock[] parts = [before, entry, after];
        foreach (SiemensDraftBlock part in parts)
        {
            if (part.Verb is not null && (part.Verb != "SHIFT" || part.Words.Count > 0))
            {
                chain.Append(new SiemensChainEntry(part.Verb, "CYCLE800", part), blocks);
            }
        }

        WarnNotCarried(block, st, dir, fr);
        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // _MODE bits 7 and 6: 00 axis-wise angles with the rotation order in bits 5 to 0, 01 spatial angles, 10 projection
    // angles, 11 the rotary axes directly (controller-mapping 1, TILT and TILT_AXIS; D82). The axis-wise rotations
    // build on each other, each about the axis the one before left, and _A, _B, _C are the first, second and third
    // rotation of the order the bit pairs give, not the angles about X, Y and Z: mode 27 (Z Y X) is exactly TILT with
    // A = _C, B = _B, C = _A, and every other order becomes TILT through the rotation matrix. The spatial-angle mode
    // turns by _A about Z and then by _B about the rotated Y, which is TILT A=0 B=_B C=_A (D335).
    // D425: D335 settles the axis-wise and the spatial modes; no page gives the projection angles a construction a
    // rotation matrix can be built from (the Zyklen manual describes them as the angle of the swivelled face projected
    // onto the first two axes, with the third rotation building on the ones before), so the projection mode stays RAW
    // until a manual gives the construction.
    // D296: _A and _B of the rotary-axes mode are the angles of rotary axis 1 and 2 of the swivel data record _TC
    // names, and which machine axes those are belongs to the commissioned kinematics, not to the program; the reader
    // takes their NCX names from the machine's [transform] TILT_AXIS_ON template, the placeholder in the place of _A
    // first and the one in the place of _B second, and keeps the block RAW where the machine has no such template
    // (decisions.md D296).
    private static SiemensDraftBlock? Entry(SiemensBlock block, int mode, decimal?[] angles)
    {
        int kind = (mode >> 6) & 3;
        if (kind == 1)
        {
            return Tilt(0m, angles[1]!.Value, angles[0]!.Value);
        }

        if (kind == 0 && Order(mode) is char[] order)
        {
            if (order is ['Z', 'Y', 'X'])
            {
                return Tilt(angles[2]!.Value, angles[1]!.Value, angles[0]!.Value);
            }

            double[,] rotation = About(order[0], (double)angles[0]!.Value);
            for (int index = 1; index < 3; index++)
            {
                rotation = Times(rotation, About(order[index], (double)angles[index]!.Value));
            }

            TiltAngles(rotation, out double a, out double b, out double c);
            return Tilt(SiemensNumbers.Round(a), SiemensNumbers.Round(b),
                SiemensNumbers.Round(c));
        }

        if (kind == 3 && RotaryAxes(block.Machine) is List<string> rotary)
        {
            var axial = new SiemensDraftBlock().WithVerb("TILT_AXIS");
            axial.Add(rotary[0], SiemensNumbers.Of(angles[0]!.Value))
                .Add(rotary[1], SiemensNumbers.Of(angles[1]!.Value));
            return axial;
        }

        block.Draft.KeepAsRaw($"CYCLE800 with _MODE {mode.ToString(CultureInfo.InvariantCulture)} gives its angles in "
            + "an order or a form the reader does not convert (controller-mapping 1, TILT)");
        return null;
    }

    // TILT A B C: the rotations about X, Y and Z of the frame where the word stands, in that order (language 4.2).
    private static SiemensDraftBlock Tilt(decimal a, decimal b, decimal c)
    {
        return new SiemensDraftBlock().WithVerb("TILT")
            .Add("A", SiemensNumbers.Of(a))
            .Add("B", SiemensNumbers.Of(b))
            .Add("C", SiemensNumbers.Of(c));
    }

    // The axes of the three rotations of the axis-wise mode, in the order they turn: bits 1 and 0 name the axis of the
    // first rotation, bits 3 and 2 that of the second, bits 5 and 4 that of the third, 01 the first geometry axis X,
    // 10 the second Y, 11 the third Z (D335). Null where a pair names no axis or an axis turns twice.
    private static char[]? Order(int mode)
    {
        var order = new char[3];
        for (int index = 0; index < 3; index++)
        {
            order[index] = ((mode >> (index * 2)) & 3) switch
            {
                1 => 'X',
                2 => 'Y',
                3 => 'Z',
                _ => '\0',
            };
        }

        return order[0] != '\0' && order[1] != '\0' && order[2] != '\0' && order[0] != order[1]
            && order[1] != order[2] && order[0] != order[2] ? order : null;
    }

    // The matrix of a rotation about X, Y or Z by an angle in degrees.
    private static double[,] About(char axis, double degrees)
    {
        double sin = Math.Sin(degrees * Math.PI / 180);
        double cos = Math.Cos(degrees * Math.PI / 180);
        return axis switch
        {
            'X' => new[,] { { 1, 0, 0 }, { 0, cos, -sin }, { 0, sin, cos } },
            'Y' => new[,] { { cos, 0, sin }, { 0, 1, 0 }, { -sin, 0, cos } },
            _ => new[,] { { cos, -sin, 0 }, { sin, cos, 0 }, { 0, 0, 1 } },
        };
    }

    private static double[,] Times(double[,] first, double[,] second)
    {
        var product = new double[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                product[row, column] = (first[row, 0] * second[0, column]) + (first[row, 1] * second[1, column])
                    + (first[row, 2] * second[2, column]);
            }
        }

        return product;
    }

    // The TILT angles of a rotation matrix: R = Rz(C) Ry(B) Rx(A), the rotations about X, Y and Z of the frame in that
    // order (language 4.2), as FanucTilt.SpatialAngles decomposes the Euler angles of G68.2.
    private static void TiltAngles(double[,] rotation, out double a, out double b, out double c)
    {
        b = Math.Asin(Math.Clamp(-rotation[2, 0], -1.0, 1.0));
        if (Math.Abs(Math.Cos(b)) > GimbalLimit)
        {
            a = Math.Atan2(rotation[2, 1], rotation[2, 2]);
            c = Math.Atan2(rotation[1, 0], rotation[0, 0]);
        }
        else
        {
            a = 0;
            c = Math.Atan2(-rotation[0, 1], rotation[1, 1]);
        }

        a = a * 180 / Math.PI;
        b = b * 180 / Math.PI;
        c = c * 180 / Math.PI;
    }

    // _FR retracts before the swivel: 0 none, 1 machine Z, 2 Z then XY, 4 in tool direction to the limit, 5 by _FR_I
    // (controller-mapping 1, TILT; D83).
    private static SiemensDraftBlock? RetractOf(SiemensBlock block, int retract, List<string> arguments)
    {
        switch (retract)
        {
            case 0:
                return null;
            case 1 or 2 or 4:
                return new SiemensDraftBlock().WithVerb("RETRACT");
            case 5 when Number(arguments, RetractDistance) is decimal distance && distance > 0:
                return new SiemensDraftBlock().WithVerb("RETRACT", SiemensNumbers.Of(distance));
            default:
                block.Draft.KeepAsRaw("_FR of CYCLE800 names no retract 0, 1, 2, 4 or 5 with its distance (controllers "
                    + "siemens.md 4)");
                return null;
        }
    }

    // The values of CYCLE800 no NCX word carries: the digits of _ST above the hundreds, the preferred solution of
    // _DIR, the XY part of the retract _FR=2 (controllers siemens.md 4).
    private static void WarnNotCarried(SiemensBlock block, int swivel, int direction, int retract)
    {
        var values = new List<string>();
        if (swivel >= 1000)
        {
            values.Add("the digits of _ST above the hundreds");
        }

        if (direction > 0)
        {
            values.Add("the preferred solution +1 of _DIR");
        }

        if (retract == 2)
        {
            values.Add("the retract in X and Y of _FR=2");
        }

        if (values.Count > 0)
        {
            block.Draft.Warnings.Add(new SiemensWarning(DiagnosticCodes.SiemensValueNotCarried,
                $"{string.Join(", ", values)} of CYCLE800 {(values.Count == 1 ? "has" : "have")} no NCX word and "
                + $"{(values.Count == 1 ? "is" : "are")} not written (controllers siemens.md 4; D82, D83)."));
        }
    }

    // ROTS X30 Y45 turns the frame by two spatial angles, TILT with the third 0; ROTS replaces the programmable frame,
    // AROTS builds on it (controllers siemens.md 4; controller-mapping 1, TILT).
    private static void ReadRots(SiemensBlock block, SourceWord rots)
    {
        var tilt = new SiemensDraftBlock().WithVerb("TILT");
        var values = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (SourceWord word in block.Unread())
        {
            string? axis = word.Text.Length > 0 ? SiemensAxes.AxisOf(block, word) : null;
            string? key = axis switch
            {
                "X" => "A",
                "Y" => "B",
                "Z" => "C",
                _ => null,
            };
            if (key is null)
            {
                continue;
            }

            block.MarkRead(word);
            if (SiemensExpression.ValueOf(block, word.Text, out string? problem) is not Value value)
            {
                block.Draft.KeepAsRaw($"{rots.Address} {word.Address}={word.Text} has no NCX value: {problem}");
                return;
            }

            values[key] = value;
        }

        foreach (string key in new[] { "A", "B", "C" })
        {
            tilt.Add(key, values.TryGetValue(key, out Value? value) ? value : new IntegerValue(0, "0"));
        }

        SiemensChain chain = block.Facts.Chain;
        var blocks = new List<SiemensDraftBlock>();
        if (values.Count == 0 || (rots.Address == "ROTS" && !chain.TryCut(blocks)))
        {
            block.Draft.KeepAsRaw($"{rots.Address} needs its spatial angles and a chain the reader knows (controllers "
                + "siemens.md 4)");
            return;
        }

        chain.Append(new SiemensChainEntry("TILT", rots.Address, tilt), blocks);
        block.Draft.Before.AddRange(blocks);
        block.Siemens.ForgetPositions();
    }

    // The rotary axes the rotary-axes mode names: the placeholders the [transform] TILT_AXIS_ON template of the
    // machine puts in the places of _A and _B of CYCLE800, in that order; null where the machine has no such
    // template, where it names no placeholder there, or where the name is no rotary axis of the machine (D296;
    // machine-config 5, TILT_AXIS_ON).
    private static List<string>? RotaryAxes(MachineConfig machine)
    {
        List<string> arguments = ArgumentsOf(machine.Transform?.TiltAxisOn);
        if (arguments.Count <= SecondAngle)
        {
            return null;
        }

        var axes = new List<string>();
        foreach (int index in new[] { FirstAngle, SecondAngle })
        {
            string placeholder = arguments[index];
            if (placeholder.Length <= 2 || placeholder[0] != '{' || placeholder[^1] != '}')
            {
                return null;
            }

            string name = placeholder.Substring(1, placeholder.Length - 2).ToUpperInvariant();
            if (!IsRotary(machine, name))
            {
                return null;
            }

            axes.Add(name);
        }

        return axes;
    }

    // The positional arguments of the one call of a template, "CYCLE800(1,\"TC\",0,192,...)"; none where the
    // template is empty or writes no call.
    private static List<string> ArgumentsOf(string? template)
    {
        int open = template is null ? -1 : template.IndexOf('(', StringComparison.Ordinal);
        int close = template is null ? -1 : template.LastIndexOf(')');
        return open < 0 || close < open ? [] : SiemensArguments.Split(template!.Substring(open, close - open + 1));
    }

    private static bool IsRotary(MachineConfig machine, string name)
    {
        foreach (AxisDef axis in machine.Axes)
        {
            if (axis.Kind == AxisKind.Rotary && string.Equals(axis.NcxName, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The SHIFT of a reference point of CYCLE800, _X0 _Y0 _Z0 before the rotation or _X1 _Y1 _Z1 after it (D333): the
    // axes whose value is not 0, no words where all are 0 or empty; null where a value is no number.
    private static SiemensDraftBlock? ReferenceShift(List<string> arguments, int first)
    {
        var shift = new SiemensDraftBlock().WithVerb("SHIFT");
        string[] axes = ["X", "Y", "Z"];
        for (int index = 0; index < 3; index++)
        {
            int place = first + index;
            if (place >= arguments.Count || arguments[place].Trim().Length == 0)
            {
                continue;
            }

            if (Number(arguments, place) is not decimal value)
            {
                return null;
            }

            if (value != 0)
            {
                shift.Add(axes[index], SiemensNumbers.Of(value));
            }
        }

        return shift;
    }

    private static int? Whole(List<string> arguments, int index)
    {
        return index < arguments.Count ? SiemensNumbers.WholeNumber(arguments[index]) : null;
    }

    private static decimal? Number(List<string> arguments, int index)
    {
        return index < arguments.Count ? SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[index])) : null;
    }
}
