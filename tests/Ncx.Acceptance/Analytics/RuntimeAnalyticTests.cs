using System.Globalization;
using Ncx.Acceptance.Cli;
using Ncx.Analytics;
using Ncx.Analytics.Runtime;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The runtime estimate of virtual machine 8 and D64 on small programs, the times computed by hand: the trapezoidal
/// profile per motion from max_feed, acceleration and rapid of the moving axes, block_time, path_mode with
/// corner_speed, the feed per revolution and CSS, spindle accel_time, dwells and cycles, the fallback without
/// [dynamics], the totals and the block range (implementation 14, P4-02). Every program starts with a RAPID from the
/// unknown start position, which has no length and no time (D100), so the block under test starts from rest.
/// </summary>
public sealed class RuntimeAnalyticTests
{
    private const int Precision = 9;

    // P4-02 test first, the profile on one block: LINE X=100 at F=6000 (100 mm/s) with 1000 mm/s^2, exact stop: 0.1 s
    // and 5 mm to reach the feed, 0.1 s and 5 mm to stop, 90 mm at 100 mm/s; 1.1 s.
    [Fact]
    public void TrapezoidalProfile_OneLineExactStop_TakesTheTimeComputedByHand()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=100 F=6000");

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // VM 8: a block too short to reach its feed accelerates and decelerates at once: LINE X=4 at 100 mm/s with
    // 1000 mm/s^2 reaches sqrt(1000 * 4) mm/s, 2 * sqrt(4000) / 1000 s.
    [Fact]
    public void TrapezoidalProfile_LineTooShortForItsFeed_AcceleratesAndBrakesAtOnce()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=4 F=6000");

