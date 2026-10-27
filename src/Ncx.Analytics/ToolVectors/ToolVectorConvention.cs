using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.ToolVectors;

/// <summary>
/// The tool vector from the rotary axis positions until the kinematics module exists (implementation 14, P4-03; virtual
/// machine 8, 10): the tool axis is the normal of the WORKPLANE; a rotary axis named A, B or C turns that vector about
/// the machine X, Y or Z axis in the order the [[axis]] list gives. Its owner says what it turns (D325; machine-config
/// 4): a tool holder (turret, tool carrier, swivel head) makes it a head axis, which turns the tool; a work spindle or
/// a table makes it a table axis, which turns the workpiece, the same angle change with the opposite sign for the
/// vector of the tool relative to the workpiece; a tool spindle makes it the spindle's own rotation, which turns no
/// tool vector. Enough for the change between the start and the end of a motion, which is what the analytic reports; it
/// is not a pose (D24 keeps kinematics out of NCX).
/// </summary>
internal sealed class ToolVectorConvention
{
    private static readonly char[] s_digits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    private readonly List<ConventionAxis> _axes = [];

    /// <summary>
    /// The convention on one machine: its rotary axes named A, B or C, in the order of [[axis]].
    /// </summary>
    public ToolVectorConvention(MachineConfig machine)
    {
        // The machine's rotary axes as [[axis]] lists them, A, B and C about the machine X, Y and Z (P4-03). D325: the
        // letter without digits gives the machine axis it turns about (DIN 66217), so the C2 of a sub spindle turns
        // about Z; a table axis counts only while its owner holds the workpiece (language 4.10); an axis without owner
        // (the default machine of D103) stays a head axis, and the report header carries a WARNING naming it.
        foreach (AxisDef axis in machine.Axes)
        {
            string? about = axis.NcxName.TrimEnd(s_digits) switch
            {
                "A" => "X",
                "B" => "Y",
                "C" => "Z",
                _ => null,
            };
            if (axis.Kind != AxisKind.Rotary || about is null)
            {
                continue;
            }

            // A table axis belongs to a resource that holds the workpiece, a work spindle or a table (P4-03; D191 reads
            // table kinematics the same way); the axis of a tool spindle is its own rotation (D325).
            ResourceType? owner = axis.Owner is string id ? machine.FindResource(id)?.Type : null;
            _axes.Add(new ConventionAxis
            {
                NcxName = axis.NcxName,
                Id = axis.Id,
                About = about,
                Owner = axis.Owner,
                Table = owner is ResourceType.WorkSpindle or ResourceType.Table,
                SpindleRotation = owner == ResourceType.ToolSpindle,
            });
        }
    }

    /// <summary>
    /// The rotary axes the convention applies, in the order of [[axis]].
    /// </summary>
    public IReadOnlyList<ConventionAxis> Axes => _axes;

    /// <summary>
    /// The tool vector at the positions where a motion starts or ends: the normal of the WORKPLANE turned by every axis
    /// that applies under the workpiece holder, in the order of [[axis]]; null when such an axis is not known as an
    /// angle, because it is unknown or a length of the polar or cylinder plane (virtual machine 3.1, D102).
    /// </summary>
    /// <param name="workplane">The WORKPLANE of the block.</param>
    /// <param name="workpieceHolder">The workpiece holder of the block.</param>
    /// <param name="positions">The positions of the axes, From or To of the MOTION.</param>
    public ToolVector? VectorAt(Workplane workplane, string? workpieceHolder,
        IReadOnlyDictionary<string, AxisPosition> positions)
    {
        ToolVector vector = ToolVector.NormalOf(workplane);
        foreach (ConventionAxis axis in _axes)
        {
            if (!axis.AppliesWhile(workpieceHolder))
            {
                continue;
            }

            AxisPosition position = positions.TryGetValue(axis.NcxName, out AxisPosition known)
                ? known
                : AxisPosition.Unknown;
            if (!position.Known || MotionPositions.InTransformedPlane(position))
            {
                return null;
            }

            // A table axis turns the workpiece, so relative to the workpiece the tool turns the other way (P4-03).
            double degrees = (double)position.Value;
            vector = vector.Turned(axis.About, axis.Table ? -degrees : degrees);
        }

        return vector;
    }
}
