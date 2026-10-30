using Ncx.Core.Model;

namespace Ncx.Core.Machine;

/// <summary>
/// [format]: how the compiler writes numbers, block numbers, line endings and the ends of programs and subprograms
/// (machine-config 2).
/// </summary>
public sealed record OutputFormat
{
    /// <summary>
    /// decimal_separator: "." or "," for Heidenhain; null when the file leaves it out.
    /// </summary>
    public string? DecimalSeparator { get; init; }

    /// <summary>
    /// decimals: the decimals per address, X = 3, F = 1, S = 0; empty when the file leaves it out.
    /// </summary>
    public IReadOnlyDictionary<string, int> Decimals { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// trailing_zeros: whether trailing zeros are written; false when the file leaves it out.
    /// </summary>
    public bool TrailingZeros { get; init; }

    /// <summary>
    /// block_numbers: whether and how blocks are numbered; null when the file leaves it out.
    /// </summary>
    public BlockNumbering? BlockNumbers { get; init; }

    /// <summary>
    /// line_ending: CRLF or LF; null when the file leaves it out.
    /// </summary>
    public LineEnding? LineEnding { get; init; }

    /// <summary>
    /// program_end: the template written for PROGRAM=END, M30 (D48, D49); null when the file leaves it out.
    /// </summary>
    public string? ProgramEnd { get; init; }

    /// <summary>
    /// sub_end: the template written for SUB=END and RETURN, M99, "LBL 0", RET; null when the file leaves it out.
    /// </summary>
    public string? SubEnd { get; init; }

    /// <summary>
    /// program_layout: one output file for all programs, or one file per program (D48); null when the file leaves it
    /// out.
    /// </summary>
    public ProgramLayout? ProgramLayout { get; init; }

    /// <summary>
    /// max_line_length: the longest line the control takes, 0 for unlimited; null when the file leaves it out.
    /// </summary>
    public int? MaxLineLength { get; init; }

    /// <summary>
    /// comment_charset: the characters comments are written in, "ASCII" or "UTF-8"; null when the file leaves it out,
    /// and then "ASCII" on every family (D263).
    /// </summary>
    public string? CommentCharset { get; init; }

    /// <summary>
    /// external_call: the Fanuc code that calls a program outside the file, which the control takes from parameter
    /// 6030 and a builder may set to something else; the compiler writes it in place of M98 for a CALL of an external
    /// program and the reader reads it as CALL (D322). Null when the file leaves it out: the compiler then writes M98
    /// and the reader reads no external call. A machine whose [sync] mark range covers the code does not name it, and
    /// the reader keeps the code as a wait mark there.
    /// </summary>
    public string? ExternalCall { get; init; }

    /// <summary>
    /// channel_files: how the job compiler names the file of each channel, O1000 and O1000.P-2, _C1 and _C2, or after
    /// the program NAME; null when the file leaves it out, and then each channel keeps the name the compiler gives the
    /// file of its program. Two channels that would write one file get _C&lt;n&gt; with a WARNING, as D266 does
    /// (D310, machine-config 2).
    /// </summary>
    public ChannelFiles? ChannelFiles { get; init; }

    // The four keys below are the [format] options of the habits of the Fanuc sources, adopted as coded; only the
    // Fanuc compiler reads them, header is a template like program_end (D105 normalisation, split at \n) whose G codes
    // are the control state after it, and each flag defaults to false. The waiting G43 H keeps its coded places
    // (D276, machine-config 2).

    /// <summary>
    /// header: the start block of the machine, the lines a compiler writes after the beginning of every program, "G0
    /// G40\nG80 G90 G94 G98" on the Fanuc mill of the sources; null when the file leaves it out.
    /// </summary>
    public string? Header { get; init; }

    /// <summary>
    /// calculator_input: parameter 3401#0 DPI of a Fanuc control. True, the calculator type, takes a number without a
    /// decimal point in an address that can take one in mm, inch, degrees or seconds (X70 is 70 mm); false counts it in
    /// least input increments (X70 is 0.070 mm). An address that takes no decimal point always counts increments
    /// (B-64484EN p.229, B-64490EN-05 p.190; controllers fanuc.md 2). True when the file leaves it out.
    /// </summary>
    public bool CalculatorInput { get; init; } = true;

    /// <summary>
    /// plane_with_first_motion: the code of the working plane stands in the first motion block after it changes, G0
    /// G17 X50.4 of the sources, not in a block of its own; false when the file leaves it out.
    /// </summary>
    public bool PlaneWithFirstMotion { get; init; }

    /// <summary>
    /// motion_code_after_tool_change: the first motion after a tool change writes its motion code although the control
    /// has it active, G0 G90 X10. after T2 M6 of the sources; false when the file leaves it out.
    /// </summary>
    public bool MotionCodeAfterToolChange { get; init; }

    /// <summary>
    /// length_offset_with_tool_axis: the tool length offset stands in the first block after it that moves the tool
    /// axis in the workpiece frame, a RAPID, LINE or ARC with a word of the tool axis or a CYCLE_CALL, G43 Z2. H1 of
    /// the sources, and not where the OFFSET:LEN word stands. A HOME and a G53 move, which end at machine positions,
    /// leave it waiting; it stands in a line of its own before a LABEL, JUMP, CALL or RAW block, before a skipped block
    /// that moves the tool axis and before the end of the program or subprogram, so that no path runs without it, and
    /// before the line of a SETPOS with a word of the tool axis, which declares that position with the offset active.
    /// False when the file leaves it out.
    /// </summary>
    public bool LengthOffsetWithToolAxis { get; init; }

    /// <summary>
    /// label_max: the largest label number of the target, the end of the range the Heidenhain compiler numbers the
    /// labels of a Klartext program in; null when the file leaves it out, and then 999 (D271: iTNC 530 from NC-SW 340
    /// 49x, 254 for 340422, 65535 for the TNC 640).
    /// </summary>
    public int? LabelMax { get; init; }

    /// <summary>
    /// counter_registers: the first and the last Q parameter of the range the Heidenhain compiler counts a REPEAT in
    /// when a JUMP leaves its section, ["QL90", "QL99"], both of one kind, Q, QL or QR; empty when the file leaves it
    /// out, and then such a REPEAT is an ERROR, never a guessed register (D213, D388 (12)).
    /// Gap closed 2026-10-27 (delegated): the form is an array of the first and the last name, as [variables]
    /// repeat_counters lists names.
    /// </summary>
    public IReadOnlyList<string> CounterRegisters { get; init; } = [];

    /// <summary>
    /// motion_code: on which motion blocks the verb is written; null when the file leaves it out, and then the family
    /// value (D299).
    /// </summary>
    public MotionCode? MotionCode { get; init; }

    /// <summary>
    /// m_codes_per_block: how many M codes a Fanuc block may carry, 1 to 5; null when the file leaves it out, and then
    /// 1, which every control accepts. 3 needs parameter 3404#7 M3B, 5 needs 11630#5 M5B on the 30i-B; a machine file
    /// sets more only from its builder's documentation. Codes beyond the limit move to the following lines (D339).
    /// </summary>
    public int? MCodesPerBlock { get; init; }

    /// <summary>
    /// m_codes_combinable: when set, the only M codes that may be the second and later M code of a block, normalized
    /// (D105); null when the file leaves it out, and then no list restricts them, while an empty list lets no code
    /// be the second M code of a block (D339). M00, M01, M02, M30, M98, M99, M198, the [sync] wait range and the
    /// macro-call codes always stand alone, and a spindle code shares a block only with a code this list names,
    /// whatever the keys say.
    /// </summary>
    public IReadOnlyList<string>? MCodesCombinable { get; init; }

    /// <summary>
    /// helix_feed: along which path the control measures F on a helix; null when the file leaves it out, and then the
    /// family value (D354).
    /// </summary>
    public HelixFeed? HelixFeed { get; init; }

    /// <summary>
    /// The value of the format keys a file leaves out, per controller family (D263, D271, D299, D339, D354): LF line
    /// endings; no block numbers, and start 10 step 10 when they are enabled, except Heidenhain, which numbers from 0
    /// in steps of 1; max_line_length 0 (unlimited), except Siemens 512; comment_charset "ASCII"; label_max 999;
    /// motion_code "arcs" on Fanuc and "every_block" on Siemens and Heidenhain; m_codes_per_block 1; helix_feed "arc"
    /// on Fanuc and "path" on Heidenhain and Siemens. Keys without a family value (decimals, decimal_separator, the
    /// templates) stay as the file writes them; the defaults of decimals belong to the number formatter (D366).
    /// </summary>
    /// <param name="controller">The family of the machine; null on the built-in default machine of D103, which takes
    /// the values every family shares.</param>
    /// <returns>A copy in which every key above holds a value.</returns>
    public OutputFormat WithFamilyDefaults(Controller? controller)
    {
        bool heidenhain = controller == Machine.Controller.Heidenhain;
        bool fanuc = controller == Machine.Controller.Fanuc;
        // Klartext always numbers its blocks: on Heidenhain a block_numbers table without enabled is enabled (D263).
        BlockNumbering numbering = BlockNumbers ?? new BlockNumbering();
        return this with
        {
            LineEnding = LineEnding ?? Model.LineEnding.Lf,
            BlockNumbers = numbering with
            {
                Enabled = numbering.Enabled ?? heidenhain,
                Start = numbering.Start ?? (heidenhain ? 0 : 10),
                Step = numbering.Step ?? (heidenhain ? 1 : 10),
            },
            MaxLineLength = MaxLineLength ?? (controller == Machine.Controller.Siemens ? 512 : 0),
            CommentCharset = CommentCharset ?? "ASCII",
            LabelMax = LabelMax ?? 999,
            MotionCode = MotionCode ?? (fanuc ? Machine.MotionCode.Arcs : Machine.MotionCode.EveryBlock),
            MCodesPerBlock = MCodesPerBlock ?? 1,
            HelixFeed = HelixFeed ?? (fanuc ? Machine.HelixFeed.Arc : Machine.HelixFeed.Path),
        };
    }
}
