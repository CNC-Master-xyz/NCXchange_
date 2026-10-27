# Connector pages for the tenth round, D347 to D371

Status: 2026-10-26. A concordance only. The entries D347 to D371 in `../rationale.md` were answered by the maintainer on 2026-10-07 and are not changed by this file.

How to read it. The tenth-round drafts state their controller facts from the manual library without a page, or with a page of the old text extraction. This file gives each such fact with its document and page in the cnc-manuals connector, taken from the audit report `13-tenth-round-D347-D371.md` and, where that report gives no page, from a check in the connector on 2026-10-26. A document is named by its file name in the connector. The fact column gives the fact as the draft states it, shortened. The status says whether the page bears the fact out ("confirmed"), contradicts it ("wrong"), or whether the connector has nothing on it ("not found"). The audit found two answers that need more than a page. D361 rests on a wrong fact: on the Nakamura, M429 is the work-counter increment, not rigid tapping on the lower path, and the correcting draft is D377 in `../rationale.md`. The answer of D368 stands, but its reasoning is wrong: Fanuc does fix which way the precision levels run, and 11682 and 11683 are not the ends of that range. This is noted in section 2 of `../manual-audit-2026-10-26.md`; no draft corrects it yet. Wrong labels that leave an answer standing (D349, D357, D358) are marked in their rows.

## D347 What `ncx --version` prints

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| (none: the draft states no controller fact) | — | — | — |

## D348 The schema version of a machine file and a cycle catalog

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| (none: the draft states no controller fact) | — | — | — |

## D349 A word for exact stop and continuous path

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Fanuc `G61 G62 G63 G64` are modal group 13 | Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN.pdf; Fanuc_30i-B_Bedienung-Drehen_B-64484EN-1_EN.pdf; Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN.pdf | 27-28; 27; 66 | wrong (label): they are group 15 on M and T; group 13 is G96/G97 on M. The decision stands. |
| `G64` is the Fanuc mode the control starts in | Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN.pdf | 181 | confirmed (power-on or system clear) |
| Fanuc `G61.1` stays with `TOLERANCE` | — | — | not found: G61.1 is not in the 30i-B G-code list |
| Siemens group 10 is `G60 G64 G641`..`G645`, group 11 the non-modal `G9`, group 12 `G601`..`G603` | Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019.pdf | 1230; 297 | confirmed |
| iTNC 530 `M90` is blockwise and needs following-error mode | Heidenhain_iTNC530_Benutzerhandbuch-Klartext_NC-SW-340490_DE_2005.pdf; Heidenhain_iTNC530-HSCI_Technisches-Handbuch_NC-SW-60642x-04_DE_2014.pdf | 267; 821 | confirmed |
| The TNC 640 has dropped `M90` | Heidenhain_TNC640_Programmierung-Klartext_892903-18_NC-SW-34059x-10_DE_2019.pdf | 617-618 | confirmed ("an TNC 640 nicht erforderlich") |
| Siemens `CYCLE832` touches group 10 | — | — | not found |

## D350 Whether `[dynamics] path_mode` is the start value

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| (none beyond D349) | — | — | — |

## D351 A stock word for the blank of the graphic

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| iTNC 530: only a cuboid, sides parallel to X, Y and Z, by a MIN and a MAX point; the MAX point may be incremental | Heidenhain_iTNC530_Programmierung-Klartext_NC-SW-606420-01_DE_2010.pdf | 95-97 | confirmed |
| TNC 640: `BLK FORM CYLINDER Z R50 L105 DIST+5 RI10`, axis, radius or diameter, length, optional `DIST` and inner radius, no X/Y centre | Heidenhain_TNC640_Programmierung-Klartext_892903-18_NC-SW-34059x-10_DE_2019.pdf | 91-93 | confirmed |
| Siemens: the blank refers to the coordinate system valid at its definition, is created in the active zero offset and stays when another one is selected | Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019.pdf; Siemens_Sinumerik-840Dsl-828D_Bedienhandbuch-Fraesen_DE_2013.pdf | 1021; 211 | confirmed |
| Siemens: the datum, and on a swivelling machine a swivel to zero, stand before the blank definition | Siemens_Sinumerik-840Dsl-828D_Bedienhandbuch-Drehen_DE_2013.pdf | 479 | confirmed |
| Siemens `WORKPIECE(...)` cylinder takes its own dimension arguments, not two corners | Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019.pdf | 1021-1023 | confirmed; the page also gives the full parameter order for every shape (report 13, D351) |

