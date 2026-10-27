# CW-W1-E Machine keys: `[machine]`, `[format]`, schema version, data hygiene

Phase: code wave 1 | Branch: w1-e | Depends on: none | Size: M

## Goal

The machine-file keys and machine data of the 2026-10 decisions D128, D130, D138, D139, D150, D159, D179, D250, D263, D271, D276, D278, D292, D299, D310, D339, D348, D354, D364, D366, plus D388 (9), the NTJX `start_mark` (`.worktrees/codewave/PLAN.md`, W1-E).

## Log

Claude (agent), 2026-10-27.

- D348: `ncx_schema` joins the root keys of the machine loader and the cycle catalog loader (one call-site edit in W1-D's `CycleCatalogLoader.cs`); `SchemaVersion.Check` reports CFG009 for any other whole number, naming the file and the version; a value that is no whole number is CFG003. The key stands as the first key of the eight `machines/`, the four `cycles/` and the five example copies. Machine-config 1, 6, 10.
- D130: the loader reports CFG004 when `[coolant] STANDARD` is missing (on the `[coolant]` line, or line 1 without the table). Every inline test machine got `STANDARD`; the 67 Config tests that failed right after the change are the evidence that the old loader accepted the file. Machine-config 5, language 4.6, VM 3.8 and 5 (one sentence each; VM 5 is the integrating owner's section).
- D139: `[machine] arc_tolerance` (mm) reaches `VmOptions.ArcTolerance` through `ForMachine`; `ArcRules.Tolerance` divides it by 25.4 in an INCH program (one line in W1-C's file). The INCH test failed on the old `ArcRules`.
- D159, D292, D364: `[machine] dwell_increment` (default 0.001), `base_units` ("mm" | "inch", kept as the UNITS word, `ResolvedBaseUnits` falls back to `units_default`), `extended_units`, `arc_radius_tolerance`. No reader or compiler reads them yet (W2-1, W2-4, W2-8, W2-9, W3-5).
- D263, D271, D299, D339, D354: `OutputFormat` gains `LabelMax`, `MotionCode`, `MCodesPerBlock`, `MCodesCombinable` (normalized, D105) and `HelixFeed`; `WithFamilyDefaults(Controller?)` gives the family value of every key a file leaves out. The loader keeps the file as written; the compilers switch to `WithFamilyDefaults` in W2-1. `comment_charset` takes "ASCII" or "UTF-8" (CFG005).
- D276, D310, D138, D128, D129, D150: markers turned into comments in `OutputFormat.cs`, `MachineConfigLoader*.cs`, `DefaultMachine.cs`, `VmOptions.cs` and the three mill files.
- Data: ANGLE in the decimals of the three mills (Heidenhain 4, Fanuc and Siemens 3, D366); `max_line_length = 512` on siemens-840dsl-mill.toml (D263); `helix_feed = "arc"` on fanuc-mill-30i.toml with 1403#5 HTG (B-64484EN p.2072-2073); Mori `m_codes_per_block = 3` and the 47 multiple-M-code-function codes (NTX1000SZM p.252-253); millturn1 `motion_code = "every_block"` (D299; siemens-840dsl-mill.toml stays on the family default, D388 16); the D179 C/H comments (Nakamura ECL-1 p.235, NT-Nurse p.347, Programmierhandbuch 2019 p.174-175; Doosan 2600SY M list p.2-3); Nakamura `[system_variables]` SYS_WEAR_Z and SYS_MPOS_B (D250).
- D388 (9): `start_mark = 198` on nakamura-ntjx.toml, checked with the connector (18i-TB guide 2009 p.19-20, "M198 (SYNCHRONISATION MIT *U*/*O*)"). The job compiler now writes M198 after the header of both WY-250L paths, so the M9 test and the two CLI job tests expect M198 first.
- D250 follow-on: the WY-250L path 2 now reads `$SYS_WEAR_Z[99]` and `$SYS_MPOS_B`, which an INTERPRETED job run needs; `tests/Ncx.Acceptance/Fixtures/NAKAMURA_WY250L_O1000.path2.vars.toml` gives both as 0, and NakamuraJobTests and JobCommandTests use it.
- 03-open-questions: wave-1 rows 5, 6, 8, 49, 52, 53, 54, 55, 91, 93; wave-3 rows 48, 81, 82, 173, 174; row 198 set to settled 2026-10-27.

Gaps closed 2026-10-27 (delegated):
- `base_units` takes the lower-case words of the D292 row and is held as the UNITS word MM or INCH.
- A non-positive `arc_tolerance`, `arc_radius_tolerance` or `dwell_increment`, a `label_max` outside 1..65535 and an `m_codes_per_block` outside 1..5 are CFG005, as D263's bad `comment_charset` is (D388 15).
- A missing `[coolant] STANDARD` is CFG004, the missing-key ERROR.
- The Heidenhain family value of `motion_code` is "every_block" (D299 names Fanuc and Siemens only; Klartext writes L or C on every block).

Not in this package: D278's type II key is `[cycles] contour_type_ii` under D388 (11), and `CyclesConfig.cs` and `s_cyclesKeys` are W1-G's; `[[axis]] frames_in_diameter` (D388 10) is in W1-C's `[[axis]]` reader; `[format] counter_registers` (D388 12, for D213) is left for W2-6; the machine-config 5 rewording of D314 is W3-1's.
