using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config;

// Machine-config 5 and 5a: the function tables of the spindles, the synchronization, the coolant and the named
// functions with their expansion rules, and the tables workpiece, sync, func_meta, transform, retract, tolerance,
// stock and raw.
public static partial class MachineConfigLoader
{
    private const string FrameSuffix = "_frame";
    private const string MirrorSuffix = "_mirror";

    // The states of the function tables whose word takes fixed values: the spindle words of language 4.5 and 4.11,
    // the modes of SPINDLE_MODE, the synchronization and a coolant channel (machine-config 5, F23). The states of a
    // [func] entry are free identifiers.
    private static readonly string[] s_spindleStates =
        ["CW", "CCW", "OFF", "ORIENT", "RPM", "VC", "CSS_OFF", "RPM_MAX"];

    private static readonly string[] s_spindleModeStates = ["AXIS", "SPINDLE"];
    private static readonly string[] s_spindleSyncStates = ["ON", "OFF", "PHASE"];
    private static readonly string[] s_coolantStates = ["ON", "OFF"];

    // The keys every function table may carry besides its states: the channel binding (D56) and the four keys of the
    // expansion rule (machine-config 5a); a spindle table also carries the machine limits (D64).
    private static readonly string[] s_bindingAndRuleKeys =
        ["channel", "channels", "pre", "post", "requires", "restore", "states"];

    private static readonly string[] s_ruleKeys = ["pre", "post", "requires", "restore"];
    private static readonly string[] s_spindleLimitKeys = ["rpm_min", "rpm_max", "accel_time"];
    private static readonly string[] s_allChannels = ["all"];

    private static readonly string[] s_workpieceFrames = ["datum", "mirror"];

    private static readonly string[] s_syncKeys =
        ["wait", "paths", "mark_range", "start_mark", "reserved_marks", "groups", "start_channel", "wait_channel"];

    private static readonly string[] s_syncPaths = ["list", "bitmask"];
    private static readonly string[] s_syncGroupKeys = ["channels", "range"];

    private static readonly string[] s_transformKeys =
    [
        "CYLINDER_ON", "CYLINDER_OFF", "POLAR_ON", "POLAR_OFF", "TCPM_ON", "TCPM_OFF", "TILT_ON", "TILT_OFF",
        "TILT_AXIS_ON", "TILT_TURN", "move", "rot", "ROTARY_PATH_SHORTEST", "ROTARY_PATH_FULL", "ROTARY_FEED_MM_MIN",
        "ROTARY_FEED_DEG_MIN", "PATH_MODE_EXACT_STOP", "PATH_MODE_CONTINUOUS",
    ];

    private static readonly string[] s_moveOptions = ["TURN", "MOVE", "STAY"];
    private static readonly string[] s_rotOptions = ["TABLE", "COORD"];
    private static readonly string[] s_retractKeys = ["MAX", "BY"];
    private static readonly string[] s_toleranceKeys =
        ["ON", "ON_ROTARY", "OFF", "mode", "held_in", "held_value", "held_comment"];

    private static readonly string[] s_stockKeys = ["BOX", "CYLINDER"];
    private static readonly string[] s_spindleSyncKeys = ["pair"];
    private const string PairKey = "pair";
    private static readonly string[] s_toleranceModes = ["FINISH", "ROUGH"];
    private static readonly string[] s_rawKeys = ["known"];

    // [spindle.ROLE] and [spindle_mode.ROLE]: one function table per role; the VM only knows SPINDLE:role=CW, the
    // template is the machine's business (machine-config 5).
    private static Dictionary<string, FunctionTable> ReadRoleTables(
        ConfigTable? tables, string tableName, IReadOnlyList<string> states, bool spindleLimits)
    {
        var byRole = new Dictionary<string, FunctionTable>(StringComparer.Ordinal);
        if (tables is null)
        {
            return byRole;
        }

        foreach (string role in tables.Keys)
        {
            if (tables.Table(role, "[" + tableName + "." + role + "]") is ConfigTable table)
            {
                byRole[role] = ReadFunctionTable(table, states, spindleLimits);
            }
        }

        return byRole;
    }

