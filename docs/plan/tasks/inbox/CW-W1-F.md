# CW-W1-F Templates, function and tolerance tables

Wave: code wave 1 (2026-10-27) | Branch: `w1-f` | Depends on: none | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The machine-file side of twelve decisions: the closed placeholder list, the states of a native code, the new keys of `[tolerance]`, `[transform]`, `[tool_change]`, `[sync]`, `[dynamics]` and `[spindle_sync]`, the new `[stock]` table, and the data of the shipped machine files.

## Scope

- Decisions: D151, D152, D154, D155, D172, D269, D286, D315, D349, D351, D367, D368 (plan section W1-F; D388 for the names).
- Core: `ToleranceTable`, `TransformTable`, `ToolChangeConfig`, `SyncConfig`, `DynamicsConfig`, `MachineConfig` (`Stock`, `SpindleSyncPair`, `SpindleSyncRoles()`), new `StockTable`.
- Config: `MachineConfigLoader.Functions.cs`, `Templates/` (`Placeholder`, `TemplateSet`, `DiagnosticCodes.Templates.cs`, new `FunctionMatch`, `FunctionStateTemplate`).
- Readers: `DiagnosticCodes.cs` (RDR020, RDR021), `ReaderBase.FindFunctions`, new `FunctionContext`, `FunctionChoice`.
- Data: `machines/*.toml` and the copies in `docs/spec/examples/machines/`.

## References

- machine-config introduction, 3, 4, 5, 11; controller-mapping 1 (TOLERANCE); architecture 6, 7; fanuc 4, heidenhain 2, siemens 3.
- decisions.md rows D151 to D368 above; rationale "Answered (tenth to thirteenth rounds, 2026-10-27)"; D388.

## Done when

- Build with `-warnaserror`, the test suite and `dotnet format --verify-no-changes` are green.
- Every shipped machine file loads without a diagnostic and its templates round-trip.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-10-27, branch `w1-f`, three commits.

- f41545e, D152: the introduction is the complete list; `Placeholder.Read` reports a well-formed name outside it as the WARNING CFG104 (`TemplatePlaceholderNotListed`) on the line of the template and reads it as a number. The list includes `{rot}` (D269, words like `{move}`) and the stock placeholders of D351. A bare `{position}` without its entry is outside the list.
- 901eaa7, the keys and the states of a native code:
  - D151, D367: `[tolerance] ON_ROTARY`, `held_in`, `held_value` (millimetres, zero or greater, a negative value CFG005 like the control-held keys of D357), `held_comment`.
  - D269: `[transform] rot` with TABLE and COORD; `{rot}` is words. D349: `PATH_MODE_EXACT_STOP`, `PATH_MODE_CONTINUOUS`.
  - D172: `[tool_change] change_named`, `preload_named`. D315: `[sync] reserved_marks`. D286: `[dynamics] rapid_exact_stop` (nullable: left out means the family default).
  - D351: `[stock]` with `BOX` and `CYLINDER`, `StockTable`, the root table `stock`.
  - D154: `[spindle_sync] pair`; `MachineConfig.SpindleSyncRoles()` answers the pair or the default spindle leading the other work spindle. A pair that is not two different names, or names a role [roles] lacks, is CFG005; a missing pair where it is required (more than two work spindles, or a default spindle that is no work spindle) is CFG004. A machine without declared work spindles is not checked, so the inline test machines stay clean.
  - D155: `TemplateSet.FindFunctionsByCode` beside `FindFunctionByCode` (unchanged answer): every state that writes the code, the `[workpiece]` selections first, with the captured values and the table. `ReaderBase.FindFunctions(block, code, context)` hands them to `FunctionChoice.Choose`, which drops the states the `FunctionContext` rules out (channel, addressed spindles, OFF of a resource not on), skips states that hold, falls back to the first in file order, and reports two resources switched on as RDR020. RDR021 is defined for the family readers (ORIENT:R=0, PHASE=0). The family readers adopt both in W2-2, W2-6, W2-8.