## D352 Whether the readers read BLK FORM and WORKPIECE() into the stock word

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Klartext has a rotationally symmetric blank, `BLK FORM` with `DIM_D`/`DIM_R` and `LBL`; the TNC 640 cylinder takes an optional inner radius | Heidenhain_TNC640_Programmierung-Klartext_892903-18_NC-SW-34059x-10_DE_2019.pdf | 91-93 | confirmed |
| The blank is needed only for the graphic and its dialogue can be cancelled | Heidenhain_iTNC530_Programmierung-Klartext_NC-SW-606420-01_DE_2010.pdf | 95-97 | confirmed |
| `WORKPIECE(...)` has `PIPE` and `N_CORNER` forms and carries name, clamping and datum | Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019.pdf | 1021-1023 | confirmed |
| The Siemens manual writes `"BOX"` with eleven, twelve and thirteen arguments | Siemens_Sinumerik-840Dsl-828D_Bedienhandbuch-Fraesen_DE_2013.pdf; Siemens_Sinumerik-840D-sl_Programmierung_BS.000508-DE-000-00_DE.pdf; Siemens_Sinumerik-840Dsl-828D_Bedienhandbuch-Drehen_DE_2013.pdf | 211; 77; 479 | confirmed as printed, but misprints: the syntax table (SW492 p. 1021-1023) allows exactly Par5 to Par12, twelve arguments |

## D353 The path `F` feeds along, on an arc and a helix

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Heidenhain: `F` is the speed at which the tool centre moves on its path | Heidenhain_iTNC530_Benutzerhandbuch-Klartext_NC-SW-340490_DE_2005.pdf | 162 | confirmed |
| Siemens: the path feed is composed "im Regelfall" of all geometry axes taking part, referred to the cutter centre | Siemens_Sinumerik-840Dsl-828D_Programmierhandbuch-Grundlagen_DE_2013.pdf | 104 | confirmed |

## D354 The feed a Fanuc control reads on a helix (parameter 1403#5)

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Fanuc parameter 1403 bit 5 (HTG) chooses the feed along the arc tangent (0) or along the tool path (1); 0 is the case explained first | Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN.pdf; Fanuc_0i-D_Connection-Manual-Function-Vol1_B-64303EN-1-02_EN.pdf | 2072; 474 | confirmed |
| Siemens `FGROUP`: the helix example drives X/Y as path axes and Z synchronously; after `FGROUP()` the basic state comes from a machine datum | Siemens_Sinumerik-840Dsl-828D_Programmierhandbuch-Grundlagen_DE_2013.pdf | 103-106 | confirmed |
| `[format] external_call` names Fanuc parameter 6030 | Fanuc_30i-B_Parameterhandbuch_B-64490EN-05_EN.pdf | 348 | confirmed (M198 when 0) |

## D355 The plane the first peck counts from

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Siemens: the first drilling depth is `FDEP` absolute or `FDPR` relative to the reference plane | Siemens_Sinumerik840D_Programmierhandbuch-Zyklen_DE_2006.pdf (glossary); Siemens_Sinumerik-840Dsl-828D_Bedienhandbuch-Fraesen_DE_2013.pdf | 373-375; 311 | confirmed |
| Siemens: with `_DAM` = 0 every further infeed equals the first | — | — | not found: the manuals give only the sign convention of `_DAM` |
| `CYCLE83` signature with `FDEP`, `FDPR`, `_DAM` | Siemens_Sinumerik-840Dsl_Programming-Manual-NC-Programming_SW494_EN_2020.pdf | 1072 | confirmed |
| Heidenhain: `Q202` is the infeed of cycle 203 below the surface `Q203` | Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011.pdf | 87-90 | confirmed |
| Fanuc: the `Q` of `G83` and `G73` is cut from the R plane | Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN.pdf | 68-69 | confirmed |