        Assert.Equal(2 * Math.Sqrt(4000) / 1000, runtime.TotalSeconds, Precision);
    }

    // P4-02 test first, the block-time floor: a 0.01 mm segment at 100 mm/s takes 0.0001 s, and a block can never be
    // shorter than the control's block_time, 0.001 s (VM 8). Without acceleration in the machine file the segment moves
    // at its feed from start to end.
    [Fact]
    public void BlockTime_SegmentOf0_01mm_TakesTheBlockTime()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop, acceleration: null),
            "LINE X=0.01 F=6000");

        Assert.Equal(0.001, runtime.TotalSeconds, Precision);
    }

    // VM 8: the block time "is what makes thousands of short segments in 3D and 5-axis programs slow": a hundred
    // segments of 0.01 mm take a hundred block times, 0.1 s, ten times their travel time at the feed.
    [Fact]
    public void BlockTime_HundredSegmentsOf0_01mm_TakeAHundredBlockTimes()
    {
        var blocks = new List<string>();
        for (int segment = 1; segment <= 100; segment++)
        {
            string x = (segment * 0.01m).ToString(CultureInfo.InvariantCulture);
            blocks.Add(segment == 1 ? $"LINE X={x} F=6000" : $"LINE X={x}");
        }

        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop, acceleration: null),
            blocks.ToArray());

        Assert.Equal(0.1, runtime.TotalSeconds, Precision);
    }

    // VM 8: path_mode = "exact_stop" brakes to zero in every block: two lines of 100 mm take 1.1 s each.
    [Fact]
    public void PathModeExactStop_TwoLinesInARow_BrakeToZeroInTheCorner()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=100 F=6000",
            "LINE X=200");

        Assert.Equal(2.2, runtime.TotalSeconds, Precision);
    }

    // VM 8: "continuous" carries the speed through corners up to corner_speed, 3000 mm/min (50 mm/s): each line
    // accelerates from or brakes to 50 mm/s in 0.05 s over 3.75 mm, and to or from rest in 0.1 s over 5 mm, and travels
    // the other 91.25 mm at 100 mm/s; 1.0625 s each, 2.125 s for the two.
    [Fact]
    public void PathModeContinuous_TwoLinesInARow_CarryTheCornerSpeedThroughTheCorner()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)),
            "LINE X=100 F=6000",
            "LINE X=200");

        Assert.Equal(2.125, runtime.TotalSeconds, Precision);
    }

    // VM 8: the commanded feed is limited by the max_feed of every axis that moves: F=12000 on X with max_feed 6000
    // moves at 6000, 1.1 s for 100 mm.
    [Fact]
    public void MaxFeed_FeedAboveTheMaxFeedOfTheAxis_MovesAtTheMaxFeed()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=100 F=12000");

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // VM 8: the block accelerates with the acceleration of the axes that move: Y alone with 500 mm/s^2 takes 0.2 s and
    // 10 mm to reach 100 mm/s and as much to stop, 80 mm at 100 mm/s; 1.2 s.
    [Fact]
    public void Acceleration_OnlyYMoves_AcceleratesWithTheAccelerationOfY()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE Y=100 F=6000");

        Assert.Equal(1.2, runtime.TotalSeconds, Precision);
    }

    // VM 8: with X (1000 mm/s^2) and Y (500 mm/s^2) moving, the smallest acceleration of the moving axes applies:
    // 100 mm to X=60 Y=80 take 1.2 s as Y alone does.
    [Fact]
    public void Acceleration_XAndYMove_AcceleratesWithTheSmallestOfTheMovingAxes()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=60 Y=80 F=6000");

        Assert.Equal(1.2, runtime.TotalSeconds, Precision);
    }

    // VM 8 with VM 3.2: an ARC turns about its center on both plane axes, so a full circle (start = end) moves X and Y
    // although neither ends elsewhere. F=12000 is limited to the max_feed of 6000 mm/min (100 mm/s), and the block
    // accelerates with the 500 mm/s^2 of Y: from X=10 Y=0 about X=0 Y=0, 2 pi 10 mm, which the MOTION gives rounded to
    // 62.831853 mm (D121); 0.2 s and 10 mm to reach 100 mm/s, as much to stop, the rest at 100 mm/s: 62.831853 / 100
    // + 0.2 s.
    [Fact]
    public void MovingAxes_FullCircleAboveTheMaxFeed_MovesAtTheMaxFeedWithTheAccelerationOfY()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RAPID X=10 Y=0 Z=0"],
            "ARC=CCW X=10 Y=0 CENTER:X=0 CENTER:Y=0 F=12000");

        Assert.Equal((62.831853 / 100) + 0.2, runtime.TotalSeconds, Precision);
    }

    // VM 8 with VM 3.2: a half circle from X=10 Y=0 to X=-10 Y=0 about X=0 Y=0 ends on the Y it started from, and Y
    // moves along it all the same: the block accelerates with the 500 mm/s^2 of Y, not the 1000 mm/s^2 of X. pi 10 mm,
    // rounded to 31.415927 mm (D121), at 100 mm/s: 31.415927 / 100 + 0.2 s.
    [Fact]
    public void MovingAxes_HalfCircleEndingOnTheYItStartedFrom_AcceleratesWithTheAccelerationOfY()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RAPID X=10 Y=0 Z=0"],
            "ARC=CCW X=-10 Y=0 CENTER:X=0 CENTER:Y=0 F=6000");

        Assert.Equal((31.415927 / 100) + 0.2, runtime.TotalSeconds, Precision);
    }

    // VM 8: RAPID and HOME use the rapid rate with the same profile: 100 mm at the rapid of 6000 mm/min, 1.1 s,
    // although the program has no F.
    [Fact]
    public void Rapid_RapidBlock_MovesAtTheRapidRateWithTheSameProfile()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "RAPID X=100");

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // The reading of the TODO(question) in RuntimeEstimator.SpeedOf: a RETRACT takes no feed (VM 3.1a) and moves at the
    // rapid rate with the same profile, as the tool list counts it as rapid. LINE X=100 at F=600 (10 mm/s) takes
    // 100 / 10 + 10 / 1000 s; RETRACT=50 moves Z alone at its rapid of 6000 mm/min (100 mm/s) with 1000 mm/s^2, 0.1 s
    // and 5 mm to reach it, as much to stop, 40 mm at 100 mm/s: 0.6 s, not the 5.01 s of the active F.
    [Fact]
    public void Retract_AfterALineWithAFeed_MovesAtTheRapidRateOfTheToolAxis()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            "LINE X=100 F=600",
            "RETRACT=50");

        Assert.Equal(10.01 + 0.6, runtime.TotalSeconds, Precision);
    }

    // The same reading without an F before it: RETRACT=50 is timed at the rapid rate of Z, 0.6 s, and is not counted
    // as a motion without a known feed.
    [Fact]
    public void Retract_WithoutAnyFeedBeforeIt_IsTimedAtTheRapidRate()
    {
        MachineConfig machine = AnalyticMachines.Mill(AnalyticMachines.ExactStop);
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(machine));
        string report = AnalyticRuns.Run(Program(["RAPID X=0 Y=0 Z=0"], "RETRACT=50"), machine, runtime);

        Assert.Equal(0.6, runtime.TotalSeconds, Precision);
        Assert.Contains(", 0 motions without a known feed or rapid rate, ", report, StringComparison.Ordinal);
    }

    // The same reading without [dynamics] (VM 8, the fallback): LINE X=100 at F=600 in 10 s, RETRACT=50 distance over
    // the rapid rate of Z, 6000 mm/min, in 0.5 s; the report says so.
    [Fact]
    public void Retract_MachineWithoutDynamics_IsDistanceOverTheRapidRate()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(AnalyticMachines.Mill(dynamics: null)));
        string report = AnalyticRuns.Run(Program(["RAPID X=0 Y=0 Z=0"], "LINE X=100 F=600", "RETRACT=50"),
            AnalyticMachines.Mill(dynamics: null), runtime);

        Assert.Equal(10.5, runtime.TotalSeconds, Precision);
        Assert.Contains("RAPID, HOME and RETRACT distance over the rapid rate", report, StringComparison.Ordinal);
    }

    // VM 8: a feed per revolution times the rpm: 6 mm/rev at 1000 rpm is 6000 mm/min, 1.1 s for 100 mm.
    [Fact]
    public void FeedPerRevolution_Line_MovesAtTheFeedTimesTheRpm()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RPM=1000", "SPINDLE=CW", "FEED_MODE=PER_REV", "RAPID X=0 Y=0 Z=0"],
            "LINE X=100 F=6",
            "SPINDLE=OFF");

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // D288: while the tool holder's spindle stands, a feed per revolution follows the workpiece holder's spindle, the
    // one a turning tool cuts on. On the default machine (D103) the holder carries the tool spindle TOOL and MAIN
    // holds the part: 6 mm/rev at 1000 rpm of MAIN is 6000 mm/min, 1.1 s for 100 mm.
    [Fact]
    public void FeedPerRevolutionSpindle_ToolSpindleStandsAndTheWorkpieceSpindleTurns_TakesTheWorkpieceSpindlesRpm()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.PolarLathe(),
            ["RPM:MAIN=1000", "SPINDLE:MAIN=CW", "FEED_MODE=PER_REV", "RAPID X=0 Z=0"],
            "LINE Z=-100 F=6",
            "SPINDLE:MAIN=OFF");

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // D288: while the tool holder's spindle turns, it is the rpm, also when the workpiece holder's spindle turns as
    // well: 6 mm/rev at the 500 rpm of TOOL is 3000 mm/min (50 mm/s), 100 mm from rest to rest with 1000 mm/s^2, and
    // not the 1000 rpm of MAIN, which would take 1.1 s.
    [Fact]
    public void FeedPerRevolutionSpindle_BothSpindlesTurn_TakesTheToolHoldersRpm()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.PolarLathe(),
            ["RPM:MAIN=1000", "SPINDLE:MAIN=CW", "RPM:TOOL=500", "SPINDLE:TOOL=CW", "FEED_MODE=PER_REV",
                "RAPID X=0 Z=0"],
            "LINE Z=-100 F=6",
            "SPINDLE:TOOL=OFF",
            "SPINDLE:MAIN=OFF");

        Assert.Equal((100d / 50) + (50d / 1000), runtime.TotalSeconds, Precision);
    }

    // VM 8: under CSS the rpm follows from VC and the current X radius: 200 m/min at X=50 is 200000 / (2 pi 50) rpm,
    // times 0.2 mm/rev; LINE Z=-100 moves X nowhere, from rest to rest with 1000 mm/s^2: 100 / v + v / 1000 s.
    [Fact]
    public void ConstantSurfaceSpeed_LineAtRadius50_TakesTheRpmFromVcAndTheRadius()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RPM_MAX=3000", "VC=200", "CSS=ON", "SPINDLE=CW", "FEED_MODE=PER_REV", "RAPID X=50 Y=0 Z=0"],
            "LINE Z=-100 F=0.2",
            "SPINDLE=OFF");

        double speed = 0.2 * (200000 / (2 * Math.PI * 50)) / 60;
        Assert.Equal((100 / speed) + (speed / 1000), runtime.TotalSeconds, Precision);
    }

    // VM 8: the rpm of CSS is capped by RPM_MAX: 200 m/min at X=5 would be 6366 rpm, RPM_MAX=3000 caps it, 0.2 mm/rev
    // times 3000 is 10 mm/s; 100 mm from rest to rest take 100 / 10 + 10 / 1000 s.
    [Fact]
    public void ConstantSurfaceSpeed_LineAtRadius5_IsCappedByRpmMax()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RPM_MAX=3000", "VC=200", "CSS=ON", "SPINDLE=CW", "FEED_MODE=PER_REV", "RAPID X=5 Y=0 Z=0"],
            "LINE Z=-100 F=0.2",
            "SPINDLE=OFF");

        Assert.Equal(10.01, runtime.TotalSeconds, Precision);
    }

    // D289: along a motion that changes X the radius of CSS is the mean of the start and the end X radius, so a
    // facing cut from X=50 to X=0 is timed at radius 25 and not at the start radius 50.
    [Fact]
    public void CssRadius_FacingCutThatChangesX_TakesTheMeanOfTheStartAndEndRadius()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RPM_MAX=3000", "VC=200", "CSS=ON", "SPINDLE=CW", "FEED_MODE=PER_REV", "RAPID X=50 Y=0 Z=0"],
            "LINE X=0 F=0.2",
            "SPINDLE=OFF");

        double speed = 0.2 * (200000 / (2 * Math.PI * 25)) / 60;
        Assert.Equal((50 / speed) + (speed / 1000), runtime.TotalSeconds, Precision);
    }

    // D289: for a RAPID the radius of CSS is the end radius of the block and not the mean, because Fanuc computes the
    // G96 rpm of a G00 block from the end position of the block, nothing being cut on the way, and Nakamura adds that
    // the speed is therefore not controlled during the positioning. RAPID X=10 from X=50 at VC=200 gives the rpm at
    // radius 10, 200000 / (2 pi 10) = 3183.1, and not the 1061.0 of the mean radius 30.
    [Fact]
    public void CssRadius_RapidThatChangesX_TakesTheEndRadiusAndNotTheMean()
    {
        MachineConfig machine = AnalyticMachines.Mill(AnalyticMachines.ExactStop);
        var motions = new MotionCollector();
        AnalyticRuns.Run(
            Program(["RPM_MAX=20000", "VC=200", "CSS=ON", "SPINDLE=CW", "FEED_MODE=PER_REV", "RAPID X=50 Y=0 Z=0"],
                "RAPID X=10",
                "SPINDLE=OFF"),
            machine,
            motions);

        MotionEvent rapid = motions.Motions.Last(motion => motion.Verb == Verb.Rapid);
        Assert.Equal(200d * 1000 / (2 * Math.PI * 10), SpindleSpeed.RpmOf(rapid, machine)!.Value, Precision);
    }

    // D289: a cutting motion keeps the mean of the start and the end X radius. LINE X=10 from X=50 at VC=200 gives the
    // rpm at the mean radius 30, 200000 / (2 pi 30) = 1061.0, and not the 3183.1 of the end radius.
    [Fact]
    public void CssRadius_CuttingMotionThatChangesX_KeepsTheMeanOfTheStartAndEndRadius()
    {
        MachineConfig machine = AnalyticMachines.Mill(AnalyticMachines.ExactStop);
        var motions = new MotionCollector();
        AnalyticRuns.Run(
            Program(["RPM_MAX=20000", "VC=200", "CSS=ON", "SPINDLE=CW", "FEED_MODE=PER_REV", "RAPID X=50 Y=0 Z=0"],
                "LINE X=10 F=0.2",
                "SPINDLE=OFF"),
            machine,
            motions);

        MotionEvent line = motions.Motions.Last(motion => motion.Verb == Verb.Line);
        Assert.Equal(200d * 1000 / (2 * Math.PI * 30), SpindleSpeed.RpmOf(line, machine)!.Value, Precision);
    }

    // VM 8, machine-config 5: a spindle start or stop adds accel_time of that spindle, 2 s each.
    [Fact]
    public void SpindleAccelTime_StartAndStop_AddTheAccelTimeOfTheSpindle()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop, accelTime: 2m),
            [],
            "SPINDLE=CW",
            "SPINDLE=OFF");

        Assert.Equal(4.0, runtime.TotalSeconds, Precision);
    }

    // D323: without rpm_max a start adds the whole accel_time, and a change from CW to CCW counts as a stop and a
    // start; CW, CCW, OFF: 2 + 4 + 2 s.
    [Fact]
    public void SpindleAccelTime_Reversal_CountsAsAStopAndAStart()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop, accelTime: 2m),
            [],
            "RPM=100 SPINDLE=CW",
            "SPINDLE=CCW",
            "SPINDLE=OFF");

        Assert.Equal(8.0, runtime.TotalSeconds, Precision);
    }

    // VM 8: dwells add their times; DWELL is in seconds (language 4.1).
    [Fact]
    public void Dwell_DwellBlock_AddsItsSeconds()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            [],
            "DWELL=2.5");

        Assert.Equal(2.5, runtime.TotalSeconds, Precision);
    }

    // VM 8, 3.3, D37: cycle plunges and dwells add their times, from the motions ExpandCycles raises. From X=0 Z=10 the
    // call at X=10 rapids 10 mm in the plane (0.1 s up to 100 mm/s, 0.1 s down: 0.2 s), 8 mm to CLEARANCE (a triangle,
    // 2 * sqrt(8000) / 1000 s), feeds 12 mm to DEPTH at CYCLE_F=600, 10 mm/s (12 / 10 + 10 / 1000 s), dwells 1 s and
    // rapids 12 mm back to CLEARANCE (12 / 100 + 100 / 1000 s).
    [Fact]
    public void CycleCall_ExpandedDrillingCycle_AddsItsRapidsPlungeAndDwell()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["RAPID X=0 Y=0 Z=10"],
            "CYCLE=DRILL_DWELL DEPTH=-10 CLEARANCE=2 CYCLE_F=600 CYCLE_DWELL=1 CYCLE_RETRACT=CLEARANCE",
            "CYCLE_CALL X=10",
            "CYCLE=OFF");

        double expected = 0.2 + (2 * Math.Sqrt(8000) / 1000) + (1.2 + 0.01) + 1 + (0.12 + 0.1);
        Assert.Equal(expected, runtime.TotalSeconds, Precision);
    }

    // VM 8: without dynamics in the configuration the estimate falls back to distance over feed and says so: 100 mm at
    // 6000 mm/min in 1 s, and 100 mm of RAPID at the rapid rate of 6000 mm/min in 1 s.
    [Fact]
    public void Fallback_MachineWithoutDynamics_IsDistanceOverFeedAndSaysSo()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(AnalyticMachines.Mill(dynamics: null)));
        string report = AnalyticRuns.Run(Program(["RAPID X=0 Y=0 Z=0"], "LINE X=100 F=6000", "RAPID X=0"),
            AnalyticMachines.Mill(dynamics: null), runtime);

        Assert.Equal(2.0, runtime.TotalSeconds, Precision);
        Assert.Contains("Motions: distance over feed", report, StringComparison.Ordinal);
        Assert.Contains("the machine has no [dynamics] (virtual machine 8)", report, StringComparison.Ordinal);
    }

    // The machine file gives its speeds in mm/min and mm/s^2 (machine-config 4): 4 in at 100 in/min are 101.6 mm at
    // 2540 mm/min (42.333 mm/s), from rest to rest with 1000 mm/s^2, 101.6 / v + v / 1000 s.
    [Fact]
    public void Units_InchProgram_IsTimedInMillimetresAgainstTheMachineFile()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(AnalyticMachines.Mill(AnalyticMachines.ExactStop)));
        AnalyticRuns.Run(
            CliHarness.OneProgram("UNITS=INCH", "RAPID X=0 Y=0 Z=0", "LINE X=4 F=100"),
            AnalyticMachines.Mill(AnalyticMachines.ExactStop), runtime);

        double speed = 2540d / 60;
        Assert.Equal((101.6 / speed) + (speed / 1000), runtime.TotalSeconds, Precision);
    }

    // VM 8: totals per tool and per section: 1.1 s under tool 1 in ROUGH, 1.1 s under tool 2 in FINISH.
    [Fact]
    public void Totals_TwoToolsInTwoSections_ArePerToolAndPerSection()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop),
            ["SECTION=\"ROUGH\"", "TOOL=1", "RAPID X=0 Y=0 Z=0"],
            "LINE X=100 F=6000",
            "SECTION=\"FINISH\"",
            "TOOL=2",
            "LINE X=0");

        Assert.Equal(1.1, runtime.SecondsOfTool("1"), Precision);
        Assert.Equal(1.1, runtime.SecondsOfTool("2"), Precision);
        Assert.Equal(1.1, runtime.SecondsOfSection("ROUGH"), Precision);
        Assert.Equal(1.1, runtime.SecondsOfSection("FINISH"), Precision);
        Assert.Equal(2.2, runtime.TotalSeconds, Precision);
    }

    // VM 8, D67: the estimate over a block range counts the blocks on its lines only: line 6 of the program, the second
    // LINE, 1.1 s of the 2.2 s.
    [Fact]
    public void BlockRange_FromToOneLine_TimesOnlyTheBlockOnIt()
    {
        MachineConfig machine = AnalyticMachines.Mill(AnalyticMachines.ExactStop);
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(machine, new BlockRange { From = 6, To = 6 }));
        AnalyticRuns.Run(
            CliHarness.OneProgram("UNITS=MM", "RAPID X=0 Y=0 Z=0", "LINE X=100 F=6000", "LINE X=200"),
            machine, runtime);

        Assert.Equal(1.1, runtime.TotalSeconds, Precision);
    }

    // Implementation 14, P4-02 and risks: the report always says "estimate" and carries the machine file and every
    // value it used.
    [Fact]
    public void Report_Always_SaysEstimateAndNamesTheMachineFileAndItsValues()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(AnalyticMachines.Mill(AnalyticMachines.ExactStop)));
        string report = AnalyticRuns.Run(Program(["RAPID X=0 Y=0 Z=0"], "LINE X=100 F=6000"),
            AnalyticMachines.Mill(AnalyticMachines.ExactStop), runtime);

        Assert.StartsWith("Runtime estimate: test.ncx, the whole file, on test.toml, INTERPRETED run\n", report,
            StringComparison.Ordinal);
        Assert.Contains("An estimate from the values of the machine file", report, StringComparison.Ordinal);
        Assert.Contains("path_mode exact_stop (the start value of PATH_MODE, D350), corner_speed none mm/min, "
            + "block_time 0.001 s", report,
            StringComparison.Ordinal);
        Assert.Contains("X     6000            6000               1000", report, StringComparison.Ordinal);
        Assert.Contains("Estimated time: 1.1 s (0:00:01), the longest channel.", report, StringComparison.Ordinal);
    }

    // D290: in the polar plane the C word is a length (VM 3.1, D102), and the deg/min and deg/s^2 of C convert at
    // r^2 / p per radian (gap closed 2026-10-28): LINE C=10 from X=10 C=0 lies at p = 10 from the centre and is nearest
    // it at its start, r = 10 (the foot of the perpendicular), so max_feed 1200 deg/min of C is 1200 * 10 * pi / 180
    // mm/min and its 100 deg/s^2 are 100 * 10 * pi / 180 mm/s^2, below the 6000 mm/min and 1000 mm/s^2 of X, which
    // limit the block as well. The report names those values of C in the table of the rotary axes and says how it
    // reads them.
    [Fact]
    public void Report_MotionInThePolarPlane_NamesTheValuesOfTheRotaryAxisItUsed()
    {
        MachineConfig lathe = AnalyticMachines.PolarLathe();
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(lathe));
        string report = AnalyticRuns.Run(
            Program(["SPINDLE_MODE:MAIN=AXIS", "RAPID Z=2", "POLAR=ON", "LINE X=10 C=0 F=6000"], "LINE C=10"),
            lathe, runtime);

        double atL = 10 * Math.PI / 180;
        Assert.Equal(FromRestToRest(10, 1200 * atL / 60, 100 * atL), runtime.TotalSeconds, Precision);
        Assert.Contains("\nRotary axes: deg/min and deg/s^2 (machine-config 4); they limit and accelerate a motion "
            + "only in the polar or cylinder plane, where their word is a length (virtual machine 3.1, D102): under "
            + "CYLINDER=r at r, 1 deg = pi r / 180 mm, rapid included; under POLAR=ON at r^2 / p per radian, r the "
            + "nearest distance of the block from the centre of rotation and p that of its line, a RAPID taking the "
            + "rapid of X (D290).\n"
            + "axis  rapid (deg/min)  max_feed (deg/min)  acceleration (deg/s^2)\n"
            + "C     3600             1200                100\n", report, StringComparison.Ordinal);
        Assert.Contains("Not in the estimate: the travel of rotary axes outside the polar and cylinder plane (0 "
            + "motions turned one;", report, StringComparison.Ordinal);
        Assert.DoesNotContain("C clamp near centre not estimated", report, StringComparison.Ordinal);
    }

    // D290: a block through the centre of the polar plane has L = 0. The estimate does not divide by zero: it takes
    // the X limits alone for that block, 6000 mm/min and 1000 mm/s^2 over the 20 mm from X=10 to X=-10 (1.1 s less
    // the 80 mm at 100 mm/s, 0.3 s), and the report lines it.
    [Fact]
    public void PolarPlane_BlockThroughTheCentre_TakesTheXLimitAloneAndSaysSo()
    {
        MachineConfig lathe = AnalyticMachines.PolarLathe();
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(lathe));
        string report = AnalyticRuns.Run(
            Program(["SPINDLE_MODE:MAIN=AXIS", "RAPID Z=2", "POLAR=ON", "LINE X=10 C=0 F=6000"], "LINE X=-10"),
            lathe, runtime);

        Assert.Equal(FromRestToRest(20, 100, 1000), runtime.TotalSeconds, Precision);
        Assert.Contains("C clamp near centre not estimated: 1 motions through the centre of the polar plane took the "
            + "limits of X alone (D290).", report, StringComparison.Ordinal);
    }

    // D290: a LINE of Z alone under POLAR=ON moves neither X nor C of the face plane, so near the centre it is timed on
    // Z alone, 100 mm at 6000 mm/min with 1000 mm/s^2, and counts no motion through the centre.
    [Fact]
    public void PolarPlane_LineOfZAloneNearTheCentre_IsTimedOnZAlone()
    {
        MachineConfig lathe = AnalyticMachines.PolarLathe();
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(lathe));
        string report = AnalyticRuns.Run(
            Program(["SPINDLE_MODE:MAIN=AXIS", "RAPID Z=2", "POLAR=ON", "LINE X=0.1 C=0 F=6000"], "LINE Z=-98"),
            lathe, runtime);

        Assert.Equal(FromRestToRest(100, 100, 1000), runtime.TotalSeconds, Precision);
        Assert.DoesNotContain("C clamp near centre", report, StringComparison.Ordinal);
    }

    // D290 with the exact bound (gap closed 2026-10-28): C turns at v p / r^2, fastest where the segment is nearest
    // the centre. LINE C=20 from X=10 C=10 lies at p = 10 from the centre and is nearest it at its start, r^2 = 200, so
    // C limits it at 1200 deg/min times 200 / 10 mm per radian; a radial LINE X=20 from X=10 (p = 0) needs no C rate
    // and moves at the 6000 mm/min of X.
    [Theory]
    [InlineData("LINE X=10 C=10 F=6000", "LINE C=20", false)]
    [InlineData("LINE X=10 C=0 F=6000", "LINE X=20", true)]
    public void PolarPlane_Line_IsLimitedByTheCRateItNeeds(string before, string line, bool radial)
    {
        MachineConfig lathe = AnalyticMachines.PolarLathe();
        RuntimeAnalytic runtime = Run(lathe, ["SPINDLE_MODE:MAIN=AXIS", "RAPID Z=2", "POLAR=ON", before], line);

        double perDegree = 20 * Math.PI / 180;
        double expected = radial
            ? FromRestToRest(10, 100, 1000)
            : FromRestToRest(10, 1200 * perDegree / 60, 100 * perDegree);
        Assert.Equal(expected, runtime.TotalSeconds, Precision);
    }

    // D290: a RAPID of C alone in the cylinder plane takes the rapid of C converted at r, 3600 deg/min at radius 30.
    [Fact]
    public void CylinderPlane_RapidOfCAlone_TakesTheRapidOfCAtTheRadius()
    {
        MachineConfig lathe = AnalyticMachines.PolarLathe();
        RuntimeAnalytic runtime = Run(lathe,
            ["SPINDLE_MODE:MAIN=AXIS", "RAPID X=30 Z=-10", "CYLINDER=30", "LINE Z=-10 C=0 F=6000"],
            "RAPID C=10");

        double atR = 30 * Math.PI / 180;
        Assert.Equal(FromRestToRest(10, 3600 * atR / 60, 100 * atR), runtime.TotalSeconds, Precision);
    }

    // D290: under CYLINDER=0 the C limit is not estimated: LINE C=10 moves at F, 6000 mm/min, and the report counts it
    // apart from the motions through the centre of the polar plane.
    [Fact]
    public void CylinderPlane_RadiusZero_IsCountedApartFromThePolarCentre()
    {
        MachineConfig lathe = AnalyticMachines.PolarLathe();
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(lathe));
        string report = AnalyticRuns.Run(
            Program(["SPINDLE_MODE:MAIN=AXIS", "RAPID X=30 Z=-10", "CYLINDER=0", "LINE Z=-10 C=0 F=6000"],
                "LINE C=10"),
            lathe, runtime);

        Assert.Equal(0.1, runtime.TotalSeconds, Precision);
        Assert.Contains("C clamp at an unknown cylinder radius not estimated: 1 motions of C in the cylinder plane",
            report, StringComparison.Ordinal);
        Assert.DoesNotContain("C clamp near centre", report, StringComparison.Ordinal);
    }

    // D290: under CYLINDER=r the rotary values convert at r: on the circumference of radius 30, LINE C=10 at F=6000
    // is limited by 1200 deg/min of C, 1200 * 30 * pi / 180 mm/min, and accelerates with 100 * 30 * pi / 180 mm/s^2.
    [Fact]
    public void CylinderPlane_MotionOfC_ConvertsTheRotaryValuesAtTheRadius()
    {
        MachineConfig lathe = AnalyticMachines.PolarLathe();
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(lathe));
        AnalyticRuns.Run(
            Program(["SPINDLE_MODE:MAIN=AXIS", "RAPID X=30 Z=-10", "CYLINDER=30", "LINE Z=-10 C=0 F=6000"],
                "LINE C=10"),
            lathe, runtime);

        double atR = 30 * Math.PI / 180;
        Assert.Equal(FromRestToRest(10, 1200 * atR / 60, 100 * atR), runtime.TotalSeconds, Precision);
    }

    // D350: [dynamics] path_mode is only the start value; PATH_MODE=CONTINUOUS in the program carries the corner speed
    // of 3000 mm/min (50 mm/s) through the corner on a machine that starts at exact stop. LINE X=100 brakes to 50 mm/s
    // in the corner, 1.0625 s as above; LINE Y=100 accelerates with the 500 mm/s^2 of Y from 50 mm/s in 0.1 s over
    // 7.5 mm and brakes to rest before the PATH_MODE block in 0.2 s over 10 mm, 82.5 mm at 100 mm/s: 1.125 s. Under
    // EXACT_STOP the next two lines take 1.1 s and 1.2 s. The report counts the corners in each mode and says that the
    // machine has no template to write the mode the program asks for.
    [Fact]
    public void PathMode_ProgramSwitchesToContinuous_TheStateRulesTheEstimate()
    {
        var dynamics = new DynamicsConfig { BlockTime = 0.001m, PathMode = PathMode.ExactStop, CornerSpeed = 3000m };
        MachineConfig machine = AnalyticMachines.Mill(dynamics);
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(machine));
        string report = AnalyticRuns.Run(
            Program(["RAPID X=0 Y=0 Z=0", "PATH_MODE=CONTINUOUS"], "LINE X=100 F=6000", "LINE Y=100",
                "PATH_MODE=EXACT_STOP", "LINE X=0", "LINE Y=0"),
            machine, runtime);

        Assert.Equal(1.0625 + 1.125 + 1.1 + 1.2, runtime.TotalSeconds, Precision);
        Assert.Contains("path_mode exact_stop (the start value of PATH_MODE, D350)", report, StringComparison.Ordinal);
        Assert.Contains("Corners: 1 timed under CONTINUOUS, 1 under EXACT_STOP; the program changes PATH_MODE "
            + "(D350).", report, StringComparison.Ordinal);
        Assert.Contains("The machine has no [transform] template for PATH_MODE=CONTINUOUS: 1 corners are timed in a "
            + "mode it cannot express (D350).", report, StringComparison.Ordinal);
    }

    // D350: PATH_MODE=EXACT_STOP brakes every block on a machine whose start value is continuous: 2.2 s.
    [Fact]
    public void PathMode_ProgramSwitchesToExactStop_BrakesInTheCorner()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)),
            ["RAPID X=0 Y=0 Z=0", "PATH_MODE=EXACT_STOP"],
            "LINE X=100 F=6000",
            "LINE X=200");

        Assert.Equal(2.2, runtime.TotalSeconds, Precision);
    }

    // D286: under CONTINUOUS the path rests at every block without path motion that the control executes, COOLANT
    // here: the two lines take 1.1 s each, as under exact stop.
    [Fact]
    public void RestPoints_CoolantBlockBetweenTwoLines_IsARestPoint()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)),
            "LINE X=100 F=6000",
            "COOLANT=ON",
            "LINE X=200");

        Assert.Equal(2.2, runtime.TotalSeconds, Precision);
    }

    // D286: a variable computation, a comment and a JUMP are transparent: the corner speed carries through, 2.125 s.
    [Fact]
    public void RestPoints_VariableAndJumpBetweenTwoLines_AreTransparentCorners()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)),
            "LINE X=100 F=6000",
            "VAR:Q1=5 ; a comment",
            "JUMP=GO",
            "LABEL=GO",
            "LINE X=200");

        Assert.Equal(2.125, runtime.TotalSeconds, Precision);
    }

    // D286: a direction reversal is a rest point: LINE X=100 and back to X=0 take 1.1 s each.
    [Fact]
    public void RestPoints_DirectionReversal_IsARestPoint()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)),
            "LINE X=100 F=6000",
            "LINE X=0");

        Assert.Equal(2.2, runtime.TotalSeconds, Precision);
    }

    // D286 with VM 3.2: the direction at the end of an ARC is its tangent. The quarter circle CCW from X=10 Y=0 to
    // X=0 Y=10 about the origin ends travelling in -X: a LINE on to X=-10 carries the corner speed (faster than the
    // same program under exact stop), a LINE back to X=10 reverses and rests (as fast as under exact stop).
    [Theory]
    [InlineData("LINE X=-10", false)]
    [InlineData("LINE X=10", true)]
    public void RestPoints_LineAfterAnArc_RestsOnlyWhereItReversesTheTangent(string line, bool rests)
    {
        string[] setUp = ["RAPID X=10 Y=0 Z=0"];
        string[] blocks = ["ARC=CCW X=0 Y=10 CENTER:X=0 CENTER:Y=0 F=6000", line];
        double continuous = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)), setUp, blocks)
            .TotalSeconds;
        double exactStop = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop), setUp, blocks).TotalSeconds;

        Assert.Equal(rests, Math.Abs(continuous - exactStop) < 1e-9);
    }

    // D286: the end of a RAPID is a rest point on a Fanuc-family machine (G00 in-position check), the test mill: RAPID
    // X=100 and LINE X=200 take 1.1 s each.
    [Fact]
    public void RestPoints_EndOfARapidOnFanuc_IsARestPoint()
    {
        RuntimeAnalytic runtime = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)),
            "RAPID X=100",
            "LINE X=200 F=6000");

        Assert.Equal(2.2, runtime.TotalSeconds, Precision);
    }

    // D286: on another family the end of a RAPID is a corner, 2.125 s; and [dynamics] rapid_exact_stop overrides the
    // family default either way.
    [Theory]
    [InlineData(Controller.Siemens, null, 2.125)]
    [InlineData(Controller.Heidenhain, true, 2.2)]
    [InlineData(Controller.Fanuc, false, 2.125)]
    public void RestPoints_EndOfARapid_FollowsTheFamilyAndRapidExactStop(Controller family, bool? rapidExactStop,
        double seconds)
    {
        MachineConfig mill = AnalyticMachines.Mill(
            AnalyticMachines.Continuous(3000m) with { RapidExactStop = rapidExactStop });
        MachineConfig machine = mill with { Machine = mill.Machine with { Controller = family } };

        RuntimeAnalytic runtime = Run(machine, "RAPID X=100", "LINE X=200 F=6000");

        Assert.Equal(seconds, runtime.TotalSeconds, Precision);
    }

    // D286 with D287: HOME and RETRACT move at the rapid rate, and on a Fanuc-family machine they rest at their end
    // like a RAPID: RETRACT=50 (0.6 s) and LINE X=100 after it take as long under CONTINUOUS as under exact stop.
    [Fact]
    public void RestPoints_EndOfARetractOnFanuc_IsARestPoint()
    {
        string[] blocks = ["RETRACT=50", "LINE X=100 F=6000"];
        double continuous = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)), blocks).TotalSeconds;
        double exactStop = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop), blocks).TotalSeconds;

        Assert.Equal(0.6 + 1.1, continuous, Precision);
        Assert.Equal(exactStop, continuous, Precision);
    }

    // D286 with VM 3.2: a half circle has a sense its geometry leaves open, and its CW or CCW gives it. ARC=CCW from
    // X=10 Y=0 to X=-10 Y=0 about the origin ends travelling in -Y; a LINE on to Y=-10 continues, one back to Y=10
    // reverses and rests.
    [Theory]
    [InlineData("LINE Y=-10", false)]
    [InlineData("LINE Y=10", true)]
    public void RestPoints_LineAfterAHalfCircle_FollowsItsDirection(string line, bool rests)
    {
        string[] setUp = ["RAPID X=10 Y=0 Z=0"];
        string[] blocks = ["ARC=CCW X=-10 Y=0 CENTER:X=0 CENTER:Y=0 F=6000", line];
        double continuous = Run(AnalyticMachines.Mill(AnalyticMachines.Continuous(3000m)), setUp, blocks)
            .TotalSeconds;
        double exactStop = Run(AnalyticMachines.Mill(AnalyticMachines.ExactStop), setUp, blocks).TotalSeconds;

        Assert.Equal(rests, Math.Abs(continuous - exactStop) < 1e-9);
    }

    // D286: the report names the rest points and what the end of a RAPID is on the machine.
    [Fact]
    public void Report_Profile_NamesTheRestPoints()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(AnalyticMachines.Mill(AnalyticMachines.Continuous(0m))));
        string report = AnalyticRuns.Run(Program(["RAPID X=0 Y=0 Z=0"], "LINE X=100 F=6000"),
            AnalyticMachines.Mill(AnalyticMachines.Continuous(0m)), runtime);

        Assert.Contains("Rest points under CONTINUOUS (D286): DWELL, a spindle start or stop, TOOL_BEGIN and TOOL_END, "
            + "STOP, program and file events, a motion not timed, a direction reversal, CYCLE_DWELL, a cycle call not "
            + "timed, every SYNC and every block without path motion except comments, variable computations, LABEL, "
            + "JUMP and CALL; the end of a RAPID, HOME or RETRACT is a rest point (Fanuc family, G00 in-position "
            + "check).", report,
            StringComparison.Ordinal);
    }

    // D357: on Fanuc a [cycles] key left out is 0, and the runtime report names the key it defaulted, a report line and
    // no diagnostic.
    [Fact]
    public void Report_PeckOnFanucWithoutPeckClearance_NamesTheKeyItDefaulted()
    {
        MachineConfig machine = AnalyticMachines.Mill(AnalyticMachines.ExactStop);
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(machine));
        string report = AnalyticRuns.Run(
            Program(["RAPID X=0 Y=0 Z=10"],
                "CYCLE=PECK DEPTH=-10 CLEARANCE=2 PECK=4 CYCLE_F=600 CYCLE_RETRACT=CLEARANCE",
                "CYCLE_CALL X=10",
                "CYCLE=OFF"),
            machine, runtime);

        Assert.Contains("[cycles] peck_clearance is not in the machine file: the expanded cycles took 0 mm, the "
            + "default of the family (D357).", report, StringComparison.Ordinal);
        Assert.DoesNotContain("chip_break_lift", report, StringComparison.Ordinal);
    }

    // D323: where rpm_max and both speeds are known, a start adds accel_time x rpm_new / rpm_max, a stop accel_time x
    // rpm_old / rpm_max, a speed change while turning accel_time x |rpm_new - rpm_old| / rpm_max and a reversal
    // accel_time x (rpm_old + rpm_new) / rpm_max: with 2 s to 1000 rpm, a start to 500 (1 s), a change to 750
    // (0.5 s), a reversal at 750 (3 s) and a stop from 750 (1.5 s).
    [Fact]
    public void SpindleAccelTime_RpmMaxKnown_IsTheLinearRamp()
    {
        RuntimeAnalytic runtime = Run(SpindleMill(rpmMax: 1000m),
            [],
            "RPM=500 SPINDLE=CW",
            "RPM=750",
            "SPINDLE=CCW",
            "SPINDLE=OFF");

        Assert.Equal(1 + 0.5 + 3 + 1.5, runtime.TotalSeconds, Precision);
    }

    // D323: without rpm_max a start and a stop add the whole accel_time and a speed change adds nothing.
    [Fact]
    public void SpindleAccelTime_WithoutRpmMax_SpeedChangeAddsNothing()
    {
        RuntimeAnalytic runtime = Run(SpindleMill(rpmMax: null),
            [],
            "RPM=500 SPINDLE=CW",
            "RPM=1000",
            "SPINDLE=OFF");

        Assert.Equal(4.0, runtime.TotalSeconds, Precision);
    }

    // D323: a speed that is unknown, under CSS here, falls back to the whole accel_time per start and stop.
    [Fact]
    public void SpindleAccelTime_CssStart_TakesTheWholeAccelTime()
    {
        RuntimeAnalytic runtime = Run(SpindleMill(rpmMax: 1000m),
            [],
            "VC=100 RPM_MAX=800 CSS=ON SPINDLE=CW",
            "SPINDLE=OFF");

        Assert.Equal(4.0, runtime.TotalSeconds, Precision);
    }

    // VM 8, D103: 2.5D_FRAESEN without a machine file: the default machine has neither [dynamics] nor a rapid rate, so
    // the LINE and ARC moves are distance over feed and the five RAPIDs of known length and the four of unknown length
    // are not timed, which the report counts.
    [Fact]
    public void NotTimed_Fraesen25DWithoutMachineFile_CountsWhatTheEstimateCannotTime()
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(DefaultMachine.Create()));
        string report = AnalyticRuns.RunExample("2.5D_FRAESEN.ncx", runtime);

        Assert.Contains("Not timed: 4 motions of unknown length, 5 motions without a known feed or rapid rate, ",
            report, StringComparison.Ordinal);
    }

    // The seconds of a motion of the given length from rest to rest at the given speed and acceleration (VM 8, D64).
    private static double FromRestToRest(double length, double speed, double acceleration)
    {
        return MotionProfile.Seconds(length, speed, 0, 0, acceleration);
    }

    // The test mill whose spindle table TOOL gives accel_time 2 s and the rpm_max given (machine-config 5).
    private static MachineConfig SpindleMill(decimal? rpmMax)
    {
        MachineConfig mill = AnalyticMachines.Mill(AnalyticMachines.ExactStop, accelTime: 2m);
        return mill with
        {
            SpindleTables = new Dictionary<string, FunctionTable>
            {
                ["TOOL"] = new FunctionTable { AccelTime = 2m, RpmMax = rpmMax },
            },
        };
    }

    // A program of the given blocks after UNITS=MM and the RAPID from the unknown start position.
    private static RuntimeAnalytic Run(MachineConfig machine, params string[] blocks)
    {
        return Run(machine, ["RAPID X=0 Y=0 Z=0"], blocks);
    }

    // A program of the given blocks after UNITS=MM and the blocks that set it up.
    private static RuntimeAnalytic Run(MachineConfig machine, string[] setUp, params string[] blocks)
    {
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(machine));
        AnalyticRuns.Run(Program(setUp, blocks), machine, runtime);
        return runtime;
    }

    private static string Program(string[] setUp, params string[] blocks)
    {
        var lines = new List<string> { "UNITS=MM" };
        lines.AddRange(setUp);
        lines.AddRange(blocks);
        return CliHarness.OneProgram(lines.ToArray());
    }

    // Keeps the MOTION events of a run, so that a test can ask SpindleSpeed for the rpm of one motion where the
    // runtime estimate itself never asks (a motion at the rapid rate is timed from the rapid rate, virtual machine 8).
    private sealed class MotionCollector : IAnalytic
    {
        public List<MotionEvent> Motions { get; } = [];

        public BlockRange Range => BlockRange.Whole;

        public void On(VmEvent vmEvent)
        {
            if (vmEvent is MotionEvent motion)
            {
                Motions.Add(motion);
            }
        }

        public string Report()
        {
            return string.Empty;
        }
    }
}
