# CW-W3-5 Arcs and rounding on the output grid

Phase: code wave 3 | Package: W3-5 (`.worktrees/codewave/PLAN.md` section 5) | Depends on: W1-C, W1-E, W1-I, W2-1, W2-5, W2-9 | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

D354 (compiler half), D363 to D366 on the branch, and the arc direction under CYLINDER on Siemens TRACYL (D146: ARC under CYLINDER was CMP536 until W3-5 settles it).

## Scope

- Framework: `src/Ncx.Compilers/NumberFormatter.cs`, new `ArcOnTheGrid.cs`, `GridAxis.cs`, `GridCentre.cs`, `PlanePoint.cs`, `RoundLimitCheck.cs`; one call line in `CompilerBase.Walks.cs`; `DiagnosticCodes.cs` (CMP020 retired, CMP023, CMP030, CMP031).
- Arc writers: `Fanuc/FanucArcs.cs`, `Heidenhain/HeidenhainArcs.cs`, `Siemens/SiemensArcs.cs`; hooks in `FanucBlock.cs`, `SiemensBlock.cs` (FormatComputed), `FanucMotion.cs` (AddFeed, D354), `SiemensMotion.cs` (motion code and plane under TRACYL), `SiemensFrames.cs` (ReportCylinder), `Fanuc/DiagnosticCodes.Fanuc.cs` (CMP324), `DiagnosticCodes.Siemens.cs` (CMP536 text).
- Spec: machine-config 2; language 4.1 (ROUND_LIMIT), 4.3 (CENTER); VM 3.2; controller-mapping 2 (F); fanuc 4; heidenhain 2, 8 rule 4; siemens 3, 12 rule 2; differences (Arcs).

## References

- `docs/decisions/decisions.md` rows D146, D354, D363, D364, D365, D366, D388.
- Connector: Siemens 840D sl/828D Programmierhandbuch Arbeitsvorbereitung 2013 p.349-353 (TRACYL), Programmierhandbuch Grundlagen 2013 p.154 (G17/G18/G19).

## Done when

- Each decision is on the branch with its spec sentences; the gate is green.

## Log

- Claude (agent), 2026-10-27, branch `w3-5`:
  - D146 (Siemens TRACYL arcs): NCX turns ARC=CCW from the cylinder axis toward C (VM 3.2, `Plane.CylinderAbout`). TRACYL works in G19 (Arbeitsvorbereitung 2013, connector p.352: `N50 G19`), the plane of the 2nd and 3rd geometry axes, `G19: Arbeitsebene Y/Z, Zustellrichtung X` (Grundlagen 2013, connector p.154), so G3 turns from Y toward Z; Y is the circumference, the C word, Z the cylinder axis (Arbeitsvorbereitung p.352-353). The same arc in the same coordinates is G2 for CCW and G3 for CW. The arc is written with C as Y, a centre on C as J and on the cylinder axis as K, and the plane is G19 under TRACYL whatever WORKPLANE holds. CMP536 keeps the Y word, another rotary word and a sweep of a turn or more (TURN= needs the end point of the developed surface). Tests first: the four arc cases of `Cylinder_Arc_TurnsTheOtherWayInTheG19OfTracyl` failed on main (CMP536).
  - Not settled from the manuals: the side of G41/G42 under TRACYL. NCX gives COMP no orientation of its own in the cylinder plane, so W2-9's unflipped G41/G42 stays. Heidenhain stays CMP101 (cycle 27 needs the contour subprogram and Q parameters no decision covers, open-questions row 234).
  - D363: `NumberFormatter.Format` rounds every value half away from zero on its decimal value, silently; CMP020 keeps its constant `[Obsolete]`. `RoundLimitCheck` (called once per step from `CompilerBase.WriteStep`) measures the deviation of the written point over the linear axis words of the block against ROUND_LIMIT and each angle word against ROUND_LIMIT:ROTARY: CMP023. Gap closed 2026-10-27 (delegated): an angle word is measured on its own against ROUND_LIMIT:ROTARY with the same code; a rotary word under CYLINDER or POLAR counts as a length. Tests first: `RoundLimit_DeviationOverTheLinearAxes_IsReportedAboveTheAllowance(0.0005)` failed on main (CMP023 did not exist; the CMP020 test failed after the change, as planned).
  - D366: `WrittenDecimalsOf` gives the default of the family: angle addresses (ANGLE, ROTATE, the rotary letters of the machine and A, B, C) 4 on Heidenhain, 3 on Fanuc and Siemens; any other address the decimals of X. The Heidenhain compiler passes ANGLE for IPA. Gap closed 2026-10-27 (delegated): without X too a linear address takes 3; a Klartext cycle parameter (Q, QL, QR with a number) takes 4 (its input range, D358 "the four decimals of Q256"); a vector component (TX..NZ, A3..C5) takes 6, the VM precision of D121; the Fanuc point on whole numbers and trailing_zeros padding follow the decimals the file writes only, so L2 and Q215=0 stay as they were.
  - D364: `ArcOnTheGrid.Choose` picks the centre among the nine grid points one step around the rounded centre (absolute grid, or the grid of the offset from the written start), best agreement of the two radii, then nearest the exact centre; CMP030 above `[machine] arc_radius_tolerance` (INCH converted) or one output step. Wired into Fanuc I J K (and the turns of ANGLE), Siemens I=AC()/I J K (and TURN=), Heidenhain CC and CC IX IY. Forms without a written end point (Heidenhain CP IPA, Siemens AR= with the centre) keep the rounded centre. N310 of 2.5D_FRAESEN is now I-.533 J-19.992 / CC X+50,001 Y+50,001: listed as D364 differences in the Fanuc and Heidenhain acceptance comparisons; three unit tests re-accepted.
  - D365: `ArcOnTheGrid.SweptEnd` and `EndBeyond` compute the end the control reaches. Siemens AR= with the centre beyond ROUND_LIMIT (or half a step on one axis without it) is written in the end-point form with the centre, Siemens's lever; Heidenhain CP IPA beyond it is CMP031 (the ANGLE decimals are its one lever).
  - D354 (compiler half): under `helix_feed = "arc"` (Fanuc default) the F of an ARC that moves a linear axis outside the plane is written times plane length / path length from the MOTION event; the target holds the scaled value so the next LINE or ARC writes the NCX feed again; an F the STATIC walk does not know is written as it stands with CMP324. Two Fanuc unit tests re-accepted (the 900-degree helix writes F99.949).
  - Orchestrator rulings 2026-10-28 (delegated): (1) Gap closed 2026-10-28 (delegated): D364 searches the neighbouring grid points only when the rounded centre misses the tolerance; `ArcOnTheGrid.Choose` takes the tolerance, so CENTER:X=50 stays CC X+50 / I-.534 J-19.993, and the D364 acceptance differences and the re-accepted unit tests are back to main's output. (2) Gap closed 2026-10-28 (delegated): under TRACYL COMP=LEFT is G42 and COMP=RIGHT G41, the same orientation change as CCW to G2 (Arbeitsvorbereitung 2013 p.352; Grundlagen 2013 p.154); test `Cylinder_Compensation_IsMirroredInTheG19OfTracyl` fails on the previous commit.
  - Gate: build -warnaserror, test (without the timing tests), format --verify-no-changes green.
