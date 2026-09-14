using System.ComponentModel;
using System.Globalization;
using System.Text;
using ModelContextProtocol.Server;
using SqlServerMcp.Models;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class DatabaseTools(SqlServerService sqlServerService)
{
    [McpServerTool(Name = "list_databases"), Description("Lists the SQL Server databases the current connection can access.")]
    public async Task<string> ListDatabases(CancellationToken cancellationToken)
    {
        var rows = await sqlServerService.ListDatabasesAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return "No accessible databases found.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Name | DatabaseId | State | CreateDate");
        builder.AppendLine("----- | ---------- | ----- | ----------");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(" | ",
                GetString(row, "Name"),
                GetString(row, "DatabaseId"),
                GetString(row, "State"),
                FormatDate(row.GetValueOrDefault("CreateDate"))));
        }

        builder.AppendLine();
        builder.Append(CultureInfo.InvariantCulture, $"Total: {rows.Count}");
        return builder.ToString();
    }

    [McpServerTool(Name = "list_tables"), Description("Lists tables and views for a database using INFORMATION_SCHEMA.")]
    public async Task<string> ListTables(
        [Description("Database name")] string database,
        CancellationToken cancellationToken)
    {
        var rows = await sqlServerService.ListTablesAsync(database, cancellationToken);
        if (rows.Count == 0)
        {
            return $"No tables found in database '{database}'.";
        }

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Database: {database}");
        builder.AppendLine("Schema | Table | Type");
        builder.AppendLine("------ | ----- | ----");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(" | ",
                GetString(row, "SchemaName"),
                GetString(row, "TableName"),
                GetString(row, "TableType")));
        }

        builder.AppendLine();
        builder.Append(CultureInfo.InvariantCulture, $"Total: {rows.Count}");
        return builder.ToString();
    }

    [McpServerTool(Name = "describe_table"), Description("Describes columns for a table, including type, size, nullability, position, and primary key information.")]
    public async Task<string> DescribeTable(
        [Description("Database name")] string database,
        [Description("Table name. Use schema.table when needed, for example dbo.PESSOA")] string table,
        CancellationToken cancellationToken)
    {
        var rows = await sqlServerService.DescribeTableAsync(database, table, cancellationToken);
        if (rows.Count == 0)
        {
            return $"Table '{table}' was not found in database '{database}'.";
        }

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"{table}");
        builder.AppendLine();

        foreach (var row in rows)
        {
            var columnName = GetString(row, "ColumnName");
            var dataType = FormatDataType(row);
            var nullable = string.Equals(GetString(row, "IsNullable"), "YES", StringComparison.OrdinalIgnoreCase)
                ? "NULL"
                : "NOT NULL";
            var keyInfo = GetString(row, "KeyInfo");
            var keySuffix = string.IsNullOrWhiteSpace(keyInfo) ? string.Empty : $" [{keyInfo}]";

            builder.AppendLine(CultureInfo.InvariantCulture, $"{columnName,-14}{dataType,-14}{nullable}{keySuffix}");
        }

        builder.AppendLine();
        builder.Append(CultureInfo.InvariantCulture, $"Total columns: {rows.Count}");
        return builder.ToString();
    }

    private static string GetString(QueryRow row, string columnName)
    {
        return row.TryGetValue(columnName, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
    }

    private static string FormatDate(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatDataType(QueryRow row)
    {
        var dataType = GetString(row, "DataType");
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return string.Empty;
        }

        if (row.TryGetValue("CharacterMaximumLength", out var charLength) && charLength is not null and not DBNull)
        {
            var length = Convert.ToInt32(charLength, CultureInfo.InvariantCulture);
            var lengthText = length < 0 ? "max" : length.ToString(CultureInfo.InvariantCulture);
            return $"{dataType}({lengthText})";
        }

        if (row.TryGetValue("NumericPrecision", out var precisionValue)
            && precisionValue is not null and not DBNull
            && row.TryGetValue("NumericScale", out var scaleValue)
            && scaleValue is not null and not DBNull)
        {
            var precision = Convert.ToInt32(precisionValue, CultureInfo.InvariantCulture);
            var scale = Convert.ToInt32(scaleValue, CultureInfo.InvariantCulture);
            if (precision > 0)
            {
                return $"{dataType}({precision},{scale})";
            }
        }

        return dataType;
    }
}
