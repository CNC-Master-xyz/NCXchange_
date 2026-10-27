using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// D154: an ORIENT template without {angle} (M19, M219, M319, M419) orients to the angle the machine sets, so a
/// compiler writes it only for ORIENT:R=0; any other angle, or one from an expression, needs a template with {angle},
/// and without one it is the ERROR CMP007 and nothing is written for the word (machine-config 5).
/// </summary>
public static class OrientTemplate
{
    /// <summary>
    /// Whether the ORIENT template can write the word; reports CMP007 when it cannot. A missing template is left to
    /// the template ERROR of the writer.
    /// </summary>
    /// <param name="template">The ORIENT template of the spindle table; null when the table has none.</param>
    /// <param name="orient">The ORIENT word.</param>
    /// <param name="block">The block, which the ERROR cites (D98).</param>
    /// <param name="diagnostics">Where the ERROR goes.</param>
    /// <returns>False when the ERROR was reported and nothing is to be written.</returns>
    public static bool Writes(string? template, Word orient, Block block, Diagnostics diagnostics)
    {
        if (template is null || TemplateText.Has(template, "angle") || IsZero(orient.Value))
        {
            return true;
        }

        diagnostics.Error(block, DiagnosticCodes.OrientAngleWithoutTemplate,
            $"{orient.ToCanonical()} needs an ORIENT template with {{angle}}: \"{template}\" orients to the angle the "
            + "machine sets and is written only for an angle of 0, so nothing is written for it (machine-config 5, "
            + "D154).");
        return false;
    }

    private static bool IsZero(Value value)
    {
        return value switch
        {
            IntegerValue integer => integer.Number == 0,
            DecimalValue number => number.Number == 0m,
            _ => false,
        };
    }
}
