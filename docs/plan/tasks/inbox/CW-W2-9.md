# CW-W2-9 Siemens compiler, non-cycle

Phase: code wave 2 | Branch: `w2-9` | Depends on: W1-E, W1-F, W2-1 | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The decisions of package W2-9 of the code-wave plan (`.worktrees/codewave/PLAN.md`, section 4): D125 (c), D146 (c, TRACYL), D169 (c), D172 (Siemens name check, CMP515), D240 (c), D292 (c, CMP551), D294 (c), D333 (c, CMP535), D381 (CMP597 retired).

## Scope

- `src/Ncx.Compilers/Siemens/*` except SiemensMotion.cs (W2-1), SiemensCycles.cs (W2-7) and SiemensArcs.cs (W3-5), which get one hook line each at most; `DiagnosticCodes.Siemens.cs`.
- The Siemens compiler tests; `FrameSnapshot.CylinderAxis` (one property, for D146).
- `machines/millturn1.toml`, `machines/dmg-ctx-840d.toml` and their copies: `[transform] TILT_ON` only (D333).
- The spec sentences each decision names.

## References

- `docs/decisions/decisions.md` rows of the decisions above and D388.
- `.worktrees/codewave/PLAN.md` sections 2, 4 (W2-9), 6 and 7.

## Done when

- Every decision of the package is implemented or its marker settled, with its spec sentence.
- `dotnet build -warnaserror`, `dotnet test` and `dotnet format --verify-no-changes` are green.

## Log

Claude (agent), 2026-10-27. Built on `main` at 920315a, tests first for every change of behaviour; each test was run against the old code before the change.

- D381 (7c39201). `SiemensFlow.WriteJumpToEndInSubprogram` loses the `DeclaresSave` branch: `JUMP=END` in a SAVE subprogram is `RET("<end label>",,$P_STACK)`. CMP597 keeps its constant as `[Obsolete]`; both TODO(question) markers are comments citing the 2006 Arbeitsvorbereitung p.92 and SW4.92 p.518, p.526. The plain SAVE end stays M17 (`SiemensProgramFrame.SubEnd`). siemens.md 8; manual-audit row 1 settled. Old code: the six inverted cases of `SiemensFlowTests` (three PROC forms, three parameter forms) failed with CMP597.
- D294 (8dd95d6). Tests for SETMS(2) from the digits of the resource id and CMP511 with neither digits nor extension; both passed on the old code, which already behaved as decided. Marker turned into a comment; machine-config 5 sentence; row 147 settled.
- D172 (42ce28c). New WARNING CMP515 `SiemensToolNameNotPortable` (`SiemensTools.ReportNames`): a TOOL or PRELOAD name with a character other than an ASCII letter, digit, `_` or `-`, or longer than 31 characters (NC-Programmierung SW4.92 connector p.67; Bedienhandbuch Fräsen 2013 p.554-555). Case is kept as written. Machine-config 3, siemens 12 rule 3. Old code: the four CMP515 cases failed (no code).
- D169 (e075685). A name the file assigns a string (`VAR:NAME="TEXT"`) is declared `DEF STRING[200]` instead of `DEF REAL` (`SiemensFile.HoldsString`), so `==`/`<>` compare STRING (Arbeitsvorbereitung 2013 p.71; NC-Programmierung SW4.92 p.431: 200 characters). siemens 12 rule 7. Old code: the test failed (DEF REAL).
- D292 (54d4e0f). New `SiemensFeedUnits` (the name `SiemensUnits.cs` was taken by the file splitter): G71/G70 where UNITS equals `base_units`, G710/G700 with `extended_units`, else G71/G70 with each F converted into the basic system and the INFO CMP551 at the UNITS block. Hooks: `SiemensMotion.WriteModalWords` (the units switch) and `WriteFeed`, `SiemensCycles.WriteCycleFeed` (one line). Controller-mapping 1 UNITS row, siemens 2 group 13; row 144 settled. Old code: four of five `SiemensUnitsTests` failed.
- D125 (cd356e7). `SiemensAxes.FrameWordFactor`/`FrameStateFactor`/`FrameValueOf`: TRANS/ATRANS X is the radius under DIAMETER=ON unless `[[axis]] frames_in_diameter` (D388). The W1-C test `D125_ShiftRewrittenFromTheChainUnderDiamon_KeepsTheDiameter` is inverted to `..._KeepsTheRadius`. siemens 12 rule 4. Old code: two of the three new tests failed.
- D240 (a0ebfb0). `$AA_IM[X]` (the rendered `[system_variables]` text) is written `($AA_IM[X]*2)` under DIAMETER=ON on a diameter X axis without `frames_in_diameter`; `$AA_IW[X]` stays one to one. siemens 8. Old code: the DIAMETER=ON case failed.
- D146 (f6bb537). Under CYLINDER=n `SiemensAxes.LetterOf` writes the NCX C (A, B) word as `Y`, and on a cylinder about X or Y the cylinder axis as Z and the radial Z as X; no DC() on it. `FrameSnapshot` carries `CylinderAxis`. siemens 12 rule 4. Old code: the test failed (`G1 Z-10 C15`).
- D333 (097ed0a). `SiemensFrames`: the shifts between the last tilt and a tilt go into `{x}` `{y}` `{z}` (`_X0`/`_Y0`/`_Z0`) of the CYCLE800 and are not rewritten as TRANS; a shift after the tilt is TRANS after CYCLE800; a tilt reset rewrites the shifts that stay; ROTATE or MIRROR before a tilt is the new ERROR CMP535 `SiemensFrameBeforeTilt`; a template without the placeholder is CMP532. `TILT_ON` of millturn1 and dmg-ctx-840d (×2) take `{x},{y},{z}`. Language 4.2, controller-mapping 1 TILT, siemens 12 rule 4, machine-config 5. Old code: all seven new tests failed.

