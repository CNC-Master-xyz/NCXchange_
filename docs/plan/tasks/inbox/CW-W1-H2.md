# CW-W1-H2 Tool measurement points, follow-up to main's cam-tool-point

Phase: code wave 2026-10-27, wave 1 | Depends on: main 4d6c1e3 (cam-tool-point, D372-D376, D389-D393) | Size: S

## Goal

Main carries the tool measurement points from the cam-tool-point branch, and main's version stays (maintainer, 2026-10-27). W1-H2 ports from branch w1-h only what main lacks, written against main's code, names and codes.

## Done when

- The point words take effect with the next tool-axis motion (language 4.4, D372) for the tip, the reported radius, VM484 and the tool list (`tests/Ncx.Core.Tests/VirtualMachine/TipDerivationTests.cs`).
- The tool-table wear is converted from `units_default` to the program's `UNITS` (D376) (same file).
- VM484 says no tip is derived while the tool axis is turned.
- The tool list prints the points in effect, `?` for an UNKNOWN value (`tests/Ncx.Acceptance/Analytics/ToolListAnalyticTests.cs`).
- A `[[tool_point]]` tool of the wrong type is CFG003 alone (`tests/Ncx.Config.Tests/ToolPointsLoaderTests.cs`).

## Log

Claude (agent), 2026-10-27, branch `w1-h2` from `main` at 4d6c1e3.

- Tests first: each new test fails on main (the tip shows at the block that sets `TIP_OFFSET`; the wear is added unconverted; the VM484 text says "in this program"; the tool list prints no points; `tool = 1.5` gives CFG003 and CFG010).
- `HolderState` and `HolderSnapshot` keep the point, `TIP_OFFSET` and `POINT_RADIUS` in effect (`ActiveToolPoint`, `ActiveTipOffset`, `ActivePointRadius`, null while UNKNOWN). `TipRules.Activate`, one call line after step 5 in `VirtualMachine.cs`, takes them over at a `RETRACT`, a `CYCLE_CALL` or a motion with a word on the tool axis. The compilers keep reading the values set (D393), because they write the registers in the block of the point word.
- The trace keeps main's form `TIP:Z <programmed> -> <tip>` and writes it at motion blocks only.
- VM484 now also warns for an UNKNOWN `TIP_OFFSET` in effect, written `TIP_OFFSET=?`.
- `ncx check`, `trace` and `analyze` pass the tool table next to the machine file to the VM (`Pipeline.cs`, `VmOptions.MeasuredPoint`), as the compiler's own run already does; the wear only shows in the MOTION events that plugin listeners read.
- Not ported, because main settles it differently: tools by name in `[[tool_point]]` (main: numbers); `[tool_points]` without a `form` (main: `form` is required); W1-H's own type and code names.
- Two fixes to main's code, asked by the orchestrator after the comparison: the shared-pool CFG010 of `CheckSharedRegisters` stands on the `radius` line of the row that reuses the register, not on line 1 (`ToolPointsLoaderTests`); VM484 is suppressed inside a subprogram that no program calls (D99), like the other tool rules, and a suppressed warning does not use up the once per program (`TipDerivationTests`, `DiagnosticTableTests`, generated `diagnostics.md`). Both new tests fail on the code before the fix. `PipelineTests` checks that the wear of the tool table next to the machine file reaches the MOTION event on the path of check, trace and analyze.
