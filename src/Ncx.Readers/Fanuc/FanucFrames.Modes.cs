using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The modes of a Fanuc block (controllers fanuc.md 4): TCPM, POLAR, CYLINDER and TOLERANCE.
/// </summary>
internal static partial class FanucFrames
{
    // G43.4 is tool center point control, G43.5 the same with the tool vector as I J K on the lines, TCPM=ON; G49 ends
    // it (controllers fanuc.md 4; controller-mapping 1, TCPM; language 4.2); a builder's codes, G435 and G436 on the
    // Nakamura, come from [transform]. H of G43.4 is the length offset, OFFSET:LEN. Where the reader does not know
    // whether TCPM is on (FanucCallerState), G49 writes TCPM=OFF, which it is after the G49 either way.
    private static void ReadTcpm(FanucBlock block)
    {
        TransformTable? transform = block.Machine.Transform;
        bool vector = block.TakeCode("G43.5");
        bool on = vector || block.TakeCode("G43.4") || (!IsNativeTcpm(transform?.TcpmOn)
            && FanucTemplates.TryMatch(block, transform?.TcpmOn, out _));
        bool active = block.Fanuc.Tcpm || block.Fanuc.Unknowns.Tcpm;
        bool off = !on && ((active && block.HasCode("G49"))
            || (!IsNativeTcpm(transform?.TcpmOff) && FanucTemplates.TryMatch(block, transform?.TcpmOff, out _)));
        if (on)
        {
            block.Draft.AddState("TCPM", null, s_on);
            block.Fanuc.Tcpm = true;
            block.Fanuc.VectorTcpm = vector;
            block.Fanuc.Unknowns.Tcpm = false;
            if (block.Find("H") is SourceWord length && FanucAxes.Of(length, block) is null)
            {
                block.MarkRead(length);
                if (length.Number is decimal register && register == decimal.Truncate(register))
                {
                    block.Draft.AddState("OFFSET", "LEN", Integer(decimal.ToInt32(register)));
                }
                else
                {
                    block.Draft.KeepAsRaw("the H of G43.4 names no offset register");
                }
            }
        }
        else if (off)
        {
            block.Draft.AddState("TCPM", null, s_off);
            block.Fanuc.Tcpm = false;
            block.Fanuc.VectorTcpm = false;
            block.Fanuc.Unknowns.Tcpm = false;
        }
    }

    // The native codes of tool center point control are read by their number, so a [transform] template that writes
    // them is not matched a second time.
    private static bool IsNativeTcpm(string? template)
    {
        return template is not null
            && (template.StartsWith("G43.4", StringComparison.Ordinal)
                || template.StartsWith("G43.5", StringComparison.Ordinal)
                || template.StartsWith("G49", StringComparison.Ordinal));
    }

    // G12.1 and G13.1 switch polar interpolation, POLAR; G7.1 C r the cylinder surface with its radius, CYLINDER=r, and
    // G7.1 C0 off; a builder's codes, G112, G113 and G107 on the Nakamura, come from [transform] (controllers fanuc.md
    // 4; controller-mapping 1, CYLINDER and POLAR; language 4.2; D96, D102).
    private static void ReadTransformations(FanucBlock block)
    {
        TransformTable? transform = block.Machine.Transform;
        if (block.TakeCode("G12.1") || FanucTemplates.TryMatch(block, transform?.PolarOn, out _))
        {
            block.Draft.AddState("POLAR", null, s_on);
            block.Fanuc.Polar = true;
            block.Fanuc.Unknowns.Polar = false;
            block.Fanuc.ForgetPositions();
        }
        else if (block.TakeCode("G13.1") || FanucTemplates.TryMatch(block, transform?.PolarOff, out _))
        {
            block.Draft.AddState("POLAR", null, s_off);
            block.Fanuc.Polar = false;
            block.Fanuc.Unknowns.Polar = false;
            block.Fanuc.ForgetPositions();
        }

        Value? cylinder = null;
        if (FanucTemplates.TryMatch(block, transform?.CylinderOff, out _))
        {
            cylinder = s_off;
        }
        else if (FanucTemplates.TryMatch(block, transform?.CylinderOn, out TemplateValues values))
        {
            cylinder = values.TryGetNumber("r", out decimal radius) && radius != 0 ? FanucNumbers.Of(radius) : s_off;
        }
        else if (block.TakeCode("G7.1"))
        {
            SourceWord? radius = block.Take("C");
            cylinder = radius?.Number switch
            {
                null => null,
                0m => s_off,
                _ => radius.ToNcxNumber(),
            };
            if (cylinder is null)
            {
                block.Draft.KeepAsRaw("G7.1 names no radius C as a number");
                return;
            }
        }

        if (cylinder is not null)
        {
            block.Draft.AddState("CYLINDER", null, cylinder);
            block.Fanuc.ForgetPositions();
        }
    }

    // G5.1 Q1 switches AI contour control on and Q0 off; the tolerance itself sits in a parameter, so NCX TOLERANCE
    // reaches Fanuc only as the on and off switch (controllers fanuc.md 4; controller-mapping 1, TOLERANCE; D85).
    // G5.1 Q0 is TOLERANCE=OFF, and G5.1 Q1 stays RAW, since TOLERANCE takes a number or OFF (language 4.1) and the
    // [tolerance] ON of a Fanuc machine writes no {tol} (machine-config 5; controller-mapping 1, TOLERANCE: the value
    // stays RAW). The specification wins over the phase plan (implementation README).
    private static void ReadTolerance(FanucBlock block)
    {
        if (!block.TakeCode("G5.1"))
        {
            return;
        }

        if (block.Take("Q")?.Number == 0m)
        {
            block.Draft.AddState("TOLERANCE", null, s_off);
            return;
        }

        block.Draft.KeepAsRaw(
            "G5.1 Q1 switches AI contour control on with the tolerance of a parameter (controller-mapping 1, "
            + "TOLERANCE)");
    }
}
