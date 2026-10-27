using System.Collections;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The state keys of a channel whose values are UNKNOWN although their row holds a value (ChannelState.Unknown). The
/// keys of the spindle rows, RPM:S1 and the like, belong to the rows themselves, which are one per job (D308): they
/// stand in the ResourceState, so that every channel of the job sees the spindle speed another channel set from an
/// expression as UNKNOWN and never the stale value before it. Every other key stays per channel.
/// </summary>
internal sealed class UnknownKeys : IEnumerable<string>
{
    private readonly HashSet<string> _own = new(StringComparer.Ordinal);
    private readonly ResourceState _resources;

    /// <summary>
    /// The UNKNOWN keys of a channel whose shared rows are those of resources.
    /// </summary>
    public UnknownKeys(ResourceState resources)
    {
        _resources = resources;
    }

    public bool Add(string key)
    {
        return SetOf(key).Add(key);
    }

    public bool Remove(string key)
    {
        return SetOf(key).Remove(key);
    }

    public bool Contains(string key)
    {
        return SetOf(key).Contains(key);
    }

    public IEnumerator<string> GetEnumerator()
    {
        var all = new List<string>(_own);
        all.AddRange(_resources.Unknown);
        return all.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // A key whose address is a spindle of the machine, RPM:S1, belongs to the shared spindle rows (D308).
    private HashSet<string> SetOf(string key)
    {
        int colon = key.LastIndexOf(':');
        return colon > 0 && _resources.Spindles.ContainsKey(key[(colon + 1)..]) ? _resources.Unknown : _own;
    }
}
