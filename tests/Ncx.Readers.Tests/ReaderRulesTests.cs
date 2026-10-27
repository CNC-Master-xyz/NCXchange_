using Ncx.Core.Model;
using Ncx.Readers.Tests.Fakes;
using static Ncx.Readers.Tests.Fakes.FakeRead;

namespace Ncx.Readers.Tests;

/// <summary>
/// The reader rules of plugins in the skeleton of every reader: every source block goes to the rules first, in load
/// order, and a block no rule claims is read by the tables and the family's words (architecture 7 and 9; D40, D66,
/// D105, D106, D231, D232; phase 3, P3-01).
/// </summary>
public sealed class ReaderRulesTests
{
    // Done when of P3-01, with the order of D231: a fake reader with three rules reads a three-line input into the
    // expected NCX text. Every block goes to the rules first, in their order, and the first one that claims a block
    // reads it: M456 and M457, which no table names, and M08, which the coolant table names but the rule of this
    // machine claims (D231, D66). Without the rules M08 is read from the table (Rules_RuleDeclines_TheTablesReadIt).
    [Fact]
    public void FakeReaderWithThreeRules_ThreeLines_ReadsTheExpectedNcx()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");
        var counter = new CodeRule(457, "FUNC:PART_COUNTER=COUNT");
        var throughCoolant = new CodeRule(8, "COOLANT:THROUGH=ON");

