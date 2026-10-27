# CW-W1-B Parser, expressions, canonical writer, word-catalog hygiene

Phase: code wave 1 | Branch: `w1-b` | Depends on: nothing | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

The seventeen decisions of package W1-B of the code-wave plan (`.worktrees/codewave/PLAN.md`, section 3): D111, D112, D113, D114 (parser, catalog, VM name check), D116, D120, D124, D142, D143, D145, D166, D167, D169 (evaluator), D199, D201, D229 (docs), D237.

## Scope

- Core: `Parsing/*`, `Expressions/*`, `Writing/*`, `Model/Trivia.cs`, `NcxProgram.cs`, the parsing and expression codes; `Catalog/WordCatalog.cs`, `CanonicalOrder.cs`, `SpindleWords.cs`, the FrameWords descriptions; new `VirtualMachine/Validation/CycleParameterValidation.cs`.
- Config: `VarsFile.cs`. Trivia callers in readers, plugins and the CLI; the D237 markers.
- The spec sections each decision names, in the same commit as its code.

## References

- `docs/decisions/decisions.md` rows of the decisions above and D388.
- `.worktrees/codewave/PLAN.md` sections 2, 3 (W1-B), 6 and 7.

## Done when

- Every decision of the package is implemented or its marker settled, with its spec sentence.
- `dotnet build -warnaserror`, `dotnet test` and `dotnet format --verify-no-changes` are green.

## Log

Claude (agent), 2026-10-27. Built on `main` at 649c923, tests first for every change of behaviour; each test was run against the old code before the change (the failures are listed per decision).

- D111, D166 (9fa16c6). The parser skips a byte order mark and `NcxProgram.HasByteOrderMark` records it; `NcxWriter` writes it back, a program built by a reader has none. New `Parsing/Columns.cs` counts code points; the lexer reports PAR columns and the writer the comment column in code points. Old code: the D111 tests did not compile (no `HasByteOrderMark`); the D166 cases failed on the UTF-16 column. The CLI still strips the mark itself (left to W3-2); its FormatCommand marker is a comment now.
- D112 (96076fc). New internal `ExprPrecedence` gives every node its grammar level; `BinaryNode` and `UnaryNode` add the parentheses a code-built tree needs and no others. Old code: 10 of the 16 built-tree cases failed.
- D145, D169 (8390a5a). PAR112 (index on a non-SYS_ variable) and PAR113 (literal index that is no whole number of 0 or more) in `ExprParser.ParseVariable`; VM907 retired with `[Obsolete]`, its row kept as "Retired (D145)" (a `#pragma` around the one row that cites it). A computed index rounds half away from zero; negative after rounding or beyond `int` it is VM908. `==` and `!=` compare two strings by ordinal text; a string against UNKNOWN is UNKNOWN, against a number still VM902. Old code: the D145 tests did not compile, the D169 tests failed (5).
- D120 (e4f8983). `VarsFile` checks every key: a name `[A-Z][A-Z0-9_]*`, or a `SYS_` name with `[n]` or `[n,m,...]` of plain whole numbers, stored without leading zeros; any other key is CFG005. Old code: 8 failures.
- D113 (4ac3122). `IsAddrRequired` on SPINDLE_MODE; the readers already always write the role. Old code: the bare SPINDLE_MODE case failed.
- D116 (9f9c1e4). Value-aware partners in `BlockRules`: a partner `KEY=VALUE` counts only with that value. Old code: 19 failures; the readers' tests stayed green.
- D114 (a7d609e, b1ad266). `WordCatalog.IsCatalogCycleBlock`; the parser's third provisional form; `CycleHandlers` keeps such keys as cycle parameters; `CycleParameterValidation` checks them against `MachineConfig.CycleCatalog`: VM254 with a machine file, VM003 once per name without one. For the once-per-name rule `ResourceResolver` gained `IsFirstNotChecked(name)` (one method beside its `_warned` set). Old code: the tests did not compile.
- D142 (898f03b). `Trivia(Line, Text, BlockIndex)`; the parser and `NcxBuilder.Trivia` set the index at the call; the writer, `AnnotatedText` and `ClaimedBlocks` place by index; the expander maps the index to the first block it emits for that block (after the post blocks of a cycle that ends there). Old code: the tests did not compile. One reader test changed its output under the index rule: the line rule had put a blank line after a section's PROGRAM=END by accident. The reader's structure pass now puts the end of a section in front of the trivia that separate it from the next (`Boundary` in `StructurePass.Sections.cs`, `Steps` emits the steps before a trivia line), which keeps that output.
- D167 (0057219). PLG006 in `PluginRewriter` (the check sits where the rewriter answers are parsed, not in `PluginSet`); the FaultyRules test plugin gains `MixedPseudoWordRule`. Ranks stay 1140/1150 (D388). Old code: the test did not compile.
- D143 (c5fcba5). Marker to comment; controller-mapping CYLINDER row cites FANUC 0i-TC B-64114GE/01 p. 78 (connector page 78): under G07.1 an arc radius cannot be given with I, J or K.
- D124, D199, D201, D229, D237 (dcd6680). Markers to comments and the spec sentences; the MIRROR catalog description cites D124.
- Open-question rows set to "settled 2026-10-27": rows 32, 33, 35-39, 58, 59, 76, 77, 80-82, 84, 89 and 101 of the wave-1 table; rows 32, 34, 40, 41 (P1-06, P1-07), 91 (P3-02c) and 92 (P4-02) of the later tables.

Gaps closed 2026-10-27 (delegated):

- D114: a catalog cycle that the machine's cycle catalog has no entry for reports each of its catalog parameters as VM254 (the message says the catalog has no entry); the cycle name itself is still the compiler's check.
- D114: the WARNING without a machine file is VM003 (the D103 code), once per cycle and parameter name.
- D145: VM907 is retired (not in the plan's reservation table, which lists PAR112/PAR113 only); "has no register" is read as beyond `int`.
- D120: lowercase keys are no longer accepted in a vars file (a name follows ADDR); an index on a non-SYS_ name is CFG005, in line with D145.
- D167: the PLG006 code name is `PseudoWordNotAlone`; the check applies to every text of an answer, Replace included.

Left for other packages: the CLI's own BOM stripping (W3-2); the catalog loader's CFG159 (W1-D); Fanuc TAPER and the `FanucCycles` markers (W2-4); CMP317 (W2-3); the Fanuc/Heidenhain/Siemens string comparison of D169 (W2-3, W2-5, W2-9); the `JobCompiler.Marks` marker of D201 (W3-1). `JobChannel.Build` rebuilds the blocks without remapping trivia; no compiler reads trivia today.

Gate, 2026-10-27: `dotnet build NCXchange.sln -warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; `dotnet test` green except `FanucCompilerTests.Chain_3DFraesen_TakesWellUnderASecond`, a timing test that also failed on 649c923 before any change (with `Convert_3DFraesen_TakesWellUnderASecond` of both readers), the machine running at a load average of about 140 with the other wave branches.

