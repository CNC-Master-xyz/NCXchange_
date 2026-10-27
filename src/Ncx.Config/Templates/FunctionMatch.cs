using Ncx.Core.Machine;

namespace Ncx.Config.Templates;

/// <summary>
/// One state of the machine that writes a native code, as <see cref="TemplateSet.FindFunctionsByCode"/> answers it: the
/// word of its table, the state, the table with its channel binding and the values the code carried (machine-config 5,
/// architecture 7, D155).
/// </summary>
public sealed record FunctionMatch
{
    /// <summary>
    /// The word of the table with its address: "SPINDLE:MAIN" for [spindle.MAIN], "SPINDLE_MODE:SUB",
    /// "SPINDLE_SYNC", "COOLANT:STANDARD", "FUNC:DOOR", and "WORKPIECE" for a [workpiece] selection (D155).
    /// </summary>
    public required string Word { get; init; }

    /// <summary>
    /// The key of the state in its table, "CW", "ON", "OPEN"; the role for a [workpiece] selection, "MAIN".
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// The function table of the state with its channel binding (D56); null for a [workpiece] selection.
    /// </summary>
    public FunctionTable? Table { get; init; }

    /// <summary>
    /// The values the native code carried for the placeholders of the template, the {angle} of "M19 S{angle}".
    /// </summary>
    public required TemplateValues Values { get; init; }

    /// <summary>
    /// The state named by the word of its table with the key of the state as its value, "SPINDLE:TOOL=CW",
    /// "WORKPIECE=MAIN": the answer of <see cref="TemplateSet.FindFunctionByCode"/> (architecture 7).
    /// </summary>
    public string Key => Word + "=" + State;

    /// <summary>
    /// The word without its address, "SPINDLE" of "SPINDLE:MAIN", "COOLANT" of "COOLANT:STANDARD".
    /// </summary>
    public string Family
    {
        get
        {
            int colon = Word.IndexOf(':', StringComparison.Ordinal);
            return colon < 0 ? Word : Word.Substring(0, colon);
        }
    }

    /// <summary>
    /// The address of the word, the role or channel or function name, "MAIN" of "SPINDLE:MAIN"; null for a word without
    /// one, SPINDLE_SYNC and WORKPIECE.
    /// </summary>
    public string? Address
    {
        get
        {
            int colon = Word.IndexOf(':', StringComparison.Ordinal);
            return colon < 0 ? null : Word.Substring(colon + 1);
        }
    }
}
