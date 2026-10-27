# CW-W1-C VM geometry: frames, motion, precision, resources

Phase: code wave 1 | Milestone: decisions of 2026-10-07 and 2026-10-27 | Depends on: none | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

Implement the VM side of D121, D123, D125, D126, D129, D132, D137, D146, D170, D183, D185, D186, D192, D203, D230, D319 and D325 (PLAN.md "W1-C"), with the spec sections each names, and the `[[axis]] frames_in_diameter` key of D388 (10).

## Scope

- Precision (D121), frames (D123, D126, D183, D230, D319), diameter values (D125, D137), resources (D129, D146, D170, D325), motion words and limits (D185, D186, D192, D203).
- The `[[axis]]` keys `parallel` (D146) and `frames_in_diameter` (D388), read by the loader; the readers and compilers that use them come in wave 2.

## References

- ncx-virtual-machine.md 2.1, 2.2, 3 step 3, 3.1-3.5, 3.8, 5; ncx-language.md 4.2, 4.3, 4.4, 4.7, 5 rule 2; machine-config.md 1, 4, 5a; architecture.md 4.2, 5.2; code-guidelines.md 2; controllers/fanuc.md 4.

## Done when

- Every decision of the package behaves as its decisions.md row says, with a test per behaviour, and the gate (build with -warnaserror, test, format) is green.

## Log

Claude (agent), 2026-10-27, branch `w1-c` on `main` at 649c923.

- D121: `Vec3.RoundToDecimal(double)` rounds every double the VM stores (arc end, centre, sweep, motion length) to `Vec3.VmDecimals` = 6, half away from zero; `MotionRules.PositionDecimals` and `MotionEvents.SweepDecimals` are gone. VM 3.2, architecture 4.2, 11 risks. Re-accepted: `2.5D_FRAESEN.events.txt`, the numbers of the analytics tests (tool list, segments, runtime, `analyze --from`). `A/ReportText.cs` is untouched (N13); its comments and the one in `ToolVectors/ToolVectorAnalytic.cs` still name `PositionDecimals`/`SweepDecimals` (W3-3).
- D123: `FrameRules.SelectOrigin` marks workpiece/polar/cylinder positions unknown when the datum changes; a repeated datum only empties the chain.
- D125: `ApplyShift` halves SHIFT X under DIAMETER=ON (`DiameterRules`). Review fix: `SiemensFrames.WriteShift` writes a TRANS rewritten from the chain entry with the state factor (`FormatComputed` and `StateFactor`, as `SiemensArcs` does), so it stays a diameter (test `D125_ShiftRewrittenFromTheChainUnderDiamon_KeepsTheDiameter`); its marker is gone.
- D126: marker in `ApplyTilt` turned into a comment; spec only.
- D129: VM500 passes while the workpiece holder's spindle runs; MILLTURN_TRANSFER loses its VM500 with and without millturn1 (expected files, P1-04 paragraph, F30 row amended).
- D132: `ApplyTool` consumes any preload; VM 3.5 row, VM 3 step 3 exception, code-guidelines 2 sample, architecture 5.2, language 4.4.
- D137: `AxisDef.AsStoredRadius` halves home, home2 and limits of a diameter or switchable axis where the VM takes them (initial position, HOME, bare RETRACT, VM442). The four builder files lose their markers; the DMG X values come from the L711 G153 program (diameters), none from machine data, so nothing was doubled. No rule of the shipped files uses `{position:NAME}`, so no requires/restore was added. The expander's literal clamp compares the word with the file's limits, both in the program's convention.
- D146: `[[axis]] parallel`; `FrameState.CylinderAxis` set at CYLINDER=n from `ResourceResolver.CylinderAxisOf`; `Plane.CylinderAbout`; the C word under CYLINDER names the workpiece holder's rotary axis; VM215 without one. Two Fanuc compiler tests on a mill without a rotary axis got `FanucCompile.TableRotaryAxis`.
- D170: created resources are `ROLE_<role>`, axes stay `C_<role>`.
- D183: `FrameRules.DiffersByShiftsAlone`; IX adds in the MACHINE frame after HOME when only shifts separate the frames; any tilt in the chain keeps VM202 (D126's example).
- D185: VM214 for vector words under CYCLE_CALL; catalog texts of TX..NZ name RAPID, LINE, ARC.
- D186: marker turned into a comment; VM 5 and language 4.3 wording; fanuc.md 4 records R over I J K (B-64484EN, cnc-manuals p. 80).
- D192: new `Validation/MaxFeedCheck` (VM441) replaces `MotionValidation.CheckFeedAboveMaxFeed`; `LimitClamp.ClampFeed` uses linear axes only.
- D203: ARC is no longer a clamped verb.
- D230: `FrameRules.Turns` no longer turns rotary axes under a tilt.
- D319: `FrameState.SetposChain`; `FrameRules.TurnSetposShifts` turns setpos shifts on a RESET of ROTATE/MIRROR, leaves them unknown after a tilt or expression angle. `HeidenhainSetpos.cs` marker stays for W2-5.
- D325: B1 of mori-ntx1000-mapps (H1) and dmg-ctx-840d (T1) carry owner (×2); machine-config 4 documents owner for head axes. The analytics WARNING is W3-3's.
- D388 (10): `[[axis]] frames_in_diameter` read and documented (machine-config 4), loader test.
- For W1-I (D351): `VirtualMachine.ResolveAxisWords` skips STOCK as it skips TILT.
- Gap closed 2026-10-27 (delegated): VM214 `VectorUnderCycleCall`, VM215 `CylinderWithoutRotaryAxis` (the codes PLAN 2.1 reserved, names in the form of their neighbours); the radial tool axis of the cylinder plane is X about Z and Z about X or Y; a removed tilt makes the setpos shifts of every linear axis unknown when a shift declared under it was not 0.
- D230 with D126 and D183, a consistent reading: D230 keeps the machine position of a rotary axis that a word moves under a tilt, while D126 and D183 decide whether IX may add in another frame; any tilt in the chain excludes that IX rule, so IC right after TILT_AXIS stays the VM202 ERROR.

Review fixes, 2026-10-27:

- D319: an axis that takes a turned component now carries the frame of the turned shift (`SetposChain`), so a second RESET turns it again (ROTATE=30, ROTATE=60, SETPOS X=0 at 10/0, two RESETs give 0/10); with a shift of its own, not 0, from another frame it becomes unknown.
- D192: `MaxFeedCheck` counts only the axes that changed and the plane axes of an ARC, not an axis named and left where it stood.
- Loader: `parallel` on a non-rotary axis and `frames_in_diameter` on an axis that is not diameter or switchable are CFG005.
- D137/D203: `LimitClamp.ClampTarget` leaves the target of a switchable axis alone, since the expander cannot see DIAMETER.
- Merge order: FrameState.cs, FrameHandlers.cs and FrameValidation.cs overlap W1-I; W1-C merges first and W1-I rebases.
