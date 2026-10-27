namespace Ncx.Core.Expressions;

/// <summary>
/// The SYS_ names, which read the state of the control: the program reads them and never assigns them, and only they
/// take an index, which selects a register or table row (language 4.12, virtual machine 2.7, D51, D145).
/// </summary>
public static class SystemNames
{
    /// <summary>
    /// The prefix of every system variable name, "SYS_".
    /// </summary>
    public const string Prefix = "SYS_";

    /// <summary>
    /// True for a SYS_ name.
    /// </summary>
    /// <param name="name">The variable name without its $ and its index.</param>
    public static bool IsSystem(string name)
    {
        return name.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
