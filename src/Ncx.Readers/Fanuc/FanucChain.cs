using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The chain of transforms the reader wrote (language 4.2, D31; virtual machine 2.1) and the G52 shift of the control:
/// the entries in the order the source set them, and whether the reader knows the whole chain, which it does not in a
/// subprogram whose callers leave it open or after a call of a subprogram that changes it (virtual machine 3.9). A
/// change of the control's transforms is written as one cut of the chain and the entries after it, and only where the
/// cut means the same under both readings of SHIFT=RESET (wave-1 question #95).
/// </summary>
internal sealed class FanucChain
{
    private static readonly IdentValue s_reset = new("RESET");
    private static readonly IdentValue s_off = new("OFF");

    private readonly List<FanucChainEntry> _entries = [];

    // The modes of the control that a block kept as RAW switched on, which the chain of NCX therefore does not hold:
    // "G68" (a rotation), "G68.2" (a tilted plane) and "G51.1" (a mirror) (D244, D245).
    private readonly HashSet<string> _native = new(StringComparer.Ordinal);

    // The axes a G51.1 kept as RAW mirrors on the control, which a G50.1 of the axis cancels (D124, D245; B-64484EN
    // p.367).
    private readonly HashSet<string> _nativeMirror = new(StringComparer.Ordinal);

    /// <summary>
    /// True while the reader knows the whole chain: from PROGRAM=BEGIN and from an ORIGIN on (language 4.2).
    /// </summary>
    public bool Known { get; private set; } = true;

    /// <summary>
    /// The entries, in chain order.
    /// </summary>
    public IReadOnlyList<FanucChainEntry> Entries => _entries;

    /// <summary>
    /// The active G52 shift of the control per axis, which an incremental word of the next G52 adds to; the control
    /// sets it also where the reader keeps the G52 as RAW (FanucFrames).
    /// </summary>
    public Dictionary<string, decimal> Local { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The three-dimensional coordinate conversions (G68 with I J K) the control holds, kept as RAW: a second one is
    /// measured in the system of the first, a third raises PS5043, and one G69 ends both (D245; B-64484EN p.342-344).
    /// </summary>
    public int Conversions { get; set; }

    /// <summary>
    /// ORIGIN starts an empty chain (language 4.2); the G52 shift of the control holds in all of G54..G59, so Local
    /// stays (D244 (a); B-64484EN p.216-217).
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
        Known = true;
    }

    /// <summary>
    /// A program begins with an empty chain, no G52 shift and no mode on (virtual machine 2.1).
    /// </summary>
    public void Reset()
    {
        Clear();
        Local.Clear();
        _native.Clear();
        _nativeMirror.Clear();
        Conversions = 0;
    }

    /// <summary>
    /// The chain is the unknown one of a caller, or the one a called subprogram left (virtual machine 3.9).
    /// </summary>
    public void Forget()
    {
        _entries.Clear();
        Local.Clear();
        _native.Clear();
        _nativeMirror.Clear();
        Conversions = 0;
        Known = false;
    }

    /// <summary>
    /// Takes over another chain, the one of the caller of a subprogram (virtual machine 3.9).
    /// </summary>
    /// <param name="other">The chain to take over.</param>
    public void CopyFrom(FanucChain other)
    {
        _entries.Clear();
        _entries.AddRange(other._entries);
        Local.Clear();
        foreach (KeyValuePair<string, decimal> axis in other.Local)
        {
            Local[axis.Key] = axis.Value;
        }

        _native.Clear();
        _nativeMirror.Clear();
        _native.UnionWith(other._native);
        _nativeMirror.UnionWith(other._nativeMirror);
        Conversions = other.Conversions;
        Known = other.Known;
    }

    /// <summary>
    /// A copy of the chain, which a block kept as RAW goes back to (FanucFrames.KeepRawModes).
    /// </summary>
    public FanucChain Copy()
    {
        var copy = new FanucChain();
        copy.CopyFrom(this);
        return copy;
    }

    /// <summary>
    /// Drops the entries of these functions without writing a change: a cancel the reader keeps as RAW on a chain it
    /// does not know (D225).
    /// </summary>
    /// <param name="owners">The functions, "G68", "G68.2".</param>
    public void Drop(params string[] owners)
    {
        _entries.RemoveAll(entry => owners.Contains(entry.Owner));
    }

