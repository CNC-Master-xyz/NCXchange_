# CW-W3-3 Analytics

Phase: code wave 3 | Package: W3-3 (`.worktrees/codewave/PLAN.md` section 5) | Depends on: W1-F (D286 key), W1-G (events, D357), W1-I (PATH_MODE state) | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The ten decisions of W3-3 in the analytics and `ncx analyze`: the report digits (D121, N13), the rest points of the runtime estimate (D286), the units of a rotary axis in the polar and cylinder plane (D290), the layout of `analyze --job` (D302), the spindle ramp (D323), the histogram bins on the command line (D324), the owner convention of the tool vector (D325), the PATH_MODE state in the estimate (D350), the path length of an arc in the comments and the segment report (D353) and the report line of a defaulted `[cycles]` key (D357).

## Scope

- Analytics: `src/Ncx.Analytics/` except `ToolList/ToolUse.cs`; new `Runtime/AxisLimits.cs`, `ArcSense.cs`, `PathDirection.cs`, `PolarApproach.cs`, `SpindleRamp.cs`.
- CLI: `Commands/AnalyzeCommand.cs`, `AnalyzeSettings.cs`, `AnalyzeJob.cs`.
- Tests: `tests/Ncx.Acceptance/Analytics/`, `tests/Ncx.Acceptance/Cli/AnalyzeCommandTests.cs`; the analytics references of 3D_FRAESEN re-accepted.
- Spec and plan: VM 8; machine-config 4, 5; architecture 9 (the analytics table), 10, risks; implementation 14 P4-02, P4-03; 03-open-questions rows 87, 89, 95, 96, 97, 98, 152, 203, 207.

## References

- `docs/decisions/decisions.md` rows D121, D286, D290, D302, D323, D324, D325, D350, D353, D357, D388.
- `.worktrees/codewave/PLAN.md` sections 2, 5 (W3-3), 6, 7; the wave-3 notes of WAVE-BRIEF.md and gaps/W1-C.md.

## Done when

- Each decision is on the branch with its spec sentences, its markers turned into comments and its open-question rows settled.
- `dotnet build NCXchange.sln -warnaserror`, `dotnet test NCXchange.sln --filter "FullyQualifiedName!~TakesWellUnderASecond"` and `dotnet format NCXchange.sln --verify-no-changes` are green.

## Log

