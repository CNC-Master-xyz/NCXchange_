# CW-W3-4 Tool measurement points in the compilers

Phase: code wave 3 | Branch: `w3-4` | Depends on: W1-H, W2-1, W2-3, W2-5, W2-9 | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

The decisions of package W3-4 of the code-wave plan (`.worktrees/codewave/PLAN.md`, section 5): D370 (CMP032), D373 (CMP016, CMP123/124, CMP341, CMP514), D375, D376, with ruling C5 of D388. Main already carries the compiler writers of the cam-tool-point work (D372-D376, D389-D393); only what is missing is implemented, and main's design wins wherever D389-D393 settle a point.

## Scope

- `src/Ncx.Compilers/CompilerBase.ToolPoints.cs`, `DiagnosticCodes.Heidenhain.cs`, the tool-point compiler tests.
- The spec sentences: machine-config 3, controller-mapping 3, heidenhain 4 and 8, siemens 12, fanuc 10.

## References

- `docs/decisions/decisions.md` rows D370, D372-D376, D388, D389-D393.
- `.worktrees/codewave/PLAN.md` sections 1 (C5), 2, 5 (W3-4) and 6.

## Done when

- Every decision of the package is implemented, with its spec sentence.
- `dotnet build -warnaserror`, `dotnet test` and `dotnet format --verify-no-changes` are green.

## Log

Claude (agent), 2026-10-27. Built on `main` at 8ed2ee4. Main compared with the package decision by decision first.

- Comparison with main. Already done there (cam-tool-point, W1-H): `ToolPointsTable`, the loader with CFG010 and `offset_memory`, `point_change`, `point_comment`, `edge_numbering`; `CompilerBase.ToolPoints.cs` with CMP016 (INFO, once per program), the family ERRORs, CMP032 against `[[tool.point]]` rows (one output step of the tool-axis address), CMP033 (D393) and the `point_comment` line; the writers in `HeidenhainToolCall.cs` (`TOOL CALL 5.1`, point change with CMP123), `SiemensTools.cs` (`D k`, flat rows, CMP514) and `FanucToolWords.cs` (`G43 H`/`D` of the mapping, `H99`/`D99`, CMP341); the shipped `[tool_points]` of fanuc-mill-30i, siemens-840dsl-mill and heidenhain-itnc530 and `machines/fanuc-mill-30i.tools.toml` (two tools, tool 5 with two points); machine-config 1 units and 3, controller-mapping 3, heidenhain 4, siemens 5, fanuc 5 (memory A/B/C and parameter 8136 bit 6). Tests for all of it in the three `*ToolPointTests` and `ToolPointsLoaderTests`.
- D373, D388 C5 (d3f4261). Missing on main: CMP123 was both the WARNING of the point change and the ERROR of k > 10. New constant `HeidenhainToolPointBeyondIndices` = CMP124 (ERROR), used by `CompilerBase.CheckMapping` for Heidenhain; CMP123 keeps only the WARNING. Machine-config 3, controller-mapping 3, heidenhain 4, open-questions row 223 text. Old code: `PointEleven_IsAnErrorCmp124` failed (CMP123 came); the new `PointTen_IsTheLastIndex` (TOOL CALL 5.9) passed.
- D375, D393 (d3f4261, c770dd9). The writer rule lists named by the plan had no point rule: heidenhain 8 rule 10, siemens 12 rule 9, fanuc 10 rule 9. Documentation only; the code already behaved so.
- Not changed: the other shipped machines (dmg-ctx-840d, millturn1, mori-ntx1000-mapps, nakamura-ntjx, doosan-puma-2600sy) keep no `[tool_points]`; no decision names their form, and without it a point other than 1 is the INFO CMP016 and writes nothing.
