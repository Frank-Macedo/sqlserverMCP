using System.Text.RegularExpressions;

namespace SqlServerMcp.Services;

internal static partial class SqlIdentifierHelper
{
    private const int MaxIdentifierLength = 128;

    [GeneratedRegex(@"^[A-Za-z0-9_\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ColumnSearchPattern();

    internal static (string? Schema, string Table) ParseOptionalTableReference(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            throw new ArgumentException("Table name cannot be empty.", nameof(table));
        }

        var trimmed = table.Trim();
        var dotIndex = trimmed.IndexOf('.');
        if (dotIndex < 0)
        {
            ValidateIdentifier(trimmed, nameof(table));
            return (null, trimmed);
        }

        var schema = trimmed[..dotIndex].Trim();
        var tableName = trimmed[(dotIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table reference must be in the format schema.table.", nameof(table));
        }

        ValidateIdentifier(schema, nameof(table));
        ValidateIdentifier(tableName, nameof(table));
        return (schema, tableName);
    }

    internal static void ValidateColumnSearchTerm(string columnSearch, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(columnSearch))
        {
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        }

        if (columnSearch.Length > MaxIdentifierLength)
        {
            throw new ArgumentException($"{parameterName} exceeds the maximum length of {MaxIdentifierLength} characters.", parameterName);
        }

        if (!ColumnSearchPattern().IsMatch(columnSearch))
        {
            throw new ArgumentException(
                $"{parameterName} contains invalid characters. Only letters, digits, and underscore are allowed.",
                parameterName);
        }
    }

    internal static (string Schema, string Table) ParseTableReference(string table, string defaultSchema = "dbo")
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            throw new ArgumentException("Table name cannot be empty.", nameof(table));
        }

        var trimmed = table.Trim();
        var dotIndex = trimmed.IndexOf('.');
        if (dotIndex < 0)
        {
            ValidateIdentifier(trimmed, nameof(table));
            return (defaultSchema, trimmed);
        }

        var schema = trimmed[..dotIndex].Trim();
        var tableName = trimmed[(dotIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table reference must be in the format schema.table.", nameof(table));
        }

        ValidateIdentifier(schema, nameof(table));
        ValidateIdentifier(tableName, nameof(table));
        return (schema, tableName);
    }

    internal static void ValidateIdentifier(string identifier, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        }

        if (identifier.Length > MaxIdentifierLength)
        {
            throw new ArgumentException($"{parameterName} exceeds the maximum length of {MaxIdentifierLength} characters.", parameterName);
        }

        if (!IdentifierPattern().IsMatch(identifier))
        {
            throw new ArgumentException(
                $"{parameterName} contains invalid characters. Only letters, digits, underscore, and hyphen are allowed.",
                parameterName);
        }
    }

    internal static string QuoteIdentifier(string identifier)
    {
        ValidateIdentifier(identifier, "identifier");
        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }
}
