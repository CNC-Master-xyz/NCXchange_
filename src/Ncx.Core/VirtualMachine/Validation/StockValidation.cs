using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The blank of STOCK in virtual machine 5: its six coordinate words (VM423), its extent (VM424) and its place in the
/// program (VM425), rules of the frame family (FrameValidation.Family; language 4.2, D351).
/// </summary>
internal static class StockValidation
{
    /// <summary>
    /// STOCK gives the blank by its bounding box, X Y Z one corner and MAX:X MAX:Y MAX:Z the opposite one, and stands
    /// once per program, after the first ORIGIN and before the first motion verb (language 4.2, virtual machine 5,
    /// D351). A coordinate from an expression, which STATIC mode does not evaluate, is not compared (virtual machine
    /// 1). Called after the verb of every block, so the ORIGIN of the STOCK block counts and its motion does not.
    /// </summary>
    public static void CheckStock(BlockContext context, StockPlacement placement)
    {
        Block block = context.Block;
        if (block.Has("PROGRAM", null, "BEGIN"))
        {
            placement.StartProgram();
        }

        if (block.Has("ORIGIN"))
        {
            placement.OriginSeen = true;
        }

        if (block.Verb is Word stock && stock.Key == "STOCK")
        {
            CheckStockPlace(context, placement);
            CheckStockBox(context, stock);
            placement.StockSeen = true;
        }

        if (MotionRules.IsMotion(block))
        {
            placement.MotionSeen = true;
        }
    }

    // VM425: once per program, after the first ORIGIN, before the first motion verb (D351).
    private static void CheckStockPlace(BlockContext context, StockPlacement placement)
    {
        string misplaced;
        if (placement.StockSeen)
        {
            misplaced = "is the second STOCK of the program";
        }
        else if (!placement.OriginSeen)
        {
            misplaced = "stands before the first ORIGIN";
        }
        else if (placement.MotionSeen)
        {
            misplaced = "stands after the first motion verb";
        }
        else
        {
            return;
        }

        context.Diagnostics.Error(context.Block, DiagnosticCodes.StockMisplaced,
            $"This STOCK {misplaced}; the blank stands once per program, after the first ORIGIN and before the first "
            + "motion verb (language 4.2, virtual machine 5, D351).");
    }

    // VM423 for a missing coordinate word, VM424 for a side of no extent or a cylinder that is no circle about the
    // tool axis of the active WORKPLANE (D351).
    private static void CheckStockBox(BlockContext context, Word stock)
    {
        Block block = context.Block;
        var missing = new List<string>();
        var missingOpposite = new List<string>();
        var extent = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        foreach (string axis in new[] { "X", "Y", "Z" })
        {
            Word? corner = block.Find(axis);
            Word? opposite = block.Find("MAX", axis);
            if (corner is null)
            {
                missing.Add(axis);
            }

            if (opposite is null)
            {
                missingOpposite.Add("MAX:" + axis);
            }

            extent[axis] = NumberOf(corner) is decimal low && NumberOf(opposite) is decimal high ? high - low : null;
        }

        missing.AddRange(missingOpposite);
        if (missing.Count > 0)
        {
            context.Diagnostics.Error(block, DiagnosticCodes.StockWithoutItsCorners,
                $"{stock.ToCanonical()} lacks {string.Join(", ", missing)}; the blank is given by the six coordinate "
                + "words of its bounding box, X Y Z and MAX:X MAX:Y MAX:Z (language 4.2, virtual machine 5, D351).");
            return;
        }

        foreach (KeyValuePair<string, decimal?> side in extent)
        {
            if (side.Value is decimal length && length <= 0m)
            {
                context.Diagnostics.Error(block, DiagnosticCodes.StockExtentInvalid,
                    $"{stock.ToCanonical()} has no extent along {side.Key}: MAX:{side.Key} must lie beyond {side.Key} "
                    + "(language 4.2, virtual machine 5, D351).");
                return;
            }
        }

        string[] plane = AcrossTheToolAxis(context.State.Frame.Workplane);
        string first = plane[0];
        string second = plane[1];
        if (BlockContext.IdentOf(stock) == "CYLINDER"
            && extent[first] is decimal across && extent[second] is decimal along && across != along)
        {
            context.Diagnostics.Error(block, DiagnosticCodes.StockExtentInvalid,
                $"{stock.ToCanonical()} is {across} along {first} and {along} along {second}, so its footprint across "
                + "the tool axis is no circle; the cylinder is inscribed in a box with two equal sides about it "
                + "(language 4.2, virtual machine 5, D351).");
        }
    }

    // The two axes of the working plane, across the tool axis (language 4.2, WORKPLANE).
    private static string[] AcrossTheToolAxis(Workplane workplane)
    {
        return workplane switch
        {
            Workplane.ZX => ["Z", "X"],
            Workplane.YZ => ["Y", "Z"],
            _ => ["X", "Y"],
        };
    }

    // A literal number; null for an expression, which STATIC mode does not evaluate, and for a missing word.
    private static decimal? NumberOf(Word? word)
    {
        return word?.Value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue value => value.Number,
            _ => null,
        };
    }
}
