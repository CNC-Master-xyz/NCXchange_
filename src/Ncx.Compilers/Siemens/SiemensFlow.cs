using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The flow of a SINUMERIK block (controllers siemens.md 8, 12; controller-mapping 1, 6; language 4.9, 4.13): labels as
/// NAME:, JUMP as GOTOF or GOTOB with IF, JUMP=END to a label before the program end, the structured loops never
/// reconstructed; CALL as NAME, NAME(1,2) with the PROC parameters, NAME P3, EXTCALL for a program outside the file;
/// REPEAT label P=; RETURN as sub_end; VAR as R1= or the declared name.
/// </summary>
internal static partial class SiemensFlow
{
    /// <summary>
    /// LABEL=name as NAME: in a line of its own before the lines of the block, which a jump enters there (controllers
    /// siemens.md 8; controller-mapping 6, LABEL). A jump arrives at the label with what the control has active at the
    /// jump, so the target state is unknown after it.
    /// </summary>
    public static void WriteLabel(SiemensBlock write)
    {
        if (write.Block.Find("LABEL") is not Word label)
        {
            return;
        }

        write.Written(label);
        write.Write(SiemensFile.LabelOf(label.Value) + ":");

        // The STATIC walk does not follow JUMP (virtual machine 1), so the target state at the label is that of the
        // block before it, and a jump, from after the label on a later pass too, arrives with that of its own block:
        // every modal code, the feed that a CYCLE_F left (D29), the edge, the datum and the master spindle stand again
        // at their next use (language 2 rule 2; controller-mapping 5, CYCLE_F; controllers siemens.md 5, 12 rule 3).
        // Every jump ends the modal call before it (WriteJump, WriteRepeat, the calls, RAW whose text may jump), so
        // the modal call is off after the label where it is off before it, and unknown otherwise: the next call at a
        // position arms it again, the next motion ends it (siemens 7; controller-mapping 5, CYCLE_CALL). The frame
        // that a RAW line of the file may change is unknown as well, and the next block that ends it writes the end
        // (siemens 4; SiemensFrames.ForgetRawFrames).
        bool modalCallOff = SiemensCycles.ModalCallOff(write);
        write.MakeTargetUnknown();
        SiemensFrames.ForgetRawFrames(write);
        if (modalCallOff)
        {
            SiemensCycles.ModalCallEnded(write);
        }
    }

    /// <summary>
    /// VAR:name=value as NAME=value, the assignments of the block in one line (controllers siemens.md 8;
    /// controller-mapping 6, VAR; language 4.9).
    /// </summary>
    public static void WriteVariables(SiemensBlock write)
    {
        var assignments = new List<string>();
        foreach (Word word in write.Block.Words)
        {
            if (word.Key != "VAR" || word.Addr is not string variable)
            {
                continue;
            }

            write.Written(word);
            string? name = SiemensExpressions.VariableName(write, variable);
            string? value = word.Value switch
            {
                StringValue text => "\"" + text.Content + "\"",
                _ => SiemensExpressions.ValueText(write, word.Value, variable),
            };
            if (name is not null && value is not null)
            {
                assignments.Add(name + "=" + value);
            }
        }

        if (assignments.Count > 0)
        {
            write.Write(string.Join(" ", assignments));
        }
    }

    /// <summary>
    /// CALL, JUMP, REPEAT and RETURN in lines of their own after the block (controllers siemens.md 8; language 4.9).
    /// </summary>
    public static void Write(SiemensBlock write, string fileStem)
    {
        WriteCall(write, fileStem);
        WriteJump(write);
        WriteRepeat(write);
        WriteReturn(write);
    }

    // NAME, NAME(1, , 3) with the parameters of its PROC, NAME P3; IF around it as IF ... ENDIF; a program outside the
    // file with EXTCALL (controllers siemens.md 8; controller-mapping 6, CALL, TIMES, ARG). MCALL alone stands before
    // the call where a modal call may be armed, a CYCLE_CALL of the same block included, so that no block with a
    // position of the called program calls it and every subprogram starts without one (siemens 7).
    private static void WriteCall(SiemensBlock write, string fileStem)
    {
        if (write.Block.Find("CALL") is not Word call)
        {
            return;
        }

        write.Written(call);
        write.Written("ARG");
        write.Written("TIMES");
        string? condition = Condition(write);
        if (write.File.SubNamed(call.Value) is Section sub)
        {
            string name = write.File.UnitNameOf(sub, fileStem, write);
            string? text = CallText(write, sub, name);
            if (text is null)
            {
                return;
            }

            SiemensCycles.EndModalCall(write);
            WriteConditional(write, condition, text);
            return;
        }

        // EXTCALL("NAME") calls a program outside the file (controller-mapping 6, CALL).
        if (write.Block.Has("ARG") || write.Block.Has("TIMES"))
        {
            write.Error(DiagnosticCodes.SiemensExternalCallNotWritable,
                $"{call.ToCanonical()} calls a program outside the file with arguments or a repeat count, which "
                + "EXTCALL does not take (controllers siemens.md 8; controller-mapping 6, CALL).");
            return;
        }

        string target = call.Value is StringValue external ? external.Content : call.Value.ToCanonical();
        SiemensCycles.EndModalCall(write);
        WriteConditional(write, condition, "EXTCALL(\"" + target + "\")");
    }

