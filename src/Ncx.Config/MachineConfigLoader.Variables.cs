using System.Globalization;
using Ncx.Core.Machine;

namespace Ncx.Config;

// Machine-config 6, 7 and 9: the cycle catalog, the variables, the system variables and the kinematic tree.
public static partial class MachineConfigLoader
{
    private static readonly string[] s_cyclesKeys =
    [
        "catalog", "surface_clearance", "chip_break_lift", "peck_clearance", "ream_feed_out", "contour_type_ii",
        "peck_rigid_tapping",
    ];

    private static readonly string[] s_variablesKeys =
        ["unassigned", "map", "declared", "block_cap", "call_depth", "repeat_counters"];

    private static readonly string[] s_reamFeedOutValues = ["CLEARANCE", "SAFE"];
    private static readonly string[] s_unassignedValues = ["error", "0"];
    private static readonly string[] s_nodeKeys = ["id", "parent", "type", "direction"];

    // [cycles]: the catalog file of the controller family (machine-config 6), the distances of the control's own
    // cycles (D158, D357), the plane REAM feeds out to (D378) and the two cycle options of D388 item 11.
    private static CyclesConfig? ReadCycles(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_cyclesKeys);
        return new CyclesConfig
        {
            CatalogFile = table.Text("catalog"),
            SurfaceClearance = Distance(table, "surface_clearance"),
            ChipBreakLift = Distance(table, "chip_break_lift"),
            PeckClearance = Distance(table, "peck_clearance"),
            ReamFeedOut = ReadReamFeedOut(table),
            ContourTypeII = table.Flag("contour_type_ii"),
            PeckRigidTapping = table.Flag("peck_rigid_tapping"),
        };
    }

    // D357: a distance of [cycles] is a decimal in millimetres whatever units_default is, zero or greater; a negative
    // value is the ERROR CFG005 and is dropped, so the family's behaviour applies. D158's surface_clearance follows
    // its neighbours (gap closed 2026-10-27, delegated).
    private static decimal? Distance(ConfigTable table, string key)
    {
        decimal? value = table.Number(key);
        if (value is decimal distance && distance < 0)
        {
            table.Diagnostics.Error(table.LineOf(key), DiagnosticCodes.ValueNotAllowed,
                $"[cycles] {key} = {distance.ToString(CultureInfo.InvariantCulture)} is negative; a distance of "
                + "[cycles] is in millimetres and zero or greater (machine-config 6, D357).");
            return null;
        }

        return value;
    }

    // D378: ream_feed_out is "CLEARANCE" or "SAFE"; any other value is the ERROR CFG005, and the family default
    // applies.
    private static ReamFeedOut? ReadReamFeedOut(ConfigTable table)
    {
        return table.Choice("ream_feed_out", s_reamFeedOutValues) switch
        {
            "CLEARANCE" => Ncx.Core.Machine.ReamFeedOut.Clearance,
            "SAFE" => Ncx.Core.Machine.ReamFeedOut.Safe,
            _ => null,
        };
    }

    // [variables]: unassigned is "error" or 0; block_cap and call_depth keep the defaults of virtual machine 3.6 when
    // the file leaves them out (machine-config 7, D38).
    private static VariablesConfig ReadVariables(ConfigTable? table)
    {
        var defaults = new VariablesConfig();
        if (table is null)
        {
            return defaults;
        }

        table.WarnUnknownKeys(s_variablesKeys);
        return new VariablesConfig
        {
            Unassigned = ReadUnassigned(table),
            Map = ReadVariableMap(table),
            Declared = Names(table.TextList("declared")),
            BlockCap = table.LongInteger("block_cap") ?? defaults.BlockCap,
            CallDepth = table.Integer("call_depth") ?? defaults.CallDepth,
            RepeatCounters = table.TextList("repeat_counters"),
        };
    }

    // declared: the names of [variables] declared in capitals, as a variable name is written (language 4.9, D261).
    private static List<string> Names(IReadOnlyList<string> written)
    {
        var names = new List<string>();
        foreach (string name in written)
        {
            names.Add(name.ToUpperInvariant());
        }

        return names;
    }

    // map: the native variable of each NCX variable prefix, as a template of the variable's number with {index}
    // (machine-config 7, D173): Siemens Q = "R{index}" writes R1, Fanuc Q = "#1{index:02}" writes #101. A bare
    // prefix cannot state the width the control needs, so an entry without {index} is an ERROR on its line.
    private static IReadOnlyDictionary<string, string> ReadVariableMap(ConfigTable table)
    {
        ConfigTable? map = table.Table("map");
        if (map is null)
        {
            return new Dictionary<string, string>();
        }

        IReadOnlyDictionary<string, string> entries = map.Strings(null);
        foreach (KeyValuePair<string, string> entry in entries)
        {
            if (!entry.Value.Contains("{index", StringComparison.Ordinal))
            {
                map.Diagnostics.Error(map.LineOf(entry.Key), DiagnosticCodes.VariableMapWithoutIndex,
                    $"The [variables] map entry {entry.Key} = \"{entry.Value}\" has no {{index}}: an entry is a "
                    + "template of the variable's number, \"R{index}\" or \"#1{index:02}\", because a bare prefix "
                    + "does not state the width the control needs (machine-config 7, D173).");
            }
        }

        return entries;
    }

    // Reading an unassigned variable is an ERROR unless the configuration sets unassigned = 0 (virtual machine 3.6,
    // machine-config 7).
    private static UnassignedVariable ReadUnassigned(ConfigTable table)
    {
        string? written = table.PlaceholderValue("unassigned");
        if (written is null || written == "error")
        {
            return UnassignedVariable.Error;
        }

        if (written == "0")
        {
            return UnassignedVariable.Zero;
        }

        table.NotAllowed("unassigned", written, s_unassignedValues);
        return UnassignedVariable.Error;
    }

    // [system_variables]: every key an NCX SYS_ name, every value the native template kept as written, parsed on the
    // line of its key (machine-config 7, D51; wave-1 question #61).
    private static SystemVariables ReadSystemVariables(ConfigTable? table)
    {
        if (table is null)
        {
            return new SystemVariables { Entries = new Dictionary<string, string>() };
        }

        IReadOnlyDictionary<string, string> entries = table.Strings(null);
        foreach (KeyValuePair<string, string> entry in entries)
        {
            table.CheckTemplate(entry.Key, entry.Value);
        }

        return new SystemVariables { Entries = entries };
    }

    // [[node]] and [machine] kinematics: the tree is loaded and not interpreted, only the kinematics module reads it;
    // every node needs its id (machine-config 9).
    private static KinematicTree? ReadKinematics(ConfigTable root, string? file)
    {
        IReadOnlyList<ConfigTable> tables = root.Tables("node", "[[node]]", "machine-config 9");
        if (file is null && tables.Count == 0)
        {
            return null;
        }

        var nodes = new List<KinematicNode>();
        foreach (ConfigTable table in tables)
        {
            table.WarnUnknownKeys(s_nodeKeys);
            string? id = table.RequiredText("id");
            string? parent = table.Text("parent");
            string? type = table.Text("type");
            IReadOnlyList<decimal> direction = table.NumberList("direction");
            if (id is not null)
            {
                nodes.Add(new KinematicNode { Id = id, Parent = parent, Type = type, Direction = direction });
            }
        }

        return new KinematicTree { File = file, Nodes = nodes };
    }
}
