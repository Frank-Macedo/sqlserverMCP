using System.ComponentModel;
using System.Globalization;
using System.Text;
using ModelContextProtocol.Server;
using SqlServerMcp.Models;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public sealed class DataTools(SqlServerService sqlServerService)
{
    [McpServerTool(Name = "sample_data"), Description("Returns a sample of rows from a table. The result is limited to the configured maximum (default 100).")]
    public async Task<string> SampleData(
        [Description("Database name")] string database,
        [Description("Table name. Use schema.table when needed, for example userNewPoint.PESSOA")] string table,
        [Description("Maximum number of rows to return")] int limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = DataAnalysisHelper.ClampSampleLimit(limit, sqlServerService.MaxSampleRows);
        var rows = await sqlServerService.SampleDataAsync(database, table, limit, cancellationToken);

        if (rows.Count == 0)
        {
            return $"No rows found in table '{table}' for database '{database}'.";
        }

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Database: {database}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Table: {table}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Requested limit: {limit}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Applied limit: {effectiveLimit}");
        builder.AppendLine();

        var columns = rows[0].Keys.ToList();
        builder.AppendLine(string.Join(" | ", columns));
        builder.AppendLine(string.Join(" | ", columns.Select(_ => "----")));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(" | ", columns.Select(column => FormatValue(row.GetValueOrDefault(column)))));
        }

        builder.AppendLine();
        builder.Append(CultureInfo.InvariantCulture, $"Returned rows: {rows.Count}");
        return builder.ToString();
    }

    [McpServerTool(Name = "row_count"), Description("Returns the total number of rows in a table using COUNT_BIG.")]
    public async Task<string> RowCount(
        [Description("Database name")] string database,
        [Description("Table name. Use schema.table when needed, for example userNewPoint.PESSOA")] string table,
        CancellationToken cancellationToken)
    {
        var count = await sqlServerService.RowCountAsync(database, table, cancellationToken);
        return $"Database: {database}\nTable: {table}\nRow count: {count.ToString(CultureInfo.InvariantCulture)}";
    }

    [McpServerTool(Name = "get_column_stats"), Description("Returns analysis statistics for a column: total rows, null count, distinct count, and min/max when supported by the column type.")]
    public async Task<string> GetColumnStats(
        [Description("Database name")] string database,
        [Description("Table name. Use schema.table when needed, for example userNewPoint.PESSOA")] string table,
        [Description("Column name")] string column,
        CancellationToken cancellationToken)
    {
        var stats = await sqlServerService.GetColumnStatsAsync(database, table, column, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Database: {database}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Table: {table}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Column: {GetString(stats, "ColumnName")}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Data type: {GetString(stats, "DataType")}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Total rows: {GetString(stats, "TotalRows")}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Null count: {GetString(stats, "NullCount")}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Distinct count: {GetString(stats, "DistinctCount")}");

        if (stats.TryGetValue("MinMaxSupported", out var supported) && supported is true)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Min value: {FormatValue(stats.GetValueOrDefault("MinValue"))}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"Max value: {FormatValue(stats.GetValueOrDefault("MaxValue"))}");
        }
        else
        {
            builder.AppendLine("Min/Max: not calculated for this data type.");
        }

        return builder.ToString();
    }

    private static string GetString(QueryRow row, string columnName)
    {
        return row.TryGetValue(columnName, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
    }

    private static string FormatValue(object? value)
    {
        if (value is null or DBNull)
        {
            return "NULL";
        }

        return value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}