    // The call of a subprogram of the file: its arguments by the positions of its PROC parameters, empty where the
    // call gives none, and P with TIMES (controllers siemens.md 8).
    private static string? CallText(SiemensBlock write, Section sub, string name)
    {
        IReadOnlyList<string> parameters = write.File.ParametersOf(sub);
        string text = name;
        if (parameters.Count > 0)
        {
            var values = new List<string>();
            foreach (string parameter in parameters)
            {
                Word? argument = write.Block.Find("ARG", parameter);
                string? value = argument is null ? "" : SiemensExpressions.ValueText(write, argument.Value, "ARG");
                if (value is null)
                {
                    return null;
                }

                values.Add(value);
            }

            text += "(" + string.Join(",", values) + ")";
        }

        if (write.Block.Find("TIMES") is Word times)
        {
            if (SiemensExpressions.ValueText(write, times.Value, "TIMES") is not string count)
            {
                return null;
            }

            text += times.Value is ExprValue ? " P=" + count : " P" + count;
        }

        return text;
    }

    // JUMP=label as GOTOF where the label stands after the jump, GOTOB where it stands before or in the block of the
    // jump, which a jump to its own label runs again; JUMP=END to the label before the program end; IF in front where
    // the block has one (controllers siemens.md 8: GOTOF forward, GOTOB backward; controller-mapping 1, JUMP=END, and
    // 6, JUMP + IF; language 4.9). MCALL alone stands before a jump to a label where a modal call may be armed, so
    // that every jump arrives at its label without one (siemens 7; WriteLabel).
    private static void WriteJump(SiemensBlock write)
    {
        if (write.Block.Find("JUMP") is not Word jump)
        {
            return;
        }

        write.Written(jump);
        Section? section = write.Step.Section;
        bool toEnd = jump.Value.ToCanonical() == "END";
        if (toEnd && section is { Kind: SectionKind.Sub })
        {
            WriteJumpToEndInSubprogram(write, section, Condition(write));
            return;
        }

        string? condition = Condition(write);
        string text;
        if (toEnd && section is not null)
        {
            text = "GOTOF " + write.File.EndLabelOf(section);
        }
        else
        {
            Block? target = section is null ? null : write.File.LabelBlock(section, jump.Value.ToCanonical());
            string direction = target is not null && target.Line <= write.Block.Line ? "GOTOB " : "GOTOF ";
            text = direction + SiemensFile.LabelOf(jump.Value);
            SiemensCycles.EndModalCall(write);
        }

        write.Write(condition is null ? text : "IF " + condition + " " + text);
    }

    // JUMP=END in a subprogram "continues at the PROGRAM=END of the current program" (language 4.9), ending the
    // program from the call (virtual machine 3.6); an M30 in a subprogram does not do that, since M17 and M30 are
    // treated as equal in the NC language and both return to the block after the call. The parameterized return does
    // it: RET("<end label>",,$P_STACK) continues at that label in the program $P_STACK levels up, which is level 0,
    // the main program, from any depth (controllers siemens.md 1, 8; controller-mapping 1, JUMP=END, and 6, RET; D344).
    // A conditional JUMP=END stands under a jump over it, as a conditional CALL does.
    private static void WriteJumpToEndInSubprogram(SiemensBlock write, Section sub, string? condition)
    {
        write.Written("IF");
        if (write.File.EndLabelReachedFrom(sub) is not string label)
        {
            write.Error(DiagnosticCodes.SiemensJumpToEndInSubprogram,
                $"JUMP=END in the subprogram {sub.Name ?? sub.Number?.ToString(CultureInfo.InvariantCulture)} ends the "
                + "program from the call (language 4.9; virtual machine 3.6), and the file gives no one program whose "
                + "end label the return RET(\"label\",,$P_STACK) can name (controller-mapping 1, JUMP=END; D344).");
            return;
        }

        // D381: RET stands in a SAVE subprogram as in any other, plain and parameterized. The p.523 sentence is about
        // RET's G64 benefit, which SAVE takes away ("Voraussetzung dafür ist, dass das Unterprogramm kein SAVE-Attribut
        // besitzt", 840D/810D Arbeitsvorbereitung 2006 p.92), not a ban: Siemens ends PROC ... SAVE DISPLOF with RET
        // (NC-Programmierung SW4.92 p.518), and a return over several levels evaluates the SAVE of each level (p.526).
        SiemensCycles.EndModalCall(write);
        WriteConditional(write, condition, "RET(\"" + label + "\",,$P_STACK)");
    }

