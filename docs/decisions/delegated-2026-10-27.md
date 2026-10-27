# Delegated decisions of 2026-10-27

On 2026-10-27 Peter delegated the open decisions: a short web research first, else the most logical approach a control manufacturer would take. This page indexes the 162 decisions taken on that delegation (D165, the copyright holder, stays with Peter). The full text of each, with its source, is in `rationale.md` under "Answered (tenth to thirteenth rounds, 2026-10-27)"; the rows are in `decisions.md`, dated "2026-10-27 (delegated)".

Basis: the manuals settled most of them; the web added little (forum posts and post-processor sources at best); the rest are reasoned. 55 differ from the draft recommendation; the confidence column marks the ones a machine test could still overturn.

## Decisions that differ from the recommendation

| D | Question | Confidence | Basis | Decision |
|---|---|---|---|---|
| D115 | Kind of a catalog parameter | medium | manual + logical | No kind key in 1.0, as recommended. An NCX catalog parameter that is not a word of language 4.7 takes a number or an expression. Words of 4.7 keep their 4.7 kinds. Native control literals (Heidenhain FMAX, FAUTO, FU, FZ, PREDEF) … |
| D120 | Indexed SYS_ registers in the vars file (machine-config 8) | medium | manual | Key = the name as the program writes it, without `$`, quoted, with the index as plain whole numbers: `"SYS_WEAR_Z[99]" = 0.012`; unindexed names stay bare keys. Allow a comma-separated multi-index (`"SYS_X[3,1]"`) in the key … |
| D129 | Default spindle of the default machine | medium | manual + logical | `default_spindle` of the D103 default machine is TOOL, as the code has it. Also take the draft's option: the VM 5 WARNING "spindle OFF before a LINE" fires only when neither the active holder's spindle nor the spindle of the … |
| D130 | Coolant channel the machine file does not name | medium | manual | With a machine file, an unnamed coolant channel is an ERROR; without one it is the D103 WARNING "not checked: no machine file", once per name. Add "coolant channel" to the VM 5 ERROR list, VM 3.8 and the D103 WARNING wording. … |
| D145 | The index of a variable (language 4.12) | medium | manual + logical | Only a SYS_ name takes an index. An index on any other variable is a PAR ERROR (the check moves from Evaluator into ExprParser.ParseVariable, as recommended). An index written as a literal number must be a whole number of 0 or … |
| D146 | Cylinder plane when the workpiece's rotary axis does not turn about Z | high | manual | Follow the recommendation. The cylinder axis is the linear axis parallel to the rotary axis of the current workpiece holder: Z for an axis owned by a work spindle, otherwise the new optional `[[axis]] parallel = "X"\|"Y"\|"Z"` … |
| D154 | Function-table states as NCX words, and the pair of [spindle_sync] | medium | manual + logical | Follow the recommendation's mapping table, with one change for ORIENT. ORIENT gives ORIENT:R={angle} when the template carries an angle. When the template has none (M19, M219, M319, M419), the reader writes ORIENT:R=0 plus a … |
| D157 | Words a family rule carries instead of a native parameter | high | manual | Follow the recommendation: the rule words follow from the entry's params, with no new key, and CycleEntry.RuleWords is computed for every entry. Corrected rules for controller-mapping 5: - Fanuc: an entry that maps CLEARANCE="R" … |
| D159 | Unit of the Fanuc G4 P | medium | manual + logical | No `"ms"\|"s"` key. Instead, add `[machine] dwell_increment` (seconds per unit of G4 P, default 0.001). It also applies to G4 X/U written without a decimal point and to the P of canned cycles (until a manual shows otherwise). … |
| D169 | Strings in comparisons (language 4.12) | medium | manual | Take the alternative. `==` and `!=` compare two strings by exact, case-sensitive text and yield 1 or 0. Every other operator, and a string against a number, stay VM902. There are still no string literals; they remain a later … |
| D170 | Resources created on the spot without a machine file | medium | logical | Keep ResourceResolver.CreateOnTheSpot: every spindle word and WORKPIECE create a work spindle with axis `C_<role>`; TOOL and PRELOAD create a holder without a spindle. Close the id collision the recommendation names: a created … |
| D177 | Fanuc dwell P outside G82 and G84 | high | manual | Mill catalog (cycles/fanuc.toml): CYCLE_DWELL = "P" only on DRILL_DWELL (G82) and TAP (G84), plus REAM with a dwell, which goes out as G89 (read back as REAM with CYCLE_DWELL). The compiler writes a DRILL with CYCLE_DWELL != 0 as … |
| D185 | Vector words under RAPID, ARC and CYCLE_CALL | medium | manual | RAPID and LINE carry TX TY TZ (NX NY NZ optional) only under TCPM=ON, with one meaning: the tool direction at the end point, stored unresolved. ARC may carry TX TY TZ under TCPM=ON too, with the same meaning, because Fanuc G43.5 … |
| D191 | What table kinematics is for the ROT WARNING | medium | manual | VM422 no longer tests for "table kinematics". With a machine file it fires for ROT when the machine has no rotary [[axis]] at all, whatever the owner (table, work_spindle or head), because no free rotary axis can then arise. … |
| D213 | A JUMP out of a running REPEAT | low | web + logical | As recommended in NCX: a JUMP or JUMP=END to a target outside the label..REPEAT section ends the repeat, and a later arrival at the REPEAT starts it fresh with TIMES passes. Compilers must reproduce this. Fanuc uses its counter, … |
| D223 | Channel of a Fanuc file without a path suffix | high | manual | The Fanuc reader writes CHANNEL=n for a file whose name ends in `.P-n` (n = 1..10, with or without a further extension) on any Fanuc machine, without the nakamura restriction. `.P-1` is read as CHANNEL=1, not as "no channel". If … |
| D230 | Machine position of a rotary axis moved under a tilt | medium | manual | Take the draft's alternative. A TILT or TILT_AXIS turns the linear frame only. A rotary axis word under it moves the machine axis by its workpiece-coordinate difference, as under ROTATE, so its recorded machine position stays … |
| D231 | Reader rules against the configuration tables | medium | logical | Every source block goes first to the loaded plugins' reader rules, in load order (D317). A block no rule claims is read as today: tables, then the family's words. Without a plugin nothing changes. D40's order stays the order of … |
| D235 | Run statistics of PROGRAM_END and the figures of TOOL_END | medium | manual + logical | PROGRAM_END and TOOL_END carry the same figures: blocks (executed), feedDistance, rapidDistance and unknownLengthMotions (the count of MOTIONs whose length is unknown, so a report can call the distances a lower bound). An … |
| D240 | Position variables of a diameter-programmed X axis | medium | manual (Siemens), web inconclusive (Fanuc) | Siemens: $AA_IW[X] reads a diameter under DIAMON, so it maps one to one. $AA_IM[X] reads a radius by default (MD20360 bits 6/7 off), so it gets factor 2 in the reader and 1/2 in the compiler under DIAMETER=ON, after one check on … |
| D244 | Fanuc G52 across G54..G59 and G92, incremental words, under … | high | manual | (a) G52 holds in all of G54..G59. FanucChain keeps Local through ORIGIN, and after ORIGIN the reader writes the active G52 SHIFT again. (b) `G52 U W` is measured from the active G54..G59 zero, like X/Z, not added to the active … |
| D245 | Fanuc G68 without a centre, and a G68 or G68.2 while one is active | high | manual + web | (a) G68 R without X Y turns about the tool position. The reader writes ROTATE only where it knows the tool stands at X0 Y0 of the plane. Otherwise the block stays RAW with a WARNING, as does a centre away from the origin. A … |
| D249 | Fanuc M98 P with more than four digits on a 30i | medium | manual | No `program_number_digits` key. The reader keeps main's 30i-format rule. M98 P with more than four digits and no L means the leading digits are the count and the last four are the program (P51002 = O1002 five times). M98 P L … |
| D253 | Replacing a Klartext transform that others follow; cycle 247 over … | medium | manual + logical | (a) HeidenhainChain.TryReplace treats cycles 7, 8, 10, 11, 26 and 19 (and absolute PLANE) like cycle 7: a new definition of the same kind replaces the old one. Where the replaced entry is the last in the chain, write RESET plus … |
| D257 | O number of a program without NUMBER | high | manual + logical | Without NUMBER, a NAME of digits is the O number (limit per D249). Otherwise the compiler takes the lowest number from 1 that the output file does not use, skipping 8000-9999 (builder-reserved), with a WARNING that names the … |
| D266 | File names under file_per_program | high | manual + logical | Keep main's names. A program without NAME takes the NCX file stem. Names are compared case-insensitively, and a taken name gets _2, _3 in file order with a WARNING. BEGIN/END PGM carry the file's final name. Align the Siemens … |
| D267 | auto_preload at the last change and in called subprograms | high | manual + web | (a) At the last change, {next} takes the program's first tool. This is the builder's own example (G340 T0303. A01.) and the common CAM post convention. Where the first tool is the tool being changed in (a one-tool program), still … |
| D269 | ROT of a tilt in the [transform] templates | medium | manual + logical | NCX keeps its defined meaning for ROT: absent means TABLE (language 4.2 unchanged), so a program means the same on every control. The Heidenhain compiler always writes the transformation type explicitly, COORD ROT or TABLE ROT … |
| D271 | LBL numbers the Klartext compiler writes | high | manual + logical | The compiler numbers the labels of each Klartext program. A numeric LABEL or SUB name that is unique, not 0 and within the target's range keeps its number. Every other one (a duplicate, 0, an identifier, a string, or a number out … |
| D278 | Fanuc G70..G76: where the CONTOUR section stands and which N numbers … | high | manual | Use the audit-corrected manual answer. The section's blocks are written inline, directly after the first G70..G73 block of the program that names them, and never as an O program. A later G70 repeats only P and Q. A second PROGRAM … |
| D279 | Cycle words and native parameters of a CYCLE:<controller>=n block | high | manual | Use the recommendation with the audit-corrected manual refinements. Fanuc G74/G76/G87/G88/G89 map CLEARANCE to R, DEPTH to Z, CYCLE_F to F, CYCLE_DWELL to P, and CYCLE_RETRACT to G98/G99. PECK maps to Q only on G73/G83, and on … |
| D280 | Counter variable of a Fanuc REPEAT | high | manual | Add `[variables] repeat_counters = ["#199", "#198"]` (one per REPEAT nesting depth) to machine-config 7. The compiler writes the count from TIMES, the IF GOTO and the D213 clearing. It reports an ERROR when the key is missing, … |
| D282 | The settings of Fanuc writer rule 1 | medium | manual | (a) The motion code follows the one [format] key of D299 (`motion_code`). The Fanuc default is "arcs": G0/G1 on change and G2/G3 on every arc block, which is main's output. (b) No key. G90 in the header is required, since 3402#3 … |
| D283 | Fanuc G20/G21 when UNITS equals units_default | high | manual + web | The Fanuc compiler always writes the units code (G20/G21) in a block of its own at the start of each program, before any coordinate-system or offset setup, whatever units_default says. units_default stays a reader-side assumption … |
| D286 | Where the path comes to rest under path_mode = "continuous" | medium | manual | Since D349/D350 (settled 2026-10-07), the rest points apply while the programmed PATH_MODE state is CONTINUOUS. Under EXACT_STOP every block rests. The path rests at: DWELL, a spindle start or stop, TOOL_BEGIN/END, STOP, program … |
| D290 | Units of a rotary axis's rapid, max_feed and acceleration in the … | medium | manual | Machine-config 4 names the units per axis kind: mm/min and mm/s^2 for linear axes, deg/min and deg/s^2 for rotary axes, in every plane. Under CYLINDER=r the rotary values convert at r (1 deg = pi*r/180 mm). Under POLAR=ON the … |
| D291 | Siemens lines without an NC action: GROUP_END and the DEFINE line | high | manual | GROUP_END(...) and GROUP_ADDEND(...) become a comment-only line holding the source text (trivia, D92); the DEFINE ... AS line stays the comment-only line ReadDefine writes. GROUP_BEGIN(<_LEVEL>,<_NAME>,<_SP>,<_MODE>,...) is a … |
| D292 | G70/G71 against G700/G710 for UNITS | high | manual | Derive the pair as recommended, but on two new machine-config keys for Siemens: `base_units` ("mm" \| "inch", MD10240; default = units_default) and `extended_units` (bool, MD10260 = 1, G700/G710 available; default false). … |
| D299 | G0 to G3 on every motion block | medium | manual | One [format] key, read by every compiler: `motion_code = "change" \| "arcs" \| "every_block"`. "change" writes the verb only on change, arcs included. "arcs" writes G0/G1 on change and G2/G3 on every arc block. "every_block" … |
| D304 | Channels at one mark with different WITH | high | manual | Keep the rule that the channels waiting at one mark must name the same participants, but report a mismatch with an ERROR of its own (next free VM57x code, "channels at mark m name different participants: ch1 WITH=1,2; ch2 … |
| D306 | WITH, WAIT_CHANNEL or START_CHANNEL naming a channel the job does not … | medium | manual | Split by the machine. A channel number not in [machine] channels stays the ERROR VM851 (the control alarms too: PS0161). A channel the machine has but the job does not run is a WARNING (next free VM85x code, naming the channel … |
| D307 | CHANNEL of the header against the channel of the job manifest | medium | manual + logical | The manifest decides the channel. A header CHANNEL that differs from it is an ERROR (VM853 raised to ERROR), because the reader resolved T words and channel-bound functions for the header's channel (D219, D56). A header without … |
| D309 | Which channels start with a job, and START_CHANNEL of a running or … | medium | manual | Keep the code: a channel named by a START_CHANNEL anywhere in the job's files waits to be started; every other channel starts in the first round; the started channel runs the program NAME selects, else its [[channel]] program. A … |
| D315 | Mark of the SYNC the job compiler generates | medium | manual | Keep main's rule (first mark of the range of all the job's channels that no channel program uses and that is not start_mark), plus an optional [sync] key `reserved_marks` (list of integers, default empty) that the pick also … |
| D316 | A subprogram that no channel program of a job calls | medium | logical | Do not drop it and do not put it in the first channel's file. The job compile writes every SUB that no channel program calls once into a separate library file of the job (Fanuc: one O program per SUB, as in a file_per_program … |
| D323 | What a spindle start, stop or reversal adds to the runtime … | medium | logical | Where rpm_max and both speeds are known, add accel_time x \|rpm_new - rpm_old\| / rpm_max for a start, a stop and a speed change while turning; a reversal is a stop plus a start, accel_time x (rpm_old + rpm_new) / rpm_max. Where … |
| D325 | Head or table: rotary axes of the tool-vector convention | medium | manual | Every rotary axis should name an owner. owner = a work spindle or table that holds the workpiece: table axis, counted only while its owner is the workpiece holder (as coded). owner = a tool holder (turret, tool carrier, swivel … |
| D333 | Replacing frame instructions against a CYCLE800 swivel | high | manual | CYCLE800 writes system frames, not the programmable frame. Reader: TRANS, ROT, SCALE, MIRROR cut only the programmable entries (SHIFT, ROTATE, SCALE, MIRROR) and the tilt survives; the reader writes their RESETs where all of them … |
| D339 | M codes per Fanuc block | high | manual | Add [format] keys `m_codes_per_block` (1 to 5; default 1, which every control accepts; 3 needs parameter 3404#7 M3B, 5 needs 11630#5 M5B on 30i-B) and `m_codes_combinable` (optional list; when set, only those codes may be the … |
| D373 | How the machine file maps a tool and a point to the control's … | medium | manual | Accept `[tool_points]` with `form = "indexed_tool" \| "edge" \| "registers"`, CMP016 (INFO, the machine has no form), CMP123/CMP341/CMP514 (ERROR, a point the form cannot map), CFG010 (loader), and `point_change = "call"`, with … |
| D379 | follow-up: SLOT on SINUMERIK 840D sl is SLOT1 "Längsnut" (a new D … | medium | manual | Peter's answer to D162 stands: one name means one cycle with one word set. The follow-up corrects only the fact his answer assumed. On the 840D sl (Operate cycles), `SLOT1` is "Längsnut", a single straight slot. Its `NUM` must be … |
| D381 | RET, plain or parameterized, in a SINUMERIK subprogram declared PROC … | medium | manual | RET is allowed in a `SAVE` subprogram, plain and parameterized. The compiler writes `RET("<end label>",,$P_STACK)` for `JUMP=END` in a `SAVE` subprogram exactly as in any other subprogram, and drops the ERROR CMP597 path from … |
| D385 | M3/M4/M5 between M29 S and G84 | medium | manual + web | Treat `M3`, `M4`, `M5` and `M19` (spindle orientation) between the M29 S line and the G84 like the blocks of Q3. The walk ends unseen and the cycle stays `RAW` with a reason that names the spindle code ("a spindle command between … |
| D386 | `M29 S500` / `G0 X10. G84 ...`: group-01 code in the G84 block | high | manual | A block with a group-01 code (`G0`..`G3`) and a drilling canned-cycle code is not a cycle. The control executes the group-01 code, and the cycle is cancelled as by `G80`. The reader keeps the block `RAW` with a WARNING whose … |
| D387 | Literal F in a Siemens MCALL position block (`MCALL CYCLE81(...)` … | high | manual | The F is both the cycle's feed and the path feed. It is the modal F at the moment the modal cycle runs, so `CYCLE81`..`CYCLE83` drill this hole and every later hole of the modal range with it, and it is also the modal feed of the … |

## Decisions that follow the recommendation

| D | Question | Confidence | Basis |
|---|---|---|---|
| D108 | InvariantGlobalization silences CA1305 | high | logical |
| D109 | Enforcing the 120-character line | high | logical |
| D110 | Rendering a diagnostic without a file | high | logical |
| D111 | Byte order mark in an NCX file | medium | manual |
| D112 | Canonical text inside braces: NOT, parentheses, case | high | manual |
| D113 | SPINDLE_MODE without a role address | high | manual |
| D114 | Parameter names of a catalog cycle in the parser | medium | manual |
| D116 | Words whose Scope column names a partner | high | manual |
| D123 | ORIGIN and positions known in the workpiece frame | high | manual |
| D124 | MIRROR=OFF as the reset form of the mirror | high | manual |
| D125 | X of SHIFT under DIAMETER=ON | medium | manual |
| D126 | Frame of the rotary positions after TILT_AXIS with MOVE=TURN or MOVE | high | manual + logical |
| D128 | Role TOOL names the tool spindle, not the holder | high | manual |
| D132 | Preload after TOOL=k with another tool preloaded | high | manual |
| D134 | Start state of a later program of the file in STATIC mode | medium | logical |
| D135 | D99 entry state when the first program has no verb | medium | logical |
| D136 | CALL with TIMES from an expression in STATIC mode | high | manual |
| D137 | X values of home, limits and [positions] on a diameter axis | medium | manual |
| D138 | Required keys of the machine file and the job manifest | high | manual |
| D139 | Machine key for the arc tolerance (D36) | high | manual |
| D140 | Override of a catalog entry: key by key | high | logical |
| D141 | Severities of the cycle catalog checks | high | manual |
| D142 | Anchoring trivia in a reader's program | medium | logical |
| D143 | Canonical rank of CENTER:C and CENTER:IC | high | manual |
| D149 | An ARG named outside V1..V33 | high | manual |
| D150 | Placeholder of the preload template | high | manual |
| D152 | Placeholder names outside the introduction's list | high | manual |
| D155 | A native code that several states write | medium | manual |
| D156 | File names of the shipped cycle catalogs | high | logical |
| D158 | Fanuc SURFACE from R: a clearance key | high | manual |
| D166 | What a column is in the canonical layout | high | logical |
| D167 | Canonical rank of @SAVE and @RESTORE | high | manual |
| D172 | Templates for named tools | high | manual |
| D179 | Sub spindle C axis address on the NTJX and the Puma 2600SY | high | manual |
| D183 | IX on an axis known only in the MACHINE frame | high | manual |
| D186 | ARC with both CENTER and R | high | manual |
| D188 | Cycle planes without a known value | high | manual |
| D192 | F against max_feed (VM441 and the clamp) | medium | manual + logical |
| D195 | Unreachable blocks inside a subprogram | high | logical |
| D196 | Single-channel job without a job manifest | high | manual |
| D197 | Variables that STATE_CHANGE and trace report | medium | logical |
| D199 | Order of the blocks of several rules and rewriters on one block | high | manual |
| D200 | The line of a generated block in a diagnostic | medium | manual |
| D201 | SKIP of the origin on its generated blocks | high | manual |
| D202 | State variables that @SAVE and @RESTORE may name | medium | logical |
| D203 | Which targets limits = "clamp" rewrites | high | manual |
| D205 | Output of trace, annotate, convert and compile after an ERROR | high | logical |
| D209 | Where convert writes its NCX text | high | logical |
| D215 | File name in CALL="name" | high | manual |
| D216 | $SYS_POS_X and $SYS_MPOS_X under DIAMETER=ON | medium | manual |
| D217 | What the two readings of 2.5D_FRAESEN must share (M6) | high | manual |
| D218 | Where the F of a Fanuc cycle block stands as the program feed | high | manual |
| D224 | CYCLE=OFF after a one-shot cycle | high | manual |
| D225 | Reading a subprogram whose blocks depend on its callers | high | manual (premise) + logical |
| D229 | Encoding of a controller program | high | manual + logical |
| D232 | How a reader rule claims a sequence of source blocks | high | logical |
| D233 | TOOL_END for the tool still in the spindle at PROGRAM_END | high | manual (supporting) + logical |
| D234 | CYCLE_CALL under ExpandCycles: with or instead of the MOTION events | high | logical |
| D236 | Holder a block counts under when a channel has several holders | high | manual (supporting) + logical |
| D237 | Form of the trace table and the annotate comments | high | logical |
| D238 | Plugin assemblies and plugin settings in ncx.toml | high | logical |
| D239 | Klartext TOOL CALL by name | high | manual |
| D248 | R of a Fanuc lathe drilling cycle | high | manual |
| D250 | System variables and G10 of the Nakamura pair | medium | manual |
| D258 | CSS codes of a spindle whose table has no VC or CSS_OFF | high | manual |
| D260 | Siemens G75 fixed points against HOME POINT | medium | manual + logical |
| D263 | Defaults and limits of the [format] keys | high | manual |
| D264 | Where a subprogram that no program calls is compiled | high | manual |
| D270 | Name after CYCL DEF n in compiled Klartext | high | manual |
| D272 | A RETURN before the end of a Klartext subprogram, and a RETURN under IF | high | manual |
| D274 | Subprograms as separate .h files | high | manual |
| D276 | The Fanuc output-habit keys of [format] | high | manual |
| D277 | Block numbers of Fanuc labels | high | manual |
| D284 | Which files a batch converts, and whether the plugins read along | high | logical |
| D285 | Form of the batch report and the key of a RAW block | high | logical |
| D294 | Control number of a spindle whose templates carry none | high | manual + logical |
| D295 | Siemens structure whose condition has no NCX form | high | manual |
| D297 | A lone Siemens T0 on a machine that changes with M6 | high | manual |
| D302 | Layout of the reports of ncx analyze --job | high | logical |
| D303 | Where the machine file of a job manifest is looked for | high | manual (analogy) + logical |
| D305 | A channel that waits for a finished or never-started channel | medium | logical (manual-informed) |
| D308 | Resource state shared by the channels of a job | medium | logical |
| D310 | Names of the output files of a job's channels | medium | manual |
| D311 | A channel-bound word in a subprogram | high | logical |
| D313 | Where a moved word stands in the owning channel's program | high | manual (supporting) |
| D314 | start_mark when the channel programs synchronize at it later | high | manual |
| D317 | Load order of the plugins and where a listed DLL is found | high | logical (manual analogy) |
| D318 | Block count of the plugin INFO line | high | logical |
| D319 | Setpos shift after a RESET of a turning entry before it | medium | manual (partly) + logical |
| D320 | Heidenhain SETPOS of an axis known only in the MACHINE frame | high | manual |
| D324 | Where a user gives the histogram bins of segments and tool vectors | high | logical |
| D328 | Expansion-rule blocks of a word the job compiler moves or duplicates | high | manual (supporting) |
| D329 | Mark range of a SYNC outside the [sync] groups | high | manual |
| D330 | Assemblies a plugin brings, and a DLL of plugins/ without a plugin class | high | logical |
| D331 | What the plugin commands write on the standard output | high | logical |
| D332 | Which plugin ncx plugin build and test take without a folder | high | logical |
| D338 | A formula where Klartext takes a number | medium | manual + web |
| D372 | The words for a tool's measurement point | high | logical |
| D374 | What the virtual machine does with the tip offset and the register radius | high | logical |
| D375 | What a compiler writes for a measurement point, and what it must not write | high | manual |
| D376 | The measurement points of a tool in the tool table of D10 | high | logical |
| D377 | A channel without rigid tapping (corrects D361's lower-path M429 table) | high | manual |
| D378 | The plane REAM feeds out to, per machine (corrects D190's change 2 for REAM) | high | manual |
| D380 | Correction: the `_AMODE` ones digit of CYCLE830 | high | manual |
| D382 | Rounding of SINUMERIK DIV with a negative operand (row 2, RDR506) | high | logical (web and manuals checked, nothing found) |
| D383 | M6, T, G43/G44/G49 between the Fanuc M29 S line and G84 (row 3) | medium | manual + logical |
| D384 | `G0 Z10. M29 S500` and then G84: does the M29 in a block with a group-01 code hold? (row … | medium | logical |
