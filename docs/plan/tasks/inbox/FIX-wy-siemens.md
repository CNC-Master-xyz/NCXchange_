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
