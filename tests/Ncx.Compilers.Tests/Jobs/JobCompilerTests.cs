using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Compilers.Tests.Jobs;

/// <summary>
/// The job compiler (implementation 16, P6-02): per channel a compile of its program with the Fanuc compiler and a view
/// of the job, SYNC as the wait code of [sync] with the paths of the job, start_mark as the first block of every
/// channel program, the ranges of the [sync] groups, one file per channel, and the deadlock of the job.
/// </summary>
public sealed class JobCompilerTests
{
    // The wait code with the paths as a list, P12 (machine-config 5; controller-mapping 7).
    private const string SyncWithPaths =
        "[sync]\nwait = \"M{mark} P{paths}\"\npaths = \"list\"\nmark_range = [100, 199]";

    // Language 4.8: WITH defaults to all channels of the job; on a three-path machine a job of two channels waits
    // with P12, not with the paths of the machine (controller-mapping 7).
    [Fact]
    public void Sync_WithoutWith_WaitsForTheChannelsOfTheJob()
    {
        string machine = JobCompile.Machine(SyncWithPaths, channels: "[1, 2, 3]");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110"), JobCompile.Program(1000, "SYNC=110")));

        Assert.Equal(["%\nO1000 (T)\nG21\nG18 G99 G40 G80\nM110 P12\nM30\n%\n",
            "%\nO1000 (T)\nG21\nG18 G99 G40 G80\nM110 P12\nM30\n%\n"], texts);
    }