## D356 A `PECK` or `CHIP_BREAK` without a peck depth on `CYCLE83`

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| The first depth of `CYCLE83` is given by `FDEP` or by `FDPR` | Siemens_Sinumerik-840Dsl_Programming-Manual-NC-Programming_SW494_EN_2020.pdf | 1072-1073 | confirmed |
| A call with neither gives the cycle no first depth | — | — | not found: the connector does not say what `CYCLE83` does with both empty; the bodies of the 2006 and 2008 Zyklen manuals have no readable text |

## D357 The control's cycle distances as machine keys

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Fanuc parameter 5114 is the `G73` return, 5115 the `G83` clearance | Fanuc_0i-F_Parameter-Parameter_B-64610EN_EN.pdf; Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN.pdf | 286-287; 60 and 69 | confirmed |
| Fanuc has no programmable form for these distances | Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN.pdf; Fanuc_0i-FPlus_Bedienung-Fraesen_B-64694EN-2_EN.pdf | 56-61; 40 | confirmed for the 30i-B; wrong for the 0i-F Plus, which programs the clearance with `,D` |
| Siemens: an absent `_DIS1` is computed by the cycle | Siemens_Sinumerik-840Dsl_Programming-Manual-NC-Programming_SW494_EN_2020.pdf; Siemens_Sinumerik-840Dsl-828D_Programmierhandbuch-Arbeitsvorbereitung_DE_2013.pdf | 1074; 646 | confirmed |
| Siemens: an absent `_VRT` is 1 mm | Siemens_Sinumerik-840Dsl_Programming-Manual-NC-Programming_SW494_EN_2020.pdf; Siemens_Sinumerik-840Dsl-828D_Programmierhandbuch-Arbeitsvorbereitung_DE_2013.pdf; Siemens_Sinumerik-840Dsl-828D_Bedienhandbuch-Fraesen_DE_2013.pdf | 1072; 646; 312 | confirmed in the programming manuals; the Bedienhandbuch says V2 = 0 means no retract and a one-revolution dwell, so the manuals disagree |
| Heidenhain: the peck re-approach is the cycle's own `Q200` | Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011.pdf; Heidenhain_TNC640_Zyklen_892905-18_NC-SW-34059x-10_DE_2019.pdf | 87; 97-98 | confirmed on the iTNC 530; on the TNC 640 with chip breaking the re-entry after the full retract goes to `Q256` above the last depth |

## D358 One source for a distance the control holds and the compiler writes

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| iTNC 530: the input range of `Q256` starts at 0.1 | Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011.pdf | 90 | confirmed (0.1 to 99999.9999) |
| TNC 640: the input range of `Q256` starts at 0 | Heidenhain_TNC640_Zyklen_892905-18_NC-SW-34059x-10_DE_2019.pdf | 101 | confirmed |
| `_VRT` is position 15 and `_DIS1` position 17 of `CYCLE83` | Siemens_Sinumerik-840Dsl_Programming-Manual-NC-Programming_SW494_EN_2020.pdf | 1072-1073 | confirmed |
| Siemens applies its own 1 mm for an absent `_VRT` | as D357 | as D357 | as D357 |
| Fanuc: neither `G73` nor `G83` has an address for the lift | Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN.pdf; Fanuc_0i-FPlus_Bedienung-Fraesen_B-64694EN-2_EN.pdf | 56-61; 40 | confirmed for the 30i-B; wrong for the 0i-F Plus (`,D`) |