    // REPEAT=label TIMES=n as REPEAT LABEL P=n, the blocks from the label to this one n more times; without TIMES
    // once more, which a Sinumerik REPEAT without P does too (controllers siemens.md 8; controller-mapping 6,
    // REPEAT + TIMES; D212 for that default alone). MCALL alone stands before it where a modal call may be armed, as
    // before a jump (WriteJump).
    // TODO(question): which text this compiler writes for a REPEAT without TIMES, P=1 or no P, is not in D212's
    // answer, which names no compiler form, so it waits on the maintainer; no P stays until then, the form of main.
    private static void WriteRepeat(SiemensBlock write)
    {
        if (write.Block.Find("REPEAT") is not Word repeat)
        {
            return;
        }

        write.Written(repeat);
        write.Written("TIMES");
        string text = "REPEAT " + SiemensFile.LabelOf(repeat.Value);
        if (write.Block.Find("TIMES") is Word times)
        {
            if (SiemensExpressions.ValueText(write, times.Value, "TIMES") is not string count)
            {
                return;
            }

            text += " P=" + count;
        }

        SiemensCycles.EndModalCall(write);
        write.Write(text);
    }

    // RETURN in a subprogram is sub_end, RET or M17 (machine-config 2); outside a subprogram it is a WARNING of the
    // virtual machine and ends the program as JUMP=END does (language 4.9, RETURN).
    private static void WriteReturn(SiemensBlock write)
    {
        if (write.Block.Find("RETURN") is not Word ret)
        {
            return;
        }

        write.Written(ret);
        if (write.Step.Section is Section { Kind: SectionKind.Program } program)
        {
            write.Write("GOTOF " + write.File.EndLabelOf(program));
            return;
        }

        if (SiemensProgramFrame.SubEnd(write, write.Step.Section) is string text)
        {
            write.Write(text);
        }
    }

    // The IF of the block as the condition of its JUMP or CALL (language 4.9, IF).
    private static string? Condition(SiemensBlock write)
    {
        if (write.Block.Find("IF") is not Word condition)
        {
            return null;
        }

        write.Written(condition);
        return condition.Value is ExprValue expression
            ? SiemensExpressions.Text(write, expression)
            : condition.Value.ToCanonical();
    }

    // A call under IF: a jump over the call where the condition does not hold, IF NOT (cond) GOTOF, the call, the
    // label; the Siemens compiler writes IF ... GOTOF chains and never reconstructs the structured forms
    // (controller-mapping 6, structured loops; siemens 8). The label is reached from the call and from the jump, so
    // the block after the call meets what the control has active at the jump (SiemensFile.MeetJumpsOverCalls).
    private static void WriteConditional(SiemensBlock write, string? condition, string text)
    {
        if (condition is null)
        {
            write.Write(text);
            return;
        }

        string label = "SKIP_CALL_" + write.Block.Line.ToString(CultureInfo.InvariantCulture);
        write.Write("IF NOT (" + condition + ") GOTOF " + label);
        write.File.JumpOverCall(write.Target);
        write.Write(text);
        write.Write(label + ":");
    }