    // [coolant] and [func]: one function table per coolant channel or named function (machine-config 5).
    private static Dictionary<string, FunctionTable> ReadNamedTables(
        ConfigTable? tables, IReadOnlyList<string>? states)
    {
        var byName = new Dictionary<string, FunctionTable>(StringComparer.Ordinal);
        if (tables is null)
        {
            return byName;
        }

        foreach (string name in tables.Keys)
        {
            if (tables.Table(name) is ConfigTable table)
            {
                byName[name] = ReadFunctionTable(table, states, spindleLimits: false);
            }
        }

        return byName;
    }

    // [spindle_sync]: ON, OFF and PHASE, bound to a channel or to all of them (machine-config 5, D56).
    private static FunctionTable? ReadSpindleSync(ConfigTable? table)
    {
        return table is null
            ? null
            : ReadFunctionTable(table, s_spindleSyncStates, spindleLimits: false, s_spindleSyncKeys);
    }

    // [spindle_sync] pair = ["MAIN", "SUB"]: the two roles ON couples, the leader first, optional (D154); a pair is two
    // different names, and anything else is reported.
    private static IReadOnlyList<string> ReadSpindleSyncPair(ConfigTable? table)
    {
        if (table is null || !table.Has(PairKey))
        {
            return [];
        }

        IReadOnlyList<string> pair = table.TextList(PairKey);
        if (pair.Count == 2 && pair[0] != pair[1])
        {
            return pair;
        }

        table.Diagnostics.Error(table.LineOf(PairKey), DiagnosticCodes.ValueNotAllowed,
            "pair in [spindle_sync] must name two different roles, the leader first, as [\"MAIN\", \"SUB\"] "
            + "(machine-config 5, D154).");
        return [];
    }

    // [spindle_sync] ON couples the default spindle with the other work spindle; a machine with more than two work
    // spindles, or whose default spindle is not a work spindle, names its pair, which is then required, and the pair
    // names roles of [roles] (D154).
    private static void CheckSpindleSyncPair(MachineConfig machine, ConfigTable? table, Diagnostics diagnostics)
    {
        if (table is null)
        {
            return;
        }

        foreach (string role in machine.SpindleSyncPair)
        {
            if (!machine.Roles.ContainsKey(role))
            {
                diagnostics.Error(table.LineOf(PairKey), DiagnosticCodes.ValueNotAllowed,
                    $"pair in [spindle_sync] names the role {role}, which [roles] does not declare (machine-config 5, "
                    + "D154).");
            }
        }

        int workSpindles = 0;
        foreach (ResourceDef resource in machine.Resources)
        {
            workSpindles += resource.Type == ResourceType.WorkSpindle ? 1 : 0;
        }

        ResourceDef? leader = machine.ResolveDefaultSpindle();
        bool pairNeeded = workSpindles > 2 || (leader is not null && leader.Type != ResourceType.WorkSpindle);
        if (table.Has(PairKey) || !pairNeeded)
        {
            return;
        }

        diagnostics.Error(table.Line, DiagnosticCodes.MissingKey,
            "[spindle_sync] has no pair, which is required where the default spindle is not a work spindle or the "
            + "machine has more than two work spindles (machine-config 5, D154).");
    }

    // A function table: its states are function values, a string or a bare integer that means M and the number, with
    // every M and G code normalized (D105); channel and channels bind it (D56); pre, post, requires and restore make
    // its expansion rule (5a); a spindle table carries the machine limits (D64). The states of a [func] entry are free,
    // those of the other tables the values of their word, and another key there is unknown (machine-config 5, F23).
    private static FunctionTable ReadFunctionTable(
        ConfigTable table, IReadOnlyList<string>? states, bool spindleLimits, IReadOnlyList<string>? ownKeys = null)
    {
        var otherKeys = new List<string>(s_bindingAndRuleKeys);
        if (spindleLimits)
        {
            otherKeys.AddRange(s_spindleLimitKeys);
        }

        // The keys of one table that are neither states nor bindings, the pair of [spindle_sync] (D154).
        otherKeys.AddRange(ownKeys ?? []);

        if (states is not null)
        {
            var known = new List<string>(states);
            known.AddRange(otherKeys);
            table.WarnUnknownKeys(known);
        }

        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in table.Keys)
        {
            bool isState = states is null ? !otherKeys.Contains(key) : states.Contains(key);
            if (isState && table.FunctionValue(key) is string template)
            {
                templates[key] = template;
            }
        }