## D359 The NCX cycle of a Fanuc G84 without its rigid-tapping line

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Fanuc enters rigid tapping in three ways: `M29 S` before the block, `M29` in the block, or G84 always rigid by parameter 5200#0 | Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN.pdf; Fanuc_30i-B_Parameterhandbuch_B-64490EN-05_EN.pdf | 103; 303 | confirmed |
| Heidenhain cycle 206 is tapping with a compensating chuck: no pitch parameter, feed `Q206` = S × p | Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011.pdf | 112 | confirmed |
| Heidenhain cycle 207 takes the pitch `Q239` and has neither `Q206` nor `Q211` | Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011.pdf | 114-115 | confirmed |
| Siemens `CYCLE840` is tapping with a compensating chuck | Siemens_Sinumerik-840Dsl_Programming-Manual-NC-Programming_SW494_EN_2020.pdf | 1116 | confirmed |

## D360 A Fanuc cycle block whose `before` line the reader cannot see

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Fanuc raises PS0203 for an S word or an axis motion between `M29` and `G84` | Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN.pdf | 104 and 108 | confirmed |
| A call block between `M29` and `G84` is not named as an alarm cause | — | — | not found: no page says whether an `M98` in between is legal |

## D361 The builder's rigid-tapping line per machine

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Mori Seiki switches rigid tapping with `M329 S_` | Mori-Seiki_NT-NTX-Serie_Programmierung_Programmieranleitung-NTX1000SZM_PX-NT_4-K0DEEN_2011.pdf | 467 and 472 | confirmed |
| Nakamura: `M29` is rigid tapping on the upper path | Nakamura-Tome_Fanuc_Programmierung_Programming-Manual-1_4810004EDG-1_EN_2015.pdf | 310-312 | confirmed; on WTW/WTS spindle rigid tapping is valid only for the upper turret (p. 312) |
| Nakamura: `M429` is rigid tapping on the lower path | Nakamura-Tome_Fanuc_Programmierung_Programming-Manual-1_4810004EDG-1_EN_2015.pdf | 79 and 148-149 | wrong, see D377: `M429` is the work-counter increment; no manual shows a lower-path rigid-tapping code |
| (added by the audit) The 30i-B sets the rigid-tapping M code in parameter 5210 | Fanuc_30i-B_Parameterhandbuch_B-64490EN-05_EN.pdf | 307 | confirmed |

## D362 A cycle word the native cycle has no parameter for

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Heidenhain cycle 207 taps from `Q239` and has no `Q206`, `Q211` or dwell | Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011.pdf | 114-115 | confirmed |
| Siemens `CYCLE840` computes the feed from pitch and speed for every `ENC` but 1 | Siemens_Sinumerik-840Dsl_Programming-Manual-NC-Programming_SW494_EN_2020.pdf | 1116 | confirmed (pitch from `MPIT`/`PIT` for `ENC` 0, 20 and 11; `ENC` 1 from the programmed feed) |

## D363 The allowance for rounding to the machine's decimals

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| (none: the draft states no controller fact) | — | — | — |

## D364 The centre of an arc whose end points the machine rounds

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Fanuc checks the radius difference against parameter 3410, alarm PS0020 | Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN.pdf; Fanuc_30i-B_Parameterhandbuch_B-64490EN-05_EN.pdf | 79-80; 196 | confirmed |
| iTNC 530 MP7431, input range 0.0001 to 0.016 mm | Heidenhain_iTNC530_Programmierung-Klartext_NC-SW-606420-01_DE_2010.pdf; Heidenhain_iTNC530_Parameter-Alarme_NC-Fehlermeldungen_DE_2003.pdf | 669; 86 | confirmed, with error 357 |
| Siemens MD21000 `CIRCLE_ERROR_CONST` and MD21010 `CIRCLE_ERROR_FACTOR`, alarm 14040 | Siemens_Sinumerik-840D-sl_Parameter_Alarme_BS.000503-DE-000-00_DE.pdf | 216 | confirmed |

