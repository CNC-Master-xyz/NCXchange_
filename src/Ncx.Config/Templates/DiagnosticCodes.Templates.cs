namespace Ncx.Config;

// The codes of the templates (P2-02) take CFG100-CFG149 of the CFG area of Ncx.Config (D98); a code is never
// renumbered or reused.
//
// A template that cannot be used is reported (machine-config introduction), and a mistake is a WARNING only where the
// specification allows it (code-guidelines 6): a template text that cannot be read as placeholders at all (a brace
// that is never closed or never opened, braces around no name, a format suffix other than a width padded with zeros)
// is an ERROR like the missing placeholder value (wave-1 question #41). A well-formed name outside the list of the
// introduction is the WARNING CFG104, which the introduction states (D152).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// A template is rendered without a value for one of its placeholders, which leaves the template unusable
    /// (machine-config introduction).
    /// </summary>
    public const string TemplateValueMissing = "CFG100";

    /// <summary>
    /// A template has a brace that opens no placeholder or closes none, or braces around no placeholder name
    /// (machine-config introduction).
    /// </summary>
    public const string TemplatePlaceholderMalformed = "CFG101";

    /// <summary>
    /// A placeholder carries a format suffix other than a width padded with zeros such as {tool:02} (machine-config
    /// introduction).
    /// </summary>
    public const string TemplateFormatUnknown = "CFG102";

    /// <summary>
    /// A padded placeholder that follows digits is written with a number wider than its field, so that the text would
    /// read back as other numbers (machine-config introduction, D153).
    /// </summary>
    public const string TemplateNumberWiderThanItsField = "CFG103";

    /// <summary>
    /// A template has a well-formed placeholder name that the complete list of the machine-config introduction does
    /// not hold; it is a WARNING and the placeholder is read as a number (D152).
    /// </summary>
    public const string TemplatePlaceholderNotListed = "CFG104";
}