- Claude (agent), 2026-10-27, branch `w3-3`:
  - D324: `ncx analyze --segment-bins` (mm) and `--vector-bins` (degrees) take comma-separated rising bounds greater than 0; another list (not rising, 0 or less, not a number, empty) is a usage error, CLI001, exit 2. `AnalyticOptions` carries the bounds, and the two analytics take them in their one-argument constructor, so `Program.cs` keeps its registration lines. Architecture 10 and P4-03 list both options. The markers of `SegmentAnalytic` and `ToolVectorAnalytic` become comments. Tests first: the bins test and the six usage-error cases failed on main (unknown option).
  - D353: the class comment, the report header and the README of the segment length say that the length of an ARC is the arc length with the travel of a helix; a new acceptance test measures a helix (it failed on main on the header text only, since the VM measured helices already). The header line of the two 3D_FRAESEN segment references changes with it.
  - D302: the marker of `AnalyzeJob` becomes the D302 comment; a new CSV test shows the heading of each channel as a one-field row (it passed on main, which already behaved so). The job time after the last channel stays with P6-03, as the existing TODO in the runtime report says.
  - D121 (N13): `ReportText` and `ToolVectorAnalytic` no longer name the removed VM constants (`MotionRules.PositionDecimals`, `MotionEvents.SweepDecimals`). The reports keep three decimals for distances and angles; the VM keeps six (`Vec3.VmDecimals`).
  - D325: a rotary axis owned by a tool spindle is the spindle's own rotation and turns no tool vector; an axis without owner stays a head axis, and the report header carries a WARNING naming it; a header line says what each kind of owner turns. The B head of the test mill is owned by its tool holder H1 (it was owned by the tool spindle, which now turns nothing). The B1 owners of mori-ntx1000-mapps.toml and dmg-ctx-840d.toml and machine-config 4 were already on main (W1-C). The two new tests failed on main; the two 3D_FRAESEN vector references gain the owner line.
  - D350: `MachineDynamics.Continuous` goes; `CornerSpeed` reads `PATH_MODE` from the `After` of the motion before the corner. The profile line names `path_mode` as the start value; where the program changes the mode the report counts the corners timed in each, and where the machine has no `[transform]` template for a mode other than the start value it counts the corners timed in a mode it cannot express.
  - D286: under CONTINUOUS the path rests at SYNC events, at a cycle call not timed, at CYCLE_DWELL, at a direction reversal and at every block without path motion that raises FUNCTION, PRELOAD or STATE_CHANGE (a block whose verb is RAPID, LINE, ARC, RETRACT, HOME or CYCLE_CALL is not one); VAR_CHANGE, JUMP, CALL, RETURN, REPEAT, SUB and SECTION events are transparent. The end of a RAPID rests on a Fanuc-family machine, `[dynamics] rapid_exact_stop` overriding the family default. The report names the rules. The marker of `RuntimeEstimator` becomes the D286 comment.
  - D290: `MovingAxes` converts the deg/min and deg/s^2 of a rotary axis at the CYLINDER radius or at L (`PolarApproach`: the segment's perpendicular distance from the centre or the nearer end; for an arc the circle's distance where the arc passes the nearest point, else the nearer end). In the polar plane the X limits apply as well; at L = 0 the X limits alone and the line "C clamp near centre not estimated". A RAPID takes the rapids of the linear axes. The rotary-axis line of the report names the units and the conversion.
  - D323: `SpindleRamp` charges the linear ramp to `rpm_max` (from the spindle table) for a start, a stop, a speed change while turning and a reversal, where `rpm_max` and the speeds are known; the whole `accel_time` per start and stop otherwise, a reversal twice, nothing for a speed change. A block's SPINDLE: and RPM: changes of one spindle are charged once. The spindle table of the report gains `rpm_max`.
  - D357: when an expanded cycle in the range pecks and the machine file leaves out the `[cycles]` key whose family behaviour is 0 (Fanuc and no family: both keys; Heidenhain: `chip_break_lift`), the runtime report names the key in a line, no diagnostic.
  - Old question numbers in `RuntimeEstimator` replaced by their decisions (wave-2 #27 -> D234, #11 -> D190; HOME as a MOTION cites VM 7 and D287).
  - Tests first, failing on main: 12 of the new runtime tests (PATH_MODE both ways, rest points at COOLANT, reversal, RAPID end on Fanuc and with rapid_exact_stop, report rest line, polar and cylinder conversion, through the centre, D357 line, the linear spindle ramp) and the changed profile-line assertion; the transparent-corner, Siemens/Fanuc-false RAPID, no-rpm_max and CSS cases passed on main and guard the old behaviour. The 3D_FRAESEN runtime reference is re-accepted: 191.5 s becomes 191.6 s (the rests at RAPID ends and state blocks), with the new report lines.
  - Gaps closed 2026-10-27 (delegated), each in the form of its neighbours:
    - D121 (N13): the reports keep three decimals for distances and angles and one for seconds; D121 fixes the VM precision, not the report's.
    - D286: a reversal is a cosine below -0.999999 between the direction of arrival and of departure (linear axes, and a rotary axis in the polar or cylinder plane; an arc by its tangent, a half or whole turn whose sense its geometry leaves open is no reversal). A block that raises no event (a state word repeating its value, a SYNC in a run of one channel) is invisible to the estimate and counts as a corner. RETURN, REPEAT, SUB_BEGIN/END and SECTION count as transparent with CALL, JUMP and comments; PRELOAD is a machine-function block.
    - D290: the corners counted are the junctions between two timed motions in the range, by the mode of the motion before; under POLAR the X limits apply to every motion of the face plane, since a face motion moves the machine X and C together. The "C clamp near centre not estimated" line appears only when such a block exists.
    - D323: an rpm above `rpm_max` is capped at `rpm_max` in the ramp.
    - D357: the report line also names `chip_break_lift` on Heidenhain and both keys in a run without a machine family, where code wave 1 made the VM's default 0 as well.
  - Left: the job time after the last channel (P6-03); the TODO(question) markers of `AnalyzeCommand` (row 94, D205 not naming analyze) and `SpindleSpeed` (row 91) belong to no decision of this package.
