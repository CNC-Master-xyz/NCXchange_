using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

/// <summary>
/// D349: the [transform] template of PATH_MODE, PATH_MODE_EXACT_STOP or PATH_MODE_CONTINUOUS for the path mode after
/// the block; PATH_MODE=DEFAULT has already taken the machine's [dynamics] path_mode in the state (D350). The
/// compilers write it after the tolerance words of the block; an empty or missing template is the WARNING of the
/// family, CMP361, CMP533 or CMP120, and nothing is written (machine-config 5; language 4.1).
/// </summary>
internal static class PathModeTemplate
{
    /// <summary>
    /// The template; null or empty when the machine has none.
    /// </summary>
    /// <param name="transform">The [transform] table; null when the machine has none.</param>
    /// <param name="after">The frame after the block.</param>
    public static string? Of(TransformTable? transform, FrameSnapshot after)
    {
        return after.PathMode == PathMode.ExactStop ? transform?.PathModeExactStop : transform?.PathModeContinuous;
    }

    /// <summary>
    /// The text of the WARNING for a word without a template.
    /// </summary>
    /// <param name="word">The word as canonical NCX writes it, "PATH_MODE=EXACT_STOP".</param>
    /// <param name="after">The frame after the block.</param>
    public static string NotWritten(string word, FrameSnapshot after)
    {
        string key = after.PathMode == PathMode.ExactStop ? "PATH_MODE_EXACT_STOP" : "PATH_MODE_CONTINUOUS";
        return $"{word} is not written: [transform] {key} of the machine is empty or missing, and the control keeps "
            + "the path mode it has (machine-config 5, D349).";
    }
}