## D365 End points the control computes from the written words

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Siemens: `AR=` goes with the centre `I J K` or with the end point | Siemens_Sinumerik-840Dsl-828D_Programmierhandbuch-Grundlagen_DE_2013.pdf | 201-202 | confirmed (checked 2026-10-26; not in report 13) |
| iTNC 530: a `CP` arc takes its radius from the start point and the pole `CC`, so the control computes the end | Heidenhain_iTNC530_Benutzerhandbuch-Klartext_NC-SW-340490_DE_2005.pdf | 228 | confirmed (checked 2026-10-26; not in report 13) |

## D366 Decimals of the angle addresses the compilers write

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| Heidenhain input resolution on rotary axes is 0.0001° | Heidenhain_iTNC530_Benutzerhandbuch-Klartext_NC-SW-340422_DE_2004.pdf | 629-630 | confirmed (set per axis by MP7290) |
| Siemens MD10210 `INT_INCR_PER_DEG`, 1000 increments per degree by default | Siemens_Sinumerik-840Dsl-828D_Funktionshandbuch-Grundfunktionen_DE_2012.pdf | 333 | confirmed |

## D367 A value of the TOLERANCE words that the machine's form cannot carry

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| The `G5.1 Q1` format carries no tolerance value | Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN.pdf | 635 | confirmed |
| Parameter 19581 is the nano-smoothing tolerance | Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN.pdf | 128 | confirmed |
| 11682/11683 are the tolerances at smoothing levels 1 and 10, 11684/11685 the rotary counterparts | Fanuc_30i-B_Parameterhandbuch_B-64490EN-05_EN.pdf | 654-655 | confirmed |

## D368 Fanuc precision level R on G5.1 Q1

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| `G05.1 Q1 [R_]`, R the precision level 1 to 10 | Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN.pdf | 635 | confirmed |
| The level needs an option | — | — | confirmed by report 13, which gives no page |
| A level once set stays in force after the mode ends | Fanuc_0i-D_Connection-Manual-Function-Vol1_B-64303EN-1-02_EN.pdf | 261 | confirmed |
| 11686 and 11687 hold a default level forced at reset or power-on when 1 to 10, and keep the level when 0 | Fanuc_30i-B_Parameterhandbuch_B-64490EN-05_EN.pdf | 655 | confirmed (11687 for AI contour control, 11686 for smoothing) |
| `G05.1`, `G08` and `G05` stand in a block of their own | Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN.pdf | 636 | confirmed |
| The two ends of the level range are 11682 and 11683 | Fanuc_0i-D_Connection-Manual-Function-Vol1_B-64303EN-1-02_EN.pdf | 262-263 | wrong, noted in `../manual-audit-2026-10-26.md` section 2: those are the nano-smoothing tolerances; the AI-contour level ends are parameters 13610 to 13615 |
| The control fixes no direction; which end is fine is a calibration | Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN.pdf; Fanuc_0i-D_Connection-Manual-Function-Vol1_B-64303EN-1-02_EN.pdf | 1677-1678; 262-263 | wrong, noted in `../manual-audit-2026-10-26.md` section 2: level 1 is emphasis on speed, level 10 on precision. The answer's "no" stands. |

## D369 Which point of the tool the axis words give

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| (answer) Heidenhain has indexed tools | Heidenhain_iTNC530-HSCI_Technisches-Handbuch_NC-SW-60642x-04_DE_2014.pdf; Heidenhain_TNC640_Technisches-Handbuch_NC-SW-34059x-11_DE_2021.pdf | 1600; 3062-3063 | confirmed (iTNC 530: index by MP7262, passed to the PLC in W266; TNC 640: indices 1 to 9) |
| (answer) How a TNC handles `TOOL CALL 5.1` with tool 5 in the spindle | — | — | not found: the manuals point to the machine maker's PLC and macro (see D373) |

## D370 The tip offset the shop measured, in the tool table of D10

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| (none: the draft states no controller fact) | — | — | — |

## D371 What a single-file ncx finds of the machine files and catalogs it ships with

| Fact as the draft states it | Connector document | Connector page | Status |
|---|---|---|---|
| (none: the draft states no controller fact) | — | — | — |