    // Controller-mapping 7, machine-config 5: with paths = "bitmask" WITH=1,3 is P5.
    [Fact]
    public void Sync_WithWithOnABitmaskMachine_WritesTheBitmaskOfTheParticipants()
    {
        string machine = JobCompile.Machine(SyncWithPaths.Replace("\"list\"", "\"bitmask\"",
            StringComparison.Ordinal), channels: "[1, 2, 3]");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=120 WITH=1,3"), JobCompile.Program(1000), JobCompile.Program(1000,
                "SYNC=120 WITH=1,3")));

        Assert.Contains("\nM120 P5\n", texts[0], StringComparison.Ordinal);
        Assert.DoesNotContain("M120", texts[1], StringComparison.Ordinal);
        Assert.Contains("\nM120 P5\n", texts[2], StringComparison.Ordinal);
    }

    // Virtual machine 3.7, 5: the compile of a channel program of a job with two channels is no single-channel job, so
    // its SYNC blocks do not warn VM570.
    [Fact]
    public void Sync_InTheProgramOfAChannel_IsNoSyncOfASingleChannelJob()
    {
        CompileResult result = JobCompile.Run(JobCompile.Machine(),
            JobCompile.Program(1000, "SYNC=110"), JobCompile.Program(1000, "SYNC=110"));

        Assert.DoesNotContain(Ncx.Core.Model.DiagnosticCodes.SyncInSingleChannelJob, JobCompile.CodesOf(result));
    }

    // Machine-config 5, start_mark: written as the first block of every channel program (Nakamura M199).
    [Fact]
    public void StartMark_ProgramsWithoutIt_IsTheFirstBlockOfEveryChannelProgram()
    {
        string machine = JobCompile.Machine(JobCompile.DefaultSync + "\nstart_mark = 199");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110"), JobCompile.Program(1000, "SYNC=110")));

        Assert.Equal("%\nO1000 (T)\nG21\nM199\nG18 G99 G40 G80\nM110\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG21\nM199\nG18 G99 G40 G80\nM110\nM30\n%\n", texts[1]);
    }

    // Machine-config 5, start_mark, and InsertStartMark (D314): programs whose first mark is the start
    // mark, as the WY pair of the Nakamura writes M199, wait at it once.
    [Fact]
    public void StartMark_EveryProgramPassesItFirst_IsNotWrittenAgain()
    {
        string machine = JobCompile.Machine(JobCompile.DefaultSync + "\nstart_mark = 199");

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=199", "SYNC=110"), JobCompile.Program(1000, "SYNC=199", "SYNC=110")));

        Assert.Equal("%\nO1000 (T)\nG21\nG18 G99 G40 G80\nM199\nM110\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG21\nG18 G99 G40 G80\nM199\nM110\nM30\n%\n", texts[1]);
    }

    // Machine-config 5, groups: the channels 3 and 4 of the WTW wait with the marks M800 to M849 of their group.
    [Fact]
    public void Groups_MarkInTheRangeOfTheGroupOfItsChannels_IsWritten()
    {
        CompileResult result = RunWtw("SYNC=810");

        List<string> texts = JobCompile.TextsOf(result);
        Assert.Contains("\nM810 P34\n", texts[0], StringComparison.Ordinal);
        Assert.Contains("\nM810 P34\n", texts[1], StringComparison.Ordinal);
    }

    // Machine-config 5, groups: a mark outside the range of the group of its channels is CMP710, and nothing is
    // written.
    [Fact]
    public void Groups_MarkOutsideTheRangeOfTheGroupOfItsChannels_IsCmp710()
    {
        CompileResult result = RunWtw("SYNC=150");

        Assert.Empty(result.Files);
        Assert.Equal(["C3.ncx(4): ERROR CMP710: SYNC=150 of the channels 3,4 lies outside their marks 800 to 849 "
            + "(machine-config 5, [sync] mark_range and groups).", "C4.ncx(4): ERROR CMP710: SYNC=150 of the "
            + "channels 3,4 lies outside their marks 800 to 849 (machine-config 5, [sync] mark_range and groups)."],
            result.Diagnostics.ToText().TrimEnd('\n').Split('\n'));
    }

    // Machine-config 8, language 4.14: two channels run the two programs of one file; each channel file holds its own
    // program alone.
    [Fact]
    public void Channels_TwoProgramsOfOneFile_EachChannelFileHoldsItsOwnProgram()
    {
        string file = string.Join('\n',
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"ONE\" NUMBER=1000",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SYNC=110",
            "PROGRAM=END",
            "PROGRAM=BEGIN NAME=\"TWO\" NUMBER=2000",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SYNC=110",
            "PROGRAM=END",
            "FILE=END") + "\n";
        var job = new JobManifest
        {
            Machine = "test.toml",
            Channels =
            [
                new ChannelProgram { Id = 1, File = "BOTH.ncx", Program = "ONE" },
                new ChannelProgram { Id = 2, File = "BOTH.ncx", Program = "TWO" },
            ],
        };

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(JobCompile.Machine(), job,
            new Dictionary<string, string> { ["BOTH.ncx"] = file }));

        Assert.Equal("%\nO1000 (ONE)\nG21\nG18 G99 G40 G80\nM110\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO2000 (TWO)\nG21\nG18 G99 G40 G80\nM110\nM30\n%\n", texts[1]);
    }

    // Language 4.13, D99, D316: a channel file holds the subprograms its program calls; a subprogram that no channel
    // program calls stands in no channel file but once in the library file JOB_LIB of the job, with the WARNING CMP720
    // that names it.
    [Fact]
    public void Subprograms_CalledByOneChannel_StandInItsFileAlone()
    {
        string file = string.Join('\n',
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"ONE\" NUMBER=1000",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "CALL=100",
            "PROGRAM=END",
            "PROGRAM=BEGIN NAME=\"TWO\" NUMBER=2000",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "DWELL=1",
            "SUB=END",
            "SUB=BEGIN NAME=200",
            "DWELL=2",
            "SUB=END",
            "FILE=END") + "\n";
        var job = new JobManifest
        {
            Name = "JOB",
            Machine = "test.toml",
            Channels =
            [
                new ChannelProgram { Id = 1, File = "BOTH.ncx", Program = "ONE" },
                new ChannelProgram { Id = 2, File = "BOTH.ncx", Program = "TWO" },
            ],
        };

        CompileResult result = JobCompile.Run(JobCompile.Machine(), job,
            new Dictionary<string, string> { ["BOTH.ncx"] = file });

        List<string> texts = JobCompile.TextsOf(result);
        Assert.Equal(3, texts.Count);
        Assert.Contains("\nO0100\n", texts[0], StringComparison.Ordinal);
        Assert.DoesNotContain("O0200", texts[0], StringComparison.Ordinal);
        Assert.DoesNotContain("O0100", texts[1], StringComparison.Ordinal);
        Assert.DoesNotContain("O0200", texts[1], StringComparison.Ordinal);
        Assert.Equal("JOB_LIB.nc", result.Files[2].Name);
        Assert.Equal("%\nO0200\nG4 X2.\nM99\n%\n", texts[2]);
        Diagnostic warning = Assert.Single(result.Diagnostics.Items,
            diagnostic => diagnostic.Code == DiagnosticCodes.SubprogramOfNoChannel);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(12, warning.Line);
        Assert.Contains("library file JOB_LIB", warning.Message, StringComparison.Ordinal);
    }

    // Virtual machine 3.7: two channels that wait at different marks are the deadlock ERROR of the job, and nothing is
    // written.
    [Fact]
    public void Deadlock_ChannelsWaitingAtDifferentMarks_IsTheErrorOfTheJobAndNothingIsWritten()
    {
        CompileResult result = JobCompile.Run(JobCompile.Machine(),
            JobCompile.Program(1000, "SYNC=110", "SYNC=111"), JobCompile.Program(1000, "SYNC=111", "SYNC=110"));

        Diagnostic error = JobCompile.ErrorOf(result);
        Assert.Equal(Ncx.Core.Model.DiagnosticCodes.SyncDeadlock, error.Code);
    }

    // Virtual machine 3.7, machine-config 5 and D314: channel 2 starts at
    // the START_CHANNEL of channel 1, so the start mark written as the first block of channel 1 can never be released;
    // the job as written deadlocks, which is the ERROR CMP712 on that SYNC, and nothing is written.
    [Fact]
    public void StartMark_ChannelStartedByStartChannel_DeadlocksTheWrittenJobAndIsCmp712()
    {
        string machine = JobCompile.Machine(JobCompile.DefaultSync + "\nstart_mark = 199" + JobCompile.ChannelWords);

        CompileResult result = JobCompile.Run(machine,
            JobCompile.Program(1000, "START_CHANNEL=2", "SYNC=110"), JobCompile.Program(1000, "SYNC=110"));

        Assert.Empty(result.Files);
        Assert.Equal(["C1.ncx(2, from 2): ERROR CMP712: job compiler ([sync] start_mark): SYNC=199 can never be "
            + "released in the job as written (machine-config 5, D56). Deadlock: channel 1 waits at "
            + "SYNC=199 with 1,2 (C1.ncx line 2); channel 2 waits to be started by START_CHANNEL=2; no channel can go "
            + "on and no mark can be released (virtual machine 3.7)."],
            result.Diagnostics.ToText().TrimEnd('\n').Split('\n'));
    }

    // Language 4.8, START_CHANNEL: a job whose channel 2 starts at the START_CHANNEL of channel 1 and that the job
    // compiler adds no SYNC to is written as it runs.
    [Fact]
    public void StartChannel_NoGeneratedSync_IsWritten()
    {
        string machine = JobCompile.Machine(JobCompile.DefaultSync + JobCompile.ChannelWords);

        List<string> texts = JobCompile.TextsOf(JobCompile.Run(machine,
            JobCompile.Program(1000, "START_CHANNEL=2", "SYNC=110"), JobCompile.Program(1000, "SYNC=110")));

        Assert.Equal("%\nO1000 (T)\nG21\nG18 G99 G40 G80\nM300 P2\nM110\nM30\n%\n", texts[0]);
        Assert.Equal("%\nO1000 (T)\nG21\nG18 G99 G40 G80\nM110\nM30\n%\n", texts[1]);
    }

    // The WTW of controller-mapping 7: four paths, the channels 1 and 2 with the marks M100 to M199, the channels 3 and
    // 4 with M800 to M849; a job of the channels 3 and 4 with the block in both programs.
    private static CompileResult RunWtw(string block)
    {
        string sync = SyncWithPaths
            + "\ngroups = [{ channels = [1, 2], range = [100, 199] }, { channels = [3, 4], range = [800, 849] }]";
        var job = new JobManifest
        {
            Machine = "test.toml",
            Channels =
            [
                new ChannelProgram { Id = 3, File = "C3.ncx" },
                new ChannelProgram { Id = 4, File = "C4.ncx" },
            ],
        };
        return JobCompile.Run(JobCompile.Machine(sync, channels: "[1, 2, 3, 4]"), job,
            new Dictionary<string, string>
            {
                ["C3.ncx"] = JobCompile.Program(1000, block),
                ["C4.ncx"] = JobCompile.Program(1000, block),
            });
    }

    // D304: channels at one mark that name different participants are the compile's ERROR CMP713, since the wait codes
    // written for them would pair different path lists (Fanuc PS0160), and nothing is written.
    [Fact]
    public void Mark_ChannelsWithDifferentParticipants_IsCmp713AndNothingIsWritten()
    {
        string machine = JobCompile.Machine(channels: "[1, 2, 3]");

        CompileResult result = JobCompile.Run(machine,
            JobCompile.Program(1000, "SYNC=110 WITH=1,2"),
            JobCompile.Program(1000, "DWELL=1", "SYNC=110"),
            JobCompile.Program(1000, "DWELL=1", "DWELL=1", "SYNC=110"));

        Diagnostic error = JobCompile.ErrorOf(result);
        Assert.Equal(DiagnosticCodes.MarkParticipantsDiffer, error.Code);
        Assert.Equal("C2.ncx", error.File);
        Assert.Contains("PS0160", error.Message, StringComparison.Ordinal);
    }

    // D142: the comment lines of a channel program keep their place before the block they stood before, also when the
    // job compiler inserts blocks in front of it, and those of the other channel's program stay out.
    [Fact]
    public void Build_CommentLines_KeepTheirPlaceInTheChannelProgram()
    {
        string file = string.Join('\n',
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"ONE\" NUMBER=1000",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "; one",
            "SYNC=110",
            "PROGRAM=END",
            "PROGRAM=BEGIN NAME=\"TWO\" NUMBER=2000",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "; two",
            "SYNC=110",
            "PROGRAM=END",
            "FILE=END") + "\n";
        NcxProgram parsed = Parser.Parse(file, "BOTH.ncx", new ParserOptions());
        var channel = new JobChannel
        {
            Channel = 2,
            File = parsed,
            Program = parsed.Programs[1],
            Marks = new ChannelMarks(parsed),
            FileDiagnostics = new Diagnostics("BOTH.ncx"),
            Diagnostics = new Diagnostics("BOTH.ncx"),
        };
        channel.InsertAfter(parsed.Programs[1].FirstBlock, parsed.Blocks[3]);

        NcxProgram built = channel.Build();

        Trivia comment = Assert.Single(built.Trivia);
        Assert.Equal("; two", comment.Text);
        Assert.NotNull(built.Blocks[comment.BlockIndex].Find("SYNC"));
        Assert.Equal(4, comment.BlockIndex);
    }
}
