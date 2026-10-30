using Ncx.Core.Model;

namespace Ncx.Core.Machine;

/// <summary>
/// The state of a function table that a word sets, under the state names machine-config 5 gives its table (D198): the
/// name an expansion rule's states and a table's state_channels both use (D414).
/// </summary>
public static class FunctionStates
{
    /// <summary>
    /// The state a word sets: SPINDLE=CW, CCW or OFF the state of that name; RPM, ORIENT and RPM_MAX their own state;
    /// CSS=ON and VC the state VC, CSS=OFF the state CSS_OFF; SPINDLE_MODE, COOLANT and FUNC their value; SPINDLE_SYNC
    /// with two roles ON and SPINDLE_SYNC=OFF OFF; PHASE the state PHASE; null for a value that names no state.
    /// </summary>
    /// <param name="word">A word written from a function table.</param>
    public static string? Of(Word word)
    {
        string? ident = word.Value is IdentValue name ? name.Name : null;
        return word.Key switch
        {
            "RPM" or "ORIENT" or "RPM_MAX" or "PHASE" => word.Key,
            "VC" => "VC",
            "CSS" => ident == "OFF" ? "CSS_OFF" : "VC",
            "SPINDLE_SYNC" => ident == "OFF" ? "OFF" : "ON",
            _ => ident,
        };
    }
}
