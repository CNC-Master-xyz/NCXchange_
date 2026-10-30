# Machine Configuration (TOML)

Status: Version 1.0 of the specification set, 2026-09-12 (draft 8 in the drafting history of `../decisions/decisions.md`). Sketch level: the schema is finalized when the compiler exists. Companion to `ncx-language.md` and `ncx-virtual-machine.md`.

Templates are strings with `{placeholders}`: `{tool}` (the tool in the spindle after the block, from the VM, so a bare `TOOL` writes the preloaded number, D91), `{name}` (tool name string), `{offset}`, `{next}` (the preloaded next tool; in the `preload` template both `{tool}` and `{next}` mean the n of `PRELOAD=n`, and `{next}` is the preferred spelling, D150), `{kind}` (tool kind from the tool table: rotary or turning, mapped per machine), `{rpm}`, `{axis}` (tool axis letter; in the `[setpos]` and `ROTARY_FEED` templates the axis the call is written for, one call per axis), `{axes}` (the axis list of a `HOME` or `SETPOS` block in the machine's address form), `{angle}`, `{b}` (index angle of the tool carrier from the next positioning block), `{c}` (tool spindle orientation from the next `ORIENT:TOOL`), `{mark}`, `{channels}`, `{paths}`, `{r}` (the reference radius of `CYLINDER=n`, the only source of `{r}`; not the arc key `R`, D96), `{d}` (twice `{r}`: the working diameter that Siemens `TRACYL` takes), `{value}`, `{index}`; for the tilt, retract and tolerance templates `{a}`, `{b}`, `{c}` (the angles of the `TILT` or `TILT_AXIS` block), `{x}`, `{y}`, `{z}`, `{move}` and `{dir}` (from the `MOVE` option), `{distance}`, `{tol}`, `{rotary}`, `{mode}`; `{point}` (the `POINT` of `HOME`, language 4.3) and `{channel}` (the value of `START_CHANNEL` or `WAIT_CHANNEL`, language 4.8), both numbers; `{radius}` (the Siemens `FGREF` radius), `{order}` (the DMG `L711` order), `{pair}` and `{ratio}` (the DMG `L725` pairing and gear ratio), numbers that no NCX word fills in 1.0, so a compiler meeting one reports the missing value CFG100 (D152); `{rot}` (the words of the `ROT` option from `[transform] rot`, TABLE when the block has none, D269); for the `[stock]` templates `{x}`, `{y}`, `{z}` (the first corner of the box), `{max_x}`, `{max_y}`, `{max_z}` (the opposite corner), and from the box for the cylinder forms `{radius}`, `{diameter}` (not `{d}`, which is twice `{r}`), `{length}` (the extent along the tool axis) and `{dist}` (the offset of the near face along it) (D351); in expansion rules `{position:NAME}` (the axis words of a `[positions]` entry, D100). This list is complete: a well-formed name outside it is the WARNING CFG104 and is still read as a number (D152). A format suffix pads numbers: `{tool:02}` writes `04`, `{tool:02}.` writes `04.` (the decimal point of the NT NURSE macros is literal text). Reading is the other way round and takes the digits of one address as one number that the placeholders split from the right, as the control does: a padded placeholder that begins a run of digits reads one or more digits, a padded placeholder that follows digits (a literal digit or another placeholder) reads exactly its width. So `T{tool:02}{offset:02}` reads `T0101` and the unpadded `T101` alike as tool 1 with offset 1, and `T12301` as tool 123 with offset 1, while `#11{index:03}` still needs its three digits. Rendering pads and never truncates: a number wider than its width is written in full where it begins the run, and where it follows digits it is an ERROR on the block, because the text would read back as other numbers (D153). A missing placeholder value leaves the template unusable and the compiler reports it.

Everything that differs between two machines lives here and nowhere else. A user copies the file of a similar machine and edits it. One machine, one file. The file is TOML; a large kinematic tree may be split into a second TOML file referenced from the first, so the user still edits one configuration language.

## 1. Identity and dialect

```toml
ncx_schema = 1                     # optional, the first key: the version of this schema the file is written in (D348)

[machine]
name = "DMU 50"
controller = "heidenhain"          # fanuc | heidenhain | siemens
dialect = "iTNC530"                # 30i | 31i-T | 0i-TF | iTNC530 | TNC640 | 840D ...
builder = "dmg"                    # machine builder id, used by RAW:DMG and by the builder function tables
gcode_system = "A"                 # Fanuc lathes only: A (G98/G99 feed, U/W incremental, G90 = turning cycle), B (G94/G95, G90/G91, cycles G77/G78/G79) or C (as B, with G70/G71 for inch and mm and cycles G20/G21/G24; D174)
s_binds_to_spindle_word = true     # Fanuc: one S word, owned by the spindle of the M code in the same block; RPM:role is written into that role's SPINDLE block
limits = "warn"                    # warn | clamp: what the expander does with RPM, F and targets beyond the machine limits (D64); clamp rewrites F against the linear axes its block names and only the absolute axis words of RAPID and LINE under FRAME=MACHINE (D192, D203)
channels = [1]                     # the channel numbers; absent means one channel, and a run without a manifest on a machine of several channels is no single-channel job (D196)
units_default = "MM"
default_spindle = "S1"
default_holder = "H1"
default_workpiece = "TABLE1"
arc_tolerance = 0.01               # optional, mm like rapid and max_feed, converted for an INCH program: |start - center| against |end - center| (D36); absent means 0.01 mm and 0.0005 in (D139)
arc_radius_tolerance = 0.005       # optional, mm, converted for an INCH program: the radius difference of a written arc beyond which the compiler reports CMP030; absent means one output step of the centre's address (D364)
dwell_increment = 0.001            # optional, Fanuc: seconds per unit of G4 P, of G4 X/U without a decimal point under calculator_input = false and of a cycle P; default 0.001 (D159)
input_increment = "IS-B"           # optional, Fanuc: the increment system of parameter 1013, "IS-A" | "IS-B" | "IS-C"; the least input increment 0.01 | 0.001 | 0.0001 mm, a tenth in inch; default "IS-B"
base_units = "mm"                  # optional, Siemens: "mm" | "inch", the basic system of MD10240; default units_default (D292)
extended_units = false             # optional, Siemens: MD10260 = 1, G700/G710 available; default false (D292)
tool_table = "dmu50.tools.toml"   # optional (D10): tool kind, length, radius per tool number; when missing, {kind} takes the default and the compiler writes a warning block at the head of the program naming the expected file and the tools it lacks, so nobody runs the program by accident
```

The tool table of `tool_table` is TOML made of `[[tool]]` tables, one per tool; an unknown key is a WARNING (D265).

```toml
# dmu50.tools.toml, next to the machine file (D10, D265)
[[tool]]
number = 4                         # the tool number TOOL=4 calls; or name = "DRILL_D8" for a tool called by name (language 4.4)
kind = "ROTARY"                    # a key of [tool_change] kind_map (section 3, D52)
length = 120.5
radius = 4

[[tool.point]]                     # optional, one per measurement point of the tool (D376)
k = 2                              # the point, from 1, as TOOL_POINT names it (language 4.4, D372)
tip_offset = 2                     # a: the axial offset of the point from the tip, not negative
radius = 8                         # r: the register radius of the point, not negative, 0 by default
wear = 0                           # what the control adds to r, 0 by default; the virtual machine adds it to the radius it reports (D374)
```

Every numeric key of the tool table, `length`, `radius`, `tip_offset` and `wear`, is in the machine's `units_default` (D370, D376). A tool without `[[tool.point]]` rows is point 1 with a = 0 and r = 0. The compiler compares the program's `TIP_OFFSET` and `POINT_RADIUS` of the active point with its row and reports the WARNING CMP032 where either differs by more than one output step of the tool-axis address (10 to the minus `decimals` of that address, section 2); it is silent where the table has no row for that tool and point, and it never corrects a coordinate (D376).

Without the table, or for a tool it does not describe, `{kind}` takes the default kind `ROTARY`, and the compiler writes the warning block of D10 at the head of that program only, after its header: comment lines of the controller, under `comment_charset` (section 2), that name D10, the expected file and the tools without data with the kind written. The block stops nothing, and a machine whose templates need no tool data gets no warning (D265).

A machine file requires `[machine] name` and `controller`, `id` and `type` of every `[[resource]]`, `id`, `ncx` and `kind` of every `[[axis]]`, `id` of every `[[node]]`, and the coolant channel `[coolant] STANDARD` (section 5, D130); a missing one is the ERROR CFG004. Every other key is optional with its documented effect, and what a compiler needs beyond them is checked by the compiler (D138).

`ncx_schema` is the format version of the machine-configuration schema, with the meaning `NCX=1` has in language 4.1: not the release of `ncx` and not the content of the file. A machine file and a cycle catalog may carry it as their first key; a file without it is version 1, and a version this build does not read is the ERROR CFG009 that names the file and the version. The number changes only when the schema breaks, so it stays 1 through release 1.0, and the shipped files carry it (D348).

`dwell_increment` is the unit of the Fanuc dwell that the control takes from its increment system, or 1 ms under parameter 1015#7 DWT. The reader reads `G4 X` or `U` with a decimal point as seconds and multiplies `P`, and `X` or `U` without a decimal point under `calculator_input = false`, by the increment; the compiler always writes `G4 X` with a decimal point in seconds and a cycle `P` as round(seconds / dwell_increment). NCX assumes parameter 3405#1 DWL = 0: a dwell is in seconds, never in revolutions under G95 (D159; Fanuc 30i-B operator's manual B-64484EN p.188, parameter manual B-64490EN-05 p.59 and p.195).

`input_increment` is the increment system of a Fanuc control, parameter 1013 (B-64490EN-05 p.57-58): the least input increment is 0.01, 0.001 or 0.0001 mm for "IS-A", "IS-B" or "IS-C", a tenth of it in inch, the same number in degrees on a rotary axis. Every count of the least input increment uses it: the lathe peck `Q` in the reader and the compiler, and the reading of a number without a decimal point under `[format] calculator_input = false` (machine-config 2; controllers fanuc.md 2). Without the key IS-B. Gap closed 2026-10-30 (delegated).

`base_units` and `extended_units` describe the Siemens group 13 pair; `units_default` stays the reset value of the group (MD20150). Where `UNITS` equals the basic system the compiler writes G71/G70; where it differs it writes G710/G700 under `extended_units = true`, otherwise G70/G71 with every feed converted into the basic system and an INFO naming the conversion. The reader takes G700/G710 as written and converts the feeds under a G70/G71 that differs from the basic system, with a WARNING (D292; SINUMERIK 840D sl function manual basic functions p.346 for MD10240, programming manual SW4.94 p.158 for MD10260).

`controller` selects the reader and compiler; `dialect` selects their variant tables. `default_*` resolve NCX words without a role address. `check`, `trace`, `annotate` and `analyze` run without a machine file against a built-in default machine (`ncx-virtual-machine.md`, section 3.8, D103); `convert` and `compile` need one (D77).

## 2. Output format

```toml
[format]
decimal_separator = "."            # "," for Heidenhain
decimals = { X = 3, Y = 3, Z = 3, A = 3, C = 3, ANGLE = 3, F = 1, S = 0 }   # ANGLE: the arc sweep (D366)
trailing_zeros = false
block_numbers = { enabled = true, start = 10, step = 10 }   # Heidenhain: consecutive from 0
line_ending = "CRLF"
program_end = "M30"                # M30 | M2 (Siemens also M17), written for PROGRAM=END (D48, D49)
sub_end = "M99"                    # written for SUB=END, and for RETURN where the family has no jump to it; Heidenhain "LBL 0", once per section: a RETURN before the end jumps to an LBL directly before it (D272); Siemens "RET"
program_layout = "one_file"        # left out: the family default, file_per_program for Heidenhain, one_file else (row W3-3 of 03-open-questions)
                                   # one_file: all programs and subprograms of an NCX file into one output file (Fanuc O programs, Siemens %_N_ units)
                                   # file_per_program: one output file per program, subprograms copied after the M30 of every caller (Heidenhain)
                                   # a subprogram no program calls goes after the last program, or into the first program's file (D264); a file is named
                                   # after its program's NAME, else the NCX file stem, a name taken already (case-insensitive) gets _2, _3 with CMP005 (D266)
max_line_length = 0                # 0 = unlimited; Siemens family value 512 (D263)
comment_charset = "ASCII"          # "ASCII" | "UTF-8", any other value is the ERROR CFG005; under ASCII umlauts are transliterated before writing (D263)
label_max = 999                    # optional: the largest label number; the Heidenhain compiler numbers labels from 1 to it (D271)
counter_registers = ["QL90", "QL99"]   # optional, Heidenhain: the first and the last Q parameter (Q, QL or QR, one kind) the compiler counts a REPEAT in when a JUMP leaves its section; without it such a REPEAT is an ERROR, never a guessed register (D213, D388 (12))
motion_code = "arcs"               # optional, every compiler: "change" | "arcs" | "every_block", when the verb G0..G3, L, C is written (D299)
m_codes_per_block = 1              # optional, Fanuc: 1 to 5 M codes per block; 3 needs parameter 3404#7 M3B, 5 needs 11630#5 M5B (D339)
# m_codes_combinable = ["M10", "M11"]   # optional, Fanuc: only these may be the second and later M code of a block (D339)
helix_feed = "arc"                 # optional: "path" | "arc", whether F is measured along the whole path of a helix or along the arc in the plane (D354)
# header = "G0 G40\nG80 G90 G94 G98"   # optional, Fanuc: the start block, a template like program_end split at \n (D276)
# calculator_input = true              # optional, Fanuc: parameter 3401#0 DPI; true reads X70 as 70 mm, false as 0.070 mm (fanuc.md 2)
# plane_with_first_motion = false       # optional, Fanuc: G17 stands in the first motion block after it changes (D276)
# motion_code_after_tool_change = false # optional, Fanuc: the first motion after a tool change writes its motion code (D276)
# length_offset_with_tool_axis = false  # optional, Fanuc: G43 H stands in the first block that moves the tool axis (D276)
# channel_files = "path_suffix"         # optional: "path_suffix" | "channel_suffix" | "program_name", the files of a job's channels (D310): O1000 and O1000.P-2 without extension (Nakamura), <job>_C1.<ext> (STAMA), NAME.<ext> (DMG)
# external_call = "M198"          # optional, Fanuc: the code that calls a program outside the file, parameter 6030 on the control; the compiler writes it
                                  # in place of M98 for a CALL of an external program and the reader reads it as CALL. Without the key the compiler writes
                                  # M98 and the reader reads no external call; a machine that calls with M198 names it, and a machine whose [sync]
                                  # mark_range covers the code must not, because the code is a wait mark there (D322)
```

A key left out takes the value of the controller family (D263): line endings LF; no block numbers, and start 10 step 10 when they are enabled, except Heidenhain, which numbers from 0 in steps of 1; `max_line_length` 0, except Siemens 512; `comment_charset` "ASCII" on every family. An overlong line is the WARNING CMP021, and a non-ASCII character without a transliteration becomes `?` with CMP022. `label_max` defaults to 999, the iTNC 530 from NC-SW 340 49x (254 for 340422, 65535 for the TNC 640, D271). `motion_code` defaults to the form each compiler writes: Fanuc "arcs" (G0 and G1 on change, G2 and G3 on every arc), Siemens and Heidenhain "every_block" (D299). `m_codes_per_block` defaults to 1, which every control accepts; whatever the keys say, the compiler writes M00, M01, M02, M30, M98, M99, M198, the `[sync]` wait codes and the configured macro calls each in a block of its own, and puts a spindle code in one block with another M code only when `m_codes_combinable` lists that code; codes beyond the limit move to the following lines (D339). Without `channel_files` each channel of a job keeps the name its family gives the file; `path_suffix` writes `O<NUMBER>` with at least four digits for the first channel of `[machine] channels` and `O<NUMBER>.P-n` for channel n, a program without `NUMBER` taking the stem; two channels that would write one file each take `_C<n>` with the WARNING CMP721, as D266 does for the programs of one file (D310). A file the job renames renames its program as well, as D266 does: the unit header `%_N_<name>_MPF` on Siemens, `BEGIN PGM` and `END PGM` on Heidenhain, the comment of the `O` line on Fanuc when it carries the old name. A job compile of several channels writes every `SUB` that no channel program calls once into the library file `<job name>_LIB` with the family's extension, on Siemens one `<name>.spf` per `SUB` and on Heidenhain one program `<job name>_LIB_<sub>.h` per `SUB`; two library files holding one program are the WARNING CMP722 (D316, D388). `helix_feed` defaults to "arc" on Fanuc (parameter 1403#5 HTG = 0) and to "path" on Heidenhain and Siemens; under "arc" the compiler scales the `F` of a helix by the plane length over the path length and the reader scales it back (D354). An address that `decimals` leaves out takes a default per family, angle addresses four decimals on Heidenhain and three on Siemens and Fanuc, a linear address the decimals of `X` (D366).

`decimals` gives the decimals each address is written with. Every number is rounded to them half away from zero on its decimal value as the NCX writes it (15.0175 becomes 15.018), silently: CMP020 is retired, and a program that wants to know states `ROUND_LIMIT`, above which the compiler reports the distance between the programmed point and the point the written file produces, over the linear axes of the block, as the WARNING CMP023, and an angle address that moves by more than `ROUND_LIMIT:ROTARY` the same way (D363). The written point is the one the file produces block by block: an absolute word sets the rounded value, an incremental word adds its rounded increment, so the drift of repeated increments is caught; a diameter `X` is compared as a radius. No address is ever written with all its digits: an address the key leaves out takes a default of the family, `ANGLE` (the one address of the sweep, which the Heidenhain compiler writes as `IPA` and Siemens as `AR=`) and the rotary axes four decimals on Heidenhain, its input resolution of 0.0001°, and three on Siemens and Fanuc; a linear address (an axis letter `X Y Z U V W` with its extension, `I J K`, `R`, `CR`) the decimals of `X` (D366). A value that is no address, a variable, an `ARG`, a `TOLERANCE` or a dwell, keeps its digits unless the key names its address. Gap closed 2026-10-27 (delegated, W3-5): without `X` too a linear address takes three; a Klartext cycle parameter (`Q`, `QL`, `QR` with a number) four, its input range of 0.0001 to 99999.9999; a component of a tool or normal vector six, the precision of the virtual machine (D121). The decimal point of a whole number on Fanuc and the padding of `trailing_zeros` follow the decimals the key writes only. Gap closed 2026-10-28 (delegated, W3-5): a feed other than zero that rounds to `F0` is the WARNING CMP024, since D363 makes only the rounding of points silent.

`calculator_input` is parameter 3401#0 DPI of a Fanuc control (B-64490EN-05 p.190) and applies to the addresses that can take a decimal point, on a mill `X Y Z U V W A B C I J K Q R F`, on a lathe the same without `Q` and with `H` in system A (B-64484EN p.229). True, the default and the assumption of controllers fanuc.md 2, reads a number without a point in mm, inch or degrees, and the dwell `G4 X`/`U` in seconds; false counts it in least input increments of `[machine] input_increment` (IS-B: 0.001 mm, 0.0001 inch, 0.001 degree on a rotary axis), `F` by the feed mode (mill 1 mm/min, 0.01 mm/rev; lathe 1 mm/min, 0.0001 mm/rev; B-64484EN p.527-528, B-64484EN-2 p.104), and `G4 X`/`U` in `dwell_increment`. An address that takes no point counts increments under both: the cycle `P`, `G4 P` and the lathe `Q`. The compiler writes every number of an address that takes a point with its point, under false also where `decimals` writes none (`F100.`), and the lathe `Q` as an integer of increments. Gap closed 2026-10-30 (delegated).

`header`, `plane_with_first_motion`, `motion_code_after_tool_change` and `length_offset_with_tool_axis` are the habits of the Fanuc sources, which only the Fanuc compiler reads: `header` is a template like `program_end` whose G codes are the control state after it, and each flag defaults to false (D276). `channel_files` names the files of a job's channels: "path_suffix" writes O<NUMBER, at least four digits> for the first channel and O<NUMBER>.P-n for channel n without extension, the stem for a program without NUMBER; "channel_suffix" writes <job>_C<n>.<ext>; "program_name" writes NAME.<ext>. Without the key each channel keeps the name of its family, and two channels that would write one file get _C<n> with a WARNING (D310).

## 3. Tool change

```toml
[tool_change]
change = "T{tool} M6"              # written for TOOL=n and for a bare TOOL ({tool} from the VM, D91)
change_preloaded = "M6"            # optional: written instead of change when n is already preloaded
preload = "T{tool}"                # written for PRELOAD=n; omit on machines without a magazine (PRELOAD is dropped with a WARNING)
unload = "T0 M6"                   # written for TOOL=0
auto_preload = true                # insert PRELOAD of the next tool after each TOOL when the program has none
preload_position = "after_change"  # after_change | before_first_motion
offsets_with_change = false        # true on Heidenhain: OFFSET words are implicit in TOOL CALL and not written
length_offset_by_macro = false     # Fanuc: true when the builder's change macro applies the length offset; otherwise the first tool-axis motion after a TOOL without OFFSET:LEN, OFFSET or a named point's register is the WARNING CMP342, and no G43 H is written by default (D389)
tool_name_allowed = false          # TOOL="NAME" accepted (Heidenhain, Siemens) or mapped to numbers
# change_named = "T=\"{name}\" M6"   # optional: written instead of change when the tool is a name, the TOOL value or the preloaded tool of a bare TOOL (D172)
# preload_named = "T=\"{name}\""     # optional: written instead of preload for PRELOAD="NAME" (D172)
kind_map = { ROTARY = "0.", TURNING = "1." }   # the value of {kind} per tool kind of the tool table (D52); Mori Seiki as here, DMG { ROTARY = 1, TURNING = 2 }; a tool without data takes ROTARY (D265)
```

Templates found in the manuals:

| Machine | `change` | `preload` |
|---|---|---|
| Fanuc machining center | `"T{tool} M6"` | `"T{tool}"` |
| Heidenhain | `"TOOL CALL {tool} {axis} S{rpm}"` | `"TOOL DEF {tool}"` |
| Siemens, change on T | `"T{tool}"`; `change_named = "T=\"{name}\" M6"` | `"T{tool}"`; `preload_named = "T=\"{name}\""` |
| Fanuc lathe turret | `"T{tool:02}{offset:02}"` | none |
| Nakamura tool spindle (ATC up to 80 tools) | `"G340 T{tool:02}{offset:02}. A{next:02}."` (decimal point literal, per the NT NURSE macro rule) | `"G341 T{next:02}."` |
| Nakamura tool spindle (120-tool ATC, six-digit T) | `"G340 T{tool:03}{offset:03} A{next:03}"` | `"G341 T{next:03}"` |
| Mori Seiki NT/NTX tool spindle (MAPPS) | `"G361 B{b} D{kind}"` with `kind` mapped 0 (rotary), 1 (turning) | `"T{tool:04}"` (the `T` word brings the tool to the change position, the change is `G361`); `unload = "T0\nG361"` |
| DMG structure programming (840D) | `change_named = "T=\"{name}\"\nTC({offset},,,{kind},{b},{c})"` with `kind` mapped 1 (rotary), 2 (turning), 3 and 4 for turret tools at spindle 3 or 4 | `preload_named = "T=\"{name}\""` on machines that prepare on the `T` word |

`change` and `preload` write numeric tools; `change_named` and `preload_named` write a tool called by name, with `{name}`. A named tool on a machine with `tool_name_allowed = true` but without the named template is a compile ERROR (D172). The row of the tool table is the mapping of a name to a number: `number` and `name` are keys of one row (D239). On Siemens a tool name is case-sensitive and holds no special characters (840D sl NC-Programmierung SW4.92 p.67) and at most 31 characters (Bedienhandbuch Fräsen 2013 p.554-555): the compiler writes the name as it stands and reports one with a character other than an ASCII letter, a digit, `_` or `-`, or longer than 31 characters, as the WARNING CMP515 (D172).

`[tool_change]` describes the change of `default_holder`. A machine with a second tool holder needs no table of its own: a `T` word of the source belongs to the tool holder whose `channel` is the channel of the program, else to `default_holder` (virtual machine 3.8 rule 2). A `T` word of `default_holder` is read through these templates first, so the Mori Seiki `T9001` is `PRELOAD=9001` through its `preload`; a `T` word none of them matches, and every `T` word of another holder, is the turret form `T{tool:02}{offset:02}` of `fanuc.md` 5, whose `TOOL` carries that holder's role, and the compiler writes a change on such a holder in that form (D219).

When `change` contains `{next}`, a `PRELOAD` word that follows the `TOOL` block (before the next motion) is folded into the change command; with `auto_preload` the compiler fills `{next}` from the STATIC look-ahead. At the last change `{next}` takes the program's first tool, and a change with `{next}` and no preload pending outside `auto_preload` is filled the same way, the next tool changed in, else the first tool; where that is the tool being changed in (a one-tool program) its number is written with the WARNING CMP006, never 0, and only where the look-ahead knows no tool at all the template stays unusable (CFG100) (D267). `{b}` and `{c}` are filled the same way from the next positioning block and the next `ORIENT:TOOL`; `{kind}` comes from the tool table (D52). A template may span lines with `\n`.

`[tool_points]` maps a tool and its measurement point (`TOOL_POINT`, language 4.4) to the control's registers (D373). The program names the point; the machine file says what selects it, and NCX derives nothing from a tool number that the file does not state.

```toml
[tool_points]
form = "registers"                 # indexed_tool (Klartext) | edge (Siemens) | registers (Fanuc); any other value is an ERROR
# indexed_tool: point k is the index k-1 of the called tool, TOOL CALL 5.1 for point 2; at most nine indices, so k <= 10 (the ERROR CMP124 beyond, D388).
#   An indexed call carries a tool number, so the TNC 640 runs its change macro (Klartext 892903-18 p. 123), and on the iTNC 530 it depends on
#   MP7480.0 and the builder's macro; a point change of the tool in the spindle is the WARNING CMP123 unless:
point_change = "call"              # indexed_tool: the builder's macro accepts an indexed call of the tool in the spindle
# edge: point k is D k of the active tool, or the edge of a [[tool_point]] row, which wins.
edge_numbering = "flat"            # edge: D numbers without relation to T (MD18102 = 1), so every point needs a row (CMP514 without one)
# registers: H and D from a [[tool_point]] row; else the shop's step rule; else tool management for point 1; else CMP341.
offset_memory = "C"                # registers: A | B | C; under A or B H and D share one pool and the loader refuses one number as a length and a radius (CFG010)
managed = false                    # registers: true writes H99 and D99, the registers of the tool at the spindle, for point 1 only (30i-B B-64484EN p. 260)
length_register_step = 100         # registers: H = tool + (k-1) * step, the shop's numbering rule; a control counterpart (parameter 11419 under G43.8/G43.9) exists under memory A or B only
radius_register_step = 100         # registers: D = tool + (k-1) * step
point_comment = false              # true: one comment line with the point, a and r where a point is selected (D375)

[[tool_point]]                     # a row per tool and point that the rule of the form does not give; tool and point are required (CFG010)
tool = 5
point = 2
length = 12                        # registers: the H of the point
radius = 12                        # registers: the D of the point
# edge = 20                        # edge: the D of the point
```

A machine file without `[tool_points]` writes nothing for a point other than 1 and reports the INFO CMP016 once per program, because a control that cannot select a point is doing nothing wrong; a point the form cannot map is the ERROR of its family, CMP124 Heidenhain (D388), CMP341 Fanuc, CMP514 Siemens, naming the tool and k, because another point's registers would cut with the wrong offsets (D373). A compiler writes only the register the mapping gives, never `TIP_OFFSET` or `POINT_RADIUS` as a control value, and a tool whose program names no point compiles as before (D375, D393). An `OFFSET` word on a holder whose tool named its point writes its own register in place of the mapped one, with the WARNING CMP033 (D393).

```toml
[home]
template = "G28 {axes}"           # Fanuc: {axes} is the named axes with an incremental 0, "U0 W0" in G-code system A and "X0 Y0 Z0" otherwise;
                                  # outside system A the compiler writes G91 in front of the template where G90 is active, "G91 G28 Z0" (D281)
point = "G30 P{point} {axes}"     # second and further reference points; Nakamura NT NURSE: "G330 A{point}."
# DMG structure programming: template = "L711({order})" (X, Z, Y tool change point), "L713(0)" on the counter spindle side
# Heidenhain: no template; the compiler writes "L {axes} M91" with the reference coordinates of the [[axis]] table

[setpos]
template = "G50 {axes}"           # Fanuc system A; "G92 {axes}" in B and C; Siemens: "PRESETON({axis},{value})"; Heidenhain: none, the compiler folds it into SHIFT
```

## 4. Roles, resources, axes

```toml
[roles]
MAIN = "S1"
SUB = "S2"
TOOL = "S3"
TURRET1 = "H1"

[[resource]]
id = "S1"
type = "work_spindle"              # work_spindle | tool_spindle | tool_holder | table
axis = "C1"                        # the C axis this spindle becomes in AXIS mode

[[resource]]
id = "S3"
type = "tool_spindle"

[[resource]]
id = "H1"
type = "tool_holder"
spindle = "S3"
magazine = true
channel = 1                        # optional: the channel that commands this resource on a machine with several (Nakamura: turret 1 on path 1, turret 2 on path 2), F23

[[axis]]
id = "X1"
ncx = "X"                          # name used in NCX programs
letter = "X"                       # address written in the output; extended axis names are written with the equals sign the control wants: "C2=" on Fanuc 30i/31i and Siemens, "C" per path on Mori Seiki (D30)
incremental_letter = "U"           # Fanuc lathes: U/W/H/V are the incremental forms of X/Z/C/Y; empty when the control uses G91
kind = "linear"                    # linear | rotary
limits = [-10, 650]                # min and max in machine coordinates (the G53 / M91 frame), for the travel-limit analytics (D100)
rapid = 30000                      # mm/min (deg/min on a rotary axis), for the runtime estimate (D290)
max_feed = 10000                   # mm/min (deg/min on a rotary axis), the F this axis can follow; VM441 compares the path feed of the axes a block moves, the expander warns or clamps (D64, D192)
acceleration = 3000                # mm/s^2 (deg/s^2 on a rotary axis), for the trapezoidal profile of the runtime estimate (D290)
programming = "diameter"           # X axis of a lathe: diameter | radius | switchable (D60); the reader interprets the source with it, the compiler writes it
home = 0                           # reference point 1 in machine coordinates (the G53 / M91 frame), for HOME (D100)
home2 = -50                        # reference point 2 (G30 P2), optional
frames_in_diameter = false         # diameter axis only: true when the control keeps frame offsets and actual-value variables in diameters (Siemens MD20360 bits 3 and 6); default false, radii (D125, D388)
clamp = { ON = "M10", OFF = "M11" }   # rotary axis clamping, written by the compiler around 3+2 machining when set

[[axis]]
id = "C1"
ncx = "C"
letter = "C"
kind = "rotary"
owner = "S1"                       # rotary axis of a work spindle
parallel = "Z"                     # rotary axis only: the linear axis it turns about, the cylinder axis of CYLINDER=n; X, Y or Z, default Z; a work spindle always turns about Z (D146)
modulo = true                      # rotary axis only: its limits are the display range, never compared or clamped (D193)
limits = [0, 360]                 # degrees, modulo axis: the display range, not a travel limit (D100)
home = 0                           # the spindle zero mark, machine frame (D100)
```

`modulo = true` on a rotary axis says that its `limits` are the display range of the control (the Fanuc roll-over axis, the Heidenhain `isModulo`, the Siemens modulo range): they are never compared, never clamped and never reported by the travel-limit analytic. Without the key the `limits` of a rotary axis are travel limits like those of a linear axis, compared in the MACHINE frame, reported by the travel-limit analytic and clamped under `limits = "clamp"`; a swivel `B` is written that way. The key decides only the limits, not how a modulo position wraps (D193).

`home` and `limits` are machine coordinates (the G53 / M91 frame). The file writes the X values the way the axis is programmed: diameters for `programming = "diameter"` or `"switchable"`, radii for `"radius"` or without `programming`. The VM halves `home`, `home2` and `limits` of a diameter axis once, where it takes them, into the radii it stores (D28, D137); `[positions]` stays as written, and `{position:NAME}` becomes an X word that the VM halves under `DIAMETER=ON`, so on a switchable machine such a rule carries `requires = { DIAMETER = "ON" }` and `restore = ["DIAMETER"]` (5a). Values copied from Siemens machine data (the software limit switches, the display in the machine coordinate system) are radii on a facing axis and must be doubled (SINUMERIK 840D sl/828D Funktionshandbuch Grundfunktionen 2012 p. 901-902); a Fanuc stroke check (parameters 1320/1321) is a diameter already (D137). `owner` of a rotary axis names what it turns (D325): a work spindle or a table that holds the workpiece makes it a table axis, counted only while its owner is the workpiece holder; a tool holder (turret, tool carrier, swivel head) makes it a head axis that turns the tool vector; a tool spindle makes it the spindle's own rotation (the C-axis mode of a tool spindle), which turns no tool vector. A rotary axis without `owner` is taken as a head axis, and the tool-vector report names that assumption in a WARNING. `owner` may name a table resource; `parallel` then says which linear axis the table axis turns about, the cylinder axis of `CYLINDER=n` (D146). The letter without digits gives the machine axis a rotary axis turns about (DIN 66217). `HOME` on an axis without `home` (or without `home2` under `POINT=2`) is a WARNING at check time, once per run and axis, and an ERROR only in a compiler that must write the coordinates (D100). `[positions]` names further machine-frame positions, each a set of axis values; expansion rules reach them through `{position:NAME}` (section 5a).

```toml
[positions]                        # named positions in machine coordinates (D100)
tool_change = { X = 0, Z = -120 }  # reached by expansion rules through {position:tool_change}
program_end = { X = 0, Z = -120 }  # optional
```

```toml
[dynamics]                         # control behaviour for the runtime estimate (D64)
block_time = 0.001                 # seconds the control needs per block; a short segment cannot be faster than this
path_mode = "continuous"           # continuous | exact_stop: the start value of the PATH_MODE state (VM 2.1); left out, exact stop (D350)
corner_speed = 2000                # mm/min carried through a corner in continuous mode
# rapid_exact_stop = true          # optional: whether the end of a RAPID is a rest point of the path; left out, true on a Fanuc-family machine (G00 in-position check) and false otherwise (D286)

[diameter]                         # how the machine switches, when `programming = "switchable"` on the X axis
ON = "DIAMON"                      # Siemens; Mori Seiki radius mode: ON = "M583", OFF = "M582"; Fanuc: none, the parameter decides
OFF = "DIAMOF"
```

The units of `rapid`, `max_feed` and `acceleration` follow the axis kind, in every plane: mm/min and mm/s^2 on a linear axis, deg/min and deg/s^2 on a rotary axis. The runtime estimate uses a rotary axis only where its word is a length: under `CYLINDER=r` its values convert at r (1 deg = pi r / 180 mm); under `POLAR=ON` at L, the closest approach of the tool centre to the centre of rotation along the block, refined for a line to the exact bound F <= max_feed x pi / 180 x r_min^2 / p, r_min its nearest distance from the centre and p the distance of its line (a radial line, p = 0, is not limited by C), and the X limits apply as well; acceleration converts the same way. Where L is 0, a block through the centre, the estimate takes the X limits alone and its report says "C clamp near centre not estimated"; a motion of C at an unknown or zero cylinder radius has a line of its own. A `RAPID` under `POLAR` is estimated with the rapid of X; under `CYLINDER` the rapid of C converts at r (D290; gap closed 2026-10-28, delegated).

`path_mode` is only the start value of the path-control state (virtual machine 2.1); `PATH_MODE` in the program changes it, `PATH_MODE=DEFAULT` and `PROGRAM=END` restore it, and the runtime estimate reads the state of each motion. A `[dynamics]` without `path_mode` starts at exact stop; a machine file without `[dynamics]` keeps the estimate at distance over feed. Where the program asks for a mode the machine has no `[transform]` template for, the state still rules the estimate and the report says so (D350). Under `CONTINUOUS` the path comes to rest at the rest points of D286 (virtual machine 8), among them the end of a `RAPID` on a Fanuc-family machine, which `rapid_exact_stop` overrides.

## 5. Functions and coolant

```toml
# functions per spindle role; the VM only knows SPINDLE:role=CW, the template is the machine's business
[spindle.MAIN]
CW = "M3"
CCW = "M4"
OFF = "M5"
ORIENT = "M19"                     # or "M19 S{angle}" where the angle is programmable
RPM = "S{rpm}"
VC = "G96 S{value}"                # Siemens: the compiler writes the group-15 code of the template as the code for the feed type the block leaves, G96 per revolution and G961 per minute (D300)
CSS_OFF = "G97"
RPM_MAX = "G50 S{value}"
rpm_min = 45                       # machine limits, not the program's RPM_MAX clamp: the expander warns or clamps (D64)
rpm_max = 6000
accel_time = 2.5                   # seconds from stop to rpm_max, for the runtime estimate of starts, stops and speed changes (D323)

[spindle.SUB]                      # Nakamura: +50, Mori Seiki: +200, Doosan: P suffix, Siemens: spindle number
CW = "M53"
CCW = "M54"
OFF = "M55"
ORIENT = "M419"
# no VC, CSS_OFF or RPM_MAX: the Fanuc compiler falls back to the family codes, G96 S{value}, G97 and G50 S{value} in
# G-code system A or G92 S{value} in B and C and on a mill; a key of the table overrides the fallback (D258)

[spindle.TOOL]
CW = "M88"
CCW = "M89"
OFF = "M90"

[spindle_mode.MAIN]
AXIS = "M91"                       # DMG on 840D: "L707({angle})"
SPINDLE = "M41"                    # DMG: "L708"

[spindle_sync]
ON = "M96"                         # Doosan: "M203", Mori Seiki: "M35", Biglia: "M61", Siemens: "COUPON(S3,S4)", DMG: "L726({angle})" with PHASE
OFF = "M97"                        # Mori Seiki: "M36", Biglia: "M63"
PHASE = "M92"                      # phase-synchronous variant (Mori Seiki "M34", Biglia "M62"); omit when the machine has none
channel = 2                        # D56: the machine accepts these codes only from this channel (Nakamura: the path that owns the second spindle)
# channels = "all"                 # D56: the code must stand in every channel program behind a SYNC (Mori Seiki M34/M35); a word every channel program already writes between the same two marks is left as it stands, and two channels writing such words between the same two marks is an ERROR (D312)
# pair = ["MAIN", "SUB"]           # D154: the two roles ON couples, the leader first; left out, the default spindle leads the other work spindle. Required with more than two work spindles, or when the default spindle is not a work spindle (DMG L725: pair = ["MAIN", "TOOL"])

[workpiece]                        # native selection of the spindle the turret works on, and how that side is programmed (D57)
MAIN = "M428"                      # Mori Seiki: "M303", Doosan: "M34", Biglia: "M64", DMG: "M814"; the selection code alone, never the datum, which is ORIGIN (D222)
SUB = "M427"                       # Mori Seiki: "M304", Doosan: "M134", Biglia: "M65", DMG: "M813"; a control without a selection code leaves the key out and WORKPIECE then writes nothing (D222)
SUB_frame = "datum"                # datum: the sub spindle datum runs Z the other way and the compiler negates Z; mirror: the compiler writes the mirror cycle. Both are the same frame, the main spindle's mirrored in Z (D57, D298)
# SUB_mirror = { ON = "G360", OFF = "G361" }   # Nakamura Z mirror cycles (M700/M701); Mori Seiki: { ON = "M396", OFF = "M395" }; a Z mirror also reverses G2/G3 and G41/G42 in the planes that hold Z, which the compiler does for SUB_frame = "datum" as well

[sync]
wait = "M{mark}"                   # Fanuc wait M code; with paths: "M{mark} P{paths}"
paths = "list"                     # list: P12, P13, P123 (parameter 8103#1 = 1); bitmask: P3, P5, P6, P7 (8103#1 = 0)
mark_range = [100, 199]            # Mori Seiki: [101, 197]; Siemens: [0, 99] with 95 taken by BARLOAD_SYNC
start_mark = 198                   # optional: written after the header of every channel program only when some channel program does not already pass it as its first mark with all channels, wherever that SYNC stands (D314); Nakamura NTJX M198, the WY template M199 (D388)
reserved_marks = [199]             # optional: marks of the range the job compiler never picks for a SYNC it generates (Nakamura G196 transfer cycle; DMG [0, 1, 10, 15, 20, 25, 80, 90, 95]); default empty. The pick takes the first mark of the range of all the job's channels that no channel program uses, that is not start_mark and not reserved; when every mark is used, the first that is not reserved; when every mark is reserved, CMP711. Each generated wait stands in a block of its own (D315)
# groups = [{ channels = [1, 4], range = [800, 839] }, { channels = [2, 3], range = [800, 839] }, { channels = [1, 3], range = [840, 849] }, { channels = [2, 4], range = [840, 849] }]
#                                  # Nakamura WTW, with for example channels 1 L-upper, 2 L-lower, 3 R-upper, 4 R-lower: M800-839 for L-upper/R-lower and L-lower/R-upper, M840-849 for L-upper/R-upper and L-lower/R-lower; a SYNC takes the range of the first group in file order whose channels hold all its participants, every other SYNC mark_range, and a group range may lie outside mark_range (D329); Mori Seiki NTX1000 narrows mark_range to [101, 197]
# Siemens: wait = "WAITM({mark},{channels})", channels written as "1,2" or as the template's variables "WAIT_K1,WAIT_K2"
start_channel = ""                 # Siemens: "START({channel})"
wait_channel = ""                  # Siemens: "WAITE({channel})"

[coolant]
STANDARD = { ON = "M8", OFF = "M9" }   # required: the channel of a bare COOLANT; a machine file without it is the ERROR CFG004 (D130)
AIR = { ON = "M14", OFF = "M15" }
THROUGH = { ON = "M51", OFF = "M9" }

[func]
CHIP_CONVEYOR = { ON = "M60", OFF = "M61" }
SUB_CHUCK = { OPEN = "M68", CLOSE = "M69" }
MAIN_CHUCK = { OPEN = "M66", CLOSE = "M67" }
TAILSTOCK = { FORWARD = "M21", BACK = "M22" }
DOOR = { OPEN = "M52", CLOSE = "M53", channel = 1 }   # D56: only channel 1 may command it; the job compiler moves the word

[func_meta]
SUB_CHUCK.CLOSE = "workpiece_transfer_to = SUB"   # reader fallback rule for machines without a [workpiece] table (D40)
# When neither table fits (a builder M code runs a hidden subprogram on this machine), a reader rule from a plugin decides; a plugin's rules are asked before the tables, and D40's order is the order of the built-in mechanisms (D40, D66, D231)

[transform]
CYLINDER_ON = "G7.1 C{r}"          # written for CYLINDER=30, {r} is that value, the reference radius (D96); Siemens: "TRACYL({d})" (the working diameter), DMG per spindle: "TRACYL_S4({d})"
CYLINDER_OFF = "G7.1 C0"
POLAR_ON = "G12.1"                 # Siemens: "TRANSMIT", DMG: "TRANSMIT_S4"
POLAR_OFF = "G13.1"
TCPM_ON = "G43.4 H{offset}"        # Nakamura: "G435", Siemens: "TRAORI", Heidenhain: "FUNCTION TCPM F TCP AXIS POS"
TCPM_OFF = "G49"                   # Nakamura: "G436", Siemens: "TRAFOOF", Heidenhain: "FUNCTION RESET TCPM"
TILT_ON = "G68.2 X{x} Y{y} Z{z} I{a} J{b} K{c}"   # Siemens: "CYCLE800(1,\"TC1\",{move},27,{x},{y},{z},{c},{b},{a},0,0,0,{dir},100,1)" ({x} {y} {z} in _X0 _Y0 _Z0, the shift before the tilt, D333; mode 27 is the axis-wise order Z Y X, which is TILT with the angles reversed, D335; {move} in the place of _ST, {dir} in the place of _DIR: one placeholder per native parameter, D262), Heidenhain: "PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move} {rot}" (D269)
TILT_OFF = "G69"                   # Siemens: "CYCLE800()", Heidenhain: "PLANE RESET STAY"
TILT_AXIS_ON = ""                  # empty = the target cannot write TILT_AXIS without the kinematics module (ERROR at compile time, D82)
                                   # Heidenhain: "PLANE AXIAL A{a} B{b} C{c} {move}"; Siemens: CYCLE800 with the rotary-axes mode (bits 7 and 6 set) and {dir}
                                   # On Siemens the two placeholders in the places of _A and _B also name the rotary axes the reader reads that mode into, in that order; without the template a CYCLE800 in the rotary-axes mode stays RAW (D296)
TILT_TURN = "G53.1"                # written after TILT_ON for MOVE=TURN where the control positions the axes in a second block (Fanuc); Heidenhain and Siemens use {move} and {dir}
move = { TURN = "TURN FMAX", MOVE = "MOVE ABST50 FMAX", STAY = "STAY" }   # values of {move} per MOVE option (Heidenhain); Siemens: the _ST of CYCLE800 with the tens digit 1 for MOVE, e.g. { TURN = "0", MOVE = "10", STAY = "0" }
# {dir} never comes from this table: it is -1 for TURN and MOVE and 0 for STAY, and a Siemens template without {dir} is an ERROR for any MOVE but TURN (D262)
# rot = { TABLE = "TABLE ROT", COORD = "COORD ROT" }   # values of {rot} per ROT option (Heidenhain, D269): the ROT of the block, TABLE when it has none, since an absent ROT means TABLE in NCX (language 4.2); the Heidenhain compiler always writes the type. Not in TILT_AXIS_ON: with PLANE AXIAL, and on a tilt that leaves no free rotary axis, ROT has no effect and is an INFO, not written. A machine without the map ignores ROT with the WARNING of language 4.2
                                   # no retract here: a RETRACT before the tilt is its own block, the Heidenhain MB and the Siemens _FR (D228, D83)
ROTARY_PATH_SHORTEST = "M126"      # D86; Siemens: rotary axes with DC() by the compiler, Fanuc: roll-over axis setting (empty = WARNING, kept as written)
ROTARY_PATH_FULL = "M127"
ROTARY_FEED_MM_MIN = "M116"        # Siemens: "FGREF[{axis}]={radius}" per rotary axis
ROTARY_FEED_DEG_MIN = "M117"
PATH_MODE_EXACT_STOP = "G61"       # PATH_MODE=EXACT_STOP (D349); Siemens: "G60"; Heidenhain: "" (Klartext has no modal switch)
PATH_MODE_CONTINUOUS = "G64"       # PATH_MODE=CONTINUOUS; Siemens: "G64"; Heidenhain: ""; an empty or missing template is a WARNING and nothing is written (CMP361, CMP533, CMP120)
# retract before a tool change is an expansion rule now: [tool_change] pre = ["FUNC:RETRACT=RUN"] with RETRACT = { RUN = "L_FREI" } (section 5a)

[retract]                          # the RETRACT verb (D83)
MAX = "M140 MB MAX"                # bare RETRACT: to the axis limit; DMG: "L_FREI"; empty = computed machine-frame move when the kinematics is known, else ERROR
BY = "M140 MB{distance}"           # RETRACT=50; Siemens: none (computed move under TRAORI with the kinematics module)

[tolerance]                        # the TOLERANCE words (D85)
ON = "G5.1 Q1"                     # Heidenhain: "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T{tol}\nCYCL DEF 32.2 HSC-MODE:{mode}"; Siemens (SINUMERIK Operate): "CYCLE832({tol},{mode},1)"; written while TOLERANCE:ROTARY is not set (D151)
# ON_ROTARY = "..."               # optional, written instead of ON while TOLERANCE:ROTARY is set (D151); Heidenhain: ON with " TA{rotary}"; Siemens: "CYCLE832({tol},1{mode},{rotary})", the 1 the tens digit of the orientation tolerance; the CYCLE832 form depends on the software version (two parameters up to SW 7.5), so each file writes the form of its version
OFF = "G5.1 Q0"                    # Heidenhain: "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T0"; Siemens: "CYCLE832(0,0,1)"
mode = { FINISH = 0, ROUGH = 1 }   # value of {mode}; Siemens: FINISH = 1, ROUGH = 3
held_in = "parameter 19581"        # optional (D367): where the control holds a value the written template has no placeholder for; the compiler names it in CMP015
held_value = 0.01                  # optional (D367): that value in millimetres, zero or greater; at least as tight as the program's TOLERANCE makes CMP015 an INFO, unknown or looser a WARNING
held_comment = false               # optional (D367): the compiler also writes a comment beside the switch naming the program's value and held_in, G5.1 Q1 (TOL 0.005 P19581)

[stock]                            # the STOCK templates, one per value form (D351); a form without a template writes nothing (INFO CMP012)
BOX = "BLK FORM 0.1 {axis} X{x} Y{y} Z{z}\nBLK FORM 0.2 X{max_x} Y{max_y} Z{max_z}"   # Heidenhain; Siemens: "WORKPIECE(,,,\"BOX\",240,{max_z},{z},{z},{x},{y},{max_x},{max_y})"
CYLINDER = "WORKPIECE(,,,\"CYLINDER\",192,{max_z},{z},{z},{diameter})"   # Siemens; the iTNC 530 has no cylinder form; a cylinder its form cannot express writes nothing (INFO CMP013)

[raw]                              # builder codes the reader keeps as RAW with the builder's name, RAW:NAKAMURA (F23)
known = ["G411", "G300"]
```

Function values may carry a parameter: `HIGH_PRESSURE = { ON = "H7={value} M7", OFF = "M9" }` for a coolant pressure that is set with the function. Function values are strings; a bare integer is accepted and means `M` followed by the number. The loader normalizes every M and G code to its spelling without leading zeros (`8`, `08`, `M08` become `"M8"`; `G01` becomes `"G1"`), the compiler writes that form, and readers compare codes by number, so a source `M08` matches the table's `M8` (D105). The normalization applies to every M or G code token of a value, also inside a multi-word template with parameters or placeholders: `"M03 P11"` loads as `"M3 P11"`, `"G01 X{x}"` as `"G1 X{x}"`; text that is not an M or G code (`H7=`, `P11`, `L707(...)`) is kept as written. The same normalization applies to every template of the file that contains an M or G code, in particular `[format] program_end` and `sub_end` (section 2), the `[tool_change]` templates (section 3), `[sync]` and `[transform]`, so that the compiler never writes a leading zero it did not compute from a placeholder; NCX text in `pre` and `post` lists (5a) is not affected, it carries no native codes. Siemens addresses a spindle by its number (`S2=`, `M2=3`, `SETMS(2)`): a spindle's number is the address extension its templates write, else the digits of its resource id (`S1` is spindle 1). The reader matches a number by those digits, as the compiler does; a spindle with neither keeps `SETMS(n)` RAW in the reader and is the ERROR CMP511 in the compiler (D294).

`[stock]` (D351): the compiler fills `{x} {y} {z}` and `{max_x} {max_y} {max_z}` with the two corners as the `STOCK` block gives them, `{axis}` with the tool axis of the active `WORKPLANE`, `{radius}` and `{diameter}` with the footprint, `{length}` with the extent along the tool axis and `{dist}` with the coordinate of the face nearest the spindle, the top of the blank along the tool axis (Heidenhain `DIST`, TNC 640 Klartext 340590-05 p.103). A `CYLINDER` template without `{axis}` is written about Z only, and one without a coordinate of the plane axes only for a cylinder centred on the tool axis through the datum; any other cylinder, and a corner given by an expression, writes nothing with the INFO CMP013. A `RAW` of the target controller or builder whose first word is the first word of a `[stock]` template (`BLK`, `WORKPIECE`) is a blank of the target: a program with it and a `STOCK` writes the `RAW` alone with the WARNING CMP014. `[tolerance]` (D151, D367): `ON_ROTARY` is written instead of `ON` while `TOLERANCE:ROTARY` is set; the value of `{tol}`, `{rotary}` or `{mode}` (for `TOLERANCE_MODE=ROUGH`, `FINISH` being the start value) that the written template has no placeholder for is CMP015 once per distinct value per program, an INFO only for `{tol}` with a `held_value` at least as tight; `held_comment` writes `TOL <value> <held_in>` as a comment beside the switch.

### 5a. Expansion rules

Some machines need a sequence where the program has one word. The example from the shop floor: the through-spindle coolant has a clutch that can only engage while the spindle stands still, so `COOLANT:THROUGH=ON` on that machine means stop the spindle, switch the coolant, start the spindle again with the speed it had. Every function table, the tool change and every catalog cycle may therefore carry four optional keys, and a function table also `states`; the expander (`ncx-virtual-machine.md`, section 1 and 3.10) turns them into generated NCX blocks around the triggering block:

```toml
[coolant]
THROUGH = { ON = "M51", OFF = "M9", states = ["ON"], requires = { SPINDLE = "OFF" }, restore = ["SPINDLE"] }

[func]
PALLET_CHANGE = { RUN = "M60", pre = ["HOME Z", "HOME X Y"], post = ["ORIGIN=1"] }

[tool_change]
pre = ["HOME Z"]                   # retract before the change; DMG: ["FUNC:RETRACT=RUN"] with RETRACT = { RUN = "L_FREI" }

# catalog entry
[[cycle]]
name = "PECK"
native = "G83"
pre = ["FUNC:PECK_MODE=RETRACT"]   # Doosan M291 before G83, as an NCX word so that the VM sees it
```

- `states`: the states of the function table whose words trigger the rule, `states = ["ON"]` on the clutch of the through-spindle coolant, so that the sequence runs when the coolant is switched on and not when it is switched off. Without the key every state of the table triggers it. One rule per table: a word triggers the state it sets, under the state names this section gives its table, so `SPINDLE=CW` triggers `CW`, `RPM`, `ORIENT` and `RPM_MAX` their own state, `CSS=ON` and `VC` the state `VC` and `CSS=OFF` the state `CSS_OFF`, `SPINDLE_MODE`, `COOLANT` and `FUNC` their value, `SPINDLE_SYNC` with two roles `ON` and `SPINDLE_SYNC=OFF` the state `OFF`, and `PHASE` the state `PHASE` (D198). The tool change and a catalog cycle have no states and take no key.
- `pre` and `post`: lists of NCX blocks inserted before and after the block. They are NCX text, not native lines, so the VM executes them and the reader can recognize them; native text goes through `RAW:BUILDER`. A native line that is part of the cycle rather than a word of the program is the `before` key of section 6 (D256). A block may contain `{position:NAME}`, which expands to the axis words of that entry of `[positions]`, e.g. `pre = ["RAPID {position:tool_change} FRAME=MACHINE"]` (D100).
- `requires`: state conditions that must hold while the function is written, as state variable = value (`SPINDLE = "OFF"`, `COOLANT = "OFF"`, `SPINDLE_MODE = "AXIS"`). The expander inserts the words that establish them, preceded by `@SAVE` of the variables named in `restore`. A rule whose `pre` or `post` reaches a `[positions]` entry through `{position:NAME}` writes an X word, which the VM halves under `DIAMETER=ON`; on a switchable machine it carries `requires = { DIAMETER = "ON" }` and `restore = ["DIAMETER"]`, as the DMG tool-change cycle L710 writes `DIAMON` before `G153 X` and restores the mode (D137).
- `restore`: the state variables to put back after the block, through `@RESTORE`; the VM re-applies the saved value from its own snapshot, so the rule never has to know the speed the spindle had. The state variables it can name are those of virtual machine 3.10 (D202); `ORIGIN` and `UNITS` are not among them. Restoring costs cycle time (a spindle stop and start), which the runtime estimate counts and a WARNING reports at compile time.

The triggering block of a catalog cycle is the block that names the cycle (`CYCLE=name` or `CYCLE:<controller>=n`), never a `CYCLE_CALL` block, and the rule covers the cycle while it is active: `@SAVE`, `requires` and `pre` stand before that block, `post` and `@RESTORE` directly after the `CYCLE=OFF` that ends the cycle, and directly before every other block that ends it (the next `CYCLE`, a `TOOL`, the `PROGRAM=END`, or the `SUB=END` of a subprogram, where generated blocks stay in their section). A required state therefore holds at every hole, and a `post` that moves cannot drill while the cycle is still modal on the control. The expander finds the end in the text of the section, without VM state, and a `JUMP` out of the range skips `post` and `@RESTORE` like any block it jumps over (D204).

Readers use the same rules backwards through a plugin's reader rule: a source sequence `M5`, `M51`, `M3 S1500` on a machine whose rule says the spindle stop belongs to `COOLANT:THROUGH=ON` is read as the one NCX word by a rule that claims the three blocks (`samples/plugins/ThroughCoolantSourceRule`), so the round trip does not accumulate stops. The rules see every block before the tables (D231) and the blocks that follow it (D232); 1.0 has no fold of its own from the configuration. Plugins that need more than these four keys get the same insertion points in code (`ncx-virtual-machine.md`, section 7).

A named function is written as `FUNC:SUB_CHUCK=OPEN` in NCX and compiles to the M code of that state. Readers map M codes back to names; an M code that matches no entry becomes `MFUNC=n` with a WARNING. The states of a `[func]` entry are free identifiers, the values `FUNC:name=` takes (`OPEN_RUNNING`, `NORMAL`, `LOW` and the like); the states of the other function tables are fixed keys: `CW`, `CCW`, `OFF`, `ORIENT`, `RPM`, `VC`, `CSS_OFF`, `RPM_MAX` in `[spindle.ROLE]`, `AXIS` and `SPINDLE` in `[spindle_mode.ROLE]`, `ON`, `OFF` and `PHASE` in `[spindle_sync]`, `ON` and `OFF` in a `[coolant]` channel (F23). Readers and compilers map them to the words of the language the same way (D154): `[spindle.R]` `CW`, `CCW`, `OFF` give `SPINDLE:R=state`; `ORIENT` gives `ORIENT:R={angle}`, and where the template carries no angle (`M19`, `M219`, `M319`, `M419`) the reader writes `ORIENT:R=0` with the WARNING RDR021, the orientation angle set by the machine, while a compiler writes such a template only for `ORIENT:R=0` and needs a template with `{angle}` for any other angle (the ERROR CMP007 without one, nothing written); `RPM` gives `RPM:R={rpm}`; `VC` gives `CSS:R=ON VC:R={value}`; `CSS_OFF` gives `CSS:R=OFF`; `RPM_MAX` gives `RPM_MAX:R={value}`; `[spindle_mode.R]` gives `SPINDLE_MODE:R=state`; `[coolant] C` gives `COOLANT:C=state`, the channel `STANDARD` as a bare `COOLANT`. The address is written as the examples do: a spindle word carries its role on a machine with more than one spindle and none on a machine with one. `[spindle_sync]` `ON` couples the default spindle, the leader, with the other work spindle, `SPINDLE_SYNC=default,other` (`MAIN,SUB` in the example files), or the two roles of `pair`; `PHASE` gives the same plus `PHASE={angle}`, `PHASE=0` with RDR021 where the template has no angle (`M92`, `M213`, `M34`); `OFF` gives `SPINDLE_SYNC=OFF`.

On Siemens a spindle's number, the n of `S<n>=`, `M<n>=3` and `SETMS(n)`, is the address extension its templates write, else the digits of its resource id (`S1`, `SP1`); the reader matches by that number as the compiler writes it, and a spindle with neither keeps `SETMS(n)` `RAW` in the reader and is an ERROR in the compiler (D294).

A native code may be written by several states: `M9` is the `OFF` of every coolant channel of the Nakamura, `M3` the `CW` of `MAIN` and, under `M90`, of `TOOL` on the Mori Seiki, `M34` on the Doosan the `[workpiece]` selection and the `SPINDLE` of `[spindle_mode.MAIN]`. The reader is given every state that writes the code, in file order with the values it carried, a `[workpiece]` template first (D40). It drops the states its source state rules out (a table bound to another channel, a spindle the source does not address, an `OFF` of a resource that is not on) and writes every remaining state that does not already hold. When nothing remains it writes the first state in file order; when two resources of one word would be switched on it writes the first and reports the WARNING RDR020 naming the others (D155). Besides its states, a function table may carry `channel` or `channels` (D56), the four keys of its expansion rule and `states`, the states that rule fires on (5a, D198); a spindle table also carries `rpm_min`, `rpm_max` and `accel_time` (D64).

## 6. Cycle catalog

```toml
[cycles]
catalog = "heidenhain.toml"         # controller family catalog <family>.toml, shipped with NCXchange, user-extendable (D156); looked up in the cycle folders (D207)
chip_break_lift = 0.6               # optional: the lift of CHIP_BREAK before it feeds on (Fanuc 5114, Heidenhain Q256, Siemens _VRT), mm (D357)
peck_clearance = 1.0                # optional: the end of the rapid re-entry of PECK above the depth reached (Fanuc 5115, Siemens _DIS1), mm (D357)
ream_feed_out = "CLEARANCE"         # optional: CLEARANCE | SAFE, where REAM stops feeding out under CYCLE_RETRACT=SAFE (D378)
surface_clearance = 2.0             # optional, Fanuc reader: SURFACE = R - surface_clearance, SURFACE = 0 when absent (D158)
contour_type_ii = false             # optional: the control has the type II pocketing of G71/G72 (D278, D388)
peck_rigid_tapping = false          # optional: the control has peck rigid tapping, Q on G74/G84 (D279, D388)

# excerpt of a catalog file
ncx_schema = 1                      # optional, as in the machine file (section 1, D348)

[[cycle]]
name = "RECT_POCKET"
native = 251
params = { LENGTH = "Q218", WIDTH = "Q219", DEPTH = "Q201", PECK = "Q202", CYCLE_F = "Q206", SURFACE = "Q203", CLEARANCE = "Q200" }
absolute_from_surface = ["DEPTH"]   # native Q201 is relative to Q203, NCX DEPTH is absolute

# excerpt of the Fanuc catalog
[[cycle]]
name = "ROUGH_TURN"
native = "G71"
contour = ["P", "Q"]                # first and last block of the contour, the SUB section of CONTOUR=name (D65)
```

The `catalog` value is looked up in the cycle folders alone, in this order: the folder `ncx.toml` names, `cycles/` of the working directory (the project), `cycles/` of the tool's own folder, and as the fourth and last place the catalogs built into `ncx` (D371). A value with a folder in it counts from each of them; a full path, or one that climbs out with `..`, names no file. A catalog found nowhere or unreadable is CLI202, exit code 2 (D97); one that loads with an ERROR exits 1. A shop's own catalog may override a shipped one on purpose; the CAM's bundle is protected by the schema version of D348 (D207).

The built-in drilling family (`DRILL`, `DRILL_DWELL`, `PECK`, `CHIP_BREAK`, `TAP`, `REAM`, `BORE`) is defined in the same way inside NCXchange and can be overridden per machine. Four catalog files ship with `ncx`: `cycles/fanuc.toml` with the mill codes of the Fanuc family, `cycles/fanuc-lathe.toml` with the lathe drilling cycles along Z and along X and the turning cycles, which a Fanuc lathe names instead of the mill file, and `cycles/heidenhain.toml` and `cycles/siemens.toml` (D160). A catalog file is written in the numbers of Fanuc G-code system A, and the reader and the compiler translate them to the system of the machine through `gcode_system`, so no entry key ties an entry to a G-code system (`../controllers/fanuc.md` 3, D174). The `native` of an entry is the native cycle as the control writes it, a number for Heidenhain (`251`) and a string for Siemens and Fanuc (`"CYCLE952"`, `"G71"`, `"G83.5"`); a program that writes the cycle natively gives exactly that text after the controller address of its `CYCLE` word (`CYCLE:FANUC=G71`, `CYCLE:FANUC="G83.5"`; language 4.7, D144).

The other keys of `[cycles]` are facts of the control that no cycle block carries. `chip_break_lift` and `peck_clearance` are decimals in millimetres whatever `units_default` is, zero or greater; a negative value is the ERROR CFG005. The VM reads them where it expands a drilling cycle (virtual machine 3.3), and so do the runtime estimate and the CAM's simulation (opencam D-061). A key left out means the family's documented behaviour: on Siemens the manual's 1 mm of `_VRT`, and the control's automatic `_DIS1`, 0.6 mm up to a drilling depth of 30 mm, otherwise the depth / 50, at most 7 mm (840D sl ISO-Fräsen 2012 p.93-94, ISO-Drehen 2012 p.104); on Heidenhain the re-approach is the cycle's `Q200`, the gap between `SURFACE` and `CLEARANCE`, so a Heidenhain file never needs `peck_clearance`; on Fanuc the VM takes 0, and the runtime report names the key it defaulted (D357). `ream_feed_out` applies only under `CYCLE_RETRACT=SAFE`: `REAM` feeds at `CYCLE_F` up to that plane and rapids the rest. Absent, the family default applies, `CLEARANCE` on Fanuc and Heidenhain and `SAFE` on Siemens, whose `CYCLE85` retracts at its retract feed to the retract plane; a TNC 640 or TNC 128 machine file writes `"SAFE"`, because cycle 201 there feeds to `Q204`. Any other value is the ERROR CFG005. Only the VM, the runtime estimate and the CAM's simulation read it; the compilers write nothing from it (D378). `surface_clearance` is read by the Fanuc reader only, and the Fanuc compiler writes `R` from `CLEARANCE` (D158). `contour_type_ii` and `peck_rigid_tapping` declare the two Fanuc options the reader and the compiler need (D388). Once the catalog file is under the machine, or the built-in drilling family where `[cycles]` names no catalog, the keys the machine sets are checked against it on the line of `[cycles]`: a key beside a `defaults` entry of the same native parameter (Heidenhain `Q256` from `chip_break_lift`; Siemens `_VRT` from `chip_break_lift` and `_DIS1` from `peck_clearance`) is the WARNING CFG156, and a Heidenhain catalog that writes `Q256` on a machine without `chip_break_lift` is the ERROR CFG158 (D358).

Besides `name`, `native`, `params`, `absolute_from_surface` and the expansion rule keys of 5a, an entry may carry:

- `modal = true`: the native cycle stays active after its block and every following block with a position calls it again, as Fanuc `G81`..`G89` and the simple turning cycles `G90`, `G92`, `G94` do; the reader emits `CYCLE=` and one `CYCLE_CALL` per such block and the compiler writes the repeat blocks back the same way (language 4.7.1). The default `false` is a native block that runs once where it stands (Fanuc `G70`..`G76`). Heidenhain and Siemens entries leave it out: their calls are words of their own (`CYCL CALL`, `M99`, `MCALL`).
- `contour`: the native words that carry the contour of `CONTOUR=name` (language 4.7, D65). Two words are the first and the last block of the contour range (Fanuc `P` and `Q`): the reader turns the range into a `SUB` section of the file; the Fanuc compiler writes the section inline after the first cycle of the program that names it, its first and its last block with the block numbers `P` and `Q`, which follow the greatest numeric label of the program (D277), and a later cycle of the program repeats `P` and `Q` alone (D278). The first block writes `G00` or `G01`; for `G71`/`G72` it names only the plane axes it moves, `X` alone (`Z` for `G72`) for type I and both for type II, which needs `contour_type_ii`. One word names the contour subprogram itself (Siemens `CYCLE95` `NPP`).
- `signature`: every native parameter in the order the control writes it, also those without an NCX word: the positional parameters of a Siemens cycle, the `Q` parameters of a Heidenhain `CYCL DEF` (`../controllers/siemens.md` 12, `heidenhain.md` 8). A Siemens position without a value stays empty and the cycle takes its default (`siemens.md` 7). A native name in `params` that the signature does not list is an address word of its own: Siemens `CYCLE_F = "F"` is the modal `F` before the call that `CYCLE81`..`CYCLE83` use (controller-mapping 5). Fanuc entries, whose parameters are address words of the cycle block, leave it out.
- `before`: one native line that belongs to the cycle itself and stands directly before the cycle block. The compiler writes it there, the reader folds it into the cycle when it finds it there, and it is a template like the function tables, so it may carry `{rpm}`: the Fanuc `TAP` entry has `before = "M29 S{rpm}"`, the rigid tapping line, which a machine file overrides with its builder's code (Mori Seiki `M329 S{rpm}`) and sets to `""` where a parameter makes the cycle rigid without one. A cycle written without the line is the other machining, tapping with a compensating chuck (D256): `TAP_COMPENSATING`, an entry on the same native with no `before` key. Of two entries on one native the reader takes the one whose `before` line reaches the block in one of D256's three ways, and the one with no `before` key where it does not, an amendment of the choice rule of D160 below; the compiler writes the entry the program names, with its line or without. Where a machine's `TAP` sets `before = ""`, its `G84` is always rigid and an entry with no `before` key counts as not in that machine's catalog (D359).
- `fixed`: native parameters the entry always writes with this value, which tell two entries of one native cycle apart: Siemens `CYCLE83` with `VARI = 1` is `PECK`, with `VARI = 0` `CHIP_BREAK` (controller-mapping 5); the reader takes the entry whose fixed values the source block carries. Of two entries on one native cycle the reader takes the first in catalog order whose fixed values the block carries and whose `params` cover its words, so that a Fanuc lathe `G83` with a peck depth `Q` is `PECK` and one without it `DRILL`, the `DRILL` entry standing first (D160).
- `defaults`: a native name and the value the compiler writes at that position of the signature where no NCX word and no `fixed` value fills it, a number or the control's literal (`cycles/heidenhain.toml` 203: `Q210 = 0`, `Q212 = 0`, `Q208 = "MAX"`). The Heidenhain reader takes the entry only when the unmapped source values equal what the compiler would write, not comparing a value the cycle does not use in that form (`Q256` when `Q213` = 0, `Q205` when `Q212` = 0); otherwise it keeps the cycle as `CYCLE:HEIDENHAIN=n` (D94, D5). On Siemens the key records the control's own default of a position, which the reader assumes for an empty position (an omitted parameter is 0, so `VARI` of `CYCLE83` defaults to 0), while the compiler leaves that position empty (D164, D181). A distance the control holds and a `[cycles]` key gives (`chip_break_lift`, `peck_clearance` of D357) is never a default: the key is its one source, the Heidenhain compiler writes `Q256` from `chip_break_lift`, the Siemens compiler `_VRT` and `_DIS1` of `CYCLE83` from the two keys. A key set while `defaults` names the same native parameter is the WARNING CFG156, naming both places, and a Heidenhain machine whose catalog writes `Q256` without `chip_break_lift` is the ERROR CFG158 (D358).
- `title`: the text the Heidenhain compiler writes after `CYCL DEF n` (`title = "BOHREN"`); an entry without one is written `CYCL DEF n` alone, with no diagnostic, the control adding the name in its dialog language when it reads the program (D270).
- `channel` and `available`, keys of a `[[cycle]]` table of a machine file only: `channel` applies the table to that channel's compilation, as the same key of a function table does (section 5, D56), the table of the compiled channel being taken first and the channel-less one after it; `available = false` (default `true`) marks a cycle the machine cannot run on that channel, or anywhere without `channel`, which the compiler reports as the ERROR CMP004 naming the cycle and the channel, writing nothing for it, no `before` line and no substitute such as a bare `G84` or `TAP_COMPENSATING`. In a catalog file, which serves every machine of its family, both keys are the WARNING CFG157 and ignored (D361, D377). The builder's rigid-tapping line is machine data: `mori-ntx1000-mapps.toml` gives `TAP` `before = "M329 S{rpm}"`, while both paths of `nakamura-ntjx.toml` keep the catalog's `M29 S{rpm}` (`M429` being the Nakamura work counter, D377), and a Fanuc-family machine that names no code writes `M29`, the Fanuc standard (parameter 5210 = 0).
- `dwell_native`: the native the entry is written as where the block carries a `CYCLE_DWELL` other than 0 and its own native has no dwell, the Fanuc mill `REAM` (`G85`) with `dwell_native = "G89"`; the `P` that `params` maps is the `P` of that native, and the reader reads the native back as the entry with `CYCLE_DWELL` (D177). The lathe `REAM` writes `dwell_native = "G85"`, its `G85` taking `P` itself.
- `for_axis`: the drilling axis the entry maps its cycle under. The entry without the key maps the default axis, so a Fanuc lathe gives `DRILL` twice, `G83` along Z and `G87` under `for_axis = "X"`; the reader sets `AXIS` from the entry it matched and the compiler looks an entry up by its name together with `AXIS`. An entry is found, overridden and checked for duplicates by its name and its `for_axis` together (D160).

A parameter of an entry that is not a word of language 4.7 takes a number or an expression, and the words of 4.7 keep their 4.7 kinds; a native control literal (Heidenhain `FMAX`, `FAUTO`, `FU`, `FZ`, `PREDEF`, `MAX`) stands only on the native side, in the `defaults` of an entry, and a Heidenhain definition that carries one at a position mapped to an NCX word is kept `RAW` (D5). 1.0 has no key for the kind of a parameter; a per-entry `kinds` table (R/I/C/S, after Siemens `uc.com`) is reserved for later string parameters (D115).

An entry of a later file overrides the entry of the same name key by key: a key it writes replaces the earlier value (a table key such as `params` as a whole), a key it leaves out stays, each of `pre`, `post`, `requires` and `restore` is a key of its own, an entry of a new name is appended, and `native` is required on every entry (D140).

The words a rule of the reader and the compiler carries instead of a native parameter follow from the `params` of the entry, with no key of their own, for every entry, the built-in family included (D157, controller-mapping 5): on Fanuc an entry that maps `CLEARANCE = "R"` carries `SURFACE`, `SAFE` and `CYCLE_RETRACT`, and an entry whose native is the tapping code (`G84`, the lathe `G88`) carries `PITCH` (D359); on Heidenhain an entry that maps `SAFE = "Q204"` carries `CYCLE_RETRACT`; on Siemens an entry that maps `SAFE = "RTP"` carries `CYCLE_RETRACT`; and an entry that maps `DEPTH`, the depth along the drilling axis, carries `AXIS` unless it maps `AXIS` itself.

The loader checks every entry of a catalog file and of a machine file (D141): ERROR CFG150 for a name written twice in one file (by name and `for_axis`), CFG151 for a name or a `params` word that is no NCX identifier, CFG154 for a `contour` of more than two words; WARNING CFG152 for a word of `absolute_from_surface` that `params` does not map, CFG153 for `absolute_from_surface` without a mapped `SURFACE`, and CFG155 for a key the entries of the family leave out (`modal` on Heidenhain and Siemens, `signature` on Fanuc). A `params` word of the machine-axis form of D93 (`W2`, `Z2`) is the ERROR CFG159: a program reads such a key as a machine axis word, so a catalog parameter of that form would read differently with and without a machine file (D114).

A cycle word of a block that the entry maps to no native parameter and no rule word carries follows one rule (D362): where the native cycle reaches the same value from another word of the block it is a compiler WARNING (a drilling entry that maps `PITCH` and no `CYCLE_F` takes the tapping feed from the pitch: Heidenhain `TAP`, Siemens `TAP` and `TAP_COMPENSATING`, so `TAP` needs only `PITCH`; `CycleEntry.DerivedFrom`); where the value is the behaviour the control has anyway (`CYCLE_DWELL=0`, `PECK=0` on a cycle without that motion) it is dropped silently; where the value is lost it is an ERROR. Each stands once at the `CYCLE` block, not at each `CYCLE_CALL`, and names the word, the native cycle and where the control takes the value from; a `CYCLE:<controller>=n` block has no `params`, so D279's ERROR stands there.

## 7. Variables

```toml
[variables]
unassigned = "error"               # error | 0
map = { Q = "#1{index:02}", QL = "#{index}", QR = "#5{index:02}" }   # compile Heidenhain-named variables to Fanuc ranges: Q1 -> #101, QR1 -> #501, QL1 -> #1 (D173)
declared = ["XMW_1", "GC1_XDREMI"]        # optional: names the machine declares outside the program (Siemens GUD and the builder's variables). The Siemens compiler declares every other name a unit uses with DEF REAL in its definition part and never these, because a DEF of an already defined symbol is an alarm (D261)
block_cap = 1000000
call_depth = 8
repeat_counters = ["#149", "#148"]  # optional, Fanuc: the counter variable of each REPEAT nesting depth (D280)

[system_variables]                 # NCX SYS_ names to native system variables (D51); {index} is the NCX index
SYS_POS_X = "#5041"                # workpiece coordinates
SYS_MPOS_X = "#5021"               # machine coordinates; the Nakamura transfer program reads #5024 and #5025 for the sub spindle slide
SYS_MPOS_B = "#5024"
SYS_WEAR_Z = "#11{index:03}"       # #11099 = Z wear register 99 in the Nakamura transfer program; Siemens: "$AA_IW[X]", "RG{index}"; Heidenhain: FN 18 ids, "ID270 NR1 IDX1" the X position, "ID240 NR1 IDX1" its REF position, "ID50 NR4 IDX{index}" the length oversize DL of a tool (D182)
SYS_PART_MAIN = ""                 # part status of the Nakamura G411 machines has no readable variable; G411 stays RAW
```

Every entry of `map` is a template of the variable's number with `{index}`, like the entries of `[system_variables]`, because a bare prefix does not state the width the control needs: `Q = "#1{index:02}"` writes `Q1` as `#101`, while the Siemens entry `Q = "R{index}"` writes `R1`, the spelling a SINUMERIK program uses, and not `R01`. An entry without `{index}` is an ERROR of the machine file. A variable name that no entry covers is an ERROR at compile time that names the variable; this includes the Heidenhain `QS` strings, because an R parameter is a field of type REAL and holds no string. Names the source itself wrote, a Siemens `R5` or a declared name, are written as they are (D173).

`repeat_counters` names one variable per `REPEAT` nesting depth, the first for the outermost repeat. The Fanuc compiler writes the count from `TIMES` into it, the `IF` `GOTO` that loops, and the clearing that a `JUMP` leaving the section needs (D213). A `REPEAT` that needs a counter on a machine without the key, with too short a list, or with a counter the program uses itself is an ERROR; there is no fallback to `#33` downward. Choose variables from `#100`..`#149`, which exist without the extended-variables option (D280).

## 8. Job manifest and variable start values

```toml
# shaft.ncxjob.toml
[job]
name = "SHAFT"
machine = "nakamura-ntjx.toml"      # looked for next to the manifest first, then as --machine takes it (D303)

[[channel]]
id = 1
file = "shaft.ncx"                 # the first program of the file runs unless `program` names another (D48)

[[channel]]
id = 2
file = "shaft.ncx"
program = "SHAFT_CH2"              # NAME of a PROGRAM section in that file; the two channel programs may also be two files

[shared]
spindles = ["S1", "S2"]             # commanded from both channels; the scheduler checks conflicts
axes = ["B1"]                       # machine axes commanded from both channels, checked like the spindles (D20, F24)
```

A job manifest requires `[job] machine` and `id` and `file` of every `[[channel]]`; every other key is optional (D138).

```toml
# pattern.vars.toml, start values for analyze
Q1 = 10
Q2 = 5
QS1 = "TEXT"
"SYS_WEAR_Z[99]" = 0.012
```

A key is the name as the program writes it, without `$`. A register of a `SYS_` name is quoted, with its index as plain whole numbers: `"SYS_WEAR_Z[99]" = 0.012`; the key grammar allows a comma-separated multi-index, `"SYS_X[3,1]"`, from the start. Unindexed names stay bare keys. A key of another form is an ERROR (CFG005). The evaluator uses a value of the vars file wherever the VM has none, also for a `SYS_` name the configuration does not map; otherwise reading it is VM903, whose message names the vars file (D120).

## 9. Kinematics (optional)

Read only by the kinematics module. A flat node list with `parent`; resources are leaves.

```toml
[[node]]
id = "X1"
parent = "BASE"
type = "linear"
direction = [1, 0, 0]

[[node]]
id = "Y1"
parent = "X1"
type = "linear"
direction = [0, 1, 0]

[[node]]
id = "TABLE1"
parent = "Y1"

[[node]]
id = "Z1"
parent = "BASE"
type = "linear"
direction = [0, 0, 1]

[[node]]
id = "S3"
parent = "Z1"
type = "rotary"
direction = [0, 0, 1]

[[node]]
id = "H1"
parent = "S3"
```

Or, for a long tree: `[machine] kinematics = "dmu50.kin.toml"`.

## 10. Project layout

```
my-shop/
  ncx.toml                 # working directory settings, plugin DLLs, the machine file --machine defaults to (the built-in default machine of D103 applies only when neither names one)
  plugins/
    MyShopRules.dll          # every DLL here is a plugin; ncx plugin build puts a plugin here (D317, D330)
  machines/
    dmu50.toml
    fanuc-vmc.toml
    millturn1.toml
  cycles/
    heidenhain.toml          # the shipped files are fanuc.toml, fanuc-lathe.toml (D160), heidenhain.toml and siemens.toml; a shop catalog may have any name (D156)
  programs/
    part-123.ncx
    part-123.vars.toml
  out/
    dmu50/part-123.h
    fanuc-vmc/part-123.nc
```

Every machine file and catalog file of the layout may begin with `ncx_schema = 1` (section 1, D348); the version is the schema's, so a file copied from another project keeps it.

`ncx.toml` names the plugin assemblies in the table `[plugins]`, `assemblies = ["MyShop.NcxPlugins.dll"]`, and holds the settings of each plugin in a section `[plugins.<name>]` below it (D80, D238); no plugin may be named `assemblies`, and a top-level list `plugins = [...]` is the ERROR CFG003 (wrong type). A listed name is a path from the working directory, else the file of that name in `plugins/`. The listed DLLs load first, in list order, then the other DLLs of `plugins/` in ordinal name order, each file once, and the classes of one DLL in ordinal order of their full names (D317); the plugins act in that order (`ncx-virtual-machine.md`, section 7). A DLL of `plugins/` without a plugin class is the WARNING PLG005. `ncx plugin build` copies a plugin that brings no other assembly into `plugins/` and lists its name; a plugin that brings assemblies stays in its build output, whose path it lists, so that its load context finds them beside its `.deps.json`, and an earlier copy of it in `plugins/` is removed (D330). A plugin name loads once: a second DLL of the name is the WARNING PLG007.

```toml
[plugins]
assemblies = ["MyShopRules.dll", "Clutch/bin/ncx/Clutch.dll"]

[plugins.MyShopRules]
before_tool = "HOME Z"
```

A machine named without a path is looked up in `machines/` of the folder `ncx.toml` names, of the working directory and of the tool's own folder, in that order, and a catalog in the `cycles/` folders in the same order; a value that names an existing file, or has a folder in it, is a path (D206, D207). The installation of `ncx` holds the shipped machine files and catalogs twice: loose in `machines/` and `cycles/` beside the binary, to read and copy, and built into the binary as the fourth and last place of both lookups, so that a single-file `ncx` run on its own finds them (D371). A shop's own file of a shipped name wins; a shop that wants a shipped machine gone shadows it with its own file of that name, since deleting the loose copy falls back to the built-in one. A built-in file is named in a diagnostic as `built-in machines/<name>.toml`. The plugin template is no built-in file: `ncx plugin new` from a lone binary reports CLI552.

## 11. Example machine files

`examples/machines/` holds six configurations written from the manuals listed in `controller-mapping.md`, section 10: five from the builder manuals and `millturn1.toml` from the SINUMERIK 840D sl programming manuals alone (D104). They are sketches of the schema, not verified against a machine: `nakamura-ntjx.toml` (Fanuc 18i-TB, two turrets, two spindles, tool spindle with ATC, the lower turret's rotary tool `TOOL2` on `M488`/`M489`/`M490`; M and G codes from the Walter Meier list 2016 and the Nakamura programming manual 4810004EDG), `nakamura-wy250l.toml` (Fanuc 31i-B, the machine of the WY-250L pair in `examples/sources/`: `nakamura-ntjx.toml` with the turret change `G0 T{tool:02}{offset:02}` on both turrets and no ATC, the lower turret's rotary tool `TOOL2` on `M488`/`M489`/`M490`, `start_mark = 199` of the WY header template and `M198`, its wait before the bar-end check, in `reserved_marks`, strokes, rapids and spindle ranges of the WY-250L operation manual 4800602ECA; from the Nakamura programming manual 4810004EEE and the WY guides 2008 and 2019), `doosan-puma-2600sy.toml` (Fanuc, single channel, sub spindle, milling spindle selected by `P` on the M code), `mori-ntx1000-mapps.toml` (Fanuc 31i under MAPPS, tool spindle with `T` + `G361` change, turret 2, sub spindle on the A axis, `M34`/`M35`/`M36` synchronization in both channel programs; from the NTX1000SZM programming manual), `dmg-ctx-840d.toml` (Siemens 840D, GILDEMEISTER structure programming: `L7xx` cycles for C axes, synchronous run and tool change point, `TC` tool change through the named templates `change_named` and `preload_named` (D172), `WAITM` for two channels with the builder's marks in `reserved_marks` (D315), the M list of the Technik Programmierung V2.1), `millturn1.toml` (a generic SINUMERIK 840D sl mill-turn without builder cycles: roles `MAIN` = `S1` with axis `C`, `SUB` = `S2` with axis `C2`, `TOOL` = `S3`, `TURRET1` = `H1`; axes `X Y Z C Z2 C2`; functions `SUB_CHUCK`, `MAIN_CHUCK`; numeric tools with `T{tool} D{offset}`; `COUPON`/`COUPOF` for `SPINDLE_SYNC`; `[workpiece]` with `SUB_frame = "datum"`; the machine of `examples/MILLTURN_TRANSFER.ncx`, which checks and compiles for it, D104).
