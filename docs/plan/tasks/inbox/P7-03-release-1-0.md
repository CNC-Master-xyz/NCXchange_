# P7-03 Release 1.0

Phase: 7 | Milestone: M10 | Depends on: `P7-02`, `P6-02`, `P5-02`, `P4-03` | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

A tagged, installable 1.0.

## Scope

- `ncx` packaged as a .NET tool (`dotnet tool install`) and as self-contained binaries for Windows, macOS and Linux from CI; the shipped `machines/` and `cycles/` are built into the binary (the last place of the lookup) and kept loose beside it (D371).
- Release notes from `docs/decisions/decisions.md` and the milestone table; a `CHANGELOG.md` started.
- `docs/reading-the-code.md` checked against the code (extended to `convert` and `compile` in P3-07); the folder READMEs checked; the definition of done applied to the whole tree.
- Known limits stated on the front page: no kinematics, Heidenhain turning and GILDEMEISTER structure programming read as `RAW`, the runtime is an estimate.

## References

- architecture.md section 12
- code-guidelines.md section 12

## Done when

- A fresh machine installs the tool and runs the acceptance examples from the release notes.
- The published executable alone, in a temporary working directory, compiles `2.5D_FRAESEN.ncx` for the three mills with exit code 0 (D371); the release notes say that `ncx plugin new` from a lone binary reports CLI552.
- Tag `v1.0.0` on the main branch with green CI.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