- 9a918c8, data and specification (see the commit message); the Heidenhain and Siemens `WriteTolerance` choose `ON_ROTARY` while `TOLERANCE:ROTARY` is set, so the rotary tolerance stays written after `ON` lost `{rotary}`; CMP015 stays with W2-1.
- Tests: `MachineConfigLoaderTableKeysTests`, `FunctionChoiceTests`, new cases in `TemplateParseTests` and `TemplateSetTests`. They use the new API and codes, so on the old code they do not compile; the behaviour changes they pin (CFG104, the list, the keys, the choice) are absent there. Changed expectations: the Siemens and Heidenhain tolerance tests (ON without the rotary tolerance, ON_ROTARY with it) and `Render_HeidenhainTolerance_WritesTheCommaOfItsFormat` (renders ON_ROTARY). The compiler and reader test helpers that append a `[transform]` table drop the file's own, which the mills now have.
- Files of other packages touched, one line or key each: `MachineConfigLoader.cs` (W1-E: the root table `stock`, the `Stock` and `SpindleSyncPair` lines, the `CheckSpindleSyncPair` call), `MachineConfigLoader.Identity.cs` (W1-E: the two named keys), `MachineConfigLoader.Resources.cs` (W1-C: `rapid_exact_stop`), `DiagnosticCodes.Structure.cs` (range comment RDR010-RDR019).
- Gap closed 2026-10-27 (delegated): N7, the optional comment of D367 is the flag `[tolerance] held_comment` (false when left out); the compiler forms the comment in its family's syntax.
- Gap closed 2026-10-27 (delegated): the names CFG104 `TemplatePlaceholderNotListed`, RDR020 `FunctionCodeOfSeveralResources`, RDR021 `FunctionAngleSetByTheMachine`; the reader framework range RDR020-RDR029 holds the function-table codes.
- Gap closed 2026-10-27 (delegated): the `[stock]` keys are `BOX` and `CYLINDER`, upper case like the other template keys; the Siemens templates are `WORKPIECE(,,,"BOX",240,{max_z},{z},{z},{x},{y},{max_x},{max_y})` and `WORKPIECE(,,,"CYLINDER",192,{max_z},{z},{z},{diameter})`, bits 4 to 7 absolute (NC-Programmierung SW 4.92 p.1021-1023).
- Gap closed 2026-10-27 (delegated): the shipped 840D sl files write the SINUMERIK Operate form `CYCLE832({tol},{mode},1)` for ON, not the two-parameter form of the D151 row, because the manuals of SW 4.x make the third parameter mandatory and 1 without tool orientation (840D sl/828D Arbeitsvorbereitung 2013 p.714-716; SW 4.92 p.1126-1127); the row's own gap sentence says each file writes the form of its version.
- Gap closed 2026-10-27 (delegated): the shipped Fanuc `held_in` says that AI contour control holds no tolerance value and names the nano smoothing parameter 19581 (30i-B B-64484EN p.128-129), since D367 asks for the parameter and the manual has none for `G5.1 Q1`.
- D155 Doosan recheck: the 2600SY list documents only P11 and P12 and M34/M134 (Puma-2600SY M-Funktionen p.1-3); P21 is the MX form (MX Programmierbuch p.21). The file keeps its tables, with a comment.
- D315 data went in here although plan section 6 gives it to W3-1, because W1-F owns the `[sync]` tables in wave 1; `start_mark` (C9, D388: 198) stays for W3-1.
- Left for other packages: the writers (W2-1: CMP015, CMP008, CMP012-014, PATH_MODE), the readers (W2-2, W2-6, W2-8: FindFunctions, RDR021), WriteTilt with `{rot}` (W2-5), the estimator (W3-3), GeneratedMark (W3-1). 03-open-questions rows 199, 201 and 217 (D349, D351, D367) are shared and stay for the package that finishes them.
