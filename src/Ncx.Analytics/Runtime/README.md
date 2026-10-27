# Runtime

The runtime estimate of virtual machine 8 and D64 (implementation 14, P4-02): per motion a trapezoidal velocity profile from the machine file, the dwells, the plunges and dwells of the expanded cycles, the spindle starts and stops; totals per tool, per `SECTION` and per channel. It is an estimate, and its report says so and lists every value of the machine file it used, so that the file is corrected rather than the code.

Start with `RuntimeAnalytic.cs`, the listener with its totals (`RuntimeAnalytic.Report.cs` writes them), then `RuntimeEstimator.cs`, the model as a stream: every event in, every time charged out (`TimedStep.cs`), with one motion kept back (`PendingMotion.cs`) whose exit speed waits for the next motion while the PATH_MODE state is CONTINUOUS (D350), and the rest points of D286 where it stops. The pieces it uses:

- `MotionProfile.cs`: the time of one motion under the trapezoidal profile, deceleration equal to acceleration (D64).
- `MovingAxes.cs` with `AxisLimits.cs`: the axes a motion moves (on an arc both plane axes, a full circle included, virtual machine 3.2), and the smallest rapid, max_feed and acceleration among them (virtual machine 8); rotary travel belongs to the kinematics module (virtual machine 9), except in the polar and cylinder plane, where the C word is a length (D102) and its deg/min convert at the cylinder radius or at L, the closest approach to the centre of the polar plane (`PolarApproach.cs`, D290).
- `PathDirection.cs` and `ArcSense.cs`: the direction of travel at the ends of a motion, for the direction reversal, a rest point of D286.
- `SpindleRamp.cs`: the seconds of a spindle start, stop, speed change or reversal, linear to rpm_max (D323).
- `CommandedFeed.cs` and `SpindleSpeed.cs`: the feed in mm/min, per minute or per revolution times the rpm, under CSS from VC and the X radius capped by RPM_MAX.
- `MachineDynamics.cs`: `[[axis]]` rapid, max_feed, acceleration, `[dynamics]` block_time, the start value path_mode, corner_speed, rapid_exact_stop (machine-config 4), accel_time and rpm_max of the spindle tables (machine-config 5), and the tables of the report that list them, the linear and the rotary axes apart; without `[dynamics]` the estimate is distance over feed.

Never here: a value of a machine in the code; every number comes from the machine file.