    /// <summary>
    /// Tells whether an entry of a Fanuc function stands in the chain.
    /// </summary>
    /// <param name="owner">The function, "G68.2".</param>
    public bool Holds(string owner)
    {
        foreach (FanucChainEntry entry in _entries)
        {
            if (entry.Owner == owner)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells whether a mode of the control is on, written into the chain or switched on by a block kept as RAW (D244,
    /// D245).
    /// </summary>
    /// <param name="owner">The function, "G68", "G68.2" or "G51.1".</param>
    public bool Active(string owner)
    {
        return Holds(owner) || HoldsNative(owner);
    }

    /// <summary>
    /// Tells whether a block kept as RAW switched on a mode of the control (D245).
    /// </summary>
    /// <param name="owner">The function, "G68", "G68.2" or "G51.1".</param>
    public bool HoldsNative(string owner)
    {
        return owner == "G51.1" ? _nativeMirror.Count > 0 : _native.Contains(owner);
    }

    /// <summary>
    /// The axes a G51.1 kept as RAW mirrors on the control (D245).
    /// </summary>
    public IReadOnlyCollection<string> NativeMirrorAxes => _nativeMirror;

    /// <summary>
    /// A G51.1 kept as RAW mirrors these axes on the control, or a G50.1 cancels them there (D124, D245).
    /// </summary>
    /// <param name="axes">The axes.</param>
    /// <param name="on">True when the axes are mirrored afterwards.</param>
    public void SetNativeMirror(IEnumerable<string> axes, bool on)
    {
        if (on)
        {
            _nativeMirror.UnionWith(axes);
        }
        else
        {
            _nativeMirror.ExceptWith(axes);
        }
    }

    /// <summary>
    /// A block kept as RAW switches a mode of the control on, or its cancel switches it off (D244, D245).
    /// </summary>
    /// <param name="owner">The function, "G68", "G68.2" or "G51.1".</param>
    /// <param name="on">True when the mode is on afterwards.</param>
    public void SetNative(string owner, bool on)
    {
        if (on)
        {
            _native.Add(owner);
        }
        else
        {
            _native.Remove(owner);
        }
    }

    /// <summary>
    /// The chain as text, for comparing the chains two callers leave.
    /// </summary>
    public string ToKey()
    {
        var parts = new List<string> { Known ? "known" : "unknown" };
        foreach (FanucChainEntry entry in _entries)
        {
            parts.Add(entry.ToKey());
        }

        var axes = new List<string>(Local.Keys);
        axes.Sort(StringComparer.Ordinal);
        foreach (string axis in axes)
        {
            parts.Add(axis + "=" + Local[axis].ToString(CultureInfo.InvariantCulture));
        }

        var native = new List<string>(_native);
        native.Sort(StringComparer.Ordinal);
        foreach (string owner in native)
        {
            parts.Add("native " + owner);
        }

        var mirrored = new List<string>(_nativeMirror);
        mirrored.Sort(StringComparer.Ordinal);
        parts.Add("native mirror " + string.Join(",", mirrored));

        parts.Add("conversions " + Conversions.ToString(CultureInfo.InvariantCulture));

        return string.Join("|", parts);
    }

    /// <summary>
    /// Appends an entry and writes it (language 4.2: every transform is appended to the chain where it stands).
    /// </summary>
    /// <param name="entry">The new entry.</param>
    /// <param name="blocks">The NCX blocks of the change, the entry's block added.</param>
    public void Append(FanucChainEntry entry, List<DraftBlock> blocks)
    {
        _entries.Add(entry);
        blocks.Add(entry.ToDraft());
    }

    /// <summary>
    /// Changes the chain to the one the control holds now: the entries both share stay, the chain is cut at the first
    /// entry that goes, and the entries of the new chain after the cut are appended again (language 4.2: "The chain is
    /// unwound from the end only: a RESET of a kind removes that entry and everything after it"). Where no cut at or in
    /// front of that entry means the same under both readings, the chain stays as it is and the result is false.
    /// </summary>
    /// <param name="desired">The chain the control holds, its shared entries the same objects as this chain's.</param>
    /// <param name="blocks">The NCX blocks of the change, in order: the RESET, then the entries appended.</param>
    public bool TryChange(IReadOnlyList<FanucChainEntry> desired, List<DraftBlock> blocks)
    {
        if (!Known)
        {
            return false;
        }

        int kept = 0;
        while (kept < _entries.Count && kept < desired.Count && ReferenceEquals(_entries[kept], desired[kept]))
        {
            kept++;
        }

        if (kept < _entries.Count)
        {
            int cut = kept;
            while (cut >= 0 && !CutsAt(cut))
            {
                cut--;
            }

            if (cut < 0)
            {
                return false;
            }

            string kind = _entries[cut].Kind;
            blocks.Add(new DraftBlock().Add(kind, kind == "MIRROR" ? s_off : s_reset));
            kept = cut;
        }

        for (int index = kept; index < desired.Count; index++)
        {
            blocks.Add(desired[index].ToDraft());
        }

        _entries.Clear();
        _entries.AddRange(desired);
        return true;
    }

    // Whether the RESET of the entry's kind cuts the chain at this entry under every reading of the documents.
    private bool CutsAt(int index)
    {
        string kind = _entries[index].Kind;
        int count = 0;
        bool later = false;
        for (int other = 0; other < _entries.Count; other++)
        {
            if (_entries[other].Kind == kind)
            {
                count++;
                later |= other > index;
            }
        }

        return kind switch
        {
            // SHIFT=RESET removes "the shifts" (the row of language 4.2) or cuts at the last shift (the chain paragraph
            // of 4.2, virtual machine 2.1): with one shift in the chain both are that entry (wave-1 question #95).
            "SHIFT" => count == 1,

            // MIRROR has OFF where the other entries have RESET (language 4.2), which the virtual machine reads as the
            // cut at the last mirror: for the one mirror at the end of the chain every reading removes that entry.
            "MIRROR" => count == 1 && index == _entries.Count - 1,

            // ROTATE=RESET and TILT=RESET remove the last entry of their kind and what follows (language 4.2, virtual
            // machine 2.1).
            _ => !later,
        };
    }
}
