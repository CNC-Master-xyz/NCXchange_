# FIX-wy-siemens WY-250L machine file, Siemens frames_in_diameter round trip

Phase: fix after code wave 3 | Branch: `fix-wy-siemens` | Size: S

## Goal

1. A machine file of its own for the Nakamura WY-250L, so that the acceptance sources `NAKAMURA_WY250L_O1000.nc` and `.P-2.nc` and the job `tests/Ncx.Acceptance/Fixtures/nakamura-wy250l.ncxjob.toml` no longer run on the NTJX file, whose `start_mark` is the NTJX mark M198 (D314, D388).
2. The Siemens reader the exact inverse of the compiler for TRANS X and `$AA_IM[X]` under every combination of DIAMETER and `[[axis]] frames_in_diameter` (D125, D240, gap notes W2-8 and W2-9).

## Done when

- `machines/nakamura-wy250l.toml` loads without ERROR and WARNING, every WY fact cited from the cnc-manuals connector; the WY tests and the job use it; it is listed wherever the shipped machines are.
- A round-trip test for each of the four combinations of DIAMETER and frames_in_diameter.
- `dotnet build NCXchange.sln -warnaserror`, `dotnet test NCXchange.sln`, `dotnet format NCXchange.sln --verify-no-changes` green.

## Log

Claude (agent), 2026-10-30. Built on `main` at 4fc3175.

### 2. Siemens frames_in_diameter

- The compiler's factors (`SiemensAxes.FrameWordFactor`, `ActualValueFactor`): TRANS X is S/2 under DIAMETER=ON without frames_in_diameter, S with it; S under DIAMETER=OFF without it, 2S with it. `$SYS_MPOS_X` is `($AA_IM[X]*2)` under ON without, `$AA_IM[X]` under ON with and OFF without, `($AA_IM[X]/2)` under OFF with. The reader handled three of the four: under DIAMOF with frames_in_diameter it took TRANS X and `$AA_IM[X]` one to one, so a radius came back doubled.
- Tests first: `SiemensFrameTests.TransX_WithFramesInDiameter_IsTheDiameterUnderDiamonAndItsHalfUnderDiamof` (ATRANS X8 under DIAMOF read SHIFT X=8, expected 4), `AaImX_UnderDiamofWithFramesInDiameter_IsTheNcxNameDoubled`, `CYCLE800_WithAnXReferencePointUnderDiamofWithFramesInDiameter_IsTheHalfShift`, and `tests/Ncx.Acceptance/SiemensFramesInDiameterRoundTrips.cs`, two theories over the four combinations on millturn1 (switchable X): TRANS X10 read, compiled back to `TRANS X10 Z5`, and the read-back NCX equal to the first reading; `R1=$AA_IM[X]` read and compiled to the reader's factor inside the compiler's (`(($AA_IM[X]*2)/2)`, `$AA_IM[X]`, `$AA_IM[X]`, `(($AA_IM[X]/2)*2)`). Red before the fix: the DIAMOF/frames_in_diameter cases (SHIFT X=10 instead of 5, `R1=($AA_IM[X]/2)`).
- Fix: `SiemensFrames.XFactor` (2 under DIAMON/DIAM90 on a diameter X axis with radius frames, the old `XInRadius`; 1/2 under DIAMOF with frames_in_diameter; else 1), used by the shift reading (new `SiemensMotion.Halved`), by `$AA_IM[X]` in `SiemensExpression` (`($SYS_MPOS_X * 2)` for 1/2, no RDR510 since the machine file states the factor) and by the `_X0`/`_X1` reference points of CYCLE800, which the compiler writes with the same frame factor (`FrameStateFactor`). Gap-closed notes in the D125 and D240 rows.
- Left as it was: on an X axis programmed in radii (a mill) the compiler halves SHIFT X under DIAMETER=ON while the reader does not double TRANS X under DIAMON; frames_in_diameter is an ERROR there (D146), so that pair lies outside the matrix of this fix.

### 1. WY-250L machine file

