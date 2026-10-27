using Ncx.Config.Templates;
using Ncx.Core.Expressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The Fanuc variables a program uses itself, as VAR or in an expression, which no counter of [variables]
/// repeat_counters may be (D280; machine-config 7). A V name is #n (D33), a name of [variables] map the variable of
/// its template; any other name is left out here, since FanucExpressions reports it where it is written.
/// </summary>
internal sealed class FanucRepeatVariables
{
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private readonly MachineConfig _machine;
    private readonly TemplateSet _templates;

    /// <summary>
    /// The variables of a program for a machine.
    /// </summary>
    /// <param name="machine">The machine, whose [variables] map names the variables of the other families.</param>
    /// <param name="templates">The templates of the machine.</param>
    public FanucRepeatVariables(MachineConfig machine, TemplateSet templates)
    {
        _machine = machine;
        _templates = templates;
    }

    /// <summary>
    /// True where the program uses a Fanuc variable itself.
    /// </summary>
    /// <param name="variable">The variable as the machine file writes it, "#149".</param>
    public bool Uses(string variable)
    {
        return _used.Contains(variable.Trim());
    }

    /// <summary>
    /// Adds the variables of a block: the VAR words and every variable of an expression.
    /// </summary>
    public void Add(Block block)
    {
        foreach (Word word in block.Words)
        {
            if (word.Key == "VAR" && word.Addr is string name && FanucVariableOf(name) is string assigned)
            {
                _used.Add(assigned);
            }

            if (word.Value is ExprValue { Tree: ExprNode tree })
            {
                Add(tree);
            }
        }
    }

    private void Add(ExprNode node)
    {
        switch (node)
        {
            case VariableNode { Index: null } named when FanucVariableOf(named.Name) is string used:
                _used.Add(used);
                break;
            case UnaryNode unary:
                Add(unary.Operand);
                break;
            case ParenthesesNode parentheses:
                Add(parentheses.Inner);
                break;
            case BinaryNode binary:
                Add(binary.Left);
                Add(binary.Right);
                break;
            case CallNode call:
                foreach (ExprNode argument in call.Arguments)
                {
                    Add(argument);
                }

                break;
        }
    }

    // The Fanuc variable of a V name or a name of [variables] map without a diagnostic; null for any other name. V0
    // is #0, never "#".
    private string? FanucVariableOf(string name)
    {
        if (name.Length > 1 && name[0] == 'V' && name.Substring(1).All(char.IsAsciiDigit))
        {
            string number = name.Substring(1).TrimStart('0');
            return "#" + (number.Length == 0 ? "0" : number);
        }

        int digits = name.Length;
        while (digits > 0 && char.IsAsciiDigit(name[digits - 1]))
        {
            digits--;
        }

        if (digits == name.Length || !_machine.Variables.Map.TryGetValue(name.Substring(0, digits), out string? native))
        {
            return null;
        }

        var quiet = new Diagnostics("repeat counters");
        Template template = _templates.For(native) ?? new Template(native, 1, quiet);
        var values = new TemplateValues();
        values.Set("index", IndexOf(name, digits));
        return template.Render(values, new Block { Line = 0, Words = [] }, quiet);
    }

    // The digits at the end of a name, Q49 gives 49, read digit by digit.
    private static decimal IndexOf(string name, int first)
    {
        decimal index = 0;
        for (int position = first; position < name.Length; position++)
        {
            index = (index * 10) + (name[position] - '0');
        }

        return index;
    }
}
