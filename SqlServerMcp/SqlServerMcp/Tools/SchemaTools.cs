using System.ComponentModel;
using System.Globalization;
using System.Text;
using ModelContextProtocol.Server;
using SqlServerMcp.Models;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class SchemaTools(SqlServerService sqlServerService)
{
    [McpServerTool(Name = "find_columns"), Description("Finds columns by name across the database schema. Supports partial matches, for example RAIO matches RAIO_MARCACAO.")]
    public async Task<string> FindColumns(
        [Description("Database name")] string database,
        [Description("Column name or partial name to search for")] string column,
        CancellationToken cancellationToken)
    {
        var rows = await sqlServerService.FindColumnsAsync(database, column, cancellationToken);
        if (rows.Count == 0)
        {
            return $"No columns matching '{column}' were found in database '{database}'.";
        }

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Database: {database}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Search: {column}");
        builder.AppendLine("Schema | Table | Column | Type | Nullable | Position");
        builder.AppendLine("------ | ----- | ------ | ---- | -------- | --------");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(" | ",
                GetString(row, "SchemaName"),
                GetString(row, "TableName"),
                GetString(row, "ColumnName"),
                GetString(row, "DataType"),
                GetString(row, "IsNullable"),
                GetString(row, "OrdinalPosition")));
        }

        builder.AppendLine();
        builder.Append(CultureInfo.InvariantCulture, $"Total: {rows.Count}");
        return builder.ToString();
    }

    [McpServerTool(Name = "find_relationships"), Description("Finds foreign key relationships for a table using SQL Server metadata only. Does not infer relationships from column names.")]
    public async Task<string> FindRelationships(
        [Description("Database name")] string database,
        [Description("Table name. Use schema.table when needed, for example userNewPoint.PESSOA")] string table,
        CancellationToken cancellationToken)
    {
        var rows = await sqlServerService.FindRelationshipsAsync(database, table, cancellationToken);
        if (rows.Count == 0)
        {
            return $"No foreign key relationships were found for table '{table}' in database '{database}'. " +
                   "This MCP only reports relationships defined by foreign keys in SQL Server.";
        }

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Database: {database}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Table: {table}");
        builder.AppendLine();

        foreach (var row in rows)
        {
            var fromSchema = GetString(row, "FromSchema");
            var fromTable = GetString(row, "FromTable");
            var fromColumn = GetString(row, "FromColumn");
            var toSchema = GetString(row, "ToSchema");
            var toTable = GetString(row, "ToTable");
            var toColumn = GetString(row, "ToColumn");
            var foreignKeyName = GetString(row, "ForeignKeyName");

            builder.AppendLine(CultureInfo.InvariantCulture,
                $"{fromSchema}.{fromTable}.{fromColumn} -> {toSchema}.{toTable}.{toColumn} (FK: {foreignKeyName})");
        }

        builder.AppendLine();
        builder.Append(CultureInfo.InvariantCulture, $"Total relationships: {rows.Count}");
        return builder.ToString();
    }

    private static string GetString(QueryRow row, string columnName)
    {
        return row.TryGetValue(columnName, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
    }
}
