using Tomlyn.Model;
using Tomlyn.Serialization;
using Tomlyn.Syntax;
using Tomlyn.Text;

namespace Ncx.Config;

/// <summary>
/// Reads a TOML file into tables as TOML 1.0 defines them, with the line of every key. It replaces the table reader of
/// Tomlyn 2.10.1, which keeps only the last run of an array of tables: a [[axis]] after another table started a new
/// array and dropped the [[axis]] entries before it, X, Y and Z of a mill, without a word.
/// </summary>
internal sealed class TomlTableConverter : TomlConverter<TomlTable>
{
    /// <summary>
    /// The one instance the loaders pass in their options.
    /// </summary>
    public static TomlTableConverter Instance { get; } = new();

    /// <summary>
    /// Reads the table the reader stands on.
    /// </summary>
    public override TomlTable? Read(TomlReader reader)
    {
        return ReadTable(reader);
    }

    /// <summary>
    /// The loaders only read; writing is not supported.
    /// </summary>
    public override void Write(TomlWriter writer, TomlTable value)
    {
        throw new NotSupportedException("The machine file loaders do not write TOML.");
    }

    // A table of the file, [header] or { inline }; an inline table has a span, a table of headers has none.
    private static TomlTable ReadTable(TomlReader reader)
    {
        var table = new TomlTable(inline: reader.CurrentSpan is not null);
        ReadTableInto(reader, table);
        return table;
    }

    // The keys of a table with the line of each key. A table defined again in the reader's events, [a] then [a.b],
    // takes the new keys; an array of tables that comes again after other tables takes the new entries after its
    // earlier ones (TOML 1.0, Array of Tables: each [[name]] appends a table to the array in the order of the file).
    private static void ReadTableInto(TomlReader reader, TomlTable table)
    {
        ITomlMetadataStore? store = reader.Options.MetadataStore;
        TomlPropertiesMetadata? properties = null;
        if (store is not null && (!store.TryGetProperties(table, out properties) || properties is null))
        {
            properties = new TomlPropertiesMetadata();
        }

        reader.Read();
        while (reader.TokenType != TomlTokenType.EndTable)
        {
            if (reader.TokenType != TomlTokenType.PropertyName)
            {
                throw reader.CreateException($"Expected a key but found {reader.TokenType}.");
            }

            string name = reader.PropertyName!;
            TomlSourceSpan? span = reader.CurrentSpan;
            reader.Read();
            if (properties is not null && span is { } located)
            {
                properties.SetProperty(name, new TomlPropertyMetadata
                {
                    Span = new SourceSpan(
                        located.SourceName,
                        new TextPosition(located.Start.Offset, located.Start.Line, located.Start.Column),
                        new TextPosition(located.End.Offset, located.End.Line, located.End.Column)),
                });
            }

            table.TryGetValue(name, out object? existing);
            if (reader.TokenType == TomlTokenType.StartTable && existing is TomlTable existingTable)
            {
                ReadTableInto(reader, existingTable);
                continue;
            }

            // A table below an array of tables, [[a.b]] after [[a]], belongs to the last entry of the array.
            if (reader.TokenType == TomlTokenType.StartTable && existing is TomlTableArray { Count: > 0 } array)
            {
                ReadTableInto(reader, array[^1]);
                continue;
            }

            object value = ReadValue(reader);
            if (value is TomlTableArray entries && existing is TomlTableArray earlierEntries)
            {
                foreach (TomlTable entry in entries)
                {
                    earlierEntries.Add(entry);
                }

                continue;
            }

            table[name] = value;
        }

        reader.Read();
        if (store is not null && properties is not null)
        {
            store.SetProperties(table, properties);
        }
    }

    // A value: a table, an array, or a string, boolean, integer, float or date-time as Tomlyn gives them.
    private static object ReadValue(TomlReader reader)
    {
        return reader.TokenType switch
        {
            TomlTokenType.StartTable => ReadTable(reader),
            TomlTokenType.StartArray => ReadArray(reader),
            TomlTokenType.String => Advance(reader, reader.GetString()),
            TomlTokenType.Boolean => Advance(reader, reader.GetBoolean()),
            TomlTokenType.Integer => Advance(reader, reader.GetInt64()),
            TomlTokenType.Float => Advance(reader, reader.GetDouble()),
            TomlTokenType.DateTime => Advance(reader, reader.GetTomlDateTime()),
            _ => throw reader.CreateException($"Expected a value but found {reader.TokenType}."),
        };
    }

    // A scalar is one token: the reader moves past it once its value is taken.
    private static object Advance(TomlReader reader, object value)
    {
        reader.Read();
        return value;
    }

    // An array: the entries of [[name]] headers, whose tables have no span, or an array value, [1, 2] or [{ ... }].
    private static TomlObject ReadArray(TomlReader reader)
    {
        reader.Read();
        if (reader.TokenType == TomlTokenType.StartTable && reader.CurrentSpan is null)
        {
            var entries = new TomlTableArray();
            while (reader.TokenType != TomlTokenType.EndArray)
            {
                entries.Add(ReadTable(reader));
            }

            reader.Read();
            return entries;
        }

        var array = new TomlArray();
        while (reader.TokenType != TomlTokenType.EndArray)
        {
            array.Add(ReadValue(reader));
        }

        reader.Read();
        return array;
    }
}