        NcxProgram program = Program(Lines("M456", "M08", "M457"), transfer, counter, throughCoolant);

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN",
                "WORKPIECE=SUB",
                "COOLANT:THROUGH=ON",
                "FUNC:PART_COUNTER=COUNT",
                "PROGRAM=END",
                "FILE=END"),
            Core.Writing.NcxWriter.Write(program));
        Assert.Equal([DiagnosticCodes.ProgramEndMissing], Codes(program));
        Assert.Equal([1, 2, 3], transfer.Offered);
        Assert.Equal([2, 3], counter.Offered);
        Assert.Equal([2], throughCoolant.Offered);
    }

    // The rules come before the tables (D231): a code a table names goes to the rules first, and a rule that claims it
    // reads it, whatever the table says; the M08 of the source and the M8 of the table are the same code (D105).
    [Fact]
    public void Rules_CodeTheTableNames_AreAskedBeforeTheTables()
    {
        var throughCoolant = new CodeRule(8, "COOLANT:THROUGH=ON");

        string text = Text(Lines("%", "O0001", "M08", "M009", "M30", "%"), throughCoolant);

        Assert.Contains("\nCOOLANT:THROUGH=ON\nCOOLANT=OFF\n", text, StringComparison.Ordinal);
        Assert.Equal([2, 3, 4, 5], throughCoolant.Offered);
    }

    // A block no rule claims is read as before: the tables, then the family's words (D231, D40). Without a plugin
    // nothing changes.
    [Fact]
    public void Rules_RuleDeclines_TheTablesReadIt()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");

        string text = Text(Lines("%", "O0001", "M08", "M009", "M30", "%"), transfer);

        Assert.Contains("\nCOOLANT=ON\nCOOLANT=OFF\n", text, StringComparison.Ordinal);
        Assert.Equal([2, 3, 4, 5], transfer.Offered);
    }

    // A code that neither the tables nor a rule decide is read by the reader of the family, MFUNC for an M code no
    // table names (architecture 7).
    [Fact]
    public void Rules_NoRuleClaimsTheBlock_TheReaderReadsIt()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");

        string text = Text(Lines("%", "O0001", "M136", "M30", "%"), transfer);

        Assert.Contains("\nMFUNC=136\n", text, StringComparison.Ordinal);
    }

    // A rule sees the source-side state after the modal codes of the block it is offered, since the codes of a block
    // act for the block itself (architecture 7; controllers fanuc.md 3).
    [Fact]
    public void Rules_Offered_SeeTheStateAfterTheModalCodesOfTheBlock()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");

        Program(Lines("%", "O0001", "G0 X0", "G1 M456", "M30", "%"), transfer);

        Assert.Equal("G1", transfer.MotionSeen[transfer.Offered.IndexOf(4)]);
    }

    // A claim is complete when Read returns: the rule's blocks stand where the claimed block stands, between the
    // blocks read before and after it, and every block is offered (D5, D231, D232).
    [Fact]
    public void Rules_ClaimBetweenTwoMotions_RuleBlocksStandInPlaceOfTheBlock()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");

        string text = Text(Lines("%", "O0001", "G0 X0", "M456", "G1 X10", "M30", "%"), transfer);

        Assert.Contains("\nRAPID X=0\nWORKPIECE=SUB\nLINE X=10\n", text, StringComparison.Ordinal);
        Assert.Equal([2, 3, 4, 5, 6], transfer.Offered);
    }

    // Comments are kept (D5): the comment of a block a rule claimed stays as a comment-only line after the rule's
    // blocks (D92).
    [Fact]
    public void Rules_ClaimedBlockWithAComment_KeepsTheCommentAsTrivia()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");

        string text = Text(Lines("%", "O0001", "M456 (TRANSFER)", "M30", "%"), transfer);

        Assert.Contains("\nWORKPIECE=SUB\n; TRANSFER\nPROGRAM=END\n", text, StringComparison.Ordinal);
    }

    // A rule sees the following blocks of its section, up to the next structure block, and no trivia: here the M30
    // that ends the program (D232).
    [Fact]
    public void Rules_Offered_SeeTheFollowingBlocksUpToTheNextStructureBlock()
    {
        var run = new RunRule(5, 3);

        Program(Lines("%", "O0001", "M5", "(NOTE)", "M51", "M3 S1500", "M30", "%"), run);

        Assert.Equal([5, 6], run.Shown[1]);
    }

    // A jump target is a structure block: the look-ahead stops in front of it (D232).
    [Fact]
    public void Rules_Offered_LookAheadStopsAtALabel()
    {
        var run = new RunRule(5, 3);

        Program(Lines("%", "O0001", "M5", "M51", "N20 M3 S1500", "GOTO 20", "M30", "%"), run);

        Assert.Equal([4], run.Shown[1]);
    }

    // A rule claims a run of blocks with the count it returns: its NCX stands where the run stood, the reader reads
    // none of the claimed blocks, applies them to the source state and keeps their comments as comment-only lines after
    // the rule's blocks (D232, D5, D92).
    [Fact]
    public void Rules_ClaimOfThreeBlocks_ReadsNoneOfThemAndKeepsTheirComments()
    {
        var run = new RunRule(5, 3);

        string text = Text(
            Lines("%", "O0001", "G0 X0", "M5 (STOP)", "M51", "G1 M3 S1500 (START)", "X10", "M30", "%"), run);

        Assert.Contains(
            "\nRAPID X=0\nCOOLANT:THROUGH=ON\n; STOP\n; START\nLINE X=10\nPROGRAM=END\n", text,
            StringComparison.Ordinal);
        Assert.Equal([null, "G0", "G0", "G1", "G1"], run.MotionSeen);
    }

    // A count of 0 declines, and the reader reads the block as without the rule (D232).
    [Fact]
    public void Rules_TooFewBlocksFollow_TheRuleDeclinesAndTheReaderReadsThem()
    {
        var run = new RunRule(5, 3);

        string text = Text(Lines("%", "O0001", "M5", "M51", "M30", "%"), run);

        Assert.Contains("\nSPINDLE=OFF\nMFUNC=51\nPROGRAM=END\n", text, StringComparison.Ordinal);
    }

    // A rule never claims more than it was shown: a larger count takes the offered block and the blocks shown, and
    // the structure block after them is read as always (D232; gap closed 2026-10-27, delegated).
    [Fact]
    public void Rules_CountBeyondTheBlocksShown_ClaimsOnlyTheBlocksShown()
    {
        var run = new RunRule(5, 4, claimsBeyond: true);

        string text = Text(Lines("%", "O0001", "M5", "M51", "M30", "%"), run);

        Assert.Contains("\nCOOLANT:THROUGH=ON\nPROGRAM=END\n", text, StringComparison.Ordinal);
    }
}
