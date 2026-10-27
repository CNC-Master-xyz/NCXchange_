# Cycles

The cycle catalogs per controller family in TOML, `fanuc.toml`, `fanuc-lathe.toml`, `heidenhain.toml` and `siemens.toml` (`../docs/spec/machine-config.md`, section 6); task P2-03 fills this folder. `fanuc.toml` holds the mill codes of the Fanuc family and `fanuc-lathe.toml` the lathe drilling cycles along Z and along X and the turning cycles, which a Fanuc lathe names instead of the mill file (D160). Every file is written in the numbers of Fanuc G-code system A; the readers and compilers translate them to the system of the machine through `gcode_system` (D174).

`ncx` finds the catalog that `[cycles] catalog` of a machine names in the cycle folder that `ncx.toml` names, then in `cycles/` of the working directory, then in `cycles/` of the tool's own folder, where the build copies this folder, and nowhere else: unlike `--machine`, the value is no path from the working directory, and a value with a folder in it (`shop/fanuc.toml`) is looked for below each cycle folder. It loads the catalog beneath the `[[cycle]]` entries of the machine file (P2-04, `../src/Ncx.Cli/RunMachine.cs`).

## Cycle names

Every name the shipped catalogs use, once, with its meaning and its native per family (D162). One name is one cycle with one word set in every catalog: a cycle converts between families only where both catalogs carry it, and a word the native of one family has no place for is a compiler WARNING where the value follows from another word and an ERROR where it would be lost (D362). `SINGLE_LIP_DRILL` (241) and `DEEP_HOLE` (`CYCLE830`) stay apart until their parameter lists carry the same words; joining them later is a rename by decision. Siemens natives are the 840D sl cycles of SW4.92 section 3.25.

| Name | Meaning | Fanuc (`fanuc.toml` mill, `fanuc-lathe.toml` lathe) | Heidenhain (`heidenhain.toml`) | Siemens (`siemens.toml`) |
|---|---|---|---|---|
| `DRILL` | drilling, centering | `G81`; lathe `G83`/`G87` without `Q` | 200 with `Q211`=0 | `CYCLE81` |
| `DRILL_DWELL` | drilling with a dwell at the bottom | `G82` | 200 with `Q211` other than 0 | `CYCLE82` |
| `PECK` | deep hole drilling, full retract after every peck | `G83`; lathe `G83`/`G87` with `Q` | 203 with `Q213`=0 | `CYCLE83` `VARI`=1 |
| `CHIP_BREAK` | deep hole drilling with chip breaking | `G73` | 203 with `Q213` at least the number of infeeds | `CYCLE83` `VARI`=0 |
| `TAP` | rigid tapping | `G84` with its `before` line (`M29 S`); lathe `G84`/`G88` | 207 | `CYCLE84` |
| `TAP_COMPENSATING` | tapping with a compensating chuck (D359) | `G84` without the line; lathe `G84`/`G88` | 206 | `CYCLE840` |
| `REAM` | reaming | `G85`, with a dwell `G89`; lathe `G85`/`G89` | 201 | `CYCLE85` |
| `BORE` | boring | `G86` | 202 | `CYCLE86` |
| `BACK_BORE` | back boring | | 204 | |
| `UNIVERSAL_PECK` | universal pecking | | 205 | |
| `BORE_MILL` | bore milling | | 208 | |
| `TAP_CHIP_BREAK` | tapping with chip breaking | | 209 | |
| `CENTERING` | centering | | 240 | |
| `SINGLE_LIP_DRILL` | single-lip deep hole drilling | | 241 | |
| `DEEP_HOLE` | deep hole drilling 2 with pilot and through-hole options | | | `CYCLE830` |
| `RECT_POCKET` | rectangular pocket | | 251 | |
| `CIRC_POCKET` | circular pocket | | 252 | |
| `SLOT` | one straight slot at the call position (D379) | | 253 | `SLOT1` (sl form) |
| `CIRC_SLOT` | circular slot (D379) | | 254 | `SLOT2` with `NUM` = 1 (sl form) |
| `RECT_STUD` | rectangular stud | | 256 | |
| `CIRC_STUD` | circular stud | | 257 | |
| `TURN_OD` | one longitudinal turning pass, outer or inner diameter | lathe `G90` | | |
| `THREAD_PASS` | one thread pass (D161) | lathe `G92` | | |
| `FACE` | one facing pass | lathe `G94` | | |
| `FINISH` | finishing along a contour | lathe `G70` | | |
| `ROUGH_TURN` | rough turning along a contour | lathe `G71` | | |
| `ROUGH_FACE` | rough facing along a contour | lathe `G72` | | |
| `PATTERN_REPEAT` | pattern repeating along a contour | lathe `G73` | | |
| `FACE_GROOVE` | face grooving, peck drilling along Z | lathe `G74` | | |
| `GROOVE` | outer or inner diameter grooving | lathe `G75` | | |
| `THREAD` | the whole thread with automatic infeed (D161) | lathe `G76` | | |
