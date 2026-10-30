using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The params of the first block of a two-block Fanuc cycle, G71 U2. R0.5 before G71 P10 Q20 U0.5 W0.1, which the
/// reader writes on the CYCLE block of the second block, the one block of NCX (D409).
/// </summary>
/// <param name="Block">The first block as the source writes it, which stays RAW in front of a second block that stays
/// RAW.</param>
/// <param name="SecondLine">The line of the second block, which takes the params.</param>
/// <param name="Params">The params of the first block by their catalog name, in source order.</param>
internal sealed record FanucFirstBlock(SourceBlock Block, int SecondLine,
    IReadOnlyList<KeyValuePair<string, Value>> Params);
