using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The O number of each program of a file (D257; controllers fanuc.md 1; language 4.1, NUMBER): NUMBER where it
/// stands; without it a NAME of digits, up to eight (D249); otherwise the lowest number from 1 that the output file
/// does not use, 8000 to 9999 left out, which the builder reserves, with the WARNING CMP310 that names it (D388 3).
/// </summary>
internal sealed class FanucProgramNumbers
{
    // O8000 to O9999 are the builder's (D257).
    private const int BuilderFirst = 8000;
    private const int BuilderLast = 9999;

    // Up to eight digits with the 8-digit number option (controllers fanuc.md 1, D249).
    private const int MostDigits = 8;

    private readonly Dictionary<Section, int> _generated = [];

    /// <summary>
    /// The numbers of a file whose programs have none to generate.
    /// </summary>
    public static FanucProgramNumbers None { get; } = new();

    /// <summary>
    /// The O number of a program; null where it has neither NUMBER nor a NAME of digits and the file gave it none.
    /// </summary>
    /// <param name="program">The program section.</param>
    public int? NumberOf(Section program)
    {
        if (program.Number is int number)
        {
            return number;
        }

        if (DigitsOf(program.Name) is int named)
        {
            return named;
        }

        return _generated.TryGetValue(program, out int generated) ? generated : null;
    }

    /// <summary>
    /// True where the compiler generated the number of the program, which the WARNING CMP310 names.
    /// </summary>
    public bool IsGenerated(Section program)
    {
        return program.Number is null && DigitsOf(program.Name) is null && _generated.ContainsKey(program);
    }

    /// <summary>
    /// The numbers of the programs of a file from the blocks of its walks: every number the file uses, the NUMBER of
    /// a program, a NAME of digits of a program or SUB and a CALL of a program by its number, is taken; each program
    /// without one takes the lowest free number, in the order of the file.
    /// </summary>
    /// <param name="steps">Every step of the run.</param>
    public static FanucProgramNumbers Of(IReadOnlyList<BlockStep> steps)
    {
        var numbers = new FanucProgramNumbers();
        var sections = new List<Section>();
        var used = new HashSet<int>();
        foreach (BlockStep step in steps)
        {
            if (step.Section is Section section && !sections.Contains(section))
            {
                sections.Add(section);
                if (section.Number is int number)
                {
                    used.Add(number);
                }
                else if (DigitsOf(section.Name) is int named)
                {
                    used.Add(named);
                }
            }

            if (step.Block.Find("CALL")?.Value is Value call)
            {
                string name = call is StringValue external ? external.Content : call.ToCanonical();
                if (DigitsOf(name.Length > 1 && name[0] == 'O' ? name.Substring(1) : name) is int called)
                {
                    used.Add(called);
                }
            }
        }

        sections.Sort((first, second) => first.FirstBlock.CompareTo(second.FirstBlock));
        int next = 1;
        foreach (Section section in sections)
        {
            if (section.Kind != SectionKind.Program || section.Number is not null || DigitsOf(section.Name) is not null)
            {
                continue;
            }

            while (used.Contains(next) || next is >= BuilderFirst and <= BuilderLast)
            {
                next++;
            }

            numbers._generated.Add(section, next);
            used.Add(next);
        }

        return numbers;
    }

    // A name of one to eight digits as its number (controllers fanuc.md 1, D249); null for any other name.
    private static int? DigitsOf(string? name)
    {
        return name is not null && name.Length is > 0 and <= MostDigits && name.All(char.IsAsciiDigit)
            ? int.Parse(name, NumberStyles.None, CultureInfo.InvariantCulture)
            : null;
    }
}