- Sources of every fact, all from the cnc-manuals connector: Nakamura-Tome Programming Manual 4810004EEE (2020, WY-250L folder) p.66, 68, 69 (T word, four digits, 24 stations, offset 00 cancels), p.75 and p.159-160 item 91 (M100 to M199 are the waiting M codes of the WY series), p.333 (both turret programs of a WT/WY example open with M198); WY-250L Operation Manual 4800602ECA (31i/32i-B, 2020) p.27 (L spindle 45 to 4500, R spindle 50 to 5000 min-1, rapids, both turrets 24 stations with turning and rotary tools), p.30-31 (axes X1/X2, Z1/Z2, Y1/Y2, B2, C), p.183-188 (strokes X1 160.5, Z 910, Y1 +-50, Y2 -50/+20, B2 945; rapids X 18000, Z and B2 40000, Y 10000; cutting feed 1 to 8000; C rapid index 600 min-1 and cutting feed 1 to 4800 deg/min; rotary tool 6000 min-1); WY guide 2008 (Fanuc 31i Model A) p.21 and Programmierhandbuch 2019 p.15 (header templates of both turrets: M198 "STANGEN-END ABFRAGE", then the bar change, then M199 "PRG.-START SYNCHRONISATION").
- `machines/nakamura-wy250l.toml`, identical copy in `docs/spec/examples/machines/`, written from `nakamura-ntjx.toml` with only these changes, each cited in its comment: name, dialect `31i-B`; turret 1 without ATC (`magazine = false`); `[tool_change] change = "T{tool:02}{offset:02}"`, no preload, no `auto_preload`, `unload` kept; limits, rapid and max_feed of X, Z, Y, C, B, C2; rpm_min/rpm_max of the three spindle tables; `[sync] start_mark = 199`, `reserved_marks = [198, 199]`; TCPM and TILT templates removed (they serve the NTJX B-axis head; the WY has turrets). Accelerations, `[dynamics]`, `[positions]`, every M code table, `[raw]`, `[workpiece]`, `[spindle_sync]`, `[system_variables]` and `channel_files = "path_suffix"` stay as on the NTJX: the WY manuals list the same codes for them.
- M198: the brief asked for M198 as a function (bar-end check) or RAW. The WY manuals make it a wait: every M100 to M199 is a waiting M code on the WY series (4810004EEE p.75, p.159-160), both turret programs of the template pass M198, and the bar-end check is the block after it (`/M99 P9999` in 2008, `G471...` in 2019). A `[func]` entry for M198 would collide with `mark_range`, so M198 stays `SYNC=198`, and `reserved_marks` keeps generated SYNCs off it. The WY pair holds no M198, so its reading is unaffected either way.
- Open, as a TODO(question) in the file: the lower turret drives rotary tools with the same M88/M89/M90 in its own path; a spindle table binds its codes to one role, so path 2's M88 reads as `SPINDLE:TOOL` of the upper drive, as before on the NTJX file.
- Tests first: `ExampleMachinesTests.NakamuraWy250l_SyncAndToolChange_AreThoseOfTheWyManuals` (red until the file existed); the WY file joined `s_exampleMachines`, so it loads without ERROR and WARNING, its copy is compared byte for byte (`FixtureTests`) and the built-in copy with the repository (`BuiltInFilesTests`, glob `machines/*.toml`).
- Pointed at the WY file: the job `tests/Ncx.Acceptance/Fixtures/nakamura-wy250l.ncxjob.toml` (and the vars file's comment), `NakamuraJobTests`, `NakamuraJobCompileTests`, `JobCommandTests` (the WY pair; the small synthetic jobs stay on the NTJX), `ConvertExampleTests`, `RoundTrips.ConvertAndCheck_NakamuraPathAsASingleFile_RaisesNoError`, `FanucReaderTests` (the two WY theories), `TemplateNakamuraLinesTests`. `POLAR_FACE` and the NTJX-only tests stay on the NTJX.
- Re-run and re-accepted. Converting both sources with the WY file gives byte for byte the NCX and the diagnostics of the NTJX file. Compiling the job differs in two ways, and only these: (1) the `M198` after the header of both paths is gone, because the start mark is M199 and both sources pass M199 as their first mark with both channels (D314), so `NakamuraJobCompileTests` now expects the source's wait codes alone, `M199 M110 M112 M106 M190 ... M195`, where it expected `M198` in front; (2) the tool changes of path 1 are written `T0101`, `T0656`, `T0202`, `T1212` as the source indexes the turret, where the NTJX ATC template wrote `G340 T0101. A06.`. The check and analyze outputs of the job (`JobCommandTests`) name `nakamura-wy250l.toml`; the job diagnostics are the same.
- Listings: `machines/README.md` (row and "six example machines"), machine-config 11 (six configurations, the WY entry), `ExampleMachinesTests` and `FixtureTests` comments (`FixtureTests` now counts six example machines), `expected-machine-warnings.txt` header, `tests/Ncx.Acceptance/Jobs/README.md`; D314 row gap-closed note.

### Gate

`dotnet build NCXchange.sln -warnaserror` clean; `dotnet test NCXchange.sln`: Core 2307, Config 1190, Readers 753, Compilers 884, Acceptance 665 passed (7 skipped, the corpus facts), plugins 12 passed; `dotnet format NCXchange.sln --verify-no-changes` clean.