    /// <summary>
    /// The PROC line of a subprogram with the parameters its calls give, REAL each: PROC NAME(REAL DEPTH, REAL
    /// COUNT) (controllers siemens.md 8, 12 rule 1; controller-mapping 6, SUB=BEGIN and CALL + ARG). Where the SUB
    /// section carries a RAW PROC line of its own subprogram with those parameters, as the reader keeps PROC NAME SAVE,
    /// that line stands in place of it, so that the unit has one PROC line and its attributes SAVE, SBLOF and DISPLOF
    /// and the defaults of its parameters stay (840D sl NC-Programmierung SW4.92, p.510-512); a RAW PROC line of
    /// another name or with other parameters is the ERROR CMP598.
    /// </summary>
    public static string ProcLine(SiemensBlock write, Section sub, string name)
    {
        // A parameter is a variable of the subprogram and takes its SINUMERIK name (language 4.9, ARG).
        var names = new List<string>();
        foreach (string parameter in write.File.ParametersOf(sub))
        {
            names.Add(SiemensExpressions.VariableName(write, parameter) ?? parameter);
        }

        if (write.File.ProcRawOf(sub) is Block rawBlock && rawBlock.Find("RAW")?.Value is StringValue raw)
        {
            if (SiemensProcLine.Parse(raw.Content) is ProcLineParts { Readable: true } parts
                && string.Equals(parts.Name, name, StringComparison.OrdinalIgnoreCase)
                && RealParameterNames(parts.Parameters) is List<string> rawNames
                && rawNames.SequenceEqual(names, StringComparer.OrdinalIgnoreCase))
            {
                return raw.Content;
            }

            string given = names.Count == 0
                ? "no parameters"
                : "the REAL parameters " + string.Join(", ", names);
            write.Diagnostics.Error(rawBlock, DiagnosticCodes.SiemensProcLineNotOwn,
                $"The RAW line \"{raw.Content}\" of the subprogram {name} is a PROC line that does not name {name} "
                + $"with what its calls give, {given}: the unit carries one PROC line, and the compiler does not "
                + "guess which (controllers siemens.md 8, 12 rule 1; 840D sl NC-Programmierung SW4.92, p.510-512).");
        }

        if (names.Count == 0)
        {
            return "PROC " + name;
        }

        return "PROC " + name + "(" + string.Join(", ", names.Select(parameter => "REAL " + parameter)) + ")";
    }

    // The names of a PROC parameter list that declares REAL values only, a default allowed: REAL DEPTH=2, REAL W;
    // empty without a list; null for a VAR, another type or an array, which the compiler's calls and EXTERN REAL do
    // not give (controllers siemens.md 8).
    private static List<string>? RealParameterNames(string? parameters)
    {
        var names = new List<string>();
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return names;
        }

        foreach (string parameter in SiemensProcLine.SplitParameters(parameters))
        {
            Match match = RealParameter().Match(parameter);
            if (!match.Success)
            {
                return null;
            }

            names.Add(match.Groups["name"].Value);
        }

        return names;
    }

    /// <summary>
    /// DEF REAL name in the definition part of a unit, after its EXTERN lines and before its first executable block,
    /// for every variable the unit uses that the control does not have already: an R parameter, a name [variables]
    /// map covers, a parameter of the PROC, a name a RAW DEF of the unit declares and a name of [variables] declared
    /// are left out. A name more than one unit uses and declared does not name is an ERROR, since a DEF is local to
    /// its unit and the value would not carry over (controllers siemens.md 8, 12 rule 7; machine-config 7; D261).
    /// </summary>
    /// <param name="write">The block being written, the head of the unit.</param>
    /// <param name="unit">The program or subprogram.</param>
    public static void WriteDeclarations(SiemensBlock write, Section unit)
    {
        IReadOnlyList<string> parameters = write.File.ParametersOf(unit);
        foreach (string name in write.File.NamesUsedBy(unit))
        {
            if (SiemensExpressions.NeedsNoDeclaration(write, name) || parameters.Contains(name)
                || write.Machine.Variables.Declared.Contains(name) || write.File.IsDeclaredByRaw(unit, name))
            {
                continue;
            }

            if (write.File.IsUsedBySeveralUnits(name))
            {
                write.Error(DiagnosticCodes.SiemensVariableNotDeclarable,
                    $"The variable {name} is used by more than one unit of the file, and a SINUMERIK DEF is local to "
                    + $"its unit; name {name} in [variables] declared of the machine, so that the control declares it "
                    + "outside the program (machine-config 7; controllers siemens.md 8; D261).");
                continue;
            }

            write.Write("DEF REAL " + name);
        }
    }

    /// <summary>
    /// EXTERN NAME(REAL, REAL) for every subprogram with parameters that the blocks of a unit call (controllers
    /// siemens.md 8).
    /// </summary>
    public static void WriteExterns(SiemensBlock write, Section unit, string fileStem)
    {
        foreach (Section sub in write.File.CalledWithParameters(unit))
        {
            var types = new List<string>();
            for (int index = 0; index < write.File.ParametersOf(sub).Count; index++)
            {
                types.Add("REAL");
            }

            write.Write("EXTERN " + write.File.UnitNameOf(sub, fileStem, write) + "(" + string.Join(", ", types)
                + ")");
        }
    }

    // One parameter of a PROC line that is a REAL value, its default allowed (controllers siemens.md 8).
    [GeneratedRegex(@"^\s*REAL\s+(?<name>[A-Z_][A-Z0-9_]*)\s*(?:=[^,]*)?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RealParameter();
}
