using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// What a template of the machine file holds, read before it is rendered (machine-config 3).
/// </summary>
internal static class TemplateText
{
    /// <summary>
    /// True when the template has the placeholder: "tol" for {tol}.
    /// </summary>
    /// <param name="template">The template as the machine file writes it.</param>
    /// <param name="name">The placeholder name without braces.</param>
    public static bool Has(string template, string name)
    {
        foreach (Placeholder placeholder in new Template(template, 1, new Diagnostics("")).Placeholders)
        {
            if (placeholder.Name == name)
            {
                return true;
            }
        }

        return false;
    }
}