Gap closed 2026-10-27 (delegated): D172 names no character set; CMP515 allows ASCII letters, digits, `_` and `-` (the corpus writes `T="SF8-ST-NL30-SL21"`) and 31 characters, and is a WARNING (the name is written).
Gap closed 2026-10-27 (delegated): D169 names no string length; `DEF STRING[200]`, the longest string of the 840D sl (NC-Programmierung SW4.92 p.431).
Gap closed 2026-10-27 (delegated): D292's INFO is CMP551 once at the UNITS block that starts the conversion; the helper file is `SiemensFeedUnits.cs`.
Gap closed 2026-10-27 (delegated): D333's ERROR is CMP535 `SiemensFrameBeforeTilt`; a shift on an axis other than X, Y, Z before a tilt, or a template without `{x}` `{y}` `{z}`, is CMP532.

Left over: positional feed parameters of the cycle signatures (FFR and the like, W2-7's SiemensCycles) are not converted under D292; arc centers under TRACYL go through SiemensArcs (W3-5), which already uses `LetterOf`.

Claude (agent), 2026-10-27, review fixes.
- D333: a tilt with another tilt before it in the chain, with or without a shift between them, is CMP535 (a second new swivel deletes the first, Bedienhandbuch Fräsen 2013 p.483). An X shift before a tilt under DIAMETER=ON is the new WARNING CMP537 (MD20360 bit 14 unverified).
- D146: TRACYL is followed by G19 and TRAFOOF by the plane of the program (Arbeitsvorbereitung 2013 p.352); only the workpiece holder's rotary axis maps to Y; an ARC, a Y word or another rotary word under CYLINDER is the new ERROR CMP536 (the arc direction against G19 is left to W3-5). The test that pinned TRACYL without a plane is updated.
- D125, D240: `frames_in_diameter` now applies under DIAMETER=OFF too ("ohne Umrechnung", Grundfunktionen 2012 p.903): TRANS X of a radius word doubled, `$AA_IM[X]` halved.
- Still open for the merge of main: D292 on the positional cycle feeds in W2-7's `CycleFeed` (review item 3).

Gap closed 2026-10-27 (delegated): D146's CMP536 SiemensCylinderNotWritable; D333's WARNING CMP537 SiemensTiltShiftUnderDiameter.
