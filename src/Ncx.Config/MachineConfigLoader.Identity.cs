using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config;

// Machine-config 1 to 3: the identity and dialect, the output format, the tool change, home and setpos.
public static partial class MachineConfigLoader
{
    private static readonly string[] s_machineKeys =
    [
        "name", "controller", "dialect", "builder", "gcode_system", "s_binds_to_spindle_word", "limits", "channels",
        "units_default", "default_spindle", "default_holder", "default_workpiece", "tool_table", "kinematics",
        "arc_tolerance", "arc_radius_tolerance", "dwell_increment", "input_increment", "base_units", "extended_units",
    ];

    private static readonly string[] s_controllers = ["fanuc", "heidenhain", "siemens"];
    private static readonly string[] s_gcodeSystems = ["A", "B", "C"];
    private static readonly string[] s_limitPolicies = ["warn", "clamp"];
    private static readonly string[] s_units = ["MM", "INCH"];
    private static readonly string[] s_incrementSystems = ["IS-A", "IS-B", "IS-C"];

    // base_units takes the words of MD10240 in the lower case of the D292 row.
    private static readonly string[] s_baseUnits = ["mm", "inch"];

    private static readonly string[] s_formatKeys =
    [
        "decimal_separator", "decimals", "trailing_zeros", "block_numbers", "line_ending", "program_end", "sub_end",
        "program_layout", "max_line_length", "comment_charset", "external_call", "header",
        "plane_with_first_motion", "calculator_input", "motion_code_after_tool_change", "length_offset_with_tool_axis",
        "channel_files",
        "label_max", "motion_code", "m_codes_per_block", "m_codes_combinable", "helix_feed", "counter_registers",
    ];

    // comment_charset takes these two values; any other is CFG005 (D263).
    private static readonly string[] s_commentCharsets = ["ASCII", "UTF-8"];
    private static readonly string[] s_motionCodes = ["change", "arcs", "every_block"];
    private static readonly string[] s_helixFeeds = ["path", "arc"];

    private static readonly string[] s_blockNumberKeys = ["enabled", "start", "step"];
    private static readonly string[] s_lineEndings = ["CRLF", "LF"];
    private static readonly string[] s_programLayouts = ["one_file", "file_per_program"];
    private static readonly string[] s_channelFiles = ["path_suffix", "channel_suffix", "program_name"];

    private static readonly string[] s_toolChangeKeys =
    [
        "change", "change_named", "change_preloaded", "preload", "preload_named", "unload",
        "auto_preload", "preload_position", "offsets_with_change",
        "tool_name_allowed", "kind_map", "pre", "post", "requires", "restore", "length_offset_by_macro",
    ];

    private static readonly string[] s_preloadPositions = ["after_change", "before_first_motion"];
    private static readonly string[] s_homeKeys = ["template", "point"];
    private static readonly string[] s_setposKeys = ["template"];

    // [machine]: name and controller are required; the controller selects the reader and the compiler, the defaults
    // resolve the words without a role address, units_default is a value of UNITS (machine-config 1, language 4.2).
    private static MachineIdentity ReadMachine(ConfigTable? table)
    {
        if (table is null)
        {
            return new MachineIdentity { Name = "" };
        }

        table.WarnUnknownKeys(s_machineKeys);
        return new MachineIdentity
        {
            Name = table.RequiredText("name") ?? "",
            Controller = table.RequiredChoice("controller", s_controllers) switch
            {
                "fanuc" => Controller.Fanuc,
                "heidenhain" => Controller.Heidenhain,
                "siemens" => Controller.Siemens,
                _ => null,
            },
            Dialect = table.Text("dialect"),
            Builder = table.Text("builder"),
            GcodeSystem = table.Choice("gcode_system", s_gcodeSystems) switch
            {
                "A" => GcodeSystem.A,
                "B" => GcodeSystem.B,
                "C" => GcodeSystem.C,
                _ => null,
            },
            SBindsToSpindleWord = table.Flag("s_binds_to_spindle_word"),
            Channels = table.IntegerList("channels"),
            UnitsDefault = table.Choice("units_default", s_units),
            DefaultSpindle = table.Text("default_spindle"),
            DefaultHolder = table.Text("default_holder"),
            DefaultWorkpiece = table.Text("default_workpiece"),

            // The tolerances are lengths in millimetres like rapid and max_feed (D139, D364).
            ArcTolerance = table.PositiveNumber("arc_tolerance"),
            ArcRadiusTolerance = table.PositiveNumber("arc_radius_tolerance"),
            DwellIncrement = table.PositiveNumber("dwell_increment") ?? 0.001m,

            // The increment system of a Fanuc control, parameter 1013 (B-64490EN-05 p.57-58): the least input
            // increment in mm, IS-B when the file leaves it out (controllers fanuc.md 2).
            InputIncrement = table.Choice("input_increment", s_incrementSystems) switch
            {
                "IS-A" => 0.01m,
                "IS-C" => 0.0001m,
                _ => 0.001m,
            },

            // The basic system and G700/G710 of a Siemens control, MD10240 and MD10260 (D292).
            BaseUnits = table.Choice("base_units", s_baseUnits) switch
            {
                "mm" => "MM",
                "inch" => "INCH",
                _ => null,
            },
            ExtendedUnits = table.Flag("extended_units"),
        };
    }

