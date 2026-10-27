using System.Globalization;

namespace Ncx.Core.Model;

/// <summary>
/// One finding: its severity, the file and line of the block it is about, for a generated block the line of the
/// block it was generated for, its code and its message (D98, code-guidelines 6). A finding about the command line
/// has the tool as its file and no line (D110).
/// </summary>
public sealed record Diagnostic
{
    /// <summary>
    /// ERROR, WARNING or INFO (D98).
    /// </summary>
    public required Severity Severity { get; init; }

    /// <summary>
    /// The file of the block.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// The 1-based line of the NCX block or source block (code-guidelines 6); null only for a diagnostic about the
    /// command line, which stands on no line of a file (D110). A diagnostic about a whole file has line 1 of it.
    /// </summary>
    public required int? Line { get; init; }

    /// <summary>
    /// For a diagnostic on a generated block, the line of the block it was generated for; null otherwise (D98).
    /// </summary>
    public int? OriginLine { get; init; }

    /// <summary>
    /// The code, an area prefix and three digits: PAR003, VM042 (D98).
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The message, which names the rule and the block, not the internal state (code-guidelines 2).
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The diagnostic as the user reads it: file(line): ERROR VM042: message, file(line, from 12): ... for one on a
    /// generated block (D98), and ncx: ERROR CLI001: message for one about the command line (D110).
    /// </summary>
    public string ToText()
    {
        // A diagnostic on a generated block names the line of the block it was generated for (D98); one without a line
        // names its origin alone, as the MSBuild canonical form names a tool (D110).
        string place = File;
        if (Line is int line)
        {
            place = OriginLine is int originLine
                ? string.Create(CultureInfo.InvariantCulture, $"{File}({line}, from {originLine})")
                : string.Create(CultureInfo.InvariantCulture, $"{File}({line})");
        }

        return $"{place}: {SeverityText(Severity)} {Code}: {Message}";
    }

    // The severities are written in capitals, as D98 names them.
    private static string SeverityText(Severity severity)
    {
        return severity switch
        {
            Severity.Error => "ERROR",
            Severity.Warning => "WARNING",
            Severity.Info => "INFO",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Not a severity of D98."),
        };
    }
}
