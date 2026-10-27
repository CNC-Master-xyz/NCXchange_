namespace Ncx.Compilers;

/// <summary>
/// One plane axis of an arc for <see cref="ArcOnTheGrid"/> (D364), every value in the units the virtual machine keeps
/// (radius values, D28): the exact start, end and centre of the virtual machine, the step of the written end point and
/// the step of the written centre, each the output step of its address divided by the factor it is written with (a
/// diameter halves it), and whether the centre is written incrementally from the written start (Fanuc I J K,
/// CENTER:IX) or absolutely (CC, I=AC()).
/// </summary>
/// <param name="Start">The start of the arc on the axis.</param>
/// <param name="End">The end of the arc on the axis.</param>
/// <param name="Centre">The centre of the arc on the axis, at the precision of the virtual machine (D121).</param>
/// <param name="PointStep">The step of the written start and end point on the axis.</param>
/// <param name="CentreStep">The step of the written centre on the axis.</param>
/// <param name="Incremental">True when the centre is written as the offset from the written start.</param>
internal sealed record GridAxis(decimal Start, decimal End, decimal Centre, decimal PointStep, decimal CentreStep,
    bool Incremental);