    // [machine] limits: what the expander does beyond the machine limits; the limits warn by default (machine-config
    // 1, D64).
    private static LimitPolicy ReadLimitPolicy(ConfigTable? table)
    {
        return table?.Choice("limits", s_limitPolicies) switch
        {
            "clamp" => LimitPolicy.Clamp,
            _ => LimitPolicy.Warn,
        };
    }

    // [format]: how the compiler writes numbers, block numbers and line endings; program_end and sub_end are templates
    // whose M and G codes are normalized (machine-config 2, 5, D105).
    private static OutputFormat? ReadFormat(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_formatKeys);
        return new OutputFormat
        {
            DecimalSeparator = table.Text("decimal_separator"),
            Decimals = table.Table("decimals")?.Integers() ?? new Dictionary<string, int>(),
            TrailingZeros = table.Flag("trailing_zeros"),
            BlockNumbers = ReadBlockNumbers(table.Table("block_numbers")),
            LineEnding = table.Choice("line_ending", s_lineEndings) switch
            {
                "CRLF" => LineEnding.CrLf,
                "LF" => LineEnding.Lf,
                _ => null,
            },
            ProgramEnd = table.Template("program_end"),
            SubEnd = table.Template("sub_end"),
            ProgramLayout = table.Choice("program_layout", s_programLayouts) switch
            {
                "one_file" => ProgramLayout.OneFile,
                "file_per_program" => ProgramLayout.FilePerProgram,
                _ => null,
            },
            MaxLineLength = table.Integer("max_line_length"),
            CommentCharset = table.Choice("comment_charset", s_commentCharsets),
            ExternalCall = table.Template("external_call"),

            // The options of the Fanuc output habits, adopted as coded; the header is a template like program_end
            // (D105, D276).
            Header = table.Template("header"),
            PlaneWithFirstMotion = table.Flag("plane_with_first_motion"),
            MotionCodeAfterToolChange = table.Flag("motion_code_after_tool_change"),
            LengthOffsetWithToolAxis = table.Flag("length_offset_with_tool_axis"),

            // Parameter 3401#0 DPI of a Fanuc control, calculator-type decimal point input, true when the file leaves
            // it out, as controllers fanuc.md 2 assumes (B-64490EN-05 p.190).
            CalculatorInput = table.FlagOr("calculator_input", true),

            // The naming of the files of a job's channels (D310, machine-config 2).
            ChannelFiles = table.Choice("channel_files", s_channelFiles) switch
            {
                "path_suffix" => ChannelFiles.PathSuffix,
                "channel_suffix" => ChannelFiles.ChannelSuffix,
                "program_name" => ChannelFiles.ProgramName,
                _ => null,
            },

            // The label range of a Klartext program, LBL 1 to label_max; 65535 is the largest of the TNC 640 (D271).
            LabelMax = table.IntegerInRange("label_max", 1, 65535),

            // The Q parameters the Heidenhain compiler counts a REPEAT in, the first and the last (D213, D388 (12)).
            CounterRegisters = ReadCounterRegisters(table),
            MotionCode = table.Choice("motion_code", s_motionCodes) switch
            {
                "change" => MotionCode.Change,
                "arcs" => MotionCode.Arcs,
                "every_block" => MotionCode.EveryBlock,
                _ => null,
            },

            // The M codes of one Fanuc block: 1 to 5, and the codes that may join another (D339).
            MCodesPerBlock = table.IntegerInRange("m_codes_per_block", 1, 5),
            // Null without the key: no list restricts the codes; an empty list lets no code join another (D339).
            MCodesCombinable = table.Has("m_codes_combinable")
                ? table.TextList("m_codes_combinable").Select(FunctionValues.Normalize).ToList()
                : null,
            HelixFeed = table.Choice("helix_feed", s_helixFeeds) switch
            {
                "path" => HelixFeed.Path,
                "arc" => HelixFeed.Arc,
                _ => null,
            },
        };
    }

    // counter_registers = ["QL90", "QL99"]: two names of one kind of Q parameter, Q, QL or QR, the first not after
    // the last; any other value is CFG005, as the other [format] values out of their range are (D213, D388 (12),
    // D271).
    private static IReadOnlyList<string> ReadCounterRegisters(ConfigTable table)
    {
        IReadOnlyList<string> names = table.TextList("counter_registers");
        if (names.Count == 0 && !table.Has("counter_registers"))
        {
            return [];
        }

        if (names.Count == 2 && CounterRange.TryParse(names[0], names[1], out _))
        {
            return names;
        }

        table.Diagnostics.Error(table.LineOf("counter_registers"), DiagnosticCodes.ValueNotAllowed,
            "[format] counter_registers must be the first and the last name of one kind of Q parameter, Q, QL or QR, "
            + $"the first not after the last, [\"QL90\", \"QL99\"], not [{string.Join(", ", names)}] "
            + "(machine-config 2).");
        return [];
    }

    // block_numbers = { enabled = true, start = 10, step = 10 } (machine-config 2).
    private static BlockNumbering? ReadBlockNumbers(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_blockNumberKeys);
        return new BlockNumbering
        {
            Enabled = table.Has("enabled") ? table.Flag("enabled") : null,
            Start = table.Integer("start"),
            Step = table.Integer("step"),
        };
    }

    // [tool_change]: the templates written for PRELOAD and TOOL, normalized like every template (D105), the preload
    // settings, the values of {kind} (D52) and the expansion rule of the change (machine-config 3, 5a).
    private static ToolChangeConfig? ReadToolChange(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_toolChangeKeys);
        return new ToolChangeConfig
        {
            Change = table.Template("change"),
            ChangeNamed = table.Template("change_named"),
            ChangePreloaded = table.Template("change_preloaded"),
            Preload = table.Template("preload"),
            PreloadNamed = table.Template("preload_named"),
            Unload = table.Template("unload"),
            AutoPreload = table.Flag("auto_preload"),
            PreloadPosition = table.Choice("preload_position", s_preloadPositions) switch
            {
                "after_change" => PreloadPosition.AfterChange,
                "before_first_motion" => PreloadPosition.BeforeFirstMotion,
                _ => null,
            },
            OffsetsWithChange = table.Flag("offsets_with_change"),
            LengthOffsetByMacro = table.Flag("length_offset_by_macro"),
            ToolNameAllowed = table.Flag("tool_name_allowed"),
            KindMap = table.Table("kind_map")?.PlaceholderValues(null) ?? new Dictionary<string, string>(),
            Rule = ReadRule(table),
        };
    }

    // [home]: the templates of reference point 1 and of the further points (machine-config 3).
    private static HomeConfig? ReadHome(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_homeKeys);
        return new HomeConfig { Template = table.Template("template"), Point = table.Template("point") };
    }

    // [setpos]: the template of SETPOS (machine-config 3).
    private static SetposConfig? ReadSetpos(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_setposKeys);
        return new SetposConfig { Template = table.Template("template") };
    }
}
