# ThroughCoolantSourceRule

A reader rule (`ISourceRule`, `../../../docs/architecture/architecture.md`, section 9) for the case of D66: a machine whose coolant clutch engages only while the spindle stands writes `COOLANT:THROUGH=ON` as the three blocks `M5`, `M51`, `M3 S1500` (`../../../docs/spec/machine-config.md`, section 5a), and a program read back from them would stop and start its spindle twice when it is compiled again. The rule folds the three blocks back into the one word.

`Fold(block, following, builder)` holds the rule, and `ThroughCoolantSourceRule.Tests/ThroughCoolantSourceRuleTests.cs` shows it with the three blocks: blocks with nothing but the three codes, a block number and the speed are folded; a stop with a move, or a stop that another code follows, is not.

`Read` hands `Fold` the block it is offered and `block.Following`, and returns the number of blocks folded: 3, or 0 to decline. The reader offers every block to the plugins' rules before its tables, which name all three codes (D231), and shows a rule the blocks that follow, up to the next structure block (D232). The last test reads a Fanuc program with the Fanuc reader and the rule: the three blocks become the one word.