        return new FunctionTable
        {
            States = templates,
            Channel = table.Integer("channel"),
            AllChannels = table.Choice("channels", s_allChannels) is not null,
            Rule = ReadRule(table),
            RpmMin = spindleLimits ? table.Number("rpm_min") : null,
            RpmMax = spindleLimits ? table.Number("rpm_max") : null,
            AccelTime = spindleLimits ? table.Number("accel_time") : null,
        };
    }

    // The optional keys of a function table, the tool change or a catalog cycle: pre and post are NCX blocks,
    // requires the state conditions, restore the state variables put back; NCX text carries no native codes and is
    // kept as written (machine-config 5a, 5). A function table may also name the states its rule fires on; without
    // the key every state of the table fires it (D198). The tool change and a catalog cycle have no states.
    internal static ExpansionRule? ReadRule(ConfigTable table)
    {
        bool hasRule = false;
        foreach (string key in s_ruleKeys)
        {
            hasRule = hasRule || table.Has(key);
        }

        if (!hasRule)
        {
            return null;
        }

        return new ExpansionRule
        {
            States = table.TextList("states"),
            Pre = table.TextList("pre"),
            Post = table.TextList("post"),
            Requires = table.Table("requires")?.Strings(null) ?? new Dictionary<string, string>(),
            Restore = table.TextList("restore"),
        };
    }

    // [func_meta]: FUNCTION.STATE = "rule", written as a dotted key, the reader fallback for machines without a
    // [workpiece] table (machine-config 5, D40).
    private static FuncMeta ReadFuncMeta(ConfigTable? table)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        if (table is null)
        {
            return new FuncMeta { Entries = entries };
        }

        foreach (string function in table.Keys)
        {
            if (!table.IsTable(function))
            {
                if (table.Text(function) is string rule)
                {
                    entries[function] = rule;
                }

                continue;
            }

            ConfigTable? states = table.Table(function);
            foreach (string state in states?.Keys ?? [])
            {
                if (states?.Text(state) is string rule)
                {
                    entries[function + "." + state] = rule;
                }
            }
        }

        return new FuncMeta { Entries = entries };
    }

    // [workpiece]: ROLE is the native selection of that holder, ROLE_frame how its side is programmed, datum or
    // mirror, and ROLE_mirror its mirror cycle (machine-config 5, D57).
    private static WorkpieceConfig? ReadWorkpiece(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        var frames = new Dictionary<string, WorkpieceFrame>(StringComparer.Ordinal);
        var mirrors = new Dictionary<string, FunctionTable>(StringComparer.Ordinal);
        foreach (string key in table.Keys)
        {
            if (key.EndsWith(FrameSuffix, StringComparison.Ordinal))
            {
                string role = key.Substring(0, key.Length - FrameSuffix.Length);
                WorkpieceFrame? frame = table.Choice(key, s_workpieceFrames) switch
                {
                    "datum" => WorkpieceFrame.Datum,
                    "mirror" => WorkpieceFrame.Mirror,
                    _ => null,
                };
                if (frame is WorkpieceFrame roleFrame)
                {
                    frames[role] = roleFrame;
                }
            }
            else if (key.EndsWith(MirrorSuffix, StringComparison.Ordinal))
            {
                string role = key.Substring(0, key.Length - MirrorSuffix.Length);
                if (table.Table(key) is ConfigTable mirror)
                {
                    mirrors[role] = new FunctionTable { States = mirror.Templates(s_onOff) };
                }
            }
            else if (table.Template(key) is string template)
            {
                templates[key] = template;
            }
        }

        return new WorkpieceConfig { Templates = templates, Frames = frames, Mirrors = mirrors };
    }

    // [sync]: the wait template, the form of {paths}, the marks, the wait groups and the Siemens channel templates
    // (machine-config 5, language 4.8).
    private static SyncConfig? ReadSync(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_syncKeys);
        IReadOnlyList<int>? markRange = table.IntegerPair("mark_range");
        var groups = new List<SyncGroup>();
        foreach (ConfigTable group in table.Tables("groups"))
        {
            // groups = [{ channels = [3, 4], range = [800, 849] }]: a second wait group between other channels.
            group.WarnUnknownKeys(s_syncGroupKeys);
            IReadOnlyList<int>? range = group.IntegerPair("range");
            groups.Add(new SyncGroup
            {
                Channels = group.IntegerList("channels"),
                Range = range is null ? null : new MarkRange(range[0], range[1]),
            });
        }

        return new SyncConfig
        {
            Wait = table.Template("wait"),
            Paths = table.Choice("paths", s_syncPaths) switch
            {
                "list" => SyncPaths.List,
                "bitmask" => SyncPaths.Bitmask,
                _ => null,
            },
            MarkRange = markRange is null ? null : new MarkRange(markRange[0], markRange[1]),
            StartMark = table.Integer("start_mark"),
            ReservedMarks = table.IntegerList("reserved_marks"),
            Groups = groups,
            StartChannel = table.Template("start_channel"),
            WaitChannel = table.Template("wait_channel"),
        };
    }

    // [transform]: the templates of the transformations, the tilted planes, the rotary axis options and the path mode
    // (D349), and the values of {move} per MOVE option and of {rot} per ROT option (D269); an empty template is kept,
    // the target cannot write the word (machine-config 5, D82).
    private static TransformTable? ReadTransform(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_transformKeys);
        return new TransformTable
        {
            CylinderOn = table.Template("CYLINDER_ON"),
            CylinderOff = table.Template("CYLINDER_OFF"),
            PolarOn = table.Template("POLAR_ON"),
            PolarOff = table.Template("POLAR_OFF"),
            TcpmOn = table.Template("TCPM_ON"),
            TcpmOff = table.Template("TCPM_OFF"),
            TiltOn = table.Template("TILT_ON"),
            TiltOff = table.Template("TILT_OFF"),
            TiltAxisOn = table.Template("TILT_AXIS_ON"),
            TiltTurn = table.Template("TILT_TURN"),
            RotaryPathShortest = table.Template("ROTARY_PATH_SHORTEST"),
            RotaryPathFull = table.Template("ROTARY_PATH_FULL"),
            RotaryFeedMmMin = table.Template("ROTARY_FEED_MM_MIN"),
            RotaryFeedDegMin = table.Template("ROTARY_FEED_DEG_MIN"),
            Move = table.Table("move")?.PlaceholderValues(s_moveOptions) ?? new Dictionary<string, string>(),
            Rot = table.Table("rot")?.PlaceholderValues(s_rotOptions) ?? new Dictionary<string, string>(),
            PathModeExactStop = table.Template("PATH_MODE_EXACT_STOP"),
            PathModeContinuous = table.Template("PATH_MODE_CONTINUOUS"),
        };
    }

    // [retract]: the templates of the RETRACT verb (machine-config 5, D83).
    private static RetractTable? ReadRetract(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_retractKeys);
        return new RetractTable { Max = table.Template("MAX"), By = table.Template("BY") };
    }

    // [tolerance]: the templates of the TOLERANCE words, ON_ROTARY written while TOLERANCE:ROTARY is set (D151), the
    // values of {mode} (machine-config 5, D85), and where the control holds a value the template cannot carry: the
    // phrase, the value in millimetres, zero or greater like the control-held keys of D357, and the comment option
    // (D367).
    private static ToleranceTable? ReadTolerance(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_toleranceKeys);
        return new ToleranceTable
        {
            On = table.Template("ON"),
            OnRotary = table.Template("ON_ROTARY"),
            Off = table.Template("OFF"),
            Mode = table.Table("mode")?.PlaceholderValues(s_toleranceModes) ?? new Dictionary<string, string>(),
            HeldIn = table.Text("held_in"),
            HeldValue = NotNegative(table, "held_value"),
            HeldComment = table.Flag("held_comment"),
        };
    }

    // A distance the control holds is zero or greater; a negative one is the ERROR of a value not allowed (D357,
    // D367).
    private static decimal? NotNegative(ConfigTable table, string key)
    {
        decimal? value = table.Number(key);
        if (value < 0)
        {
            table.Diagnostics.Error(table.LineOf(key), DiagnosticCodes.ValueNotAllowed,
                $"{key} in {table.Name} must be zero or greater, not {value} ({table.Section}, D367).");
            return null;
        }

        return value;
    }

    // [stock]: the templates of STOCK=BOX and STOCK=CYLINDER, in the house style of [retract], several lines joined
    // by \n (machine-config 5, D351).
    private static StockTable? ReadStock(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_stockKeys);
        return new StockTable { Box = table.Template("BOX"), Cylinder = table.Template("CYLINDER") };
    }

    // [raw]: the builder codes the reader keeps as RAW with the builder's name (machine-config 5, F23).
    private static RawTable? ReadRaw(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_rawKeys);
        return new RawTable { Known = table.TextList("known") };
    }
}
