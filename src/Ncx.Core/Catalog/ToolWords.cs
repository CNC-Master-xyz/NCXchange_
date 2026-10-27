namespace Ncx.Core.Catalog;

/// <summary>
/// The tool words of language 4.4: the two words of a tool change, PRELOAD and TOOL, the offset registers and the
/// cutter radius compensation.
/// </summary>
internal static class ToolWords
{
    /// <summary>
    /// The rows of the table of language 4.4; the bare TOOL is the form of TOOL without a value.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        // PRELOAD prepares the tool of a holder like TOOL, addressed by the holder role and without one for the default
        // holder (virtual machine 2.3, PRELOAD[:r]; 3.8 rule 2; language 4.10); the row of language 4.4 names no
        // address.
        new WordDefinition
        {
            Key = "PRELOAD",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.Integer | ValueKinds.String,
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Preload,
            Section = "4.4",
            Description = "Prepares a tool in the magazine, modal until the change consumes it; PRELOAD=0 clears a "
                + "pending preload (D47).",
        },
        new WordDefinition
        {
            Key = "TOOL",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.Bare | ValueKinds.Integer | ValueKinds.String,
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Tool,
            Section = "4.4",
            Description = "The spindle or the addressed holder now carries this tool, TOOL=0 empties it; a bare TOOL "
                + "changes to the preloaded tool (D47, D91).",
        },

        // OFFSET alone is the combined register; LEN and RAD are the fixed addresses, in the order of bucket 9 (D90).
        new WordDefinition
        {
            Key = "OFFSET",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.Integer,
            AddrKind = AddrKind.OffsetKind,
            AddrRanks = new Dictionary<string, int>
            {
                ["LEN"] = CanonicalRanks.OffsetLen,
                ["RAD"] = CanonicalRanks.OffsetRad,
            },
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Offset,
            Section = "4.4",
            Description = "Combined offset register; OFFSET:LEN the tool length offset register (G43 H), OFFSET:RAD "
                + "the tool radius offset register (D); 0 cancels.",
        },

        // The measurement point of the tool: its number k with the holder role as an optional address, its axial
        // offset a from the tip and its register radius r, each modal per holder, valid in a TOOL block or in a block
        // of their own, taking effect with the next tool-axis motion (D372). TIP_OFFSET and POINT_RADIUS carry no
        // address and follow the holder of the block (D391).
        new WordDefinition
        {
            Key = "TOOL_POINT",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.Integer,
            AddrKind = AddrKind.Role,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.ToolPoint,
            Section = "4.4",
            Description = "The measurement point k of the active tool, from 1; a TOOL without it resets the holder to "
                + "point 1 (D372).",
        },
        new WordDefinition
        {
            Key = "TIP_OFFSET",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.NumberOrExpr,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.TipOffset,
            Section = "4.4",
            Description = "The axial offset a of the measurement point from the tool tip, not negative; never written "
                + "to the control (D372, D375).",
        },
        new WordDefinition
        {
            Key = "POINT_RADIUS",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.NumberOrExpr,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.PointRadius,
            Section = "4.4",
            Description = "The register radius r of the measurement point, not negative, 0 for a point on the axis; "
                + "never written to the control (D372, D375).",
        },
        new WordDefinition
        {
            Key = "COMP",
            Group = WordKind.Tool,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["LEFT", "RIGHT", "OFF"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Comp,
            Section = "4.4",
            Description = "Cutter radius compensation (G41, G42, G40), from the motion of the same block on.",
        },
    ];
}
