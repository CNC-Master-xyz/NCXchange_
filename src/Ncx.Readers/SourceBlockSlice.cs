using System.Collections;

namespace Ncx.Readers;

/// <summary>
/// A read-only window of a list of source blocks, made in constant time: the look-ahead a reader rule sees in
/// SourceBlock.Following, over the one list of the blocks the reader reads (D232).
/// </summary>
internal sealed class SourceBlockSlice : IReadOnlyList<SourceBlock>
{
    private readonly List<SourceBlock> _blocks;
    private readonly int _start;

    /// <param name="blocks">The shared list.</param>
    /// <param name="start">The index of the first block of the window.</param>
    /// <param name="count">The number of blocks of the window.</param>
    public SourceBlockSlice(List<SourceBlock> blocks, int start, int count)
    {
        _blocks = blocks;
        _start = start;
        Count = count;
    }

    public int Count { get; }

    public SourceBlock this[int index] => (uint)index < (uint)Count
        ? _blocks[_start + index]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<SourceBlock> GetEnumerator()
    {
        for (int index = 0; index < Count; index++)
        {
            yield return _blocks[_start + index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
